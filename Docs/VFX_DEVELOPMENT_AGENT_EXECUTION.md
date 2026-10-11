# VFX Development Agent 执行规范

> Status: Current

## 当前创作入口

- 唯一 UI 入口是 `MxFramework/VFX/VFX Resource Library` → `创作实验室`。实验室内并列提供 `手动组装` 与 `Development Agent 创作`，不再维护 Fireball、Lightning 等效果专用窗口或菜单。
- `手动组装` 从当前项目允许的素材入口建立有序 Layer Stack；当前配置为 `ExplicitAuthored`，展示受版本控制的手工派生全局 Element、当前草稿本地 Element 和当前草稿实际引用的全局 Element。通用框架仍支持 `DraftLocalOnly`、自动精选、全量源组件和 Production Primitive 模式。
- 组合以 `mx.vfx.composition-draft.v3` JSON 保存到 `Assets/Config/MxFramework/Vfx/Compositions/`，每个具体 VFX 使用 `Build/Rebuild/Loop/Complete/Dissipate` 五阶段且 `Loop` 必填；旧版 v1/v2 不直接迁移并拒绝烘焙。预览只修改隔离实例；只有显式“烘焙 Candidate Prefab”才通过 Unity Editor API 写入 Git 忽略的 `Assets/VFX/MxCandidates/Manual/<draftId>/`。
- Source Library 深度索引是 `Library/MxFramework/VfxSourceLibrary/index.json` 下可重建的机器本地缓存；版本化 `AuthoredElements/` 才是手工派生素材的可见性权威。索引缺失不会隐藏素材卡，但会阻断成员级预览、编辑、导入和烘焙。Element 只保存稳定来源描述，不改写 `Assets/Piloto Studio`。
- 六套 Fireball 与一个 Lightning 仅作为普通 Composition Draft 示例，用于证明同一协议可处理不同效果；它们没有专用执行路径，也不能写 `Production` 或 `Ready`。

## 目的

VFX 创作实验室只负责创建和验证 `mx.vfx.agent-task.v1` 任务包。真正的资源选择、Prefab 组合、材质与贴图绑定、ParticleSystem 参数、曲线、渐变、Transform、模拟空间、生命周期和捕获迭代由本机 Development Agent 通过 Unity MCP / Unity Editor API 完成。

正式任务目录固定为 `Temp/MxFramework/VfxAgentTasks/<taskId>/`，候选 Unity 资产固定为 `Assets/VFX/MxCandidates/<taskId>/`。任务、进度和回执不进入 Runtime、SaveState、网络或 gameplay hash。

## 执行步骤

1. 在 VFX 创作工作台选择“创作实验室”，先点击“连接授权源库”，选择项目内或本机 Piloto 源目录。注册为 `LicensedReusable` 时必须同时绑定 package id、license evidence 和公开再分发授权；工具只在 `Temp/MxFramework/VfxReferencePacks/` 保存本机注册与扫描 manifest，绝对路径不进入 Catalog 或稳定报告。
2. 检查实时语义覆盖、Production/Ready 数量与审批证据一致性。当前目录为 40 项，strictCoverage=true；覆盖率仅表示合同完整。只有 Manifest、Catalog 与有效人工审批回执一致的 Production + Ready 才能作为正式视觉复用输入，不把历史数量或失效回执当作当前批准。
3. 填写目标 Effect ID、生命周期和视觉简报，点击“创建 Agent 任务”。任务绑定目标 Effect 合同 hash、Source Library/Curation、Catalog、生成模板目录和资源索引 hash；任一输入变化都会使旧任务 stale。
4. 点击“复制 Agent 指令”，把指令交给当前 Codex 任务。Development Agent 首先读取并校验 `task.json`。
5. Agent 将 `progress.json` 从 `Created` 推进为 `Claimed → Planning → Authoring → Capturing → AwaitingReview`。用户发出取消时先进入 `CancelRequested`，Agent 在安全边界写为 `Cancelled`。
6. Agent 检索 Effect、Recipe、Primitive、Prefab、Shader、材质、贴图和 Mesh。只有 `Production + Ready` 可作为视觉复用输入；`LegacyPlaceholder` 只能提供 ResourceKey、Definition、Trigger、Anchor、生命周期和参数合同。
7. 对 `ReferenceOnly` 包，Agent 只能观察 Hit/Explosion/Trail/Orb 的构图、节奏、层次和可读性，禁止形成依赖。对已验证的 `LicensedReusable` Piloto 源库，Agent 可以通过正式提取器复用职责层，但必须记录源 Prefab GUID、子对象路径、内容 hash、派生方式和 license；不得修改源资产或绕过 Unity Editor API。
8. Agent 只能通过 Unity MCP / Unity Editor API 克隆和保存 Prefab；不得手写 Unity YAML，不得修改正式发布资产本体。
9. 只有明确记录复用缺口后才能新增候选支持资产。缺少贴图时优先使用 Development Agent 图像生成能力创建有来源记录的 PNG/Flipbook；能力不可用时写 `mx.vfx.texture-brief.v1` 并把任务置为 `Blocked`，禁止回退到程序纹理。
10. 每个候选完成 1–3 轮真实播放与多时间点/多视角捕获，写入 `receipt.json`。回执必须区分 `contractReferences` 与 `actualVisualDependencies`，并记录 GUID/hash、Shader/材质选择、ParticleSystem 模块、Transform、迭代和 provenance。
11. Unity 工作台点击“刷新 Agent 结果”。只有 `vfx agent-task validate-receipt` 通过、无 LegacyPlaceholder 视觉依赖，且所有第三方依赖均由当前 `LicensedReusable` manifest 完整授权的候选，才可进入 A/B 实时播放与人工评审。
12. 用户播放并勾选“已人工运行检查当前候选”后，可用一次性内存授权把一个候选事务化移动到正式评审目录，并登记为 `Candidate + ReviewNeeded` Primitive。任何自动流程都不能写 `Production` 或 `Ready`。

## 六方向合同

首轮固定为：合同忠实、高辨识度、低预算、强冲击、主题变体、新基础单元探索。它们是创作目标，不是固定参数模板；六个结果必须体现不同的原创资源选择或配置方案，并达到相同的完成度基线。

`mx.vfx.provider.procedural` 仅保留给测试夹具和旧会话识别。它生成的贴图、材质、Prefab、截图和评分不得进入 Agent receipt、晋升证据或 Exemplar Index。

## ReviewNeeded 门禁

ReviewNeeded 只要求：回执与输入 hash 有效、Unity 序列化来源可信、无路径逃逸、无程序 Provider 证据、基础 Console/粉色材质/生命周期检查通过，以及用户真实播放确认。目标平台性能矩阵、fallback 等价性和更高等级审批仍是 Ready 前置条件，不能通过备注豁免。
