# Text-line recognition annotations

Moi dong hop le duoc crop thanh mot anh. File nhan la UTF-8 TSV, khong co
header, chinh xac hai cot:

```text
data/recognition/train/images/ck_001_a_l001.jpg	CIRCLE K
data/recognition/train/images/ck_001_a_l002.jpg	TONG THANH TOAN
data/recognition/train/images/ck_001_a_l003.jpg	125.000
```

Ky tu phan cach la mot tab that, khong phai chuoi `\t`.

Quy tac:

- Giu dau tieng Viet, `.`, `,`, `%`, `/` va `-`.
- Khong chuan hoa format ngay hoac so tien trong transcription.
- Khong dua crop khong doc duoc vao TSV.
- Augmentation chi tao tu train va khong duoc thay doi label.
- Validation/test khong augmentation.
- Muc tieu train toi thieu 5.000 text-line hop le.
