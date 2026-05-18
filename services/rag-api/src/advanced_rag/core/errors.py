from typing import Any

from fastapi import Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import BaseModel, ConfigDict, Field
from starlette.exceptions import HTTPException as StarletteHTTPException

from advanced_rag.core.request_id import REQUEST_ID_HEADER


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


async def api_exception_handler(request: Request, exception: ApiException) -> JSONResponse:
    request.state.safe_error_code = exception.code
    return error_response(
        request=request,
        status_code=exception.http_status,
        code=exception.code,
        message=str(exception),
        details=exception.details,
    )


async def http_exception_handler(request: Request, exception: StarletteHTTPException) -> JSONResponse:
    if exception.status_code == 404:
        request.state.safe_error_code = "NOT_FOUND"
        return error_response(
            request=request,
            status_code=404,
            code="NOT_FOUND",
            message="Resource not found.",
        )

    request.state.safe_error_code = "HTTP_ERROR"
    return error_response(
        request=request,
        status_code=exception.status_code,
        code="HTTP_ERROR",
        message="HTTP request failed.",
        details={"status_code": exception.status_code},
    )


async def validation_exception_handler(
    request: Request,
    exception: RequestValidationError,
) -> JSONResponse:
    request.state.safe_error_code = "VALIDATION_FAILED"
    return error_response(
        request=request,
        status_code=400,
        code="VALIDATION_FAILED",
        message="Validation failed.",
        details={"errors": _validation_errors(exception)},
    )


def error_response(
    *,
    request: Request,
    status_code: int,
    code: str,
    message: str,
    details: dict[str, Any] | None = None,
) -> JSONResponse:
    request_id = getattr(request.state, "request_id", None) or request.headers.get(REQUEST_ID_HEADER) or ""
    envelope = ApiErrorEnvelope(
        error=ApiErrorBody(
            code=code,
            message=message,
            details=details or {},
            requestId=request_id,
        )
    )

    return JSONResponse(
        status_code=status_code,
        content=envelope.model_dump(by_alias=True),
        headers={REQUEST_ID_HEADER: request_id},
    )


def _validation_errors(exception: RequestValidationError) -> list[dict[str, str]]:
    errors: list[dict[str, str]] = []
    for error in exception.errors():
        field = ".".join(str(part) for part in error["loc"])
        errors.append({"field": field, "message": str(error["msg"])})
    return errors
