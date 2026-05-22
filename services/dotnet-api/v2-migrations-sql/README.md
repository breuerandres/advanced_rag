# V2 EF Core migrations — SQL scripts (pending materialisation)

These SQL files are the v2 `app` schema additions. They are written as raw SQL so that
the next PC can:

1. Run `dotnet ef migrations add V2_<Name> --context AppDbContext` (or `--project ...`)
   on an environment with `dotnet-ef` installed (the MVP locks `dotnet-ef` 8.0.27).
2. Open the generated `<timestamp>_V2_<Name>.cs` file.
3. Replace the auto-generated `Up()` body with:

   ```csharp
   migrationBuilder.Sql(File.ReadAllText(
       Path.Combine(AppContext.BaseDirectory, "..", "..", "v2-migrations-sql", "<N>_<name>.up.sql")));
   ```

   And `Down()` similarly with the `*.down.sql` file. Or simply paste the SQL inline.
4. Save and run `dotnet ef migrations script` or `dotnet ef database update` to apply.

Why not commit ready-to-apply `.cs` migrations? Each EF migration has a sibling
`.Designer.cs` snapshot of the entire model that the tooling auto-generates. Authoring
those snapshots by hand is error-prone and brittle. The SQL is the durable truth; the
Designer files are derived.

## Files (apply in numeric order)

| # | File | Purpose | ADR |
|---|---|---|---|
| 001 | `001_add_tenant_config.up.sql` / `.down.sql` | Singleton `app.tenant_config` for brand + provider config | 0001/0007 |
| 002 | `002_add_user_role_column.up.sql` / `.down.sql` | Promote role from `app.user_roles` join to first-class column | 0006 |
| 003 | `003_add_dimensions.up.sql` / `.down.sql` | `app.dimensions`, `app.dimension_values`, M:N table | 0008 |
| 004 | `004_add_document_views.up.sql` / `.down.sql` | View tracking for analytics | — |
| 005 | `005_add_document_reactions.up.sql` / `.down.sql` | Per-user ±1 reactions per document | — |
| 006 | `006_add_document_favorites.up.sql` / `.down.sql` | Per-user favourites | — |
| 007 | `007_add_api_keys.up.sql` / `.down.sql` | Scoped + rate-limited API keys | — |
| 008 | `008_add_webhooks.up.sql` / `.down.sql` | Outgoing webhook subscriptions | — |
| 009 | `009_add_document_language_summary.up.sql` / `.down.sql` | Adds `language`, `summary`, `external_key` to documents | 0003 |
| 010 | `010_add_document_versions_markdown.up.sql` / `.down.sql` | Adds `content_format` and `content_markdown` to versions | — |
| 011 | `011_drop_viewer_exchange_codes.up.sql` / `.down.sql` | Removes deprecated viewer exchange code flow | 0006 |
| 012 | `012_add_mv_document_metrics.up.sql` / `.down.sql` | Materialised view aggregating views/reactions/favourites/citations | — |
| 013 | `013_grant_v2_app_reads_to_rag_owner.up.sql` / `.down.sql` | Extend `rag_owner` SELECT to new app tables it reads | — |

## Notes

- Apply in order. Some scripts reference tables created earlier.
- Each script is idempotent at the `IF NOT EXISTS` / `IF EXISTS` level where possible.
- The `app_reporting_reader` role and `rag_owner` role are created by `postgres-init`
  (see `infra/compose/postgres-init/`). These scripts only add grants on new tables.
- For details about what each table is for, see the matching ADR or `docs/v2/02-target-architecture.md`.
