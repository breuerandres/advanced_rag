# RAG Evaluations (RAGAS)

This folder is the v2 quality gate against regressions. See ADR-0010
(`docs/adr/0010-ragas-evals.md`) for the rationale.

## Files

- `golden.jsonl` — hand-curated questions with expected answer + expected document ids.
  20-line starter set; grow toward 100-200 representative cases.
- `ragas_runner.py` — CI entry point. Loads `golden.jsonl`, runs each question through
  the live RAG API, computes RAGAS metrics, compares to `baseline_metrics.json`.
- `baseline_metrics.json` — frozen "good" thresholds. Operator bumps when an intentional
  improvement raises the bar.
- `requirements.txt` — minimal Python deps for the runner.

## Running locally

```bash
cd evals
pip install -r requirements.txt
# Make sure the docker compose stack is up and a tenant is set up.
export RAG_API_URL=http://localhost:8000
export RAG_API_TOKEN=<a valid session bearer or api key>
python ragas_runner.py --golden golden.jsonl --baseline baseline_metrics.json
```

## CI

`.github/workflows/eval.yml` runs the same command on every PR that modifies:

- `services/rag-api/**`
- `services/dotnet-api/**` (indexing logic)
- `evals/**`
- `services/rag-api/src/advanced_rag/rag/prompts/**`

The job spins up the compose stack, runs the runner, and fails the PR if any metric
drops more than 5% vs the baseline.

## Adding cases

Each line in `golden.jsonl` is:

```json
{
  "id": "q001",
  "question": "How do I invoice VAT on an export operation?",
  "expected_answer": "Open ABR522, …",
  "expected_doc_ids": ["d-uuid-1", "d-uuid-2"],
  "dimensions": { "product": ["DUX3"], "module": ["ABR522"] },
  "language": "es-AR",
  "tags": ["billing", "exports"]
}
```

Guidelines:

- 80% golden-path questions that should clearly succeed, 20% adversarial (ambiguous,
  multi-document, edge cases).
- Mix languages once multilingual content is indexed.
- Keep `expected_doc_ids` to the minimum set that materially answers; reranker may pick
  others legitimately.
- Tag with `regression` when the case was added to lock in a fix; CI summary highlights
  regressions specially.
