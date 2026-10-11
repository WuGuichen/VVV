# Socket Bake 二进制导出实施任务

> Status: Design（已实施并完成专项验收）
>
> 执行状态：**P0～P5 已完成**，六个活跃包使用 v3 索引 + 唯一 bytes；最终验收见 §11.5。
> 2026-09-21 收尾：Unity 编译与 Console 通过，EditMode 分组 79/79、PlayMode 6/6；有效 Profiler 分配量已测得。Player/AssetBundle 成品未构建，按 §7.3 保留明确验证边界。
>
> 编制与基线复查：2026-09-21。
>
> 评审修订：2026-09-21（第二轮）。本轮补齐 §1.1 压缩取舍实测依据、§2.2.1 需同步文档清单、§2.3 完整消费点、§3.1 `.meta` 约束、§5.1 证据结构体、§5.3 旧 JSON 解析器归属定论、§6.1 措辞、§7.1 原字段口径与 §9.1 环境要求；并把 §2.1 的二进制体积从"估算"升级为"已离线复现"。同时更正第一轮评审中"`checkedFrameCount` 口径冲突"的误判——回执、缓存与 JSON 三者自洽，该字段等于全部位置帧数，详见 §7.1。
>
> 用户已确定：**强制重新导入。正式运行端不兼容旧完整 JSON，不保留自动回退。**

## 1. 任务目标与固定决策

将角色生成包中的 Socket Bake 从完整 JSON 改为 **小型 JSON 索引 + 无损二进制载荷**，一次完成生成、读取、缓存、导入回执、Readiness、Player 暂存及现有活跃包迁移。

本次决策固定如下，实施者不再另选序列化框架或兼容策略：

| 决策 | 本任务采用的做法 |
| --- | --- |
| 新格式 | 自有有界二进制格式，版本 3；小 JSON 索引也使用 v3 |
| 旧包 | 必须通过正式 CLI + Unity 同步重新导入；运行端拒绝旧完整 JSON |
| 数值精度 | 位置、位移和方向基全部保留现有 Int64，不转浮点，不降低量化精度 |
| 压缩 | 首版不压缩，不引入差分编码、变长整数、RLE、内存映射或延迟解码 |
| Runtime 对象 | 加载时恢复现有 Catalog/Track/Frame 对象，查询接口和 Tick 逻辑不变 |
| 逐帧载荷 | 只保留一份 `.bytes`；JSON 索引不再包含轨道或帧数组 |
| 完整诊断 JSON | 仅显式导出到 `Assets` 之外，不随正式包发布 |
| 推广范围 | 先验证 000003，再迁移其余五个活跃包；不恢复退役包 |
| 本轮非目标 | 不调整采样、动画、IK、伤害或姿态算法；不重构其它 Bake；不做 Git 提交/推送 |

目录大小减少是硬性目标；加载耗时和分配量需要测量。不得用目录字节收益代替 Player 成品大小、常驻内存或帧率收益。

### 1.1 压缩取舍的实测依据

同目录的 `character_hurtbox_bake_catalog.bytes`、`weapon_trace_bake_catalog.bytes` 与 `contact_motion_bake_catalog.bytes` 已经使用 Deflate，本任务**仍按 §1 固定决策不压缩**。该取舍必须建立在实测数字上，不能靠感觉，因此记录如下：

按 §4 协议对 000003 现有 JSON 完整编码后实测（同卷 Temp 内离线复算，非估算）：

| 形态 | 字节 | 相对本轮旧 JSON |
| --- | ---: | ---: |
| 旧完整 JSON | 10,875,774 | 100% |
| v3 二进制（本任务交付） | 1,654,118 | 15.21% |
| v3 二进制 + Deflate(level 6) | 585,837 | 5.39% |
| v3 二进制 + Deflate(level 9) | 582,806 | 5.36% |

即不压缩时已达成 84.79% 减少，压缩还能再减少 64.7%。选择不压缩的理由是首版优先保证可逐字节校验、可直接定位、无解压分配尖峰；代价是 Socket Bake 会成为包内最大的生成载荷（约为同类 Hurtbox 载荷的 6.8 倍）。

因此：

- §P5 必须把上表实测值写入执行记录，使"不压缩"是一个被记录的决定，而不是未被检验的默认。
- 压缩作为**明确的后续项**留出：后续任务若引入压缩，只允许作为 `binaryVersion 4` 的新版本，不得原地改变 v3 语义；v3 读取器必须继续接受未压缩载荷。
- 若实施者认为应在首版纳入压缩，必须先取得用户确认并同步修订 §1、§3.2、§4、§7.1 的版本号，不得只改实现。

## 2. 已核实的基线和权威入口

### 2.1 当前文件与数据量

以下为本文件编制时的只读检查，执行 P0 时必须重新采集。

| 角色包 | 当前 Socket Bake JSON（B） | 轨道数 | 位置帧数 | 方向基帧数 |
| --- | ---: | ---: | ---: | ---: |
| 000001 | 98 | 0 | 0 | 0 |
| 000002 | 98 | 0 | 0 | 0 |
| 000003 | 10,875,774 | 106 | 14,274 | 12,588 |
| 000004 | 98 | 0 | 0 | 0 |
| 000008 | 98 | 0 | 0 | 0 |
| 000009 | 98 | 0 | 0 | 0 |

迁移前基线格式均为 `mx.character.socketBakeCatalog.v2`；当前产物已为 v3，见 §11.3。000003 单轨道最多 211 帧，位置帧连续，方向基与位置帧对齐；当前每条轨道的 socketPath 不变化，但新协议不能因此丢失对逐帧不同路径的表达能力。

迁移前生成的是缩进 JSON。`MxAnimationBakedVector3` 的 X/Y/Z 是 `long`，`SocketId` 是正整数。Socket Bake 除挂点外还服务攻击轨迹和权威姿态，不能只替换写文件方法而遗漏消费端。

按 §4 的固定结构计算，000003 二进制预计为 **1,654,118 B**（= 28 B 文件头 + 106×24 B 轨道头 + 14,274×52 B 位置帧 + 12,588×72 B 方向基帧 + 2,962 B 字符串表，29 个去重字符串），另加不足 8 KiB 的索引。二进制单独相对现有 JSON 约减少 84.79%。该值已用 §4 协议对现有 JSON 离线完整编码复现，逐字节吻合，因此不是结构估算；但性能与分配仍未实测，见 §P0 与 §10.3 的记录要求。

当前 000005 / 000006 / 000007 已退役。本任务不得恢复其源目录、生成目录或注册条目。

### 2.2 规则与文档

- 根 [AGENTS.md](../AGENTS.md)、[Packages 规则](../Packages/com.vincent.wgameframework/AGENTS.md)、[Assets 规则](../Assets/AGENTS.md) 及命中路径的子规则。
- [API 标准](API_STANDARDS.md)、[质量门禁](QUALITY_GATE.md)、[CharacterApplication 接口](Interfaces/CharacterApplication.md)。
- [原角色包导出优化方案](CHARACTER_PACKAGE_EXPORT_SIZE_OPTIMIZATION_PLAN.md)、[预览 GLB 制作侧任务及批量迁移记录](CHARACTER_PREVIEW_GLB_AUTHORING_ONLY_TASK.md)。
- 游戏行为验证遵循 [Agent 游戏创建指南](AGENT_GAME_CREATION_GUIDE.md) 和 [CharacterTest 开发标准](Guides/CHARACTER_TEST_GAME_DEVELOPMENT_STANDARD.md)。

本任务是目标设计；当前实现以源码和测试为准。实施完成后再同步公共接口文档，不提前把计划描述为已交付行为。

### 2.2.1 迁移后必然失效、必须在 §P5 同步的既有文档

以下文档当前把 `generated/character_socket_bake_catalog.json` 描述为完整数据的运行时权威。只改代码不改这些位置，会留下与实现相反的权威描述：

| 文档 | 失效内容 |
| --- | --- |
| [CharacterAction 接口](Interfaces/CharacterAction.md) | Baked Socket Capsule 的运行时权威路径描述 |
| [CharacterApplication 接口](Interfaces/CharacterApplication.md) | Unity Importer Bridge 产出的 Socket Bake 产物描述 |
| [角色系统数据架构复查规范](CHARACTER_SYSTEM_DATA_ARCHITECTURE_REVIEW_SPEC.md) | 产物清单表把该文件标为 JSON v1 并给出 `CharacterSocketBakeCatalog.cs` 的行号，行号必然漂移 |
| [原角色包导出优化方案](CHARACTER_PACKAGE_EXPORT_SIZE_OPTIMIZATION_PLAN.md) | 第 123 行仍把 Socket 描述为"独立 Socket JSON" |

`Tools/RuntimeHash/character_content_migration_outputs.json` 逐包记录了该 JSON 的 sha256/size。它是既有数字内容迁移的一次性快照，不是活门禁，**本任务不更新它**；若实施时发现它被当作活校验输入，停止并回报，不自行改写迁移证据。

### 2.3 必查实现入口

| 子系统 | 当前入口及修改要求 |
| --- | --- |
| 数据结构/现有 JSON | [CharacterSocketBakeCatalog.cs](../Packages/com.vincent.wgameframework/Character.RuntimeSpawn/CharacterSocketBakeCatalog.cs)：保留 Catalog 查询模型，将完整 JSON 从正式读取链路移除 |
| 二进制组织参考 | [CharacterHurtboxBakeCatalog.cs](../Packages/com.vincent.wgameframework/Character.RuntimeSpawn/CharacterHurtboxBakeCatalog.cs)：参考索引 + bytes，不照搬其压缩和错误处理 |
| Unity 生成/缓存 | [CharacterImportedPackagePrefabBuilder.SocketBake.cs](../Packages/com.vincent.wgameframework/Editor/CharacterImport/CharacterImportedPackagePrefabBuilder.SocketBake.cs)：写入、复用、输出指纹一起调整 |
| Builder 版本和结果 | [CharacterImportedPackagePrefabBuilder.cs](../Packages/com.vincent.wgameframework/Editor/CharacterImport/CharacterImportedPackagePrefabBuilder.cs)：版本失效及新增接受证据 |
| 正式回执 | [CharacterUnityEditorImportReceipt.cs](../Packages/com.vincent.wgameframework/Editor/CharacterImport/CharacterUnityEditorImportReceipt.cs)：receipt v3、双文件证据和发布前校验 |
| Unity 请求桥 | [CharacterPackageImportRequestBridge.cs](../Packages/com.vincent.wgameframework/Editor/CharacterImport/CharacterPackageImportRequestBridge.cs)：status 投影及异步同步入口 |
| 制作工具 Readiness | [EditorServer.Readiness.cs](../Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.Readiness.cs)：当前 session 和持久 receipt 两条路径均调整；关联 DTO 由引用搜索定位 |
| 战斗侧加载 | [CharacterTestBakedWeaponTraceSource.cs](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Combat/CharacterTestBakedWeaponTraceSource.cs)：`LoadSocketCatalog` 改为统一入口，不能忽略加载错误 |
| 地形姿态加载 | [CharacterTestAuthorityPoseTerrainSystem.cs](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestAuthorityPoseTerrainSystem.cs)：`Register` 改为统一入口，同时修改内容 hash 的来源 |
| Player 暂存 | [CharacterTestPackageTestCommand.cs](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Editor/CharacterTestPackageTestCommand.cs)：现有 json/bytes 收集保持，补成对预检和暂存后读取验证 |
| CLI 接受证据 DTO | [EditorServer.CharacterUnityImportEditor.cs](../Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/EditorServer.CharacterUnityImportEditor.cs)：`socketBake*` 扁平字段与 `CharacterUnityEditorImportReceiptSocketBake` 一起加字段，不能只改 Unity 侧 |
| CLI 测试 fixture | [CharacterPackageTests.EditorServer.cs](../Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/CharacterPackageTests.EditorServer.cs)：第 989 行直接把该 JSON 写成 `"{}"` 当作通过证据，必须换成合法 v3 索引 + bytes |
| 导入集成测试 | [CharacterGenericImportIntegrationTests.cs](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterImport/CharacterGenericImportIntegrationTests.cs)：断言 receipt 的 `socketBake` 计数，受 §7.1 口径影响 |
| 回执测试 | [CharacterUnityImportReceiptWriterTests.cs](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/CharacterImport/CharacterUnityImportReceiptWriterTests.cs)：构造接受证据并写入该 JSON，必须同步到 v3 双文件 |
| 生成物清单断言 | [CharacterTestProfilerGcRemediationTests.cs](../Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Game/CharacterTest/CharacterTestProfilerGcRemediationTests.cs)：第 946 行把该 JSON 列进文件清单，新增 `.bytes` 后清单需要同步 |

上述入口不是消费点白名单。实施前后都要搜索 `CharacterSocketBakeCatalogJson`、`character_socket_bake_catalog.json`、`SocketBakeHash` 和 `socketBake`，检查 Runtime、Editor、Authoring DTO、测试及文档的所有直接读取与判定点。编制本文件时按该搜索确认的完整命中集合即上表；表内任一行的归属或行号漂移时以实际搜索结果为准，不得因为表里没有就跳过。

## 3. 正式产物与小索引

### 3.1 路径与所有权

路径均相对于 `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/`：

| 文件 | 合同 |
| --- | --- |
| `generated/character_socket_bake_catalog.json` | 沿用旧路径，原位替换为小索引；保留其 `.meta` 和 GUID |
| `generated/character_socket_bake_catalog.bytes` | 唯一逐帧载荷；首次由 Unity 生成 `.meta`，后续保持 GUID |

两者的正式写入权威均为 Unity Socket Bake 生成器。CLI 不直接生成这两个文件，不从备份的旧 JSON 转换正式载荷。

`.bytes` 的 `.meta` 必须由 Unity 导入产生（`TextScriptImporter` 形态，与同目录既有的 `character_hurtbox_bake_catalog.bytes.meta` 一致），实施者不得手写 `.meta`、不得手工指定 GUID，这与 [Assets 规则](../Assets/AGENTS.md) 中"`.meta` 最好 Unity 生成"的约束一致。索引 `.meta` 沿用原位文件，不重新生成。

空 Catalog 同样输出合法索引和二进制头。不能以不存在 `.bytes` 表示空数据，也不能把解析失败解释为空 Catalog。仓库内已有同类先例：000003 的空武器轨迹载荷 `weapon_trace_bake_catalog.bytes` 就是只有文件头的 53 B，本任务的空包行为与之一致。

### 3.2 索引字段

固定以下字段及顺序，示例零值由实际输出替换：

```json
{
  "format": "mx.character.socketBakeCatalog.v3",
  "packageId": "000003",
  "binaryPath": "generated/character_socket_bake_catalog.bytes",
  "binaryVersion": 3,
  "binaryHash": "sha256:<完整二进制文件的 SHA-256 小写十六进制>",
  "binarySize": 0,
  "trackCount": 0,
  "frameCount": 0,
  "basisFrameCount": 0
}
```

- UTF-8 无 BOM、LF 换行、固定字段顺序、文件末尾一个 LF；索引可保留缩进。
- 不含时间戳、机器路径、随机值、完整路径列表、轨道数组或帧数组；总大小 ≤8 KiB。
- `binaryPath` 必须精确等于固定相对路径。读取器不接受任意绝对路径、`..`、URI 或其它文件名。
- `binaryHash` 覆盖二进制文件全部字节，包括头和字符串表；`binarySize` 为完整文件长度。
- 计数分别为轨道数、全部位置帧数、全部方向基帧数；必须与实际解码结果及二进制头一致。
- packageId 必须同时匹配调用方期望包和二进制字符串表中的 packageId。

## 4. 二进制协议 v3

### 4.1 通用约定

全部整数为 Little Endian；使用托管 `BinaryReader/BinaryWriter` 或等价显式实现，不使用结构体内存布局、unsafe 或平台相关直接拷贝。

字符串使用严格 UTF-8，不允许替换非法编码后继续。长度使用本节规定的 UInt32，禁止使用 `BinaryWriter.Write(string)` 的隐含字符串长度格式。

写入器从已经建立的 Catalog 读取数据。它必须拒绝重复键、空帧、非连续帧号、帧内 socketId 与所属轨道不一致、方向基不完整等输入，不得通过重排、截断、补零或降低精度掩盖问题。

### 4.2 文件头：28 B

| 顺序 | 字段 | 类型/值 |
| ---: | --- | --- |
| 1 | Magic | 四个 ASCII 字节 `MSB3`；UInt32 值为 `0x3342534D` |
| 2 | Version | UInt16，固定 3 |
| 3 | Reserved | UInt16，固定 0 |
| 4 | PackageIdStringIndex | UInt32 |
| 5 | StringCount | UInt32 |
| 6 | TrackCount | UInt32 |
| 7 | TotalFrameCount | UInt32 |
| 8 | TotalBasisFrameCount | UInt32 |

之后依次写字符串表和全部轨道。文件末尾没有隐含填充或额外扩展段。

### 4.3 字符串表

收集 packageId、所有 clipKey 和所有位置帧的 socketPath，按字符串 Ordinal 语义去重，再按 `StringComparer.Ordinal` 排序。

每项：`UInt32 UTF8ByteLength + UTF8Bytes`。所有字符串索引从零开始。

允许 socketPath 为空字符串，以保持现有构造语义；packageId、clipKey 必须非空。保留每帧 socketPath，不能假设轨道内路径永远相同。

### 4.4 轨道和帧

轨道按 `clipKey Ordinal → socketId 数值升序` 排列。相同 `(clipKey, socketId)` 不得出现两次。

每条轨道先写 24 B 轨道头：

| 字段 | 类型/约束 |
| --- | --- |
| ClipKeyStringIndex | UInt32 |
| SocketId | Int32，必须 >0 |
| QuantizationScale | Int32，必须 >0 |
| BasisQuantizationScale | Int32；无方向基为 0，有方向基必须 >0 |
| FrameCount | UInt32 |
| Flags | UInt32；仅 bit 0 为 HasBasis，其余位必须为 0 |

随后写当前轨道的位置记录，再写可选方向基记录，然后开始下一条轨道。

位置记录每帧 **52 B**：

1. `SocketPathStringIndex`：UInt32。
2. `Position.X/Y/Z`：三个 Int64。
3. `DeltaPosition.X/Y/Z`：三个 Int64。

有方向基时，每帧再写 **72 B**：Right、Up、Forward，按此顺序各三个 Int64。

帧号由位置下标恢复为 `0..FrameCount-1`；对应方向基使用同一下标。不得从 Position 重算 DeltaPosition，两个量都需要逐整数保存。

允许位置 FrameCount 为 0 的轨道；此时 HasBasis 必须为 false、BasisQuantizationScale 必须为 0。有方向基的轨道必须有位置帧且方向基数量完全相等。

### 4.5 限制与严格读取

| 项目 | 首版上限 |
| --- | ---: |
| 索引 UTF-8 字节 | 8 KiB |
| 二进制文件 | 256 MiB |
| 字符串数量 | 65,536 |
| 单字符串 UTF-8 字节 | 65,535 B |
| 字符串总内容字节 | 8 MiB |
| 轨道数量 | 4,096 |
| 单轨道位置帧 | 65,536 |
| 全部位置帧 | 1,048,576 |

方向基总数不得超过位置帧总数。每次分配前使用 `checked` 计算该计数需要的最小字节，并与实际剩余长度比较；不能先按未验证计数分配大数组。

读取必须检查：格式/版本/Magic/保留位、packageId、长度和 SHA-256、计数、字符串索引、轨道键顺序及重复、量化比例、方向基完整性，以及精确 EOF。

方向基复用现有长度、正交性、手性及分量范围校验；需要时提取共享内部校验函数，不能为新格式降低容差或删掉验证。

无论错误出现在第一条还是最后一条轨道，都必须拒绝整个新载荷。失败不得返回已加载的部分 Catalog。

至少提供以下稳定诊断码，错误文本包含 packageId、文件或轨道上下文，但不能依赖文本内容做逻辑判断：

| 诊断码 | 条件 |
| --- | --- |
| `SOCKET_BAKE_REIMPORT_REQUIRED` | 识别到旧完整 JSON 格式或旧接受证据 |
| `SOCKET_BAKE_FORMAT_UNSUPPORTED` | 未知索引/二进制版本 |
| `SOCKET_BAKE_PAYLOAD_MISSING` | 索引或载荷缺失 |
| `SOCKET_BAKE_IDENTITY_MISMATCH` | packageId 不匹配 |
| `SOCKET_BAKE_HASH_MISMATCH` | 声明/回执哈希与文件不符 |
| `SOCKET_BAKE_PAYLOAD_INVALID` | 错 Magic、截断、长度不一致或尾随字节 |
| `SOCKET_BAKE_LIMIT_EXCEEDED` | 计数、长度或计算溢出 |
| `SOCKET_BAKE_TRACK_INVALID` | 无效轨道、重复键、索引、帧或量化比例 |
| `SOCKET_BAKE_BASIS_INVALID` | 方向基不完整或数学约束失败 |

未来未知版本不能伪装成“重新导入旧包即可修复”，应明确要求匹配版本的工具/运行端。

## 5. 编解码 API 与消费端

### 5.1 最小接口

在现有 noEngine 程序集 `MxFramework.Character.RuntimeSpawn` 内新增 `CharacterSocketBakeCatalogBinary` 和目录加载入口；不新增程序集，不引入第三方序列化依赖。

目标签名：

```csharp
public static class CharacterSocketBakeCatalogBinary
{
    public const string Format = "mx.character.socketBakeCatalog.v3";
    public const string DefaultIndexRelativePath = "generated/character_socket_bake_catalog.json";
    public const string DefaultBinaryRelativePath = "generated/character_socket_bake_catalog.bytes";

    public static byte[] Serialize(string packageId, CharacterSocketBakeCatalog catalog);
    public static string SerializeIndex(string packageId, CharacterSocketBakeCatalog catalog, byte[] binary);
    public static bool TryLoad(string indexJson, byte[] binary, string expectedPackageId,
        out CharacterSocketBakeCatalog catalog, out string[] issues);
}

/// <summary>Actual verified product identity. Only produced by a successful load.</summary>
public readonly struct CharacterSocketBakeCatalogEvidence
{
    public string Format { get; }
    public string IndexHash { get; }
    public string BinaryHash { get; }
    public long BinarySize { get; }
    public int TrackCount { get; }
    public int FrameCount { get; }
    public int BasisFrameCount { get; }
}

public static class CharacterSocketBakeCatalogLoader
{
    public static bool TryLoadFromDirectory(string packageRoot, string expectedPackageId,
        out CharacterSocketBakeCatalog catalog,
        out CharacterSocketBakeCatalogEvidence evidence,
        out string[] issues);
}
```

`SerializeIndex` 从完整 bytes 计算哈希与长度，核对 packageId、头和 Catalog 计数；不接受调用方随意传入未经验证的哈希或大小。`TryLoad` 失败返回 false、catalog=null、至少一条带稳定诊断码的 issue。

目录加载成功时 `evidence` 只承载**实际重新计算并校验通过**的值，因此同时给出 `indexHash` 与 `binaryHash`，供 §7.1 回执和 §7.2 Readiness 直接使用，调用方不必再读一次文件重算哈希。失败时 `catalog=null`、`evidence` 为 `default`（各 hash 为空、`BinarySize=0`），调用方不得把 `default` 当作有效证据；判断有效性只看返回值，不看 evidence 字段是否非空。

读取文件前先检查长度上限；只打开包根下固定两个文件，对解析后的路径进行包根约束，不允许通过链接目录逃出包根。Runtime 不使用 `AssetDatabase`。

写入器面对非法 Catalog 使用明确异常阻断生成；调用方必须将异常转换为失败的 Socket Bake 结果，不发布成功回执。

### 5.2 保留的数据模型

保持现有 `CharacterSocketBakeCatalog`、`CharacterSocketBakeTrack`、`MxAnimationBakedSocketFrame`、`CharacterSocketBakeBasisFrame`、`PrepareClip`、`TryGetFrame` 和 `TryGetBasis`。

首版在加载阶段恢复相同对象图，因此不能预先承诺常驻内存按文件大小同比下降。逐帧查询保持现有方式；不在 Tick 中打开文件、解析索引、计算哈希或建立新缓存。

### 5.3 旧 JSON 和诊断

- 正式生成器不再调用原完整 JSON Serialize；正式消费者不再调用原 JSON Load。
- **删除 Runtime 程序集内的 `CharacterSocketBakeCatalogJson`（含 `Load` 与 `Serialize`）**，所有原调用点改为新 API，编译错误用于暴露遗漏。该类型是 `MxFramework.Character.RuntimeSpawn` 内的 public 类型，无法用 `internal` 隔离，因此只能整体移除；不移除就会出现"测试仍可读旧完整 JSON"的活口，与 §1 的强制重导入决策冲突。
- 旧 JSON 基线解析迁到**测试辅助代码**：在 CharacterTest 测试程序集内新建 `CharacterSocketBakeLegacyJson`（仅 `Load`/`Serialize` v2），只被测试与 §P0 基线使用，不进入任何生产程序集。§P0 步骤 5 与 §10.1 的"旧格式拒绝"测试都通过它构造输入。
- 方向基的数学校验（长度、正交性、手性、分量范围与容差）**提取为 Runtime 内的共享内部校验**（例如 `CharacterSocketBakeBasisValidation`），同时被 v3 读取器和测试侧 `CharacterSocketBakeLegacyJson` 调用。禁止为迁就新格式复制出第二套容差更松的校验。
- 门禁形式：源码扫描确认 Runtime/Editor 生产代码内不存在 `CharacterSocketBakeCatalogJson` 与 `mx.character.socketBakeCatalog.v2` 字样；唯一允许出现 v2 字样的位置是测试程序集与本节所述的 legacy 测试辅助类型。
- 完整诊断 JSON 的生成放在 Editor 诊断入口；读取新正式数据后按需导出，默认写入 `Temp/CharacterSocketBakeDiagnostics/`，任何指定输出目录均必须在 `Assets` 外。诊断导出是**从已加载 Catalog 重新序列化的新代码**，不复活旧 JSON 读取链路。
- 诊断入口使用 Editor 菜单 `MxFramework/Character/Diagnostics/Export Socket Bake JSON`，由用户在 Project 中选中生成包根目录后执行；输出文件名包含 packageId。没有有效选择时给出提示，不扫描/导出全部包。

### 5.4 正式加载与内容身份

战斗源 `LoadSocketCatalog` 和地形姿态 `Register` 均改用统一目录加载器。检查并移除 `Load(..., out _)` 形式的错误丢弃。

失败时在包加载/注册阶段记录一次可定位诊断，并使相应注册不可用；不要每帧重复打印，也不要把失败加入成功 Catalog 缓存。缺少权威姿态功能的角色仍按现有功能分支处理，不能强制为其创建姿态配置。

地形姿态当前对完整 JSON 文本计算 sourceHash。迁移后直接采用**实际校验过的 binaryHash**，沿用现有正 revision 派生规则。不得只哈希小索引文本或不经验证就信任索引声明。旧格式与新格式之间内容身份发生变化是本次硬迁移的一部分，不能在运行中的战斗会话中替换载荷。

## 6. Unity 写入、配对发布与缓存

### 6.1 发布流程

按包根做互斥，覆盖整个 Socket Bake 的复用、生成、写入和接受证据采集，防止同包重入；不用此任务引入新的通用任务调度系统。推广期间的 CLI/共享 Profile 更新继续串行协调。

`WriteSocketBakeCatalog` 的顺序固定为：

1. 完成原有采样，取得 Catalog。
2. 生成 bytes 和索引；在内存中用新读取器回读，逐字段与原 Catalog 比较。
3. 与包内**现有的索引和 bytes**（若存在）分别逐字节比较；两个文件都完全一致时不改内容、mtime 或 meta。现状只有一个 JSON，因此首次迁移必然发生一次写入；此后必须幂等。
4. 有变化时，在同卷 `Temp/CharacterSocketBakeMigration/<run>/<packageId>/` 准备临时文件、旧配对备份及包含路径/hash/GUID 的 manifest。
5. 暂停自动刷新期间发布：先替换二进制，再替换索引，最后恢复刷新；暂停/恢复必须位于 try/finally。发布前调用仓库已有的 `AssetDatabase.ReleaseCachedFileHandles()` 处理 Windows 映射句柄。
6. 通过目录加载器从正式路径重新校验配对；再导入/刷新正式两个资产，已有 meta 原样保留。
7. 验证成功后才发布缓存及 Socket Bake 成功证据；回执由原正式收口流程写入。

不删除旧 JSON 的 meta，不通过 DeleteAsset + 新建索引来替换身份；二进制存在时也不得删除其 meta 后重建。

### 6.2 非原子配对与失败恢复

两个固定路径的文件替换不是整体原子提交，不能声称“先写 bytes 再写 JSON”已经实现两文件原子性。

- 可捕获异常：从本轮 manifest 恢复被修改文件的旧配对；若原本没有 bytes，只移除本轮新建的 bytes 及其新建 meta。再次验证恢复结果。
- 进程崩溃：保留备份；不匹配的索引/载荷必须被 Loader、Readiness 和暂存门禁拒绝，下一次正式导入重建。
- 发布过程中已有 reader 观察到不匹配时返回失败，不读取部分数据。迁移操作不与 Play Mode 或同包运行会话并行。
- 恢复失败时保留全部证据，报告需重导入，不伪造成功，也不清理恢复材料。

### 6.3 缓存和版本

- Socket Bake 输入前缀由 `CharacterSocketBake.v2` 升级到 `CharacterSocketBake.v3`。
- 当前 BuilderVersion 为 `CharacterImportedPackagePrefabBuilder.v2.7`，本任务目标为 v2.8。若执行时该版本已被其他任务占用，应在最新源码上使用下一未占用版本，不得覆盖或降级其它任务的版本。
- `socket_bake` 缓存记录格式版本，以及索引和 bytes 各自的长度/hash；命中必须同时匹配两个文件、版本及索引/载荷关系。
- 只有一个文件、旧 JSON、篡改 bytes 或新格式头不匹配，均不得命中缓存。
- 空 Catalog 分支也使用相同写入和指纹机制，第二次导入不重复写空文件。
- 不清空整个 `Library` 来强迫迁移，不能把清缓存当作实现缓存失效的替代品。

## 7. 回执、Readiness 与 Player

### 7.1 接受证据合同

receipt 升级为 `mx.characterUnityEditorImportReceipt.v3`，schemaVersion 相应升级到 `3.0`。在现有 `socketBake` 中保留原字段，并增加：

| 字段 | 含义 |
| --- | --- |
| `format` | `mx.character.socketBakeCatalog.v3` |
| `indexPath` | 相对于工程根的正式索引路径 |
| `indexHash` | 索引文件完整字节的 SHA-256 |
| `binaryPath` | 相对于工程根的正式 bytes 路径 |
| `binaryHash` | bytes 完整字节的 SHA-256 |
| `binarySize` | bytes 长度 |

原字段的口径本次**不变**，实施者不得顺手重定义：

| 保留字段 | 口径 |
| --- | --- |
| `status` | 仍为 `Ready`/`Failed`；旧版格式一律不得为 `Ready` |
| `hash` | 仍是 Bake **输入**身份（`CharacterSocketBake.v3` 前缀），不是产物字节 |
| `requestCount` / `clipCount` | 请求数与不同 clipKey 数，语义不变 |
| `socketTrackCount` | `catalog.TrackCount`，即**位置轨道与方向基轨道合计**的轨道数 |
| `checkedFrameCount` | **全部位置帧数**，等于 Σ 每条轨道的位置帧数，也等于新索引的 `frameCount` |
| `issueCount` / `issues` | 语义不变 |

`checkedFrameCount` 容易误读，这里写死依据：生成器里位置采样循环只遍历 socket 轨道、方向基循环只遍历 basis 轨道，两者合起来对每条轨道恰好计一次，方向基帧在同一次迭代内一并采样但**不额外计数**。因此对 000003 有 1686（socket 轨道位置帧）+ 12588（basis 轨道位置帧）= 14274，与现有回执、缓存和 JSON 的 14274 完全一致。新索引的 `frameCount` 必须等于该值，这是一条可交叉校验的不变式；`basisFrameCount` 是独立计数，不得与 `checkedFrameCount` 混用。

当前 request/status 使用对应扁平字段：`socketBakeFormat`、`socketBakeIndexPath`、`socketBakeIndexHash`、`socketBakeBinaryPath`、`socketBakeBinaryHash`、`socketBakeBinarySize`。Unity build result、请求桥 status 投影、CLI DTO 一起调整，不能只修改 JSON 输出的一侧。

区分三种身份：

- 原 `SocketBakeHash` / receipt `socketBake.hash` 仍表示 Bake 输入身份。
- `indexHash`、`binaryHash` 表示实际产物字节。
- packageId + 五项 Compiler hash 继续用于匹配本次正式导入，不重新定义或手工改写。

发布 receipt 前执行完整新格式校验。缓存命中也不能仅凭原 `Ready` 字段跳过两个产物的接受检查。

旧 receipt v1/v2 可以识别并显示“需要重新导入”，不得被新 Socket Bake 的接受路径判定 Ready。禁止补填新字段假装已经通过验证。

### 7.2 制作工具的两条 Readiness 路径

同时更新持久 receipt 和当前 Editor session：

1. 要求新版格式、全部新增字段和固定包内文件路径。
2. 检查两个文件存在、长度合法、索引格式/packageId 合法。
3. 对文件流计算 SHA-256，核对 indexHash、binaryHash、binarySize 和索引中的声明。
4. 使用现有导入身份检查确认这些证据属于当前包和输入。
5. 任一缺失、旧版或不匹配均显示不可用与具体原因，不能只靠 `.json` 存在判断 Ready。

CLI 继续独立于 Unity：使用现有 JSON 和 .NET 文件/hash 能力核对接受证据，不引用 Unity 程序集，也不复制第二套完整帧解析器。完整结构校验由正式 Unity 接受步骤和运行加载器完成。

`compile_summary.exportLayoutVersion` 本次保持 4；该摘要是 CLI 导出说明，不充当 Unity Socket Bake 迁移门禁。正式完成状态由 v3 索引和 receipt 确认。

### 7.3 Player 收集

现有暂存器已收集 json 和 bytes，继续使用同一包内容机制，不额外增加一份 Resources 镜像或独立资源键。

- 暂存前校验新索引与 bytes；旧格式或配对失败阻止该包暂存。
- 暂存后从真实目标包目录调用统一加载器，确认能加载并且内容 hash 相同。
- 保持按 packageId 计算目标目录，不能覆盖另一个角色的暂存内容。
- 清单必须同时包含索引和 bytes；完整诊断 JSON 不得进入暂存或构建输入。
- 检查正常 Player 暂存调用者和任何直接选择文件的工具入口，避免只有一个 UI 路径带校验。

本任务必须验证暂存与实际目标目录读取；不强制构建全部 AssetBundle/Player。未构建成品时，报告必须明确边界。

## 8. 分阶段执行与停点

### P0：基线和恢复材料

1. 读取路径规则，检查 dirty worktree 与并行任务；不 reset、stash 或覆盖无关改动。
2. 重新确认六个活跃包和源目录。若名单已变，依据源和注册表记录实际名单，不复活退役角色。
3. 在 `Temp/CharacterSocketBakeMigration/<run>/` 保存逐文件清单、原 Socket Bake/meta、receipt、必要配置和共享 Profile 的恢复材料。
4. 保存作者源文件 hash、源模型/贴图 hash、主/Runtime Prefab GUID，以及每个生成包的总字节。
5. 用测试侧旧格式解析器建立完整语义基线；记录 track/frame/basis 数量、所有数值和路径。旧格式加载有 issue 时先查清，不以失败数据作为对照真值。
6. 在代码切换前测旧 JSON 加载：预热一次，再在相同进程测十次，记录中位耗时和线程分配字节；完整读取与仅解码两组分开报告。

**退出条件：** 基线和备份可读取，000003 原数据无解析错误，当前无同包/共享 Profile 的在途导入。没有基线就不能进入正式迁移。

### P1：二进制和测试

实现 §3～§5 的编解码器、限制和测试；先使用隔离 fixture，不改正式资产。建立手工小样本 golden bytes，防止写入器和读取器犯相同错误而 round-trip 仍通过。

**退出条件：** 精确等价、确定性、数值边界、损坏输入和旧格式拒绝测试均有实际通过结果。

### P2：完整链路接入

同时接入生成器、缓存、运行消费者、内容 hash、request/status、receipt、Readiness、诊断出口及 Player 暂存。更新相关 fixture 到新格式，不能通过删测试或放宽断言消除失败。

执行真实 Unity 编译门禁和聚焦测试；CLI 改动运行命中的 .NET Readiness/receipt 测试。只有修复测试发现的问题才扩大范围，不启动与任务无关的全仓检查。

**退出条件：** 编译通过，隔离包可从生成到暂存闭环；旧格式得到明确重导入错误；任何一个产物缺失都不能 Ready。

### P3：000003 试点

1. 停止使用该包的 Play Mode/测试会话，串行执行正式 CLI 导入和 Unity 同步。
2. 通过正式请求桥等待 `completed`，核对新回执；不能根据文件 mtime 或 MCP 返回超时推断成功/取消。
3. 用新加载器读取结果，与 P0 对照逐轨道、逐帧、逐整数比较；包含所有方向基和路径。
4. 检查小索引、唯一 bytes、正式 Prefab/Avatar/Bake/Registry 和 GUID。
5. 运行自然攻击、权威姿态与地形姿态相关回归。
6. 再完整导入一次，检查索引/bytes 的内容、mtime、GUID 稳定；receipt 允许在内容相同时保持旧 mtime，只以正式完成证据证明重验。
7. 在隔离暂存目录调用生产暂存方法，再从实际目标目录读取；不得写入其它正式包的镜像。

**退出条件：** 语义完全相等，索引 + bytes ≤本轮旧 JSON 基线的 25%，正式回执 v3 Ready，第二次导入不重写 Socket Bake 配对文件，行为回归通过。

### P4：其余活跃包

按 `000001 → 000002 → 000004 → 000008 → 000009` 串行执行导入、同步、接受、二次幂等和暂存验证。空 Catalog 必须有合法载荷且读取成功，不能省略其验证。

最后全量扫描活跃包，确认不存在旧完整 Socket Bake JSON。失败包不得记为“已推广”；先修复失败再继续。

### P5：交接收口

更新本文件末尾执行记录、CharacterApplication 公共接口和受影响的导入/开发指南，附实测体积、语义比较、测试作业、requestId、GUID 和原有警告的结果。

将恢复资料留在本地，不写入 Assets 或版本化文档。默认不提交、不推送。

## 9. 可复用的运行与取证入口

### 9.1 独立构建 CLI

避免覆盖正在运行的 Editor Hub 使用的 authoring.dll。由工程根执行以下命令，将本轮产物放入本地 Temp：

环境要求：必须用 PowerShell（或任何 `APPDATA` 已就绪的 shell）。在 Git Bash 下 `APPDATA` 为空会让**所有** dotnet restore 与 `dotnet nuget list source` 失败并报 `Value cannot be null. (Parameter 'path1')`，与代码无关；遇到该错误不要改代码，换 shell 或显式补全 `APPDATA`/`LOCALAPPDATA` 后重试。

```powershell
dotnet build Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj --artifacts-path Temp/CharacterSocketBakeMigration/build --nologo
```

之后使用本次实际构建输出的 `authoring.dll`。当前 SDK 的路径形态为 `Temp/CharacterSocketBakeMigration/build/bin/MxFramework.Authoring.Cli/debug/authoring.dll`，执行前验证存在且来自本次构建。

导入单包示例；每次执行使用独立 report-out，不覆盖前次证据：

```powershell
dotnet Temp/CharacterSocketBakeMigration/build/bin/MxFramework.Authoring.Cli/debug/authoring.dll character import-unity --package Authoring/CharacterPackages/character-000003 --project-root . --build-target StandaloneWindows64 --check-files --check-hashes --report-out Temp/CharacterSocketBakeMigration/run-001/000003/cli-1
```

CLI 完成只代表 CLI 阶段，不能作为 Socket Bake 完成证据。若执行预检，增加 `--dry-run`，并使用独立报告目录。

### 9.2 Unity 同步请求桥

正式入口是 `Temp/MxFrameworkAuthoring/CharacterImport/rebuild_request.json`。请求至少包含：

```json
{
  "requestId": "<本次唯一请求 ID>",
  "createdUtc": "<UTC ISO-8601>",
  "package": "Authoring/CharacterPackages/character-000003",
  "generatedRoot": "Assets/MxFrameworkGenerated/CharacterPackages",
  "packageId": "000003",
  "sourcePackageHash": "<当前正式 import_report 值>",
  "generatedConfigHash": "<当前正式 import_report 值>",
  "geometryBindingHash": "<当前正式 import_report 值>",
  "resourceMappingHash": "<当前正式 import_report 值>",
  "writePlanHash": "<当前正式 import_report 值>"
}
```

五项 hash 只复制当前正式导入报告的值，不从旧基线取值，不自行重算。写请求前确认没有待消费请求和已知运行中的导入；存在时等待并协调，不覆盖。

先写唯一临时请求，再重命名到固定入口。轮询 `Temp/MxFrameworkAuthoring/CharacterImport/status/<requestId>.json`，读取 `completed / failed` 和错误。长调用超时不构成新提交同包请求的理由，先检查已有 status。

### 9.3 Unity 编译验收

按根规则优先调用 `execute_custom_tool(tool_name="validate_unity_scripts", parameters={})`。必须有 refresh completed、compilation started/finished、domain reload observed、Editor ready 和 Console error=0 的完整证据。

连接因 domain reload 断开时查询既有验证作业状态，不反复触发新的编译。只有工具不可用或明确失败才分步回退。`.csproj` 构建不能替代真实 Unity 编译。

## 10. 测试矩阵与验收标准

### 10.1 编解码与失败测试

| 场景 | 必须断言 |
| --- | --- |
| 手工 golden bytes | 头长度、大小端、字段顺序、字符串表及逐帧字节符合 §4 |
| 空 Catalog/零帧轨道 | 正常读取；有 basis 的零帧轨道拒绝 |
| 位置/方向基 | 每个坐标、位移、基向量、scale、ID 和路径逐值相等 |
| 数值范围 | 负数、零、超 Int32 合法位置值、Int64 边界值不截断；basis 仍受数学范围限制 |
| 可变路径 | 同轨道不同帧路径正确恢复 |
| 注册顺序 | 打乱 Catalog 插入顺序，bytes、索引及 hash 完全相同 |
| 旧/未知格式 | v2 明确要求重导入；未来版本明确不支持，无自动回退 |
| 文件与身份 | 缺文件、错 packageId、错误大小/hash、错 Magic、截断、尾随数据全部失败 |
| 数量与结构 | 超上限、乘法溢出、坏字符串索引、非法 UTF-8、重复/乱序轨道、空帧均拒绝 |
| 方向基 | 错数量、缺失、非法分量、非正交或错误手性全部拒绝，不返回部分成功 |
| 成对一致 | 索引来自 A、bytes 来自 B 时不可读取或 Ready |

测试必须包含“已经成功解码前几条轨道，最后一条损坏”，确认失败结果不泄漏部分 Catalog。

### 10.2 链路测试

- 真实生成器无变化时跳过两文件写入；只篡改一个文件时缓存失效。
- 在发布 bytes 前、bytes 后索引前、索引后接受前注入失败，验证恢复或阻断。
- 旧 receipt、缺字段、缺载荷、改载荷、改索引分别使持久 receipt 和当前 session 失效。
- 清理旧格式不误删 meta；索引 GUID 保持，bytes GUID 在二次生成保持。
- 生产暂存清单包含两个文件，暂存后加载一致；其它包的暂存内容保持。
- 正式 `CharacterUnityImportReceiptWriterTests` 与 CLI Readiness 对照用同一组接受字段和失败案例。
- 源代码扫描确认没有生产 `CharacterSocketBakeCatalogJson.Load` 或直接读完整 JSON 的遗漏。

相关现有测试入口包括 `CharacterAuthorityPoseRigBindingTests`、`CharacterRuntimeSpawnFirstSliceTests`、`CharacterTestDragon14NaturalAttackPlayModeTests`、`CharacterTestDragon14AuthorityTerrainPosePlayModeTests`，以及 AuthorityPose Consumer/Terrain/Performance 测试。准确测试名以执行时源码为准，记录本次实际选择和结果。

### 10.3 完成判据

以下全部满足才可把执行状态改为完成：

- [x] 当前全部活跃包都使用 v3 索引 + 唯一 bytes；旧包明确不可用并提示重导入。
- [x] 所有有效数据与 P0 基线逐整数、逐路径等价，采样与运行语义未变。
- [x] 000003 的索引 + bytes 总长度 ≤ §P0 实测旧 JSON 基线的 25%。以编制时基线 10,875,774 B 计即 ≤2,718,943 B；若 P0 重新采集后基线不同，以 P0 值为准并记录两个数字，不沿用本文档的参考值。
- [x] 所有索引 ≤8 KiB；空包存在合法二进制且获得成功回执。
- [x] 六包均有正式 completed 请求和 receipt v3，五项 Compiler hash 匹配。
- [x] 第二次导入不重写未变化的 Socket Bake 文件，其内容、mtime 和 GUID 均稳定。
- [x] 本任务未改写作者源、模型/贴图或既有正式资产 GUID；未覆盖共享 Profile 中其它任务内容。并行 000009 变更另行保留，见 §11.5。
- [x] Unity 编译门禁通过，聚焦测试有实际通过数量；`total=0` 不算通过。
- [x] 攻击/权威姿态/地形姿态聚焦行为回归通过；原有失败明确区分且不能替代相关必要验收。
- [x] Player 暂存与实际目标路径加载通过，诊断 JSON 不在发布输入中。
- [x] 记录新旧加载中位耗时、线程分配和非 meta 总体积，不作未经测量的帧率/常驻内存结论。
- [x] 文档、接口与操作说明同步，恢复材料留存，本任务未擅自提交或推送。

体积门槛未达成时先检查是否漏删完整帧 JSON、重复载荷、路径未去重或格式未生效，不得擅自降低精度、删除 basis 或修改角色内容来达标。

## 11. 回滚与交付材料

### 11.1 回滚边界

新 Runtime 强制新格式，因此单独恢复旧 JSON 不会恢复可运行状态。

- 正式切换前失败：恢复本次改动的代码与产物配对，保留原有无关工作；不使用全仓 reset。
- 单包生成失败：优先修复并重导入，不能将旧 JSON 写回后继续宣称该包 Ready。
- 必须整体回退时：协调停止相关会话，恢复匹配的旧读取/生成版本、所有受影响包产物及 receipt；重验后才恢复使用。
- Profile、Prefab 或其它生成物仅在确实由本轮改动且有对应备份时恢复，不覆盖迁移期间其它任务的新内容。

### 11.2 最终执行记录模板

实施者在此节追加实际记录，保留设计与实测的区别：

| 项目 | 待填写 |
| --- | --- |
| 执行日期/源码基线 | 未执行 |
| 新格式和 BuilderVersion | 未执行 |
| 实际迁移包名单 | 未执行 |
| 000003 旧 JSON / 新 index / 新 bytes / 净减少 | 未执行 |
| 轨道/位置帧/方向基等价结果 | 未执行 |
| 六包 requestId / receipt 结果 | 未执行 |
| 两次导入内容/mtime/GUID 对照 | 未执行 |
| Unity 编译作业与 Console | 未执行 |
| EditMode / PlayMode / Authoring 测试数量和结果 | 未执行 |
| Player 暂存和读取结果 | 未执行 |
| 实际 Player/AssetBundle 成品构建 | 未执行；未做时明确写未验证 |
| 加载耗时/分配量/测量口径 | 未执行 |
| 原有警告与新增问题 | 未执行 |
| 本地备份/完整证据目录 | 未执行 |
| 剩余事项 | 待实施本任务全部阶段 |

交付内容应包括任务源码、测试、公共接口说明和正式生成资产及 meta。`Temp`、`Library`、备份、测试完整输出和本机状态不进入版本化内容。

### 11.3 P0～P4 阶段执行记录（2026-09-21，最终收口见 §11.5）

状态：**P0 / P1 / P2 生产链路 / P3 / P4 已完成。** 详细证据见
`Temp/CharacterSocketBakeMigration/run-001/`（`P0_RECORD.md`、`P1_P2_P3_RECORD.md`、`manifest.json`、`baseline/`）。

| 项目 | 实测 |
| --- | --- |
| 执行日期/源码基线 | 2026-09-21，HEAD `0d45cf731` |
| 新格式和 BuilderVersion | `mx.character.socketBakeCatalog.v3`；`CharacterImportedPackagePrefabBuilder.v2.8`；回执 v3 / schemaVersion 3.0 |
| 实际迁移包名单 | **全部六个**：000001 / 000002 / 000003 / 000004 / 000008 / 000009 |
| 000003 旧 JSON / 新 index / 新 bytes / 净减少 | 10,875,774 B / **352 B** / **1,654,118 B** / 总 1,654,470 B = **15.21%**（门槛 25% PASS） |
| 轨道/位置帧/方向基等价结果 | 106 / 14,274 / 12,588；迁移后语义转储与 P0 基线 **diff 行数 = 0** |
| 六包 requestId / receipt 结果 | 六包全部 completed 且 receipt v3 Ready（含二次幂等轮）；000003 首轮 `c3dab4dd05d4400ea39ced6d98e465bb` |
| 两次导入内容/mtime/GUID 对照 | 六包全部 **完全一致**（幂等 PASS）；旧 `.json.meta` 未重建 |
| Unity 编译作业与 Console | `validate_unity_scripts` passed（refresh/compile/domain reload/ready，Console error=0） |
| EditMode 测试数量和结果 | 新增 `CharacterSocketBakeBinaryCatalogTests` **33/33 passed**（含源码扫描门禁）；暂存组 **5/5 passed**；CLI `dotnet build` 0 warning 0 error |
| Player 暂存和读取结果 | **已验证**：暂存函数内置成对预检 + 暂存后从真实目标目录回读并比对 indexHash/binaryHash；5 个暂存测试全部通过（含真实包 000001 与双包隔离） |
| 实际 Player/AssetBundle 成品构建 | **未验证**（按 §7.3 不强制构建成品）；已验证到"暂存 + 从实际目标目录读取"这一步 |
| 加载耗时/分配量/测量口径 | 旧 JSON：仅解码中位 540.35 ms、读取+解码中位 553.26 ms。**新加载器（含 IO + SHA-256 校验）中位 32.05 ms**（min 31.44 / max 40.44），约 17× 更快；其中仅解码 17.51 ms、SHA-256 12.34 ms；保留内存 1.59 MiB。逐线程分配字节仍不可得（`GC.GetAllocatedBytesForCurrentThread` 与 `GC.GetTotalAllocatedBytes` 在本运行时均不可用） |
| 原有警告与新增问题 | 既有失败已区分（几何绑定 v7/v8、`invalidTokenCount` 陈旧断言等，均与本任务无关）；本任务直接导致的失败仅"未迁移包不可用"与三个暂存 fixture |
| 本地备份/完整证据目录 | `Temp/CharacterSocketBakeMigration/run-001/`（114 个备份文件 + 6 份语义基线 + 记录） |
| 本阶段剩余事项 | 当时尚缺必要行为回归、公共文档同步和有效分配量测量；成品构建按 §7.3 非必需。后续收尾以 §11.4 为准，不再使用“唯一未做项”结论。 |

P4 已完成，六个活跃包的 Socket Bake 均通过运行加载器验证（000003 = 106 轨道 / 14,274 帧；其余五包为合法空 Catalog）。

### 11.3.1 P4 实测（六包）

| 包 | index B | bytes B | 合计 B | 轨道 | 位置帧 | 方向基帧 | 回执 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 000001 | 337 | 38 | 375 | 0 | 0 | 0 | v3 Ready |
| 000002 | 337 | 38 | 375 | 0 | 0 | 0 | v3 Ready |
| 000003 | 352 | 1,654,118 | 1,654,470 | 106 | 14,274 | 12,588 | v3 Ready |
| 000004 | 337 | 38 | 375 | 0 | 0 | 0 | v3 Ready |
| 000008 | 337 | 38 | 375 | 0 | 0 | 0 | v3 Ready |
| 000009 | 337 | 38 | 375 | 0 | 0 | 0 | v3 Ready |

- 空包载荷恒为 38 B（= 28 B 文件头 + 4 B 长度 + 6 B packageId），合法且可读，非"缺文件"表示。
- 全量扫描：**不存在**旧完整 Socket Bake JSON；六包索引均无 BOM、无 CRLF、单一结尾 LF、≤8 KiB。
- 六包回执 `indexHash` / `binaryHash` / `binarySize` 均与实际文件逐字节核对一致；`checkedFrameCount == 索引 frameCount`。

### 11.3.2 旧 JSON 解析器的最终移除（§5.3 落实）

`CharacterSocketBakeCatalogJson` 已从 `MxFramework.Character.RuntimeSpawn` **删除**；v2 的 `Load`/`Serialize`
原样迁到测试程序集 `CharacterSocketBakeLegacyJson`（`Assets/.../Tests/EditMode/CharacterRuntimeSpawn/`），
仍复用 `CharacterSocketBakeBasisValidation`，因此退役格式不可能与 v3 读取器产生容差漂移。

新增源码扫描门禁 `ProductionSources_ContainNoLegacyFullJsonSocketBakeReader`：
扫描 `Packages/com.vincent.wgameframework` 与 `Assets/Scripts/WGameFrameworkProject` 的非测试 `.cs`，
断言 ①不存在 `CharacterSocketBakeCatalogJson` 字样；②`mx.character.socketBakeCatalog.v2` 只允许出现在
`CharacterSocketBakeCatalogBinary.cs` 的旧格式探测表中。生产侧"只能读新格式"由编译期 + 门禁共同保证，而非仅靠约定。

### 11.3.3 实施期发现并修复的缺陷（P3 之后）

索引曾按 Newtonsoft 缩进默认换行输出为 **CRLF**，违反 §3.2 的 LF 约定，并会让 `indexHash` 变成平台相关
（Windows 生成的索引与 macOS/Linux 不同，跨平台导入会导致 Readiness 哈希不匹配）。已改为显式归一化为 LF，
新增断言（无 `\r`、恰好一个结尾 LF），并对六个包重新迁移验证。


### 11.4 首次收尾复核记录（2026-09-21；当时等待窗口，现已由 §11.5 完成）

本节保留第一次收尾的历史状态；以下“等待/待验收”已在 §11.5 关闭。本节取代 §11.3 的“只剩成品构建”判断。成品构建按 §7.3 是允许明确留白的验证边界；有效分配量和命中行为/接受测试仍需要证据。

本轮完成的修正：

1. 用正式加载器读取 P0 的真实旧包备份复现：由于旧包没有 bytes，原先先返回 `SOCKET_BAKE_PAYLOAD_MISSING`，没有进入旧格式提示。调整为先识别旧索引，再检查新格式 companion；测试覆盖小型空旧包、大旧包，以及带残留 bytes 的旧包，全部要求返回 `SOCKET_BAKE_REIMPORT_REQUIRED` 且不泄漏 Catalog/evidence。
2. 发现 CLI Readiness 原实现只要求 Socket Bake 哈希字段非空、产物存在，没有实际重算文件哈希。新增独立 .NET 文件见证校验，当前 session 与持久 receipt 均核对固定包内路径、索引身份/version、长度、indexHash、binaryHash 及索引内的声明；不引入 Unity 依赖或第二套帧解码器。
3. 补上 Unity receipt 发布前的正式目录加载和与 build result 证据的比对。不能在文件被改写后凭旧 result 再发布 Ready。接受测试 fixture 改用合法空 v3 配对，新增同大小载荷篡改和仅改索引的拒绝用例；该批 Unity 修改等待最终复验。
4. 更新 AuthorityPose 的聚焦测试夹具：当前 geometry v8、rig v2、合法 SHA-256 测试摘要；真实 Dragon14 Catalog 通过新目录加载器读取。没有放宽攻击、姿态或校验断言，也没有新增旧格式 Runtime 回退。
5. 公共 CharacterApplication/CharacterAction 接口、产物架构表、原导出优化方案及项目索引已同步小索引 + bytes、receipt v3 和硬迁移边界。

#### 已获得的本轮证据

| 检查 | 实际结果 |
| --- | --- |
| 加载器修复后的 Unity 编译 | 作业 `581001e0eace4e878e31af89b4cb3e5c`，2 assemblies，完整 refresh/compile/reload/ready 证据，Console 0 error |
| 新旧包诊断/编解码 | `CharacterSocketBakeBinaryCatalogTests` **35/35 Passed**（旧 33 条加两个参数组合）；作业 `17b068d5de7743129ebecb62a9a7b695` |
| 同一轮其它聚焦 EditMode | RigBinding 2 passed / 3 failed；Consumer 9 failed、Terrain 7 failed、Performance 2 failed。失败定位到旧协议、旧读取入口或非法摘要夹具；修正后的重跑尚未获得，不以这些失败冒充通过 |
| 正式 PlayMode 行为 | `ae0fccb4a4f847cfa032ee3a3a270c60`：自然攻击组 **3/3**、权威地形姿态组 **3/3**，总 **6/6 Passed**，0 failed/skipped；涵盖攻击动作、伤害去重、正式骨骼表现、地形确定性/失败矩阵与同帧消费 |
| Authoring 构建 | 独立输出目录 `Temp/CharacterSocketBakeCloseout/build/`，0 warning / 0 error |
| Authoring 接受测试 | `character-unity-import-receipt` 通过，覆盖 v1/v2 重导、正常 v3、两条 Readiness 路径的索引/同大小 bytes 篡改和恢复 |
| 六个正式 Socket Bake 配对 | indexHash / binaryHash / binarySize 与当前各自 receipt 逐文件一致，体积未变；本轮没有重新生成六包 |
| 文档引用 | 本轮更新文档的相对文件链接均可解析 |

以上编译通过证据对应加载器修复，**不覆盖后来新增的 Unity receipt 发布前复验及全部 fixture 修改**。并行任务曾造成 `RenderDeckTray` 编译中间态及反复进入 Play Mode；不能把共享 Editor 当时的编译失败归入 Socket Bake，也不能以随后未开始运行的 `total=0` 作业作为通过证据。

#### 分配量与共享窗口

`GC.GetAllocatedBytesForCurrentThread` 的 8 KiB 正向对照仍返回 0。`ProfilerRecorder` 的 `GC.Alloc` 标记在当前环境的 UnitType 为 **TimeNanoseconds**，读取到的 Value 不能冒充分配字节。已确认可以通过 Editor 的 RawFrameDataView 读取采样 metadata，后续需在稳定验证窗口中以正向对照校准后采集分配字节。

用户明确告知并行任务仍在使用 Unity，要求先完成不依赖 Editor 的工作。因此停止主动 Unity 操作，保留以下待验收项：

- 最新 Unity 源码的完整编译门禁；RigBinding/Consumer/Terrain/Performance、receipt writer、暂存和编解码聚焦测试重跑。
- 使用 Unity Profiler 原始 GC.Alloc 样本 metadata 完成有效分配量测量；不使用 GC 保留内存、标记耗时或恒零计数器代替。
- 根据最终结果更新 §10.3 勾选项和本节状态。目前不声明 P5 已完成。

证据目录为 `Temp/CharacterSocketBakeCloseout/`：`editmode-before.xml`、`playmode.xml`、`socket-pairs.json`、`protected-before.json`、`protected-check.json` 及独立构建输出。期间观察到 000009 作者源和生成配置有并行改动，本轮未修改或回退这些内容；不能把跨会话整体目录变化当作本轮迁移净收益。


### 11.5 最终收口（2026-09-21，P5 完成）

用户释放共享 Unity 验证窗口后，已完成最新源码编译、相关回归、实际产物读取以及有效分配量采集。本节是当前完成状态；§11.3/§11.4 为阶段记录，不能再据其旧待办判断当前状态。

#### 修复及最终测试

- 旧包诊断先识别旧 JSON，再检查新格式 bytes；真实备份复现与空/大旧包测试均覆盖。
- CLI 当前 session 和持久 receipt 均重算双文件 hash，检测同大小载荷篡改与索引修改；Unity receipt 发布前用生产 Loader 校验配对，并匹配 build result 的 indexHash、binaryHash、大小、轨道及帧数。
- Rig/Consumer/Terrain 测试 fixture 更新到当前数字协议：使用创建的 1/2/3 通道及第二肢体通道 5；次级姿态 snapshot 带齐 region/channel/socket 身份；表现层通过显式数字挂点绑定测试，不再依赖按骨骼名称猜测。原攻击、同帧消费、失败关闭、姿态回退及位置断言均保留。
- 回执 fixture 使用合法空 v3 配对，计数断言与该实际空样本一致，保留 Generic Avatar、多材质及原子替换验证；没有通过忽略错误或恢复旧 Runtime Reader 使测试通过。

最终 Unity 编译门禁 `d7da58b39459464f8bc1a87d8c14a8bc`：refresh completed、compilation started/finished、domain reload observed、Editor ready、Console error=0，9 个程序集编译，0 warning。包括最终写入器修正；此前门禁 `0da2ef18b6a94513978d4666054ea69e` 也通过。

最终 EditMode 作业 `fa1f4a566d2f496295176a978d7b1844`：**73/73 Passed，0 failed / skipped**，总计约 27.15 s：

| 组 | 通过数量 |
| --- | ---: |
| Socket Bake 二进制/旧包诊断/源码扫描 | 35 |
| AuthorityPose RigBinding | 5 |
| AuthorityPose Consumer | 9 |
| AuthorityPose Terrain | 7 |
| AuthorityPose Performance | 2 |
| Unity import receipt writer / 篡改拒绝 / 原子替换 | 10 |
| Player 暂存 / 双包隔离 / 路径与身份 | 5 |

发布故障注入补测作业 `e88a801cfda345a0b097e79beb7b2566`：**6/6 Passed**，与上表合计 **79 项 EditMode 均通过**。覆盖：正常发布后两个 Unity 资产可见且都有 GUID、无变化不写入、BeforeBinary/AfterBinary/AfterIndex 三阶段异常恢复旧配对及 meta、新建失败无残留，以及同包重入不覆盖恢复材料。

补测前发现写入器曾向 `AssetDatabase.ImportAsset` 传入包内相对路径 `generated/...`，缺少包根。已改为完整工程相对资产路径，并增加同包发布重入守卫；故障注入入口为 internal 测试接口，正常生产路径不注入回调。正式六包没有因此重新生成，既有索引/bytes/meta 内容保持。

正式 PlayMode 作业 `ae0fccb4a4f847cfa032ee3a3a270c60`：自然攻击 3/3、权威地形姿态 3/3，**6/6 Passed**。这轮真实行为证据与最终 EditMode 配对；后续修改限于接受门禁与测试 fixture，没有改变运行攻击/地形逻辑。

Authoring 的 `character-unity-import-receipt` 聚焦套件通过；独立构建 0 error / warning。该结果包含正常接受、v1/v2 重导、索引篡改、同长度 bytes 篡改和当前会话/持久回执两个分支。

#### 有效分配量测量

环境为本机 Unity 6000.5.0f1 Editor，主线程，先预热真实旧解析器与新生产目录加载器，再采集 **10 次有效配对样本**。新路径包含文件 IO、SHA-256 和解码；旧路径包含文件 IO 与旧 JSON 解码。

采集方法：打开 Editor Profiler，在 `ProfilerMarker.Auto()` 区间内执行单次加载，然后停止录制；读取该区间子样本中 `GC.Alloc` 的 `Size` metadata，确认其单位为 Bytes 后求和。每轮正向对照 `new byte[8192]` 都得到 **8,224 B / 1 次分配**。空采样不是零分配，未计入有效样本并已重采。CodeDom 中带条件编译的 `Profiler.BeginSample` 调用不能作为此采样入口；`ProfilerRecorder` 的 GC.Alloc 耗时 Value 也未被当作字节。

| 完整加载路径 | 累计分配字节中位数 | 十次有效样本范围 | 分配事件数/次 |
| --- | ---: | ---: | ---: |
| 旧完整 JSON | 239,335,942 B | 239,335,942～239,335,942 B | 3,459,075 |
| v3 索引 + bytes，含完整哈希验证 | 4,291,926 B | 4,291,926～4,291,926 B | 27,802 |

累计分配量减少 **98.21%**。这是主线程加载区间的分配量，**不是常驻内存或 GC 后保留内存**。原 §11.3 的“保留内存 1.59 MiB”不作为该结论依据。现有 Tick 性能测试使用的 GC 计数器在此运行时不能提供有效字节证据，因此本任务不从其零值另作“已证明零 GC”的扩展结论。

本轮开启 Profiler 后的配对耗时中位数为旧路径 797.90 ms、新路径 34.09 ms，含采样开销，仅供复核；不能与此前未开启 Profiler 的 553.26 / 32.05 ms 混为同一测量。文件体积仍为 10,875,774 → **1,654,470 B**，保持 15.21% / 减少 84.79%。

#### 正式产物和边界

- 最终用生产目录 Loader 读取全部六包，全部成功、issues 为空；000003 为 106 轨道，其余为合法空 Catalog。
- 六包当前 receipt 均 ready，索引/二进制哈希与实际文件一致，五项 Compiler hash 与各自当前 import report 一致。
- 与首次收尾前清单比较，六包 Socket Bake 的索引、bytes 和现有 meta 内容无变化；本轮没有为了测试重新迁移或重写正式配对。
- 并行任务期间修改过 000009 的源模型、制作配置及对应生成配置；已记录在 `protected-check.json`，本任务没有覆盖、回滚或将其体积变化计入收益。`TacticalCardDemoScene` 的并行改动同样保留。
- Console 最终读取为 0 error。Editor 留在 Edit Mode，Profiler.enabled 与 profileEditor 均恢复 false。
- **实际 Player / AssetBundle 成品未构建**。已验证到生产暂存及真实目标目录加载，符合 §7.3；不宣称成品压缩率、平台运行或全项目测试全部通过。
- 未提交、未推送；本任务必要收尾项已完成，没有剩余强制验收项。

最终证据在 `Temp/CharacterSocketBakeCloseout/`：`editmode-final.xml`、`publisher-final.xml`、`playmode.xml`、`allocation-summary.json`、十份 `allocations-*.json`、对应计时、`profiler-positive.json`、`final-runtime-check.json`、`socket-pairs.json` 和源码/产物保护清单。原迁移备份仍位于 `Temp/CharacterSocketBakeMigration/run-001/`。
