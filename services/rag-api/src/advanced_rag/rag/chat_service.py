from __future__ import annotations

import hashlib
import math
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from decimal import Decimal
from typing import Any
from uuid import UUID, uuid4
from zoneinfo import ZoneInfo

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker

from advanced_rag.auth.chat_tokens import ChatTokenClaims
from advanced_rag.core.config import Settings
from advanced_rag.core.errors import ApiException
from advanced_rag.rag.chunking import CHUNKER_VERSION
from advanced_rag.rag.embeddings import EmbeddingProvider


PROMPT_VERSION = 1


@dataclass(frozen=True)
class RetrievedChunk:
    id: UUID
    instruction_id: UUID
    instruction_version_id: UUID
    heading_path: list[str]
    content: str


@dataclass(frozen=True)
class Citation:
    chunk_id: UUID
    instruction_id: UUID
    instruction_version_id: UUID
    heading_path: list[str]


@dataclass(frozen=True)
class ChatAnswer:
    answer: str
    citations: list[Citation]
    cache_hit: bool
    cached_at: datetime | None
    input_tokens: int
    cached_tokens: int
    output_tokens: int
    estimated_cost_usd: Decimal


@dataclass(frozen=True)
class PricingSnapshot:
    id: UUID
    input_token_price_usd: Decimal
    cached_token_price_usd: Decimal
    output_token_price_usd: Decimal


class ChatService:
    def __init__(
        self,
        session_factory: async_sessionmaker[AsyncSession],
        embedding_provider: EmbeddingProvider,
        chat_completion_provider: Any,
        settings: Settings,
    ) -> None:
        self._session_factory = session_factory
        self._embedding_provider = embedding_provider
        self._chat_completion_provider = chat_completion_provider
        self._settings = settings

    async def answer(
        self,
        *,
        question: str,
        claims: ChatTokenClaims,
        request_id: str,
    ) -> ChatAnswer:
        normalized_question = question.strip()
        if not normalized_question:
            raise ApiException(
                "VALIDATION_FAILED",
                400,
                "Question is required.",
                details={"field": "question"},
            )
        corpus = "published" if claims.role == "Viewer" else claims.corpus
        started_at = datetime.now(UTC)

        async with self._session_factory() as session:
            await self._ensure_pricing_configured(session)
            await self._enforce_budget(session, UUID(claims.user_id), started_at)
            question_embedding = await self._embed_question(normalized_question)

            cache_hit = await self._lookup_cache(
                session,
                corpus=corpus,
                access_scope_hash=claims.access_scope_hash,
                question_embedding=question_embedding,
            )
            if cache_hit is not None:
                answer = await self._audit_cache_hit(
                    session,
                    cache_hit=cache_hit,
                    question=normalized_question,
                    claims=claims,
                    corpus=corpus,
                    request_id=request_id,
                    latency_ms=_latency_ms(started_at),
                )
                await session.commit()
                return answer

            chunks = await self._retrieve_chunks(
                session,
                corpus=corpus,
                groups=[UUID(group_id) for group_id in claims.groups],
                question_embedding=question_embedding,
            )
            if not chunks:
                completion = _FallbackCompletion(
                    answer="No encontré información publicada suficiente para responder esa consulta.",
                    cited_chunk_ids=[],
                    input_tokens=_estimate_tokens(normalized_question),
                    output_tokens=12,
                )
            else:
                completion = await self._chat_completion_provider.complete(
                    question=normalized_question,
                    context_chunks=chunks,
                    model=self._settings.openai_chat_model,
                )

            citations = [
                Citation(
                    chunk_id=chunk.id,
                    instruction_id=chunk.instruction_id,
                    instruction_version_id=chunk.instruction_version_id,
                    heading_path=chunk.heading_path,
                )
                for chunk in chunks
                if chunk.id in set(completion.cited_chunk_ids)
            ]
            embedding_pricing = await self._active_pricing(session, self._settings.openai_embedding_model, "embedding")
            chat_pricing = await self._active_pricing(session, self._settings.openai_chat_model, "chat")
            embedding_tokens = _estimate_tokens(normalized_question)
            estimated_cost = _calculate_cost(
                embedding_tokens=embedding_tokens,
                input_tokens=completion.input_tokens,
                output_tokens=completion.output_tokens,
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
                answer=completion.answer,
                cache_hit=False,
                cached_at=None,
                input_tokens=completion.input_tokens + embedding_tokens,
                cached_tokens=0,
                output_tokens=completion.output_tokens,
                pricing_snapshot_id=chat_pricing.id,
                estimated_cost=estimated_cost,
                latency_ms=_latency_ms(started_at),
                access_scope_hash=claims.access_scope_hash,
                corpus=corpus,
            )
            await self._insert_citations(session, audit_id, citations)
            if citations:
                await self._write_cache(
                    session,
                    audit_id=audit_id,
                    corpus=corpus,
                    access_scope_hash=claims.access_scope_hash,
                    question=normalized_question,
                    answer=completion.answer,
                    question_embedding=question_embedding,
                    citations=citations,
                )
            await session.commit()
            return ChatAnswer(
                answer=completion.answer,
                citations=citations,
                cache_hit=False,
                cached_at=None,
                input_tokens=completion.input_tokens + embedding_tokens,
                cached_tokens=0,
                output_tokens=completion.output_tokens,
                estimated_cost_usd=estimated_cost,
            )

    async def invalidate_sources(self, instruction_ids: list[UUID]) -> int:
        if not instruction_ids:
            return 0
        async with self._session_factory() as session:
            result = await session.execute(
                text(
                    """
                    delete from rag.semantic_cache_entries entry
                    using rag.semantic_cache_sources source
                    where source.cache_entry_id = entry.id
                      and source.instruction_id = any(:instruction_ids)
                    """
                ),
                {"instruction_ids": instruction_ids},
            )
            await session.commit()
            return int(getattr(result, "rowcount", 0) or 0)

    async def _embed_question(self, question: str) -> list[float]:
        embeddings = await self._embedding_provider.embed_texts(
            [question],
            model=self._settings.openai_embedding_model,
            dimensions=self._settings.openai_embedding_dimensions,
        )
        return embeddings[0]

    async def _retrieve_chunks(
        self,
        session: AsyncSession,
        *,
        corpus: str,
        groups: list[UUID],
        question_embedding: list[float],
    ) -> list[RetrievedChunk]:
        if not groups:
            return []
        result = await session.execute(
            text(
                """
                select
                    chunk.id,
                    chunk.instruction_id,
                    chunk.instruction_version_id,
                    chunk.heading_path,
                    chunk.content
                from rag.document_chunks chunk
                where chunk.corpus = :corpus
                  and chunk.is_active = true
                  and exists (
                    select 1
                    from app.instruction_permissions permission
                    where permission.instruction_id = chunk.instruction_id
                      and permission.group_id = any(:groups)
                  )
                order by chunk.embedding <=> (:embedding)::vector
                limit 8
                """
            ),
            {
                "corpus": corpus,
                "groups": groups,
                "embedding": _vector_literal(question_embedding),
            },
        )
        return [
            RetrievedChunk(
                id=row.id,
                instruction_id=row.instruction_id,
                instruction_version_id=row.instruction_version_id,
                heading_path=list(row.heading_path),
                content=row.content,
            )
            for row in result
        ]

    async def _lookup_cache(
        self,
        session: AsyncSession,
        *,
        corpus: str,
        access_scope_hash: str,
        question_embedding: list[float],
    ) -> dict[str, Any] | None:
        result = await session.execute(
            text(
                """
                select id, answer, cached_at, question_embedding::text as question_embedding
                from rag.semantic_cache_entries
                where corpus = :corpus
                  and access_scope_hash = :access_scope_hash
                  and expires_at > now()
                order by cached_at desc
                """
            ),
            {"corpus": corpus, "access_scope_hash": access_scope_hash},
        )
        for row in result:
            similarity = _cosine_similarity(question_embedding, _parse_vector(row.question_embedding))
            if similarity >= Decimal(str(self._settings.rag_semantic_cache_similarity_threshold)):
                citations = await session.execute(
                    text(
                        """
                        select
                            source.instruction_id,
                            source.instruction_version_id,
                            chunk.id as chunk_id,
                            chunk.heading_path
                        from rag.semantic_cache_sources source
                        join rag.document_chunks chunk
                          on chunk.instruction_id = source.instruction_id
                         and chunk.instruction_version_id = source.instruction_version_id
                         and chunk.is_active = true
                        where source.cache_entry_id = :cache_entry_id
                        order by chunk.chunk_index
                        """
                    ),
                    {"cache_entry_id": row.id},
                )
                return {
                    "id": row.id,
                    "answer": row.answer,
                    "cached_at": row.cached_at,
                    "citations": [
                        Citation(
                            chunk_id=citation.chunk_id,
                            instruction_id=citation.instruction_id,
                            instruction_version_id=citation.instruction_version_id,
                            heading_path=list(citation.heading_path),
                        )
                        for citation in citations
                    ],
                }
        return None

    async def _audit_cache_hit(
        self,
        session: AsyncSession,
        *,
        cache_hit: dict[str, Any],
        question: str,
        claims: ChatTokenClaims,
        corpus: str,
        request_id: str,
        latency_ms: int,
    ) -> ChatAnswer:
        chat_pricing = await self._active_pricing(session, self._settings.openai_chat_model, "chat")
        audit_id = uuid4()
        citations = cache_hit["citations"]
        await self._insert_audit(
            session,
            audit_id=audit_id,
            user_id=UUID(claims.user_id),
            request_id=request_id,
            question=question,
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
        )
        await self._insert_citations(session, audit_id, citations)
        return ChatAnswer(
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
                    id, corpus, access_scope_hash, question_hash, question, answer,
                    question_embedding, embedding_model, embedding_dimensions,
                    similarity_threshold, cached_at, expires_at, created_by_query_audit_event_id
                )
                values (
                    :id, :corpus, :access_scope_hash, :question_hash, :question, :answer,
                    (:question_embedding)::vector, :embedding_model, :embedding_dimensions,
                    :similarity_threshold, :cached_at, :expires_at, :audit_id
                )
                """
            ),
            {
                "id": cache_id,
                "corpus": corpus,
                "access_scope_hash": access_scope_hash,
                "question_hash": hashlib.sha256(question.encode("utf-8")).hexdigest(),
                "question": question,
                "answer": answer,
                "question_embedding": _vector_literal(question_embedding),
                "embedding_model": self._settings.openai_embedding_model,
                "embedding_dimensions": self._settings.openai_embedding_dimensions,
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
                        cache_entry_id, instruction_id, instruction_version_id
                    )
                    values (:cache_entry_id, :instruction_id, :instruction_version_id)
                    on conflict do nothing
                    """
                ),
                {
                    "cache_entry_id": cache_id,
                    "instruction_id": citation.instruction_id,
                    "instruction_version_id": citation.instruction_version_id,
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
    ) -> None:
        await session.execute(
            text(
                """
                insert into rag.query_audit_events (
                    id, user_id, request_id, question, answer, cache_hit, cached_at,
                    chat_model, embedding_model, embedding_dimensions,
                    input_tokens, cached_tokens, output_tokens, pricing_snapshot_id,
                    estimated_cost_usd, latency_ms, access_scope_hash, corpus,
                    prompt_version, chunker_version
                )
                values (
                    :id, :user_id, :request_id, :question, :answer, :cache_hit, :cached_at,
                    :chat_model, :embedding_model, :embedding_dimensions,
                    :input_tokens, :cached_tokens, :output_tokens, :pricing_snapshot_id,
                    :estimated_cost_usd, :latency_ms, :access_scope_hash, :corpus,
                    :prompt_version, :chunker_version
                )
                """
            ),
            {
                "id": audit_id,
                "user_id": user_id,
                "request_id": request_id,
                "question": question,
                "answer": answer,
                "cache_hit": cache_hit,
                "cached_at": cached_at,
                "chat_model": self._settings.openai_chat_model,
                "embedding_model": self._settings.openai_embedding_model,
                "embedding_dimensions": self._settings.openai_embedding_dimensions,
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
                        id, query_audit_event_id, chunk_id, instruction_id,
                        instruction_version_id, heading_path
                    )
                    values (
                        :id, :query_audit_event_id, :chunk_id, :instruction_id,
                        :instruction_version_id, :heading_path
                    )
                    """
                ),
                {
                    "id": uuid4(),
                    "query_audit_event_id": audit_id,
                    "chunk_id": citation.chunk_id,
                    "instruction_id": citation.instruction_id,
                    "instruction_version_id": citation.instruction_version_id,
                    "heading_path": citation.heading_path,
                },
            )

    async def _ensure_pricing_configured(self, session: AsyncSession) -> None:
        await self._active_pricing(session, self._settings.openai_chat_model, "chat")
        await self._active_pricing(session, self._settings.openai_embedding_model, "embedding")

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
            raise ApiException("RAG_PROVIDER_MISCONFIGURED", 500, "Model pricing is not configured.")
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


@dataclass(frozen=True)
class _FallbackCompletion:
    answer: str
    cited_chunk_ids: list[UUID]
    input_tokens: int
    output_tokens: int


def _vector_literal(values: list[float]) -> str:
    return "[" + ",".join(str(value) for value in values) + "]"


def _parse_vector(value: str) -> list[float]:
    return [float(item) for item in value.strip("[]").split(",") if item]


def _cosine_similarity(left: list[float], right: list[float]) -> Decimal:
    dot = sum(a * b for a, b in zip(left, right, strict=True))
    left_norm = math.sqrt(sum(value * value for value in left))
    right_norm = math.sqrt(sum(value * value for value in right))
    if left_norm == 0 or right_norm == 0:
        return Decimal("0")
    return Decimal(str(dot / (left_norm * right_norm)))


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


def _estimate_tokens(text: str) -> int:
    return max(1, len(text.split()))


def _latency_ms(started_at: datetime) -> int:
    return max(1, int((datetime.now(UTC) - started_at).total_seconds() * 1000))


def _month_bounds_utc(now: datetime, timezone_name: str) -> tuple[datetime, datetime]:
    if timezone_name.upper() == "UTC":
        local_now = now.astimezone(UTC)
        local_start = local_now.replace(day=1, hour=0, minute=0, second=0, microsecond=0)
        if local_start.month == 12:
            local_end = local_start.replace(year=local_start.year + 1, month=1)
        else:
            local_end = local_start.replace(month=local_start.month + 1)
        return local_start, local_end
    timezone = ZoneInfo(timezone_name)
    local_now = now.astimezone(timezone)
    local_start = local_now.replace(day=1, hour=0, minute=0, second=0, microsecond=0)
    if local_start.month == 12:
        local_end = local_start.replace(year=local_start.year + 1, month=1)
    else:
        local_end = local_start.replace(month=local_start.month + 1)
    return local_start.astimezone(UTC), local_end.astimezone(UTC)
