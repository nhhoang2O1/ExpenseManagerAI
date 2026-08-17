from __future__ import annotations

import os
import random
import re
import shutil
import tempfile
import unicodedata
import zipfile
from collections import Counter, defaultdict
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent
DATASET_DIR = BASE_DIR / "dataset"
ZIP_PATH = BASE_DIR / "vietnamese-receipts-mc-ocr-2021.zip"
TRAIN_LABEL = DATASET_DIR / "clean_train.txt"
VAL_LABEL = DATASET_DIR / "clean_val.txt"
TEST_IMAGES_DIR = DATASET_DIR / "test_images"
TEST_LABEL = DATASET_DIR / "clean_test.txt"
SUMMARY_FILE = DATASET_DIR / "test_set_summary.txt"

TEST_SIZE = 250
RANDOM_SEED = 42
MAX_TEXT_LENGTH = 64

# True: ngoài việc không trùng filename, test còn không lấy crop khác
# thuộc cùng một hóa đơn với train/val.
STRICT_RECEIPT_DISJOINT = True

# Ưu tiên official test nếu dataset có; sau đó val còn dư; cuối cùng train còn dư.
SOURCE_PRIORITY = ("test", "val", "valid", "validation", "train", "other")
IMAGE_EXTS = {".jpg", ".jpeg", ".png", ".bmp", ".webp"}


def normalize_label(text: str) -> str:
    return unicodedata.normalize("NFC", text.strip())


def basename(path: str) -> str:
    return os.path.basename(path.replace("\\", "/").strip())


def receipt_id(filename: str) -> str:
    """mcocr_public_xxx_0.jpg -> mcocr_public_xxx"""
    stem = Path(filename).stem
    m = re.match(r"^(.*)_\d+$", stem)
    return m.group(1) if m else stem


def read_clean_file(path: Path):
    if not path.exists():
        raise FileNotFoundError(f"Không tìm thấy: {path}")

    rows = []
    with path.open("r", encoding="utf-8") as f:
        for line_no, raw in enumerate(f, 1):
            parts = raw.rstrip("\r\n").split("\t", 1)
            if len(parts) != 2:
                continue
            image_path, label = parts
            label = normalize_label(label)
            filename = basename(image_path)
            if filename and label:
                rows.append({
                    "filename": filename,
                    "receipt_id": receipt_id(filename),
                    "label": label,
                    "line_no": line_no,
                })
    return rows


def annotation_role(path: Path) -> str:
    name = path.name.lower()
    if "test" in name:
        return "test"
    if "validation" in name:
        return "validation"
    if "valid" in name:
        return "valid"
    if "val" in name:
        return "val"
    if "train" in name:
        return "train"
    return "other"


def role_rank(role: str) -> int:
    try:
        return SOURCE_PRIORITY.index(role)
    except ValueError:
        return len(SOURCE_PRIORITY)


def parse_annotation(path: Path):
    samples = []
    try:
        with path.open("r", encoding="utf-8-sig", errors="replace") as f:
            for line_no, raw in enumerate(f, 1):
                line = raw.rstrip("\r\n")
                if "\t" not in line:
                    continue
                image_path, label = line.split("\t", 1)
                image_path = image_path.replace("\\", "/").strip()
                label = normalize_label(label)
                filename = basename(image_path)
                if not filename or not label:
                    continue
                if Path(filename).suffix.lower() not in IMAGE_EXTS:
                    continue
                samples.append({
                    "annotation": path.name,
                    "role": annotation_role(path),
                    "line_no": line_no,
                    "original_path": image_path,
                    "filename": filename,
                    "receipt_id": receipt_id(filename),
                    "label": label,
                })
    except Exception as exc:
        print(f"⚠️ Bỏ qua annotation không đọc được {path.name}: {exc}")
    return samples


def build_image_index(root: Path):
    index = defaultdict(list)
    count = 0
    for p in root.rglob("*"):
        if p.is_file() and p.suffix.lower() in IMAGE_EXTS:
            index[p.name].append(p)
            count += 1
    print(f"🔎 Tìm thấy {count} ảnh trong dataset gốc.")
    return index


def find_image(root: Path, sample, image_index):
    p = root / sample["original_path"].lstrip("./")
    if p.is_file():
        return p
    matches = image_index.get(sample["filename"], [])
    return matches[0] if matches else None


def choose_by_priority(candidates, size, seed):
    rng = random.Random(seed)
    grouped = defaultdict(list)
    for s in candidates:
        grouped[s["role"]].append(s)

    selected = []
    for role in sorted(grouped, key=role_rank):
        group = grouped[role][:]
        rng.shuffle(group)
        need = size - len(selected)
        if need <= 0:
            break
        selected.extend(group[:need])
    return selected


def main():
    print("=" * 68)
    print("TẠO TEST SET ĐỘC LẬP - MC-OCR 2021")
    print("=" * 68)

    if not ZIP_PATH.exists():
        raise FileNotFoundError(
            f"Không tìm thấy {ZIP_PATH.name}.\n"
            f"Hãy đặt file ZIP tại: {ZIP_PATH}"
        )

    train = read_clean_file(TRAIN_LABEL)
    val = read_clean_file(VAL_LABEL)
    used_files = {x["filename"] for x in train + val}
    used_receipts = {x["receipt_id"] for x in train + val}

    print(f"Train hiện tại      : {len(train)}")
    print(f"Validation hiện tại : {len(val)}")
    print(f"Test mục tiêu       : {TEST_SIZE}")
    print(f"max_text_length     : {MAX_TEXT_LENGTH}")
    print(f"Receipt-level split : {'BẬT' if STRICT_RECEIPT_DISJOINT else 'TẮT'}")

    with tempfile.TemporaryDirectory(prefix="mcocr_test_", dir=BASE_DIR) as tmp:
        root = Path(tmp)
        print("\n📦 Đang giải nén dataset gốc...")
        with zipfile.ZipFile(ZIP_PATH, "r") as zf:
            zf.extractall(root)

        txt_files = sorted(root.rglob("*.txt"))
        if not txt_files:
            raise RuntimeError("Không tìm thấy annotation .txt trong ZIP.")

        all_samples = []
        print(f"📄 Tìm thấy {len(txt_files)} file .txt")
        for txt in txt_files:
            rows = parse_annotation(txt)
            if rows:
                print(f"   {txt.name}: {len(rows)} dòng ({annotation_role(txt)})")
                all_samples.extend(rows)

        if not all_samples:
            raise RuntimeError("Không đọc được annotation image<TAB>label nào.")

        # Unique theo filename; nếu trùng thì giữ nguồn có priority cao hơn.
        all_samples.sort(key=lambda s: (role_rank(s["role"]), s["annotation"], s["line_no"]))
        unique = {}
        for s in all_samples:
            unique.setdefault(s["filename"], s)
        unique_samples = list(unique.values())

        stats = Counter()
        candidates = []
        for s in unique_samples:
            if s["filename"] in used_files:
                stats["exact_overlap"] += 1
                continue
            if STRICT_RECEIPT_DISJOINT and s["receipt_id"] in used_receipts:
                stats["same_receipt"] += 1
                continue
            if len(s["label"]) > MAX_TEXT_LENGTH:
                stats["too_long"] += 1
                continue
            candidates.append(s)

        print("\n--- Candidate sau khi lọc ---")
        print(f"Unique annotation      : {len(unique_samples)}")
        print(f"Loại exact overlap     : {stats['exact_overlap']}")
        print(f"Loại cùng receipt      : {stats['same_receipt']}")
        print(f"Loại label > {MAX_TEXT_LENGTH:<3}    : {stats['too_long']}")
        print(f"Candidate hợp lệ       : {len(candidates)}")

        if not candidates:
            raise RuntimeError("Không còn candidate hợp lệ để tạo test set.")

        image_index = build_image_index(root)

        if TEST_IMAGES_DIR.exists():
            shutil.rmtree(TEST_IMAGES_DIR)
        TEST_IMAGES_DIR.mkdir(parents=True, exist_ok=True)

        ordered = choose_by_priority(candidates, len(candidates), RANDOM_SEED)
        final = []
        missing = 0

        for s in ordered:
            if len(final) >= TEST_SIZE:
                break
            src = find_image(root, s, image_index)
            if src is None:
                missing += 1
                continue
            dest = TEST_IMAGES_DIR / s["filename"]
            shutil.copy2(src, dest)
            final.append(s)

        with TEST_LABEL.open("w", encoding="utf-8", newline="\n") as f:
            for s in final:
                f.write(f"test_images/{s['filename']}\t{s['label']}\n")

        final_files = {s["filename"] for s in final}
        final_receipts = {s["receipt_id"] for s in final}
        train_files = {x["filename"] for x in train}
        val_files = {x["filename"] for x in val}

        overlap_train = final_files & train_files
        overlap_val = final_files & val_files
        overlap_receipt = final_receipts & used_receipts
        role_counts = Counter(s["role"] for s in final)
        lengths = [len(s["label"]) for s in final]

        summary = [
            "MC-OCR 2021 - TEST SET SUMMARY",
            "=" * 45,
            f"Random seed: {RANDOM_SEED}",
            f"Requested test size: {TEST_SIZE}",
            f"Final test size: {len(final)}",
            f"max_text_length: {MAX_TEXT_LENGTH}",
            f"Strict receipt disjoint: {STRICT_RECEIPT_DISJOINT}",
            "",
            f"Train samples: {len(train)}",
            f"Validation samples: {len(val)}",
            f"Train/Test exact filename overlap: {len(overlap_train)}",
            f"Val/Test exact filename overlap: {len(overlap_val)}",
            f"Receipt-level overlap with Train/Val: {len(overlap_receipt)}",
            f"Missing images while selecting: {missing}",
            "",
            "Test source counts:",
        ]
        for role, count in sorted(role_counts.items(), key=lambda x: role_rank(x[0])):
            summary.append(f"  - {role}: {count}")

        if lengths:
            summary += [
                "",
                f"Min label length: {min(lengths)}",
                f"Max label length: {max(lengths)}",
                f"Average label length: {sum(lengths)/len(lengths):.2f}",
            ]

        SUMMARY_FILE.write_text("\n".join(summary) + "\n", encoding="utf-8")

        print("\n" + "=" * 68)
        print("KẾT QUẢ")
        print("=" * 68)
        print(f"✅ Test samples             : {len(final)}")
        print(f"✅ Train/Test exact overlap : {len(overlap_train)}")
        print(f"✅ Val/Test exact overlap   : {len(overlap_val)}")
        print(f"✅ Receipt overlap          : {len(overlap_receipt)}")
        print(f"✅ clean_test.txt           : {TEST_LABEL}")
        print(f"✅ test_images              : {TEST_IMAGES_DIR}")
        print(f"✅ summary                  : {SUMMARY_FILE}")

        if len(final) < TEST_SIZE:
            print(f"⚠️ Chỉ tạo được {len(final)}/{TEST_SIZE} mẫu hợp lệ.")

        if overlap_train or overlap_val or (STRICT_RECEIPT_DISJOINT and overlap_receipt):
            print("❌ Test set còn leakage. Không nên dùng để báo cáo.")
        else:
            print("✅ Test set độc lập với train/validation.")


if __name__ == "__main__":
    main()