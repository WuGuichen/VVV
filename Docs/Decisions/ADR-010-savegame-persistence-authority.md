# ADR-010: SaveGame Persistence Authority, Envelope And Publication Boundary

> Status: ADR

Date: 2026-07-19

Decision Status: Accepted

## Context

MxFramework 已实现 `RuntimeSaveState`、participant registry/coordinator、Runtime schema migration、JSON roundtrip，以及只在最近一次完整成功 Tick 与全部 participant quiescent 后捕获的 `RuntimeCheckpointBarrier`。这些能力定义了“保存什么”和“何时可捕获”，但不拥有 profile/slot、durable bytes、generation、checksum、crash-safe commit、last-known-good 或 live session publication。

现有 `MxSettingsJsonFileStore` 是用户偏好专用的同步 JSON 文件实现。它允许文件缺失或损坏时回退默认 profile，并使用直接覆盖写入；这些语义不适用于 Gameplay SaveGame。Gameplay Trigger、Items、Story、VFX 等 participant 各自拥有 module custom state 和 migration，也不能由通用持久化层复制或解析。

如果不先冻结 authority、wire format、limits 与 publication 边界，首个本地文件实现会把以下偶然选择固化为长期兼容负担：

- 把任意 participant 的根 header 当成 build/config/resource authority；
- 把 checksum、Runtime schema 与 envelope schema 混为一个版本；
- 使用 wall-clock 选择“最新”存档；
- 两个 writer 静默 last-write-wins；
- 将 participant-local restore 原子性误报为整个 Runtime 原子恢复；
- 让 Trigger payload schema、pending detail 或 JSON 结构泄漏进 Persistence public API；
- 在不具备原子 rename/durability 证据的平台上直接覆盖 current 文件并报告成功。

本 ADR 接受 `Docs/SAVEGAME_PERSISTENCE_DESIGN.md` 的总体边界，并冻结 SaveGame v1 可以开始实现所需的具体决策。

## Decision

### 1. Authority And Assembly Direction

SaveGame 由以下 owner 分层拥有：

| 数据或行为 | 唯一 owner |
| --- | --- |
| Runtime frame、entities、module/custom state、Runtime schema/migration | `MxFramework.Runtime` 与各 participant owner |
| end-of-Tick completion 与 quiescence | `RuntimeCheckpointBarrier` |
| profile、slot、generation、envelope、integrity、recovery contract | `MxFramework.Persistence` |
| durable bytes 与 provider-specific atomic publication | `ISaveGameStore` provider |
| writable root 与宿主 storage facts | Platform/provider adapter |
| save/autosave/load policy、candidate session 与 live publication | Application/project composition root |
| user preferences | `MxFramework.Settings`，不进入 Gameplay SaveGame |

依赖方向冻结为：

```text
MxFramework.Persistence                         BCL only, noEngine
MxFramework.Persistence + MxFramework.Runtime
  -> MxFramework.Persistence.RuntimeBridge      noEngine bridge
MxFramework.Persistence + MxFramework.Platform
  -> MxFramework.Persistence.FileSystem         noEngine local provider
Application/project composition root
  -> Persistence.RuntimeBridge + provider adapters
```

`MxFramework.Persistence` 不引用 Runtime、Platform、Settings、Gameplay、Gameplay.Triggers、Resources、UI 或 Unity。Runtime 与 Trigger 不反向引用 Persistence。首个 Core PR 使用独立 `MxFramework.Persistence` asmdef，`references=[]`、`noEngineReferences=true`、`allowUnsafeCode=false`。

### 2. Stable Identity And Generation

v1 使用以下强类型 identity：

- `SaveGameProfileId`
- `SaveGameSlotId`
- `SaveGameGeneration`
- `SaveGameOperationId`

Profile、slot 与 operation id 的 canonical value 规则冻结为：

- 长度为 1..64 个 ASCII 字符；
- 第一个字符必须是 `A-Z`、`a-z` 或 `0-9`；
- 后续字符只允许 ASCII 字母、数字、`.`、`_`、`-`；
- 使用 ordinal、case-sensitive equality 和 ordering；
- 不 trim、不 case-fold、不做 Unicode normalization；
- default struct、空白、`.`、`..`、separator、rooted path、控制字符和超长输入无效；
- display title、平台账号名和本地化文本不作为 identity。

FileSystem adapter 即使收到 path-safe canonical ID，仍必须把 ID 当作逻辑值编码为自己的 path segment，并在解析后的绝对路径上验证 root containment；公共 API 永不接受任意文件路径作为 slot identity。

`SaveGameGeneration` 的底层值为 `ulong`：

- `0` 表示 slot 从未有过 published generation；不是可读取 generation；
- 第一次成功 commit 产生 generation `1`；
- 每次成功 commit 或 tombstone delete 使用 `checked(head + 1)`；
- `ulong.MaxValue` 的 head 不能继续写入，返回 `GenerationExhausted`；
- corrupt head、recovery、delete、重新创建同名 slot 都不得复用或倒退 generation；
- compare-and-swap precondition 使用 observed head generation，不能使用时间戳或“最高仍可读 generation”。

Header JSON 中 generation 使用无符号十进制字符串，避免经过 JavaScript 或通用 JSON number 后丢失 64-bit 精度。FileSystem generation 文件名使用固定 20 位十进制，左侧补零。

### 3. SaveGame v1 Hard Limits

以下是 v1 跨 provider hard maximum。provider 或项目可以声明更小限制；effective limit 取所有参与方的最小值。扩大 hard maximum 需要新的 Accepted ADR，并在影响 wire compatibility 时提升 envelope version。

| Item | v1 hard maximum |
| --- | ---: |
| Profile / slot / operation id | 64 ASCII chars |
| Format、codec、transform、build/catalog/content id | 128 UTF-8 bytes each |
| Compatibility tags | 32 entries |
| Compatibility tag key / value | 64 / 128 UTF-8 bytes |
| Display metadata | 32 entries |
| Display metadata key / value | 64 / 256 UTF-8 bytes |
| Canonical header bytes | 64 KiB |
| Stored payload bytes | 64 MiB |
| Uncompressed payload bytes | 64 MiB in v1 |
| Slots returned per profile list | 256 |
| Concurrent active operations per store instance | 8 |
| JSON nesting depth | 16 |
| Transform chain length | 0 in v1 |

长度按 strict UTF-8 encoded byte count 计算，不按 UTF-16 `string.Length`。所有 length、count、checked addition 和总 envelope length 必须在分配 payload/header buffer 前验证。超限返回结构化 `LimitExceeded`；不得先读入或解压任意大内容再失败。

### 4. Envelope v1 Wire Codec

SaveGame v1 使用 deterministic framed binary envelope，payload 本身仍是 codec-owned opaque bytes。固定布局为：

```text
8 bytes   Magic = ASCII "MXSVGM01"
2 bytes   EnvelopeVersion = 1, unsigned little-endian
2 bytes   Flags, unsigned little-endian (`0` save, bit `0` tombstone)
4 bytes   HeaderLength, unsigned little-endian
8 bytes   PayloadLength, unsigned little-endian
N bytes   Canonical UTF-8 JSON header, no BOM
M bytes   Stored payload
32 bytes  EnvelopeSha256
```

Rules:

- 总长度必须精确等于 fixed prefix + `HeaderLength` + `PayloadLength` + 32；trailing bytes 与 truncation 均失败。
- v1 只定义 `Tombstone = 0x0001`；未知 bit 返回 `UnsupportedEnvelopeFeature`。`Flags=0` 必须对应 `recordKind="save"`，`Flags=1` 必须对应 `recordKind="tombstone"`，不一致返回 `InvalidEnvelope`。
- Header JSON 必须是单一 object，使用 RFC 8259 strict UTF-8；duplicate property、unknown required shape、non-finite number、invalid escape、invalid date 或 trailing token 均失败。
- codec writer 使用下述固定 property order；tag 和 metadata key 按 ordinal 排序且 key 唯一；不依赖 `Dictionary` 枚举顺序。
- canonical writer 对 quote 使用 `\"`、对 reverse-solidus 使用 `\\`，对全部 U+0000..U+001F（包括 tab/newline）统一使用六个 ASCII 字符的 lowercase `\u00xx`；不使用 `\b`、`\f`、`\n`、`\r`、`\t` 短转义。其他 Unicode scalar 直接编码为最短合法 UTF-8，不输出 BOM、不做 Unicode normalization；unpaired surrogate 和非法 UTF-8 一律拒绝。
- canonical JSON 不包含 string 外 whitespace。object/array delimiter、colon 与 comma 紧邻相邻 token。整数只允许 ASCII decimal digits；除单个 `0` 外禁止前导零，禁止 `+`/`-`、fraction 和 exponent。
- UTC 时间格式固定为 `yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'`。Local 或 Unspecified 输入在 checkpoint capture 前结构化拒绝，codec 不自行转换。
- generation 与 Runtime frame 使用 decimal string：generation 范围 `1..ulong.MaxValue`，Runtime frame 范围 `0..long.MaxValue`，两者都禁止前导零。Save record 的 `runtimeSchemaVersion` 是 `0..int.MaxValue` 的 JSON integer；`0` 是 Runtime 当前合法的初始 schema，不表示缺失。length 是受 v1 hard limits 约束的 JSON integer。数组 count 由数组长度表达，不另写冗余字段。
- envelope `FormatId` 固定为 `mx.save-game`；payload `CodecId` 的首个 Runtime bridge 值固定为 `runtime-save-state-json`。
- Runtime payload codec、Runtime schema 与 envelope codec/version 独立。Persistence Core 不反序列化 `RuntimeSaveState`。

v1 header schema 精确冻结如下。属性名区分大小写；所有列出的属性都是 required，禁止未知属性。除 tombstone 的 `payload` 外不允许 `null`。空 array 编码为 `[]`，不得省略。

```json
{
  "format": "mx.save-game",
  "profile": "profile-id",
  "slot": "slot-id",
  "generation": "1",
  "recordKind": "save",
  "createdAtUtc": "2026-07-19T00:00:00.0000000Z",
  "updatedAtUtc": "2026-07-19T00:00:00.0000000Z",
  "build": {
    "frameworkVersion": "1",
    "buildId": "build-id",
    "configVersion": "config-id",
    "resourceCatalogVersion": "catalog-id",
    "contentSetId": ""
  },
  "compatibilityTags": [
    { "key": "tag", "value": "value" }
  ],
  "payload": {
    "codecId": "runtime-save-state-json",
    "runtimeSchemaVersion": 0,
    "runtimeFrame": "42",
    "uncompressedLength": 123,
    "storedLength": 123,
    "transformIds": [],
    "sha256": "64-lowercase-hex"
  },
  "displayMetadata": [
    { "key": "title", "value": "Save title" }
  ]
}
```

The textual layout above is illustrative whitespace only. Canonical bytes contain no insignificant whitespace. Top-level property order and nested object property order are exactly the displayed order. Each tag/metadata entry is exactly `key` then `value`; arrays are sorted by `key` using ordinal comparison. Duplicate keys fail. Strings are required; `contentSetId` may be empty when the project has no content-set authority, while identity fields, all other build fields, and save `codecId` must be non-empty.

For a save record, `recordKind` is `save`, `payload` is the displayed object, frame `PayloadLength` is `1..64 MiB`, `storedLength` mirrors it, and `uncompressedLength == storedLength`. For a tombstone, `recordKind` is `tombstone`, `payload` is exactly `null`, frame `PayloadLength` is `0`, no payload bytes follow the header, and both tag/metadata arrays are empty. Tombstone `createdAtUtc` and `updatedAtUtc` are equal to the delete request UTC timestamp. The build object remains required so diagnostics can identify which build published the deletion.

codec 实现必须提交至少一个 golden byte vector。依赖库升级不得静默改变 canonical bytes；任何有意 wire change 必须增加 vector、migration 和 version decision。

### 5. Integrity And Checksum Coverage

v1 使用两个 SHA-256 值：

1. Save header 内的 `payload.sha256` 是 stored payload bytes 的 SHA-256，以 64 个 lowercase hexadecimal ASCII 字符表示。Tombstone 没有 payload object，也不携带该字段。
2. 尾部 `EnvelopeSha256` 覆盖 checksum 字段之前的全部原始 bytes，即 frame prefix + canonical header + payload；不包含 checksum 自身。Tombstone 的 payload 长度为零，因此覆盖范围精确为 prefix + canonical header。

验证顺序为：

1. 验证最小长度、magic、version、flags 和 checked lengths；
2. 验证 exact total length；
3. 验证 `EnvelopeSha256`；
4. strict parse bounded header；
5. 验证 header/frame/record-kind mirror fields；save 再验证 `payload.sha256`；
6. 之后才允许交给 transform/payload codec。

这使 profile、slot、generation、compatibility、display metadata、payload descriptor 和 payload bytes 都受 accidental-corruption 检测。Display metadata 不进入 Runtime authority hash，但仍受 envelope integrity 保护。

SHA-256 只声明完整性检测，不声明 authenticity、anti-cheat、signature 或 confidentiality。密钥、签名和加密不属于 v1。

### 6. Transform Policy

SaveGame v1 只接受 identity payload：

- `TransformIds` 必须为空；
- `StoredLength == UncompressedLength`；
- 任意非空 transform chain 返回 `UnsupportedTransform`；
- reader 不猜测、不跳过 unknown transform，也不因 transform 不支持而自动读取 older generation。

compression、encryption 或 signing 必须由后续 Accepted ADR 冻结 transform id、顺序、limits、bomb protection、key ownership 和 golden vectors。它们不得改变 Runtime authority schema。

### 7. Retention, Selection And Recovery

本地 v1 provider 的逻辑 retention target 为每个 slot 两个 published generation record；save 与 tombstone 使用同一 `generation-{20-digit}.mxsave` 命名和 envelope integrity：

- 当前 head；
- head 之前最近的一个完整、integrity-valid generation，作为 last-known-good candidate。

发布成功后更旧 generation 由 provider best-effort 清理。一个或多个清理失败通过成功的 Commit/Delete result 聚合为 singular typed `SaveGameWarning.RetentionCleanupFailed`，不回滚已经发布的 generation；额外残留文件不扩大 API 的 retention 保证。该 warning 使用 `Retention` phase 与逻辑 `retention` path，不含 message、exception 或物理路径；逐项 cleanup exception 只保留在 provider-local diagnostics。`ExactGeneration` 对已清理 generation 可以返回 `GenerationNotFound`。

`Current` 的 primary 永远是数值最高的已发布 generation，即使其内容损坏。若最高完整 record 是 tombstone，`Current` 返回 `SlotDeleted` 且绝不 recovery 到更旧 save。只有最高 record 本身发生 truncation、malformed framing/header、length 或 checksum failure，request 明确使用 `PrimaryThenLastKnownGood`，且前一 retained record 是 save 时，Store 才能选择该 generation。

以下情况不得自动回退：

- unsupported envelope version/flag/transform；
- missing envelope migration；
- Runtime schema/config/resource/content incompatibility；
- participant restore failure；
- exact-generation read。

Recovery 必须返回 original generation、selected generation 和 `RecoverySource`。成功读取 backup 不自动覆盖或删除损坏 primary。List 默认排除 head 为 tombstone 的 slot；diagnostic list 可以显式包含其 generation 与 deleted state。`ExactGeneration` 读取 tombstone 返回 `SlotDeleted`，读取仍保留的旧 save 可以成功，但不得改变 Current；恢复旧 save 必须通过新的 CAS commit 形成更高 generation。删除只发布 tombstone，不直接移除所有 generation 文件。

### 8. Three Independent Atomicity Boundaries

本 ADR 冻结三种不同原子性，任何文档、API 或 diagnostics 都必须明确指出所指层级：

1. **Checkpoint capture atomicity**：`RuntimeCheckpointBarrier` 失败时不发布 partial `RuntimeSaveState`，也不创建 write plan。
2. **Storage commit atomicity**：provider 只有在完整 staging、flush/capability barrier、重读校验和 atomic publish 成功后才更新 head；失败保留旧 head。
3. **Runtime restore atomicity**：participant 可以实现自己的 validate-plan-commit，但现有 coordinator 没有跨 participant rollback，因此不声明全局原子 restore。

FileSystem v1 使用同 slot 目录的 immutable generation journal、create-new staging 与 same-volume atomic rename。若目标文件系统无法证明 create-new + reliable rename，返回 `AtomicCommitUnsupported`；不得退化为直接覆盖 current 并报告成功。`Application.persistentDataPath` 只提供 logical root，不构成原子性或 durability 证明。

### 9. Compare-And-Swap And Cancellation

每个 Commit/Delete request 必须携带 caller observed `ExpectedHeadGeneration` 与严格的 `NextGeneration=Expected+1`。provider 在 publish 前重新比较实际 head：

- 匹配时最多一个 writer 可以成功；
- 不匹配返回 `GenerationConflict`；
- 禁止 silent last-write-wins；
- wall-clock、file mtime 和 display timestamp 不参与冲突判断。

同一 profile/slot 同时最多一个 active writer lease。不同 slot 是否并行由 provider capability 决定。

取消只在 publication 前协作式生效并清理 staging。operation 仅在 publication boundary 前可以报告 `CanCancel=true`；Succeeded/Failed/Cancelled/TimedOut 终态固定为 `IsDone=true`、`CanCancel=false`。publication 或不可取消 cleanup/drain 进行中允许 `Running && !CanCancel`，调用方继续 poll 到终态。atomic publish 已完成后 operation 必须返回 success，即使 cancellation 随后到达；不能把已经发布的 generation 伪报为 Cancelled。Core 不提供默认 timeout、wall-clock timer 或隐式 deadline；只有显式 provider/上层 timeout policy 可以返回 TimedOut 与 `Timeout` error。timeout、quota、permission、disk full 与 provider unavailable 均不得改变旧 head。

### 10. Candidate Restore And Publication

Production load 固定使用 prepare -> candidate restore -> validate -> publish：

```text
read/select envelope
  -> integrity/envelope migration
  -> compatibility check
  -> Runtime JSON parse/migration
  -> build unpublished candidate session
  -> restore participants in candidate
  -> validate candidate
  -> one composition-root publication commit
  -> first live Tick = saved frame F + 1
```

`PrepareLoad` 不调用 restorer。`RestorePrepared` 只接受在同一临界区完成未发布状态校验与排他占用的 candidate restore lease；不得用公开 `IsPublished` 预检后再恢复的 TOCTOU 形态。lease、candidate coordinator 与 prepared plan 必须绑定 reference-equal 的 per-load sibling profile；未 complete 即释放 lease 必须永久 poison candidate。恢复成功结果必须保留实际取得的 candidate `TargetId` 与 `F -> F+1`，取得 lease 后的失败详情也必须保留同一 `TargetId`，以便审计目标身份。任意 participant restore 或 candidate validation 失败时销毁整个 candidate；旧 live session、resource plan、scene 和 frame driver 保持不变。

一次 publication 必须是不可失败的单 owner/aggregate state swap，或提供等价证明。所有可能失败的准备与 handoff 必须在 swap 前完成；swap 后旧 candidate/scene/resource cleanup 失败只能作为 warning，不能把已发布的新 session 报告为失败。

没有 candidate-session transaction 的实现只能声明 durable capture/prepare-load，不能声明 production-safe load。完成 frame F 的 checkpoint 恢复后，唯一 live frame driver 必须从 F+1 开始，不能自行重置为 0 或重复 Tick F。

### 11. Root Header Authority Prerequisite

Runtime bridge 采用 composition-owned root header authority，而不是要求所有 legacy participant header 相等：

- 稳定 participant id 为 `mxframework.runtime.save-header`；
- save-state order 固定为 `0`；
- composition preflight 必须保证它唯一、最先且没有负 order provider；
- authority provider 只拥有 root schema/framework/config/resource header，不拥有 Gameplay entity/module body；
- world、Trigger 和其他 participant 继续只拥有自己的 body/custom state；
- `RuntimeCheckpointBarrier` 仍负责把最终 root frame 归一为完成帧 F；
- Persistence.RuntimeBridge 在 encode/store 前把 merged root 与注入的 build/config/resource descriptor 严格比较；mismatch fail-closed。

当前多个 participant 为兼容旧 `IRuntimeSaveStateProvider` 会返回空或 participant-local root header。它们的 legacy header 字段不是 durable authority，不参与跨 participant equality。未来若 Runtime 引入 body-only capture contract，可通过独立 ADR/迁移收紧；在此之前不得用“所有 participant header 相等”破坏现有模块。

Root header authority、Runtime migration target/pipeline 和 candidate participant catalog 必须在 session composition 后冻结。Runtime bridge Phase 不得在该 prerequisite 缺失时写出长期文件。

### 12. Gameplay Trigger Is Opaque And Isolated

Persistence Core 与 Runtime bridge 不引用 `MxFramework.Gameplay.Triggers`，也不复制以下 Trigger-owned facts：

- `ParticipantId`、recommended order；
- payload/module/custom-state schema；
- definition/binding/catalog hashes；
- pending detail enum；
- internal deferred、reliable outbox 或 JSON shape。

Trigger payload 只作为 `RuntimeModuleSaveState.CustomState.PayloadJson` 内的 opaque Runtime payload 存储。Trigger migration、catalog/entity/range validation 与 participant-local validate-plan-commit 继续由 Trigger owner 执行。

Barrier 返回任意 participant pending/error 时，Persistence.RuntimeBridge 只保留通用 participant id、phase、Runtime error code 与 numeric detail。它不解释或映射 Trigger detail。Trigger pending/reservation/finalization/outbox transfer 存在时不得 drain、clear 或强制 capture；save request 失败或由 Application 在后续 safe point 重试。

Candidate restore 必须保持 gameplay world 在 Trigger 之前恢复的依赖顺序；当前 recommended world/Trigger order `100/200` 是组合规则，不写入 Persistence wire contract。Trigger restore 成功而后续 participant 失败时，整个 candidate 仍被丢弃；不得据此宣称 coordinator 全局原子 restore。

### 13. Settings Isolation

Settings profile、graphics/audio/localization preferences 和 input override 不进入 Gameplay SaveGame envelope、Runtime payload、Runtime hash 或 replay。`MxSettingsJsonFileStore` 不实现 `ISaveGameStore`，首个 Persistence PR 不重构或迁移 Settings。

未来可以复用经过验证的低层 atomic-file primitive，但不能共享 SaveGame generation、compatibility、candidate restore 或“损坏回默认值”语义。

### 14. Required Validation

Phase 1 Persistence Core 最低验证：

- ID 的 empty、invalid character、`.`/`..`、case、N-1/N/N+1；
- generation `0/1/ulong.MaxValue` 与 checked exhaustion；
- envelope v1 golden vector 与 byte-identical roundtrip；
- duplicate property、unknown flags、trailing bytes、truncation 与 length overflow；
- payload byte、protected header byte、prefix byte 各翻转一位后 integrity failure；
- Local/Unspecified timestamp fail-closed；
- metadata/tag/header/payload hard-limit N-1/N/N+1；
- non-empty transform chain 返回 `UnsupportedTransform`；
- in-memory Store buffer ownership、Current/Exact、CAS、delete tombstone、generation 不复用；
- operation terminal/result/error/cancellation invariants；
- `MxFramework.Persistence` asmdef noEngine compile，且无 Runtime/Platform/Settings/Trigger/Unity dependency。

Phase 2 FileSystem 最低验证：

- path containment、URI/provider-owned/read-only location preflight；
- staged write、flush、verify、rename 每个边界的 deterministic fault injection；
- crash/fault 后只观察完整旧 generation 或完整新 generation；
- corrupt head 显式 LKG recovery、unsupported version/transform 不回退；
- retention cleanup failure 只产生 warning；
- 两个相同 expected-generation writer 恰好一个成功。

Phase 3+ Runtime/Application 最低验证：

- 缺失/重复/non-first root header authority 以及 descriptor mismatch 在 encode/store 前失败；
- host 未 Tick、Tick 中、最新 Tick 失败时不调用 provider；
- participant pending numeric detail 原样保留且不创建 write plan；
- Runtime/envelope migration 独立选择，默认在 Application driver 线程；
- corrupt/incompatible prepare-load 不调用任何 live restorer；
- middle participant failure、Trigger success 后 later failure 均丢弃 candidate，live session 不变；
- successful restore 只 publication 一次，首 Tick 精确为 F+1；
- Unity playable save/load 必须另有真实入口、Console、focused EditMode 与 PlayMode continuation evidence。

验证通过只关闭对应 Phase gate；Phase 1 Core 不能被描述为 durable local storage，Phase 1-3 不能被描述为 production-safe load，没有真实入口不能描述为 Playable save/load。

## Consequences

Benefits:

- Runtime authority、durable storage 与 Application publication 拥有清晰且可独立测试的边界。
- v1 wire bytes、limits、generation 和 corruption behavior 不再由首个 provider 偶然决定。
- compare-and-swap、immutable generation 与 explicit recovery 避免静默覆盖和静默旧档回退。
- Trigger、Items、Story 等 participant 可以独立演进 schema/migration，Persistence 不形成反向依赖。
- 候选 session 将现有 coordinator 缺少跨 participant rollback 的事实隔离在未发布状态。

Costs:

- v1 不提供 compression、encryption、signature、cloud conflict resolution 或任意大存档。
- 本地 provider 必须实现 capability preflight、fault injection seam 和 immutable journal，复杂度高于直接 `WriteAllText`。
- Application 必须拥有 candidate session 与单次 publication，而不能把 load 简化为 live coordinator 原地 restore。
- 现有 Runtime participant API 仍携带 legacy root header；需要 order `0` authority prerequisite，长期可再演进为 body-only contract。
- 固定 hard limits 与两代 retention 会拒绝一部分超大或长期历史版本需求；这些需求必须显式版本化扩展。

## Alternatives Considered

### Put persistence directly into MxFramework.Runtime

Rejected because Runtime should own authority state and migration, not platform roots、file I/O、slot policy or cloud/provider concerns. It would also force Runtime to depend on outer storage/platform modules.

### Reuse MxSettingsJsonFileStore

Rejected because Settings deliberately supports defaults-on-missing/corrupt and direct synchronous overwrite. SaveGame requires CAS、generation、integrity、recovery source and candidate restore.

### Store one mutable current file

Rejected because crash/power-loss semantics of overwrite/replace differ across Unity targets, and it cannot preserve a verified last-known-good generation without another transaction protocol.

### Use timestamps for newest/conflict resolution

Rejected because wall clocks can move backward, differ between devices and are presentation metadata rather than authority. Generation is the only ordering/CAS source.

### Require every participant root header to match

Rejected for v1 because current participants return blank or participant-local legacy headers while owning valid body state. A unique order `0` composition authority provides deterministic merged root without coupling Persistence to Trigger or forcing an immediate Runtime-wide provider migration.

### Restore directly into the live session and stop on first failure

Rejected because existing coordinator and participants do not provide cross-participant rollback. Partial mutation would make the previous live state untrustworthy.

### Enable compression or encryption in envelope v1

Rejected because transform ordering、limits、bomb protection、key ownership and platform support are not yet validated. v1 identity-only behavior is explicit and fail-closed.

### Treat checksum as anti-cheat protection

Rejected because an attacker able to modify a file can recompute SHA-256. Authentication/signing requires separate key ownership and threat-model decisions.

## Adoption Gate

This Accepted ADR authorizes phased implementation but does not itself make SaveGame a Current capability.

- Phase 1 may add `MxFramework.Persistence`, logical/wire contracts, integrity, limits, operations and in-memory Store.
- Phase 2 may add `MxFramework.Persistence.FileSystem` only after Platform location/capability contracts exist.
- Phase 3 may add `MxFramework.Persistence.RuntimeBridge` only after root header authority and shared migration/catalog preflight are implemented.
- Production load remains blocked until an Application candidate-session publication transaction is validated.
- Trigger development may continue independently; Persistence work must treat Trigger state/errors as opaque and avoid editing Trigger implementation or schema merely to satisfy storage.

Current API and capability claims must be updated only after the corresponding code and tests land.

### Implementation Status (2026-07-20)

Phase 1 Persistence Core and Phase 3 `MxFramework.Persistence.RuntimeBridge` have landed in the current implementation. Phase 3 provides authoritative checkpoint planning, bounded Runtime serialization and Store commit, fail-closed prepare-load, and exclusive unpublished-candidate restore with exact `F -> F+1` completion. The live checkpoint coordinator's restore profile is the frozen template authority; each prepared load receives a fresh sibling profile identity with the same descriptor/migration topology, and the independent candidate coordinator must bind that exact sibling. This preserves the ADR's requirement that load never restore through the live coordinator or mutate live participants.

`MxFramework.Application.PersistenceBridge` now implements the adopted noEngine Application boundary.
It binds the SessionBridge graph's opaque generation identity and exact RuntimeHost to a
builder-produced authoritative candidate coordinator lease, restores only through the reference-equal
per-load sibling profile, validates the candidate remains unticked, preserves `F -> F+1`, and uses a
repeatable prepared handoff followed by a no-failure graph/scene claim and the Application live-state
CAS. Failed restore, validation, contradiction, or pre-publication disposal poisons and rolls back only
the candidate. Retired live ownership drains Runtime/AppFlow/Scene/Resources in stable order and
reports warning/debt without rolling back the already-published new state.

This closes the noEngine production-safe restore/publication transaction, but not the unique global
frame driver, Unity Boot/loading UI, suspend policy, or Playable save/load surface. Those claims remain
blocked on later #710 stages and Unity evidence. Trigger implementation/schema remains untouched and
its participant state/errors remain opaque to Persistence.

## References

- `Docs/SAVEGAME_PERSISTENCE_DESIGN.md`
- `Docs/PLATFORM_CONTRACTS_DESIGN.md`
- `Docs/APPLICATION_STARTUP_ORCHESTRATION_DESIGN.md`
- `Docs/Interfaces/Runtime.md`
- `Docs/Interfaces/Settings.md`
- `Docs/Interfaces/Gameplay.Triggers.md`
- `Docs/Decisions/ADR-008-gameplay-trigger-runtime-authority.md`
