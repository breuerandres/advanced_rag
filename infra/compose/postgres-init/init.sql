SELECT format('CREATE DATABASE %I', :'target_db')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'target_db')
\gexec

\connect :target_db

CREATE EXTENSION IF NOT EXISTS vector;

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'app_user', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'app_user')
\gexec

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'rag_user', :'rag_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'rag_user')
\gexec

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'reporting_user', :'reporting_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'reporting_user')
\gexec

SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', :'app_user', :'app_password')
\gexec

SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', :'rag_user', :'rag_password')
\gexec

SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', :'reporting_user', :'reporting_password')
\gexec

CREATE SCHEMA IF NOT EXISTS app;
CREATE SCHEMA IF NOT EXISTS rag;

SELECT format('ALTER SCHEMA app OWNER TO %I', :'app_user')
\gexec

SELECT format('ALTER SCHEMA rag OWNER TO %I', :'rag_user')
\gexec

SELECT format('GRANT USAGE ON SCHEMA rag TO %I', :'reporting_user')
\gexec

SELECT format('GRANT USAGE ON SCHEMA app TO %I', :'rag_user')
\gexec

SELECT format('GRANT SELECT ON app.instruction_permissions TO %I', :'rag_user')
\gexec

SELECT format('GRANT SELECT ON app.user_ai_budget_limits TO %I', :'rag_user')
\gexec
