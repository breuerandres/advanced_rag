from __future__ import annotations

import base64
import hashlib
import json
import logging
from collections.abc import AsyncIterator
from datetime import UTC, datetime, timedelta
from decimal import Decimal
from typing import Any, cast
from uuid import UUID, uuid4
from zoneinfo import ZoneInfo

from pydantic import BaseModel, ConfigDict
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker

from advanced_rag.auth.chat_tokens import ChatTokenClaims
from advanced_rag.core.config import Settings
from advanced_rag.core.errors import ApiException
from advanced_rag.core.tenant_config_refresher import TenantConfigRefresher
from advanced_rag.providers.base import (
    ChatUsage,
    IEmbeddingProvider,
    ILlmProvider,
    ImageInput,
    IMultimodalLlmProvider,
    IRerankerProvider,
)
from advanced_rag.rag.answer_generator import (
    AnswerGeneration,
    generate_answer_stream,
    generate_multimodal_answer_stream,
)
from advanced_rag.rag.chunking import CHUNKER_VERSION
from advanced_rag.rag.conversation_memory import condense_question, load_session_history
from advanced_rag.rag.multimodal_images import (
    SelectedMultimodalImage,
    fetch_selected_images,
    load_image_candidates,
    select_image_candidates,
)
from advanced_rag.rag.hybrid_retrieval import (
    HybridCandidate,
    HybridRetrievalParams,
    hybrid_retrieve,
)
from advanced_rag.rag.rerank import rerank_candidates


logger = logging.getLogger(__name__)

PROMPT_VERSION = 1

# Root organizational unit ("Empresa"), seeded by the .NET migration
# 20260608100000_AddHierarchicalAccessBaseline. A document rule scoped to this unit is
# the explicit company-wide rule and matches every user, so retrieval passes it to the
# branch-aware SQL as the root marker.
ROOT_ORGANIZATIONAL_UNIT_ID = UUID("01000000-0000-0000-0000-000000000001")


class RetrievedChunk(BaseModel):
    model_config = ConfigDict(frozen=True)

    id: UUID
    document_id: UUID
    document_version_id: UUID
    heading_path: list[str]
    content: str


class Citation(BaseModel):
    model_config = ConfigDict(frozen=True)

    chunk_id: UUID
    document_id: UUID
    document_version_id: UUID
    heading_path: list[str]


class ChatAnswer(BaseModel):
    model_config = ConfigDict(frozen=True)

    query_audit_event_id: UUID
    answer: str
    citations: list[Citation]
    cache_hit: bool
    cached_at: datetime | None
    input_tokens: int
    cached_tokens: int
    output_tokens: int
    estimated_cost_usd: Decimal


class ChatStreamEvent(BaseModel):
    """One SSE event yielded by `ChatService.answer_stream`.

    `event` is one of `cache-hit | answer-token | citations | usage`; the router wraps it
    with the `request-id`/`done`/`error` envelope. Payloads match the historical SSE
    contract verbatim so frontends need no changes.
    """

    model_config = ConfigDict(frozen=True)

    event: str
    payload: dict[str, Any]


class PricingSnapshot(BaseModel):
    model_config = ConfigDict(frozen=True)

    id: UUID
    input_token_price_usd: Decimal
    cached_token_price_usd: Decimal
    output_token_price_usd: Decimal


class ChatService:
    """Answer chat questions over the indexed corpus.

    Wiring (v2):
      * `embedding_provider`   → embeds the user question for vector retrieval.
      * `llm_provider`         → generates the natural-language answer with citations.
      * `reranker_provider`    → optional cross-encoder over the hybrid candidates;
                                 pass `None` (or set `enable_reranker=False` in the
                                 factory config) to skip the rerank step.

    Retrieval pipeline:
      1. Cache lookup partitioned by `(corpus, access_scope_hash, filters_hash)`.
      2. Hybrid: pgvector kNN + BM25 (`content_tsv`) + trigram fallback, fused with RRF.
      3. Optional cross-encoder rerank → final top-K.
      4. LLM generation via `answer_generator.generate_answer` (provider-agnostic).
      5. Audit row + cache write (only if citations exist).
    """

    def __init__(
        self,
        session_factory: async_sessionmaker[AsyncSession],
        embedding_provider: IEmbeddingProvider,
        llm_provider: ILlmProvider,
        reranker_provider: IRerankerProvider | None,
        settings: Settings,
        hnsw_iterative_scan_supported: bool = False,
        tenant_config_refresher: TenantConfigRefresher | None = None,
    ) -> None:
        self._session_factory = session_factory
        self._embedding_provider = embedding_provider
        self._llm_provider = llm_provider
        self._reranker_provider = reranker_provider
        self._settings = settings
        self._tenant_config_refresher = tenant_config_refresher
        # Set to True only when the server's pgvector advertised >= 0.8 at startup.
        # When False the retrieval SQL relies on `ef_search` alone.
        self._hnsw_iterative_scan_supported = hnsw_iterative_scan_supported

    def set_hnsw_iterative_scan_supported(self, supported: bool) -> None:
        """Update the pgvector iterative-scan capability after the startup probe.

        The probe is async and runs once the database is reachable, so the service is
        constructed with the conservative default and upgraded here.
        """
        self._hnsw_iterative_scan_supported = supported

    async def precheck(self, *, question: str, claims: ChatTokenClaims) -> None:
        """Validate the question and enforce the budget BEFORE streaming starts.

        Pre-stream failures (empty question, over-budget) must surface as a normal JSON
        error envelope, not a mid-stream SSE `error` event, and must happen before any
        paid provider call. The router awaits this before constructing the
        `StreamingResponse`; `answer_stream` re-checks defensively.
        """
        if not question.strip():
            raise ApiException(
                "VALIDATION_FAILED",
                400,
                "Question is required.",
                details={"field": "question"},
            )
        async with self._session_factory() as session:
            if self._tenant_config_refresher is not None:
                await self._tenant_config_refresher.refresh_if_stale(session)
            max_chars = self._settings.chat_max_question_chars
            if max_chars > 0 and len(question) > max_chars:
                raise ApiException(
                    "CHAT_QUESTION_TOO_LONG",
                    400,
                    "Question exceeds the configured maximum length.",
                    details={"field": "question", "maxChars": max_chars},
                )
            await self._ensure_pricing_configured(session)
            await self._enforce_budget(session, UUID(claims.user_id), datetime.now(UTC))

    async def answer_stream(
        self,
        *,
        question: str,
        claims: ChatTokenClaims,
        request_id: str,
        filters: list[UUID] | None = None,
        session_id: UUID | None = None,
        locale: str | None = None,
        scope_document_id: UUID | None = None,
    ) -> AsyncIterator[ChatStreamEvent]:
        """Answer a chat question, streaming answer tokens as the model produces them.

        Yields typed `ChatStreamEvent`s (`cache-hit` | `answer-token` | `citations` |
        `usage`); the router wraps them with `request-id`/`done`/`error`. Retrieval-
        grounded answers stream character deltas through the shared JSON parser — both
        the text path (`generate_answer_stream`) and the multimodal path
        (`generate_multimodal_answer_stream`); only cache hits and no-results answers
        emit a single whole-answer token. The audit row is written after generation
        completes, preserving exact usage and the end-to-end latency.
        """
        normalized_question = question.strip()
        if not normalized_question:
            raise ApiException(
                "VALIDATION_FAILED",
                400,
                "Question is required.",
                details={"field": "question"},
            )
        if scope_document_id is not None:
            # Doc-scoped mini chat always answers from published content, regardless of
            # role, preserving the "public chat retrieves only Published content" invariant.
            corpus = "published"
        else:
            corpus = "published" if claims.role == "Viewer" else claims.corpus
        active_locale = locale or self._settings.default_locale
        filters_hash = _compute_filters_hash(filters)
        started_at = datetime.now(UTC)

        async with self._session_factory() as session:
            await self._ensure_pricing_configured(session)
            await self._enforce_budget(session, UUID(claims.user_id), started_at)
            retrieval_question = normalized_question
            rewritten_question: str | None = None
            previous_event_id: UUID | None = None
            if session_id is not None:
                connection = await session.connection()
                history = await load_session_history(
                    connection,
                    session_id=session_id,
                    user_id=UUID(claims.user_id),
                    limit=self._settings.conversation_history_turns,
                )
                if history:
                    previous_event_id = UUID(str(history[-1]["id"]))
                    condensed = await condense_question(
                        llm=self._llm_provider,
                        history=history,
                        new_question=normalized_question,
                        locale=active_locale,
                        condenser_model=self._settings.resolved_chat_model,
                    )
                    retrieval_question = condensed.strip() or normalized_question
                    if retrieval_question != normalized_question:
                        rewritten_question = retrieval_question

            question_embedding = await self._embed_question(retrieval_question)

            cache_hit = None
            if scope_document_id is None:
                cache_hit = await self._lookup_cache(
                    session,
                    corpus=corpus,
                    access_scope_hash=claims.access_scope_hash,
                    filters_hash=filters_hash,
                    question_embedding=question_embedding,
                )
            if cache_hit is not None:
                cached_answer = await self._audit_cache_hit(
                    session,
                    cache_hit=cache_hit,
                    question=normalized_question,
                    rewritten_question=rewritten_question,
                    claims=claims,
                    corpus=corpus,
                    filters=filters,
                    filters_hash=filters_hash,
                    session_id=session_id,
                    previous_event_id=previous_event_id,
                    request_id=request_id,
                    latency_ms=_latency_ms(started_at),
                )
                await session.commit()
                yield ChatStreamEvent(
                    event="cache-hit",
                    payload={
                        "cached_at": (
                            cached_answer.cached_at.isoformat()
                            if cached_answer.cached_at
                            else None
                        )
                    },
                )
                yield ChatStreamEvent(event="answer-token", payload={"delta": cached_answer.answer})
                yield ChatStreamEvent(
                    event="citations",
                    payload={
                        "query_audit_event_id": str(cached_answer.query_audit_event_id),
                        "citations": _citations_payload(cached_answer.citations),
                    },
                )
                yield ChatStreamEvent(
                    event="usage",
                    payload={
                        "input_tokens": 0,
                        "cached_tokens": 0,
                        "output_tokens": 0,
                        "cost_usd": 0.0,
                    },
                )
                return

            chunks, rerank_audit = await self._retrieve_chunks(
                session,
                corpus=corpus,
                claims=claims,
                question=retrieval_question,
                question_embedding=question_embedding,
                filters=filters,
                scope_document_id=scope_document_id,
            )

            embedding_tokens = _estimate_tokens(retrieval_question)
            selected_images: list[SelectedMultimodalImage] = []
            if not chunks:
                completion = AnswerGeneration(
                    answer=(
                        _no_results_message_scoped(active_locale)
                        if scope_document_id is not None
                        else _no_results_message(active_locale)
                    ),
                    cited_chunk_ids=[],
                    usage=_zero_usage(),
                )
                input_tokens = embedding_tokens
                output_tokens = _estimate_tokens(completion.answer)
                yield ChatStreamEvent(event="answer-token", payload={"delta": completion.answer})
            else:
                # Query-time multimodal: select authorized images from the final
                # retrieved chunks. Both the multimodal and the text path stream answer
                # tokens through the same JSON parser, so the SSE forwarding below is shared.
                selected_images = await self._select_multimodal_images(session, chunks, claims)
                if selected_images:
                    answer_deltas = generate_multimodal_answer_stream(
                        llm=cast(IMultimodalLlmProvider, self._llm_provider),
                        question=retrieval_question,
                        chunks=chunks,
                        images=[_to_image_input(image) for image in selected_images],
                        locale=active_locale,
                        model=self._settings.resolved_chat_model,
                        temperature=self._settings.openai_chat_temperature,
                        max_tokens=self._settings.openai_chat_max_tokens,
                    )
                else:
                    answer_deltas = generate_answer_stream(
                        llm=self._llm_provider,
                        question=retrieval_question,
                        chunks=chunks,
                        locale=active_locale,
                        model=self._settings.resolved_chat_model,
                        temperature=self._settings.openai_chat_temperature,
                        max_tokens=self._settings.openai_chat_max_tokens,
                    )
                final_generation: AnswerGeneration | None = None
                try:
                    async for item in answer_deltas:
                        if isinstance(item, str):
                            yield ChatStreamEvent(event="answer-token", payload={"delta": item})
                        else:
                            final_generation = item
                except Exception as exc:  # noqa: BLE001 - surface as SSE error event
                    logger.warning(
                        "Answer streaming failed for request %s.", request_id, exc_info=True
                    )
                    raise ApiException(
                        "RAG_PROVIDER_UNAVAILABLE",
                        503,
                        "The answer provider is currently unavailable.",
                    ) from exc
                completion = final_generation or AnswerGeneration(
                    answer="", cited_chunk_ids=[], usage=ChatUsage()
                )
                input_tokens = (completion.usage.input_tokens or 0) + embedding_tokens
                output_tokens = completion.usage.output_tokens or _estimate_tokens(
                    completion.answer
                )
            multimodal_used = bool(selected_images)

            citations = [
                Citation(
                    chunk_id=chunk.id,
                    document_id=chunk.document_id,
                    document_version_id=chunk.document_version_id,
                    heading_path=chunk.heading_path,
                )
                for chunk in chunks
                if chunk.id in set(completion.cited_chunk_ids)
            ]
            embedding_pricing = await self._active_pricing(
                session, self._settings.resolved_embedding_model, "embedding"
            )
            chat_pricing = await self._active_pricing(
                session, self._settings.resolved_chat_model, "chat"
            )
            estimated_cost = _calculate_cost(
                embedding_tokens=embedding_tokens,
                input_tokens=completion.usage.input_tokens or 0,
                output_tokens=output_tokens,
                embedding_pricing=embedding_pricing,
                chat_pricing=chat_pricing,
            )
            audit_id = uuid4()
            await self._insert_audit(
                session,
                audit_id=audit_id,
                user_id=UUID(claims.user_id),
                request_id=request_id,
                question=normalized_question,
                rewritten_question=rewritten_question,
                answer=completion.answer,
                cache_hit=False,
                cached_at=None,
                input_tokens=input_tokens,
                cached_tokens=completion.usage.cached_input_tokens or 0,
                output_tokens=output_tokens,
                pricing_snapshot_id=chat_pricing.id,
                estimated_cost=estimated_cost,
                latency_ms=_latency_ms(started_at),
                access_scope_hash=claims.access_scope_hash,
                corpus=corpus,
                session_id=session_id,
                previous_event_id=previous_event_id,
                filters=filters,
                filters_hash=filters_hash,
                rerank_audit=rerank_audit,
                scope_document_id=scope_document_id,
                multimodal_used=multimodal_used,
                multimodal_image_count=len(selected_images),
                multimodal_image_detail=(
                    self._settings.multimodal_image_detail if selected_images else None
                ),
                multimodal_image_bytes_total=sum(image.byte_count for image in selected_images),
                multimodal_image_ids=(
                    [str(image.image_id) for image in selected_images] or None
                ),
            )
            await self._insert_citations(session, audit_id, citations)
            if citations and scope_document_id is None and not multimodal_used:
                await self._write_cache(
                    session,
                    audit_id=audit_id,
                    corpus=corpus,
                    access_scope_hash=claims.access_scope_hash,
                    filters_hash=filters_hash,
                    question=retrieval_question,
                    answer=completion.answer,
                    question_embedding=question_embedding,
                    citations=citations,
                )
            await session.commit()

            yield ChatStreamEvent(
                event="citations",
                payload={
                    "query_audit_event_id": str(audit_id),
                    "citations": _citations_payload(citations),
                },
            )
            yield ChatStreamEvent(
                event="usage",
                payload={
                    "input_tokens": input_tokens,
                    "cached_tokens": completion.usage.cached_input_tokens or 0,
                    "output_tokens": output_tokens,
                    "cost_usd": float(estimated_cost),
                },
            )

    async def invalidate_sources(self, document_ids: list[UUID]) -> int:
        if not document_ids:
            return 0
        async with self._session_factory() as session:
            result = await session.execute(
                text(
                    """
                    delete from rag.semantic_cache_entries entry
                    using rag.semantic_cache_sources source
                    where source.cache_entry_id = entry.id
                      and source.document_id = any(:document_ids)
                    """
                ),
                {"document_ids": document_ids},
            )
            await session.commit()
            return int(getattr(result, "rowcount", 0) or 0)

    async def list_sessions(self, *, user_id: UUID, limit: int = 30) -> list[dict[str, Any]]:
        async with self._session_factory() as session:
            result = await session.execute(
                text(
                    """
                    with ranked as (
                        select
                            session_id,
                            question,
                            answer,
                            created_at,
                            row_number() over (
                                partition by session_id
                                order by created_at asc, id asc
                            ) as first_rank,
                            row_number() over (
                                partition by session_id
                                order by created_at desc, id desc
                            ) as last_rank,
                            count(*) over (partition by session_id) as turn_count
                        from rag.query_audit_events
                        where user_id = :user_id
                          and session_id is not null
                          and scope_document_id is null
                    )
                    select
                        session_id,
                        max(question) filter (where first_rank = 1) as title,
                        max(question) filter (where last_rank = 1) as last_question,
                        max(answer) filter (where last_rank = 1) as last_answer,
                        max(created_at) as last_activity_at,
                        max(turn_count) as turn_count
                    from ranked
                    group by session_id
                    order by last_activity_at desc
                    limit :limit
                    """
                ),
                {"user_id": user_id, "limit": limit},
            )
            return [dict(row._mapping) for row in result]

    async def get_session_history(
        self,
        *,
        user_id: UUID,
        session_id: UUID,
    ) -> list[dict[str, Any]]:
        async with self._session_factory() as session:
            result = await session.execute(
                text(
                    """
                    select
                        id,
                        question,
                        answer,
                        created_at,
                        cache_hit,
                        feedback_value,
                        feedback_comment
                    from rag.query_audit_events
                    where user_id = :user_id
                      and session_id = :session_id
                    order by created_at asc, id asc
                    """
                ),
                {"user_id": user_id, "session_id": session_id},
            )
            turns = [dict(row._mapping) for row in result]
            if not turns:
                raise ApiException(
                    "CHAT_SESSION_NOT_FOUND",
                    404,
                    "Chat session was not found.",
                )

            event_ids = [turn["id"] for turn in turns]
            citation_result = await session.execute(
                text(
                    """
                    select
                        query_audit_event_id,
                        chunk_id,
                        document_id,
                        document_version_id,
                        heading_path
                    from rag.query_audit_citations
                    where query_audit_event_id = any(:event_ids)
                    order by created_at asc, id asc
                    """
                ),
                {"event_ids": event_ids},
            )
            citations_by_event: dict[UUID, list[dict[str, Any]]] = {}
            for row in citation_result:
                row_dict = dict(row._mapping)
                citations_by_event.setdefault(row_dict["query_audit_event_id"], []).append(row_dict)

            for turn in turns:
                turn["citations"] = citations_by_event.get(turn["id"], [])
            return turns

    async def _embed_question(self, question: str) -> list[float]:
        vectors, _usage = await self._embedding_provider.embed([question])
        if not vectors:
            raise ApiException(
                "RAG_PROVIDER_MISCONFIGURED",
                500,
                "Embedding provider returned no vectors.",
            )
        return vectors[0]

    async def _select_multimodal_images(
        self,
        session: AsyncSession,
        chunks: list[RetrievedChunk],
        claims: ChatTokenClaims,
    ) -> list[SelectedMultimodalImage]:
        """Select and fetch authorized images for the final retrieved chunks.

        Returns an empty list (text-only path) when multimodal is disabled, the
        provider has no `multimodal_stream`, no chunk has images, or every fetch
        is dropped (auth/size/transport). A single fetch failure never fails chat.
        """
        if not (self._settings.multimodal_enabled and chunks):
            return []
        if getattr(self._llm_provider, "multimodal_stream", None) is None:
            return []
        candidates = await load_image_candidates(session, [chunk.id for chunk in chunks])
        selected_candidates = select_image_candidates(
            candidates, max_images=self._settings.multimodal_max_images
        )
        if not selected_candidates:
            return []
        return await fetch_selected_images(
            selected_candidates,
            user_id=UUID(claims.user_id),
            roles=[claims.role],
            internal_token=self._settings.resolved_internal_service_token,
            base_url=self._settings.dotnet_internal_base_url,
            max_total_bytes=self._settings.multimodal_max_total_image_bytes,
            detail=self._settings.multimodal_image_detail,
        )

    async def _retrieve_chunks(
        self,
        session: AsyncSession,
        *,
        corpus: str,
        claims: ChatTokenClaims,
        question: str,
        question_embedding: list[float],
        filters: list[UUID] | None,
        scope_document_id: UUID | None = None,
    ) -> tuple[list[RetrievedChunk], dict | None]:
        # Branch-aware access filtering runs inside the retrieval SQL: a global admin
        # bypasses the rule filter, every other user is limited to documents whose
        # access rules match their organizational-unit branch and/or groups. Users with
        # no groups can still match organizational-unit and company-wide rules, so there
        # is deliberately no group-presence short circuit here.
        params = HybridRetrievalParams(
            corpus=corpus,
            embedding_model=self._settings.resolved_embedding_model,
            is_global_admin=claims.is_global_admin,
            user_organizational_unit_id=UUID(claims.organizational_unit_id),
            root_organizational_unit_id=ROOT_ORGANIZATIONAL_UNIT_ID,
            user_groups=[UUID(group_id) for group_id in claims.groups],
            dimension_value_filter=filters,
            scope_document_id=scope_document_id,
            vector_top_k=self._settings.rag_vector_top_k,
            bm25_top_k=self._settings.rag_bm25_top_k,
            rrf_k=self._settings.rag_rrf_k,
            final_top_k=self._settings.rag_hybrid_top_k,
            ef_search=self._settings.rag_hnsw_ef_search,
            iterative_scan=(
                self._settings.rag_hnsw_iterative_scan and self._hnsw_iterative_scan_supported
            ),
        )
        connection = await session.connection()
        hybrid_candidates: list[HybridCandidate] = await hybrid_retrieve(
            connection,
            q_text=question,
            q_embedding=question_embedding,
            params=params,
        )
        if not hybrid_candidates:
            return [], None

        reranked, rerank_audit = await rerank_candidates(
            reranker=self._reranker_provider,
            query=question,
            candidates=hybrid_candidates,
            top_k=self._settings.rag_final_top_k,
        )
        chunks = [
            RetrievedChunk(
                id=candidate.chunk_id,
                document_id=candidate.document_id,
                document_version_id=candidate.document_version_id,
                heading_path=list(candidate.heading_path),
                content=candidate.content,
            )
            for candidate in reranked
        ]
        return chunks, rerank_audit

    async def _query_cache_candidate(
        self,
        session: AsyncSession,
        *,
        corpus: str,
        access_scope_hash: str,
        filters_hash: str | None,
        question_embedding: list[float],
    ) -> Any:
        """Return the single nearest cache entry for the partition, or None.

        Isolated so the lookup can degrade gracefully if the query fails and so tests
        can inject a failure via this seam. The HNSW index orders globally and the WHERE
        filters by partition + embedding model; cosine similarity (`1 - distance`) is
        returned for the threshold check by the caller.
        """
        result = await session.execute(
            text(
                """
                select
                    id,
                    answer,
                    cached_at,
                    citations,
                    1 - (question_embedding <=> (:question_embedding)::vector) as similarity
                from rag.semantic_cache_entries
                where corpus = :corpus
                  and access_scope_hash = :access_scope_hash
                  and filters_hash is not distinct from :filters_hash
                  and embedding_model = :embedding_model
                  and expires_at > now()
                order by question_embedding <=> (:question_embedding)::vector
                limit 1
                """
            ),
            {
                "corpus": corpus,
                "access_scope_hash": access_scope_hash,
                "filters_hash": filters_hash,
                "embedding_model": self._settings.resolved_embedding_model,
                "question_embedding": _vector_literal(question_embedding),
            },
        )
        return result.first()

    async def _lookup_cache(
        self,
        session: AsyncSession,
        *,
        corpus: str,
        access_scope_hash: str,
        filters_hash: str | None,
        question_embedding: list[float],
    ) -> dict[str, Any] | None:
        try:
            row = await self._query_cache_candidate(
                session,
                corpus=corpus,
                access_scope_hash=access_scope_hash,
                filters_hash=filters_hash,
                question_embedding=question_embedding,
            )
        except Exception:  # noqa: BLE001 - cache must never break the chat path
            logger.warning("Semantic cache lookup failed; continuing without cache.", exc_info=True)
            return None
        if row is None:
            return None
        # The HNSW scan orders globally and the WHERE filters by partition, so a very
        # crowded cache table can occasionally miss a valid entry (post-filtering). That
        # is acceptable — a missed hit just runs the full RAG path. Do not add correctness
        # logic that depends on cache hits.
        threshold = Decimal(str(self._settings.rag_semantic_cache_similarity_threshold))
        if Decimal(str(row.similarity)) < threshold:
            return None
        payload = row.citations if isinstance(row.citations, list) else json.loads(row.citations)
        return {
            "id": row.id,
            "answer": row.answer,
            "cached_at": row.cached_at,
            "citations": [
                Citation(
                    chunk_id=UUID(item["chunk_id"]),
                    document_id=UUID(item["document_id"]),
                    document_version_id=UUID(item["document_version_id"]),
                    heading_path=list(item["heading_path"]),
                )
                for item in payload
            ],
        }

    async def _audit_cache_hit(
        self,
        session: AsyncSession,
        *,
        cache_hit: dict[str, Any],
        question: str,
        rewritten_question: str | None,
        claims: ChatTokenClaims,
        corpus: str,
        filters: list[UUID] | None,
        filters_hash: str | None,
        session_id: UUID | None,
        previous_event_id: UUID | None,
        request_id: str,
        latency_ms: int,
    ) -> ChatAnswer:
        chat_pricing = await self._active_pricing(
            session, self._settings.resolved_chat_model, "chat"
        )
        audit_id = uuid4()
        citations = cache_hit["citations"]
        await self._insert_audit(
            session,
            audit_id=audit_id,
            user_id=UUID(claims.user_id),
            request_id=request_id,
            question=question,
            rewritten_question=rewritten_question,
            answer=cache_hit["answer"],
            cache_hit=True,
            cached_at=cache_hit["cached_at"],
            input_tokens=0,
            cached_tokens=0,
            output_tokens=0,
            pricing_snapshot_id=chat_pricing.id,
            estimated_cost=Decimal("0"),
            latency_ms=latency_ms,
            access_scope_hash=claims.access_scope_hash,
            corpus=corpus,
            session_id=session_id,
            previous_event_id=previous_event_id,
            filters=filters,
            filters_hash=filters_hash,
            rerank_audit=None,
            scope_document_id=None,
        )
        await self._insert_citations(session, audit_id, citations)
        return ChatAnswer(
            query_audit_event_id=audit_id,
            answer=cache_hit["answer"],
            citations=citations,
            cache_hit=True,
            cached_at=cache_hit["cached_at"],
            input_tokens=0,
            cached_tokens=0,
            output_tokens=0,
            estimated_cost_usd=Decimal("0"),
        )

    async def _write_cache(
        self,
        session: AsyncSession,
        *,
        audit_id: UUID,
        corpus: str,
        access_scope_hash: str,
        filters_hash: str | None,
        question: str,
        answer: str,
        question_embedding: list[float],
        citations: list[Citation],
    ) -> None:
        cache_id = uuid4()
        cached_at = datetime.now(UTC)
        await session.execute(
            text(
                """
                insert into rag.semantic_cache_entries (
                    id, corpus, access_scope_hash, filters_hash,
                    question_hash, question, answer, citations,
                    question_embedding, embedding_model, embedding_dimensions,
                    similarity_threshold, cached_at, expires_at, created_by_query_audit_event_id
                )
                values (
                    :id, :corpus, :access_scope_hash, :filters_hash,
                    :question_hash, :question, :answer, cast(:citations as jsonb),
                    (:question_embedding)::vector, :embedding_model, :embedding_dimensions,
                    :similarity_threshold, :cached_at, :expires_at, :audit_id
                )
                """
            ),
            {
                "id": cache_id,
                "corpus": corpus,
                "access_scope_hash": access_scope_hash,
                "filters_hash": filters_hash,
                "question_hash": hashlib.sha256(question.encode("utf-8")).hexdigest(),
                "question": question,
                "answer": answer,
                "citations": json.dumps(
                    [
                        {
                            "chunk_id": str(citation.chunk_id),
                            "document_id": str(citation.document_id),
                            "document_version_id": str(citation.document_version_id),
                            "heading_path": citation.heading_path,
                        }
                        for citation in citations
                    ]
                ),
                "question_embedding": _vector_literal(question_embedding),
                "embedding_model": self._settings.resolved_embedding_model,
                "embedding_dimensions": self._settings.resolved_embedding_dimensions,
                "similarity_threshold": Decimal(str(self._settings.rag_semantic_cache_similarity_threshold)),
                "cached_at": cached_at,
                "expires_at": cached_at + timedelta(hours=self._settings.rag_semantic_cache_ttl_hours),
                "audit_id": audit_id,
            },
        )
        for citation in citations:
            await session.execute(
                text(
                    """
                    insert into rag.semantic_cache_sources (
                        cache_entry_id, document_id, document_version_id
                    )
                    values (:cache_entry_id, :document_id, :document_version_id)
                    on conflict do nothing
                    """
                ),
                {
                    "cache_entry_id": cache_id,
                    "document_id": citation.document_id,
                    "document_version_id": citation.document_version_id,
                },
            )

    async def _insert_audit(
        self,
        session: AsyncSession,
        *,
        audit_id: UUID,
        user_id: UUID,
        request_id: str,
        question: str,
        rewritten_question: str | None,
        answer: str,
        cache_hit: bool,
        cached_at: datetime | None,
        input_tokens: int,
        cached_tokens: int,
        output_tokens: int,
        pricing_snapshot_id: UUID,
        estimated_cost: Decimal,
        latency_ms: int,
        access_scope_hash: str,
        corpus: str,
        session_id: UUID | None,
        previous_event_id: UUID | None,
        filters: list[UUID] | None,
        filters_hash: str | None,
        rerank_audit: dict | None,
        scope_document_id: UUID | None = None,
        multimodal_used: bool = False,
        multimodal_image_count: int = 0,
        multimodal_image_detail: str | None = None,
        multimodal_image_bytes_total: int = 0,
        multimodal_image_ids: list[str] | None = None,
    ) -> None:
        await session.execute(
            text(
                """
                insert into rag.query_audit_events (
                    id, user_id, request_id, question, answer, cache_hit, cached_at,
                    chat_model, embedding_model, embedding_dimensions,
                    input_tokens, cached_tokens, output_tokens, pricing_snapshot_id,
                    estimated_cost_usd, latency_ms, access_scope_hash, corpus,
                    prompt_version, chunker_version,
                    session_id, previous_event_id, rewritten_question, filters, filters_hash,
                    vector_top_k, bm25_top_k, rerank_top_k,
                    reranker_model, reranker_score,
                    scope_document_id,
                    multimodal_used, multimodal_image_count, multimodal_image_detail,
                    multimodal_image_bytes_total, multimodal_image_ids
                )
                values (
                    :id, :user_id, :request_id, :question, :answer, :cache_hit, :cached_at,
                    :chat_model, :embedding_model, :embedding_dimensions,
                    :input_tokens, :cached_tokens, :output_tokens, :pricing_snapshot_id,
                    :estimated_cost_usd, :latency_ms, :access_scope_hash, :corpus,
                    :prompt_version, :chunker_version,
                    :session_id, :previous_event_id, :rewritten_question, cast(:filters as jsonb), :filters_hash,
                    :vector_top_k, :bm25_top_k, :rerank_top_k,
                    :reranker_model, :reranker_score,
                    :scope_document_id,
                    :multimodal_used, :multimodal_image_count, :multimodal_image_detail,
                    :multimodal_image_bytes_total, cast(:multimodal_image_ids as jsonb)
                )
                """
            ),
            {
                "id": audit_id,
                "user_id": user_id,
                "request_id": request_id,
                "question": question,
                "rewritten_question": rewritten_question,
                "answer": answer,
                "cache_hit": cache_hit,
                "cached_at": cached_at,
                "chat_model": self._settings.resolved_chat_model,
                "embedding_model": self._settings.resolved_embedding_model,
                "embedding_dimensions": self._settings.resolved_embedding_dimensions,
                "input_tokens": input_tokens,
                "cached_tokens": cached_tokens,
                "output_tokens": output_tokens,
                "pricing_snapshot_id": pricing_snapshot_id,
                "estimated_cost_usd": estimated_cost,
                "latency_ms": latency_ms,
                "access_scope_hash": access_scope_hash,
                "corpus": corpus,
                "prompt_version": PROMPT_VERSION,
                "chunker_version": CHUNKER_VERSION,
                "session_id": session_id,
                "previous_event_id": previous_event_id,
                "filters": json.dumps([str(f) for f in filters]) if filters else None,
                "filters_hash": filters_hash,
                "vector_top_k": self._settings.rag_vector_top_k,
                "bm25_top_k": self._settings.rag_bm25_top_k,
                "rerank_top_k": (
                    self._settings.rag_final_top_k
                    if rerank_audit is not None
                    else None
                ),
                "reranker_model": rerank_audit["reranker_model"] if rerank_audit else None,
                "reranker_score": (
                    Decimal(str(rerank_audit["top_score"])) if rerank_audit else None
                ),
                "scope_document_id": scope_document_id,
                "multimodal_used": multimodal_used,
                "multimodal_image_count": multimodal_image_count,
                "multimodal_image_detail": multimodal_image_detail,
                "multimodal_image_bytes_total": multimodal_image_bytes_total,
                "multimodal_image_ids": (
                    json.dumps(multimodal_image_ids) if multimodal_image_ids else None
                ),
            },
        )

    async def _insert_citations(
        self,
        session: AsyncSession,
        audit_id: UUID,
        citations: list[Citation],
    ) -> None:
        for citation in citations:
            await session.execute(
                text(
                    """
                    insert into rag.query_audit_citations (
                        id, query_audit_event_id, chunk_id, document_id,
                        document_version_id, heading_path
                    )
                    values (
                        :id, :query_audit_event_id, :chunk_id, :document_id,
                        :document_version_id, :heading_path
                    )
                    """
                ),
                {
                    "id": uuid4(),
                    "query_audit_event_id": audit_id,
                    "chunk_id": citation.chunk_id,
                    "document_id": citation.document_id,
                    "document_version_id": citation.document_version_id,
                    "heading_path": citation.heading_path,
                },
            )

    async def _ensure_pricing_configured(self, session: AsyncSession) -> None:
        await self._active_pricing(session, self._settings.resolved_chat_model, "chat")
        await self._active_pricing(session, self._settings.resolved_embedding_model, "embedding")

    async def _active_pricing(self, session: AsyncSession, model_id: str, model_kind: str) -> PricingSnapshot:
        result = await session.execute(
            text(
                """
                select id, input_token_price_usd, cached_token_price_usd, output_token_price_usd
                from rag.model_pricing
                where model_id = :model_id
                  and model_kind = :model_kind
                  and effective_from <= now()
                  and (effective_to is null or effective_to > now())
                order by effective_from desc
                limit 1
                """
            ),
            {"model_id": model_id, "model_kind": model_kind},
        )
        row = result.first()
        if row is None:
            raise ApiException(
                "RAG_PROVIDER_MISCONFIGURED",
                500,
                f"Model pricing is not configured for {model_kind}={model_id!r}.",
            )
        return PricingSnapshot(
            id=row.id,
            input_token_price_usd=Decimal(row.input_token_price_usd),
            cached_token_price_usd=Decimal(row.cached_token_price_usd or 0),
            output_token_price_usd=Decimal(row.output_token_price_usd or 0),
        )

    async def _enforce_budget(
        self,
        session: AsyncSession,
        user_id: UUID,
        now: datetime,
    ) -> None:
        result = await session.execute(
            text(
                """
                select monthly_budget_usd, is_disabled
                from app.user_ai_budget_limits
                where user_id = :user_id
                """
            ),
            {"user_id": user_id},
        )
        row = result.first()
        if row is not None and bool(row.is_disabled):
            return
        monthly_budget = (
            Decimal(row.monthly_budget_usd)
            if row is not None and row.monthly_budget_usd is not None
            else Decimal(str(self._settings.default_monthly_ai_budget_usd))
        )
        start, end = _month_bounds_utc(now, self._settings.customer_timezone)
        spend_result = await session.execute(
            text(
                """
                select coalesce(sum(estimated_cost_usd), 0)
                from rag.query_audit_events
                where user_id = :user_id
                  and created_at >= :period_start
                  and created_at < :period_end
                """
            ),
            {"user_id": user_id, "period_start": start, "period_end": end},
        )
        current_spend = Decimal(spend_result.scalar_one())
        if current_spend >= monthly_budget:
            raise ApiException(
                "AI_BUDGET_EXCEEDED",
                429,
                "AI usage budget exceeded.",
            )


def _compute_filters_hash(filters: list[UUID] | None) -> str | None:
    """Stable hash that partitions cache and audit lookups by the applied filter set.

    Returns None when no filters are applied so the query partitions match (cache
    lookup uses `IS NOT DISTINCT FROM`).
    """
    if not filters:
        return None
    sorted_values = sorted(str(f) for f in filters)
    digest = hashlib.sha256("|".join(sorted_values).encode("utf-8")).hexdigest()
    return digest[:16]


def _citations_payload(citations: list[Citation]) -> list[dict[str, Any]]:
    """Serialize citations for the SSE `citations` event (stable wire shape)."""
    return [
        {
            "chunk_id": str(citation.chunk_id),
            "document_id": str(citation.document_id),
            "document_version_id": str(citation.document_version_id),
            "heading_path": citation.heading_path,
        }
        for citation in citations
    ]


def _no_results_message(locale: str) -> str:
    if locale.startswith("es"):
        return "No encontré información publicada suficiente para responder esa consulta."
    return "I couldn't find enough published information to answer that question."


def _no_results_message_scoped(locale: str) -> str:
    if locale.startswith("es"):
        return "No encontré información en este documento para responder esa pregunta."
    return "I couldn't find information in this document to answer that question."


def _vector_literal(values: list[float]) -> str:
    return "[" + ",".join(str(value) for value in values) + "]"


def _calculate_cost(
    *,
    embedding_tokens: int,
    input_tokens: int,
    output_tokens: int,
    embedding_pricing: PricingSnapshot,
    chat_pricing: PricingSnapshot,
) -> Decimal:
    return (
        Decimal(embedding_tokens) * embedding_pricing.input_token_price_usd
        + Decimal(input_tokens) * chat_pricing.input_token_price_usd
        + Decimal(output_tokens) * chat_pricing.output_token_price_usd
    ).quantize(Decimal("0.00000001"))


def _to_image_input(image: SelectedMultimodalImage) -> ImageInput:
    return ImageInput(
        data_base64=base64.b64encode(image.bytes_data).decode("ascii"),
        media_type=image.content_type,
        detail=image.detail,
    )


def _estimate_tokens(text: str) -> int:
    return max(1, len(text.split()))


def _latency_ms(started_at: datetime) -> int:
    return max(1, int((datetime.now(UTC) - started_at).total_seconds() * 1000))


def _zero_usage() -> ChatUsage:
    return ChatUsage(input_tokens=0, output_tokens=0)


def _month_bounds_utc(now: datetime, timezone_name: str) -> tuple[datetime, datetime]:
    tz = UTC if timezone_name.upper() == "UTC" else ZoneInfo(timezone_name)
    local_now = now.astimezone(tz)
    local_start = local_now.replace(day=1, hour=0, minute=0, second=0, microsecond=0)
    if local_start.month == 12:
        local_end = local_start.replace(year=local_start.year + 1, month=1)
    else:
        local_end = local_start.replace(month=local_start.month + 1)
    return local_start.astimezone(UTC), local_end.astimezone(UTC)
