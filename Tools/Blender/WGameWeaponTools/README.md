# WGame Weapon Tools (Blender 专属武器工具插件)

专门面向 WGame 6 位数字武器管线的 Blender 扩展插件。支持武器身份识别与申请、刀刃判定线段（Trace Blade）辅助定位、FBX 规格化导出，以及**在 Blender 内部一键直达 Unity 预制体（Prefab）生成**。

---

## 核心功能

1. **自动识别 / 申请武器编号 (Weapon Identity)**
   - 自动检测 `.blend` 工程路径中的 6 位数字编号（如 `000001`）。
   - 一键点击“申请新武器编号”，通过中央编号池原子分配下一个 ID 并生成基础脚手架与原材料清单。
2. **攻击判定辅助 (Trace Blade Helpers)**
   - 一键生成/定位 `trace_blade_root` 与 `trace_blade_tip` 判定点物体。
   - 美术可直接在 3D 视口拖拽微调判定点位置，系统实时计算攻击有效距离（Reach Meters）。
3. **一步直达 Unity 预制体 (One-Click Export to Unity Prefab)**
   - **自动变换规范化**：烘焙网格旋转与缩放为 (1,1,1)，保持握持原点 (0,0,0) 不受影响。
   - **自动化 FBX 导出**：按照 Unity 标准坐标系（-Z 前向，Y 向上，三角化）导出到 `ArtSource/Common/Weapons/<id>/model/<id>.fbx`。
   - **元数据回填**：自动将判定线段坐标回填至 `weapon_sources.json`。
   - **Authoring 资源同步**：通知 Authoring 服务将模型同步至 `Assets/MxFrameworkGenerated/Common/Weapons/<id>/` 并注册公共映射。
   - **驱动 Unity 预制体生成**：向 Unity 发送重建请求，Unity 自动生成最终的 `<id>_weapon.prefab` 与材质。

---

## 安装说明

在 PowerShell 中运行安装脚本：

```powershell
.\Tools\Blender\WGameWeaponTools\install.ps1 -BlenderVersion "5.2"
```

然后在 Blender 中：
1. 打开 `Edit` -> `Preferences` -> `Add-ons`。
2. 搜索 `WGame Weapon Tools` 并勾选启用。
3. 在 3D 视口按 `N` 键展开右侧边栏，即可看到 `WGame Weapon` 标签页。

## 导出修复 1.0.1

- 导出范围是当前场景全部可见武器网格，不受活动分件限制；界面显示导出分件数。
- Trace 使用明确命名的 Empty 和制作变换，隐藏集合不会把坐标变成零；正式导出拒绝缺失或零长度的配对。
- 「仅导出武器源 FBX」只写 `ArtSource/Common/Weapons/<id>/model/<id>.fbx` 和源清单，不写 Unity Assets 或重建请求。独立命令行脚本也使用同一契约。
- 「一步导出并生成 Unity 预制体」另执行公共注册、当前武器资源同步与 Unity 重建。必须在 Unity 交接完成后使用。
- 新配置类型读取编号池，不推断剑标签、动作或拖尾；现有配置的 VFX、判定半径和动作绑定保留，通过带 revision 的单条 Authoring API 更新。
- 共享色板实际使用 `WGame/PropStylizedLit` 与 `_WG_PALETTE_ON`，第二 UV 为 PaletteUV，首个顶点颜色层为 PaletteData；不进入角色烘焙合图流程。
