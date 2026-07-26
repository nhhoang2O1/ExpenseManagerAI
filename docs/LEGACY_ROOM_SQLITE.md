# Room/SQLite — tài liệu legacy

`3_room_db_shared_prefs.md` và các mô tả Room/DAO/SQLite trong báo cáo cũ phản
ánh prototype trước khi chuyển sang backend. Chúng được giữ nguyên để bảo toàn
lịch sử, không phải hướng dẫn triển khai phiên bản hiện tại.

Kiến trúc hiện tại:

- Android gọi backend bằng Retrofit qua Repository/ViewModel.
- PostgreSQL là nguồn dữ liệu nghiệp vụ duy nhất.
- Android chỉ giữ token mã hóa, receipt draft, alarm metadata theo user và tùy
  chọn UI; không có Room database cho transaction/category/budget/goal.

Tham chiếu [Kiến trúc Android](ANDROID_ARCHITECTURE.md) và
[PostgreSQL/storage](POSTGRESQL_STORAGE_AND_OPERATIONS.md).

