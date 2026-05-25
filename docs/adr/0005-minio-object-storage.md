# ADR-0005 — MinIO as default S3-compatible object storage

**Status**: Accepted (v2 design phase, 2026-05-22)
**Phase**: 3

## Context

The MVP embeds images as base64 in HTML. This is wrong at scale: it bloats every chunk,
slows the chunker's tokeniser, blows up the embedding cost, and makes responses heavy.

We need a self-hosted, S3-compatible store that:

- Runs in Docker Compose without external dependencies.
- Free / commercially permissive license.
- Speaks the S3 API so the abstraction can target a hosted S3 in customers that prefer.

## Decision

Ship **MinIO** as the default object store in `infra/compose/compose.yaml`. Document
**Garage** and **SeaweedFS** as drop-in alternatives.

Bucket layout:

```
helpcenter/
├── documents/<doc_id>/images/<uuid>.webp
├── documents/<doc_id>/imports/<original-name>.{pdf,docx}      (admin-flag retain)
├── tenant/brand/logo.{png,webp,svg}
└── tenant/brand/favicon.png
```

Access:
- `tenant/brand/*` public read (logo/favicon).
- `documents/*` require signed URL (5-minute default TTL) obtained from .NET.

The application code consumes an `IObjectStorage` abstraction so the operator can swap
MinIO for AWS S3, Cloudflare R2, or Garage by changing config.

## Comparative analysis

| Aspect | MinIO | Garage | SeaweedFS |
|---|---|---|---|
| License | AGPLv3 | Apache 2.0 | Apache 2.0 |
| S3 compatibility | 100% | ~95% | ~90% |
| Binary size | ~80 MB | ~20 MB | ~30 MB |
| Memory minimum | ~256 MB | ~100 MB | ~150 MB |
| Web console | Yes (commercial) / mc CLI free | Yes (basic) | Yes |
| Replication | Yes | Yes (geo built-in) | Yes |
| Maturity | Very mature, large user base | Newer (Deuxfleurs, 2023) | Mature |
| Community | Huge | Small but active | Large |
| AGPL risk for our distribution | Negligible — customers install their own MinIO from upstream | None | None |

We chose MinIO because:

- Compatibility is perfect; the abstraction's S3 SDK path is the same as production AWS.
- Ecosystem (mc CLI, web console, integration patterns) is well documented.
- Customers download MinIO themselves from MinIO's upstream registry; we never bundle or
  redistribute the binary, so the AGPL copyleft does not propagate to our code.

## Alternatives considered

### Filesystem on a Docker volume (no S3 layer)
Pros: Simplest.
Cons: No content-addressed retrieval, no presigned URLs, harder to scale, can't be swapped
for AWS S3 without rewriting.

### Cloudflare R2 / AWS S3 only (no self-hosted option)
Pros: Zero infra to operate.
Cons: Defeats "self-hosted by company" pillar. Some customers can't use cloud storage.

### LakeFS / Ceph
Pros: Powerful.
Cons: Significantly more operational complexity than necessary.

## Consequences

**Positive**
- Images out of the DB → faster chunker, cheaper embeddings, smaller responses.
- Signed URL pattern enforces per-document permission re-check.
- Operator can switch to AWS S3 / R2 by changing `tenant_config.s3_endpoint`.

**Negative**
- One more container in compose (MinIO + healthcheck).
- New abstraction layer in both .NET and Python.
- Signed URL TTL adds slight latency at viewer-image-load time.

**Risks / mitigations**
- MinIO upstream changes (license, distribution) → ship Garage compose overlay file as
  ready-to-swap alternative.
- Bucket misconfiguration leaks docs → init script creates buckets with private ACL by
  default; only `tenant/brand/` is made public.
- Signed URLs expire mid-page-load on slow networks → 5-minute TTL is generous; viewer
  refetches on 403.

## References

- `infra/compose/compose.yaml` (minio service)
- `infra/compose/minio/init-bucket.sh`
- MinIO licensing: minio.io/legal/license
- Garage: garagehq.deuxfleurs.fr
