#!/usr/bin/env python3
"""角色包导出体积与布局测量。

用途（对应《角色包导出 JSON 精简实施方案》§5 A0 与 §9.4）：
  * 按类别统计导出目录的 JSON / 非 JSON 字节与文件数；
  * 统计进入 Player 暂存的集合（全部 .json + .bytes，排除已废弃快照与摘要）；
  * 依据 import_report.json 的 resourceFile 集合列出未声明的孤立资源候选；
  * 输出可与优化前基线逐项对比的稳定清单。

用法：
  python scripts/measure_character_package_export.py <生成角色包目录> [--json] [--label <名称>]

退出码：0 正常；2 参数或目录错误。
"""

import argparse
import json
import os
import sys

# 与 CharacterTestPackageTestCommand.ExcludedPlayerGeneratedPackageFiles 保持一致。
EXCLUDED_PLAYER_FILES = (
    "package_cache/compile_result.json",
    "package_cache/animation_compile_result.json",
    "package_cache/compile_summary.json",
    "generated/character_application_config_patch.json",
    "generated/character_geometry_binding.json",
    "generated/character_resource_mapping.json",
)

# 与 CharacterPackageExportCleanup.OrphanScanRoots 保持一致。
ORPHAN_SCAN_ROOTS = ("resources", "previews")

# 与 CharacterPackageExportCleanup 的固定废弃路径保持一致。
DEPRECATED_FIXED = (
    "package_cache/compile_result.json",
    "package_cache/animation_compile_result.json",
    "generated/character_application_config_patch.json",
    "generated/character_geometry_binding.json",
    "generated/character_resource_mapping.json",
)


def iter_files(root):
    for dirpath, _dirnames, filenames in os.walk(root):
        for name in filenames:
            full = os.path.join(dirpath, name)
            yield os.path.relpath(full, root).replace("\\", "/"), full


def read_import_report(root):
    path = os.path.join(root, "package_cache", "import_report.json")
    if not os.path.exists(path):
        return None
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def declared_resource_paths(report, root):
    """按包根前缀剥离 import_report 的 resourceFile 目标路径。"""
    declared = set()
    if not report:
        return declared

    prefix = os.path.relpath(root, os.path.dirname(root)).replace("\\", "/")
    for operation in report.get("operations", []):
        if operation.get("kind") != "resourceFile":
            continue
        target = (operation.get("targetPath") or "").replace("\\", "/")
        if not target:
            continue
        marker = "/" + prefix + "/"
        index = target.find(marker)
        declared.add(target[index + len(marker):] if index >= 0 else target)
    return declared


def measure(root):
    files = list(iter_files(root))
    json_files = [(rel, full) for rel, full in files if rel.endswith(".json")]
    bytes_files = [(rel, full) for rel, full in files if rel.endswith(".bytes")]
    meta_files = [(rel, full) for rel, full in files if rel.endswith(".meta")]
    asset_files = [(rel, full) for rel, full in files if not rel.endswith((".json", ".bytes", ".meta"))]

    def total(entries):
        return sum(os.path.getsize(full) for _rel, full in entries)

    def by_category(entries, category):
        selected = [(rel, full) for rel, full in entries if rel.startswith(category + "/")]
        return len(selected), total(selected)

    report = read_import_report(root)
    declared = declared_resource_paths(report, root)

    all_stageable = json_files + bytes_files
    staged = [(rel, full) for rel, full in all_stageable if rel not in EXCLUDED_PLAYER_FILES]
    excluded_present = [
        rel for rel in EXCLUDED_PLAYER_FILES
        if any(candidate == rel for candidate, _full in all_stageable)
    ]

    orphans = []
    for scan_root in ORPHAN_SCAN_ROOTS:
        for rel, full in files:
            if not rel.startswith(scan_root + "/") or rel.endswith(".meta"):
                continue
            if rel in declared:
                continue
            orphans.append((rel, os.path.getsize(full)))

    deprecated_present = [
        rel for rel in DEPRECATED_FIXED
        if any(candidate == rel for candidate, _full in files)
    ]

    result = {
        "packageRoot": root.replace("\\", "/"),
        "fileCount": {
            "json": len(json_files),
            "bytes": len(bytes_files),
            "asset": len(asset_files),
            "meta": len(meta_files),
            "totalNonMeta": len(json_files) + len(bytes_files) + len(asset_files),
        },
        "bytes": {
            "json": total(json_files),
            "bytes": total(bytes_files),
            "asset": total(asset_files),
            "meta": total(meta_files),
            "totalNonMeta": total(json_files) + total(bytes_files) + total(asset_files),
        },
        "jsonByCategory": {
            category: {"count": by_category(json_files, category)[0], "bytes": by_category(json_files, category)[1]}
            for category in ("config", "generated", "package_cache")
        },
        "playerStaging": {
            "fileCount": len(staged),
            "bytes": total(staged),
            "unfilteredFileCount": len(all_stageable),
            "unfilteredBytes": total(all_stageable),
            "excludedPresent": sorted(excluded_present),
        },
        "deprecatedPresent": sorted(deprecated_present),
        "declaredResourceCount": len(declared),
        "orphanCandidates": sorted(
            ({"path": rel, "bytes": size} for rel, size in orphans),
            key=lambda item: item["path"],
        ),
        "orphanBytes": sum(size for _rel, size in orphans),
        "hasCompileSummary": os.path.exists(os.path.join(root, "package_cache", "compile_summary.json")),
    }
    return result


def render(result, label):
    lines = []
    lines.append("== 角色包导出测量: %s" % (label or result["packageRoot"]))
    lines.append("  路径: %s" % result["packageRoot"])
    lines.append("  文件数: json=%d bytes=%d asset=%d meta=%d 非 meta 合计=%d"
                 % (result["fileCount"]["json"], result["fileCount"]["bytes"], result["fileCount"]["asset"],
                    result["fileCount"]["meta"], result["fileCount"]["totalNonMeta"]))
    lines.append("  字节:   json=%d bytes=%d asset=%d meta=%d 非 meta 合计=%d"
                 % (result["bytes"]["json"], result["bytes"]["bytes"], result["bytes"]["asset"],
                    result["bytes"]["meta"], result["bytes"]["totalNonMeta"]))
    for category, stats in result["jsonByCategory"].items():
        lines.append("  JSON %-14s n=%-3d bytes=%d" % (category, stats["count"], stats["bytes"]))
    staging = result["playerStaging"]
    lines.append("  Player 暂存: 过滤前 n=%d bytes=%d -> 过滤后 n=%d bytes=%d"
                 % (staging["unfilteredFileCount"], staging["unfilteredBytes"],
                    staging["fileCount"], staging["bytes"]))
    lines.append("    当前存在的排除项: %s" % (", ".join(staging["excludedPresent"]) or "无"))
    lines.append("  已声明资源: %d" % result["declaredResourceCount"])
    if result["orphanCandidates"]:
        lines.append("  孤立资源候选: %d 个 / %d 字节" % (len(result["orphanCandidates"]), result["orphanBytes"]))
        for item in result["orphanCandidates"]:
            lines.append("    - %s (%d)" % (item["path"], item["bytes"]))
    else:
        lines.append("  孤立资源候选: 无")
    lines.append("  废弃固定路径仍存在: %s" % (", ".join(result["deprecatedPresent"]) or "无"))
    lines.append("  轻量摘要存在: %s" % ("是" if result["hasCompileSummary"] else "否"))
    return "\n".join(lines)


def main(argv):
    parser = argparse.ArgumentParser(description="角色包导出体积与布局测量")
    parser.add_argument("package_root", help="生成角色包目录（含 config/、package_cache/ 等）")
    parser.add_argument("--json", action="store_true", help="输出机器可读 JSON")
    parser.add_argument("--label", default="", help="报告中显示的标签")
    args = parser.parse_args(argv)

    if not os.path.isdir(args.package_root):
        sys.stderr.write("包目录不存在: %s\n" % args.package_root)
        return 2

    result = measure(os.path.abspath(args.package_root))
    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True))
    else:
        print(render(result, args.label))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
