# ADR-0010 — Continuous evals with RAGAS + golden set + CI gate

**Status**: Accepted (v2 design phase, 2026-05-22)
**Phase**: 5

## Context

The MVP has no automated way to detect a regression in RAG quality. Tweaks to the prompt,
chunker, retriever, or model can silently degrade answers. Manual evaluation does not
scale.

We need:

- A small, hand-curated **golden set** of representative questions with expected answers.
- An **automated runner** that scores the current build against the golden set.
- A **CI gate** that fails when scores degrade beyond a threshold.

## Decision

Adopt **RAGAS** (github.com/explodinggradients/ragas) as the eval framework. Add:

```
evals/
├── golden.jsonl                  20-100 hand-curated Q&A entries
├── ragas_runner.py               loads golden set, runs queries, calls RAGAS metrics
├── README.md                     how to add cases
└── baseline_metrics.json         current "good" thresholds for comparison
.github/workflows/eval.yml        triggered on PRs that touch rag/** or prompts/**
```

### Metrics tracked

- `faithfulness` — does the answer hallucinate? (Target ≥ 0.85)
- `answer_relevancy` — does it address the question? (Target ≥ 0.80)
- `context_precision` — are retrieved chunks relevant? (Target ≥ 0.75)
- `context_recall` — do retrieved chunks cover the expected answer? (Target ≥ 0.70)

CI gate: fail the PR if any metric drops > 5% vs `baseline_metrics.json`. Operator
explicitly bumps the baseline when an intentional improvement raises the bar.

### Golden set format

```jsonl
{"id": "q001", "question": "Cómo facturo IVA?", "expected_answer": "...", "expected_doc_ids": ["d-123"], "dimensions": {"product": ["DUX3"]}, "language": "es-AR"}
```

## Alternatives considered

### promptfoo
Pros: Simple, YAML-based test cases.
Cons: General-purpose; RAGAS has RAG-specific metrics out of the box.

### Phoenix / Arize evals
Pros: Visual UI for inspecting traces.
Cons: SaaS-tilted, overkill for the in-CI use case. Useful in production observability;
not for the regression gate.

### LangSmith
Pros: Mature.
Cons: Vendor lock-in; the v2 product is generic and shouldn't tie evals to LangChain
infrastructure.

### Custom eval scripts only
Pros: No new dep.
Cons: Reinvents the wheel; RAGAS metrics are research-backed.

## Consequences

**Positive**
- Confident iteration on prompts, chunker params, models.
- Visible quality trend over time.
- New contributors can see if their PR helped or hurt.

**Negative**
- Golden set maintenance (questions, expected answers, dimension tags must stay current
  as the corpus evolves). Document the workflow for adding/retiring cases.
- RAGAS itself needs a real LLM to compute metrics → eval CI run costs LLM calls.
  Mitigated by running on a tiny golden set (20–50 cases) and using a cheap evaluator
  model.

**Risks / mitigations**
- "Metrics gaming" — tuning to the golden set instead of broader generalisation →
  diversify the set, rotate cases periodically, keep some hidden.
- Eval-time provider drift (the model used to evaluate changes its outputs) → pin a
  specific evaluator model version in `evals/ragas_runner.py`.

## References

- `evals/ragas_runner.py`
- RAGAS docs (explodinggradients.com/ragas)
- Open Question OQ-002 (default embedding affects what gets evaluated)
