# Expense Manager AI

Ứng dụng quản lý tài chính cá nhân gồm Android client, ASP.NET Core API,
PostgreSQL và dịch vụ OCR hóa đơn bằng PaddleOCR.

## Chạy nhanh bằng Docker

Yêu cầu: Git, Docker Desktop có Docker Compose V2, NVIDIA GPU, driver NVIDIA và
khả năng cấp GPU cho Linux container. Cấu hình mặc định chạy OCR bằng GPU.

```powershell
Copy-Item .env.example .env
```

Mở `.env` và thay mọi giá trị `change-me`/`your-account`:

- `POSTGRES_PASSWORD`: mật khẩu database local.
- `JWT_SECRET`: chuỗi ngẫu nhiên tối thiểu 32 byte.
- `SMTP_USERNAME` và `SMTP_FROM_ADDRESS`: tài khoản Gmail gửi mail.
- `SMTP_PASSWORD`: Gmail App Password 16 ký tự.

Sau đó chạy:

```powershell
docker compose up --build -d
docker compose ps
.\scripts\e2e_smoke.ps1
```

Backend có tại `http://localhost:8080`; Swagger có tại
`http://localhost:8080/swagger`. Lần khởi động OCR đầu tiên có thể lâu hơn vì
PaddleOCR cần tải model.

Nếu cần chạy bản CPU, phải chỉ định file override riêng:

```powershell
docker compose -f docker-compose.yml -f docker-compose.cpu.yml up --build -d
```

Health OCR trả `device=gpu` hoặc `device=cpu` để xác nhận runtime đang dùng.

Dừng stack mà không xóa dữ liệu:

```powershell
docker compose down
```

## Android

Mở thư mục gốc bằng Android Studio. `local.properties` là file riêng của từng
máy và không được đưa lên Git; Android Studio sẽ tạo file này theo Android SDK
đã cài. Debug build trên emulator gọi backend qua `http://10.0.2.2:8080/`.

Kiểm tra bằng dòng lệnh:

```powershell
.\gradlew.bat :app:testDebugUnitTest
.\gradlew.bat :app:assembleDebug
.\gradlew.bat :app:lintDebug
```

## Kiểm tra backend và OCR

```powershell
dotnet test backend\ExpenseManager.sln
cd ocr-service
python -m pytest
```

Có thể chạy test OCR trong container nếu máy chưa cài Python.

## Tài liệu

- [Chạy local](docs/RUN_LOCAL.md)
- [Tổng quan hệ thống](docs/AUTHORITATIVE_OVERVIEW.md)
- [Kiến trúc Android](docs/ANDROID_ARCHITECTURE.md)
- [API backend](docs/API_REFERENCE.md)

Không commit `.env`, App Password, JWT secret, database backup hoặc dữ liệu hóa
đơn thật.
