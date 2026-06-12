"""Query-time multimodal image selection and authorized byte fetching.

Image candidates are loaded for the FINAL retrieved chunks only, deduplicated by
`image_id` in retrieval order, capped by count, then fetched (with a total-bytes
cap) from the .NET internal document-image endpoint. Any single failure drops that
image rather than failing the chat request.
"""

from __future__ import annotations

from uuid import UUID

import httpx
from pydantic import BaseModel, ConfigDict
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession


ALLOWED_IMAGE_CONTENT_TYPES = {
    "image/png",
    "image/jpeg",
    "image/jpg",
    "image/webp",
    "image/gif",
}


class ChunkImageCandidate(BaseModel):
    model_config = ConfigDict(frozen=True)

    chunk_id: UUID
    document_id: UUID
    document_version_id: UUID
    image_id: UUID
    ordinal: int


class SelectedMultimodalImage(BaseModel):
    model_config = ConfigDict(frozen=True)

    image_id: UUID
    content_type: str
    bytes_data: bytes
    byte_count: int
    detail: str


async def load_image_candidates(
    session: AsyncSession, chunk_ids: list[UUID]
) -> list[ChunkImageCandidate]:
    """Load image references for the given chunks, in chunk then image order."""
    if not chunk_ids:
        return []
    result = await session.execute(
        text(
            """
            select chunk_id, document_id, document_version_id, image_id, ordinal
            from rag.document_chunk_images
            where chunk_id = any(cast(:chunk_ids as uuid[]))
            order by array_position(cast(:chunk_ids as uuid[]), chunk_id), ordinal
            """
        ),
        {"chunk_ids": [str(chunk_id) for chunk_id in chunk_ids]},
    )
    return [
        ChunkImageCandidate(
            chunk_id=row.chunk_id,
            document_id=row.document_id,
            document_version_id=row.document_version_id,
            image_id=row.image_id,
            ordinal=int(row.ordinal),
        )
        for row in result
    ]


def select_image_candidates(
    candidates: list[ChunkImageCandidate],
    *,
    max_images: int,
) -> list[ChunkImageCandidate]:
    """Deduplicate by image_id in retrieval order and cap to `max_images`."""
    selected: list[ChunkImageCandidate] = []
    seen: set[UUID] = set()
    for candidate in candidates:
        if candidate.image_id in seen:
            continue
        selected.append(candidate)
        seen.add(candidate.image_id)
        if len(selected) >= max_images:
            break
    return selected


async def fetch_selected_images(
    candidates: list[ChunkImageCandidate],
    *,
    user_id: UUID,
    roles: list[str],
    internal_token: str,
    base_url: str,
    max_total_bytes: int,
    detail: str,
    client: httpx.AsyncClient | None = None,
) -> list[SelectedMultimodalImage]:
    """Fetch authorized image bytes from .NET, enforcing the total-bytes cap.

    Non-200 responses, unsupported content types, and images that would exceed the
    total-bytes cap are skipped. The caller treats an empty result as "text-only".
    """
    owns_client = client is None
    http = client or httpx.AsyncClient(timeout=10)
    selected: list[SelectedMultimodalImage] = []
    total_bytes = 0
    try:
        for candidate in candidates:
            url = f"{base_url.rstrip('/')}/internal/document-images/{candidate.image_id}/content"
            params: list[tuple[str, str | int | float | bool | None]] = [
                ("userId", str(user_id)),
                *[("roles", role) for role in roles],
            ]
            try:
                response = await http.get(
                    url,
                    params=params,
                    headers={"X-Internal-Service-Token": internal_token},
                )
            except httpx.HTTPError:
                continue
            if response.status_code != 200:
                continue
            content_type = response.headers.get("content-type", "").split(";", 1)[0].strip().lower()
            if content_type not in ALLOWED_IMAGE_CONTENT_TYPES:
                continue
            byte_count = len(response.content)
            if total_bytes + byte_count > max_total_bytes:
                continue
            selected.append(
                SelectedMultimodalImage(
                    image_id=candidate.image_id,
                    content_type=content_type,
                    bytes_data=response.content,
                    byte_count=byte_count,
                    detail=detail,
                )
            )
            total_bytes += byte_count
    finally:
        if owns_client:
            await http.aclose()
    return selected
