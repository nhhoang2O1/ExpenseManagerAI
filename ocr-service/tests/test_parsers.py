from datetime import date

from app.schemas import Classification
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
    assert result.fields.store_name == "some unrelated words"
    assert result.fields.receipt_date is None
    assert result.fields.total_amount is None
    assert "RECEIPT_DATE_NOT_FOUND" in result.warnings
    assert "TOTAL_AMOUNT_NOT_FOUND" in result.warnings
