# Resource Management M6 Closeout

> 状态：Accepted / Closed
> 日期：2026-05-10
> 范围：M6.0 / M6A / M6B / M6C 已完成；M6D Addressables Adapter 后置为可选项

## 结论

M6 主线收口，不继续把 Addressables 作为必选阶段推进。

当前资源系统已经具备可用闭环：

- Catalog schema 已预留 `variant` 和 `providerData`。
- Preload Group / Scene Warmup 已可用于场景切换和 UI warmup。
- Variant Profile 已可显式控制平台、画质、语言 fallback。
- RetainPolicy 已可处理短时间重复加载 / 卸载造成的 asset churn。
- RemoteBundle Provider 已具备 file/local HTTP、cache hit、SHA-256 校验和结构化错误的最小闭环。
- Diagnostics 可观察 loaded、ref-count、retained、evictable、pinned、recent errors 和 recent evictions。

Addressables Adapter 保持 Deferred / Optional。只有目标项目已经安装并决定使用 Addressables Groups、Profile、Remote Catalog 或 Content Update 时，才新增独立程序集 `MxFramework.Resources.Addressables`。

## 当前推荐接入路线

```text
基础 Demo / 小样例
  -> ResourcesProvider

常规运行时资源
  -> AssetBundleProvider

场景切换 / 战斗开场 / UI 打开前
  -> ResourcePreloadService

平台 / 画质 / 语言差异
  -> ResourceVariantProfile

常用 UI 图标 / 通用材质 / 高频特效
  -> ResourceRetainPolicy.Timed or KeepAlive

Mod / DLC / 外部包下载
  -> RemoteBundleProvider

项目已经强依赖 Unity Addressables
  -> Optional Addressables Adapter in separate asmdef
```

## Provider 选择规则

| 场景 | 默认选择 | 不推荐 |
| --- | --- | --- |
| EditMode / noEngine 测试 | `MemoryResourceProvider` | Unity Provider |
| Demo、小型固定资源 | `ResourcesProvider` | 大量内容放入 `Resources` |
| 正式本地运行时资源 | `AssetBundleProvider` | 业务代码直接拼路径 |
| 外部包、Mod、DLC 最小闭环 | `RemoteBundleProvider` | 直接暴露下载路径给 Gameplay |
| 已有 Addressables 项目 | 可选 `AddressablesProvider` | 把 Addressables 变成框架硬依赖 |

## Addressables 决策

Addressables 不是“只有利没有弊”：

- 它能提供 Unity 官方 Groups、Profile、Remote Catalog 和 Content Update 工作流。
- 它也会带来双重 Catalog、双重 handle、双重 ref-count，以及额外 package 依赖。
- 对外部 Mod Package 自带 Catalog + Bundle 的路线，Addressables 默认工作流不一定是最短路径。

因此 MxFramework 的规则是：

- `MxFramework.Resources` 不依赖 Addressables。
- `MxFramework.Resources.Unity` 不依赖 Addressables。
- Addressables 只能在独立可选程序集接入。
- 调用方仍然只接触 `ResourceKey` / `ResourceHandle<T>` / `IResourceManager.Release`。
- 业务代码不得直接持有或释放 Addressables internal handle。

## 验收记录

M6 期间已验证：

```text
dotnet build MxFramework.Tests.csproj --no-restore
Unity EditMode: MxFramework.Tests.Resources.ResourcePreloadServiceTests, 5/5 passed
Unity EditMode: MxFramework.Tests.Resources.ResourceManagerTests, 15/15 passed
Unity EditMode: MxFramework.Tests.Resources.RemoteBundleProviderTests, 4/4 passed
Unity EditMode: MxFramework.Tests.Resources, 40/40 passed
影响面检查完成
```

## 后续建议

短期优先做：

- 继续用现有 ResourceManager 接入 Runtime Vertical Slice 或 Preview 的默认配置、图标、文本和 prefab；首批 Runtime HUD UI 资源已在 `RESOURCE_MANAGEMENT_RUNTIME_RESOURCE_MIGRATION_01.md` 中完成迁移，Combat debug material 改为运行时生成以减少 `Resources` 内容。
- 给 Demo/Preview 增加一个真实 warmup plan，验证 Preload + Retain 的使用体验。
- 补 Editor Catalog builder，减少手写 Catalog JSON 成本。

暂不做：

- Addressables Adapter，除非项目安装并决定使用 Addressables。
- RemoteBundle 重试、断点续传、签名和 CDN 发布工具。
- 内存预算驱逐，除非已有实际峰值内存数据。
