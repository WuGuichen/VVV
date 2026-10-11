using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuneTurretRuntime
    {
        private const string LockedTargetDeployMode = "locked_target";
        private const string ForwardDeployMode = "caster_forward";
        internal const string ProjectileImpactDeployMode = "projectile_impact";
        internal const string ProjectileExpirationDeployMode = "projectile_expire";

        private readonly CharacterTestRuneTurretTuning _tuning;
        private readonly List<ActiveRuneTurretState> _activeTurrets = new List<ActiveRuneTurretState>();
        private readonly List<CharacterTestRuneTurretDeployed> _deployedTurrets =
            new List<CharacterTestRuneTurretDeployed>();
        private readonly List<CharacterTestRuneTurretFired> _firedTurrets =
            new List<CharacterTestRuneTurretFired>();
        private readonly List<CharacterTestRuneTurretExpired> _expiredTurrets =
            new List<CharacterTestRuneTurretExpired>();
        private readonly List<CombatQueryResult> _queryResults = new List<CombatQueryResult>();
        private int _nextRuntimeId = 1;

        public CharacterTestRuneTurretRuntime()
            : this(CharacterTestRuneTurretTuningSource.LoadDefault())
        {
        }

        public CharacterTestRuneTurretRuntime(CharacterTestRuneTurretTuning tuning)
        {
            _tuning = tuning ?? CharacterTestRuneTurretTuning.Default;
        }

        public CharacterTestRuneTurretTuning Tuning => _tuning;
        public CharacterTestRuneTurretTickSummary LastTickSummary { get; private set; }
        public int ActiveCount => _activeTurrets.Count;

        public CharacterTestRuneTurretDeployResult TryResolveDeployTarget(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity caster,
            Func<int> lockedTargetEntityIdProvider,
            out CharacterTestRuneTurretDeployTarget target)
        {
            target = default;
            if (world == null)
                return CharacterTestRuneTurretDeployResult.Failed("CharacterTest world is missing.");
            if (physicsRuntime?.World == null)
                return CharacterTestRuneTurretDeployResult.Failed("Combat physics world is missing.");
            if (caster == null || !caster.BattleState.CountsAsAlive)
                return CharacterTestRuneTurretDeployResult.Failed("Caster is missing or dead.");

            ResolveDeployCenter(
                world,
                physicsRuntime,
                caster,
                lockedTargetEntityIdProvider,
                out FixVector3 center,
                out string deployMode);
            target = new CharacterTestRuneTurretDeployTarget(center, ResolveForward(caster), deployMode);
            return CharacterTestRuneTurretDeployResult.Succeeded(default);
        }

        public CharacterTestRuneTurretDeployResult CreateTurret(
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
            if (traceId <= 0 || !CharacterTestAuthoredContentIds.IsActive(401, traceId))
                return CharacterTestRuneTurretDeployResult.Failed("Rune turret source event ID is missing or unknown.");
            if (world == null)
                return CharacterTestRuneTurretDeployResult.Failed("CharacterTest world is missing.");
            if (physicsRuntime?.World == null)
                return CharacterTestRuneTurretDeployResult.Failed("Combat physics world is missing.");
            if (caster == null || !caster.BattleState.CountsAsAlive)
                return CharacterTestRuneTurretDeployResult.Failed("Caster is missing or dead.");
            if (!forward.TryNormalize(out FixVector3 normalizedForward))
                normalizedForward = ResolveForward(caster);

            int runtimeId = _nextRuntimeId++;
            if (_nextRuntimeId <= 0)
                _nextRuntimeId = 1;

            long createdFrame = frame.Value;
            long activateFrame = checked(createdFrame + _tuning.WarmupFrames);
            long expireFrame = checked(createdFrame + _tuning.DurationFrames);
            CombatEntityId sourceCombatId = physicsRuntime.GetEntityId(caster);
            var state = new ActiveRuneTurretState(
                runtimeId,
                caster.Id.Value,
                sourceCombatId.Value,
                actionInstanceId,
                sourceOrder,
                localFrame,
                createdFrame,
                activateFrame,
                expireFrame,
                activateFrame,
                0,
                center,
                normalizedForward,
                traceId);
            _activeTurrets.Add(state);

            var deployed = new CharacterTestRuneTurretDeployed(
                runtimeId,
                caster.Id.Value,
                center,
                normalizedForward,
                deployMode,
                state.TraceId,
                localFrame,
                sourceOrder,
                createdFrame,
                activateFrame,
                expireFrame);
            _deployedTurrets.Add(deployed);
            LastTickSummary = new CharacterTestRuneTurretTickSummary(
                _activeTurrets.Count,
                _deployedTurrets.Count,
                _firedTurrets.Count,
                _expiredTurrets.Count,
                0,
                0,
                0);
            return CharacterTestRuneTurretDeployResult.Succeeded(deployed);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call paths may allocate diagnostics.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            RuntimeFrame frame)
        {
            _firedTurrets.Clear();
            _expiredTurrets.Clear();
            int queryCount = 0;
            int queryHitCount = 0;
            int rejectedTargetCount = 0;

            if (world == null || physicsRuntime?.World == null || _activeTurrets.Count == 0)
            {
                LastTickSummary = new CharacterTestRuneTurretTickSummary(
                    _activeTurrets.Count,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
                return;
            }

            for (int i = _activeTurrets.Count - 1; i >= 0; i--)
            {
                ActiveRuneTurretState turret = _activeTurrets[i];
                CharacterTestCharacterEntity caster = FindEntity(world, turret.CasterEntityId);
                if (caster == null
                    || !caster.BattleState.CountsAsAlive
                    || frame.Value >= turret.ExpireFrame
                    || turret.ShotsFired >= _tuning.MaxShots)
                {
                    _activeTurrets.RemoveAt(i);
                    _expiredTurrets.Add(CreateExpired(turret, frame.Value));
                    continue;
                }

                if (frame.Value < turret.ActivateFrame || frame.Value < turret.NextThinkFrame)
                    continue;

                queryCount++;
                _queryResults.Clear();
                QueryScanSphere(physicsRuntime, turret, _queryResults);
                queryHitCount += _queryResults.Count;
                if (!TrySelectNearestTarget(
                        world,
                        physicsRuntime,
                        turret,
                        caster,
                        out CharacterTestCharacterEntity target,
                        out FixVector3 targetCenter))
                {
                    rejectedTargetCount += _queryResults.Count;
                    _activeTurrets[i] = turret.WithNextThinkFrame(frame.Value + _tuning.ScanIntervalFrames);
                    continue;
                }

                int shotsFired = turret.ShotsFired + 1;
                _firedTurrets.Add(new CharacterTestRuneTurretFired(
                    turret.RuntimeId,
                    turret.CasterEntityId,
                    target.Id.Value,
                    turret.SourceCombatEntityId,
                    turret.ActionInstanceId,
                    turret.SourceOrder,
                    turret.LocalFrame,
                    turret.Center,
                    turret.Forward,
                    targetCenter,
                    turret.TraceId,
                    frame.Value,
                    shotsFired));
                _activeTurrets[i] = turret.WithFire(frame.Value + _tuning.FireIntervalFrames, shotsFired);
            }

            _queryResults.Clear();
            LastTickSummary = new CharacterTestRuneTurretTickSummary(
                _activeTurrets.Count,
                0,
                _firedTurrets.Count,
                _expiredTurrets.Count,
                queryCount,
                queryHitCount,
                rejectedTargetCount);
        }

        public int DrainLastDeployedTurrets(List<CharacterTestRuneTurretDeployed> output)
        {
            if (output == null)
                return 0;

            for (int i = 0; i < _deployedTurrets.Count; i++)
                output.Add(_deployedTurrets[i]);
            int count = _deployedTurrets.Count;
            _deployedTurrets.Clear();
            return count;
        }

        public int DrainLastFiredTurrets(List<CharacterTestRuneTurretFired> output)
        {
            if (output == null)
                return 0;

            for (int i = 0; i < _firedTurrets.Count; i++)
                output.Add(_firedTurrets[i]);
            int count = _firedTurrets.Count;
            _firedTurrets.Clear();
            return count;
        }

        public int DrainLastExpiredTurrets(List<CharacterTestRuneTurretExpired> output)
        {
            if (output == null)
                return 0;

            for (int i = 0; i < _expiredTurrets.Count; i++)
                output.Add(_expiredTurrets[i]);
            int count = _expiredTurrets.Count;
            _expiredTurrets.Clear();
            return count;
        }

        private CharacterTestRuneTurretRuntimeState _hashCharacterTestRuneTurretRuntimeState;

        internal CharacterTestRuneTurretRuntimeState ReadStateView()
        {
            if (_hashCharacterTestRuneTurretRuntimeState == null)
            {
                var view = new CharacterTestRuntimeStateView<CharacterTestRuneTurretSnapshot>(() => _activeTurrets.Count, i => _activeTurrets[i].ToSnapshot());
            _hashCharacterTestRuneTurretRuntimeState = new CharacterTestRuneTurretRuntimeState(
                CharacterTestRuneTurretRuntimeState.CurrentSchemaVersion,
                CharacterTestRuneTurretRuntimeState.DefaultModuleId,
                _nextRuntimeId,
                view, borrow: true);
            }
            _hashCharacterTestRuneTurretRuntimeState.NextRuntimeId = _nextRuntimeId;
            return _hashCharacterTestRuneTurretRuntimeState;
        }

        public CharacterTestRuneTurretRuntimeState CreateStateSnapshot()
        {
            var snapshots = new CharacterTestRuneTurretSnapshot[_activeTurrets.Count];
            for (int i = 0; i < _activeTurrets.Count; i++)
                snapshots[i] = _activeTurrets[i].ToSnapshot();

            return new CharacterTestRuneTurretRuntimeState(
                CharacterTestRuneTurretRuntimeState.CurrentSchemaVersion,
                CharacterTestRuneTurretRuntimeState.DefaultModuleId,
                _nextRuntimeId,
                snapshots);
        }

        public bool TryApplyStateSnapshot(CharacterTestRuneTurretRuntimeState state, out string failure)
        {
            failure = string.Empty;
            if (state != null)
            {
                if (state.SchemaVersion != CharacterTestRuneTurretRuntimeState.CurrentSchemaVersion || state.ModuleId != CharacterTestRuneTurretRuntimeState.DefaultModuleId || state.NextRuntimeId <= 0)
                { failure = "Rune turret snapshot schema/counter is invalid."; return false; }
                var ids = new HashSet<int>();
                for (int i = 0; i < state.ActiveCount; i++)
                {
                    CharacterTestRuneTurretSnapshot item = state.ActiveTurrets[i];
                    if (item.RuntimeId <= 0 || item.RuntimeId >= state.NextRuntimeId || !ids.Add(item.RuntimeId)
                        || item.CasterEntityId <= 0 || item.SourceCombatEntityId <= 0
                        || item.TraceId <= 0 || !CharacterTestAuthoredContentIds.IsActive(401, item.TraceId))
                    { failure = "Rune turret snapshot contains an invalid instance or authored source event ID."; return false; }
                }
            }
            _activeTurrets.Clear();
            _deployedTurrets.Clear();
            _firedTurrets.Clear();
            _expiredTurrets.Clear();
            _queryResults.Clear();
            _nextRuntimeId = state?.NextRuntimeId > 0 ? state.NextRuntimeId : 1;
            if (state == null || state.ActiveCount == 0)
            {
                LastTickSummary = default;
                return true;
            }

            IReadOnlyList<CharacterTestRuneTurretSnapshot> turrets = state.ActiveTurrets;
            for (int i = 0; i < turrets.Count; i++)
            {
                CharacterTestRuneTurretSnapshot turret = turrets[i];
                _activeTurrets.Add(ActiveRuneTurretState.FromSnapshot(turret));
            }

            LastTickSummary = new CharacterTestRuneTurretTickSummary(
                _activeTurrets.Count,
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
            _activeTurrets.Clear();
            _deployedTurrets.Clear();
            _firedTurrets.Clear();
            _expiredTurrets.Clear();
            _queryResults.Clear();
            _nextRuntimeId = 1;
            LastTickSummary = default;
        }

        private void QueryScanSphere(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            ActiveRuneTurretState turret,
            List<CombatQueryResult> output)
        {
            int traceId = turret.TraceId;
            var header = new CombatQueryHeader(
                queryId: turret.RuntimeId,
                kind: CombatQueryKind.Sphere,
                sourceEntityId: new CombatEntityId(turret.SourceCombatEntityId),
                traceId: traceId,
                actionId: CharacterTestCharacterCombatTraceSystem.StablePositiveId(
                    CharacterTestCharacterActionConfigSource.RuneTurretActionId),
                sourceOrder: turret.SourceOrder,
                layerMask: CombatPhysicsLayerMask.FromLayer(
                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer));
            physicsRuntime.World.QuerySphere(
                new CombatSphereQuery(header, turret.Center, _tuning.ScanRadius),
                output,
                includeSourceEntity: false);
        }

        private bool TrySelectNearestTarget(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            ActiveRuneTurretState turret,
            CharacterTestCharacterEntity caster,
            out CharacterTestCharacterEntity selectedTarget,
            out FixVector3 selectedCenter)
        {
            selectedTarget = null;
            selectedCenter = FixVector3.Zero;
            Fix64 bestDistanceSquared = Fix64.Zero;
            bool hasBest = false;
            for (int i = 0; i < _queryResults.Count; i++)
            {
                CombatQueryResult query = _queryResults[i];
                CharacterTestCharacterEntity target = FindEntityByCombatId(world, query.TargetEntityId);
                if (!IsHostileAlive(caster, target))
                    continue;

                FixVector3 center = ResolveBodyCenter(physicsRuntime, target);
                Fix64 distanceSquared = HorizontalDistanceSquared(turret.Center, center);
                if (!hasBest
                    || distanceSquared < bestDistanceSquared
                    || (distanceSquared == bestDistanceSquared
                        && target.Id.Value < selectedTarget.Id.Value))
                {
                    selectedTarget = target;
                    selectedCenter = center;
                    bestDistanceSquared = distanceSquared;
                    hasBest = true;
                }
            }

            return hasBest;
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

        private static CharacterTestRuneTurretExpired CreateExpired(
            ActiveRuneTurretState turret,
            long frame)
        {
            return new CharacterTestRuneTurretExpired(
                turret.RuntimeId,
                turret.CasterEntityId,
                turret.SourceCombatEntityId,
                turret.ActionInstanceId,
                turret.SourceOrder,
                turret.LocalFrame,
                turret.Center,
                turret.Forward,
                turret.TraceId,
                frame,
                turret.ShotsFired);
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

        private readonly struct ActiveRuneTurretState
        {
            public ActiveRuneTurretState(
                int runtimeId,
                int casterEntityId,
                int sourceCombatEntityId,
                int actionInstanceId,
                int sourceOrder,
                int localFrame,
                long createdFrame,
                long activateFrame,
                long expireFrame,
                long nextThinkFrame,
                int shotsFired,
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
                ActivateFrame = activateFrame < 0L ? 0L : activateFrame;
                ExpireFrame = expireFrame < 0L ? 0L : expireFrame;
                NextThinkFrame = nextThinkFrame < 0L ? 0L : nextThinkFrame;
                ShotsFired = shotsFired < 0 ? 0 : shotsFired;
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
            public long ActivateFrame { get; }
            public long ExpireFrame { get; }
            public long NextThinkFrame { get; }
            public int ShotsFired { get; }
            public FixVector3 Center { get; }
            public FixVector3 Forward { get; }
            /// <summary>Authored source event identity (domain 401), not an execution trace.</summary>
            public int TraceId { get; }

            public ActiveRuneTurretState WithNextThinkFrame(long nextThinkFrame)
            {
                return new ActiveRuneTurretState(
                    RuntimeId,
                    CasterEntityId,
                    SourceCombatEntityId,
                    ActionInstanceId,
                    SourceOrder,
                    LocalFrame,
                    CreatedFrame,
                    ActivateFrame,
                    ExpireFrame,
                    nextThinkFrame,
                    ShotsFired,
                    Center,
                    Forward,
                    TraceId);
            }

            public ActiveRuneTurretState WithFire(long nextThinkFrame, int shotsFired)
            {
                return new ActiveRuneTurretState(
                    RuntimeId,
                    CasterEntityId,
                    SourceCombatEntityId,
                    ActionInstanceId,
                    SourceOrder,
                    LocalFrame,
                    CreatedFrame,
                    ActivateFrame,
                    ExpireFrame,
                    nextThinkFrame,
                    shotsFired,
                    Center,
                    Forward,
                    TraceId);
            }

            public static ActiveRuneTurretState FromSnapshot(CharacterTestRuneTurretSnapshot snapshot)
            {
                return new ActiveRuneTurretState(
                    snapshot.RuntimeId,
                    snapshot.CasterEntityId,
                    snapshot.SourceCombatEntityId,
                    snapshot.ActionInstanceId,
                    snapshot.SourceOrder,
                    snapshot.LocalFrame,
                    snapshot.CreatedFrame,
                    snapshot.ActivateFrame,
                    snapshot.ExpireFrame,
                    snapshot.NextThinkFrame,
                    snapshot.ShotsFired,
                    snapshot.Center,
                    snapshot.Forward,
                    snapshot.TraceId);
            }

            public CharacterTestRuneTurretSnapshot ToSnapshot()
            {
                return CharacterTestRuneTurretSnapshot.FromVectors(
                    RuntimeId,
                    CasterEntityId,
                    SourceCombatEntityId,
                    ActionInstanceId,
                    SourceOrder,
                    LocalFrame,
                    CreatedFrame,
                    ActivateFrame,
                    ExpireFrame,
                    NextThinkFrame,
                    ShotsFired,
                    Center,
                    Forward,
                    TraceId);
            }
        }
    }

    internal readonly struct CharacterTestRuneTurretDeployTarget
    {
        public CharacterTestRuneTurretDeployTarget(FixVector3 center, FixVector3 forward, string deployMode)
        {
            Center = center;
            Forward = forward;
            DeployMode = deployMode ?? string.Empty;
        }

        public FixVector3 Center { get; }
        public FixVector3 Forward { get; }
        public string DeployMode { get; }
    }

    internal readonly struct CharacterTestRuneTurretDeployResult
    {
        private CharacterTestRuneTurretDeployResult(
            bool success,
            string failureReason,
            CharacterTestRuneTurretDeployed deployed)
        {
            Success = success;
            FailureReason = failureReason ?? string.Empty;
            Deployed = deployed;
        }

        public bool Success { get; }
        public string FailureReason { get; }
        public CharacterTestRuneTurretDeployed Deployed { get; }

        public static CharacterTestRuneTurretDeployResult Succeeded(CharacterTestRuneTurretDeployed deployed)
        {
            return new CharacterTestRuneTurretDeployResult(true, string.Empty, deployed);
        }

        public static CharacterTestRuneTurretDeployResult Failed(string failureReason)
        {
            return new CharacterTestRuneTurretDeployResult(false, failureReason, default);
        }
    }

    internal readonly struct CharacterTestRuneTurretTickSummary
    {
        public CharacterTestRuneTurretTickSummary(
            int activeTurretCount,
            int deployedCount,
            int firedCount,
            int expiredCount,
            int queryCount,
            int queryHitCount,
            int rejectedTargetCount)
        {
            ActiveTurretCount = activeTurretCount;
            DeployedCount = deployedCount;
            FiredCount = firedCount;
            ExpiredCount = expiredCount;
            QueryCount = queryCount;
            QueryHitCount = queryHitCount;
            RejectedTargetCount = rejectedTargetCount;
        }

        public int ActiveTurretCount { get; }
        public int DeployedCount { get; }
        public int FiredCount { get; }
        public int ExpiredCount { get; }
        public int QueryCount { get; }
        public int QueryHitCount { get; }
        public int RejectedTargetCount { get; }
    }

    internal readonly struct CharacterTestRuneTurretDeployed
    {
        public CharacterTestRuneTurretDeployed(
            int runtimeId,
            int casterEntityId,
            FixVector3 center,
            FixVector3 forward,
            string deployMode,
            int traceId,
            int localFrame,
            int sourceOrder,
            long createdFrame,
            long activateFrame,
            long expireFrame)
        {
            RuntimeId = runtimeId;
            CasterEntityId = casterEntityId;
            Center = center;
            Forward = forward;
            DeployMode = deployMode ?? string.Empty;
            TraceId = traceId;
            LocalFrame = localFrame;
            SourceOrder = sourceOrder;
            CreatedFrame = createdFrame;
            ActivateFrame = activateFrame;
            ExpireFrame = expireFrame;
        }

        public int RuntimeId { get; }
        public int CasterEntityId { get; }
        public FixVector3 Center { get; }
        public FixVector3 Forward { get; }
        public string DeployMode { get; }
        /// <summary>Authored source event identity (domain 401), not an execution trace.</summary>
            public int TraceId { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }
        public long CreatedFrame { get; }
        public long ActivateFrame { get; }
        public long ExpireFrame { get; }
    }

    internal readonly struct CharacterTestRuneTurretFired
    {
        public CharacterTestRuneTurretFired(
            int runtimeId,
            int casterEntityId,
            int targetEntityId,
            int sourceCombatEntityId,
            int actionInstanceId,
            int sourceOrder,
            int localFrame,
            FixVector3 center,
            FixVector3 forward,
            FixVector3 targetCenter,
            int traceId,
            long fireFrame,
            int shotsFired)
        {
            RuntimeId = runtimeId;
            CasterEntityId = casterEntityId;
            TargetEntityId = targetEntityId;
            SourceCombatEntityId = sourceCombatEntityId;
            ActionInstanceId = actionInstanceId;
            SourceOrder = sourceOrder;
            LocalFrame = localFrame;
            Center = center;
            Forward = forward;
            TargetCenter = targetCenter;
            TraceId = traceId;
            FireFrame = fireFrame;
            ShotsFired = shotsFired;
        }

        public int RuntimeId { get; }
        public int CasterEntityId { get; }
        public int TargetEntityId { get; }
        public int SourceCombatEntityId { get; }
        public int ActionInstanceId { get; }
        public int SourceOrder { get; }
        public int LocalFrame { get; }
        public FixVector3 Center { get; }
        public FixVector3 Forward { get; }
        public FixVector3 TargetCenter { get; }
        /// <summary>Authored source event identity (domain 401), not an execution trace.</summary>
            public int TraceId { get; }
        public long FireFrame { get; }
        public int ShotsFired { get; }
    }

    internal readonly struct CharacterTestRuneTurretExpired
    {
        public CharacterTestRuneTurretExpired(
            int runtimeId,
            int casterEntityId,
            int sourceCombatEntityId,
            int actionInstanceId,
            int sourceOrder,
            int localFrame,
            FixVector3 center,
            FixVector3 forward,
            int traceId,
            long expiredFrame,
            int shotsFired)
        {
            RuntimeId = runtimeId;
            CasterEntityId = casterEntityId;
            SourceCombatEntityId = sourceCombatEntityId;
            ActionInstanceId = actionInstanceId;
            SourceOrder = sourceOrder;
            LocalFrame = localFrame;
            Center = center;
            Forward = forward;
            TraceId = traceId;
            ExpiredFrame = expiredFrame;
            ShotsFired = shotsFired;
        }

        public int RuntimeId { get; }
        public int CasterEntityId { get; }
        public int SourceCombatEntityId { get; }
        public int ActionInstanceId { get; }
        public int SourceOrder { get; }
        public int LocalFrame { get; }
        public FixVector3 Center { get; }
        public FixVector3 Forward { get; }
        /// <summary>Authored source event identity (domain 401), not an execution trace.</summary>
            public int TraceId { get; }
        public long ExpiredFrame { get; }
        public int ShotsFired { get; }
    }
}


