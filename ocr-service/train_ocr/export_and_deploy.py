"""
Script Xuất Mô Hình (Export Inference Model) và tự động kết nối vào OCR Service của dự án.
"""
import os
import subprocess
import sys

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
SERVICE_DIR = os.path.dirname(BASE_DIR)

# Tự động nhận diện thư mục PaddleOCR
possible_paddle_dirs = [
    os.path.join(SERVICE_DIR, "PaddleOCR"),
    os.path.join(BASE_DIR, "PaddleOCR"),
]

PADDLE_DIR = next((d for d in possible_paddle_dirs if os.path.exists(d)), os.path.join(BASE_DIR, "PaddleOCR"))
CONFIG_YML = os.path.join(PADDLE_DIR, "my_local_rec_config.yml")
CHECKPOINT_DIR = os.path.join(PADDLE_DIR, "output", "my_vietnamese_rec", "best_accuracy").replace("\\", "/")

DEPLOY_MODELS_DIR = os.path.join(SERVICE_DIR, "models", "my_receipt_rec_model")


def export_model():
    os.makedirs(DEPLOY_MODELS_DIR, exist_ok=True)
    export_py = os.path.join(PADDLE_DIR, "tools", "export_model.py")

    cmd = [
        sys.executable, export_py,
        "-c", CONFIG_YML,
        "-o", f"Global.pretrained_model={CHECKPOINT_DIR}",
        f"Global.save_inference_dir={DEPLOY_MODELS_DIR.replace('\\', '/')}",
        "Global.export_with_pir=False"
    ]

    print(f"[EXPORT] Dang export inference model vao thu muc: {DEPLOY_MODELS_DIR}")
    subprocess.run(cmd, cwd=PADDLE_DIR, check=True)
    print("[SUCCESS] Export mo hinh thanh cong!")


def update_env_file():
    env_file = os.path.join(SERVICE_DIR, ".env")
    relative_model_dir = "./models/my_receipt_rec_model"
    line_to_set = f"OCR_RECOGNITION_MODEL_DIR={relative_model_dir}\n"

    lines = []
    updated = False
    if os.path.exists(env_file):
        with open(env_file, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        for i, line in enumerate(lines):
            if line.startswith("OCR_RECOGNITION_MODEL_DIR="):
                lines[i] = line_to_set
                updated = True
                break

    if not updated:
        lines.append(line_to_set)

    with open(env_file, 'w', encoding='utf-8') as f:
        f.writelines(lines)

    print(f"[CONFIG] Da cau hinh file .env: OCR_RECOGNITION_MODEL_DIR={relative_model_dir}")


def main():
    export_model()
    update_env_file()
    print("\n[COMPLETE] HOAN THANH TICH HOP MO HINH VAO DU AN!")
    print(f"[PATH] Mo hinh xuat tai: {DEPLOY_MODELS_DIR}")
    print("[READY] Dich vu OCR service san sang su dung model moi khi khoi chay!")


if __name__ == "__main__":
    main()
