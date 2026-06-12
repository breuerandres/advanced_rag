from fastapi import APIRouter, Header
from pydantic import BaseModel, ConfigDict, Field
from starlette.requests import Request

from advanced_rag.core.errors import ApiException
from advanced_rag.rag.maintenance_service import MaintenanceService

router = APIRouter(prefix="/internal/maintenance", tags=["internal-maintenance"])


class PurgeResponse(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    inactive_chunks_deleted: int = Field(serialization_alias="inactiveChunksDeleted")
    cache_entries_deleted: int = Field(serialization_alias="cacheEntriesDeleted")


@router.post("/purge", response_model=PurgeResponse, response_model_by_alias=True)
async def purge(
    request: Request,
    internal_service_token: str | None = Header(default=None, alias="X-Internal-Service-Token"),
) -> PurgeResponse:
    settings = request.app.state.settings
    expected_token = settings.resolved_internal_service_token
    if not expected_token or internal_service_token != expected_token:
        raise ApiException("AUTH_INTERNAL_TOKEN_INVALID", 401, "Internal service token is invalid.")
    service: MaintenanceService = request.app.state.maintenance_service
    result = await service.purge(retention_days=settings.rag_inactive_chunk_retention_days)
    return PurgeResponse(
        inactive_chunks_deleted=result.inactive_chunks_deleted,
        cache_entries_deleted=result.cache_entries_deleted,
    )
