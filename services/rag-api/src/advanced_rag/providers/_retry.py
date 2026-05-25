"""Shared retry / timeout policy for provider HTTP calls.

Uses `tenacity` with conservative defaults (2 retries, exponential backoff with jitter,
30-second per-attempt timeout via the underlying client). Provider implementations
should call into these helpers via context managers or decorators.

See docs/adr/0001-multi-provider-llm.md (consequences section).
"""

from __future__ import annotations

import logging
from collections.abc import Awaitable, Callable
from typing import TypeVar

logger = logging.getLogger(__name__)

T = TypeVar("T")


# tenacity is the canonical retry library used in the .NET / FastAPI side already.
# We import lazily so that tests can monkey-patch.
def _import_tenacity():  # type: ignore[no-untyped-def]
    import tenacity

    return tenacity


def is_transient_openai_error(exc: BaseException) -> bool:
    """Return True for transient OpenAI errors that should be retried."""
    try:
        from openai import APIConnectionError, APIStatusError, RateLimitError  # type: ignore

        if isinstance(exc, (APIConnectionError, RateLimitError)):
            return True
        if isinstance(exc, APIStatusError) and exc.status_code >= 500:
            return True
    except ImportError:
        pass
    return False


def is_transient_anthropic_error(exc: BaseException) -> bool:
    """Return True for transient Anthropic errors that should be retried."""
    try:
        from anthropic import APIConnectionError, APIStatusError, RateLimitError  # type: ignore

        if isinstance(exc, (APIConnectionError, RateLimitError)):
            return True
        if isinstance(exc, APIStatusError) and exc.status_code >= 500:
            return True
    except ImportError:
        pass
    return False


def is_transient_httpx_error(exc: BaseException) -> bool:
    """Return True for transient generic httpx errors (used by Ollama, TEI, Cohere)."""
    try:
        import httpx

        if isinstance(exc, (httpx.ConnectError, httpx.ReadTimeout, httpx.ReadError)):
            return True
        if isinstance(exc, httpx.HTTPStatusError) and exc.response.status_code >= 500:
            return True
    except ImportError:
        pass
    return False


async def retry_async(
    func: Callable[[], Awaitable[T]],
    *,
    retry_on: Callable[[BaseException], bool],
    max_attempts: int = 3,
    initial_wait_ms: int = 250,
    max_wait_ms: int = 2000,
) -> T:
    """Run `func` with retry on transient errors.

    Provider implementations should wrap their network call::

        return await retry_async(
            lambda: client.chat.completions.create(...),
            retry_on=is_transient_openai_error,
        )

    On final failure the original exception is re-raised. Successful results pass
    through unchanged.
    """
    tenacity = _import_tenacity()

    retryer = tenacity.AsyncRetrying(
        stop=tenacity.stop_after_attempt(max_attempts),
        wait=tenacity.wait_exponential_jitter(
            initial=initial_wait_ms / 1000.0,
            max=max_wait_ms / 1000.0,
        ),
        retry=tenacity.retry_if_exception(retry_on),
        before_sleep=tenacity.before_sleep_log(logger, logging.WARNING),
        reraise=True,
    )

    async for attempt in retryer:
        with attempt:
            return await func()

    # `reraise=True` guarantees we either returned above or re-raised. This is for mypy.
    raise RuntimeError("unreachable: retry_async exited without returning or raising")
