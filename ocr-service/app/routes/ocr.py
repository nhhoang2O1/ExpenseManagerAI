from fastapi import APIRouter, File, HTTPException, Request, UploadFile, status
from starlette.concurrency import run_in_threadpool

from app.schemas import OCRResponse
from app.services.errors import ImageValidationError, OCRExecutionError

router = APIRouter(prefix="/internal/v1/ocr", tags=["ocr"])

ALLOWED_CONTENT_TYPES = {
    "image/jpeg",
    "image/png",
    "image/webp",
    "image/bmp",
}


@router.post("/receipts", response_model=OCRResponse)
async def recognize_receipt(
    request: Request,
    image: UploadFile = File(...),
) -> OCRResponse:
    if image.content_type not in ALLOWED_CONTENT_TYPES:
        raise HTTPException(
            status_code=status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
            detail={
                "code": "UNSUPPORTED_IMAGE_TYPE",
                "message": "Only JPEG, PNG, WebP, and BMP images are supported.",
            },
        )

    max_bytes = request.app.state.settings.max_upload_bytes
    content = await image.read(max_bytes + 1)
    await image.close()
    if len(content) > max_bytes:
        raise HTTPException(
            status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
            detail={
                "code": "IMAGE_TOO_LARGE",
                "message": f"Image exceeds the {max_bytes} byte limit.",
            },
        )

    try:
        return await run_in_threadpool(request.app.state.ocr_service.process, content)
    except ImageValidationError as exc:
        raise HTTPException(
            status_code=exc.status_code,
            detail={"code": exc.code, "message": str(exc)},
        ) from exc
    except OCRExecutionError as exc:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail={"code": "OCR_ENGINE_UNAVAILABLE", "message": str(exc)},
        ) from exc
