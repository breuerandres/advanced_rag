from __future__ import annotations

import re
from html import escape
from html.parser import HTMLParser
from uuid import UUID

from pydantic import BaseModel, ConfigDict


CHUNKER_VERSION = 1
TARGET_CHUNK_TOKENS = 500
MAX_CHUNK_TOKENS = 800
OVERLAP_TOKENS = 80


# Same-origin stable document-image reference, mirroring .NET's
# `StableDocumentImageSourcePattern`. Only these are indexable as multimodal
# candidates; external URLs and data: URIs are ignored.
STABLE_IMAGE_SRC_PATTERN = re.compile(
    r"^/api/document-images/(?P<image_id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}"
    r"-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/content$"
)


class ChunkImageRef(BaseModel):
    model_config = ConfigDict(frozen=True)

    image_id: UUID
    ordinal: int  # 0-based order of appearance within the chunk
    alt_text: str | None = None


class DocumentChunk(BaseModel):
    model_config = ConfigDict(frozen=True)

    chunk_index: int
    heading_path: list[str]
    token_count: int
    char_count: int
    content: str
    content_html: str
    images: list[ChunkImageRef] = []


def chunk_html(content_html: str) -> list[DocumentChunk]:
    blocks = _HtmlBlockParser.parse(content_html)
    chunks: list[DocumentChunk] = []
    current_parts: list[str] = []
    current_images: list[tuple[UUID, str | None]] = []
    current_heading: list[str] = []

    for block in blocks:
        tokens = _tokens(block.text)
        if block.tag in {"h1", "h2", "h3"}:
            current_heading = _next_heading_path(current_heading, block.tag, block.text)

        if not tokens:
            # The block has no indexable text (e.g. an image with no alt text); still
            # carry any image references forward so they attach to the next chunk.
            current_images.extend(block.images)
            continue

        candidate = " ".join([*current_parts, block.text]).strip()
        if current_parts and len(_tokens(candidate)) > TARGET_CHUNK_TOKENS:
            chunks.append(
                _create_chunk(len(chunks), current_heading, " ".join(current_parts), current_images)
            )
            current_parts = _overlap_tail(current_parts)
            current_images = []  # overlap tails carry no images (avoid duplication)

        if len(tokens) > MAX_CHUNK_TOKENS:
            split_parts = _split_long_text(block.text)
            for offset, part in enumerate(split_parts):
                # The block's images attach only to the first chunk produced from it.
                part_images = current_images + block.images if offset == 0 else []
                chunks.append(_create_chunk(len(chunks), current_heading, part, part_images))
            current_parts = []
            current_images = []
            continue

        current_parts.append(block.text)
        current_images.extend(block.images)

    if current_parts:
        chunks.append(
            _create_chunk(len(chunks), current_heading, " ".join(current_parts), current_images)
        )

    return chunks


def _create_chunk(
    index: int,
    heading_path: list[str],
    content: str,
    images: list[tuple[UUID, str | None]] | None = None,
) -> DocumentChunk:
    normalized = " ".join(content.split())
    image_refs = [
        ChunkImageRef(image_id=image_id, ordinal=ordinal, alt_text=alt_text)
        for ordinal, (image_id, alt_text) in enumerate(images or [])
    ]
    return DocumentChunk(
        chunk_index=index,
        heading_path=heading_path.copy(),
        token_count=len(_tokens(normalized)),
        char_count=len(normalized),
        content=normalized,
        content_html=f"<p>{escape(normalized)}</p>",
        images=image_refs,
    )


def _split_long_text(text: str) -> list[str]:
    tokens = _tokens(text)
    parts: list[str] = []
    start = 0
    while start < len(tokens):
        parts.append(" ".join(tokens[start : start + MAX_CHUNK_TOKENS]))
        start += MAX_CHUNK_TOKENS - OVERLAP_TOKENS
    return parts


def _overlap_tail(parts: list[str]) -> list[str]:
    tokens = _tokens(" ".join(parts))
    if not tokens:
        return []
    return [" ".join(tokens[-OVERLAP_TOKENS:])]


def _tokens(text: str) -> list[str]:
    return [token for token in text.split() if token]


def _next_heading_path(current: list[str], tag: str, text: str) -> list[str]:
    level = int(tag[1])
    return [*current[: level - 1], text]


class _Block:
    def __init__(
        self,
        tag: str,
        text: str,
        images: list[tuple[UUID, str | None]] | None = None,
    ) -> None:
        self.tag = tag
        self.text = text
        self.images = images or []


class _HtmlBlockParser(HTMLParser):
    _block_tags = {"h1", "h2", "h3", "p", "li", "pre", "figure", "figcaption"}

    def __init__(self) -> None:
        super().__init__()
        self._active_tag: str | None = None
        self._buffer: list[str] = []
        self._image_buffer: list[tuple[UUID, str | None]] = []
        self.blocks: list[_Block] = []

    @classmethod
    def parse(cls, html: str) -> list[_Block]:
        parser = cls()
        parser.feed(html)
        parser.close()
        parser._flush()
        return parser.blocks

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag in self._block_tags:
            self._flush()
            self._active_tag = tag
        if tag == "img":
            image_text = _image_text(attrs)
            if image_text:
                self._buffer.append(image_text)
            self._record_image_reference(attrs)

    def handle_endtag(self, tag: str) -> None:
        if tag == self._active_tag:
            self._flush()
            self._active_tag = None

    def handle_data(self, data: str) -> None:
        normalized = data.strip()
        if normalized:
            self._buffer.append(normalized)

    def _record_image_reference(self, attrs: list[tuple[str, str | None]]) -> None:
        attributes = {key.lower(): value for key, value in attrs if value is not None}
        match = STABLE_IMAGE_SRC_PATTERN.match((attributes.get("src") or "").strip())
        if match is None:
            return
        alt_raw = attributes.get("alt")
        alt_text = " ".join(alt_raw.split()) if alt_raw and alt_raw.strip() else None
        self._image_buffer.append((UUID(match.group("image_id")), alt_text))

    def _flush(self) -> None:
        text = " ".join(self._buffer).strip()
        if text or self._image_buffer:
            self.blocks.append(_Block(self._active_tag or "p", text, self._image_buffer))
        self._buffer = []
        self._image_buffer = []


def _image_text(attrs: list[tuple[str, str | None]]) -> str:
    attributes = {
        key.lower(): " ".join(value.split())
        for key, value in attrs
        if value is not None and value.strip()
    }
    for key in ("alt", "aria-label", "title"):
        if key in attributes:
            return attributes[key]
    return ""
