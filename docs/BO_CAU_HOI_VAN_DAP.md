# Bộ câu hỏi và trả lời mô phỏng vấn đáp — Expense Manager AI

> Tài liệu này bám theo code hiện tại. Khi có mâu thuẫn với báo cáo cũ, ưu tiên
> `AUTHORITATIVE_OVERVIEW.md`, `ANDROID_ARCHITECTURE.md`, `API_REFERENCE.md` và code.

## 1. Bài giới thiệu nên học thuộc

### Phiên bản 30 giây

**Hỏi:** Em hãy giới thiệu ngắn gọn đề tài.

**Trả lời mẫu:** Expense Manager AI là ứng dụng Android quản lý tài chính cá nhân,
hỗ trợ thu chi, danh mục, ngân sách, mục tiêu tiết kiệm, nhắc nhở, thống kê, xuất
báo cáo và nhập giao dịch từ ảnh hóa đơn. Android viết bằng Java theo MVVM, gọi
ASP.NET Core 8 API bằng Retrofit và JWT. Backend dùng EF Core với PostgreSQL. OCR
là dịch vụ FastAPI/PaddleOCR riêng, chỉ backend được gọi. Sau OCR, người dùng vẫn
phải kiểm tra và xác nhận trước khi tạo giao dịch để tránh dữ liệu sai.

### Phiên bản 2 phút

**Hỏi:** Em hãy trình bày bài toán, giải pháp và điểm nổi bật.

**Trả lời mẫu:** Bài toán là giúp một cá nhân ghi nhận và theo dõi tài chính nhưng
giảm thao tác nhập tay. Giải pháp gồm bốn phần. Android chịu trách nhiệm giao diện,
trạng thái màn hình và gọi API. ASP.NET Core xử lý xác thực, phân quyền theo người
dùng, luật nghiệp vụ, đồng thời, idempotency và báo cáo. PostgreSQL là nguồn dữ liệu
nghiệp vụ duy nhất. FastAPI/PaddleOCR xử lý ảnh hóa đơn và gợi ý tên cửa hàng, ngày,
tổng tiền, VAT cùng độ tin cậy.

Luồng nổi bật là hóa đơn bất đồng bộ: Android upload ảnh, backend lưu cả metadata
và byte ảnh, sau đó xếp job; worker gọi OCR, Android polling kết quả, người dùng
review rồi mới confirm để tạo giao dịch. Hệ thống còn xử lý retry, lease khi worker
crash, chống tạo trùng bằng idempotency key, chống ghi đè dữ liệu cũ bằng version và
ETag, tách dữ liệu tuyệt đối theo user, và dùng số nguyên `long`/`bigint` cho tiền VND.

## 2. Tổng quan và kiến trúc

### Câu 1. Vì sao chọn kiến trúc nhiều dịch vụ thay vì làm tất cả trong Android?

**Trả lời:** Vì dữ liệu cần dùng được trên nhiều thiết bị, cần xác thực tập trung,
phân quyền, sao lưu và xử lý OCR nặng. Tách backend và OCR giúp Android nhẹ hơn;
OCR có thể dùng GPU, thay model hoặc mở rộng độc lập mà không phát hành lại app.
Đổi lại, hệ thống phụ thuộc mạng và triển khai phức tạp hơn.

### Câu 2. Luồng dữ liệu chuẩn từ UI đến database là gì?

**Trả lời:** `Activity/Fragment → ViewModel → Repository → Retrofit ApiService →
Controller → service/EF Core → PostgreSQL`. Dữ liệu trả về đi ngược lại dưới dạng
DTO, ViewModel chuyển thành state/LiveData để UI render.

### Câu 3. Kiến trúc Android có phải MVVM thuần không?

**Trả lời:** Có thể gọi là MVVM kết hợp Repository. View không gọi Retrofit trực
tiếp; ViewModel giữ trạng thái qua thay đổi cấu hình; Repository đóng vai trò biên
dữ liệu. Tuy nhiên đây không phải Clean Architecture đầy đủ vì chưa tách riêng use
case/domain layer ở Android và việc khởi tạo dependency còn khá trực tiếp.

### Câu 4. Database chính là Room hay PostgreSQL?

**Trả lời:** Ở phiên bản hiện tại là PostgreSQL. Android không có database nghiệp
vụ Room/SQLite trong runtime và `app/build.gradle` cũng không có dependency Room.
Android chỉ lưu dữ liệu vận hành tối thiểu như token mã hóa, receipt draft, metadata
alarm và tùy chọn UI. Một số tài liệu cũ nói Room là kiến trúc trước đây, không nên
dùng để mô tả bản hiện tại.

### Câu 5. Vì sao không cache toàn bộ dữ liệu nghiệp vụ trên Android?

**Trả lời:** Để tránh hai nguồn sự thật và bài toán đồng bộ xung đột. PostgreSQL là
nguồn authoritative nên các thiết bị thấy dữ liệu nhất quán hơn. Nhược điểm là trải
nghiệm offline còn hạn chế; hướng phát triển có thể thêm cache read-only/outbox có
chiến lược đồng bộ rõ ràng.

### Câu 6. Trách nhiệm của từng thành phần là gì?

**Trả lời:** Android lo UI và state; API lo auth, quyền sở hữu, validation và nghiệp
vụ; PostgreSQL lưu dữ liệu, session và byte ảnh; worker nhận job OCR và quản lý
retry/lease; OCR service tiền xử lý, nhận dạng và trích xuất trường; SMTP gửi mã xác
minh/khôi phục tài khoản.

### Câu 7. Vì sao Android không gọi thẳng OCR service?

**Trả lời:** Backend cần kiểm soát quyền sở hữu, kích thước/tính hợp lệ của ảnh,
trạng thái job, retry và lưu kết quả nhất quán. OCR không có lớp auth công khai và
chỉ được expose trong Docker network. Nếu Android gọi trực tiếp, dịch vụ xử lý nặng
sẽ bị lộ và luồng dữ liệu dễ mất nhất quán.

### Câu 8. Vì sao dùng Docker Compose?

**Trả lời:** Compose mô tả nhất quán PostgreSQL, backend và OCR, gồm biến môi trường,
network, volume, GPU/CPU override, healthcheck và thứ tự phụ thuộc. Nhờ đó môi trường
demo gần với môi trường triển khai và giảm cấu hình thủ công.

## 3. Android, MVVM và vòng đời

### Câu 9. ViewModel giải quyết vấn đề gì?

**Trả lời:** ViewModel tách logic trạng thái khỏi Activity/Fragment và sống qua việc
xoay màn hình. UI observe LiveData nên chỉ render theo state, tránh gọi lại mạng hoặc
mất tiến trình không cần thiết khi view được tạo lại.

### Câu 10. Repository pattern có ích gì trong dự án?

**Trả lời:** Repository che giấu chi tiết Retrofit, chuẩn hóa callback/lỗi và tạo một
điểm truy cập dữ liệu cho ViewModel. Nhờ vậy UI không phụ thuộc trực tiếp vào HTTP
contract và dễ mock/test hơn, dù dự án hiện tại vẫn có thể cải thiện dependency
injection để kiểm thử ViewModel tốt hơn.

### Câu 11. LiveData khác dữ liệu bình thường ở điểm nào?

**Trả lời:** LiveData nhận biết lifecycle, chỉ thông báo cho observer đang active.
ViewModel giữ `MutableLiveData`, còn UI thường chỉ nhận `LiveData`, nhờ đó giảm việc
UI sửa state tùy ý và hạn chế cập nhật một view đã bị destroy.

### Câu 12. App xử lý access token hết hạn thế nào?

**Trả lời:** `JwtInterceptor` gắn access token. Khi nhận 401,
`RefreshTokenAuthenticator` thực hiện refresh theo cơ chế single-flight để nhiều
request đồng thời không cùng rotate một refresh token. Request được thử lại một lần;
nếu thất bại thì token bị xóa và app phát sự kiện session-expired để về màn login.

### Câu 13. Token được lưu ở đâu và bảo vệ ra sao?

**Trả lời:** Token pair được lưu trong `EncryptedSharedPreferences` của AndroidX
Security: key dùng AES-256-SIV và value dùng AES-256-GCM. Đây tốt hơn SharedPreferences
thuần, nhưng thiết bị đã root hoặc runtime bị compromise vẫn là rủi ro; release còn
phải bắt buộc HTTPS.

### Câu 14. Vì sao debug dùng `10.0.2.2` thay vì `localhost`?

**Trả lời:** Trong Android emulator, `localhost` là chính emulator. `10.0.2.2` là
địa chỉ đặc biệt trỏ về loopback của máy host, nơi backend Docker mở cổng 8080.

### Câu 15. App chống response cũ ghi đè response mới như thế nào?

**Trả lời:** Các luồng bất đồng bộ quan trọng dùng token/yêu cầu mới nhất. Ví dụ
`ReceiptViewModel` tăng `operationToken`; callback mang token cũ sẽ bị bỏ qua. Cách
này ngăn callback đến trễ làm UI quay lại trạng thái cũ.

### Câu 16. Nếu app bị xoay màn hình hoặc process bị kill khi đang OCR thì sao?

**Trả lời:** ViewModel chịu được rotation, còn process death được xử lý bằng
`ReceiptDraftStore`, lưu receipt ID, phase, URI ảnh và idempotency key. Khi mở lại,
app đọc draft, upload lại an toàn nếu cần hoặc GET receipt rồi tiếp tục polling.

### Câu 17. Nhắc nhở hoạt động thế nào?

**Trả lời:** Backend là nguồn dữ liệu của reminder; Android đồng bộ reminder theo
user rồi lập lịch alarm cục bộ. `ReminderReceiver` hiển thị thông báo và
`BootReceiver` giúp lập lịch lại sau khi máy khởi động. Metadata alarm phải gắn user
để tránh nhắc nhở của tài khoản trước xuất hiện ở tài khoản sau.

## 4. Backend, API và cơ sở dữ liệu

### Câu 18. Backend dùng những công nghệ chính nào?

**Trả lời:** ASP.NET Core 8 Web API, Entity Framework Core 8, Npgsql cho PostgreSQL,
JWT Bearer authentication, Swagger/OpenAPI, built-in rate limiting, health checks và
`BackgroundService` cho worker OCR.

### Câu 19. Các entity nghiệp vụ chính là gì?

**Trả lời:** User, Category, Transaction, Budget, Goal, GoalHistory, Reminder,
Receipt, ReceiptImage và OcrResult. Phần bảo mật/vận hành có RefreshToken,
AccountVerificationCode và IdempotencyRecord.

### Câu 20. Dữ liệu của hai người dùng được tách bằng cách nào?

**Trả lời:** JWT chứa định danh user; `IUserContext` lấy ID đã xác thực. Mọi truy vấn
tài nguyên đều lọc theo `UserId`, và category liên quan cũng phải thuộc user đó.
Không tin `userId` do client gửi. Test `UserIsolationTests` kiểm tra user không xem
hoặc sửa giao dịch của user khác.

### Câu 21. Vì sao tiền dùng `long`/`bigint`, không dùng `double`?

**Trả lời:** VND trong hệ thống được lưu theo đơn vị nguyên nên `long` tránh sai số
dấu chấm động, ví dụ tổng nhiều khoản không sinh sai lệch nhị phân. `double` chỉ phù
hợp ở biên trực quan hóa như phần trăm/góc biểu đồ, không phải giá trị tiền domain.

### Câu 22. Database bảo vệ tính hợp lệ bằng gì ngoài validation ở controller?

**Trả lời:** Có foreign key, unique index, check constraint và concurrency token.
Ví dụ amount giao dịch/ngân sách phải dương; type chỉ INCOME/EXPENSE; một budget là
duy nhất theo user-category-tháng; một receipt chỉ liên kết tối đa một transaction;
goal phải có `0 ≤ currentAmount ≤ targetAmount`. Đây là lớp bảo vệ cuối khi có
request đồng thời hoặc bug ứng dụng.

### Câu 23. Idempotency là gì và dự án dùng ở đâu?

**Trả lời:** Idempotency giúp retry cùng một thao tác không tạo dữ liệu lần hai.
Client gửi UUID ổn định qua `Idempotency-Key`; backend lưu scope, key, hash request
và response trong `IdempotencyRecord`. Cùng key/cùng payload thì replay response;
cùng key/khác payload trả 409. Nó dùng cho các thao tác dễ bị retry như upload hóa
đơn, tạo giao dịch, thêm tiền mục tiêu và tạo reminder.

### Câu 24. Optimistic concurrency là gì?

**Trả lời:** Mỗi entity có `Version` là concurrency token. Server trả version/ETag;
client gửi `If-Match` khi sửa hoặc xóa. Nếu bản ghi đã thay đổi, server trả 412 thay
vì âm thầm ghi đè. Đây phù hợp khi xung đột hiếm và không muốn giữ lock suốt thời
gian người dùng đang sửa form.

### Câu 25. Khi nào dự án dùng lock bi quan?

**Trả lời:** Với thao tác tài chính đọc-sửa-ghi nhạy cảm, như cấp tiền cho goal hoặc
worker claim receipt, backend dùng transaction và PostgreSQL row lock. Lock bi quan
phù hợp vì hai request đồng thời có thể cùng đọc một số dư cũ rồi đều cho là hợp lệ.

### Câu 26. Pagination giao dịch được làm thế nào?

**Trả lời:** API nhận `page/pageSize`, lọc theo user và điều kiện ngày/loại/danh mục,
rồi sắp xếp ổn định theo `transaction_date`, `created_at`, `id`. Sort key phụ tránh
trùng hoặc thiếu item khi nhiều giao dịch cùng ngày.

### Câu 27. Vì sao thống kê được tính ở server?

**Trả lời:** PostgreSQL có thể lọc và aggregate gần dữ liệu, tránh tải toàn bộ giao
dịch về điện thoại, giảm băng thông và cho kết quả thống nhất giữa các client. Các API
daily, monthly và by-category phục vụ đúng nhu cầu màn hình.

### Câu 28. HTTP status quan trọng cần nhớ?

**Trả lời:** 201 là tạo thành công; 202 chỉ là đã xếp hàng, chưa xử lý xong; 400 là
input sai; 401 là chưa xác thực/token hết hạn; 404 là không tồn tại hoặc không thuộc
user tùy policy; 409 là xung đột lifecycle/idempotency/unique; 412 là ETag cũ; 429 là
vượt rate limit; 503 có thể là OCR runtime không sẵn sàng.

## 5. Xác thực và bảo mật

### Câu 29. Luồng đăng ký hiện tại như thế nào?

**Trả lời:** Backend chuẩn hóa email về lowercase, tạo user chưa xác minh, hash mật
khẩu, tạo danh mục mặc định và gửi mã 6 số. Register trả 202; người dùng gọi
`confirm-registration`. Mã đúng, chưa hết hạn và chưa quá số lần thử thì email được
xác minh và server mới tạo session/token pair.

### Câu 30. Mật khẩu có được tự hash SHA-256 ở Android không?

**Trả lời:** Không ở kiến trúc hiện tại. Android gửi mật khẩu qua HTTPS cho backend;
backend dùng `IPasswordHasher<User>` để hash có salt và verify. Tài liệu cũ mô tả
SHA-256 phía Android/Room là phiên bản legacy và không phản ánh code hiện tại.

### Câu 31. Access token và refresh token khác nhau thế nào?

**Trả lời:** Access token là JWT sống khoảng 15 phút, dùng gọi API. Refresh token
sống khoảng 30 ngày, là chuỗi ngẫu nhiên dùng để xin token pair mới. Database chỉ
lưu hash refresh token, không lưu raw token, để giảm thiệt hại nếu DB bị lộ.

### Câu 32. Refresh token rotation và reuse detection hoạt động ra sao?

**Trả lời:** Mỗi lần refresh thành công, token cũ bị revoke và trỏ tới token thay thế.
Nếu token cũ đã rotate bị dùng lại, backend coi là dấu hiệu bị đánh cắp, revoke toàn
bộ session và tăng `TokenVersion`; validator khiến access token cũ mất hiệu lực.

### Câu 33. Logout và logout-all khác gì?

**Trả lời:** Logout revoke refresh token của session hiện tại. Logout-all revoke mọi
refresh token của user và tăng token version để các access token đã phát cũng không
còn hợp lệ dù chưa hết thời gian.

### Câu 34. Hệ thống chống dò mật khẩu/mã xác minh thế nào?

**Trả lời:** Auth endpoints có fixed-window rate limit theo IP; luồng nhạy cảm có
quota chặt hơn. Mã 6 số sống 10 phút, tối đa 5 lần thử, chỉ lưu hash HMAC theo scope.
Forgot-password trả thông điệp trung lập để hạn chế dò xem email có tồn tại.

### Câu 35. Những rủi ro bảo mật còn lại là gì?

**Trả lời:** Debug dùng HTTP local; production phải cấu hình HTTPS. Rate limit theo
IP có thể chưa đủ sau proxy nếu cấu hình forwarded headers sai. Secret phải nằm ở
secret manager thay vì file. Cần log/audit, monitoring, dependency scanning, backup
mã hóa và kiểm thử xâm nhập trước khi gọi là production hoàn chỉnh.

## 6. OCR và vòng đời hóa đơn

### Câu 36. Hãy mô tả toàn bộ luồng hóa đơn.

**Trả lời:** (1) Android upload multipart kèm idempotency key. (2) Backend kiểm tra
ảnh, lưu metadata và byte ảnh vào PostgreSQL, trả 201. (3) Android gọi process;
backend chuyển QUEUED và trả 202. (4) Worker claim job, chuyển PROCESSING, gọi OCR.
(5) Kết quả thành REVIEW_REQUIRED hoặc retry rồi OCR_FAILED. (6) Android polling,
hiển thị ảnh và trường gợi ý. (7) Người dùng sửa/confirm; backend tạo đúng một giao
dịch EXPENSE và chuyển receipt thành CONFIRMED.

### Câu 37. Vì sao process trả 202 thay vì chờ OCR xong?

**Trả lời:** OCR có thể mất nhiều giây, lần đầu còn phải load model. Trả 202 giải
phóng HTTP request, tránh timeout và cho worker kiểm soát retry. 202 chỉ có nghĩa job
đã được nhận/xếp hàng nên client phải polling trạng thái.

### Câu 38. State machine của receipt gồm những trạng thái nào?

**Trả lời:** Luồng chính là `UPLOADED → QUEUED → PROCESSING → REVIEW_REQUIRED →
CONFIRMED`. Nếu lỗi có retry/backoff rồi cuối cùng `OCR_FAILED`. OCR_FAILED vẫn cho
phép người dùng nhập tay hợp lệ và confirm; đây là graceful degradation.

### Câu 39. Worker tránh hai instance xử lý cùng một receipt thế nào?

**Trả lời:** Worker claim job trong PostgreSQL bằng row lock, ghi lease và số lần thử.
Instance khác không claim cùng row đang được giữ/leased. Nếu worker crash, lease hết
hạn để job được reclaim. Đây đáng tin cậy hơn một cờ boolean không có thời hạn.

### Câu 40. Pipeline OCR gồm các bước gì?

**Trả lời:** Decode/validate ảnh → resize → đo sáng, tương phản, độ nét và edge ratio
→ thử perspective correction và deskew → tăng tương phản CLAHE/khử nhiễu khi hữu ích
→ PaddleOCR nhận dạng dòng chữ → parser trích tên cửa hàng, ngày, tổng tiền và VAT →
tính confidence, classification và warnings.

### Câu 41. Parser tìm tổng tiền bằng cách nào?

**Trả lời:** Parser chuẩn hóa chuỗi, tìm các mẫu số tiền và chấm điểm theo từ khóa
như “TỔNG THANH TOÁN”, “TỔNG CỘNG”, “PHẢI TRẢ”, “TOTAL”, đồng thời xét vị trí và
confidence. Đây là heuristic, không phải mô hình hiểu hóa đơn hoàn hảo, nên kết quả
luôn là gợi ý cần review.

### Câu 42. Tại sao OCR luôn trả REVIEW_REQUIRED kể cả confidence cao?

**Trả lời:** Giao dịch tài chính cần độ chính xác cao; OCR có thể đọc sai một chữ số
nhưng vẫn có confidence tương đối tốt. Human-in-the-loop giúp người dùng chịu trách
nhiệm xác nhận dữ liệu trước khi ảnh hưởng số dư, ngân sách và thống kê.

### Câu 43. Ảnh hóa đơn được lưu ở đâu?

**Trả lời:** Upload mới lưu byte trong bảng `receipt_images` kiểu `bytea`, cùng hệ
quản trị với metadata. Volume `receipt-storage` chỉ mount read-only để migrate ảnh
legacy. Ưu điểm là transaction và backup nhất quán; nhược điểm là DB phình nhanh,
nên quy mô lớn có thể chuyển object storage và giữ checksum/metadata trong DB.

### Câu 44. Vì sao không tự xóa receipt khi người dùng đóng màn hình?

**Trả lời:** Đóng màn hình có thể do rotation, navigation hoặc process death, không
đồng nghĩa người dùng muốn xóa dữ liệu. Xóa phải là hành động rõ ràng. Receipt đã tạo
transaction không được xóa để bảo toàn truy vết.

### Câu 45. Confirm hóa đơn có thể tạo hai giao dịch không?

**Trả lời:** Không theo thiết kế. Quan hệ Receipt–Transaction là one-to-one và
`transactions.receipt_id` có unique index. Confirmation service kiểm tra lifecycle;
nếu client timeout sau khi server commit rồi retry, receipt ID vẫn dẫn về cùng kết
quả thay vì tạo giao dịch thứ hai.

## 7. Luật nghiệp vụ tài chính

### Câu 46. Số dư và số dư khả dụng khác nhau thế nào?

**Trả lời:** `balance = income - expense`. `availableBalance = balance - reserved`,
trong đó reserved là tiền đã dành cho các mục tiêu đang hoạt động. Nhờ vậy người dùng
không vô tình chi phần tiền đã cam kết tiết kiệm.

### Câu 47. Khi nạp vượt số tiền còn thiếu của goal thì sao?

**Trả lời:** `GoalFundingRules` áp dụng `min(requested, target-current)`. Hệ thống
ghi cả requested amount và applied amount trong history; current amount không bao
giờ vượt target. Việc nạp còn phải kiểm tra số dư khả dụng trong transaction có lock.

### Câu 48. Vì sao cần GoalHistory?

**Trả lời:** CurrentAmount chỉ cho biết trạng thái hiện tại, không giải thích nó hình
thành thế nào. GoalHistory tạo audit trail cho FUND, COMPLETE, CANCEL, số tiền yêu
cầu/thực áp dụng và balance sau thao tác, hữu ích cho UI lịch sử và điều tra lỗi.

### Câu 49. Vì sao budget chỉ dùng category EXPENSE?

**Trả lời:** Ngân sách trong domain là hạn mức chi, nên gắn với danh mục chi. Backend
kiểm tra category thuộc user và có type EXPENSE. Nếu cần kế hoạch thu nhập thì nên là
khái niệm nghiệp vụ khác thay vì làm mơ hồ Budget hiện tại.

### Câu 50. Có thể đổi loại hoặc xóa category đang dùng không?

**Trả lời:** Không tùy tiện. Đổi type có thể làm giao dịch/ngân sách hiện hữu sai
nghĩa; xóa category đang được tham chiếu vi phạm toàn vẹn. Backend trả conflict và
yêu cầu xử lý/migrate dữ liệu liên quan trước.

### Câu 51. Vì sao giao dịch sinh khi hoàn thành goal không được sửa/xóa?

**Trả lời:** Nó là bằng chứng nghiệp vụ liên kết trạng thái hoàn thành goal với biến
động tiền. Cho sửa hoặc xóa riêng sẽ làm goal, history, số dư và transaction lệch
nhau. Controller trả 409 cho hai thao tác này.

## 8. Kiểm thử, triển khai và đánh giá

### Câu 52. Dự án có những tầng kiểm thử nào?

**Trả lời:** Android có unit test cho formatter/date, mapper, API contract,
authenticator, adapter diff và receipt state helper; có instrumented test cho alarm,
user isolation và receipt draft. Backend có unit/integration test cho auth/security,
business rules, isolation, database integrity, concurrency, idempotency, receipt,
OCR client và export. OCR có pytest cho schema, preprocessing, parser, adapter và
endpoint. Ngoài ra có script E2E smoke chạy qua stack Docker.

### Câu 53. Unit test, integration test và E2E khác nhau thế nào trong dự án?

**Trả lời:** Unit test cô lập một rule/parser/helper và chạy nhanh. Integration test
kiểm tra nhiều lớp với database hoặc HTTP contract, bắt lỗi mapping/constraint.
E2E dựng cả stack rồi đi qua API thực, có độ tin cậy cao hơn nhưng chậm và khó chẩn
đoán hơn. Ba tầng bổ sung cho nhau, không tầng nào thay hoàn toàn tầng khác.

### Câu 54. Healthcheck live và ready khác nhau thế nào?

**Trả lời:** Liveness trả lời process còn sống hay không; readiness trả lời service
đã sẵn sàng nhận traffic, bao gồm phụ thuộc như database. Tách hai loại giúp hệ thống
không restart một process chỉ vì dependency tạm lỗi nhưng vẫn ngừng gửi traffic tới
instance chưa sẵn sàng.

### Câu 55. Nếu không có GPU thì chạy thế nào?

**Trả lời:** Compose mặc định dùng GPU và cố ý fail nếu không được cấp CUDA, tránh
âm thầm chạy CPU rất chậm. Khi không có GPU phải dùng file override
`docker-compose.cpu.yml`. Health OCR trả `device=cpu` hoặc `gpu` để xác nhận runtime.

### Câu 56. Migration ảnh legacy có điểm an toàn nào?

**Trả lời:** Backend mount volume cũ read-only, đọc byte ảnh vào PostgreSQL, kiểm tra
kích thước rồi mới loại `file_path`. Nếu thiếu file, startup dừng thay vì âm thầm mất
dữ liệu. Không nên chạy migration final trực tiếp hoặc xóa volume trước khi xác minh.

### Câu 57. Điểm mạnh kỹ thuật nổi bật nhất là gì?

**Trả lời:** Luồng receipt có human review, state machine, idempotency, background
worker, retry và lease; phần tài chính có bigint, constraint, isolation, concurrency
và row lock; auth có refresh rotation/reuse detection; kiến trúc phân ranh giới rõ
giữa Android, API, database và OCR.

### Câu 58. Hạn chế lớn nhất hiện tại là gì?

**Trả lời:** App phụ thuộc mạng và chưa có offline-first; OCR parser vẫn heuristic và
cần dữ liệu đánh giá thực tế; lưu ảnh trong PostgreSQL khó mở rộng khi dung lượng lớn;
polling tạo request định kỳ; DI phía Android còn thủ công; môi trường demo dùng HTTP;
quan sát production như metrics/tracing/audit chưa đầy đủ. Nên thừa nhận cụ thể và
đề xuất lộ trình thay vì nói hệ thống “hoàn hảo”.

### Câu 59. Nếu có thêm thời gian em sẽ cải tiến gì?

**Trả lời:** Ưu tiên theo thứ tự: đo chất lượng OCR bằng dataset giữ riêng và metric
field-level; thêm HTTPS/reverse proxy và secret manager; metrics/tracing/alerting;
cache offline có outbox; thay polling bằng push/WebSocket hoặc notification; chuyển
ảnh sang object storage khi quy mô tăng; bổ sung DI và UI test Android; CI chạy test,
lint, build và security scan.

### Câu 60. Em chứng minh dự án chạy đúng bằng cách nào?

**Trả lời:** Không chỉ demo UI. Em chạy unit/integration test của ba thành phần,
Docker healthcheck, script E2E; kiểm tra PostgreSQL constraint; demo retry cùng
idempotency key không tạo bản ghi kép; thử user B truy cập resource user A; thử ETag
cũ; và trình diễn receipt từ upload đến confirm. Bằng chứng nên là kết quả lệnh và dữ
liệu DB/log, không chỉ ảnh chụp màn hình.

## 9. Mô phỏng hỏi xoáy

### Tình huống 1: “Tại sao em gọi là AI khi parser dùng regex?”

**Trả lời mẫu:** Phần nhận dạng chữ dùng mô hình PaddleOCR; regex/heuristic là tầng
trích xuất field sau recognition. Em không gọi toàn bộ parser là mô hình AI. Thiết kế
lai giúp thay parser hoặc fine-tune recognition độc lập. Giới hạn hiện tại là chưa có
mô hình information extraction học máy riêng.

### Tình huống 2: “OCR sai thì số dư sai, hệ thống của em có nguy hiểm không?”

**Trả lời mẫu:** OCR không tự commit giao dịch. Kết quả luôn vào REVIEW_REQUIRED;
người dùng xem ảnh gốc, chỉnh lại dữ liệu rồi confirm. Ngay cả OCR_FAILED vẫn cho
nhập tay. Unique relation và transaction backend ngăn confirm kép.

### Tình huống 3: “Chỉ cần unique index, tại sao còn idempotency?”

**Trả lời mẫu:** Unique index chỉ bảo vệ những trùng lặp đã có khóa nghiệp vụ rõ.
Hai lần tạo giao dịch thủ công có nội dung giống nhau vẫn có thể là hai giao dịch hợp
lệ. Idempotency key diễn đạt ý định “đây là cùng một request retry” và còn replay
đúng response hoặc phát hiện cùng key nhưng payload khác.

### Tình huống 4: “Có ETag rồi tại sao còn row lock?”

**Trả lời mẫu:** ETag bảo vệ cập nhật một resource khỏi dữ liệu cũ, phù hợp xung đột
hiếm. Cấp tiền goal phải đọc tổng tài chính và reserved rồi quyết định trong cùng thao
tác; hai request đồng thời có thể đều hợp lệ trên snapshot cũ. Transaction và row
lock bảo vệ invariant liên-resource ngay tại thời điểm quyết định.

### Tình huống 5: “Lưu ảnh trong database có phải thiết kế tệ không?”

**Trả lời mẫu:** Không tuyệt đối. Ở quy mô đồ án, nó đơn giản hóa transaction, quyền
truy cập, backup và migration; tránh metadata tồn tại nhưng file mất. Ở quy mô lớn,
chi phí DB/backup tăng, nên em sẽ dùng object storage, checksum và lifecycle policy,
vẫn giữ metadata cùng trạng thái trong PostgreSQL.

### Tình huống 6: “Ứng dụng có chạy offline không?”

**Trả lời mẫu:** Không phải offline-first. Dữ liệu nghiệp vụ authoritative ở backend;
local chỉ giữ token, draft và alarm metadata. Đây là trade-off để tránh đồng bộ phức
tạp. Nếu yêu cầu offline là bắt buộc, em sẽ thêm cache Room, outbox và conflict policy
thay vì khẳng định app hiện tại đã hỗ trợ.

### Tình huống 7: “Tài liệu nói Room, code lại không có. Vậy em trình bày cái nào?”

**Trả lời mẫu:** Em trình bày kiến trúc hiện hành theo tài liệu authoritative và code:
PostgreSQL là nguồn dữ liệu duy nhất, Android gọi API. Tài liệu Room là lịch sử phiên
bản cũ và cần được đánh dấu legacy. Em có thể chỉ ra `app/build.gradle` không có Room
và repository hiện tại gọi Retrofit.

### Tình huống 8: “Em có thể gọi hệ thống production-ready không?”

**Trả lời mẫu:** Em sẽ nói production-oriented thay vì production-ready tuyệt đối.
Hệ thống đã có auth, constraints, migration, retry, healthcheck, backup và test, nhưng
production thực tế còn cần TLS, secret manager, quan sát, đánh giá tải/OCR, security
review, disaster recovery rehearsal và chính sách dữ liệu.

## 10. Kịch bản vấn đáp 10 phút

1. **Phút 0–1:** Dùng phần giới thiệu 30 giây, nêu rõ bốn thành phần.
2. **Phút 1–3:** Vẽ luồng `Android → API → PostgreSQL` và nhánh `worker → OCR`.
3. **Phút 3–5:** Trình bày receipt state machine và lý do human review.
4. **Phút 5–6:** Nói về `long/bigint`, constraint, user isolation.
5. **Phút 6–7:** Nói access/refresh token, rotation và encrypted token store.
6. **Phút 7–8:** Nói idempotency khác concurrency và khi nào dùng row lock.
7. **Phút 8–9:** Nêu chiến lược test theo ba tầng.
8. **Phút 9–10:** Chủ động thừa nhận 2–3 hạn chế và hướng cải tiến.

## 11. Checklist trước khi vào vấn đáp

- Không nói Android hiện tại dùng Room cho transaction/budget.
- Không nói đăng ký trả token ngay; phải xác minh email trước.
- Không nói OCR tự động tạo giao dịch không cần người dùng duyệt.
- Không nói 202 nghĩa là OCR đã xong.
- Không nói JWT được lưu trong SharedPreferences thuần.
- Phân biệt idempotency, optimistic concurrency và row lock.
- Nhớ công thức `balance = income - expense` và
  `available = balance - reserved`.
- Nhớ emulator dùng `10.0.2.2`, OCR không mở cổng public.
- Khi bị hỏi hạn chế, trả lời thẳng và gắn với phương án cải tiến.
- Khi không nhớ tên class, mô tả đúng trách nhiệm và luồng trước.

## 12. File nên mở khi ôn cùng code

- `docs/AUTHORITATIVE_OVERVIEW.md`: ranh giới và luồng tổng thể.
- `docs/ANDROID_ARCHITECTURE.md`: MVVM, local state và receipt draft.
- `docs/API_REFERENCE.md`: route, status code và API behavior.
- `backend/src/ExpenseManager.Api/Program.cs`: cấu hình DI, JWT, rate limit, health.
- `backend/src/ExpenseManager.Api/Data/AppDbContext.cs`: bảng, quan hệ, index, constraint.
- `backend/src/ExpenseManager.Api/Domain/CoreBusinessRules.cs`: luật thuần dễ giải thích.
- `backend/src/ExpenseManager.Api/Services/ReceiptProcessingWorker.cs`: worker/lease/retry.
- `backend/src/ExpenseManager.Api/Services/AuthSessionService.cs`: refresh rotation.
- `app/src/main/java/com/example/appquanlychitieu/ui/receipt/ReceiptViewModel.java`:
  state machine phía Android và khôi phục draft.
- `ocr-service/app/services/preprocessing.py` và `parsers.py`: pipeline OCR.
