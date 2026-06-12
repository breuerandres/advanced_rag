from __future__ import annotations

from uuid import UUID

import pytest

from advanced_rag.rag.hybrid_retrieval import (
    HybridRetrievalParams,
    detect_iterative_scan_support,
    hybrid_retrieve,
)


class _StubResult:
    def __init__(self, value: str | None) -> None:
        self._value = value

    def scalar_one_or_none(self) -> str | None:
        return self._value


class _StubConnection:
    """Minimal async connection that returns a fixed pg_extension version."""

    def __init__(self, version: str | None) -> None:
        self._version = version
        self.executed: list[str] = []

    async def execute(self, statement: object, *args: object) -> _StubResult:
        self.executed.append(str(statement))
        return _StubResult(self._version)


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("version", "expected"),
    [
        ("0.8.1", True),
        ("0.8.0", True),
        ("1.0.0", True),
        ("0.7.4", False),
        ("0.5.1", False),
        (None, False),
        ("garbage", False),
    ],
)
async def test_detect_iterative_scan_support(version: str | None, expected: bool) -> None:
    connection = _StubConnection(version)
    assert await detect_iterative_scan_support(connection) is expected  # type: ignore[arg-type]


@pytest.mark.asyncio
async def test_hybrid_retrieve_rejects_unknown_corpus() -> None:
    params = HybridRetrievalParams(
        corpus="not-a-corpus",
        user_groups=[],
        user_organizational_unit_id=UUID(int=1),
        root_organizational_unit_id=UUID(int=1),
    )
    with pytest.raises(ValueError, match="Unsupported corpus"):
        await hybrid_retrieve(
            _StubConnection(None),  # type: ignore[arg-type]
            q_text="x",
            q_embedding=[0.0],
            params=params,
        )
