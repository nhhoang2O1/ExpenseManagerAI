from datetime import date

import pytest

from app.schemas import Classification, OCRLine
from app.services.parsers import ReceiptParser
from tests.helpers import line


def test_generic_parser_extracts_circle_k_fields_and_vat_without_brand_rules() -> None:
    result = ReceiptParser().parse(
        [
            line("CIRCLE K", y=0),
            line("Ngay: 09/07/2026", y=30),
            line("TONG THANH TOAN: 125.000 VND", y=60),
            line("Thue GTGT: 8.000", y=90),
        ]
    )

    assert result.classification == Classification.SUPPORTED
    assert result.fields.store_name == "CIRCLE K"
    assert result.fields.receipt_date == date(2026, 7, 9)
    assert result.fields.total_amount == 125_000
    assert result.fields.vat_amount == 8_000
    assert result.warnings == []


def test_generic_parser_handles_gs25_year_first_date_and_comma_amount() -> None:
    result = ReceiptParser().parse(
        [
            line("GS 25 VIETNAM", y=0),
            line("Date 2026-07-09 18:20", y=30),
            line("TOTAL 89,000 VND", y=60),
        ]
    )

    assert result.classification == Classification.SUPPORTED
    assert result.fields.store_name == "GS 25 VIETNAM"
    assert result.fields.receipt_date == date(2026, 7, 9)
    assert result.fields.total_amount == 89_000
    assert result.fields.vat_amount is None


def test_generic_parser_uses_header_and_receipt_signals() -> None:
    result = ReceiptParser().parse(
        [
            line("Nha Thuoc Long Chau", y=0),
            line("HOA DON BAN HANG", y=30),
            line("09-07-2026", y=60),
            line("Tong cong 245 000 VND", y=90),
        ]
    )

    assert result.classification == Classification.SUPPORTED
    assert result.fields.store_name == "Nha Thuoc Long Chau"
    assert result.fields.total_amount == 245_000


def test_generic_parser_prefers_merchant_next_to_address_over_receipt_code() -> None:
    result = ReceiptParser().parse(
        [
            line("HOA DON THANH TOAN", y=0),
            line("Ma HD: #5V2MW", y=30),
            line("TN: Chi Hanh", y=60),
            line("Ngay: 12/07/2026", y=90),
            line("Tong tien: 25,000 VND", y=120),
            line("TRA SUA PE MINH BA", y=150),
            line("Quan 9, Ho Chi Minh, Viet Nam", y=180),
            line("Hotline: 0768233023", y=210),
        ]
    )

    assert result.fields.store_name == "TRA SUA PE MINH BA"


def test_unrecognized_text_preserves_missing_field_warnings() -> None:
    result = ReceiptParser().parse([line("some unrelated words")])

    assert result.classification == Classification.UNRECOGNIZED
    assert result.fields.store_name is None
    assert result.fields.receipt_date is None
    assert result.fields.total_amount is None
    assert "STORE_NAME_NOT_FOUND" in result.warnings
    assert "RECEIPT_DATE_NOT_FOUND" in result.warnings
    assert "TOTAL_AMOUNT_NOT_FOUND" in result.warnings


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("Date: Tue 18 Jun 2024 09:49:29", date(2024, 6, 18)),
        ("Date: 18 June 2024", date(2024, 6, 18)),
        ("18 Jun 2024", date(2024, 6, 18)),
        ("Ngày: 18/06/2024", date(2024, 6, 18)),
    ],
)
def test_parser_extracts_english_textual_and_existing_vietnamese_dates(
    text: str, expected: date
) -> None:
    result = ReceiptParser().parse([line(text)])

    assert result.fields.receipt_date == expected


@pytest.mark.parametrize(
    "text",
    [
        "18/06/2024",
        "18-06-2024",
        "18.06.2024",
        "2024/06/18",
        "2024-06-18",
        "2024.06.18",
    ],
)
def test_all_existing_numeric_date_formats_are_preserved(text: str) -> None:
    result = ReceiptParser().parse([line(text)])

    assert result.fields.receipt_date == date(2024, 6, 18)


@pytest.mark.parametrize(
    "text",
    [
        "Date: Tue 31 Jun 2024 09:49:29",
        "Date: Tue 18 Foo 2024 09:49:29",
    ],
)
def test_invalid_english_textual_dates_are_rejected_safely(text: str) -> None:
    result = ReceiptParser().parse([line(text)])

    assert result.fields.receipt_date is None


def test_amount_parser_prefers_expected_total_for_english_vat_receipt() -> None:
    result = ReceiptParser().parse(
        [
            line("Subtotal: 11,000 VND", y=0),
            line("Total(+VAT): 11,000 VND", y=30),
            line("Cash: 11,000 VND", y=60),
            line("CHANGE DUE: 0 VND", y=90),
        ]
    )

    assert result.fields.total_amount == 11_000


def test_lets_go_real_ocr_regression() -> None:
    def actual_line(
        text: str, confidence: float, box: list[list[float]]
    ) -> OCRLine:
        return OCRLine(text=text, confidence=confidence, box=box)

    lines = [
        actual_line("Nhà Hàng Let's Go", 0.999970, [[259, 61], [469, 59], [469, 90], [260, 92]]),
        actual_line("Đ/c: 100/15A Đưng Trân Phú -Phưng Lôc Tho", 0.979004, [[221, 94], [508, 91], [508, 112], [221, 115]]),
        actual_line("- Tp. Nha Trang", 0.993028, [[316, 113], [412, 113], [412, 130], [316, 130]]),
        actual_line("Tel: 02583 524495 - Hot: 01699999346", 0.996466, [[245, 144], [480, 141], [480, 158], [245, 161]]),
        actual_line("PHIÊU TAM TÍNH", 0.985946, [[308, 159], [421, 159], [421, 176], [308, 176]]),
        actual_line("10:11 07/05/2018", 0.999818, [[405, 194], [516, 193], [516, 211], [405, 212]]),
        actual_line("Ső HD:HD.4008", 0.937956, [[215, 196], [315, 196], [315, 210], [215, 210]]),
        actual_line("Khu vưc:Tâng Làu", 0.939718, [[214, 210], [323, 210], [323, 227], [214, 227]]),
        actual_line("Bàn:B28", 0.990933, [[464, 210], [518, 210], [518, 227], [464, 227]]),
        actual_line("Già vào: 19:04", 0.974227, [[214, 225], [305, 225], [305, 242], [214, 242]]),
        actual_line("06/05/2018", 0.999996, [[214, 240], [288, 240], [288, 257], [214, 257]]),
        actual_line("Thu ngân: thunganlanh", 0.999280, [[213, 255], [353, 256], [353, 274], [213, 273]]),
        actual_line("Món", 0.999593, [[244, 275], [278, 275], [278, 294], [244, 294]]),
        actual_line("T.Tiên", 0.968191, [[458, 276], [504, 276], [504, 294], [458, 294]]),
        actual_line("D.Giá", 0.950520, [[389, 277], [431, 277], [431, 295], [389, 295]]),
        actual_line("DVT", 0.999851, [[311, 278], [341, 278], [341, 294], [311, 294]]),
        actual_line("SL", 0.999948, [[349, 278], [371, 278], [371, 295], [349, 295]]),
        actual_line("9.010.000", 0.995590, [[468, 897], [538, 896], [538, 914], [468, 915]]),
        actual_line("Tőng công", 0.928110, [[206, 899], [279, 899], [279, 917], [206, 917]]),
        actual_line("115,8", 0.999909, [[345, 899], [387, 899], [387, 917], [345, 917]]),
        actual_line("9.010.000", 0.996979, [[459, 913], [537, 912], [537, 930], [459, 931]]),
        actual_line("Thành tiên", 0.934892, [[207, 915], [287, 915], [287, 929], [207, 929]]),
        actual_line("(Bâng chü:Chin triêu muài ngàn đöng)", 0.918077, [[206, 925], [459, 927], [459, 944], [206, 942]]),
        actual_line("Xin Cám On Quý Khách!", 0.908337, [[293, 938], [449, 940], [449, 953], [292, 951]]),
    ]

    result = ReceiptParser().parse(lines)

    assert result.fields.store_name == "Nhà Hàng Let's Go"
    assert result.fields.receipt_date == date(2018, 5, 7)
    assert result.fields.total_amount == 9_010_000


def test_transaction_date_beats_earlier_neighboring_check_in_date() -> None:
    result = ReceiptParser().parse(
        [
            line("Giờ vào: 19:04", y=0),
            line("06/05/2018", y=30),
            line("10:11 07/05/2018", y=60),
        ]
    )

    assert result.fields.receipt_date == date(2018, 5, 7)


def test_highlands_real_ocr_regression() -> None:
    def actual_line(
        text: str, confidence: float, box: list[list[float]]
    ) -> OCRLine:
        return OCRLine(text=text, confidence=confidence, box=box)

    result = ReceiptParser().parse(
        [
            actual_line("HIGHLANDS COFFEE", .999810, [[130, 34], [221, 31], [222, 56], [130, 58]]),
            actual_line("327 Nguyen Van Tang St., Long Thanh My", .969701, [[74, 67], [278, 63], [279, 82], [75, 87]]),
            actual_line("ward, Thu Duc city HCMC", .999001, [[109, 81], [234, 77], [235, 94], [109, 97]]),
            actual_line("SDT:028.7100.0327", .997297, [[125, 93], [219, 92], [219, 107], [125, 108]]),
            actual_line("ShopID: 352", .998013, [[139, 106], [205, 106], [205, 123], [139, 123]]),
            actual_line("Hoa Don Thanh Toan", .992423, [[124, 122], [225, 122], [225, 146], [124, 146]]),
            actual_line("Pager:14", .985641, [[233, 155], [292, 155], [292, 185], [233, 185]]),
            actual_line("In Store", .999897, [[65, 158], [114, 158], [114, 185], [65, 185]]),
            actual_line("Check: 126431", .974844, [[67, 193], [138, 193], [138, 210], [67, 210]]),
            actual_line("POS01", .941349, [[255, 206], [290, 206], [290, 225], [255, 225]]),
            actual_line("Ngay : 24-10-2025 09:41", .966800, [[68, 209], [191, 208], [192, 225], [68, 226]]),
            actual_line("Thu ngan: Thu Ngan Sang", .988767, [[71, 226], [198, 226], [198, 240], [71, 240]]),
            actual_line("55.000", .997455, [[249, 250], [289, 250], [289, 268], [249, 268]]),
            actual_line("Phindi Kem Sua L", .946571, [[102, 253], [185, 253], [185, 267], [102, 267]]),
            actual_line("tien:", .995465, [[100, 279], [130, 282], [129, 298], [98, 295]]),
            actual_line("55.000", .935331, [[250, 281], [287, 281], [287, 296], [250, 296]]),
            actual_line("55.000", .957854, [[249, 296], [290, 296], [290, 321], [249, 321]]),
            actual_line("Tong tien:", .995268, [[69, 297], [124, 297], [124, 318], [69, 318]]),
            actual_line("HoMo QR", .866599, [[71, 314], [113, 314], [113, 329], [71, 329]]),
            actual_line("55.000", .997251, [[250, 317], [288, 317], [288, 332], [250, 332]]),
        ]
    )

    assert result.fields.store_name == "HIGHLANDS COFFEE"
    assert result.fields.receipt_date == date(2025, 10, 24)
    assert result.fields.total_amount == 55_000
