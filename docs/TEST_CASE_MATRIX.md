# Test case dựa trên các use case chính

Các test case dưới đây được sinh từ luồng chính, luồng thay thế và luồng lỗi
của từng use case. Unit, Integration và E2E chỉ là cấp độ thực thi của test
case, không phải điểm bắt đầu để thiết kế test.

## Unit Test - mỗi dòng là một test case

| ID | Use case | Dữ liệu / tình huống kiểm tra | Kết quả mong đợi | Test tự động | Trạng thái |
|---|---|---|---|---|---|
| UT-UC01-01 | Đăng ký | Tên, email và mật khẩu đều hợp lệ | Request được validation chấp nhận | `Register_accepts_valid_name_email_and_password` | Pass |
| UT-UC01-02 | Đăng ký | Email là `not-an-email` | Báo lỗi trường Email | `Register_rejects_invalid_email` | Pass |
| UT-UC01-03 | Đăng ký | Mật khẩu ngắn hơn 8 ký tự | Báo lỗi trường Password | `Register_rejects_password_shorter_than_eight_characters` | Pass |
| UT-UC01-04 | Đăng ký | Tên chỉ chứa khoảng trắng | Báo lỗi trường Name | `Register_rejects_blank_name` | Pass |
| UT-UC01-05 | Đăng ký/đăng nhập | Email là `  USER@EXAMPLE.COM  ` | Chuẩn hóa thành `user@example.com` | `Email_is_trimmed_and_normalized_to_lowercase` | Pass |
| UT-UC02-01 | Đăng nhập | Email sai định dạng | Báo lỗi trường Email | `Login_rejects_invalid_email` | Pass |
| UT-UC02-02 | Đăng nhập | Mật khẩu rỗng | Báo lỗi trường Password | `Login_rejects_empty_password` | Pass |
| UT-UC05-01 | Ngân sách | Amount bằng 0 | Báo lỗi trường Amount | `Budget_rejects_zero_amount` | Pass |
| UT-UC05-02 | Ngân sách | Amount bằng -1 | Báo lỗi trường Amount | `Budget_rejects_negative_amount` | Pass |
| UT-UC05-03 | Ngân sách | MonthYear là `2026-01` | Chấp nhận tháng 1 | `Budget_accepts_valid_month(2026-01)` | Pass |
| UT-UC05-04 | Ngân sách | MonthYear là `2026-12` | Chấp nhận tháng 12 | `Budget_accepts_valid_month(2026-12)` | Pass |
| UT-UC05-05 | Ngân sách | MonthYear là `2026-00` | Từ chối tháng 0 | `Budget_rejects_invalid_month(2026-00)` | Pass |
| UT-UC05-06 | Ngân sách | MonthYear là `2026-13` | Từ chối tháng 13 | `Budget_rejects_invalid_month(2026-13)` | Pass |
| UT-UC05-07 | Ngân sách | MonthYear là `2026-8` | Từ chối sai định dạng `yyyy-MM` | `Budget_rejects_invalid_month(2026-8)` | Pass |
| UT-UC05-08 | Ngân sách | MonthYear là `08-2026` | Từ chối sai thứ tự năm/tháng | `Budget_rejects_invalid_month(08-2026)` | Pass |
| UT-UC05-09 | Ngân sách | MonthYear rỗng | Từ chối | `Budget_rejects_invalid_month(empty)` | Pass |
| UT-UC05-10 | Ngân sách | Category loại EXPENSE | Cho phép dùng làm ngân sách | `Budget_accepts_expense_category` | Pass |
| UT-UC05-11 | Ngân sách | Category loại INCOME | Không cho phép dùng làm ngân sách | `Budget_rejects_income_category` | Pass |
| UT-UC06-01 | Mục tiêu | Số tiền nạp bằng 0 | Báo lỗi trường Amount | `Add_funds_rejects_zero_amount` | Pass |
| UT-UC06-02 | Mục tiêu | Số tiền nạp bằng -1 | Báo lỗi trường Amount | `Add_funds_rejects_negative_amount` | Pass |
| UT-UC06-03 | Mục tiêu | Mục tiêu 1 triệu, hiện có 200 nghìn, nạp 300 nghìn | Áp dụng 300 nghìn, số dư mới 500 nghìn | `Add_funds_applies_full_request_when_below_remaining_amount` | Pass |
| UT-UC06-04 | Mục tiêu | Mục tiêu 1 triệu, hiện có 800 nghìn, yêu cầu nạp 500 nghìn | Chỉ áp dụng 200 nghìn, không vượt mục tiêu | `Add_funds_caps_request_at_remaining_amount` | Pass |
| UT-UC06-05 | Mục tiêu | Mục tiêu đã đủ 1 triệu | AppliedAmount bằng 0 và xác định đã hoàn thành | `Add_funds_identifies_an_already_funded_goal` | Pass |
| UT-UC08-01 | Thống kê | Thu 5 triệu, chi 1,2 triệu | Số dư 3,8 triệu | `Balance_is_income_minus_expense(5000000,1200000)` | Pass |
| UT-UC08-02 | Thống kê | Chỉ có thu 2 triệu | Số dư 2 triệu | `Balance_is_income_minus_expense(2000000,0)` | Pass |
| UT-UC08-03 | Thống kê | Chỉ có chi 700 nghìn | Số dư âm 700 nghìn | `Balance_is_income_minus_expense(0,700000)` | Pass |
| UT-UC08-04 | Thống kê | Không có thu và chi | Số dư bằng 0 | `Balance_is_income_minus_expense(0,0)` | Pass |
| UT-UC04-01 | Danh mục | Tên là `  Ăn uống  ` | Chuẩn hóa thành `Ăn uống` | `Category_name_is_trimmed` | Pass |
| UT-UC04-02 | Danh mục | Tên chỉ chứa khoảng trắng | Chuẩn hóa thành rỗng để controller từ chối | `Category_blank_name_remains_empty_for_controller_rejection` | Pass |
| UT-UC04-03 | Danh mục | Type là INCOME | Chấp nhận | `Category_accepts_income_type` | Pass |
| UT-UC04-04 | Danh mục | Type là EXPENSE | Chấp nhận | `Category_accepts_expense_type` | Pass |
| UT-UC04-05 | Danh mục | Type ép thành giá trị 999 | Từ chối loại không hỗ trợ | `Category_rejects_unknown_type` | Pass |
| UT-UC04-06 | Danh mục | Color và Icon có khoảng trắng hai đầu | Loại bỏ khoảng trắng | `Category_optional_metadata_is_trimmed` | Pass |

Các test trên nằm trong `CoreBusinessRulesTests.cs`. Mỗi dòng `[InlineData]` của
xUnit được tính là một test case riêng khi chạy test.

## UC01 - Đăng ký và xác nhận tài khoản

| ID | Luồng | Tiền điều kiện / dữ liệu | Thao tác chính | Kết quả mong đợi | Cấp độ | Tự động hóa |
|---|---|---|---|---|---|---|
| TC-UC01-01 | Chính | Email mới, mật khẩu hợp lệ | Đăng ký, nhập đúng mã xác nhận | Tài khoản được xác thực, nhận access/refresh token và danh mục mặc định | Integration/E2E | Integration và API E2E Pass |
| TC-UC01-02 | Lỗi | Mã sai hoặc hết hạn | Xác nhận đăng ký | Backend từ chối, user vẫn chưa xác thực | Integration | `AuthTests.cs`, `AuthSecurityTests.cs` - Pass |
| TC-UC01-03 | Thay thế | Email chưa xác thực đã đăng ký trước đó | Đăng ký lại với đúng mật khẩu | Backend gửi mã xác nhận mới, không tạo user trùng | Integration | Chưa có test riêng |
| TC-UC01-04 | Lỗi | Email đã được xác thực | Đăng ký lại | Trả conflict, không tạo tài khoản mới | Integration | Có trong luồng auth, cần tách assertion riêng |

## UC02 - Đăng nhập và quản lý phiên

| ID | Luồng | Tiền điều kiện / dữ liệu | Thao tác chính | Kết quả mong đợi | Cấp độ | Tự động hóa |
|---|---|---|---|---|---|---|
| TC-UC02-01 | Chính | Tài khoản đã xác thực | Đăng nhập đúng email/mật khẩu | Backend cấp token; Android lưu token mã hóa và mở màn hình chính | E2E | API E2E Pass; Android UI chưa kiểm tra |
| TC-UC02-02 | Lỗi | Sai mật khẩu hoặc user chưa xác thực | Đăng nhập | Không cấp token, trả thông báo trung tính | Integration | `AuthTests.cs` - Pass |
| TC-UC02-03 | Thay thế | Access token hết hạn | Gửi nhiều request đồng thời | Chỉ refresh một lần, các request dùng token mới | Integration | `RefreshTokenAuthenticatorTest.java` - Pass |
| TC-UC02-04 | Lỗi | Refresh token cũ bị tái sử dụng | Gọi refresh | Revoke toàn bộ session liên quan | Integration | `AuthSecurityTests.cs` - Pass |
| TC-UC02-05 | Chính | User đang đăng nhập | Đăng xuất | Token bị revoke, dữ liệu session và alarm của user được xóa | E2E | API revoke Pass; dữ liệu/alarm Android chưa kiểm tra |

## UC03 - Quản lý giao dịch thu chi

| ID | Luồng | Tiền điều kiện / dữ liệu | Thao tác chính | Kết quả mong đợi | Cấp độ | Tự động hóa |
|---|---|---|---|---|---|---|
| TC-UC03-01 | Chính | Category đúng loại và thuộc user | Tạo giao dịch | Giao dịch được lưu và xuất hiện trong danh sách | E2E | `e2e_smoke.ps1` - Pass |
| TC-UC03-02 | Lỗi | Số tiền không dương | Tạo giao dịch | Validation/database từ chối | Integration | `DatabaseIntegrityPostgreSqlTests.cs` - Pass với Docker |
| TC-UC03-03 | Lỗi | Category sai loại hoặc thuộc user khác | Tạo giao dịch | Backend từ chối, không lưu dữ liệu | Integration | `FinancialControllerBehaviorTests.cs` - Pass |
| TC-UC03-04 | Thay thế | Request bị gửi lại với cùng Idempotency-Key | Tạo giao dịch lần hai | Chỉ tồn tại một giao dịch | Integration | `FinanceIntegrityTests.cs` - Pass |
| TC-UC03-05 | Thay thế | Hai thiết bị cùng sửa một giao dịch | Update bằng version cũ | Trả 412, không ghi đè dữ liệu mới | Integration | `FinanceIntegrityTests.cs` - Pass |
| TC-UC03-06 | Chính | Có trên 100 giao dịch | Xem danh sách nhiều trang | Không mất hoặc trùng giao dịch | Integration | `FinanceIntegrityTests.cs`, `RemoteTransactionRepositoryPaginationTest.java` - Pass |
| TC-UC03-07 | Lỗi | User A dùng ID transaction user B | Xem, sửa hoặc xóa | Không trả và không thay đổi dữ liệu user B | Integration | `UserIsolationTests.cs` - Pass |

## UC04 - Quản lý danh mục

| ID | Luồng | Tiền điều kiện / dữ liệu | Thao tác chính | Kết quả mong đợi | Cấp độ | Tự động hóa |
|---|---|---|---|---|---|---|
| TC-UC04-01 | Chính | Tên và loại hợp lệ | Tạo danh mục | Danh mục được lưu cho user hiện tại | Integration | `FinancialControllerBehaviorTests.cs` - Pass |
| TC-UC04-02 | Lỗi | Trùng tên và loại trong cùng user | Tạo danh mục | Backend từ chối duplicate | Integration | Chưa có test case tách riêng |
| TC-UC04-03 | Lỗi | Category đang được transaction/budget sử dụng | Đổi loại hoặc xóa | Backend từ chối để bảo vệ dữ liệu tài chính | Integration | `FinanceIntegrityTests.cs` - Pass |
| TC-UC04-04 | Lỗi | User A dùng category ID user B | Sửa hoặc xóa | Không thay đổi category user B | Integration | Được bao phủ bởi kiểm tra ownership backend |
| TC-UC04-05 | Lỗi | Type bị ép thành giá trị enum 999 | Tạo danh mục | Backend trả bad request, không lưu dữ liệu | Unit + Integration | Unit và Integration Pass |
| TC-UC04-06 | Thay thế | Tên, màu và icon có khoảng trắng hai đầu | Tạo danh mục | Dữ liệu được chuẩn hóa trước khi lưu | Unit + Integration | Unit và Integration Pass |

## UC05 - Quản lý ngân sách

| ID | Luồng | Tiền điều kiện / dữ liệu | Thao tác chính | Kết quả mong đợi | Cấp độ | Tự động hóa |
|---|---|---|---|---|---|---|
| TC-UC05-01 | Chính | Category chi tiêu thuộc user | Tạo ngân sách tháng | Ngân sách được lưu và trả đúng số tiền | E2E | `e2e_smoke.ps1` - Pass |
| TC-UC05-02 | Thay thế | Đã có ngân sách cùng category/tháng | Tạo lại với số tiền mới | Cập nhật bản ghi hiện có, không tạo trùng | Integration | `FinancialControllerBehaviorTests.cs` - Pass |
| TC-UC05-03 | Lỗi | Category thu nhập hoặc thuộc user khác | Tạo ngân sách | Backend từ chối | Integration | `FinancialControllerBehaviorTests.cs` - Pass |
| TC-UC05-04 | Lỗi | MonthYear sai định dạng | Tạo ngân sách | Backend trả bad request | Integration | `FinancialControllerBehaviorTests.cs` - Pass |

## UC06 - Quản lý mục tiêu tiết kiệm

| ID | Luồng | Tiền điều kiện / dữ liệu | Thao tác chính | Kết quả mong đợi | Cấp độ | Tự động hóa |
|---|---|---|---|---|---|---|
| TC-UC06-01 | Chính | Tên và target hợp lệ | Tạo mục tiêu | Mục tiêu xuất hiện trong danh sách | E2E | `e2e_smoke.ps1` - Pass |
| TC-UC06-02 | Chính | Mục tiêu chưa hoàn thành | Nạp tiền | CurrentAmount và history được cập nhật | Integration | `FinanceIntegrityTests.cs` - Pass |
| TC-UC06-03 | Thay thế | Số nạp lớn hơn phần còn thiếu | Nạp tiền | Chỉ áp dụng phần còn thiếu | Integration | `FinanceIntegrityTests.cs` - Pass |
| TC-UC06-04 | Thay thế | Gửi lại cùng Idempotency-Key | Nạp tiền lần hai | Không cộng tiền hai lần | Integration | `FinanceIntegrityTests.cs` - Pass |

## UC07 - Quản lý nhắc nhở

| ID | Luồng | Tiền điều kiện / dữ liệu | Thao tác chính | Kết quả mong đợi | Cấp độ | Tự động hóa |
|---|---|---|---|---|---|---|
| TC-UC07-01 | Chính | Nội dung và thời gian hợp lệ | Tạo reminder | Backend lưu reminder; Android tạo alarm đúng user | Integration/E2E | Backend, API E2E và Android scheduling integration Pass; Android UI E2E chưa có |
| TC-UC07-02 | Chính | Alarm tháng hiện tại vừa phát | Receiver lên lịch lại | Chỉ tạo lịch cho kỳ tháng tiếp theo | Unit + Integration | Unit và Android Integration Pass |
| TC-UC07-03 | Thay thế | Chọn ngày 31 | Lên lịch qua tháng 30 hoặc tháng 2 | Dùng ngày hợp lệ 30/28/29 | Unit | `ReminderScheduleCalculatorTest.java` - Pass |
| TC-UC07-04 | Lỗi | Reminder không có user hợp lệ | Lên lịch | Không tạo alarm và không ghi `user_-1` | Integration | Android Integration Pass |
| TC-UC07-05 | Chính | User A và B đều có reminder | User A đăng xuất | Chỉ alarm/dữ liệu A bị xóa, B được giữ nguyên | Integration | Android Integration Pass |
| TC-UC07-06 | Thay thế | Gửi lại create cùng Idempotency-Key | Tạo reminder lần hai | Không tạo reminder trùng | Integration | `ReminderIntegrationTests.cs` - Pass |
| TC-UC07-07 | Lỗi | Update version cũ hoặc xóa reminder user khác | Sửa/xóa reminder | Trả 412/404, dữ liệu không đổi | Integration | `ReminderIntegrationTests.cs` - Pass |

## UC08 - Xem thống kê và xuất báo cáo

| ID | Luồng | Tiền điều kiện / dữ liệu | Thao tác chính | Kết quả mong đợi | Cấp độ | Tự động hóa |
|---|---|---|---|---|---|---|
| TC-UC08-01 | Chính | Có giao dịch thu và chi | Xem thống kê tháng | Tổng thu, chi và số dư chính xác | Integration | `FinancialControllerBehaviorTests.cs` - Pass |
| TC-UC08-02 | Lỗi | Dữ liệu của nhiều user | Xem thống kê | Chỉ tính giao dịch user hiện tại | Integration | `FinancialControllerBehaviorTests.cs` - Pass |
| TC-UC08-03 | Lỗi | Khoảng ngày hoặc tháng sai | Xem thống kê | Backend trả bad request | Integration | `FinancialControllerBehaviorTests.cs` - Pass |
| TC-UC08-04 | Chính | Chọn tháng hợp lệ | Xuất Excel | File có cấu trúc OpenXML hợp lệ và đúng dữ liệu | Integration | `ExcelReportTests.cs` - Pass |
| TC-UC08-05 | Chính | Chọn CSV/PDF | Xuất báo cáo | Tải được đúng định dạng và dữ liệu user | Integration/E2E | Chưa có test tự động đầy đủ |

## UC09 - Quét và xác nhận hóa đơn

| ID | Luồng | Tiền điều kiện / dữ liệu | Thao tác chính | Kết quả mong đợi | Cấp độ | Tự động hóa |
|---|---|---|---|---|---|---|
| TC-UC09-01 | Chính | Ảnh hóa đơn chụp thực tế | Upload và process | Receipt đi đến REVIEW_REQUIRED | E2E | Ảnh Android MediaStore + PP-OCRv6 GPU - Pass |
| TC-UC09-02 | Lỗi | Ảnh sai loại, quá lớn hoặc không giải mã được | Upload | Service từ chối với status phù hợp | Unit + Integration | OCR tests - Pass |
| TC-UC09-03 | Thay thế | OCR lỗi tạm thời | Worker xử lý | Retry rồi chuyển OCR_FAILED nếu hết số lần | Integration | `ReceiptProcessingTests.cs` - Pass |
| TC-UC09-04 | Thay thế | Worker dừng giữa quá trình | Lease hết hạn | Job được reclaim sau restart | Integration | `ReceiptProcessingTests.cs` - Pass |
| TC-UC09-05 | Chính | Kết quả OCR hợp lệ | User chỉnh sửa và xác nhận | Tạo đúng một transaction và xuất hiện trong danh sách | Integration/E2E | Integration và API E2E Pass |
| TC-UC09-06 | Thay thế | Receipt ở OCR_FAILED | User nhập tay hợp lệ | Vẫn tạo được transaction | Integration | `ReceiptConfirmationTests.cs` - Pass |
| TC-UC09-07 | Lỗi | Xác nhận cùng receipt hai lần | Confirm lần hai | Không tạo transaction trùng | Integration | `ReceiptConfirmationTests.cs` - Pass |
| TC-UC09-08 | Lỗi | User A dùng receipt ID user B | Xem ảnh/process/confirm | Không lộ hoặc thay đổi receipt user B | Integration | `ReceiptControllerTests.cs` - Pass |

## UC10 - Quản lý tài khoản

| ID | Luồng | Tiền điều kiện / dữ liệu | Thao tác chính | Kết quả mong đợi | Cấp độ | Tự động hóa |
|---|---|---|---|---|---|---|
| TC-UC10-01 | Chính | User đã đăng nhập | Cập nhật tên | Profile và session Android được cập nhật | Integration | `AuthSecurityTests.cs` - Pass backend |
| TC-UC10-02 | Chính | Mật khẩu hiện tại đúng | Đổi mật khẩu | Mật khẩu mới hoạt động, session cũ bị xử lý đúng | Integration | `AuthSecurityTests.cs` - Pass |
| TC-UC10-03 | Chính | Email mới hợp lệ | Yêu cầu và xác nhận đổi email | Email được cập nhật sau khi nhập đúng mã | Integration | `AuthSecurityTests.cs` - Pass |
| TC-UC10-04 | Lỗi | Sai mật khẩu hoặc mã xác nhận | Đổi thông tin bảo mật | Backend từ chối, dữ liệu không đổi | Integration | `AuthSecurityTests.cs` - Pass |
| TC-UC10-05 | Chính | User xác nhận bằng mật khẩu | Xóa tài khoản | Dữ liệu user bị xóa và không đăng nhập lại được | Integration/E2E | Integration Pass; E2E Android chưa có |

## Các luồng E2E chính

| ID | Use case kết hợp | Luồng xuyên suốt | Trạng thái |
|---|---|---|---|
| E2E-01 | UC01, UC02, UC03, UC05, UC06, UC07, UC08 | Đăng ký → xác nhận → đăng nhập → giao dịch → ngân sách → mục tiêu/idempotency → reminder → thống kê → đăng xuất | Pass trên Docker Development |
| E2E-02 | UC09, UC03 | Upload ảnh chụp hóa đơn → PP-OCRv6 → review → confirm → transaction xuất hiện | Pass với ảnh thật trên OCR GPU (`device=gpu`) |

## Quy tắc ghi kết quả

- Chỉ ghi **Pass** khi test đã chạy thành công trên phiên bản được báo cáo.
- Test PostgreSQL không chạy vì thiếu Docker phải ghi **Skip**.
- Test Android chỉ biên dịch nhưng chưa chạy emulator phải ghi **Not run**.
- Test kỹ thuật không truy vết được về use case không đưa vào bảng test case chính.

## Kết quả thực thi gần nhất

| Phạm vi | Pass | Skip | Fail | Ghi chú |
|---|---:|---:|---:|---|
| Android JVM Unit Test | 37 | 0 | 0 | Đã chạy `:app:testDebugUnitTest` |
| Backend Unit + Integration | 86 | 0 | 0 | Bao gồm 8 PostgreSQL Integration Test chạy bằng Docker |
| OCR Unit + Integration | 29 | 0 | 0 | Gồm test cấu hình device CPU/GPU; chạy bằng pytest |
| Android Integration Test | 5 | 0 | 0 | Chạy trên Medium_Phone_API_36.1; gồm 4 case nghiệp vụ và 1 case mẫu |
| E2E API nghiệp vụ | 1 | 0 | 0 | E2E-01 Pass trên Docker Development |
| E2E OCR ảnh chụp thực tế | 1 | 0 | 0 | PP-OCRv6 GPU xử lý ảnh Android MediaStore và tạo transaction thành công |
