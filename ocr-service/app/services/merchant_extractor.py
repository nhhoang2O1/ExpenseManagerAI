import re
import unicodedata
from dataclasses import dataclass
from statistics import median
from typing import Iterable

from app.schemas import OCRLine


MIN_ACCEPTED_SCORE = 37.5
MIN_SCORE_MARGIN = 4.0
STRONG_CONTEXT_SCORE = 18.0
MAX_GROUP_SIZE = 3

WEIGHTS = {
    "base": 10.0,
    "alphabetic_ratio": 8.0,
    "useful_length": 4.0,
    "digit_ratio": -9.0,
    "ocr_confidence": 8.0,
    "relative_width": 4.0,
    "center_alignment": 3.0,
    "relative_height": 4.0,
    "top_position": 2.0,
    "receipt_evidence": 3.0,
    "missing_receipt_evidence": -7.0,
    "address_below": 14.0,
    "hotline_below": 14.0,
    "address_nearby": 5.0,
    "tax_code_nearby": 4.0,
    "receipt_title_nearby": 3.0,
    "document_title_below": 14.0,
    "transaction_metadata_below": 7.0,
    "footer_merchant_block": 5.0,
    "dictionary_bonus": 5.0,
    "multiline_bonus": 2.0,
    "metadata_penalty": -8.0,
    "address_penalty": -16.0,
    "address_continuation_penalty": -14.0,
    "amount_penalty": -12.0,
    "table_header_penalty": -18.0,
    "receipt_title_penalty": -12.0,
    "footer_penalty": -16.0,
    "product_table_penalty": -22.0,
    "product_modifier_penalty": -10.0,
}

DOCUMENT_PREFIXES = (
    ("PHIEU", "THANH", "TOAN"),
    ("PHIEU", "TINH", "TIEN"),
    ("HOA", "DON", "THANH", "TOAN"),
    ("HOA", "DON", "BAN", "HANG"),
    ("HOA", "DON"),
    ("PHIEU", "TAM", "TINH"),
    ("RECEIPT",),
)

KNOWN_MERCHANT_ALIASES = {
    "CIRCLE K": ("CIRCLE K", "CIRCIL K", "CIRCLEK"),
    "GS25": ("GS25", "GS 25", "GS-25"),
    "DINH ĐỘC LẬP": ("DINH DOC LAP", "DINH DOC LP"),
}

DATE_PATTERN = re.compile(
    r"(?<!\d)(?:\d{1,2}[./-]\d{1,2}[./-]\d{2,4}|"
    r"\d{4}[./-]\d{1,2}[./-]\d{1,2})(?!\d)"
)
TIME_PATTERN = re.compile(r"(?<!\d)\d{1,2}:\d{2}(?::\d{2})?(?!\d)")
AMOUNT_ONLY_PATTERN = re.compile(
    r"^[^\w]*(?:\d{1,3}(?:[.,\s]\d{3})+|\d{3,})\s*(?:VND|D|Đ)?[^\w]*$",
    re.IGNORECASE,
)
PHONE_PATTERN = re.compile(r"(?:\+?84|0)[\s.()-]*\d(?:[\s.()-]*\d){8,10}")
INVOICE_CODE_PATTERN = re.compile(
    r"^(?:MA|SO)\s*(?:HD|HOA DON)|^SO\s*CT|^RECEIPT\s*(?:NO|NUMBER)",
)

METADATA_SIGNALS = (
    "NGAY", "DATE", "GIO VAO", "GIO RA", "THU NGAN", "NHAN VIEN",
    "MA HD", "SO HD", "MA HOA DON", "BAN MANG VE", "SO CT",
    "THANH TOAN", "CHUYEN KHOAN", "TIEN MAT",
    "SHOPID", "SHOP ID", "PAGER", "CHECK", "POS ", "POS0", "IN STORE",
    "PAYMENT", "CASH", "CARD",
    "TICKET CODE", "TICKET TYPE", "TICKET NO", "TICKET NUMBER",
    "MA VE", "GIO XUAT VE", "PRICE", "TOTAL AMOUNT",
)
TABLE_HEADER_SIGNALS = (
    "STT", "TEN MON", "TEN HANG", "SAN PHAM", "SL", "SO LUONG",
    "DON GIA", "GIA BAN", "THANH TIEN", "VAT",
)
TOTAL_SIGNALS = (
    "TONG TIEN", "TONG CONG", "TONG THANH TOAN", "TONG SO MON",
    "PHAI TRA", "TOTAL",
)
FOOTER_SIGNALS = (
    "POWERED BY", "CAM ON", "THANK YOU", "HEN GAP LAI", "QUY KHACH",
)
PRODUCT_MODIFIER_SIGNALS = (
    "SIZE", "TOPPING", "DA CHUNG", "IT DA", "THACH", "TRAN CHAU",
)
LOCALITY_SIGNALS = (
    "THU DUC", "GO VAP", "TAN BINH", "BINH THANH", "PHU NHUAN",
    "TAN PHU", "BINH TAN", "NHA BE", "HOC MON", "CU CHI",
)


@dataclass(frozen=True)
class _Box:
    min_x: float
    max_x: float
    min_y: float
    max_y: float

    @property
    def width(self) -> float:
        return max(0.0, self.max_x - self.min_x)

    @property
    def height(self) -> float:
        return max(0.0, self.max_y - self.min_y)

    @property
    def center_x(self) -> float:
        return (self.min_x + self.max_x) / 2

    @property
    def center_y(self) -> float:
        return (self.min_y + self.max_y) / 2


@dataclass(frozen=True)
class MerchantCandidate:
    text: str
    score: float
    features: dict[str, float]
    source_line_indexes: tuple[int, ...]


@dataclass(frozen=True)
class MerchantExtraction:
    merchant_name: str | None
    candidates: tuple[MerchantCandidate, ...]
    accepted: MerchantCandidate | None
    runner_up: MerchantCandidate | None


@dataclass(frozen=True)
class _DocumentLayout:
    bounds: _Box | None
    median_height: float
    table_ranges: tuple[tuple[int, int], ...]
    has_receipt_evidence: bool


@dataclass(frozen=True)
class _CandidateSeed:
    text: str
    source_line_indexes: tuple[int, ...]


class MerchantNameNormalizer:
    """Conservative alias normalization applied only after extraction."""

    def normalize(self, value: str) -> str:
        normalized = _normalize(value)
        for canonical, aliases in KNOWN_MERCHANT_ALIASES.items():
            if any(normalized == _normalize(alias) for alias in aliases):
                return canonical
        return value.strip()


class MerchantNameExtractor:
    """Explainable merchant extraction over line text, confidence and layout."""

    def __init__(self, normalizer: MerchantNameNormalizer | None = None) -> None:
        self.normalizer = normalizer or MerchantNameNormalizer()

    def extract(self, lines: list[OCRLine]) -> str | None:
        return self.analyze(lines).merchant_name

    def analyze(self, lines: list[OCRLine]) -> MerchantExtraction:
        if not lines:
            return MerchantExtraction(None, (), None, None)

        normalized = [_normalize(line.text) for line in lines]
        boxes = [_box(line) for line in lines]
        layout = _document_layout(lines, normalized, boxes)
        seeds = self._candidate_seeds(lines, normalized, boxes, layout)
        candidates = tuple(
            sorted(
                (self._score(seed, lines, normalized, boxes, layout) for seed in seeds),
                key=lambda candidate: (-candidate.score, candidate.source_line_indexes),
            )
        )
        if not candidates:
            return MerchantExtraction(None, (), None, None)

        top = candidates[0]
        runner_up = next(
            (candidate for candidate in candidates[1:] if not _same_candidate_family(top, candidate)),
            None,
        )
        margin = top.score - (runner_up.score if runner_up is not None else 0.0)
        context_strength = sum(
            top.features.get(name, 0.0)
            for name in (
                "address_below", "hotline_below", "address_nearby",
                "tax_code_nearby", "document_title_below",
                "transaction_metadata_below", "footer_merchant_block",
            )
        )
        strong_context = context_strength >= STRONG_CONTEXT_SCORE
        accepted = (
            top.score >= MIN_ACCEPTED_SCORE
            and (runner_up is None or margin >= MIN_SCORE_MARGIN or strong_context)
        )
        return MerchantExtraction(
            self.normalizer.normalize(top.text) if accepted else None,
            candidates,
            top if accepted else None,
            runner_up,
        )

    def _candidate_seeds(
        self,
        lines: list[OCRLine],
        normalized: list[str],
        boxes: list[_Box | None],
        layout: _DocumentLayout,
    ) -> list[_CandidateSeed]:
        seeds: list[_CandidateSeed] = []
        eligible: dict[int, str] = {}
        for index, line in enumerate(lines):
            candidate_text = _strip_document_prefix(line.text)
            if candidate_text is None or _hard_reject(candidate_text):
                continue
            eligible[index] = candidate_text
            seeds.append(_CandidateSeed(candidate_text, (index,)))

        for start in sorted(eligible):
            if not _groupable_text(normalized[start]):
                continue
            group = [start]
            for index in range(start + 1, min(len(lines), start + MAX_GROUP_SIZE)):
                if index not in eligible or index != group[-1] + 1:
                    break
                if _inside_table(index, layout.table_ranges):
                    break
                if (
                    _is_address_like(normalized[index])
                    or _is_hotline(normalized[index])
                    or _is_tax_code(normalized[index])
                    or _looks_like_address_fragment(normalized[index])
                    or not _groupable_text(normalized[index])
                ):
                    break
                if not _lines_form_block(group[-1], index, boxes, layout):
                    break
                group.append(index)
                text = " ".join(eligible[item] for item in group)
                if len(text) <= 100:
                    seeds.append(_CandidateSeed(text, tuple(group)))

        unique: dict[tuple[str, tuple[int, ...]], _CandidateSeed] = {}
        for seed in seeds:
            unique[(_normalize(seed.text), seed.source_line_indexes)] = seed
        return list(unique.values())

    def _score(
        self,
        seed: _CandidateSeed,
        lines: list[OCRLine],
        normalized: list[str],
        boxes: list[_Box | None],
        layout: _DocumentLayout,
    ) -> MerchantCandidate:
        text = seed.text.strip()
        normalized_text = _normalize(text)
        features: dict[str, float] = {"base": WEIGHTS["base"]}
        characters = [character for character in text if not character.isspace()]
        length = max(len(characters), 1)
        alphabetic_ratio = sum(character.isalpha() for character in characters) / length
        digit_ratio = sum(character.isdigit() for character in characters) / length
        features["alphabetic_ratio"] = WEIGHTS["alphabetic_ratio"] * alphabetic_ratio
        features["useful_length"] = (
            WEIGHTS["useful_length"] if 4 <= len(text) <= 60 else -2.0
        )
        features["digit_ratio"] = WEIGHTS["digit_ratio"] * digit_ratio

        confidence = sum(lines[index].confidence for index in seed.source_line_indexes) / len(
            seed.source_line_indexes
        )
        features["ocr_confidence"] = WEIGHTS["ocr_confidence"] * (confidence - 0.5)

        candidate_box = _union_box(boxes[index] for index in seed.source_line_indexes)
        if candidate_box is not None and layout.bounds is not None:
            document = layout.bounds
            document_width = max(document.width, 1.0)
            document_height = max(document.height, 1.0)
            relative_width = candidate_box.width / document_width
            center_distance = abs(candidate_box.center_x - document.center_x) / (document_width / 2)
            height_ratio = candidate_box.height / max(layout.median_height, 1.0)
            normalized_y = (candidate_box.center_y - document.min_y) / document_height
            features["relative_width"] = WEIGHTS["relative_width"] * min(relative_width / 0.45, 1.0)
            features["center_alignment"] = WEIGHTS["center_alignment"] * max(0.0, 1.0 - center_distance)
            features["relative_height"] = WEIGHTS["relative_height"] * max(0.0, min(height_ratio - 1.0, 1.0))
            if normalized_y <= 0.25:
                features["top_position"] = WEIGHTS["top_position"]

        features["receipt_evidence"] = (
            WEIGHTS["receipt_evidence"]
            if layout.has_receipt_evidence
            else WEIGHTS["missing_receipt_evidence"]
        )

        context = _context_features(seed, normalized, boxes, layout)
        context_only_candidate = (
            _is_address_like(normalized_text)
            or _looks_like_address_fragment(normalized_text)
            or _is_hotline(normalized_text)
            or _is_tax_code(normalized_text)
            or _is_receipt_title(normalized_text)
            or _is_transaction_metadata(normalized_text)
        )
        for name, active in context.items():
            if active and (not context_only_candidate or name.endswith("_penalty")):
                features[name] = WEIGHTS[name]

        if _dictionary_match(normalized_text):
            features["dictionary_bonus"] = WEIGHTS["dictionary_bonus"]
        if len(seed.source_line_indexes) > 1:
            features["multiline_bonus"] = WEIGHTS["multiline_bonus"]
        if any(signal in normalized_text for signal in METADATA_SIGNALS):
            features["metadata_penalty"] = WEIGHTS["metadata_penalty"]
        if (
            _is_address_like(normalized_text)
            or _is_hotline(normalized_text)
            or _looks_like_address_fragment(normalized_text)
        ):
            features["address_penalty"] = WEIGHTS["address_penalty"]
        if _contains_amount(normalized_text):
            features["amount_penalty"] = WEIGHTS["amount_penalty"]
        if _is_table_header(normalized_text):
            features["table_header_penalty"] = WEIGHTS["table_header_penalty"]
        if _is_receipt_title(normalized_text):
            features["receipt_title_penalty"] = WEIGHTS["receipt_title_penalty"]
        if any(signal in normalized_text for signal in FOOTER_SIGNALS):
            features["footer_penalty"] = WEIGHTS["footer_penalty"]
        if any(_inside_table(index, layout.table_ranges) for index in seed.source_line_indexes):
            features["product_table_penalty"] = WEIGHTS["product_table_penalty"]
        if any(signal in normalized_text for signal in PRODUCT_MODIFIER_SIGNALS):
            features["product_modifier_penalty"] = WEIGHTS["product_modifier_penalty"]

        return MerchantCandidate(
            text=text,
            score=round(sum(features.values()), 4),
            features=features,
            source_line_indexes=seed.source_line_indexes,
        )


def _document_layout(
    lines: list[OCRLine], normalized: list[str], boxes: list[_Box | None]
) -> _DocumentLayout:
    valid_boxes = [box for box in boxes if box is not None]
    bounds = _union_box(valid_boxes)
    median_height = median(box.height for box in valid_boxes) if valid_boxes else 20.0
    table_ranges = _table_ranges(normalized)
    has_receipt_evidence = any(
        DATE_PATTERN.search(line.text)
        or any(signal in normalized[index] for signal in TOTAL_SIGNALS)
        or _is_receipt_title(normalized[index])
        or _is_address_like(normalized[index])
        or _is_hotline(normalized[index])
        for index, line in enumerate(lines)
    )
    return _DocumentLayout(bounds, max(median_height, 1.0), table_ranges, has_receipt_evidence)


def _context_features(
    seed: _CandidateSeed,
    normalized: list[str],
    boxes: list[_Box | None],
    layout: _DocumentLayout,
) -> dict[str, bool]:
    first = seed.source_line_indexes[0]
    last = seed.source_line_indexes[-1]
    candidate_box = _union_box(boxes[index] for index in seed.source_line_indexes)

    address_below = False
    hotline_below = False
    address_nearby = False
    tax_nearby = False
    title_nearby = False
    title_below = False
    metadata_below = False
    address_continuation = False
    for index, text in enumerate(normalized):
        if index in seed.source_line_indexes:
            continue
        below, nearby = _relative_context(
            last, index, candidate_box, boxes[index], layout.median_height
        )
        if _is_address_like(text):
            address_below = address_below or below
            address_nearby = address_nearby or nearby
            if index < first and nearby:
                address_continuation = True
        if _is_hotline(text):
            hotline_below = hotline_below or below
            address_nearby = address_nearby or nearby
        if _is_tax_code(text) and nearby:
            tax_nearby = True
        if _is_receipt_title(text) and nearby:
            title_nearby = True
            title_below = title_below or below
        if _is_transaction_metadata(text) and below:
            metadata_below = True

    footer_block = False
    if candidate_box is not None and layout.bounds is not None:
        normalized_y = (candidate_box.center_y - layout.bounds.min_y) / max(layout.bounds.height, 1.0)
        footer_block = normalized_y >= 0.55 and (address_below or hotline_below)
    elif first >= len(normalized) // 2:
        footer_block = address_below or hotline_below

    return {
        "address_below": address_below,
        "hotline_below": hotline_below,
        "address_nearby": address_nearby and not address_below and not hotline_below,
        "tax_code_nearby": tax_nearby,
        "receipt_title_nearby": title_nearby,
        "document_title_below": title_below,
        "transaction_metadata_below": metadata_below and title_below,
        "footer_merchant_block": footer_block,
        "address_continuation_penalty": address_continuation,
    }


def _relative_context(
    candidate_index: int,
    context_index: int,
    candidate_box: _Box | None,
    context_box: _Box | None,
    median_height: float,
) -> tuple[bool, bool]:
    if candidate_box is not None and context_box is not None:
        delta = context_box.center_y - candidate_box.center_y
        below = 0 < delta <= median_height * 3.5
        nearby = abs(delta) <= median_height * 5
        return below, nearby
    delta_index = context_index - candidate_index
    return 0 < delta_index <= 3, abs(delta_index) <= 4


def _table_ranges(normalized: list[str]) -> tuple[tuple[int, int], ...]:
    header_indexes = [
        index for index, text in enumerate(normalized) if _is_table_header(text)
    ]
    starts: list[int] = []
    for index in header_indexes:
        nearby_headers = sum(
            1 for other in header_indexes if index <= other <= index + 4
        )
        if nearby_headers >= 2 and (not starts or index > starts[-1] + 4):
            starts.append(index)

    ranges: list[tuple[int, int]] = []
    for start in starts:
        end = len(normalized)
        for index in range(start + 2, len(normalized)):
            if _is_total_line(normalized[index]):
                end = index
                break
        ranges.append((start, end))
    return tuple(ranges)


def _lines_form_block(
    previous: int,
    current: int,
    boxes: list[_Box | None],
    layout: _DocumentLayout,
) -> bool:
    first_box = boxes[previous]
    second_box = boxes[current]
    if first_box is None or second_box is None:
        return current == previous + 1
    vertical_gap = second_box.min_y - first_box.max_y
    if vertical_gap > layout.median_height * 0.9 or vertical_gap < -layout.median_height * 0.5:
        return False
    overlap = max(0.0, min(first_box.max_x, second_box.max_x) - max(first_box.min_x, second_box.min_x))
    overlap_ratio = overlap / max(min(first_box.width, second_box.width), 1.0)
    if layout.bounds is None:
        return overlap_ratio >= 0.4
    center_delta = abs(first_box.center_x - second_box.center_x) / max(layout.bounds.width, 1.0)
    return overlap_ratio >= 0.4 or center_delta <= 0.12


def _strip_document_prefix(value: str) -> str | None:
    raw_tokens = value.strip().split()
    normalized_tokens = [_normalize(token) for token in raw_tokens]
    for prefix in DOCUMENT_PREFIXES:
        if len(normalized_tokens) < len(prefix):
            continue
        if all(_token_matches(actual, expected) for actual, expected in zip(normalized_tokens, prefix)):
            remainder = " ".join(raw_tokens[len(prefix) :]).strip(" :-–—")
            return remainder or None
    return value.strip()


def _hard_reject(value: str) -> bool:
    clean = value.strip()
    normalized = _normalize(clean)
    compact = [character for character in clean if not character.isspace()]
    letters = sum(character.isalpha() for character in compact)
    if len(clean) < 3 or letters < 3 or letters / max(len(compact), 1) < 0.32:
        return True
    if AMOUNT_ONLY_PATTERN.fullmatch(clean):
        return True
    if PHONE_PATTERN.fullmatch(clean):
        return True
    if INVOICE_CODE_PATTERN.search(normalized):
        return True
    if _looks_like_identifier(normalized):
        return True
    if _is_date_time_only(clean, normalized):
        return True
    if not any(character.isalnum() for character in clean):
        return True
    return False


def _is_date_time_only(raw: str, normalized: str) -> bool:
    has_date = DATE_PATTERN.search(raw) is not None
    has_time = TIME_PATTERN.search(raw) is not None
    if not (has_date or has_time):
        return False
    residual = DATE_PATTERN.sub(" ", raw)
    residual = TIME_PATTERN.sub(" ", residual)
    residual = _normalize(residual)
    allowed = ("NGAY", "DATE", "GIO", "TIME", "NV")
    return not residual or all(
        token in allowed or token.isdigit() for token in residual.split()
    )


def _is_address_like(value: str) -> bool:
    tokens = value.split()
    if _fuzzy_phrase(tokens, ("DIA", "CHI")):
        return True
    if value.startswith(("D C ", "DC ", "ADDRESS ")):
        return True
    street_markers = {
        "ST", "STREET", "RD", "ROAD", "AVE", "AVENUE", "BLVD",
        "BOULEVARD", "LANE", "LN", "DUONG",
    }
    if re.match(r"^(?:\d{2,}|\d+[A-Z]?[/\-]\d+)\s+", value) and any(
        token in street_markers for token in tokens[1:]
    ):
        return True
    signals = (
        "VIET NAM", "HO CHI MINH", "TP HCM", "PHUONG ", "DUONG ",
        "THI TRAN", "THANH PHO", "WARD ", "DISTRICT ",
    )
    if any(signal in f"{value} " for signal in signals):
        return True
    if value.startswith("TP "):
        return True
    if any(signal in value for signal in LOCALITY_SIGNALS):
        return True
    return any(_token_matches(token, "QUAN") for token in tokens[:3]) and any(
        character.isdigit() for character in value
    )


def _looks_like_address_fragment(value: str) -> bool:
    tokens = value.split()
    return bool(
        tokens
        and tokens[0] in ("DIA", "DJA", "DA")
        and any(token in ("P", "Q", "TP", "PHUONG", "QUAN") for token in tokens[1:])
    )


def _looks_like_identifier(value: str) -> bool:
    tokens = value.split()
    if len(tokens) != 1 or len(value) < 12:
        return False
    letters = sum(character.isalpha() for character in value)
    digits = sum(character.isdigit() for character in value)
    return letters >= 3 and digits >= 3 and digits / len(value) >= 0.2


def _groupable_text(value: str) -> bool:
    return not (
        any(signal in value for signal in METADATA_SIGNALS)
        or any(signal in value for signal in FOOTER_SIGNALS)
        or _is_hotline(value)
        or _is_tax_code(value)
        or _is_transaction_metadata(value)
        or _contains_amount(value)
        or _is_receipt_title(value)
        or _is_table_header(value)
        or _looks_like_identifier(value)
    )


def _is_hotline(value: str) -> bool:
    tokens = value.split()
    return (
        any(_token_matches(token, "HOTLINE") for token in tokens[:3])
        or any(token in ("PHONE", "TEL", "SDT") for token in tokens[:3])
        or "DIEN THOAI" in value
    )


def _is_tax_code(value: str) -> bool:
    return "MA SO THUE" in value or value.startswith("MST") or "TAX CODE" in value


def _is_transaction_metadata(value: str) -> bool:
    prefixes = (
        "TICKET CODE", "TICKET TYPE", "TICKET NO", "TICKET NUMBER",
        "MA VE", "GIO XUAT VE", "DATE ", "NGAY ", "TRANSACTION DATE",
        "PAYMENT DATE", "RECEIPT DATE", "PRINT DATE", "TOTAL AMOUNT",
        "TONG TIEN", "CASHIER", "THU NGAN", "SHOPID", "SHOP ID",
        "CHECK ", "POS ", "POS0",
    )
    if value.startswith(prefixes):
        return True
    return "XUAT VE" in value and "TIME" in value


def _is_receipt_title(value: str) -> bool:
    tokens = value.split()
    invoice_title = (
        len(tokens) >= 2
        and _token_matches(tokens[0], "HOA")
        and _token_matches(tokens[1], "DON")
    )
    ticket_title = (
        "TICKET" in tokens
        and not any(token in tokens for token in ("CODE", "TYPE", "NO", "NUMBER"))
        and not any(character.isdigit() for character in value)
    )
    return (
        invoice_title
        or value.startswith("PHIEU THANH TOAN")
        or value.startswith("PHIEU TINH TIEN")
        or value.startswith("PHIEU TAM TINH")
        or value == "RECEIPT"
        or ticket_title
    )


def _is_table_header(value: str) -> bool:
    return any(
        value == signal or signal in value
        for signal in TABLE_HEADER_SIGNALS
    )


def _is_total_line(value: str) -> bool:
    return any(signal in value for signal in TOTAL_SIGNALS) or value.startswith("TONG SO ")


def _contains_amount(value: str) -> bool:
    return re.search(r"(?<!\d)\d{1,3}(?:[.,\s]\d{3})+(?!\d)", value) is not None


def _dictionary_match(value: str) -> bool:
    return any(
        value == _normalize(alias) or value.startswith(f"{_normalize(alias)} ")
        for aliases in KNOWN_MERCHANT_ALIASES.values()
        for alias in aliases
    )


def _inside_table(index: int, ranges: Iterable[tuple[int, int]]) -> bool:
    return any(start <= index < end for start, end in ranges)


def _same_candidate_family(first: MerchantCandidate, second: MerchantCandidate) -> bool:
    if set(first.source_line_indexes) & set(second.source_line_indexes):
        return True
    first_text = _normalize(first.text)
    second_text = _normalize(second.text)
    return first_text in second_text or second_text in first_text


def _box(line: OCRLine) -> _Box | None:
    points = [point for point in line.box if len(point) >= 2]
    if len(points) < 2:
        return None
    xs = [float(point[0]) for point in points]
    ys = [float(point[1]) for point in points]
    box = _Box(min(xs), max(xs), min(ys), max(ys))
    return box if box.width > 0 and box.height > 0 else None


def _union_box(values: Iterable[_Box | None]) -> _Box | None:
    boxes = [value for value in values if value is not None]
    if not boxes:
        return None
    return _Box(
        min(box.min_x for box in boxes),
        max(box.max_x for box in boxes),
        min(box.min_y for box in boxes),
        max(box.max_y for box in boxes),
    )


def _token_matches(actual: str, expected: str) -> bool:
    if actual == expected:
        return True
    if abs(len(actual) - len(expected)) > 1 or min(len(actual), len(expected)) < 3:
        return False
    return _edit_distance(actual, expected) <= 1


def _fuzzy_phrase(tokens: list[str], phrase: tuple[str, ...]) -> bool:
    if len(tokens) < len(phrase):
        return False
    return any(
        all(_token_matches(tokens[start + offset], expected) for offset, expected in enumerate(phrase))
        for start in range(len(tokens) - len(phrase) + 1)
    )


def _edit_distance(first: str, second: str) -> int:
    previous = list(range(len(second) + 1))
    for row, left in enumerate(first, start=1):
        current = [row]
        for column, right in enumerate(second, start=1):
            current.append(
                min(
                    current[-1] + 1,
                    previous[column] + 1,
                    previous[column - 1] + (left != right),
                )
            )
        previous = current
    return previous[-1]


def _normalize(value: str) -> str:
    decomposed = unicodedata.normalize("NFD", value.upper())
    without_marks = "".join(
        character
        for character in decomposed
        if unicodedata.category(character) != "Mn"
    ).replace("Đ", "D")
    punctuation_as_space = re.sub(r"[^A-Z0-9]+", " ", without_marks)
    return re.sub(r"\s+", " ", punctuation_as_space).strip()
