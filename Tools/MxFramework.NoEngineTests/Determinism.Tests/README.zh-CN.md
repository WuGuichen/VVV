# 2026-10-08 恢复修复回归

以下既有记录是历史证据，其 strict 3fail / GC 6fail 不代表当前结果。本次当前报告存于任务工作区 `evidence/candidate`，运行不启动 Unity。

本次修复完整 allocator/save v2、hash v2、缺 history 明确恢复/失败停止、限速 retry、完整 participant coverage、full snapshot 重复/旧帧处理、明确重连、恢复期命令保留、同帧 history 替换与 full restore history barrier。原有 provider 严格续建回归保留，live projection 回归改为正式 allocation-state API；没有放宽 generation 或身份断言。

统一 runner 的 TacticalExecutionPort/SingleCardDriver 链接已不适合当前 noEngine 入口，它们新增了项目侧 behind-preview 类型依赖且不是本入口 fixtures 的调用依赖。清单改为真实纯逻辑 TacticalExecutionInput/TacticalPointMoveRules；原有 TacticalCardEngine、TimeCoordinator 和 TacticalDeterminism tests 均保留。没有 stub、复制玩法算法、Ignore 或删除旧 fixture。新增生命周期恢复测试不按 Allocat 名字误路由到 GC 类。

当前源码不宣称完整 StageRun 战斗中恢复、UnityTransport 多机或全平台通过。原生清单含实际 GameSlice history exhaustion、新增 provider/native store 回归、既有单卡 session restore 与 StageFlow/receipt/source-sequence 测试。GC allocation 分类本次未执行，旧 6fail 保留为历史线索。

---

# 历史工作记录（原基线 992edb4）
# 专用确定性测试入口

本套件直接链接生产源码和原有 NUnit fixtures，不复制模拟算法，不安装依赖，不启动 Unity。开发基线为 `992edb4bb288aea046714c47bd6c1bddddcffea7`。它是分层回归检查，不能证明整个项目、任意输入或跨平台位级一致。

## 统一运行

在仓库根目录执行（需要现有 .NET 9 SDK、NUnit 3.13.1 和 Newtonsoft.Json 13.0.3 DLL）：

```powershell
./Tools/MxFramework.NoEngineTests/run-determinism.ps1
./Tools/MxFramework.NoEngineTests/run-determinism.ps1 -Suite restore -SkipBuild
./Tools/MxFramework.NoEngineTests/run-determinism.ps1 -Suite network -SkipBuild
./Tools/MxFramework.NoEngineTests/run-determinism.ps1 -Suite regressions -SkipBuild
./Tools/MxFramework.NoEngineTests/run-determinism.ps1 -Suite allocation -SkipBuild
./Tools/MxFramework.NoEngineTests/run-determinism.ps1 -Suite all -SkipBuild
./Tools/MxFramework.NoEngineTests/run-determinism.ps1 -Suite unity-plan
```

`-SkipBuild` 仅在源码与上次构建相同的情况下使用；改源码后先重新执行默认命令。可通过 `-NUnitFrameworkPath` / `-NewtonsoftPath` 指向已有 DLL；缺依赖会报错，不自动下载。包源为空，restore 只生成这个小工具的 SDK 构建元数据。受限 Codex 环境仍可能需要批准读取用户级 NuGet 设置。`-OutputDirectory` 指定报告位置。失败返回非零退出码，报告是标准 NUnit XML。

## 日常分层与实测成本

| 入口 | 何时运行 | 实测用例 / 结果 | 本机测试耗时（不含进程启动） |
|---|---|---|---|
| core | 日常 tick、随机、命令、动作、Buff、伤害、牌堆规则变更 | 205 / 全通过 | 0.333 秒 |
| restore | 快照、存档、身份、动作恢复、Buff 生命周期变更 | 58 / 全通过 | 0.325 秒 |
| network | 传输协议、命令排序、去重、rollback、hash 改动 | 46 / 全通过 | 0.214 秒 |
| regressions | allocator 或迟到命令路径变更时必须运行；不得只看上面绿色结果 | 3 / 全失败（两种缺陷） | 0.141 秒 |
| allocation | 按性能变更或低频检查，独立于逻辑确定性 | 16 / 10 通过、6 失败 | 0.228 秒 |
| all | 显式聚合以上分类，包含红色回归与分配断言 | 本次未额外重复运行 | 与上述分类同一用例集合 |
| unity-plan | 导出低频集成清单，由根协调空闲 Editor 窗口 | 导出成功，Unity 测试未运行 | 不适用 |

本机小工具构建约 4.4 秒；这是链接模块的构建，不是 Unity 全量 build。29 条编译警告来自已有源码。成本会随机器、配置和 JIT 变化。各分类通过 NUnit 原 runner/filter 执行，TestCase、fixture setup/teardown 和既有断言保持有效。分配类按名称 `Allocat` 路由，恢复类按 SaveState/Snapshot/Restore/Checkpoint 路由，其余网络 namespace 独立；改名后应复核 XML 用例清单。已知严格契约回归以 `DeterminismRegression` 路由，不 Ignore、不吞失败。

## 覆盖矩阵

| 契约 / 状态 | 复用内容 | 本次完善 | 边界 |
|---|---|---|---|
| 同初态 + 同逐帧输入 | RuntimeFrameCommandReplayTests、RuntimeHostTests、RuntimeDeterministicRandomTests | 64 帧生产 Clock/CommandBuffer/Random/EntityLifecycle 双跑，逐帧比较 | core owner 的行为组合；不能代替完整 GameSlice 双实例 |
| 随机与未来队列 | Random CaptureRestore、命令缓冲 sequence/frame/pending 恢复 | 0/23 帧边界续跑；检查算法、seed、drawCount、state、nextSequence | allocator 有空洞的情况单独红色回归 |
| render 节奏独立 | 实际 CharacterTestFixedTickDriver | 快慢/零 delta 与 hitch 超预算两条节奏，比较相同 logic frame，并验证固定 admitted delta | 不同 wall 时间到达帧数可不同；没有比较 cloth、camera、像素；未运行 Unity 视图异步故障 |
| replay 首分歧 | RuntimeReplayPlaybackTests、RuntimeGoldenReplayHarnessTests、RuntimeSnapshotDiffTests | 新 FrameTrace 用正式 diff 输出首分歧帧、字段、expected/actual、输入、最多 12 个变化字段 | 原 replay 仍以其 hash 覆盖范围为准 |
| allocator 完整恢复 | GameplayComponentStoreTests、正式 save provider | live snapshot 和正式 save provider 两条续建回归 | 当前只存 live entities，空闲 generation/order 没恢复：红灯 |
| 动作状态 / 事件身份 | CharacterActionRunnerTests、CharacterActionPhaseAuthorityTests、CombatActionStateSyncSystemTests | 扩展 gateway 恢复测试至动作结束，比较实例、nextId、frame、phase、state、trace、finish 和事件 | 测的是正式 noEngine action owner，未声称每种真实动画/战斗绑定皆覆盖 |
| Buff / modifier | GameplayComponentBuffModifierTests | 捕获时 hash 相等之外，续跑到过期，比较每帧 hash / attribute，并确认 Buff 消失 | 不包含全部角色内容与所有效果组合 |
| 伤害 / HP | CombatDamageApplicationSystemTests | 正式世界存档恢复后，逐帧比较伤害生成的 RuntimeCommand 与 HP | 已有命中、armor、guard 断言保留；新 trace 不穷举所有 damage profile |
| 战术牌堆 / 动量 / 阶段 | TacticalCardEngineTests、TacticalTimeCoordinatorTests | 24 回合两份 owner 比较所有牌区顺序/身份/version、pending/resolving、private random/nextId、动量 token、turn phase/pause | 测试只读反射补观测，不改生产；**不是完整 StageRun 快照或恢复** |
| 战术 queue / ingress / Story | 既有 TacticalStageFlowCoordinatorTests、TacticalStageFlowTests、TacticalStageEndTriggerTests | 统一列入 Unity 集成计划 | 依赖实际资源/Story/角色绑定；不能用假桥替代后宣称通过；尚未运行 |
| 晚、乱、重复命令 | NetworkSessionRunnerTests 原 ReliableOrdered/rollback/duplicate fixtures | 使用原 LoopbackNetworkTransport、rollback participant，新增 history 用尽后 resync-or-fail-stop 强契约回归 | 纯 server/client runner 可运行；真实 UnityTransport/远程双机未运行 |
| 当前场景组合根 | 既有 CharacterTestReplay/FixedHash/Slope/BodyPart/NetworkLoopback tests | 统一导出真实 GameSlice 验证清单 | TacticalCardDemoScene 当前 Offline；不能拿 generic host/client 测试替代其 ready/freeze 证明 |

## 重复与维护成本

保留了有独立价值的原测试，没有删除 fixtures 或迁移生产代码。核心回归复用命令队列、随机、Host、save provider、既有 fake ports；新 fixtures 驱动正式 owner，未实现第二套伤害、洗牌或 rollback 算法。generic golden replay 校验播放器的错误报告；CharacterTest replay 校验真实组合根，两者职责不同。随机恢复与牌堆重洗牌分别约束通用随机 owner 和牌堆自己的状态，不能仅凭命名相似删除。

本次有 11 个新增 NUnit 用例实例；另外扩展原动作/Buff 恢复测试并修正三处失效 fixture（动作 track ID、整数 replay 诊断、Buff payload 的旧 schema/编号）。Buff 非法值测试改为从正式 provider 的合法产物只改 `buffId=-1`，避免先被版本/未注册 schema 挡住；仍断言 InvalidDocument 和 invalid value。旧动作测试继续严格检查同一 fixture 写入的明确数字 ID，没有放宽为任意事件存在。

## 红灯与缺验证

1. allocator：存活实体相等后续建，原世界下一个为 `(index=2,generation=2)`，恢复世界为 `(1,1)`。两条正式恢复入口都失败。只有完整 allocation lifecycle 状态契约落地后才能修复；本次未改生产。
2. history：容量 2 的生产 rollback 丢失 frame 0；receiveFrame 12 收到该命令，hook 返回失败后被当迟到命令丢弃，Connected 且恢复请求数 0。新的严格模拟契约断言失败。旧“无快照风暴”测试仍保留；需由根决定 failure/resync 协议，不能静默改变默认框架策略。
3. 分配组：Buff cleanup 24B、Buff projection 144B、component store copy 72B、action Tick 20864B、damage trace 196608B、command batch 24B 的既有零分配断言在此次 .NET 9 环境失败。未放宽断言；这是此执行环境的观测，不推断 Unity 运行时必然相同，也不将其当逻辑分歧。
4. 缺完整战术状态 capture/restore：牌堆随机、动量保留 token、StageRun/turn、command pump 的 pending/receipt/source sequence 尚无统一权威恢复契约。本次只能比较双跑与已有 queue 测试，不能制造 DTO 复制私有字段后冒充生产快照。
5. Unity 集成、资源/表现失败、真实 host/client 和平台范围验证均未执行。`unity-integration-plan.json` 给出精确 fixture 名称；需要根协调 Editor 后运行。未跨平台测试，不做跨平台位级一致声明。

生产源码、配置、场景、用户存档均未修改；无 commit / push。仅共享测试改动需要根整合，四个文件的主目录 SHA256 记录在任务工作区 `determinism-shared-files-baseline.json`。

## 主目录集成记录

已在主目录 `E:/Unity/WxFramework/WGameFramework` 合入四个测试文件和九个工具文件；manager owner、生产行为、场景与用户存档未改，无 commit/push。

主目录统一入口实际执行 core：205/205 通过，构建 4.70 秒，测试 0.331 秒。核对 523 个编译源文件，候选与主目录源码内容一致（两处仅编码/换行字节差异）。因此 restore 58/58、network 46/46、regressions 0/3、allocation 10/16 复用上表候选回执，没有因文件复制重复执行。已知仍有 9 项失败；本次没有新的 strict/GC 实测字节数。

各分类以完整 NUnit 用例名计数，两两不重叠：总共 328 个用例，候选回执为 319 通过、9 失败。前三组 309 是通过子集。`regressions` 是严格恢复/同步契约；`allocation` 是 GC 托管分配，和实体身份 allocator 不同。

Unity 单次验证请求在 domain reload 时连接断开，任务最终报告 timed_out，不能称自动验证工具成功。但其保存记录明确给出 refresh completed、compilation started/finished、domain reload observed 和编译 errors=[]，编译的是 MxFramework.Tests；另用只读原生查询确认 Console error=0、Editor 非 Play、未编译/导入、当前 TacticalCardDemoScene clean。现有未改 Fireball 测试有 UAC0005 analyzer warning，原生 warning 总数为 33。没有为此再次触发编译、运行 Unity 测试或 Player build。

新 Items save v3 与 cloth 接口不在纯逻辑入口的链接模块范围内；Unity 聚合程序集编译确认当前引用兼容。它们的业务语义验证由各自测试负责人处理，本套件不声称覆盖 Items v3 或 cloth 运行时。

Scene、GraphicsSettings、QualitySettings 及所记录 GameManager 的磁盘 hash 保持集成前值。完整战术恢复仍缺牌堆 RNG/身份与顺序、动量 token、StageRun/阶段/暂停/执行进度，以及 CommandPump pending/receipts/source sequence 的统一权威恢复边界；本次没有增加伪造的生产快照。