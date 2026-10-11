# UPM-P2 CharacterTest Project Root Decision

> Status: Current
>
> Decision: Accepted for UPM-P2
>
> Date: 2026-08-26
>
> P1 package tree input: `85a60bf29fd0433b0da4b57dcd02f4cf05fc45d228159da7cac375879dd7a677`
>
> Implementation state: Applied and remotely validated；UPM-P2 is `Complete`

## Selected root

UPM-P2 使用以下唯一 project-owned 根：

```text
Assets/Scripts/WGameFrameworkProject/CharacterTest/
  <MxFramework.Game.CharacterTest runtime assembly>
  Editor/
  Tests/EditMode/
  Tests/PlayMode/
```

`Assets/Scripts/WGameFrameworkProject/` 是 repository validation owner，不属于 `com.vincent.wgameframework` package root。CharacterTest assembly 名称、namespace、`.meta`、GUID 和序列化引用在移动中保持不变。

## Assembly closure

必须整体迁移：

1. `MxFramework.Game.CharacterTest` 及 `Game/CharacterTest/` 全部内容；
2. `MxFramework.Game.CharacterTest.Editor` 及 `Game/CharacterTest.Editor/` 全部内容；
3. `MxFramework.Tests.PlayMode.CharacterTest` 及对应 PlayMode fixture；
4. `MxFramework.Tests` 聚合程序集拥有的完整编译闭包。

第 4 项虽然包含部分通用 Editor tests，仍必须整体迁移：该程序集直接引用 CharacterTest/Editor，且大量 framework assembly 通过 `InternalsVisibleTo("MxFramework.Tests")` 暴露内部测试面。拆成新 assembly 会改变 friend identity、覆盖和大量源码；该拆分属于 UPM-P3，不在 P2 中猜测完成。P2 只把现有 aggregate identity 作为 repository-validation owner 移出 package。

保持在 package root、留给 P3 处理：

- `MCPForUnityTests.EditMode`；
- `MxFramework.Tests.Application.Unity.Editor`；
- `MxFramework.Tests.Persistence.RuntimeBridge`；
- 除 CharacterTest 外的独立 PlayMode test assemblies；
- `MxFramework.Tests.UI.FairyGUI`。

## Direct project resources

CharacterTest 使用的 `Assets/Scenes/`、`Assets/Config/`、`Assets/StreamingAssets/`、`Assets/MxFrameworkGenerated/`、`FGUIProject/` 和 `Tools/` 已位于 package root 外，本阶段只在移动清单中登记 owner/reference，不为目录整齐复制或搬迁这些共享/生成权威。

只有当前位于 `Game/CharacterTest*` 下的代码、UI 文本资产和程序集内容随 assembly root 移动。

## Dependency rule

移动后允许：

```text
WGameFrameworkProject CharacterTest/Tests -> MxFramework package assemblies
WGameFrameworkProject repository-validation -> WGameFrameworkProject CharacterTest
```

禁止：

```text
MxFramework package assembly -> WGameFrameworkProject CharacterTest/Tests
```

所有仍位于 package root 的 asmdef 必须在 strict E0 中保持 0 `surface-owner-dependency`。

## Rejected alternatives

- 永久保留 `Game/CharacterTest`：继续把项目游戏随基础包安装，违反 accepted surface owner。
- 改名 CharacterTest assemblies：会破坏 asmdef identity、friend assembly、测试和外部引用，不属于目录迁移。
- 在 P2 立即拆分 `MxFramework.Tests`：会混入 P3 的 package-test/repository-test 决策，并要求大规模 `InternalsVisibleTo` 迁移。
- 搬迁所有 `Assets/Scenes`/`Assets/Config`：这些已经是 package 外 project owner，移动只会制造无收益 GUID/path churn。

## Rollback boundary

每个移动组必须保留 source/target、asset/meta GUID 和内容 hash。任何 GUID、asmdef identity、序列化引用或 Unity compile 回归，完整恢复最近一组及其 parent-folder meta；不得只恢复源码或重新生成 GUID。
