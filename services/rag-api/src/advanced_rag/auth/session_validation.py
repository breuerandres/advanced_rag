from __future__ import annotations

import hashlib
import time
from collections.abc import Callable
from typing import Protocol

import httpx
from pydantic import BaseModel, ConfigDict, Field, ValidationError

from advanced_rag.auth.chat_tokens import ChatTokenClaims, ChatTokenValidatorProtocol
from advanced_rag.core.errors import ApiException
from advanced_rag.core.request_id import REQUEST_ID_HEADER


class SessionValidatorProtocol(Protocol):
    async def validate(
        self,
        session_cookie: str,
        request_id: str | None = None,
    ) -> ChatTokenClaims:
        pass


class LegacyChatTokenSessionValidator:
    def __init__(
        self,
        token_validator: Callable[[], ChatTokenValidatorProtocol],
    ) -> None:
        self._token_validator = token_validator

    async def validate(
        self,
        session_cookie: str,
        request_id: str | None = None,
    ) -> ChatTokenClaims:
        _ = request_id
        return self._token_validator().validate(session_cookie)


class DotnetSessionValidator:
    def __init__(
        self,
        *,
        validate_url: str,
        internal_service_token: str,
        cache_seconds: int,
        session_cookie_name: str,
        client: httpx.AsyncClient | None = None,
        monotonic: Callable[[], float] = time.monotonic,
    ) -> None:
        self._validate_url = validate_url
        self._internal_service_token = internal_service_token
        self._cache_seconds = cache_seconds
        self._session_cookie_name = session_cookie_name
        self._client = client or httpx.AsyncClient(timeout=5)
        self._monotonic = monotonic
        self._cache: dict[str, tuple[float, ChatTokenClaims]] = {}

    async def validate(
        self,
        session_cookie: str,
        request_id: str | None = None,
    ) -> ChatTokenClaims:
        key = hashlib.sha256(session_cookie.encode("utf-8")).hexdigest()
        now = self._monotonic()
        cached = self._cache.get(key)
        if cached is not None and cached[0] > now:
            return cached[1]

        claims = await self._fetch_claims(session_cookie, request_id)
        self._cache[key] = (now + self._cache_seconds, claims)
        return claims

    async def _fetch_claims(
        self,
        session_cookie: str,
        request_id: str | None,
    ) -> ChatTokenClaims:
        if not self._internal_service_token:
            raise ApiException("AUTH_REQUIRED", 401, "Session validation failed.")

        headers = {
            "X-Internal-Service-Token": self._internal_service_token,
            "Cookie": f"{self._session_cookie_name}={session_cookie}",
        }
        if request_id:
            headers[REQUEST_ID_HEADER] = request_id

        try:
            response = await self._client.get(
                self._validate_url,
                headers=headers,
            )
        except httpx.HTTPError as exc:
            raise ApiException("AUTH_REQUIRED", 401, "Session validation failed.") from exc

        if response.status_code != 200:
            raise ApiException("AUTH_REQUIRED", 401, "Session validation failed.")

        try:
            payload = SessionValidationResponse.model_validate(response.json())
        except (ValueError, ValidationError) as exc:
            raise ApiException("AUTH_REQUIRED", 401, "Session validation failed.") from exc

        return ChatTokenClaims(
            user_id=payload.user_id,
            role=payload.role,
            groups=payload.groups,
            access_scope_hash=payload.access_scope_hash,
            corpus=payload.corpus,
        )


class SessionValidationResponse(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    user_id: str = Field(alias="userId")
    role: str
    groups: list[str]
    access_scope_hash: str = Field(alias="accessScopeHash")
    corpus: str
