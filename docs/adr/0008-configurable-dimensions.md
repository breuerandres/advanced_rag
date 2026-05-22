# ADR-0008 — Configurable dimensions for document categorisation

**Status**: Accepted (v2 design phase, 2026-05-22)
**Phase**: 2

## Context

The legacy CentroDeAyuda (Mymtec's existing in-house help center) categorises articles by
three flat lists: `programa` (e.g. `IMA001`), `producto`, `area`. The MVP has none of
that — only tags via `document_tags` and group-based permissions.

The v2 product is generic. Each customer has its own categorisation taxonomy:

- A SaaS company might categorise by `product`, `feature`, `audience`.
- A bank might categorise by `process`, `department`, `compliance-tier`.
- A logistics company might categorise by `route-type`, `country`, `service`.

Hard-coding "module / product / area" would betray the product's genericity. Letting
customers define their own dimensions makes the data model and the UI work for all of
them.

## Decision

Introduce a generic dimensions model. A customer's admin defines any number of
**dimensions** (e.g. `Product`, `Department`, `Tier`), each with a set of **values**, and
optionally a hierarchical parent/child relationship.

Schema:

```sql
CREATE TABLE app.dimensions (
  id              UUID PRIMARY KEY,
  key             TEXT NOT NULL UNIQUE,        -- 'product', 'department'
  label           TEXT NOT NULL,                -- 'Producto'
  label_i18n      JSONB DEFAULT '{}'::jsonb,    -- { "en-US": "Product", ... }
  hierarchical    BOOLEAN NOT NULL DEFAULT false,
  required        BOOLEAN NOT NULL DEFAULT false,
  display_order   INT NOT NULL DEFAULT 0
);

CREATE TABLE app.dimension_values (
  id              UUID PRIMARY KEY,
  dimension_id    UUID NOT NULL REFERENCES app.dimensions(id),
  parent_id       UUID REFERENCES app.dimension_values(id),
  external_key    TEXT,                         -- 'IMA001' for customer-friendly URLs
  label           TEXT NOT NULL,
  label_i18n      JSONB,
  description     TEXT,
  color           TEXT,
  icon            TEXT,
  display_order   INT NOT NULL DEFAULT 0,
  UNIQUE (dimension_id, external_key)
);

CREATE TABLE app.document_dimension_values (
  document_id           UUID,
  dimension_value_id    UUID,
  PRIMARY KEY (document_id, dimension_value_id)
);
```

A document can belong to multiple dimensions and multiple values per dimension (M:N). The
chat-web filter UI shows one chip group per dimension.

## Alternatives considered

### Free tags (only)
Pros: Simplest; one M:N table.
Cons: No structure; tag explosion; no required dimensions; no labels per language;
hierarchies impossible. User explicitly preferred structured dimensions.

### Hierarchical "spaces" (Notion/Confluence-style)
Pros: Familiar mental model.
Cons: One hierarchy per doc means the doc belongs to a single place; the legacy
CentroDeAyuda needed multi-dimensional categorisation (one article tagged with 3
programs + 2 products).

### Hard-coded `module`, `product`, `area` columns
Pros: Less indirection.
Cons: Bakes Mymtec's vocabulary into the product. Other customers' admins would have to
"misuse" the columns.

## Consequences

**Positive**
- Each customer models their world cleanly.
- Hierarchy support allows "Module > Sub-module" if they want.
- i18n labels stored per dimension value (multilingual UI for filter chips).
- Chat deep-linking is natural: `?dim_product=DUX3&dim_module=IMA001`.
- Permissions can extend to "viewer role X can only see docs with dimension value Y" in
  Phase 5 (see `docs/v2/03-phases.md` Phase 5.8 if added later).

**Negative**
- More tables; more setup work for the admin.
- Setup wizard must walk the admin through "define your dimensions" with examples.
- Migration of MVP docs (none in greenfield) is N/A; data-rich customers would need a CSV
  importer to bulk-assign.

**Risks / mitigations**
- Admins over-engineer dimensions (15 dimensions, none with meaningful values) → setup
  wizard suggests 1–3 dimensions, gives industry examples.
- Filter chip UI clutter with many dimensions → collapse infrequent dimensions into a
  "More filters" drawer.

## References

- Legacy CentroDeAyuda schema (`articulosprogramas`, `articulosproductos`, `areasprogramas`)
  for the inspiration of the multi-dimensional case
- `services/dotnet-api/...Migrations/...AddDimensions.cs`
- chat-web filter chips in `apps/chat-web/src/features/chat/FilterChips.tsx`
