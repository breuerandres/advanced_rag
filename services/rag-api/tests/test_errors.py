from advanced_rag.core.errors import ApiErrorBody, ApiErrorEnvelope, ApiException


def test_error_envelope_serializes_request_id_alias() -> None:
    envelope = ApiErrorEnvelope(
        error=ApiErrorBody(
            code="VALIDATION_FAILED",
            message="Validation failed.",
            details={"field": "question"},
            requestId="req-123",
        )
    )

    assert envelope.model_dump(by_alias=True) == {
        "error": {
            "code": "VALIDATION_FAILED",
            "message": "Validation failed.",
            "details": {"field": "question"},
            "requestId": "req-123",
        }
    }


def test_api_exception_preserves_safe_error_fields() -> None:
    exception = ApiException(
        code="AUTH_REQUIRED",
        http_status=401,
        message="Chat session required.",
        details={"reason": "missing_cookie"},
    )

    assert exception.code == "AUTH_REQUIRED"
    assert exception.http_status == 401
    assert exception.details == {"reason": "missing_cookie"}
    assert str(exception) == "Chat session required."
