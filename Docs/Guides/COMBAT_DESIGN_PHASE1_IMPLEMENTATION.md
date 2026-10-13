# 战斗设计第一期：隔离端到端实现与集成交接

本期是共享面向机制的窄切片：Agent `query → plan → submit/status`，Editor owner 自动修改共享源、反查直接消费者、编译、生成描述与卡面文字、在独立目录发布、重载并重新编译；EditorHub 提供可读查阅。主项目草稿只读。实例、整备、动作槽装配、场景、Play、MCP 原生工具及跨 owner 批量事务不在本期已实现范围内。

## 数据与接口

- 权威源仍是 `CharacterTestRuntimeAiLabDraftWorkspace`、已有 DraftPersistence、共享源 codec、DSL compiler、PublishedBehaviorCard publisher 与持久目录 owner。没有第二套手填数值/关联台账。
- 生成物在 `Temp/MxFramework/CombatDesign`：`snapshots`、`plans`、`requests`、`responses`、`running`；`fixtures/<句柄前12位>` 包含独立 Drafts、StreamingAssets、恢复 journal 和证据。manifest 保留完整句柄并校验前缀碰撞，短目录避免 Windows 长路径。快照可重建；卡面文字与语义事实由 owner 从源生成，理由单独标注 Agent 编写。
- CLI：`authoring combat-design query|plan|submit|status --project-root <项目>`；plan/submit 用 `--request <UTF-8 JSON>`；status 用 `--operation-id <返回身份>`。HTTP 同一服务的 `/api/combat-design/query`、`plan`、`submit`、`status?operationId=...`。
- Plan 字段：`handle` 与 `snapshotHash` 从 query 返回，`thresholdDegrees`、`maximumFrames`、`rationale` 是设计输入。禁止传 authoring 路径、共享 ID、卡 ID 或任意属性补丁。提交使用 `operation`、`idempotencyKey` 和返回 `planToken`；validate/publish 使用 query 返回句柄与快照哈希。
- 操作：`create-fixture`、`refresh`、`commit`、`validate`、`publish`。不存在 Editor 时返回 `PendingEditor`，不是成功验证或发布。

## 保护与真实边界

Strict JSON 拒绝重复/未知字段和越界参数；路径拒绝 reparse/junction；计划绑定完整快照、登记编译源码 read-set、owner revision，过期/源变化拒绝。owner 在事务前重复 CAS 与 Facing 结构语义识别，保留非目标字段；消费者通过既有编译器校验后才持久化。改值后旧证据自动陈旧，证据不是仅一个「完成」勾选。

同键同体返回同一操作与已完成结果，包括自身修改使旧快照失效后的重试；同键异体冲突。排队/回执使用原子文件写入与排他锁。Editor 中断留下 running 与 owner 恢复 journal，禁止盲目重放；`refresh` 请求既有目录 owner 恢复，重读权威状态，再重新计划。发布按稳定卡身份顺序逐卡复用持久 owner，每卡独立提交；不宣称跨卡的全有或全无。若提交后草稿 checkpoint 中断，再次发布会按固定源比较重载版本并自动收敛已有提交；未证明可恢复时返回 `RecoveryRequired` 并保留诊断。

样例自动从已编译 ID 目录选择三个现有身份，仅用于独立目录；不改全局身份目录、不写正式 Catalog，不证明新 ID 分配链路。样例两卡引用共享面向机制、一张对照卡无直接引用。反查范围是已识别共享定义到真实编排引用/草稿卡，不声称覆盖全部战斗对象或设计意图。

生命周期分开显示设计、代码契约、配置、启用范围、编译证据、Play。卡面预览是既有生成器的文字输出，不是 FairyGUI/引擎视觉截图。主项目草稿不能证明当前已启用或全部设计语义；无法概括的结构保留源与警告，不按名称猜测。

## 帧同步边界

遵守 [Frame Sync Development](CHARACTER_TEST_FRAME_SYNC_DEVELOPMENT.md) 与 [Boundary Remediation](CHARACTER_TEST_FRAME_SYNC_BOUNDARY_REMEDIATION.md)。没有改 RuntimeFrame、固定 Tick、命令入口、战斗状态、SaveState/network/replay schema。阈值和预算写入既有静态 DSL，预算是整数帧；DateTime 仅用于 authoring 计划有效期、证据时间，Editor realtime 仅用于轮询，Guid 仅生成并固化作者身份/临时文件，均不参与 authority Tick 或战斗存档。队列及发布内容按稳定身份排序，同步 owner 编译/提交，冲突不自动 rebase；后台任务完成先后不改变被冻结的发布源。

## 验证与 Editor 窗口

2026-10-01 本工作树已完成：portable gateway 8/8；Authoring Core/CLI 构建；Hub JS 语法检查；通过主项目已编译程序集与 Unity 引用进行新 Editor owner、既有 bridge partial 与四项新增 NUnit 测试的源码接口编译检查。此检查没有启动 Unity，不等同于 Editor 导入、测试执行或 Play。本机没有可用 Python，code standards 扫描器未执行；只做定向文本边界检查。

主项目已合入17个明确文件并完成 Editor 编译。四项定向 EditMode 第二轮3项通过、发布项因 Windows 已存在 `.previous` 备份替换失败；隔离 adapter 将旧备份保留到 LegacyPrevious 后，该失败项定向重验1/1通过，包含发布重载重编译、重复发布新增版本0。既有 Console 留有 UAC0005 警告，不宣称 Console 历史记录清零。

2026-10-02 最终队列修复已合入主项目：错误字段安全转为 JObject，单操作加重入保护；未验证快照输出 `current=false/level=not-recorded` 对象，保留缺证据语义。真实恢复栈定位到 Windows 快照原子替换失败；同一完整临时文件最多额外重试3次、总退避140ms，不删除目标、不重放 owner 事务。最终 owner SHA256：`f2d300df63b5cd72c7a70f596053fbfe845d581d965a3e3048426500465f5c21`；实际加载的 Editor module MVID：`14d98598-57f0-412a-a889-933e606ca342`。聚合检查遇 domain reload 失联，未把失联返回当成功；实际新程序集加载及下述真实队列执行确认编译有效，最终 Console error 查询为0。

最终版本 CLI → Editor 完整流程通过：机制8度/45帧、自动定位2消费者、对照卡revision保持、旧证据/计划失效、同键重试10次、生成描述/卡面文字、隔离发布重载重编译、重复发布新增版本0。定向故障探针通过：JSON `plan:null` 与过期快照准确返回 SNAPSHOT_CONFLICT，保留失败handle及诊断，拒绝写入删除running标记，重复读回执保持不变。历史中断fixture通过新refresh/新key收敛已有发布，新增版本0；原中断key、回执和journal保留，没有盲重放。主草稿只读查询成功，未验证证据不会冒充已验证。

本机证据目录（不入仓库）：`C:\Users\WuGC\Documents\Codex\2026-10-02\task-2\combat-design-p1-baseline`。最终结果为 `agent-end-to-end-final/result.json`、`queue-closeout-probes-result.json`、`readonly-query-closeout.json`；早期失败记录仍保留，不能用旧结果覆盖最终源码绑定证据。

HTTP 静态页面/JS/CSS返回成功；重复/未知字段400、跨源403、非JSON415已验。当前环境缺少 Browser/ComputerUse JS 执行工具，实际网页点击、重复/错误 UI 状态与视觉卡面未验。临时HTTP服务已停止。Editor已释放：非Play、非编译/更新，TacticalCardDemoScene无未保存修改。259个正式JSON相对初始基线未变；场景磁盘于2026-10-01 19:10 UTC已有旧基线外变更，早于本轮收尾，未覆盖/回退。没有正式卡、场景保存或用户存档写入，没有Play、Player打包或无关整套测试；新增失败仅做对应路径重验。

一期结构化CLI与隔离owner发布闭环已完成；真实浏览器查阅验收仍未完成，机器可验证文本预览不等于引擎截图。不是正式物品配置迁移、实例/卡组整备接入或P2/P3/P4完成。

## 可用入口

在主仓库运行 `Tools\MxFramework.EditorHub\start-editor-hub.bat`，默认4873端口，进入「战斗设计」；直达 `http://127.0.0.1:4873/Tools/MxFramework.EditorHub/web/combat-design.html`。端口以launcher输出为准；本次临时4986服务已停止，不宣称当前在线。

CLI示例：`dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- combat-design query --project-root E:\Unity\WxFramework\WGameFramework`。修改从query取得句柄/hash，再plan、submit/status；fixture写入仍要求项目Editor在线，主草稿只读，没有新增人工ID/关联台账。

已有共享文件仅四个：

1. `Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.cs`：仅在显式隔离 scope 中使用同一 owner 的隔离 reload。
2. `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/Program.cs`：命令分发。
3. `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.cs`：路由。
4. `Tools/MxFramework.EditorHub/web/app.js`：项目范围查阅入口。

其余为新增 Core gateway、CLI/HTTP、Editor owner/bridge partial、Hub 三文件、NoEngine 测试、四项 EditMode 测试、验收脚本与本指南。不合入工作树继承的 facing 8 源文件/2 测试，也不合入 plan/catalog 文档副本；它们只是 dirty 基线输入。没有碰 LevelEditor、植被、场景、正式 authoring 内容、身份目录、存档、asmdef；无 commit/push。
