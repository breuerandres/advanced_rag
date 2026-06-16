-- v2 migration 001: tenant_config singleton
-- See docs/adr/0001-multi-provider-llm.md, docs/adr/0007-shared-ui-design-system.md

CREATE TABLE IF NOT EXISTS app.tenant_config (
    id                              uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Branding
    brand_name                      text NOT NULL DEFAULT 'Help Center',
    brand_logo_url                  text,
    brand_favicon_url               text,
    primary_color                   text NOT NULL DEFAULT '#2563eb',

    -- Locale
    default_locale                  text NOT NULL DEFAULT 'es-AR',
    supported_locales               text[] NOT NULL DEFAULT ARRAY['es-AR']::text[],

    -- LLM provider
    llm_provider                    text NOT NULL DEFAULT 'openai',
    llm_model                       text NOT NULL DEFAULT 'gpt-4.1-nano',
    llm_base_url                    text,

    -- Embedding provider
    embedding_provider              text NOT NULL DEFAULT 'openai',
    embedding_model                 text NOT NULL DEFAULT 'text-embedding-3-small',
    embedding_dimensions            int  NOT NULL DEFAULT 1024,

    -- Reranker provider
    reranker_provider               text NOT NULL DEFAULT 'tei-bge',
    reranker_model                  text NOT NULL DEFAULT 'BAAI/bge-reranker-v2-m3',
    reranker_base_url               text,

    -- Retrieval params (per-tenant tuning)
    enable_bm25                     boolean NOT NULL DEFAULT true,
    enable_reranker                 boolean NOT NULL DEFAULT true,
    enable_conversational_memory    boolean NOT NULL DEFAULT true,
    enable_query_rewrite            boolean NOT NULL DEFAULT false,
    rag_top_k_vector                int     NOT NULL DEFAULT 20,
    rag_top_k_bm25                  int     NOT NULL DEFAULT 20,
    rag_top_k_final                 int     NOT NULL DEFAULT 8,
    rrf_k                           int     NOT NULL DEFAULT 60,
    conversation_history_turns      int     NOT NULL DEFAULT 5,

    -- Cache params
    cache_ttl_hours                 int           NOT NULL DEFAULT 24,
    cache_similarity_threshold      numeric(4, 3) NOT NULL DEFAULT 0.90,

    -- Budgets
    default_monthly_budget_usd      numeric(8, 2) NOT NULL DEFAULT 5.00,
    global_daily_budget_usd         numeric(10, 2),

    -- Feature flags
    enable_vlm_image_description    boolean NOT NULL DEFAULT false,
    enable_otel                     boolean NOT NULL DEFAULT false,

    -- Object storage
    s3_endpoint                     text,
    s3_bucket                       text NOT NULL DEFAULT 'helpcenter',
    s3_region                       text NOT NULL DEFAULT 'us-east-1',

    -- Audit
    created_at                      timestamptz NOT NULL DEFAULT now(),
    updated_at                      timestamptz NOT NULL DEFAULT now()
);

-- Ensure only one row exists; the row id is mounted in code as a constant.
CREATE UNIQUE INDEX IF NOT EXISTS ux_tenant_config_singleton
    ON app.tenant_config ((1));

-- Seed the singleton row on first apply if absent. Subsequent migrations / setup wizard
-- updates an existing row instead of inserting.
INSERT INTO app.tenant_config (id)
    SELECT gen_random_uuid()
    WHERE NOT EXISTS (SELECT 1 FROM app.tenant_config);

-- Trigger to bump updated_at on every change.
CREATE OR REPLACE FUNCTION app.f_tenant_config_touch()
    RETURNS trigger AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tg_tenant_config_touch
    BEFORE UPDATE ON app.tenant_config
    FOR EACH ROW
    EXECUTE FUNCTION app.f_tenant_config_touch();
