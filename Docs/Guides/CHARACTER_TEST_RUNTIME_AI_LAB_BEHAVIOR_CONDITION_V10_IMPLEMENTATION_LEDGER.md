# Runtime AI Lab 对象优先条件 v10 实施台账

> Status: Current
>
> Authority: `mx.character-test.runtime-ai-lab.behavior-card-logic.v10`
>
> Published runtime contract: `7`

## 已实现

- `SubjectScope` 与 `CurrentCombatTarget / Self / Environment` 稳定对象身份。
- v9 语义/内容哈希认证后的确定性 v10 迁移；仅保存或发布时写出 v10。
- 单一条件描述目录，覆盖 15 个现有 Fact，并补齐 TargetDead trace identity。
- 对象优先 Condition Picker、独立逻辑组入口、对象卡片和 Authority 参数选择。
- `Satisfied / Unsatisfied / SubjectUnavailable / NotObserved` selected-frame 观察投影。
- Composer-owned Runtime AI Lab Apply/Publish、Unity 验证和 interaction profile。

## 当前验收证据

- v10 schema/目录/迁移/求值/事务/观察专项 `9/9` 通过。
- v9 Published/项目独立卡/行为组固定 hash 双重认证与当前目录回归合并批次 `46/46` 通过。
- 真实发布 Condition Picker/Inspector/Condition Row 组件加载合同 `1/1` 通过。
- `MxFramework.Game.CharacterTest.csproj` 与 `MxFramework.Tests.csproj` 构建均为 `0 error`；警告为仓库既有 Unity/FMOD/obsolete/serialized-field 警告。
- UI Composer operation `cf50783be4a4463299546c5e83c8f706` 完成 Preflight、Apply、Publish、artifact、Unity 和 Runtime AI Lab interaction verification；Panel configured/open、root package 和 command binding 均通过。
- Unity `validate_unity_scripts` 最终证据包含 refresh、compile、domain reload、Editor ready 与 Console `0 error`。
- 版本化 v9 Draft 通过 v10 迁移后，真实 `runtime_ai_lab_open_running_behavior_logic` 启动、角色生成、运行时间轴和 Timeline Inspector 验证通过；Game View 证据位于忽略目录 `Logs/RuntimeAiLabV10/start-session-fixed-20260827-184012.png`。
- changed-code standards `15` 文件无发现，Docs health 与 `git diff --check` 通过。

## 未闭合证据

- `CharacterTestRuntimeAiLabBehaviorRecipeV5Tests` 当前为 `42/44`：两个失败分别是旧 PrimaryActionSignal fixture 计数和旧测试 CharacterAction reference catalog 缺项，均在本次 v10 实施前已有的未提交 CharacterAction-only 批次范围内；v10 新增专项不依赖这两项。

## 明确边界

- 不增加生命值、Buff、破绽、精确命中、标记、机关或任意对象事实。
- 不修改公共 `MxFramework.AI`、SaveState 字段、network wire 或 Planner snapshot 13。
- 当前正式画面若被用户本机旧 recovery/schema 阻断，只能单独报告该本地状态，不能用源码或 package 加载成功替代完整行为卡运行闭环。
