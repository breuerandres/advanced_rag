from __future__ import annotations

import asyncio
from uuid import UUID, uuid4

import httpx

from advanced_rag.rag.multimodal_images import (
    ChunkImageCandidate,
    fetch_selected_images,
    select_image_candidates,
)


def _candidate(image_id: UUID, ordinal: int = 0) -> ChunkImageCandidate:
    return ChunkImageCandidate(
        chunk_id=uuid4(),
        document_id=uuid4(),
        document_version_id=uuid4(),
        image_id=image_id,
        ordinal=ordinal,
    )


def test_select_image_references_deduplicates_and_caps_by_order() -> None:
    first, second, third, fourth = (uuid4() for _ in range(4))
    candidates = [
        _candidate(first, 0),
        _candidate(second, 1),
        _candidate(first, 2),  # duplicate of `first`
        _candidate(third, 3),
        _candidate(fourth, 4),
    ]

    selected = select_image_candidates(candidates, max_images=3)

    assert [candidate.image_id for candidate in selected] == [first, second, third]


def test_fetch_selected_images_skips_failures_and_enforces_total_bytes() -> None:
    ok_first, missing, oversized, ok_second = (uuid4() for _ in range(4))
    small_image = b"\x89PNG" + b"0" * 96  # 100 bytes
    big_image = b"0" * 10_000

    def handler(request: httpx.Request) -> httpx.Response:
        image_id = request.url.path.split("/")[-2]
        if image_id == str(missing):
            return httpx.Response(404)
        if image_id == str(oversized):
            return httpx.Response(200, content=big_image, headers={"content-type": "image/png"})
        return httpx.Response(200, content=small_image, headers={"content-type": "image/png"})

    async def run() -> list[UUID]:
        transport = httpx.MockTransport(handler)
        async with httpx.AsyncClient(transport=transport) as client:
            candidates = [
                _candidate(ok_first),
                _candidate(missing),
                _candidate(oversized),
                _candidate(ok_second),
            ]
            selected = await fetch_selected_images(
                candidates,
                user_id=uuid4(),
                roles=["Viewer"],
                internal_token="token",
                base_url="http://dotnet-api:8080",
                max_total_bytes=500,
                detail="low",
                client=client,
            )
            return selected

    result = asyncio.run(run())

    assert [image.image_id for image in result] == [ok_first, ok_second]
    assert all(image.detail == "low" for image in result)
    assert all(image.content_type == "image/png" for image in result)
    assert all(image.byte_count == len(small_image) for image in result)
