# CD-M2-T1：DamageZone Authoring Core、服务与 Compiler

> Status: Guide
>
> 工作包：CD-M2-T1
>
> 工作包状态权威：[CD-M2 总控第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md)
>
> 里程碑状态权威：[实施总台账第 4 节](CHARACTER_TEST_CHARACTER_DISTANCE_DEVELOPMENT.md)
>
> 前置：CD-M2-T0 `Verified`
>
> 本文只交付无界面 Authoring/Compiler 链路；不实现 CharacterStudio UI、Runtime Zone 聚合或 Planner Zone lock。

## 1. 任务结果

让可选的 `geometry/damage_zones.json` 具备可读取、可严格校验、可确定性保存、可编译和可由现有 Authoring 服务传输的完整后端链路，并将结果编译进唯一 Runtime 几何真源 `geometry_binding.json`。

## 2. 最小上下文与前置检查

- [CD-M2 总控](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md) 与 [T0 冻结记录](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_T0_CONTRACT_AUDIT.md)。
- `Docs/API_STANDARDS.md`、`Docs/QUALITY_GATE.md`、`Docs/CHARACTER_SDK_AUTHORING_PIPELINE_PLAN.md`。
- [CharacterApplication 接口](../Interfaces/CharacterApplication.md) 的 package、Compiler 和 Runtime binding 章节。
- 当前 Authoring Core 的 `CharacterAuthoringGeometry.cs`、`CharacterResourcePackage.cs`、`CharacterResourcePackageSchemas.cs`、`CharacterPackageValidation.cs`、`CharacterResourcePackageMigration.cs`、`CharacterAuthoringCompiler.cs`。
- 当前 Authoring 测试中的 package serialization、migration、compiler 和 editor-server focused fixtures。

开工前必须确认 T0 已冻结 schema、版本、canonical order、diagnostic code 和 `geometry_binding.json` 版本策略；不得在本任务内重新设计。

## 3. 实现范围

### 3.1 Authoring Core

- [ ] 增加 T0 冻结的 DamageZone DTO 和稳定 ID/字段类型，不使用 Runtime/Unity 类型污染 noEngine Core。
- [ ] 将 `damage_zones.json` 接入 package 加载与保存模型，同时保留“文件缺失”和“存在但为空”的区别。
- [ ] 对 format/schema、重复 Zone ID、重复 HitZone 归属、未知 HitZone、禁用部位、非法优先级和非法字段组合执行 typed validation。
- [ ] 已知旧 schema 仅按 T0 记录的真实来源迁移；没有旧格式时只实现当前版本 round-trip 和未来迁移拒绝入口。
- [ ] 未知 schema 保留原始 payload 与只读阻断信息，禁止 normalize 成当前默认值后覆盖。
- [ ] 缺失可选文件不标脏、不自动创建、不因 load/save 其它 package 内容而补写。
- [ ] 将 DamageZone 内容纳入 T0 指定的 canonical hash；容器枚举顺序不得改变结果。

### 3.2 服务、CLI 与保存事务

- [ ] 通过现有 package state / editor-server 数据合同暴露 DamageZone 状态、write eligibility 和 diagnostics，不建立第二套旁路 API。
- [ ] 保存事务只在用户显式创建或修改 DamageZone 文档时写入文件。
- [ ] 未知 schema、验证失败或 Compiler 失败时阻断相关保存/编译，且不产生部分生成结果。
- [ ] CLI validate/compile 能打印稳定 diagnostic code 和 source object path。

### 3.3 Compiler 投影

- [ ] 将有效 DamageZone 按冻结顺序投影进现有 `geometry_binding.json`；不得生成第二份 Runtime Zone 文件。
- [ ] 生成内容包括 Runtime 所需的稳定 Zone、HitZone 映射、正交属性、作者优先级和 source mapping，不夹带 CharacterStudio-only 状态。
- [ ] 更新 geometry binding canonical text/hash、write plan、source mapping 和 Unity import bridge 所需投影。
- [ ] 对缺失文件应用 T0 冻结的输出/hash 兼容策略。
- [ ] 生成结果中的未知/非法引用不允许以空字符串或默认 Zone 静默落地。

## 4. 候选影响区域

以下是开工定位入口，不是预授权的文件清单；实际修改必须记录在第 8 节：

- `Tools/MxFramework.Authoring/src/MxFramework.Authoring.Core/CharacterPackages/`
- `Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/CharacterPackageTests.*.cs`
- CharacterStudio 使用的现有 editor-server/package state 路由。
- 需要用于 focused fixture 的 `Tools/MxFramework.Authoring/samples/`；不得顺手批量改写正式角色包。

## 5. 自动化测试

计划新增聚焦入口 `character-damage-zones` 到 `MxFramework.Authoring.Tests/Program.cs`，由 `CharacterPackageTests.DamageZones.cs`（或 T0 冻结的等价单一入口）拥有 T1 用例。实现后至少执行并记录：

```powershell
dotnet build Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj
dotnet run --no-build --project Tools/MxFramework.Authoring/tests/MxFramework.Authoring.Tests/MxFramework.Authoring.Tests.csproj -- character-damage-zones
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- character validate --package <damage-zone-fixture> --check-files
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- character compile --package <damage-zone-fixture> --out Temp/MxFrameworkAuthoring/CharacterDistanceM2 --check-files
```

`<damage-zone-fixture>` 的正式路径由 T0 测试矩阵冻结；不得临时使用带无关本地改动的正式角色包充当唯一 fixture。

- [ ] 当前 schema serialize → load → serialize round-trip 稳定。
- [ ] 打乱 Zone 和 HitZone 输入顺序后 canonical hash 与编译结果符合冻结合同。
- [ ] 重复 Zone ID、重复归属、未知 HitZone、禁用部位、非法属性和非法优先级返回精确 diagnostics。
- [ ] 缺失文件的旧包 validate/compile/save 无损，且磁盘上不会出现 `damage_zones.json`。
- [ ] 未知 schema 原始 payload 保留、write eligibility=false，任何保存入口均不能覆盖。
- [ ] 已知旧 schema 的迁移幂等；若 T0 认定没有迁移源，则测试明确证明未知/旧版本被拒绝而非伪迁移。
- [ ] Compiler 只更新约定的 geometry binding 投影、hash 和 source mapping，连续两次输出字节稳定。
- [ ] editor-server/package state 和 CLI 使用同一 Core 结果，不出现 UI/CLI 分叉语义。

## 6. 完成 Gate

- Authoring Core、CLI、editor-server 和 Compiler focused tests 全部通过，记录精确通过/失败数量。
- 缺失、非法和未知 schema 三条失败/兼容路径都有磁盘级“不覆盖、不补写”证据。
- `geometry_binding.json` 是唯一 Runtime 投影，hash/source mapping 可追踪回 `damage_zones.json`。
- 无 CharacterStudio UI 或 Runtime 行为被提前宣称完成。
- 实际文件、测试命令/入口、结果和限制写入第 8 节后，T1 才能进入 `Verified`。

## 7. 交接给后续任务

- T2 只消费本任务产生的正式 binding DTO/文件，不重新读取 Authoring JSON。
- T5 复用本任务的 package state、diagnostics、write eligibility 和保存事务，不复制 schema 校验。
- 若 Runtime 或 UI 需要新增字段，必须返回 T0 做合同变更，不能在消费者中私加字段。

## 8. 实际提交与验证结果

| 项目 | 当前记录 |
| --- | --- |
| 工作包状态 | 见[CD-M2 总控第 3 节](CHARACTER_TEST_CHARACTER_DISTANCE_CD_M2_DAMAGE_ZONE.md) |
| 实际分支 / 提交 | `main` 脏工作区，基线 `84d3d084d`，尚未提交；保留既有 M0/M1 与无关改动 |
| 实际修改文件 | `CharacterAuthoringGeometry.cs`、`CharacterPackageValidation.cs`、`CharacterPackageCommands.cs`、`EditorServer.cs`、`CharacterAuthoringCompiler.cs`、`CharacterPackageTests.DamageZones.cs`、测试路由 |
| Authoring Core / CLI | `character-damage-zones` focused 入口通过；独立 Authoring build 0 warning / 0 error；Dragon14 validate 为 Ready |
| editor-server | package state 暴露 Current/Missing/UnsupportedSchema/Invalid、raw payload 与 write eligibility；与 Core 共用 DTO/校验 |
| Compiler / hash | `mx.characterDamageZones.v1` schema `1.0` 稳定编译到唯一 `geometry_binding.json` v2；Dragon14 compile 仅有 3 个既有 LOCO_CAL warning |
| 兼容与磁盘写入证据 | 缺失文件不补写并保留 binding v1；未知/无效 schema 原 payload lossless 只读；重复归属/非法引用 fail closed |
| 已知限制 / 阻断 | 无 T1 阻断；未伪造不存在的历史旧 schema 迁移源 |
