import logging
from time import perf_counter

from app.config import Settings
from app.schemas import Classification, OCRResponse, OCRStatus
from app.services.paddle_ocr import OCREngine
from app.services.parsers import ReceiptParser, deduplicate_warnings
from app.services.preprocessing import ImagePreprocessor

logger = logging.getLogger(__name__)


class ReceiptOCRService:
    def __init__(
        self,
        settings: Settings,
        preprocessor: ImagePreprocessor,
        ocr_engine: OCREngine,
        parser: ReceiptParser | None = None,
    ) -> None:
        self.settings = settings
        self.preprocessor = preprocessor
        self.ocr_engine = ocr_engine
        self.parser = parser or ReceiptParser()

    @property
    def model_loaded(self) -> bool:
        return bool(getattr(self.ocr_engine, "loaded", False))

    def process(self, content: bytes) -> OCRResponse:
        started = perf_counter()
        preprocess_started = perf_counter()
        preprocessed = self.preprocessor.process(content)
        preprocess_ms = round((perf_counter() - preprocess_started) * 1000)

        recognition_started = perf_counter()
        lines = self.ocr_engine.recognize(preprocessed.image)
        recognition_ms = round((perf_counter() - recognition_started) * 1000)

        parser_started = perf_counter()
        parsed = self.parser.parse(lines)
        parser_ms = round((perf_counter() - parser_started) * 1000)

        raw_text = "\n".join(line.text for line in lines)
        warnings = [*preprocessed.warnings, *parsed.warnings]
        ocr_confidence = (
            sum(line.confidence for line in lines) / len(lines) if lines else 0.0
        )

        classification = parsed.classification
        if not lines:
            warnings.append("NO_TEXT_DETECTED")
        if lines and ocr_confidence < self.settings.low_ocr_confidence:
            warnings.append("LOW_OCR_CONFIDENCE")
        if preprocessed.low_quality or (
            lines and ocr_confidence < self.settings.low_ocr_confidence
        ):
            classification = Classification.LOW_QUALITY

        elapsed_ms = max(0, round((perf_counter() - started) * 1000))
        logger.info(
            "ocr_timing preprocess_ms=%s recognition_ms=%s parser_ms=%s "
            "total_ms=%s lines=%s",
            preprocess_ms,
            recognition_ms,
            parser_ms,
            elapsed_ms,
            len(lines),
        )
        return OCRResponse(
            classification=classification,
            status=OCRStatus.REVIEW_REQUIRED,
            raw_text=raw_text,
            lines=lines,
            fields=parsed.fields,
            overall_confidence=round(ocr_confidence, 4),
            model_version=self.settings.model_version,
            parser_version=self.settings.parser_version,
            warnings=deduplicate_warnings(warnings),
            processing_time_ms=elapsed_ms,
        )
