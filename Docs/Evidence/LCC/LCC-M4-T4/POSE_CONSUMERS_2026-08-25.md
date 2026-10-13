# LCC-M4-T4 Resolved Authority Pose Consumers Evidence — 2026-08-25

## 结论与生产边界

T4已完成Hurtbox、明确绑定的claw BakedSocket Trace/Planner、PhysicalAudio ContactPoint adapter与Unity View/ProceduralBoneRig的窄接入，状态为`Implementation Complete / Handoff Ready`。Body Presence保持root-only；Projectile/ChainLightning没有T1 attachment，生产行为未改；没有新增通用pose/provider抽象、第二cache/frame/revision或T1～T3算法改动。

本批没有为Dragon14猜测或新增ContactPoint authoring。当前Dragon14为0个`CharacterBodyPartContactPointConfig`和0个`AttachmentKind.ContactPoint`，因此live contact event保持fail closed。四个ground probe只是诊断candidate，不能转成numeric contact/bodyPart身份。

## 唯一帧与调度证据

RuntimeModule只做经批准的窄调度调整：

```text
Character motion tick
  -> TickAuthorityPose(expected RuntimeFrame)
  -> existing SyncBodyPartDerivedTopology(entities, same RuntimeFrame)
  -> perception / later consumers
```

View Bridge的四个实际同步路径都显式传入当前`RuntimeFrame`。Hurtbox、socket/Planner、contact和View只接受以下条件同时成立的T3 sample：

- resolved frame已发布且`RuntimeFrame == expected RuntimeFrame`；
- resolved revision与当前authority state一致；
- rig definition与baked base source的revision/content-hash witness与当前package一致；
- sample `Valid == true`。

`Failure=NoHit/...`但仍`Valid=true`的retained/fading sample和secondary-only sample仍使用当帧resolved结果，不因failure/source mask突然回base。只有invalid、未发布、stale或missing回baked/base；不接受旧帧，不产生零点。

已知性能跟进：Motion内已有一次Hurtbox topology sync，调度窄改后同一tick为同帧authority消费者再同步一次。正确性优先的双sync由主代理批准，成本测量/去重交T5，T4不新建调度层。

## Attachment delta冻结数学

对当前clip/frame的baked base channel position `P_b`、正交basis `B_b`和T3 resolved position/basis `P_r/B_r`，consumer的ModelRoot-local点`x`先转channel-local，再回ModelRoot-local：

```text
q  = transpose(B_b) * (x - P_b)
x' = P_r + B_r * q
a' = B_r * transpose(B_b) * a
```

`a`/`a'`是shape/socket/View的basis或axis。对blend的每个source各自先求delta再blend；角色root position/Yaw在最后只应用一次。View世界位置使用`modelRoot.position + modelRoot.rotation * localPosition`，旋转使用`modelRoot.rotation * localRotation`，不使用`TransformPoint`，因此非1 root scale不会进入authority数学。

狭入口`CharacterTestAuthorityPoseConsumerMath`只返回当次attachment delta，不保存、复制或发布authority frame。Hurtbox、claw Trace、Planner预览、ContactPoint和View复用这一数学入口。

## T1 attachment库存

Dragon14 `authority_pose_rig.json`当前显式attachment为：

| kind | stable id | channel |
| --- | --- | --- |
| Collider | `hurt.left_forelimb.capsule.0` | `left_fore.root` |
| Collider | `hurt.left_forelimb.capsule.1` | `left_fore.mid` |
| Collider | `hurt.right_forelimb.capsule.0` | `right_fore.root` |
| Collider | `hurt.right_forelimb.capsule.1` | `right_fore.mid` |
| Collider | `hurt.left_hindlimb.capsule.0` | `left_hind.root` |
| Collider | `hurt.left_hindlimb.capsule.2` | `left_hind.tip` |
| Collider | `hurt.right_hindlimb.capsule.0` | `right_hind.root` |
| Collider | `hurt.right_hindlimb.capsule.2` | `right_hind.tip` |
| Socket | `leftClawStart` | `left_fore.tip` |
| Socket | `leftClawEnd` | `left_fore.tip` |
| Socket | `rightClawStart` | `right_fore.tip` |
| Socket | `rightClawEnd` | `right_fore.tip` |

`ContactPoint=0`。四个limb/probe的`partStateId`当前都为空；`probeId`/`tipChannelId`只能作诊断witness，不能代替显式contact stable id。

## 消费者行为

| consumer | T4行为 | fail/base行为 |
| --- | --- | --- |
| Hurtbox | sphere/capsule/OBB的point及axis在root前消费同一channel delta；每source先delta后blend | Severed过滤保持优先；disabled/missing/stale/invalid回base |
| claw BakedSocket Trace | 左/右claw的start/end四个显式socket使用同package socket catalog和resolved frame | actual injected source不是`CharacterTestDefaultBakedWeaponTraceSource`时不借用默认catalog，overlay fail-to-base |
| Planner preview | 与实际claw trace复用相同socket frame/catalog/delta | 定制source或未声明attachment保持原行为 |
| Projectile / ChainLightning | 无T1 attachment，没有生产接入 | 未声明不叠加authority delta |
| PhysicalAudio contact | config `StableId` + 显式`AttachmentKind.ContactPoint`同时存在才投影resolved channel | 显式attachment失败用baked/static base；无attachment的PresentationOnly旧路径保持；不反读View |
| Unity View | 仅authored channelId→bonePath；固定12个restore slot；T3已组合的channel不再叠加旧`_authorityPoseSamples`同source secondary | 下帧stale/missing/invalid先恢复animation/base；未受T3控制的PresentationOnly保留且不回写 |
| ProceduralBoneRig | authority bone命中时跳过TwoBoneIK和相交secondary chain的float Transform写入 | 未绑定PresentationOnly/ragdoll路径不受影响 |
| Body Presence | 未改 | 仍仅跟随ModelRoot |

## Hurtbox真实shape数值对照

聚焦复跑job `54512defe8624be3854e0f72026f520b`，frame 72/revision 1，`Fix64.Scale=1,000,000`：

```text
baselineA.raw=(-245000,1692000,1967003)
resolvedA.raw=(3966007,4691000,4244008)
baselineB.raw=(-566001,1957000,1735003)
resolvedB.raw=(3734007,4956000,4565009)
```

折算ModelRoot-local米制值：

- endpoint A: `(-0.245, 1.692, 1.967003)` → `(3.966007, 4.691, 4.244008)`；
- endpoint B: `(-0.566001, 1.957, 1.735003)` → `(3.734007, 4.956, 4.565009)`。

该用例同时断言basis/axis delta、radius不变、root一次变换、Severed tail collider移除与resolved后的15个active leaves。

## 聚焦Unity测试

### Consumer / Contact / View / NoAlloc

job `175d53b2288049be802543a726775088`

```text
mode=EditMode
summary.total=8
summary.passed=8
summary.failed=0
summary.skipped=0
summary.durationSeconds=0.6621207
resultState=Passed
```

覆盖NoHit retained/fade、secondary-only、stale/invalid base fallback、same-frame/revision/source witness、warmed 0 B、注入非默认socket source不借default catalog、View非1 root scale/恢复/防双叠，显式ContactPoint stableId和Dragon14 0/0 fail-closed。

### PhysicalAudio既有路径回归

job `d978842c974a492c84c301e5b4a3c1e7`

```text
mode=EditMode
summary.total=4
summary.passed=4
summary.failed=0
summary.skipped=0
summary.durationSeconds=0.3034737
resultState=Passed
```

覆盖baked center、precompiled tick warmed 0 B、无attachment时旧PresentationPose路径保持，missing frame回base。

### Hurtbox / Trace / Planner / Procedural回归

job `bdb38859199c450590cb99f93f2c87dc`

```text
mode=EditMode
summary.total=11
summary.passed=11
summary.failed=0
summary.skipped=0
summary.durationSeconds=3.1017926
resultState=Passed
```

覆盖Hurtbox topology全类、authority-controlled ProceduralBoneRig float IK/secondary skip，以及既有DirectHitTrace sampler/preview和Planner real/commit路径。

为记录真实shape原始数值，另复跑单项Hurtbox测试：job `54512defe8624be3854e0f72026f520b`，1/1 passed，0 failed/skipped，1.4263098 s。

## Game View / Console初证

在`Assets/Scenes/CharacterTestScene.unity`进入Play，通过现有runtime menu启动游戏并显式解除pause，然后保存以下本地证据：

| 证据 | 尺寸 | SHA-256 |
| --- | --- | --- |
| `C:\UnityProjects\WGameFramework\Temp\CodexEvidence\LCC-M4-T4\LCC-M4-T4-GameView-Running.png` | 2560×1440 | `d170b7b4cccf0b94b1a0b64dc2af77d36eb54e64d2a3fe8231dd68847f8f1987` |
| `C:\UnityProjects\WGameFramework\Temp\CodexEvidence\LCC-M4-T4\LCC-M4-T4-GameView-Dragon14.png` | 1280×720 | `5217b346c46a3bf8526725578725f4bf07041f5ab171b8fcfca4f13e039ccdec` |
| `C:\UnityProjects\WGameFramework\Temp\CodexEvidence\LCC-M4-T4\LCC-M4-T4-SceneView-DebugLayers.png` | 1015×286 | `82841a7b65a57d8d8823eff132176eddb10add889b083ca15d0fa184e3b7167d` |

Game View证明修改后的Dragon14 runtime View可见；Scene View补充截图只展示现有的紫色Hurtbox、青色Presence和橙色root几何层。Play期间只读runtime snapshot为`runtimeFrame=2, dragonEntity=3, poseFrame=1, revision=1, samples=12, baseRevision=101734401, valid=12, terrain=0, secondary=0, noHit=0`；它证明已发布的base authority frame和可见View，不证明terrain overlay已激活。

截图后已退出Play；active scene为`CharacterTestScene`，`isDirty=false`。当时Console error读取为`total=0`。

视觉限制：T4没有新增统一base/resolved/probe target/Hurtbox/socket/contact数值overlay，截图也没有捕获激活terrain sample。不将该初证表述为完整同源overlay或Playable验收；它们交T5。

## Unity脚本最终验证

唯一integrated `execute_custom_tool(tool_name="validate_unity_scripts")`调用成功：job `b152394699e6406ea1093ae198ce54d6`。

```text
success=true
outcome=passed
refresh.requested=true
refresh.completed=true
compilation.requested=true
compilation.started=true
compilation.finished=true
domain_reload_observed=true
editor_ready=true
console.error_count=0
```

该调用完整覆盖refresh、真实compile、domain reload、Editor ready与Console error检查；未触发fallback，也没有重复validator。

## 实际文件与回滚

生产owner：

- 新增`Assets/Scripts/MxFramework/Game/CharacterTest/Runtime/Systems/Character/CharacterTestAuthorityPoseConsumerMath.cs`及meta；
- `CharacterTestCharacterHurtboxSystem.cs`、`CharacterTestCharacterRuntimeModule.cs`；
- `CharacterTestPhysicalAudioSystem.Bindings.cs`、`.Motion.cs`、`.Rules.cs`、`.State.cs`；
- `CharacterTestBakedWeaponTraceSource.cs`、`CharacterTestCharacterCombatTraceSystem.AbilityTimelines.cs`、`CharacterTestRuntimeAiPlannerAttackOpportunityAdapter.cs`；
- `CharacterTestRuntimeCharacterLoader.cs`、`CharacterTestRuntimeCharacterView.cs`、`CharacterTestCharacterUnityViewBridge.cs`；
- `Assets/Scripts/MxFramework/Character.RuntimeSpawn.Unity/CharacterProceduralBoneRig.cs`。

测试owner：

- 新增`CharacterTestAuthorityPoseConsumerTests.cs`及meta；
- `CharacterTestHurtboxTopologyTests.cs`的T4真实shape数值用例；
- `CharacterProceduralBoneRigTests.cs`的authority-controlled IK/secondary skip用例。

文档owner：本Evidence和T4任务记录。没有改父M4、总台账或T5。

回滚时只删除T4 helper/tests及meta，并撤销上述消费者owner中的T4窄hunk和两份T4文档。不回滚T1～T3 producer/schema/fixtures，不碰共享dirty worktree的其它任务修改。

## 未执行与交接限制

- 未跑全仓测试、PlayMode套件、`.csproj` build、8-character/T5性能或独立终审；
- 未stage/commit/push/clean；
- Dragon14当前无可用的live ContactPoint authoring，只用合成显式fixture验证adapter；
- terrain-active Game View、统一数值overlay、双Hurtbox sync成本和最终Playable属于T5。
