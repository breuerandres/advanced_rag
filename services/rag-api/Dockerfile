FROM ghcr.io/astral-sh/uv:0.9.22-python3.12-bookworm-slim AS runtime

WORKDIR /app

ENV PATH="/app/.venv/bin:$PATH" \
    PYTHONUNBUFFERED=1 \
    UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy

COPY pyproject.toml uv.lock README.md ./
RUN uv sync --locked --no-dev --no-install-project

COPY src ./src
RUN uv sync --locked --no-dev

EXPOSE 8000

CMD ["uvicorn", "advanced_rag.main:app", "--host", "0.0.0.0", "--port", "8000"]
