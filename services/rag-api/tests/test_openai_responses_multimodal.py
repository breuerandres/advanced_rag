from __future__ import annotations

import asyncio

from advanced_rag.providers.base import ChatCompletionRequest, ChatMessage, ImageInput
from advanced_rag.providers.openai_provider import OpenAILlmProvider


class _FakeUsageDetails:
    cached_tokens = 2


class _FakeUsage:
    input_tokens = 30
    output_tokens = 12
    input_tokens_details = _FakeUsageDetails()


class _FakeResponse:
    output_text = '{"answer": "The switch is red", "cited_chunk_ids": []}'
    usage = _FakeUsage()


class _FakeResponses:
    def __init__(self) -> None:
        self.captured: dict | None = None

    async def create(self, **kwargs: object) -> _FakeResponse:
        self.captured = dict(kwargs)
        return _FakeResponse()


class _FakeClient:
    def __init__(self) -> None:
        self.responses = _FakeResponses()


def test_multimodal_complete_builds_responses_payload_and_maps_usage() -> None:
    provider = OpenAILlmProvider(api_key="x")
    fake = _FakeClient()
    provider._client = fake  # type: ignore[assignment]

    req = ChatCompletionRequest(
        messages=[
            ChatMessage(role="system", content="You are helpful."),
            ChatMessage(role="user", content="Context:\n[chunk]\n\nQuestion:\nWhat color is the switch?"),
        ],
        model="gpt-4.1-nano",
        response_format={"type": "json_object"},
    )
    images = [ImageInput(data_base64="iVBORw0KGgo=", media_type="image/png", detail="low")]

    content, usage = asyncio.run(provider.multimodal_complete(req, images))

    assert content == '{"answer": "The switch is red", "cited_chunk_ids": []}'
    assert usage.input_tokens == 30
    assert usage.output_tokens == 12
    assert usage.cached_input_tokens == 2

    payload = fake.responses.captured
    assert payload is not None
    assert payload["store"] is False
    assert payload["model"] == "gpt-4.1-nano"
    assert payload["instructions"] == "You are helpful."
    assert payload["text"] == {"format": {"type": "json_object"}}

    user_message = payload["input"][0]
    assert user_message["role"] == "user"
    parts = user_message["content"]
    assert parts[0]["type"] == "input_text"
    assert "What color is the switch?" in parts[0]["text"]
    assert parts[1] == {
        "type": "input_image",
        "image_url": "data:image/png;base64,iVBORw0KGgo=",
        "detail": "low",
    }
