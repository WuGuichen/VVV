# 独立角色烘焙预览验收（2026-09-20）

## 行为

Blender 画板 3.14.17 提供“烘焙并预览 → 返回制作／检查效果 → 确认并发布到 Unity”。入口位于 WGame 的“头发与烘焙设置”和 WGame Rig 的“发布角色资产”。当前运行中的 Blender 已更新，保留用户当前网格内容、编辑模式和选区。

预览烘焙只写 `Temp/baked-preview-<id>-*/`，捕获当前内存网格与贴图像素，不覆盖 Work 或正式导出、不同步角色包、不请求 Unity 导入。完成独立 FBX 回读后才能成为可查看候选。“上次发布 UV”明确表示历史正式结果，与本次临时候选分开。

发布要求候选已查看、当前制作输入一致、候选文件哈希完整。发布前保存制作源，再直接采用已预览的 FBX 和两张纹理，不重新烘焙；仅更新描述中的已保存源文件哈希。当前制作内容变化、未查看、候选不完整或被修改时拒绝发布。候选为会话状态，重新启动 Blender 后需重新烘焙。

## 验证

- `test_baked_preview_workflow.py`：8 项通过。覆盖网格、Profile、发根、材质常量、引导线及未保存像素的变化；选区、节点排版、Edit/Object 切换不误判；保存生成图像导致 GENERATED → FILE 时不误判；快照不改 Work；过期／损坏／跨角色候选拒绝；未查看不能发布；预览 worker 不触发发布。
- `test_character_surface_publish_policy.py`：9 项通过。包含发布只采用已确认字节、候选完整性保留及同步导入失败后恢复旧产物。
- `test_save_character_source.py`：7 项通过。保存失败不启动发布，未保存绘画和制作清单仍按已有合同处理。
- `test_hair_guide_placement.py`：26 项通过；既有发根／引导操作未回归。
- `test_baked_preview.py`：重复开关预览、UV0／512 图片、头发对比、返回 Edit Mode、无数据泄漏、从预览保存时清理副本及正式 Work 哈希不变检查通过。
- 真实 `000009` 的 512×512 流程在独立 Blender 后台进程完成烘焙、FBX 导出／回读、临时预览打开／关闭；正式 Work 与 Final_Source 文件哈希不变。`000008` 和 `000009` 的实际模型均通过 Edit/Object 往返制作输入指纹一致检查。

本地集成输出位于 `.agent/bake-preview-e2e-result.json` 及其记录的临时目录；这些不是正式角色产物。发布成功分支使用临时角色包和模拟 Unity 导入回执验证，未为测试发布当前用户的角色或替换 Unity 正式资源。Blender 预览仍是现有 EEVEE 外观近似，不代表与 Unity 逐像素一致。

## 后续真实发布与文件占用修复

2026-09-20 用户确认发布 `000008` 后，旧发布器的 `dotnet run --project` 在编译阶段失败：运行中的 CharacterStudio `authoring` 进程占用了默认输出目录中的 `MxFramework.Authoring.Core.dll`，出现 MSB3027/MSB3021。后台子进程输出未正确进入 `publish.log`，原日志只留下退出码 1。

发布器现于候选 `publish_<id>/` 下隔离编译中间目录和输出目录，直接运行这次编译出的 `authoring.dll`。编译在正式产物写入之前完成；编译和导入输出分别保存为 `authoring-build.log`、`import-unity.log`，失败尾部及日志路径写入主发布异常。保留正在运行的 CharacterStudio 服务。

- 发布策略测试现为 10 项通过，新增编译失败不改正式产物、具体诊断保留；同步导入失败仍恢复之前的包和 Final_Source。
- 真实服务运行期间，隔离编译完成，0 警告、0 错误。直接发布用户已查看的 `baked-preview-000008-75603zzf`，没有再次烘焙。
- Unity 请求 `8f2c9544b3624edf82320030ceb8f3e3` 状态为 `completed`；持久导入回执 `ready=true`，五项输入哈希与本次请求一致。
- 模型、BaseColor、SurfaceData 在临时候选、Final_Source、角色包和 Unity 导出四处逐字节一致。材质绑定主 Shader `WGame/CharacterStylizedLit`，使用烘焙底色和生成的分类 mip 控制图；Editor ready，Console 错误 0。Blender 发布监控已结束并显示 Unity 重建完成。

## 000002 闲置材质阻塞快照修复

`000002` 的所有面使用 `WGame_ColorPalette_SurfaceProfiles_Preview`，其 RGB565 色板已打包；闲置槽 `MAT_WGame_CharacterPaletteShared` 仍引用已不存在的 `WGameCharacterPaletteAtlas.png`。旧快照遍历整个 Blend 的所有材质和节点组，误将闲置槽的缺图视为当前烘焙缺图，因此后台任务尚未启动就失败，临时目录没有 `preview.log`。

快照现仅收集当前场景网格实际面所用的材质，并递归收集这些材质引用的节点组。保留闲置槽和所有制作数据；真正使用的贴图缺失仍报错。`test_baked_preview_workflow.py` 9 项通过，新增闲置缺图不阻塞、切换为使用后仍拒绝、源文件和材质槽保持不变的回归验证。

实际 `000002` 重试候选 `baked-preview-000002-sk159lbq` 已完成贴图、35,010 三角形 FBX 导出和独立回读验收，并打开临时预览；制作源文件哈希保持不变，未发布正式资产。
