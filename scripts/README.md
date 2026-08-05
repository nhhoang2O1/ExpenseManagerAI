# Database backup and restore

## API smoke test

Khởi động stack kiểm thử cục bộ bằng OCR GPU mặc định. File override E2E chỉ
tắt SMTP trong môi trường Development để backend ghi mã xác nhận vào log;
production không sử dụng file này:

```powershell
docker compose -f docker-compose.yml -f docker-compose.e2e.yml up -d --build
```

Nếu cần kiểm thử bằng CPU:

```powershell
docker compose -f docker-compose.yml -f docker-compose.cpu.yml `
    -f docker-compose.e2e.yml up -d --build
```

Chạy toàn bộ use case API với PP-OCRv6 và ảnh hóa đơn chụp từ điện thoại:

```powershell
.\scripts\e2e_smoke.ps1 `
    -ReadVerificationCodeFromDockerLogs `
    -ReceiptImagePath C:\fixtures\receipt-real.jpg
```

`e2e_smoke.ps1` luôn kiểm tra health, auth, category, transaction, pagination và
statistics. Script còn kiểm tra đăng nhập, idempotency khi nạp mục tiêu, logout
thu hồi refresh token và transaction được tạo từ kết quả OCR. OCR chỉ được chạy
khi truyền một ảnh fixture. `receipt-smoke.png` là ảnh tổng hợp, chỉ phù hợp để
kiểm tra pipeline. Kết quả E2E nghiệp vụ phải dùng ảnh chụp hóa đơn từ điện thoại:

```powershell
.\scripts\e2e_smoke.ps1 -ReceiptImagePath C:\fixtures\receipt-real.jpg
```

Script không tạo ảnh OCR giả và không tự điền store/date/total fallback. Nếu
fixture không tạo được payload review đầy đủ, smoke test thất bại rõ ràng.

## PostgreSQL

`backup-postgres.ps1` runs `pg_dump -Fc` inside the Compose PostgreSQL
container, writes a timestamped dump to `backups/postgres`, and records a
SHA-256 checksum. It retains fourteen days of dumps. The script never removes
the live `postgres-data` volume.

Use `restore-postgres.ps1 -DumpFile backups/postgres/<file>.dump
-DropAndCreate` only against a dedicated test database. Validate metadata and
the SHA-256 of representative `receipt_images.data` values after restore.
