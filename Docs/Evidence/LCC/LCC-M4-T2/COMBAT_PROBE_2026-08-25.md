# LCC-M4-T2 Combat Ground Probe Evidence — 2026-08-25

## 结论

T2生产实现与15项聚焦EditMode测试完成；实现保持noEngine/fixed-point、复用现有Combat broadphase，不使用Unity Physics、采样、第二套broadphase、动态容器或新框架。唯一 integrated `validate_unity_scripts` 保留首次CS0177失败原证据；机械修正后的分步scripts refresh/compile、domain reload、Editor ready、15/15测试与Console error=0均已成立，因此fallback Unity gate为green，可交接主代理。

## Stop-gate与最小修补

- `staticLayerMask=3`可命中layer 0；单独layer 1 mask可命中layer 1；即使layer 8/9/10/11 surface更近也全部排除。
- 既有Ray query对primitive和compound leaf均保留entity/body/collider/shape、point、normal。compound leaf collider id就是其stable leaf identity。
- 缺口：旧公开`TryGet*Collider`只回查flat primitive，不能由既有`CombatQueryResult`精确取得compound leaf material。
- 授权后的最小修补只有`CombatPhysicsWorld.TryGetSurfaceMaterial(in CombatQueryResult, out CombatSurfaceMaterial)`：按entity/body/collider/shape精确回查flat或compound；缺失、重复、primitive/compound冲突一律fail closed，无registry、cache、fallback或QueryResult修改。

## API与几何合同

- `CombatGroundProbeQuery`：probe/source identity、world origin、向下单位direction、distance、radius、caller-owned static mask、minimum slope cosine。
- `CombatPhysicsWorld.QueryGroundProbe(...)`：single-best；`radius=0`复用Ray narrow phase，`radius>0`解析sphere cast。
- 四种target shape：AABB按offset face和rounded edge解析首次接触；OBB转local复用AABB；sphere使用combined-radius二次方程；capsule使用圆柱与两端球二次方程。broadphase仍是现有Combat capsule swept AABB。
- 非零radius的`Distance`是probe球心沿normalized direction到首次接触的轴向行程；`Point`是该时刻target原始表面点；`Normal`从target指向probe球心。
- 输入边界与T1一致：distance/radius各`<=100 m`；distance必须正、radius非负；direction分量在`[-1,1]`且长度平方误差不超过`2/32767`，归一后必须向下；raw endpoint与origin/endpoint的radius扩张先做Int64溢出检查。没有原先无来源的2048/64/8/4常量。
- candidate全序：raw axial distance升序、target priority降序、entity/body/collider/shape、query trace/action/source、primitive/compound标记、raw point、raw normal。公开hit comparer再按量化distance、priority、surface entity/body/collider/leaf/shape、point、normal、slope、material raw fields全序。
- 量化：distance/point到1 mm；normal分量到signed `1/32767`后重归一；slope由量化normal Y得到；surface identity与material raw fields不改写。
- status：`Success/InvalidInput/NoHit/TooSteep/OutOfRange/UnsupportedShape/SurfaceIdentityUnavailable/SurfaceMaterialUnavailable`。失败`HasHit=false`，不暴露伪造target。single-best无容量，因此删除不可达的预留`CapacityExceeded`。
- warmed hot path不创建hit list或字符串；4096次调用实测`GC.GetAllocatedBytesForCurrentThread()`差值0 B。

## 聚焦测试

最终有效Unity job：`08b703c9b5ef478897ed5206a004db21`

```text
mode=EditMode
status=succeeded
summary.total=15
summary.passed=15
summary.failed=0
summary.skipped=0
summary.durationSeconds=0.4972615
resultState=Passed
```

覆盖：layer 0/1与8/9/10/11排除、compound非默认material精确回查、flat量化、上下坡/too-steep、同一路径双surface轴向先后、AABB/sphere/capsule/OBB、台阶边、相切distance limit、同距与注册顺序、typed failure、identity冲突fail-closed、512次fixed replay、4096次0 B、旧Ray/Capsule query兼容。新增的非零radius斜坡OBB用例明确走`ToObbLocal`与解析sphere-cast路径，断言轴向distance=`1.811 m`、target原始表面point=`(±0.075, 0.059, 0) m`、normal raw=`(∓500012, 866019, 0)`、slope raw=`866023`，并覆盖镜像与两侧各512次重演稳定。

窄整改期间只有测试期望常量修正，无生产代码变化：job `67207499b42642158b1bc420b3e97596`暴露normal期望常量不精确，job `be41992bb7e74f1b86cbbba52d051f1e`暴露slope期望相差1 raw；`29d97a427c3348629a1d822139213dc0`重用了未刷新的Tests程序集，不计作有效结果。三者均未暴露生产几何缺陷，最终精确常量由固定点结果冻结并由15/15 job重演验证。

以下过滤尝试均只返回项目根suite且`total=0`，不计作通过：`ffe369900a84458e8253b58b13b37ba4`、`ce984c66f6b6415391737312376dc8cc`、`a64763b9d97c42c8bfb35747d3472e78`、`1f294002eaad40a2858b3e97dacb83b1`。

## Unity脚本校验原证据

唯一 integrated validator：`execute_custom_tool(tool_name="validate_unity_scripts", parameters={})`

```text
job_id=8cc4a8ead0f74269a473b95b40f62290
success=false
outcome=compilation_failed
refresh.requested=true
refresh.completed=true
compilation.requested=true
compilation.started=true
compilation.finished=true
assembly_count=1
errors=4 x CS0177 at GroundProbes.cs former lines 428/450
domain_reload_observed=false
editor_ready=true
console.error_count=0
```

首个失败是短路分支在material/radius合并失败时未给`out distance/normal`赋值；只补`Fix64.Zero/FixVector3.Zero`，未改合同，且未第二次调用integrated validator。

修正后的分步fallback令牌（最后一次仅为使新增test source进入Tests程序集）：

```text
refresh_unity(mode=force, scope=scripts, compile=request, wait_for_ready=true)
success=true, compile_requested=true

editor_state observed_at_unix_ms=1787621183591
activity.phase=idle
compilation.is_compiling=false
compilation.is_domain_reload_pending=false
compilation.last_domain_reload_after_unix_ms=1787621160738
assets.is_updating=false
assets.external_changes_dirty=false
tests.is_running=false
tests.last_finished_job_id=08b703c9b5ef478897ed5206a004db21

read_console(action=clear)
success=true, message="Console cleared successfully."

read_console(types=[error])
total=0, items=[]
```

最终15/15 job证明修正后的Combat与Tests程序集已加载执行；其后只读状态确认Editor idle、无编译或待处理domain reload、Test Runner已停止。Test Runner曾留下1条自身的“Saving results”临时Console条目；按主代理明确授权清除后，`read_console(types=[error])`返回0。未第二次调用integrated validator；其首次失败仍如实保留，而修正后的分步fallback Unity gate已满足根AGENTS要求。

## 文件、未执行项与回滚

实际文件：

- `Assets/Scripts/MxFramework/Combat/Physics/CombatGroundProbe.cs`及`.meta`
- `Assets/Scripts/MxFramework/Combat/Physics/CombatPhysicsWorld.GroundProbes.cs`及`.meta`
- `Assets/Scripts/MxFramework/Tests/Combat/Physics/CombatGroundProbeTests.cs`及`.meta`
- `Docs/Interfaces/Combat.md`中的ground probe公开合同增量
- T2任务记录与本文

报告式code standards扫描3文件、0 finding；scoped diff check无输出。未跑全仓测试、PlayMode、CharacterTest、RuntimeSpawn、Network、`.csproj` build、父M4/T3/T4/T5验证，也未做独立终审。共享dirty worktree未stage/commit/push/clean。

限制：static语义由caller mask负责；T2只输出几何事实，不做Character eligibility、IK、stale判断或状态保存。回滚时删除两份生产C#及meta、一份聚焦测试及meta，并撤销`Docs/Interfaces/Combat.md`的T2段落；不要触碰共享工作区其它修改。
