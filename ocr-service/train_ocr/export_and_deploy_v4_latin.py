from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path

import yaml


# ============================================================
# PATHS
# ============================================================

BASE_DIR = Path(__file__).resolve().parent          # train_ocr/
SERVICE_DIR = BASE_DIR.parent                      # ocr-service/

OUTPUT_MODEL_NAME = "my_vietnamese_rec"
DEPLOY_MODEL_NAME = "my_receipt_rec_model_v4_latin"

POSSIBLE_PADDLE_DIRS = [
    SERVICE_DIR / "PaddleOCR",
    BASE_DIR / "PaddleOCR",
]

PADDLE_DIR = next(
    (p for p in POSSIBLE_PADDLE_DIRS if p.exists()),
    BASE_DIR / "PaddleOCR",
)

TRAIN_OUTPUT_DIR = PADDLE_DIR / "output" / OUTPUT_MODEL_NAME
CHECKPOINT_PREFIX = TRAIN_OUTPUT_DIR / "best_accuracy"

DEPLOY_MODELS_DIR = SERVICE_DIR / "models" / DEPLOY_MODEL_NAME

# Ưu tiên config được lưu cùng lần train.
CONFIG_CANDIDATES = [
    TRAIN_OUTPUT_DIR / "config.yml",
    TRAIN_OUTPUT_DIR / "config.yaml",
    PADDLE_DIR / "my_local_rec_config.yml",
]

LATIN_DICT_PATH = PADDLE_DIR / "ppocr" / "utils" / "dict" / "latin_dict.txt"

# Nếu chạy lại script và thư mục output đã tồn tại:
# True  = xóa thư mục inference cũ rồi export lại.
# False = dừng để tránh ghi đè.
OVERWRITE_EXISTING = True


# ============================================================
# HELPERS
# ============================================================

def configure_console() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8")
        except Exception:
            pass


def normalize_path(path: Path) -> str:
    """PaddleOCR thường ổn định hơn với dấu / trên Windows."""
    return str(path.resolve()).replace("\\", "/")


def find_config() -> Path:
    for config_path in CONFIG_CANDIDATES:
        if config_path.is_file():
            return config_path

    searched = "\n".join(f"  - {p}" for p in CONFIG_CANDIDATES)
    raise FileNotFoundError(
        "Không tìm thấy config của model V4 cũ.\n"
        f"Đã kiểm tra:\n{searched}"
    )


def load_config(config_path: Path) -> dict:
    with config_path.open("r", encoding="utf-8") as f:
        config = yaml.safe_load(f)

    if not isinstance(config, dict):
        raise ValueError(f"Config YAML không hợp lệ: {config_path}")

    return config


def validate_export_inputs() -> tuple[Path, dict]:
    print("=" * 72)
    print("EXPORT PP-OCRv4 FINE-TUNED CŨ - LATIN DICTIONARY")
    print("=" * 72)

    if not PADDLE_DIR.is_dir():
        raise FileNotFoundError(
            f"Không tìm thấy thư mục PaddleOCR: {PADDLE_DIR}"
        )

    export_py = PADDLE_DIR / "tools" / "export_model.py"
    if not export_py.is_file():
        raise FileNotFoundError(
            f"Không tìm thấy export_model.py: {export_py}"
        )

    checkpoint_file = Path(str(CHECKPOINT_PREFIX) + ".pdparams")
    if not checkpoint_file.is_file():
        raise FileNotFoundError(
            "Không tìm thấy checkpoint tốt nhất:\n"
            f"  {checkpoint_file}\n\n"
            "Hãy kiểm tra trong PaddleOCR/output/my_vietnamese_rec/ "
            "có best_accuracy.pdparams hay không."
        )

    config_path = find_config()
    config = load_config(config_path)

    global_cfg = config.get("Global", {})
    architecture = config.get("Architecture", {})

    dict_from_config = global_cfg.get("character_dict_path")
    max_text_length = global_cfg.get("max_text_length")
    model_name = global_cfg.get("model_name")
    algorithm = architecture.get("algorithm")

    print(f"[PADDLE]     {PADDLE_DIR}")
    print(f"[CONFIG]     {config_path}")
    print(f"[CHECKPOINT] {checkpoint_file}")
    print(f"[MODEL]      {model_name}")
    print(f"[ALGORITHM]  {algorithm}")
    print(f"[MAX LENGTH] {max_text_length}")
    print(f"[DICT CFG]   {dict_from_config}")
    print(f"[DICT USED]  {LATIN_DICT_PATH}")
    print(f"[OUTPUT]     {DEPLOY_MODELS_DIR}")

    # Kiểm tra model đúng là bản cũ cần export.
    if model_name and model_name != "PP-OCRv4_mobile_rec":
        raise ValueError(
            f"Config không phải PP-OCRv4_mobile_rec: {model_name}"
        )

    if algorithm and algorithm != "SVTR_LCNet":
        raise ValueError(
            f"Architecture không phải SVTR_LCNet: {algorithm}"
        )

    # Config user gửi của model cũ có max_text_length = 25.
    if max_text_length != 25:
        raise ValueError(
            "Config này không giống cấu hình V4 cũ trước khi đổi dictionary.\n"
            f"Global.max_text_length hiện tại = {max_text_length}, "
            "trong khi bản cũ mong đợi = 25."
        )

    if not LATIN_DICT_PATH.is_file():
        raise FileNotFoundError(
            f"Không tìm thấy latin_dict.txt: {LATIN_DICT_PATH}"
        )

    # Không phụ thuộc tuyệt đối vào path cũ trong YAML.
    # Khi export, ta override bằng LATIN_DICT_PATH hiện tại.
    if dict_from_config:
        cfg_name = Path(str(dict_from_config)).name.lower()
        if cfg_name != "latin_dict.txt":
            raise ValueError(
                "Config đang trỏ tới dictionary khác latin_dict.txt:\n"
                f"  {dict_from_config}\n"
                "Dừng export để tránh sai mapping output."
            )

    print("\n✅ Kiểm tra đầu vào hợp lệ.")
    return config_path, config


def prepare_output_dir() -> None:
    if DEPLOY_MODELS_DIR.exists():
        has_files = any(DEPLOY_MODELS_DIR.iterdir())

        if has_files and not OVERWRITE_EXISTING:
            raise FileExistsError(
                "Thư mục output đã tồn tại và có dữ liệu:\n"
                f"  {DEPLOY_MODELS_DIR}\n"
                "Đổi DEPLOY_MODEL_NAME hoặc đặt OVERWRITE_EXISTING = True."
            )

        if OVERWRITE_EXISTING:
            print(
                f"[CLEAN] Xóa inference model cũ tại: "
                f"{DEPLOY_MODELS_DIR}"
            )
            shutil.rmtree(DEPLOY_MODELS_DIR)

    DEPLOY_MODELS_DIR.mkdir(parents=True, exist_ok=True)


def export_model(config_path: Path) -> None:
    prepare_output_dir()

    export_py = PADDLE_DIR / "tools" / "export_model.py"

    checkpoint_posix = normalize_path(CHECKPOINT_PREFIX)
    deploy_posix = normalize_path(DEPLOY_MODELS_DIR)
    dict_posix = normalize_path(LATIN_DICT_PATH)

    # Fix cho Paddle 2.6.x: không dùng PIR export.
    env = os.environ.copy()
    env["FLAGS_enable_pir_api"] = "0"
    env["KMP_DUPLICATE_LIB_OK"] = "TRUE"
    env["FLAGS_use_mkldnn"] = "0"

    cmd = [
        sys.executable,
        str(export_py),
        "-c",
        str(config_path),
        "-o",
        f"Global.pretrained_model={checkpoint_posix}",
        f"Global.save_inference_dir={deploy_posix}",
        f"Global.character_dict_path={dict_posix}",
        "Global.export_with_pir=False",
        "Global.use_gpu=False",
        "Global.distributed=False",
    ]

    print("\n[EXPORT] Bắt đầu export...")
    print(f"[PYTHON] {sys.executable}")

    subprocess.run(
        cmd,
        cwd=str(PADDLE_DIR),
        env=env,
        check=True,
    )

    print("\n✅ Export model thành công.")


def copy_deployment_metadata(config_path: Path) -> None:
    """
    Copy dictionary + training config vào thư mục model để tránh nhầm
    với model custom Vietnamese dictionary về sau.
    """
    copied_dict = DEPLOY_MODELS_DIR / "latin_dict.txt"
    copied_config = DEPLOY_MODELS_DIR / "training_config_v4_latin.yml"

    shutil.copy2(LATIN_DICT_PATH, copied_dict)
    shutil.copy2(config_path, copied_config)

    print(f"[COPY] Dictionary: {copied_dict}")
    print(f"[COPY] Config    : {copied_config}")


def verify_export() -> None:
    pdiparams = DEPLOY_MODELS_DIR / "inference.pdiparams"
    pdmodel = DEPLOY_MODELS_DIR / "inference.pdmodel"
    json_model = DEPLOY_MODELS_DIR / "inference.json"
    yml_file = DEPLOY_MODELS_DIR / "inference.yml"

    if not pdiparams.is_file():
        raise RuntimeError(
            f"Export xong nhưng thiếu inference.pdiparams: {pdiparams}"
        )

    if not pdmodel.is_file() and not json_model.is_file():
        raise RuntimeError(
            "Export xong nhưng không thấy inference.pdmodel "
            "hoặc inference.json."
        )

    print("\n" + "=" * 72)
    print("KẾT QUẢ EXPORT")
    print("=" * 72)
    print(f"📁 {DEPLOY_MODELS_DIR}")

    for path in sorted(DEPLOY_MODELS_DIR.iterdir()):
        if path.is_file():
            size_mb = path.stat().st_size / (1024 * 1024)
            print(f"   - {path.name:<32} {size_mb:>8.2f} MB")

    if yml_file.is_file():
        print("\n✅ Có inference.yml.")
    else:
        print(
            "\nℹ️ Không thấy inference.yml. "
            "Điều này phụ thuộc phiên bản PaddleOCR export."
        )

    print("\n🚫 .env KHÔNG được thay đổi.")
    print(
        "✅ Model hiện tại của dự án vẫn giữ nguyên cho đến khi "
        "bạn benchmark và chủ động đổi .env."
    )


def main() -> None:
    configure_console()

    config_path, _ = validate_export_inputs()
    export_model(config_path)
    copy_deployment_metadata(config_path)
    verify_export()

    print("\n[COMPLETE] Hoàn tất export PP-OCRv4 fine-tuned cũ.")
    print(
        "[NEXT] Dùng thư mục này để benchmark:\n"
        f"       {DEPLOY_MODELS_DIR}"
    )


if __name__ == "__main__":
    main()