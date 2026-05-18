import json

from fastapi.testclient import TestClient

from advanced_rag.core.config import Settings
from advanced_rag.main import create_app


def test_request_log_includes_operational_fields_and_safe_error_code(tmp_path) -> None:
    app = create_app(Settings(log_directory=str(tmp_path)))
    client = TestClient(app)

    response = client.get(
        "/missing",
        headers={"X-Request-ID": "req-log-rag", "X-Forwarded-For": "198.51.100.44"},
    )

    assert response.status_code == 404
    log_path = next(tmp_path.glob("log-*.json"))
    entry = json.loads(log_path.read_text(encoding="utf-8").strip())
    assert entry["request_id"] == "req-log-rag"
    assert entry["origin_ip"] == "198.51.100.44"
    assert entry["route"] == "/missing"
    assert entry["response_status"] == 404
    assert entry["safe_error_code"] == "NOT_FOUND"
    assert entry["timestamp"]
