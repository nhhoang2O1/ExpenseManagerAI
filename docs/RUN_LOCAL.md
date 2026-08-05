# Chay local Backend va OCR

Tai lieu nay dung cho moi truong phat trien local cua V2. Android chi goi
backend; `ocr-service` chi truy cap duoc ben trong Docker network.

## 1. Chuan bi

Can cai:

- Docker Desktop co Docker Compose V2.
- NVIDIA GPU, driver NVIDIA va Docker co quyen truy cap GPU. Cau hinh mac dinh
  chay OCR bang GPU.
- .NET 8 SDK neu muon chay migration tu host.
- Android Studio va Android emulator cho luong mobile.

Tao file env local:

```powershell
Copy-Item .env.example .env
```

Thay `POSTGRES_PASSWORD`, `JWT_SECRET` va cac bien `SMTP_*` trong `.env`.
`SMTP_USERNAME` va `SMTP_FROM_ADDRESS` la tai khoan Gmail dung de gui mail;
`SMTP_PASSWORD` la Gmail App Password, khong phai mat khau dang nhap thong
thuong. Secret JWT local nen la chuoi ngau nhien toi thieu 32 ky tu. Khong
commit `.env`.

## 2. Khoi dong Docker

Build va chay toan bo stack:

```powershell
docker compose up --build -d
docker compose ps
docker compose logs -f backend ocr-service
```

Lan khoi dong OCR dau tien co the lau hon vi PaddleOCR can tai model. Health OCR
phai tra `device=gpu`.

Neu muon chay OCR bang CPU, dung file override rieng:

```powershell
docker compose -f docker-compose.yml -f docker-compose.cpu.yml up --build -d
```

Cong local:

| Dich vu | Dia chi |
|---|---|
| Backend | `http://localhost:8080` |
| PostgreSQL | `localhost:5432` |
| OCR | Khong publish; backend goi `http://ocr-service:8000` |
| Email | Backend gui truc tiep qua `smtp.gmail.com:587` |

`postgres-data` la volume chinh cua PostgreSQL. Anh receipt moi duoc luu trong
bang `receipt_images` cua PostgreSQL cung transaction voi metadata. Volume
`receipt-storage` chi duoc mount read-only de importer anh tu phien ban cu trong
giai doan migration; sau khi xac minh import co the xoa volume nay.

Dung stack:

```powershell
docker compose down
```

Lenh tren khong xoa volume. Khong dung `docker compose down -v` neu chua backup
PostgreSQL. Chi xoa `receipt-storage` rieng sau khi da kiem tra bang
`receipt_images` co day du byte anh.

## 3. Migration PostgreSQL

Backend Docker image dat `Database__ApplyMigrations=true`, vi vay migration se
duoc ap dung khi container backend khoi dong sau khi PostgreSQL healthy. Lan
dau, migration staging giu `file_path`, doc tung file legacy tu volume read-only,
ghi vao `receipt_images`, kiem tra kich thuoc, roi moi drop `file_path`. Neu
thieu file, backend dung khoi dong de tranh mat anh im lang.

Neu database da co receipt cu, phai chay backend voi volume legacy duoc mount
de importer co the doc file. Khong chay migration final bang SQL truc tiep truoc
khi import xong. Neu muon chay migration thu cong tu host, tat co tu dong
migration va dung cung connection string:

```powershell
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=expense_manager;Username=expense_manager;Password=<POSTGRES_PASSWORD>"
dotnet ef database update --project backend/src/ExpenseManager.Api --startup-project backend/src/ExpenseManager.Api
```

Khong dua mat khau that vao script hay tai lieu.

## 4. Android emulator

Android emulator khong dung `localhost` cua may host. Debug build goi:

```text
http://10.0.2.2:8080
```

Thiet bi Android vat ly phai dung IP LAN cua may chay Docker, vi du
`http://192.168.1.20:8080`, va firewall phai cho phep TCP 8080.

## 5. GPU, CPU va OCR

Cau hinh mac dinh dung CUDA 12.6 va Paddle GPU. Entrypoint se dung container
neu Paddle khong duoc build voi CUDA hoac Docker khong cap GPU, tranh truong hop
am tham roi ve CPU.

Ban CPU chi duoc kich hoat bang `docker-compose.cpu.yml`. PaddleOCR tren CPU co the:

- Tai model va khoi tao cham o lan chay dau.
- Can nhieu RAM hon cac service con lai.
- Mat vai giay hoac lau hon cho moi receipt tuy CPU, do dai anh va tien xu ly.

OCR service dat `PADDLE_PDX_ENABLE_MKLDNN_BYDEFAULT=False` mac dinh de tranh
loi oneDNN/PIR cua PaddleOCR v3 tren Windows CPU. Chi bat lai MKLDNN khi da
test tren may deploy cu the.

Healthcheck OCR co `start_period` 60 giay de model co thoi gian khoi tao.
Theo doi tai nguyen va log:

```powershell
docker stats
docker compose logs -f ocr-service
docker compose exec ocr-service python -c "import paddle; print(paddle.device.get_device())"
```

Khong dung thoi gian cua request dau tien lam benchmark. Warm up model, sau do
do nhieu receipt va bao cao median/p95 cung cau hinh may.

## 6. Troubleshooting

### Compose bao thieu bien env

Dam bao `.env` ton tai o cung thu muc voi `docker-compose.yml` va hai bien
`POSTGRES_PASSWORD`, `JWT_SECRET` khong rong:

```powershell
docker compose config
```

### Port 5432 hoac 8080 da bi chiem

Tim process/container dang dung port, dung no, sau do chay lai:

```powershell
Get-NetTCPConnection -LocalPort 5432,8080 -ErrorAction SilentlyContinue
docker ps
```

Contract local co dinh PostgreSQL host port 5432 va backend host port 8080,
khong doi mapping neu dang kiem thu tich hop Android.

### Backend khong ket noi PostgreSQL

```powershell
docker compose ps
docker compose logs postgres backend
```

Trong container, database host phai la `postgres`, khong phai `localhost`.
Backend chi khoi dong sau khi `pg_isready` bao healthy.

### Backend cho OCR unavailable

```powershell
docker compose ps
docker compose logs ocr-service backend
docker compose exec ocr-service python -c "import urllib.request; print(urllib.request.urlopen('http://127.0.0.1:8000/health').read())"
```

OCR service can cung cap `GET /health` tren port 8000. Khong them mapping public
cho OCR chi de Android goi truc tiep.

### OCR khoi dong lau hoac container bi kill

Kiem tra RAM trong Docker Desktop, model cache va log. Tang memory limit neu
co `OOMKilled`; lan dau khoi dong can cho model load xong. Neu may demo chi co
CPU, xu ly tung receipt thay vi gui nhieu request dong thoi.

### Android emulator khong goi duoc API

Kiem tra backend bang `http://localhost:8080/health` tren host, sau do xac nhan
base URL Android la `http://10.0.2.2:8080`. Voi HTTP cleartext trong debug,
Android network security config phai cho phep dia chi local nay.
