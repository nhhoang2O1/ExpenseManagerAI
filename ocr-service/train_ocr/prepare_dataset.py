"""
Script tự động Tải Dataset MC-OCR 2021 từ Kaggle, Giải nén và Lọc dữ liệu sạch cho Train & Val (Test).
"""
import glob
import os
import shutil
import subprocess
import sys
import zipfile
import cv2

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATASET_DIR = os.path.join(BASE_DIR, "dataset")
IMAGES_DIR = os.path.join(DATASET_DIR, "images")
TEMP_DIR = os.path.join(BASE_DIR, "temp_mc_ocr")
ZIP_PATH = os.path.join(BASE_DIR, "vietnamese-receipts-mc-ocr-2021.zip")

KAGGLE_DATASET_ID = "domixi1989/vietnamese-receipts-mc-ocr-2021"
DEFAULT_KAGGLE_TOKEN = "KGAT_5e0a5b549fe3411e8569c5bd59110b4e"


def prepare_directories():
    os.makedirs(IMAGES_DIR, exist_ok=True)
    os.makedirs(TEMP_DIR, exist_ok=True)
    print(f"📁 Thư mục dataset local: {DATASET_DIR}")


def download_dataset_from_kaggle():
    if os.path.exists(ZIP_PATH):
        print(f"📦 Đã tìm thấy file zip có sẵn: {ZIP_PATH}")
        return

    print("📥 Đang tải dataset MC-OCR 2021 từ Kaggle...")
    os.environ['KAGGLE_API_TOKEN'] = os.environ.get('KAGGLE_API_TOKEN', DEFAULT_KAGGLE_TOKEN)

    # Đảm bảo đã cài thư viện kaggle
    try:
        import kaggle  # noqa: F401
    except ImportError:
        print("📦 Đang cài đặt thư viện kaggle...")
        subprocess.run([sys.executable, "-m", "pip", "install", "-q", "kaggle"], check=True)

    try:
        subprocess.run(
            [sys.executable, "-m", "kaggle", "datasets", "download", "-d", KAGGLE_DATASET_ID, "-p", BASE_DIR],
            check=True
        )
        print("✅ Tải dataset thành công!")
    except Exception as e:
        print(f"⚠️ Lỗi khi tải bằng Kaggle API: {e}")
        print("ℹ️ Bạn có thể tải thủ công file zip về và đặt vào thư mục train_ocr.")


def extract_dataset():
    zip_files = glob.glob(os.path.join(BASE_DIR, "*.zip")) + glob.glob(os.path.join(os.path.dirname(BASE_DIR), "*.zip"))
    if not zip_files:
        print("❌ Không tìm thấy file dataset zip để giải nén!")
        return False

    target_zip = ZIP_PATH if os.path.exists(ZIP_PATH) else zip_files[0]
    print(f"📦 Đang giải nén {target_zip} vào {TEMP_DIR}...")
    with zipfile.ZipFile(target_zip, 'r') as zip_ref:
        zip_ref.extractall(TEMP_DIR)
    print("✅ Giải nén thành công!")
    return True


def collect_clean_dataset(input_txt: str, output_txt: str, max_count: int = 2000):
    if not os.path.exists(input_txt):
        print(f"⚠️ Không tìm thấy file nhãn: {input_txt}")
        return 0

    with open(input_txt, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    clean_lines = []
    print(f"🔍 Đang lọc dữ liệu từ {input_txt} ({len(lines)} mẫu gốc)...")

    for line in lines:
        parts = line.strip().split('\t')
        if len(parts) < 2:
            continue
        img_name, label = parts[0].strip(), parts[1].strip()
        if not label:
            continue

        base_name = os.path.basename(img_name)
        
        # Tìm đường dẫn file ảnh thực tế
        found_src = None
        for search_path in [
            os.path.join(TEMP_DIR, base_name),
            os.path.join(BASE_DIR, base_name),
        ] + glob.glob(os.path.join(TEMP_DIR, "**", base_name), recursive=True):
            if os.path.isfile(search_path):
                found_src = search_path
                break

        if not found_src:
            continue

        dest_img_path = os.path.join(IMAGES_DIR, base_name)
        if not os.path.exists(dest_img_path):
            try:
                img = cv2.imread(found_src)
                if img is None or img.shape[0] < 8 or img.shape[1] < 12:
                    continue
                shutil.copy2(found_src, dest_img_path)
            except Exception:
                continue

        clean_lines.append(f"images/{base_name}\t{label}\n")
        if len(clean_lines) >= max_count:
            break

    with open(output_txt, 'w', encoding='utf-8') as f:
        f.writelines(clean_lines)

    print(f"✅ ĐÃ LỌC THÀNH CÔNG: {len(clean_lines)} mẫu sạch -> {output_txt}")
    return len(clean_lines)


def main():
    prepare_directories()
    download_dataset_from_kaggle()
    
    if extract_dataset():
        train_files = glob.glob(os.path.join(TEMP_DIR, "**", "*train*.txt"), recursive=True)
        val_files = glob.glob(os.path.join(TEMP_DIR, "**", "*val*.txt"), recursive=True)

        input_train = train_files[0] if train_files else os.path.join(TEMP_DIR, "text_recognition_train_data.txt")
        input_val = val_files[0] if val_files else os.path.join(TEMP_DIR, "text_recognition_val_data.txt")

        out_train = os.path.join(DATASET_DIR, "clean_train.txt")
        out_val = os.path.join(DATASET_DIR, "clean_val.txt")

        collect_clean_dataset(input_train, out_train, max_count=2000)
        collect_clean_dataset(input_val, out_val, max_count=400)

        # Dọn dẹp thư mục tạm
        if os.path.exists(TEMP_DIR):
            try:
                shutil.rmtree(TEMP_DIR)
            except Exception:
                pass

        print("\n🎉 HOÀN THÀNH CHUẨN BỊ DATASET!")
        print(f"📊 Tập Train: {out_train}")
        print(f"📊 Tập Test/Val: {out_val}")
        print("👉 Bây giờ bạn chỉ cần chạy: python setup_and_train.py")


if __name__ == "__main__":
    main()
