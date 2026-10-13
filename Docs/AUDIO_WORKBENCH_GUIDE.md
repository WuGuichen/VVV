# Audio Workbench 使用指南

> Status: Current
> Scope: Audio authoring, optional preview review, and runtime catalog publishing workflow.
>
> Code-Doc: `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/AuthoringResources/AudioAuthoringManifestV2.cs`
>
> Code-Doc: `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/AuthoringResources/AuthoringResourceContracts.cs`
>
> Code-Doc: `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.AudioWorkbench.Persistence.cs`
>
> Code-Doc: `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.PhysicalAudio.cs`

## 打开方式

先启动 EditorHub：

```bash
Tools/MxFramework.EditorHub/start-editor-hub.sh
```

然后从 EditorHub 打开 **Audio Workbench**。Resource Manager 的 Audio 页只用于查看来源、引用和诊断；点击“在 Audio Workbench 编辑”会携带 `package` 和 `audioCueId` 深链。

主流程固定为：

```text
选择 / 创建 Cue → 绑定 Event → 保存草稿 → 发布到运行时
                                      ↘ 可选 Unity / FMOD 试听与质量记录
```

Workbench 使用左侧常驻导航组织六个一级工作区；它们共享同一份前端状态和 Authoring API，不是六套独立数据：

| 工作区 | 负责回答的问题 |
| --- | --- |
| 项目总览 | 为什么尚未激活、下一批应处理什么、距离发布还有多远 |
| Cue 制作 | 为什么播放、绑定哪个 Event、语义和版本是否正确 |
| 候选实验室 | 生成什么、A/B 后采用哪些素材、是否已应用到 FMOD |
| Basis 与响应 | 稳定声音身份是什么、物理属性怎样影响选择和参数 |
| 材质覆盖 | 哪些公共 Pair × Variant 可运行、缺失或只能 fallback |
| 游戏预览与发布 | 玩家情境最终解析成什么、Catalog 是否可原子发布 |

“素材生成与 FMOD 采用”集中在“候选实验室”，但它仍是可选的素材生产支线。运行时发布只要求 Cue/Event、FMOD snapshot、Bank、参数和 Catalog 等技术事实有效；真实试听属于可选内容质量记录，不阻止同步。项目总览中的工作队列和进度都来自 Authoring server 的 `runtimeActivation`；浏览器不自行计算发布权威。

### UI feedback Cue set

UI 自动反馈目标设计见 `UI_AUDIO_FEEDBACK_AUTOMATION_DESIGN.md`。当 Semantic
Manifest 显式声明 metadata `"uiFeedbackCueSet": "v1"` 时，Current validator
要求 `ui.click`、`ui.confirm`、`ui.back`、`ui.error` 四个 Cue 使用
`OneShot + MissingPolicy=Mute`，并要求对应 Event 为 UI Bus 上的 2D non-loop
Event 且具有匹配的 `uiFeedback` metadata。未知 Cue set 版本会阻断校验，不能
静默绕过约束。

该开关只有在四个 Event/Cue 已能完整保存时才应启用。Cue 必须通过 Event、Bus、
播放模式和运行时目录等技术校验；Unity/FMOD Preview 与人工审听是可选质量记录，
不参与同步门禁。`MxFramework.UI.Audio` bridge 的存在不构成可听证据，也不得直接
修改 FMOD Metadata XML、generated catalog 或 StreamingAssets mirror 来跳过 Workbench。

### Phase 17 公共材质 Pair 与基础音效工作区

Workbench 顶层按“项目总览 → Cue 制作 → 候选实验室 → Basis 与响应 → 材质覆盖 → 游戏预览与发布”拆分。`collisionProfile` 仍保留碰撞体/受体方向用于生成；点击“提升为基础音效”后，双方材质按稳定 ID 规范化为全局无序 Pair，再绑定 Family 对应的动作 Variant。

```text
定义交互 → 生成并应用素材 → 提升为基础音效
  → 配置覆盖范围 → 模拟游戏属性 → Unity 真实试听 → 发布
```

当前工作台可以：

- 在独立顶层模式查看 14 种 Active 材质的 105 个上三角组合；A×B 与 B×A 不重复显示；
- 按 Variant、Required、缺失、fallback-only 和 Ready 筛选 Pair，并在右侧查看 Impact/Step/Landing/Hit/Block/Sweep 的 Basis/Cue/Event/采用版本/审听状态；
- 查看项目 Basis 库，并从已 Applied 的当前 Cue 版本提升 Pair×Variant Basis 草稿；Cue 页面只显示公共 Pair 引用并可一键定位；
- 编辑 Family fallback、cooldown、priority、selector weights 和 response profile；
- 声明 Required 材质集合与 Variant；只有 Required Pair×Variant 缺失才阻止发布，其他组合可以走显式材质 fallback；
- 查看 coverage report，并输入双方材质、质量、面积、表面、构造及交互参数，得到 Basis、得分、fallback trace 和参数预览；
- 从 Unity 捕获最近一次成功进入 Physical Audio 播放队列的真实交互事实，并把双方属性、交互参数、runtime Basis/Cue、匹配分和最终参数回填到情境预览；没有真实来源时会明确提示，不生成模拟数据；
- 对任意一个事实字段执行单参数 sweep，或对两个字段生成最多 64 点的二维覆盖热图。每个采样都请求 Authoring server resolver，点击采样点可回填事实继续权威试听；
- 通过显式 managed 操作在 `event:/MxFramework/Generated/Physical/<family>/<basisId>` 中创建受控 Basis/Cue/Event。操作会从模板继承 Bank/Bus/3D/距离，自动 Build Desktop Banks、同步 StreamingAssets、请求 Unity 刷新和校验 snapshot；失败时回滚，不修改未知外部 Event；
- 用独立 Physical hash 与 Semantic/Generation manifest 原子保存，并在发布时生成 Physical Catalog 双镜像。
- 发布时校验三份 manifest hash、Active Basis 的 Applied set、非原生必需 FMOD 参数存在性和 Bank hash 一致性。绑定 Basis string/runtime ID 与当前 adoption set 的人工试听证据作为可选质量记录；`PhysicalManifestHash` 随证据保存用于审计。当前 Volume/Pitch 为 backend 原生控制，不要求 Event-local parameter。

CharacterTest 已在成对 Catalog 有效且 hash 匹配时让 Footstep、Landing、ObjectImpact、WeaponHit/Block 使用统一 resolver；目录不完整时整份使用 legacy fallback。组合根已在角色 spawn 时从实体物理量缓存 Body/Foot，并缓存默认 katana Weapon profile；Combat hit/contact surface 仍负责 receiver 材质，动态装备切换、部位、Collider/地面属性刷新仍待后续。当前 checkout 尚无两份 Physical Runtime Catalog，因此实际运行仍整份走 legacy fallback。Air Displacement 的当前状态见 Roadmap；LowPass/Brightness/Scrape 的 parameter + automation evidence 尚未落地，当前 seed 只输出 backend 原生 Volume/Pitch。Scrape/Roll/持续挤压属于 Phase 18。完整目标见 `AUDIO_PHYSICAL_EXPRESSION_DESIGN.md`。

### 任务：捕获、扫描并权威试听物理情境

1. 在 CharacterTest 中触发一次脚步、落地、物体撞击或武器命中/格挡，使该事实成功进入 Physical Audio 播放队列。
2. 回到 Workbench 的“游戏情境预览”，点击“从 Unity 捕获真实交互”。成功后核对 runtime Basis、Cue、匹配距离、fallback 和最终参数；如果材质 runtime ID 无法唯一映射回当前 Physical Manifest，Workbench 会拒绝覆盖现有输入。
3. 选择一个字段运行 Sweep，查看 Basis 切换边界和未命中点；或选择两个字段生成二维热图，检查材质对、速度与强度组合的覆盖空洞。分析结果在基准事实或 Physical Manifest 变化后会标记为 stale。
4. 点击结果点把该事实回填，再点击“Unity 权威解析并试听”。Unity 会加载请求对应的 Runtime Catalog，运行真实 `PhysicalAudioResolver`，核对预期 Basis/Cue/hash 后播放 FMOD Event。浏览器解析结果不能单独形成批准证据。

### 任务：配置公共材质 Pair 与必需覆盖

1. 从左侧进入“材质覆盖”，选择 Variant 和状态筛选。矩阵只显示上三角；例如“金属×木材”已经代表反向交互。
2. 选择单元格。未创建的组合可分配稳定 Pair ID；已有 Pair 会列出每个 Variant 的 Basis、Cue/Event、当前采用版本和审听状态。点击 Cue 引用可返回素材工作区，Cue 的“在公共矩阵中定位”会深链回来。
3. 把项目玩法真正要求的 Pair×Variant 标记为 Required。Required 缺失显示红色，部分动作黄色，全部 Required Ready 绿色，仅 fallback 可达显示斜纹；非 Required 空组合不会单独阻止发布。
4. 在 Basis 范围和游戏情境预览中调整质量、面积、速度、强度与角度，再用 Sweep/二维热图检查同一 Pair Variant 内多个 Basis 的边界。修改 Pair、material fallback、Required、Basis 或曲线后必须重新校验覆盖。

材质 fallback 是 Material Profile 的显式有向链。Resolver 会同时搜索双方链，并按“总深度 → 最大单侧深度 → 稳定 Pair ID”选择唯一 Pair；fallback 环、同分歧义和 Required 缺失都会阻止发布。

### 任务：创建受控基础音效身份

1. 在“Basis 与响应”工作区选择 Family、Basis ID、显示名和显式 Event 模板；如果新建 Basis，runtime Basis/Cue/Event ID 会从各自单调空间分配，删除后不复用。
2. 点击受控创建操作。路径固定为 `event:/MxFramework/Generated/Physical/<family>/<basisId>`；外部 Event、额外路径段和不安全 ID 会被拒绝。
3. 等待“备份 → FMOD 创建 → Build Banks → 同步 → Unity 刷新 → Snapshot Ready”。Semantic/Physical manifest 只在最后验证通过后写入；中途失败会恢复 FMOD 工程、Build、StreamingAssets 和 snapshot。
4. 创建成功后再进入配方/生成/候选流程应用素材、提升或激活 Basis并发布；若需要内容质量验收，再执行 Unity 真实试听并记录结果。受控创建只建立稳定身份和 Event 模板，不自动生成素材，也不自动批准。

如果 Basis、Cue 和 Event Definition 已经预分配，按钮显示“创建 / 同步当前受控 Event”。此模式只把当前稳定身份 materialize 到 FMOD，不再创建第二个 Basis，也不会改 runtime ID。模板必须是当前 Snapshot 中真实存在的 OneShot Event；它只提供 Bank、Bus、2D/3D 和距离技术事实，不复制模板素材。创建后的空 Event 仍显示“素材 Draft / 未采用”，必须继续完成素材应用；Unity 试听和人工批准仅在需要内容质量验收时执行。

## 任务零：生成或重制高质量音效

1. 选择 Cue。Workbench 会从显示名、用途、语境、标签和 Event 技术事实派生可编辑的英文 sound brief，默认方向是“写实底层 + 克制的奇幻强化”，并固定要求 dry、close-mic、isolated、game-ready。
2. 在“配方 / 生成 / 候选与采用”三个标签页间完成工作。顶部摘要会分别显示候选总数和 FMOD 采用目标；例如 Light Attack 标准档生成 `3 × 6 = 18` 个候选，采用目标是 `3 × 4 = 12` 个。
   - Footstep、Landing、Object Collision、Weapon Hit（含 Light Attack）会进入接触音效物理配方。Workbench 按 Cue 语义自动标出“鞋底 × 地面”“落地角色 × 地面”“碰撞体 × 受体”或“武器 × 目标”。
   - 双方分别选择材质、质量、接触面积、表面和构造；本次接触再选择强度、速度、角度和方式。`rough`、`hard-slick`、`elastic`、`blocked` 等现有标签会形成初始值，点击“恢复语义默认值”可以重新应用。
   - 目标时长使用随类别变化的滑杆；短促、推荐、延展提供快速落点，精确输入收在折叠详情中。所有物理选项会随 Recipe 保存，并进入历史与最终 adoption provenance。
   - 未人工修改英文 Prompt 时，结构化物理参数会立即重建总 Prompt 和各层 Prompt；已有人工 Prompt 时只显示过期提示，必须显式点击“按当前参数重建”，不会静默覆盖。
3. 在“生成”页选择快速、标准或充分探索档位，也可逐层设置 `1–12` 个生成候选；单 Job 最多 36 个候选。最终采用目标在“候选与采用”页按层设置，并提供单样本、标准变化集和高频防重复快捷策略。点击“生成 N 个候选”后，浏览器只调用本地 Authoring server；Authoring server 再用 Bearer token 代理到 `192.168.1.200:10188`，不会把主机 token 暴露给页面。
4. 在候选卡中查看波形、频段、Peak/RMS、原始削波率、crest factor 和 spectral flatness，逐个试听并做 A/B。`Clipped` / `NoiseLike` 候选只保留用于诊断，禁止选择和采用。`Space` 仍只播放当前 FMOD Event，不会改成播放候选。
   - Ready 候选可展开“快捷剪辑”，直接在完整波形上拖动青色入点/出点和黄色淡入/淡出手柄；选区外会变暗，黄色包络线会实时显示实际淡化范围。下方仍可精确填写毫秒值（淡化范围 `0–200ms`），并支持播放选区、循环、自动裁静音、撤销和恢复原始。
   - 剪辑是 Temp 会话中的非破坏编辑，原候选不变。A/B 混音立即使用剪辑选区；正式采用时 Host 才输出 48kHz/24-bit WAV，并再次执行技术门禁。
5. 人工按 layer 勾选 source set，满足每层采用目标后点击“保存并应用替换 N 个”。Workbench 会保存不可变版本，并自动更新 FMOD Event、构建 Desktop Banks、同步 StreamingAssets、请求 Unity 刷新 Banks 与 snapshot。

```text
Assets/Audio/MxFramework/Generated/<eventDefinitionId>/sets/<setId>/<layer>/<take>.wav
Assets/Config/MxFramework/Audio/audio_generation_manifest.json
```

6. 等待进度完成。新的 set 会使旧人工审听证据标记为过期；若需要内容质量验收，可在 Workbench 进行 Unity/FMОD 试听并记录批准。
7. 点击“发布到运行时”。当前 set、Build Banks、StreamingAssets mirror 与 snapshot 必须一致；试听记录不参与同步门禁。

“候选与采用”的版本历史永久保留原始 FMOD 版本和所有正式采用 set；点击“应用此版本”会走同一自动应用管线，不覆盖历史 WAV。

候选生成和 FMOD 采用是运行时同步的技术流程；Unity 人工审听是独立、可选的内容质量流程。模型、Workbench 和 Development Agent 都不能伪造人工批准。

### 4080 Host 配置

部署目录为 `E:\MxFramework\AudioGenerationHost`，provider 源码固定到官方 `stable-audio-3` commit `8a3ded325ec6082113dbce11620b7b25b31ce6b0`。主机需要单独配置：

- 已接受模型许可证的 `HF_TOKEN`；
- 不进入仓库的 `MX_AUDIO_GENERATION_TOKEN`；
- 指向不可变 Hugging Face commit 的 `MX_AUDIO_MODEL_REVISION`，禁止使用 `main`；
- IndexTTS2/CosyVoice 的 stop/start 命令和 9888/9889 health 地址。

生产 Job 串行执行，并在 `finally` 中卸载 SFX、恢复并验证两个语音服务。`MX_AUDIO_GENERATION_FAKE=1` 仅用于部署 smoke，假音频禁止采用。

## 任务一：创建 Cue

1. 点击左侧“新建”。工作台从 Cue ID 单调递增空间分配 `runtimeCueId`；该 ID 删除后不复用。
2. 填写显示名、用途、使用语境和标签。`audioCueId` 是 gameplay contract，命名建议使用 `sfx.<domain>.<intent>`。
3. 选择 Event Definition。多个声学实现确实相同的语义 Cue 可以共享同一个 Event；Landing、Object Collision、Weapon Hit 这类声学语义不同的 Cue 应使用独立 Event Definition。
4. 选择 `OneShot` / `StartEvent` 和缺失策略。
5. 保持 Lifecycle 为 `Draft`，点击“保存草稿”。Draft 不需要 Unity 在线，也不会改动运行时目录。

Cue 的 `defaultParameters[].name` 必须按大小写精确引用当前绑定 Event Definition 的参数；其他 Event 中的同名参数不能满足此引用。未知、空名或空条目返回 Error `AUTH_RES_AUDIO_CUE_DEFAULT_PARAMETER_MISSING`，诊断包含 Cue ID、参数名、Event Definition ID 和 `cues/<index>/defaultParameters/<index>/name` 位置。缺失 Event 仍使用既有 Event 引用诊断，不追加参数错误；无默认参数的 Cue 和 schema v1 升级不因此误报。

含错误的草稿仍可保存，保留作者输入并返回诊断；正式发布返回既有 `AUTH_AUDIO_PUBLISH_BLOCKED`，在写入前阻断，保留已有 Generated / StreamingAssets 目录及文件。开发发布的非 Archived Cue 也不能绕过该引用检查，返回 `AUTH_AUDIO_DEVELOPMENT_PUBLISH_CUE_INVALID`。物理情境预览编译的 Active Cue 存在此错误时返回 HTTP 400 及参数引用诊断，不写预览目录或 Unity 请求。直接调用 `CompileRuntimeCatalog` 编译包含无效默认参数的 Active Cue 会抛出带上述诊断码和 Cue 信息的 `InvalidOperationException`，不会静默删参；有效默认参数的 runtime parameter ID 和数值保持原义。

复制 Cue 会分配新 `runtimeCueId` 并清除审听批准。归档不会释放 ID；永久删除只对“已归档且全项目零引用”的 Cue 开放。

## 任务二：替换 FMOD event

1. 选择 Cue，在“Event Definition”区域绑定目标 Event。
2. 检查替换契约：2D/3D、Loop、长度、必需参数和 Bank 是否保持兼容。
3. Event path 与 guid 来自当前 FMOD snapshot；不要手写一个 snapshot 中不存在的 path。
4. 绑定变化会立即使旧审听证据失效，但不会把技术状态从 `Ready` 降级；可选质量记录单独显示为过期。
5. 保存并发布。若本次需要内容质量验收，可先完成真实试听并记录批准；Generated catalog 会覆盖 CharacterTest 旧绑定，无需修改 C#。

“绑定另一个 Event”只改变 Cue 到一个已存在 Event 的引用，本身不会创建 Event 或触发 bank build。目标 Event 必须先在 FMOD Studio 中存在，并通过 FMOD Unity cache / snapshot 可见。与此不同，“保存并应用替换 N 个”属于素材采用管线，会自动修改当前已有 Event、构建 Desktop Banks、同步 StreamingAssets 并请求 Unity 刷新。

## 任务三：可选试听与发布

1. 保持 Unity Editor 已打开且完成脚本编译。
2. 先查看“审听证据”中的源素材波形、Peak 和低/中/高频相对能量，粗略判断起音、峰值和尾音。没有直接源文件的 Cue 会复用同一 Event Definition 下其他 Cue 的参考源，并明确标为“共享 Event 参考”。这些数据不代表 FMOD 随机、多层或参数混音后的最终输出。
3. 点击“Unity / FMOD 试听”或在焦点不处于表单控件时按 `Space` 快速试听。Authoring server 写入 `Temp/MxFrameworkAuthoring/Audio/preview_request.json`，`MxFramework.Audio.FMOD.Editor` 使用真实 `EditorUtils.PreviewEvent` 播放 Event，并回写状态。
4. 只有状态为 `succeeded` 后，“批准”或“拒绝”按钮才会启用。通用证据记录 request ID、Event binding hash、snapshot hash、当前 set/Bank 和时间；当前 Cue 已提升为 Basis 时，还记录 Basis string/runtime ID 与 Physical manifest hash。审批有效性校验 Basis string/runtime ID 和当前 adoption set；Physical manifest hash 仅供审计，Bank 一致性由 adoption diagnostics 校验。
5. 点击“保存草稿”，使批准证据落入 Authoring manifest。
6. 发布前确认三份 manifest hash 当前、FMOD snapshot 非 stale、Event/Bank/参数和 Runtime Catalog 技术校验通过，再点击“发布到运行时”。Cue 主状态 `Ready` 只表达技术可同步；可选试听记录在独立质量区域显示，不参与主状态。Volume/Pitch 走 backend 原生控制，不要求 FMOD local parameter。

发布同时写入：

```text
Assets/MxFrameworkGenerated/Audio/audio_runtime_catalog.json
Assets/StreamingAssets/MxFramework/Audio/audio_runtime_catalog.json
<package>/config/audio_cue_manifest.json
```

Unity 未连接时仍可保存 Draft，也可以在已有非 stale FMOD snapshot 且技术校验通过时发布；不能产生新的试听批准证据。Development Agent 不得自动批准艺术审听。

## 运行时与故障排查

- Editor 中 CharacterTest 读取 Generated catalog；Player 读取 StreamingAssets mirror。
- catalog 缺失或 JSON 损坏时，CharacterTest Console 会明确 warning，并整份切换到 legacy fallback；不会混合两份定义。
- Workbench 显示 hash conflict 时，说明 manifest 被其他工具修改；刷新后重新编辑，不能覆盖写。
- Preview 长时间 pending：检查 Unity 是否打开、是否有编译/模态弹窗、FMOD EventManager cache 是否可用。
- snapshot stale：先在 FMOD 构建 bank、在 Unity 刷新 Banks，再点击“刷新 FMOD Snapshot”。
- SFX Host offline：Cue 和 Draft 仍可编辑保存；检查 200 主机、`10188`、模型许可证/token 和 immutable revision。不得伪造成功 Job。
- `AUTH_AUDIO_SET_NOT_APPLIED`：当前版本未完整应用，回到版本历史重试。
- `AUTH_AUDIO_BANK_HASH_STALE`：Build 与 StreamingAssets Bank 不一致，重新应用当前版本。
- `AUTH_AUDIO_ADOPTION_REQUIRES_REVIEW`：采用时间晚于上次人工试听，旧质量记录已过期；仅在需要内容质量验收时重新试听批准，不阻止同步。
