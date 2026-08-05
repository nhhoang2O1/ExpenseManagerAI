from datetime import date
from enum import Enum

from pydantic import BaseModel, ConfigDict, Field, field_serializer


def to_camel(value: str) -> str:
    first, *rest = value.split("_")
    return first + "".join(part.capitalize() for part in rest)


class CamelModel(BaseModel):
    model_config = ConfigDict(
        alias_generator=to_camel,
        populate_by_name=True,
    )


class Classification(str, Enum):
    SUPPORTED = "SUPPORTED"
    GENERIC = "GENERIC"
    UNRECOGNIZED = "UNRECOGNIZED"
    LOW_QUALITY = "LOW_QUALITY"


class OCRStatus(str, Enum):
    REVIEW_REQUIRED = "REVIEW_REQUIRED"


class OCRLine(CamelModel):
    text: str
    confidence: float = Field(ge=0, le=1)
    box: list[list[float]] = Field(
        description="Four-point polygon ordered as returned by the OCR engine."
    )


class ExtractedFields(CamelModel):
    store_name: str | None = None
    receipt_date: date | None = None
    total_amount: int | None = Field(default=None, ge=0)
    vat_amount: int | None = Field(default=None, ge=0)

    @field_serializer("store_name")
    def serialize_store_name(self, value: str | None) -> str | None:
        if value is None or value != value.upper():
            return value

        # OCR commonly returns merchant names in all caps. Keep short brand
        # tokens such as "K" or "GS" uppercase while making the API response
        # easier to read. The parser model itself still preserves raw OCR text.
        tokens = []
        for token in value.split():
            if token.isalpha() and len(token) <= 2:
                tokens.append(token)
            else:
                tokens.append(token.capitalize())
        return " ".join(tokens)


class OCRResponse(CamelModel):
    classification: Classification
    status: OCRStatus = OCRStatus.REVIEW_REQUIRED
    raw_text: str
    lines: list[OCRLine]
    fields: ExtractedFields
    overall_confidence: float = Field(ge=0, le=1)
    model_version: str
    parser_version: str
    warnings: list[str]
    processing_time_ms: int = Field(ge=0)


class HealthResponse(CamelModel):
    status: str
    service: str
    model_version: str
    device: str
    model_loaded: bool
