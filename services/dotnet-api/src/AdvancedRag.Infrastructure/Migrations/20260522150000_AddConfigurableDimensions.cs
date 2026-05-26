using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260522150000_AddConfigurableDimensions")]
public partial class AddConfigurableDimensions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            create table if not exists app.dimensions (
                id              uuid primary key default gen_random_uuid(),
                key             text not null,
                label           text not null,
                label_i18n      jsonb not null default '{}'::jsonb,
                hierarchical    boolean not null default false,
                required        boolean not null default false,
                display_order   int not null default 0,
                created_at      timestamptz not null default now(),
                updated_at      timestamptz not null default now(),
                deleted_at      timestamptz,
                constraint uq_dimensions_key unique (key)
            );

            create index if not exists ix_dimensions_display_order
                on app.dimensions (display_order)
                where deleted_at is null;

            create table if not exists app.dimension_values (
                id              uuid primary key default gen_random_uuid(),
                dimension_id    uuid not null references app.dimensions(id) on delete cascade,
                parent_id       uuid references app.dimension_values(id) on delete restrict,
                external_key    text,
                label           text not null,
                label_i18n      jsonb not null default '{}'::jsonb,
                description     text,
                color           text,
                icon            text,
                display_order   int not null default 0,
                created_at      timestamptz not null default now(),
                updated_at      timestamptz not null default now(),
                deleted_at      timestamptz,
                constraint uq_dimension_values_external unique (dimension_id, external_key)
            );

            create index if not exists ix_dimension_values_dimension
                on app.dimension_values (dimension_id)
                where deleted_at is null;

            create index if not exists ix_dimension_values_parent
                on app.dimension_values (parent_id)
                where parent_id is not null and deleted_at is null;

            create index if not exists ix_dimension_values_display_order
                on app.dimension_values (dimension_id, display_order)
                where deleted_at is null;

            do $$
            begin
                if to_regclass('app.documents') is not null then
                    create table if not exists app.document_dimension_values (
                        document_id         uuid not null references app.documents("Id") on delete cascade,
                        dimension_value_id  uuid not null references app.dimension_values(id) on delete cascade,
                        primary key (document_id, dimension_value_id)
                    );

                    create index if not exists ix_ddv_dimension_value
                        on app.document_dimension_values (dimension_value_id);

                    create index if not exists ix_ddv_document
                        on app.document_dimension_values (document_id);
                end if;
            end $$;

            do $$
            begin
                if to_regrole('rag_owner') is not null then
                    grant select on table app.dimensions to rag_owner;
                    grant select on table app.dimension_values to rag_owner;
                    if to_regclass('app.document_dimension_values') is not null then
                        grant select on table app.document_dimension_values to rag_owner;
                    end if;
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
                if to_regrole('rag_owner') is not null then
                    if to_regclass('app.document_dimension_values') is not null then
                        revoke select on table app.document_dimension_values from rag_owner;
                    end if;

                    if to_regclass('app.dimension_values') is not null then
                        revoke select on table app.dimension_values from rag_owner;
                    end if;

                    if to_regclass('app.dimensions') is not null then
                        revoke select on table app.dimensions from rag_owner;
                    end if;
                end if;
            end $$;

            drop table if exists app.document_dimension_values;
            drop table if exists app.dimension_values;
            drop table if exists app.dimensions;
            """);
    }
}
