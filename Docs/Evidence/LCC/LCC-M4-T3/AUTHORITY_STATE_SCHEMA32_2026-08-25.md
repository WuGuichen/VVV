# LCC-M4-T3 Authority Terrain Pose / Schema 32 Evidence — 2026-08-25

## 结论

T3生产实现、formal schema 32与15项专属EditMode用例已完成，可交接主代理。实现只增加CharacterTest专属四肢terrain authority链与一个固定容量resolved frame；复用T1 rig/base bake、既有fixed-point two-bone solver、T2 `QueryGroundProbe`、现有component world与network snapshot/replay链。没有新增通用IK图、状态机、pose graph、第二套snapshot/revision、动态tick容器、BigInteger、HTTP或Unity Physics/Animator/Transform authority。

唯一 integrated `validate_unity_scripts`在domain reload期间发生Unity plugin session断线，失败原证据保留；未循环重调validator。按根`AGENTS.md`失败回退后完成scripts refresh/domain reload、重连后的真实EditMode执行及Console复查，fallback Unity gate为green。

## 实际生产链与空间合同

每个authority tick的唯一顺序：

```text
baked ModelRoot-local base position+basis
  -> tip-channel-local probe origin/axis
  -> authority root position/Yaw一次转world
  -> T2 quantized single-best ground probe
  -> target/normal按逆Yaw一次转回ModelRoot
  -> fixed-point two-bone terrain position+basis
  -> non-conflicting secondary translation
  -> fixed-capacity CharacterTestResolvedAuthorityPoseFrame
```

- 每角色最多4个limb/probe、最多32个resolved channel sample；frame按定义中已稳定排序的channel id发布。
- root/mid forward沿solved child；bend plane来自`aim`与authored pole，退化只投影ModelRoot `+X`、`+Z`。tip up取ground normal；tip forward依次尝试base tip `+Z`投影、parent forward、ModelRoot `+Z`、`+X`。
- eligibility读取现有确定性`MovementMode`、active action/tag、collider posture、airborne、launch/battle dominant reason、combat alive与Body Part `Severed`状态；不读View接触。
- `DefinitionStale/SourceStale/ChannelConflict/NoHit/TooSteep/OutOfReach/MotionDisabled/PostureDisabled/ActionDisabled/Flying/KnockedBack/Dead/Severed/DegeneratePole/InvalidResolvedBasis/InvalidProbe`均为typed failure。失败按`lostFrames`保留合法历史，再按`exitFrames`逐步回base；miss count按配置上限饱和。
- 世界原点是合法target：历史有效性由显式`LastValidFrame >= 0`且normal合法判断，不由target是否为零判断。

## Schema 32字段与future-state审计

T0核对时formal最高schema为31；T3只新增`AuthorityPoseTerrainStateSchemaVersionValue=32`，未预占33+。

| schema 32字段 | 保留原因 |
| --- | --- |
| `entityId/isConfigured` | 绑定现有entity与配置形状，restore不猜测 |
| `lastPublishedRevision` | 决定下一次resolved frame revision |
| `definitionRevision/contentHash` | rig definition source witness；不一致fail closed |
| `baseSourceRevision/contentHash` | baked base channel source witness；不一致fail closed |
| limb `ordinal/probeId/ikBindingId/partStateId` | 稳定匹配既有4-slot配置，禁止按数组位置猜测 |
| `lastValidFrame`、`consecutiveMissCount` | 显式历史有效性与有界lost/release迟滞 |
| `lastValidTargetWorld`、`lastValidNormalWorld` | rollback后一帧继续相同retained-target solve；零点target合法 |
| surface entity/body/collider/leaf/shape与material raw fields | 保留T2量化probe witness，不从View或碰撞世界猜测/重建旧命中 |
| `currentWeight` | rollback后一帧继续相同enter/release混合 |

`CurrentValid`与`CurrentFailure`仅是当帧derived diagnostics；`ConsecutiveValidCount/TargetWeight/ReleaseProgress/WasEligible/LastEligibilityTransitionFrame`在future-state usage审计中没有下一tick读取，未进入生产state或schema。base samples、world probe origin、当帧hit、solved mid/tip与resolved samples都可重算，明确不进入wire。restore保留future history与revision，但清空derived resolved frame，从base重新发布。

capture/apply/clone、canonical limb order、full/owned/non-owned hash均包含上述future state。schema 0～31沿现有legacy路径将terrain history归零；最终补回既有支持列表中遗漏的schema27一行，并以代表性schema27与schema31迁移验证。schema33、envelope/document不一致与definition/base source mismatch均失败关闭。

## Geometry v6资源身份窄修

首次replay job `258de209d7974fe08ed00468479f52f3`的3项均在bootstrap前失败，原因为生产Dragon14已是`mx.characterGeometryBinding.v6/6.0`，而既有`ValidatePlayableCharacterAuthorityPackage`只接受v1～v5：

```text
Playable character package authority identity rejected.
characterId=634812596
packageRoot=Assets/MxFrameworkGenerated/CharacterPackages/000003
reason=geometry format/schema is invalid
```

经主代理确认后只做现有函数内的additive窄修：v6沿用v5 Body Presence与Body-Part Damage校验，并额外要求`AuthorityPoseRig.IsAuthored`、`revision>0`、合法`sha256:` content hash；canonical直接追加rig format/schema/revision/contentHash四字段。没有新增兼容层或公共抽象。actual Dragon v6通过，Missing rig及非法hash均fail closed，rig revision/content hash变化会改变playable catalog identity。

## 聚焦测试原始结果

最终terrain job：`38a0d82a1f7742089d686e269bcf1afc`

```text
mode=EditMode
summary.total=6
summary.passed=6
summary.failed=0
summary.skipped=0
summary.durationSeconds=0.3666048
resultState=Passed
```

覆盖single-yaw/world↔ModelRoot一次变换、position+basis冻结规则、reachable/out-of-reach、degenerate pole、mirror、motion/posture/flying/launch/dead、真实action tag与Severed source、definition/base source stale、no-hit loss/recovery、有界miss、合法世界原点target、稳定channel order与4-limb warmed tick 0 B。

最终network job：`3dca1b0c96f4465f9cc85141cef486c5`

```text
mode=EditMode
summary.total=6
summary.passed=6
summary.failed=0
summary.skipped=0
summary.durationSeconds=3.2845465
resultState=Passed
```

覆盖schema32 capture/apply/deep clone、wire中无derived字段、零点target roundtrip、full/owned/non-owned hash ownership、definition/base source mismatch、future schema、schema27/schema31 legacy defaults、rollback+1实际probe/state/resolved/hash parity、Dragon14四肢late join，以及geometry v6 rig identity通过/变化/fail-closed。

最终replay job：`85b99cf28bfd4eeb87360e9bff03247e`

```text
mode=EditMode
summary.total=3
summary.passed=3
summary.failed=0
summary.skipped=0
summary.durationSeconds=5.7313778
resultState=Passed
```

覆盖schema32 Dragon severed bootstrap、derived frame不入wire且restore后清空、rollback+1 command/result hash parity、future schema与envelope/document schema mismatch fail-closed。

最小既有回归job：`95fe8d93fffa45c69484fa637ad13457`

```text
CharacterAuthorityPoseTests=5/5 passed
CharacterTestBodyPartStateNetworkTests.Schema31_CaptureCanonicalCloneAndOwnershipHashes_CoverAllAuthorityFields=1/1 passed
summary.total=6
summary.passed=6
summary.failed=0
summary.skipped=0
summary.durationSeconds=1.285559
```

## Unity脚本验证原证据与fallback

唯一 integrated调用：`mcp__unityMCP__validate_unity_scripts({})`

```text
success=false
error=Unity plugin session 70048178-a331-4114-aeb5-4a0e51c8b71e disconnected while awaiting command_result
```

未第二次调用validator。失败后的分步证据：

```text
refresh_unity(mode=force, scope=scripts, compile=request, wait_for_ready=true)
success=true, compile_requested=true

editor_state.activity.phase=idle
editor_state.compilation.is_compiling=false
editor_state.compilation.is_domain_reload_pending=false
editor_state.compilation.last_domain_reload_after_unix_ms=1787625399864

post-reconnect proof test job=78b67f4b5dd640b98560a4b8f0a1cb5c
summary=1/1 passed, durationSeconds=0.2882359
```

重连proof test后Console仅出现Unity Test Runner自身的临时条目：

```text
Saving results to: C:/Users/WGH/AppData/LocalLow/MxFramework/WGameFramework\\TestResults.xml
```

该原始事实先记录，经主代理明确同意后只清理Console显示；最终`read_console(types=[error])`返回`total=0`。没有隐藏C#编译或测试异常。

## 文件、规范、未执行项与回滚

生产与测试owner：

- `Assets/Scripts/MxFramework/Game/CharacterTest/Combat/CharacterTestAuthorityPose.TerrainIk.cs`及meta
- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/World/CharacterTestTerrainIkStateComponent.cs`及meta
- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/World/CharacterTestResolvedAuthorityPoseFrame.cs`及meta
- `Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/Systems/Character/CharacterTestAuthorityPoseTerrainSystem.cs`及meta
- `Assets/Scripts/MxFramework/Game/CharacterTest/Network/Snapshot/CharacterTestNetworkAuthorityPoseSnapshot.cs`及meta
- `CharacterTestComponentWorld.cs`、`CharacterTestCharacterEntity.cs`、`CharacterTestCharacterRuntimeModule.cs`、`CharacterTestNetworkSnapshot.cs`的T3 partial
- 经批准的`CharacterTestNetworkSessionFactory.cs` v6资源身份partial
- `CharacterTestAuthorityPoseTerrainTests.cs`、`CharacterTestAuthorityPoseNetworkTests.cs`及meta；`CharacterTestReplayTests.cs`的`AuthorityPoseSchema32_*`增量
- T3任务记录与本文

changed-path standards report-only扫描13个C#文件，0 finding；scoped `git diff --check`无输出；禁止项扫描未发现BigInteger、HTTP、UnityEditor、Unity Physics、`Quaternion.LookRotation`或LINQ。

未跑全仓测试、PlayMode、`.csproj` build、8-character/T5性能、T4 consumer、独立终审，也未stage/commit/push/clean。T3不证明Hurtbox/socket/contact/View已消费resolved frame；这些仍属于T4。

回滚时只删除5个T3新增生产C#及meta、2个T3新增测试及meta和本Evidence，并撤销上述4个runtime/network partial、replay T3增量、schema32/legacy27行与经批准的v6资源身份partial。不要触碰共享dirty worktree、T1/T2产物或任何T4 consumer。
