# Annotation contract

Dung UTF-8 cho tat ca file. ID va duong dan phai khop metadata, khong dua thong
tin ca nhan chua che vao repository.

## Full-page

`full-page/README.md` mo ta bounding box va transcription tren anh toan trang.
Anh goc nam trong `ml/data/full-page/`, khong nam trong thu muc annotation.

## Recognition TSV

`recognition/README.md` mo ta crop dong chu va `labels.tsv`. Nhan la noi dung
nhin thay tren crop, giu dau tieng Viet va ky tu punctuation.

## Field-level JSON

`fields/README.md` mo ta ground truth chuan hoa cho parser. Dung
`fields/field.example.json` lam mau, khong sua file mau thanh nhan that.
