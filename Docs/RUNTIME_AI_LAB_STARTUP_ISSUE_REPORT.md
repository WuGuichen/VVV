# Runtime AI Lab 启动分配问题反馈

> Status: Current
>
> 观察日期：2026-09-13。本文记录本次现场诊断，不宣称优化已完成。对应方案：[启动优化设计](RUNTIME_AI_LAB_STARTUP_OPTIMIZATION_DESIGN.md)。
>
> 源码基线：418701eeb47bcad97201e1e28209e93f2af92abc，加工作区尚未提交的 UI 优化与启动 Profiler 标记。实现或内容改变后，应重新采样并更新问题状态。

## 1. 结论与范围

**优先处理调用组织、对象生命周期和历史存储策略；随后优化内容装载机制。单独将 JSON 换成二进制不能根治本次尖峰。**

这里的“逻辑问题”主要是何时准备、准备几次、保存多少、由谁持有数据，不代表战斗规则已经错误。“底层实现问题”主要是每次解析、克隆、构建对象和校验的成本。两层叠加，不能把同一笔分配重复归入两个独立收益项。

判断依据：

- 诊断历史全容量复制与预分配约 484.3 MiB，占本次准备链约 46.4%。这部分在内存中分配数组，替换 JSON 格式不会改变它。
- 动作配置约 256.6 MiB，既有同一启动加载 6 次的调用问题，也有每次重建 JSON、Registry 和验证结果的实现成本。
- 已有整数 ID 查询约 113.3 MiB，既有保存中重复执行 20 次整目录事务的调用问题，也有目录 JSON 解析、深拷贝和规范化哈希的实现成本。
- 上述三类合计约 81.9%；剩余约 18.1% 已划分至资源、模块、帧存储等阶段，尚未逐对象归因。

本文聚焦“开始测试播放”启动帧的 Controller 准备链。角色 Spawn、UI 重建、首次 JIT 是另列问题，不能混入这 1.02 GiB 的收益计算。旧字符串帧哈希迁移、全仓序列化替换和脚本后端更换不属于本次诊断结论。

## 2. 证据口径

### 2.1 原始用户采样

场景为 CharacterTestRuntimeAiLabScene。用户点击“开始测试播放”，当前 Profiler 缓冲区原始帧号为 297：

| 原始标记 | CPU 时间 | 分配 / 次数 |
| --- | ---: | ---: |
| Main Thread | 5,451.47 ms | 约 1.19 GiB |
| Controller.Update | 5,282.31 ms | 约 1.19 GiB |
| Controller.Update Self | 2,962.62 ms | 不等于某一个具体业务方法 |
| Controller 直属 GC.Alloc | 257.58 ms | 7,263,375 次，约 1.02 GiB |
| GameManager.Simulation | 982.75 ms | 3 次，约 135 MiB |
| CharacterRuntime.Spawn，包含于 Simulation | 792.04 ms | 约 134.6 MiB |
| BindAiLabModel | 432.47 ms | 约 29.7 MiB |
| BuildAiLabModel | 84.88 ms | 约 6.8 MiB |
| Controller 直属 Mono.JIT | 375.41 ms | 1,931 次 |

父子标记存在包含关系，不得相加。GC.Alloc 的时间也不是“分配对象的全部业务 CPU 时间”。上述分配是累计分配量，不是同时存活量，也不是已证明的内存泄漏。

### 2.2 分段复现

Windows x64，Unity 6000.5.0f1，Mono Editor；Deep Profiling 关闭。增加 28 个启动阶段 ProfilerMarker 后，重新打开同一战术草稿，走生产 QueueTestCurrentChanges / Update 启动路径。为保留工具触发附近的完整大帧，分段复现开启 Editor Profiling；统计只取 Controller 子树。

本次首次启动复现帧号 2419，Controller 累计 1,273,243,960 B。扣除 Simulation 141,450,743 B、Bind 31,208,992 B、Build 7,105,120 B 后，准备链为 **1,093,479,105 B = 1,042.823 MiB ≈ 1.018 GiB**。该口径与原截图的“直属约 1.02 GB”近似对应，并不是声称跨次采样字节逐一相等。

| 准备链来源 | 原始字节数 | MiB | 状态 |
| --- | ---: | ---: | --- |
| CaptureRecovery | 338,383,782 | 322.7 | 阶段实测，诊断存储 Clone 分配栈已验证 |
| PrepareTimeline | 169,442,384 | 161.6 | 阶段实测，构造路径与数组数量对应 |
| LoadActions，共 6 次 | 269,116,242 | 256.6 | 阶段实测，调用来源已逐条核对 |
| PrepareNumericIds（SaveDraft 子阶段） | 118,774,224 | 113.3 | 阶段实测，单次已有 ID 查询另有调用栈 |
| 其余准备工作 | 197,762,473 | 188.6 | 残余总量；阶段可见，未逐对象归因 |
| 合计 | 1,093,479,105 | 1,042.8 | 准备链 |

本次 Editor Profiling 与调用栈诊断增加了观测开销；不得把复现大帧耗时直接当作优化前后 CPU 对照。原始采样没有分配调用栈，后续通过限定范围的独立调用验证栈来源。

线程分配计数器 GC.GetAllocatedBytesForCurrentThread 在本机连正向分配对照都返回 0，判定不可用。本文分配字节来自 Profiler GC.Alloc 元数据，未使用该计数器作“零分配”证明。

## 3. 问题清单

### STARTUP-001：恢复与新会话重复承担全容量诊断存储（P1，未修复）

**责任层：历史存储策略和恢复生命周期，兼有对象布局成本。置信度：高。**

首次启动时旧时间线容量为 1,800，当前测试帧上限为 900。CaptureSessionStartRecoverySnapshot 为失败恢复调用 Timeline.Clone；DecisionTraceStore.Clone 遍历整个槽数组，未使用的槽也创建新的 FrameSlot / AgentSlot / Facts / Candidates。随后 PrepareTimelineForSessionStart 再创建新时间线及其完整诊断 arena。

已验证调用栈：

~~~text
SessionStartRecoverySnapshot
  → Timeline.Clone
    → DecisionTraceStore.Clone
      → FrameSlot.Clone
        → AgentSlot 构造
          → Facts[] / Candidates[]
~~~

存储默认每个保留帧另加 4 个恢复槽，每槽最多 8 个 Agent。因此新时间线为 (900 + 4) × 8 = 7,232 个 AgentSlot；旧时间线恢复则为 (1,800 + 4) × 8 = 14,432 个。

新时间线对应 7,232 个 13,344 B 数组和 7,232 个 9,504 B 数组，两类合计 165,236,736 B。加上外围对象，PrepareTimeline 为 169,442,384 B。旧时间线 Clone 约为其两倍。这是“为未来容量预分配并在恢复时再完整复制”的成本，不由磁盘 JSON 引起。

反向约束：历史查询、失败恢复、分支 pin、Seek 和诊断溢出处理需要保持；不能删除记录、共享可变数组或覆盖已发布帧来获得低分配。当前构造后 NoAlloc 的目标合理，问题在于全容量即时物化和恢复复制的策略。

源码：[诊断存储及 Clone](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabDecisionTrace.cs)，[时间线创建](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerDevSceneController.Timeline.cs)，[恢复快照](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerDevSceneController.ShellNavigation.cs)。

### STARTUP-002：同一次启动完整加载动作配置 6 次（P1，未修复）

**责任层：内容准备的调用组织与加载实现共同造成。置信度：高。**

| 调用来源 | LoadActions 次数 | 分配 |
| --- | ---: | ---: |
| ServerSlice + ClientSlice 的 CharacterRuntime | 2 | 89,705,414 B |
| 会话权威目录 SessionAuthority | 2 | 89,705,414 B |
| 玩家卡组目录 PlayerDeck | 2 | 89,705,414 B |

LocalPlayerDeckService.TryCreateCatalog 先 Load 动作配置，再调用 ProjectCatalogLoader.Load；后者内部又 Load 一次。会话权威和玩家卡组各走一遍这个嵌套路径。

每次 Load 经过 ResolvePayload / ExternalJsonResolver、动作调参、CharacterActionJsonImporter.Import、Registry 创建和验证。六次不是仅创建六个轻量运行实例，而是重复走完整内容准备。

反向约束：Server/Client 的可变执行状态必须独立；修改动作源、调参、导入器或整数目录后要正确失效。不能直接把整个 CharacterActionRuntime 单例化，也不能仅用路径或显示名称证明内容相同。

源码：[动作配置入口](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Combat/CharacterTestCharacterActionConfigSource.cs)，[BuildRuntime](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Combat/CharacterTestCharacterActionConfigSource.Runtime.cs)，[TryCreateCatalog](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/RuntimeAiPlanner/CharacterTestRuntimeAiPlannerLocalPlayerDeckService.cs)，[ProjectCatalogLoader](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiPlannerProjectCatalog.cs)。

### STARTUP-003：保存时已有整数 ID 查询反复解析全目录（P1，未修复）

**责任层：事务粒度与已有 ID 查询实现。置信度：高。**

PrepareForSave 对每个 entry 查询 entry ID 与 card definition ID。Editor bridge 将查询直接连接至 GetOrAllocate；即使已存在，Mutate 仍先读取整个目录、ReadValidated、CanonicalHash，再判断条目是否需要新增。

CanonicalHash 先 DeepClone 文档，Canonical 再递归建立排序后的 JSON 树；还有文件字节读取、内容哈希和唯一性检查。本机目录文件为 132,238 B。

限定范围的已有 ID 查询实测：返回的整数前后均为 1；单次分配 **5,938,576 B、80,870 次**。分配栈命中 JObject.Parse → ReadValidated，以及 JValue.CloneToken、Canonical。本次保存样本的 20 次查询对应约 118.77 MB；20 × 单次字节数与阶段总量仅相差少量调用外围开销。20 不是固定协议次数，调用数随待保存 entry 数量及是否纳入 selected presentation 变化，当前实现每 entry 调用两次。

SaveDraft 总计 124,955,354 B，其中 PrepareNumericIds 为 118,774,224 B；两次 LoadDraftDirectory 合计仅 4,053,722 B。因此“保存的主因是两次草稿回读”已被阶段实测否定。

反向约束：新增/复制分配新号、retired 不复用、revision/hash 冲突检查和原子提交必须保留。优化应改变一次事务中的工作复用，不能跳过目录校验或在 Runtime 现场分配身份。

源码：[PrepareForSave](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabDraftNumericIdentities.cs)，[保存事务](../Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestRuntimeAiLabDraftPersistence.cs)，[GetOrAllocate / Mutate / CanonicalHash](../Packages/com.vincent.wgameframework/Editor/CharacterAction/CharacterContentIdentityAuthoringCatalog.cs)。

### STARTUP-004：准备链其余约 188.6 MiB（P2，部分归因）

已看到资源运行时创建约 35.6 MiB、Slice 中除动作加载之外的模块准备、帧存储预热约 13.6 MiB、候选/校验和其他启动状态构造。它们不是一个已证实的单一根因，也不能全部计入“JSON 可节省量”。先完成前三项后复测残余，再选择下一项。

### STARTUP-005：启动时释放 UI 实例并整树重建（P2，单列范围）

RebuildRuntimeSession 先 ReleaseUiRuntime，末尾重新 ConfigureUiRuntime。上一轮静止页面缓存随旧实例释放，因此启动仍会完整构建 UI。原始 Bind 子树包含 27,822 次 AddComponent、13,316 次 SetParent 和 13,805 次 Activate。以上数量属于 UI Bind 子树，不能归给角色 Spawn。

静止页面此前已实测 300 个渲染帧 / 50 次检查 / 0 次完整绑定。该结果不代表启动 UI 已优化。保留 UI 外壳需要重新绑定会话服务并解除旧订阅，否则会产生陈旧回调和资源泄漏。

### STARTUP-006：Bootstrap 与角色 Spawn、首次 JIT（单列，待后续测量）

现场 bootstrap 计数为 2 次初始化 Tick，随后另有首个可见状态 Tick。必要的角色生成、注册和 authority 基线不能直接删掉。原始 Spawn 约 134.6 MiB 和首次 JIT 375 ms 各有独立证据，本文不将其算入准备链前三项，也不据此宣称每次启动都支付同样的 JIT 成本。

## 4. 逻辑层与底层实现的责任判断

| 维度 | 证据支持的判断 | 不支持的结论 |
| --- | --- | --- |
| 优先级 | 生命周期、事务粒度和重复调用先处理；其中诊断存储一项占准备链约 46.4% | 不能声称全部底层代码都没有问题 |
| JSON | 动作内容及 ID 目录存在可测解析/对象树成本 | 不能把 1.02 GiB 都归给 JSON |
| 二进制 | 适合制作期准备后、运行时只读的动作定义/目录产物 | 不能靠改扩展名、压缩 JSON 或换通用 serializer 消除重复加载和数组 Clone |
| 预分配 | 可维持稳态无分配，但要计入启动、恢复及峰值内存 | 不能只展示 Tick 0 GC，隐藏 484 MiB 启动/恢复成本 |
| 数值身份 | 整数身份本身已存在，取号调用仍做全目录工作 | 不能把这笔成本解释成整数协议本身低效 |

## 5. 已完成、未完成和验收边界

已完成：现场分段归因、诊断存储与已有 ID 查询栈验证、6 次动作加载来源核对、28 个 Profiler 标记；最后一次 Unity validate_unity_scripts 返回 passed，refresh / compile / domain reload / Editor ready 完整且 Console error = 0。

尚未完成：分配策略修改、二进制实现和格式选型、优化后的行为/性能验收、Player 对照。本文没有承诺某个节省比例已经实现。既有 UI 与迁移改动保留，新增标记也尚未提交。

本机证据位于 .agent/startup-allocation-conclusions.json、startup-final-allocation-breakdown.json、numericIdStackCapture.json、numericIdInnerStacks.json 等文件；这些是未版本化的机器采样。本文冻结关键数字、方法和边界，便于没有本机文件的评审者理解。复现前应额外保存内容指纹、命令序列和 profiler 设置，形成正式验收基线。

验收细则及修复顺序见[启动优化设计](RUNTIME_AI_LAB_STARTUP_OPTIMIZATION_DESIGN.md)。
