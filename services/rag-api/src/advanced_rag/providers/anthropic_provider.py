"""Anthropic Claude implementation of `ILlmProvider`.

Anthropic does not provide an embedding API, so embeddings still use OpenAI or TEI.

See docs/adr/0001-multi-provider-llm.md.

Note: this provider depends on the `anthropic` Python SDK. Add it to pyproject.toml's
`[project.optional-dependencies]` group `providers-anthropic` so that deployments using
OpenAI-only do not need to install it.
"""

from __future__ import annotations

from collections.abc import AsyncIterator
from typing import Any

from advanced_rag.providers._retry import is_transient_anthropic_error, retry_async
from advanced_rag.providers.base import (
    ChatCompletionDelta,
    ChatCompletionRequest,
    ChatUsage,
    ILlmProvider,
)


class AnthropicLlmProvider(ILlmProvider):
    name = "anthropic"

    def __init__(self, *, api_key: str, base_url: str | None = None) -> None:
        # Lazy import to avoid forcing the dep on OpenAI-only deployments.
        from anthropic import AsyncAnthropic  # type: ignore

        kwargs: dict[str, Any] = {"api_key": api_key}
        if base_url:
            kwargs["base_url"] = base_url
        self._client = AsyncAnthropic(**kwargs)

    def _convert_messages(
        self, messages: list, response_format: dict | None
    ) -> tuple[str | None, list[dict[str, str]]]:
        """Split a list of ChatMessages into Anthropic's (system, messages) shape.

        Anthropic takes `system` as a top-level argument and the rest as a `messages`
        array of `{role, content}` where role is 'user' or 'assistant'. If a JSON
        response_format is requested, we append an instruction to return strict JSON.
        """
        system_prompt: str | None = None
        out: list[dict[str, str]] = []
        for m in messages:
            if m.role == "system":
                system_prompt = (system_prompt + "\n\n" + m.content) if system_prompt else m.content
            else:
                out.append({"role": m.role, "content": m.content})

        if response_format and response_format.get("type") in ("json_object", "json_schema"):
            instruction = "Return only valid JSON, no prose, no markdown fences."
            if system_prompt:
                system_prompt = system_prompt + "\n\n" + instruction
            else:
                system_prompt = instruction

        return system_prompt, out

    async def chat_stream(
        self, req: ChatCompletionRequest
    ) -> AsyncIterator[ChatCompletionDelta]:
        system_prompt, messages = self._convert_messages(req.messages, req.response_format)

        async def _open_stream():  # type: ignore[no-untyped-def]
            return self._client.messages.stream(
                model=req.model,
                max_tokens=req.max_tokens,
                temperature=req.temperature,
                system=system_prompt,
                messages=messages,
            )

        # We don't retry mid-stream, only on initial open.
        async with await retry_async(_open_stream, retry_on=is_transient_anthropic_error) as stream:
            async for text in stream.text_stream:
                yield ChatCompletionDelta(content=text)
            final_message = await stream.get_final_message()
            yield ChatCompletionDelta(content=None, finish_reason=final_message.stop_reason)

    async def chat_complete(
        self, req: ChatCompletionRequest
    ) -> tuple[str, ChatUsage]:
        system_prompt, messages = self._convert_messages(req.messages, req.response_format)

        async def _call():  # type: ignore[no-untyped-def]
            return await self._client.messages.create(
                model=req.model,
                max_tokens=req.max_tokens,
                temperature=req.temperature,
                system=system_prompt,
                messages=messages,
            )

        response = await retry_async(_call, retry_on=is_transient_anthropic_error)

        # Concatenate text blocks (Anthropic returns a list of content blocks).
        parts = [block.text for block in response.content if getattr(block, "type", None) == "text"]
        content = "".join(parts)

        usage = response.usage
        return content, ChatUsage(
            input_tokens=getattr(usage, "input_tokens", 0),
            output_tokens=getattr(usage, "output_tokens", 0),
            cached_input_tokens=getattr(usage, "cache_read_input_tokens", 0),
        )
