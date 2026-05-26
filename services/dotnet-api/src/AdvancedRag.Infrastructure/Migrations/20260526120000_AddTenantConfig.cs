using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260526120000_AddTenantConfig")]
public partial class AddTenantConfig : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            create table if not exists app.tenant_config (
                id                              uuid primary key default gen_random_uuid(),
                brand_name                      text not null default 'Help Center',
                brand_logo_url                  text,
                brand_favicon_url               text,
                primary_color                   text not null default '#2563eb',
                default_locale                  text not null default 'es-AR',
                supported_locales               text[] not null default array['es-AR']::text[],
                llm_provider                    text not null default 'openai',
                llm_model                       text not null default 'gpt-4o-mini',
                llm_base_url                    text,
                embedding_provider              text not null default 'openai',
                embedding_model                 text not null default 'text-embedding-3-large',
                embedding_dimensions            int not null default 1024,
                reranker_provider               text not null default 'tei-bge',
                reranker_model                  text not null default 'BAAI/bge-reranker-v2-m3',
                reranker_base_url               text,
                enable_bm25                     boolean not null default true,
                enable_reranker                 boolean not null default true,
                enable_conversational_memory    boolean not null default true,
                enable_query_rewrite            boolean not null default false,
                rag_top_k_vector                int not null default 20,
                rag_top_k_bm25                  int not null default 20,
                rag_top_k_final                 int not null default 8,
                rrf_k                           int not null default 60,
                conversation_history_turns      int not null default 5,
                cache_ttl_hours                 int not null default 24,
                cache_similarity_threshold      numeric(4, 3) not null default 0.90,
                default_monthly_budget_usd      numeric(8, 2) not null default 5.00,
                global_daily_budget_usd         numeric(10, 2),
                enable_vlm_image_description    boolean not null default false,
                enable_otel                     boolean not null default false,
                s3_endpoint                     text,
                s3_bucket                       text not null default 'helpcenter',
                s3_region                       text not null default 'us-east-1',
                created_at                      timestamptz not null default now(),
                updated_at                      timestamptz not null default now()
            );

            create unique index if not exists ux_tenant_config_singleton
                on app.tenant_config ((1));

            insert into app.tenant_config (id)
                select gen_random_uuid()
                where not exists (select 1 from app.tenant_config);

            create or replace function app.f_tenant_config_touch()
                returns trigger as $$
            begin
                new.updated_at := now();
                return new;
            end;
            $$ language plpgsql;

            drop trigger if exists tg_tenant_config_touch on app.tenant_config;

            create trigger tg_tenant_config_touch
                before update on app.tenant_config
                for each row
                execute function app.f_tenant_config_touch();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            drop trigger if exists tg_tenant_config_touch on app.tenant_config;
            drop function if exists app.f_tenant_config_touch();
            drop table if exists app.tenant_config;
            """);
    }
}
