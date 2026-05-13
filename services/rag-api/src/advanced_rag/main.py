from collections.abc import Awaitable, Callable
from typing import cast

from fastapi import FastAPI
from fastapi.exceptions import RequestValidationError
from pydantic import BaseModel
from starlette.exceptions import HTTPException as StarletteHTTPException
from starlette.requests import Request
from starlette.responses import Response

from advanced_rag.core.config import Settings
from advanced_rag.core.errors import (
    ApiException,
    api_exception_handler,
    http_exception_handler,
    validation_exception_handler,
)
from advanced_rag.core.request_id import RequestIdMiddleware


class HealthResponse(BaseModel):
    status: str


ExceptionHandler = Callable[[Request, Exception], Response | Awaitable[Response]]


def create_app() -> FastAPI:
    app = FastAPI(title="Advanced RAG RAG API")
    app.state.settings = Settings()
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

    return app


app = create_app()
