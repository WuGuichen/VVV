# Animation Designer 临时角色装配

> Status: Guide

> 快速替换与 Git 撤回：2026-09-23

## 使用

打开 `MxFramework → Character → Animation Designer Launcher`，依次选择角色、装备组合和动画片段，点击“开始编辑”。

- 角色来自已导入角色包；装备组合保留各自 Loadout 身份，默认使用该角色默认组合。“隐藏武器”只改变预览显示。
- 动画列表可以搜索；也可以向“动画片段”拖入 `.anim` 或 FBX 内的 AnimationClip 子资源。Project 中选中片段后，右键 `MxFramework → Edit with Character in Animation Designer` 可以带入片段。
- 独立舞台仅显示临时装配。Animation Designer 负责时间轴播放、修改、设置保存及动画导出；“聚焦模型”重新对准角色。
- 调整窗口内选择不会立即修改舞台。点击“应用选择”才更新；同角色换装保留 Animator 与插件当前设置。
- 关闭 Launcher 不结束编辑。点击“结束编辑”回到原场景；切换角色或主动结束时，对未保存设置提供保存、取消和继续选项。保存使用插件原生 AnimationDesignerSave 格式，导出动画仍在插件中操作。

### 直接编辑片段时实时预览

在 Launcher 选好角色、装备组合和动画片段后，点击 **普通片段预览**。工具会在同一临时舞台装配角色与武器，将 Unity Animation 窗口绑定到临时角色和所选片段，并启用原生 Preview；可直接拖动或播放时间轴，同时在 Scene View 观察姿态与根位移。修改 Root T 等曲线时，同一帧的预览会重新采样，不要求先保存片段。循环回到首帧时，角色回到起点。启动前需关闭 Animation 窗口的录制模式。

日常修复根运动时，直接点击 **根运动曲线预览**：它执行同一实时预览流程，并切到 Animation 窗口的 Curves 视图，展开、选中 Root T.x/y/z。快捷入口只定位曲线，不调整关键帧，也不自动保存。片段没有 Root T 曲线时会给出提示。

曲线修改后，点击 Launcher 的 **保存当前动画** 才会把修改写入磁盘。预览进行中它保存正在预览的片段；没有预览时保存 Launcher 选中的片段。按钮仅支持独立 `.anim`，只写这一份资源，不保存场景或其他脏资源，也不创建 Git 提交；FBX 子片段需要先导出。按钮会显示当前动画是否仍有未保存修改。关闭预览、切换场景或脚本重载都不是保存动作，重要修改请先点击保存。

此模式只预览片段本身，不叠加 Animation Designer 插件效果或 CharacterAction 烘焙位移；按钮不会保存或提交 `.anim`。改选角色、装备或片段后，再点一次预览按钮才应用选择。点击 **结束预览**、离开临时舞台、进入 Play Mode 或脚本重载会释放临时角色并恢复原选择；关闭 Launcher 窗口本身不结束预览。预览期间先结束预览，再使用“快速替换并提交”或“撤回上次替换”。

首次打开需要选择角色。角色、各角色的装备以及片段选择按项目保存在 EditorPrefs；片段使用 GUID + local file ID，能区分同一个 FBX 的子片段。失效选择保留并显示诊断，不会自动替换。刷新目录只更新候选。

## 临时会话边界

窗口和第三方适配器归项目 Editor 所有，框架只提供通用角色装配 API。模型先在 Preview Scene 中完成装配预检，再移入舞台专用的普通附加场景。舞台继续提供隔离的 Scene View／Hierarchy 和面包屑导航；原场景保持打开且保持原来的 active scene。专用场景不保存，退出时通过 `CloseScene` 关闭。正式 Prefab、角色配置和默认装备不发生写回。

Animation Designer 的 `OnGUI` 对所有 `EditorSceneManager.IsPreviewScene(target.scene)` 都会直接返回，并显示 “Detected Prefab Isolated Mode”。因此不能把 Preview Scene 当作最终插件编辑容器，也不能用 `IsReady`、内部采样或直接调用导出成功来代替窗口验收。适配器在交接前再次检查场景兼容性；不修改插件源码，不开放真正的 Prefab 隔离编辑。

入口从 MainStage 发起；若当前在其它 Prefab／隔离舞台，先退出它。Unity 不允许在仍有未命名场景时新建普通附加场景，入口会提示先为该场景保存路径；已有命名场景即使有未保存修改，也不会被自动保存或丢弃。

装配使用正式装备解析、资源目录和挂接姿态。预览挂点挂在实例的实际骨骼下，武器随正在编辑的姿态即时更新，不依赖原动画的 Socket Bake。临时 Animator 默认开启 `Apply Root Motion`，由 Animation Designer 的采样驱动根位移，避免预览原地化和导出丢失水平位移。角色运行脚本、输入、碰撞与其它动画播放器均不运行。Humanoid 会准备插件默认肢体；Generic 角色仍可使用插件的骨架／肢体设置工具进一步调整。

离开舞台、关闭插件、进入 Play Mode 或脚本重载会结束临时会话。重载后只恢复选择，不恢复未保存的插件修改；进行这些操作前请使用插件保存设置。Launcher 的“取消”只适用于它发起的切换／结束对话框，不能撤销 Unity 已发生的舞台导航或重载。

缺少模型、武器、挂点、Avatar 或不匹配的片段时显示诊断；换装预检失败保留原模型和武器。不自动导入角色，不自由配装。快速替换只更新所选动画资产的内容，动作资源映射与角色配置仍由原有制作流程管理。

## 快速替换、提交与撤回

1. 在 Animation Designer 中用 **Save As New Animation Clip** 导出一个新的独立 `.anim`。
2. Launcher 上方“动画片段”是要替换的旧资源。“替换与 Git 版本 → 已导出新版”自动带入当前编辑会话的最近一次导出，也可手动拖入或点击“带入最近导出”。核对新旧片段后点击 **快速替换并提交**。
3. 工具先自动保存新版的未保存修改，保留旧资源的 GUID、local file ID、名称与 `.meta`，通过 Unity 写入新版曲线、事件及动画设置，再对这一个 `.anim` 创建本地 Git 提交。提交确认成功后，无引用的新版 `.anim` 及其 `.meta` 会清理；被 Animation Designer 设计存档、其他资产或配置引用的新版副本会保留，并显示引用来源，不阻止原名动画替换与提交。所有原有引用生效；共享动画会影响所有使用位置。
4. **撤回上次替换** 将这一个动画恢复为该次替换提交的父提交版本，并创建新的恢复提交。后续对其它文件的提交会保留；可以连续撤回本工具的多次替换，不会在两个版本之间反复切换。

目标 `.anim` 和 `.meta` 必须已经被 Git 提交过，并且当前工作区与暂存区均没有它们的未提交修改。其他文件可以有暂存或未暂存工作。FBX 子资源、源目标相同、动画类型不匹配、分离 HEAD 以及正在进行的 merge/rebase/cherry-pick 不进入替换。新版必须是已经导出的独立 `.anim`，允许它仍有 Unity 未保存修改，按钮会单独保存新版；此按钮不在原片段上直接烘焙。

工具在写入目标动画前检查 Git 索引锁，并把操作前的动画原件备份到 `Library/AnimationDesignerReplacements/`。后台读取目标状态时不刷新 Git 索引。若提示 `index.lock`，先等待正在运行的 Git 操作结束；确认没有 Git 进程后，清理过期锁并重试。锁仍存在时不会开始替换；若锁在提交期间出现并阻止 Git 恢复命令，工具在确认分支、目标内容、`.meta` 和暂存状态未变化后，从本地备份恢复目标动画。提交完成后若仍检测到锁，工具会在结果中提示并记入本地操作记录。

新版副本必须尚未被 Git 跟踪。替换前检查已保存资产、配置、已加载场景和已加载脏配置／Controller 的引用；已有引用只阻止删除副本，替换与提交仍可完成，不重写或丢弃插件设计参数。对可清理的副本，清理前再次检查文件内容、`.meta`、脏状态与引用；期间发生变化会保留副本并报告“动画已提交，但新版副本尚未清理”。提交失败不会删除新版。目标内容本来就与新版一致时不创建空提交，只清理通过检查的新版副本；已有引用的副本仍保留并显示原因。撤回只还原原名动画的上一版，不重新生成已清理的导出副本。

提交采用单路径 `git commit --only`，保留正常 Git hooks 和签名行为，提交留在当前本地分支。正常提交失败且目标仍是本操作写入的内容时，只恢复当前动画；若发现分支、HEAD、目标内容、`.meta` 或目标暂存状态被并发修改，会保留现场并显示诊断，不重置整个仓库。hooks 若改变了提交范围或提交父节点，会报告异常，不自动改写已产生的历史。

成功替换或撤回后，活动预览以新的基础片段重新开始，避免把原设计效果再次叠加。原设计参数和事务诊断保存在 `Library/AnimationDesignerReplacements/` 的本地 JSON 中；参数备份不是正式插件存档，需要正式保存设计时仍使用插件 New Save。关闭 Launcher 不取消已经开始的 Git 操作。

撤回记录由 Git 提交中的 `Animation-Designer-*` trailers 重建，清理 Library 不影响已提交替换的查找。若动画后来又被其它操作修改，或路径／身份发生变化，工具会阻止直接撤回旧替换。每次只恢复动画资产，不恢复整份工作区或设计参数。

2026-09-23 验证：`AnimationClipGitRepositoryTests` 的 7 项 Git 测试通过，覆盖单文件提交、其他暂存／未暂存内容保留、后续无关提交保留、连续撤回、目标脏状态拒绝、失败恢复和并发保护；测试使用 Library 下独立的临时 Git 仓库。`AnimationClipContentReplacementTests` 的 1 项 Unity 测试通过，验证 GUID、local file ID、名称、引用和 `.meta` 保持，曲线、事件、帧率更新。实际项目没有用用户动画做提交测试。C# 编译、域重载、Editor ready 与最终 Console 零错误检查通过。

## 验证

仓库回归 `CharacterModelPreviewSessionTests` 覆盖无效装备保留、逐组合换装不替换 Animator、运行脚本禁用和幂等清理。测试使用当前仓库已导入角色，不属于 package 自包含测试。

Editor 验收应包含菜单和右键入口、FBX 子片段恢复、插件播放及导出、换装保留设置、骨骼修改后武器跟随、保存／取消、关闭 Launcher 不结束会话，以及舞台退出／重载后的原场景与临时对象检查。普通编译遵循根 AGENTS.md 的 `validate_unity_scripts` 证据要求。

### 2026-09-22 实测记录

以下首次验收覆盖了装配及内部播放／导出链路，但遗漏了插件窗口对 Preview Scene 的拒绝条件；该记录不代表旧 Preview Scene 容器能显示完整编辑界面。用户报告后已将最终容器修正为普通附加场景，并增加 `AnimationDesignerSceneCompatibilityTests`，检查舞台场景类型、插件接收条件及原场景保留／释放。

- Unity 6000.5.0f1；000001、000002、000003、000004、000008、000009 六个已导入角色的默认装配全部成功，000003 空手，其余默认一件武器。
- EditMode 回归 3/3 通过（job `dce1507a721c4c00b7f76ac9d85ae058`）。默认／空手／隐藏／恢复与无效 Loadout 检查通过；同角色换装保留同一个 Animator 和插件设置对象。
- 插件达到 `IsReady=true` 并播放；手骨平移 `(0.1, 0.2, 0.3)` 后武器即时跟随，测得位移约 `0.374166`。通过原生 `ExportToFile` 导出 2.233333 秒、130 条曲线的 Humanoid 片段。
- 保存适配器生成原生 `AnimationDesignerSave`，保存前清除临时 GameObject/Component 引用；实测存档中临时场景引用为 0。原生导出及保存验收资产均已删除。
- 实际取消保存／结束提示后会话保持活动。动画右键入口传入片段；关闭 Launcher 保持会话，重新打开恢复选择；另以 FBX 子资源验证 GUID + local file ID 恢复。
- 关闭插件及脚本重载后回到 MainStage，临时角色根对象为 0；原 `TacticalCardDemoScene` 保持 4 个根对象且未标脏。编译开始提前清理与 Inspector 刷新用于避免域重载恢复已销毁目标。
- 编译验证已观测 refresh completed、compilation started/finished、domain reload、Editor ready 和 Console error count 0。MCP 链接在重载时可能重连，使用同一个验证任务的 `status` 获取持久化证据，不将连接中断等同于编译失败。

未覆盖任意第三方 Generic 骨架的逐项效果兼容，也未把进入 Play Mode 的清理钩子扩展为当前游戏场景的完整运行验收。

### Preview Scene 兼容修复验收

- 最终容器使用普通附加场景，并分配独立的 Scene Culling Mask；保持 Scene View／Hierarchy 隔离，原 Game View 不显示临时角色。先创建场景再进入舞台，避免新场景事件重置导航。
- `AnimationDesignerSceneCompatibilityTests` 实际运行 1/1 通过（job `9e9a0c3e3eb7499e85aabcfd367f2606`）：普通场景可交接、Preview Scene 拒绝、渲染 mask 不重叠、原 active scene 与脏状态保留，退出后关闭且仅关闭自己的场景。
- 使用 000009、原持剑 Loadout 和 `sword_combo01_01`，已截取实际 Unity 窗口确认播放条、Armature Setup、Limb Setups 和导出控件可见；`_serializationChanges=false`，不再停在隔离模式提示。
- 播放进度实际从 0 推进；退出后场景数 1，原 `TacticalCardDemoScene` 保持 4 个根对象、未标脏。解除交接时同步清除 `TargetClip` 和插件 `_latestClip`，避免下一次 OnGUI 在已释放设置上执行片段切换。
- 本次编译验证完整通过，回归及窗口复测后 Console error count 为 0；第三方插件源码没有修改。
