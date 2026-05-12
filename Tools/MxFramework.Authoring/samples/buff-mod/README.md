# 示例 Buff Mod 包

与 `samples/buff-preview/` 平行的 Mod 风格样例：

- `mod.json.kind = "Mod"`
- `gameVersionRange = ">=0.1.0 <1.0.0"`
- `packageId = sample.buff.mod`

用途：

- 给 Authoring CLI / EditorServer / 测试覆盖 `Mod` 模式打底。
- 与 `buff-preview`（Preview 模式）配套，验证 LayeredMerger Base→Patch→Mod 行为。

请勿在此包写入 `Base` 层 Patch（Validator 会阻断）。
