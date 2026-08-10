import cv2
import numpy as np
import pytest

from app.config import Settings
from app.services.errors import ImageValidationError
from app.services.preprocessing import ImagePreprocessor
from tests.helpers import encoded_test_image


def test_rejects_undecodable_image() -> None:
    preprocessor = ImagePreprocessor(Settings())

    with pytest.raises(ImageValidationError) as error:
        preprocessor.process(b"not an image")

    assert error.value.code == "INVALID_IMAGE"
    assert error.value.status_code == 400


def test_rejects_image_below_minimum_dimensions() -> None:
    image = np.full((32, 32, 3), 127, dtype=np.uint8)
    success, encoded = cv2.imencode(".png", image)
    assert success

    with pytest.raises(ImageValidationError) as error:
        ImagePreprocessor(Settings()).process(encoded.tobytes())

    assert error.value.code == "IMAGE_TOO_SMALL"


def test_resizes_long_side_to_configured_limit() -> None:
    settings = Settings(max_image_side=640)
    result = ImagePreprocessor(settings).process(
        encoded_test_image(width=900, height=1200)
    )

    assert max(result.image.shape[:2]) <= 640


def test_marks_blank_dark_image_as_low_quality() -> None:
    image = np.zeros((300, 300, 3), dtype=np.uint8)
    success, encoded = cv2.imencode(".png", image)
    assert success

    result = ImagePreprocessor(Settings()).process(encoded.tobytes())

    assert result.low_quality is True
    assert "IMAGE_TOO_DARK" in result.warnings
    assert "LOW_CONTENT" in result.warnings


def test_perspective_correction_skips_deskew(monkeypatch) -> None:
    preprocessor = ImagePreprocessor(Settings())
    image = np.zeros((300, 200, 3), dtype=np.uint8)
    perspective = np.full((250, 150, 3), 127, dtype=np.uint8)

    monkeypatch.setattr(
        preprocessor,
        "_find_receipt_and_warp",
        lambda _: perspective,
    )

    def unexpected_deskew(_: np.ndarray) -> float | None:
        pytest.fail("deskew must not run after perspective correction")

    monkeypatch.setattr(preprocessor, "_estimate_skew", unexpected_deskew)

    corrected, warnings = preprocessor._geometry_corrections(image)

    assert corrected is perspective
    assert warnings == ["PERSPECTIVE_CORRECTED"]


def test_deskews_when_receipt_boundary_is_not_found(monkeypatch) -> None:
    preprocessor = ImagePreprocessor(Settings())
    image = np.zeros((300, 200, 3), dtype=np.uint8)
    rotated = np.full_like(image, 127)

    monkeypatch.setattr(
        preprocessor,
        "_find_receipt_and_warp",
        lambda _: None,
    )
    monkeypatch.setattr(preprocessor, "_estimate_skew", lambda _: 4.0)
    monkeypatch.setattr(preprocessor, "_rotate", lambda _, __: rotated)

    corrected, warnings = preprocessor._geometry_corrections(image)

    assert corrected is rotated
    assert warnings == ["DESKEWED"]


def test_high_frequency_image_is_not_denoised() -> None:
    checkerboard = (np.indices((128, 128)).sum(axis=0) % 2 * 255).astype(np.uint8)
    image = cv2.cvtColor(checkerboard, cv2.COLOR_GRAY2BGR)

    enhanced = ImagePreprocessor._enhance_when_useful(image)

    assert np.array_equal(enhanced, checkerboard)
