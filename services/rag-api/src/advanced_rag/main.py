from fastapi import FastAPI
from pydantic import BaseModel

from advanced_rag.core.config import Settings


class HealthResponse(BaseModel):
    status: str


def create_app() -> FastAPI:
    app = FastAPI(title="Advanced RAG RAG API")
    app.state.settings = Settings()

    @app.get("/health/live", response_model=HealthResponse)
    async def live_health() -> HealthResponse:
        return HealthResponse(status="ok")

    @app.get("/health/ready", response_model=HealthResponse)
    async def ready_health() -> HealthResponse:
        return HealthResponse(status="ok")

    return app


app = create_app()
