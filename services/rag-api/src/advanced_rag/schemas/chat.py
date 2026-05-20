from __future__ import annotations

from pydantic import BaseModel, ConfigDict, Field


class ChatRequest(BaseModel):
    question: str = Field(min_length=1, max_length=4000)


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
