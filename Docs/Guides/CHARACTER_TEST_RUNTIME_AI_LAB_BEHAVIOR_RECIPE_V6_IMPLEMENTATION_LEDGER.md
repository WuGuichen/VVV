# CharacterTest Runtime AI Lab 行为配方 v6 实施台账（Archive）

> Superseded by: [行为配方 v7 实施台账](CHARACTER_TEST_RUNTIME_AI_LAB_BEHAVIOR_RECIPE_V7_IMPLEMENTATION_LEDGER.md)
>
> 本文只保留 v6 历史证据，不再作为当前 schema、运行合同或 UI 权威。

> Status: Implemented and verified
>
> Started: 2026-08-13
>
> Schema: `behavior-card-logic.v6`; runtime contract `3`; snapshot schema `8`

## 已实施

- v6 Timeline/Event 保存模型、JSON 往返、规范化与 semantic hash。
- All/Any/Not/FactPredicate/EventSignal 条件树及深度、叶节点、引用与循环校验。
- v4→v5→v6 旧哈希认证迁移；v5 PrimaryAction/Trigger 收敛为普通 Event。
- 不可变 v6 编译结果、稳定索引、可达性和控制冲突校验。
- 确定性运行器：区间、次数、冷却、上一帧 EventSignal、控制通道、跳转和返回决策。
- CharacterAction/Ability 继续复用既有编排替换/终止接口。
- 运行合同 3 与快照 schema 8 的状态保存和旧合同拒绝。
- 时间轴统一 Event 投影、水平拖动、固定区间右边缘缩放、右键新建/删除。
- 统一 Event Inspector、条件组/事实/EventSignal 编辑、操作/时间/重复/冷却/优先级修改。
- 行为启用条件和 Timeline 进入条件使用同一 Inspector 条件树，且不生成 Event。
- Timeline 不再生成独立 Header 行；点击 Event 以外的 Timeline 空白会取消 Event 选择并打开 Timeline 进入条件，空 Timeline 保留一行非持久化背景命中区。
- Inspector 选择已升级为 `Recipe / Timeline / Event` 一等状态，使用稳定 TimelineId/EventId 恢复；Event 删除后返回所属 Timeline，切换行为卡会重置为 Recipe。
- 右上正式组件升级为 `RuntimeAiLabBehaviorRecipeInspector` 三模式组件；v6 编辑逻辑不再回退到旧 Stage/Detector/Transition Inspector。
- Timeline 名称和行为入口使用原生 v6 mutation；当前入口不能取消为空，进入条件继续直接写 Timeline 条件树。
- “持续接敌”原生语义为无条件接近、距离阈值返回决策、路径不可用返回决策；不自动添加目标消失退出。

## 发布证据

- Timeline Inspector 正式迁移的 Composer `publish-all --fast` operation：`ba4d5bb27075422190f5340306fb11ad`。
- Apply operation：`eaaacd06d9b041068634fb301aab9cab`；Publish operation：`f7b7e249327245508579bdbf4d4260f4`。
- 新发布 bytes SHA-256：`b09c7fdfe1346dc0801209f0d25eb15de0f9bd4269a96a769d6e25091223a42c`。
- 结构化发布日志：`Logs/UiComposerPublish/ba4d5bb27075422190f5340306fb11ad.json`。
- Timeline 名称输入由旧 1px 透明占位控件升级为正式可读输入后，最终 Composer operation：`b736c81dcc8f46aaa74407ba87d065c2`；Apply=`53f150baadc748ca869af406c766db9f`，Publish=`8d7ed710e78d4c21bb0255d7a885ba89`。
- 最终发布 bytes SHA-256：`297d7e2e7eacda0f7cdb6a5d3cef9e88957336f8b0ba040199c6c5a4c7778a47`；结构化日志：`Logs/UiComposerPublish/b736c81dcc8f46aaa74407ba87d065c2.json`。
- 本轮曾有一次 Apply 读取 helper progress 文件时遇到瞬时 Windows 占用，operation=`c4734ce9f7ac42e1a1d96d9dd9926677`；它在 Publish 前停止，随后幂等重跑成功，未被记为发布成功。

- Composer `publish-all --fast` operation：`e886d591a45c4e638213e46bb52314c6`。
- Preflight、helper Probe、Apply、source hash、Publish、XML/package/state/bytes/catalog/manifest 核对均成功。
- 权威 recipe hash：`234f5699df166c53cde4cd263df5559bff7d4a70d69819efdd68170c5c7c81df`。
- 权威 contract hash：`d214867a78f9356fae6c7c091d8349d11b5266bcffa677bc496753c1f5c5ccff`。
- Materialize 后 source XML hash：`1a9204333bb0020ab51fa845e97942a832515e06beadbc65b7d55415864e6999`。
- 发布 bytes SHA-256：`fcfb9d1e29c341ff21a0469495d9bb7d51bf7ebcd5cf6985b6cad494ed0c693f`。
- 结构化发布日志：`Logs/UiComposerPublish/e886d591a45c4e638213e46bb52314c6.json`。
- Authoring CLI 的 `ui-composer-publish-all` 专项测试通过。
- 随后的幂等复发 operation `46c7acbdebe7487687f28655ba51ca7c` 在相同权威 hash 下完成 Preflight、Probe、Apply 与 source 校验，但 Publish 因 Unity 占用 `_fui.bytes` 返回 Windows IO 1224；该失败未改写先前已核对成功的发布 bytes，不作为第二次成功发布证据。

## 测试证据

- v6 JSON、hash、编译器、零帧事件、冷却/次数、上一帧 EventSignal 与共享旧 Event 专项测试已建立。
- Timeline 投影和 Inspector 直接 v6 mutation 专项测试已建立。
- 最终 focused EditMode job：`aff3bec6b1af4a318dc61fdc2366c9de`，`43/43` 通过；覆盖 v4→v5→v6 迁移、正式行为目录、v6 Timeline 投影、统一 Inspector 直接 mutation、条件属性 owner、条件组新增/重排和发布包组件。
- 最终 Unity 脚本验证 job：`f9a6057397f04786ab301de5f31c6f66`；refresh 完成、compile 开始并结束、编译 1 个程序集、观察到 domain reload、Editor ready、Console error count=`0`。
- 验证仅留下两个既有的 `FindObjectsSortMode` 弃用警告，均位于 `CharacterTestGameplayTriggerWorkspacePersistenceTests.cs`，不属于行为配方 v6 错误。
- Runtime AI Lab Trigger `runtime-ai-lab-open-running-behavior-logic` 成功进入真实 Play Mode，确认 schema v6、runtime contract 3、至少三个统一 Event、Track 标签隐藏、Event 可选择、统一 Inspector 显示且旧详情隐藏。
- 最终真实 Game View 截图：`Logs/UiComposerPublish/v6-runtime-ai-lab/runtime-ai-lab-v6-unified-event-inspector-final.png`。
- 移除 Timeline Header 并合并空白区右键菜单后，最终 Unity 脚本验证 job `b56973af9c4b480197d8d6875af4b390`：refresh、4 个程序集编译、domain reload、Editor ready 与 Console error count=`0` 全部成立。
- 更新后的真实 Trigger 再次通过，明确验证 `recipeTimelineHeader0` 不存在、Event 点击保持 Event Inspector、轨道空白点击切换为“时间轴进入条件”Inspector。
- Timeline Inspector 正式迁移后的 focused EditMode：5 项 v6 投影/mutation/发布包测试与 1 项 Screen package 测试全部通过。
- 最终 Unity 脚本验证 job：`9485b1dc582d4df8a8790f6514a99268`；refresh、1 个程序集编译、domain reload、Editor ready、Console error count=`0`。
- 更新后的真实 Trigger 在 6.31 秒内通过，验证默认 Recipe Inspector、Event 选择、轨道空白选择 Timeline，以及旧详情始终隐藏。
- Timeline Inspector 真实 Game View 截图：`Logs/UiComposerPublish/ba4d5bb27075422190f5340306fb11ad/runtime-ai-lab-v6-timeline-inspector.png`。
- 最终 focused EditMode job：`00b1824d47f94ad49928c6c6f0c8c3f8`，`5/5` 通过。
- 最终发布后 Unity 验证 job：`e738dc382a324f4fa235d221128dbc6d`；refresh、compile、domain reload、Editor ready 与 Console error count=`0` 全部成立。
- 最终 Trigger 在 `7.13s` 内通过；最终真实 Game View 截图：`Logs/UiComposerPublish/b736c81dcc8f46aaa74407ba87d065c2/runtime-ai-lab-v6-timeline-inspector-final.png`。

## 验证结论

最终验证没有活动阻断。任务较早阶段曾观察到与本次行为配方无关的 VFX stale-evidence 错误 `vfx.magic.fireball_burst.charge`，但最终 `validate_unity_scripts` 已返回 Console error count=`0`，因此不再把它列为当前阻断，也未修改任何无关 VFX 资产。
