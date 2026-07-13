import json
import os
import threading
from collections.abc import Iterable, Mapping
from typing import Any, Protocol, runtime_checkable

import cv2
import numpy as np
from numpy.typing import NDArray

from app.config import Settings
from app.schemas import OCRLine
from app.services.errors import OCRExecutionError


@runtime_checkable
class OCREngine(Protocol):
    @property
    def loaded(self) -> bool: ...

    def load(self) -> None: ...

    def recognize(self, image: NDArray[np.uint8]) -> list[OCRLine]: ...


class PaddleOCREngine:
    """Lazy, process-local PaddleOCR adapter.

    PaddleOCR has changed its result container between v2 and v3. The adapter
    accepts the v3 ``predict`` shape and retains a small legacy fallback so the
    service API is insulated from that SDK detail.
    """

    def __init__(self, settings: Settings) -> None:
        self.settings = settings
        self._model: Any | None = None
        self._load_lock = threading.Lock()
        self._inference_lock = threading.Lock()

    @property
    def loaded(self) -> bool:
        return self._model is not None

    def load(self) -> None:
        if self._model is not None:
            return
        with self._load_lock:
            if self._model is not None:
                return
            try:
                _configure_paddle_runtime()
                from paddleocr import PaddleOCR

                options: dict[str, Any] = {"lang": self.settings.language}
                if self.settings.recognition_model_dir is not None:
                    options["text_recognition_model_dir"] = str(
                        self.settings.recognition_model_dir
                    )
                self._model = PaddleOCR(**options)
            except Exception as exc:
                raise OCRExecutionError(
                    f"Could not initialize PaddleOCR: {exc}"
                ) from exc

    def recognize(self, image: NDArray[np.uint8]) -> list[OCRLine]:
        self.load()
        prepared = (
            cv2.cvtColor(image, cv2.COLOR_GRAY2BGR)
            if image.ndim == 2
            else image
        )
        try:
            with self._inference_lock:
                predict = getattr(self._model, "predict", None)
                if callable(predict):
                    results = predict(prepared)
                    lines = self._parse_v3_results(results)
                else:
                    lines = self._parse_legacy_results(
                        self._model.ocr(prepared, cls=True)
                    )
            return sorted(lines, key=_reading_order)
        except OCRExecutionError:
            raise
        except Exception as exc:
            raise OCRExecutionError(f"PaddleOCR inference failed: {exc}") from exc

    def _parse_v3_results(self, results: Any) -> list[OCRLine]:
        lines: list[OCRLine] = []
        for result in _iter_results(results):
            data = _as_mapping(result)
            payload = data.get("res", data)
            texts = _as_list(payload.get("rec_texts", []))
            scores = _as_list(payload.get("rec_scores", []))
            boxes = _as_list(
                payload.get("rec_polys", payload.get("dt_polys", []))
            )
            for index, text in enumerate(texts):
                clean_text = str(text).strip()
                if not clean_text:
                    continue
                score = float(scores[index]) if index < len(scores) else 0.0
                box = _normalize_box(boxes[index] if index < len(boxes) else [])
                lines.append(
                    OCRLine(
                        text=clean_text,
                        confidence=max(0.0, min(score, 1.0)),
                        box=box,
                    )
                )
        return lines

    @staticmethod
    def _parse_legacy_results(results: Any) -> list[OCRLine]:
        lines: list[OCRLine] = []
        pages = results if isinstance(results, list) else [results]
        for page in pages:
            if page is None:
                continue
            entries = page
            if _looks_like_legacy_entry(page):
                entries = [page]
            for entry in entries:
                if not _looks_like_legacy_entry(entry):
                    continue
                box, recognition = entry
                text, confidence = recognition
                clean_text = str(text).strip()
                if clean_text:
                    lines.append(
                        OCRLine(
                            text=clean_text,
                            confidence=max(0.0, min(float(confidence), 1.0)),
                            box=_normalize_box(box),
                        )
                    )
        return lines


def _iter_results(results: Any) -> Iterable[Any]:
    if results is None:
        return []
    if isinstance(results, (Mapping, str, bytes)):
        return [results]
    try:
        return iter(results)
    except TypeError:
        return [results]


def _as_mapping(result: Any) -> Mapping[str, Any]:
    if isinstance(result, Mapping):
        return result
    value = getattr(result, "json", None)
    if callable(value):
        value = value()
    if isinstance(value, str):
        value = json.loads(value)
    if isinstance(value, Mapping):
        return value
    try:
        return dict(result)
    except (TypeError, ValueError) as exc:
        raise OCRExecutionError("PaddleOCR returned an unknown result format.") from exc


def _as_list(value: Any) -> list[Any]:
    if value is None:
        return []
    if isinstance(value, np.ndarray):
        return value.tolist()
    if isinstance(value, list):
        return value
    if isinstance(value, tuple):
        return list(value)
    return [value]


def _normalize_box(value: Any) -> list[list[float]]:
    if isinstance(value, np.ndarray):
        value = value.tolist()
    if not isinstance(value, (list, tuple)):
        return []
    points: list[list[float]] = []
    for point in value:
        if (
            isinstance(point, (list, tuple, np.ndarray))
            and len(point) >= 2
        ):
            points.append([float(point[0]), float(point[1])])
    return points


def _looks_like_legacy_entry(value: Any) -> bool:
    return (
        isinstance(value, (list, tuple))
        and len(value) == 2
        and isinstance(value[1], (list, tuple))
        and len(value[1]) == 2
        and isinstance(value[1][0], str)
    )


def _reading_order(line: OCRLine) -> tuple[float, float]:
    if not line.box:
        return (float("inf"), float("inf"))
    return (
        min(point[1] for point in line.box),
        min(point[0] for point in line.box),
    )


def _configure_paddle_runtime() -> None:
    # PaddleOCR v3 can default to MKLDNN/oneDNN on CPU. On Windows CPU this can
    # fail at inference with a PIR/oneDNN runtime attribute error, so keep the
    # portable Paddle backend unless deployment explicitly opts back in.
    os.environ.setdefault("PADDLE_PDX_ENABLE_MKLDNN_BYDEFAULT", "False")
