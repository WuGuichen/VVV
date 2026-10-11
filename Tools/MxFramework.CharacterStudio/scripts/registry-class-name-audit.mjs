#!/usr/bin/env node
// 审计 Docs/CHARACTER_SYSTEM_DATA_ARCHITECTURE_REVIEW_SPEC.md §4.2 注册表第 4/5 列
// 引用的类型名是否在仓库源码中真实存在。
//
// 用法:
//   node scripts/registry-class-name-audit.mjs                  # 审计默认文档
//   node scripts/registry-class-name-audit.mjs --doc <path>     # 审计指定文档（用于负样本验证）
//   node scripts/registry-class-name-audit.mjs --json           # 输出机器可读结果
//
// 退出码: 0 = 全部可解析; 1 = 存在不可解析类型名
//
// 口径:
//   - 只从表格第 4/5 列的反引号片段中提取类型名（路径片段含 / 或 . 结尾会被自动排除）。
//   - 点分名称只校验**首段**（外层类型），避免把方法名或文件名后缀误判为类型。
//   - 检索域为仓库内 .cs 源码（Tools/、Packages/、Assets/，排除 Library/Temp/bin/obj/node_modules）。
//   - 显式豁免写在 scripts/registry-class-name-audit.exempt.json。

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "..", "..", "..");

const argv = process.argv.slice(2);
const jsonMode = argv.includes("--json");
const docIndex = argv.indexOf("--doc");
const docPath = docIndex >= 0 && argv[docIndex + 1]
  ? path.resolve(argv[docIndex + 1])
  : path.join(repoRoot, "Docs", "CHARACTER_SYSTEM_DATA_ARCHITECTURE_REVIEW_SPEC.md");

const exemptPath = path.join(scriptDir, "registry-class-name-audit.exempt.json");
const scanRoots = ["Tools", "Packages", "Assets"];
const skipDirs = new Set(["Library", "Temp", "bin", "obj", "node_modules", ".git", "Logs", "UserSettings"]);

function fail(message) {
  if (jsonMode) console.log(JSON.stringify({ ok: false, error: message }, null, 2));
  else console.error(message);
  process.exit(1);
}

if (!fs.existsSync(docPath)) fail(`文档不存在: ${docPath}`);

let exempt = [];
if (fs.existsSync(exemptPath)) {
  try {
    const parsed = JSON.parse(fs.readFileSync(exemptPath, "utf8"));
    exempt = Array.isArray(parsed.exempt) ? parsed.exempt.filter((v) => typeof v === "string") : [];
  } catch (error) {
    fail(`豁免文件解析失败: ${exemptPath} (${error.message})`);
  }
}
const exemptSet = new Set(exempt);

// ---- 1. 解析注册表 ----
const lines = fs.readFileSync(docPath, "utf8").split(/\r?\n/);
const rowPattern = /^\*{0,2}\d+\*{0,2}$/;
const found = new Map(); // name -> [{ row, column }]
let rowCount = 0;

for (const line of lines) {
  if (!line.startsWith("|")) continue;
  const cells = line.replace(/^\||\|$/g, "").split("|").map((c) => c.trim());
  if (cells.length < 5 || !rowPattern.test(cells[0])) continue;
  rowCount += 1;
  const row = cells[0].replace(/\*/g, "");
  [["producer", cells[3]], ["consumer", cells[4]]].forEach(([column, cell]) => {
    for (const span of cell.match(/`([^`]+)`/g) || []) {
      const inner = span.slice(1, -1);
      if (!/^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$/.test(inner)) continue;
      const first = inner.split(".")[0];
      if (!/^[A-Z]/.test(first) || first.length < 4) continue;
      if (!found.has(first)) found.set(first, []);
      found.get(first).push({ row, column });
    }
  });
}

if (rowCount === 0) fail(`未从文档解析到任何注册表行: ${docPath}`);
const names = [...found.keys()].sort();

// ---- 2. 扫描源码 ----
const sourceFiles = [];
const walk = (dir) => {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (skipDirs.has(entry.name)) continue;
      walk(path.join(dir, entry.name));
    } else if (entry.name.endsWith(".cs")) {
      sourceFiles.push(path.join(dir, entry.name));
    }
  }
};
for (const root of scanRoots) {
  const abs = path.join(repoRoot, root);
  if (fs.existsSync(abs)) walk(abs);
}

const combined = new RegExp(`\\b(${names.map((n) => n.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")).join("|")})\\b`, "g");
const resolved = new Set();
for (const file of sourceFiles) {
  let text;
  try {
    text = fs.readFileSync(file, "utf8");
  } catch {
    continue;
  }
  combined.lastIndex = 0;
  for (const match of text.matchAll(combined)) resolved.add(match[1]);
}

// ---- 3. 报告 ----
const unresolved = names.filter((n) => !resolved.has(n) && !exemptSet.has(n));
const skipped = names.filter((n) => !resolved.has(n) && exemptSet.has(n));
const result = {
  ok: unresolved.length === 0,
  doc: path.relative(repoRoot, docPath).replace(/\\/g, "/"),
  rowsParsed: rowCount,
  csFilesScanned: sourceFiles.length,
  namesChecked: names.length,
  unresolved,
  exempted: skipped,
  unresolvableDetail: unresolved.map((n) => ({ name: n, referencedBy: found.get(n) })),
};

if (jsonMode) {
  console.log(JSON.stringify(result, null, 2));
} else {
  console.log(`注册表类型名审计: ${result.doc}`);
  console.log(`  解析行数        = ${rowCount}`);
  console.log(`  .cs 文件扫描数  = ${sourceFiles.length}`);
  console.log(`  待校验类型名    = ${names.length}`);
  console.log(`  已豁免          = ${skipped.length}${skipped.length ? ` (${skipped.join(", ")})` : ""}`);
  console.log(`  不可解析        = ${unresolved.length}`);
  for (const name of unresolved) {
    const refs = found.get(name).map((r) => `#${r.row}/${r.column}`).join(", ");
    console.log(`    - ${name}  <- ${refs}`);
  }
  console.log(result.ok ? "结果: PASS（全部类型名可解析）" : "结果: FAIL（存在不可解析类型名）");
}

process.exit(result.ok ? 0 : 1);
