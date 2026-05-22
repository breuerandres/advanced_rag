# Observability stack (opt-in)

This folder holds configuration files for the optional Grafana stack overlay that ships
with the v2 refactor. Bring it up with:

```bash
docker compose \
    -f compose.yaml \
    -f compose.observability.yaml \
    --profile observability \
    up -d
```

## Components

| Service | Purpose | Config |
|---|---|---|
| `otel-collector` | Receives OTLP traces, metrics, logs from .NET and FastAPI | `otel-collector.yaml` |
| `tempo` | Traces storage and query | `tempo.yaml` |
| `prometheus` | Metrics scraping + storage | `prometheus.yml` |
| `loki` | Log aggregation | `loki.yaml` |
| `grafana` | UI for all three | `grafana-datasources.yaml`, `grafana-dashboards/*.json` |

## Wiring services to emit OTel

The .NET and FastAPI containers must export to OTLP. Add these env vars in the main
compose file (or via `.env`) when the observability profile is on:

```env
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
OTEL_SERVICE_NAME=dotnet-api   # or rag-api
OTEL_RESOURCE_ATTRIBUTES=deployment.environment=prod,tenant=acme
```

Both services need the OTel SDK installed:

- .NET: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`,
  `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`.
- FastAPI: `opentelemetry-distro`, `opentelemetry-exporter-otlp`,
  `opentelemetry-instrumentation-fastapi`, `opentelemetry-instrumentation-sqlalchemy`.

## Dashboards

Initial dashboards live under `grafana-dashboards/`. Recommended set (to be authored as
the system runs in production):

- `rag-pipeline.json` — chat p50/p95/p99 latency by stage (embed, retrieve, rerank,
  generate), cost USD per minute, cache hit rate.
- `provider-health.json` — error rate and latency per LLM/embedding/reranker provider.
- `documents.json` — indexing job throughput, failures, retry rate.
- `auth.json` — login attempts, rate-limit hits, csrf failures.

Placeholders for these files are TBD on the next PC; ship the stack first, dashboards
next.
