# Character Gameplay Camera 接口

> Status: Current
>
> Character Application 8.0 加字段兼容；不升级 schema。

## 权威数据

角色级 Gameplay Camera 的唯一 Authoring 来源是 CharacterStudio 中 `CharacterPresentationProfileConfig.gameplayCameraProfile`。`previewCameraPresetId` 仍只控制 CharacterStudio 预览和缩略图，不参与 Gameplay Camera。

显式 Profile 包含：

- `profileId`
- `focusAbstractBoneId` 与角色根局部空间 `focusOffset`
- 角色根局部空间 `framingBoundsCenter/framingBoundsExtents`
- `normal` 普通跟随参数
- `locked` 锁定取景参数；`framingScale` 可在 `0.25～1` 内缩放锁定战斗使用的主体取景体积
- `obstruction` 遮挡参数

旧角色包缺少该字段时使用 CharacterTest 既有默认值且不产生警告；一旦声明显式 Profile，任何非法字段或无法唯一解析的焦点抽象骨骼都会阻止生成，不允许静默回退。

## 编译与运行时

Compiler 将 Profile 作为 `CharacterPresentationProfileConfig` 的结构化字段输出。CharacterTest playable catalog 将其带入角色 Definition。运行时通过 entity 到 `CharacterAbstractBoneRuntimeMap` 的桥接解析焦点，只消费 Unity 导入阶段生成并验证过的抽象骨骼引用。

运行时不得通过 Renderer Bounds、FBX 节点名、角色根节点、`HumanBodyBones` 或 `Animator.GetBoneTransform` 补数据。显式焦点在运行时失效时停止更新该角色的镜头目标，并且每个实体只输出一次结构化错误。

普通模式使用玩家自己的 Profile。锁定和多目标模式分别使用每个实体的焦点与取景体积；锁定求解结果限制在玩家 `locked.minDistance/maxDistance` 内。`locked.framingScale` 只影响锁定战斗构图，默认 `1`；普通跟随、CharacterStudio 测量结果和多目标完整取景继续使用原始 Bounds。取景体积随实体 Yaw 转换到世界空间，不随动画骨骼变形。

显式 Profile 的 `focusAbstractBoneId + focusOffset` 是普通跟随和锁定第三人称的真实光学焦点。普通模式与锁定模式的最终 `FocusCenter` 必须保持在该挂点上；锁定目标可以改变 Yaw、Pitch 和 Distance，但不得用玩家 Bounds 中心、双方几何中点或目标锚点覆盖玩家焦点。玩家取景体积在锁定模式下只约束水平取景，允许下半身贴近底边或越过屏幕；目标取景体积仍按安全边距尽量完整容纳。无显式 Profile 的旧包继续使用原有兼容构图。

锁定取景按当前 Camera Aspect、水平/垂直 FOV 逐角点投影双方经过各自 `locked.framingScale` 缩放的战斗 Bounds，并在 `locked.minDistance/maxDistance` 内求能容纳全部角点的最短距离。普通镜头 `normal.distance` 不得成为锁定模式的隐含最短距离。`locked.targetPadding` 只扩展镜头视空间的水平与垂直安全边距，不扩展深度，也不得把 Bounds 的最大 X/Z Extent 当作球形半径强制后拉。仅当玩家和目标都使用旧包兼容默认时，继续保留原有锁定构图语义。

显式 Profile 的锁定构图以玩家物理焦点作为光学 `FocusCenter` 和遮挡胶囊起点；旧包兼容构图产生的合成 `FocusCenter` 不得直接作为遮挡胶囊起点。遮挡查询必须使用玩家已解析的物理焦点：显式 Profile 使用 `focusAbstractBoneId + focusOffset`，旧包兼容使用角色根加旧 `focusOffset`。取景高度以角色根为地面基准，不以可能伸入地面的渲染 Bounds 最低点代替角色根。

## CharacterStudio 测量

“从当前预览测量”只遍历当前角色 Body 预览根下的可见 Mesh，排除 Socket 和调试标记。结果转换到角色根局部空间，Extents 增加 5% 安全边距并按 `0.01m` 量化。测量只更新当前草稿，必须显式保存后才进入编译结果。

## 诊断

- `CHARACTER_CAMERA_PROFILE_INVALID`
- `CHARACTER_CAMERA_FOCUS_MISSING`
- `CHARACTER_CAMERA_BOUNDS_INVALID`
- `CHARACTER_CAMERA_ANCHOR_UNRESOLVED`

这些诊断至少携带 package、profile、字段和 `abstractBoneId`；解析失败时同时记录 Socket/绑定路径和失败原因。
