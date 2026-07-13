import cv2
import numpy as np

from app.schemas import OCRLine


def line(text: str, confidence: float = 0.95, y: float = 0) -> OCRLine:
    return OCRLine(
        text=text,
        confidence=confidence,
        box=[[0, y], [200, y], [200, y + 20], [0, y + 20]],
    )


def encoded_test_image(width: int = 500, height: int = 700) -> bytes:
    rng = np.random.default_rng(42)
    image = rng.integers(60, 220, size=(height, width, 3), dtype=np.uint8)
    for y in range(50, height - 20, 45):
        cv2.line(image, (30, y), (width - 30, y), (10, 10, 10), 2)
    success, encoded = cv2.imencode(".png", image)
    assert success
    return encoded.tobytes()
