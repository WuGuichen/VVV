# UPM-P6 Embedded Package Cutover 任务规格

> Status: Current
>
> Task State: Complete
>
> Phase: UPM-P6 / R4 / L5
>
> Depends On: UPM-P5 Editor Portability (`Complete`)
>
> Hands Off To: UPM-P7 Consumer Evidence and Optional Matrix

## 1. 阶段身份与目标结果

本任务独立执行 accepted Embedded Package topology：把已收敛、已分类的 canonical package root 从 `Assets/Scripts/MxFramework` 切换到 `Packages/com.vincent.wgameframework`。完成后开发工程与外部 Consumer 使用同一种 package asset path，旧开发根和 locator fallback 不再存在。

P6 已完成：root cutover、3,204/3,204 纯移动身份回执、embedded 单一装载、locator fallback 删除、主工程 Unity/Repository 回归与 Local 四档 E1 均通过；提交并推送的候选 `c046158f569536cd8167471524f3a7ed4fa0adad` 也从新 Git subpath 完成 base `14/14` 和三个 optional profile `15/15` 的同 SHA PinnedGit E2。阶段现移交 P7。

P6 是纯 canonical-root cutover。它必须保持 package 内部全部资产的 `.meta`、GUID、asmdef name/identity、package ID、公共 API 与模块语义；源根目录自身的 sibling folder meta（当前为 `Assets/Scripts/MxFramework.meta`）不属于 package 内容，必须作为预期退役特例单独审计。不得混入 CharacterTest、Tests、Demo、Editor owner 迁移或无关代码重构。仅为目录美观把模块二次嵌套进 `Runtime/`/`Editor/` 也不属于本阶段。

活动文件数、asmdef 数、candidate、证据与 readiness gap 只读取 [`UPM_CURRENT_STATUS.md`](../../UPM_CURRENT_STATUS.md)，本文不冻结易变化数字。

## 2. 权威输入

- [`ADR-015`](../../Decisions/ADR-015-upm-embedded-package-topology.md)：Accepted 决策与后果；
- [`UPM_PHASE_TASKS.md`](../../UPM_PHASE_TASKS.md)：P6 边界和退出条件；
- [`UPM_OPTIMIZATION_ROADMAP.md`](../../UPM_OPTIMIZATION_ROADMAP.md)：R4 顺序与最低验证；
- [`UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md`](../../UPM_PACKAGE_SURFACE_LAYOUT_OPTIMIZATION.md)：L5 物理拓扑、locator 与迁移门禁；
- [`UPM_CURRENT_STATUS.md`](../../UPM_CURRENT_STATUS.md)：实施时唯一 Current 事实；
- [`UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md`](../../UPM_CONSUMER_BASELINE_EVIDENCE_OPTIMIZATION.md)：candidate freshness、Local/PinnedGit、mutation guard；
- [`MULTI_PROJECT_WORKFLOW.md`](../../MULTI_PROJECT_WORKFLOW.md)：Consumer `file:` 与 Git subpath 迁移；
- [`UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md`](../../UPM_OPTIONAL_INTEGRATION_OPTIMIZATION.md)：matching optional profile 和 source identity；
- 根/路径 `AGENTS.md`、`Assets/AGENTS.md`、`Docs/QUALITY_GATE.md`。

## 3. 进入条件

1. P1-P5 均为 `Complete`，且同一 P5 候选上的 surface owner 已冻结；
2. owner 已明确批准本次 cutover 使用的 package ID；沿用当前 identity 时记录批准，改变 identity 时必须先以新 ADR 取代 ADR-015，并据此修订目标 root、Consumer 路径和本任务规格；
3. CharacterTest、repository tests、非 sample Demo 与 project-only Editor 已在 package root 外；
4. package root 内只有已分类的 base runtime、portable Editor、approved optional adapter、package tests 和唯一 sample；
5. 已生成源 root 内部的完整 tracked file/`.meta`/GUID/asmdef/package tree hash baseline，并单独记录源 root sibling folder meta 的 path/GUID/content；
6. 已枚举旧 root 的全部源码、locator、manifest、runner、CI、fixture 和文档引用；
7. 已冻结与批准 package ID 一致的目标 root、local `file:` 新值、Git `?path=` 新值以及 rollback point；
8. 工作树无与大范围资产移动重叠的用户修改，Unity 无未保存场景或模态 reload；
9. P5 clean Local/PinnedGit 基线可复核，且源候选远端可达。

条件不足时不得执行目录移动；只读调查不改变 `Pending` 状态。

## 4. 明确交付物

1. canonical root `Packages/com.vincent.wgameframework/`，内容与冻结的 package-internal 清单一一对应，包含所有内部资产原 `.meta`；
2. 删除旧 canonical source root `Assets/Scripts/MxFramework/`，不保留影子副本、junction 或同步脚本；
3. package asset locator 只解析 `Packages/com.vincent.wgameframework/...`，删除 development fallback 及对应过渡测试；
4. 主工程 `Packages/manifest.json`/lockfile 所需 Embedded Package 状态，且 package 仅加载一次；
5. `Tools/Harness/check_upm_package_boundary.py`、Consumer runner/fixture、CI 和报告 identity 的新 local root 与 Git subpath；
6. `MULTI_PROJECT_WORKFLOW.md` 等 Current/Guide 文档中的 canonical 安装示例更新；历史文档可保留旧路径，但必须明确为历史而非可执行 Current 指令；
7. `Docs/UPM/Migrations/UPM_P6_EMBEDDED_PACKAGE_CUTOVER.md` 类型的 preview breaking migration note，给出现有 Consumer 的 manifest `file:` 和 Git subpath before/after、lock/Library 处理、验证与回退；
8. 机器可读移动审计：old relative path、new relative path、meta GUID、content hash、asmdef name/hash、结果；源 root sibling folder meta 单列为 `retired-root-folder-meta`，不生成 `Packages/com.vincent.wgameframework.meta`；
9. P6 cutover 报告：candidate SHA、前后 package tree hash/语义差异、旧路径剩余命中分类、Local/PinnedGit matrix、mutation guard 和回滚演练结果。

## 5. 实施切片及顺序

### S1：冻结 cutover manifest 和恢复点

- 记录 owner-approved package ID；若它不是当前 identity，停止本规格并先完成替代 ADR 与目标路径修订；
- 从 P5 完成候选生成 tracked package 文件、内部 `.meta`、GUID、asmdef identity、public API baseline 和 tree hash，并单列源 root sibling folder meta；
- 将每个旧路径引用标为 production、harness/CI、Current docs、historical docs 或 expected migration text；
- 保存可恢复的提交/分支点，不依赖未提交工作树作为回滚来源。

### S2：受控移动 canonical root

- 使用 Unity Editor、Unity MCP 或可审计的受控资产移动执行 root move；
- 文件相对 package root 的布局保持不变；
- 携带 package 内部所有 `.meta`，禁止复制后重建 GUID；删除旧 Assets root 时同步退役 `Assets/Scripts/MxFramework.meta`，不得创建无意义的 `Packages/com.vincent.wgameframework.meta`；
- 移动切片中不编辑业务代码、asmdef name/references 或 public API。

### S3：让开发工程只装载 Embedded Package

- 处理主工程 manifest/lock，使 Unity 将目标目录识别为 Embedded Package；
- 检查没有重复 assembly、重复 package 或旧 Assets 编译残留；
- locator 改为 canonical path only，并删除 `Assets/Scripts/MxFramework` fallback；
- 断言 package 内资源在开发工程和 Consumer 的 AssetDatabase path 相同。

### S4：切换消费与自动化入口

- local `file:` 指向 `/Packages/com.vincent.wgameframework`；
- Git dependency 的 `?path=` 指向 `/Packages/com.vincent.wgameframework`，revision 仍为完整 immutable SHA；
- 更新 E0、runner、fixture、CI、报告字段与 mutation allowlist；
- 不猜测尚未由 owner 批准的 canonical repository URL 或许可证；package ID 只使用进入条件已批准的 cutover identity，不在本切片重新决策。

### S5：发布 preview migration note

- 给出 local 和 pinned Git before/after JSON；
- 说明先提交/获取包含新 root 的候选，再修改 Consumer manifest；
- 说明何时清理 lock/Library、如何确认 resolved SHA/path、如何回退；
- 明确这是 preview breaking path change，不是 API/语义 breaking change。

### S6：同一候选验证并移交

- 完成第 8 节全部验证；
- 搜索并分类全部旧 root 剩余命中；production/Current executable 命中必须为 0；
- 退出证据齐全后才更新 P6 与 Current 状态，并把 P7 标为下一阶段可准备项。

## 6. 范围与非目标

范围：单一 root move、locator fallback 删除、开发工程 Embedded Package 识别、local/Git subpath、runner/CI/fixture/Current docs、migration note 和完整身份审计。

非目标：

- 不再次迁移 CharacterTest、Tests、Demo 或 Editor owner；
- 不拆 Git repository，不拆多个 UPM package；
- 不在 cutover 中更改已冻结的 package ID、assembly name、public API、schema 或模块语义；
- 不把现有模块重新排进新的 `Runtime/`/`Editor/` 层级；
- 不顺带清理命名、namespace、依赖或技术债；
- 不执行 P7 report schema/matrix 重构，不执行 P8 metadata/release readiness；
- 不把 moving `main` 变成 release channel。

## 7. 关键不变量

- old/new 每个 package-internal Unity 资产的 `.meta` 与 GUID 一致；源 root sibling folder meta 是唯一预先登记的退役特例；
- asmdef `name`、references、define constraints 和身份不因移动变化；
- package ID 与 P6 进入前的 owner 决策一致；若 owner 要求替代 identity，本任务在移动前阻断并等待新 ADR/规格，不允许移动后补改；
- package-relative 文件集合和内容在排除明确 path-only 配置后保持等价；
- package assembly 不产生新的 project 反向依赖；
- 开发工程与 Consumer 均使用 `Packages/com.vincent.wgameframework/...`；
- cutover 完成后旧 root、影子副本、development fallback 和 Current 旧安装示例为 0；
- Local 与 PinnedGit 证据必须来自同一 candidate，dirty 或历史结果不可晋升；
- rollback 恢复旧 root 时也必须恢复 locator、runner、manifest 和文档，禁止半切换状态。

## 8. 逐层验证

1. **前后身份审计**：tracked relative file set、内部 `.meta`/GUID、asmdef name/hash、package manifest identity、public API baseline 与 package tree 语义对比；源 root sibling folder meta 必须只出现为已登记退役项，其余差异逐项解释。
2. **静态/E0**：对新 root 运行严格 surface/boundary；禁止路径、optional reverse closure、initializer 清单、package hygiene 通过；工具不再读取旧 root。
3. **旧路径清零**：production source、locator、manifest、runner、CI、fixture 和 Current executable docs 的旧 root 命中为 0；migration/historical 命中单独 allowlist。
4. **主工程 Unity**：refresh/compile、domain reload、Editor ready、Console error 为 0；无重复 assembly/package；受影响 EditMode 和 package asset locator tests 通过。
5. **Package surface**：package-owned Editor 资源、Tests 和唯一 Sample 均可通过 canonical path 定位；P5 absent-capability/no-write smoke 不回归。
6. **Local Consumer**：空 Library、使用新 `file:` root；base 与受影响 matching optional profiles通过，package/Consumer mutation guard 无未解释变化。
7. **PinnedGit Consumer**：提交并推送完整候选后，以新 Git subpath 和完整 SHA运行 base、matching optional 以及 package tests/sample smoke；requested/resolved SHA 一致。
8. **迁移演练**：至少一个已有 fixture 按 migration note 从旧引用升级到新引用并可回退；记录 lock/path/package version 和 asset resolution。
9. **差异质量**：`git diff --check`、报告 schema 校验、report hashes 和 candidate/tree identity 完整。

## 9. 失败与回滚

- GUID、asmdef identity、public API 或 package-relative 内容出现未解释变化时立即停止；恢复到 S1 提交点，不在移动后“修补新身份”。
- Unity 出现重复 assembly/package 时同时检查旧 Assets 残留、manifest/lock 和 import cache；不得通过删除 asmdef 或改 assembly name绕过。
- Local 成功但 PinnedGit 解析失败时，保持 P6 未完成；核对候选是否远端可达、`?path=` 顺序和完整 SHA，不把 Local 当 immutable 证据。
- migration note 无法让已有 fixture 升级/回退时，回滚消费入口切换或补足说明后重演练。
- rollback 必须作为一个完整反向切片：恢复 old root、全部内部 `.meta` 及原 sibling folder meta、旧 locator fallback、旧 runner/CI/manifest/docs，并重跑最小 E0 + Unity compile；禁止只恢复目录。
- 失败报告保留为诊断证据，但不能与修复候选拼接。

## 10. 退出证据与 P7 移交

P6 只有在以下事实同时成立时才能 `Complete`：canonical root 与 owner-approved package ID 一致；开发工程和干净 Consumer 都从该 Embedded Package root 加载；旧 Assets root 与 locator fallback 已删除；GUID/asmdef/API/语义审计无未解释变化；主工程 Unity、E0、Local/PinnedGit base、matching optional、mutation guard 和迁移演练均在同一候选上通过；migration note 可执行且可回退。

移交 P7 时提供：完整 candidate SHA、package tree hash、canonical root/subpath、harness/fixture identity、Local/PinnedGit 原始报告与哈希、package/Consumer mutation allowlist、唯一 sample smoke、P5 absent-capability/no-write 结果、migration receipt 以及旧路径剩余命中的分类清单。P7 基于这些事实升级证据 schema/matrix，不得重新打开 root 或 owner 迁移。
