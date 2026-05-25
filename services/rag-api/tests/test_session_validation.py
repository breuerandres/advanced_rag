from __future__ import annotations

import httpx
import pytest

from advanced_rag.auth.session_validation import DotnetSessionValidator
from advanced_rag.core.errors import ApiException


@pytest.mark.asyncio
async def test_dotnet_session_validator_caches_claims_by_session_cookie_hash() -> None:
    calls: list[httpx.Request] = []

    async def handler(request: httpx.Request) -> httpx.Response:
        calls.append(request)
        assert request.headers["X-Internal-Service-Token"] == "internal-token"
        assert request.headers["Cookie"] == "__Host-session=session-cookie-value"
        assert request.headers["X-Request-ID"] == "req-1"
        return httpx.Response(
            200,
            json={
                "userId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                "role": "Viewer",
                "groups": ["bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"],
                "accessScopeHash": "scope-allowed",
                "corpus": "published",
            },
        )

    client = httpx.AsyncClient(transport=httpx.MockTransport(handler))
    validator = DotnetSessionValidator(
        validate_url="http://dotnet-api/internal/session/validate",
        internal_service_token="internal-token",
        cache_seconds=60,
        session_cookie_name="__Host-session",
        client=client,
    )

    first = await validator.validate("session-cookie-value", request_id="req-1")
    second = await validator.validate("session-cookie-value", request_id="req-2")

    assert first.user_id == "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
    assert second == first
    assert len(calls) == 1


@pytest.mark.asyncio
async def test_dotnet_session_validator_fails_closed_when_dotnet_rejects_session() -> None:
    async def handler(_: httpx.Request) -> httpx.Response:
        return httpx.Response(401, json={"error": {"code": "AUTH_REQUIRED"}})

    client = httpx.AsyncClient(transport=httpx.MockTransport(handler))
    validator = DotnetSessionValidator(
        validate_url="http://dotnet-api/internal/session/validate",
        internal_service_token="internal-token",
        cache_seconds=60,
        session_cookie_name="__Host-session",
        client=client,
    )

    with pytest.raises(ApiException) as exception:
        await validator.validate("bad-session-cookie", request_id="req-bad")

    assert exception.value.code == "AUTH_REQUIRED"
    assert exception.value.http_status == 401
