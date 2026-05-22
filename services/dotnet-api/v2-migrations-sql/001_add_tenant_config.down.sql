-- Reverse of 001_add_tenant_config

DROP TRIGGER IF EXISTS tg_tenant_config_touch ON app.tenant_config;
DROP FUNCTION IF EXISTS app.f_tenant_config_touch();
DROP TABLE IF EXISTS app.tenant_config;
