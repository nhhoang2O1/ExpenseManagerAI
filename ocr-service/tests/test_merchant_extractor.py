from app.schemas import OCRLine
import pytest

from app.services.merchant_extractor import MerchantNameExtractor


def ocr_line(
    text: str,
    confidence: float,
    box: list[list[float]],
) -> OCRLine:
    return OCRLine(text=text, confidence=confidence, box=box)


def simple_line(
    text: str,
    y: float,
    confidence: float = 0.95,
    x: float = 100,
    width: float = 300,
    height: float = 24,
) -> OCRLine:
    return ocr_line(
        text,
        confidence,
        [[x, y], [x + width, y], [x + width, y + height], [x, y + height]],
    )


def candidate(result, text: str):
    return next(item for item in result.candidates if item.text == text)


def test_real_lai_tea_footer_block_beats_noisy_header() -> None:
    lines = [
        ocr_line("1o-02.2194012", 0.874228835105896, [[124, 0], [285, 0], [285, 20], [124, 20]]),
        ocr_line("HETQR", 0.945949912071228, [[55, 155], [160, 155], [160, 199], [55, 199]]),
        ocr_line("50+", 0.9998104572296143, [[432, 160], [472, 160], [472, 184], [432, 184]]),
        ocr_line("MB", 0.9754956364631653, [[250, 163], [294, 163], [294, 186], [250, 186]]),
        ocr_line("LÀI TEA - NGUYĚN VÁN TÄNG", 0.931526780128479, [[203, 455], [580, 449], [580, 476], [204, 482]]),
        ocr_line("Đja chi: 118 Nguyên Vān Tăng, Long Thanh My,", 0.9420568943023682, [[148, 481], [638, 472], [638, 499], [149, 508]]),
        simple_line("Thú Dúc", 510, 0.8678736686706543, x=344, width=93),
        simple_line("HÓA DON THANH TOÁN", 547, 0.9875714182853699, x=205, width=369, height=37),
        simple_line("Ngay:27/07/2026", 696, 0.999496340751648, x=386, width=205, height=28),
        simple_line("STT", 777, 0.9992313385009766, x=107, width=57, height=31),
        simple_line("Tên món", 780, 0.9943330883979797, x=216, width=105, height=29),
        simple_line("Tóng tiên:", 1048, 0.934746265411377, x=91, width=144, height=39),
        simple_line("18,000 g", 1050, 0.99, x=580, width=90, height=30),
        simple_line("Powered by iPOS.vn", 1210, 0.96, x=300, width=260, height=35),
    ]

    result = MerchantNameExtractor().analyze(lines)

    assert result.merchant_name == "LÀI TEA - NGUYĚN VÁN TÄNG"
    lai_tea = candidate(result, "LÀI TEA - NGUYĚN VÁN TÄNG")
    noisy = candidate(result, "HETQR")
    assert lai_tea.score > noisy.score
    assert lai_tea.features["address_below"] > 0
    assert "top_position" in noisy.features


def test_real_bach_hoa_xanh_title_is_stripped_without_selecting_table_header() -> None:
    lines = [
        ocr_line("PHIÊU THANH TOÁN BÁCH HÓA XANH", 0.9775711297988892, [[161, 437], [661, 451], [660, 485], [160, 471]]),
        ocr_line("s cr: OV203386607387212", 0.8989298343658447, [[159, 476], [498, 481], [498, 508], [159, 503]]),
        ocr_line("24/07/2026 09:12 - NV:138163", 0.9468161463737488, [[159, 500], [439, 505], [439, 526], [158, 521]]),
        ocr_line("SL", 0.9999282360076904, [[209, 539], [249, 539], [249, 568], [209, 568]]),
        simple_line("Giá bán(VAT) Thành tiên", 540, 0.97, x=300, width=480, height=30),
        simple_line("mi hào hào tôm chua cay 75g", 580, 0.96, x=150, width=450),
        simple_line("quà tǎng: phiêu mua hàng 20.000d", 1100, 0.93, x=140, width=550),
        ocr_line("52.400", 0.9999521374702454, [[689, 1510], [791, 1510], [791, 1542], [689, 1542]]),
        ocr_line("Tông tiên:", 0.9005641937255859, [[130, 1520], [274, 1518], [275, 1552], [130, 1554]]),
        ocr_line("52.400", 0.9988129734992981, [[688, 1545], [790, 1543], [790, 1572], [689, 1574]]),
    ]

    result = MerchantNameExtractor().analyze(lines)

    assert result.merchant_name == "BÁCH HÓA XANH"
    merchant = candidate(result, "BÁCH HÓA XANH")
    table_header = candidate(result, "Giá bán(VAT) Thành tiên")
    assert merchant.score > table_header.score
    assert table_header.features["table_header_penalty"] < 0
    assert table_header.features["product_table_penalty"] < 0


def test_real_pe_min_footer_merchant_beats_product_name() -> None:
    lines = [
        ocr_line("HOÁ DON THANH TOÁN", 0.9952734708786011, [[364, 471], [701, 519], [695, 564], [357, 516]]),
        ocr_line("S6 HD: 130100", 0.9576998353004456, [[445, 517], [621, 549], [616, 580], [439, 548]]),
        ocr_line("Má HD: #5V2MW", 0.9531370401382446, [[271, 561], [459, 573], [458, 607], [269, 595]]),
        ocr_line("TN: Chi Hanh", 0.9492340087890625, [[527, 589], [654, 610], [649, 639], [523, 618]]),
        ocr_line("Ngày: 12/07/2026", 0.969430685043335, [[523, 619], [696, 641], [692, 673], [519, 651]]),
        simple_line("STT", 654, 0.8959698677062988, x=265, width=55, height=33),
        simple_line("Tên món", 664, 0.9698634147644043, x=379, width=97, height=31),
        simple_line("SL", 687, 0.9965493679046631, x=519, width=37, height=30),
        simple_line("Đan giá", 688, 0.8774258494377136, x=553, width=81, height=32),
        ocr_line("TRA SÜA OLONG", 0.9449485540390015, [[335, 694], [511, 717], [507, 748], [331, 725]]),
        simple_line("- Dá Chung", 731, 0.9876028299331665, x=325, width=118, height=33),
        simple_line("· Size M", 769, 0.911023736000061, x=321, width=89, height=32),
        simple_line("- Thach Cú Năng", 805, 0.9220860004425049, x=317, width=170, height=34),
        simple_line("Tóng só mán: 1", 847, 0.9548758268356323, x=243, width=166, height=35),
        ocr_line("25,000 d", 0.9014649391174316, [[648, 913], [741, 924], [737, 961], [643, 950]]),
        ocr_line("25,000 d", 0.9351255893707275, [[628, 955], [737, 968], [733, 1004], [624, 991]]),
        ocr_line("TRÀ SÜA PÉ MIN Ba", 0.9156919717788696, [[385, 1036], [584, 1039], [584, 1074], [385, 1071]]),
        simple_line("Da Lng Tang P,Tang Nh PA", 1060, 0.6297978758811951, x=245, width=449, height=51),
        simple_line("Qun 9, H Chi Minh, Vit Nam", 1095, 0.976978063583374, x=320, width=303, height=36),
        ocr_line("Hotline: 0768233023", 0.9759507179260254, [[367, 1122], [572, 1116], [573, 1147], [368, 1153]]),
        simple_line("Cám on Quy Khách Hęn Gp Lai", 1218, 0.8793945908546448, x=304, width=323, height=45),
        simple_line("Powered by iPOS.vn", 1247, 0.9645330905914307, x=364, width=200, height=36),
    ]

    result = MerchantNameExtractor().analyze(lines)

    assert result.merchant_name == "TRÀ SÜA PÉ MIN Ba"
    merchant = candidate(result, "TRÀ SÜA PÉ MIN Ba")
    product = candidate(result, "TRA SÜA OLONG")
    assert merchant.score > product.score
    assert merchant.features["hotline_below"] > 0
    assert product.features["product_table_penalty"] < 0


def test_unknown_merchant_at_receipt_header_is_supported_without_dictionary() -> None:
    result = MerchantNameExtractor().analyze(
        [
            simple_line("TIEM BANH BA NAM", 0),
            simple_line("Ngay 10/08/2026", 35),
            simple_line("TONG CONG 45.000 VND", 70),
        ]
    )

    assert result.merchant_name == "TIEM BANH BA NAM"
    assert "dictionary_bonus" not in result.accepted.features


def test_document_title_without_merchant_is_not_a_candidate() -> None:
    result = MerchantNameExtractor().analyze(
        [
            simple_line("HÓA ĐƠN THANH TOÁN", 0),
            simple_line("Ngày 10/08/2026", 35),
            simple_line("Tổng tiền 45.000 VND", 70),
        ]
    )

    assert result.merchant_name is None


def test_low_confidence_noise_is_rejected_as_ambiguous() -> None:
    result = MerchantNameExtractor().analyze(
        [
            simple_line("ABCD XYZ", 0, confidence=0.12),
            simple_line("Ngay 10/08/2026", 35),
            simple_line("Tong cong 45.000 VND", 70),
        ]
    )

    assert result.merchant_name is None


def test_missing_boxes_use_line_order_for_address_context() -> None:
    result = MerchantNameExtractor().analyze(
        [
            OCRLine(text="QUAN AN GIA DINH", confidence=0.91, box=[]),
            OCRLine(text="Đja chi: 12 Duong So 1", confidence=0.88, box=[]),
            OCRLine(text="Ngay 10/08/2026", confidence=0.95, box=[]),
            OCRLine(text="Tong cong 120.000 VND", confidence=0.98, box=[]),
        ]
    )

    assert result.merchant_name == "QUAN AN GIA DINH"


def test_multiline_merchant_is_grouped_conservatively() -> None:
    result = MerchantNameExtractor().analyze(
        [
            simple_line("THE COFFEE", 0, x=180, width=260),
            simple_line("HOUSE", 26, x=240, width=140),
            simple_line("Địa chỉ: 10 Nguyễn Huệ", 60, x=100, width=500),
            simple_line("Ngày 10/08/2026", 100),
            simple_line("Tổng cộng 80.000 VND", 140),
        ]
    )

    assert result.merchant_name == "THE COFFEE HOUSE"
    assert result.accepted.source_line_indexes == (0, 1)


def test_single_unrelated_line_has_insufficient_receipt_evidence() -> None:
    result = MerchantNameExtractor().analyze([simple_line("some unrelated words", 0)])

    assert result.merchant_name is None


def test_real_entrance_ticket_normalizes_ocr_damaged_venue_name() -> None:
    lines = [
        ocr_line(
            "DINH DOC LP",
            0.9651193618774414,
            [[176, 244], [369, 251], [368, 278], [175, 271]],
        ),
        ocr_line(
            "VÉ VÀO CỔNG / ENTRANCE TICKET",
            0.9919315576553345,
            [[123, 275], [419, 286], [418, 312], [122, 301]],
        ),
        simple_line("Mã vé / Ticket code: 503222", 313, width=203),
        simple_line("Giờ xuất vé / Time: 09/08/2026 07:05:40", 336, width=288),
        simple_line("Loại vé / Ticket type", 368, width=154),
        simple_line("Tổng tiền / Total amount: 1,620,000đ", 470, width=260),
    ]

    result = MerchantNameExtractor().analyze(lines)

    assert result.merchant_name == "DINH ĐỘC LẬP"
    assert result.accepted is not None
    assert result.accepted.text == "DINH DOC LP"
    assert result.accepted.features["receipt_title_nearby"] > 0
    assert result.accepted.features["document_title_below"] > 0
    assert result.accepted.features["transaction_metadata_below"] > 0


def test_unknown_venue_uses_document_context_without_dictionary_or_grouping() -> None:
    result = MerchantNameExtractor().analyze(
        [
            simple_line("CITY HISTORY MUSEUM", 0),
            simple_line("ADMISSION TICKET", 28),
            simple_line("Ticket code: 123456", 56),
            simple_line("Date: 09/08/2026 07:05", 84),
            simple_line("Total amount: 120.000 VND", 112),
        ]
    )

    assert result.merchant_name == "CITY HISTORY MUSEUM"
    assert result.accepted is not None
    assert result.accepted.source_line_indexes == (0,)
    assert result.accepted.features["document_title_below"] > 0
    assert result.accepted.features["transaction_metadata_below"] > 0
    assert "dictionary_bonus" not in result.accepted.features
    assert not any(
        set(candidate.source_line_indexes) & {1, 2}
        and len(candidate.source_line_indexes) > 1
        for candidate in result.candidates
    )


@pytest.mark.parametrize(
    "address",
    [
        "Đ/c: 10 Trần Phú",
        "ĐC: 10 Trần Phú",
        "D/C: 10 Trần Phú",
        "DC: 10 Trần Phú",
        "Địa chỉ: 10 Trần Phú",
        "Address: 10 Tran Phu",
    ],
)
def test_generic_address_aliases_support_merchant_context(address: str) -> None:
    result = MerchantNameExtractor().analyze(
        [
            simple_line("NHA HANG THU NGHIEM", 0),
            simple_line(address, 28),
            simple_line("Tel: 0258 1234567", 56),
            simple_line("PHIẾU TẠM TÍNH", 84),
            simple_line("Ngày: 10/08/2026", 112),
            simple_line("Tổng cộng: 100.000 VND", 140),
        ]
    )

    assert result.merchant_name == "NHA HANG THU NGHIEM"


def test_real_highlands_address_is_context_not_merchant_content() -> None:
    lines = [
        ocr_line("HIGHLANDS COFFEE", .999810, [[130, 34], [221, 31], [222, 56], [130, 58]]),
        ocr_line("327 Nguyen Van Tang St., Long Thanh My", .969701, [[74, 67], [278, 63], [279, 82], [75, 87]]),
        ocr_line("ward, Thu Duc city HCMC", .999001, [[109, 81], [234, 77], [235, 94], [109, 97]]),
        ocr_line("SDT:028.7100.0327", .997297, [[125, 93], [219, 92], [219, 107], [125, 108]]),
        ocr_line("ShopID: 352", .998013, [[139, 106], [205, 106], [205, 123], [139, 123]]),
        ocr_line("Hoa Don Thanh Toan", .992423, [[124, 122], [225, 122], [225, 146], [124, 146]]),
        simple_line("Ngay : 24-10-2025 09:41", 209, x=68, width=124),
        simple_line("Tong tien:", 297, x=69, width=55),
        simple_line("55.000", 297, x=249, width=41),
    ]

    result = MerchantNameExtractor().analyze(lines)

    assert result.merchant_name == "HIGHLANDS COFFEE"
    assert result.accepted is not None
    assert result.accepted.source_line_indexes == (0,)
    assert result.accepted.features["address_below"] > 0
    assert result.accepted.features["hotline_below"] > 0
    assert not any(
        candidate.source_line_indexes == (0, 1)
        for candidate in result.candidates
    )
