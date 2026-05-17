from collections.abc import Awaitable, Callable
from typing import cast

from fastapi import FastAPI
from fastapi.exceptions import RequestValidationError
from pydantic import BaseModel
from starlette.exceptions import HTTPException as StarletteHTTPException
from starlette.requests import Request
from starlette.responses import Response

from advanced_rag.api.routers.indexing import router as indexing_router
from advanced_rag.core.config import Settings
from advanced_rag.core.errors import (
    ApiException,
    api_exception_handler,
    http_exception_handler,
    validation_exception_handler,
)
from advanced_rag.core.request_id import RequestIdMiddleware
from advanced_rag.db.session import create_database_engine, create_session_factory
from advanced_rag.rag.embeddings import EmbeddingProvider, OpenAIEmbeddingProvider
from advanced_rag.rag.indexing_service import InternalIndexingService


class HealthResponse(BaseModel):
    status: str


ExceptionHandler = Callable[[Request, Exception], Response | Awaitable[Response]]


def create_app(
    settings: Settings | None = None,
    embedding_provider: EmbeddingProvider | None = None,
) -> FastAPI:
    app = FastAPI(title="Advanced RAG RAG API")
    app.state.settings = settings or Settings()
    app.state.database_engine = create_database_engine(app.state.settings.resolved_rag_database_url)
    app.state.session_factory = create_session_factory(app.state.database_engine)
    app.state.embedding_provider = embedding_provider or OpenAIEmbeddingProvider(
        api_key=app.state.settings.resolved_openai_api_key
    )
    app.state.internal_indexing_service = InternalIndexingService(
        app.state.session_factory,
        app.state.embedding_provider,
        app.state.settings,
    )
    app.add_middleware(RequestIdMiddleware)
    app.add_exception_handler(ApiException, cast(ExceptionHandler, api_exception_handler))
    app.add_exception_handler(StarletteHTTPException, cast(ExceptionHandler, http_exception_handler))
    app.add_exception_handler(
        RequestValidationError,
        cast(ExceptionHandler, validation_exception_handler),
    )

    @app.get("/health/live", response_model=HealthResponse)
    async def live_health() -> HealthResponse:
        return HealthResponse(status="ok")

    @app.get("/health/ready", response_model=HealthResponse)
    async def ready_health() -> HealthResponse:
        return HealthResponse(status="ok")

    app.include_router(indexing_router)

    return app


app = create_app()
