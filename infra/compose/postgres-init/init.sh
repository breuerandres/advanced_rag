#!/usr/bin/env bash
set -euo pipefail

export PGPASSWORD="$(cat "${PGPASSWORD_FILE}")"

psql \
  --set=ON_ERROR_STOP=1 \
  --set=target_db="${TARGET_POSTGRES_DB}" \
  --set=app_user="${POSTGRES_APP_USER}" \
  --set=rag_user="${POSTGRES_RAG_USER}" \
  --set=reporting_user="${POSTGRES_REPORTING_USER}" \
  --set=app_password="$(cat /run/secrets/postgres_app_password)" \
  --set=rag_password="$(cat /run/secrets/postgres_rag_password)" \
  --set=reporting_password="$(cat /run/secrets/postgres_reporting_password)" \
  --file=/init/init.sql
