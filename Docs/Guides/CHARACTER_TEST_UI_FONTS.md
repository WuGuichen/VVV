# CharacterTest UI 字体装配

> Status: Current

当前使用 **MxUI 精简字库**，10 个运行字体文件共 **19,915,556 字节（18.99 MiB）**，较上一版 105.40 MiB 减少约 **82%**。资源、生成规则和许可证见[字体资源说明](../../Assets/Resources/Fonts/README.md)。

## 当前使用方式

- 卡名／主标题：`MxUI Serif SC`，保留思源宋体 SemiBold 字形。
- 正文／按钮／设置项／费用：统一 `MxUI Sans SC`，共用 Regular；均不启用伪粗体。
- 切换繁中、日、韩时，这两个稳定角色原位切换地区字形。地区文件只保存与 SC 不同或缺少的字形，其余经显式资源引用共用；西文补缺也只保留两个文件。
- 字库保留 GB2312、Big5、Shift-JIS、完整现代韩文音节及项目现有文本，不等同于完整 Unicode 字库。新增生僻字需要重建补入。
- 8 个 CJK 派生字体按 OFL 要求更名为 MxUI，源字体地址与哈希保存在 ArtSource 的清单中，原始大字体仅留在忽略的工具缓存，不在运行 Resources 中。
- 生成器：`Tools/MxFramework.FairyGuiUiComposer/scripts/build_compact_ui_fonts.py`，依赖 `fonttools==4.62.1`；生成结果和运行资源均受 20 MiB 上限约束。

## 精简版验证（2026-09-28）

- Unity 脚本作业 `1bbe46653db144b98a849c1d55a21114` 完整通过，Console error 0。
- 字体专项 `7acf97c7e3034e078d17e7769c8555bb` **15/15 通过**，覆盖 11 语种实际字形、地区切换、活跃字体句柄更新，以及 Resources 中恰为 10 个字体且不超预算。
- Composer 字体 Apply／Sync 专项通过，11 个包已重新发布。9 个通过通用 CLI 验收；Guidance 与 Foundation 的原有 manifest／资源登记验收限制仍见历史记录。
- 原有卡库源包存在顺序和 XML 换行归一化造成的同步漂移；经 Composer Sync 确认源身份后，保留原蓝图布局、换行和本轮字体修改再发布，未手改生成 XML。
- 已用真实发布卡面渲染 11 语种精简字体对照，截图在本机 `Library/CompactFontEvidence/steam-11-compact.png`。正文保留 hinting，较大字号的标题移除 hint 程序后，实测仍保持清晰字形。
- 全局 Player 资源于本机时间 2026-09-28 15:31:47 重新构建；重新播放后确认 Guidance、Foundation 也解析到 `MxUI Serif SC`，排除旧运行资源包缓存。主菜单和设置均使用 MxUI 字体。最终 Console error 0，保持原场景，未保存场景改动。

## 历史装配与验证（以下 105 MiB／Medium 方案已被精简版替代）

### 首轮验证

- Unity 脚本验证：`85d397186cf14321a75bdb08c8016bc4`，刷新、编译、domain reload、Editor ready 全部通过，Console error 0。
- 字体 EditMode 专项：`d80203d1ced74f39a3f75bf85d7d875e`，3/3 通过。验证别名确实指向项目资源、重复初始化复用对象、卡面中文和数字标点有字形。
- Authoring 专项 `ui-composer-fonts` 通过：字体进入组件 XML、Medium 按钮无伪粗体、字体修改可从 FairyGUI XML 同步回蓝图。
- 11 个 Composer 包均完成 Apply 和 FairyGUI 发布。9 个通过通用 CLI 全部验收；`MxPlayerGuidanceUi` 的 manifest 位于 Packages，超出当前 CLI 的 Assets/Scripts 查找范围；`MxUiFoundationDemo` 未登记该验收器要求的全局资源 ID。未为了字体任务修改这些既有登记关系。
- 对上述两个包补做 Play Mode 真实发布字节加载，并实例化 `GuidanceCardPanel`、`GuidanceNoticeStackPanel`、`UiFoundationDemoPanel`；同轮加载了 `MainMenuPanel` 和 `SettingsPanel`，确认文本引用这三种字体。
- 经 Input System 鼠标路径打开测试卡库“守距出手 · 测试”的预览：卡名为宋体 SemiBold、费用为黑体 Medium、描述为黑体 Regular、按钮为黑体 Medium，均 `bold=false`。描述四行，文本高 124、容器高 125；名称、费用也未溢出。截图：本机 `Library/FontSetupEvidence/card-fonts.png`。
- 全局 Player 资源通过现有生成器重新构建，`global_resource_artifact_set.json` 更新时间为 2026-09-28 01:51:38（本机时间），日志确认 `Global v2 Player resource artifacts generated`。验收后恢复原先的 `TacticalCardDemoScene` 到 Edit Mode，最终 Console error 0，未保存或修改场景。

这验证了 Editor 中的实际界面和发布组件；未执行完整 Player 应用构建或跨平台截图验收。

## 11 种语言字体扩展（2026-09-28）

本轮采用简体中文、繁体中文、英语、日语、韩语、法语、德语、西班牙语（西班牙）、意大利语、俄语、葡萄牙语（巴西）。这是一份项目字体支持名单，Steam 没有固定的“11 国语言”标准。Steam 代码与验证样本登记在 `Assets/Resources/Fonts/font-locales.json`。

欧美语种复用原 SC 主字体中的拉丁／西里尔字形；共用 Source Sans 3 Regular／Medium、Source Serif 4 Semibold 补齐扩展字符。繁中、日、韩新增 TW／JP／KR 的官方地区子集。共 4 组主字形、3 份共用补缺字体、15 个文件，避免每种语言各装一整套。全部官方原文件、许可证及 SHA-256 已登记，合计约 105.4 MiB（构建压缩前）。

现有发布组件中的三个字体名作为稳定的正文／控件／标题角色；切换本地化语言时原位更换对应地区的 native font，并通知已打开的文字重建。通知来自现有 LocalizationRuntime，经 SessionBuilder 订阅／释放，设置应用与回退继续共用原语言状态，不另建语言系统。按需解析当前地区字体；缓存由 Unity Resources 持有，不在每帧查找字库。

补缺链为 SC → Western，TW／JP／KR → Western → SC；Western 无下级引用，不存在循环。SC 回退保留尚未翻译的开发文本可读性。游戏译文和设置中的可选语言仍由原语言表管理，此任务没有伪造 11 种完整翻译。

验证：Unity 脚本作业 `590eb22195884dc583116b1d51297b5a` 完整通过；EditMode 字体作业 `7282c333c5c148568f57fe119578718a` 为 **15/15 通过**，包含 11 语种 × 正文／控件／标题的实际字形请求、Steam 代码映射、别名复用和语言变化后的字体版本更新。字形检查包括法文 œ、德文 ẞ、俄文 ё、日文假名、韩文谚文、繁中及数字标点。

Play Mode 中通过真实 LocalizationRuntime 将 zh-CN 切为 ja-JP，已确认角色别名解析到项目内 JP 字体。另以已发布的 `TacticalPlayerCardFace` 实例渲染 11 语种对照，截图为本机 `Library/MultilingualFontEvidence/steam-11-fonts.png`；对照使用诊断样本文字，非正式卡片翻译。未修改卡片数据或保存场景。
