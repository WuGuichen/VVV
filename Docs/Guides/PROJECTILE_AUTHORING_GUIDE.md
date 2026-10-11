# Projectile Authoring 使用指南

> Status: Current
>
> 版本 0.2.1 | 现行合同核对：2026-10-05

## 打开工作台

在 Unity Editor 选择 `MxFramework/Combat/Projectile Authoring`。窗口读取项目路径配置中的 Projectile Authoring package，并按 `Definition -> Request Variants` 两层结构展示；没有可用 Authoring CLI host 时显示不可用状态，不代表任意 package consumer 都已接通该工具。

工作台提供 identity、共享影响、CRUD draft、完整规则 Inspector、校验、冲突、撤销/重做、原子保存和确定性预览。它是 Definition、Request/Spawn Profile 与 Presentation Binding 的唯一 Unity 可写入口。

M6 预览使用与 CharacterAction 同源的 `AuthoringTimelinePlaybackController` 和 `AuthoringPreviewRenderScene`。轨迹、碰撞与事件由正式 `CombatProjectileRuntime` / `CombatPhysicsWorld` 固定帧前向编译为只读 cache；正常前向播放再通过 `CombatProjectileVfxBridge`、`CombatProjectileUnityPresenter` 和 `VfxRuntimeModule` 路由 v2 七槽。拖动或跳转时间尺只读取 compile 期 active/runtime state cache，并以 empty event buffer 触发 Flight/Trail Rebuild，不补播历史 Spawn/Impact/Expire one-shot。Published 与本地有效 Candidate 均通过现有 VFX 选择解析源得到实际 prefab；缺失资源报告 `PROJAUTH024`，但不阻断 gameplay 轨迹。

预览对象全部由 `PreviewRenderUtility` scene owner 管理，不进入用户 active Scene。进入 Scene Edit 会暂停播放；origin/fallback、collision radius、area radius、arc height、sway amplitude 和 bezier controls 只在 mouse-up 时提交一个 typed mutation，Escape 或窗口/文档切换会取消未提交 drag。

## Definition 与 Request

- Definition 保存确定性运动、碰撞和命中规则，可被多个 Request 共享。
- Request 保存 spawn/aim 入口并引用一个 Definition。
- Presentation Binding 与 Definition numeric ID 关联，但只负责表现，不进入确定性规则。

共享 Definition 的重命名或删除会先显示 Reference Impact。存在 blocking consumer 时操作不可通过确认强行绕过；删除 Request 永远不会隐式删除最后一个 Definition。

## 编辑与保存

1. 在左侧选择 Definition 或 Request。
2. 在右侧输入显式 stable ID，执行新建、复制、重命名或删除。
3. 命令只调用 `projectile mutate` 生成版本化 draft，不会立即写入 source。
4. `校验` 调用 authoritative CLI；Problems 显示 diagnostic code、severity、source 和 field。
5. `保存` 仅调用 M1 `projectile apply`，在同一事务中提交 source、generated JSON 和 bytes。

保存失败会保留 dirty 与 draft。当前 Editor `保存` 通过 service 调用 `projectile apply --project-root <当前项目>`，在同一事务中同步 source、Tools generated、Unity Generated 和 CharacterTest Player mirror 的运行时 JSON / bytes；只有 receipt 为 `Committed` 且 `playerMirrorFreshness=UpdatedAtomically` 才刷新 AssetDatabase 并重载 workspace。不传 `--project-root` 的独立 CLI apply 不进行这两份项目同步，receipt 为 `NotUpdatedByTransaction`。显式 `projectile import-unity` 仍可同步已有产物并输出 freshness/hash/rollback receipt，`--dry-run` 可先审计；这些数据同步不等于重新构建或验收 Player。

## 从 CharacterAction 打开

CharacterAction 的 Projectile gameplay event 只保存 Request Stable ID 与触发帧。其 Inspector 显示 Definition、Spawn、Presentation 和引用健康度的只读摘要；点击“在 Projectile Authoring 打开”会在本窗口定位同一 Request。缺失或 generated-only ID 会 fail-soft 定位并进入 Problems，不会由 CharacterAction 静默创建或改写 projectile 数据。

Socket、运动、碰撞、命中、生命周期 VFX 和 Presentation Binding 均在本窗口编辑。Arc Mine、Poison Bottle、Rune Turret 等 consumer-specific mechanic tuning 仍由其原有 Mechanic Tuning 所有者维护，不进入通用 projectile schema。

一次性生命周期槽（出生、直接命中、范围命中、格挡命中、过期）使用“技能合同长度（帧）”控制展示时长。当前窗口“应用参数”与 Formal 选择提交 SetPresentationSlot 时显式传入 synchronizeVfxContractLifetime=false，只更新槽位参数。Core 在调用方显式开启该开关且长度 >0、VFX definition/profile 均可唯一定位时，才将当前槽 lifetimeFramesOverride、vfxDefinitions[].defaultLifetimeFrames 与 vfxPresentationProfiles[].lifetimeFramesOverride 纳入同一 draft/apply 事务；不能把这项 Core 能力等同于窗口按钮已启用。它不修改 ParticleSystem、Animation 或子 VFX 原始播放速度；flightBody 与 trail 是持续槽，不参与一次性合同同步。

## External Changed

`刷新` 会比较 workspace/source hash。dirty session 检测到外部变化后进入 `External Changed`，并阻止保存，避免静默覆盖：

- `Reload Disk` 丢弃当前 draft 并接受磁盘 workspace。
- `Compare Summary` 显示 base/current workspace hash、draft 和 dirty source 摘要。
- `Reapply Draft` 接受最新 workspace 后按原顺序重放完整 pending command sequence，不会只重放最后一次操作，也不会盲目替换 expected hash；冲突仍回到 Reference Impact 由用户决定。

连续执行 CRUD 时，窗口保留 pending command sequence，并由 Core 在隔离 workspace 中按顺序规划。后续命令可以引用前序命令创建或重命名的对象；最终只生成一个相对原始 workspace hash 的 composite draft，保存时通过一次 M1 apply 原子提交。Undo/Redo 会切换命令序列快照并重新生成对应 composite draft。

每次成功生成 composite draft 时，Core 同时返回该暂存结果的完整 `projectedWorkspace`。导航、Inspector、引用数、Presentation 状态和 dirty badge 都读取这个 draft projection，所以新建 Definition 无需先保存即可继续新建 Request、复制或重命名。窗口另行保留只读的 disk workspace 作为 external hash/compare 基线；blocked 或失败的 mutation 不会替换当前 draft projection，Save 成功并重新加载后才用磁盘状态取代它。

Definition dirty 使用 projection 提供的精确 `sourcePaths`、`presentationSourcePaths` 和 Request source path，不再用 stable ID 子串匹配。相似 ID 不会互相误标。

Domain reload、进入 Play Mode 或窗口暂时禁用时不会依赖临时 draft 文件恢复。会话只持久化命令、dirty/history、选择、完整筛选、展开状态和 pane size；重新加载 workspace 后再重建 draft。显式 Discard 会清理这些恢复信息和临时作业。
- `Cancel` 保留 draft，继续阻止保存。

字段级合并 UI 不属于 M2；相同字段冲突不会被静默覆盖。

## CLI

```powershell
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- projectile workspace --package <package> --out <workspace.json>
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- projectile mutate --package <package> --request <mutation.json> --draft-out <draft.json> --report-out <dir>
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- projectile apply --package <package> --draft <draft.json> --out <generated-dir>
```

`workspace` 输出稳定、相对路径、无时间戳的 Editor contract。`mutate` 是 pure planning command；内容提交由 `apply` 执行。`compile`、`import-unity` 和显式输出/report 参数各有写出边界，见 [Core 与 CLI 事务合同](#core-与-cli-事务合同)。

`workspace` 兼容读取 generated Definition schema 3/4/5、Spawn 1/2、Binding 1/2，与 source authority 做差集并显示只读 `Generated-only`；当前普通 compiler 输出 5/2/2。选择条目后执行 `Adopt Generated`，窗口只向 CLI 传递 opaque `generatedOnlyStableId`，不会重新序列化 gameplay 数据。CLI 内部解析完整记录；缺少 Definition 必需字段或 Presentation Binding 时 fail-closed。

Rename/Delete/共享 Definition 操作先执行 `planOnly`。Reference Impact 列出 Request、consumer path、需要授权的路径和 hard blocking 原因；确认后提交 confirmation token 与精确授权路径。未知 consumer 和不受支持的删除影响不可通过确认绕过。

## 工作台与安全 CRUD 合同

现行窗口最小尺寸为 1280×720。左侧虚拟化 ListView 按 Definition / Request 展开，支持搜索、Motion / Impact / Status、Used / Shared / Dirty / Invalid 过滤及状态 badge；选择使用 stable ID，过滤后无法定位时清除选择。三栏使用 TwoPaneSplitView，pane size、展开状态与完整过滤条件进入 UI session。工具栏和外壳一次构建，列表、Inspector、Problems 按操作刷新；Problems 显示 code、severity、source / object / field 路径，过滤或复制不改原 diagnostics。M2 阶段的 placeholder 已被后续规则 Inspector、Presentation 和实际预览接入替代，不再列为现行能力限制。

Editor 经 IProjectileAuthoringService / 异步 CLI adapter 调用 workspace、mutate 和 apply；不以 Editor 窄 DTO 重新序列化完整 gameplay 内容，也不直接写 source / generated / mirror。Validate 使用 CLI 返回的 workspace diagnostics。CLI 进程完成需退出且 stdout / stderr 都收到 EOF，再在 Editor update 回调中交付结果；Cancel 解绑完成回调并清理进程和临时作业，不能用主线程 WaitForExit。窗口通过显式刷新比较 workspace hash；不把该接口描述成每帧全目录 hash 轮询。

| 操作 | 持久规则 |
|------|----------|
| CreateDefinition | 显式唯一 stable ID；numeric ID 由 Core 查询 package / dependency / reserved authority，冲突阻断，自动分配跳过占用值；创建规范默认 Definition / Presentation，可选 Request。 |
| CreateRequestForExistingDefinition | 新 Request / Spawn，复用现有 Definition / Presentation，显示共享影响。 |
| DuplicateDefinition | 新 stable / numeric identity，创建新 Request；从唯一 canonical JsonNode 子树复制 Definition / Motion / Spawn，按 copyPresentation 选项复制表现，保留这些子树中的未知扩展；无法唯一定位时阻断。 |
| DuplicateRequest | 只克隆 Request / Spawn 子树并更新其 identity，继续引用同一 Definition，不复制 Definition。 |
| RenameRequest | 先展示 consumer impact；确认 token 与精确路径授权后更新 source 和获授权 consumer，不改共享 Definition。 |
| RenameDefinition | 更新 canonical Definition 的 stable ID 及所有 Spawn 的 projectileStableId，保留其 numeric identity / binding 归属；先核对引用影响。 |
| DeleteRequest | 去除 Request / Spawn，混合 source 中仍保留 Definition / Presentation；最后一个 Request 也不隐式删除 Definition。 |
| DeleteDefinition | Request 引用、consumer 删除不支持或其他 hard blocking 都阻断；仅符合条件且显式 destructive confirmation 才规划 canonical Definition / Presentation 删除。 |
| AdoptGeneratedOnly | Adopt 前只读，Editor 传 opaque generatedOnlyStableId，由 Core 解析完整记录；缺必要字段、binding / identity 不匹配或 generated catalog malformed 时阻断，后者报告 PROJAUTH_GENERATED_CATALOG_INVALID。 |

这些操作先规划和确认，再将服务生成的版本化 draft 纳入 session；只有 Save/apply 写内容。Reference Impact 的 hard blocking 不能用确认绕过；消费者更新仍接受 M1 expected-hash 与原子事务检查。不要恢复历史 task 内的固定 source / Definition 数量或测试次数为当前指标。

## 会话与生命周期合同

Content history 最多 64 个快照，仅保存内容命令与 dirty source，选择、过滤、pane size 和 Problems 状态不计入内容历史。Undo/Redo 切换命令序列并重建 composite draft，返回可达 base 快照时 clean。dirty 按精确 sourcePaths、presentationSourcePaths 和 Request sourcePath 追踪，不用 stable ID 子串匹配。blocked / failed mutation 不替换原 draft projection；保存成功后才用重载 disk workspace 作为新基线。

重载保存 commands、dirty/history、选择、完整过滤、展开状态和 pane size，恢复时从当前 workspace 重建 draft，不保存失效临时 draft 路径；恢复发现 source hash 改变仍进入 External Changed 并阻止 Save。窗口使用 hasUnsavedChanges / SaveChanges / DiscardChanges 接入 Editor 未保存提示；Discard 清理恢复信息并取消 CLI。before assembly reload、PlayMode change、OnDisable / OnDestroy 取消作业、释放预览并解除 lifecycle callback。预览对象仍由既有 PreviewRenderUtility scene owner 管理，不把旧 M2“完全不创建 GameObject”误用为当前 Preview 实现的约束。

字段级合并 UI、当前真实窗口/关闭对话框的手动效果、异步关闭竞态和当前测试通过情况不由本次文档核查证明。源码入口是 package Editor 的 ProjectileAuthoringWindow.cs、ProjectileAuthoringDocumentSession.cs、ProjectileAuthoringCliService.cs，以及 Tools Authoring Core 的 ProjectileAuthoringMutation.cs / Workspace.cs 和 CLI ProjectilePackageCommands.cs；本轮只读核实，未运行 Unity、CLI 内容写入或新的 Player 验收。

## 规则 Inspector 与字段命令

Definition 使用 `EditDefinitionFields`，Request 使用 `EditRequestFields`。`fieldEdits` 为有序白名单路径与显式 Bool / Int / Float / String / Enum / Vector / StringList / IntList / Object（JsonValue 对象）/ Null 值；空批次、空或重复路径、未知/只读路径、类型或条件错误使整批阻断，不产生 draft。Core 在唯一 canonical source 的 raw JsonNode 上定点修改，保留未触及的未知 sibling；替换整个已知列表/对象时以新值为准，不把 typed detail 整体序列化回 source。UI 使用成功 mutation 的 typed detail 与 raw draft projection，blocked mutation 保留原投影。

| 区域 | 可编辑字段及约束 |
|------|------------------|
| Identity | `physicalEventsEnabled`；numeric Definition ID 只读，stable ID 使用 Rename 命令。 |
| Motion | `kind`、`speedPerFrame`、`lifetimeFrames`、`speedProfile.kind`；速度和帧数必须正数。Motion 为 Linear / ArcToTarget / BoomerangReturn / SwayForward / HomingToTarget / BezierToTarget / BallisticArcToTarget；速度曲线为 Constant / EaseInQuadratic / EaseOutQuadratic / EaseInOutSmoothstep。 |
| 条件运动参数 | Arc 高度 >0；Sway 振幅 >0、周期 1..1000；Homing 转向率 (0,1]；Bezier A/B 三维偏移不能同时为零；Ballistic 重力 >0、角度 [0,90)，正切由编译器派生。所有提交的浮点/Vector 分量必须有限。 |
| Collision | `radius` ≥0、`stopOnFirstAcceptedImpact`、`stopOnBlockedImpact`、`maxAcceptedImpacts` >0、`hitSameTargetPolicy` 为 AllowRepeat / OncePerProjectile；layerMask 只读，由目标类型派生。 |
| Impact | `policyKind` 为 DirectHit / AreaPulseOnImpact；Area 的 `areaRadius` >0，才可启用 `triggerOnExpire`；`effectTimelineId` 只编辑引用，不编辑时间轴内容；areaLayerMask 与 legacy presentationProfileId 只读。 |
| Direct / Area HitEvent | 分别维护 `stableEventId`、`targetTypes`、`environmentHitPolicy`（None / BlockedPresentation）、`maxAcceptedHits` >0、`hitSameTargetPolicy`（AllowRepeat / OncePerEvent）、`onHitBuffs`；两套 damage 不互相覆盖。damageTypeId 非空，baseDamage / impactPower / launchPower ≥0，另有有序 tags 和 analysisCategory。 |
| Request | `projectileStableId` 必须指向已有 Definition；origin 仅 Socket，编辑 socketId、localOffset、fallbackRootOffset；aim 为 CasterForwardRange / LockedTargetCenter / LockedTargetOrCasterForwardRange，maxRange ≥0，后两者需 CasterForwardRange fallback，并有 targetHeightOffset。 |

目标类型为 None / HostileCharacter / FriendlyCharacter / Self / ThirdPartyCharacter / DamageableObject / WorldWall / WorldGround / WorldStatic；旧 NeutralCharacter 编辑时规范为 ThirdPartyCharacter。Core 保序并去重目标值；tags 不接受空项，Buff ID 必须正数且唯一，UI 的标签/Buff 列表支持添加、删除和上下移动。targetTypes 一般不可为空；当前 Core 保留 physicalEventsEnabled + collision.radius >0 时直接命中空/仅 None 目标的编译器兼容分支，不把早期“一律非空”当通用限制。原始 mask 不写入 field draft，投影 detail 展示派生值。Socket 候选来自 workspace knownSocketIds；有该权威列表时未知 socket 阻断，无列表时只保留兼容文本与提示，不能宣称任意几何/场景节点都是 M3 挂点权威。

切换类型用一次 compound edit/history node 提交必需字段，并规范已知不适用字段；合法旧参数复用，缺少参数时采用稳定默认：Arc=1、Sway=0.5/1、Bezier A=(0,1,0)/B=(0,1,1)、Ballistic 重力=0.01/角度=45、Homing=1、Area 半径=1。显式提交的非法值仍阻断，不由 Core 默认值掩盖。Direct → Area 优先复用合法 Area event / damage，再从 Direct 继承，否则补默认；回到 Direct 关闭 triggerOnExpire，保留 dormant Area 与未知 sibling。Aim 切回 CasterForwardRange 时 targetHeightOffset 归零；仅改变 aim.kind 且未显式提供 fallback 时 Core 补 CasterForwardRange。

文本在失焦或 Enter 时显式提交；数字、Vector、Toggle、Enum 按各控件 change event 提交，不宣称所有数值输入都采用 delayed 模式。字段批次先 plan；Definition 被多个 Request 引用时按当前 workspace/source revision 显示 Reference Impact，确认 token 与本次路径/引用集合匹配才可提交。Request 字段编辑检查自己的 workspace token，不复用 Definition 共享确认。

PROJAUTH025 表示未知/只读路径，026 表示 value kind 错误，027 表示枚举/范围错误，028 表示条件组合、canonical/batch 错误，029 表示 stale workspace 或缺失/过期共享确认。Problems 按 objectPath / relatedStableIds 与 canonical fieldPath 先区分 Definition / Request，再使用 SourcePath 回退；展开所属 Request/section 并 focus 可定位控件，未知字段只回退 section。workspace diagnostics 与 transient mutation diagnostics 合并去重；成功计划/校验清除旧 transient，不丢 workspace 问题。

External Changed 中的字段命令按原顺序重放，保留 fieldEdits，清除旧 workspace/confirmation token，逐步 plan 并获取当前 token；共享影响需重新确认，取消保留原 dirty/draft，直到完整重放成功才接受新基线。纯 CRUD 的旧重放分支另行保留，不声称全部命令已改成字段级 merge。字段命令参加现有 session/history、reopen 和 M1 expected-hash Apply/rollback 合同。

## Presentation Binding v2 与兼容

Presentation Binding 按 projectileDefinitionId 关联 Definition，viewNamePrefix 默认 ProjectileVFX，持久化配置过的 slots 和 exitPolicy。ResourceKey 保存 id / typeId / variant / packageId，类型必须 GameObject；orientation、localOffset（三维、默认零）和 lifetimeFramesOverride 属于该槽。缺槽表示未配置，不添加 enabled=false；Prefab 路径、GUID、Candidate 路径/receipt/本机 override 不进入 source。缺少整个 binding 可 fail-soft，不阻断 gameplay；仅表现修改与 Combat Definition artifact/hash 分离。

| 固定槽顺序 | 固定 anchor | 允许 orientation | lifecycle |
|------------|-------------|-------------------|-----------|
| spawn | Projectile Origin | ProjectileForward / WorldUp | OneShot |
| flightBody | Projectile Transform | ProjectileForward | Persistent |
| trail | Projectile Transform | ProjectileForward | Persistent |
| directImpact | Hit Point | SurfaceNormal / ProjectileForward | OneShot |
| areaImpact | Area Pulse Center | ImpactNormalOrWorldUp / WorldUp | OneShot |
| blockedImpact | Hit Point | SurfaceNormal | OneShot |
| expire | Final Position | ProjectileForward / WorldUp | OneShot |

Anchor 只读；lifetime override ≥0，Persistent 必须为 0。acceptedImpact / blockedImpact / expiration / externalRemoval 只允许 Complete 或 Dissipate，默认依次为 Complete / Dissipate / Complete / Dissipate；runtimeReset 固定 Immediate 且只读，普通退出不可选择 Immediate。SetPresentationSlot / ClearPresentationSlot / EditPresentationExitPolicy 为 typed 命令，按所属 Definition 而非选中 Request 定位，参加共享确认、history、Problems slot/exit 定位与原子 apply。

Legacy v1 的 prefabResource 只映射 flightBody，viewNamePrefix 保留；读取给 PROJAUTH013，不写 source。显式 MigratePresentationBindingV1ToV2 只改 canonical binding：保留未知 sibling、补规范 exit、删 prefabResource，既有 flightBody 不被无条件覆盖，不推断 impact/blocked/expire，也不将 legacy presentationProfileId 变成 ResourceKey；后者给 PROJAUTH014，需人工处理。迁移前非迁移编辑由 PROJAUTH013 阻断，不生成 draft。迁移仍受共享确认、expected hash、undo/reopen 与 M1 rollback 约束。

Compiler 输出 Presentation schema 2，binding 按 Definition ID、槽按上表排序；规范化 ResourceKey / orientation / offset / lifetime / exit 进入独立表现产物与 binary payload hash。五个 OneShot 槽必须解析紧凑 VfxDefinitionId，无法解析给 PROJAUTH011 并阻断 Apply；旧缓存 ID 每次重置后按当前 Key 重解析，不能绕过 catalog。flightBody / trail 可用 catalog-only Published Effect，保留完整 Key，binary compact ID 为 0。PROJAUTH012 阻断非法持久 Key / orientation / lifecycle 参数 / exit；可能同帧的 OneShot 多槽重用同一 Key 给 PROJAUTH015 warning。

JSON / binary importer 都支持显式 v1 兼容与 v2；v1 只映射 flightBody，writer 显式 schema 1 时仍写真正的 v1 payload，普通 compiler 写 v2，未知 schema 拒绝。JSON 错误抛 InvalidDataException；binary 返回结构化 load failure，检查 header/schema、payload hash、entry count、truncated/trailing bytes、重复 ID/槽和枚举/槽合同后才返回临时 registry，失败不暴露部分 registry。Runtime 预建 numeric ID 字典与固定七槽数组；TryGetV2 / TryGetSlot 缺失返回 false/default，旧 obsolete API 是初始化期缓存的 flight-only adapter。这里只确认源码结构，不沿用历史 0 B 分配测量或测试次数为本次性能/验收结论。

## VFX 本机选择与发布边界

资源卡、Picker、Ping 正式 Prefab 与预览使用共享 Published/Candidate resolver；Formal 选择清除对应本机 override 并提交稳定 ResourceKey。有效 Candidate 只能预览当前槽已绑定的同一 Key，不产生内容 command/history/draft/session/generated/hash 改动；stale 或跨 Key Candidate 不可选择。角色、推荐 anchor/space/lifecycle 是当前 Picker 的排序/提示信息，语义不匹配本身不阻断选择；固定槽 orientation、Persistent lifetime、OneShot compact ID 等持久结构约束仍严格校验。两种约束不可混为一谈。

显式 Inspector Production/Player 检查拒绝 legacy v1，并由 ProjectileAuthoringVfxProductionGate.EvaluateSlot 拒绝 unresolved、Candidate-only、stale、非 Production + Ready，形成 transient Error。当前 helper 不检查语义 role/anchor/space/lifecycle mismatch；已查到的产品 consumer 为 Inspector 按钮，不能据此宣称所有 publish/Player build 流程已接入。语义建议不升级为选择、保存或 Production 阻断规则；严格校验仍针对实际 ResourceKey、槽参数、解析及发布就绪条件。

源码入口为 Tools Core 的 ProjectileAuthoringFieldEditing.cs / ProjectileAuthoringPresentationEditing.cs / AbilityAuthoringCompiler.cs，CLI RuntimeCatalogBinaryArtifacts.cs，package Editor 的 ProjectileAuthoringWindow.RuleInspector.cs / PresentationInspector.cs / ProjectileAuthoringVfxSelectionBridge.cs、共享 CharacterActionVfxSelection.cs，以及 Combat.Projectiles.Unity 的 BindingV2 / JSON、binary importer / registry。对应 M3、M4 与 RuleInspector 测试定义用于核对合同；本次未运行这些测试、Unity 或内容写入 CLI。

## Core 与 CLI 事务合同

Workspace 保留 per-ID source 的原始 bytes、JSON tree 与 SHA-256；语义 no-op 返回原 bytes（包含 BOM / newline），完整文档 proposal 改动或移除未建模的未知字段由 PROJAUTH005 阻断。typed 字段命令在 raw tree 定点修改，未触及的未知 sibling 保留。Request / Spawn Profile、stable Definition / numeric ID、Presentation Binding、ResourceKey、consumer 与 timeline/profile 引用分别建索引，支持多个 Request 指向一个 Definition；不以 ID 后缀推断身份。源码仍在 package-local config/projectiles，generated 是派生产物，不能当第二可写权威。

当前普通 compiler 输出 Definition schema 5、Spawn schema 2、Presentation schema 2；Generated-only 读取兼容 Definition 3/4/5、Spawn 1/2、Binding 1/2。Workspace/diagnostic source path 和 transaction receipt path 使用相对路径；CLI 的 output 参数回显不承诺将任意绝对路径脱敏。

| CLI 命令 | 当前读写边界 |
|----------|--------------|
| validate | 读取并编译校验；仅显式 report-out 写 validation report，不写 source/catalog。 |
| workspace | 输出 Editor inventory、引用、diagnostic 与 hash；可写显式 workspace 输出文件，不改内容权威。 |
| mutate | pure planning，输出版本化 draft / projectedWorkspace / report，不写 source/generated/mirror。 |
| compile | 不改 source；事务写三份 projectile JSON、三份 bytes 与 compile report。 |
| apply | 校验 draft schema、expected hash，隔离 staged package 重新编译，无 blocking 后同一事务提交 source、获授权 consumer 与七份 Tools 产物。带 project-root 时另纳入六份 Unity Generated 和六份 Player mirror；独立 CLI 不传该参数则不更新项目双目录。 |
| import-unity | 编译一次，去掉 compile report，将同样六份运行时产物原子写入 Generated 与 Player mirror。dry-run 输出目标集合而不写这十二份产物；显式 report-out 仍写审计报告，当前 dry-run 不输出逐文件 hash 清单。 |

Apply 对既有 source/artifact 要求 expected SHA-256，新建使用 Missing sentinel；source delete、upsert 与允许的 consumer 更新都参加同一事务。目标唯一、路径不可逃出对应 root，Generated 与 mirror 不能重叠。stage 前、每项 commit 前及最终提交结果均检查 hash；失败逆序 rollback 仅恢复仍匹配本事务 after hash 的文件，不覆盖期间外部写入。无法安全回滚时保留 recovery backup，receipt 提供结构化 diagnostic、relative path、before/after hash、rollback/freshness 与 RecoveryRequired，不能把所有失败都描述成必定恢复原状。

project-root apply / import-unity 成功 receipt 为 Committed + UpdatedAtomically；失败依实际 rollback 为 RolledBackWithGenerated 或 RecoveryRequired。不带 project-root 的 apply 为 NotUpdatedByTransaction。CLI 退出值 0=Ready、1=tool/transaction error、2=blocked、3=unsupported schema。当前 Core 的 IProjectileAuthoringTransactionFaultInjector 和接受该参数的 Execute overload 是 public；CLI 未暴露故障注入开关。

源码入口为 Tools Core 的 ProjectileAuthoringWorkspace.cs / Transaction.cs / Dtos.cs / AbilityAuthoringCompiler.cs 和 CLI ProjectilePackageCommands.cs。上述是源码合同核对，不是新一次 CLI 执行、事务故障测试或 Player 验收。

## Runtime 生命周期桥

CombatProjectileVfxBridge 接受正式 frame event buffer，按 Spawn → Direct/Blocked Impact → Area Pulse → Expire 路由。固定事件 kind 数值为 Spawn=1、DirectImpact=2、AreaImpact=3、BlockedImpact=4、Expire=5、ExternalRemoval=6、RuntimeReset=7、LateAttachRebuild=8。去重键为 projectileId、projectile.LastFrame、kind、该来源数组中的 sequence；固定容量环形记录只保证记录尚未被淘汰的窗口，不宣称永久去重。tracked capacity 溢出记录 diagnostic，不增长 tracked array。

| 事件 / 路径 | 展示行为 |
|-------------|----------|
| Spawn | 原点 spawn one-shot；跟踪成功才 Build flightBody / trail。 |
| Direct sweep | accepted Damage（HP damage 为 0 也可接受）走 directImpact；Blocked 走 blockedImpact；Rejected 不走命中槽。被消耗才退出 Persistent，穿透未消耗继续飞行。 |
| Area Pulse | 按 pulse trigger 发一个 areaImpact，不按 0/1/N 个 target hit 复制爆炸；area target impact 不再发 directImpact。 |
| Expire | 按正式 expiration 事件发 expire；expire-triggered Area 仍按其 pulse 发一次 areaImpact。 |
| Late attach / disappearance / Reset | age >0 的 active snapshot 只 Rebuild Persistent，不补历史 one-shot；消失按 externalRemoval，不伪造 Expire；Reset Immediate 清 bounded state。 |

One-shot 使用 binding 中的 compact VfxDefinitionId，经 VfxRuntimeModule 路由，采用权威事件 position/normal、共享 slot pose/orientation/localOffset，保留 Source/Target context 与稳定 trigger/sequence；不借 projectile Trace 生成第二套命中事件。缺 binding/slot、非法 compact ID、backend 不可用以 diagnostic counter fail-soft，不改变 gameplay。初始化复制 custom extension 注册顺序；首个 claim 确定采用，重复 claim 计数。claim 只抑制对应 one-shot，Persistent Build/Rebuild/Exit 保持独立。

Unity presenter 分别拥有 flightBody / trail，两者可共享 ResourceKey pool；Complete / Dissipate 进入 retiring，等待 lifecycle complete，Immediate 直接释放。CharacterTest 正式 Tick 保留 EffectTimeline dispatch、target reaction、材质/盾反馈和 Arc Mine / Poison Pool / Rune Turret / Laser / Tornado 等 mechanic/custom 路由，再接 bridge；presentation tick 只 TickRetiringViews，不以旧 Sync 绕过 bridge。共享 source lifecycle 与独立 target reaction 不能合并去重。

当前 CreateActiveView 在 pool 缺失或耗尽时仍可能 Load / Instantiate，再记 pool exhausted；创建 view 仍拼接名称。因此当前不保证耗尽即跳过、固定池容量或完整路径0 B，性能须按实际资源与路径单独测量。源码入口为 CombatProjectileVfxBridge.cs、CombatProjectileUnityPresentation.cs 和 CharacterTestCharacterCombatTraceSystem.Projectiles.cs / SliceCompositionBuilder.cs / SliceServices.cs；未重新运行历史 allocation 或 PlayMode 测量。

## 确定性预览与清理

Compile input 取当前 projected Definition / Request 与 workspace hash，LowFreq 构建正式 CombatProjectileRuntime / CombatPhysicsWorld 和 scripted fixture，再向前 Tick 编译 0..total 的轨迹、marker、active snapshot 与 runtime SaveState cache。场景为 Miss / DirectHit / Blocked / AreaHit / Expire / Pierce / LateAttach；同一输入和 fixture 的结果可比较，但这些 fixture 不等同于任意角色真实资产验收。Ballistic 复用正式 TrySolveLaunchArc，不另写轨迹公式。

有 Request 时按其 socket/localOffset/fallback、aim/maxRange/targetHeight 与 tracking identity 解析；只有 Definition 时使用显式 synthetic main-hand + LockedTargetOrForward fixture 并给 PROJAUTH022 提示。当前 Request resolver 可读 source/generated profile，但不能把缺省 fixture 当挂点权威。EffectTimelineReference 仅只读标记，不在 sandbox 执行时间轴。

正常连续前向播放由 retained runtime 和复用 event buffer 经正式 bridge 路由；seek/scrub 从 cache RestoreState，以 empty events 重建 flight/trail，不补历史 Spawn/Impact/Expire。Restart 重置 runtime 和 transient VFX state，再从 frame 0 前向路由；位置、场景帧和播放模式属于 UI session，不生成内容 history。marker/trace 使用 frame、projectileId、kind/slot、sequence 身份并共享 slot pose、compact ID、lifetime/exit 参数，非 UI 文本计数代替运行时事实。

播放使用共享 AuthoringTimelinePlaybackController，实际对象归 AuthoringPreviewRenderScene / PreviewRenderUtility scene owner，保留有效 Published/Candidate 本机解析。Scene Edit 必须显式注册，暂停并钳制当前 frame，mouse-up 才提交一个 scalar/vector typed mutation；Escape、文档切换、关闭取消 pending drag，target fixture 调整不写内容。有效 refresh 恢复模式意图；Generated-only、无选择或 dispose 清理编辑注册。窗口 reload/PlayMode/disable/destroy 释放 callbacks、preview scene、host/presenter/VFX 与本机 catalog；Dispose 可重复调用。

诊断必须结合 producer 与 severity：preview 的 PROJAUTH026 当前为 unsupported scenario，窗口呈 Error。PROJAUTH025 cleanup error 在本次已查 preview producer 中未见，不据此宣称有 cleanup 门禁；规则 Inspector 的 025/026 含义另见前节。PROJAUTH023 的逐帧验证/恢复失败路径和 PROJAUTH024 的资源缺失不证明已运行完整 golden/parity 矩阵。

当前真实 one-shot 播放由 ProjectilePreviewPresentationSession 调用 host.Instantiate，再到 scene owner 的 Object.Instantiate；没有可确认的 one-shot 预热池。Lifecycle 测试中 warmed runtime identity/restart 循环不能覆盖所有实际 prefab 的播放分配，不能据此保证完整 viewport 0 B或全部路径不 Instantiate。源码入口为 ProjectilePreviewSandbox.cs / SpawnResolver.cs / PresentationSession.cs / UnityObjectHost.cs、ProjectileAuthoringWindow.Preview.cs、ProjectileSceneViewEdit.cs 和共享 AuthoringPreviewRenderScene.cs；本轮未运行 Unity 或这些测试。

## Consumer 与兼容快照

Projectile Authoring 是唯一 Unity projectile writer；CharacterAction 仅维护其自有 gameplay event 的 Request 引用、trigger frame，以及自有 event identity/kind 等高级参数。Request picker membership 来自已加载 store 中的 RequestId、Spawn 和 generated Definition/Profile，可包含 generated compatibility record，不依赖 .projectile 后缀。未知/旧引用保留并显示健康度，不静默改写。其摘要来自已保存 source / generated 的只读 CharacterActionProjectileAuthoringStore，不是 Projectile 窗口未保存 draft projection；CharacterAction 预览读取最后正式导入 catalog。

| Consumer | Projectile 引用与保留所有者 |
|----------|-----------------------------|
| Fireball / Fireball Burst | 两条独立 Request/Definition；action trigger timing 归 CharacterAction，Burst 的 pulse lifecycle 与 target reaction 分开。 |
| Arc Mine | character_test.arc_mine.deploy 引用 arc_mine.projectile；落地 trap、触发/fuse、blast tuning 归原 mechanic。 |
| Rune Turret | deploy 与 shot Request；ownership、think cadence、targeting、duration 归 turret runtime/tuning。 |
| Tornado | 通用 Request；persistent/tick consumer 行为归其 runtime。 |
| Laser / Beam | spawn-profile registry 解析；path/beam custom behavior 归 consumer。 |
| Poison Pool | bottle flight projectile 集中创作；pool lifetime/tick/area 归 pool runtime。 |
| Meteor Barrage | .0..5 六 Request 共享 canonical character_test.meteor_barrage.projectile Definition；canonical 本身不要求 Spawn，barrage scheduling 归 consumer。 |

普通 gameplay Request 不因名称被纳入 projectile picker。CharacterAction 的旧 projectile command/projector、generated writer、dirty/save/history 和 SceneView edit 路径在当前源码中未见可写入口；Save 的文件集为其 action/profile/library/mechanic 等自有内容。只读摘要和 deep link 不授予第二套 projectile 写权限。

现有 source 可包含 legacy prefabResource；v1 只映射 flightBody，显式迁移与 PROJAUTH013 处理见 Presentation 节。运行产物由正式 compiler/apply/import 流程维护；Generated 与 Player mirror 字节相同不能单独证明 source语义、新编译 freshness或Player build验收。

当前 CharacterTest generic-hit filter 固定抑制 Fireball Burst / Meteor profile 的 source explosion，其他 target reaction 保留；未见按任意 registry v2 存在性切换所有 legacy impact 的统一 gate。没有 binding 时 embedded fallback registry 只提供 flight-only 映射。不能把这些特定profile规则推广为所有consumer均已接入通用v2/v1切换。

## 未完成范围

统一 Production/Player 发布门禁、全部 consumer 的 v2/v1 impact 切换，以及完整运行/预览路径的性能验收尚未确认。需要这些扩展时，先明确发布入口、兼容切换与测量范围，再实施；它们不属于上文现行保证。本次只核对源码并整理文档，没有重新执行 Unity、CLI 内容写入或测试。
