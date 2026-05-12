# Mod Package 04：多包 Runtime Patch 合并闭环

> **状态**: ✅ 已完成（r1175）
> **优先级**：P0
> 前置任务：`MOD_PACKAGE_03_PACKAGE_CATALOG_LOAD_PLAN.md`
> 目标版本：Phase 10.8

## 目标

在 Package Catalog / LoadPlan 已完成的基础上，让运行时可以按加载计划合并多个 Mod Package 的 Runtime Patch，并输出可解释的合并报告。

目标链路：

```text
Package containers
  -> RuntimeModPackageDiscovery.Discover()
  -> RuntimeModPackageLoadPlanBuilder.Build()
  -> Ordered packages
  -> Collect Runtime Patch entries
  -> RuntimeConfigPatchMerger
  -> Merged ConfigRegistry
  -> RuntimeVerticalSlice / PreviewWorld
  -> Merge report
```

这一步完成后，框架才真正具备“多个包共同影响运行时配置”的基础能力。仍然不做玩家 UI、冲突解决界面、依赖关系和发布平台。

## 背景

当前已经完成：

- 单包 `RuntimeModPackageLoader.LoadFromDirectory()`。
- 多容器扫描 `RuntimeModPackageDiscovery.Discover()`。
- 确定性排序 `RuntimeModPackageLoadPlanBuilder.Build()`。
- `RuntimeConfigPatchMerger.Merge()` 已支持按 patch 顺序合并单表配置。

缺口是：LoadPlan 目前只说明“哪些包准备加载、顺序是什么”，还没有把这些包真正应用到基础配置上，也没有报告包之间覆盖了哪些行。

本任务只做 v0 合并策略：

- 按 LoadPlan 顺序应用。
- 同一 table/id 被后面的包修改时，后者生效。
- 所有覆盖、删除、noop 都进入报告。
- 不在运行时弹窗让用户选择冲突处理。

## 范围

### 必须完成

1. 新增多包合并入口。

建议放在 `MxFramework.Config.Runtime`：

```text
RuntimeModPackagePatchMerger.cs
RuntimeModPackageMergeReport.cs
```

推荐 API：

```csharp
public static RuntimeModPackageMergeResult Merge(
    RuntimeModPackageLoadPlan loadPlan,
    IConfigProvider baseRegistry)
```

如果当前 Demo 只支持 `BasicBuffConfig` / `BasicModifierConfig`，v0 可以先固定这两类配置，但 API 设计不要把类型名写死到报告格式里。

2. 明确合并输入。

输入来自 `RuntimeModPackageLoadPlan.OrderedItems`：

- 只处理 valid + enabled + ordered 包。
- skipped 包不参与合并，但进入报告摘要。
- 每个 ordered item 应能拿到 `RuntimeConfigPatchBundle`。
- 如果 catalog item 当前只保存 manifest，不保存 load result，则本任务可以补充必要字段或在合并阶段重新通过 `RuntimeModPackageLoader.LoadFromDirectory()` 加载。

要求：

- 合并阶段不能因为单个包失败而静默跳过。
- 如果 ordered 包在合并阶段加载失败，整体 merge result 应失败，并报告 packageId/path/error。

3. 固定 v0 合并策略。

策略：

```text
Base rows
  -> package[0] patches
  -> package[1] patches
  -> ...
  -> package[n] patches
```

规则：

- 后加载包覆盖先加载包。
- `Remove` 删除当前已存在行；如果不存在，记为 `Noop`。
- `Upsert` 新增或替换行。
- 同一 table/id 被多个包触碰时，记录 override chain。
- 不允许不同 table 共用 id 被误判为冲突，冲突 key 必须包含 table/config type + id。

4. 输出合并报告。

建议报告类型包含：

```csharp
public sealed class RuntimeModPackageMergeReport
{
    public IReadOnlyList<RuntimeModPackageMergePackageReport> Packages { get; }
    public IReadOnlyList<RuntimeModPackageOverrideRecord> Overrides { get; }
    public IReadOnlyList<string> Errors { get; }
    public int AppliedPackageCount { get; }
    public int SkippedPackageCount { get; }
}
```

报告至少要能回答：

- 加载了哪些包。
- 跳过了哪些包。
- 每个包贡献了多少 buff patch / modifier patch。
- 哪些 table/id 被多个包覆盖。
- 最终哪个 packageId 生效。
- 是否有错误。

5. 生成合并后的运行时 registry。

v0 至少输出：

- 合并后的 `ConfigTable<BasicBuffConfig>`。
- 合并后的 `ConfigTable<BasicModifierConfig>`。
- 可直接注册到 `ConfigRegistry`。

推荐结果类型：

```csharp
public sealed class RuntimeModPackageMergeResult
{
    public bool Success { get; }
    public ConfigRegistry Registry { get; }
    public RuntimeModPackageMergeReport Report { get; }
}
```

6. RuntimeVerticalSlice 集成。

复用 `RuntimeVerticalSlice.unity`，不新增场景。

建议在 `RuntimeVerticalSliceRunner` 中增加模式：

```csharp
[SerializeField] private bool _useModPackageLoadPlanMerge;
```

行为：

- 扫描 demo 容器目录。
- 构建 load plan。
- 合并 ordered packages。
- 使用合并后的 registry 创建 `ConfigBuffFactory` / `ConfigModifierFactory`。
- 在 OnGUI 或日志显示 merge report 摘要。

7. 文档更新。

至少更新：

- `Docs/CAPABILITIES.md`
- `Docs/USAGE.md` 或 `Docs/Interfaces/Config.md`

说明：

- 单包加载、catalog/load plan、多包 merge 三者边界。
- v0 是 deterministic last-write-wins，不是最终冲突解决系统。

### 不做

- 不做冲突解决 UI。
- 不做依赖关系和 load priority 字段。
- 不做玩家 Mod 管理界面。
- 不做 zip、签名、发布平台。
- 不做资源包加载。
- 不做热更新下载。
- 不引入真实 WGame 数据。
- 不新增 Unity 场景。

## 建议实现

### 1. 复用现有单表合并器

不要重写 `RuntimeConfigPatchMerger.Merge()`。多包合并器只负责：

1. 从 LoadPlan 按顺序收集所有 bundle。
2. 拼接 `ModifierPatches`。
3. 拼接 `BuffPatches`。
4. 调用现有单表 `RuntimeConfigPatchMerger.Merge()`。
5. 根据 change set 和 package 顺序构建报告。

### 2. Override 记录

建议按以下 key 统计覆盖：

```text
configTypeFullName + ":" + id
```

当同一 key 被多个 package 触碰时，生成 override record：

```text
BasicBuffConfig:100001
  sample.base -> sample.mod.a -> sample.mod.b
  winner: sample.mod.b
```

`Remove` 也算触碰。

### 3. 错误语义

合并阶段的错误不要伪装成 skipped：

- LoadPlan 生成前发现的 invalid/disabled 包：skipped。
- LoadPlan ordered 包在 merge 阶段读取失败：merge error，`Success = false`。

这样可以区分“用户没启用/包无效”和“计划已经决定加载但执行失败”。

### 4. Demo 样例

建议至少准备两个 demo package：

```text
Assets/StreamingAssets/MxFramework/Demo/runtime-patch-mod/
Assets/StreamingAssets/MxFramework/Demo/runtime-patch-mod-override/
```

第二个包覆盖同一个 buff 或 modifier，用于验证 last-write-wins 和 override report。

注意：`.meta` 由 Unity 生成。不要手写 Unity 场景 YAML。

## 验收标准

1. 单包 load plan 合并结果与 `MOD_PACKAGE_02` 单包加载结果一致。
2. 两个包修改不同 id 时，最终 registry 同时包含两者变更。
3. 两个包修改同一 table/id 时，后加载包生效。
4. 同一 table/id 被多个包触碰时，报告中有 override record。
5. `Remove` 删除已存在行时，最终 registry 不再包含该行，并记录 change。
6. `Remove` 删除不存在行时，记录 `Noop`，不报错。
7. skipped 包不参与合并，但出现在 report 摘要。
8. ordered 包合并阶段加载失败时，`Success = false` 且有明确 error。
9. `RuntimeVerticalSlice.unity` 可以显示多包 merge 摘要。
10. `AttributeChangedEvent` 在多包合并后的 Buff/Modifier 生效时仍正确发布。
11. Unity EditMode 测试覆盖：单包、多包不同 id、多包同 id 覆盖、remove、skipped、merge error。
12. 实现不引用 Authoring Core / CLI，不引用 `UnityEditor`。

## 推荐测试

- `RuntimeModPackagePatchMerger_SinglePackage_MatchesSingleLoad`
- `RuntimeModPackagePatchMerger_MultiplePackages_DifferentIds_AllApplied`
- `RuntimeModPackagePatchMerger_MultiplePackages_SameId_LastWins`
- `RuntimeModPackagePatchMerger_MultiplePackages_SameId_RecordsOverride`
- `RuntimeModPackagePatchMerger_RemoveExisting_RemovesRow`
- `RuntimeModPackagePatchMerger_RemoveMissing_RecordsNoop`
- `RuntimeModPackagePatchMerger_SkippedPackages_NotAppliedButReported`
- `RuntimeModPackagePatchMerger_OrderedPackageLoadFails_ReturnsError`

测试数据优先使用临时目录，不依赖用户机器绝对路径。

## 完成定义

- 任务状态改为 `✅ 已完成（rXXXX）`。
- 多包 merge API 有 EditMode 测试。
- `RuntimeVerticalSlice.unity` 不新增场景即可查看 merge report 摘要。
- `Docs/CAPABILITIES.md` 更新运行时能力。
- `Docs/USAGE.md` 或 `Docs/Interfaces/Config.md` 说明单包、LoadPlan、多包 merge 的使用边界。
- SVN 提交信息建议：

```text
Add runtime multi-package patch merge
```
