# 表现 Pose 与 VFX LOD 接入边界

> Status: Guide
> 核对：既有接入边界与2026-10-09 main `cbc1409f` 的455 owner回执；本次文档同步未运行Unity。
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Presentation/Lod/CharacterTestPresentationLod*.cs`
> Code-Doc: `Assets/Scripts/WGameFrameworkProject/CharacterTest/Presentation/Vfx/CharacterTestVfxPresentationBudgetSource.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Animation.Unity/UnityPlayablesAnimationBackend.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Vfx.Unity/UnityVfxParticleBudget*.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Vfx.Unity/UnityVfxPresenter.cs`
> Code-Doc: `Packages/com.vincent.wgameframework/Vfx.Unity/UnityVfxTimedBackend.cs`

本页是Pose／VFX表现预算接入的当前工程权威；模块职责分别见[Animation](../Interfaces/Animation.md)、[Rendering](../Interfaces/Rendering.md)与[Vfx](../Interfaces/Vfx.md)。预算只属于表现，不改变固定步进、Authority动作时间轴、根运动、确定性武器采样、命中／伤害、Replay或状态hash。复用既有主体代际、相机快照、bounds与重要性观察，不新增全场扫描管理器或RendererFeature。

| 消费者 | 当前接入 | 保守边界 |
|---|---|---|
| Playables pose | 独立屏幕高度cadence、滞回与稳定等待；每次调用继续推进表现状态 | 首次pose、资源未就绪、AnimationEvent、root motion、未知backend全量 |
| bone/socket/表现mirror | 与实际pose publication同一次完整发布 | 活跃cloth/procedural work强制full；replay全量 |
| 动态定向阴影 | receiver union提供当前pose需求，读取实际version/frame与可靠形变声明 | 根变换／bounds／pose改变或未知时full；不跳过Raw skin buffer／Compute |
| 刀光timed backend | 已有显式SourceId主体归属与cadence消费；隐藏装饰可降低anchor/backend更新频率 | 可见、重要、未知主体全速；retiring不预算，退出结清积压时间。作者入口和发布边界另见[武器拖尾](WEAPON_BLADE_TRAIL.md) |
| 普通ParticleSystem | `UnityVfxParticleBudgetPlayback`和Presenter已主合，允许显式装饰的简单local-space粒子采用唯一手动时钟 | 默认Unknown，关键预警／未知／不满足准入的效果走原全速路径；15项原生EditMode可控时钟断言已通过，完整Play／Player退场未验 |
| VFX Graph／世界效果 | Graph的既有backend绑定不等于预算消费；上一批扫描回执为1绑定／预算0 | 未证明Graph正式负载预算接入或GPU收益，不宣称全部VFX已纳入 |

Pose与VFX独立预算，读取所有显式gameplay camera的最高屏幕需求、可见性、重要性和未知状态；阴影按投影光线与receiver union判断，离屏投影者仍可要求完整pose。`AdvancePresentationState`推进表现时钟／Blend phase／权重，`EvaluateCurrentPose`求值并发布；暂停、teleport、restore或资源切换使策略失效并恢复，不追播漏掉的表现帧。

浅水样区的显式反射相机通过 `RegisterReflectionCamera` 加入同一观察集合，
要求同场景 `MxSceneRenderingAuxiliaryCamera` 标记、Reflection kind 和既有
有效 Game camera 准入。GameManager 在既有表现遍历前更新镜像视图并登记，
最高屏幕需求同时覆盖原画面与倒影；不增加第二次角色 pose 求值。
反射停用、源相机更换或样区释放时撤销登记。相机输出分辨率参与现有
屏幕高度计算；UI overlay 与独立 preview 仍受原排除规则约束。
此处是源码接入事实，实机验证状态见[浅水样区](SHALLOW_WATER_SAMPLE.md)，
不能由接入代码推出 pose/GPU 一致性或性能收益。

## 普通ParticleSystem：已接入范围与失败边界

`CharacterTestVfxPresentationBudgetSource`提供显式importance；未配置admission source时返回Unknown。`UnityVfxParticleBudgetPlayback.TryBegin`只接纳Decorative、有效SourceId、已有粒子并且没有其他driver的实例，还逐系统检查loop、单位scale、scaled time、local simulation、无StopAction及无距离发射／SubEmitter／Collision／Trigger／ExternalForce／InheritVelocity／速度或力over-lifetime。任一不满足即不进入手动预算，不能按“Prefab里有PS”推断它已降频。

手动owner先Pause各缓存系统，再以`Simulate(... withChildren:false, restart:false, fixedTimeStep:false)`推进获准时长，禁止同一系统同时自动模拟与手动补步；检测外部Play恢复时交回原全频播放。Presenter在spawn／rebuild接入TryBegin，stop进入BeginExit，retiring依赖真实live particles／完成状态再释放，pool reset清理预算owner。源码中的退出上限与清理分支说明设计保护；最新原生EditMode可控时钟回执已覆盖Complete／Dissipate、自然回池、复用与shutdown，仍不等于完整Play／Player退场验收。

当前owner回执：普通PS5文件＋helper meta已主合，helper10/10、26+60离线及部分真实帧通过；**原先两处已知失败已关闭，Presenter原生EditMode可控时钟15项断言通过**。helper的NaturalExitCompletion曾因暂停循环PS在空粒子时仍返回IsAlive=true而失败，已在严格准入范围内按剩余粒子数判完成；Presenter的Complete夹具在Pause后Emit(1)恢复播放，触发既有独占时钟保护，注入后恢复Pause修正了夹具，未改生产PS实现或放宽尾迹断言。

最新15项断言覆盖主体积压0.2秒只冲刷一次并保留活尾、delta0冻结、再推进0.1秒不重复主体时间且不查budget、自然／Dissipate回池、Required撤销后保持唯一手工时钟全频、Rebuild恢复自动状态且Presenter停止手动推进，以及模板缓存复用和shutdown一次释放。证据为VFX owner 2026-10-08的`P4_EDIT_NATIVE_PRESENTER_COMPLETE 15 assertions`回执。该EditMode夹具排除了真实PlayerLoop自动推进断言；完整Play coroutine、恢复自动后的真实PlayerLoop推进、完整速度矩阵、生产素材视觉／性能及Player PS退场仍未验。默认生产素材仍Unknown，不据此宣布PS LOD已启用或性能收益已证实。

刀光正式Common／角色配置及既有镜像发布、455作者窗口帧采样和统一保存已有独立回执；455的Editor Mono Play和重建后的Windows x64 IL2CPP Release Player实际技术验收均已通过，详见[武器拖尾当前验收](WEAPON_BLADE_TRAIL.md#actual-validation-status-2026-10-09)。动作镜像Refresh、Global资源、G/C/R及恢复后的RequireCurrent／产物验证已通过；场景与561份实际持久文件保持。生成000001预览材质差异已核清，仅兼容 `_Color` 的RGB浮点舍入，无需修复或回滚。VFX自身视觉质量与性能收益仍未验。旧动作卡三项生产提示识别回归及一项版本选择夹具过时已修，27项专项原生EditMode测试通过，五卡效果Tooltip已逐张GameView截图验收。后续新源码IL2CPP Release增量构建通过（0错误／19警告，默认压缩与旧LZ4HC不同），但未重跑455；上述VFX运行证据仍对应此前源码快照，不替代457／458或普通PS的完整Play／Player退场验证。

## 证据范围

历史focused LOD41/41、VFX原生6/6及其中阴影25/25按当时输入与覆盖读取，不重复相加，也不外推到后续普通PS或新UI变更。动画分离／pose策略／VFX adapter已有专项原生回执；完整同输入战斗hash／命中对照、真实Raw skin GPU一致性、Graph正式负载和整帧性能收益没有据此获得结论。当前定向阴影资源与缓存操作的独立合同见[定向阴影指南](DIRECTIONAL_TRIANGLE_SHADOWS.md)，不在本页复制分辨率／烘焙完成台账。
