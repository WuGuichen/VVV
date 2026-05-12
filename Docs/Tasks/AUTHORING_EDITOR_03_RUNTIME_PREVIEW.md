# 子需求 03：游戏运行时实时预览

> **状态**: ✅ 已完成（详情见 `Docs/CAPABILITIES.md`）

## 目标

让外部编辑器不模拟战斗，而是把临时 Patch 交给真实游戏运行时预览器，由游戏热加载并回传真实结果。

## 核心流程

```text
外部编辑器保存草稿
  -> Authoring Core 生成临时 Patch
  -> 通过本机 JSON-RPC 通道发送给游戏预览器
  -> 游戏热加载 Patch
  -> 创建测试场景 / 测试实体
  -> 应用当前 Buff
  -> 回传 Buff 状态、属性变化、伤害、日志和性能
```

## 传输层

首版定为本机 WebSocket + JSON-RPC 2.0：

- 游戏预览器只监听 `127.0.0.1`。
- 端口由游戏预览器自动选择（建议在 `49152~65535` 范围中扫描可用端口）。
- 连接信息写入临时**连接描述文件**，外部编辑器和 CLI 自动读取，不要求玩家手填端口。
- 消息 payload 复用 Authoring Core 的 `PatchDocument`、`ValidationReport` 和本子需求定义的 `RuntimePreviewResult`。
- 连接失败时，外部编辑器仍保留离线校验和合并预览能力。

## 连接描述文件

文件路径（按平台）：

| 平台 | 路径 |
| --- | --- |
| Windows | `%LOCALAPPDATA%\MxFramework\AuthoringPreview\preview.json` |
| macOS | `~/Library/Application Support/MxFramework/AuthoringPreview/preview.json` |
| Linux | `$XDG_RUNTIME_DIR/mxframework/authoring-preview/preview.json`，缺省退化到 `~/.cache/mxframework/authoring-preview/preview.json` |

文件结构：

```json
{
  "schemaVersion": "1.0",
  "endpoint": "ws://127.0.0.1:54123/preview",
  "port": 54123,
  "token": "随机 32 字节 base64",
  "processId": 12345,
  "gameVersion": "0.3.1",
  "startedAt": "2026-05-06T10:11:12Z",
  "capabilities": ["preview.loadPatch", "preview.applyBuff", "preview.reset", "preview.getSnapshot", "preview.getLogs"]
}
```

硬性要求：

- 文件由游戏预览器进程在启动 RPC 监听后**原子写入**（先写 `preview.json.tmp` 再 rename）。
- 进程退出时（包括 crash）必须尽量删除该文件；无法保证时由读取端按 `processId` 是否存活兜底判定。
- `token` 是可选客户端校验码：客户端连接后必须以 `preview.handshake { token }` 第一帧发送；不匹配立即断开。
- 同一机器最多保留一个有效描述文件；若读取到的 `processId` 已不存在，读取端应忽略并报"未连接"。

## JSON-RPC 2.0 方法

所有方法走单一 WebSocket 通道，编码 UTF-8 JSON。每条消息严格遵循 JSON-RPC 2.0：`{ jsonrpc, id, method, params }` / `{ jsonrpc, id, result }` / `{ jsonrpc, id, error }`。

通用错误码：

| code | 含义 |
| --- | --- |
| -32600 | 非法请求（JSON-RPC 协议错误）|
| -32601 | 未知方法 |
| -32602 | 参数非法 |
| -32603 | 内部错误 |
| 1001 | 未握手 |
| 1002 | token 不匹配 |
| 2001 | Patch 解析失败 |
| 2002 | Patch 加载失败（运行时拒绝）|
| 2003 | 应用 Buff 失败 |
| 2004 | 当前不在预览模式 |

### `preview.handshake`

客户端连接后第一条必须发送。

```json
// request
{ "jsonrpc": "2.0", "id": 1, "method": "preview.handshake",
  "params": { "clientName": "MxAuthoringEditor", "clientVersion": "0.2.0", "token": "..." } }

// response
{ "jsonrpc": "2.0", "id": 1,
  "result": { "serverName": "MxRuntimePreview", "gameVersion": "0.3.1",
              "schemaVersion": "1.0", "capabilities": ["preview.loadPatch", ...] } }
```

未握手前调用其它方法返回 `1001`。

### `preview.loadPatch`

把当前包合并后的临时 Patch 加载到运行时配置层。多次调用时新内容覆盖前次。

```json
// request
{ "method": "preview.loadPatch",
  "params": {
    "packageId": "sample.buff.preview",
    "kind": "Preview",
    "patches": [ /* PatchDocument[] */ ],
    "baseLayers": [],
    "patchLayers": [],
    "schemaVersion": "1.0",
    "discardPrevious": true
  } }

// result
{ "result": {
    "loadedPatchIds": ["sample.buff.preview"],
    "rejectedPatches": [],
    "mergeWarnings": [],
    "elapsedMs": 42
  } }
```

异常：

- 解析失败 `2001`，`error.data` 含具体 `ValidationIssue[]`。
- 运行时拒绝（如包含未实现 BuffType）`2002`。

### `preview.applyBuff`

在测试角色身上应用一条 Buff，可重复调用。

```json
// request
{ "method": "preview.applyBuff",
  "params": {
    "buffId": "100001",
    "casterId": "TestCaster",
    "targetId": "TestTarget",
    "stack": 1,
    "durationOverrideMs": null,
    "waitTicks": 60
  } }

// result -> RuntimePreviewResult
```

`waitTicks` 表示游戏端在 apply 后再 tick 多少帧再回结果（默认 0，即立即）。

异常：`2003`、`2004`。

### `preview.reset`

清理预览角色 / 全部 Buff / 已加载临时 Patch；不影响 Base。

```json
{ "method": "preview.reset", "params": { "reloadBase": false } }
{ "result": { "elapsedMs": 18 } }
```

### `preview.getSnapshot`

不应用新 Buff，只取当前预览状态。

```json
{ "method": "preview.getSnapshot", "params": { "targetId": "TestTarget" } }
{ "result": /* RuntimePreviewResult */ }
```

### `preview.getLogs`

取自上次 `preview.reset` 之后累计的日志，可分页。

```json
{ "method": "preview.getLogs", "params": { "afterSeq": 0, "max": 200 } }
{ "result": { "logs": [ /* LogEntry[] */ ], "lastSeq": 137 } }
```

## RuntimePreviewResult 结构

```json
{
  "requestId": "client-uuid",
  "success": true,
  "loadedPatchIds": ["sample.buff.preview"],
  "appliedBuffId": "100001",
  "buffSnapshots": [
    { "buffId": "100001", "ownerId": "TestTarget", "stack": 1,
      "remainingMs": 4800, "totalMs": 5000, "casterId": "TestCaster",
      "addedAt": "2026-05-06T10:11:12.345Z" }
  ],
  "attributeChanges": [
    { "ownerId": "TestTarget", "attribute": "Hp", "before": 1000, "after": 940, "deltaSource": "100001" }
  ],
  "damageTicks": [
    { "buffId": "100001", "tickIndex": 0, "amount": 60, "damageType": "Magic", "elementType": "Fire" }
  ],
  "statusChanges": [
    { "ownerId": "TestTarget", "status": "Burning", "applied": true }
  ],
  "logs": [ { "seq": 12, "level": "info", "message": "Buff 100001 applied", "atMs": 42 } ],
  "errors": [],
  "performance": { "loadMs": 42, "applyMs": 7, "tickCount": 60, "totalMs": 1024 }
}
```

字段约束：

- 时间戳统一 UTC ISO-8601。
- 数值字段全部为 `number`（JSON 双精度）；超长整数走字符串。
- `errors[].code` 复用上面的错误码表，加上业务级 `5000+` 区间。
- 单个 result body 软上限 1 MB；超过时游戏端必须截断 `logs` 并回传 `truncated=true`。

## 游戏端能力

必须支持：

- 启动 / 退出预览模式（独立场景或现役测试沙盒）。
- 接收临时 Patch 并合并到运行时配置层（不修改 Base 资产）。
- 选择测试角色和目标（默认提供至少一对 dummy）。
- 应用指定 Buff，设置层数、持续时间、来源对象。
- 清理预览状态（不影响 Base）。
- 回传结构化结果（按 `RuntimePreviewResult`）。
- 记录最近 N 条结构化日志，供 `preview.getLogs` 分页拉取。

## 外部编辑器展示

必须展示：

- 连接状态（已连接 / 未连接 / 握手失败 / token 不匹配）。
- Patch 是否加载成功（按 `loadedPatchIds`）。
- Buff 是否成功应用、当前层数、剩余时间。
- 属性变化、伤害 tick、状态变化（来自 `RuntimePreviewResult`）。
- 错误日志（`errors[]` 含可读 message）。
- 性能摘要（loadMs / applyMs / totalMs）。

## 不做

- 不在外部编辑器里重写战斗系统。
- 不要求玩家安装 Unity。
- 不把预览协议和 Unity Editor 绑定。
- 不把游戏的真实业务对象暴露给外部编辑器（只回传结构化字段）。

## 验收标准

- 修改 Buff 字段后可以重新生成临时 Patch 并预览。
- 游戏端能回传结构化结果，不只依赖 Console 文本。
- 预览失败不会破坏 Base 数据或当前 Mod 包。
- 预览结果可以进入 AI 上下文（`runtimePreviewSummary`）。
- 外部编辑器不需要用户手动配置端口即可连接本机预览器。
- 加载临时 Patch 到收到首个结构化结果目标 <= 3 秒。
- token 不匹配的客户端被立即断开，错误码 1002。
- 预览器进程崩溃后，连接描述文件最迟在下一次启动时被覆盖或忽略。

## 实施分阶段

| 阶段 | 目标 | 关键产物 |
| --- | --- | --- |
| 03.1 | 协议 Spec Ready ✅ | 本文件 + EPIC §预览通信协议 引用本文件 |
| 03.2 | Authoring 端客户端实现 ✅ | `MxFramework.Authoring.Preview`（DTO/locator） + `MxFramework.Authoring.Preview.NetClient`（WebSocket Client）+ Mock Server + CLI `preview ping/load/apply/reset/snapshot/logs` |
| 03.3 ✅ | 游戏端 PreviewServer 雏形 | Unity 内独立 asmdef `MxFramework.Preview.Runtime` / `.Editor`，`DummyPreviewWorld` + `MemoryBuffPatchLoader` + `PreviewRpcServer` + `MxPreviewBootstrap` + Editor 启动菜单 |
| 03.4 ✅ | 编辑器 UI 接入 | `web/app.js` 增加预览面板，`/api/preview/status` 与 `/api/preview/run` 转发到本机 PreviewServer |
| 03.5 | In Progress / Partial Verified | 真实闭环拆分见 `AUTHORING_EDITOR_10_RUNTIME_PREVIEW_03_5_REAL_GAMEPLAY_LOOP.md`；03.5A Runtime Adapter 已落地，Runtime Patch resolver 主路径已有实现证据；继续收口 result mapping、UI status 和完整验收指标 |

## 依赖

- 子需求 01 Authoring Core / CLI（已 Tool Verified）。
- 子需求 02 Buff 外部编辑器 MVP（已 UI Integrated partial）。
- 游戏侧运行时动态配置加载能力（待项目侧补齐 `IDynamicConfigProvider` 接口实现）。

## 状态

`UI Integrated + 03.5 partial` —— 03.2 Authoring 客户端、03.3 Unity PreviewServer 雏形和 03.4 外部编辑器接入已落地。03.5A Runtime Adapter 已统一 `applyBuff / tick / snapshot / reset` 边界，Runtime Patch v1 loader / merger / factory 主路径已有实现证据；后续继续按 `AUTHORING_EDITOR_10_RUNTIME_PREVIEW_03_5_REAL_GAMEPLAY_LOOP.md` 收口 result mapping、UI status、`GameplayDiagnosticSnapshot` 映射和完整验收记录。
