from __future__ import annotations

from advanced_rag.rag.chunking import chunk_html


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
