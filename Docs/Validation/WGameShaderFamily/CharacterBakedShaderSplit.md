# 角色烘焙 Shader 拆分验收（2026-09-20）

## 结果

`WGame/CharacterStylizedLit` 已收敛为烘焙主路径；`WGame/CharacterPaletteLegacy` 承接色板、FaceDetail 和纯色兼容。操作与数据合同见 [角色合图烘焙与头发](../../Guides/CHARACTER_BAKED_SURFACE_AND_HAIR.md)。此次基线包含工作区已有的头发亮带／阴影调整。

当前 19 个原主 Shader 材质中，5 个色板与 13 个纯色迁入 Legacy，1 个烘焙材质保留主 Shader。19 份材质 `.meta` 哈希全部不变，主 Shader GUID 保持 `36061221506164f67a10b2a89d0fa880`。重复执行显式迁移后，19 个材质文件哈希均未再次改变。没有重烘焙正式角色或保存测试场景。

## 验证证据

- Unity 脚本验证任务 `0be8330cb7264bb68e0afeadff5a81eb` 返回 passed：刷新完成、编译开始／结束、域重载已观察、Editor ready、Console error 为 0。MCP 在域重载时断线，重连后通过同一任务的持久状态取得结果。
- 主 Shader 与 Legacy 的 `ShaderUtil.GetShaderMessages` 均为空。定向 EditMode 任务 `80434d821bcf4ed783b5e45b1769a493` 共 47 项，45 项通过，2 项既有材质审计失败，详见下节。迁移、冲突拒绝、重复执行、分类 mip、烘焙隔离、两套面板、MaterialBindingHub 效果与 Pass 检查通过。
- 临时复制 `000009` 的烘焙输入、`000001` 的色板配置，并在临时 Preview Scene 调用实际材质导入方法。两条路径各重复生成两次，分别保持主 Shader／Legacy，生成材质 GUID 稳定；烘焙两张纹理均已绑定且不依赖旧模式 keyword。临时资产目录已通过 Unity 删除。
- 使用 `000009`、`000008` 和纯色球体，在同一临时场景设置下分别渲染三个角度、正常／受击闪光／半程溶解，共 27 组拆分前后截图；全部像素差为 0。正式角色当前均未启用独立 FaceDetail，其兼容性由定向属性、UV 源码和运行时 PropertyBlock 回归覆盖。该结果不等于所有光照、平台和材质组合均已验收。
- 发布策略 8 项测试通过：新角色、旧色板、显式转换、烘焙优先、损坏描述、实际统一发布入口分流，以及导入失败后旧发布字节恢复。7 项源文件保存测试在 Blender 5.2 后台通过；预览身份测试确认 151 个制作操作受隔离，正式 Work 文件哈希不变。
- CharacterStudio 的材质工作台 smoke 与既有色板 usage 归属 smoke 通过，覆盖色板服务不可用时的烘焙展示、损坏描述、新角色引导、切包及保存隔离。此处是脚本／DOM 测试，没有宣称完整浏览器交互验收。

本机截图、哈希清单和临时导入结果保存在 `.agent/shader-split/`，属于未版本化验收输出。

## CharacterStudio 网页预览补验

用户反馈 `000009` 在网页中仍然粉色条纹、身体发黑。Chrome 中复现后确认：原 `viewport3d.js` 只消费 `paletteBinding`，烘焙模型的 UV1 发流被误写成色板采样 UV；此前材质面板与 Unity 渲染验收没有覆盖该缺口。

新增网页烘焙材质适配，并在本体加载处让烘焙描述优先；保持原始 UV0、发流、蒙皮和独立武器路径。实际浏览器恢复白发、黄色衣物和面部细节，材质审计为 `bakedSurface=true`，色板 usage 为 null。

- Chrome/WebGL 实测 `000009 → 000008 → 000009` 切包，烘焙和历史色板分流正确，无 JavaScript／Shader 错误。
- 在独立浏览器会话中损坏描述、阻断 SurfaceData 请求，均显示具体错误，没有用旧色板渲染本体；恢复请求后正常显示。未保存任何角色配置。
- 实际 FBX 的新增烘焙预览 smoke、既有 usage 归属 smoke、材质面板 smoke 通过。
- 既有 `palette-preview-smoke.mjs` 在 `000001` 的“必须仍有待重画面”断言失败。用 `HEAD` 的未修改预览代码单独重跑，复现相同失败；未更改该断言或角色资产。
- 浏览器还请求了角色当前不存在的旧默认待机动画与 FBX 内嵌纹理路径，产生已有 404；此次没有宣称实际待机动画已验收。

本机复现截图、修复截图和浏览器核对结果保存在 `.agent/character-preview-check/`。本次只修改 CharacterStudio 网页及测试、说明文档，没有改 Unity Shader 或重新烘焙角色。

## 既有失败

以下两个未纳入本次迁移的材质仍使用 `Universal Render Pipeline/Lit`；其工作区无改动，`HEAD` 中也引用相同 URP/Lit Shader GUID：

- `Assets/Art/WGame/Materials/Character/CharacterPaletteLookDevGround.mat`
- `Assets/MxFrameworkGenerated/CharacterPackages/000001/preview_materials/CharacterPreview_148C8C8C.mat`

因此 `FirstPartyContent_HasNoStockUrpLitUsage` 与 `GeneratedCharacterPreviewMaterials_UseExpectedCharacterShader` 仍失败。本次没有修改测试门槛、增加豁免或顺带改变它们的外观。
