from __future__ import annotations

from datetime import UTC, datetime
from uuid import UUID, uuid4

from pydantic import BaseModel, ConfigDict
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker

from advanced_rag.core.config import Settings
from advanced_rag.core.errors import ApiException
from advanced_rag.providers.base import IEmbeddingProvider
from advanced_rag.rag.chunking import CHUNKER_VERSION, chunk_html
from advanced_rag.schemas.indexing import InternalIndexingRequest


class IndexingJobResult(BaseModel):
    model_config = ConfigDict(frozen=True)

    job_id: UUID
    status: str
    chunk_count: int
    error_code: str | None = None
    error_message: str | None = None


class InternalIndexingService:
    """Synchronous (in-request) indexing pipeline.

    v2: consumes `IEmbeddingProvider` rather than the legacy `EmbeddingProvider`.
    The provider knows its own `model` and `dimensions`, so the service no longer
    threads them as call-time arguments. The values are still recorded in the
    audit columns (`embedding_model`, `embedding_dimensions`) via the provider's
    own metadata.
    """

    def __init__(
        self,
        session_factory: async_sessionmaker[AsyncSession],
        embedding_provider: IEmbeddingProvider,
        settings: Settings,
    ) -> None:
        self._session_factory = session_factory
        self._embedding_provider = embedding_provider
        self._settings = settings

    async def create_job(self, request: InternalIndexingRequest) -> IndexingJobResult:
        if request.corpus_mode not in {"published", "preview"}:
            raise ApiException("VALIDATION_FAILED", 400, "Invalid corpus mode.")

        chunks = chunk_html(request.content_html)
        job_id = uuid4()
        embedding_model = self._embedding_provider.model
        embedding_dimensions = self._embedding_provider.dimensions

        async with self._session_factory() as session:
            await self._insert_pending_job(
                session,
                job_id,
                request,
                embedding_dimensions=embedding_dimensions,
            )
            await session.commit()

            if not chunks:
                await self._mark_failed(session, job_id, "INDEXING_NO_CONTENT", "No indexable content.")
                await session.commit()
                return IndexingJobResult(
                    job_id=job_id,
                    status="Failed",
                    chunk_count=0,
                    error_code="INDEXING_NO_CONTENT",
                    error_message="No indexable content.",
                )

            try:
                await session.execute(
                    text(
                        """
                        update rag.indexing_jobs
                        set status = 'Running', attempts = attempts + 1, started_at = :started_at
                        where id = :job_id
                        """
                    ),
                    {"job_id": job_id, "started_at": datetime.now(UTC)},
                )
                embeddings, _usage = await self._embedding_provider.embed(
                    [chunk.content for chunk in chunks]
                )
                if len(embeddings) != len(chunks):
                    raise RuntimeError("Embedding provider returned a mismatched embedding count.")

                # Deactivate prior chunks by (document, corpus): this covers both
                # re-indexing the same version and superseding an older published
                # version, while a preview index never deactivates published chunks.
                await session.execute(
                    text(
                        """
                        update rag.document_chunks
                        set is_active = false
                        where document_id = :document_id
                          and corpus = :corpus
                          and is_active = true
                        """
                    ),
                    {"document_id": request.document_id, "corpus": request.corpus_mode},
                )
                for chunk, embedding in zip(chunks, embeddings, strict=True):
                    await session.execute(
                        text(
                            """
                            insert into rag.document_chunks (
                                id,
                                indexing_job_id,
                                document_id,
                                document_version_id,
                                corpus,
                                chunk_index,
                                heading_path,
                                token_count,
                                char_count,
                                content,
                                content_html,
                                embedding,
                                embedding_model,
                                is_active
                            )
                            values (
                                :id,
                                :indexing_job_id,
                                :document_id,
                                :document_version_id,
                                :corpus,
                                :chunk_index,
                                :heading_path,
                                :token_count,
                                :char_count,
                                :content,
                                :content_html,
                                :embedding,
                                :embedding_model,
                                true
                            )
                            """
                        ),
                        {
                            "id": uuid4(),
                            "indexing_job_id": job_id,
                            "document_id": request.document_id,
                            "document_version_id": request.document_version_id,
                            "corpus": request.corpus_mode,
                            "chunk_index": chunk.chunk_index,
                            "heading_path": chunk.heading_path,
                            "token_count": chunk.token_count,
                            "char_count": chunk.char_count,
                            "content": chunk.content,
                            "content_html": chunk.content_html,
                            "embedding": str(embedding),
                            "embedding_model": embedding_model,
                        },
                    )
                await session.execute(
                    text(
                        """
                        update rag.indexing_jobs
                        set status = 'Succeeded',
                            completed_at = :completed_at,
                            chunk_count = :chunk_count,
                            embedding_model = :embedding_model,
                            embedding_tokens = :embedding_tokens
                        where id = :job_id
                        """
                    ),
                    {
                        "job_id": job_id,
                        "completed_at": datetime.now(UTC),
                        "chunk_count": len(chunks),
                        "embedding_model": embedding_model,
                        "embedding_tokens": sum(chunk.token_count for chunk in chunks),
                    },
                )
                await session.commit()
                return IndexingJobResult(job_id=job_id, status="Succeeded", chunk_count=len(chunks))
            except Exception as exc:
                await session.rollback()
                async with self._session_factory() as failure_session:
                    await self._mark_failed(
                        failure_session,
                        job_id,
                        "INDEXING_FAILED",
                        "Indexing job failed.",
                    )
                    await failure_session.commit()
                return IndexingJobResult(
                    job_id=job_id,
                    status="Failed",
                    chunk_count=0,
                    error_code="INDEXING_FAILED",
                    error_message=str(exc),
                )

    async def _insert_pending_job(
        self,
        session: AsyncSession,
        job_id: UUID,
        request: InternalIndexingRequest,
        *,
        embedding_dimensions: int,
    ) -> None:
        await session.execute(
            text(
                """
                insert into rag.indexing_jobs (
                    id,
                    document_id,
                    document_version_id,
                    corpus,
                    status,
                    attempts,
                    chunker_version,
                    embedding_dimensions
                )
                values (
                    :id,
                    :document_id,
                    :document_version_id,
                    :corpus,
                    'Pending',
                    0,
                    :chunker_version,
                    :embedding_dimensions
                )
                """
            ),
            {
                "id": job_id,
                "document_id": request.document_id,
                "document_version_id": request.document_version_id,
                "corpus": request.corpus_mode,
                "chunker_version": CHUNKER_VERSION,
                "embedding_dimensions": embedding_dimensions,
            },
        )

    async def _mark_failed(
        self,
        session: AsyncSession,
        job_id: UUID,
        code: str,
        message: str,
    ) -> None:
        await session.execute(
            text(
                """
                update rag.indexing_jobs
                set status = 'Failed',
                    completed_at = :completed_at,
                    error_code = :error_code,
                    error_message = :error_message
                where id = :job_id
                """
            ),
            {
                "job_id": job_id,
                "completed_at": datetime.now(UTC),
                "error_code": code,
                "error_message": message,
            },
        )
