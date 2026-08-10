#!/bin/sh
set -eu

# PaddleOCR, PaddleX, Hugging Face, and ModelScope write model caches below the
# service account's home. Prepare both cache roots before dropping privileges.
mkdir -p /home/ocr/.paddlex /home/ocr/.cache
chown -R ocr:ocr /home/ocr/.paddlex /home/ocr/.cache

if [ "${OCR_DEVICE:-gpu}" = "gpu" ]; then
    runuser -u ocr -- python -c 'import paddle, sys; count = paddle.device.cuda.device_count(); print(f"ocr_gpu_check compiled_with_cuda={paddle.is_compiled_with_cuda()} device_count={count}"); sys.exit(0 if paddle.is_compiled_with_cuda() and count > 0 else 1)'
fi

exec runuser -u ocr -- "$@"
