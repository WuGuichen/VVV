# Buff 主创编辑器使用说明

> Status: Guide
>
本文记录当前外部主创编辑器的可用功能、操作步骤、限制和排错方式。后续开发和 AI agent 应优先读取本文，不要每次从代码里重新推断。

## 1. 当前阶段结论

当前阶段已完成 **Buff Authoring MVP / DamageByAttr 垂直切片**：

- 外部网页编辑器可打开并读取项目工作包。
- 可选择 Preview / Mod 包。
- 可创建、选择和编辑 Buff 草稿。
- `DamageByAttr` 已支持中文字段、类型专属字段显示、必填标记、字段提示和即时校验。
- Patch 可保存到包目录。
- Authoring Core / CLI 可校验、合并预览和生成报告。
- Unity Runtime Preview Server 链路已打通。
- 运行时预览面板可区分 unavailable、success、failed、scene preview 和 dummy fallback，并展示 Preview Server 返回的结构化结果。
- `DamageByAttr` 可通过 Preview Server 的真实结果字段展示 Buff、属性、伤害、日志和错误；没有场景目标时仍可能进入 dummy fallback。

当前阶段不等于最终真实战斗表现预览。框架只展示 Preview Server 提供的 scene/dummy 预览结果，不导入 WGame 真实角色、怪物或关卡数据。

## 2. 入口

### 外部编辑器

本地编辑器地址：

```text
http://127.0.0.1:4873/Tools/MxFramework.Authoring.Editor/web/
```

如果服务没有启动，使用：

```bash
dotnet run --no-build --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- editor serve --root "$WGAMEFRAMEWORK_ROOT" --port 4873
```

### Unity 预览服务

Unity 菜单：

```text
MxFramework / Runtime Preview / Start Server
```

停止后重启：

```text
MxFramework / Runtime Preview / Stop Server
MxFramework / Runtime Preview / Start Server
```

编辑器通过连接描述文件自动发现 Unity Preview Server，不需要手动配置端口。

## 3. 当前可用包

默认包：

```text
Tools/MxFramework.Authoring/samples/buff-preview
```

示例 Buff：

```text
BuffId: 100001
Type: DamageByAttr
```

## 3.1 Mod 诊断面板（v0）

右侧新增 `诊断` 面板，点击 `刷新诊断` 会调用 EditorServer `/api/mod/diagnose`，返回 `mx.modDiagnosticSnapshot.v1` 并按 snapshot 原始顺序展示：

- Summary：`success`、`discovered/valid/invalid/enabled/ordered/skipped/overrides/errors/warnings`
- Packages：`packageKey/packageId/displayName/version/kind` + `valid/enabled`
- Loadout：`profileId/enabledPackageKeys`
- Load Plan：`Ordered/Skipped` + `skipReason`
- Overrides：`configType/id/packageChain/winner`
- Issues：`severity/code/source/message`

默认诊断输入：

- `container=Assets/StreamingAssets/MxFramework/Demo`
- `loadout=Assets/StreamingAssets/MxFramework/Demo/mod_loadout.json`

点击 `复制诊断 JSON` 会复制完整 snapshot JSON；复制失败会在校验区显示错误。

## 4. DamageByAttr 编辑流程

1. 打开外部编辑器。
2. 左侧选择 `sample.buff.preview` 包。
3. 左侧 Buff 列表选择 `100001`，或点击 `新建 Buff` 创建一个 `DamageByAttr` 草稿。
4. 在中间 `字段草稿` 区填写字段。
5. 检查字段旁提示和右侧 `校验报告`。
6. 点击 `保存 Patch`。
7. 如果只做数据校验，点击 `生成报告`。
8. 如果要验证 Unity 预览链路，先在 Unity 启动 Runtime Preview Server，再点击 `运行时预览`。

## 5. DamageByAttr 字段

### 公共字段

| 字段 | 中文名 | 当前说明 |
| --- | --- | --- |
| `Id` | 编号 | Buff 稳定 ID。创建后尽量不要修改。 |
| `Type` | Buff 类型 | 决定类型专属字段显示。 |
| `Name` | 名称 | 多语言 key 或直接文本。 |
| `Desc` | 描述 | 多语言 key 或直接文本。 |
| `ShowHeadIcon` | 显示头顶图标 | 是否显示头顶图标。 |
| `Removeable` | 可移除 | 玩家或系统是否允许移除。 |

### 目标 / 堆叠 / 持续

| 字段 | 中文名 | 当前说明 |
| --- | --- | --- |
| `Target` | 生效目标 | Buff 添加到谁身上。DamageByAttr 通常选择目标。 |
| `AddType` | 重复添加规则 | 同一 Buff 重复添加时刷新、替换或叠层的规则。 |
| `AddNum` | 叠层数量 | 每次添加增加的层数；为空时由运行时默认规则处理。 |
| `Duration` | 持续时间 | 单位毫秒。DamageByAttr 必须大于 0。 |
| `HitCooldown` | 触发间隔 | 单位毫秒。DamageByAttr 必须大于 0，且不建议超过持续时间。 |

### DamageByAttr 类型专属字段

| 字段 | 中文名 | 当前说明 |
| --- | --- | --- |
| `Values` | 伤害公式 | 当前预览支持固定数值，或 `caster.Attack * 系数`，例如 `caster.Attack * 0.35`。 |
| `DmgType` | 伤害类型 | 例如 `Magic`。 |
| `EleType` | 元素类型 | 例如 `Fire`。 |
| `EleValue` | 元素值 | 元素附加值。 |
| `DamageBaseTypeID` | 伤害基准类型 | 开发模式字段；Mod 模式默认隐藏。 |
| `HitEffect` | 命中特效 | 资源路径，例如 `Effects/Hit/FireSmall`。 |

## 6. 校验规则

编辑器和 Authoring Core 目前会检查：

- 必填字段不能为空。
- `Integer` 字段必须是整数。
- `Float` 字段必须是数字。
- `Enum` 字段必须来自 Manifest 声明的枚举值。
- `LocalizedText` 字段如果引用未声明 key，会给 warning。
- `AssetPath` 必须在资源白名单前缀内。
- `DamageByAttr.Values` 只支持固定数值或 `caster.Attack * 系数`。
- `DamageByAttr.Duration` 必须大于 0。
- `DamageByAttr.HitCooldown` 必须大于 0。
- `HitCooldown > Duration` 时给 warning，因为预览中可能不会触发伤害 tick。

## 7. 运行时预览状态

运行时预览由 Unity Preview Server 决定实际模式，网页只展示服务端返回字段：

- `unavailable`：没有发现或无法连接 Unity Preview Server；网页不会返回 HTTP 500。
- `success / scene`：预览命中场景预览世界，结果来自场景目标。
- `success / dummy`：预览走 dummy fallback，面板会显示 `fallback`。
- `failed`：Preview Server 返回结构化失败，例如 `2001/2002/2003/2004`；面板会显示 `code / reason / previewMode / result.errors[]`。

面板会展示：

- `previewMode`
- `configMetadata.sourceId / layer / loadedPatchIds / changedConfigIds / failedConfigIds / mergeWarnings`
- `buffSnapshots[]`
- `attributeChanges[]`
- `damageTicks[]`
- `statusChanges[]`
- `logs[]`
- `errors[]`
- `performance.loadMs / applyMs / tickCount / totalMs`
- `truncated`

如果当前 Unity 侧落入 `DummyPreviewWorld`，默认 fixture 仍是：

- 默认 caster：`TestCaster`
- 默认 target：`TestTarget`
- caster 默认 Attack：`100`
- target 默认 Hp：`1000`

示例：

```text
Values = caster.Attack * 0.35
```

预览计算：

```text
100 * 0.35 = 35
Hp 1000 -> 965
```

这个结果只能证明配置 Patch、Preview RPC、Buff 工厂和 tick 链路可用。它不是最终战斗表现预览。若 Preview Server 没有产生伤害 tick，面板会显示 `logs[]` 中的 no-damage 解释，而不是在前端重算伤害。

## 8. 当前限制

- 右侧运行时预览面板仍偏技术结果展示，不是最终策划可读面板。
- 只有 `DamageByAttr` 做了相对完整的编辑体验。
- 其他 Buff 类型还没有同级别的专属表单和预览逻辑。
- `Values` 仍是文本公式，尚未拆成结构化公式编辑器。
- 当前框架不携带 WGame 真实业务数据，sample 数据只用于工具链验证。
- 场景预览依赖 Unity 侧 Preview Server 和场景目标配置；缺失时可能显示 dummy fallback 或结构化失败。

## 9. 常见问题

### 运行时预览连接失败

提示类似：

```text
failed to connect to preview server at ws://127.0.0.1:49152/preview
```

含义：网页没有发现 Unity Preview Server，或连接描述中的端口已失效。当前 API 会返回 `status=unavailable`，不会把这个状态变成 HTTP 500。

处理：

1. 在 Unity 执行 `MxFramework / Runtime Preview / Start Server`。
2. 如果仍失败，先 `Stop Server`，再 `Start Server`。
3. 再回网页点击 `运行时预览`。

### 预览失败但网页仍是 HTTP 200

这是预期行为。`/api/preview/run` 会把运行时失败映射成结构化 JSON，例如：

```text
status=failed
code=2003
reason=missing_target
previewMode=scene
```

右侧 `运行时预览` 面板会继续展示 `errors[]`、`logs[]` 和 `configMetadata`，用于定位缺配置、缺目标、缺工厂或 Preview Server 不在预览模式等问题。

### 保存 Patch 后 sample 文件末尾换行变化

网页保存可能导致 JSON 文件末尾换行变化。这类变更不代表业务内容变化，提交前应检查 `svn diff`。

## 10. 下一阶段

下一阶段应单独立项为 **策划可读的场景目标选择与反馈**，不要在外部编辑器里重算运行时规则。

建议目标：

1. 在外部编辑器提供 caster / target 选择，而不是固定 `TestCaster` / `TestTarget`。
2. 把 scene preview 的目标状态压缩成策划可读摘要，保留技术字段作为展开项。
3. 对 `configMetadata.failedConfigIds` 和 `mergeWarnings` 增加更直观的定位入口。
4. 为不同 Buff 类型补齐专属表单和预览摘要。
5. 为 `logs[]` 增加分页或“拉取完整日志”操作。

这个阶段完成后，运行时预览会从“技术结果可见”推进到“日常主创调参可用”。

## 11. 已提交版本

- `r1133 Complete DamageByAttr preview loop`
- `r1134 Improve DamageByAttr authoring fields`
