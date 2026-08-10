from dataclasses import dataclass
from typing import Iterable

import cv2
import numpy as np
from numpy.typing import NDArray

from app.config import Settings
from app.services.errors import ImageValidationError

ImageArray = NDArray[np.uint8]


@dataclass(frozen=True)
class QualityMetrics:
    brightness: float
    contrast: float
    sharpness: float
    edge_ratio: float


@dataclass(frozen=True)
class PreprocessResult:
    image: ImageArray
    metrics: QualityMetrics
    warnings: list[str]
    low_quality: bool


class ImagePreprocessor:
    def __init__(self, settings: Settings) -> None:
        self.settings = settings

    def process(self, content: bytes) -> PreprocessResult:
        image = self._decode_and_validate(content)
        image = self._resize(image)
        original_metrics = self._measure(image)

        candidate, transform_warnings = self._geometry_corrections(image)
        candidate = self._enhance_when_useful(candidate)
        candidate_metrics = self._measure(candidate)

        original_score = self._quality_score(original_metrics)
        candidate_score = self._quality_score(candidate_metrics)
        transformed = bool(transform_warnings)
        use_candidate = (
            candidate_score >= original_score * 0.95
            if transformed
            else candidate_score > original_score + 0.01
        )

        selected = candidate if use_candidate else image
        selected_metrics = candidate_metrics if use_candidate else original_metrics
        warnings = self._quality_warnings(selected_metrics)
        if use_candidate:
            warnings.extend(transform_warnings)
            warnings.append("ENHANCED_IMAGE_SELECTED")

        return PreprocessResult(
            image=selected,
            metrics=selected_metrics,
            warnings=_deduplicate(warnings),
            low_quality=self._is_low_quality(selected_metrics),
        )

    def _decode_and_validate(self, content: bytes) -> ImageArray:
        if not content:
            raise ImageValidationError(
                "The uploaded image is empty.",
                code="EMPTY_IMAGE",
                status_code=400,
            )
        if len(content) > self.settings.max_upload_bytes:
            raise ImageValidationError(
                "The uploaded image exceeds the configured size limit.",
                code="IMAGE_TOO_LARGE",
                status_code=413,
            )

        encoded = np.frombuffer(content, dtype=np.uint8)
        image = cv2.imdecode(encoded, cv2.IMREAD_UNCHANGED)
        if image is None:
            raise ImageValidationError(
                "The uploaded file could not be decoded as an image.",
                code="INVALID_IMAGE",
                status_code=400,
            )

        height, width = image.shape[:2]
        if min(height, width) < self.settings.min_image_side:
            raise ImageValidationError(
                f"Image dimensions must be at least {self.settings.min_image_side}px.",
                code="IMAGE_TOO_SMALL",
            )
        if height * width > self.settings.max_image_pixels:
            raise ImageValidationError(
                "Decoded image dimensions exceed the configured pixel limit.",
                code="IMAGE_DIMENSIONS_TOO_LARGE",
                status_code=413,
            )

        if image.ndim == 2:
            return cv2.cvtColor(image, cv2.COLOR_GRAY2BGR)
        if image.shape[2] == 4:
            return cv2.cvtColor(image, cv2.COLOR_BGRA2BGR)
        if image.shape[2] != 3:
            raise ImageValidationError(
                "The image has an unsupported channel layout.",
                code="INVALID_IMAGE_CHANNELS",
            )
        return image

    def _resize(self, image: ImageArray) -> ImageArray:
        height, width = image.shape[:2]
        longest = max(height, width)
        if longest <= self.settings.max_image_side:
            return image
        scale = self.settings.max_image_side / longest
        return cv2.resize(
            image,
            (max(1, round(width * scale)), max(1, round(height * scale))),
            interpolation=cv2.INTER_AREA,
        )

    def _geometry_corrections(
        self, image: ImageArray
    ) -> tuple[ImageArray, list[str]]:
        perspective = self._find_receipt_and_warp(image)
        if perspective is not None:
            return perspective, ["PERSPECTIVE_CORRECTED"]

        angle = self._estimate_skew(image)
        if angle is not None:
            return self._rotate(image, angle), ["DESKEWED"]

        return image, []

    def _find_receipt_and_warp(self, image: ImageArray) -> ImageArray | None:
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        blurred = cv2.GaussianBlur(gray, (5, 5), 0)
        edges = cv2.Canny(blurred, 50, 150)
        edges = cv2.morphologyEx(
            edges,
            cv2.MORPH_CLOSE,
            cv2.getStructuringElement(cv2.MORPH_RECT, (7, 7)),
        )
        contours, _ = cv2.findContours(
            edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE
        )
        image_area = image.shape[0] * image.shape[1]
        for contour in sorted(contours, key=cv2.contourArea, reverse=True)[:5]:
            area_ratio = cv2.contourArea(contour) / image_area
            if not 0.35 <= area_ratio <= 0.96:
                continue
            perimeter = cv2.arcLength(contour, True)
            polygon = cv2.approxPolyDP(contour, 0.02 * perimeter, True)
            if len(polygon) != 4 or not cv2.isContourConvex(polygon):
                continue
            return self._four_point_transform(image, polygon.reshape(4, 2))
        return None

    @staticmethod
    def _four_point_transform(image: ImageArray, points: NDArray) -> ImageArray:
        points = points.astype(np.float32)
        sums = points.sum(axis=1)
        differences = np.diff(points, axis=1).reshape(-1)
        ordered = np.array(
            [
                points[np.argmin(sums)],
                points[np.argmin(differences)],
                points[np.argmax(sums)],
                points[np.argmax(differences)],
            ],
            dtype=np.float32,
        )
        top_left, top_right, bottom_right, bottom_left = ordered
        width = int(
            max(
                np.linalg.norm(bottom_right - bottom_left),
                np.linalg.norm(top_right - top_left),
            )
        )
        height = int(
            max(
                np.linalg.norm(top_right - bottom_right),
                np.linalg.norm(top_left - bottom_left),
            )
        )
        if width < 32 or height < 32:
            return image
        destination = np.array(
            [[0, 0], [width - 1, 0], [width - 1, height - 1], [0, height - 1]],
            dtype=np.float32,
        )
        matrix = cv2.getPerspectiveTransform(ordered, destination)
        return cv2.warpPerspective(
            image,
            matrix,
            (width, height),
            flags=cv2.INTER_CUBIC,
            borderMode=cv2.BORDER_REPLICATE,
        )

    @staticmethod
    def _estimate_skew(image: ImageArray) -> float | None:
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        edges = cv2.Canny(gray, 50, 150)
        min_line_length = max(30, image.shape[1] // 5)
        lines = cv2.HoughLinesP(
            edges,
            1,
            np.pi / 180,
            threshold=60,
            minLineLength=min_line_length,
            maxLineGap=20,
        )
        if lines is None:
            return None
        angles: list[float] = []
        for x1, y1, x2, y2 in lines[:, 0]:
            angle = float(np.degrees(np.arctan2(y2 - y1, x2 - x1)))
            if -15 <= angle <= 15:
                angles.append(angle)
        if len(angles) < 4:
            return None
        median = float(np.median(angles))
        return median if 1.0 <= abs(median) <= 12.0 else None

    @staticmethod
    def _rotate(image: ImageArray, angle: float) -> ImageArray:
        height, width = image.shape[:2]
        center = (width / 2, height / 2)
        matrix = cv2.getRotationMatrix2D(center, angle, 1.0)
        return cv2.warpAffine(
            image,
            matrix,
            (width, height),
            flags=cv2.INTER_CUBIC,
            borderMode=cv2.BORDER_REPLICATE,
        )

    @staticmethod
    def _enhance_when_useful(image: ImageArray) -> ImageArray:
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        contrast = float(gray.std())
        if contrast < 55:
            gray = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8)).apply(gray)
        return gray

    @staticmethod
    def _measure(image: ImageArray) -> QualityMetrics:
        gray = (
            image
            if image.ndim == 2
            else cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        )
        return QualityMetrics(
            brightness=float(gray.mean()),
            contrast=float(gray.std()),
            sharpness=float(cv2.Laplacian(gray, cv2.CV_64F).var()),
            edge_ratio=float(np.count_nonzero(cv2.Canny(gray, 50, 150)) / gray.size),
        )

    @staticmethod
    def _quality_score(metrics: QualityMetrics) -> float:
        exposure = max(0.0, 1.0 - abs(metrics.brightness - 145.0) / 145.0)
        contrast = min(metrics.contrast / 60.0, 1.0)
        sharpness = min(metrics.sharpness / 250.0, 1.0)
        content = min(metrics.edge_ratio / 0.04, 1.0)
        return 0.2 * exposure + 0.3 * contrast + 0.3 * sharpness + 0.2 * content

    @staticmethod
    def _quality_warnings(metrics: QualityMetrics) -> list[str]:
        warnings: list[str] = []
        if metrics.brightness < 35:
            warnings.append("IMAGE_TOO_DARK")
        elif metrics.brightness > 245:
            warnings.append("IMAGE_OVEREXPOSED")
        if metrics.contrast < 18:
            warnings.append("LOW_CONTRAST")
        if metrics.sharpness < 45:
            warnings.append("BLURRY_IMAGE")
        if metrics.edge_ratio < 0.002:
            warnings.append("LOW_CONTENT")
        return warnings

    @staticmethod
    def _is_low_quality(metrics: QualityMetrics) -> bool:
        severe_exposure = metrics.brightness < 20 or metrics.brightness > 250
        severe_detail_loss = metrics.contrast < 10 or metrics.edge_ratio < 0.001
        severe_blur = metrics.sharpness < 20
        moderate_issues = sum(
            (
                metrics.contrast < 18,
                metrics.sharpness < 45,
                metrics.edge_ratio < 0.002,
                metrics.brightness < 35 or metrics.brightness > 245,
            )
        )
        return severe_exposure or severe_detail_loss or severe_blur or moderate_issues >= 2


def _deduplicate(values: Iterable[str]) -> list[str]:
    return list(dict.fromkeys(values))
