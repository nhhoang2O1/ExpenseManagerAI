#!/usr/bin/env python3
"""Validate receipt metadata and create leakage-safe dataset splits."""

from __future__ import annotations

import argparse
import json
import os
import random
import re
import sys
import tempfile
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


SPLITS = ("train", "validation", "test")
STORE_GROUPS = (
    "circle_k",
    "gs25",
    "supermarket_food",
    "cafe_food",
    "pharmacy",
    "independent_retail",
)
REQUIRED_FIELDS = (
    "imageId",
    "receiptGroupId",
    "imagePath",
    "storeGroup",
    "storeName",
    "device",
    "lighting",
    "captureAngle",
    "quality",
    "sensitiveDataRedacted",
)
OPTIONAL_FIELDS = ("capturedAt", "split", "notes")
ALLOWED_FIELDS = frozenset(REQUIRED_FIELDS + OPTIONAL_FIELDS)
ENUMS = {
    "storeGroup": frozenset(STORE_GROUPS),
    "lighting": frozenset(("daylight", "indoor", "low_light", "mixed")),
    "captureAngle": frozenset(("straight", "tilted", "perspective")),
    "quality": frozenset(
        ("good", "faded", "blurred", "overexposed", "underexposed")
    ),
    "split": frozenset(SPLITS),
}
ID_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")
SAMPLE_STORE_NAMES = {
    "circle_k": "Circle K",
    "gs25": "GS25",
    "supermarket_food": "Sample Supermarket",
    "cafe_food": "Sample Cafe",
    "pharmacy": "Sample Pharmacy",
    "independent_retail": "Sample Independent Retail",
}


class DatasetError(ValueError):
    """Raised when config or metadata violates the dataset contract."""


def load_json(path: Path) -> dict[str, Any]:
    try:
        with path.open("r", encoding="utf-8") as handle:
            value = json.load(handle)
    except (OSError, json.JSONDecodeError) as exc:
        raise DatasetError(f"Cannot read JSON {path}: {exc}") from exc
    if not isinstance(value, dict):
        raise DatasetError(f"{path}: top-level JSON must be an object")
    return value


def load_jsonl(path: Path) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    try:
        with path.open("r", encoding="utf-8-sig") as handle:
            for line_number, raw_line in enumerate(handle, start=1):
                if not raw_line.strip():
                    continue
                try:
                    value = json.loads(raw_line)
                except json.JSONDecodeError as exc:
                    raise DatasetError(
                        f"{path}:{line_number}: invalid JSON: {exc.msg}"
                    ) from exc
                if not isinstance(value, dict):
                    raise DatasetError(
                        f"{path}:{line_number}: each JSONL row must be an object"
                    )
                value["_lineNumber"] = line_number
                records.append(value)
    except OSError as exc:
        raise DatasetError(f"Cannot read JSONL {path}: {exc}") from exc
    if not records:
        raise DatasetError(f"{path}: metadata is empty")
    return records


def require_non_negative_int(value: Any, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise DatasetError(f"{label} must be a non-negative integer")
    return value


def validate_config(config: dict[str, Any], source: Path) -> None:
    if config.get("allocationUnit") != "receiptGroupId":
        raise DatasetError(
            f"{source}: allocationUnit must be 'receiptGroupId'"
        )

    configured_splits = config.get("splits")
    if not isinstance(configured_splits, dict):
        raise DatasetError(f"{source}: splits must be an object")
    if set(configured_splits) != set(SPLITS):
        raise DatasetError(
            f"{source}: splits must contain exactly {', '.join(SPLITS)}"
        )

    split_totals = {
        split: require_non_negative_int(
            configured_splits[split], f"{source}: splits.{split}"
        )
        for split in SPLITS
    }

    groups = config.get("storeGroups")
    if not isinstance(groups, dict) or set(groups) != set(STORE_GROUPS):
        raise DatasetError(
            f"{source}: storeGroups must contain exactly "
            f"{', '.join(STORE_GROUPS)}"
        )

    calculated_split_totals = Counter()
    calculated_total = 0
    for store_group in STORE_GROUPS:
        quota = groups[store_group]
        if not isinstance(quota, dict):
            raise DatasetError(
                f"{source}: storeGroups.{store_group} must be an object"
            )
        expected_keys = {"total", *SPLITS}
        if set(quota) != expected_keys:
            raise DatasetError(
                f"{source}: storeGroups.{store_group} must contain exactly "
                "total, train, validation, test"
            )
        total = require_non_negative_int(
            quota["total"], f"{source}: storeGroups.{store_group}.total"
        )
        parts = {
            split: require_non_negative_int(
                quota[split],
                f"{source}: storeGroups.{store_group}.{split}",
            )
            for split in SPLITS
        }
        if sum(parts.values()) != total:
            raise DatasetError(
                f"{source}: quotas for {store_group} sum to "
                f"{sum(parts.values())}, expected {total}"
            )
        calculated_total += total
        calculated_split_totals.update(parts)

    target = require_non_negative_int(
        config.get("targetReceiptGroups"),
        f"{source}: targetReceiptGroups",
    )
    if calculated_total != target:
        raise DatasetError(
            f"{source}: store group totals sum to {calculated_total}, "
            f"expected {target}"
        )
    if dict(calculated_split_totals) != split_totals:
        raise DatasetError(
            f"{source}: per-group split totals {dict(calculated_split_totals)} "
            f"do not match splits {split_totals}"
        )
    seed = config.get("seed")
    if isinstance(seed, bool) or not isinstance(seed, int):
        raise DatasetError(f"{source}: seed must be an integer")


def clean_records(records: Iterable[dict[str, Any]]) -> list[dict[str, Any]]:
    return [
        {key: value for key, value in record.items() if key != "_lineNumber"}
        for record in records
    ]


def validate_relative_image_path(value: str, label: str) -> None:
    if "\\" in value:
        raise DatasetError(f"{label}: imagePath must use '/' separators")
    path = PurePosixPath(value)
    if path.is_absolute() or ".." in path.parts or not path.parts:
        raise DatasetError(
            f"{label}: imagePath must be a safe path relative to ml/"
        )
    if ":" in path.parts[0]:
        raise DatasetError(
            f"{label}: imagePath must not contain a drive or URI scheme"
        )


def validate_record(record: dict[str, Any]) -> None:
    line = record["_lineNumber"]
    label = f"line {line}"
    fields = set(record) - {"_lineNumber"}
    missing = set(REQUIRED_FIELDS) - fields
    extra = fields - ALLOWED_FIELDS
    if missing:
        raise DatasetError(
            f"{label}: missing fields: {', '.join(sorted(missing))}"
        )
    if extra:
        raise DatasetError(
            f"{label}: unsupported fields: {', '.join(sorted(extra))}"
        )

    for field in (
        "imageId",
        "receiptGroupId",
        "imagePath",
        "storeName",
        "device",
    ):
        value = record[field]
        if not isinstance(value, str) or not value.strip():
            raise DatasetError(f"{label}: {field} must be a non-empty string")

    for field in ("imageId", "receiptGroupId"):
        if not ID_PATTERN.fullmatch(record[field]):
            raise DatasetError(
                f"{label}: {field} must match {ID_PATTERN.pattern}"
            )

    validate_relative_image_path(record["imagePath"], label)

    for field, allowed in ENUMS.items():
        if field in record and record[field] not in allowed:
            raise DatasetError(
                f"{label}: invalid {field}={record[field]!r}; "
                f"expected one of {', '.join(sorted(allowed))}"
            )

    if record["sensitiveDataRedacted"] is not True:
        raise DatasetError(
            f"{label}: sensitiveDataRedacted must be true before use"
        )
    if "notes" in record and not isinstance(record["notes"], str):
        raise DatasetError(f"{label}: notes must be a string")
    if "capturedAt" in record:
        captured_at = record["capturedAt"]
        if not isinstance(captured_at, str):
            raise DatasetError(f"{label}: capturedAt must be an ISO date-time")
        try:
            parsed = datetime.fromisoformat(captured_at.replace("Z", "+00:00"))
        except ValueError as exc:
            raise DatasetError(
                f"{label}: capturedAt must be an ISO date-time"
            ) from exc
        if parsed.tzinfo is None:
            raise DatasetError(
                f"{label}: capturedAt must include a UTC offset or Z"
            )


def analyze_records(
    records: list[dict[str, Any]],
    config: dict[str, Any],
    *,
    require_target_counts: bool,
    require_split: bool,
) -> dict[str, Any]:
    seen_images: set[str] = set()
    group_store: dict[str, str] = {}
    group_store_name: dict[str, str] = {}
    group_splits: defaultdict[str, set[str]] = defaultdict(set)
    groups_by_store: Counter[str] = Counter()

    for record in records:
        validate_record(record)
        image_id = record["imageId"]
        receipt_group = record["receiptGroupId"]
        store_group = record["storeGroup"]

        if image_id in seen_images:
            raise DatasetError(
                f"line {record['_lineNumber']}: duplicate imageId {image_id!r}"
            )
        seen_images.add(image_id)

        previous_store = group_store.setdefault(receipt_group, store_group)
        if previous_store != store_group:
            raise DatasetError(
                f"receiptGroupId {receipt_group!r} has multiple storeGroup "
                f"values: {previous_store!r}, {store_group!r}"
            )
        previous_name = group_store_name.setdefault(
            receipt_group, record["storeName"]
        )
        if previous_name != record["storeName"]:
            raise DatasetError(
                f"receiptGroupId {receipt_group!r} has multiple storeName "
                f"values: {previous_name!r}, {record['storeName']!r}"
            )
        if "split" in record:
            group_splits[receipt_group].add(record["split"])
        elif require_split:
            raise DatasetError(
                f"line {record['_lineNumber']}: split is required"
            )

    for receipt_group, split_values in group_splits.items():
        if len(split_values) > 1:
            raise DatasetError(
                f"group leakage: receiptGroupId {receipt_group!r} appears in "
                f"multiple splits: {', '.join(sorted(split_values))}"
            )

    groups_by_split: Counter[str] = Counter()
    groups_by_store_split: Counter[tuple[str, str]] = Counter()
    for receipt_group, store_group in group_store.items():
        groups_by_store[store_group] += 1
        split_values = group_splits.get(receipt_group, set())
        if split_values:
            split = next(iter(split_values))
            groups_by_split[split] += 1
            groups_by_store_split[(store_group, split)] += 1

    if require_target_counts:
        expected_total = config["targetReceiptGroups"]
        if len(group_store) != expected_total:
            raise DatasetError(
                f"metadata has {len(group_store)} receipt groups, "
                f"expected {expected_total}"
            )
        for store_group in STORE_GROUPS:
            actual = groups_by_store[store_group]
            expected = config["storeGroups"][store_group]["total"]
            if actual != expected:
                raise DatasetError(
                    f"{store_group} has {actual} receipt groups, "
                    f"expected {expected}"
                )
        if require_split:
            for split in SPLITS:
                actual = groups_by_split[split]
                expected = config["splits"][split]
                if actual != expected:
                    raise DatasetError(
                        f"split {split} has {actual} receipt groups, "
                        f"expected {expected}"
                    )
            for store_group in STORE_GROUPS:
                for split in SPLITS:
                    actual = groups_by_store_split[(store_group, split)]
                    expected = config["storeGroups"][store_group][split]
                    if actual != expected:
                        raise DatasetError(
                            f"{store_group}/{split} has {actual} groups, "
                            f"expected {expected}"
                        )

    return {
        "images": len(records),
        "receiptGroups": len(group_store),
        "groupsByStore": {
            store_group: groups_by_store[store_group]
            for store_group in STORE_GROUPS
        },
        "groupsBySplit": {
            split: groups_by_split[split] for split in SPLITS
        },
    }


def write_jsonl(path: Path, records: Iterable[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    file_descriptor, temp_name = tempfile.mkstemp(
        prefix=f".{path.name}.",
        suffix=".tmp",
        dir=path.parent,
        text=True,
    )
    try:
        with os.fdopen(file_descriptor, "w", encoding="utf-8", newline="\n") as handle:
            for record in records:
                handle.write(
                    json.dumps(record, ensure_ascii=False, separators=(",", ":"))
                )
                handle.write("\n")
        os.replace(temp_name, path)
    except BaseException:
        try:
            os.unlink(temp_name)
        except FileNotFoundError:
            pass
        raise


def command_validate(args: argparse.Namespace) -> None:
    config = load_json(args.config)
    validate_config(config, args.config)
    records = load_jsonl(args.metadata)
    summary = analyze_records(
        records,
        config,
        require_target_counts=args.require_target_counts,
        require_split=args.require_split,
    )
    print(json.dumps({"status": "ok", **summary}, indent=2))


def command_split(args: argparse.Namespace) -> None:
    if args.metadata.resolve() == args.output.resolve():
        raise DatasetError("Refusing to overwrite input metadata in place")
    config = load_json(args.config)
    validate_config(config, args.config)
    records = load_jsonl(args.metadata)
    if any("split" in record for record in records):
        raise DatasetError(
            "Input already contains split values; use an unsplit metadata file"
        )
    analyze_records(
        records,
        config,
        require_target_counts=True,
        require_split=False,
    )

    groups_by_store: defaultdict[str, list[str]] = defaultdict(list)
    seen_groups: set[str] = set()
    for record in records:
        receipt_group = record["receiptGroupId"]
        if receipt_group not in seen_groups:
            seen_groups.add(receipt_group)
            groups_by_store[record["storeGroup"]].append(receipt_group)

    group_assignment: dict[str, str] = {}
    seed = config["seed"]
    for store_group in STORE_GROUPS:
        receipt_groups = sorted(groups_by_store[store_group])
        random.Random(f"{seed}:{store_group}").shuffle(receipt_groups)
        offset = 0
        for split in SPLITS:
            count = config["storeGroups"][store_group][split]
            selected = receipt_groups[offset : offset + count]
            group_assignment.update(
                {receipt_group: split for receipt_group in selected}
            )
            offset += count

    output_records = clean_records(records)
    for record in output_records:
        record["split"] = group_assignment[record["receiptGroupId"]]
    output_records.sort(key=lambda item: (item["split"], item["imageId"]))
    write_jsonl(args.output, output_records)

    reloaded = load_jsonl(args.output)
    summary = analyze_records(
        reloaded,
        config,
        require_target_counts=True,
        require_split=True,
    )
    print(
        json.dumps(
            {"status": "ok", "output": str(args.output), **summary},
            indent=2,
        )
    )


def command_make_sample(args: argparse.Namespace) -> None:
    config = load_json(args.config)
    validate_config(config, args.config)
    records: list[dict[str, Any]] = []
    for store_group in STORE_GROUPS:
        count = config["storeGroups"][store_group]["total"]
        for index in range(1, count + 1):
            receipt_group = f"{store_group}_{index:03d}"
            records.append(
                {
                    "imageId": f"{receipt_group}_a",
                    "receiptGroupId": receipt_group,
                    "imagePath": (
                        f"data/full-page/unassigned/{receipt_group}_a.jpg"
                    ),
                    "storeGroup": store_group,
                    "storeName": SAMPLE_STORE_NAMES[store_group],
                    "device": "Synthetic metadata only - no image",
                    "lighting": "indoor",
                    "captureAngle": "straight",
                    "quality": "good",
                    "sensitiveDataRedacted": True,
                    "notes": "Generated metadata fixture; not a real receipt",
                }
            )
    write_jsonl(args.output, records)
    print(
        json.dumps(
            {
                "status": "ok",
                "output": str(args.output),
                "receiptGroups": len(records),
            },
            indent=2,
        )
    )


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Validate receipt metadata and create deterministic, "
            "receipt-group-safe splits."
        )
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    validate_parser = subparsers.add_parser(
        "validate", help="Validate config, metadata, counts, and leakage."
    )
    validate_parser.add_argument("--metadata", type=Path, required=True)
    validate_parser.add_argument("--config", type=Path, required=True)
    validate_parser.add_argument(
        "--require-target-counts",
        action="store_true",
        help="Require all 300 receipt groups and exact store quotas.",
    )
    validate_parser.add_argument(
        "--require-split",
        action="store_true",
        help="Require a split on every row and validate split quotas.",
    )
    validate_parser.set_defaults(handler=command_validate)

    split_parser = subparsers.add_parser(
        "split", help="Create a deterministic split manifest."
    )
    split_parser.add_argument("--metadata", type=Path, required=True)
    split_parser.add_argument("--config", type=Path, required=True)
    split_parser.add_argument("--output", type=Path, required=True)
    split_parser.set_defaults(handler=command_split)

    sample_parser = subparsers.add_parser(
        "make-sample",
        help="Generate metadata-only fixtures matching the configured quotas.",
    )
    sample_parser.add_argument("--config", type=Path, required=True)
    sample_parser.add_argument("--output", type=Path, required=True)
    sample_parser.set_defaults(handler=command_make_sample)

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    try:
        args.handler(args)
    except DatasetError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
