using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260614223958_AddDocumentTypeCatalog")]
public partial class AddDocumentTypeCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            create table if not exists app.document_types (
                "Id" uuid primary key,
                name character varying(80) not null,
                is_active boolean not null default true,
                sort_order integer not null default 0,
                created_at timestamp with time zone not null default now(),
                updated_at timestamp with time zone not null default now()
            );

            create unique index if not exists "IX_document_types_name"
                on app.document_types (name);

            insert into app.document_types ("Id", name, is_active, sort_order, created_at, updated_at)
            values
                ('20000000-0000-0000-0000-000000000001', 'Artículo', true, 0, now(), now()),
                ('20000000-0000-0000-0000-000000000002', 'Instructivo', true, 1, now(), now()),
                ('20000000-0000-0000-0000-000000000003', 'Procedimiento', true, 2, now(), now())
            on conflict ("Id") do nothing;

            do $$
            begin
                if to_regclass('app.document_versions') is null then
                    return;
                end if;

                alter table app.document_versions
                    add column if not exists document_type_id uuid null;

                create index if not exists "IX_document_versions_document_type_id"
                    on app.document_versions (document_type_id);

                if not exists (
                    select 1 from pg_constraint
                    where conname = 'FK_document_versions_document_types_document_type_id'
                      and conrelid = 'app.document_versions'::regclass
                ) then
                    alter table app.document_versions
                        add constraint "FK_document_versions_document_types_document_type_id"
                        foreign key (document_type_id)
                        references app.document_types("Id")
                        on delete restrict;
                end if;

                -- Backfill: preserve every distinct existing free-text type as a catalog
                -- entry (one per case-insensitive value), then map versions to it. Empty
                -- or whitespace values stay null. This keeps the FK nullable and ensures
                -- the migration cannot fail on legacy data.
                if exists (
                    select 1 from information_schema.columns
                    where table_schema = 'app'
                      and table_name = 'document_versions'
                      and column_name = 'document_type'
                ) then
                    insert into app.document_types ("Id", name, is_active, sort_order, created_at, updated_at)
                    select gen_random_uuid(), src.name, true, 100, now(), now()
                    from (
                        select distinct on (lower(btrim(dv.document_type))) btrim(dv.document_type) as name
                        from app.document_versions dv
                        where btrim(coalesce(dv.document_type, '')) <> ''
                        order by lower(btrim(dv.document_type))
                    ) src
                    where not exists (
                        select 1 from app.document_types dt
                        where lower(dt.name) = lower(src.name)
                    );

                    update app.document_versions dv
                    set document_type_id = dt."Id"
                    from app.document_types dt
                    where dv.document_type_id is null
                      and btrim(coalesce(dv.document_type, '')) <> ''
                      and lower(dt.name) = lower(btrim(dv.document_type));

                    alter table app.document_versions drop column if exists document_type;
                end if;
            end $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
                if to_regclass('app.document_versions') is not null then
                    alter table app.document_versions
                        add column if not exists document_type character varying(80) not null default '';

                    update app.document_versions dv
                    set document_type = dt.name
                    from app.document_types dt
                    where dv.document_type_id = dt."Id";

                    alter table app.document_versions
                        drop constraint if exists "FK_document_versions_document_types_document_type_id";
                    drop index if exists app."IX_document_versions_document_type_id";
                    alter table app.document_versions
                        drop column if exists document_type_id;
                end if;

                drop table if exists app.document_types;
            end $$;
            """);
    }
}
