from fastapi import APIRouter, Request

from app.schemas import HealthResponse

router = APIRouter(tags=["health"])


@router.get("/health", response_model=HealthResponse)
def health(request: Request) -> HealthResponse:
    service = request.app.state.ocr_service
    return HealthResponse(
        status="ok",
        service=request.app.state.settings.app_name,
        model_version=request.app.state.settings.model_version,
        device=request.app.state.settings.device,
        model_loaded=service.model_loaded,
    )
