from __future__ import annotations

from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field


class ChatFilters(BaseModel):
    """Optional pre-applied filters for the chat retrieval.

    v2 supports dimension filters as a flat list of `dimension_value_id` UUIDs. The
    chat-web SPA reads these from the URL (`?dimension_value=uuid&dimension_value=uuid`)
    and passes them through so deep-linked queries restrict retrieval consistently.
    """

    model_config = ConfigDict(populate_by_name=True)

    dimension_value_ids: list[UUID] | None = Field(default=None, alias="dimensionValueIds")


class ChatRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    question: str = Field(min_length=1, max_length=4000)
    session_id: UUID | None = Field(default=None, alias="sessionId")
    filters: ChatFilters | None = None
    locale: str | None = None


class CitationSchema(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    chunk_id: str = Field(alias="chunkId")
    document_id: str = Field(alias="documentId")
    document_version_id: str = Field(alias="documentVersionId")
    heading_path: list[str] = Field(alias="headingPath")


class CacheInvalidationRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    document_ids: list[str] = Field(alias="documentIds")


class CacheInvalidationResponse(BaseModel):
    invalidated: int


class FeedbackRequest(BaseModel):
    value: str = Field(pattern="^(up|down)$")
    comment: str | None = None


class FeedbackResponse(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    query_audit_event_id: str = Field(alias="queryAuditEventId")
    value: str
    comment: str | None
