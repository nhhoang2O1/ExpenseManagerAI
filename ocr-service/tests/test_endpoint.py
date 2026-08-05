from typing import ClassVar

import numpy as np
from fastapi.testclient import TestClient
from numpy.typing import NDArray

from app.config import Settings
from app.main import create_app
from app.schemas import OCRLine
from app.services.errors import OCRExecutionError
from tests.helpers import encoded_test_image, line


class FakeOCREngine:
    loaded: ClassVar[bool] = True

    def load(self) -> None:
        raise AssertionError("Fake engine must not load a Paddle model")

    def recognize(self, image: NDArray[np.uint8]) -> list[OCRLine]:
        assert image.size > 0
        return [
            line("CIRCLE K", y=0),
            line("Ngay 09/07/2026", y=30),
            line("TONG THANH TOAN 125.000 VND", y=60),
        ]


class LowConfidenceEngine(FakeOCREngine):
    def recognize(self, image: NDArray[np.uint8]) -> list[OCRLine]:
        return [line("raw but uncertain text", confidence=0.2)]


class FailingEngine(FakeOCREngine):
    def recognize(self, image: NDArray[np.uint8]) -> list[OCRLine]:
        raise OCRExecutionError("model unavailable")


def make_client(engine: FakeOCREngine | None = None) -> TestClient:
    app = create_app(
        settings=Settings(model_version="test-model", parser_version="test-parser"),
        ocr_engine=engine or FakeOCREngine(),
    )
    return TestClient(app)


def test_health_does_not_initialize_real_model() -> None:
    with make_client() as client:
        response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {
        "status": "ok",
        "service": "receipt-ocr-service",
        "modelVersion": "test-model",
        "device": "cpu",
        "modelLoaded": True,
    }


def test_receipt_endpoint_returns_camel_case_contract() -> None:
    with make_client() as client:
        response = client.post(
            "/internal/v1/ocr/receipts",
            files={"image": ("receipt.png", encoded_test_image(), "image/png")},
        )

    assert response.status_code == 200
    payload = response.json()
    assert payload["classification"] == "SUPPORTED"
    assert payload["status"] == "REVIEW_REQUIRED"
    assert payload["rawText"].startswith("CIRCLE K")
    assert payload["fields"] == {
        "storeName": "Circle K",
        "receiptDate": "2026-07-09",
        "totalAmount": 125000,
        "vatAmount": None,
    }
    assert payload["modelVersion"] == "test-model"
    assert payload["parserVersion"] == "test-parser"
    assert payload["lines"][0]["box"]
    assert payload["overallConfidence"] == 0.95
    assert "processingTimeMs" in payload
    assert all("_" not in key for key in payload)


def test_receipt_endpoint_rejects_wrong_media_type() -> None:
    with make_client() as client:
        response = client.post(
            "/internal/v1/ocr/receipts",
            files={"image": ("receipt.txt", b"text", "text/plain")},
        )

    assert response.status_code == 415
    assert response.json()["detail"]["code"] == "UNSUPPORTED_IMAGE_TYPE"


def test_receipt_endpoint_reports_invalid_image() -> None:
    with make_client() as client:
        response = client.post(
            "/internal/v1/ocr/receipts",
            files={"image": ("receipt.png", b"broken", "image/png")},
        )

    assert response.status_code == 400
    assert response.json()["detail"]["code"] == "INVALID_IMAGE"


def test_low_confidence_response_keeps_raw_text() -> None:
    with make_client(LowConfidenceEngine()) as client:
        response = client.post(
            "/internal/v1/ocr/receipts",
            files={"image": ("receipt.png", encoded_test_image(), "image/png")},
        )

    assert response.status_code == 200
    assert response.json()["classification"] == "LOW_QUALITY"
    assert response.json()["rawText"] == "raw but uncertain text"
    assert response.json()["overallConfidence"] == 0.2
    assert "LOW_OCR_CONFIDENCE" in response.json()["warnings"]


def test_ocr_engine_failure_returns_service_unavailable() -> None:
    with make_client(FailingEngine()) as client:
        response = client.post(
            "/internal/v1/ocr/receipts",
            files={"image": ("receipt.png", encoded_test_image(), "image/png")},
        )

    assert response.status_code == 503
    assert response.json()["detail"]["code"] == "OCR_ENGINE_UNAVAILABLE"
