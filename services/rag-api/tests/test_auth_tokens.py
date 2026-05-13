from __future__ import annotations

from datetime import UTC, datetime, timedelta
from uuid import uuid4

import jwt
import pytest
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import rsa

from advanced_rag.auth.chat_tokens import (
    ChatTokenValidationSettings,
    ChatTokenValidator,
)
from advanced_rag.core.errors import ApiException


ISSUER = "advanced-rag-dotnet-api"
AUDIENCE = "advanced-rag-chat"
USER_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
GROUP_ID = "cccccccc-cccc-cccc-cccc-cccccccccccc"


def test_valid_signed_chat_token_is_accepted_without_user_state_introspection() -> None:
    key_pair = _rsa_key_pair()
    token = _encode_chat_token(key_pair.private_key_pem)
    validator = ChatTokenValidator(
        ChatTokenValidationSettings(
            issuer=ISSUER,
            audience=AUDIENCE,
            public_keys_by_kid={"test-key": key_pair.public_key_pem},
        )
    )

    claims = validator.validate(token)

    assert claims.user_id == USER_ID
    assert claims.role == "Viewer"
    assert claims.groups == [GROUP_ID]
    assert claims.access_scope_hash == "scope-hash"
    assert claims.corpus == "published"


def test_expired_chat_token_is_rejected() -> None:
    key_pair = _rsa_key_pair()
    token = _encode_chat_token(
        key_pair.private_key_pem,
        expires_at=datetime.now(UTC) - timedelta(minutes=1),
    )
    validator = _validator(key_pair.public_key_pem)

    with pytest.raises(ApiException) as exception:
        validator.validate(token)

    assert exception.value.code == "AUTH_TOKEN_EXPIRED"
    assert exception.value.http_status == 401


@pytest.mark.parametrize(
    ("issuer", "audience"),
    [
        ("wrong-issuer", AUDIENCE),
        (ISSUER, "wrong-audience"),
    ],
)
def test_wrong_issuer_or_audience_is_rejected(issuer: str, audience: str) -> None:
    key_pair = _rsa_key_pair()
    token = _encode_chat_token(key_pair.private_key_pem, issuer=issuer, audience=audience)
    validator = _validator(key_pair.public_key_pem)

    with pytest.raises(ApiException) as exception:
        validator.validate(token)

    assert exception.value.code == "AUTH_TOKEN_INVALID"
    assert exception.value.http_status == 401


def test_missing_access_scope_hash_is_rejected() -> None:
    key_pair = _rsa_key_pair()
    payload = _chat_token_payload()
    del payload["access_scope_hash"]
    token = jwt.encode(
        payload,
        key_pair.private_key_pem,
        algorithm="RS256",
        headers={"kid": "test-key"},
    )
    validator = _validator(key_pair.public_key_pem)

    with pytest.raises(ApiException) as exception:
        validator.validate(token)

    assert exception.value.code == "AUTH_TOKEN_INVALID"
    assert exception.value.http_status == 401


def test_unknown_key_id_is_rejected() -> None:
    key_pair = _rsa_key_pair()
    token = _encode_chat_token(key_pair.private_key_pem, kid="retired-key")
    validator = _validator(key_pair.public_key_pem)

    with pytest.raises(ApiException) as exception:
        validator.validate(token)

    assert exception.value.code == "AUTH_TOKEN_INVALID_KEY"
    assert exception.value.http_status == 401


def _validator(public_key_pem: str) -> ChatTokenValidator:
    return ChatTokenValidator(
        ChatTokenValidationSettings(
            issuer=ISSUER,
            audience=AUDIENCE,
            public_keys_by_kid={"test-key": public_key_pem},
        )
    )


def _encode_chat_token(
    private_key_pem: str,
    *,
    issuer: str = ISSUER,
    audience: str = AUDIENCE,
    expires_at: datetime | None = None,
    kid: str = "test-key",
) -> str:
    payload = _chat_token_payload(issuer=issuer, audience=audience, expires_at=expires_at)
    return jwt.encode(payload, private_key_pem, algorithm="RS256", headers={"kid": kid})


def _chat_token_payload(
    *,
    issuer: str = ISSUER,
    audience: str = AUDIENCE,
    expires_at: datetime | None = None,
) -> dict[str, object]:
    now = datetime.now(UTC)
    return {
        "iss": issuer,
        "aud": audience,
        "sub": USER_ID,
        "role": "Viewer",
        "groups": [GROUP_ID],
        "attributes": {},
        "access_scope_hash": "scope-hash",
        "corpus": "published",
        "jti": str(uuid4()),
        "iat": now,
        "exp": expires_at or now + timedelta(minutes=15),
    }


def _rsa_key_pair() -> RsaKeyPair:
    private_key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    private_key_pem = private_key.private_bytes(
        serialization.Encoding.PEM,
        serialization.PrivateFormat.PKCS8,
        serialization.NoEncryption(),
    ).decode("utf-8")
    public_key_pem = private_key.public_key().public_bytes(
        serialization.Encoding.PEM,
        serialization.PublicFormat.SubjectPublicKeyInfo,
    ).decode("utf-8")
    return RsaKeyPair(private_key_pem=private_key_pem, public_key_pem=public_key_pem)


class RsaKeyPair:
    def __init__(self, *, private_key_pem: str, public_key_pem: str) -> None:
        self.private_key_pem = private_key_pem
        self.public_key_pem = public_key_pem
