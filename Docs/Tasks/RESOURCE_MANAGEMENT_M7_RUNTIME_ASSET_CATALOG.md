# Resource Management M7: Runtime Asset Catalog Binding

> 状态：Planned
> 日期：2026-05-10
> 优先级：P1
> 前置任务：M6A Preload Group + Scene Warmup、M6B Variant Catalog + Retain Policy、Runtime Resource Migration 01

## 目标

M7 把资源系统从“核心可用”推进到“Demo 场景真实使用”：为运行时 UI、调试材质、配置和未来 prefab 建立可生成、可校验、可预加载的 Catalog，并用 M6A/M6B 的 warmup、variant 和 retain 策略驱动 `RuntimeVerticalSlice`。

## 范围

- 为 `Assets/UI/MxFramework/Showcase`、`Assets/Art/MxFramework/Showcase`、`Assets/Config/MxFramework` 中适合运行时加载的资源定义 catalog entry。
- 保持 `Assets/Resources` 不作为长期资源目录；`ResourcesProvider` 只保留给兼容和测试。
- 增加 Editor 侧 catalog 生成入口，输出 schema v1 JSON，包含 `variant`、`labels`、`providerData`。
- 在 `RuntimeVerticalSlice` 场景启动时创建 warmup plan，按 label 预加载 HUD、常用材质和未来扩展资源。
- 场景退出时释放 group；短时间重复进入场景时通过 `Timed` retain 避免 asset churn。

## 关键规则

- Catalog label 表示逻辑资源分组，同一资源的多个 variant 可共享 label；实际变体由 `ResourceVariantProfile` 解析。
- 真实运行时路径优先走 AssetBundle / StreamingFile / RemoteBundle；不得为了方便重新把 Demo 资产塞回 `Assets/Resources`。
- `RetainPolicy` 只影响底层 loaded record，调用方 release handle 后 handle 仍为 released。
- Addressables 仍保持可选适配器，不作为 M7 默认依赖。

## 验收

- `RuntimeVerticalSlice` 能通过 ResourceManager warmup 所需运行时资源。
- Catalog 校验能发现非法 provider、非法 address、重复 `id + type + variant`、缺失依赖。
- M6A/M6B 资源测试继续通过。
- Unity Console 无编译错误、测试清理错误。

## 建议测试

```text
dotnet build MxFramework.Tests.csproj --no-restore
Unity EditMode: MxFramework.Tests.Resources
Unity EditMode: MxFramework.Tests.Combat.RuntimeCombatShowcaseRunnerTests
```
