# Hướng dẫn Huấn luyện Mô hình PaddleOCR trên Máy cá nhân (Local)

Bộ công cụ này giúp bạn tự huấn luyện (fine-tune) mô hình nhận diện dòng chữ trên hóa đơn (MC-OCR 2021) trực tiếp trên máy Windows cá nhân của bạn và tích hợp thẳng vào dịch vụ OCR trong dự án `ExpenseManagerAI`.

---

## 📁 Cấu trúc Thư mục

```text
d:\ExpenseManagerAI\ocr-service\train_ocr\
├── prepare_dataset.py       # Giải nén & lọc mẫu ảnh sạch cho train
├── setup_and_train.py       # Tự động clone PaddleOCR, tải weights & huấn luyện
├── export_and_deploy.py     # Xuất mô hình & tích hợp tự động vào .env của OCR Service
└── dataset/                 # Thư mục dữ liệu (tự động tạo)
```

---

## 🚀 Các bước thực hiện

### Bước 1: Mở PowerShell tại thư mục `ocr-service\train_ocr`

```powershell
cd d:\ExpenseManagerAI\ocr-service\train_ocr
```

---

### Bước 2: Cài đặt thư viện (nếu chưa có)

```powershell
pip install paddlepaddle pyyaml opencv-python pillow
```
*(Nếu máy bạn có GPU NVIDIA, có thể dùng `pip install paddlepaddle-gpu` để train nhanh hơn. Nếu chỉ dùng CPU, bản `paddlepaddle` thường train 50 Epochs mất khoảng 15-20 phút).*

---

### Bước 3: Chuẩn bị Dataset

Đặt file `vietnamese-receipts-mc-ocr-2021.zip` vào thư mục `train_ocr` (hoặc thư mục cha `ocr-service`), sau đó chạy:

```powershell
python prepare_dataset.py
```

---

### Bước 4: Khởi tạo và Huấn luyện Mô hình

```powershell
python setup_and_train.py
```

Script sẽ tự động:
1. Clone PaddleOCR về máy local.
2. Tải Pretrained Weights `latin_PP-OCRv3_rec_train`.
3. Khởi tạo cấu hình CTCHead ổn định.
4. Chạy quá trình huấn luyện 50 Epochs.

---

### Bước 5: Xuất Mô hình & Tích hợp vào Dự án

Sau khi huấn luyện xong, chạy lệnh:

```powershell
python export_and_deploy.py
```

Mô hình sau khi export sẽ nằm tại `ocr-service/models/my_receipt_rec_model`, và file `ocr-service/.env` sẽ tự động được cập nhật `OCR_RECOGNITION_MODEL_DIR=./models/my_receipt_rec_model`. Khi khởi chạy `ocr-service`, mô hình fine-tune của bạn sẽ tự động được nạp!
