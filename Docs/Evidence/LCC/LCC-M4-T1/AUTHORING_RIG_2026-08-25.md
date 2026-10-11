# LCC-M4-T1 Authoring Rig Evidence（2026-08-25）

## 1. 结论与边界

LCC-M4-T1 实现完成并可交给主代理执行一次交接门禁。Dragon14 的四条稳定 root/mid/tip chain 可由正式 package authoring 绑定；现有 `mx.character.socketBakeCatalog.v1` 可用 additive optional basis 承载逐帧base basis，同时旧 position-only 写入和读取合同保持不变。没有引入 BigInteger、HTTP 取证、通用求解框架、第二套 bake catalog、二进制协议、独立评审轮次或 CharacterTest orchestration。

Stop-gate 结果：`PASS`。若 authority rig 缺失，geometry v1-v5仍解析为合法 `Missing`；若v6 rig、basis或future/invalid作者源不合法，对相应新能力fail closed，不合成旁路数据。

## 2. 实际文件

Authoring / compiler / CLI：

- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterAuthoringAuthorityPoseRig.cs`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterPackageValidation.AuthorityPoseRig.cs`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterAuthoringCompiler.AuthorityPoseRig.cs`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterAuthoringCompiler.cs`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/CharacterPackageValidation.cs`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/CharacterPackageCommands.AuthorityPoseRig.cs`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/CharacterPackageCommands.cs`
- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/Program.cs`
- `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/CharacterPackageTests.AuthorityPoseRig.cs`
- `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/Program.cs`

Runtime / Unity import / tests：

- `Assets/Scripts/MxFramework/Character.RuntimeSpawn/CharacterAuthorityPoseRigRuntimeBinding.cs`（及Unity生成的`.meta`）
- `Assets/Scripts/MxFramework/Character.RuntimeSpawn/CharacterImportedPackageJson.AuthorityPoseRig.cs`（及Unity生成的`.meta`）
- `Assets/Scripts/MxFramework/Character.RuntimeSpawn/CharacterImportedPackageJson.cs`
- `Assets/Scripts/MxFramework/Character.RuntimeSpawn/CharacterRuntimeSpawnContracts.cs`
- `Assets/Scripts/MxFramework/Character.RuntimeSpawn/CharacterAuthorityPose.cs`
- `Assets/Scripts/MxFramework/Character.RuntimeSpawn/CharacterSocketBakeCatalog.cs`
- `Assets/Scripts/MxFramework/Editor/CharacterImport/CharacterImportedPackagePrefabBuilder.Support.cs`
- `Assets/Scripts/MxFramework/Editor/CharacterImport/CharacterImportedPackagePrefabBuilder.SocketBake.cs`
- `Assets/Scripts/MxFramework/Tests/CharacterRuntimeSpawn/CharacterAuthorityPoseRigBindingTests.cs`（及Unity生成的`.meta`）

正式作者数据 / 任务生成物：

- `Authoring/CharacterPackages/character-000003/geometry/authority_pose_rig.json`
- `Assets/MxFrameworkGenerated/CharacterPackages/000003/config/geometry_binding.json`
- `Assets/MxFrameworkGenerated/CharacterPackages/000003/generated/character_geometry_binding.json`
- `Assets/MxFrameworkGenerated/CharacterPackages/000003/generated/character_socket_bake_catalog.json`

文档只修改本T1 owner和本证据；未改父M4、总台账、T0或T2-T5状态。

## 3. Schema、space与失败合同

- 作者真源：`mx.characterAuthorityPoseRig.v1` / schema `1.0`；包含12个channel、4个limb、12个collider/socket attachment。
- 编译输出：仅当当前作者源完整合法时，把现有geometry binding additive升级为 `mx.characterGeometryBinding.v6` / `6.0`；v1-v5 section与消费者保持原语义。
- 位置与长度：作者整数单位为millimeter；`tipProbeOriginMillimeters`位于tip-channel local；`poleDirectionSnorm`位于ModelRoot local；`tipProbeAxisSnorm`位于tip-channel local；basis使用snorm `32767`。
- probe字段覆盖up/down distance、radius、clearance、static layer mask、minimum slope cosine、world-normal target offset；eligibility覆盖motion/posture/action tag与enter/exit/lost hysteresis。
- validation覆盖唯一identity、channel存在与层级、正长度、reach范围、snorm/量化范围、重复channel控制、mirror、Severed映射、attachment与typed conflict。整数加法使用宽类型防溢出。
- compiler/runtime使用canonical sort、stable ordinal与fixed capacity；tick不解析JSON，也不读取Transform scale。
- Missing：不生成v6，不更改旧source/geometry hash；runtime得到空的`Missing` binding并走base pose。
- Future/Invalid：保留raw且不可写；compiler不产生部分v6。v6 runtime对header、capacity、ordinal、hierarchy、hash或字段漂移严格拒绝。
- Socket basis：catalog format仍是v1。旧`RegisterTrack`/`TryGetFrame`未改变；position-only序列化不写basis字段，`HasBasis=false`。basis缺失合法；basis计数/scale损坏会记录诊断并令basis不可用，但保留原position track。

## 4. Dragon14 四肢与测量来源

| limb | root → mid → tip（导入骨名） | 长度 mm（root-mid / mid-tip） | ModelRoot pole snorm | tip-local probe axis snorm |
| --- | --- | ---: | --- | --- |
| `left_fore` | `Dragon15_l_upperarm1` → `Dragon15_l_forearm` → `Dragon15_l_hand` | 893 / 701 | (-13997, -894, -29614) | (-30651, 11467, 1645) |
| `right_fore` | `Dragon15_r_upperarm1` → `Dragon15_r_forearm` → `Dragon15_r_hand` | 893 / 701 | (13997, -894, -29614) | (-30651, 11467, -1645) |
| `left_hind` | `Dragon15_l_thigh` → `Dragon15_l_calf` → `Dragon15_l_horselink/Dragon15_l_foot` | 994 / 796 | (-3218, -875, 32597) | (-32766, -183, -59) |
| `right_hind` | `Dragon15_r_thigh` → `Dragon15_r_calf` → `Dragon15_r_horselink/Dragon15_r_foot` | 994 / 796 | (3217, -875, 32597) | (-32766, -183, 59) |

来源是Unity正式生成的Dragon14 preview prefab静态导入姿态：asset GUID `f53d07c084dd5bb41a622faec8ca0782`，dependency hash `2bc9de46549da0d1ef83a1ee4fa9f9ef`。长度由该姿态中ModelRoot-local关节位置计算后量化为mm；pole由root/mid/tip rest-bend plane在ModelRoot-local中归一化并量化；probe axis由ModelRoot `-Y` 转到tip-channel local后归一化量化。逐帧position/basis由Unity正式animation clip registry与现有generator采样，不写回人工逐帧作者数据。

`staticLayerMask=3`表示bit 0与bit 1。依据：正式CharacterTest `world_object_definitions.runtime.json`中的静态物体实际使用combat layer 0/1，而 `CharacterTestCombatPhysicsSceneBridge` 在注册静态sphere/capsule时按 `obstacleMask |= 1 << layer` 聚合；因此不包含动态character 8、movable 9、hurtbox 10、presence 11。

Dragon14的limb body parts当前不是M3 `Severable`（可断的是tail），所以T1没有伪造四肢`partStateId`。未来若作者把limb标为Severable而未给合法映射，validation会阻断。

## 5. 正式生成与重复性

第一次正式CLI import报告：`status=WarningOnly`、`canWrite=True`、`canSpawn=True`，geometry config/generated均写成v6，rig content hash为 `sha256:10fa6a34ddc21f65c522ad6758493b01c212845663601f5e75f1af8d2e93622c`。进程非零退出来自共享全局前置 `RESOURCE_MAPPING_BUILD_TARGET_REQUIRED`（global resource build report没有concrete build target），不是T1校验或写入失败。

现有Unity generator第一次调用超过MCP客户端30秒响应窗，但Unity Console随后明确记录：preview prefab created、`MxFramework Character runtime entry points synchronized`。正式catalog为同一个 `mx.character.socketBakeCatalog.v1`：8 clips、106 tracks、0 bake issues；其中96条为 `12 authority channels × 8 clips` 的position+basis，10条既有action socket轨道继续position-only。basis scale为32767。

第二次CLI import：`updated=0 skipped=49`。第二次Unity generator通过唯一token `LCC_M4_T1_REPEAT`执行，Console终态：

```text
LCC_M4_T1_REPEAT_END status=Built socketReady=True socketHash=sha256:1b4ee413f1290cc772371b8acf7783a2126bbba1c2783c9c109f7855790ae42a tracks=106 issues=0
```

本任务三项正式生成物前后SHA-256完全相同：

| artifact | 第一次 | 第二次 |
| --- | --- | --- |
| `config/geometry_binding.json` | `c531bb13b9944bf0ac317a13695825facbe4200de0ef892d871edc0a03afbb47` | 相同 |
| `generated/character_geometry_binding.json` | `a90950edcdba05dcc26a0ad61535637da77669f97d38cc01aee7a103019a3621` | 相同 |
| `generated/character_socket_bake_catalog.json` | `061058015bd9d7ca582bd674b697a99c0302d82c583c89a3624cfd19254cf8c2` | 相同 |

## 6. 原始验证结果

Authoring focused test：

```text
dotnet run --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj -- character-authority-pose
Character authority-pose authoring tests passed.
```

CLI build：

```text
dotnet build Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj --no-restore -v:minimal
0 warnings, 0 errors
```

Unity根脚本门禁：

```text
job_id=51856e8f54ad44b797cb8146dd5c1aef
outcome=passed
refresh requested/completed=true
compilation started/finished=true (11 assemblies)
domain_reload_observed=true
editor_ready=true
console.error_count=0
```

该门禁只列出工作区既有CharacterAction/Runtime AI Planner unused/obsolete warnings；没有T1源码warning，未扩大清债。

Unity EditMode focused tests：

```text
job=8471f751473b4fcca12789787c9b83cc
status=succeeded, total=4, passed=4, failed=0, skipped=0, stuck=false

job=ec9d1cefc3944107b696d1d3c05bdced
status=succeeded, total=5, passed=5, failed=0, skipped=0, stuck=false
```

第一组覆盖legacy geometry/position-only catalog兼容、optional basis round-trip、Dragon14正式v6/四肢definition/content-hash漂移拒绝、minimum reach与确定性pole fallback。第二组是既有authority-pose deterministic snapshot/solver回归。

Unity内直接用正式runtime catalog loader读取生成文件并采样`idle1`首帧，结果为：

```text
IssueCount=0
PositionReady=12
BasisReady=12
LegacyPositionOnly=True (attack1a/rightClawStart)
```

这证明正式产物不仅含JSON字段，也能被现有runtime catalog路径同时消费新basis与旧position-only轨道。

## 7. 未执行项、风险与回滚

- 按T1非目标，未执行terrain query、authority tick IK、Hurtbox/View/network、CharacterTest消费编排或PlayMode地形贴合；这些属于T2-T4。
- 未新增独立rig gizmo/UI或另做截图。formal prefab的12条channel在8个正式clips上均得到position+basis，runtime fixture与hash检查已通过；实际地形视觉结果仍需后续consumer里程碑证明。
- 逐帧basis使正式JSON catalog达到10,878,970 bytes（106 tracks）。这是复用现有v1 JSON catalog并覆盖12 channels × 8 clips的直接成本；T1没有为压缩另造二进制协议或第二套catalog。后续若有加载/包体预算问题，应以单独性能数据决定优化，不能在本任务无证据扩抽象。
- 共享全局Resource Mapping build-target前置仍令`character import-unity`进程非零退出；T1报告本身是`WarningOnly`且所有任务产物已写入/重复稳定。不要把该前置误报为authority-rig失败，也不要在T1扩大清债。
- rollback：移除`authority_pose_rig.json`与T1新增partials/tests（含Unity `.meta`），回退T1在现有compiler/runtime/socket-bake中的additive字段，再运行现有generator恢复geometry v5与position-only catalog。因为旧catalog format/API未迁移，不需要数据转换；不得触碰共享dirty worktree其它里程碑修改。

## 8. 主代理交接门禁 P1 整改

主代理一次交接门禁发现：原loader只校验basis scale/count/index；raw分量越界、非单位轴、非正交轴或错误handedness仍可能形成`HasBasis=true`。整改严格限定在现有`CharacterSocketBakeCatalog.Load`与原focused test：

- 每个`right/up/forward` raw分量必须是Int64可表达的JSON integer，且位于`[-basisQuantizationScale, +basisQuantizationScale]`。
- 使用现有有界`double`归一化，容差为`min(0.05, 8 / basisQuantizationScale)`；分别校验三轴长度平方接近1、三组点积接近0、`dot(cross(right, up), forward) > 0`。
- 用每track的`bool[]`标记local frame；invalid index、duplicate index、missing frame、component range、vector length、orthogonality与handedness均产生固定文本diagnostic。
- 任一basis帧失败即把整条basis置为不可用（scale=0、空basis frames）；原position frames仍注册并可由`TryGetFrame`读取。
- 没有修改catalog format、authoring/schema、正式生成数据、10.9MB catalog策略或其它里程碑。

正式Dragon14 catalog共有12,588个basis frames；只读数值审计结果：

```text
invalidRange=0
maxLengthSquaredError=5.1028415697818E-05
maxAbsDot=4.2145850163211E-05
minHandedness=0.999947208889981
tolerance(scale=32767)=0.000244148075807978
```

整改后的唯一根Unity脚本门禁：

```text
job_id=9bcf6f41dc394f73bac513f113dac54e
outcome=passed
refresh requested/completed=true
compilation started/finished=true (2 assemblies)
domain_reload_observed=true
editor_ready=true
console.error_count=0
```

告警仍只来自既有Runtime AI Planner obsolete API，不含T1整改源码。

整改focused EditMode job：

```text
job=86a996368a474dfc8939cfc9507fd5b0
status=succeeded, total=5, passed=5, failed=0, skipped=0
stuck=false, blocked_reason=null, failures=[]
durationSeconds=0.95708
```

新增用例`NonOrthogonalSocketBasisFailsClosedWithoutDroppingPositionTrack`通过，固定诊断为：

```text
Socket bake basis orthogonality is invalid for clip.walk/channel.foot at frame 0.
```

同一focused job也完整加载正式Dragon14 catalog，`socketIssues`为空且`idle1/left_fore.tip`首帧basis可读；其余legacy position-only、valid optional basis、geometry v6/hash与IK fallback回归全部通过。未执行其它测试job，未重新生成或压缩catalog。
