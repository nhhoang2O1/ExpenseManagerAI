# Danh gia va hoan thien theo yeu cau do an

Tai lieu nay doi chieu ung dung quan ly chi tieu hien tai voi yeu cau do an sau khi bo qua phan AI/OCR.

## Pham vi bo qua

- OCR va cac noi dung lien quan den nhan dien hoa don tu anh.
- Tien xu ly anh, OpenCV, Tesseract/EasyOCR/PaddleOCR.
- Trich xuat ngay, cua hang, VAT tu hoa don.

## Doi chieu yeu cau chinh

| Yeu cau | Trang thai hien tai | Ghi chu |
|---|---|---|
| Khao sat yeu cau va phan tich bai toan | Dat mot phan | Da co tai lieu trong `docs/`, can cap nhat theo code moi. |
| Thiet ke co so du lieu | Dat | Room SQLite co user, category, transaction, budget, goal, reminder, goal history. |
| Quan ly giao dich them/sua/xoa | Dat | Co `AddEditTransactionActivity`, danh sach giao dich va xoa bang long-click. |
| Tim kiem giao dich | Da bo sung | Tim theo ghi chu hoac danh muc trong man hinh giao dich. |
| Thong ke va truc quan hoa | Dat | Co bieu do tron chi tieu theo danh muc va lich su theo thang. |
| Quan ly ngan sach | Dat | Co ngan sach theo danh muc/thang va theo doi muc da chi. |
| Xuat bao cao Excel/PDF | Da bo sung PDF | Settings co chuc nang xuat bao cao PDF bang Storage Access Framework. |
| Kiem thu va danh gia | Chua dat | Hien moi co test mau, can them test nghiep vu. |
| Trien khai va hoan thien | Dang hoan thien | Da sua loi build resource, can chay build/test va demo tren thiet bi. |

## Cac hoan thien da thuc hien

1. Sua loi resource lam app khong build duoc trong `bottom_nav_menu.xml`.
2. Bo sung tim kiem giao dich theo ghi chu hoac ten danh muc.
3. Bo sung xuat bao cao PDF trong man hinh Cai dat.
4. Bo sung truy van sync phuc vu viec tao bao cao.

## De xuat tiep theo

1. Viet migration Room that su va bo `fallbackToDestructiveMigration()`.
2. Them unit test cho dinh dang tien, ngay thang, hash mat khau va parse so tien.
3. Them instrumented test cho luong dang ky, dang nhap, them giao dich, thong ke.
4. Cap nhat cac bao cao trong `docs/` de khop voi code hien tai: BCrypt, version DB 6, muc tieu, nhac nho, xuat PDF.
