from __future__ import annotations

from html.parser import HTMLParser

from pydantic import BaseModel, ConfigDict


CHUNKER_VERSION = 1
TARGET_CHUNK_TOKENS = 500
MAX_CHUNK_TOKENS = 800
OVERLAP_TOKENS = 80


class DocumentChunk(BaseModel):
    model_config = ConfigDict(frozen=True)

    chunk_index: int
    heading_path: list[str]
    token_count: int
    char_count: int
    content: str
    content_html: str


def chunk_html(content_html: str) -> list[DocumentChunk]:
    blocks = _HtmlBlockParser.parse(content_html)
    chunks: list[DocumentChunk] = []
    current_parts: list[str] = []
    current_heading: list[str] = []

    for block in blocks:
        tokens = _tokens(block.text)
        if block.tag in {"h1", "h2", "h3"}:
            current_heading = _next_heading_path(current_heading, block.tag, block.text)

        if not tokens:
            continue

        candidate = " ".join([*current_parts, block.text]).strip()
        if current_parts and len(_tokens(candidate)) > TARGET_CHUNK_TOKENS:
            chunks.append(_create_chunk(len(chunks), current_heading, " ".join(current_parts)))
            current_parts = _overlap_tail(current_parts)

        if len(tokens) > MAX_CHUNK_TOKENS:
            for part in _split_long_text(block.text):
                chunks.append(_create_chunk(len(chunks), current_heading, part))
            current_parts = []
            continue

        current_parts.append(block.text)

    if current_parts:
        chunks.append(_create_chunk(len(chunks), current_heading, " ".join(current_parts)))

    return chunks


def _create_chunk(index: int, heading_path: list[str], content: str) -> DocumentChunk:
    normalized = " ".join(content.split())
    return DocumentChunk(
        chunk_index=index,
        heading_path=heading_path.copy(),
        token_count=len(_tokens(normalized)),
        char_count=len(normalized),
        content=normalized,
        content_html=f"<p>{normalized}</p>",
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
    def __init__(self, tag: str, text: str) -> None:
        self.tag = tag
        self.text = text


class _HtmlBlockParser(HTMLParser):
    _block_tags = {"h1", "h2", "h3", "p", "li", "pre"}

    def __init__(self) -> None:
        super().__init__()
        self._active_tag: str | None = None
        self._buffer: list[str] = []
        self.blocks: list[_Block] = []

    @classmethod
    def parse(cls, html: str) -> list[_Block]:
        parser = cls()
        parser.feed(html)
        parser.close()
        if not parser.blocks:
            text = " ".join(parser._buffer).strip()
            return [_Block("p", text)] if text else []
        return parser.blocks

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag in self._block_tags:
            self._flush()
            self._active_tag = tag

    def handle_endtag(self, tag: str) -> None:
        if tag == self._active_tag:
            self._flush()
            self._active_tag = None

    def handle_data(self, data: str) -> None:
        normalized = data.strip()
        if normalized:
            self._buffer.append(normalized)

    def _flush(self) -> None:
        text = " ".join(self._buffer).strip()
        if text:
            self.blocks.append(_Block(self._active_tag or "p", text))
        self._buffer = []
