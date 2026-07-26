# LEGACY — KHÔNG PHẢI KIẾN TRÚC ĐANG CHẠY

> Tài liệu này mô tả bản Room/SQLite cũ và chỉ được giữ để tham khảo lịch sử.
> Ứng dụng hiện tại gọi backend API; dữ liệu nghiệp vụ và ảnh receipt nằm trong
> PostgreSQL. Xem `docs/README.md` và `docs/ANDROID_ARCHITECTURE.md`.

# HƯỚNG DẪN ÔN TẬP LẬP TRÌNH ANDROID: QUẢN LÝ DỮ LIỆU & LƯU TRỮ (ROOM DATABASE & SHAREDPREFERENCES)

Tài liệu này đi sâu vào cách lưu trữ dữ liệu trong ứng dụng của bạn, bao gồm cơ sở dữ liệu quan hệ **Room Database (SQLite)** và lưu trữ cấu hình nhỏ **SharedPreferences**. Đây là phần thầy cô thường xuyên đặt câu hỏi truy vấn cấu trúc dữ liệu.

---

## 1. Cơ sở dữ liệu Room Database

Room Database cung cấp một lớp trừu tượng phía trên SQLite, giúp ứng dụng tương tác với cơ sở dữ liệu một cách an toàn và mạnh mẽ hơn so với việc viết các câu lệnh SQLiteHelper truyền thống.

### 1.1. Cấu trúc bảng cơ sở dữ liệu (Entities)
Ứng dụng có 4 thực thể chính tương ứng với 4 bảng trong SQLite:

#### 1. Bảng Người dùng (`users`) - [User.java](file:///d:/AppQuanLyChiTieu/app/src/main/java/com/example/appquanlychitieu/data/model/User.java)
*   **Thuộc tính:** `id` (Khóa chính - tự tăng), `name`, `email` (Duy nhất - unique index), `password`, `createdAt`.
*   **Chú thích nổi bật:** `@Index(value = "email", unique = true)` đảm bảo rằng không thể đăng ký hai tài khoản trùng email trong cơ sở dữ liệu.

#### 2. Bảng Danh mục (`categories`) - [Category.java](file:///d:/AppQuanLyChiTieu/app/src/main/java/com/example/appquanlychitieu/data/model/Category.java)
*   **Thuộc tính:** `id` (Khóa chính - tự tăng), `name`, `icon` (lưu tên file ảnh drawable), `color` (lưu mã màu HEX dạng chuỗi), `type` (Enum `TransactionType` nhận giá trị INCOME hoặc EXPENSE), `isDefault` (đánh dấu danh mục mặc định của hệ thống).

#### 3. Bảng Giao dịch (`transactions`) - [Transaction.java](file:///d:/AppQuanLyChiTieu/app/src/main/java/com/example/appquanlychitieu/data/model/Transaction.java)
*   **Thuộc tính:** `id` (Khóa chính - tự tăng), `amount` (Số tiền), `note` (Ghi chú), `date` (Thời gian - timestamp dạng long), `categoryId` (Khóa ngoại), `type` (INCOME/EXPENSE), `userId` (Khóa ngoại liên kết người dùng).
*   **Khóa ngoại (ForeignKey):**
    ```java
    foreignKeys = @ForeignKey(
            entity = Category.class,
            parentColumns = "id",
            childColumns = "categoryId",
            onDelete = ForeignKey.SET_NULL
    )
    ```
    *   *Ý nghĩa:* Liên kết cột `categoryId` của bảng `transactions` tới cột `id` của bảng `categories`. 
    *   *onDelete = ForeignKey.SET_NULL:* Nếu người dùng xóa một Danh mục chi tiêu, tất cả các giao dịch thuộc danh mục đó không bị xóa mất đi mà chỉ bị chuyển giá trị cột `categoryId` về `NULL` (tránh mất dữ liệu lịch sử chi tiêu).

#### 4. Bảng Hạn mức (`budgets`) - [Budget.java](file:///d:/AppQuanLyChiTieu/app/src/main/java/com/example/appquanlychitieu/data/model/Budget.java)
*   **Thuộc tính:** `id` (Khóa chính - tự tăng), `categoryId` (Khóa ngoại), `amount` (Số tiền hạn mức), `monthYear` (Tháng áp dụng, ví dụ: "2026-05"), `userId` (Khóa ngoại).
*   **Khóa ngoại (ForeignKey):** Liên kết `categoryId` của bảng `budgets` tới `id` của bảng `categories` với thuộc tính `onDelete = ForeignKey.CASCADE`. Nếu danh mục bị xóa thì hạn mức của danh mục đó cũng tự động bị xóa theo.
*   **Chỉ mục duy nhất (Unique Index):**
    `indices = @Index(value = {"categoryId", "monthYear", "userId"}, unique = true)` đảm bảo mỗi danh mục chi tiêu chỉ có duy nhất 1 hạn mức được thiết lập trong 1 tháng đối với 1 người dùng cụ thể.

---

### 1.2. Type Converters (Bộ chuyển đổi kiểu dữ liệu)
*   **Tệp tin:** [Converters.java](file:///d:/AppQuanLyChiTieu/app/src/main/java/com/example/appquanlychitieu/data/database/Converters.java).
*   **Tại sao cần?** SQLite chỉ hỗ trợ các kiểu dữ liệu nguyên thủy (NULL, INTEGER, REAL, TEXT, BLOB). Trong code Java, chúng ta sử dụng kiểu dữ liệu Enum `TransactionType` (INCOME, EXPENSE). 
*   **Cách hoạt động:** Room dùng hai hàm được đánh dấu `@TypeConverter` để tự động biến đổi:
    *   Khi ghi dữ liệu: Đổi Enum `TransactionType` thành `String` để lưu vào SQLite.
    *   Khi đọc dữ liệu: Đọc chuỗi `String` từ SQLite rồi chuyển ngược lại thành Enum `TransactionType` trong đối tượng Java.

---

### 1.3. Lớp DAO (Data Access Object - Giao tiếp dữ liệu)
DAO chứa các phương thức khai báo truy vấn SQL hoặc thao tác CRUD. Room tự sinh mã thực thi ngầm cho các Interface này.
*   **Ví dụ truy vấn phức tạp** trong [TransactionDao.java](file:///d:/AppQuanLyChiTieu/app/src/main/java/com/example/appquanlychitieu/data/database/dao/TransactionDao.java):
    *   *Tính tổng số tiền chi tiêu/thu nhập:*
        `SELECT COALESCE(SUM(amount), 0) FROM transactions WHERE userId = :userId AND type = :type AND date BETWEEN :startDate AND :endDate`
        (Sử dụng `COALESCE` để nếu chưa có giao dịch nào thì trả về số 0 thay vì giá trị NULL gây lỗi).
    *   *Tính toán thống kê theo danh mục:*
        Truy vấn sử dụng phép nối bảng `INNER JOIN` giữa bảng `transactions` và bảng `categories`, gom nhóm theo `categoryId` để tính tổng số tiền đã tiêu của từng danh mục phục vụ cho việc vẽ biểu đồ hình tròn.

---

## 2. Lưu trữ nhỏ gọn SharedPreferences
*   **Tệp tin:** [SessionManager.java](file:///d:/AppQuanLyChiTieu/app/src/main/java/com/example/appquanlychitieu/util/SessionManager.java).
*   **Cách thức khởi tạo:**
    `context.getSharedPreferences("expense_manager_session", Context.MODE_PRIVATE)`
    *   `MODE_PRIVATE`: Đảm bảo tệp tin cấu hình này chỉ có thể được đọc và ghi bởi chính ứng dụng này, các ứng dụng khác bên ngoài không thể truy cập vào được.
*   **Phương thức ghi dữ liệu:** Sử dụng `editor.putLong()`, `editor.putString()`, ... và bắt buộc phải gọi `editor.apply()` (ghi dữ liệu bất đồng bộ dưới nền) hoặc `editor.commit()` (ghi đồng bộ ngay lập tức và trả về trạng thái boolean) để lưu thông tin xuống ổ đĩa.
*   **Dữ liệu lưu trữ:** Trạng thái đăng nhập `is_logged_in` (boolean), ID người dùng `user_id` (long), tên người dùng `user_name` (String), email người dùng `user_email` (String).

---

## 3. Bộ câu hỏi lý thuyết & trả lời mẫu dành cho giảng viên

### Câu hỏi 1: Sự khác biệt cơ bản giữa Room Database và SharedPreferences là gì? Khi nào dùng cái nào?
*   **Trả lời mẫu:** 
    *   **SharedPreferences:** Dùng để lưu trữ dữ liệu nhỏ, đơn giản dưới dạng các cặp khóa-giá trị (Key-Value) như cấu hình ứng dụng, cài đặt bật/tắt chế độ tối, trạng thái đăng nhập của người dùng. Dữ liệu được lưu dưới dạng file XML thô nên truy xuất nhanh nhưng không hỗ trợ truy vấn phức tạp hay liên kết dữ liệu.
    *   **Room Database:** Dùng để lưu trữ dữ liệu có cấu trúc, quan hệ phức tạp và có số lượng bản ghi lớn (như danh sách hàng trăm giao dịch, các danh mục, hạn mức chi tiêu). Room hỗ trợ viết các câu lệnh truy vấn SQL phức tạp (SELECT, JOIN, GROUP BY), hỗ trợ ràng buộc toàn vẹn dữ liệu (khóa ngoại, chỉ mục duy nhất) và hỗ trợ trả về dữ liệu dạng LiveData để cập nhật UI tự động.

### Câu hỏi 2: Ràng buộc `onDelete = ForeignKey.CASCADE` và `ForeignKey.SET_NULL` trong các thực thể của em có ý nghĩa gì?
*   **Trả lời mẫu:**
    *   `CASCADE` (trong bảng `budgets` liên kết với `categories`): Có nghĩa là xóa dây chuyền. Nếu một Danh mục chi tiêu bị xóa, hệ thống sẽ tự động xóa tất cả các bản ghi Hạn mức thiết lập cho danh mục đó. Vì hạn mức không thể tồn tại nếu không gắn liền với một danh mục cụ thể nào.
    *   `SET_NULL` (trong bảng `transactions` liên kết với `categories`): Có nghĩa là đặt về null. Nếu một Danh mục chi tiêu bị xóa, các giao dịch thuộc danh mục đó vẫn được giữ lại trong lịch sử, chỉ có trường `categoryId` của giao dịch đó là bị chuyển thành `NULL` (hoặc hiển thị "Không xác định"). Điều này giúp bảo toàn số liệu tổng thu chi của người dùng không bị sai lệch khi xóa một danh mục.

### Câu hỏi 3: Tại sao em lại trả về `LiveData<List<Transaction>>` trong Dao mà không phải là `List<Transaction>` bình thường?
*   **Trả lời mẫu:**
    *   Nếu trả về `List<Transaction>` bình thường, đó là dữ liệu tĩnh. Mỗi khi có thay đổi (thêm, xóa, sửa giao dịch), em lại phải viết code truy vấn lại database thủ công và cập nhật lên UI.
    *   Khi trả về **LiveData**, Room Database sẽ tự động theo dõi bảng dữ liệu tương ứng. Mỗi khi có một thao tác ghi (Insert/Update/Delete) tác động lên bảng `transactions`, Room sẽ tự động thực hiện lại truy vấn đó dưới background thread và thông báo dữ liệu mới nhất cho View thông qua cơ chế Observer. Giao diện người dùng sẽ luôn tự động cập nhật một cách thời gian thực mà không cần viết thêm mã xử lý đồng bộ thủ công.
