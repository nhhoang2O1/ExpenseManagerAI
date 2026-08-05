# Receipt OCR Service

Internal FastAPI service for Vietnamese retail receipt OCR. The service
preprocesses an uploaded image, runs PaddleOCR, extracts receipt fields, and
returns suggestions for mandatory user review.

## API

- `GET /health`
- `POST /internal/v1/ocr/receipts`
  - multipart field: `image`
  - accepted MIME types: JPEG, PNG, WebP, BMP
  - default maximum upload size: 10 MiB

Successful OCR requests return HTTP 200 with `status=REVIEW_REQUIRED`, including
for unrecognized or low-quality images. Invalid/unsupported image uploads return
4xx responses. OCR runtime/model failures return HTTP 503.

## Run on CPU

Python 3.11 is recommended.

```powershell
cd ocr-service
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements-dev.txt
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

Paddle downloads pretrained assets on first model initialization. The model is
lazy-loaded on the first OCR request by default. Set `OCR_PRELOAD_MODEL=true`
to initialize it during application startup instead.

The service sets `PADDLE_PDX_ENABLE_MKLDNN_BYDEFAULT=False` before importing
PaddleOCR. This keeps CPU inference on the portable Paddle backend and avoids a
known Windows CPU oneDNN/PIR inference failure. A tuned deployment may override
the variable after testing its own runtime.

Run tests without loading or downloading a Paddle model:

```powershell
pytest
```

## Configuration

All settings use the `OCR_` environment prefix.

| Variable | Default | Purpose |
| --- | --- | --- |
| `OCR_LANGUAGE` | `vi` | PaddleOCR language |
| `OCR_DEVICE` | `cpu` | PaddleOCR runtime device (`cpu` or `gpu`) |
| `OCR_MODEL_VERSION` | `paddleocr-v6-medium-vi` | Response model metadata |
| `OCR_PARSER_VERSION` | `receipt-parser-v1` | Response parser metadata |
| `OCR_RECOGNITION_MODEL_DIR` | unset | Local Paddle recognition inference model |
| `OCR_PRELOAD_MODEL` | `false` | Load model during FastAPI startup |
| `OCR_MAX_UPLOAD_BYTES` | `10485760` | Multipart image byte limit |
| `OCR_MAX_IMAGE_PIXELS` | `40000000` | Decoded image pixel limit |
| `OCR_MAX_IMAGE_SIDE` | `2200` | Longest side after resizing |
| `OCR_MIN_IMAGE_SIDE` | `64` | Minimum accepted width and height |
| `OCR_LOW_OCR_CONFIDENCE` | `0.45` | Low-confidence classification threshold |

`OCR_RECOGNITION_MODEL_DIR` is passed to PaddleOCR v3 as
`text_recognition_model_dir`. This allows a future exported local recognition
model to replace the pretrained recognizer without changing the HTTP API.
Detection remains pretrained unless the service is extended separately.

## Docker

Trong stack đầy đủ, image mặc định là CUDA 12.6/Paddle GPU và yêu cầu NVIDIA
GPU được Docker nhận diện:

```powershell
docker compose up --build -d
```

Entrypoint kiểm tra Paddle được biên dịch với CUDA và có ít nhất một GPU. Nếu
không thấy GPU, container dừng thay vì âm thầm chạy CPU. `/health` trả
`device=gpu` để xác nhận cấu hình.

Chỉ khi muốn dùng CPU mới chạy file override riêng:

```powershell
docker compose -f docker-compose.yml -f docker-compose.cpu.yml up --build -d
```

To use a local recognition model, mount its directory read-only and provide the
container path:

```powershell
docker run --rm -p 8000:8000 `
  -v D:\models\receipt-rec:/models/receipt-rec:ro `
  -e OCR_RECOGNITION_MODEL_DIR=/models/receipt-rec `
  expense-receipt-ocr
```

The service has no public authentication layer because it is intended to be
reachable only by the backend on the internal container network.
