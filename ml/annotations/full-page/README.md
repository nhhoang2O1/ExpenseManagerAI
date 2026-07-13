# Full-page annotations

Moi receipt image co mot JSON annotation cung `imageId`. Bounding box dung bon
diem theo chieu kim dong ho, bat dau gan goc tren-trai:

```json
{
  "imageId": "ck_001_a",
  "width": 3024,
  "height": 4032,
  "lines": [
    {
      "lineId": "ck_001_a_l001",
      "polygon": [[110, 120], [820, 118], [824, 190], [112, 194]],
      "transcription": "CIRCLE K",
      "legible": true
    }
  ]
}
```

Quy tac:

- Toa do pixel nam trong kich thuoc anh.
- Polygon khong tu cat va bao sat ca dong chu.
- Transcription phai trung noi dung in, khong chuan hoa ngay/tien.
- Dong khong doc duoc dat `legible=false` va transcription rong; khong doan.
- Moi `lineId` la duy nhat va duoc dung de dat ten recognition crop.
- Split cua annotation phai khop split metadata theo `receiptGroupId`.
