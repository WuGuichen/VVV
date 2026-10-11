# LCC-M4：确定性地形探针与权威姿态

> Status: Guide
>
> 里程碑：LCC-M4
>
> 里程碑状态权威：[LCC 实施总台账第 6 节](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_DEVELOPMENT.md)
>
> 工作包状态权威：本文第 5 节
>
> 当前状态：`Verified`
>
> 执行包准备度：`Production-Frozen`（T0～T5 已全部 `Verified`；M4 Gate 已闭合）

> `main` 集成后 schema 映射：terrain authority pose 当前使用 schema 33；本文历史表、旧测试名和 Evidence 文件中的 schema32 保留集成前原始证据标签。

## 1. 目标与阶段边界

M4 让大型非人形角色使用 `CombatPhysicsWorld` 的确定性地形事实调整足爪/肢体姿态，并保证同一权威 pose overlay 同时驱动 Animated Hurtbox 和 Unity 表现，从而避免“画面脚掌贴地、战斗几何仍在原位置”的双真源。

Dragon14 首期范围：

- 四肢足爪的向下地形探针；
- package-authored limb chain / two-bone IK definition；
- 有界、量化的目标、reach clamp、失效和恢复；
- 权威 pose channel overlay；
- Hurtbox 与 View 使用同一结果；
- 斜坡、台阶和局部不平地面的 Playable。

首期不处理整条脊柱贴地、长蛇多段约束、触手、飞行翼空气动力、高速扑地全身解算或 Unity ragdoll。这些需要在 M4 Verified 后另立路线。

## 2. 权威输入与探针

### 2.1 输入来源

唯一地形事实来自 `CombatPhysicsWorld` 的 static surface query：

- probe origin 由当前 baked authority pose 的稳定 socket/channel 和 root transform 计算；
- query direction、距离、半径/shape、layer mask 和 slope policy 来自编译配置；
- hit point、normal、surface material 和 target identity 使用 Combat query 结果；
- 禁止读取 Unity Terrain/Raycast、Collider、Animator foot IK、骨骼 Transform 或 Renderer bounds。

现有 `CombatBodyPartContactSensorRunner` 的 sphere Started/Stayed/Ended 事件可继续用于脚步音频和接触表现，但它不返回本阶段需要的完整向下目标合同，不能作为 IK target 唯一输入。

### 2.2 Probe definition

Authoring 应复用/扩展 Character Authority Pose rig 数据，不在 CharacterTest 代码硬编码 Dragon 骨骼。每个 limb 至少包含：

- stable `probeId` / `ikBindingId`；
- root/mid/tip channel 或稳定骨骼映射；
- local probe origin/axis；
- max down/up distance、probe radius/shape；
- allowed static layer/slope；
- root-to-mid、mid-to-tip 长度和 reach clamp；
- pole direction；
- target offset/foot clearance；
- enabled motion/action/posture policy；
- 有界迟滞/恢复参数。

缺失 binding、非法长度、未知 channel、无地形 hit 或目标超 reach 时使用明确失败原因，并回到该肢体的 baked base pose；不得生成零点 target 或读取 View 补偿。

### 2.3 坐标空间与旋转合同

- base/resolved channel position 与 orientation/basis 都以 `ModelRoot` local fixed-point 表达；root position/Yaw 不烘入 channel，所有消费者只在最终 world projection 时应用一次。
- `local probe origin/axis` 相对该 limb 的 base tip channel；先由当前 baked base channel 转到 ModelRoot，再由 authority root position/Yaw 转到 world 发起 probe。
- ground hit point/normal、surface identity/material 位于 Combat world；target offset 沿量化 world normal 应用。进入 IK 前仅用 authority root position/Yaw 的确定性逆变换转回 ModelRoot，一正一逆，不读取或应用 Transform scale。
- pole direction 以 ModelRoot local 表达；T0 必须冻结 handedness、退化 fallback 和从 solved root/mid/tip 生成 deterministic orientation/basis 的规则，不能只输出位置让 View 浮点补旋转。

## 3. Pose 组合与 Runtime 状态

### 3.1 组合顺序

M4-T0 必须依据当前 pose/hurtbox pipeline 复核并冻结，计划目标为：

```text
offline baked locomotion/action blend
  -> deterministic terrain IK overlay for configured limb channels
  -> deterministic secondary offsets for non-conflicting channels
  -> root position/Yaw transform
  -> same resolved channels feed Hurtbox and Unity presentation
```

- 同一 channel 同时被 terrain IK 和 secondary binding 控制时必须有 typed conflict validation；不得依赖调用顺序静默覆盖。
- T3 是 resolved authority pose frame 的唯一生产 owner：frame/revision、definition/source revision、按 channelId 稳定排序的 fixed-capacity samples，以及每个 sample 的 ModelRoot-local position、orientation/basis、validity 和 source mask。
- View 只能消费 resolved authority pose samples。authority-controlled limb channel 禁止在镜像后再次运行现有 Unity/float two-bone IK；PresentationOnly 修饰只能作用于未被 Combat/Hurtbox/Trace/contact 绑定的 channel，且不得回写 authority。
- 动作可通过 authored policy 禁用或降低 terrain IK，但 policy 必须由 deterministic action frame/state 决定。

### 3.2 Runtime state

若目标迟滞、稳定窗口、上一合法 target 或 blend progress 会影响未来 pose/Hurtbox，必须作为正式状态保存。每个 probe/limb 候选状态包括：

- validity/failure reason；
- quantized target point/normal；
- last valid frame；
- blend/weight；
- stable surface body/collider identity（仅当未来决策需要）；
- definition/source revision。

派生的最终 mid/tip position若可从 snapshot target + definition + base pose纯重算，不重复持久化。

### 3.3 失效与恢复

- 无合法地形 hit、surface 过陡、距离超限、动作禁用或 binding stale 时，目标权重以冻结的 deterministic 规则回到 base pose。
- 不允许无限持有陈旧目标；迟滞帧数/阈值在 T0 冻结并进入 hash/config identity。
- spawn/restore 第一帧没有历史目标时从 base pose 开始；不得借用 restore 前的派生 cache。
- 飞行、击飞、死亡、ragdoll/presentation-only 状态与地面 locomotion 使用显式 eligibility，不通过脚是否接触 Unity Collider 推断。

## 4. Hurtbox、Socket 与接触联动

- `CharacterTestCharacterHurtboxSystem` 必须在计算每个绑定 shape 前消费 resolved authority pose channel，而不是读取已更新的 Unity bone。
- BakedSocketCapsule/weapon socket 是否消费 terrain overlay 由 socket/channel 绑定明确决定；不能让 View 与实际 Trace 端点不同。
- Body Presence v1 仍是 ModelRoot 稳定体积，不随足爪 IK 变形。
- body-part contact point 的世界样本可从同一 resolved tip/channel 生成，用于 Combat contact/audio；不得另读脚骨 Transform。
- M3 Severed limb/part 的 probe 和 IK binding 必须禁用，且不生成接触点；稳定 probe identity 保留。

## 5. 工作包状态与实施顺序

| 工作包 | 状态 | 交付物 | 前置 |
| --- | --- | --- | --- |
| [LCC-M4-T0](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M4_T0_AUTHORITY_POSE_AUDIT.md) | `Verified` | 重审 authority pose、baked hurtbox/socket、two-bone solver、contact point、network 和 Dragon14 rig；冻结组合顺序/状态/预算 | M3 Verified |
| [LCC-M4-T1](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M4_T1_PROBE_IK_AUTHORING.md) | `Verified` | package-authored ground-probe/IK definitions、validation/compiler/runtime binding、Dragon14 four-limb fixture | T0 Verified |
| [LCC-M4-T2](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M4_T2_COMBAT_GROUND_PROBES.md) | `Verified` | CombatPhysics static probes、稳定 hit selection、world point/normal/identity/material quantization 与纯几何 failure diagnostics | T1 Verified |
| [LCC-M4-T3](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M4_T3_IK_AUTHORITY_STATE.md) | `Verified` | character eligibility、fixed-point IK/resolved frame唯一生产、冲突/失效恢复、snapshot/hash/rollback/replay | T2 Verified |
| [LCC-M4-T4](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M4_T4_POSE_CONSUMER_INTEGRATION.md) | `Verified` | resolved authority pose channel 同时接入 Hurtbox、socket/contact points 和 Unity View | T3 Verified |
| [LCC-M4-T5](CHARACTER_TEST_LARGE_CHARACTER_CONTACT_LCC_M4_T5_ACCEPTANCE.md) | `Verified` | Dragon14 斜坡/台阶 Playable、网络 parity、性能、聚焦回归、接口/能力表和唯一独立终审 | T4 Verified |

主代理是本表唯一写入者。全路线 `Audit-Frozen` 且 M3 `Verified` 后才派发 T0；T0 `Verified` 并提交结构化回报后，由主代理完成生产任务的文件级回写并把 M4 提升为 `Production-Frozen`。Authoring、Combat probe、authority/network、pose consumers 和最终验收按任务文档唯一 owner 串行交接；T1～T4不另开独立评审，T5只做一次里程碑最终只读P0/P1审查。

## 6. 预计影响面

- Character Resource Package authority-pose / rig Authoring 和 compiler
- `Character.RuntimeSpawn/CharacterAuthorityPose.cs` 及 imported binding
- CharacterTest authority pose state/component world/snapshot/hash
- CharacterTest motion/action eligibility 与 ground probe system
- `CharacterTestCharacterHurtboxSystem`、baked socket/trace pose resolver
- CharacterTest RuntimeCharacterView / procedural bone rig bridge
- body-part contact point adapter / Physical Audio projection
- Dragon14 rig/bake/generated package 和正式 Playable 场景
- noEngine、EditMode、PlayMode、network、NoAlloc tests

## 7. Schema 与网络

M4 使用实施时下一可用 CharacterTest formal schema；不得在 M3 未完成时预占固定数字。

必须覆盖：

- 所有影响未来 target/weight/Hurtbox 的 probe/IK state；
- definition/source revision mismatch；
- full/owned/non-owned hash；
- capture/apply/clone/legacy migration；
- rollback 后下一 tick probe result、pose channels、Hurtbox query、Trace endpoint、Planner command 和 state hash；
- late join/replay；
- 派生缓存 restore 清理和确定性重建。

Unity View bone matrix、Renderer、Gizmo 和 presentation-only offset 不进入 snapshot。

## 8. 验证矩阵

| 类别 | 必须覆盖 |
| --- | --- |
| Authoring | limb/channel/socket 引用、长度/reach/pole、重复控制、非法参数、future schema、稳定 hash、Dragon14 fixture |
| Probe | 平地、上/下坡、台阶边、无 hit、过陡、多个 surface 同距、顺序扰动、spawn/restore 第一帧 |
| IK | 可达/不可达 target、reach clamp、退化 pole、左右镜像、fixed-point 重演、冲突 channel fail closed |
| 状态 | eligibility 切换、动作禁用、失效/恢复、迟滞、飞行/击飞/死亡、Severed limb |
| 同源 | View tip、Hurtbox、BakedSocket Trace、contact point 来自同一 resolved channel；PresentationOnly 修饰不回写 |
| 网络 | 下一 schema、full/owned/non-owned hash、rollback 下一 pose/hit/command、late join/replay/source mismatch |
| Playable | Dragon14 平地、斜坡、台阶、不均匀四足支撑；攻击窗口和受击位置与画面一致；四层 overlay + probe/target |
| 性能 | 四肢 probes + fixed-point solve + Hurtbox SyncAll 全链预算；steady-state 0 B，失败/诊断路径不污染高频 |
| 回归 | LCC-M0～M3、CD-M0～M4、Dragon14 natural attacks、motion/pathfinding、Physical Audio contact |

## 9. M4 完成 Gate

- T0～T5 全部 `Verified`。
- 所有 ground facts 来自 CombatPhysicsWorld；代码/审查确认 authority 不读取 Unity Physics/Animator/bones/Transform。
- package-authored probe/IK rig 经 Authoring/Compiler/Runtime 单链消费；非法/未知数据 fail closed。
- fixed-point target/IK/失效状态进入正确 snapshot/hash/rollback/replay，下一 tick pose/Hurtbox/Trace/command parity 通过。
- Hurtbox、相关 socket/contact point 和 Unity View 消费同一 authority pose channel；直接 Game View 证明画面与战斗几何一致。
- Dragon14 斜坡、台阶、无 hit、动作禁用、Severed limb 场景全部通过；不把首期范围夸大为长蛇/触手/全身程序化动画。
- 全链性能满足 T0 校准预算，steady-state 0 B GC；Unity Console 0、全回归、独立审查 P0/P1 为 0。
- 按实际实现回写 Character/Combat/Animation/Network/Audio/Authoring 接口和 `Docs/CAPABILITIES.md`，明确 Framework Feature、Runtime Slice 或 Playable 等级。

## 10. 实际交付记录

| 项目 | 当前记录 |
| --- | --- |
| 工作包进度 | 6/6 Verified；M4 Gate 已闭合 |
| 实际分支 / 提交 | `main`共享dirty worktree；T0～T5已收口，未stage/commit/push/clean，保留既有无关修改 |
| 实际文件 / 资产 | T1已落authority rig作者真源、Authoring/validation/compiler/CLI、geometry v6/runtime binding、现有socket-bake optional basis与Dragon14四肢正式生成物；T2新增feature-specific ground probe、精确primitive/compound material lookup及focused tests；T3落唯一resolved frame与schema32；T4将同帧delta接入Hurtbox、claw Trace/Planner、显式ContactPoint与View；T5新增三个专用验收fixture、迁移陈旧测试口径并按事实回写接口/能力表。T5发现的View exact path、rollback derived Animation和Presence v6缺陷由原owner最小修复。 |
| Authoring / solver tests | T1 Authoring focused、CLI build、rig/basis5/5与AuthorityPose回归5/5通过；T2 ground probe最终15/15、四形状/TOI/量化/身份/0 B与旧Query回归通过 |
| network / rollback | T3 schema32、capture/apply/clone、legacy0～31、future/source mismatch、full/owned/non-owned hash、rollback+1、replay与late join闭合；resolved mid/tip保持可重算派生 |
| Unity / Playable | T1 Unity validator PASS；T2与T3各自保留integrated首次失败原证据，并按规则以分步scripts compile/domain reload/Editor idle、真实focused tests与Console0闭合；T4唯一validator `b152394699e6406ea1093ae198ce54d6` 完整通过。T5 terrain-active Dragon14 PlayMode 3/3及冻结5-class PlayMode 12/12通过；最终integrated validator仅调用一次并在domain reload回包时断线，一次分步fallback确认新compile/domain reload、Editor idle/not compiling/no pending与Console0。Game View保留正式Dragon14、不等高地面和tests-only marker。 |
| View/Hurtbox 同源证据 | T4 consumer/contact/View 8/8、PhysicalAudio 4/4、Hurtbox/Trace/Planner/Procedural 11/11及真实Hurtbox数值1/1通过；Game View证明角色可见，不把未激活terrain sample的截图写成地形终验 |
| 性能 / NoAlloc | T3四肢fixed-capacity authority tick warmed 0 B；T4 warmed helper/contact path 0 B；T5以8只Dragon、warm300/measure1800实测首Sync→4 probes/IK→第二Sync全链p50/p95/max=`1238.5/1821.4/6708.6 µs`，full/no-hit/recovery均0 B。双Sync通过校准且T0无绝对毫秒阈值，因此保留、不优化。 |
| 接口 / 能力表 | Character/Combat/Animation/CharacterAction/Network/Audio接口与`Docs/CAPABILITIES.md`已按实证回写；能力等级为Framework Feature，当前Playable证据限定Dragon14四肢。 |
| 限制 / 阻断 | 无阻断。唯一终审P0=0、P1=0；接受P3：约10.88 MB首版JSON catalog、每tick双Hurtbox Sync、正式Dragon14 ContactPoint 0/0且无action-disabled tag/limb partStateId、无生产统一overlay。未跑全仓、dedicated server、多进程或公网transport；不声明长蛇/触手/全身/飞行翼求解。 |

## 11. 状态历史

| 日期 | 工作包 | 原状态 | 新状态 | 证据摘要 | 限制或阻断 |
| --- | --- | --- | --- | --- | --- |
| 2026-08-24 | T0～T5 | 初始化 | `Planned` | 草拟 CombatPhysics probe、权威 pose 组合、Hurtbox/View 同源、网络和首期 Dragon14 四足范围 | 执行包仍待审查；等待 M3 Verified，无生产实现 |
| 2026-08-24 | T0～T5 | `Drafting` | `Audit-Frozen` | 6/6 执行文档经结构、DAG、写入边界和验证合同独立审查，P0/P1 为 0 | 等待 M3 `Verified` 后才可派 T0；无生产实现 |
| 2026-08-25 | T0 | `Planned` | `In Progress` | M3 Gate解除；按Audit-Frozen边界派发authority pose/terrain/rig/consumer/network与预算只读审计 | 只允许写T0冻结表与Evidence；T1～T5生产写入继续锁定 |
| 2026-08-25 | T0 / M4 / T1 | `In Progress` / `Audit-Frozen` / `Planned` | `Verified` / `Production-Frozen` / `In Progress` | T0冻结current flow、schema32候选、Combat static-layer stop gate、Dragon14四肢缺口、4-probe/32-sample预算与T1～T5 owner；主代理删除任务级评审并要求优先复用现有socket-bake链后派发T1 | 只证明可实施；terrain probe、IK、resolved frame和消费者仍未实现 |
| 2026-08-25 | T1 / T2 | `In Progress` / `Planned` | `Verified` / `In Progress` | T1完成四肢作者rig、geometry v6/runtime binding与既有catalog optional basis；主代理回派并关闭basis合法性P1，最终5/5 focused、5/5回归、Unity Console0和代码规范0问题后串行派发T2 | 接受约10.88 MB首版JSON catalog P3；terrain query、authority state、consumer和Playable仍待T2～T5 |
| 2026-08-25 | T2 / T3 | `In Progress` / `Planned` | `Verified` / `In Progress` | T2完成single-best fixed-point ray/sphere-cast、四种target、compound材质回查与稳定量化；主代理关闭轴向TOI、边界来源和nonzero-radius OBB证据缺口，最终15/15、4096次0 B、分步Unity gate与Console0后派发T3 | integrated validator首次CS0177失败原证据保留；T2不判断eligibility/state，authority/network与消费者仍待T3/T4 |
| 2026-08-25 | T3 / T4 | `In Progress` / `Planned` | `Verified` / `In Progress` | T3完成固定4肢/32样本resolved frame、fixed-point IK、eligibility/迟滞、schema32与完整网络链；主代理关闭世界原点、future-state瘦身、orientation、geometry v6与schema27窄缺口，直接复核terrain6/6、network6/6、replay3/3、回归6/6及Console0后派发T4 | integrated validator在domain reload时session断开原证据保留；Hurtbox/Trace/contact/View仍必须由T4消费同一frame，不新增第二帧或通用Pose Graph |
| 2026-08-25 | T4 / T5 | `In Progress` / `Planned` | `Verified` / `In Progress` | T4完成同帧/revision的Hurtbox、claw Trace/Planner、显式ContactPoint与View消费；34项聚焦检查、warmed 0 B、Dragon14 Game View初证与唯一integrated validator通过后串行派发T5 | 统一数值overlay、terrain-active Playable、双Hurtbox sync成本、8角色性能和唯一独立终审仍由T5收口 |
| 2026-08-25 | T5 / M4 | `In Progress` / `In Progress` | `Verified` / `Verified` | T5闭合terrain-active PlayMode 3/3、EditMode 290/290、冻结PlayMode 12/12、network/rollback各1/1、8 Dragon全链性能/0 B、接口/能力表及Unity脚本门禁；唯一最终只读审查P0=0、P1=0、P2=1、P3=3，无生产回派 | P2父状态残留随本次封版关闭；接受catalog体积、双Sync与正式Dragon内容覆盖P3，不追加评审轮次 |
