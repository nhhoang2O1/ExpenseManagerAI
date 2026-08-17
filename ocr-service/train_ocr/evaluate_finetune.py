from __future__ import annotations

import argparse
import csv
import json
import os
import re
import shutil
import statistics
import subprocess
import sys
import time
import unicodedata
from collections import defaultdict
from pathlib import Path

try:
    import yaml
except ImportError as exc:
    raise SystemExit("Thiếu PyYAML. Chạy: pip install pyyaml") from exc


# ---------------------------------------------------------------------
# PATHS
# ---------------------------------------------------------------------

BASE_DIR = Path(__file__).resolve().parent
OCR_SERVICE_DIR = BASE_DIR.parent
PADDLE_DIR = OCR_SERVICE_DIR / "PaddleOCR"

DATASET_DIR = BASE_DIR / "dataset"
TEST_LABEL = DATASET_DIR / "clean_test.txt"

TRAIN_LOG = (
    PADDLE_DIR
    / "output"
    / "my_vietnamese_rec_vi_dict"
    / "train_full.log"
)

MODEL_BEFORE = Path(
    r"D:\ExpenseManagerAI_Main\ocr-service\PaddleOCR"
    r"\pretrain_models\ch_PP-OCRv4_rec_train\student"
)

MODEL_AFTER = Path(
    r"D:\ExpenseManagerAI_Main\ocr-service\models\my_receipt_rec_model"
)

# V4 fine-tune cũ: latin_dict.txt, max_text_length=25
MODEL_OLD = Path(
    r"D:\ExpenseManagerAI_Main\ocr-service\models\my_receipt_rec_model_v4_latin"
)

OLD_TRAIN_CONFIG = MODEL_OLD / "training_config_v4_latin.yml"

RESULT_DIR = BASE_DIR / "evaluation_results"

BASE_CONFIG_CANDIDATES = [
    PADDLE_DIR / "configs" / "rec" / "PP-OCRv4" / "PP-OCRv4_mobile_rec.yml",
    PADDLE_DIR / "configs" / "rec" / "PP-OCRv4" / "ch_PP-OCRv4_rec.yml",
]

AFTER_TRAIN_CONFIG = PADDLE_DIR / "my_local_rec_config.yml"

REC_BATCH_NUM = 8
WARMUP_COUNT = 2


# ---------------------------------------------------------------------
# HELPERS
# ---------------------------------------------------------------------

def normalize_text(text: str) -> str:
    return unicodedata.normalize("NFC", text).strip()


def path_arg(path: Path) -> str:
    return str(path.resolve()).replace("\\", "/")


def require_file(path: Path, label: str) -> None:
    if not path.is_file():
        raise FileNotFoundError(f"Không tìm thấy {label}: {path}")


def require_dir(path: Path, label: str) -> None:
    if not path.is_dir():
        raise FileNotFoundError(f"Không tìm thấy {label}: {path}")


def find_base_config() -> Path:
    for path in BASE_CONFIG_CANDIDATES:
        if path.is_file():
            return path
    raise FileNotFoundError(
        "Không tìm thấy config PP-OCRv4 gốc:\n"
        + "\n".join(f"  - {p}" for p in BASE_CONFIG_CANDIDATES)
    )


def load_yaml(path: Path) -> dict:
    require_file(path, "YAML")
    with path.open("r", encoding="utf-8") as f:
        return yaml.safe_load(f) or {}


def resolve_from_paddle(raw: str | None) -> Path:
    if not raw:
        raise RuntimeError("character_dict_path rỗng trong config.")
    raw = raw.replace("\\", "/")
    p = Path(raw)
    if p.is_absolute():
        return p
    return (PADDLE_DIR / p).resolve()


# ---------------------------------------------------------------------
# TEST SET
# ---------------------------------------------------------------------

def read_test_set() -> list[dict]:
    require_file(TEST_LABEL, "clean_test.txt")
    rows = []

    with TEST_LABEL.open("r", encoding="utf-8") as f:
        for line_no, raw in enumerate(f, start=1):
            parts = raw.rstrip("\r\n").split("\t", 1)
            if len(parts) != 2:
                print(f"⚠️ Bỏ dòng {line_no}: sai format image<TAB>label")
                continue

            rel = parts[0].strip().replace("\\", "/")
            gt = normalize_text(parts[1])
            if not rel or not gt:
                continue

            image = (TEST_LABEL.parent / rel).resolve()
            if not image.is_file():
                raise FileNotFoundError(
                    f"Ảnh test không tồn tại ở dòng {line_no}: {image}"
                )

            rows.append(
                {
                    "relative_image": rel,
                    "image": str(image),
                    "ground_truth": gt,
                }
            )

    if not rows:
        raise RuntimeError("clean_test.txt không có mẫu hợp lệ.")

    print(f"✅ Test set: {len(rows)} mẫu")
    return rows


# ---------------------------------------------------------------------
# METRICS
# ---------------------------------------------------------------------

def levenshtein_distance(a: str, b: str) -> int:
    if a == b:
        return 0
    if not a:
        return len(b)
    if not b:
        return len(a)

    if len(a) < len(b):
        a, b = b, a

    prev = list(range(len(b) + 1))

    for i, ca in enumerate(a, start=1):
        cur = [i]
        for j, cb in enumerate(b, start=1):
            cur.append(
                min(
                    cur[j - 1] + 1,
                    prev[j] + 1,
                    prev[j - 1] + (ca != cb),
                )
            )
        prev = cur

    return prev[-1]


def calculate_metrics(
    test_rows: list[dict],
    predictions: list[dict],
    elapsed_sec: float,
) -> tuple[dict, list[dict]]:
    by_name = defaultdict(list)
    for p in predictions:
        by_name[Path(p["image"]).name].append(p)

    exact_count = 0
    norm_sum = 0.0
    total_edit = 0
    total_gt_chars = 0
    confidences = []
    detail = []

    for row in test_rows:
        name = Path(row["image"]).name
        matches = by_name.get(name, [])
        if len(matches) != 1:
            raise RuntimeError(
                f"Không map được duy nhất prediction cho ảnh: {name}"
            )

        item = matches[0]
        gt = normalize_text(row["ground_truth"])
        pred = normalize_text(item.get("text", ""))
        conf = float(item.get("confidence", 0.0))

        dist = levenshtein_distance(pred, gt)
        denom = max(len(gt), len(pred), 1)
        norm_sim = 1.0 - dist / denom
        exact = int(pred == gt)

        exact_count += exact
        norm_sum += norm_sim
        total_edit += dist
        total_gt_chars += len(gt)
        confidences.append(conf)

        detail.append(
            {
                "image": row["relative_image"],
                "ground_truth": gt,
                "prediction": pred,
                "exact_match": exact,
                "edit_distance": dist,
                "norm_edit_similarity": norm_sim,
                "confidence": conf,
            }
        )

    n = len(test_rows)
    metrics = {
        "samples": n,
        "exact_match_accuracy": exact_count / n,
        "norm_edit_similarity": norm_sum / n,
        "cer": total_edit / total_gt_chars if total_gt_chars else 0.0,
        "avg_confidence": statistics.mean(confidences) if confidences else 0.0,
        "elapsed_sec": elapsed_sec,
        "avg_inference_ms": elapsed_sec * 1000.0 / n,
        "total_edit_distance": total_edit,
        "total_ground_truth_chars": total_gt_chars,
    }
    return metrics, detail


# ---------------------------------------------------------------------
# CONFIG
# ---------------------------------------------------------------------

def get_config_algorithm(config: dict) -> str:
    return config.get("Architecture", {}).get("algorithm", "SVTR_LCNet")


def get_config_shape(config: dict) -> str:
    shape = config.get("Global", {}).get("d2s_train_image_shape")
    if isinstance(shape, list) and len(shape) == 3:
        return ",".join(str(int(v)) for v in shape)

    transforms = (
        config.get("Eval", {})
        .get("dataset", {})
        .get("transforms", [])
    )
    for transform in transforms:
        if not isinstance(transform, dict):
            continue
        cfg = transform.get("RecResizeImg")
        if isinstance(cfg, dict):
            shape = cfg.get("image_shape")
            if isinstance(shape, list) and len(shape) == 3:
                return ",".join(str(int(v)) for v in shape)

    return "3,48,320"


def get_config_space(config: dict) -> bool:
    return bool(config.get("Global", {}).get("use_space_char", True))


def get_base_dict(config: dict) -> Path:
    p = resolve_from_paddle(
        config.get("Global", {}).get("character_dict_path")
    )
    require_file(p, "dictionary của model gốc")
    return p


def extract_after_dict(output_path: Path) -> Path:
    inference_yml = MODEL_AFTER / "inference.yml"

    if inference_yml.is_file():
        cfg = load_yaml(inference_yml)
        chars = cfg.get("PostProcess", {}).get("character_dict", [])
        if chars:
            with output_path.open("w", encoding="utf-8", newline="\n") as f:
                for ch in chars:
                    f.write(str(ch) + "\n")
            return output_path

    fallback = (
        PADDLE_DIR
        / "ppocr"
        / "utils"
        / "dict"
        / "vietnamese_receipt_dict.txt"
    )
    require_file(fallback, "vietnamese_receipt_dict.txt")
    return fallback


def get_after_runtime_config() -> dict:
    shape = "3,48,320"
    inference_yml = MODEL_AFTER / "inference.yml"

    if inference_yml.is_file():
        cfg = load_yaml(inference_yml)
        for transform in cfg.get("PreProcess", {}).get("transform_ops", []):
            if not isinstance(transform, dict):
                continue
            resize = transform.get("RecResizeImg")
            if isinstance(resize, dict):
                found = resize.get("image_shape")
                if isinstance(found, list) and len(found) == 3:
                    shape = ",".join(str(int(v)) for v in found)
                    break

    if AFTER_TRAIN_CONFIG.is_file():
        train_cfg = load_yaml(AFTER_TRAIN_CONFIG)
        algorithm = get_config_algorithm(train_cfg)
        use_space = get_config_space(train_cfg)
    else:
        algorithm = "SVTR_LCNet"
        use_space = True

    return {
        "algorithm": algorithm,
        "shape": shape,
        "use_space_char": use_space,
    }



def extract_old_dict(output_path: Path) -> Path:
    """
    Lấy dictionary đúng của model V4 fine-tune cũ.
    Ưu tiên character_dict nhúng trong inference.yml, sau đó latin_dict.txt
    được copy cùng model, cuối cùng mới fallback về PaddleOCR repo.
    """
    inference_yml = MODEL_OLD / "inference.yml"

    if inference_yml.is_file():
        cfg = load_yaml(inference_yml)
        chars = cfg.get("PostProcess", {}).get("character_dict", [])
        if chars:
            with output_path.open("w", encoding="utf-8", newline="\n") as f:
                for ch in chars:
                    f.write(str(ch) + "\n")
            return output_path

    bundled = MODEL_OLD / "latin_dict.txt"
    if bundled.is_file():
        return bundled

    fallback = (
        PADDLE_DIR
        / "ppocr"
        / "utils"
        / "dict"
        / "latin_dict.txt"
    )
    require_file(fallback, "latin_dict.txt của model V4 cũ")
    return fallback


def get_model_runtime_config(
    model_dir: Path,
    train_config_path: Path | None = None,
) -> dict:
    """
    Đọc algorithm, image shape và use_space_char cho một inference model V4.
    """
    shape = "3,48,320"
    algorithm = "SVTR_LCNet"
    use_space = True

    inference_yml = model_dir / "inference.yml"
    if inference_yml.is_file():
        cfg = load_yaml(inference_yml)

        for transform in cfg.get("PreProcess", {}).get("transform_ops", []):
            if not isinstance(transform, dict):
                continue
            resize = transform.get("RecResizeImg")
            if isinstance(resize, dict):
                found = resize.get("image_shape")
                if isinstance(found, list) and len(found) == 3:
                    shape = ",".join(str(int(v)) for v in found)
                    break

    if train_config_path is not None and train_config_path.is_file():
        train_cfg = load_yaml(train_config_path)
        algorithm = get_config_algorithm(train_cfg)
        use_space = get_config_space(train_cfg)

    return {
        "algorithm": algorithm,
        "shape": shape,
        "use_space_char": use_space,
    }


# ---------------------------------------------------------------------
# EXPORT MODEL BEFORE
# ---------------------------------------------------------------------

def inference_model_exists(folder: Path) -> bool:
    return (
        (folder / "inference.pdiparams").is_file()
        and (
            (folder / "inference.pdmodel").is_file()
            or (folder / "inference.json").is_file()
        )
    )


def export_before_model(
    base_config: Path,
    output_dir: Path,
    force: bool,
) -> None:
    checkpoint = Path(str(MODEL_BEFORE) + ".pdparams")
    require_file(checkpoint, "MODEL_BEFORE .pdparams")

    if inference_model_exists(output_dir) and not force:
        print(f"✅ Base inference model đã có: {output_dir}")
        return

    export_script = PADDLE_DIR / "tools" / "export_model.py"
    require_file(export_script, "tools/export_model.py")

    if output_dir.exists():
        shutil.rmtree(output_dir)

    print("\n📦 Export PP-OCRv4 Original -> inference model...")

    cmd = [
        sys.executable,
        str(export_script),
        "-c",
        str(base_config),
        "-o",
        f"Global.pretrained_model={path_arg(MODEL_BEFORE)}",
        f"Global.save_inference_dir={path_arg(output_dir)}",
        # Paddle 2.6.x không hỗ trợ export_with_pir=True.
        # Ép export Old IR (.pdmodel + .pdiparams), tương thích với
        # môi trường Paddle 2.6.2 đã dùng để fine-tune.
        "Global.export_with_pir=False",
    ]

    export_env = os.environ.copy()
    export_env["FLAGS_enable_pir_api"] = "0"

    subprocess.run(
        cmd,
        cwd=str(PADDLE_DIR),
        env=export_env,
        check=True,
    )

    if not inference_model_exists(output_dir):
        raise RuntimeError(
            f"Export xong nhưng không tìm thấy inference model: {output_dir}"
        )

    print(f"✅ Base inference model: {output_dir}")


def make_runtime_model_dir(source: Path, dest: Path) -> None:
    """
    Tạo runtime dir không có inference.yml.
    Dictionary/algorithm/image_shape được truyền tường minh.
    """
    require_dir(source, "inference model")

    if dest.exists():
        shutil.rmtree(dest)
    dest.mkdir(parents=True)

    for name in (
        "inference.pdiparams",
        "inference.pdiparams.info",
        "inference.pdmodel",
        "inference.json",
    ):
        src = source / name
        if not src.is_file():
            continue
        dst = dest / name
        try:
            os.link(src, dst)
        except OSError:
            shutil.copy2(src, dst)

    if not inference_model_exists(dest):
        raise RuntimeError(f"Thiếu file inference trong {source}")


# ---------------------------------------------------------------------
# LOCAL PADDLEOCR INFERENCE HELPER
# ---------------------------------------------------------------------

RUNTIME_HELPER_CODE = r"""
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

import cv2
import numpy as np

sys.path.insert(0, os.getcwd())

import tools.infer.utility as utility
from tools.infer.predict_rec import TextRecognizer


def safe_imread(path):
    try:
        data = np.fromfile(path, dtype=np.uint8)
        image = cv2.imdecode(data, cv2.IMREAD_COLOR)
        if image is not None:
            return image
    except Exception:
        pass
    return cv2.imread(path)


def main():
    image_list_file = Path(os.environ["EVAL_IMAGE_LIST"])
    output_json = Path(os.environ["EVAL_OUTPUT_JSON"])
    warmup_count = int(os.environ.get("EVAL_WARMUP_COUNT", "2"))

    image_paths = [
        line.strip()
        for line in image_list_file.read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]

    args = utility.parse_args()
    recognizer = TextRecognizer(args)

    images = []
    for path in image_paths:
        img = safe_imread(path)
        if img is None:
            raise RuntimeError(f"Không đọc được ảnh: {path}")
        images.append(img)

    if warmup_count > 0:
        for _ in range(warmup_count):
            recognizer([images[0]])

    results, elapsed_sec = recognizer(images)

    predictions = []
    for path, result in zip(image_paths, results):
        if result is None:
            text, confidence = "", 0.0
        elif isinstance(result, (list, tuple)) and len(result) >= 2:
            text = str(result[0])
            try:
                confidence = float(result[1])
            except Exception:
                confidence = 0.0
        else:
            text, confidence = str(result), 0.0

        predictions.append(
            {
                "image": str(Path(path).resolve()),
                "text": text,
                "confidence": confidence,
            }
        )

    output_json.write_text(
        json.dumps(
            {
                "elapsed_sec": float(elapsed_sec),
                "predictions": predictions,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
"""


def run_recognition(
    label: str,
    model_dir: Path,
    dict_path: Path,
    algorithm: str,
    image_shape: str,
    use_space_char: bool,
    image_list_file: Path,
    output_json: Path,
    helper_file: Path,
    use_gpu: bool,
) -> dict:
    print(f"\n🔎 {label} ({'GPU' if use_gpu else 'CPU'})...")

    cmd = [
        sys.executable,
        str(helper_file),
        f"--rec_model_dir={path_arg(model_dir)}",
        f"--rec_char_dict_path={path_arg(dict_path)}",
        f"--rec_algorithm={algorithm}",
        f"--rec_image_shape={image_shape}",
        f"--rec_batch_num={REC_BATCH_NUM}",
        f"--use_gpu={'True' if use_gpu else 'False'}",
        f"--use_space_char={'True' if use_space_char else 'False'}",
        "--warmup=False",
    ]

    env = os.environ.copy()
    env["EVAL_IMAGE_LIST"] = str(image_list_file)
    env["EVAL_OUTPUT_JSON"] = str(output_json)
    env["EVAL_WARMUP_COUNT"] = str(WARMUP_COUNT)
    env["FLAGS_allocator_strategy"] = "auto_growth"

    subprocess.run(
        cmd,
        cwd=str(PADDLE_DIR),
        env=env,
        check=True,
    )

    require_file(output_json, "prediction JSON")
    return json.loads(output_json.read_text(encoding="utf-8"))


# ---------------------------------------------------------------------
# TRAIN LOG
# ---------------------------------------------------------------------

TRAIN_RE = re.compile(
    r"epoch:\s*\[(?P<epoch>\d+)/(?P<total>\d+)\],"
    r".*?global_step:\s*(?P<step>\d+),"
    r".*?acc:\s*(?P<acc>[0-9.eE+-]+),"
    r".*?norm_edit_dis:\s*(?P<norm>[0-9.eE+-]+),"
    r".*?loss:\s*(?P<loss>[0-9.eE+-]+)"
)

EVAL_RE = re.compile(
    r"cur metric,\s*acc:\s*(?P<acc>[0-9.eE+-]+),\s*"
    r"norm_edit_dis:\s*(?P<norm>[0-9.eE+-]+)"
)

BEST_RE = re.compile(
    r"best metric,\s*acc:\s*(?P<acc>[0-9.eE+-]+),"
    r".*?norm_edit_dis:\s*(?P<norm>[0-9.eE+-]+),"
    r".*?best_epoch:\s*(?P<epoch>\d+)"
)


def parse_training_log() -> dict:
    require_file(TRAIN_LOG, "train_full.log")

    train_points = []
    eval_points = []
    best = None
    current_epoch = None
    current_step = None

    with TRAIN_LOG.open("r", encoding="utf-8", errors="replace") as f:
        for line in f:
            m = TRAIN_RE.search(line)
            if m:
                current_epoch = int(m.group("epoch"))
                current_step = int(m.group("step"))
                train_points.append(
                    {
                        "epoch": current_epoch,
                        "step": current_step,
                        "loss": float(m.group("loss")),
                        "acc": float(m.group("acc")),
                        "norm_edit_similarity": float(m.group("norm")),
                    }
                )
                continue

            m = EVAL_RE.search(line)
            if m and current_epoch is not None:
                eval_points.append(
                    {
                        "epoch": current_epoch,
                        "step": current_step,
                        "acc": float(m.group("acc")),
                        "norm_edit_similarity": float(m.group("norm")),
                    }
                )

            m = BEST_RE.search(line)
            if m:
                best = {
                    "epoch": int(m.group("epoch")),
                    "acc": float(m.group("acc")),
                    "norm_edit_similarity": float(m.group("norm")),
                }

    if not train_points:
        raise RuntimeError("Không parse được training metrics từ train_full.log.")

    return {"train": train_points, "eval": eval_points, "best": best}


def aggregate_log(parsed: dict) -> dict:
    train_groups = defaultdict(list)
    for p in parsed["train"]:
        train_groups[p["epoch"]].append(p)

    train_epoch = []
    for epoch in sorted(train_groups):
        points = train_groups[epoch]
        train_epoch.append(
            {
                "epoch": epoch,
                "loss": statistics.mean(p["loss"] for p in points),
            }
        )

    eval_last = {}
    for p in parsed["eval"]:
        eval_last[p["epoch"]] = p

    eval_epoch = [eval_last[e] for e in sorted(eval_last)]

    return {
        "train_epoch": train_epoch,
        "eval_epoch": eval_epoch,
        "best": parsed["best"],
    }


# ---------------------------------------------------------------------
# PLOTS / TABLES
# ---------------------------------------------------------------------

def get_plt():
    try:
        import matplotlib.pyplot as plt
    except ImportError as exc:
        raise RuntimeError(
            "Thiếu matplotlib. Chạy: pip install matplotlib"
        ) from exc
    return plt


def plot_training(agg: dict) -> None:
    plt = get_plt()

    fig, axes = plt.subplots(1, 2, figsize=(11.5, 4.2))

    epochs = [p["epoch"] for p in agg["train_epoch"]]
    losses = [p["loss"] for p in agg["train_epoch"]]

    axes[0].plot(epochs, losses, marker="o", markersize=3, linewidth=1.4)
    axes[0].set_title("Training Loss")
    axes[0].set_xlabel("Epoch")
    axes[0].set_ylabel("Loss")
    axes[0].grid(alpha=0.25)

    eval_points = agg["eval_epoch"]
    if eval_points:
        xs = [p["epoch"] for p in eval_points]
        acc = [p["acc"] * 100 for p in eval_points]
        norm = [p["norm_edit_similarity"] * 100 for p in eval_points]

        axes[1].plot(
            xs, acc,
            marker="o", markersize=3, linewidth=1.3,
            label="Exact-match Accuracy"
        )
        axes[1].plot(
            xs, norm,
            marker="s", markersize=3, linewidth=1.3,
            label="Normalized Edit Similarity"
        )

        best = agg.get("best")
        if best:
            axes[1].axvline(
                best["epoch"],
                linestyle="--",
                linewidth=1.0,
                label=f"Best checkpoint: epoch {best['epoch']}",
            )
        axes[1].legend(fontsize=8)

    axes[1].set_title("Validation Metrics")
    axes[1].set_xlabel("Epoch")
    axes[1].set_ylabel("Score (%)")
    axes[1].set_ylim(bottom=0)
    axes[1].grid(alpha=0.25)

    fig.suptitle(
        "PP-OCRv4 Fine-tuning: Training & Validation",
        fontsize=12,
    )
    fig.tight_layout()

    fig.savefig(
        RESULT_DIR / "01_training_validation_curves.png",
        dpi=220,
        bbox_inches="tight",
    )
    plt.close(fig)


def percent(v: float) -> str:
    return f"{v * 100:.2f}%"


def signed_pp(after: float, before: float) -> str:
    return f"{(after - before) * 100:+.2f} pp"


def comparison_rows(before: dict, after: dict) -> list[dict]:
    return [
        {
            "metric": "Exact Match Accuracy ↑",
            "before": percent(before["exact_match_accuracy"]),
            "after": percent(after["exact_match_accuracy"]),
            "change": signed_pp(
                after["exact_match_accuracy"],
                before["exact_match_accuracy"],
            ),
        },
        {
            "metric": "Norm. Edit Similarity ↑",
            "before": percent(before["norm_edit_similarity"]),
            "after": percent(after["norm_edit_similarity"]),
            "change": signed_pp(
                after["norm_edit_similarity"],
                before["norm_edit_similarity"],
            ),
        },
        {
            "metric": "CER ↓",
            "before": percent(before["cer"]),
            "after": percent(after["cer"]),
            "change": signed_pp(after["cer"], before["cer"]),
        },
        {
            "metric": "Avg. Inference Time ↓",
            "before": f"{before['avg_inference_ms']:.2f} ms",
            "after": f"{after['avg_inference_ms']:.2f} ms",
            "change": (
                f"{after['avg_inference_ms'] - before['avg_inference_ms']:+.2f} ms"
            ),
        },
    ]


def save_comparison(rows: list[dict]) -> None:
    csv_path = RESULT_DIR / "02_before_after_metrics.csv"
    with csv_path.open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(
            f,
            fieldnames=["metric", "before", "after", "change"],
        )
        writer.writeheader()
        writer.writerows(rows)

    plt = get_plt()
    fig, ax = plt.subplots(figsize=(9.2, 2.8))
    ax.axis("off")

    table = ax.table(
        cellText=[
            [r["metric"], r["before"], r["after"], r["change"]]
            for r in rows
        ],
        colLabels=[
            "Metric",
            "PP-OCRv4 Original",
            "Fine-tuned",
            "Change",
        ],
        cellLoc="center",
        loc="center",
        colWidths=[0.34, 0.22, 0.22, 0.18],
    )
    table.auto_set_font_size(False)
    table.set_fontsize(9.5)
    table.scale(1, 1.55)

    ax.set_title(
        "PP-OCRv4 Original vs Fine-tuned on Independent Test Set",
        fontsize=11,
        pad=12,
    )

    fig.tight_layout()
    fig.savefig(
        RESULT_DIR / "02_before_after_table.png",
        dpi=220,
        bbox_inches="tight",
    )
    plt.close(fig)


def save_predictions(
    test_rows: list[dict],
    before_rows: list[dict],
    after_rows: list[dict],
) -> None:
    bmap = {r["image"]: r for r in before_rows}
    amap = {r["image"]: r for r in after_rows}

    path = RESULT_DIR / "test_predictions.csv"
    with path.open("w", encoding="utf-8-sig", newline="") as f:
        fields = [
            "image",
            "ground_truth",
            "before_prediction",
            "before_exact",
            "before_edit_distance",
            "before_norm_edit_similarity",
            "before_confidence",
            "after_prediction",
            "after_exact",
            "after_edit_distance",
            "after_norm_edit_similarity",
            "after_confidence",
        ]
        writer = csv.DictWriter(f, fieldnames=fields)
        writer.writeheader()

        for row in test_rows:
            key = row["relative_image"]
            b = bmap[key]
            a = amap[key]
            writer.writerow(
                {
                    "image": key,
                    "ground_truth": row["ground_truth"],
                    "before_prediction": b["prediction"],
                    "before_exact": b["exact_match"],
                    "before_edit_distance": b["edit_distance"],
                    "before_norm_edit_similarity": f"{b['norm_edit_similarity']:.8f}",
                    "before_confidence": f"{b['confidence']:.8f}",
                    "after_prediction": a["prediction"],
                    "after_exact": a["exact_match"],
                    "after_edit_distance": a["edit_distance"],
                    "after_norm_edit_similarity": f"{a['norm_edit_similarity']:.8f}",
                    "after_confidence": f"{a['confidence']:.8f}",
                }
            )


def save_summary(
    before: dict,
    after: dict,
    parsed_log: dict,
) -> None:
    payload = {
        "test_set": {
            "label_file": str(TEST_LABEL),
            "samples": len(read_test_set_cache),
        },
        "before": {
            "model_checkpoint": str(MODEL_BEFORE),
            "metrics": before,
        },
        "after": {
            "model_dir": str(MODEL_AFTER),
            "metrics": after,
        },
        "training": {
            "log": str(TRAIN_LOG),
            "best": parsed_log.get("best"),
        },
        "metric_definitions": {
            "exact_match_accuracy": (
                "prediction == ground truth sau Unicode NFC + strip"
            ),
            "norm_edit_similarity": (
                "mean(1 - Levenshtein(pred, gt) / max(len(pred), len(gt), 1))"
            ),
            "cer": (
                "sum Levenshtein(pred, gt) / sum ground-truth characters"
            ),
            "avg_inference_ms": (
                "TextRecognizer preprocess + inference + postprocess, sau warm-up"
            ),
        },
    }

    (RESULT_DIR / "metrics_summary.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )

    best = parsed_log.get("best") or {}
    lines = [
        "PP-OCRv4 FINE-TUNE EVALUATION SUMMARY",
        "=" * 48,
        f"Test samples: {after['samples']}",
        "",
        "PP-OCRv4 ORIGINAL",
        f"- Exact Match Accuracy: {percent(before['exact_match_accuracy'])}",
        f"- Norm Edit Similarity: {percent(before['norm_edit_similarity'])}",
        f"- CER: {percent(before['cer'])}",
        f"- Avg inference time: {before['avg_inference_ms']:.2f} ms/sample",
        "",
        "FINE-TUNED",
        f"- Exact Match Accuracy: {percent(after['exact_match_accuracy'])}",
        f"- Norm Edit Similarity: {percent(after['norm_edit_similarity'])}",
        f"- CER: {percent(after['cer'])}",
        f"- Avg inference time: {after['avg_inference_ms']:.2f} ms/sample",
        "",
        "CHANGE",
        "- Accuracy: "
        + signed_pp(
            after["exact_match_accuracy"],
            before["exact_match_accuracy"],
        ),
        "- Norm Edit Similarity: "
        + signed_pp(
            after["norm_edit_similarity"],
            before["norm_edit_similarity"],
        ),
        "- CER: " + signed_pp(after["cer"], before["cer"]),
    ]

    if best:
        lines += [
            "",
            "TRAINING CHECKPOINT",
            f"- Best epoch (main metric = acc): {best['epoch']}",
            f"- Best validation exact accuracy: {percent(best['acc'])}",
            (
                "- Norm Edit Similarity at best checkpoint: "
                f"{percent(best['norm_edit_similarity'])}"
            ),
        ]

    (RESULT_DIR / "report_summary.txt").write_text(
        "\n".join(lines) + "\n",
        encoding="utf-8",
    )



# ---------------------------------------------------------------------
# PP-OCRv6 ORIGINAL (PaddleOCR 3.x TextRecognition API)
# ---------------------------------------------------------------------

V6_HELPER_CODE = r"""
from __future__ import annotations

import json
import os
import time
from pathlib import Path

import paddle
from paddleocr import TextRecognition

print(f"[V6] Paddle version: {paddle.__version__}")


def result_to_dict(res):
    # PaddleOCR Result object hỗ trợ dict-like và thuộc tính .json.
    try:
        rec_text = res["rec_text"]
        rec_score = res["rec_score"]
        input_path = res["input_path"]
        return {
            "input_path": str(input_path),
            "rec_text": str(rec_text),
            "rec_score": float(rec_score),
        }
    except Exception:
        pass

    try:
        data = res.json
        if callable(data):
            data = data()
        if isinstance(data, str):
            data = json.loads(data)
        if isinstance(data, dict) and "res" in data:
            data = data["res"]
        return {
            "input_path": str(data.get("input_path", "")),
            "rec_text": str(data.get("rec_text", "")),
            "rec_score": float(data.get("rec_score", 0.0)),
        }
    except Exception as exc:
        raise RuntimeError(
            f"Không đọc được output TextRecognition: {type(res)} | {exc}"
        )


def main():
    image_list_path = Path(os.environ["V6_IMAGE_LIST"])
    output_json = Path(os.environ["V6_OUTPUT_JSON"])
    model_name = os.environ.get("V6_MODEL_NAME", "PP-OCRv6_medium_rec")
    model_dir = os.environ.get("V6_MODEL_DIR", "").strip()
    device = os.environ.get("V6_DEVICE", "gpu")
    batch_size = int(os.environ.get("V6_BATCH_SIZE", "8"))
    warmup_count = int(os.environ.get("V6_WARMUP_COUNT", "2"))

    image_paths = [
        line.strip()
        for line in image_list_path.read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]
    if not image_paths:
        raise RuntimeError("Danh sách ảnh V6 rỗng.")

    kwargs = {
        "device": device,
        "engine": "paddle_static",
    }

    if model_dir:
        kwargs["model_dir"] = model_dir
    else:
        kwargs["model_name"] = model_name

    print(
        f"[V6] Init TextRecognition: "
        f"{model_dir if model_dir else model_name} | device={device}"
    )
    model = TextRecognition(**kwargs)

    # Warm-up không tính vào benchmark.
    for _ in range(max(warmup_count, 0)):
        warm = model.predict(input=image_paths[0], batch_size=1)
        # Ép materialize generator/list.
        list(warm)

    start = time.perf_counter()
    output = model.predict(input=image_paths, batch_size=batch_size)
    results = list(output)
    elapsed_sec = time.perf_counter() - start

    predictions = []
    for res in results:
        item = result_to_dict(res)
        p = str(Path(item["input_path"]).resolve())
        predictions.append(
            {
                "image": p,
                "text": item["rec_text"],
                "confidence": item["rec_score"],
            }
        )

    payload = {
        "model_name": model_name,
        "model_dir": model_dir or None,
        "elapsed_sec": float(elapsed_sec),
        "predictions": predictions,
    }
    output_json.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
"""


def run_v6_recognition(
    image_list: Path,
    output_json: Path,
    helper_path: Path,
    use_gpu: bool,
    model_name: str,
    model_dir: str | None,
    v6_python: str,
) -> dict:
    """
    Chạy PP-OCRv6 recognition qua API TextRecognition chính thức.
    Model được warm-up trước, benchmark không tính thời gian khởi tạo/download.
    """
    helper_path.write_text(
        V6_HELPER_CODE,
        encoding="utf-8",
        newline="\n",
    )

    env = os.environ.copy()
    env["V6_IMAGE_LIST"] = str(image_list)
    env["V6_OUTPUT_JSON"] = str(output_json)
    env["V6_MODEL_NAME"] = model_name
    env["V6_MODEL_DIR"] = model_dir or ""
    env["V6_DEVICE"] = "gpu:0" if use_gpu else "cpu"
    env["V6_BATCH_SIZE"] = str(REC_BATCH_NUM)
    env["V6_WARMUP_COUNT"] = str(WARMUP_COUNT)

    # Nếu model chưa cache, ưu tiên nguồn BOS.
    env.setdefault("PADDLE_PDX_MODEL_SOURCE", "BOS")
    env.setdefault("PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK", "True")

    v6_python_path = Path(v6_python).resolve()
    require_file(v6_python_path, "Python runtime riêng cho PP-OCRv6")

    print(
        f"\n🔎 PP-OCRv6 Original: "
        f"{model_dir if model_dir else model_name}"
    )
    print(f"   V6 runtime: {v6_python_path}")

    subprocess.run(
        [str(v6_python_path), str(helper_path)],
        cwd=str(BASE_DIR),
        env=env,
        check=True,
    )

    if not output_json.is_file():
        raise RuntimeError(
            f"Không tạo được V6 prediction JSON: {output_json}"
        )

    return json.loads(output_json.read_text(encoding="utf-8"))


def three_model_rows(
    v4_original: dict,
    v4_finetuned: dict,
    v6_original: dict,
) -> list[dict]:
    return [
        {
            "metric": "Exact Match Accuracy ↑",
            "v4_original": percent(v4_original["exact_match_accuracy"]),
            "v4_finetuned": percent(v4_finetuned["exact_match_accuracy"]),
            "v6_original": percent(v6_original["exact_match_accuracy"]),
        },
        {
            "metric": "Norm. Edit Similarity ↑",
            "v4_original": percent(v4_original["norm_edit_similarity"]),
            "v4_finetuned": percent(v4_finetuned["norm_edit_similarity"]),
            "v6_original": percent(v6_original["norm_edit_similarity"]),
        },
        {
            "metric": "CER ↓",
            "v4_original": percent(v4_original["cer"]),
            "v4_finetuned": percent(v4_finetuned["cer"]),
            "v6_original": percent(v6_original["cer"]),
        },
        {
            "metric": "Avg. Inference Time ↓",
            "v4_original": f"{v4_original['avg_inference_ms']:.2f} ms",
            "v4_finetuned": f"{v4_finetuned['avg_inference_ms']:.2f} ms",
            "v6_original": f"{v6_original['avg_inference_ms']:.2f} ms",
        },
    ]


def save_three_model_comparison(rows: list[dict]) -> None:
    csv_path = RESULT_DIR / "03_three_model_metrics.csv"

    with csv_path.open(
        "w",
        encoding="utf-8-sig",
        newline="",
    ) as f:
        writer = csv.DictWriter(
            f,
            fieldnames=[
                "metric",
                "v4_original",
                "v4_finetuned",
                "v6_original",
            ],
        )
        writer.writeheader()
        writer.writerows(rows)

    plt = get_plt()
    fig, ax = plt.subplots(figsize=(9.3, 2.8))
    ax.axis("off")

    table = ax.table(
        cellText=[
            [
                r["metric"],
                r["v4_original"],
                r["v4_finetuned"],
                r["v6_original"],
            ]
            for r in rows
        ],
        colLabels=[
            "Metric",
            "PP-OCRv4 Original",
            "PP-OCRv4 Fine-tuned",
            "PP-OCRv6 Original",
        ],
        cellLoc="center",
        loc="center",
        colWidths=[0.32, 0.22, 0.23, 0.23],
    )

    table.auto_set_font_size(False)
    table.set_fontsize(9.2)
    table.scale(1, 1.58)

    ax.set_title(
        "PP-OCRv4 Original vs Fine-tuned vs PP-OCRv6 Original",
        fontsize=11,
        pad=12,
    )

    fig.tight_layout()
    fig.savefig(
        RESULT_DIR / "03_three_model_table.png",
        dpi=220,
        bbox_inches="tight",
    )
    plt.close(fig)


def automatic_comparison_conclusion(
    v4_original: dict,
    v4_finetuned: dict,
    v6_original: dict,
    v6_model_name: str,
) -> str:
    models = {
        "PP-OCRv4 Original": v4_original,
        "PP-OCRv4 Fine-tuned": v4_finetuned,
        f"{v6_model_name} Original": v6_original,
    }

    best_acc = max(
        models,
        key=lambda k: models[k]["exact_match_accuracy"],
    )
    best_norm = max(
        models,
        key=lambda k: models[k]["norm_edit_similarity"],
    )
    best_cer = min(
        models,
        key=lambda k: models[k]["cer"],
    )
    fastest = min(
        models,
        key=lambda k: models[k]["avg_inference_ms"],
    )

    v6_better_recognition = (
        v6_original["exact_match_accuracy"]
        >= max(
            v4_original["exact_match_accuracy"],
            v4_finetuned["exact_match_accuracy"],
        )
        and v6_original["norm_edit_similarity"]
        >= max(
            v4_original["norm_edit_similarity"],
            v4_finetuned["norm_edit_similarity"],
        )
        and v6_original["cer"]
        <= min(
            v4_original["cer"],
            v4_finetuned["cer"],
        )
    )

    lines = [
        "AUTO COMPARISON (không dùng như kết luận cuối nếu chưa kiểm tra prediction)",
        f"- Best Accuracy: {best_acc}",
        f"- Best Norm Edit Similarity: {best_norm}",
        f"- Lowest CER: {best_cer}",
        f"- Fastest measured runtime: {fastest}",
        "",
    ]

    if v6_better_recognition:
        lines += [
            "Kết quả test hiện tại hỗ trợ hướng kết luận:",
            (
                f"{v6_model_name} pretrained cho chất lượng nhận dạng tốt hơn "
                "hai cấu hình PP-OCRv4 trên test set độc lập."
            ),
            (
                "PP-OCRv4 vẫn được sử dụng cho thí nghiệm fine-tuning để đáp ứng "
                "yêu cầu tùy biến mô hình; PP-OCRv6 được tích hợp trực tiếp ở dạng "
                "pretrained do chi phí tài nguyên fine-tuning cao hơn đối với "
                "phần cứng của nhóm."
            ),
        ]
    else:
        lines += [
            "Kết quả test hiện tại CHƯA hỗ trợ kết luận rằng PP-OCRv6 tốt nhất "
            "trên cả Accuracy, Norm Edit Similarity và CER.",
            "Hãy dựa vào bảng metric thực tế thay vì kết luận trước.",
        ]

    return "\n".join(lines) + "\n"


def save_three_model_predictions(
    test_rows: list[dict],
    v4_original_rows: list[dict],
    v4_finetuned_rows: list[dict],
    v6_original_rows: list[dict],
) -> None:
    maps = {
        "v4o": {r["image"]: r for r in v4_original_rows},
        "v4f": {r["image"]: r for r in v4_finetuned_rows},
        "v6": {r["image"]: r for r in v6_original_rows},
    }

    path = RESULT_DIR / "test_predictions_3models.csv"
    fields = [
        "image",
        "ground_truth",
        "v4_original_prediction",
        "v4_original_exact",
        "v4_original_norm_similarity",
        "v4_finetuned_prediction",
        "v4_finetuned_exact",
        "v4_finetuned_norm_similarity",
        "v6_original_prediction",
        "v6_original_exact",
        "v6_original_norm_similarity",
    ]

    with path.open(
        "w",
        encoding="utf-8-sig",
        newline="",
    ) as f:
        writer = csv.DictWriter(f, fieldnames=fields)
        writer.writeheader()

        for row in test_rows:
            key = row["relative_image"]
            a = maps["v4o"][key]
            b = maps["v4f"][key]
            c = maps["v6"][key]

            writer.writerow(
                {
                    "image": key,
                    "ground_truth": row["ground_truth"],
                    "v4_original_prediction": a["prediction"],
                    "v4_original_exact": a["exact_match"],
                    "v4_original_norm_similarity": f"{a['norm_edit_similarity']:.8f}",
                    "v4_finetuned_prediction": b["prediction"],
                    "v4_finetuned_exact": b["exact_match"],
                    "v4_finetuned_norm_similarity": f"{b['norm_edit_similarity']:.8f}",
                    "v6_original_prediction": c["prediction"],
                    "v6_original_exact": c["exact_match"],
                    "v6_original_norm_similarity": f"{c['norm_edit_similarity']:.8f}",
                }
            )


# ---------------------------------------------------------------------
# FOUR-MODEL COMPARISON
# ---------------------------------------------------------------------

def four_model_rows(
    v4_original: dict,
    v4_old: dict,
    v4_new: dict,
    v6_original: dict,
) -> list[dict]:
    return [
        {
            "metric": "Exact Match Accuracy ↑",
            "v4_original": percent(v4_original["exact_match_accuracy"]),
            "v4_old": percent(v4_old["exact_match_accuracy"]),
            "v4_new": percent(v4_new["exact_match_accuracy"]),
            "v6_original": percent(v6_original["exact_match_accuracy"]),
        },
        {
            "metric": "Norm. Edit Similarity ↑",
            "v4_original": percent(v4_original["norm_edit_similarity"]),
            "v4_old": percent(v4_old["norm_edit_similarity"]),
            "v4_new": percent(v4_new["norm_edit_similarity"]),
            "v6_original": percent(v6_original["norm_edit_similarity"]),
        },
        {
            "metric": "CER ↓",
            "v4_original": percent(v4_original["cer"]),
            "v4_old": percent(v4_old["cer"]),
            "v4_new": percent(v4_new["cer"]),
            "v6_original": percent(v6_original["cer"]),
        },
        {
            "metric": "Avg. Inference Time ↓",
            "v4_original": f"{v4_original['avg_inference_ms']:.2f} ms",
            "v4_old": f"{v4_old['avg_inference_ms']:.2f} ms",
            "v4_new": f"{v4_new['avg_inference_ms']:.2f} ms",
            "v6_original": f"{v6_original['avg_inference_ms']:.2f} ms",
        },
    ]


def save_four_model_comparison(rows: list[dict]) -> None:
    csv_path = RESULT_DIR / "04_four_model_metrics.csv"
    with csv_path.open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(
            f,
            fieldnames=[
                "metric",
                "v4_original",
                "v4_old",
                "v4_new",
                "v6_original",
            ],
        )
        writer.writeheader()
        writer.writerows(rows)

    plt = get_plt()
    fig, ax = plt.subplots(figsize=(11.8, 3.0))
    ax.axis("off")

    table = ax.table(
        cellText=[
            [
                r["metric"],
                r["v4_original"],
                r["v4_old"],
                r["v4_new"],
                r["v6_original"],
            ]
            for r in rows
        ],
        colLabels=[
            "Metric",
            "V4 Original",
            "V4 Fine-tuned cũ\n(Latin dict)",
            "V4 Fine-tuned mới\n(Vietnamese dict)",
            "V6 Original",
        ],
        cellLoc="center",
        loc="center",
        colWidths=[0.27, 0.17, 0.20, 0.20, 0.17],
    )
    table.auto_set_font_size(False)
    table.set_fontsize(9.0)
    table.scale(1, 1.65)

    ax.set_title(
        "OCR Model Comparison on Independent Test Set",
        fontsize=11,
        pad=13,
    )
    fig.tight_layout()
    fig.savefig(
        RESULT_DIR / "04_four_model_table.png",
        dpi=220,
        bbox_inches="tight",
    )
    plt.close(fig)


def save_four_model_predictions(
    test_rows: list[dict],
    v4_original_rows: list[dict],
    v4_old_rows: list[dict],
    v4_new_rows: list[dict],
    v6_rows: list[dict],
) -> None:
    maps = {
        "v4o": {r["image"]: r for r in v4_original_rows},
        "v4old": {r["image"]: r for r in v4_old_rows},
        "v4new": {r["image"]: r for r in v4_new_rows},
        "v6": {r["image"]: r for r in v6_rows},
    }

    path = RESULT_DIR / "test_predictions_4models.csv"
    fields = [
        "image",
        "ground_truth",
        "v4_original_prediction",
        "v4_original_exact",
        "v4_original_norm_similarity",
        "v4_old_prediction",
        "v4_old_exact",
        "v4_old_norm_similarity",
        "v4_new_prediction",
        "v4_new_exact",
        "v4_new_norm_similarity",
        "v6_prediction",
        "v6_exact",
        "v6_norm_similarity",
    ]

    with path.open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fields)
        writer.writeheader()

        for row in test_rows:
            key = row["relative_image"]
            a = maps["v4o"][key]
            b = maps["v4old"][key]
            c = maps["v4new"][key]
            d = maps["v6"][key]

            writer.writerow(
                {
                    "image": key,
                    "ground_truth": row["ground_truth"],
                    "v4_original_prediction": a["prediction"],
                    "v4_original_exact": a["exact_match"],
                    "v4_original_norm_similarity": f"{a['norm_edit_similarity']:.8f}",
                    "v4_old_prediction": b["prediction"],
                    "v4_old_exact": b["exact_match"],
                    "v4_old_norm_similarity": f"{b['norm_edit_similarity']:.8f}",
                    "v4_new_prediction": c["prediction"],
                    "v4_new_exact": c["exact_match"],
                    "v4_new_norm_similarity": f"{c['norm_edit_similarity']:.8f}",
                    "v6_prediction": d["prediction"],
                    "v6_exact": d["exact_match"],
                    "v6_norm_similarity": f"{d['norm_edit_similarity']:.8f}",
                }
            )


def save_four_model_summary(
    v4_original: dict,
    v4_old: dict,
    v4_new: dict,
    v6_original: dict,
    v6_model_name: str,
    v6_python: str,
) -> None:
    models = {
        "PP-OCRv4 Original": v4_original,
        "PP-OCRv4 Fine-tuned cũ (Latin dict)": v4_old,
        "PP-OCRv4 Fine-tuned mới (Vietnamese dict)": v4_new,
        f"{v6_model_name} Original": v6_original,
    }

    best_acc = max(models, key=lambda k: models[k]["exact_match_accuracy"])
    best_norm = max(models, key=lambda k: models[k]["norm_edit_similarity"])
    best_cer = min(models, key=lambda k: models[k]["cer"])
    fastest = min(models, key=lambda k: models[k]["avg_inference_ms"])

    payload = {
        "test_samples": v4_original["samples"],
        "models": models,
        "v6_runtime_python": str(v6_python),
        "best_by_metric": {
            "exact_match_accuracy": best_acc,
            "norm_edit_similarity": best_norm,
            "cer": best_cer,
            "avg_inference_ms": fastest,
        },
        "timing_note": (
            "Ba model PP-OCRv4 chạy bằng legacy TextRecognizer trong môi trường "
            "Paddle 2.x; PP-OCRv6 chạy bằng PaddleOCR 3.x/Paddle 3.x ở một "
            "Python environment riêng. Accuracy, Norm Edit Similarity và CER "
            "dùng cùng test set/ground truth; thời gian inference chỉ nên dùng "
            "tham khảo vì runtime pipeline không hoàn toàn giống nhau."
        ),
    }

    (RESULT_DIR / "04_four_model_summary.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )

    v6_name = f"{v6_model_name} Original"
    v6_best_recognition = (
        v6_original["exact_match_accuracy"]
        >= max(
            v4_original["exact_match_accuracy"],
            v4_old["exact_match_accuracy"],
            v4_new["exact_match_accuracy"],
        )
        and v6_original["norm_edit_similarity"]
        >= max(
            v4_original["norm_edit_similarity"],
            v4_old["norm_edit_similarity"],
            v4_new["norm_edit_similarity"],
        )
        and v6_original["cer"]
        <= min(
            v4_original["cer"],
            v4_old["cer"],
            v4_new["cer"],
        )
    )

    old_better_than_new = (
        v4_old["norm_edit_similarity"] > v4_new["norm_edit_similarity"]
        and v4_old["cer"] < v4_new["cer"]
    )

    lines = [
        "FOUR-MODEL EVALUATION",
        "=" * 60,
        f"- Best Exact Match Accuracy: {best_acc}",
        f"- Best Norm Edit Similarity: {best_norm}",
        f"- Lowest CER: {best_cer}",
        f"- Fastest measured runtime: {fastest}",
        "",
    ]

    if old_better_than_new:
        lines += [
            "V4 OLD vs V4 NEW:",
            "- Bản V4 fine-tune cũ (Latin dict) tốt hơn bản custom Vietnamese "
            "dict trên cả Norm Edit Similarity và CER.",
            "",
        ]
    else:
        lines += [
            "V4 OLD vs V4 NEW:",
            "- Không có bằng chứng đồng thời từ Norm Edit Similarity và CER "
            "rằng bản V4 cũ tốt hơn bản V4 mới.",
            "",
        ]

    if v6_best_recognition:
        lines += [
            "V6:",
            f"- {v6_name} đạt kết quả nhận dạng tốt nhất hoặc đồng hạng tốt nhất "
            "trên Accuracy, Norm Edit Similarity và CER.",
            "- Có thể dùng kết quả này để lập luận rằng V6 pretrained cho chất "
            "lượng nhận dạng tốt hơn, còn V4 được chọn cho thí nghiệm fine-tune "
            "do phù hợp tài nguyên huấn luyện hơn.",
        ]
    else:
        lines += [
            "V6:",
            "- Kết quả hiện tại CHƯA đủ để kết luận V6 tốt nhất trên cả ba "
            "chỉ số nhận dạng. Hãy dùng đúng số liệu trong bảng.",
        ]

    (RESULT_DIR / "04_four_model_conclusion.txt").write_text(
        "\n".join(lines) + "\n",
        encoding="utf-8",
    )


# ---------------------------------------------------------------------
# GPU
# ---------------------------------------------------------------------

def detect_gpu(force_cpu: bool) -> bool:
    if force_cpu:
        print("ℹ️ Ép dùng CPU.")
        return False

    try:
        import paddle
        if paddle.is_compiled_with_cuda():
            device = str(paddle.get_device())
            if device.lower().startswith("gpu"):
                print(f"⚡ Dùng GPU: {device}")
                return True
    except Exception as exc:
        print(f"⚠️ Không xác nhận được GPU: {exc}")

    print("ℹ️ Dùng CPU cho inference.")
    return False


# ---------------------------------------------------------------------
# MAIN
# ---------------------------------------------------------------------

def parse_cli():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--cpu",
        action="store_true",
        help="Ép inference bằng CPU.",
    )
    parser.add_argument(
        "--force-export-before",
        action="store_true",
        help="Export lại model gốc.",
    )
    parser.add_argument(
        "--v6-model-name",
        default="PP-OCRv6_medium_rec",
        choices=[
            "PP-OCRv6_medium_rec",
            "PP-OCRv6_small_rec",
            "PP-OCRv6_tiny_rec",
        ],
        help=(
            "PP-OCRv6 recognition model để so sánh. "
            "Mặc định: PP-OCRv6_medium_rec."
        ),
    )
    parser.add_argument(
        "--v6-model-dir",
        default=None,
        help=(
            "Đường dẫn model PP-OCRv6 đã import/download sẵn. "
            "Nếu bỏ trống, PaddleOCR tự lấy model theo --v6-model-name."
        ),
    )
    parser.add_argument(
        "--v6-python",
        default=str(
            (
                OCR_SERVICE_DIR.parent
                / ".venv_v6"
                / "Scripts"
                / "python.exe"
            ).resolve()
        ),
        help=(
            "Python executable của environment Paddle 3.x dành riêng cho V6. "
            "Mặc định: <project>/.venv_v6/Scripts/python.exe"
        ),
    )
    return parser.parse_args()


read_test_set_cache = []


def main():
    global read_test_set_cache

    args = parse_cli()

    print("=" * 72)
    print("OCR MODEL COMPARISON - 4 MODELS")
    print("=" * 72)

    require_dir(PADDLE_DIR, "PaddleOCR")
    require_dir(MODEL_AFTER, "V4 fine-tuned mới")
    require_dir(MODEL_OLD, "V4 fine-tuned cũ")
    require_file(Path(args.v6_python), "Python runtime Paddle 3.x cho V6")
    require_file(TRAIN_LOG, "train_full.log")

    RESULT_DIR.mkdir(parents=True, exist_ok=True)

    # Test set
    read_test_set_cache = read_test_set()
    test_rows = read_test_set_cache

    image_list = RESULT_DIR / "_test_image_list.txt"
    image_list.write_text(
        "\n".join(row["image"] for row in test_rows) + "\n",
        encoding="utf-8",
    )

    # Training curves
    print("\n📈 Parse train_full.log...")
    parsed_log = parse_training_log()
    plot_training(aggregate_log(parsed_log))

    # Config before
    base_config_path = find_base_config()
    base_cfg = load_yaml(base_config_path)
    before_dict = get_base_dict(base_cfg)
    before_algorithm = get_config_algorithm(base_cfg)
    before_shape = get_config_shape(base_cfg)
    before_space = get_config_space(base_cfg)

    # Config V4 fine-tuned mới
    after_dict = extract_after_dict(RESULT_DIR / "_new_character_dict.txt")
    after_cfg = get_after_runtime_config()

    # Config V4 fine-tuned cũ (Latin dict)
    old_dict = extract_old_dict(RESULT_DIR / "_old_character_dict.txt")
    old_cfg = get_model_runtime_config(
        MODEL_OLD,
        OLD_TRAIN_CONFIG if OLD_TRAIN_CONFIG.is_file() else None,
    )

    print("\n🔧 Runtime config")
    print(f"   V4 Original dict : {before_dict}")
    print(f"   V4 Original algo : {before_algorithm}")
    print(f"   V4 Original shape: {before_shape}")
    print(f"   V4 Old dict      : {old_dict}")
    print(f"   V4 Old algo      : {old_cfg['algorithm']}")
    print(f"   V4 Old shape     : {old_cfg['shape']}")
    print(f"   V4 New dict      : {after_dict}")
    print(f"   V4 New algo      : {after_cfg['algorithm']}")
    print(f"   V4 New shape     : {after_cfg['shape']}")

    # Export original
    before_infer = RESULT_DIR / "_before_ppocrv4_infer"
    export_before_model(
        base_config_path,
        before_infer,
        args.force_export_before,
    )

    # Runtime dirs without inference.yml
    runtime_before = RESULT_DIR / "_runtime_before"
    runtime_old = RESULT_DIR / "_runtime_v4_old"
    runtime_after = RESULT_DIR / "_runtime_v4_new"
    make_runtime_model_dir(before_infer, runtime_before)
    make_runtime_model_dir(MODEL_OLD, runtime_old)
    make_runtime_model_dir(MODEL_AFTER, runtime_after)

    helper = RESULT_DIR / "_run_recognition_eval.py"
    helper.write_text(
        RUNTIME_HELPER_CODE,
        encoding="utf-8",
        newline="\n",
    )

    use_gpu = detect_gpu(args.cpu)

    try:
        # Before
        raw_before = run_recognition(
            "PP-OCRv4 Original",
            runtime_before,
            before_dict,
            before_algorithm,
            before_shape,
            before_space,
            image_list,
            RESULT_DIR / "_before_predictions.json",
            helper,
            use_gpu,
        )

        before_metrics, before_rows = calculate_metrics(
            test_rows,
            raw_before["predictions"],
            float(raw_before["elapsed_sec"]),
        )

        # V4 fine-tuned cũ - Latin dict
        raw_old = run_recognition(
            "PP-OCRv4 Fine-tuned cũ (Latin dict)",
            runtime_old,
            old_dict,
            old_cfg["algorithm"],
            old_cfg["shape"],
            old_cfg["use_space_char"],
            image_list,
            RESULT_DIR / "_v4_old_predictions.json",
            helper,
            use_gpu,
        )

        old_metrics, old_rows = calculate_metrics(
            test_rows,
            raw_old["predictions"],
            float(raw_old["elapsed_sec"]),
        )

        # V4 fine-tuned mới - custom Vietnamese dict
        raw_after = run_recognition(
            "PP-OCRv4 Fine-tuned mới (Vietnamese dict)",
            runtime_after,
            after_dict,
            after_cfg["algorithm"],
            after_cfg["shape"],
            after_cfg["use_space_char"],
            image_list,
            RESULT_DIR / "_v4_new_predictions.json",
            helper,
            use_gpu,
        )

        after_metrics, after_rows = calculate_metrics(
            test_rows,
            raw_after["predictions"],
            float(raw_after["elapsed_sec"]),
        )

        # V6 Original qua PaddleOCR 3.x TextRecognition API
        v6_helper = RESULT_DIR / "_run_v6_recognition_eval.py"
        v6_raw = run_v6_recognition(
            image_list=image_list,
            output_json=RESULT_DIR / "_v6_predictions.json",
            helper_path=v6_helper,
            use_gpu=use_gpu,
            model_name=args.v6_model_name,
            model_dir=args.v6_model_dir,
            v6_python=args.v6_python,
        )

        v6_metrics, v6_rows = calculate_metrics(
            test_rows,
            v6_raw["predictions"],
            float(v6_raw["elapsed_sec"]),
        )

    finally:
        for folder in (runtime_before, runtime_old, runtime_after):
            try:
                if folder.exists():
                    shutil.rmtree(folder)
            except Exception:
                pass

    # Save outputs
    rows = comparison_rows(before_metrics, after_metrics)
    save_comparison(rows)
    save_predictions(test_rows, before_rows, after_rows)
    save_summary(before_metrics, after_metrics, parsed_log)

    # Bảng chính 4 model cho báo cáo
    rows4 = four_model_rows(
        before_metrics,
        old_metrics,
        after_metrics,
        v6_metrics,
    )
    save_four_model_comparison(rows4)
    save_four_model_predictions(
        test_rows,
        before_rows,
        old_rows,
        after_rows,
        v6_rows,
    )
    save_four_model_summary(
        before_metrics,
        old_metrics,
        after_metrics,
        v6_metrics,
        args.v6_model_name,
        args.v6_python,
    )

    print("\n" + "=" * 72)
    print("KẾT QUẢ TEST SET - 4 MODELS")
    print("=" * 72)

    def print_metric_block(name: str, m: dict) -> None:
        print(f"\n{name}")
        print(f"  Accuracy             : {percent(m['exact_match_accuracy'])}")
        print(f"  Norm Edit Similarity : {percent(m['norm_edit_similarity'])}")
        print(f"  CER                  : {percent(m['cer'])}")
        print(f"  Avg inference        : {m['avg_inference_ms']:.2f} ms/sample")

    print_metric_block("PP-OCRv4 Original", before_metrics)
    print_metric_block("PP-OCRv4 Fine-tuned cũ (Latin dict)", old_metrics)
    print_metric_block("PP-OCRv4 Fine-tuned mới (Vietnamese dict)", after_metrics)
    print_metric_block(f"{args.v6_model_name} Original", v6_metrics)

    print("\n📁 Output chính:")
    for name in (
        "01_training_validation_curves.png",
        "04_four_model_metrics.csv",
        "04_four_model_table.png",
        "test_predictions_4models.csv",
        "04_four_model_summary.json",
        "04_four_model_conclusion.txt",
    ):
        print(f"   ✅ {RESULT_DIR / name}")


if __name__ == "__main__":
    main()