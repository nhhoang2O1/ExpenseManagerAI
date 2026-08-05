from datetime import date

import pytest
from pydantic import ValidationError

from app.config import Settings
from app.schemas import ExtractedFields, OCRLine, to_camel


def test_to_camel_converts_api_field_names() -> None:
    assert to_camel("store_name") == "storeName"
    assert to_camel("processing_time_ms") == "processingTimeMs"
    assert to_camel("status") == "status"


def test_extracted_fields_accept_camel_case_and_serialize_readable_merchant_name() -> None:
    fields = ExtractedFields.model_validate(
        {
            "storeName": "GS 25 VIETNAM",
            "receiptDate": "2026-07-09",
            "totalAmount": 125_000,
            "vatAmount": 8_000,
        }
    )

    assert fields.store_name == "GS 25 VIETNAM"
    assert fields.receipt_date == date(2026, 7, 9)
    assert fields.model_dump(by_alias=True, mode="json") == {
        "storeName": "GS 25 Vietnam",
        "receiptDate": "2026-07-09",
        "totalAmount": 125_000,
        "vatAmount": 8_000,
    }


@pytest.mark.parametrize(
    ("model", "payload"),
    [
        (OCRLine, {"text": "total", "confidence": -0.01, "box": []}),
        (OCRLine, {"text": "total", "confidence": 1.01, "box": []}),
        (ExtractedFields, {"totalAmount": -1}),
        (ExtractedFields, {"vatAmount": -1}),
    ],
)
def test_schema_rejects_values_outside_the_api_contract(model: type, payload: dict) -> None:
    with pytest.raises(ValidationError):
        model.model_validate(payload)


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("max_upload_bytes", 1023),
        ("max_image_pixels", 999_999),
        ("max_image_side", 639),
        ("min_image_side", 15),
        ("low_ocr_confidence", 1.01),
    ],
)
def test_settings_reject_unsafe_processing_limits(field: str, value: int | float) -> None:
    with pytest.raises(ValidationError):
        Settings(**{field: value})


def test_settings_rejects_unsupported_ocr_device() -> None:
    with pytest.raises(ValidationError):
        Settings(device="cuda")
