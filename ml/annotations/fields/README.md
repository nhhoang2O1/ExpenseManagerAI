# Field-level ground truth

Moi receipt group co mot JSON chuan hoa de danh gia parser. Neu nhieu anh cung
mot hoa don, chung dung mot `receiptGroupId` va mot ground truth:

```json
{
  "receiptGroupId": "ck_001",
  "storeName": "Circle K",
  "receiptDate": "2026-07-09",
  "totalAmount": 125000,
  "vatAmount": null
}
```

Quy tac:

- `receiptDate` dung ISO `YYYY-MM-DD`.
- `totalAmount` va `vatAmount` la integer VND, khong dung float.
- `vatAmount` la `null` neu receipt khong cho biet VAT ro rang.
- Ten store dung ten chuan da thong nhat cho bao cao.
- File nay danh gia field extraction, khong dung thay transcription.
