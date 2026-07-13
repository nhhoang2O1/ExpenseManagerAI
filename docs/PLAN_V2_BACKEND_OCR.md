# PLAN V2.2 - TACH BACKEND VA OCR HOA DON

## 1. Muc tieu

Nang cap ung dung quan ly chi tieu Android hien tai thanh he thong co backend
tap trung va dich vu OCR rieng:

```text
Android Java
    |
    | REST API + JWT
    v
.NET 8 Web API
    |-- PostgreSQL
    |-- Receipt file storage
    `-- Python FastAPI OCR
            `-- OpenCV + PaddleOCR
```

Android la giao dien chinh cua do an. Backend duoc thiet ke doc lap voi mobile,
vi vay neu mo rong sang Web sau nay chi can xay them Web frontend goi lai cung
REST API.

## 2. Cac quyet dinh da khoa sau review

- Android chi goi .NET Web API, khong goi truc tiep OCR service.
- PostgreSQL la nguon du lieu chinh.
- Room chi la read cache trong MVP, chua ho tro ghi offline hai chieu.
- Anh hoa don luu ngoai PostgreSQL; database chi luu duong dan va metadata.
- MVP dung PaddleOCR pretrained de hoan thanh luong end-to-end truoc.
- Chi fine-tune sau khi da do baseline va xac dinh loi nam o recognition.
- Fine-tune recognition truoc; detection chi fine-tune neu co bang chung bi bo
  sot vung chu.
- Truong OCR bat buoc gom cua hang, ngay va tong tien; VAT la tuy chon.
- OCR chi goi y du lieu. Nguoi dung phai kiem tra truoc khi tao giao dich.
- Mot hoa don chi duoc tao toi da mot giao dich.
- Pham vi danh gia chinh gom nhieu nhom hoa don ban le, trong do Circle K va
  GS25 la hai nhom trong tam.

## 3. Pham vi chuc nang

### 3.1 Chuc nang bat buoc

- Dang ky, dang nhap va xac thuc JWT.
- Quan ly danh muc.
- Them, sua, xoa, tim kiem va loc giao dich.
- Thong ke thu chi theo ngay, thang va danh muc.
- Truc quan hoa du lieu bang bieu do tren Android.
- Chup hoac chon anh hoa don.
- Tien xu ly anh bang OpenCV.
- Nhan dien van ban bang PaddleOCR.
- Trich xuat cua hang, ngay, tong tien va VAT neu co.
- Tu dong dien form giao dich tu ket qua OCR.
- Nguoi dung chinh sua va xac nhan de luu giao dich.
- Xuat bao cao Excel theo thang tu backend.
- Kiem thu, danh gia OCR va trien khai bang Docker Compose.

### 3.2 Chuc nang de mo rong

- Budget tren backend.
- Trich xuat danh sach san pham va phuong thuc thanh toan.
- Ghi offline va dong bo hai chieu.
- Di chuyen du lieu Room cu len backend.
- Object storage, refresh token, monitoring va OCR queue.
- Web frontend su dung lai .NET API.

## 4. Backend va database

### 4.1 Bang du lieu MVP

- `users`
- `categories`
- `transactions`
- `receipts`
- `ocr_results`

### 4.2 Quy tac du lieu

- Tien VND dung `numeric(18,0)`, khong dung `float`.
- Ngay giao dich dung kieu `date`.
- Timestamp he thong luu UTC.
- Thong ke theo timezone `Asia/Ho_Chi_Minh`.
- `transactions.receipt_id` la nullable va co unique constraint.
- Moi ban ghi deu duoc kiem tra quyen so huu theo `user_id`.
- Moi receipt co mot anh trong MVP, khong can bang `receipt_files` rieng.
- `ocr_results` luu raw text, bounding boxes, confidence, extracted fields,
  model version, parser version, warning va thoi gian xu ly.

## 5. API chinh

### 5.1 Authentication

```text
POST /api/auth/register
POST /api/auth/login
```

### 5.2 Categories va transactions

```text
GET    /api/categories
POST   /api/categories
PUT    /api/categories/{id}
DELETE /api/categories/{id}

GET    /api/transactions
POST   /api/transactions
PUT    /api/transactions/{id}
DELETE /api/transactions/{id}
```

API giao dich ho tro loc theo ngay, thang, loai, danh muc, tu khoa va phan
trang.

### 5.3 Receipts

```text
POST   /api/receipts
POST   /api/receipts/{id}/process
GET    /api/receipts/{id}
POST   /api/receipts/{id}/retry
POST   /api/receipts/{id}/confirm
DELETE /api/receipts/{id}
```

Trang thai receipt:

```text
UPLOADED -> PROCESSING -> REVIEW_REQUIRED -> CONFIRMED
                       `-> OCR_FAILED
```

Endpoint `confirm` phai idempotent. Neu Android gui lai request do mat mang,
backend tra ve transaction da ton tai thay vi tao giao dich trung.

### 5.4 Statistics va reports

```text
GET /api/statistics/daily
GET /api/statistics/monthly
GET /api/statistics/by-category
GET /api/reports/monthly.xlsx
```

## 6. OCR baseline

### 6.1 Tien xu ly OpenCV

- Kiem tra anh mo, qua toi, qua sang hoac thieu noi dung.
- Resize theo gioi han canh anh.
- Crop vung hoa don neu xac dinh duoc bien.
- Deskew va sua perspective nhe.
- Denoise, grayscale va tang tuong phan khi can.
- Khong ap dung threshold co dinh cho tat ca anh.
- So sanh anh goc va anh da xu ly de chon dau vao tot hon.

### 6.2 PaddleOCR pretrained

- Su dung model pretrained co ho tro tieng Viet.
- Model duoc load mot lan khi FastAPI khoi dong.
- Ket qua gom text, recognition confidence va bounding box.
- Chua fine-tune trong giai doan MVP.

### 6.3 Field extraction

- Parser Circle K.
- Parser GS25.
- Generic parser cho hoa don cua hang khac.
- Regex cho ngay, so tien va VAT.
- Keyword nhu `TONG`, `THANH TOAN`, `VAT`, `THUE`, `TIEN KHACH TRA`.
- Neu co nhieu so tien, parser xep hang ung vien theo keyword va vi tri.
- Category chi duoc goi y, khong tu dong chot.

### 6.4 Phan loai ket qua

- `SUPPORTED`: parser chuyen biet nhan dien duoc template/cua hang.
- `GENERIC`: hoa don cua hang khac, dung generic parser.
- `UNRECOGNIZED`: khong du dau hieu de ket luan la hoa don.
- `LOW_QUALITY`: anh kho doc hoac confidence qua thap.

Anh hop le khong bi tu choi chi vi heuristic khong nhan ra hoa don. Nguoi dung
van co the xem raw text, chup lai hoac nhap giao dich thu cong.

## 7. Ke hoach dataset da dang cua hang

### 7.1 Muc tieu

Thu thap 300 hoa don vat ly khac nhau. Khong tinh nhieu anh chup cua cung mot
hoa don la cac mau doc lap.

| Nhom hoa don | So luong | Train | Validation | Test |
|---|---:|---:|---:|---:|
| Circle K | 80 | 56 | 8 | 16 |
| GS25 | 80 | 56 | 8 | 16 |
| Sieu thi/thuc pham | 40 | 28 | 4 | 8 |
| Ca phe/do an | 35 | 24 | 4 | 7 |
| Nha thuoc | 35 | 24 | 4 | 7 |
| Ban le doc lap | 30 | 22 | 2 | 6 |
| **Tong** | **300** | **210** | **30** | **60** |

Vi du nguon du lieu:

- Sieu thi/thuc pham: WinMart+, Co.opmart, Co.op Food hoac Bach Hoa Xanh.
- Ca phe/do an: Highlands, Phuc Long, chuoi fast food hoac nha hang.
- Nha thuoc: Pharmacity, Long Chau hoac nha thuoc ban le.
- Ban le doc lap: tap hoa, van phong pham, cua hang gia dung, quan an nho.

Circle K va GS25 van chiem 160/300 anh de giu do chinh xac tren hai nhom trong
tam. 140 anh con lai bo sung font, bo cuc, do dai hoa don va cach hien thi tien
khac nhau de recognition tong quat hon.

### 7.2 Phuong an toi thieu neu khong du 300 anh

Neu nguon luc chi cho phep 200 hoa don, su dung phan bo:

| Nhom hoa don | So luong | Train | Validation | Test |
|---|---:|---:|---:|---:|
| Circle K | 50 | 35 | 5 | 10 |
| GS25 | 50 | 35 | 5 | 10 |
| Sieu thi/thuc pham | 30 | 21 | 3 | 6 |
| Ca phe/do an | 25 | 18 | 2 | 5 |
| Nha thuoc | 25 | 18 | 2 | 5 |
| Ban le doc lap | 20 | 13 | 3 | 4 |
| **Tong** | **200** | **140** | **20** | **40** |

Phuong an 300 anh la muc tieu khuyen nghi; phuong an 200 anh la muc toi thieu.

### 7.3 Nguyen tac thu thap

- Moi `receiptGroupId` tuong ung mot hoa don vat ly.
- Neu chup cung mot hoa don o nhieu goc, tat ca anh phai nam cung mot split.
- Can bang anh thang, nghieng, sang, thieu sang, in mo va nen phuc tap.
- Co hoa don ngan, dai, nhieu dong san pham, khuyen mai va VAT.
- Luu metadata ve cua hang, thiet bi, anh sang, goc chup va chat luong.
- Loai bo hoac che thong tin nhay cam truoc khi chia se dataset.

## 8. Cau truc nhan du lieu

Dataset gom ba lop.

### 8.1 Anh toan trang

Anh goc va metadata dung de danh gia OpenCV, detection va luong OCR end-to-end.

### 8.2 Text-line recognition

Moi bounding box duoc crop thanh mot anh dong chu va co transcription chinh
xac:

```text
rec/train/images/line_00001.jpg	CIRCLE K
rec/train/images/line_00002.jpg	TONG THANH TOAN
rec/train/images/line_00003.jpg	125.000
```

- File label dung UTF-8 va phan cach bang tab.
- Giu dau tieng Viet, dau cham, dau phay, `%`, `/` va `-`.
- Nhan phai giong noi dung tren anh, khong chuan hoa ngay/tien trong nhan OCR.
- Dong khong doc duoc khong duoc doan nhan.
- Muc tieu toi thieu la 5.000 text-line trong tap train.
- Augmentation chi ap dung cho train, khong ap dung validation/test.

Voi 210 hoa don train, trung binh 24 dong hop le moi hoa don se tao duoc hon
5.000 text-line. Neu chua dat, bo sung hoa don hoac du lieu text-line tong hop;
khong sao chep anh lap lai de tang so luong gia.

### 8.3 Field-level ground truth

Moi hoa don co nhan chuan hoa dung de danh gia parser:

```json
{
  "storeName": "Circle K",
  "receiptDate": "2026-07-09",
  "totalAmount": 125000,
  "vatAmount": null
}
```

Nhan field-level khong thay the transcription va khong dung truc tiep de
fine-tune recognition.

## 9. Fine-tune recognition

Fine-tune chi bat dau sau khi:

1. Backend, Android va OCR pretrained chay end-to-end on dinh.
2. Test set da duoc khoa.
3. Da do CER, WER va field accuracy cua pretrained.
4. Da phan loai loi thanh detection, recognition, parser va chat luong anh.
5. Recognition duoc xac dinh la mot diem yeu thuc su.

Quy trinh:

1. Dung detection pretrained tao bounding boxes.
2. Kiem tra va sua box bang cong cu gan nhan.
3. Crop text-line va gan transcription.
4. Giu nguyen dictionary tieng Viet cua pretrained model.
5. Fine-tune tu pretrained weights voi learning rate nho.
6. Dung validation de early stopping va chon checkpoint.
7. Export model thanh `receipt-ocr-v1`.
8. Danh gia tren 60 hoa don test chua tung tham gia train.
9. Chi dua model moi vao OCR service neu tot hon baseline.

Khong fine-tune detection va recognition cung luc trong giai doan dau.

## 10. Tieu chi danh gia OCR

### 10.1 Recognition

- CER.
- WER.
- Exact line accuracy.
- Accuracy rieng cho cac dong ngay, tong tien va VAT.

### 10.2 Field extraction

- `storeName` exact accuracy.
- `receiptDate` exact accuracy.
- `totalAmount` exact accuracy.
- `vatAmount` accuracy tren tap co VAT ro rang.

### 10.3 Muc tieu nghiem thu

- Store exact accuracy >= 90%.
- Total exact accuracy >= 80%.
- Date exact accuracy >= 75%.
- Fine-tuned model phai cai thien so voi pretrained tren test set khoa cung.
- Bao cao ca so mau dung/sai, khong chi bao cao phan tram.
- Neu fine-tuned model khong tot hon, pretrained + parser van la ket qua hop le
  cua thuc nghiem.

## 11. Android

- Them Retrofit/OkHttp va JWT interceptor.
- Luu token bang secure preferences.
- Tao `ApiService`, `AuthRepository`, `RemoteTransactionRepository` va
  `ReceiptRepository`.
- Chup anh bang camera hoac chon anh tu thu vien.
- Upload multipart va hien thi tien do/trang thai OCR.
- Form review gom cua hang, ngay, tong tien, VAT, danh muc va ghi chu.
- Cho phep sua moi truong truoc khi confirm.
- Sau confirm, refresh danh sach va thong ke.
- Khi mat mang, hien thi Room cache va thong bao khong the chinh sua online.

## 12. Kiem thu

### 12.1 Backend

- Register, login, token sai va token het han.
- User khong doc/sua duoc du lieu cua user khac.
- CRUD, tim kiem, loc va phan trang giao dich.
- Thong ke ngay, thang va danh muc.
- Upload sai MIME hoac qua 10 MB bi tu choi.
- Retry confirm khong tao giao dich trung.
- Excel tao duoc va dung du lieu.

### 12.2 OCR

- So sanh anh goc va anh da tien xu ly.
- Pretrained baseline tren test set khoa cung.
- Fine-tuned model tren cung test set.
- Anh ngoai pham vi, anh mo, anh khong phai hoa don.
- Do thoi gian OCR tren may demo.

### 12.3 Android va end-to-end

```text
Register
-> Login
-> Upload receipt
-> OCR
-> Review/Edit
-> Confirm
-> Transaction list
-> Statistics
-> Excel report
```

## 13. Thu tu trien khai

### Giai doan 1 - Nen tang

1. Khoa schema, API contract va quy tac tien/ngay.
2. Xay .NET auth, category va transaction API.
3. Tao PostgreSQL migrations va integration tests.

### Giai doan 2 - OCR baseline

1. Tao Python FastAPI OCR service.
2. Tich hop OpenCV va PaddleOCR pretrained.
3. Xay parser Circle K, GS25 va generic parser.
4. Hoan thanh upload, process, retry va confirm.

### Giai doan 3 - Android

1. Tich hop Retrofit, JWT va remote repositories.
2. Xay luong chup/chon anh.
3. Xay man hinh review va confirm.
4. Hoan thanh end-to-end voi pretrained OCR.

### Giai doan 4 - Dataset va fine-tune

1. Thu thap dataset theo bang phan bo da cua hang.
2. Chia train/validation/test theo hoa don vat ly.
3. Gan nhan full-page, text-line va field-level.
4. Do baseline va phan tich loi.
5. Fine-tune recognition neu du dieu kien.
6. So sanh va chon model.

### Giai doan 5 - Hoan thien

1. Hoan thanh bieu do va thong ke.
2. Xuat Excel.
3. Chay Docker Compose.
4. Kiem thu end-to-end va toi uu.
5. Viet tai lieu trien khai va bao cao thuc nghiem.

## 14. Trien khai

Docker Compose gom:

- `postgres`
- `backend`
- `ocr-service`
- volume `receipt-storage`

Chi backend expose ra ngoai. OCR service chi nhan request trong Docker network.
Android emulator goi backend qua `http://10.0.2.2:<port>` trong debug build.
JWT secret, connection string va storage path duoc truyen qua environment
variables.
