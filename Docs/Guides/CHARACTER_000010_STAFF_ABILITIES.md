# 000010（C006_1）法杖火球与火焰枪

> Status: Current
> 状态：角色适配及 AI Lab Profile 已落盘并完成原生技能验证；正式角色获取待协调
> 2026-10-10：已验证 Mage 动作、杖端发射、前摇特效跟随、锁定瞄准、伤害、三目标穿透及降防 Buff 到期。验证使用隔离 SaveGame 和临时运行时实体，不代表玩家已正式拥有 000010。

本说明仅覆盖该角色的两项技能。执行合同见 [CharacterAction](../Interfaces/CharacterAction.md)，角色组合见 [CharacterApplication](../Interfaces/CharacterApplication.md)，编辑器数据权威见 [CharacterStudio](CHARACTER_STUDIO_DATA_OWNERSHIP.md)。本任务不修改主卡／配件规则，不修改真实用户存档。

## 角色作用域适配

| 技能 | 原 Ability / Common / Projectile | 000010 动作 | 真实 Mage 动画 | 释放帧 / 前摇 | 总时长 |
| --- | --- | --- | --- | --- | --- |
| 火球 | 910001 / 302 / 91001 | 1090004 | Combo_Attack_01_01 | F20 / 0.333 秒 | 75 帧 |
| 火焰枪 | 910003 / 304 / 91003 | 1090005 | Combo_Attack_01_04 | F30 / 0.5 秒 | 99 帧 |

新动作由正式 ID 生成器分配；Common 302、304 源配置和原 1090000–3 保持不变。000010 的 staff 组合通过 actionRoleOverrides 和 abilityActionBindings 选择专属动作。Runtime 使用实体有效 ActionSet 解析覆盖，权限检查和提交仍归属原 Ability 的冷却／资源成本；重复 Ability 绑定拒绝提交。

60 Hz 时间线从 F0 开始，前摇不早于 F12。火球 Startup F0–19、Active F20–22、Recovery F23–74；火焰枪 Startup F0–29、Active F30–32、Recovery F33–98。速度 1，动画过渡 0.08 秒。初次原生采样发现 F24 已回摆，最终改为杖端位于前方的 F20；旧 F24 回执仅是中间诊断。

原玩法保持不变：火球冷却 60 帧、基础伤害 10／削韧 8、速度 0.35／帧、寿命 90、首次命中结束；火焰枪冷却 75 帧、基础伤害 7／削韧 5、速度 0.55／帧、寿命 80、OncePerProjectile 最多命中三个不同目标。Buff 601009 是降防：300 帧、RefreshDuration、最多一层、Defense 属性 3 加值 -3，不是灼烧。伤害仍由既有属性结算，不能把基础伤害当作每个目标的最终伤害。

## 杖端与正式发布

新增 staffTip（Pose 302），与 mainHand 绑定同一手骨。现有 staff 模型末端 (0, 0.93, 0) 与 mainHand 旋转／(0, 0.06, 0) 偏移合成手骨局部位置 (0.93, 0.06, 0)。新增 Request 422／423 引用原 Projectile 91001／91003，只把 origin 指向 staffTip；既有 aim、命中策略、Buff 和弹道定义保持原样。两项动作的前摇 charge VFX 也绑定 staffTip，并在事件结束时停止。

Socket Bake 从已发布 SpawnProfile 收集 SendGameplayRequest Socket，Runtime 按实体 GeneratedPackageRoot 查询。原 AnimationMode bake 包含动画根位移而 Runtime 忽略该位移，现复用既有 WeaponTracePoseSampler；缓存版本为 v3 runtime-pose-v1。原生 Socket Bake Ready：3 轨道、332 帧、17582 字节，binaryHash 为 sha256:3908214dcc151bc9c6e0b1265967d2b5496e5f964c4f816a6b34c37b7d404902。

正式 projectile import-unity、character import-unity、Unity 重建与导入回执、Action Player 工件及全局资源包构建均已执行，没有手写 Prefab、.anim 或 .meta。最终角色 sourcePackageHash 为 sha256:ce86cfb904845e0c0659cb86d68a6e8f78d7bd865d2dfc9468d3c098c7f491a9；资源构建／发布产物校验通过，24 bundles、283 entries。这是资源包验证，不是独立 Player 可执行文件验证。

角色包新增 character_tactical_defaults.json，初始 actionIds 为 action:1090004、action:1090005，并纳入 manifest；它是模板授权声明，不会创建角色物品所有权。

## 原生验证与修复

本地回执目录为 `.agent/mage-staff-skills-20261010/`，属于忽略的工作状态。

- C#：native-compile-final.json、native-compile-stage2.json、native-compile-stage2-socket.json 均通过完整 refresh／compile 起止／domain reload／Editor ready 门禁。最新 job 36fcea98fbbf43ba9062f5a22da4b1e8，Console error 0。
- 原生 EditMode：native-tests-final.json 为 5/5（两项角色作用域权限／冷却回归及 000001／000004／000008 根位移采样回归）；native-target-lock-tests-stage2-passed.json 为 2/2。后两项检查显式 Set 与 Toggle 的 caster／target、正式 Hurtbox 距离、视角切换、重复拒绝、取消、System Set 和失效清理。首次测试初始化超时，没有执行测试；独立失败回执保留。VFX 数值 Socket 缓存的后续三行修复由最新编译和直接 Play 定位核验覆盖，未重复运行上述测试。
- 锁定命令：SetTargetLock 的 TargetId 是目标 NPC；提交方法现显式接收调用方已验证的 primary，避免把 NPC 再解析为 caster。SourceId 仍是输入来源类别。权限、取消、事件及网络 schema 保持原语义。原生句柄 SourceEntityId=3、TargetEntityId=4、LogicalHurtboxId=77；发射朝真实锁定 Hurtbox，而非原先的 CasterForwardRange 回退。
- VFX 注册：接管时 AI Lab 序列化 GameManager 的 Profile 在工作树及 HEAD 都为空，导致应用 catalog／provider 未注册。本轮仅通过 Editor 为现有 GameManager 绑定共享 CharacterTestSceneProfile.asset（GUID aade23f767d4b4f7699bf62fa44ebac0），未修改共享 Profile 或生成场景；其引用现已按下述单属性精确写入落盘。
- VFX 挂点：CharacterTestRuntimeCharacterView 的 Socket 缓存原只解析 BindingId 字符串，忽略正式数值 SocketId；已加入数值键。stage2-charge-socket-fixed.json 的 F10 VFX 与实时 Socket 位置完全一致：(0.8752488, 0.7436787, -0.478868276)，没有采用固定回退位置。
- 火球：stage2-fireball-windup.json 显示释放前没有弹体；stage2-fireball-release-final.json 的 F20 发射 91001，origin (0.276267, 1.341461, 0.995987) 与实时 Socket 约差 0.0000013 米。Mage 动作播放成功。stage2-fireball-impact-final-live-views.json 显示一次接受伤害、HP 100→91，飞行与命中特效各有实际 GameObject、启用 Renderer 和粒子；截图在 screenshots-stage2/。
- 火焰枪：stage2-flame-windup.json 的 F1–29 无弹体，stage2-flame-release.json 的 F30 发射 91003。stage2-flame-penetration.json 显示三个不同目标各 100→94，第四目标保留此前火球后的 HP 91，三次命中后弹体结束；继续 90 帧无额外发射或命中，动作恢复 Idle。stage2-flame-flight-live-views.json、stage2-flame-impact-live-views.json 和截图确认飞行及三个命中特效有实际渲染实例。
- Buff：stage2-buff-before-impact.json、stage2-buff-applied.json、stage2-buff-end-minus-one.json、stage2-buff-expired.json 为直接权威状态读数。命中 TraceId=18，Buff SourceId=18，StartFrame=34／EndFrame=334，单层非永久；Defense 10→7。处理帧 333 后仍存在，处理帧 334 后 Buff／Modifier 消失，Defense 恢复 10。未手动添加、删除 Buff 或修改属性。
- 原冷却：native-cooldowns.json 确认 910001／910003 仍按原 Ability 提交 60／75 帧。最新 Play 的 3988 个 C# 文件未变化，清单哈希 9ce73cf312e8c9f4dccf8539e1976d8ef68906356f7e66b4cabd81136f150663。最新 Play 结束前后 UnityEditor.LogEntries 的实际 Console error 均为 0。

三目标并发命中产生两条 VFX pool exhausted 诊断；源码路径会直接 Instantiate，三个命中特效实际均存在。这是冷池分配诊断，不是 NotFound 或丢失实例；本任务未调整共享池策略。临时新生 LocalPlayer 的整组动画诊断曾为 Ready=false／locomotion animation failed: Success，但两项 Mage action 的播放结果成功且截图已检查；不据此宣称完整 locomotion 已验收。原第一轮 NotFound、锁定回退和旧 F24 回执保留作为已修复问题的历史证据。

## 使用与交接边界

在 CharacterStudio／Character Action Authoring 中选择 000010、staff 组合及动作 1090004／1090005，可检查正式专属动作。运行时原入口仍是 Ability 910001／910003，000010 的有效 staff ActionSet 负责适配。

AI Lab Profile 绑定现已落盘。持久化前重新用 Unity Editor SaveScene(saveAsCopy:true) 生成当前内存副本，并与磁盘逐字节／逐字段核对，唯一差分是现有 GameManager（local ID 250961279）的 _profile 引用。项目现有保存器会保存整活动场景，未用它保存本次绑定；Unity MCP 在 Editor 内以该原生副本为候选，核对原磁盘哈希和最新内存副本后，通过原子 File.Replace 精确写入这一处 Native 生成差分。没有手写引用值，没有保存或重载活动场景，没有修改 .meta。NativeSceneGuard/persistence/ 保存了操作前磁盘与内存副本、候选及 persist-receipt.json。

profile-persistence-native-reopen.json 记录从已落盘原路径经 EditorSceneManager.OpenPreviewScene 独立反序列化，Profile GUID 为 aade23f767d4b4f7699bf62fa44ebac0，火球／火焰枪 Prefab 和 PublishedVfxBindings 非空；原活动场景及 GameManager 身份未改变。这验证了未来重开所读取的序列化引用，不是再一次完整 Play 验收。

原场景 GUID 仍为 e8d7db015b8dda445a0bb700e933373b，六个活动根对象和两项水面反射均保留，Play=false、dirty=true。写前 SHA256 为 69915289b202997569cbcc4b1649ceb09c1adb58171c8980c12a4fc561b42b11，写后为 87022b31b253f67902f246e5eaeafab470e9d26ab4312d662964e43236f24b54。剩余 dirty 状态和其它模块／用户草稿没有随绑定保存或丢弃。早先 MCP 测试运行器自动保存过脏基线，但发生在 Profile 绑定前，基线序列化字节当时与磁盘完全一致；随后已恢复 dirty。

验证存储根使用 `MX_CHARACTERTEST_TACTICAL_SAVE_ROOT` 指向 .agent/ 内独立目录，结束后已恢复原值 null。user-save-guard-after-stage2.json 确认真实用户存档目录 35/35 文件哈希未变。本轮 000010 由临时运行时 Spawn 实体执行，没有伪造正式物品获取或修改真实用户授权。

正式 character_item_inventory.json 仍只有 000001／000002，D421 没有分配 000010 条目。玩家装配页面尚不能获取／选择 000010。新增定义将改变 Items definitionHash，旧档恢复要求严格整表 hash 相等；应由持久化 owner 完成可信增量目录兼容／迁移，再经正式 AcquireCharacterItemAtTick 或 GrantContentAtTick(Character) 获取，并在隔离存档验收授权、装配、保存和重载。本任务没有放宽 hash 门禁、手写新 Item ID、重建旧档或新增获取 UI。

尚未验收正式玩家 000010 获取／技能装配、切换／卸装拒绝、受击中断恢复、完整 locomotion、独立 Player／目标设备；不能把临时实体 Play 验证写成这些入口已通过。
