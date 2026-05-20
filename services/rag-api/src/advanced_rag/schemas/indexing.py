from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field


class InternalIndexingRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    document_id: UUID = Field(alias="documentId")
    document_version_id: UUID = Field(alias="documentVersionId")
    content_html: str = Field(alias="contentHtml")
    corpus_mode: str = Field(alias="corpusMode")
    retry: bool = False


class InternalIndexingResponse(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    job_id: UUID = Field(alias="jobId")
    status: str
    chunk_count: int = Field(alias="chunkCount")
    error_code: str | None = Field(default=None, alias="errorCode")
    error_message: str | None = Field(default=None, alias="errorMessage")
