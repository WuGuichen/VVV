# SaveGame Persistence 持久化设计

> Status: Design
>
> Version: 0.2.0
>
> Date: 2026-07-20
>
> Implementation status: Phase 1 Persistence Core and Phase 3 Runtime bridge are implemented. Durable checkpoint planning/commit, prepare-load and unpublished-candidate restore are available; production-safe live load remains blocked on the Application candidate publication transaction.

## 1. Context

MxFramework 当前已经具备可复用的 Runtime SaveState 基础：

- `RuntimeSaveState` 根文档和 entity/module/custom state DTO；
- `IRuntimeSaveStateProvider` / `IRuntimeSaveStateRestorer` participant；
- schema migration pipeline 与 JSON roundtrip；
- participant 稳定排序、capture/restore 编排和结构化错误；
- `RuntimeCheckpointBarrier` 对最近一次成功 RuntimeHost Tick 和 participant quiescence 的 fail-closed 检查；
- Gameplay、Items、Story、VFX、Gameplay Triggers 等模块已有各自 SaveState participant。

这解决的是“什么运行时状态可以恢复，以及何时可以安全捕获”，尚未解决：

- 用户 profile 与 save slot；
- 可崩溃恢复的持久化提交；
- envelope、checksum、backup 和 last-known-good；
- 存档列表与展示元数据；
- framework/config/resource/content 兼容预检；
- quota、损坏、取消和恢复来源诊断；
- desktop file system 之外的 storage provider；
- load 失败时不污染 live session 的应用级事务。

`MxSettingsJsonFileStore` 是 Settings 专用的同步 JSON 文件实现，不是 Gameplay SaveGame store。Replay 记录输入与结果验证，也不是长期存档。本文设计 `MxFramework.Persistence` 及其 Runtime/FileSystem bridges，只填补 durable storage 和 session publication 边界，不复制 Runtime SaveState。

本文是目标设计。未实现前不得在 `CAPABILITIES.md` 或 Current interface docs 中宣称支持通用存档槽。

## 2. Goals And Non-Goals

目标：

- 复用 `RuntimeCheckpointBarrier` 捕获完整 end-of-Tick authority state。
- 提供 profile/slot、envelope、完整性校验、原子 commit 与 backup recovery。
- 把 prepare-load 与 live restore/publication 分离，失败不污染当前 session。
- 允许 file system、WebGL/console storage、cloud mirror 等 provider 替换，但 v0.1 只要求一个可靠本地 provider。
- 使用结构化错误、稳定 operation state 和可诊断恢复来源。
- 保存格式、Runtime schema、config/resource/content 兼容版本彼此独立演进。
- 保存与加载是显式低频操作；不把文件 I/O 放入 Tick 高频路径。

非目标：

- 不重新定义 `RuntimeSaveState`、participant、migration、checkpoint 或 Trigger payload。
- 不把 Settings profile 合并进 Gameplay SaveGame；Settings 继续独立保存用户偏好。
- 不把 Replay log 当作 save payload，也不承诺从任意 replay frame 构造存档。
- 不在 v0.1 支持 live session 原地无损回滚。
- 不内置 WGame 关卡、角色、任务、背包展示字段或自动存档时机。
- 不默认提供加密、防作弊或数字签名；checksum 只检测意外损坏，不证明可信。
- 不在 core 中直接接入 Steam Cloud、iCloud、Google Play Games 或主机云存档。
- 不让 Persistence 决定暂停 Runtime、切场景、显示 Loading UI 或覆盖冲突存档。
- 不用任意文件路径作为公共 slot identity。

## 3. Source Of Truth And Ownership

| 数据 / 行为 | 唯一 owner | Persistence 如何使用 |
| --- | --- | --- |
| Authority state、frame、module payload | Runtime + participant owner | 只消费 checkpoint capture 结果 |
| Capture completion/quiescence | `RuntimeCheckpointBarrier` | save 的唯一生产级 capture 入口 |
| Runtime schema 和 module migration | `MxFramework.Runtime` / module owner | load prepare 时调用，不复制 migration |
| Profile、slot、generation、envelope | Persistence | 直接拥有并版本化 |
| Durable bytes、atomic commit、backup | `ISaveGameStore` provider | Persistence 通过 store contract 调用 |
| Writable root、platform lifecycle | Platform / Application | 通过 adapter 或组合根注入 |
| Config/resource/content compatibility | 对应 catalog/build owner | 通过 compatibility checker 只读验证 |
| Autosave/manual save policy | Application / project | 向 Persistence 发出 request |
| Save UI、thumbnail、localized label | UI / project | 只读 slot summary；不成为存档权威 |
| Cloud conflict resolution | future provider/application policy | v0.1 不内置 last-write-wins |

三个不同的“原子性”必须分开描述：

1. **Checkpoint capture atomicity**：Barrier 失败时不发布 partial `RuntimeSaveState`。
2. **Storage commit atomicity**：新 generation 完整验证并提交后才成为新的 published head；失败保留旧 head。
3. **Runtime restore atomicity**：现有 `RuntimeSaveStateCoordinator` 会按顺序执行 restorer 并聚合错误，但没有跨 participant rollback。

Trigger participant 自身的 validate-plan-commit 只能保证 Trigger participant 内部原子，不能升级为整个 Runtime restore 原子。v0.1 的生产 load 必须恢复到未发布的候选 session；任何 participant 失败时销毁候选 session，live session 保持不变。

## 4. Target Assemblies And Dependency Direction

```text
BCL
  -> MxFramework.Platform                          (zero framework dependencies; host facts)

MxFramework.Core
  -> MxFramework.Runtime                           (authority SaveState/checkpoint)

BCL
  -> MxFramework.Persistence                       (zero framework dependencies; slot/envelope/store contracts)

MxFramework.Persistence + MxFramework.Runtime
  -> MxFramework.Persistence.RuntimeBridge         (checkpoint + Runtime payload bridge)

MxFramework.Persistence + MxFramework.Platform
  -> MxFramework.Persistence.FileSystem            (optional local file adapter)

Application / project composition root
  -> Persistence.RuntimeBridge + Persistence.FileSystem  (policy and session publication)
```

目标 asmdef：

| Assembly | 职责 | 允许依赖 |
| --- | --- | --- |
| `MxFramework.Persistence` | slot、envelope、operation、store、integrity、recovery contract | BCL |
| `MxFramework.Persistence.RuntimeBridge` | `RuntimeSaveState` codec、checkpoint capture、durable commit handoff、prepared candidate restore | Persistence、Runtime |
| `MxFramework.Persistence.FileSystem` | local layout、same-directory staging、replace/backup | Persistence、Platform、BCL IO |

`MxFramework.Persistence` 和两个 bridge 均保持 `noEngineReferences=true`。Persistence core 不引用 Gameplay、Trigger、Resources、Settings、UI 或 Unity；兼容性由注入 checker 和不可变 descriptor 表达。

若实现阶段证明三个 asmdef 对 v0.1 过细，可以合并 FileSystem 到 Persistence，但必须保持 Runtime bridge 的单向依赖和可替换 store contract；不得为了少一个 asmdef 让 core 读取 `UnityEngine.Application`。

## 5. Terminology And Stable Identity

| Term | Meaning |
| --- | --- |
| Save Profile | 一个本地用户/角色域下的 slot namespace；不是平台账号本身。 |
| Save Slot | 用户可见或系统保留的稳定逻辑位置。 |
| Save Generation | 对同一 slot 的一次成功提交，单调递增。 |
| Save Envelope | Persistence-owned header + encoded Runtime payload + integrity data。 |
| Prepared Load | 已读取、校验、迁移但尚未修改任何 live Runtime 的候选结果。 |
| Head Generation | 最高已原子发布的 generation id；并发 precondition 以它为准，即使之后发现文件损坏也不静默倒退。 |
| Selected Generation | 某次 read 实际返回的 head、exact 或 recovery generation，并携带 recovery source。 |
| Last-Known-Good | head 损坏或不可读时可显式恢复的前一完整、已验证 generation。 |
| Recovery Source | Primary、Backup、OlderGeneration 或 ProviderRecovery。 |

候选 typed IDs：

- `SaveGameProfileId`
- `SaveGameSlotId`
- `SaveGameGeneration`
- `SaveGameOperationId`

ID 由校验后的稳定 value 表示，不等于目录名。FileSystem adapter 必须单独编码路径段，拒绝 `..`、separator、rooted path、控制字符和超长输入。

## 6. Save Envelope

### 6.1 Envelope Versus Runtime Payload

```text
SaveGameEnvelope v1
  FormatId                     "mx.save-game"
  EnvelopeVersion             Persistence schema
  ProfileId / SlotId
  Generation
  RecordKind                   save / tombstone
  CreatedAtUtc / UpdatedAtUtc  caller-supplied UTC presentation metadata only
  BuildDescriptor
  CompatibilityDescriptor
  PayloadDescriptor            save required; tombstone null
      CodecId                  e.g. runtime-save-state-json
      RuntimeSchemaVersion     save accepts 0..int.MaxValue; tombstone keeps schema 0 sentinel
      UncompressedLength
      StoredLength
      TransformIds[]
      Sha256
  DisplayMetadata              bounded, non-authority
  PayloadBytes                 encoded RuntimeSaveState; tombstone empty
```

v1 的精确 binary frame、canonical JSON property 名称/顺序、required/null/unknown-property 规则、flag mirror 和 tombstone 表示由 `Decisions/ADR-010-savegame-persistence-authority.md` 冻结；实现与 golden vectors 必须以 ADR 为准，不能从本概念图自行推导另一种 wire shape。

Envelope version 与 `RuntimeSaveState.SchemaVersion` 独立：

- save record 的 Runtime schema `0` 是合法初始版本，不表示字段缺失；负值无效；
- envelope migration 只处理存储结构、checksum/transform/header；
- Runtime migration 继续由 `RuntimeSaveStateMigrationPipeline` 处理；
- module custom state migration 继续由对应 participant owner 处理；
- 任一层缺失 migration 时结构化失败，不猜测默认值。
- envelope 中镜像的 runtime frame/schema/framework/config/resource 字段必须与解码后的 inner root 一致；不一致返回 `EnvelopePayloadMismatch`，不选择其中一方覆盖另一方。

时间戳由 Application 使用 Platform `IWallClock` 生成并随 save request 注入；Application/Platform bridge 对无效 clock kind 返回 Platform `AdapterFailure`。Persistence core 不依赖 Platform，并在 checkpoint capture 前执行防御性二次验证；Local/Unspecified 返回 Persistence 自有的 `InvalidTimestamp`，且不自行转换。宿主时钟允许回拨，因此 slot 排序、并发 precondition 和“最新”判断只使用 generation，绝不使用时间戳。

### 6.2 Compatibility Descriptor

至少记录：

- framework/build version 或 build manifest id；
- Runtime SaveState schema；
- config version；
- resource catalog version；
- content set id/hash（如果项目启用 ContentPackages）；
- project-defined compatibility tags（有数量和长度上限）。

`ISaveGameCompatibilityChecker` 对这些 descriptor 返回：

- `Compatible`
- `MigrationRequired`
- `Incompatible`
- `Unknown`

Unknown 默认 fail-closed，只有项目显式 policy 才允许带警告继续。文件中记录的 display metadata 不能覆盖真实 catalog/build descriptor。

### 6.3 Display Metadata

允许存储 title、scene label、playtime、difficulty label 等低频展示信息，但必须：

- 是 bounded key/value 集合；
- 不参与 Runtime restore；
- 不参与 authority hash；
- 不被当作可信规则输入；
- 可在 payload 损坏时单独读取，但 summary 必须标记 integrity state。

thumbnail 不内嵌 v0.1 envelope。项目可使用 sidecar artifact provider，但删除/恢复必须与 generation 生命周期一致。

### 6.4 Runtime Header Authority Prerequisite

当前 `RuntimeSaveStateCoordinator` merge 使用第一个成功 provider 的 root header，再拼接所有 participant body；它尚未验证每个 provider 的 framework/config/resource/schema header 一致性。Durable SaveGame 不能让“偶然排在第一的 gameplay participant”决定长期兼容 header。

Phase 3 Runtime bridge 固定采用 composition-owned 的唯一 order `0` root-header authority。registry preflight 必须证明该 authority 唯一、最先且没有负 order provider；Persistence bridge 再把最终 merged root 与注入的 build/config/resource descriptor 做严格一致性校验。

legacy participant 返回的空或 participant-local root header 不参与跨 participant equality；world、Trigger 等 participant 继续只拥有 body/module state。order `0` authority 缺失/重复，或最终 merged root 与注入 descriptor mismatch 时，save 在 encode/store 前 fail-closed；不能写出“checksum 正确但兼容 header 来源不确定”的文件。

## 7. Contract Candidates

以下 Store/operation contract 已由 Persistence Core 实现；Runtime bridge 小节记录 Phase 3 当前公共 API。后续 Application publication contract 仍是目标形态。

### 7.1 Store Contract

```csharp
public interface ISaveGameStore
{
    ISaveGameOperation<SaveGameSlotList> ListSlots(
        SaveGameProfileId profileId,
        CancellationToken cancellationToken = default);

    ISaveGameOperation<SaveGameReadResult> Read(
        SaveGameReadRequest request,
        CancellationToken cancellationToken = default);

    ISaveGameOperation<SaveGameCommitResult> Commit(
        SaveGameCommitRequest request,
        CancellationToken cancellationToken = default);

    ISaveGameOperation<SaveGameDeleteResult> Delete(
        SaveGameDeleteRequest request,
        CancellationToken cancellationToken = default);
}
```

`SaveGameReadRequest` 明确包含 profile/slot、`Current` 或 `ExactGeneration` selection，以及仅供 Current 使用的 recovery policy。`Current` 的 primary 永远是 published head，不是“最高仍可读文件”；ExactGeneration 永不自动回退；invalid/default exact generation 在 I/O 前失败。这样 Application 可以在用户明确选择旧代时发出 exact request，而 Store 无需猜测意图。

Store 接收已经编码并带 checksum 的 envelope bytes；它只解析完成结构/length/integrity/recovery 所需的 Persistence header，不解析 `RuntimeSaveState`，也不决定兼容性。`Commit` 必须实现 compare-generation 或 equivalent precondition，禁止两个 writer 静默 last-write-wins。

### 7.2 Operation Contract

```csharp
public interface ISaveGameOperation<TResult>
{
    SaveGameOperationId OperationId { get; }
    SaveGameOperationStatus Status { get; }
    float Progress { get; }
    bool IsDone { get; }
    bool CanCancel { get; }
    SaveGameError Error { get; }
    bool HasResult { get; }
    TResult Result { get; }
    bool TryGetResult(out TResult result);
    void Poll();
    void RequestCancel();
}
```

`SaveGameOperationStatus` 固定为 `Pending / Running / Succeeded / Failed / Cancelled / TimedOut`。采用可轮询 operation，而不是强制 `Task`/线程：desktop provider 可以后台 I/O，WebGL/console provider 可以由宿主 pump。operation 正常 poll 必须无异常、进度单调且 clamp 到 `[0,1]`；`Result` 仅在 Succeeded 后有效，提前读取返回 default 且不能触发工作。Succeeded/Failed/Cancelled/TimedOut 都是 `IsDone=true`、`CanCancel=false` 的终态；Succeeded 才有 result，Failed 的 `Error` 非空，Cancelled 使用 `Error=None`，TimedOut 使用 `Error.Code=Timeout` 且没有 result。

`RequestCancel` 是协作式请求，且只在 provider 的 atomic publication boundary 之前允许 `CanCancel=true`。进入不可撤销 publication 后必须先把 `CanCancel` 置为 false；operation 可以在 publication 或不可取消的 cleanup/drain 尚未完成时保持 `Running && !CanCancel`，调用方必须继续 poll 到终态。publish 已完成时即使 cancellation 随后到达仍返回 success。传入 `CancellationToken` 被触发等价于向该 operation 发出一次幂等 `RequestCancel`，不承诺立即终结或撤回已发布 commit。Persistence Core 不提供默认 timeout、wall-clock timer 或隐式 deadline；只有显式 provider/上层 timeout policy 才能产生 TimedOut/Timeout。

属性读取、正常 `Poll`、终态 `RequestCancel`、`IsDone` 和 `CanCancel` 在初始化后都是 `NoAlloc`。operation/result 创建和 provider I/O 属于低频分配边界。

### 7.3 Runtime Bridge

```csharp
public interface IRuntimeSaveGameService
{
    RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan> PlanCheckpointSave(
        RuntimeSaveGameCheckpointRequest request);
    IRuntimeSaveGameOperation<SaveGameCommitResult> Commit(
        RuntimeSaveGameWritePlan writePlan,
        CancellationToken cancellationToken = default);
    IRuntimeSaveGameOperation<PreparedRuntimeSaveGame> PrepareLoad(
        RuntimeSaveGameLoadRequest request,
        CancellationToken cancellationToken = default);
    RuntimeSaveGameBridgeResult<long> RestorePrepared(
        PreparedRuntimeSaveGame prepared,
        IRuntimeSaveGameRestoreTarget candidateTarget);
}

public interface IRuntimeSaveGameRestoreTarget
{
    bool TryAcquireRestoreLease(
        RuntimeSaveStateRestoreProfile expectedRestoreProfile,
        out IRuntimeSaveGameCandidateRestoreLease lease,
        out RuntimeSaveGameTargetValidationError validationError);
}

public interface IRuntimeSaveGameCandidateRestoreLease : IDisposable
{
    string TargetId { get; }
    RuntimeSaveStateCoordinator Coordinator { get; }
    RuntimeSaveStateRestoreProfile RestoreProfile { get; }
    bool TryCompleteRestore(
        long completedFrame,
        long nextFrame,
        out RuntimeSaveGameTargetValidationError validationError);
}
```

已实现语义：

- `PlanCheckpointSave` 只能在 authority/bootstrap 主线程调用，并同步执行 `RuntimeCheckpointBarrier.CaptureCheckpoint()`；capture 失败时不生成 write plan、不启动 store write。
- `RuntimeSaveGameWritePlan` 只持有不可变 snapshot、slot precondition 和 metadata；不能引用 live Runtime 对象。
- capture 成功后，`Commit` 的 serialization/checksum/store 可以由 provider 异步或分块执行。
- `PrepareLoad` 只读、校验、envelope migrate、compatibility check、Runtime JSON parse/migrate；它从 live checkpoint/template profile 创建一次性的 sibling identity 并把 prepared plan 绑定到该 sibling，不调用任何 restorer。
- `RestorePrepared` 先通过 `TryAcquireRestoreLease` 在同一临界区校验并排他占用未发布候选，避免 `IsPublished` 检查与恢复之间的 TOCTOU；再要求 lease、candidate coordinator 和 prepared plan 使用 reference-equal 的 per-load sibling profile，绝不复用 live coordinator 的 participant。
- candidate restore 成功后以 `TryCompleteRestore(F, F+1, ...)` 精确提交帧交接；未成功 complete 就释放 lease 时，target 必须永久 poison 该候选，使其不能再发布或重新获取。
- `IRuntimeSaveGameRestoreTarget` 是 Persistence.RuntimeBridge 的最小 bridge contract，只暴露原子 lease 获取；具体 identity、coordinator 和 restore profile 只在已获取的 lease 上暴露，不提供全局 service locator。
- Application transaction 的 concrete candidate 通过 `ApplicationStartup.PersistenceBridge` 实现该 target；Persistence 不依赖 Application，也不创建全局 session singleton。

`RuntimeSaveGameWritePlan` 和 `PreparedRuntimeSaveGame` 都是 caller-owned、幂等 `IDisposable` lease：

- write plan 返回后由 caller 持有；`Commit` 接受 operation 后 ownership 转给 operation，operation 在成功、失败或取消终态释放；从未提交的 plan 由 caller 释放。
- successful `PrepareLoad` 把 prepared lease 转给 caller；Application step 在 restore 成功、restore 失败、cancel 或 candidate rollback 后统一释放。
- failed/cancelled prepare operation 自行释放内部 buffer，不返回半有效 lease。
- 重复 Dispose 安全；释放后任何 restore/commit 返回结构化 `LeaseReleased`。

Phase 3 已引入上述公共 target/lease，但只完成 build 前的 prepare 与候选 restore。Application 仍必须提供 build -> restore -> validate -> atomic publish transaction，并由测试证明失败时 live session 未变；在该事务落地前不得把本桥接描述为 production-safe live load。

### 7.4 Recovery Policy

```csharp
public enum SaveGameRecoveryPolicy
{
    PrimaryOnly = 0,
    PrimaryThenLastKnownGood = 1
}
```

默认 manual load 可以 `PrimaryThenLastKnownGood`，但这里只处理 truncated/malformed envelope、length/checksum 等存储完整性失败。完整文件声明了未知/不支持的 `EnvelopeVersion`、缺失 migration，或 build/config/resource/content 不兼容时默认显式失败；只有 Application 明确请求某个旧 generation 时才重新执行 prepare/compatibility，不能由 Store 静默回退。必须把 `RecoverySource`、原 generation、恢复 generation 和 warning 返回给 UI/diagnostics。自动覆盖损坏 primary 前必须先保留证据；读取 backup 成功不等于立即写回。

## 8. Save Transaction

生产 save 流程：

```text
Application save policy
  -> validate profile/slot and acquire per-slot writer lease
  -> RuntimeCheckpointBarrier.CaptureCheckpoint()
       -> latest RuntimeHost Tick succeeded
       -> every quiescence checker Ready
       -> coordinator capture succeeds with no partial value
  -> create immutable envelope header
  -> serialize RuntimeSaveState payload
  -> v1 identity payload only (no transform); future transforms require a new Accepted ADR
  -> compute checksum over stored payload + protected header fields
  -> store.Commit(expectedGeneration, nextGeneration, envelopeBytes)
       -> stage in same transactional domain
       -> flush/provider durability barrier where supported
       -> read/verify staged checksum
       -> publish new generation atomically
       -> retain previous valid generation as last-known-good
  -> release writer lease and publish result
```

规则：

- capture frame 为完成帧 `F`，恢复后的第一个 Runtime tick 仍是 `F + 1`。
- Trigger pending/reservation/outbox checker 返回 Pending 时 save 失败或由上层在后续 safe point 重试；Persistence 不 drain/clear participant。
- capture 成功后 Runtime 可以继续运行；异步 writer 只持有不可变 snapshot/envelope，不读取 live objects。
- 同一 profile/slot 同时最多一个 writer；不同 slot 是否并行由 provider capability 决定。
- next generation 使用 checked monotonic increment；溢出 fail-closed，删除或 recovery 不允许复用旧 generation id。
- cancellation 在 commit publication 之前可以取消并清理 staging；publication 已完成后返回 success，不伪报 cancelled。
- timeout、quota、disk full、permission 和 provider unavailable 均保留旧 published head。

### 8.1 FileSystem Adapter Commit

`Persistence.FileSystem` 优先使用不可变 generation journal，而不是原地覆盖 current：

```text
profiles/<safe-profile>/slots/<safe-slot>/
  generation-00000000000000000012.mxsave
  generation-00000000000000000013.mxsave
  .staging-<unique-id>
```

最低策略：

1. 构造/预检只接受 Platform `FileSystemPath + IsWritable` location；URI/ProviderOwned 立即返回 `InvalidLocation`，并探测同卷 create-new/rename/flush 能力。
2. 扫描最高已原子发布的 generation id 作为 head（不因内容损坏复用或倒退 id），并校验 request 的 expected head generation。
3. 在目标 slot 同目录以 create-new 语义创建唯一 staging 文件。
4. 写入完整 bytes，flush 到 provider 可支持的 durability level。
5. 重新读取 header/checksum 验证 staging。
6. 使用 same-volume atomic rename 把 staging 发布为不存在的下一 generation 文件。
7. reader 忽略 staging，把最高已发布 generation 作为 primary；只有 primary 被截断、结构损坏或 integrity 失败时，且 request 允许 recovery，才按降序寻找上一完整 generation。完整但 `EnvelopeVersion` 不受支持的 primary 显式失败，不回退；Store 不判断 Runtime/config/resource/content compatibility。
8. 保留最新和上一有效 generation；更旧 generation 延迟 best-effort 清理。

发布新不可变文件不依赖 `File.Replace` 在所有 Unity 平台上的一致行为。若目标 file system 连 create-new + reliable rename 都不能保证，adapter 必须返回 `AtomicCommitUnsupported`，或使用经过平台验证的 journal/transaction provider；不能退化为 `File.WriteAllText(current)` 并报告成功。清理失败返回 typed `SaveGameWarning.RetentionCleanupFailed`，不回滚已经成功发布的新 generation。一次 operation 中一个或多个 retention cleanup failure 聚合为同一个 singular warning；逐项失败细节只留在 provider-local diagnostics。warning 通过成功的 `SaveGameCommitResult` / `SaveGameDeleteResult` 携带；公共 warning 不含 message、exception 或物理路径，cleanup exception 只留在 provider-local diagnostics。

`Application.persistentDataPath` 只提供逻辑 root，不证明以上语义。WebGL、console 和 cloud provider 必须有独立验证。

## 9. Load And Session Publication Transaction

生产 load 流程：

```text
Read published head generation (or explicitly requested exact generation)
  -> envelope size/header validation
  -> checksum/integrity validation
  -> optional last-known-good recovery
  -> envelope migration
  -> compatibility preflight (latest incompatible fails explicitly by default)
  -> decode RuntimeSaveState
  -> Runtime schema migration
  -> PreparedRuntimeSaveGame bound to a per-load sibling profile (still no live mutation)
  -> construct isolated candidate application/runtime session
  -> register the exact participant catalog/order
  -> restore every participant into candidate
  -> run candidate validation / required resource checks
  -> publish candidate as live session in one composition-root commit
  -> dispose previous session after successful handoff
```

禁止流程：

```text
read file -> call live RuntimeSaveStateCoordinator.RestoreSaveState()
          -> some participants mutate
          -> later participant fails
          -> continue using partially restored live session
```

如果候选 session restore 失败：

- 不发布候选对象；
- 逆序 dispose/release candidate resources；
- 当前 live session 保持原引用和状态；
- 返回所有 participant errors 及失败 phase；
- 不自动删除或覆盖原存档。

在没有候选 session publication 机制前，通用 production load 能力不能标记完成。开发 Showcase 可以显式使用 destructive reset/load，但必须在 UI 与文档中标为非生产路径。

## 10. Error Model

建议 `SaveGameErrorCode` 分段：

| Range | Category | Examples |
| --- | --- | --- |
| 1000 | Request / identity | InvalidProfile、InvalidSlot、Busy、GenerationConflict |
| 1100 | Checkpoint / capture | CheckpointUnavailable、PendingWork、CaptureFailed |
| 1200 | Encoding | SerializeFailed、TransformFailed、PayloadTooLarge、InvalidTimestamp |
| 1300 | Store | StoreUnavailable、QuotaExceeded、PermissionDenied、AtomicCommitUnsupported、IoFailure |
| 1400 | Integrity / format | InvalidEnvelope、IntegrityFailed、UnsupportedEnvelopeVersion |
| 1500 | Compatibility | RuntimeSchemaUnsupported、ConfigIncompatible、ResourceCatalogIncompatible、ContentSetIncompatible |
| 1600 | Restore / publication | CandidateBuildFailed、RestoreFailed、CandidateValidationFailed、PublicationFailed |
| 1700 | Control | Timeout；Cancelled 使用 operation terminal status，不重复编码为 error |

结果对象应包含：

- profile/slot/operation/generation；
- phase；
- stable error code；
- optional provider code；
- Runtime participant errors（如有）；
- recovery source 和 warnings；
- exception 仅用于本地低频 diagnostics。

成功路径不构造错误文本。错误 message 不进入 SaveState、hash、network 或兼容判断。

Provider 写入公共 `SaveGameError.Path` / `SaveGameWarning.Path` 时只能使用稳定逻辑 token，例如 `location`、`staging`、`flush`、`verify`、`publish`、`retention`；不得暴露绝对路径、用户目录、文件名或 URI。Retention cleanup 的公共 warning 是封闭的 typed value，只暴露 `RetentionCleanupFailed`、`Retention` phase 与 `retention` path；cleanup exception 和物理路径只进入 provider-local diagnostics。`InvalidLocation`、`AtomicCommitUnsupported`、`PermissionDenied`、`QuotaExceeded`、`IoFailure` 是 Store 段的稳定 provider-facing code。`TimedOut`/`Timeout` 只由显式 provider/上层策略产生，Core 没有默认计时器。

## 11. Concurrency, Limits And Security

### 11.1 Concurrency

- coordinator 维护 per-slot writer lease。
- commit request 带 expected generation；冲突必须显式返回。
- delete request 同样带 expected generation；v1 只能提交 ADR-010 定义的 immutable tombstone generation，不直接删除全部 generation，也不得跨并发 writer 静默删除较新 generation。
- list/read 可以与其他 slot writer 并行，但读取同一 slot 必须得到完整旧 generation 或完整新 generation。
- restore/publication 由 Application 单线程串行化；不能同时 publish 两个 candidate session。

### 11.2 Limits

实现前冻结并测试：

- profile/slot id 最大长度；
- display metadata count/key/value 长度；
- envelope header 和 payload 最大 bytes；
- retained generations 数；
- concurrent operations 数；
- transform chain 长度；
- parse nesting/decompression ratio 防护。

超限返回结构化错误，不能在读入任意大 payload 后才失败。

### 11.3 Integrity And Confidentiality

- SHA-256/checksum 检测 accidental corruption，不是反作弊签名。
- v1 不执行 compression/encryption，transform chain 必须为空。未来版本只有在新的 Accepted ADR 冻结 transform ID、顺序、limits 与安全边界后才可接入；未知 transform fail-closed。
- 密钥不能硬编码在 SaveGame core，也不能记录到 diagnostics。
- 加密、签名、cloud provider 都不能改变 Runtime authority schema。
- 存档路径、平台 user id、device id 默认不写入 payload 或 telemetry。

## 12. Diagnostics And Allocation Contract

`SaveGameDebugSnapshot` 建议包含：

- active operations 与 phase；
- current profile/slot/generation；
- last save/load result；
- bytes、duration、recovery source；
- per-provider capability 与 last error code；
- 不包含完整 payload、密钥或敏感路径。

分配分类：

- checkpoint capture：沿用 Runtime `AllocByDesign`。
- list/read/serialize/commit/prepare-load：`AllocByDesign` 或 `LowFreqAlloc`。
- operation poll/progress getter：初始化后 `NoAlloc`。
- Application 每帧 pump：初始化后没有 active phase transition 时 `NoAlloc`。
- file/network I/O 不在 Runtime Tick 内执行。

大存档的 serialization 和 checksum 应支持后台或分块 provider；但 Unity object、Runtime participant 和 live session 访问只能在其线程契约允许的阶段进行。

### 12.1 Stage Threading Matrix

| Stage | Default thread contract |
| --- | --- |
| Platform location resolve / provider creation | Application/main thread |
| Store file/network I/O、raw length、SHA-256 | provider worker 或宿主 pump；不得访问 Unity/Runtime live state |
| Envelope parse/migration | 默认 Application driver；只有 codec 显式声明 thread-safe 才可 worker |
| Runtime JSON deserialize / migration | 默认 Application driver；现有 migration contract 未承诺 thread-safe |
| Config/resource/content compatibility | 默认 Application driver；checker 可能读取 composition-owned catalogs |
| `PlanCheckpointSave` / checkpoint capture | authority/main thread safe point |
| `RestorePrepared` / participant restore | exclusive candidate bootstrap thread，通常主线程 |
| operation terminal delivery | marshal 到 Application driver 后再推进 step/publication |

Bridge 不得因为某个测试替身线程安全，就把所有 migration/checker 默认放到 worker。若未来增加 `IsThreadSafe` capability，必须逐实现声明并覆盖并发测试。

## 13. Test Strategy

### 13.1 Core / Store

| Test ID | Scenario | Required assertion |
| --- | --- | --- |
| `SAVE-ID-01` | path traversal / invalid ID | rejected before provider path creation |
| `SAVE-ENV-01` | envelope v1 roundtrip | header、payload、checksum stable |
| `SAVE-ENV-02` | envelope/runtime schema independent | each migration selected by correct version |
| `SAVE-ENV-03` | Local/Unspecified timestamp or wall-clock rollback | invalid kind rejected before capture；rollback does not affect generation/head selection |
| `SAVE-HDR-01` | order `0` authority missing/duplicate or merged root/build descriptor mismatch | rejected before encode/store；legacy participant header 不参与 equality |
| `SAVE-INT-01` | one-byte payload/header corruption | integrity failure, no decode/restore |
| `SAVE-COMMIT-01` | crash/fault before publish | old generation remains published head |
| `SAVE-COMMIT-02` | fault during publish/rename | old or new complete generation only, never partial |
| `SAVE-COMMIT-03` | two writers same expected generation | exactly one success, one GenerationConflict |
| `SAVE-COMMIT-04` | head generation at maximum / corrupt head present | overflow fails closed; corrupt head id is not reused or silently decremented |
| `SAVE-REC-01` | primary corrupt, backup valid | recovery source explicit, primary not silently overwritten |
| `SAVE-REC-02` | primary and backup corrupt | fail closed, files preserved for diagnosis |
| `SAVE-REC-03` | latest integrity-valid but content-incompatible | explicit incompatibility; no silent older-generation fallback |
| `SAVE-REC-04` | latest complete but unsupported envelope version | explicit UnsupportedEnvelopeVersion; no silent older-generation fallback |
| `SAVE-LOC-01` | URI/provider-owned/read-only location | FileSystem store rejects before write |
| `SAVE-LEASE-01` | write-plan/prepared success/failure/cancel/dispose | ownership transfers once; repeated dispose safe |
| `SAVE-LIMIT-01` | N-1/N/N+1 payload and metadata limits | deterministic accept/reject |

### 13.2 Runtime Integration

| Test ID | Scenario | Required assertion |
| --- | --- | --- |
| `SAVE-CHK-01` | host never ticked / tick in progress / last tick failed | no provider capture, stable checkpoint error |
| `SAVE-CHK-02` | Trigger pending detail 1..7 | save rejected with participant/detail preserved |
| `SAVE-FRM-01` | capture end-of-F | restored candidate first tick is F+1 |
| `SAVE-PREP-01` | corrupt/incompatible load | live participant methods never called |
| `SAVE-RST-01` | middle participant restore fails | candidate discarded; live session reference/state unchanged |
| `SAVE-RST-02` | Trigger restore succeeds, later participant fails | no claim of global atomic restore; candidate discarded |
| `SAVE-RST-03` | all participant restores succeed | candidate published once, old session disposed after handoff |
| `SAVE-RES-01` | missing required config/resource | failure occurs before publication |

### 13.3 Platform / Fault Injection

- disk full/quota exceeded；
- permission denied/read-only root；
- cancellation at capture/encode/stage/verify/publish boundary；
- process crash simulation after each commit step；
- URI/virtual storage provider；
- WebGL/console provider-specific durability evidence；
- payload truncation、duplicate header keys、unknown transform、decompression bomb；
- operation poll allocation check。
- migration/compatibility 默认回到 Application driver 的线程断言。

每个 fault seed 必须可重复，验证旧 published head、backup、staging cleanup 和返回 error，而不只断言“抛了异常”。

## 14. Phased Delivery

| Phase | Deliverable | Minimum gate |
| --- | --- | --- |
| 0 Design | 本文 + 待接受 ADR | 三种原子性、session publication、格式分层无歧义 |
| 1 Persistence Core | IDs、envelope、integrity、operation/lease、in-memory store、limits | `SAVE-ID-*`、`SAVE-ENV-*`、`SAVE-INT-*`、`SAVE-LEASE-01`、`SAVE-LIMIT-*` |
| 2 FileSystem Store | location preflight、staging、atomic commit、backup/recovery、generation conflict | `SAVE-LOC-01`、`SAVE-COMMIT-*`、`SAVE-REC-*` + fault injection |
| 3 Runtime Bridge | ✅ checkpoint capture、root header authority/validation、durable commit、prepare-load、Runtime codec/migration、candidate restore lease | `SAVE-HDR-01`、`SAVE-CHK-*`、`SAVE-FRM-01`、`SAVE-PREP-01` + RuntimeBridge focused suites |
| 4 Candidate Session Load | isolated build/restore/validate/publish | `SAVE-RST-*`、`SAVE-RES-01` |
| 5 Unity / Playable | manual save/load + suspend checkpoint smoke | Unity compile、Console、focused EditMode + PlayMode continuation |
| 6 Provider Extensions | cloud/WebGL/console adapters as product needs | provider contract、quota/conflict/recovery 真机证据 |

当前已完成 Phase 3，只能宣称 durable checkpoint/commit、prepare-load 和 unpublished-candidate restore；没有 Phase 4 不得宣称 production-safe live load。只有 Phase 5 有真实可操作入口后，才能宣称 Playable save/load。

## 15. ADR Decisions Required Before Implementation

1. 接受 Persistence 不复制 Runtime SaveState，只拥有 durable envelope/store/session handoff。
2. 接受 checkpoint、storage commit、runtime restore 是三种不同原子性。
3. 接受 production load 必须使用候选 session，不能直接恢复到 live session。
4. 接受 compare-generation，禁止并发 writer 静默 last-write-wins。
5. 接受 checksum 默认只做完整性，不宣称安全/防作弊。
6. 接受 Settings profile 不进入 Gameplay SaveGame。
7. 冻结 v0.1 envelope format、limits、backup retention 和首个 store capability contract。

这些决策及 v1 wire/limits/retention 细节已由 owner 接受并冻结在 `Decisions/ADR-010-savegame-persistence-authority.md`。本文继续作为分阶段实现设计；Phase 3 当前事实以 `Interfaces/Persistence.md`、`Interfaces/Runtime.md`、源码和测试为准，Application publication 验证通过前不得声明 production-safe live load。

## 16. Design Acceptance Checklist

- [ ] Runtime SaveState / migration / checkpoint 没有被重复实现。
- [ ] save 只从 `RuntimeCheckpointBarrier` 获取 production authority snapshot。
- [ ] Trigger pending work 不被 Persistence 偷偷 drain 或清除。
- [ ] envelope schema 与 Runtime schema 分离。
- [ ] commit 失败保留上一完整 generation。
- [ ] checksum、backup、recovery source 和 generation conflict 可诊断。
- [ ] load prepare 不修改 live runtime。
- [ ] participant restore 失败时候选 session 被丢弃，live session 不变。
- [ ] Settings、Replay、cloud、encryption 的边界清楚。
- [ ] 当前能力文档不会提前宣称通用存档槽已实现。

## References

- `API_STANDARDS.md`
- `ARCHITECTURE.md`
- `RUNTIME_FOUNDATION_SYSTEM.md`
- `Interfaces/Runtime.md`
- `Interfaces/Settings.md`
- `GAMEPLAY_TRIGGER_RUNTIME_DESIGN.md`
- `Decisions/ADR-008-gameplay-trigger-runtime-authority.md`
- `Packages/com.vincent.wgameframework/Runtime/RuntimeSaveState.cs`
- `Packages/com.vincent.wgameframework/Runtime/RuntimeSaveStateJson.cs`
- `Packages/com.vincent.wgameframework/Runtime/RuntimeSaveStateCoordinator.cs`
- `Packages/com.vincent.wgameframework/Runtime/RuntimeCheckpointBarrier.cs`
- `Packages/com.vincent.wgameframework/Gameplay.Triggers/GameplayTriggerRuntimeSaveStateParticipant.cs`
- `Packages/com.vincent.wgameframework/Settings/MxSettingsJsonFileStore.cs`
