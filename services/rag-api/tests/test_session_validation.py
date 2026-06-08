from __future__ import annotations

import httpx
import pytest

from advanced_rag.auth.session_validation import DotnetSessionValidator
from advanced_rag.core.errors import ApiException


@pytest.mark.asyncio
async def test_dotnet_session_validator_fetches_access_claims_every_request() -> None:
    calls: list[httpx.Request] = []

    async def handler(request: httpx.Request) -> httpx.Response:
        calls.append(request)
        assert request.headers["X-Internal-Service-Token"] == "internal-token"
        assert request.headers["Cookie"] == "__Host-session=session-cookie-value"
        assert request.headers["X-Request-ID"] in {"req-1", "req-2"}
        return httpx.Response(
            200,
            json={
                "userId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                "role": "Viewer",
                "isGlobalAdmin": False,
                "organizationalUnitId": "01000000-0000-0000-0000-000000000003",
                "groups": ["bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"],
                "accessScopeVersion": len(calls),
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
    assert first.organizational_unit_id == "01000000-0000-0000-0000-000000000003"
    assert first.access_scope_version == 1
    assert second.access_scope_version == 2
    assert len(calls) == 2


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
