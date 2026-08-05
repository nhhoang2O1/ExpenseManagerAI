#!/bin/sh
set -eu

# Named Docker volumes are created as root. PaddleX needs to write model and
# temporary files here, so fix only this mounted cache before dropping privileges.
mkdir -p /home/ocr/.paddlex
chown -R ocr:ocr /home/ocr/.paddlex

if [ "${OCR_DEVICE:-gpu}" = "gpu" ]; then
    python -c 'import paddle, sys; count = paddle.device.cuda.device_count(); print(f"ocr_gpu_check compiled_with_cuda={paddle.is_compiled_with_cuda()} device_count={count}"); sys.exit(0 if paddle.is_compiled_with_cuda() and count > 0 else 1)'
fi

exec runuser -u ocr -- "$@"
