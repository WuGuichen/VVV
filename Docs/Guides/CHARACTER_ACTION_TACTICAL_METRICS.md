# 角色动作战术元数据与标准靶测量

> 2026-09-09 距离口径修订：当前测量器 `character-test-reference-prober-v4-hurtbox-surface` 的搜索距离、命中区间和最佳距离，统一表示出招前自身姿态胶囊到参考靶机唯一受击框的三维表面净空。旧根距离摘要不能沿用，需重新测量。JSON 显式记录 `distanceMetric: HurtboxSurfaceClearance`；读取最佳点使用 `TryGetDefaultSurfaceDistance`。历史任务记录中的根距离结果不代表新版产物。

每个表面距离网格点通过正式完成帧几何查询布置靶机：在唯一胶囊前侧沿根节点 Z 方向搜索，定位误差最多 1 毫米；零距离取接触边界，避免任意深度穿透。若该规格下无法达到请求距离，测量失败，不返回伪造数值。动作命中和推荐点继续回跑正式 Attack Opportunity；此范围只是标准靶推荐，不保证实际目标必定可命中。

> Status: Guide
>
> 实施与验收记录：[任务 01 执行记录](../../StorySource/玩法基石/任务/任务01执行记录.md)。
> 纯数据接口：[CharacterAction](../Interfaces/CharacterAction.md)。

## 操作入口

### 当前角色的单项／全量重建

AI Lab 动作库底部提供 **“重建所选摘要”** 和 **“全量重建摘要”**，不可选动作也可以打开准备入口。窗口带入当前角色、实际装备和所选动作，不查询基础行为映射或正式持有清单。

1. 点击动作库中的对应重建按钮。系统先保存原卡制作内容、编辑位置、单卡测试模式和临时动作绑定；保存失败则保持播放并显示原因。
2. 系统自动退出 Unity 播放，跨域重载保留角色、装备、动作与任务种类。等 Editor 停止编译／导入并就绪后，才打开摘要窗口并自动执行重建；不会在播放中打开一排禁用按钮的窗口。
3. 查看进度与成功／失败／跳过清单。可用窗口取消按钮或测量进度框的取消按钮；取消在采样之间生效。成功项目保留，未完成及失败动作不覆盖旧缓存。全量遍历完整目录，与搜索／筛选无关，只针对带入的角色及装备。
4. 完成或取消后，点击“返回 AI Lab 原卡制作”，恢复原卡、编辑位置、临时动作绑定及单卡测试入口，动作库重新查询摘要。返回恢复制作上下文，不延续退出前的战斗帧。

独立入口：`MxFramework → CharacterTest → Tactical Metrics → Rebuild Character Action Summaries`（只在非播放状态可用）。窗口允许明确选择单项动作并手动启动；旧 `Prepare Character Opener Summary` 菜单保留为同一窗口的兼容入口，不再自动选基础攻击。

当前测量器只支持 `BasicAttack`；闪避无需空间摘要，技能与其它类型明确跳过，不把“批量完成”解释为所有动作均可执行。攻击结构不受支持时跳过；资源失败、无命中或区间不符合战术要求时记失败并继续。角色、装备及测量规格在批次内固定，批次期间动作作者源变化、进入播放、脚本重载或关闭窗口会中断。

逐项报告保存到本机 `Library/MxFramework/CharacterActionTacticalMetrics/BatchReports/`，记录角色、装备、测量规格、成功／失败／跳过及未处理动作。“定位结果报告”可打开所在目录。摘要为派生缓存，不修改动作正文、角色默认拥有或动态存档。

Unity EditMode：

- `MxFramework → CharacterTest → Tactical Metrics → Query Action 301`：重载离线读取缓存，检查已保存摘要，输出 Valid / Missing / Stale / Unavailable。不会隐式重测。
- `MxFramework → CharacterTest → Tactical Metrics → Remeasure Action 301`：重新读取正式动作/装备/烘焙输入，测量成功后原子更新派生二进制缓存，失败不覆盖旧文件。

结果位于 `Library/MxFramework/CharacterActionTacticalMetrics/source-668367834-action-301.bin`；具体路径同时输出到 Console。Library 是本机派生缓存，不提交为作者资产。菜单使用当前默认角色定义、60 Hz 标准执行、50mm 步长、[0,5000]mm 搜索，参考靶机版本 1、高 1800mm、半径 300mm、根局部中心 (0,900,0)mm。

菜单只允许 EditMode。它失效离线导入读取缓存，以读取磁盘变化；既有 reader 实例不被改写。运行中调用 store 时，应传入该 session 当前实际使用的 providers；磁盘文件编辑尚未经过正式热重载时，不把磁盘版本冒充当前已加载 Runtime 输入。

## 二进制缓存（2026-09-24）

摘要缓存仅使用 `.bin`，批量结果报告继续使用 JSON。原 `.json` 摘要不读取、不自动转换：只有旧文件时返回 `Missing / SummaryLegacyFormatRequiresRemeasure`，界面提示“旧格式摘要需要重新测量”。默认角色回退也只读取有效二进制；损坏的二进制不会回退到同名旧 JSON。

内部格式 v1：文件标识 `CTSM`（4 字节）、版本（UInt16）、负载长度（Int32）、负载、SHA-256 负载校验（32 字节）。整数为小端，字符串为 UInt16 字节长度＋严格 UTF-8；布尔只接受 0/1。负载保存距离口径、动作 ID、动作／靶机指纹、测量器版本、步长／搜索范围、靶机参数、状态、全部命中区间、可空最佳距离、边界标记与朝向扇区。它保持原有整数精度，不更改动作指纹、测量版本或过期判断。

读取上限仍为 1 MiB，字符串上限 4096 字节，区间数上限沿用 `MaximumGridSamples`。版本、长度、枚举、UTF-8、区间、边界、校验和尾随数据均严格检查。JSON 编解码工具只用于诊断、比较及已有指纹的语义投影，不是运行缓存读取路径。

重建先写临时文件并校验，再原子替换、重新查询；失败或取消保留原二进制，提交后校验失败回滚。只有对应动作的新二进制成功时才清理同名旧 JSON，其它角色／动作的旧缓存不删除。清理失败保留旧文件并警告，新二进制仍有效。缓存读取与编译不自动转换旧数据；用户通过已有重建入口逐角色生成。

## 其它动作和调用方集成

CharacterTest assembly 内部使用同一入口，文件缓存按源角色 ID 与动作 ID 隔离：

```csharp
var measurement = new CharacterActionSpatialMeasurement(
    CharacterActionRangeProber.Version, 50, 0, 5000,
    new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
var store = new CharacterActionTacticalMetricsStore();
using (var session = new CharacterActionReferenceProbeSession(actionId, measurement.ReferenceDummy, sourceCharacterId))
{
    var input = CharacterActionTacticalInput.FromSession(session);
    var loaded = store.Query(input, measurement);
    // Explicit user/configuration action only:
    // loaded = store.Refresh(session, measurement);
    if (loaded.TryGetDefaultSurfaceDistance(out double meters))
    {
        // Nominal Hurtbox surface-clearance approach target. Live eligibility still uses Attack Opportunity.
    }
}
```

读取、内容 SHA-256、序列化、测量均是 LowFreqAlloc，不放入 Tick、实时 guard 或逐帧 Timeline 求值。测量器仅接受当前已有的 BasicAttack/direct-hit evaluator；未知/缺失数据失败关闭。有限动作轨迹上限 4096 帧、单次距离网格上限 4096 点，超预算不会截断成有效结果。场地为固定水平地面，源角色和靶机不互相推挤；查询核验根坐标保持固定及实际表面净空符合请求，不能把坐标漂移或距离不符的结果当作有效样本。

## 测量权威与结果解释

1. 先通过 Gateway 真正执行动作，记录正式逐帧量化、物理结算后的源姿态。直接累加 RootMotion 配置会遗漏地面等结算，不能作为该探针的源轨迹。
2. 参考靶机只注册唯一一个 `reference-capsule` 受击框，以单叶 compound capsule 接入现有 Hurtbox owner 和 Combat Physics，不加载角色的多部位受击框。保留中心偏移、逻辑根节点与 snapshot revision。每个测量点必须核对当前帧、唯一受击框数量、逻辑身份、形状尺寸／偏移及受击层，任何不符直接拒绝产出摘要。测量器版本为 `character-test-reference-prober-v4-hurtbox-surface`，旧根距离摘要按既有版本门禁失效，需显式重测。
3. 每个距离点复用正式 Trace sampler、Attack Opportunity 与伤害资格判据。它是静态标准靶预测；运行时具体目标的状态、障碍、动画姿态仍由 live guard 决定。
4. `Intervals` 表达连续命中的离散采样段。只有相邻命中/未命中样本夹住的局部边界可用该步长描述精度，不宣称全局绝对边界或数学连续。
5. 封闭采样跨度中点回跑正式查询；脱靶改用已验证样本。整数毫米无法表达半毫米中点时，明确报告并使用已验证样本。开放边界不伪造 Dmax，中点证据/降级原因保存在直接测量结果中。

`NoHit` 无默认距离。`Discontinuous` 保留多个离散段和经过验证的推荐点。`OpenBounded` 的开放侧 `MinDistance` / `MaxDistance` 为 null，仍保留已观察命中采样；它可以同时不连续。扇区契约区分 Authored / Measured / Fallback；本中心轴探针目前输出明确的 Fallback（0°±30°），未进行角度覆盖测量，不能把该容差解释为实际覆盖范围。

## 失效与缺失

严格查询 `Query` 同时比较动作身份、当前实际配置/烘焙/装备/源胶囊内容 Hash、参考靶机几何及版本 Hash、算法版本、步长和搜索两端。任一变化返回 Stale，且 Metrics 为空。必需输入缺失或 JSON 损坏返回 Unavailable；文件不存在返回 Missing。所有拒绝结果的 `TryGetDefaultSurfaceDistance` 均返回 false。

2026-09-10 起，战术卡输入准备使用 `QueryForTacticalCard`：优先使用当前角色的有效摘要；当前摘要 Missing 或 Stale 时，允许借用默认角色 `DefaultPlayableCharacterId` 的**同动作、同规格且通过有效性校验**的摘要，并输出 `[Runtime AI Lab][TacticalSummaryFallback]` Warning。同一依赖组合去重提示，不在逐帧执行中查询文件或打印警告。

回退只借用空间推荐，当前动作的时序、资源规则和内容指纹仍来自动作自身。`CharacterTestTacticalCardInputs.SpatialSummary` 表示实际采用的空间摘要，`SummarySourceCharacterId` 与 `UsesDefaultSummary` 明示来源；冻结产物保留该来源。默认摘要原有 Hash 不会被改成当前角色的 Hash，也不会另存成当前角色的实测文件。依赖指纹包含当前动作内容与借用来源，避免运行身份误配。

显式查询与重测仍走严格 `Query` / `Refresh`。已测得 NoHit、损坏摘要、真实动作依赖缺失不被回退掩盖；默认角色本身缺少有效的同动作摘要时返回 `DefaultSummaryUnavailable`，不以另一个动作的数据充数。批量测量结果见[批量测量与默认回退记录](../../StorySource/玩法基石/任务/任务04_批量动作测量与默认摘要回退.md)。

Refresh 成功前不会覆盖旧缓存；失败结果也不会带回旧 Metrics。有效 NoHit 可以存储并重新加载，但始终不能提供向心目标。算法由 v1 改为记录实际执行轨迹的 v2 后，原 v1 摘要必须重新测量。

## 时序查询边界

`CharacterActionTacticalUtility.ExtractTiming` 读取双闭区间并集，避免重叠重复计数；缺失 Startup/Active 长度为 0，缺失 StartupEndFrame 为 -1，总时长保留 nullable。

`TryGetEarliestTransitionFrame` 直接复用 CancelRule.Matches。规则侧 Any=0 为通配，查询侧 0 不搜索任意非零规则；集合是存在匹配语义。返回动作本地最早窗口起点，仅用于规划，不表示当前时刻可切换、自然完成或通用退卡许可。
