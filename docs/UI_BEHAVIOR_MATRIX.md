# Ma trận hành vi UI Android

PostgreSQL qua .NET API là nguồn dữ liệu chính. Room không được dùng làm nguồn hiển thị hoặc ghi dự phòng trong phiên đăng nhập backend.

| Luồng | Loading | Content/Empty | Lỗi kết nối | Submit/Retry | Rotation/Back |
|---|---|---|---|---|---|
| Đăng nhập/đăng ký | Khóa nút và hiện progress | Chuyển Main khi có JWT | Giữ form, lỗi inline | Người dùng chủ động thử lại | Không tạo local session |
| Tổng quan | Progress khi cold start | KPI và 5 giao dịch gần nhất hoặc empty state | Giữ dữ liệu API đang có và hiện banner; cold start hiện Retry | GET có thể retry thủ công | ViewModel giữ dữ liệu |
| Giao dịch | Progress khi cold start | Danh sách lọc hoặc empty state | Không thay bằng Room | Không tự retry POST/PUT/DELETE | Form giữ draft và lựa chọn |
| OCR | UPLOADING/PROCESSING/CONFIRMING | REVIEW cho phép sửa kết quả | Giữ receipt/draft và hành động retry | Confirm khóa double tap; backend idempotent theo receiptId | URI, draft và category được phục hồi |
| Ngân sách/mục tiêu | Progress khi cold start | RecyclerView hoặc empty state | Hiện Retry; không ghi offline | Mutation chỉ chạy một lần từ thao tác người dùng | Planning giữ tab; Budget giữ tháng |
| Thống kê | Chờ category và monthly summary | KPI, chart, history hoặc empty state từng vùng | Cold start hiện Retry | Chỉ retry GET thủ công | Tháng chọn nằm trong ViewModel |
| Nhắc nhở | Danh sách từ API | List hoặc empty state | Không lưu Room | Alarm local chỉ lập sau thao tác API | System notification permission chỉ hỏi khi bật/tạo |

## Quy tắc chung

- Nút ghi dữ liệu bị khóa trong lúc request đang chạy.
- Không queue thao tác ghi offline và không tự động retry request tạo dữ liệu.
- Lỗi form hiển thị tại trường liên quan; lỗi mạng dùng Snackbar hoặc error state.
- Bottom navigation chỉ hiển thị tại năm destination gốc; màn chi tiết Goal History ẩn thanh điều hướng.
- Các trạng thái cảnh báo luôn có text hoặc phần trăm, không dùng màu làm tín hiệu duy nhất.
