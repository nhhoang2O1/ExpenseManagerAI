# API backend

> Trạng thái: authoritative ở mức route và hành vi tích hợp. Swagger/OpenAPI
> của backend là nguồn chi tiết cho request/response schema.

Base path là `/api`. Trừ register/login/refresh/forgot/reset, API yêu cầu
`Authorization: Bearer <access-token>`. Resource theo user luôn kiểm tra owner.

## Auth và account

| Method | Route | Chức năng |
|---|---|---|
| POST | `/auth/register`, `/auth/login` | Tạo user/đăng nhập, trả token pair |
| POST | `/auth/refresh` | Rotate refresh token |
| POST | `/auth/logout`, `/auth/logout-all` | Revoke một/toàn bộ session |
| POST | `/auth/forgot-password`, `/auth/reset-password` | Reset; forgot luôn trả kết quả trung lập |
| GET/PUT | `/account/profile` | Xem/sửa profile |
| POST | `/account/change-password` | Đổi password và revoke session |
| POST | `/account/email-change/request`, `/account/email-change/confirm` | Đổi email bằng mã 6 số |
| DELETE | `/account` | Xóa account và revoke session |

Access token: 15 phút. Refresh token: 30 ngày. Reset/email code: 6 số, 10 phút,
tối đa 5 lần thử.

## Tài chính và planning

| Resource | Route chính | Ghi chú |
|---|---|---|
| Transaction | `GET/POST /transactions`, `PUT/DELETE /transactions/{id}` | GET phân trang; create idempotent |
| Category | `GET/POST /categories`, `PUT/DELETE /categories/{id}` | Không đổi loại/xóa khi đang dùng; conflict 409 |
| Budget | `GET/POST /budgets`, `PUT/DELETE /budgets/{id}` | Mutation hỗ trợ version/ETag |
| Goal | CRUD `/goals`; `POST /goals/{id}/funds`; `GET /goals/{id}/history` | Add funds có row lock và history |
| Reminder | CRUD `/reminders` | Create idempotent |
| Statistics | `/statistics/daily`, `/monthly`, `/by-category` | Tổng hợp server-side |
| Report | `/reports/range.xlsx`, `/reports/range.pdf` | Query `from=yyyy-MM-dd&to=yyyy-MM-dd` |

Giai đoạn tương thích vẫn chấp nhận thiếu `If-Match`; client mới nên gửi
version nhận gần nhất. Upload receipt, create transaction, add funds và create
reminder nên gửi UUID ổn định trong `Idempotency-Key` khi retry.

## Receipt

| Method | Route | Kết quả |
|---|---|---|
| POST | `/receipts` | Upload multipart, lưu byte PostgreSQL, trả 201 |
| POST | `/receipts/{id}/process` | Queue job, trả 202 |
| POST | `/receipts/{id}/retry` | Queue lại job lỗi, trả 202 |
| GET | `/receipts` | Phân trang/lọc receipt |
| GET | `/receipts/{id}` | Trạng thái/kết quả OCR để polling |
| GET | `/receipts/{id}/image` | Stream ảnh sau kiểm tra owner |
| POST | `/receipts/{id}/confirm` | Tạo transaction sau review/nhập tay hợp lệ |
| DELETE | `/receipts/{id}` | Xóa rõ ràng; 409 nếu đã liên kết transaction |

Trạng thái chính: `UPLOADED → QUEUED → PROCESSING → REVIEW_REQUIRED →
CONFIRMED`; lỗi có retry/backoff và cuối cùng thành `OCR_FAILED`.

## Status code tích hợp

- `202`: đã queue, client phải polling.
- `401`: refresh một lần rồi đưa về Login nếu vẫn lỗi.
- `404`: không tồn tại/không thuộc user theo policy endpoint.
- `409`: concurrency, idempotency hoặc lifecycle conflict.
