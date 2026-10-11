using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestArcMineRuntime
    {
        private const string LockedTargetDeployMode = "locked_target";
        private const string ForwardDeployMode = "caster_forward";
        internal const string ProjectileImpactDeployMode = "projectile_impact";
        internal const string ProjectileExpirationDeployMode = "projectile_expire";
        internal const string ProjectileDropImpactDeployMode = "projectile_drop_impact";
        internal const string ProjectileDropExpirationDeployMode = "projectile_drop_expire";

        private readonly CharacterTestArcMineTuning _tuning;
        private readonly List<ActiveArcMineState> _activeMines = new List<ActiveArcMineState>();
        private readonly List<CharacterTestArcMineDeployed> _deployedMines =
            new List<CharacterTestArcMineDeployed>();
        private readonly List<CharacterTestArcMineDeployed> _waitingVfxMines =
            new List<CharacterTestArcMineDeployed>();
        private readonly List<CharacterTestArcMineFuseStarted> _fuseStartedMines =
            new List<CharacterTestArcMineFuseStarted>();
        private readonly List<CharacterTestArcMineTriggered> _triggeredMines =
            new List<CharacterTestArcMineTriggered>();
        private readonly List<CombatQueryResult> _queryResults = new List<CombatQueryResult>();
        private int _nextRuntimeId = 1;

        public CharacterTestArcMineRuntime()
            : this(CharacterTestArcMineTuningSource.LoadDefault())
        {
        }

        public CharacterTestArcMineRuntime(CharacterTestArcMineTuning tuning)
        {
            _tuning = tuning ?? CharacterTestArcMineTuning.Default;
        }

        public CharacterTestArcMineTuning Tuning => _tuning;
        public CharacterTestArcMineTickSummary LastTickSummary { get; private set; }
        public int ActiveCount => _activeMines.Count;

        public CharacterTestArcMineDeployResult TryResolveDeployTarget(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity caster,
            Func<int> lockedTargetEntityIdProvider,
            out CharacterTestArcMineDeployTarget target)
        {
            target = default;
            if (world == null)
                return CharacterTestArcMineDeployResult.Failed("CharacterTest world is missing.");
            if (physicsRuntime?.World == null)
                return CharacterTestArcMineDeployResult.Failed("Combat physics world is missing.");
            if (caster == null || !caster.BattleState.CountsAsAlive)
                return CharacterTestArcMineDeployResult.Failed("Caster is missing or dead.");

            ResolveDeployCenter(
                world,
                physicsRuntime,
                caster,
                lockedTargetEntityIdProvider,
                out FixVector3 center,
                out string deployMode);
            target = new CharacterTestArcMineDeployTarget(center, ResolveForward(caster), deployMode);
            return CharacterTestArcMineDeployResult.Succeeded(default);
        }

        public CharacterTestArcMineDeployResult Deploy(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity caster,
            RuntimeFrame frame,
            int traceId,
            int localFrame,
            int sourceOrder,
            int actionInstanceId,
            Func<int> lockedTargetEntityIdProvider)
        {
            CharacterTestArcMineDeployResult targetResult = TryResolveDeployTarget(
                world,
                physicsRuntime,
                caster,
                lockedTargetEntityIdProvider,
                out CharacterTestArcMineDeployTarget target);
            if (!targetResult.Success)
                return targetResult;

            return CreateMine(
                world,
                physicsRuntime,
                caster,
                frame,
                target.Center,
                target.Forward,
                target.DeployMode,
                traceId,
                localFrame,
                sourceOrder,
                actionInstanceId);
        }

        public CharacterTestArcMineDeployResult CreateMine(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity caster,
            RuntimeFrame frame,
            FixVector3 center,
            FixVector3 forward,
            string deployMode,
            int traceId,
            int localFrame,
            int sourceOrder,
            int actionInstanceId)
        {
            if (world == null)
                return CharacterTestArcMineDeployResult.Failed("CharacterTest world is missing.");
            if (physicsRuntime?.World == null)
                return CharacterTestArcMineDeployResult.Failed("Combat physics world is missing.");
            if (caster == null || !caster.BattleState.CountsAsAlive)
                return CharacterTestArcMineDeployResult.Failed("Caster is missing or dead.");
            if (!forward.TryNormalize(out FixVector3 normalizedForward))
                normalizedForward = ResolveForward(caster);

            int runtimeId = _nextRuntimeId++;
            if (_nextRuntimeId <= 0)
                _nextRuntimeId = 1;

            long createdFrame = frame.Value;
            long armedFrame = createdFrame;
            long expireFrame = checked(createdFrame + _tuning.DurationFrames);
            CombatEntityId sourceCombatId = physicsRuntime.GetEntityId(caster);
            var state = new ActiveArcMineState(
                runtimeId,
                caster.Id.Value,
                sourceCombatId.Value,
                actionInstanceId,
                sourceOrder,
                localFrame,
                createdFrame,
                armedFrame,
                expireFrame,
                0L,
                0L,
                0,
                center,
                normalizedForward,
                traceId <= 0
                    ? CharacterTestCharacterActionConfigSource.ArcMineDeployStableEventId
                    : traceId);
            _activeMines.Add(state);
            var deployed = new CharacterTestArcMineDeployed(
                runtimeId,
                caster.Id.Value,
                center,
                normalizedForward,
                deployMode,
                state.TraceId,
                localFrame,
                sourceOrder,
                createdFrame,
                armedFrame,
                expireFrame);
            _deployedMines.Add(deployed);
            _waitingVfxMines.Add(deployed);
            LastTickSummary = new CharacterTestArcMineTickSummary(
                _activeMines.Count,
                _deployedMines.Count,
                _triggeredMines.Count,
                0,
                0,
                0,
                0);
            return CharacterTestArcMineDeployResult.Succeeded(deployed);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call paths may allocate diagnostics.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            RuntimeFrame frame)
        {
            _fuseStartedMines.Clear();
            _triggeredMines.Clear();
            int expiredCount = 0;
            int queryCount = 0;
            int queryHitCount = 0;
            int rejectedTargetCount = 0;

            if (world == null || physicsRuntime?.World == null || _activeMines.Count == 0)
            {
                LastTickSummary = new CharacterTestArcMineTickSummary(
                    _activeMines.Count,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
                return;
            }

            for (int i = _activeMines.Count - 1; i >= 0; i--)
            {
                ActiveArcMineState mine = _activeMines[i];
                if (!mine.HasFuse && frame.Value >= mine.ExpireFrame)
                {
                    _activeMines.RemoveAt(i);
                    expiredCount++;
                    continue;
                }

                CharacterTestCharacterEntity caster = FindEntity(world, mine.CasterEntityId);
                if (caster == null || !caster.BattleState.CountsAsAlive)
                {
                    _activeMines.RemoveAt(i);
                    expiredCount++;
                    continue;
                }

                if (mine.HasFuse)
                {
                    if (frame.Value >= mine.DetonateFrame)
                    {
                        _activeMines.RemoveAt(i);
                        _triggeredMines.Add(new CharacterTestArcMineTriggered(
                            mine.RuntimeId,
                            mine.CasterEntityId,
                            mine.TriggerTargetEntityId,
                            mine.SourceCombatEntityId,
                            mine.ActionInstanceId,
                            mine.SourceOrder,
                            mine.LocalFrame,
                            mine.Center,
                            mine.Forward,
                            default,
                            mine.TraceId,
                            frame.Value));
                    }

                    continue;
                }

                queryCount++;
                _queryResults.Clear();
                QueryTriggerSphere(physicsRuntime, mine, _queryResults);
                queryHitCount += _queryResults.Count;
                if (!TrySelectTriggerTarget(world, mine, caster, out int triggerTargetEntityId, out _))
                {
                    rejectedTargetCount += _queryResults.Count;
                    if (ShouldRefreshWaitingVfx(mine, frame.Value))
                        _waitingVfxMines.Add(CreateWaitingVfxPulse(mine));
                    continue;
                }

                long fuseStartedFrame = frame.Value;
                long detonateFrame = checked(fuseStartedFrame + _tuning.FuseDelayFrames);
                _activeMines[i] = mine.WithFuse(
                    fuseStartedFrame,
                    detonateFrame,
                    triggerTargetEntityId);
                _fuseStartedMines.Add(new CharacterTestArcMineFuseStarted(
                    mine.RuntimeId,
                    mine.CasterEntityId,
                    triggerTargetEntityId,
                    mine.SourceCombatEntityId,
                    mine.ActionInstanceId,
                    mine.SourceOrder,
                    mine.LocalFrame,
                    mine.Center,
                    mine.Forward,
                    mine.TraceId,
                    fuseStartedFrame,
                    detonateFrame));
            }

            _queryResults.Clear();
            LastTickSummary = new CharacterTestArcMineTickSummary(
                _activeMines.Count,
                0,
                _triggeredMines.Count,
                expiredCount,
                queryCount,
                queryHitCount,
                rejectedTargetCount);
        }

        public int DrainLastDeployedMines(List<CharacterTestArcMineDeployed> output)
        {
            if (output == null)
                return 0;

            for (int i = 0; i < _deployedMines.Count; i++)
                output.Add(_deployedMines[i]);
            int count = _deployedMines.Count;
            _deployedMines.Clear();
            return count;
        }

        public int DrainLastWaitingVfxMines(List<CharacterTestArcMineDeployed> output)
        {
            if (output == null)
                return 0;

            for (int i = 0; i < _waitingVfxMines.Count; i++)
                output.Add(_waitingVfxMines[i]);
            int count = _waitingVfxMines.Count;
            _waitingVfxMines.Clear();
            return count;
        }

        public int DrainLastTriggeredMines(List<CharacterTestArcMineTriggered> output)
        {
            if (output == null)
                return 0;

            for (int i = 0; i < _triggeredMines.Count; i++)
                output.Add(_triggeredMines[i]);
            int count = _triggeredMines.Count;
            _triggeredMines.Clear();
            return count;
        }

        public int DrainLastFuseStartedMines(List<CharacterTestArcMineFuseStarted> output)
        {
            if (output == null)
                return 0;

            for (int i = 0; i < _fuseStartedMines.Count; i++)
                output.Add(_fuseStartedMines[i]);
            int count = _fuseStartedMines.Count;
            _fuseStartedMines.Clear();
            return count;
        }

        private CharacterTestArcMineRuntimeState _hashCharacterTestArcMineRuntimeState;

        internal CharacterTestArcMineRuntimeState ReadStateView()
        {
            if (_hashCharacterTestArcMineRuntimeState == null)
            {
                var view = new CharacterTestRuntimeStateView<CharacterTestArcMineSnapshot>(() => _activeMines.Count, i => _activeMines[i].ToSnapshot());
            _hashCharacterTestArcMineRuntimeState = new CharacterTestArcMineRuntimeState(
                CharacterTestArcMineRuntimeState.CurrentSchemaVersion,
                CharacterTestArcMineRuntimeState.DefaultModuleId,
                _nextRuntimeId,
                view, borrow: true);
            }
            _hashCharacterTestArcMineRuntimeState.NextRuntimeId = _nextRuntimeId;
            return _hashCharacterTestArcMineRuntimeState;
        }

        public CharacterTestArcMineRuntimeState CreateStateSnapshot()
        {
            var snapshots = new CharacterTestArcMineSnapshot[_activeMines.Count];
            for (int i = 0; i < _activeMines.Count; i++)
                snapshots[i] = _activeMines[i].ToSnapshot();

            return new CharacterTestArcMineRuntimeState(
                CharacterTestArcMineRuntimeState.CurrentSchemaVersion,
                CharacterTestArcMineRuntimeState.DefaultModuleId,
                _nextRuntimeId,
                snapshots);
        }

        public bool TryApplyStateSnapshot(CharacterTestArcMineRuntimeState state, out string failure)
        {
            failure = string.Empty;
            if (state != null)
            {
                if (state.SchemaVersion != CharacterTestArcMineRuntimeState.CurrentSchemaVersion
                    || state.ModuleId != CharacterTestArcMineRuntimeState.DefaultModuleId
                    || state.NextRuntimeId <= 0)
                {
                    failure = "Unsupported Arc Mine snapshot version.";
                    return false;
                }
                var runtimeIds = new HashSet<int>();
                foreach (CharacterTestArcMineSnapshot mine in state.ActiveMines)
                {
                    if (mine.RuntimeId <= 0 || !runtimeIds.Add(mine.RuntimeId)
                        || mine.CasterEntityId <= 0 || mine.SourceCombatEntityId <= 0
                        || mine.TraceId < 0 || (mine.TraceId > 0 && !CharacterTestAuthoredContentIds.IsActive(401, mine.TraceId)))
                    {
                        failure = "Arc Mine snapshot contains an invalid or unknown integer identity.";
                        return false;
                    }
                }
            }
            _activeMines.Clear();
            _deployedMines.Clear();
            _waitingVfxMines.Clear();
            _fuseStartedMines.Clear();
            _triggeredMines.Clear();
            _queryResults.Clear();
            _nextRuntimeId = state?.NextRuntimeId > 0 ? state.NextRuntimeId : 1;
            if (state == null || state.ActiveCount == 0)
            {
                LastTickSummary = default;
                return true;
            }

            IReadOnlyList<CharacterTestArcMineSnapshot> mines = state.ActiveMines;
            for (int i = 0; i < mines.Count; i++)
            {
                CharacterTestArcMineSnapshot mine = mines[i];
                if (mine.RuntimeId <= 0 || mine.CasterEntityId <= 0 || mine.SourceCombatEntityId <= 0)
                    continue;

                _activeMines.Add(ActiveArcMineState.FromSnapshot(mine));
            }

            LastTickSummary = new CharacterTestArcMineTickSummary(
                _activeMines.Count,
                0,
                0,
                0,
                0,
                0,
                0);
            return true;
        }

        public void Clear()
        {
            _activeMines.Clear();
            _deployedMines.Clear();
            _waitingVfxMines.Clear();
            _fuseStartedMines.Clear();
            _triggeredMines.Clear();
            _queryResults.Clear();
            _nextRuntimeId = 1;
            LastTickSummary = default;
        }

        private void QueryTriggerSphere(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            ActiveArcMineState mine,
            List<CombatQueryResult> output)
        {
            int traceId = mine.TraceId;
            var header = new CombatQueryHeader(
                queryId: mine.RuntimeId,
                kind: CombatQueryKind.Sphere,
                sourceEntityId: new CombatEntityId(mine.SourceCombatEntityId),
                traceId: traceId,
                actionId: CharacterTestCharacterCombatTraceSystem.StablePositiveId(
                    CharacterTestCharacterActionConfigSource.ArcMineActionId),
                sourceOrder: mine.SourceOrder,
                layerMask: CombatPhysicsLayerMask.FromLayer(
                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer));
            physicsRuntime.World.QuerySphere(
                new CombatSphereQuery(header, mine.Center, _tuning.TriggerRadius),
                output,
                includeSourceEntity: false);
        }

        private bool TrySelectTriggerTarget(
            CharacterTestCharacterRuntimeWorld world,
            ActiveArcMineState mine,
            CharacterTestCharacterEntity caster,
            out int targetEntityId,
            out CombatQueryResult triggerQuery)
        {
            targetEntityId = 0;
            triggerQuery = default;
            for (int i = 0; i < _queryResults.Count; i++)
            {
                CombatQueryResult query = _queryResults[i];
                CharacterTestCharacterEntity target = FindEntityByCombatId(world, query.TargetEntityId);
                if (!IsHostileAlive(caster, target))
                    continue;

                targetEntityId = target.Id.Value;
                triggerQuery = query;
                return true;
            }

            return false;
        }

        private void ResolveDeployCenter(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity caster,
            Func<int> lockedTargetEntityIdProvider,
            out FixVector3 center,
            out string deployMode)
        {
            if (TryResolveLockedHostileTargetGroundCenter(
                    world,
                    physicsRuntime,
                    caster,
                    lockedTargetEntityIdProvider,
                    out center))
            {
                deployMode = LockedTargetDeployMode;
                return;
            }

            FixVector3 casterCenter = ResolveBodyCenter(physicsRuntime, caster);
            FixVector3 forward = ResolveForward(caster);
            center = ProjectToGround(physicsRuntime, caster, casterCenter)
                + new FixVector3(
                    forward.X * Fix64.FromInt(_tuning.FallbackForwardMeters),
                    Fix64.Zero,
                    forward.Z * Fix64.FromInt(_tuning.FallbackForwardMeters));
            deployMode = ForwardDeployMode;
        }

        private bool TryResolveLockedHostileTargetGroundCenter(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity caster,
            Func<int> lockedTargetEntityIdProvider,
            out FixVector3 center)
        {
            center = FixVector3.Zero;
            if (world == null || caster == null || lockedTargetEntityIdProvider == null)
                return false;

            int targetEntityId = lockedTargetEntityIdProvider();
            if (targetEntityId <= 0 || targetEntityId == caster.Id.Value)
                return false;

            CharacterTestCharacterEntity target = FindEntity(world, targetEntityId);
            if (!IsHostileAlive(caster, target))
                return false;

            FixVector3 casterCenter = ResolveBodyCenter(physicsRuntime, caster);
            FixVector3 targetCenter = ResolveBodyCenter(physicsRuntime, target);
            Fix64 maxRange = Fix64.FromInt(_tuning.DeployRangeMeters);
            if (HorizontalDistanceSquared(casterCenter, targetCenter) > maxRange * maxRange)
                return false;

            center = ProjectToGround(physicsRuntime, target, targetCenter);
            return true;
        }

        private static FixVector3 ResolveBodyCenter(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity entity)
        {
            if (physicsRuntime?.World != null
                && physicsRuntime.World.TryGetBody(physicsRuntime.GetBodyId(entity), out CombatPhysicsBody body))
            {
                return body.Position;
            }

            return new FixVector3(
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.XMillimeters),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.YMillimeters)
                    + (physicsRuntime?.GetCharacterCenterOffsetY(entity) ?? Fix64.One),
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.ZMillimeters));
        }

        private static FixVector3 ProjectToGround(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity entity,
            FixVector3 bodyCenter)
        {
            return new FixVector3(
                bodyCenter.X,
                CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.YMillimeters),
                bodyCenter.Z);
        }

        private static FixVector3 ResolveForward(CharacterTestCharacterEntity entity)
        {
            FixVector3 forward = CharacterTestFireballProjectileAdapter.ResolveDirection(entity);
            forward = new FixVector3(forward.X, Fix64.Zero, forward.Z);
            return forward.TryNormalize(out FixVector3 normalized)
                ? normalized
                : new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
        }

        private static bool IsHostileAlive(
            CharacterTestCharacterEntity caster,
            CharacterTestCharacterEntity target)
        {
            return caster != null
                && target != null
                && caster.Id.Value != target.Id.Value
                && caster.Team.TeamId != target.Team.TeamId
                && target.BattleState.CanReceiveHpDamage;
        }

        private static Fix64 HorizontalDistanceSquared(FixVector3 a, FixVector3 b)
        {
            Fix64 x = a.X - b.X;
            Fix64 z = a.Z - b.Z;
            return x * x + z * z;
        }

        private static CharacterTestCharacterEntity FindEntity(
            CharacterTestCharacterRuntimeWorld world,
            int entityId)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Id.Value == entityId)
                    return entities[i];
            }

            return null;
        }

        private static CharacterTestCharacterEntity FindEntityByCombatId(
            CharacterTestCharacterRuntimeWorld world,
            CombatEntityId combatEntityId)
        {
            int entityId = CharacterTestCombatEntityLookup.ResolveCharacterEntityId(combatEntityId);
            return entityId <= 0 ? null : FindEntity(world, entityId);
        }

        private static bool ShouldRefreshWaitingVfx(ActiveArcMineState mine, long frame)
        {
            long age = frame - mine.CreatedFrame;
            return age > 0L
                && age % CharacterTestArcMineIds.WaitingVfxRefreshIntervalFrames == 0L;
        }

        private static CharacterTestArcMineDeployed CreateWaitingVfxPulse(ActiveArcMineState mine)
        {
            return new CharacterTestArcMineDeployed(
                mine.RuntimeId,
                mine.CasterEntityId,
                mine.Center,
                mine.Forward,
                string.Empty,
                mine.TraceId,
                mine.LocalFrame,
                mine.SourceOrder,
                mine.CreatedFrame,
                mine.ArmedFrame,
                mine.ExpireFrame);
        }

        private readonly struct ActiveArcMineState
        {
            public ActiveArcMineState(
                int runtimeId,
                int casterEntityId,
                int sourceCombatEntityId,
                int actionInstanceId,
                int sourceOrder,
                int localFrame,
                long createdFrame,
                long armedFrame,
                long expireFrame,
                long fuseStartedFrame,
                long detonateFrame,
                int triggerTargetEntityId,
                FixVector3 center,
                FixVector3 forward,
                int traceId)
            {
                RuntimeId = runtimeId <= 0 ? 1 : runtimeId;
                CasterEntityId = casterEntityId < 0 ? 0 : casterEntityId;
                SourceCombatEntityId = sourceCombatEntityId < 0 ? 0 : sourceCombatEntityId;
                ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
                SourceOrder = sourceOrder < 0 ? 0 : sourceOrder;
                LocalFrame = localFrame < 0 ? 0 : localFrame;
                CreatedFrame = createdFrame < 0L ? 0L : createdFrame;
                ArmedFrame = armedFrame < 0L ? 0L : armedFrame;
                ExpireFrame = expireFrame < 0L ? 0L : expireFrame;
                FuseStartedFrame = fuseStartedFrame < 0L ? 0L : fuseStartedFrame;
                DetonateFrame = detonateFrame < 0L ? 0L : detonateFrame;
                TriggerTargetEntityId = triggerTargetEntityId < 0 ? 0 : triggerTargetEntityId;
                Center = center;
                Forward = forward;
                TraceId = traceId;
            }

            public int RuntimeId { get; }
            public int CasterEntityId { get; }
            public int SourceCombatEntityId { get; }
            public int ActionInstanceId { get; }
            public int SourceOrder { get; }
            public int LocalFrame { get; }
            public long CreatedFrame { get; }
            public long ArmedFrame { get; }
            public long ExpireFrame { get; }
            public long FuseStartedFrame { get; }
            public long DetonateFrame { get; }
            public int TriggerTargetEntityId { get; }
            public FixVector3 Center { get; }
            public FixVector3 Forward { get; }
            public int TraceId { get; }
            public bool HasFuse => DetonateFrame > 0L;

            public ActiveArcMineState WithFuse(
                long fuseStartedFrame,
                long detonateFrame,
                int triggerTargetEntityId)
            {
                return new ActiveArcMineState(
                    RuntimeId,
                    CasterEntityId,
                    SourceCombatEntityId,
                    ActionInstanceId,
                    SourceOrder,
                    LocalFrame,
                    CreatedFrame,
                    ArmedFrame,
                    ExpireFrame,
                    fuseStartedFrame,
                    detonateFrame,
                    triggerTargetEntityId,
                    Center,
                    Forward,
                    TraceId);
            }

            public static ActiveArcMineState FromSnapshot(CharacterTestArcMineSnapshot snapshot)
            {
                return new ActiveArcMineState(
                    snapshot.RuntimeId,
                    snapshot.CasterEntityId,
                    snapshot.SourceCombatEntityId,
                    snapshot.ActionInstanceId,
                    snapshot.SourceOrder,
                    snapshot.LocalFrame,
                    snapshot.CreatedFrame,
                    snapshot.ArmedFrame,
                    snapshot.ExpireFrame,
                    snapshot.FuseStartedFrame,
                    snapshot.DetonateFrame,
                    snapshot.TriggerTargetEntityId,
                    snapshot.Center,
                    snapshot.Forward,
                    snapshot.TraceId);
            }

            public CharacterTestArcMineSnapshot ToSnapshot()
            {
                return CharacterTestArcMineSnapshot.FromVectors(
                    RuntimeId,
                    CasterEntityId,
                    SourceCombatEntityId,
                    ActionInstanceId,
                    SourceOrder,
                    LocalFrame,
                    CreatedFrame,
                    ArmedFrame,
                    ExpireFrame,
                    FuseStartedFrame,
                    DetonateFrame,
                    TriggerTargetEntityId,
                    Center,
                    Forward,
                    TraceId);
            }
        }
    }

    internal readonly struct CharacterTestArcMineDeployTarget
    {
        public CharacterTestArcMineDeployTarget(FixVector3 center, FixVector3 forward, string deployMode)
        {
            Center = center;
            Forward = forward;
            DeployMode = deployMode ?? string.Empty;
        }

        public FixVector3 Center { get; }
        public FixVector3 Forward { get; }
        public string DeployMode { get; }
    }

    internal readonly struct CharacterTestArcMineDeployResult
    {
        private CharacterTestArcMineDeployResult(
            bool success,
            CharacterTestArcMineDeployed deployed,
            string failureReason)
        {
            Success = success;
            Deployed = deployed;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        public CharacterTestArcMineDeployed Deployed { get; }
        public string FailureReason { get; }

        public static CharacterTestArcMineDeployResult Succeeded(CharacterTestArcMineDeployed deployed)
        {
            return new CharacterTestArcMineDeployResult(true, deployed, string.Empty);
        }

        public static CharacterTestArcMineDeployResult Failed(string failureReason)
        {
            return new CharacterTestArcMineDeployResult(false, default, failureReason);
        }
    }

    internal readonly struct CharacterTestArcMineDeployed
    {
        public CharacterTestArcMineDeployed(
            int runtimeId,
            int casterEntityId,
            FixVector3 center,
            FixVector3 forward,
            string deployMode,
            int traceId,
            int localFrame,
            int sourceOrder,
            long createdFrame,
            long armedFrame,
            long expireFrame)
        {
            RuntimeId = runtimeId <= 0 ? 1 : runtimeId;
            CasterEntityId = casterEntityId < 0 ? 0 : casterEntityId;
            Center = center;
            Forward = forward;
            DeployMode = deployMode ?? string.Empty;
            TraceId = traceId;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
            SourceOrder = sourceOrder < 0 ? 0 : sourceOrder;
            CreatedFrame = createdFrame < 0L ? 0L : createdFrame;
            ArmedFrame = armedFrame < 0L ? 0L : armedFrame;
            ExpireFrame = expireFrame < 0L ? 0L : expireFrame;
        }

        public int RuntimeId { get; }
        public int CasterEntityId { get; }
        public FixVector3 Center { get; }
        public FixVector3 Forward { get; }
        public string DeployMode { get; }
        public int TraceId { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }
        public long CreatedFrame { get; }
        public long ArmedFrame { get; }
        public long ExpireFrame { get; }
    }

    internal readonly struct CharacterTestArcMineTriggered
    {
        public CharacterTestArcMineTriggered(
            int runtimeId,
            int casterEntityId,
            int triggerTargetEntityId,
            int sourceCombatEntityId,
            int actionInstanceId,
            int sourceOrder,
            int localFrame,
            FixVector3 center,
            FixVector3 forward,
            CombatQueryResult triggerQuery,
            int traceId,
            long triggeredFrame)
        {
            RuntimeId = runtimeId <= 0 ? 1 : runtimeId;
            CasterEntityId = casterEntityId < 0 ? 0 : casterEntityId;
            TriggerTargetEntityId = triggerTargetEntityId < 0 ? 0 : triggerTargetEntityId;
            SourceCombatEntityId = sourceCombatEntityId < 0 ? 0 : sourceCombatEntityId;
            ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
            SourceOrder = sourceOrder < 0 ? 0 : sourceOrder;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
            Center = center;
            Forward = forward;
            TriggerQuery = triggerQuery;
            TraceId = traceId;
            TriggeredFrame = triggeredFrame < 0L ? 0L : triggeredFrame;
        }

        public int RuntimeId { get; }
        public int CasterEntityId { get; }
        public int TriggerTargetEntityId { get; }
        public int SourceCombatEntityId { get; }
        public int ActionInstanceId { get; }
        public int SourceOrder { get; }
        public int LocalFrame { get; }
        public FixVector3 Center { get; }
        public FixVector3 Forward { get; }
        public CombatQueryResult TriggerQuery { get; }
        public int TraceId { get; }
        public long TriggeredFrame { get; }
    }

    internal readonly struct CharacterTestArcMineFuseStarted
    {
        public CharacterTestArcMineFuseStarted(
            int runtimeId,
            int casterEntityId,
            int triggerTargetEntityId,
            int sourceCombatEntityId,
            int actionInstanceId,
            int sourceOrder,
            int localFrame,
            FixVector3 center,
            FixVector3 forward,
            int traceId,
            long fuseStartedFrame,
            long detonateFrame)
        {
            RuntimeId = runtimeId;
            CasterEntityId = casterEntityId;
            TriggerTargetEntityId = triggerTargetEntityId;
            SourceCombatEntityId = sourceCombatEntityId;
            ActionInstanceId = actionInstanceId;
            SourceOrder = sourceOrder;
            LocalFrame = localFrame;
            Center = center;
            Forward = forward;
            TraceId = traceId;
            FuseStartedFrame = fuseStartedFrame;
            DetonateFrame = detonateFrame;
        }

        public int RuntimeId { get; }
        public int CasterEntityId { get; }
        public int TriggerTargetEntityId { get; }
        public int SourceCombatEntityId { get; }
        public int ActionInstanceId { get; }
        public int SourceOrder { get; }
        public int LocalFrame { get; }
        public FixVector3 Center { get; }
        public FixVector3 Forward { get; }
        public int TraceId { get; }
        public long FuseStartedFrame { get; }
        public long DetonateFrame { get; }
    }

    public readonly struct CharacterTestArcMineTickSummary
    {
        public CharacterTestArcMineTickSummary(
            int activeCount,
            int deployedCount,
            int triggeredCount,
            int expiredCount,
            int queryCount,
            int queryHitCount,
            int rejectedTargetCount)
        {
            ActiveCount = activeCount < 0 ? 0 : activeCount;
            DeployedCount = deployedCount < 0 ? 0 : deployedCount;
            TriggeredCount = triggeredCount < 0 ? 0 : triggeredCount;
            ExpiredCount = expiredCount < 0 ? 0 : expiredCount;
            QueryCount = queryCount < 0 ? 0 : queryCount;
            QueryHitCount = queryHitCount < 0 ? 0 : queryHitCount;
            RejectedTargetCount = rejectedTargetCount < 0 ? 0 : rejectedTargetCount;
        }

        public int ActiveCount { get; }
        public int DeployedCount { get; }
        public int TriggeredCount { get; }
        public int ExpiredCount { get; }
        public int QueryCount { get; }
        public int QueryHitCount { get; }
        public int RejectedTargetCount { get; }
    }
}


