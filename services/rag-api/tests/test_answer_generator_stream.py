from __future__ import annotations

from collections.abc import AsyncIterator
from dataclasses import dataclass, field
from uuid import UUID

import pytest

from advanced_rag.providers.base import (
    ChatCompletionDelta,
    ChatCompletionRequest,
    ChatUsage,
    ImageInput,
)
from advanced_rag.rag.answer_generator import (
    AnswerGeneration,
    generate_answer_stream,
    generate_multimodal_answer_stream,
)

CHUNK_ID = UUID("11111111-1111-1111-1111-111111111111")
DOCUMENT_ID = UUID("22222222-2222-2222-2222-222222222222")


@dataclass
class _Chunk:
    id: UUID
    document_id: UUID
    content: str
    heading_path: list[str] = field(default_factory=list)


class _FakeStreamingLlm:
    name = "fake"

    def __init__(self, deltas: list[ChatCompletionDelta]) -> None:
        self._deltas = deltas

    async def chat_stream(self, req: ChatCompletionRequest) -> AsyncIterator[ChatCompletionDelta]:
        for delta in self._deltas:
            yield delta

    async def chat_complete(self, req: ChatCompletionRequest) -> tuple[str, ChatUsage]:
        raise NotImplementedError


@pytest.mark.asyncio
async def test_generate_answer_stream_yields_deltas_then_generation() -> None:
    payload = f'{{"answer": "Hola mundo", "cited_chunk_ids": ["{CHUNK_ID}"]}}'
    third = len(payload) // 3
    deltas = [
        ChatCompletionDelta(content=payload[:third]),
        ChatCompletionDelta(content=payload[third : 2 * third]),
        ChatCompletionDelta(content=payload[2 * third :], finish_reason="stop"),
        ChatCompletionDelta(usage=ChatUsage(input_tokens=12, output_tokens=5)),
    ]
    llm = _FakeStreamingLlm(deltas)

    text_parts: list[str] = []
    final: AnswerGeneration | None = None
    async for item in generate_answer_stream(
        llm=llm,
        question="What is the rule?",
        chunks=[_Chunk(id=CHUNK_ID, document_id=DOCUMENT_ID, content="Hola mundo.")],
        locale="en-US",
        model="fake-model",
    ):
        if isinstance(item, str):
            text_parts.append(item)
        else:
            final = item

    assert "".join(text_parts) == "Hola mundo"
    assert final is not None
    assert final.answer == "Hola mundo"
    assert final.cited_chunk_ids == [CHUNK_ID]
    assert final.usage.input_tokens == 12
    assert final.usage.output_tokens == 5


class _FakeMultimodalStreamingLlm:
    name = "fake"

    def __init__(self, deltas: list[ChatCompletionDelta]) -> None:
        self._deltas = deltas
        self.multimodal_calls: list[list[ImageInput]] = []

    async def multimodal_stream(
        self, req: ChatCompletionRequest, images: list[ImageInput]
    ) -> AsyncIterator[ChatCompletionDelta]:
        self.multimodal_calls.append(list(images))
        for delta in self._deltas:
            yield delta


@pytest.mark.asyncio
async def test_generate_multimodal_answer_stream_yields_deltas_then_generation() -> None:
    payload = f'{{"answer": "Hola mundo", "cited_chunk_ids": ["{CHUNK_ID}"]}}'
    chunk_size = 5
    deltas = [
        ChatCompletionDelta(content=payload[index : index + chunk_size])
        for index in range(0, len(payload), chunk_size)
    ]
    deltas.append(
        ChatCompletionDelta(finish_reason="stop", usage=ChatUsage(input_tokens=15, output_tokens=7))
    )
    llm = _FakeMultimodalStreamingLlm(deltas)
    images = [ImageInput(data_base64="QQ==", media_type="image/png", detail="low")]

    text_parts: list[str] = []
    final: AnswerGeneration | None = None
    async for item in generate_multimodal_answer_stream(
        llm=llm,
        question="What is the rule?",
        chunks=[_Chunk(id=CHUNK_ID, document_id=DOCUMENT_ID, content="Hola mundo.")],
        images=images,
        locale="en-US",
        model="fake-model",
    ):
        if isinstance(item, str):
            text_parts.append(item)
        else:
            final = item

    # Streams answer characters incrementally (more than one fragment), not one blob.
    assert len(text_parts) > 1
    assert "".join(text_parts) == "Hola mundo"
    assert final is not None
    assert final.answer == "Hola mundo"
    assert final.cited_chunk_ids == [CHUNK_ID]
    assert final.usage.input_tokens == 15
    assert final.usage.output_tokens == 7
    # The selected images are forwarded to the provider's multimodal call.
    assert llm.multimodal_calls == [images]
