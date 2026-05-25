"""CI entry point for RAG evals.

Loads `golden.jsonl`, calls the live RAG API for each question, builds a RAGAS dataset,
and computes the four canonical metrics. Compares against `baseline_metrics.json` and
exits non-zero if any metric drops more than `regression_tolerance`.

Usage:
    python ragas_runner.py --golden golden.jsonl --baseline baseline_metrics.json

Environment:
    RAG_API_URL          base URL of the deployed FastAPI service (e.g. http://localhost:8000)
    RAG_API_TOKEN        valid session/JWT or API key for chat requests
    OPENAI_API_KEY       used by RAGAS as the evaluator LLM
    RAGAS_EVALUATOR_MODEL  default 'gpt-4o-mini'

See evals/README.md.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
import sys
from collections.abc import Iterable
from pathlib import Path
from typing import Any

import httpx


async def query_rag(client: httpx.AsyncClient, base_url: str, token: str, payload: dict) -> dict:
    """POST to /api/v1/chat and accumulate the SSE stream into a final answer + citations."""
    headers = {
        "Authorization": f"Bearer {token}",
        "Accept": "text/event-stream",
    }
    answer_parts: list[str] = []
    citations: list[dict] = []

    async with client.stream(
        "POST",
        f"{base_url}/api/v1/chat",
        json=payload,
        headers=headers,
        timeout=120.0,
    ) as response:
        response.raise_for_status()
        current_event: str | None = None
        async for line in response.aiter_lines():
            if not line:
                current_event = None
                continue
            if line.startswith("event:"):
                current_event = line.split(":", 1)[1].strip()
                continue
            if line.startswith("data:"):
                data = line.split(":", 1)[1].strip()
                if not data:
                    continue
                if current_event == "answer-token":
                    try:
                        answer_parts.append(json.loads(data).get("delta", ""))
                    except json.JSONDecodeError:
                        pass
                elif current_event == "citations":
                    try:
                        payload_data = json.loads(data)
                        citations = payload_data.get("citations", [])
                    except json.JSONDecodeError:
                        pass

    return {
        "answer": "".join(answer_parts),
        "citations": citations,
    }


def load_golden(path: Path) -> list[dict]:
    items: list[dict] = []
    with path.open(encoding="utf-8") as fp:
        for line_num, line in enumerate(fp, 1):
            line = line.strip()
            if not line or line.startswith("//"):
                continue
            try:
                items.append(json.loads(line))
            except json.JSONDecodeError as exc:
                raise SystemExit(
                    f"Invalid JSON in {path} at line {line_num}: {exc}"
                ) from exc
    return items


def compute_metrics(samples: Iterable[dict]) -> dict[str, float]:
    """Compute RAGAS metrics over the collected samples.

    Imported lazily because ragas pulls in heavy deps.
    """
    from datasets import Dataset
    from ragas import evaluate
    from ragas.metrics import (
        answer_relevancy,
        context_precision,
        context_recall,
        faithfulness,
    )

    ds = Dataset.from_list(list(samples))
    result = evaluate(
        ds,
        metrics=[faithfulness, answer_relevancy, context_precision, context_recall],
    )
    return {k: float(v) for k, v in result.items()}


def compare_with_baseline(
    metrics: dict[str, float], baseline_path: Path
) -> tuple[bool, list[str]]:
    """Return (passed, messages)."""
    baseline = json.loads(baseline_path.read_text(encoding="utf-8"))
    tolerance = float(baseline.get("regression_tolerance", 0.05))
    base_metrics: dict[str, float] = baseline["metrics"]

    messages: list[str] = []
    passed = True

    for name, base_value in base_metrics.items():
        if name not in metrics:
            messages.append(f"missing metric: {name}")
            passed = False
            continue
        current = metrics[name]
        drop = base_value - current
        rel_drop = drop / base_value if base_value > 0 else 0.0
        marker = "OK" if rel_drop <= tolerance else "FAIL"
        if marker == "FAIL":
            passed = False
        messages.append(
            f"  {name:>20}: current={current:.3f}  baseline={base_value:.3f}  "
            f"drop={drop:+.3f} ({rel_drop:+.1%})  [{marker}]"
        )

    return passed, messages


async def main_async(args: argparse.Namespace) -> int:
    base_url = os.environ.get("RAG_API_URL")
    token = os.environ.get("RAG_API_TOKEN")
    if not base_url or not token:
        print("RAG_API_URL and RAG_API_TOKEN environment variables are required.", file=sys.stderr)
        return 2

    golden_path = Path(args.golden)
    baseline_path = Path(args.baseline)
    if not golden_path.exists():
        print(f"Golden file not found: {golden_path}", file=sys.stderr)
        return 2
    if not baseline_path.exists():
        print(f"Baseline file not found: {baseline_path}", file=sys.stderr)
        return 2

    items = load_golden(golden_path)
    print(f"Loaded {len(items)} golden cases from {golden_path}.")

    samples: list[dict[str, Any]] = []
    async with httpx.AsyncClient() as client:
        for index, item in enumerate(items, 1):
            payload = {
                "question": item["question"],
                "filters": {"dimensions": item.get("dimensions", {})},
            }
            print(f"[{index:>3}/{len(items)}] {item['id']}: {item['question'][:60]}…")
            try:
                response = await query_rag(client, base_url, token, payload)
            except Exception as exc:
                print(f"  request failed: {exc}", file=sys.stderr)
                continue

            samples.append(
                {
                    "question": item["question"],
                    "answer": response["answer"],
                    "contexts": [c.get("text_quote") or c.get("content", "") for c in response["citations"]],
                    "ground_truth": item.get("expected_answer", ""),
                }
            )

    if not samples:
        print("No samples collected; cannot evaluate.", file=sys.stderr)
        return 3

    print("\nComputing RAGAS metrics…")
    metrics = compute_metrics(samples)

    print("\nResults:")
    for name, value in metrics.items():
        print(f"  {name:>20}: {value:.3f}")

    passed, messages = compare_with_baseline(metrics, baseline_path)
    print("\nVs baseline:")
    for line in messages:
        print(line)

    return 0 if passed else 1


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--golden", default="golden.jsonl")
    parser.add_argument("--baseline", default="baseline_metrics.json")
    args = parser.parse_args()
    return asyncio.run(main_async(args))


if __name__ == "__main__":
    sys.exit(main())
