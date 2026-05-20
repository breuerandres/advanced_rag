from __future__ import annotations

import json
from decimal import Decimal
from uuid import UUID

from fastapi import APIRouter, Header, Request
from starlette.responses import StreamingResponse

from advanced_rag.auth.chat_tokens import ChatTokenValidatorProtocol
from advanced_rag.core.errors import ApiException
from advanced_rag.core.request_id import REQUEST_ID_HEADER
from advanced_rag.rag.chat_service import ChatAnswer, ChatService
from advanced_rag.rag.feedback_service import FeedbackService
from advanced_rag.schemas.chat import (
    CacheInvalidationRequest,
    CacheInvalidationResponse,
    ChatRequest,
    FeedbackRequest,
    FeedbackResponse,
)


router = APIRouter(tags=["chat"])


@router.post("/api/chat")
async def post_chat(body: ChatRequest, request: Request) -> StreamingResponse:
    token = request.cookies.get("__Host-chat-token")
    if not token:
        raise ApiException("AUTH_REQUIRED", 401, "Chat session required.")

    validator: ChatTokenValidatorProtocol = request.app.state.chat_token_validator
    claims = validator.validate(token)
    if not request.app.state.rate_limiter.allow(f"chat:user:{claims.user_id}", 30, 60):
        raise ApiException(
            "CHAT_RATE_LIMITED",
            429,
            "Chat request rate limit exceeded.",
            details={"limit": 30, "windowSeconds": 60},
        )
    service: ChatService = request.app.state.chat_service
    request_id = getattr(request.state, "request_id", "") or request.headers.get(REQUEST_ID_HEADER, "")
    answer = await service.answer(question=body.question, claims=claims, request_id=request_id)
    return StreamingResponse(
        _stream_answer(answer, request_id),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache"},
    )


@router.post(
    "/api/feedback/{query_audit_event_id}",
    response_model=FeedbackResponse,
    response_model_by_alias=True,
)
async def post_feedback(
    query_audit_event_id: UUID,
    body: FeedbackRequest,
    request: Request,
) -> FeedbackResponse:
    token = request.cookies.get("__Host-chat-token")
    if not token:
        raise ApiException("AUTH_REQUIRED", 401, "Chat session required.")

    validator: ChatTokenValidatorProtocol = request.app.state.chat_token_validator
    claims = validator.validate(token)
    service: FeedbackService = request.app.state.feedback_service
    comment = await service.submit_feedback(
        query_audit_event_id=query_audit_event_id,
        user_id=UUID(claims.user_id),
        value=body.value,
        comment=body.comment,
    )
    return FeedbackResponse(
        queryAuditEventId=str(query_audit_event_id),
        value=body.value,
        comment=comment,
    )


@router.post(
    "/internal/cache-invalidations",
    response_model=CacheInvalidationResponse,
    response_model_by_alias=True,
)
async def invalidate_cache(
    body: CacheInvalidationRequest,
    request: Request,
    internal_service_token: str | None = Header(default=None, alias="X-Internal-Service-Token"),
) -> CacheInvalidationResponse:
    settings = request.app.state.settings
    expected_token = settings.resolved_internal_service_token
    if not expected_token or internal_service_token != expected_token:
        raise ApiException(
            "AUTH_INTERNAL_TOKEN_INVALID",
            401,
            "Internal service token is invalid.",
        )
    service: ChatService = request.app.state.chat_service
    invalidated = await service.invalidate_sources([UUID(value) for value in body.document_ids])
    return CacheInvalidationResponse(invalidated=invalidated)


async def _stream_answer(answer: ChatAnswer, request_id: str):
    yield _event("request-id", {"request_id": request_id})
    if answer.cache_hit:
        yield _event("cache-hit", {"cached_at": answer.cached_at.isoformat() if answer.cached_at else None})
    yield _event("answer-token", {"delta": answer.answer})
    yield _event(
        "citations",
        {
            "query_audit_event_id": str(answer.query_audit_event_id),
            "citations": [
                {
                    "chunk_id": str(citation.chunk_id),
                    "document_id": str(citation.document_id),
                    "document_version_id": str(citation.document_version_id),
                    "heading_path": citation.heading_path,
                }
                for citation in answer.citations
            ]
        },
    )
    yield _event(
        "usage",
        {
            "input_tokens": answer.input_tokens,
            "cached_tokens": answer.cached_tokens,
            "output_tokens": answer.output_tokens,
            "cost_usd": _decimal_to_float(answer.estimated_cost_usd),
        },
    )
    yield _event("done", {})


def _event(name: str, payload: dict[str, object]) -> str:
    return f"event: {name}\ndata: {json.dumps(payload, separators=(',', ':'))}\n\n"


def _decimal_to_float(value: Decimal) -> float:
    return float(value)
