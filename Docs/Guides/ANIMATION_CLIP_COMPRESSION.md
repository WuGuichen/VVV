# 动画片段批量压缩

> Status: Current

## 快捷使用

只处理剑与法杖基础移动时，无需选择资源：点击 **MxFramework → Assets → 一键压缩剑与法杖基础移动动画**。调整参数或先分析使用 **MxFramework → Assets → 基础移动动画压缩**；窗口的“动画来源”也可选择“剑与法杖基础移动配置”。

每次执行都会重新读取 `WeaponSource/global_weapon_library.json`，沿 `combat_girl_sword.sword_empty` 和 `combined.000002.staff_empty` 的 `animationConfigurationId`，解析 Profile 中 `slotId=locomotion` 的 Set / Group，收集其中的片段引用并去重。只处理这些移动组；不遍历动画目录，不加入 combat / reaction 槽位。当前配置为剑 63 个、法杖 9 个片段，数量随配置变化。

1. 在 Unity Project 中多选独立 `.anim` 或动画文件夹。文件夹会递归查找；重叠选择不会重复处理。
2. 右键 **MxFramework → 动画 → 快速压缩动画（默认）**：按最大浮点曲线误差 `0.001` 压缩并保存，保留根运动曲线，自动打开结果窗口。
3. 需要调整参数时，右键 **MxFramework → 动画 → 批量压缩动画片段**，或打开顶部菜单 **MxFramework → Assets → 批量压缩动画片段**。Framework Manager 的资源工具中也有入口。
4. **分析动画**只计算预计可减少的浮点关键帧，不写资源，也不估算文件字节数。**压缩并保存**显示实际文件大小、减少比例、保存后的关键帧数量和逐片段结果；报告可复制。Unity 可能同步 Transform 的各通道关键帧，所以保存后的数量可能与分析估计不同。

剑动画可选择 `Assets/Art/MxFramework/Characters/CombatGirlSword/AnimationClips/CombatLocomotion`；法杖动画可在 `Assets/Art/MxFramework/Characters/MageAnimationPack/AnimationClips` 中多选需要处理的移动片段。选择法杖的整个目录也会处理其中的攻击等片段。

上述 Project 选择流程仅用于“Project 选中资源”来源。基础移动配置来源不受 Project 当前选择影响；找不到配置、重复 ID 或无法解析任意片段引用时，整批在写入前停止并显示原因。优先以 Unity GUID 定位片段，未配置 GUID 时使用配置中的资产路径。

## 参数与保留范围

- 最大误差是每条浮点曲线的绝对值误差，默认 `0.001`。越小越保真，越大通常压缩越多。肌肉、位置、旋转分量分别按自身单位计算；它不是关节角度、世界空间误差或整个人物姿态误差。
- 默认原样保留 `RootT.*`、`RootQ.*`、`MotionT.*`、`MotionQ.*` 和空路径的 Transform 曲线。需要精简这些曲线时可取消“保留根运动曲线”。
- 精简保留每条曲线的首尾关键帧、保留帧的数值和切线斜率，以及曲线环绕模式。使用原曲线与候选曲线的三次多项式差值极值检查整个时间区间，包含关键帧之间的过冲。
- 对象引用曲线、离散曲线、阶梯曲线、加权曲线和无效曲线不精简。没有足够冗余的曲线保持原样。
- 只修改曲线。片段事件、采样率、循环设置、名称、时长、GUID、local file ID、标签与资源引用保持不变。保留帧的切线模式改为 Free，避免删帧后自动切线重算。

## 保存与恢复

压缩直接保存原 `.anim`，不创建 Git 提交。恢复可使用 Unity Undo（每个片段一项，撤销后再保存），或从 Git 恢复指定片段。批次不会保存其它脏资源。

有未保存修改、只读或版本控制未开放编辑的片段会跳过并报告，请先保存或解锁后重试。仅支持 `Assets/` 下的独立 `.anim`；FBX 子片段先通过已有 FBX 提取工具导出。

逐片段处理允许取消剩余工作；取消、关闭窗口、进入 Play Mode 或脚本重载不会撤回已经保存的片段。单个大动画的分析与写入仍在 Unity 主线程完成，取消会在片段之间生效。

压缩是有损精简。完成后按常规预览动作；如果这些片段用于姿态、接触运动或武器轨迹烘焙，按既有流程更新派生产物。根运动保护不代表其它姿态烘焙仍然有效。
