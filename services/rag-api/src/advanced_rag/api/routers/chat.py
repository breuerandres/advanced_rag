from __future__ import annotations

import json
from uuid import UUID

from fastapi import APIRouter, Header, Query, Request
from starlette.responses import StreamingResponse

from advanced_rag.auth.chat_tokens import ChatTokenClaims
from advanced_rag.auth.session_validation import SessionValidatorProtocol
from advanced_rag.core.csrf import validate_csrf_request
from advanced_rag.core.errors import ApiException
from advanced_rag.core.request_id import REQUEST_ID_HEADER
from advanced_rag.rag.chat_service import ChatService
from advanced_rag.rag.feedback_service import FeedbackService
from advanced_rag.schemas.chat import (
    CacheInvalidationRequest,
    CacheInvalidationResponse,
    ChatRequest,
    ChatSessionHistoryResponse,
    ChatSessionListResponse,
    ChatSessionSummary,
    ChatSessionTurn,
    CitationSchema,
    FeedbackRequest,
    FeedbackResponse,
)


router = APIRouter(tags=["chat"])


@router.post("/api/chat")
async def post_chat(body: ChatRequest, request: Request) -> StreamingResponse:
    request_id = getattr(request.state, "request_id", "") or request.headers.get(REQUEST_ID_HEADER, "")
    validate_csrf_request(request)
    claims = await _validate_session(request, request_id)
    if not request.app.state.rate_limiter.allow(f"chat:user:{claims.user_id}", 30, 60):
        raise ApiException(
            "CHAT_RATE_LIMITED",
            429,
            "Chat request rate limit exceeded.",
            details={"limit": 30, "windowSeconds": 60},
        )
    service: ChatService = request.app.state.chat_service
    filters = (
        body.filters.dimension_value_ids
        if body.filters is not None and body.filters.dimension_value_ids
        else None
    )
    # Pre-stream failures (empty question, over-budget) raise here and return the regular
    # JSON error envelope before any token is streamed or any paid provider call is made.
    await service.precheck(question=body.question, claims=claims)

    async def event_stream():  # type: ignore[no-untyped-def]
        yield _event("request-id", {"request_id": request_id})
        try:
            async for item in service.answer_stream(
                question=body.question,
                claims=claims,
                request_id=request_id,
                filters=filters,
                session_id=body.session_id,
                locale=body.locale,
                scope_document_id=body.document_id,
            ):
                yield _event(item.event, item.payload)
        except ApiException as exc:
            yield _event(
                "error",
                {"error": {"code": exc.code, "message": str(exc), "request_id": request_id}},
            )
            return
        yield _event("done", {})

    return StreamingResponse(
        event_stream(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache"},
    )


@router.get(
    "/api/chat/sessions",
    response_model=ChatSessionListResponse,
    response_model_by_alias=True,
)
async def list_chat_sessions(
    request: Request,
    limit: int = Query(default=30, ge=1, le=100),
) -> ChatSessionListResponse:
    request_id = getattr(request.state, "request_id", "") or request.headers.get(REQUEST_ID_HEADER, "")
    claims = await _validate_session(request, request_id)
    service: ChatService = request.app.state.chat_service
    sessions = await service.list_sessions(user_id=UUID(claims.user_id), limit=limit)
    return ChatSessionListResponse(
        sessions=[
            ChatSessionSummary(
                sessionId=row["session_id"],
                title=row["title"],
                lastQuestion=row["last_question"],
                lastAnswer=row["last_answer"],
                lastActivityAt=row["last_activity_at"],
                turnCount=row["turn_count"],
            )
            for row in sessions
        ]
    )


@router.get(
    "/api/chat/sessions/{session_id}",
    response_model=ChatSessionHistoryResponse,
    response_model_by_alias=True,
)
async def get_chat_session_history(
    session_id: UUID,
    request: Request,
) -> ChatSessionHistoryResponse:
    request_id = getattr(request.state, "request_id", "") or request.headers.get(REQUEST_ID_HEADER, "")
    claims = await _validate_session(request, request_id)
    service: ChatService = request.app.state.chat_service
    turns = await service.get_session_history(user_id=UUID(claims.user_id), session_id=session_id)
    return ChatSessionHistoryResponse(
        sessionId=session_id,
        turns=[
            ChatSessionTurn(
                queryAuditEventId=turn["id"],
                question=turn["question"],
                answer=turn["answer"],
                createdAt=turn["created_at"],
                cacheHit=turn["cache_hit"],
                feedbackValue=turn["feedback_value"],
                feedbackComment=turn["feedback_comment"],
                citations=[
                    CitationSchema(
                        chunkId=str(citation["chunk_id"]),
                        documentId=str(citation["document_id"]),
                        documentVersionId=str(citation["document_version_id"]),
                        headingPath=list(citation["heading_path"]),
                    )
                    for citation in turn["citations"]
                ],
            )
            for turn in turns
        ],
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
    request_id = getattr(request.state, "request_id", "") or request.headers.get(REQUEST_ID_HEADER, "")
    validate_csrf_request(request)
    claims = await _validate_session(request, request_id)
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


def _event(name: str, payload: dict[str, object]) -> str:
    return f"event: {name}\ndata: {json.dumps(payload, separators=(',', ':'))}\n\n"


async def _validate_session(request: Request, request_id: str) -> ChatTokenClaims:
    session_cookie = request.cookies.get(request.app.state.settings.session_cookie_name)
    if not session_cookie:
        raise ApiException("AUTH_REQUIRED", 401, "Session required.")

    validator: SessionValidatorProtocol = request.app.state.session_validator
    return await validator.validate(session_cookie, request_id=request_id)
