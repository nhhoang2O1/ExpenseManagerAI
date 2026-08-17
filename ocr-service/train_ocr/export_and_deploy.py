import os
import subprocess
import sys
import yaml

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
SERVICE_DIR = os.path.dirname(BASE_DIR)

OUTPUT_MODEL_NAME = "my_vietnamese_rec_vi_dict"

possible_paddle_dirs = [
    os.path.join(SERVICE_DIR, "PaddleOCR"),
    os.path.join(BASE_DIR, "PaddleOCR"),
]

PADDLE_DIR = next(
    (d for d in possible_paddle_dirs if os.path.exists(d)),
    os.path.join(BASE_DIR, "PaddleOCR"),
)

CONFIG_YML = os.path.join(PADDLE_DIR, "my_local_rec_config.yml")
CHECKPOINT_DIR = os.path.join(
    PADDLE_DIR,
    "output",
    OUTPUT_MODEL_NAME,
    "best_accuracy",
).replace("\\", "/")

DEPLOY_MODELS_DIR = os.path.join(
    SERVICE_DIR,
    "models",
    "my_receipt_rec_model",
)


def validate_export_inputs():
    if not os.path.exists(CONFIG_YML):
        raise FileNotFoundError(
            f"Không tìm thấy config: {CONFIG_YML}. "
            "Hãy train bằng setup_and_train.py trước."
        )

    pdparams = CHECKPOINT_DIR + ".pdparams"
    if not os.path.exists(pdparams):
        raise FileNotFoundError(
            f"Không tìm thấy best checkpoint: {pdparams}"
        )

    with open(CONFIG_YML, "r", encoding="utf-8") as f:
        config = yaml.safe_load(f)

    dict_path = config.get("Global", {}).get("character_dict_path")
    max_text_length = config.get("Global", {}).get("max_text_length")

    if not dict_path or not os.path.exists(dict_path):
        raise FileNotFoundError(
            f"Dictionary trong config không tồn tại: {dict_path}"
        )

    print(f"[CONFIG] Dictionary: {dict_path}")
    print(f"[CONFIG] max_text_length: {max_text_length}")


def export_model():
    validate_export_inputs()

    os.makedirs(DEPLOY_MODELS_DIR, exist_ok=True)
    export_py = os.path.join(PADDLE_DIR, "tools", "export_model.py")

    deploy_dir_posix = DEPLOY_MODELS_DIR.replace("\\", "/")

    cmd = [
        sys.executable,
        export_py,
        "-c",
        CONFIG_YML,
        "-o",
        f"Global.pretrained_model={CHECKPOINT_DIR}",
        f"Global.save_inference_dir={deploy_dir_posix}",
        "Global.export_with_pir=False",
    ]

    print(
        f"[EXPORT] Đang export inference model vào: "
        f"{DEPLOY_MODELS_DIR}"
    )
    subprocess.run(cmd, cwd=PADDLE_DIR, check=True)
    print("[SUCCESS] Export model thành công!")


def update_env_file():
    env_file = os.path.join(SERVICE_DIR, ".env")
    relative_model_dir = "./models/my_receipt_rec_model"
    line_to_set = (
        f"OCR_RECOGNITION_MODEL_DIR={relative_model_dir}\n"
    )

    lines = []
    updated = False

    if os.path.exists(env_file):
        with open(env_file, "r", encoding="utf-8") as f:
            lines = f.readlines()

        for index, line in enumerate(lines):
            if line.startswith("OCR_RECOGNITION_MODEL_DIR="):
                lines[index] = line_to_set
                updated = True
                break

    if not updated:
        lines.append(line_to_set)

    with open(env_file, "w", encoding="utf-8", newline="\n") as f:
        f.writelines(lines)

    print(
        "[CONFIG] Đã cập nhật .env: "
        f"OCR_RECOGNITION_MODEL_DIR={relative_model_dir}"
    )


def main():
    export_model()
    update_env_file()
    print("\n[COMPLETE] HOÀN THÀNH TÍCH HỢP MODEL VÀO DỰ ÁN!")
    print(f"[PATH] Model export tại: {DEPLOY_MODELS_DIR}")
    print(
        "[READY] OCR service sẽ dùng model mới khi khởi động lại."
    )


if __name__ == "__main__":
    main()