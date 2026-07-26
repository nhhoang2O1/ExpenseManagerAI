#!/bin/sh
set -eu

# Named Docker volumes are created as root. PaddleX needs to write model and
# temporary files here, so fix only this mounted cache before dropping privileges.
mkdir -p /home/ocr/.paddlex
chown -R ocr:ocr /home/ocr/.paddlex

exec runuser -u ocr -- "$@"
