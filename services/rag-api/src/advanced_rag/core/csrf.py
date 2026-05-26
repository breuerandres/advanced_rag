from __future__ import annotations

import base64
import hashlib
import hmac

from fastapi import Request

from advanced_rag.core.errors import ApiException

CSRF_COOKIE_NAME = "__Host-CSRF"
CSRF_HEADER_NAME = "X-CSRF-Token"


def validate_csrf_request(request: Request) -> None:
    signing_key = request.app.state.settings.resolved_csrf_signing_key
    header_token = request.headers.get(CSRF_HEADER_NAME)
    cookie_token = request.cookies.get(CSRF_COOKIE_NAME)

    if (
        not signing_key
        or header_token is None
        or cookie_token is None
        or not _validate_token_pair(header_token, cookie_token, signing_key)
    ):
        raise ApiException("CSRF_TOKEN_INVALID", 400, "CSRF token is invalid.")


def _validate_token_pair(header_token: str, cookie_token: str, signing_key: str) -> bool:
    if not hmac.compare_digest(header_token.encode("utf-8"), cookie_token.encode("utf-8")):
        return False

    parts = header_token.split(".")
    if len(parts) != 3 or not parts[1].isdigit():
        return False

    payload = f"{parts[0]}.{parts[1]}"
    expected_signature = _sign(payload, signing_key)
    return hmac.compare_digest(expected_signature.encode("utf-8"), parts[2].encode("utf-8"))


def _sign(payload: str, signing_key: str) -> str:
    digest = hmac.new(
        signing_key.encode("utf-8"),
        payload.encode("utf-8"),
        hashlib.sha256,
    ).digest()
    return base64.urlsafe_b64encode(digest).decode("ascii").rstrip("=")
