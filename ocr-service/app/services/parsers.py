import re
import unicodedata
from dataclasses import dataclass
from datetime import date
from typing import Iterable

from app.schemas import Classification, ExtractedFields, OCRLine

AMOUNT_PATTERN = re.compile(
    r"(?<!\d)(\d{1,3}(?:[.,\s]\d{3})+|\d{3,})(?!\d)"
)
DATE_PATTERNS = (
    re.compile(r"(?<!\d)(\d{1,2})[./-](\d{1,2})[./-](\d{2,4})(?!\d)"),
    re.compile(r"(?<!\d)(\d{4})[./-](\d{1,2})[./-](\d{1,2})(?!\d)"),
)

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
STORE_BLOCKLIST = (
    "HOA DON",
    "RECEIPT",
    "PHIEU",
    "TONG",
    "THANH TOAN",
    "CAM ON",
    "MA SO THUE",
    "MST",
    "DIA CHI",
    "NGAY",
    "DATE",
    "TEL",
    "MA HD",
    "SO HD",
    "MA HOA DON",
    "NHAN VIEN",
    "THU NGAN",
    "BAN:",
    "GIO VAO",
    "GIO RA",
    "STT",
    "TEN MON",
    "DON GIA",
)
ADDRESS_SIGNALS = (
    "DIA CHI",
    "HOTLINE",
    "VIET NAM",
    "QUAN ",
    "PHUONG ",
    "DUONG ",
)


@dataclass(frozen=True)
class ParseResult:
    classification: Classification
    fields: ExtractedFields
    confidence: float
    warnings: list[str]


@dataclass(frozen=True)
class _AmountCandidate:
    amount: int
    score: float
    line_index: int


@dataclass(frozen=True)
class _StoreMatch:
    name: str
    confidence: float


class GenericReceiptParser:
    @staticmethod
    def match_store(
        lines: list[OCRLine], normalized_lines: list[str]
    ) -> _StoreMatch | None:
        candidates: list[tuple[float, str]] = []
        for index, (line, text) in enumerate(zip(lines, normalized_lines)):
            clean = line.text.strip()
            letters = sum(character.isalpha() for character in clean)
            if not (
                3 <= len(clean) <= 80
                and letters >= 3
                and letters / max(len(clean), 1) >= 0.4
            ) or any(keyword in text for keyword in STORE_BLOCKLIST):
                continue

            # A merchant name is commonly next to its address or hotline. This
            # is more reliable than blindly taking the first alphabetic line,
            # which often is a receipt code or cashier name on Vietnamese bills.
            nearby = normalized_lines[max(0, index - 1) : index + 4]
            address_bonus = 0.35 if any(
                signal in candidate
                for candidate in nearby
                for signal in ADDRESS_SIGNALS
            ) else 0.0
            header_bonus = 0.08 if index < 6 else 0.0
            candidates.append((0.45 + address_bonus + header_bonus, clean))

        if not candidates:
            return None
        score, name = max(candidates, key=lambda candidate: candidate[0])
        return _StoreMatch(name=name, confidence=min(0.85, score))


class ReceiptParser:
    def __init__(self) -> None:
        self.generic_parser = GenericReceiptParser()

    def parse(self, lines: list[OCRLine]) -> ParseResult:
        normalized = [_normalize(line.text) for line in lines]
        store_name, store_confidence = self._extract_store(lines, normalized)
        receipt_date, date_confidence = self._extract_date(lines, normalized)
        total_amount, total_confidence = self._extract_total(lines, normalized)
        vat_amount, vat_confidence = self._extract_vat(lines, normalized)

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
        required_confidences = [
            store_confidence,
            date_confidence,
            total_confidence,
        ]
        present_confidences = [value for value in required_confidences if value > 0]
        field_confidence = (
            sum(present_confidences) / 3.0 if present_confidences else 0.0
        )
        if vat_amount is not None:
            field_confidence = min(1.0, 0.9 * field_confidence + 0.1 * vat_confidence)

        return ParseResult(
            classification=classification,
            fields=fields,
            confidence=round(field_confidence, 4),
            warnings=warnings,
        )

    def _extract_store(
        self,
        lines: list[OCRLine], normalized: list[str]
    ) -> tuple[str | None, float]:
        match = self.generic_parser.match_store(lines, normalized)
        if match is not None:
            return match.name, match.confidence
        return None, 0.0

    @staticmethod
    def _extract_date(
        lines: list[OCRLine], normalized: list[str]
    ) -> tuple[date | None, float]:
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
                    keyword_bonus = 0.08 if "NGAY" in text or "DATE" in text else 0
                    position_bonus = max(0, 0.04 - index * 0.003)
                    candidates.append(
                        (parsed, min(0.96, 0.82 + keyword_bonus + position_bonus))
                    )
        return max(candidates, key=lambda item: item[1]) if candidates else (None, 0.0)

    def _extract_total(
        self, lines: list[OCRLine], normalized: list[str]
    ) -> tuple[int | None, float]:
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

        if not candidates:
            return None, 0.0
        best = max(candidates, key=lambda item: (item.score, item.amount))
        confidence = min(0.96, 0.48 + best.score / 220)
        return best.amount, confidence

    @staticmethod
    def _extract_vat(
        lines: list[OCRLine], normalized: list[str]
    ) -> tuple[int | None, float]:
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
            return None, 0.0
        best = max(candidates, key=lambda item: (item.score, item.line_index))
        return best.amount, 0.88

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
