FROM python:3.12-slim-bookworm AS runtime

RUN pip install uv

WORKDIR /app

ENV PATH="/app/.venv/bin:$PATH" \
    PYTHONUNBUFFERED=1 \
    UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy

COPY pyproject.toml uv.lock README.md ./
RUN uv sync --locked --no-dev --no-install-project

COPY src ./src
COPY alembic.ini ./
COPY alembic ./alembic
COPY docker-entrypoint.sh ./docker-entrypoint.sh
RUN uv sync --locked --no-dev
RUN chmod +x ./docker-entrypoint.sh

EXPOSE 8000

CMD ["./docker-entrypoint.sh"]
