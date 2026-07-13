# ML dataset scaffold

Thu muc nay chua contract dataset da cua hang trong
`docs/PLAN_V2_BACKEND_OCR.md`. Scaffold khong tai dataset, khong chua anh hoa
don that va khong phu thuoc PaddleOCR.

## Don vi du lieu

- `receiptGroupId` la mot hoa don vat ly.
- Mot group co the co nhieu anh/goc chup.
- Moi group chi duoc xuat hien trong mot split.
- Quota split tinh theo group, khong tinh theo so file anh.
- Muc tieu chinh la 300 group: train 210, validation 30, test 60.

Test set phai duoc khoa truoc khi do baseline. Khong augmentation validation
hoac test.

## Cau truc

```text
ml/
|-- annotations/
|   |-- fields/
|   |-- full-page/
|   `-- recognition/
|-- config/split_300.json
|-- data/full-page/{train,validation,test}/
|-- examples/
|-- schemas/metadata.schema.json
|-- scripts/dataset_tool.py
`-- splits/
```

Anh that dat trong `ml/data/` va bi git ignore. Model/checkpoint cung khong
duoc commit. File example va config van duoc track.

## Metadata

Moi dong JSONL la mot anh. Xem:

- `schemas/metadata.schema.json`: JSON Schema cho mot dong.
- `examples/metadata.example.jsonl`: vi du nho, co hai anh cung group.
- `config/split_300.json`: quota chinh xac theo sau nhom.

Duong dan anh la relative path tinh tu `ml/`, vi du
`data/full-page/train/receipt_0001_a.jpg`. Truoc khi chia se dataset, phai xoa
hoac che thong tin nhay cam va dat `sensitiveDataRedacted=true`.

## Validate va tao split

Script chi dung Python standard library (Python 3.9+):

```powershell
python ml/scripts/dataset_tool.py validate --metadata ml/examples/metadata.example.jsonl --config ml/config/split_300.json
python ml/scripts/dataset_tool.py make-sample --config ml/config/split_300.json --output ml/examples/metadata_300.example.jsonl
python ml/scripts/dataset_tool.py split --metadata ml/examples/metadata_300.example.jsonl --config ml/config/split_300.json --output ml/splits/split.example.jsonl
python ml/scripts/dataset_tool.py validate --metadata ml/splits/split.example.jsonl --config ml/config/split_300.json --require-target-counts --require-split
```

`split` dung seed trong config, sap xep group truoc khi shuffle va gan tat ca
anh cua cung group vao cung split. Lenh se fail neu metadata khong co dung quota
group theo tung nhom.

## Nhan du lieu

Doc `annotations/README.md` truoc khi gan nhan. Ba lop nhan doc lap:

1. Full-page: anh goc, metadata, bounding boxes/transcription.
2. Recognition: crop dong chu va TSV UTF-8 phan cach bang tab.
3. Field-level: JSON chuan hoa store/date/total/VAT de danh gia parser.

Field-level JSON khong thay the transcription va khong dung truc tiep de
fine-tune recognition.
