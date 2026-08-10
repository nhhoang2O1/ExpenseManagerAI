import re
import unicodedata
from dataclasses import dataclass
from datetime import date
from typing import Iterable

from app.schemas import Classification, ExtractedFields, OCRLine
from app.services.merchant_extractor import MerchantNameExtractor

AMOUNT_PATTERN = re.compile(
    r"(?<!\d)(\d{1,3}(?:[.,\s]\d{3})+|\d{3,})(?!\d|[.,]\d)"
)
DATE_PATTERNS = (
    re.compile(r"(?<!\d)(\d{1,2})[./-](\d{1,2})[./-](\d{2,4})(?!\d)"),
    re.compile(r"(?<!\d)(\d{4})[./-](\d{1,2})[./-](\d{1,2})(?!\d)"),
)
ENGLISH_DATE_PATTERN = re.compile(
    r"(?<![A-Z0-9])"
    r"(?:(?:MON|TUE|WED|THU|FRI|SAT|SUN)\s*,?\s+)?"
    r"(\d{1,2})\s+"
    r"(JAN(?:UARY)?|FEB(?:RUARY)?|MAR(?:CH)?|APR(?:IL)?|MAY|"
    r"JUN(?:E)?|JUL(?:Y)?|AUG(?:UST)?|SEP(?:TEMBER)?|"
    r"OCT(?:OBER)?|NOV(?:EMBER)?|DEC(?:EMBER)?)\s+"
    r"(\d{4})(?!\d)",
    re.IGNORECASE,
)
ENGLISH_MONTHS = {
    "JAN": 1, "JANUARY": 1,
    "FEB": 2, "FEBRUARY": 2,
    "MAR": 3, "MARCH": 3,
    "APR": 4, "APRIL": 4,
    "MAY": 5,
    "JUN": 6, "JUNE": 6,
    "JUL": 7, "JULY": 7,
    "AUG": 8, "AUGUST": 8,
    "SEP": 9, "SEPTEMBER": 9,
    "OCT": 10, "OCTOBER": 10,
    "NOV": 11, "NOVEMBER": 11,
    "DEC": 12, "DECEMBER": 12,
}

TOTAL_KEYWORDS = (
    ("TONG TIEN THANH TOAN", 110),
    ("TONG THANH TOAN", 105),
    ("TONG CONG", 100),
    ("TONG TIEN", 95),
    ("THANH TIEN", 90),
    ("PHAI TRA", 90),
    ("THANH TOAN", 85),
    ("TIEN KHACH TRA", 70),
    ("TOTAL", 95),
)
VAT_KEYWORDS = ("VAT", "THUE GTGT", "THUE", "GTGT")
POSITIVE_DATE_CONTEXTS = (
    "TRANSACTION DATE", "PAYMENT DATE", "RECEIPT DATE", "PRINT DATE",
)
SECONDARY_DATE_CONTEXTS = (
    "GIO VAO", "GIA VAO", "CHECK IN", "ORDER TIME", "START TIME",
)


@dataclass(frozen=True)
class ParseResult:
    classification: Classification
    fields: ExtractedFields
    warnings: list[str]


@dataclass(frozen=True)
class _AmountCandidate:
    amount: int
    score: float
    line_index: int


class ReceiptParser:
    def __init__(self, merchant_extractor: MerchantNameExtractor | None = None) -> None:
        self.merchant_extractor = merchant_extractor or MerchantNameExtractor()

    def parse(self, lines: list[OCRLine]) -> ParseResult:
        normalized = [_normalize(line.text) for line in lines]
        store_name = self._extract_store(lines)
        receipt_date = self._extract_date(lines, normalized)
        total_amount = self._extract_total(lines, normalized)
        vat_amount = self._extract_vat(lines, normalized)

        fields = ExtractedFields(
            store_name=store_name,
            receipt_date=receipt_date,
            total_amount=total_amount,
            vat_amount=vat_amount,
        )
        warnings: list[str] = []
        if store_name is None:
            warnings.append("STORE_NAME_NOT_FOUND")
        if receipt_date is None:
            warnings.append("RECEIPT_DATE_NOT_FOUND")
        if total_amount is None:
            warnings.append("TOTAL_AMOUNT_NOT_FOUND")

        classification = self._classify(
            normalized=normalized,
            store_name=store_name,
            receipt_date=receipt_date,
            total_amount=total_amount,
        )
        return ParseResult(
            classification=classification,
            fields=fields,
            warnings=warnings,
        )

    def _extract_store(self, lines: list[OCRLine]) -> str | None:
        return self.merchant_extractor.extract(lines)

    @staticmethod
    def _extract_date(
        lines: list[OCRLine], normalized: list[str]
    ) -> date | None:
        candidates: list[tuple[date, float]] = []
        for index, (line, text) in enumerate(zip(lines, normalized)):
            for pattern_index, pattern in enumerate(DATE_PATTERNS):
                for match in pattern.finditer(line.text):
                    groups = [int(value) for value in match.groups()]
                    if pattern_index == 0:
                        day, month, year = groups
                    else:
                        year, month, day = groups
                    if year < 100:
                        year += 2000
                    try:
                        parsed = date(year, month, day)
                    except ValueError:
                        continue
                    keyword_bonus = _date_context_score(index, normalized)
                    position_bonus = max(0, 0.04 - index * 0.003)
                    candidates.append(
                        (parsed, min(0.96, 0.82 + keyword_bonus + position_bonus))
                    )
            for match in ENGLISH_DATE_PATTERN.finditer(line.text):
                day_text, month_text, year_text = match.groups()
                try:
                    parsed = date(
                        int(year_text),
                        ENGLISH_MONTHS[month_text.upper()],
                        int(day_text),
                    )
                except (KeyError, TypeError, ValueError):
                    continue
                keyword_bonus = _date_context_score(index, normalized)
                position_bonus = max(0, 0.04 - index * 0.003)
                candidates.append(
                    (parsed, min(0.96, 0.82 + keyword_bonus + position_bonus))
                )
        return max(candidates, key=lambda item: item[1])[0] if candidates else None

    def _extract_total(
        self, lines: list[OCRLine], normalized: list[str]
    ) -> int | None:
        candidates: list[_AmountCandidate] = []
        line_count = max(len(lines), 1)
        for index, text in enumerate(normalized):
            if any(keyword in text for keyword in VAT_KEYWORDS):
                continue
            keyword_score = next(
                (score for keyword, score in TOTAL_KEYWORDS if keyword in text),
                0,
            )
            amounts = _amounts(lines[index].text)
            if keyword_score and not amounts and index + 1 < len(lines):
                amounts = _amounts(lines[index + 1].text)
            for amount in amounts:
                candidates.append(
                    _AmountCandidate(
                        amount=amount,
                        score=keyword_score + 8 * index / line_count,
                        line_index=index,
                    )
                )

            if not keyword_score and "VND" in text:
                for amount in amounts:
                    candidates.append(
                        _AmountCandidate(
                            amount=amount,
                            score=35 + 8 * index / line_count,
                            line_index=index,
                        )
                    )

        document_box = _document_box(lines)
        for label_index, text in enumerate(normalized):
            if any(keyword in text for keyword in VAT_KEYWORDS):
                continue
            keyword_score = next(
                (score for keyword, score in TOTAL_KEYWORDS if keyword in text),
                0,
            )
            if not keyword_score:
                continue
            for value_index, value_line in enumerate(lines):
                if value_index == label_index or not _same_row(
                    lines[label_index], value_line
                ):
                    continue
                for amount in _amounts(value_line.text):
                    score = keyword_score + 20
                    label_box = _line_box(lines[label_index])
                    value_box = _line_box(value_line)
                    if label_box is not None and value_box is not None:
                        if value_box[0] >= label_box[2]:
                            score += 14
                        if document_box is not None:
                            document_width = max(document_box[2] - document_box[0], 1)
                            document_height = max(document_box[3] - document_box[1], 1)
                            value_x = (value_box[0] + value_box[2]) / 2
                            value_y = (value_box[1] + value_box[3]) / 2
                            score += 8 * (value_x - document_box[0]) / document_width
                            if (value_y - document_box[1]) / document_height >= 0.7:
                                score += 8
                    if amount < 1_000:
                        score -= 15
                    candidates.append(
                        _AmountCandidate(
                            amount=amount,
                            score=score,
                            line_index=value_index,
                        )
                    )

        if not candidates:
            return None
        best = max(candidates, key=lambda item: (item.score, item.amount))
        return best.amount

    @staticmethod
    def _extract_vat(
        lines: list[OCRLine], normalized: list[str]
    ) -> int | None:
        candidates: list[_AmountCandidate] = []
        for index, text in enumerate(normalized):
            if not any(keyword in text for keyword in VAT_KEYWORDS):
                continue
            amounts = _amounts(lines[index].text)
            if not amounts and index + 1 < len(lines):
                amounts = _amounts(lines[index + 1].text)
            for amount in amounts:
                candidates.append(
                    _AmountCandidate(amount=amount, score=90, line_index=index)
                )
        if not candidates:
            return None
        best = max(candidates, key=lambda item: (item.score, item.line_index))
        return best.amount

    @staticmethod
    def _classify(
        normalized: list[str],
        store_name: str | None,
        receipt_date: date | None,
        total_amount: int | None,
    ) -> Classification:
        required_fields = sum(
            (store_name is not None, receipt_date is not None, total_amount is not None)
        )
        if required_fields == 3:
            return Classification.SUPPORTED

        joined = "\n".join(normalized)
        signals = sum(
            (
                store_name is not None,
                receipt_date is not None,
                total_amount is not None,
                any(token in joined for token in ("HOA DON", "RECEIPT", "PHIEU TINH")),
                any(token in joined for token in ("VND", "VAT", "THUE")),
            )
        )
        return (
            Classification.GENERIC if signals >= 2 else Classification.UNRECOGNIZED
        )


def _amounts(text: str) -> list[int]:
    values: list[int] = []
    for match in AMOUNT_PATTERN.finditer(text):
        digits = re.sub(r"\D", "", match.group(1))
        if not digits:
            continue
        value = int(digits)
        if 0 < value <= 10**12:
            values.append(value)
    return values


def _date_context_score(index: int, normalized: list[str]) -> float:
    current = normalized[index]
    previous = normalized[index - 1] if index > 0 else ""
    score = 0.0
    if "NGAY" in current or "DATE" in current:
        score += 0.08
    if any(context in current for context in POSITIVE_DATE_CONTEXTS):
        score += 0.04
    elif any(context in previous for context in POSITIVE_DATE_CONTEXTS):
        score += 0.06
    if any(
        context in current or context in previous
        for context in SECONDARY_DATE_CONTEXTS
    ):
        score -= 0.12
    return score


def _line_box(line: OCRLine) -> tuple[float, float, float, float] | None:
    points = [point for point in line.box if len(point) >= 2]
    if len(points) < 2:
        return None
    xs = [float(point[0]) for point in points]
    ys = [float(point[1]) for point in points]
    return min(xs), min(ys), max(xs), max(ys)


def _document_box(lines: list[OCRLine]) -> tuple[float, float, float, float] | None:
    boxes = [box for line in lines if (box := _line_box(line)) is not None]
    if not boxes:
        return None
    return (
        min(box[0] for box in boxes),
        min(box[1] for box in boxes),
        max(box[2] for box in boxes),
        max(box[3] for box in boxes),
    )


def _same_row(first: OCRLine, second: OCRLine) -> bool:
    first_box = _line_box(first)
    second_box = _line_box(second)
    if first_box is None or second_box is None:
        return False
    overlap = max(0.0, min(first_box[3], second_box[3]) - max(first_box[1], second_box[1]))
    min_height = max(min(first_box[3] - first_box[1], second_box[3] - second_box[1]), 1)
    if overlap / min_height >= 0.4:
        return True
    first_center = (first_box[1] + first_box[3]) / 2
    second_center = (second_box[1] + second_box[3]) / 2
    max_height = max(first_box[3] - first_box[1], second_box[3] - second_box[1], 1)
    return abs(first_center - second_center) <= max_height * 0.6


def _normalize(value: str) -> str:
    decomposed = unicodedata.normalize("NFD", value.upper())
    without_marks = "".join(
        character
        for character in decomposed
        if unicodedata.category(character) != "Mn"
    )
    return re.sub(r"\s+", " ", without_marks.replace("\u0110", "D")).strip()


def deduplicate_warnings(values: Iterable[str]) -> list[str]:
    return list(dict.fromkeys(values))
