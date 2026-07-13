from functools import lru_cache
from pathlib import Path

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_prefix="OCR_",
        env_file=".env",
        extra="ignore",
    )

    app_name: str = "receipt-ocr-service"
    model_version: str = "paddleocr-v3-vi-pretrained"
    parser_version: str = "receipt-parser-v1"
    language: str = "vi"
    recognition_model_dir: Path | None = None
    preload_model: bool = False

    max_upload_bytes: int = Field(default=10 * 1024 * 1024, ge=1024)
    max_image_pixels: int = Field(default=40_000_000, ge=1_000_000)
    max_image_side: int = Field(default=2200, ge=640)
    min_image_side: int = Field(default=64, ge=16)
    low_ocr_confidence: float = Field(default=0.45, ge=0, le=1)


@lru_cache
def get_settings() -> Settings:
    return Settings()
