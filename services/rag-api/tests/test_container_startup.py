from pathlib import Path


SERVICE_ROOT = Path(__file__).resolve().parents[1]


def test_container_entrypoint_runs_alembic_before_starting_server() -> None:
    dockerfile = (SERVICE_ROOT / "Dockerfile").read_text(encoding="utf-8")
    entrypoint = (SERVICE_ROOT / "docker-entrypoint.sh").read_text(encoding="utf-8")

    assert "docker-entrypoint.sh" in dockerfile
    assert "COPY alembic.ini" in dockerfile
    assert "COPY alembic" in dockerfile
    assert "alembic upgrade head" in entrypoint
    assert "uvicorn advanced_rag.main:app" in entrypoint
