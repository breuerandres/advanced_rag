from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Protocol

import jwt
from jwt import PyJWKClient

from advanced_rag.core.errors import ApiException


@dataclass(frozen=True)
class ChatTokenValidationSettings:
    issuer: str
    audience: str
    public_keys_by_kid: dict[str, str]


@dataclass(frozen=True)
class ChatTokenClaims:
    user_id: str
    role: str
    groups: list[str]
    access_scope_hash: str
    corpus: str


class ChatTokenValidatorProtocol(Protocol):
    def validate(self, token: str) -> ChatTokenClaims:
        pass


class ChatTokenValidator:
    def __init__(self, settings: ChatTokenValidationSettings) -> None:
        self._settings = settings

    def validate(self, token: str) -> ChatTokenClaims:
        public_key = self._public_key_for_token(token)

        try:
            payload = jwt.decode(
                token,
                public_key,
                algorithms=["RS256"],
                issuer=self._settings.issuer,
                audience=self._settings.audience,
                options={
                    "require": [
                        "iss",
                        "aud",
                        "sub",
                        "role",
                        "groups",
                        "access_scope_hash",
                        "corpus",
                        "exp",
                        "iat",
                        "jti",
                    ]
                },
            )
        except jwt.ExpiredSignatureError as exc:
            raise ApiException("AUTH_TOKEN_EXPIRED", 401, "Chat token expired.") from exc
        except jwt.InvalidTokenError as exc:
            raise ApiException("AUTH_TOKEN_INVALID", 401, "Chat token invalid.") from exc

        return _claims_from_payload(payload)

    def _public_key_for_token(self, token: str) -> str:
        try:
            kid = jwt.get_unverified_header(token).get("kid")
        except jwt.InvalidTokenError as exc:
            raise ApiException("AUTH_TOKEN_INVALID", 401, "Chat token invalid.") from exc

        if not isinstance(kid, str) or not kid:
            raise ApiException("AUTH_TOKEN_INVALID_KEY", 401, "Chat token key is invalid.")

        public_key = self._settings.public_keys_by_kid.get(kid)
        if public_key is None:
            raise ApiException("AUTH_TOKEN_INVALID_KEY", 401, "Chat token key is invalid.")

        return public_key


class JwksChatTokenValidator:
    def __init__(
        self,
        *,
        issuer: str,
        audience: str,
        jwks_url: str,
        jwks_client: Any | None = None,
    ) -> None:
        self._issuer = issuer
        self._audience = audience
        self._jwks_client = jwks_client or PyJWKClient(jwks_url)

    def validate(self, token: str) -> ChatTokenClaims:
        try:
            signing_key = self._jwks_client.get_signing_key_from_jwt(token)
        except jwt.PyJWKClientError as exc:
            raise ApiException("AUTH_TOKEN_INVALID_KEY", 401, "Chat token key is invalid.") from exc
        except jwt.InvalidTokenError as exc:
            raise ApiException("AUTH_TOKEN_INVALID", 401, "Chat token invalid.") from exc

        try:
            payload = jwt.decode(
                token,
                signing_key.key,
                algorithms=["RS256"],
                issuer=self._issuer,
                audience=self._audience,
                options={
                    "require": [
                        "iss",
                        "aud",
                        "sub",
                        "role",
                        "groups",
                        "access_scope_hash",
                        "corpus",
                        "exp",
                        "iat",
                        "jti",
                    ]
                },
            )
        except jwt.ExpiredSignatureError as exc:
            raise ApiException("AUTH_TOKEN_EXPIRED", 401, "Chat token expired.") from exc
        except jwt.InvalidTokenError as exc:
            raise ApiException("AUTH_TOKEN_INVALID", 401, "Chat token invalid.") from exc

        return _claims_from_payload(payload)


def _claims_from_payload(payload: dict[str, Any]) -> ChatTokenClaims:
    user_id = payload.get("sub")
    role = payload.get("role")
    groups = payload.get("groups")
    access_scope_hash = payload.get("access_scope_hash")
    corpus = payload.get("corpus")

    if not isinstance(user_id, str) or not user_id:
        raise ApiException("AUTH_TOKEN_INVALID", 401, "Chat token invalid.")
    if not isinstance(role, str) or not role:
        raise ApiException("AUTH_TOKEN_INVALID", 401, "Chat token invalid.")
    if not isinstance(groups, list) or not all(isinstance(item, str) for item in groups):
        raise ApiException("AUTH_TOKEN_INVALID", 401, "Chat token invalid.")
    if not isinstance(access_scope_hash, str) or not access_scope_hash:
        raise ApiException("AUTH_TOKEN_INVALID", 401, "Chat token invalid.")
    if not isinstance(corpus, str) or corpus not in {"published", "preview"}:
        raise ApiException("AUTH_TOKEN_INVALID", 401, "Chat token invalid.")

    return ChatTokenClaims(
        user_id=user_id,
        role=role,
        groups=groups,
        access_scope_hash=access_scope_hash,
        corpus=corpus,
    )
