from __future__ import annotations

from pydantic import BaseModel, ConfigDict, Field


class ChatRequest(BaseModel):
    question: str = Field(min_length=1, max_length=4000)


class CitationSchema(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    chunk_id: str = Field(alias="chunkId")
    instruction_id: str = Field(alias="instructionId")
    instruction_version_id: str = Field(alias="instructionVersionId")
    heading_path: list[str] = Field(alias="headingPath")


class CacheInvalidationRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    instruction_ids: list[str] = Field(alias="instructionIds")


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
