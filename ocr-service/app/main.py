from contextlib import asynccontextmanager
from typing import AsyncIterator

from fastapi import FastAPI

from app.config import Settings, get_settings
from app.routes.health import router as health_router
from app.routes.ocr import router as ocr_router
from app.services.paddle_ocr import OCREngine, PaddleOCREngine
from app.services.preprocessing import ImagePreprocessor
from app.services.receipt_ocr import ReceiptOCRService


def create_app(
    settings: Settings | None = None,
    ocr_engine: OCREngine | None = None,
) -> FastAPI:
    resolved_settings = settings or get_settings()
    engine = ocr_engine or PaddleOCREngine(resolved_settings)
    service = ReceiptOCRService(
        settings=resolved_settings,
        preprocessor=ImagePreprocessor(resolved_settings),
        ocr_engine=engine,
    )

    @asynccontextmanager
    async def lifespan(_: FastAPI) -> AsyncIterator[None]:
        if resolved_settings.preload_model:
            engine.load()
        yield

    app = FastAPI(
        title="Receipt OCR Service",
        version="0.1.0",
        docs_url="/docs",
        redoc_url=None,
        lifespan=lifespan,
    )
    app.state.settings = resolved_settings
    app.state.ocr_service = service
    app.include_router(health_router)
    app.include_router(ocr_router)
    return app


app = create_app()
