from __future__ import annotations

import json
from datetime import UTC, datetime
from pathlib import Path
from time import perf_counter

from starlette.middleware.base import BaseHTTPMiddleware, RequestResponseEndpoint
from starlette.requests import Request
from starlette.responses import Response

from advanced_rag.core.request_id import REQUEST_ID_HEADER


class OperationalRequestLoggingMiddleware(BaseHTTPMiddleware):
    def __init__(self, app, log_directory: str = "/var/log/rag-api") -> None:  # type: ignore[no-untyped-def]
        super().__init__(app)
        self._log_directory = Path(log_directory)

    async def dispatch(self, request: Request, call_next: RequestResponseEndpoint) -> Response:
        timestamp = datetime.now(UTC)
        started = perf_counter()
        response = await call_next(request)
        self._write_log(request, response, timestamp, int((perf_counter() - started) * 1000))
        return response

    def _write_log(self, request: Request, response: Response, timestamp: datetime, elapsed_ms: int) -> None:
        self._log_directory.mkdir(parents=True, exist_ok=True)
        path = self._log_directory / f"log-{timestamp:%Y%m%d}.json"
        entry = {
            "timestamp": timestamp.isoformat(),
            "service": "rag-api",
            "request_id": getattr(request.state, "request_id", None) or request.headers.get(REQUEST_ID_HEADER),
            "origin_ip": _origin_ip(request),
            "route": request.url.path,
            "method": request.method,
            "response_status": response.status_code,
            "safe_error_code": getattr(request.state, "safe_error_code", None),
            "elapsed_ms": elapsed_ms,
        }
        with path.open("a", encoding="utf-8") as log_file:
            log_file.write(json.dumps(entry, separators=(",", ":")) + "\n")


def _origin_ip(request: Request) -> str:
    forwarded = request.headers.get("X-Forwarded-For")
    if forwarded:
        return forwarded.split(",", 1)[0].strip()
    if request.client:
        return request.client.host
    return "unknown"
