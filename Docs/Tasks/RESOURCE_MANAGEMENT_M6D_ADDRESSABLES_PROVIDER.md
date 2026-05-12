# Resource Management M6D: Addressables Adapter

> 状态：Deferred / Optional
> 日期：2026-05-10
> 优先级：P3
> 前置任务：项目已安装并决定使用 Addressables

## 结论

Addressables Adapter 不作为框架主路线。

它应当是项目层或独立 asmdef 的可插拔 Provider，不能让 `MxFramework.Resources.Unity` 主程序集硬依赖 Addressables。

当前 M6 主线已通过 M6.0 / M6A / M6B / M6C 收口。由于项目 `Packages/manifest.json` 未安装 Addressables，且当前资源系统已经具备 Catalog、AssetBundle、RemoteBundle、Preload、Variant、Retain 和 Diagnostics 闭环，M6D 不继续实现。

## 独立程序集

建议新增可选程序集：

```text
MxFramework.Resources.Addressables
```

依赖：

```text
MxFramework.Resources
Unity.Addressables
```

不使用 Addressables 的项目不应受影响。

## Provider 设计

```csharp
public sealed class AddressablesProvider : IResourceProvider
{
    public string ProviderId => "addressables";
}
```

Catalog entry 示例：

```json
{
  "id": "ui.icon.fire_burst",
  "type": "Texture2D",
  "variant": "",
  "provider": "addressables",
  "address": "ui/icon/fire_burst",
  "providerData": {
    "releaseMode": "releaseInternalHandle"
  }
}
```

## 关键风险

- 双重 Catalog：MxFramework Catalog 与 Addressables Catalog 谁是权威。
- 双重 handle：`ResourceHandle` 与 Addressables internal handle。
- 双重 ref-count：框架 release 与 Addressables release 必须一一对应。
- Mod Package：外部包自带 Catalog + bundle 时，不一定适合 Addressables 默认工作流。

## 规则

- 调用方只接触 `ResourceHandle<T>`。
- 调用方只调用 `IResourceManager.Release(handle)`。
- 不暴露 Addressables handle。
- Provider 内部负责 Addressables handle 的生命周期。
- Addressables 依赖和 catalog 更新由 Adapter 或项目层处理，不进入 `MxFramework.Resources` 核心。

## 什么时候做

满足以下条件之一再做：

- 目标项目已经大量使用 Addressables。
- 团队决定用 Addressables Groups 管理资源。
- 需要 Unity 官方 profile / remote catalog 工作流。
- 项目希望减少手写 bundle 依赖管理。

若只是“以后可能用”，不做。等目标项目明确采用 Addressables 后，再新增独立程序集 `MxFramework.Resources.Addressables`。

## 不做范围

- 不让主框架强依赖 Addressables package。
- 不重写 ResourceManager。
- 不让业务代码直接操作 Addressables handle。
- 不解决 RemoteBundle Provider 的下载缓存策略。
