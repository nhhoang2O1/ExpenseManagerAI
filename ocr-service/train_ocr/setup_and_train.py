"""
Script tự động Clone PaddleOCR (hoặc dùng thư mục có sẵn), tải Pretrained weights, cấu hình và tiến hành Train Local trên Windows.
Tự động phát hiện GPU/CPU để huấn luyện tối ưu nhất.
"""
import os
import subprocess
import sys
import urllib.request
import tarfile
import yaml

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
SERVICE_DIR = os.path.dirname(BASE_DIR)

# Tự động nhận diện PaddleOCR dù nằm ở ocr-service/PaddleOCR hay ocr-service/train_ocr/PaddleOCR
possible_paddle_dirs = [
    os.path.join(SERVICE_DIR, "PaddleOCR"),
    os.path.join(BASE_DIR, "PaddleOCR"),
]

PADDLE_DIR = next((d for d in possible_paddle_dirs if os.path.exists(d)), os.path.join(BASE_DIR, "PaddleOCR"))
PRETRAIN_DIR = os.path.join(PADDLE_DIR, "pretrain_models")
WEIGHTS_TAR = os.path.join(PRETRAIN_DIR, "latin_PP-OCRv3_rec_train.tar")
WEIGHTS_URL = "https://paddleocr.bj.bcebos.com/PP-OCRv3/multilingual/latin_PP-OCRv3_rec_train.tar"


def detect_gpu_availability():
    try:
        import paddle
        if paddle.is_compiled_with_cuda():
            device = paddle.get_device()
            if device.startswith("gpu"):
                x = paddle.to_tensor([1.0], place=paddle.CUDAPlace(0))
                _ = x * 2.0
                print(f"⚡ ĐÃ KÍCH HOẠT THÀNH CÔNG GPU & CUDNN: {device.upper()} -> Huấn luyện bằng GPU!")
                return True
    except Exception as exc:
        print(f"ℹ️ cuDNN chưa sẵn sàng ({exc}). Chuyển sang CPU Mode.")
        pass
    print("ℹ️ Đang chạy ở chế độ CPU Mode (Huấn luyện mượt mà trên CPU).")
    return False


def check_and_clone_paddleocr():
    global PADDLE_DIR, PRETRAIN_DIR, WEIGHTS_TAR
    for d in possible_paddle_dirs:
        if os.path.exists(d):
            PADDLE_DIR = d
            PRETRAIN_DIR = os.path.join(PADDLE_DIR, "pretrain_models")
            WEIGHTS_TAR = os.path.join(PRETRAIN_DIR, "latin_PP-OCRv3_rec_train.tar")
            print(f"✅ Đã nhận diện thư mục PaddleOCR tại: {PADDLE_DIR}")
            return

    print("🚀 Đang clone PaddleOCR từ GitHub...")
    subprocess.run(["git", "clone", "https://github.com/PaddlePaddle/PaddleOCR.git", PADDLE_DIR], check=True)
    print("✅ Clone PaddleOCR hoàn tất!")


def download_pretrained_weights():
    os.makedirs(PRETRAIN_DIR, exist_ok=True)
    extracted_path = os.path.join(PRETRAIN_DIR, "latin_PP-OCRv3_rec_train", "best_accuracy")
    if not os.path.exists(extracted_path):
        print("📥 Đang tải pretrained weights (latin_PP-OCRv3)...")
        urllib.request.urlretrieve(WEIGHTS_URL, WEIGHTS_TAR)
        print("📦 Đang giải nén weights...")
        with tarfile.open(WEIGHTS_TAR, "r:*") as tar:
            tar.extractall(PRETRAIN_DIR)
        print("✅ Giải nén weights thành công!")
    else:
        print(f"✅ Đã tìm thấy pretrained weights tại: {extracted_path}")


def create_local_yaml_config(use_gpu: bool):
    base_yml = os.path.join(PADDLE_DIR, "configs", "rec", "PP-OCRv3", "multi_language", "latin_PP-OCRv3_mobile_rec.yml")
    target_yml = os.path.join(PADDLE_DIR, "my_local_rec_config.yml")
    dict_path = os.path.join(PADDLE_DIR, "ppocr", "utils", "dict", "latin_dict.txt").replace("\\", "/")

    with open(base_yml, 'r', encoding='utf-8') as f:
        config = yaml.safe_load(f)

    config['Global']['use_gpu'] = use_gpu
    config['Global']['character_dict_path'] = dict_path

    # Xóa RecConAug khỏi transforms để đảm bảo ổn định 100%
    if 'Train' in config and 'dataset' in config['Train'] and 'transforms' in config['Train']['dataset']:
        config['Train']['dataset']['transforms'] = [
            t for t in config['Train']['dataset']['transforms'] if 'RecConAug' not in t
        ]

    with open(target_yml, 'w', encoding='utf-8') as f:
        yaml.dump(config, f)

    mode_str = "GPU Mode ⚡" if use_gpu else "CPU Mode"
    print(f"⚙️ Đã khởi tạo file cấu hình Local ({mode_str}): {target_yml}")
    return target_yml


def run_training(config_yml: str, use_gpu: bool, epochs: int = 50):
    dataset_dir = os.path.join(BASE_DIR, "dataset").replace("\\", "/")
    train_label = f"{dataset_dir}/clean_train.txt"
    val_label = f"{dataset_dir}/clean_val.txt"
    pretrained = os.path.join(PRETRAIN_DIR, "latin_PP-OCRv3_rec_train", "best_accuracy").replace("\\", "/")
    output_dir = os.path.join(PADDLE_DIR, "output", "my_vietnamese_rec").replace("\\", "/")
    dict_path = os.path.join(PADDLE_DIR, "ppocr", "utils", "dict", "latin_dict.txt").replace("\\", "/")

    train_py = os.path.join(PADDLE_DIR, "tools", "train.py")

    cmd = [
        sys.executable, train_py,
        "-c", config_yml,
        "-o", f"Global.pretrained_model={pretrained}",
        f"Global.character_dict_path={dict_path}",
        f"Global.use_gpu={use_gpu}",
        f"Global.epoch_num={epochs}",
        f"Global.save_model_dir={output_dir}",
        "Global.eval_batch_step=[0, 100]",
        "Train.loader.batch_size_per_card=32",
        "Eval.loader.batch_size_per_card=32",
        "Train.loader.num_workers=0",
        "Eval.loader.num_workers=0",
        f"Train.dataset.data_dir={dataset_dir}/",
        f"Train.dataset.label_file_list=['{train_label}']",
        f"Eval.dataset.data_dir={dataset_dir}/",
        f"Eval.dataset.label_file_list=['{val_label}']"
    ]

    mode_str = "GPU ⚡" if use_gpu else "CPU"
    print(f"🔥 Bắt đầu quá trình huấn luyện bằng {mode_str} ({epochs} Epochs)...")
    subprocess.run(cmd, cwd=PADDLE_DIR, check=True)


def main():
    use_gpu = detect_gpu_availability()
    check_and_clone_paddleocr()
    download_pretrained_weights()
    config_yml = create_local_yaml_config(use_gpu)
    run_training(config_yml, use_gpu, epochs=50)


if __name__ == "__main__":
    main()
