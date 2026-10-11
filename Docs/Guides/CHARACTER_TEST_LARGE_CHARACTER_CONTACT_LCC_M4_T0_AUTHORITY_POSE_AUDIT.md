# LCC-M4-T0：Authority Pose、地形与 rig 审计

> Status: Guide
>
> 工作包：LCC-M4-T0（只读审计）
>
> 状态权威：[LCC-M4 第 5 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M4_AUTHORITY_TERRAIN_POSE.md)
>
> 执行文档状态：`Verified`（主代理已完成事实、边界、链接与 diff 复核）
>
> 前置：M3 `Verified`。

## 1. 结果与边界

已冻结 current flow、base→terrain IK→secondary→root 组合、最小字段与坐标合同、T1～T5 文件级 owner、schema 32 候选状态、四肢容量、停止条件和证据名。主代理已复核源码事实并收紧过度设计；证据见 [AUDIT_2026-08-25](../Evidence/LCC/LCC-M4-T0/AUDIT_2026-08-25.md)。

本轮只做文档与定向源码核对；未运行 Unity、测试、编译、网络或性能采样，不证明 M4 Runtime 能力。非目标：不实现 probe/IK，不修改 Dragon14 资产，不接消费者，不建立全身程序化动画、ragdoll、Unity Physics 旁路、`BigInteger`、HTTP 或新通用求解框架。

## 2. Current flow 与缺口

| 函数 / 入口 | 当前数据流 | 冻结缺口 |
| --- | --- | --- |
| `CharacterTestAuthorityPoseDefinitionSource.Create()` | CharacterTest 硬编码 10 条 secondary 与左右手臂 2 条 IK binding | 非 package-authored、非 Dragon14 四肢；T1 接管 definition |
| `CharacterAuthorityPoseStateComponent.ConfigureDefinition/ApplySecondaryImpulse/Tick` | 只创建并推进 `_secondaryStates` | 无 IK target/state/producer；T3 新建 terrain state |
| `TryGetChannelOffset/TryGetSocketOffset/CopySecondaryPoseSamplesTo` | 只输出 channel/socket 平移 offset | 无 base position、orientation、validity/source mask |
| `CharacterAuthorityTwoBoneIkSolver.TrySolve` | 孤立 fixed-point 解析器，只返回 mid/tip position | 生产无调用；缺 typed status、orientation/basis 与完整退化合同 |
| `CharacterTestCharacterEntity.TickAuthorityPose` | 对比 tick 前后 secondary sample，变化时 touch source revision | 无 terrain/resolved frame revision |
| `CharacterTestBakedWeaponTraceProvider.BuildTraceFrame` | baked 端点加 secondary socket offset，再经 root pose 转 world | T4 改读 authored resolved socket/channel |
| projectile socket resolver / Runtime AI preview | 同样读取 `TryGetSocketOffset` | actual/preview 必须同 frame/revision |
| `CharacterTestCharacterHurtboxSystem.SyncAll/SyncCore` | clip/action 选择 60 FPS baked root-space shape；Severed 先过滤；只应用 root position/Yaw | 完全不读 authority pose；T4 在 root transform 前应用 channel delta |
| `CharacterTestRuntimeCharacterView.SyncAuthorityPose` | 只镜像 secondary sample 到 `Transform.position` | 无 orientation；只能只读 T3 resolved frame |
| `TickPresentation` → `CharacterProceduralBoneRig.Tick` / `CharacterTwoBoneIkTarget.Tick` | authority mirror 后仍用 float/Transform/Quaternion IK 改骨 | authority-controlled channel 必须跳过，View 不回写 |
| body-part contact motion | PresentationOnly audio 可读 View Transform；Dragon14 contact points 为空 | 不是 authority；T4 新建 resolved tip/channel adapter |
| `CharacterTestNetworkSnapshotParticipant` | 现场 formal schema 最高 31 | T3 只占下一可用 32，不预占 33+ |

现状结论：只有 baked root-space pose 与 secondary translation 的分叉消费者；没有 terrain IK Runtime 链，也没有可按 channel 组合的 Hurtbox/socket orientation bake。

## 3. 最小字段、坐标、orientation 与 failure

T1 作者真源冻结为 `geometry/authority_pose_rig.json`（`mx.characterAuthorityPoseRig.v1`）；Compiler 在当前 v5 后只输出下一版 `mx.characterGeometryBinding.v6` additive section。动画 base channel 不建立第二套通用 bake 框架：优先扩展现有 `generated/character_socket_bake_catalog.json` 的稳定 channel track，补充可选量化 basis，并由现有 Unity import bake 流程生成；旧 position-only track 与 geometry binding v1～v5 兼容读，unknown/future 原文只读且禁止覆盖。若现有 catalog 无法在不破坏旧消费者的前提下承载该 additive 字段，T1 停止并回报，不自行新增另一套 catalog/二进制协议。

每个 limb 作者定义至少包含：stable `probeId/ikBindingId/partStateId/handedness/mirrorPeer`；root/mid/tip `channelId` 与 `bonePath`；tip-channel-local origin/axis；up/down/radius/clearance 毫米、static layer、slope cosine；两段长度、min/max reach、ModelRoot-local pole；motion/posture/action eligibility 与 enter/exit/lost frames；collider/socket/contact stable id→channel 的 attachment。每 clip/frame 的 ModelRoot-local base position + orthonormal basis 是 Unity import 的生成结果，不要求作者逐帧填写。禁止 Runtime 从 Transform 测长度、猜左右或猜 consumer 归属。

- 坐标为 LeftHanded ModelRoot：`+X right/+Y up/+Z forward`，正 Yaw 从 `+Z` 转 `+X`，禁止 scale。
- local→world 恰好一次：`R(x,y,z)=(x*c+z*s,y,-x*s+z*c)`，`world=root+R(local)`；world→local 恰好一次：`R^-1(x,y,z)=(x*c-z*s,y,x*s+z*c)`。
- point/distance/clearance 量化到 1 mm；normal 分量量化到 signed `1/32767` 后重归一；surface body/collider/shape identity 与 material raw fields 原样保留。
- 单次解析 solve：`bend=normalize(pole-aim*dot(pole,aim))`；退化依次投影 ModelRoot `+X`、`+Z`，仍失败为 `DegeneratePole` 回 base。左右差异只来自 authored pole/basis。
- root/mid basis：forward 沿 solved child，planeUp=`normalize(cross(aim,bend))`，right=`normalize(cross(planeUp,forward))`，up=`normalize(cross(forward,right))`。tip up=ground normal；forward=base tip `+Z` 在地面投影，退化依次用 parent forward、`+Z`、`+X`；全部 fixed-point。View 不用 `Quaternion.LookRotation` 重解 authority。

组合顺序唯一为：

```text
baked base channel/consumer pose
  -> T2 quantized static ground target
  -> T3 fixed-point terrain IK position+basis
  -> secondary translation on non-conflicting channels
  -> root position/Yaw once
  -> Hurtbox / authored socket / contact / View consume one frame
```

同 channel terrain/secondary 重复控制为 `ChannelConflict`，authoring 与 runtime 都 fail closed 到该 limb base，不按调用顺序覆盖。Severed 优先禁用 probe/solve/contact；Body Presence 只随 ModelRoot。

Typed status 分责：T1 `MissingDefinition/DuplicateIdentity/UnknownChannel/MissingBaseFrame/InvalidBasis/InvalidLength/InvalidLayerMask/MirrorMismatch/MissingSeveredMapping/ChannelConflict/FutureSchema`；T2 `Success/InvalidInput/NoHit/TooSteep/OutOfRange/UnsupportedShape/SurfaceIdentityUnavailable/SurfaceMaterialUnavailable/CapacityExceeded`；T3 `Success/MotionDisabled/PostureDisabled/ActionDisabled/Flying/KnockedBack/Dead/Severed/DefinitionStale/SourceStale/ChannelConflict/DegeneratePole/OutOfReach/InvalidResolvedBasis`。失败不伪造零点 target；spawn/restore 从 base 开始并清派生 cache。

## 4. T3 唯一 owner 与 schema 32 候选

T3 唯一定义/生产 `CharacterTestResolvedAuthorityPoseFrame`：`runtimeFrame/revision/definitionRevision/contentHash/baseSourceRevision`，加按 ordinal/channelId 稳定排序的 fixed-capacity sample；每项含 channelId、ModelRoot-local position、basis X/Y/Z、validity、`sourceMask(Base/TerrainIk/Secondary)` 与 typed failure。T4 只能消费同一 readonly frame/revision，不得创建第二种 snapshot/revision。

schema 32 必须保存所有会影响未来 pose/Hurtbox/Trace/Planner witness 的历史：last published revision；definition/content/base-source witness；每 probe current validity/failure、last-valid frame、连续 valid/miss 计数；last-valid target point/normal、surface body/collider/shape/material；current/target weight、enter/release progress、last eligibility transition frame；stable probe/IK/part identity与ordinal。

当帧 base channel、world probe origin、solved mid/tip 与 resolved samples若可纯重算则不重复入 wire。现有 action/motion/posture/death/Severed authority不复制。capture/apply/clone、legacy 0～31、future fail-closed、full/owned/non-owned hash、rollback+1、replay、late join同批闭合。

## 5. CombatPhysics 与 Dragon14 stop gate

现有 `QueryRay` 已有 fixed-point distance/point/normal、stable entity/body/collider/shape identity与稳定 comparer；collider持有 material，现有 contact runner按 shape+body/collider二次读取。缺口：`CombatQueryResult` 不携 material/static classification，static 只靠 caller layer mask，且无 single-best typed ground-probe API。

T2 第一步必须用纯 Combat tests 证明 static mask 排除 Dynamic Character/Presence/Hurtbox，命中 identity 可精确回查 material，compound leaf identity不丢失；任一项失败立即停止并先修通用 Combat API。禁止 Unity Terrain/Physics/Raycast/Collider/Renderer bounds 旁路。

Dragon14 当前只有四个 limb part 根、部分 limb colliders、前爪 sockets；没有四条完整 root/mid/tip authority chain、hind-foot terrain socket/contact、量化长度/pole、per-frame channel basis或consumer attachment。T1 必须从正式 authoring/generator证明四条链；任何 chain 只能靠 Runtime Transform 测量、bake缺 orientation、consumer或Severed映射不唯一时停止，不生成 fixture。

## 6. 容量、iteration、0 B 与校准预算

| 项目 | 冻结值 |
| --- | --- |
| limbs/probes | 每角色最多 4 limbs、4 probes；每 limb 每 authority tick 最多一次 single-best query |
| solve | 每 eligible limb 一次解析 solve；`MaxIkSolveIterations=1`，无 CCD/FABRIK 循环 |
| state/frame | 4 history slots、4 solve results、最多 32 unique resolved samples；超限 typed fail closed |
| query output | 单个 best result；不得用有限 hit list 静默截断 world；scratch warm-up 后复用 |
| GC | probe→solve→resolved frame→Hurtbox/socket/contact/View steady-state 0 B；诊断字符串只在低频 capture |
| 时间校准 | T5 使用 8 个 Dragon14、每只 4 probes+4 solves+18 Hurtbox；warm-up 300 ticks，测量 1800 ticks；同机记录 overlay-off/on 的 probe、solve、`SyncAll`、全链 p50/p95/max 与 GC。T0 不写未经实测的绝对毫秒门槛，结果只作为首版校准基线 |

## 7. T1～T5 owner、hand-off、filter 与 Evidence

| 包 | 精确文件 / partial owner | focused filter | Evidence | hand-off |
| --- | --- | --- | --- | --- |
| T1 | Dragon14 `geometry/authority_pose_rig.json`；新建 feature-specific Authoring/validation/compiler/CLI partial；`CharacterAuthorityPose.cs` definition/solver-input partial；`CharacterRuntimeSpawnContracts.cs` authority binding partial；`CharacterImportedPackageJson.cs` parse/canonical partial；现有 `CharacterSocketBakeCatalog.cs` 与 `CharacterImportedPackagePrefabBuilder.SocketBake.cs` 的 additive channel-basis partial；`CharacterPackageTests.AuthorityPose.cs` | `CharacterPackageTests.*AuthorityPose*|CharacterAuthorityPoseTests.*(Definition|TwoBone|Dragon14)*` | `Docs/Evidence/LCC/LCC-M4-T1/AUTHORING_RIG_2026-08-25.md` | T2 得 probe geometry；T3 得 binding/base bake/revision；T4 得 attachment |
| T2 | 新建 `Combat/Physics/CombatGroundProbe.cs`、`CombatPhysicsWorld.GroundProbes.cs`；必要时仅 partial 通用 helper in `Narrowphase/Sorting`；新建 `CombatGroundProbeTests.cs` | `CombatGroundProbeTests` | `Docs/Evidence/LCC/LCC-M4-T2/COMBAT_PROBE_2026-08-25.md` | 只交 T3 typed fixed-point result，不交 eligibility/state |
| T3 | 新建 `CharacterTestAuthorityPose.TerrainIk.cs`、`CharacterTestTerrainIkStateComponent.cs`、`CharacterTestResolvedAuthorityPoseFrame.cs`、`CharacterTestAuthorityPoseTerrainSystem.cs`、`CharacterTestNetworkAuthorityPoseSnapshot.cs`；partial：`CharacterTestComponentWorld.cs` store/revision、`CharacterTestCharacterEntity.cs` state/tick、`CharacterTestCharacterRuntimeModule.cs` schedule/restore、`CharacterTestNetworkSnapshot.cs` schema32/document/capture/apply/clone/hash；新建 terrain/network tests与 replay `AuthorityPoseSchema32_*` | `CharacterTestAuthorityPoseTerrainTests|CharacterTestAuthorityPoseNetworkTests|CharacterTestReplayTests.AuthorityPoseSchema32_*` | `Docs/Evidence/LCC/LCC-M4-T3/AUTHORITY_STATE_SCHEMA32_2026-08-25.md` | 交 T4 唯一 readonly frame/revision；T4不扩 schema |
| T4 | `CharacterTestCharacterHurtboxSystem.cs` resolved-delta partial；`CharacterTestBakedWeaponTraceProvider.cs`与 projectile/Planner socket resolver统一 adapter；新建 `CharacterTestAuthorityPoseContactPointAdapter.cs`；`CharacterTestRuntimeCharacterView.cs`、`CharacterTestCharacterUnityViewBridge.cs` mirror；`CharacterProceduralBoneRig.cs` authority-channel skip；consumer Edit/PlayMode tests | `CharacterTestAuthorityPoseConsumerTests|CharacterTestAuthorityPoseConsumerPlayModeTests` | `Docs/Evidence/LCC/LCC-M4-T4/POSE_CONSUMERS_2026-08-25.md` | 交 T5 同帧数值对照、float IK禁用、Game View初证 |
| T5 | M4专属 PlayMode/fixture/performance/network regression；只按实际结果回写 Animation/Combat/CharacterApplication/CharacterAction/Network/Audio/Authoring Interfaces与 `Docs/CAPABILITIES.md`；父状态仍由主代理写 | `CharacterTestDragon14AuthorityTerrainPosePlayModeTests|CharacterTestAuthorityPosePerformanceTests|CharacterTestAuthorityPoseNetworkAcceptanceTests` | `ACCEPTANCE_2026-08-25.md`、`PERFORMANCE_2026-08-25.md`、`INDEPENDENT_REVIEW_2026-08-25.md` under `Docs/Evidence/LCC/LCC-M4-T5/` | 全 gate、P0/P1=0 后回主代理决定 M4/LCC 晋升 |

T1～T4 不另开独立评审轮次；每项由主代理做一次范围、源码与 focused evidence 门禁后串行交接。M4 仅在 T5 做一次未参与实现者的最终只读 P0/P1 审查。

## 8. 测试矩阵与实际记录

测试矩阵：T1 roundtrip/order/future/missing/invalid/conflict/Dragon14；T2 flat/slope/step/no-hit/too-steep/same-distance/order/identity/material/NoAlloc；T3 single-yaw transform/reach/degenerate/mirror/eligibility/Severed/hysteresis/schema32/hash/rollback/replay/late-join/NoAlloc；T4 same-frame Hurtbox/Trace/contact/View parity与float IK禁用；T5 flat/slope/step/uneven/no-hit/action/flying/knockback/dead/Severed、8-character性能、LCC-M0～M3/CD-M0～M4回归、Game View与Console。

| 项目 | 当前记录 |
| --- | --- |
| 分支 / 提交 | `main` shared dirty worktree；未 stage/commit/push |
| 实际修改 | 本文；`Docs/Evidence/LCC/LCC-M4-T0/AUDIT_2026-08-25.md` |
| 源码核对 | 2026-08-25 定向只读完成；未做穷尽调查 |
| Unity / tests / compile / network | 按约束未执行 |
| schema / budget | schema 32候选；4 limbs/probes、32 samples、1 solve、steady 0 B；时间仅校准 |
| 状态 | `Verified`；主代理已回写 T1～T5 并将 M4 提升为 `Production-Frozen` |
| 主代理复核 | current flow、schema 31→32候选、Combat static-layer stop gate、Dragon14缺口、链接/diff均通过；删除T1～T4任务级独立评审，并复用现有socket bake链而非预建第二套catalog |
| 限制 | 只读审计；准备度不表示 terrain probe、IK、resolved frame 或消费者已经实现 |
