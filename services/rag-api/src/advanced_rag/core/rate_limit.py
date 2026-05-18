from __future__ import annotations

from dataclasses import dataclass
from datetime import UTC, datetime, timedelta


class FixedWindowRateLimiter:
    def __init__(self) -> None:
        self._counters: dict[str, _Counter] = {}

    def allow(self, key: str, limit: int, window_seconds: int) -> bool:
        now = datetime.now(UTC)
        counter = self._counters.get(key)
        if counter is None or now - counter.started_at >= timedelta(seconds=window_seconds):
            self._counters[key] = _Counter(started_at=now, count=1)
            return True

        counter.count += 1
        return counter.count <= limit


@dataclass
class _Counter:
    started_at: datetime
    count: int
