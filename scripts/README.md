# Database backup and restore

## API smoke test

`e2e_smoke.ps1` luôn kiểm tra health, auth, category, transaction, pagination và
statistics. OCR chỉ được chạy khi truyền một ảnh fixture thật:

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
