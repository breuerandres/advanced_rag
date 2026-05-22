from collections.abc import Awaitable, Callable
from typing import cast

from fastapi import FastAPI
from fastapi.exceptions import RequestValidationError
from pydantic import BaseModel
from starlette.exceptions import HTTPException as StarletteHTTPException
from starlette.requests import Request
from starlette.responses import JSONResponse, Response

from advanced_rag.api.routers.chat import router as chat_router
from advanced_rag.api.routers.indexing import router as indexing_router
from advanced_rag.auth.chat_tokens import (
    ChatTokenValidationSettings,
    ChatTokenValidator,
    ChatTokenValidatorProtocol,
    JwksChatTokenValidator,
)
from advanced_rag.core.config import Settings
from advanced_rag.core.errors import (
    ApiException,
    api_exception_handler,
    http_exception_handler,
    validation_exception_handler,
)
from advanced_rag.core.health import OperationalReadinessChecker, ReadinessResult
from advanced_rag.core.logging import OperationalRequestLoggingMiddleware
from advanced_rag.core.request_id import RequestIdMiddleware
from advanced_rag.core.rate_limit import FixedWindowRateLimiter
from advanced_rag.db.session import create_database_engine, create_session_factory
from advanced_rag.providers import (
    IEmbeddingProvider,
    ILlmProvider,
    IRerankerProvider,
    ProviderFactory,
)
from advanced_rag.providers.factory import TenantProviderConfig
from advanced_rag.rag.chat_service import ChatService
from advanced_rag.rag.feedback_service import FeedbackService
from advanced_rag.rag.indexing_service import InternalIndexingService


class HealthResponse(BaseModel):
    status: str
    checks: list[str] | None = None


ExceptionHandler = Callable[[Request, Exception], Response | Awaitable[Response]]


def create_app(
    settings: Settings | None = None,
    embedding_provider: IEmbeddingProvider | None = None,
    llm_provider: ILlmProvider | None = None,
    reranker_provider: IRerankerProvider | None = None,
    chat_token_validator: ChatTokenValidatorProtocol | None = None,
) -> FastAPI:
    """Compose the FastAPI app.

    v2 wiring: when `embedding_provider`/`llm_provider`/`reranker_provider` are not
    supplied, a `ProviderFactory` is built from `Settings` and the concrete providers
    are instantiated. Tests pass explicit fakes so no real network/LLM calls happen.

    `reranker_provider` is allowed to be `None` (rerank step is then skipped). The
    factory short-circuits to `None` when `settings.enable_reranker=False`.
    """
    app = FastAPI(title="Advanced RAG RAG API")
    resolved_settings = settings or Settings()
    app.state.settings = resolved_settings
    app.state.database_engine = create_database_engine(resolved_settings.resolved_rag_database_url)
    app.state.session_factory = create_session_factory(app.state.database_engine)

    factory = _build_provider_factory(resolved_settings)
    app.state.provider_factory = factory

    app.state.embedding_provider = embedding_provider or factory.make_embedding()
    app.state.llm_provider = llm_provider or factory.make_llm()
    if reranker_provider is not None:
        app.state.reranker_provider = reranker_provider
    elif resolved_settings.enable_reranker and resolved_settings.reranker_base_url:
        app.state.reranker_provider = factory.make_reranker()
    else:
        app.state.reranker_provider = None

    app.state.internal_indexing_service = InternalIndexingService(
        app.state.session_factory,
        app.state.embedding_provider,
        resolved_settings,
    )
    app.state.chat_token_validator = chat_token_validator or _create_chat_token_validator(resolved_settings)
    app.state.chat_service = ChatService(
        app.state.session_factory,
        app.state.embedding_provider,
        app.state.llm_provider,
        app.state.reranker_provider,
        resolved_settings,
    )
    app.state.feedback_service = FeedbackService(app.state.session_factory)
    app.state.rate_limiter = FixedWindowRateLimiter()
    app.state.readiness_checker = OperationalReadinessChecker(resolved_settings, app.state.database_engine)
    app.add_middleware(RequestIdMiddleware)
    app.add_middleware(OperationalRequestLoggingMiddleware, log_directory=resolved_settings.log_directory)
    app.add_exception_handler(ApiException, cast(ExceptionHandler, api_exception_handler))
    app.add_exception_handler(StarletteHTTPException, cast(ExceptionHandler, http_exception_handler))
    app.add_exception_handler(
        RequestValidationError,
        cast(ExceptionHandler, validation_exception_handler),
    )

    @app.get("/health/live", response_model=HealthResponse, response_model_exclude_none=True)
    async def live_health() -> HealthResponse:
        return HealthResponse(status="ok")

    @app.get("/health/ready", response_model=HealthResponse, response_model_exclude_none=True)
    async def ready_health() -> HealthResponse | JSONResponse:
        result: ReadinessResult = await app.state.readiness_checker.check()
        if result.is_ready:
            return HealthResponse(status="ok")

        return JSONResponse(
            status_code=503,
            content={"status": "unhealthy", "checks": result.failed_checks},
        )

    app.include_router(indexing_router)
    app.include_router(chat_router)

    return app


def _build_provider_factory(settings: Settings) -> ProviderFactory:
    """Translate `Settings` into the `TenantProviderConfig` consumed by the factory.

    Until tenant_config is loaded from the database (Phase 1.1), settings is the
    source of truth and the factory is fully owned by this process.
    """
    return ProviderFactory(
        TenantProviderConfig(
            llm_provider=settings.llm_provider,
            llm_model=settings.resolved_chat_model,
            llm_base_url=settings.llm_base_url or None,
            llm_api_key=settings.resolved_llm_api_key,
            embedding_provider=settings.embedding_provider,
            embedding_model=settings.resolved_embedding_model,
            embedding_dimensions=settings.resolved_embedding_dimensions,
            embedding_base_url=settings.embedding_base_url or None,
            embedding_api_key=settings.resolved_embedding_api_key,
            enable_reranker=settings.enable_reranker,
            reranker_provider=settings.reranker_provider,
            reranker_model=settings.reranker_model,
            reranker_base_url=settings.reranker_base_url or None,
            reranker_api_key=settings.reranker_api_key,
            azure_endpoint=settings.azure_endpoint or None,
            azure_api_version=settings.azure_api_version,
            azure_chat_deployment=settings.azure_chat_deployment or None,
            azure_embedding_deployment=settings.azure_embedding_deployment or None,
        )
    )


def _create_chat_token_validator(settings: Settings) -> ChatTokenValidatorProtocol:
    if settings.dotnet_jwks_url:
        return JwksChatTokenValidator(
            issuer=settings.chat_token_issuer,
            audience=settings.chat_token_audience,
            jwks_url=settings.dotnet_jwks_url,
        )

    return ChatTokenValidator(
        ChatTokenValidationSettings(
            issuer=settings.chat_token_issuer,
            audience=settings.chat_token_audience,
            public_keys_by_kid=settings.chat_token_public_keys_by_kid,
        )
    )


app = create_app()
