from __future__ import annotations

from uuid import UUID

from advanced_rag.rag.chunking import MAX_CHUNK_TOKENS, chunk_html


def test_chunk_html_indexes_image_alt_text_and_caption_without_urls() -> None:
    html = """
    <h1>Evacuation</h1>
    <figure>
      <img
        src="/api/document-images/11111111-1111-1111-1111-111111111111/content"
        alt="Extinguisher beside the evacuation map"
      />
      <figcaption>Lobby emergency station.</figcaption>
    </figure>
    <p>Follow marked exits.</p>
    """

    content = " ".join(chunk.content for chunk in chunk_html(html))

    assert "Extinguisher beside the evacuation map" in content
    assert "Lobby emergency station." in content
    assert "/api/document-images" not in content


def test_chunk_html_extracts_ordered_stable_image_references() -> None:
    html = (
        "<p>"
        '<img src="/api/document-images/11111111-1111-1111-1111-111111111111/content"'
        ' alt="First panel" />'
        '<img src="/api/document-images/22222222-2222-2222-2222-222222222222/content"'
        ' alt="Second panel" />'
        "</p>"
    )

    chunks = chunk_html(html)

    assert len(chunks) == 1
    images = chunks[0].images
    assert [image.image_id for image in images] == [
        UUID("11111111-1111-1111-1111-111111111111"),
        UUID("22222222-2222-2222-2222-222222222222"),
    ]
    assert [image.ordinal for image in images] == [0, 1]
    assert [image.alt_text for image in images] == ["First panel", "Second panel"]


def test_chunk_html_ignores_non_document_image_sources() -> None:
    html = (
        "<p>"
        '<img src="https://example.com/raw.png" alt="External image" />'
        '<img src="data:image/png;base64,AAAA" alt="Inline image" />'
        "</p>"
    )

    chunks = chunk_html(html)

    assert chunks
    assert chunks[0].images == []


def test_chunk_html_attaches_oversized_block_images_to_first_chunk_only() -> None:
    long_text = " ".join(f"word{n}" for n in range(MAX_CHUNK_TOKENS + 200))
    html = (
        "<p>"
        '<img src="/api/document-images/33333333-3333-3333-3333-333333333333/content"'
        ' alt="Diagram" />'
        f" {long_text}"
        "</p>"
    )

    chunks = chunk_html(html)

    assert len(chunks) > 1
    assert [image.image_id for image in chunks[0].images] == [
        UUID("33333333-3333-3333-3333-333333333333")
    ]
    for chunk in chunks[1:]:
        assert chunk.images == []


def test_chunk_html_uses_accessible_image_text_when_alt_is_missing() -> None:
    html = """
    <p>
      <img
        src="/api/document-images/22222222-2222-2222-2222-222222222222/content"
        title="Floor plan diagram"
      />
      <img
        src="/api/document-images/33333333-3333-3333-3333-333333333333/content"
        aria-label="Safety valve photo"
      />
    </p>
    """

    content = " ".join(chunk.content for chunk in chunk_html(html))

    assert "Floor plan diagram" in content
    assert "Safety valve photo" in content
    assert "/api/document-images" not in content
