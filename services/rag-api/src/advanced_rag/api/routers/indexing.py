from fastapi import APIRouter, Header, Request

from advanced_rag.core.errors import ApiException
from advanced_rag.rag.indexing_service import InternalIndexingService
from advanced_rag.schemas.indexing import InternalIndexingRequest, InternalIndexingResponse


router = APIRouter(prefix="/internal/indexing-jobs", tags=["internal-indexing"])


@router.post("", response_model=InternalIndexingResponse, response_model_by_alias=True)
async def create_indexing_job(
    body: InternalIndexingRequest,
    request: Request,
    internal_service_token: str | None = Header(default=None, alias="X-Internal-Service-Token"),
) -> InternalIndexingResponse:
    settings = request.app.state.settings
    expected_token = settings.resolved_internal_service_token
    if not expected_token or internal_service_token != expected_token:
        raise ApiException(
            "AUTH_INTERNAL_TOKEN_INVALID",
            401,
            "Internal service token is invalid.",
        )

    service: InternalIndexingService = request.app.state.internal_indexing_service
    result = await service.create_job(body)
    return InternalIndexingResponse(
        jobId=result.job_id,
        status=result.status,
        chunkCount=result.chunk_count,
        errorCode=result.error_code,
        errorMessage=result.error_message,
    )
