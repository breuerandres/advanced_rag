from __future__ import annotations

import asyncio
from collections.abc import AsyncIterator
from typing import Any

from advanced_rag.providers.base import ChatCompletionRequest, ChatMessage, ImageInput
from advanced_rag.providers.openai_provider import OpenAILlmProvider

ANSWER_PAYLOAD = '{"answer": "The switch is red", "cited_chunk_ids": []}'


class _FakeDeltaEvent:
    type = "response.output_text.delta"

    def __init__(self, delta: str) -> None:
        self.delta = delta


class _FakeUsageDetails:
    cached_tokens = 2


class _FakeUsage:
    input_tokens = 30
    output_tokens = 12
    input_tokens_details = _FakeUsageDetails()


class _FakeResponseObj:
    usage = _FakeUsage()


class _FakeCompletedEvent:
    type = "response.completed"
    response = _FakeResponseObj()


async def _event_stream(events: list[Any]) -> AsyncIterator[Any]:
    for event in events:
        yield event


class _FakeResponses:
    def __init__(self, events: list[Any]) -> None:
        self.captured: dict | None = None
        self._events = events

    async def create(self, **kwargs: object) -> AsyncIterator[Any]:
        self.captured = dict(kwargs)
        return _event_stream(self._events)


class _FakeClient:
    def __init__(self, events: list[Any]) -> None:
        self.responses = _FakeResponses(events)


def test_multimodal_stream_builds_responses_payload_and_maps_usage() -> None:
    provider = OpenAILlmProvider(api_key="x")
    # Split the JSON payload across several text deltas, then a completed event.
    deltas = [
        _FakeDeltaEvent(ANSWER_PAYLOAD[index : index + 10])
        for index in range(0, len(ANSWER_PAYLOAD), 10)
    ]
    fake = _FakeClient([*deltas, _FakeCompletedEvent()])
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

    async def _consume() -> tuple[list[str], Any]:
        parts: list[str] = []
        usage = None
        async for delta in provider.multimodal_stream(req, images):
            if delta.content:
                parts.append(delta.content)
            if delta.usage is not None:
                usage = delta.usage
        return parts, usage

    parts, usage = asyncio.run(_consume())

    # The content arrives as multiple deltas (true streaming), not one blob.
    assert len(parts) > 1
    assert "".join(parts) == ANSWER_PAYLOAD
    assert usage is not None
    assert usage.input_tokens == 30
    assert usage.output_tokens == 12
    assert usage.cached_input_tokens == 2

    payload = fake.responses.captured
    assert payload is not None
    assert payload["stream"] is True
    assert payload["store"] is False
    assert payload["model"] == "gpt-4.1-nano"
    assert payload["instructions"] == "You are helpful."
    assert payload["text"] == {"format": {"type": "json_object"}}

    user_message = payload["input"][0]
    assert user_message["role"] == "user"
    parts_payload = user_message["content"]
    assert parts_payload[0]["type"] == "input_text"
    assert "What color is the switch?" in parts_payload[0]["text"]
    assert parts_payload[1] == {
        "type": "input_image",
        "image_url": "data:image/png;base64,iVBORw0KGgo=",
        "detail": "low",
    }
