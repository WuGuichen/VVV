# WGame 六位数字角色与可选 DCC 制作资产

> Status: Current
> Code-Doc: `Tools/Harness/check_character_authoring_sources.py`
> Code-Doc: `Tools/Harness/tests/test_character_authoring_sources.py`

## 身份

- 新角色由 `Authoring/CharacterPackages/character_id_catalog.json` 分配 `000001`～`999999` 六位字符串 ID。
- `packageId`、`characterId`、Authoring/ArtSource/Generated 目录和角色 ResourceKey 段使用同一数字 ID。
- `displayName` 只用于显示和搜索，不进入稳定路径。
- `000000` 无效；归档后的 ID 永久退休；旧语义角色由 `ArtSource/Characters/authoring_legacy_exemptions.json` 兼容。

## ArtSource

新角色根为 `ArtSource/Characters/<sixDigitId>/`。`character_sources.json` 继续声明 CharacterStudio/Unity 编译输入；`authoring_manifest.json` 独立管理可选 `.blend`、模型图、milestone、audit 和 provenance。

可选资产未声明时不阻断 Compile、Unity Import 或发布。一旦声明，必须使用角色根内安全相对路径、当前 SHA-256 和 Git LFS。

标准文件名：

- `Blender_Work/<id>_work.blend`
- `Blender_Work/milestones/<id>_M0..M3_<stage>.blend`
- `Final_Source/<id>_LOD0.fbx`

当前已有Work的数字角色统一以 `Blender_Work/<id>_work.blend` 继续制作，`authority.blenderPath` 指向该已登记文件。旧schema仍识别 `BlenderAuthority`／`Final_Source/<id>_authoring.blend`，这不要求保留第二份活动制作源。本批旧副本、旧验收／export audit及明确关联的旧迁移备份已按用户要求移入Windows回收站；现有正式产物、发布哈希和历史发布库保留。当前Work登记不代表已完成绑定、重绘、导出或原生验收；后续发布重新执行既有完整验证并生成回执。用途未证实的备份保持原位待确认。

000002的两份旧调色／缩放备份经隔离只读Blender数据比对，未发现备份独有的对象、网格、骨架、材质、图像或动作；差异属于已被当前Work替代的调色／缩放状态，故按同一要求移入回收站。当前Work文件未保存或修改。

`.blend1`、autosave、recovery 和新生成的时间戳备份不得版本化。默认保留用户本地备份；用户明确要求清理时，只按核对过的范围移入回收站，不以永久删除文件消除审计诊断。

### 制作清单审计与本地备份

现有 `Tools/Harness/check_character_authoring_sources.py` 默认从 Git index（含强制添加的文件）和未跟踪且未被忽略的交付候选收集范围。Git 忽略的未跟踪本地备份不阻断交付；已声明的制作源无论是否被忽略，都必须存在且匹配清单 SHA。数字角色即使没有采用可选 DCC 资产，也要保留身份一致、`sourceRoot="."`、空 `items` 的清单；模型／纹理／动画继续由 `character_sources.json` 管理，空清单不宣称已完成绑定或原生验收。

HEAD 中已有的 `Blender_Work/publish_backups/` 是保留的历史发布快照，保留文件和原始内容，不要求登记为当前制作源。它们不能成为清单 `items` 中的活动来源；更改这些历史快照须单独审查。新备份属于被 Git 忽略的本地工作状态，不能强制添加到该历史目录绕过登记或替代正式源。此外，HEAD中已有且未修改的三类旧色板迁移记录（LegacyPalettePreserve v1、ColorPaletteMigrationReport v2、SurfacePaletteMigrationReport v1）可凭结构化format、角色／已采用源、catalog SHA和安全backupBlend链接，连同链接到的既有未修改备份按证据留存；备份不能被提升为活动源。新建、修改、跨角色／越界链接或无法匹配记录的文件仍须审查。其他未登记的正式文件或迁移快照仍报告问题，不凭文件名／日期自动豁免。

正式烘焙 producer 已通过角色包 `resource_catalog.json` 与 `character_baked_surface_binding.json` 登记的 Final_Source 产物，沿用这条权威链，不再复制到可选 DCC `items`。审计只认可既有固定输出：`<id>_LOD0.fbx`、`BaseColor.png`、`SurfaceData.png` 与 binding；须核对包身份、两份 binding 除相对文件路径外的完整数据、已采用的来源路径与kind、发布保存源／烘焙快照SHA格式、唯一资源键、catalog SHA、Final 与包内实际字节及 LFS。未匹配的产物仍阻断，不因文件名或存在一份 binding 自动放行。此检查证明登记与产物字节身份，不证明来源真实、艺术／原生验收。

`items.sha256` 登记当前制作文件；binding的 `authoringSourceHash` 固定当次发布前保存源，`sourceBlendHash` 固定当次烘焙快照。合法保存并同步当前Work清单后，Work可以不同于旧发布SHA，报告“有未发布制作改动”，旧产物继续独立核验。Work与其当前清单不符则另报“制作登记待同步”，严格DCC检查与统一入口仍失败，不因旧产物完整放行。来源缺失、非法路径／hash、未登记角色、产物或binding／catalog身份与字节不符仍阻断。

冻结来源证据单独输出状态：当前路径字节匹配、HEAD原始字节或HEAD/LFS缓存实际内容匹配才对应标记已核验；仅本地LFS缓存内容匹配仍标记版本关联未验证。找不到冻结来源字节明确标记未验证，不能把有效SHA格式当作来源验收。当前binding没有受管理的烘焙快照路径，因此`sourceBlendHash`对应快照字节仍标未验证；检查器不依赖Temp或遍历人工备份取得CI通过，也不自动恢复／下载来源。

清单 SHA 只绑定当前已采用源的内容，不能替代烘焙／发布／导入回执。更新当前制作登记与发布新产物是独立步骤；来源角色未明确时先由制作owner确认，再经既有保存／采用流程登记。不得机械刷新历史发布SHA或修改回执把未发布内容宣称已验收，也不要求每次保存Work都重写旧发布哈希。

## 从零绑定

WGame Rigging Assistant 对无 Armature/无绑定权重 Mesh 使用独立路线：模型预检 → 绑定姿势 → 组件分类 → WGame Humanoid 规格 → ARP Reference Bones → Reference 验收 → Match Rig → 分组件 Skin → Weight Paint → 压力测试 → Unity 四权重与 FBX 验收。

Reference、Control、Deform 和 Custom Shape 必须在 UI 中分别标识。用户可选择本地工作模式；只有显式“纳入版本管理”才写标准目录和制作清单。

## 创建与归档

CharacterStudio 新建/复制不接受手填 packageId，先读取编号池 revision，再由 Authoring Server 事务分配。数字角色归档到 `Authoring/Archive/CharacterPackages/character-<id>/` 与 `ArtSource/Archive/Characters/<id>/`，编号永久退休。

仓库编号池无法阻止两条离线分支同时取到同一编号；创建新角色前应同步目标分支，合并冲突必须在发布/Unity receipt 前通过专用重新分配迁移处理。

## 彻底删除角色（CharacterStudio）

顶部工具栏 **彻底删除** → 核对六位角色编号 → **检查删除范围**。默认填入当前角色，也可填写已归档或上次未删完的角色编号。弹窗列出待删除目录、文件数量与占用大小；存在外部引用时显示引用文件并禁止执行。

角色自己的登记不再被当作外部使用：弹窗的 **确认删除时一并清理** 会列出 `global_resource_mapping_profile.v2.json` 中由 `character.package.<id>` 生产者 v1 拥有且记录 ID 为 `charpkg.<id>.*` 的资源登记，以及 `tactical_acquisition.json` 中该角色动作、卡牌均为空且不含未知字段的记录。仅移除对应条目，保留整个文件及其他角色内容；非空战术配置、手工或未知来源的资源记录、其他条目对角色的引用仍会阻止删除。

这些共享文件的完整哈希纳入确认计划。确认后若文件发生变化则拒绝旧计划；清理登记成功后才删除角色文件。清理途中失败会报告剩余文件并保留角色文件，重新检查后可继续；已完成的清理不会重复执行。

已进入全局运行资源目录的角色，还会清理 `global_runtime_catalog.json` 中可由上述角色资源登记证明归属的 AssetBundle 条目：资源 ID、packageId、类型、变体及角色专属生成路径均须一致，目录地址和 providerData 也必须匹配。清理运行登记先于制作登记，确保中途失败后仍保有归属依据。其他条目的依赖、路径或来源不匹配的条目继续阻塞。此处移除目录登记，不重写共享 AssetBundle；之后按现有资源构建流程重新构建包体。

检查通过后，输入同一角色编号，确认已关闭该角色的 Blender 文件和 Unity 编辑对象并停止导入、烘焙和发布，再点 **确认彻底删除**。此操作不保留归档副本，无法通过编辑器撤销恢复；ID 永久停用。文件或引用在检查后变化时必须重新检查。部分文件因占用等原因未删完时，界面保留剩余路径，按原编号重新检查后可继续清理。

Authoring 服务复用下面的删除工具，串行处理请求，完成后使资源缓存失效并刷新角色列表；不需要为网页入口停止服务。需要服务主机安装 Python 3；PATH 中有 `rg` 时自动加速只读引用扫描，没有时使用内置查找。检查期间显示已等待秒数。共享资源、Git/LFS 历史以及仓库外副本不删除。上述自有登记之外的外部引用须先通过其所属工具解除，不提供强制忽略引用开关。

## 永久删除误建角色（离线命令）

`Tools/MxFramework.Authoring/scripts/delete_numeric_characters.py` 提供永久删除命令。
这与归档不同：删除当前制作工程、角色包、同 ID 归档以及该角色专属的 Generated
CharacterPackages / CharacterActions Profiles / ActionSets / Libraries 目录和对应 `.meta`，不留下归档副本。
活动条目移除，ID 写入 `retiredIds`；`nextCandidate` 不回退。Git 历史、Git LFS 历史对象、
仓库外原始素材及其他副本不在删除范围。

执行前停止 Authoring Server，并关闭正在编辑这些数据的 Blender / Unity；该命令只维护磁盘
数据，不向运行中的 Editor 或 Server 注入缓存更新。执行后重新启动工具，重新读取角色目录。
不要与创建、归档、导入、发布或 Git 同步并发执行。锁文件只协调本删除命令的并发调用。

默认只读预览：

```powershell
python Tools/MxFramework.Authoring/scripts/delete_numeric_characters.py --ids 000005 000006
```

检查输出的 `paths`、`files`、`sharedEdits`、`blockingReferences` 和 `status`。仅 `ready` 可执行；
使用本次输出的完整 `planHash` 执行：

```powershell
python Tools/MxFramework.Authoring/scripts/delete_numeric_characters.py --ids 000005 000006 --apply --plan-hash <本次预览的planHash>
```

可用 `--root <仓库根>` 明确指定仓库。应用时重新计算目录内容、编号池和引用检查结果；
与预览不一致则拒绝执行。引用检查覆盖 Assets / Authoring / ArtSource / Packages 中常用文本
资源的明确数字 ID、角色资源标识、目录路径和待删除 Unity GUID；动态拼接引用及二进制外部
引用不能由此检查证明不存在。检测到符号链接或 junction 时拒绝处理，不跟随它们删除。

结果为 `deleted`、`already_deleted` 或 `partial`。编号先退休再清理文件，避免中断后重新分配
旧 ID。`partial` 返回失败原因和残留目录；解决占用等问题后重新预览，使用新 `planHash` 重试。
退出码 0 表示预览可执行/删除完成/已删除，2 表示引用阻断或部分删除，1 表示输入或运行错误。
如果进程被强制终止并留下 `.character-permanent-delete.lock`，确认没有删除进程运行后再处理锁文件。

回归测试只在临时夹具目录执行，不删除真实角色：

```powershell
python Tools/MxFramework.Authoring/scripts/test_delete_numeric_characters.py -v
```
