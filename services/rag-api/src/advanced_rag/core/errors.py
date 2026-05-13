from typing import Any

from pydantic import BaseModel, ConfigDict, Field


class ApiErrorBody(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    code: str
    message: str
    details: dict[str, Any] | None = None
    request_id: str = Field(alias="requestId")


class ApiErrorEnvelope(BaseModel):
    error: ApiErrorBody


class ApiException(Exception):
    def __init__(
        self,
        code: str,
        http_status: int,
        message: str,
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(message)
        self.code = code
        self.http_status = http_status
        self.details = details
