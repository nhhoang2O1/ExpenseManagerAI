# BÁO CÁO THIẾT KẾ TEST CASE VÀ MA TRẬN TRUY VẾT THEO USE CASE

## 1. Mục đích

Báo cáo trình bày cách xây dựng test case cho hệ thống **Expense Manager AI** từ các
use case nghiệp vụ. Mỗi test case phải truy vết được về một use case, một luồng của
use case và một yêu cầu cần kiểm chứng.

Bộ kiểm thử được chia thành ba cấp độ:

- **Unit Test:** kiểm tra độc lập hàm validation, hàm tính toán, mapper, parser và
  quy tắc nghiệp vụ.
- **Integration Test:** kiểm tra sự phối hợp giữa controller, service, database,
  Android framework hoặc OCR service.
- **End-to-End Test (E2E):** kiểm tra luồng nghiệp vụ hoàn chỉnh từ thao tác người
  dùng đến kết quả cuối cùng của hệ thống.

Ma trận thiết kế trong báo cáo gồm **91 test case**: 30 Unit Test, 39 Integration
Test và 22 E2E Test. Đây là số lượng test case được thiết kế. Số lượng bài test tự
động thực tế và trạng thái chạy test phải được báo cáo riêng, không cộng trực tiếp
với số lượng test case thiết kế.

## 2. Phạm vi use case

| Mã | Tên use case | Tác nhân chính | Kết quả nghiệp vụ cần kiểm chứng |
|---|---|---|---|
| UC01 | Đăng ký và xác nhận tài khoản | Người dùng | Tài khoản được tạo, xác minh và có danh mục mặc định |
| UC02 | Đăng nhập và quản lý phiên | Người dùng | Phiên đăng nhập hợp lệ, token được cấp/thu hồi an toàn |
| UC03 | Quản lý giao dịch thu chi | Người dùng | Giao dịch được tạo, sửa, xóa và hiển thị chính xác |
| UC04 | Quản lý danh mục | Người dùng | Danh mục hợp lệ và thuộc đúng người dùng |
| UC05 | Quản lý ngân sách | Người dùng | Ngân sách tháng được thiết lập và cập nhật chính xác |
| UC06 | Quản lý mục tiêu tiết kiệm | Người dùng | Số tiền và lịch sử mục tiêu được cập nhật đúng |
| UC07 | Quản lý nhắc nhở | Người dùng, Android OS | Reminder được lưu và alarm được lập đúng lịch |
| UC08 | Xem thống kê và xuất báo cáo | Người dùng | Số liệu và tệp báo cáo chính xác |
| UC09 | Quét và xác nhận hóa đơn | Người dùng, OCR service | Ảnh được nhận dạng và tạo đúng một giao dịch |
| UC10 | Quản lý tài khoản | Người dùng | Hồ sơ và thông tin bảo mật được cập nhật an toàn |

## 3. Phương pháp xây dựng test case từ use case

### 3.1. Chuỗi truy vết

Mỗi test case được tạo theo chuỗi sau:

```text
Use case
  -> Tiền điều kiện và quy tắc nghiệp vụ
  -> Luồng chính / luồng thay thế / luồng lỗi
  -> Điều kiện cần kiểm tra
  -> Dữ liệu kiểm thử
  -> Cấp độ Unit / Integration / E2E
  -> Kết quả mong đợi
```

Không bắt đầu thiết kế bằng câu hỏi “cần bao nhiêu Unit Test”. Trước hết phải phân
tích đủ các luồng của use case, sau đó mới chọn cấp độ kiểm thử phù hợp cho từng
điều kiện.

### 3.2. Quy tắc chuyển từ luồng use case thành test case

| Thành phần trong use case | Cách tạo test case | Cấp độ thường dùng |
|---|---|---|
| Tiền điều kiện | Kiểm tra khi tiền điều kiện đúng và khi bị vi phạm | Integration/E2E |
| Luồng chính | Tạo ít nhất một happy-path từ đầu đến cuối | E2E |
| Luồng thay thế | Mỗi nhánh có kết quả khác nhau tạo ít nhất một case | Unit/Integration |
| Luồng lỗi | Mỗi nguyên nhân lỗi và mã lỗi quan trọng tạo một case | Unit/Integration |
| Quy tắc dữ liệu | Phân lớp tương đương: hợp lệ, không hợp lệ | Unit |
| Giá trị biên | Kiểm tra 0, âm, giới hạn dưới, giới hạn trên | Unit/Integration |
| Thay đổi trạng thái | Kiểm tra trạng thái trước và sau thao tác | Integration/E2E |
| Phân quyền | Kiểm tra đúng user và truy cập chéo user | Integration/E2E |
| Gửi lại/đồng thời | Kiểm tra idempotency và optimistic concurrency | Integration |

### 3.3. Quy ước mã test case

```text
<Cấp độ>-<Use case>-<Số thứ tự>
```

Ví dụ:

- `UT-UC05-03`: Unit Test số 03 của use case quản lý ngân sách.
- `IT-UC03-04`: Integration Test số 04 của use case quản lý giao dịch.
- `E2E-UC09-02`: E2E Test số 02 của use case quét hóa đơn.

Mã use case nằm ngay trong mã test giúp chứng minh nguồn gốc của test case và hỗ
trợ cập nhật test khi yêu cầu thay đổi.

## 4. Minh họa cách sinh test case từ một use case

### 4.1. Use case UC03 - Quản lý giao dịch thu chi

**Tiền điều kiện:** người dùng đã đăng nhập; danh mục tồn tại và thuộc người dùng.

**Luồng chính:**

1. Người dùng mở màn hình thêm giao dịch.
2. Người dùng chọn loại thu hoặc chi.
3. Người dùng nhập số tiền, ngày, ghi chú và chọn danh mục.
4. Người dùng nhấn Lưu.
5. Hệ thống lưu và hiển thị giao dịch trong danh sách.

**Luồng thay thế:** danh sách có nhiều trang; request được gửi lại; hai thiết bị
cùng sửa một giao dịch.

**Luồng lỗi:** số tiền không dương; danh mục sai loại; danh mục hoặc giao dịch
thuộc người dùng khác.

### 4.2. Bảng dẫn xuất test case của UC03

| Nguồn trong UC03 | Điều kiện kiểm thử | Test case dẫn xuất | Cấp độ |
|---|---|---|---|
| Quy tắc số tiền | Amount hợp lệ, bằng 0 và âm | `UT-UC03-01..03` | Unit |
| Luồng chính bước 4-5 | Controller, service và DB lưu đúng | `IT-UC03-01` | Integration |
| Luồng lỗi | Danh mục sai loại hoặc sai owner | `IT-UC03-02..03` | Integration |
| Luồng thay thế | Gửi lại cùng Idempotency-Key | `IT-UC03-04` | Integration |
| Luồng thay thế | Cập nhật bằng version cũ | `IT-UC03-05` | Integration |
| Luồng thay thế | Danh sách trên 100 giao dịch | `IT-UC03-06` | Integration |
| Luồng lỗi bảo mật | User A dùng transaction của user B | `IT-UC03-07` | Integration |
| Luồng chính hoàn chỉnh | Thêm giao dịch từ UI đến DB | `E2E-UC03-01` | E2E |
| Luồng cập nhật | Sửa giao dịch và tải lại danh sách | `E2E-UC03-02` | E2E |
| Luồng xóa | Xóa và cập nhật thống kê | `E2E-UC03-03` | E2E |
| Luồng xem dữ liệu | Lọc theo tháng, loại, danh mục | `E2E-UC03-04` | E2E |

Bảng trên là bằng chứng trực tiếp cho việc test case được xây dựng từ luồng chính,
luồng thay thế, luồng lỗi và quy tắc nghiệp vụ của use case, không phải được liệt
kê ngẫu nhiên theo công nghệ.

## 5. Danh mục Unit Test - 30 test case

Các bảng thực thi sử dụng cột trạng thái theo quy ước: `Not Run` khi chưa chạy,
`Pass` khi kết quả thực tế khớp hoàn toàn với kết quả kỳ vọng và `Fail` khi có ít
nhất một bước hoặc một kết quả không khớp. Khi in báo cáo có thể thay ô trống bằng
`Pass` hoặc `Fail` sau mỗi lần chạy.

| ID | Use case | Mô tả thực hiện | Kết quả kỳ vọng | Trạng thái (Pass/Fail) |
|---|---|---|---|---|
| UT-UC01-01 | Đăng ký | Tên, email, mật khẩu hợp lệ | Validation thành công | Not Run |
| UT-UC01-02 | Đăng ký | Email `not-an-email` | Báo lỗi Email | Not Run |
| UT-UC01-03 | Đăng ký | Mật khẩu dưới 8 ký tự | Báo lỗi Password | Not Run |
| UT-UC01-04 | Đăng ký | Tên chỉ có khoảng trắng | Báo lỗi Name | Not Run |
| UT-UC01-05 | Đăng ký | Email ` USER@EXAMPLE.COM ` | Chuẩn hóa thành `user@example.com` | Not Run |
| UT-UC02-01 | Đăng nhập | Mật khẩu rỗng | Báo lỗi Password | Not Run |
| UT-UC03-01 | Giao dịch | Amount hợp lệ | Mapper giữ đúng ID, loại, số tiền, ngày | Not Run |
| UT-UC03-02 | Giao dịch | Amount bằng 0 | Từ chối | Not Run |
| UT-UC03-03 | Giao dịch | Amount âm | Từ chối | Not Run |
| UT-UC04-01 | Danh mục | Tên ` Ăn uống ` | Chuẩn hóa thành `Ăn uống` | Not Run |
| UT-UC04-02 | Danh mục | Tên chỉ có khoảng trắng | Từ chối | Not Run |
| UT-UC04-03 | Danh mục | Type INCOME hoặc EXPENSE | Chấp nhận | Not Run |
| UT-UC04-04 | Danh mục | Type có giá trị 999 | Từ chối | Not Run |
| UT-UC05-01 | Ngân sách | Amount bằng 0 | Từ chối | Not Run |
| UT-UC05-02 | Ngân sách | Amount âm | Từ chối | Not Run |
| UT-UC05-03 | Ngân sách | `2026-01`, `2026-12` | Chấp nhận | Not Run |
| UT-UC05-04 | Ngân sách | `2026-00`, `2026-13` | Từ chối | Not Run |
| UT-UC05-05 | Ngân sách | `2026-8`, `08-2026`, rỗng | Từ chối sai định dạng | Not Run |
| UT-UC05-06 | Ngân sách | Category INCOME | Không cho phép lập ngân sách | Not Run |
| UT-UC06-01 | Mục tiêu | Nạp 300.000, còn thiếu 800.000 | Cộng đủ 300.000 | Not Run |
| UT-UC06-02 | Mục tiêu | Nạp 500.000, còn thiếu 200.000 | Chỉ cộng 200.000 | Not Run |
| UT-UC06-03 | Mục tiêu | Mục tiêu đã hoàn thành | Không cộng thêm tiền | Not Run |
| UT-UC07-01 | Nhắc nhở | Ngày 31, tháng sau có 30 ngày | Lập lịch ngày 30 | Not Run |
| UT-UC07-02 | Nhắc nhở | Ngày 31, tháng 2 năm nhuận | Lập lịch ngày 29 | Not Run |
| UT-UC08-01 | Thống kê | Thu 5 triệu, chi 1,2 triệu | Số dư 3,8 triệu | Not Run |
| UT-UC08-02 | Thống kê | Thu 0, chi 700.000 | Số dư -700.000 | Not Run |
| UT-UC08-03 | Thống kê | Không có giao dịch | Số dư 0 | Not Run |
| UT-UC09-01 | OCR | Kết quả thiếu số tiền | Báo lỗi dữ liệu review | Not Run |
| UT-UC09-02 | OCR | OCR có tổng tiền và ngày hợp lệ | Parser trích xuất đúng | Not Run |
| UT-UC09-03 | OCR | Activity được tạo lại | Không xử lý callback trùng | Not Run |

## 6. Danh mục Integration Test - 39 test case

| ID | Use case | Mô tả thực hiện | Kết quả kỳ vọng | Trạng thái (Pass/Fail) |
|---|---|---|---|---|
| IT-UC01-01 | Đăng ký | API + DB, email mới | User chưa xác minh được tạo | Not Run |
| IT-UC01-02 | Xác minh | API + DB, mã đúng | User được xác minh và nhận token | Not Run |
| IT-UC01-03 | Xác minh | Mã sai hoặc hết hạn | Từ chối, trạng thái user không đổi | Not Run |
| IT-UC01-04 | Đăng ký | Email đã xác minh | Conflict, không tạo user trùng | Not Run |
| IT-UC02-01 | Đăng nhập | API + DB, mật khẩu đúng | Trả access và refresh token | Not Run |
| IT-UC02-02 | Đăng nhập | Mật khẩu sai | Không cấp token | Not Run |
| IT-UC02-03 | Refresh | Access token hết hạn | Cấp access token mới | Not Run |
| IT-UC02-04 | Bảo mật phiên | Tái sử dụng refresh token cũ | Thu hồi session liên quan | Not Run |
| IT-UC03-01 | Giao dịch | Controller + service + DB | Giao dịch được lưu | Not Run |
| IT-UC03-02 | Giao dịch | Category sai loại | Backend từ chối | Not Run |
| IT-UC03-03 | Giao dịch | Category thuộc user khác | Không lưu dữ liệu | Not Run |
| IT-UC03-04 | Giao dịch | Hai POST cùng Idempotency-Key | Chỉ tạo một bản ghi | Not Run |
| IT-UC03-05 | Giao dịch | Update bằng version cũ | Trả 412, không ghi đè | Not Run |
| IT-UC03-06 | Giao dịch | Trên 100 giao dịch | Phân trang không mất/trùng dữ liệu | Not Run |
| IT-UC03-07 | Giao dịch | User A truy cập transaction user B | Không trả hoặc thay đổi dữ liệu B | Not Run |
| IT-UC04-01 | Danh mục | Controller + DB, dữ liệu hợp lệ | Category được lưu đúng owner | Not Run |
| IT-UC04-02 | Danh mục | Trùng tên và loại | Từ chối duplicate | Not Run |
| IT-UC04-03 | Danh mục | Category đang được sử dụng | Không cho đổi loại hoặc xóa | Not Run |
| IT-UC05-01 | Ngân sách | Category chi và tháng hợp lệ | Ngân sách được lưu | Not Run |
| IT-UC05-02 | Ngân sách | Cùng category và tháng | Update bản ghi cũ | Not Run |
| IT-UC05-03 | Ngân sách | Category thu hoặc sai owner | Backend từ chối | Not Run |
| IT-UC06-01 | Mục tiêu | API + DB, dữ liệu hợp lệ | Mục tiêu được tạo | Not Run |
| IT-UC06-02 | Mục tiêu | Nạp tiền | CurrentAmount và history cùng cập nhật | Not Run |
| IT-UC06-03 | Mục tiêu | Gửi lại cùng Idempotency-Key | Không cộng tiền hai lần | Not Run |
| IT-UC07-01 | Nhắc nhở | Backend + Android AlarmManager | Reminder được lưu và alarm được tạo | Not Run |
| IT-UC07-02 | Nhắc nhở | User A và B có reminder | Alarm được tách theo user | Not Run |
| IT-UC07-03 | Nhắc nhở | User A đăng xuất | Chỉ alarm của A bị hủy | Not Run |
| IT-UC08-01 | Thống kê | API + DB có dữ liệu thu/chi | Tổng thu, chi, số dư chính xác | Not Run |
| IT-UC08-02 | Thống kê | DB có nhiều user | Chỉ tính dữ liệu user hiện tại | Not Run |
| IT-UC08-03 | Báo cáo | Xuất Excel | OpenXML hợp lệ và đúng dữ liệu | Not Run |
| IT-UC08-04 | Báo cáo | Xuất CSV/PDF | Đúng định dạng và đúng owner | Not Run |
| IT-UC09-01 | OCR | Upload JPEG/PNG hợp lệ | Receipt và ảnh được lưu | Not Run |
| IT-UC09-02 | OCR | File sai loại hoặc quá lớn | API trả lỗi phù hợp | Not Run |
| IT-UC09-03 | OCR | Worker + OCR service thành công | Trạng thái REVIEW_REQUIRED | Not Run |
| IT-UC09-04 | OCR | OCR lỗi tạm thời | Worker retry đúng chính sách | Not Run |
| IT-UC09-05 | OCR | Worker mất lease | Job được reclaim | Not Run |
| IT-UC09-06 | OCR | Confirm dữ liệu hợp lệ | Tạo đúng một transaction | Not Run |
| IT-UC09-07 | OCR | Confirm cùng receipt hai lần | Không tạo transaction trùng | Not Run |
| IT-UC10-01 | Tài khoản | User A truy cập dữ liệu user B | Không lộ hoặc thay đổi dữ liệu B | Not Run |

## 7. Danh mục E2E Test - 22 test case

| ID | Use case | Mô tả thực hiện | Kết quả kỳ vọng | Trạng thái (Pass/Fail) |
|---|---|---|---|---|
| E2E-UC01-01 | Đăng ký | Mở app, đăng ký, nhập mã xác minh | Vào màn hình chính và có session | Not Run |
| E2E-UC02-01 | Đăng nhập | Nhập tài khoản đúng | Hiển thị Dashboard đúng user | Not Run |
| E2E-UC02-02 | Đăng nhập | Nhập sai mật khẩu | Hiển thị lỗi, không tạo session | Not Run |
| E2E-UC02-03 | Quản lý phiên | Để access token hết hạn rồi tải dữ liệu | Tự refresh và tiếp tục hoạt động | Not Run |
| E2E-UC02-04 | Đăng xuất | Đăng xuất trong Settings | Xóa session, về Login | Not Run |
| E2E-UC03-01 | Giao dịch | Thêm giao dịch chi | Hiển thị trong danh sách và Dashboard | Not Run |
| E2E-UC03-02 | Giao dịch | Sửa số tiền/nội dung | Danh sách và thống kê được cập nhật | Not Run |
| E2E-UC03-03 | Giao dịch | Xóa và xác nhận | Giao dịch biến mất, số liệu cập nhật | Not Run |
| E2E-UC03-04 | Giao dịch | Lọc tháng, loại, category | Chỉ hiện dữ liệu phù hợp | Not Run |
| E2E-UC05-01 | Ngân sách | Tạo ngân sách rồi thêm khoản chi | Số đã chi và tỷ lệ được cập nhật | Not Run |
| E2E-UC05-02 | Ngân sách | Chi vượt hạn mức | Hiển thị cảnh báo rõ ràng | Not Run |
| E2E-UC06-01 | Mục tiêu | Tạo mục tiêu rồi nạp tiền | Số hiện tại và history chính xác | Not Run |
| E2E-UC06-02 | Mục tiêu | Nạp đủ target | Hiển thị hoàn thành | Not Run |
| E2E-UC07-01 | Nhắc nhở | Tạo reminder và chờ đến lịch | Android hiển thị notification | Not Run |
| E2E-UC08-01 | Thống kê | Tạo thu/chi rồi mở Statistics | KPI và biểu đồ chính xác | Not Run |
| E2E-UC08-02 | Báo cáo | Chọn tháng và xuất file | Tải/mở file có đúng dữ liệu | Not Run |
| E2E-UC09-01 | OCR | Chụp/chọn ảnh, upload và process | Hiển thị màn hình review | Not Run |
| E2E-UC09-02 | OCR | Chỉnh kết quả và confirm | Tạo một giao dịch trong danh sách | Not Run |
| E2E-UC09-03 | OCR | Upload ảnh không nhận dạng được | Cho retry hoặc nhập tay | Not Run |
| E2E-UC09-04 | OCR | Xoay màn hình lúc review | Ảnh và draft được giữ nguyên | Not Run |
| E2E-UC10-01 | Tài khoản | Đăng nhập A rồi B | B không thấy dữ liệu A | Not Run |
| E2E-UC10-02 | Tài khoản | Nhập mật khẩu và xóa tài khoản | Không đăng nhập lại được | Not Run |

## 8. Ma trận truy vết Use Case - Test Case

### 8.1. Ma trận số lượng và mức bao phủ

| Use case | Unit | Integration | E2E | Tổng case | Luồng chính | Luồng thay thế | Luồng lỗi |
|---|---:|---:|---:|---:|:---:|:---:|:---:|
| UC01 - Đăng ký/xác minh | 5 | 4 | 1 | 10 | Có | Có | Có |
| UC02 - Đăng nhập/phiên | 1 | 4 | 4 | 9 | Có | Có | Có |
| UC03 - Giao dịch | 3 | 7 | 4 | 14 | Có | Có | Có |
| UC04 - Danh mục | 4 | 3 | 0 | 7 | Có | Có | Có |
| UC05 - Ngân sách | 6 | 3 | 2 | 11 | Có | Có | Có |
| UC06 - Mục tiêu | 3 | 3 | 2 | 8 | Có | Có | Có |
| UC07 - Nhắc nhở | 2 | 3 | 1 | 6 | Có | Có | Có |
| UC08 - Thống kê/báo cáo | 3 | 4 | 2 | 9 | Có | Có | Có |
| UC09 - OCR hóa đơn | 3 | 7 | 4 | 14 | Có | Có | Có |
| UC10 - Tài khoản | 0 | 1 | 2 | 3 | Có | Có | Có |
| **Tổng** | **30** | **39** | **22** | **91** |  |  |  |

UC04 không có E2E độc lập vì danh mục được kiểm tra gián tiếp trong luồng tạo giao
dịch và ngân sách. UC10 không có Unit Test riêng vì các hành vi được kiểm tra ở
ranh giới API, database và phiên người dùng. Nếu yêu cầu bắt buộc mọi use case đều
có đủ ba cấp độ, cần bổ sung E2E quản lý danh mục và Unit Test cho chuẩn hóa dữ liệu
tài khoản; đây là quyết định về phạm vi, không nên tạo test chỉ để làm đầy ma trận.

### 8.2. Ma trận truy vết chi tiết

| Use case | Unit Test | Integration Test | E2E Test |
|---|---|---|---|
| UC01 | UT-UC01-01..05 | IT-UC01-01..04 | E2E-UC01-01 |
| UC02 | UT-UC02-01 | IT-UC02-01..04 | E2E-UC02-01..04 |
| UC03 | UT-UC03-01..03 | IT-UC03-01..07 | E2E-UC03-01..04 |
| UC04 | UT-UC04-01..04 | IT-UC04-01..03 | Được bao phủ gián tiếp bởi UC03/UC05 |
| UC05 | UT-UC05-01..06 | IT-UC05-01..03 | E2E-UC05-01..02 |
| UC06 | UT-UC06-01..03 | IT-UC06-01..03 | E2E-UC06-01..02 |
| UC07 | UT-UC07-01..02 | IT-UC07-01..03 | E2E-UC07-01 |
| UC08 | UT-UC08-01..03 | IT-UC08-01..04 | E2E-UC08-01..02 |
| UC09 | UT-UC09-01..03 | IT-UC09-01..07 | E2E-UC09-01..04 |
| UC10 | Không có case độc lập | IT-UC10-01 | E2E-UC10-01..02 |

## 9. Mẫu trình bày một test case chi tiết

Danh mục ở các phần trên dùng để rà soát tổng thể. Khi thực thi hoặc đưa vào phụ
lục, mỗi test case quan trọng nên được trình bày theo mẫu sau:

| Thuộc tính | Nội dung mẫu |
|---|---|
| Test Case ID | IT-UC03-04 |
| Tên test | Không tạo trùng giao dịch khi gửi lại request |
| Use Case tham chiếu | UC03 - Quản lý giao dịch, luồng thay thế gửi lại request |
| Mục tiêu | Kiểm tra tính idempotent của API tạo giao dịch |
| Tiền điều kiện | User đã đăng nhập; category EXPENSE thuộc user; chưa có transaction với key kiểm thử |
| Dữ liệu | Amount = 100.000; Idempotency-Key = `transaction-001` |
| Bước 1 | Gửi POST tạo giao dịch với key `transaction-001` |
| Bước 2 | Gửi lại đúng request và cùng key |
| Kết quả mong đợi | Hai response cùng đại diện một giao dịch; database chỉ có một bản ghi |
| Kết quả thực tế | Điền sau khi chạy test |
| Trạng thái | Not Run / Pass / Fail / Blocked |
| Bằng chứng | Log, ảnh chụp, tên hàm test hoặc đường dẫn báo cáo CI |

## 10. Cách rà soát ma trận

Ma trận được rà soát theo hai chiều.

### 10.1. Rà soát xuôi từ yêu cầu đến test

Với từng use case:

1. Kiểm tra đã có case cho luồng chính chưa.
2. Kiểm tra từng luồng thay thế có ít nhất một case chưa.
3. Kiểm tra từng luồng lỗi có case từ chối và xác nhận dữ liệu không đổi chưa.
4. Kiểm tra quy tắc dữ liệu đã có lớp hợp lệ, không hợp lệ và giá trị biên chưa.
5. Kiểm tra quyền sở hữu dữ liệu và phân tách user.
6. Kiểm tra các thay đổi trạng thái quan trọng.
7. Chọn một E2E đại diện cho kết quả nghiệp vụ cuối cùng.

### 10.2. Rà soát ngược từ test đến yêu cầu

Với từng test case:

1. Test có mã use case và nguồn luồng rõ ràng hay không.
2. Kết quả mong đợi có đo được hay chỉ ghi chung chung “thành công”.
3. Test có trùng mục tiêu với case khác không.
4. Cấp độ test có phù hợp không; ví dụ validation thuần không cần E2E.
5. Test tự động có đúng với test case thiết kế hay chỉ là test kỹ thuật.
6. Nếu use case bị xóa hoặc thay đổi, xác định được ngay các test bị ảnh hưởng.

## 11. Cách thể hiện trong báo cáo đồ án

Nên trình bày theo thứ tự sau để người đọc thấy rõ nguồn gốc test:

1. **Đặc tả use case:** tác nhân, tiền điều kiện, hậu điều kiện, luồng chính, luồng
   thay thế và luồng lỗi.
2. **Bảng dẫn xuất test:** chọn một use case tiêu biểu như UC03 hoặc UC09 và chỉ ra
   mỗi nhánh sinh ra test case nào.
3. **Danh mục test case theo cấp độ:** ba bảng Unit, Integration và E2E như các
   phần 5-7.
4. **Ma trận truy vết:** trình bày bảng Use Case - Test Case ở phần 8.
5. **Kết quả thực thi:** số Pass, Fail, Skip, Not Run theo môi trường và ngày chạy.
6. **Đánh giá độ bao phủ:** nêu use case hoặc nhánh chưa có test, rủi ro và kế hoạch
   bổ sung.

Không dùng số lượng test đã Pass để thay thế cho ma trận truy vết. Một hệ thống có
nhiều test kỹ thuật vẫn có thể bỏ sót luồng nghiệp vụ. Bằng chứng tốt nhất là mỗi
use case và mỗi nhánh quan trọng đều liên kết được tới ID test cụ thể.

## 12. Kết luận

Bộ 91 test case bao phủ 10 use case ở ba cấp độ kiểm thử. Cách đặt ID và ma trận
truy vết cho phép chứng minh rõ rằng test case được xây dựng từ yêu cầu nghiệp vụ.
Unit Test bảo vệ quy tắc nhỏ và giá trị biên; Integration Test kiểm tra sự phối hợp
và tính toàn vẹn dữ liệu; E2E Test xác nhận người dùng hoàn thành được mục tiêu
nghiệp vụ từ đầu đến cuối.
