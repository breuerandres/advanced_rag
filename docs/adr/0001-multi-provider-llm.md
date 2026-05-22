# ADR-0001 — Multi-provider LLM abstraction

**Status**: Accepted (v2 design phase, 2026-05-22)
**Phase**: 1
**Supersedes**: parts of `context/code-standards.md` "Use the official OpenAI Python SDK only in FastAPI"

## Context

The MVP hard-codes `AsyncOpenAI` everywhere a chat completion or embedding is needed. The
v2 product is generic and self-hosted by different companies, each of whom may want a
different provider (OpenAI for some, Anthropic for others, Azure for Microsoft-shop
customers, Ollama or vLLM for on-prem). Making provider choice a `tenant_config` setting
requires abstracting the SDK call.

## Decision

Introduce three provider interfaces in `services/rag-api/src/advanced_rag/providers/`:

```python
class ILlmProvider(Protocol):
    name: str
    async def chat_stream(self, req: ChatCompletionRequest) -> AsyncIterator[ChatCompletionDelta]: ...
    async def chat_complete(self, req: ChatCompletionRequest) -> tuple[str, ChatUsage]: ...

class IEmbeddingProvider(Protocol):
    name: str
    dimensions: int
    async def embed(self, texts: list[str]) -> tuple[list[list[float]], ChatUsage]: ...

class IRerankerProvider(Protocol):
    name: str
    async def rerank(self, query: str, documents: list[str], top_k: int) -> list[tuple[int, float]]: ...
```

A factory reads `app.tenant_config` (`llm_provider`, `llm_model`, `embedding_provider`,
`embedding_model`, `reranker_provider`, `reranker_model`) and returns the appropriate
implementation. Initial implementations:

- `OpenAILlmProvider` / `OpenAIEmbeddingProvider`
- `AnthropicLlmProvider`
- `AzureOpenAILlmProvider` / `AzureOpenAIEmbeddingProvider`
- `OllamaLlmProvider` (OpenAI-compatible local API)
- `TeiEmbeddingProvider` (HuggingFace Text Embeddings Inference, for BGE-M3)
- `TeiRerankerProvider` (BGE-reranker-v2-m3 via TEI)
- `CohereRerankerProvider`

All providers share a `_retry.py` wrapper using `tenacity` with provider-specific transient
error detection.

## Alternatives considered

### LiteLLM (sidecar or library)
Pros: 100+ providers out of the box, telemetry built in, retry/fallback included.
Cons: Opinionated logging, possible bugs cross-provider, larger surface to audit, less
control over per-provider quirks (e.g. structured outputs differ a lot OpenAI vs
Anthropic).

### OpenAI-compatible-only (force every backend to expose OpenAI-shaped API)
Pros: Single SDK path.
Cons: Anthropic, Bedrock, and many other production providers don't expose the OpenAI
shape natively; routes through proxies add a moving piece.

### Hard-code per-deployment (build flag chooses provider)
Pros: Simpler in the small.
Cons: Defeats the "swap provider via config" requirement; admin can't change in production.

## Consequences

**Positive**
- Customers can pick provider without code changes.
- Easier testing: a `FakeLlmProvider` in tests, real provider in integration.
- Provider-specific retry/timeout policy isolated.
- Structured-output quirks (OpenAI's `response_format` JSON-schema vs Anthropic's tool-use
  pattern) absorbed inside the provider implementation.

**Negative**
- More code to maintain than direct SDK usage.
- Each new provider needs a full implementation pass (chat + embeddings + reranker if
  applicable).
- Latency budget includes provider-specific overhead the abstraction can't hide.

**Risks / mitigations**
- Providers diverge over time (new features) → abstraction must stay minimal; capabilities
  expansion goes through an explicit `ILlmProviderCapabilities` extension interface.
- Reranker support is uneven (Anthropic has none; OpenAI has none mainstream) → reranker
  provider is always separate from chat provider; only providers with real reranker APIs
  appear there.

## References

- `services/rag-api/src/advanced_rag/providers/base.py`
- Open question OQ-008 (default Anthropic model)
- Open question OQ-002 (default embedding model)
