using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Combat.Projectiles
{
    public sealed class CombatProjectileRuntime
    {
        private readonly CombatProjectileDefinitionRegistry _definitions;
        private readonly ICombatProjectileSpawnSource _spawnSource;
        private readonly CombatPhysicsWorld _physicsWorld;
        private readonly ICombatProjectileImpactResolver _impactResolver;
        private readonly ICombatProjectileTrackingTargetProvider _trackingTargetProvider;
        private readonly ICombatProjectileDirectSweepProvider _directSweepProvider;
        private readonly ITeamRelationProvider _teamRelationProvider;
        private readonly IHitTargetStateResolver _targetStateResolver;
        private readonly bool _allowFriendlyFire;
        private readonly List<ActiveProjectile> _activeProjectiles = new List<ActiveProjectile>();
        private readonly List<CombatProjectileSpawnRequest> _spawnRequests =
            new List<CombatProjectileSpawnRequest>();
        private readonly List<CombatCapsuleQuery> _directSweepQueries =
            new List<CombatCapsuleQuery>(8);
        private readonly List<CombatQueryResult> _queryResults = new List<CombatQueryResult>();
        private readonly List<HitCandidate> _singleCandidate = new List<HitCandidate>(1);
        private readonly List<HitResolveResult> _singleResult = new List<HitResolveResult>(1);
        private readonly HashSet<WeaponHitOnceKey> _hitOnceKeys = new HashSet<WeaponHitOnceKey>();
        private readonly HitResolveSystem _hitResolve = new HitResolveSystem();
        private readonly List<CombatProjectileSpawnEvent> _frameSpawns =
            new List<CombatProjectileSpawnEvent>();
        private readonly List<CombatProjectileAreaPulseTriggerEvent> _frameAreaPulseTriggers =
            new List<CombatProjectileAreaPulseTriggerEvent>();
        private readonly List<CombatProjectileImpactEvent> _frameImpacts =
            new List<CombatProjectileImpactEvent>();
        private readonly List<CombatProjectileExpiredEvent> _frameExpirations =
            new List<CombatProjectileExpiredEvent>();
        private readonly List<CombatProjectileRejectedEvent> _frameRejections =
            new List<CombatProjectileRejectedEvent>();
        private readonly List<CombatProjectilePhysicalEvent> _framePhysicalEvents =
            new List<CombatProjectilePhysicalEvent>();
        private readonly CombatProjectileFrameEventBuffer _lastFrameEventBuffer =
            new CombatProjectileFrameEventBuffer();
        private int _nextProjectileId = 1;
        private int _nextSpawnSequence;
        private CombatProjectileFrameEvents _lastFrameEvents = CombatProjectileFrameEvents.Empty;

        public CombatProjectileRuntime(
            CombatProjectileDefinitionRegistry definitions,
            ICombatProjectileSpawnSource spawnSource,
            CombatPhysicsWorld physicsWorld,
            ICombatProjectileImpactResolver impactResolver,
            ITeamRelationProvider teamRelationProvider = null,
            IHitTargetStateResolver targetStateResolver = null,
            bool allowFriendlyFire = false,
            ICombatProjectileTrackingTargetProvider trackingTargetProvider = null,
            ICombatProjectileDirectSweepProvider directSweepProvider = null)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _spawnSource = spawnSource ?? throw new ArgumentNullException(nameof(spawnSource));
            _physicsWorld = physicsWorld ?? throw new ArgumentNullException(nameof(physicsWorld));
            _impactResolver = impactResolver ?? throw new ArgumentNullException(nameof(impactResolver));
            _trackingTargetProvider = trackingTargetProvider;
            _directSweepProvider = directSweepProvider;
            _teamRelationProvider = teamRelationProvider;
            _targetStateResolver = targetStateResolver;
            _allowFriendlyFire = allowFriendlyFire;
        }

        public int ActiveCount => _activeProjectiles.Count;
        public int NextProjectileId => _nextProjectileId;
        public long DefinitionHash => _definitions.ComputeStableHash();
        public CombatProjectileFrameEvents LastFrameEvents => _lastFrameEvents;

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public CombatProjectileFrameEvents Tick(CombatFrame frame)
        {
            TickCore(frame);
            CopyLastFrameEvents();
            _lastFrameEvents = CreateFrameEventsSnapshot();
            return _lastFrameEvents;
        }

        /// <summary>
        /// NoAlloc Tick output path when <paramref name="events"/> is reused and sized during warmup.
        /// </summary>
        /// <remarks>NoAlloc on the warmed steady-state path; output capacity growth and invalid-call diagnostics may allocate.</remarks>
        public void Tick(CombatFrame frame, CombatProjectileFrameEventBuffer events)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));

            TickCore(frame);
            CopyLastFrameEvents();
            events.CopyFrom(_lastFrameEventBuffer);
            _lastFrameEvents = CombatProjectileFrameEvents.Empty;
        }

        private void TickCore(CombatFrame frame)
        {
            BeginFrame();
            DrainSpawnRequests(frame);
            _activeProjectiles.Sort(ActiveProjectileComparer.Instance);

            for (int i = 0; i < _activeProjectiles.Count;)
            {
                ActiveProjectile projectile = _activeProjectiles[i];
                if (!_definitions.TryGet(projectile.DefinitionId, out CombatProjectileDefinition definition))
                {
                    _frameExpirations.Add(new CombatProjectileExpiredEvent(projectile.ToSnapshot(string.Empty, default)));
                    _activeProjectiles.RemoveAt(i);
                    continue;
                }

                if (frame <= projectile.SpawnFrame || frame <= projectile.LastFrame)
                {
                    i++;
                    continue;
                }

                bool removed = AdvanceProjectile(frame, definition, ref projectile);
                if (removed)
                {
                    _activeProjectiles.RemoveAt(i);
                    continue;
                }

                _activeProjectiles[i] = projectile;
                i++;
            }

        }

        private void CopyLastFrameEvents()
        {
            _lastFrameEventBuffer.CopyFrom(
                _frameSpawns,
                _frameAreaPulseTriggers,
                _frameImpacts,
                _frameExpirations,
                _frameRejections,
                _framePhysicalEvents);
        }

        private CombatProjectileFrameEvents CreateFrameEventsSnapshot()
        {
            return
                _frameSpawns.Count == 0
                && _frameAreaPulseTriggers.Count == 0
                && _frameImpacts.Count == 0
                && _frameExpirations.Count == 0
                && _frameRejections.Count == 0
                && _framePhysicalEvents.Count == 0
                    ? CombatProjectileFrameEvents.Empty
                    : new CombatProjectileFrameEvents(
                        _frameSpawns,
                        _frameAreaPulseTriggers,
                        _frameImpacts,
                        _frameExpirations,
                        _frameRejections,
                        _framePhysicalEvents);
        }

        private static readonly Comparison<CombatProjectileSnapshot> StateViewComparison = CombatProjectileSnapshotComparer.Instance.Compare;
        private readonly List<CombatProjectileSnapshot> _stateViewProjectiles = new List<CombatProjectileSnapshot>(64);
        private CombatProjectileRuntimeState _stateView;

        /// <summary>Reads borrowed state for immediate indexed consumption. The view is invalidated by the next read or runtime mutation.</summary>
        /// <remarks>NoAlloc after initialization and capacity warmup. Use CreateStateSnapshot for retained history.</remarks>
        public CombatProjectileRuntimeState ReadStateView(string moduleId = CombatProjectileRuntimeModule.DefaultModuleId)
        {
            _stateViewProjectiles.Clear();
            CopyActiveSnapshotsTo(_stateViewProjectiles, true);
            long definitionHash = _definitions.ComputeStableHash();
            _stateView ??= new CombatProjectileRuntimeState(CombatProjectileRuntimeState.CurrentSchemaVersion,
                moduleId, definitionHash, _nextProjectileId, _stateViewProjectiles, true);
            _stateView.ModuleId = moduleId ?? string.Empty;
            _stateView.DefinitionHash = definitionHash;
            _stateView.NextProjectileId = _nextProjectileId;
            return _stateView;
        }

        public CombatProjectileRuntimeState CreateStateSnapshot(string moduleId = CombatProjectileRuntimeModule.DefaultModuleId)
        {
            return CaptureRuntimeState(moduleId);
        }

        public int CopyActiveSnapshotsTo(List<CombatProjectileSnapshot> output) => CopyActiveSnapshotsTo(output, false);

        private int CopyActiveSnapshotsTo(List<CombatProjectileSnapshot> output, bool borrow)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            int start = output.Count;
            for (int i = 0; i < _activeProjectiles.Count; i++)
            {
                ActiveProjectile projectile = _activeProjectiles[i];
                string stableId = _definitions.TryGet(projectile.DefinitionId, out CombatProjectileDefinition definition)
                    ? definition.StableId
                    : string.Empty;
                RuntimeHashDigest labelDigest = definition != null ? definition.StableLabelDigest : default;
                output.Add(projectile.ToSnapshot(stableId, labelDigest, borrow));
            }

            if (borrow && start == 0) output.Sort(StateViewComparison);
            else output.Sort(start, output.Count - start, CombatProjectileSnapshotComparer.Instance);
            return output.Count - start;
        }

        /// <summary>
        /// Copies lightweight active projectile views into a caller-owned list for Tick/presentation consumers.
        /// </summary>
        /// <remarks>NoAlloc when <paramref name="output"/> capacity has been warmed and definitions are registered.</remarks>
        public int CopyActiveViewsTo(List<CombatProjectileActiveView> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int start = output.Count;
            for (int i = 0; i < _activeProjectiles.Count; i++)
            {
                ActiveProjectile projectile = _activeProjectiles[i];
                string stableId = _definitions.TryGet(projectile.DefinitionId, out CombatProjectileDefinition definition)
                    ? definition.StableId
                    : string.Empty;
                output.Add(projectile.ToActiveView(stableId));
            }

            output.Sort(start, output.Count - start, CombatProjectileActiveViewComparer.Instance);
            return output.Count - start;
        }

        public bool TryRestoreStateSnapshot(CombatProjectileRuntimeState state, out string failure)
        {
            failure = string.Empty;
            if (state == null)
            {
                failure = "Combat projectile runtime state is missing.";
                return false;
            }

            if (state.SchemaVersion != CombatProjectileRuntimeState.CurrentSchemaVersion)
            {
                failure = "Combat projectile runtime state schema mismatch. expected="
                    + CombatProjectileRuntimeState.CurrentSchemaVersion
                    + " actual="
                    + state.SchemaVersion
                    + ".";
                return false;
            }

            long currentDefinitionHash = DefinitionHash;
            if (state.DefinitionHash != currentDefinitionHash)
            {
                failure = "Combat projectile definition hash mismatch.";
                return false;
            }

            try
            {
                RestoreRuntimeState(state);
                return true;
            }
            catch (Exception exception)
            {
                failure = "Combat projectile runtime state restore failed: " + exception.Message;
                return false;
            }
        }

        public CombatProjectileDebugSnapshot CreateDebugSnapshot(string moduleId = CombatProjectileRuntimeModule.DefaultModuleId)
        {
            CombatProjectileRuntimeState state = CaptureRuntimeState(moduleId);
            var entries = new List<CombatProjectileDebugEntry>(state.ActiveProjectiles.Count);
            for (int i = 0; i < state.ActiveProjectiles.Count; i++)
            {
                CombatProjectileSnapshot snapshot = state.ActiveProjectiles[i];
                int remainingFrames = 0;
                int remainingAcceptedImpacts = 0;
                if (_definitions.TryGet(snapshot.DefinitionId, out CombatProjectileDefinition definition))
                {
                    int lifetimeFrames = snapshot.EffectiveLifetimeFrames > 0
                        ? snapshot.EffectiveLifetimeFrames
                        : definition.Motion.LifetimeFrames;
                    remainingFrames = lifetimeFrames - snapshot.AgeFrames;
                    remainingAcceptedImpacts = definition.Collision.MaxAcceptedImpacts - snapshot.AcceptedImpactCount;
                }

                entries.Add(new CombatProjectileDebugEntry(snapshot, remainingFrames, remainingAcceptedImpacts));
            }

            CombatProjectileFrameEvents lastFrameEvents = _lastFrameEvents;
            if (ReferenceEquals(lastFrameEvents, CombatProjectileFrameEvents.Empty)
                && _lastFrameEventBuffer.HasEvents)
            {
                lastFrameEvents = new CombatProjectileFrameEvents(
                    _lastFrameEventBuffer.Spawns,
                    _lastFrameEventBuffer.AreaPulseTriggers,
                    _lastFrameEventBuffer.Impacts,
                    _lastFrameEventBuffer.Expirations,
                    _lastFrameEventBuffer.Rejections,
                    _lastFrameEventBuffer.PhysicalEvents);
            }

            return new CombatProjectileDebugSnapshot(
                state.ModuleId,
                state.DefinitionHash,
                state.ActiveProjectiles,
                lastFrameEvents,
                entries);
        }

        internal CombatProjectileRuntimeState CaptureRuntimeState(string moduleId)
        {
            var snapshots = new List<CombatProjectileSnapshot>(_activeProjectiles.Count);
            CopyActiveSnapshotsTo(snapshots);
            return new CombatProjectileRuntimeState(
                CombatProjectileRuntimeState.CurrentSchemaVersion,
                moduleId,
                _definitions.ComputeStableHash(),
                _nextProjectileId,
                snapshots);
        }

        internal void RestoreRuntimeState(CombatProjectileRuntimeState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            _activeProjectiles.Clear();
            _nextProjectileId = state.NextProjectileId <= 0 ? 1 : state.NextProjectileId;
            _nextSpawnSequence = 0;
            for (int i = 0; i < state.ActiveProjectiles.Count; i++)
            {
                CombatProjectileSnapshot snapshot = state.ActiveProjectiles[i];
                _activeProjectiles.Add(ActiveProjectile.FromSnapshot(snapshot));
                _nextSpawnSequence = Math.Max(_nextSpawnSequence, snapshot.SpawnSequence + 1);
            }

            _activeProjectiles.Sort(ActiveProjectileComparer.Instance);
        }

        private void BeginFrame()
        {
            _spawnRequests.Clear();
            _directSweepQueries.Clear();
            _queryResults.Clear();
            _singleCandidate.Clear();
            _singleResult.Clear();
            _hitOnceKeys.Clear();
            _frameSpawns.Clear();
            _frameAreaPulseTriggers.Clear();
            _frameImpacts.Clear();
            _frameExpirations.Clear();
            _frameRejections.Clear();
            _framePhysicalEvents.Clear();
        }

        private void DrainSpawnRequests(CombatFrame frame)
        {
            int start = _spawnRequests.Count;
            _spawnSource.CollectSpawnRequests(frame, _spawnRequests);
            _spawnRequests.Sort(start, _spawnRequests.Count - start, CombatProjectileSpawnRequestComparer.Instance);
            for (int i = start; i < _spawnRequests.Count; i++)
            {
                CombatProjectileSpawnRequest request = _spawnRequests[i];
                if (!_definitions.TryGet(request.DefinitionId, out CombatProjectileDefinition definition))
                {
                    _frameRejections.Add(new CombatProjectileRejectedEvent(
                        request,
                        "Projectile definition was not found."));
                    continue;
                }

                int projectileId = _nextProjectileId++;
                FixVector3 direction = request.Direction;
                int effectiveLifetimeFrames = request.EffectiveLifetimeFrames;
                Fix64 launchSpeedPerFrame = Fix64.Zero;
                if (definition.Motion.Kind == CombatProjectileMotionKind.BallisticArcToTarget
                    && request.HasTargetPoint)
                {
                    int maxLifetimeFrames = effectiveLifetimeFrames > 0
                        ? effectiveLifetimeFrames
                        : definition.Motion.LifetimeFrames;
                    if (!CombatProjectileBallistics.TrySolveLaunchArc(
                            request.Origin,
                            request.TargetPoint,
                            definition.Motion.SpeedPerFrame,
                            definition.Motion.GravityPerFrameSquared,
                            definition.Motion.MaxLaunchAngleTangent,
                            maxLifetimeFrames,
                            out FixVector3 initialVelocity,
                            out launchSpeedPerFrame,
                            out effectiveLifetimeFrames))
                    {
                        _frameRejections.Add(new CombatProjectileRejectedEvent(
                            request,
                            "Ballistic projectile target is not reachable with the configured max speed, gravity, angle cap, and lifetime."));
                        continue;
                    }

                    if (!initialVelocity.TryNormalize(out direction))
                    {
                        _frameRejections.Add(new CombatProjectileRejectedEvent(
                            request,
                            "Ballistic projectile solved a zero launch direction."));
                        continue;
                    }
                }

                var projectile = new ActiveProjectile(
                    projectileId,
                    _nextSpawnSequence++,
                    request.DefinitionId,
                    request.SourceEntityId,
                    request.ActionId,
                    request.ActionInstanceId,
                    request.TraceId,
                    request.LocalFrame,
                    request.SpawnFrame,
                    request.SpawnFrame,
                    0,
                    request.Origin,
                    direction,
                    request.Origin,
                    request.HasTargetPoint,
                    request.TargetPoint,
                    effectiveLifetimeFrames,
                    launchSpeedPerFrame,
                    request.HasTrackingTarget,
                    request.TrackingTargetEntityId,
                    acceptedImpacts: null);
                _activeProjectiles.Add(projectile);
                var birth = new CombatProjectileSpawnEvent(projectile.ToSnapshot(definition.StableId, definition.StableLabelDigest), request);
                _frameSpawns.Add(birth);
                if (_impactResolver is ICombatProjectileSpawnObserver observer) observer.OnProjectileSpawned(birth);
            }
        }

        private bool AdvanceProjectile(
            CombatFrame frame,
            CombatProjectileDefinition definition,
            ref ActiveProjectile projectile)
        {
            FixVector3 oldPosition = projectile.Position;
            int newAge = projectile.AgeFrames + 1;
            int lifetimeFrames = ResolveEffectiveLifetimeFrames(definition.Motion, projectile);
            ActiveProjectile motionProjectile = ResolveTrackingMotion(frame, definition, projectile);
            FixVector3 newPosition = ResolveMotionPosition(definition.Motion, motionProjectile, newAge, lifetimeFrames);
            ActiveProjectile movingProjectile = motionProjectile.WithPosition(newPosition, frame, newAge);
            CombatProjectileSnapshot movingSnapshot = movingProjectile.ToSnapshot(definition.StableId, definition.StableLabelDigest);

            var header = new CombatQueryHeader(
                queryId: projectile.ProjectileId,
                kind: CombatQueryKind.Capsule,
                sourceEntityId: projectile.SourceEntityId,
                traceId: projectile.TraceId,
                actionId: projectile.ActionId,
                sourceOrder: projectile.SpawnSequence,
                layerMask: definition.Collision.LayerMask);
            _queryResults.Clear();
            bool usesDefaultSweep = QueryDirectSweeps(
                frame,
                movingSnapshot,
                definition,
                header,
                oldPosition,
                newPosition);

            bool consumed = false;
            _hitOnceKeys.Clear();
            for (int i = 0; i < _queryResults.Count; i++)
            {
                CombatQueryResult hit = _queryResults[i];
                if (definition.Collision.HitSameTargetPolicy == CombatProjectileHitSameTargetPolicy.OncePerProjectile
                    && movingProjectile.HasAcceptedImpact(hit.TargetEntityId))
                {
                    continue;
                }

                if (!TryResolveImpact(
                        movingSnapshot,
                        hit,
                        frame,
                        new CombatProjectileImpactContext(CombatProjectileImpactSourceKind.DirectSweep),
                        out HitResolveResult result,
                        out CombatProjectileImpactPayload payload,
                        incomingDirection: usesDefaultSweep ? newPosition - oldPosition : FixVector3.Zero))
                    continue;

                if (result.Kind == HitResolveKind.Duplicate)
                    continue;

                if (definition.Impact.PolicyKind == CombatProjectileImpactPolicyKind.AreaPulseOnImpact)
                {
                    if (ShouldTriggerAreaPulse(result))
                    {
                        CombatProjectileSnapshot pulseSnapshot = movingProjectile.WithPosition(hit.Point, frame, newAge)
                            .ToSnapshot(definition.StableId, definition.StableLabelDigest);
                        TriggerAreaPulse(
                            frame,
                            definition,
                            pulseSnapshot,
                            hit.Point,
                            consumedProjectile: true,
                            CombatProjectileAreaPulseTriggerKind.DirectSweep,
                            hasDirectHit: true,
                            directPhysicsHit: hit,
                            directResolveResult: result,
                            directPayload: payload);
                        return true;
                    }

                    continue;
                }

                consumed = IsAcceptedImpact(result, definition.Collision);
                bool consumedProjectile = false;
                if (consumed)
                {
                    movingProjectile = movingProjectile.WithAcceptedImpact(result.TargetId, frame);
                    movingSnapshot = movingProjectile.ToSnapshot(definition.StableId, definition.StableLabelDigest);
                    consumedProjectile = definition.Collision.StopOnFirstAcceptedImpact
                        || definition.Collision.StopOnBlockedImpact && result.Kind == HitResolveKind.Blocked
                        || movingProjectile.AcceptedImpactCount >= definition.Collision.MaxAcceptedImpacts;
                }

                _frameImpacts.Add(new CombatProjectileImpactEvent(
                    movingSnapshot,
                    hit,
                    result,
                    payload,
                    new CombatProjectileImpactContext(CombatProjectileImpactSourceKind.DirectSweep),
                    consumedProjectile,
                    definition.Impact.EffectTimelineId));
                TryAddImpactPhysicalEvent(definition, movingSnapshot, hit.Point);
                if (consumedProjectile)
                    return true;
            }

            projectile = movingProjectile;
            if (newAge >= lifetimeFrames)
            {
                CombatProjectileSnapshot expiredSnapshot = projectile.ToSnapshot(definition.StableId, definition.StableLabelDigest);
                if (definition.Impact.PolicyKind == CombatProjectileImpactPolicyKind.AreaPulseOnImpact
                    && definition.Impact.TriggerOnExpire)
                {
                    TriggerAreaPulse(
                        frame,
                        definition,
                        expiredSnapshot,
                        newPosition,
                        consumedProjectile: true,
                        CombatProjectileAreaPulseTriggerKind.Expiration,
                        hasDirectHit: false,
                        directPhysicsHit: default,
                        directResolveResult: default,
                        directPayload: default);
                }

                TryAddExpirationPhysicalEvent(definition, expiredSnapshot);
                _frameExpirations.Add(new CombatProjectileExpiredEvent(expiredSnapshot, definition.Impact.EffectTimelineId));
                return true;
            }

            TryAddActiveFramePhysicalEvent(definition, projectile.ToSnapshot(definition.StableId, definition.StableLabelDigest));
            return false;
        }

        private void TryAddActiveFramePhysicalEvent(
            CombatProjectileDefinition definition,
            CombatProjectileSnapshot projectile)
        {
            TryAddPhysicalEvent(
                definition,
                projectile,
                CombatProjectilePhysicalEventKind.ActiveFrame,
                projectile.Position,
                projectile.Direction,
                definition.Collision.Radius,
                definition.Impact.HitEvent.Damage.ImpactPower,
                CombatProjectileAreaPulseTriggerKind.DirectSweep);
        }

        private void TryAddImpactPhysicalEvent(
            CombatProjectileDefinition definition,
            CombatProjectileSnapshot projectile,
            FixVector3 hitPoint)
        {
            TryAddPhysicalEvent(
                definition,
                projectile,
                CombatProjectilePhysicalEventKind.Impact,
                hitPoint,
                projectile.Direction,
                definition.Collision.Radius,
                definition.Impact.HitEvent.Damage.ImpactPower,
                CombatProjectileAreaPulseTriggerKind.DirectSweep);
        }

        private void TryAddAreaPulsePhysicalEvent(
            CombatProjectileDefinition definition,
            CombatProjectileSnapshot projectile,
            FixVector3 center,
            CombatProjectileAreaPulseTriggerKind triggerKind)
        {
            TryAddPhysicalEvent(
                definition,
                projectile,
                CombatProjectilePhysicalEventKind.AreaPulse,
                center,
                projectile.Direction,
                definition.Impact.AreaRadius,
                ResolveAreaPulsePhysicalImpactPower(definition.Impact),
                triggerKind);
        }

        private void TryAddExpirationPhysicalEvent(
            CombatProjectileDefinition definition,
            CombatProjectileSnapshot projectile)
        {
            if (definition.Impact.PolicyKind == CombatProjectileImpactPolicyKind.AreaPulseOnImpact
                && definition.Impact.TriggerOnExpire)
            {
                TryAddPhysicalEvent(
                    definition,
                    projectile,
                    CombatProjectilePhysicalEventKind.Expiration,
                    projectile.Position,
                    projectile.Direction,
                    definition.Impact.AreaRadius,
                    ResolveAreaPulsePhysicalImpactPower(definition.Impact),
                    CombatProjectileAreaPulseTriggerKind.Expiration);
                return;
            }

            TryAddPhysicalEvent(
                definition,
                projectile,
                CombatProjectilePhysicalEventKind.Expiration,
                projectile.Position,
                projectile.Direction,
                definition.Collision.Radius,
                definition.Impact.HitEvent.Damage.ImpactPower,
                CombatProjectileAreaPulseTriggerKind.DirectSweep);
        }

        private void TryAddPhysicalEvent(
            CombatProjectileDefinition definition,
            CombatProjectileSnapshot projectile,
            CombatProjectilePhysicalEventKind kind,
            FixVector3 position,
            FixVector3 direction,
            Fix64 radius,
            int impactPower,
            CombatProjectileAreaPulseTriggerKind areaPulseTriggerKind)
        {
            if (definition == null || !definition.PhysicalEventsEnabled)
                return;
            if (radius <= Fix64.Zero || impactPower <= 0)
                return;

            _framePhysicalEvents.Add(new CombatProjectilePhysicalEvent(
                projectile,
                kind,
                position,
                direction,
                radius,
                impactPower,
                areaPulseTriggerKind));
        }

        private static int ResolveAreaPulsePhysicalImpactPower(CombatProjectileImpactConfig impact)
        {
            int impactPower = impact.AreaHitEvent.Damage.ImpactPower;
            return impactPower > 0 ? impactPower : impact.HitEvent.Damage.ImpactPower;
        }

        private ActiveProjectile ResolveTrackingMotion(
            CombatFrame frame,
            CombatProjectileDefinition definition,
            ActiveProjectile projectile)
        {
            if (definition.Motion.Kind != CombatProjectileMotionKind.HomingToTarget
                || !projectile.HasTrackingTarget
                || _trackingTargetProvider == null)
            {
                return projectile;
            }

            var query = new CombatProjectileTrackingTargetQuery(
                frame,
                projectile.ProjectileId,
                projectile.DefinitionId,
                projectile.SourceEntityId,
                projectile.TrackingTargetEntityId,
                projectile.Position,
                projectile.Direction);
            if (!_trackingTargetProvider.TryResolveTrackingTarget(query, out FixVector3 targetPoint))
                return projectile;

            FixVector3 delta = targetPoint - projectile.Position;
            if (!delta.TryNormalize(out FixVector3 desiredDirection))
                return projectile;

            FixVector3 direction = SteerDirectionToward(
                projectile.Direction,
                desiredDirection,
                definition.Motion.HomingTurnRatePerFrame);
            return projectile.WithTrackingTarget(targetPoint, direction);
        }

        private bool QueryDirectSweeps(
            CombatFrame frame,
            CombatProjectileSnapshot projectile,
            CombatProjectileDefinition definition,
            CombatQueryHeader header,
            FixVector3 oldPosition,
            FixVector3 newPosition)
        {
            _directSweepQueries.Clear();
            if (_directSweepProvider != null)
            {
                _directSweepProvider.CollectDirectSweepQueries(
                    frame,
                    projectile,
                    definition,
                    header,
                    oldPosition,
                    newPosition,
                    _directSweepQueries);
            }

            bool usesDefaultSweep = _directSweepQueries.Count == 0;
            if (usesDefaultSweep)
            {
                _directSweepQueries.Add(new CombatCapsuleQuery(
                    header,
                    oldPosition,
                    newPosition,
                    definition.Collision.Radius));
            }

            int queryCount = _directSweepQueries.Count;
            for (int i = 0; i < queryCount; i++)
            {
                _physicsWorld.QueryCapsule(
                    _directSweepQueries[i],
                    _queryResults,
                    includeSourceEntity: false);
            }

            if (queryCount > 1 && _queryResults.Count > 1)
                _physicsWorld.SortResults(_queryResults);
            return usesDefaultSweep;
        }

        private static FixVector3 SteerDirectionToward(
            FixVector3 currentDirection,
            FixVector3 desiredDirection,
            Fix64 turnRatePerFrame)
        {
            if (!desiredDirection.TryNormalize(out FixVector3 desired))
                return currentDirection;
            if (!currentDirection.TryNormalize(out FixVector3 current))
                return desired;

            Fix64 rate = Fix64.Clamp(turnRatePerFrame, Fix64.Zero, Fix64.One);
            if (rate >= Fix64.One)
                return desired;
            if (rate <= Fix64.Zero)
                return current;

            FixVector3 blended = current * (Fix64.One - rate) + desired * rate;
            if (blended.TryNormalize(out FixVector3 normalized))
                return normalized;

            FixVector3 fallback = ResolvePlanarRight(current);
            blended = current * (Fix64.One - rate) + fallback * rate;
            return blended.TryNormalize(out normalized) ? normalized : current;
        }

        private static FixVector3 ResolveMotionPosition(
            CombatProjectileMotionConfig motion,
            ActiveProjectile projectile,
            int ageFrames,
            int lifetimeFrames)
        {
            switch (motion.Kind)
            {
                case CombatProjectileMotionKind.ArcToTarget:
                    return ResolveArcMotionPosition(motion, projectile, ageFrames, lifetimeFrames);
                case CombatProjectileMotionKind.BallisticArcToTarget:
                    return ResolveBallisticArcMotionPosition(motion, projectile, ageFrames, lifetimeFrames);
                case CombatProjectileMotionKind.BoomerangReturn:
                    return ResolveBoomerangMotionPosition(motion, projectile, ageFrames, lifetimeFrames);
                case CombatProjectileMotionKind.SwayForward:
                    return ResolveSwayMotionPosition(motion, projectile, ageFrames, lifetimeFrames);
                case CombatProjectileMotionKind.HomingToTarget:
                    return ResolveHomingMotionPosition(motion, projectile, ageFrames, lifetimeFrames);
                case CombatProjectileMotionKind.BezierToTarget:
                    return ResolveBezierMotionPosition(motion, projectile, ageFrames, lifetimeFrames);
                case CombatProjectileMotionKind.Linear:
                default:
                    return ResolveLinearMotionPosition(motion, projectile, ageFrames, lifetimeFrames);
            }
        }

        private static FixVector3 ResolveLinearMotionPosition(
            CombatProjectileMotionConfig motion,
            ActiveProjectile projectile,
            int ageFrames,
            int lifetimeFrames)
        {
            if (motion.SpeedProfileKind == CombatProjectileSpeedProfileKind.Constant)
                return projectile.Origin + projectile.Direction * motion.SpeedPerFrame * Fix64.FromInt(ageFrames);

            return projectile.Origin
                + projectile.Direction
                * motion.SpeedPerFrame
                * Fix64.FromInt(lifetimeFrames)
                * motion.EvaluateProgress(ageFrames, lifetimeFrames);
        }

        private static FixVector3 ResolveHomingMotionPosition(
            CombatProjectileMotionConfig motion,
            ActiveProjectile projectile,
            int ageFrames,
            int lifetimeFrames)
        {
            Fix64 oldProgress = motion.EvaluateProgress(projectile.AgeFrames, lifetimeFrames);
            Fix64 newProgress = motion.EvaluateProgress(ageFrames, lifetimeFrames);
            Fix64 progressDelta = newProgress - oldProgress;
            if (progressDelta <= Fix64.Zero)
                return projectile.Position;

            Fix64 stepDistance = motion.SpeedPerFrame * Fix64.FromInt(lifetimeFrames) * progressDelta;
            return projectile.Position + projectile.Direction * stepDistance;
        }

        private static FixVector3 ResolveBoomerangMotionPosition(
            CombatProjectileMotionConfig motion,
            ActiveProjectile projectile,
            int ageFrames,
            int lifetimeFrames)
        {
            Fix64 progress = motion.EvaluateProgress(ageFrames, lifetimeFrames);
            Fix64 legProgress = progress <= Fix64.Half
                ? progress * Fix64.FromInt(2)
                : (Fix64.One - progress) * Fix64.FromInt(2);
            FixVector3 outboundTarget = ResolveMotionTargetPoint(motion, projectile, lifetimeFrames);
            return projectile.Origin + (outboundTarget - projectile.Origin) * legProgress;
        }

        private static FixVector3 ResolveSwayMotionPosition(
            CombatProjectileMotionConfig motion,
            ActiveProjectile projectile,
            int ageFrames,
            int lifetimeFrames)
        {
            Fix64 progress = motion.EvaluateProgress(ageFrames, lifetimeFrames);
            FixVector3 targetPoint = ResolveMotionTargetPoint(motion, projectile, lifetimeFrames);
            FixVector3 center = projectile.Origin + (targetPoint - projectile.Origin) * progress;
            FixVector3 right = ResolvePlanarRight(projectile.Direction);
            Fix64 sway = EvaluateSignedTriangleWave(progress, motion.LateralCycles) * motion.LateralAmplitude;
            return center + right * sway;
        }

        private static FixVector3 ResolveArcMotionPosition(
            CombatProjectileMotionConfig motion,
            ActiveProjectile projectile,
            int ageFrames,
            int lifetimeFrames)
        {
            Fix64 t = motion.EvaluateProgress(ageFrames, lifetimeFrames);
            FixVector3 targetPoint = ResolveMotionTargetPoint(motion, projectile, lifetimeFrames);
            FixVector3 linear = projectile.Origin + (targetPoint - projectile.Origin) * t;
            Fix64 verticalOffset = Fix64.FromInt(4) * motion.ArcHeight * t * (Fix64.One - t);
            return linear + new FixVector3(Fix64.Zero, verticalOffset, Fix64.Zero);
        }

        private static FixVector3 ResolveBallisticArcMotionPosition(
            CombatProjectileMotionConfig motion,
            ActiveProjectile projectile,
            int ageFrames,
            int lifetimeFrames)
        {
            int life = Math.Max(1, lifetimeFrames);
            int clampedAge = Math.Max(0, Math.Min(ageFrames, life));
            if (projectile.HasTargetPoint && clampedAge >= life)
                return projectile.TargetPoint;

            Fix64 time = Fix64.FromInt(clampedAge);
            FixVector3 gravity = new FixVector3(Fix64.Zero, -motion.GravityPerFrameSquared, Fix64.Zero);
            Fix64 launchSpeedPerFrame = projectile.LaunchSpeedPerFrame > Fix64.Zero
                ? projectile.LaunchSpeedPerFrame
                : motion.SpeedPerFrame;
            FixVector3 initialVelocity = projectile.Direction * launchSpeedPerFrame;
            return projectile.Origin + initialVelocity * time + gravity * (Fix64.Half * time * time);
        }

        private static FixVector3 ResolveBezierMotionPosition(
            CombatProjectileMotionConfig motion,
            ActiveProjectile projectile,
            int ageFrames,
            int lifetimeFrames)
        {
            Fix64 t = motion.EvaluateProgress(ageFrames, lifetimeFrames);
            Fix64 inverse = Fix64.One - t;
            Fix64 t2 = t * t;
            Fix64 inverse2 = inverse * inverse;
            FixVector3 p0 = projectile.Origin;
            FixVector3 p3 = ResolveMotionTargetPoint(motion, projectile, lifetimeFrames);
            FixVector3 p1 = p0 + ResolveProjectileLocalOffset(projectile.Direction, motion.BezierControlOffsetA);
            FixVector3 p2 = p3 + ResolveProjectileLocalOffset(projectile.Direction, motion.BezierControlOffsetB);
            return p0 * (inverse2 * inverse)
                + p1 * (Fix64.FromInt(3) * inverse2 * t)
                + p2 * (Fix64.FromInt(3) * inverse * t2)
                + p3 * (t2 * t);
        }

        private static FixVector3 ResolveProjectileLocalOffset(FixVector3 direction, FixVector3 localOffset)
        {
            FixVector3 right = ResolvePlanarRight(direction);
            return right * localOffset.X
                + new FixVector3(Fix64.Zero, localOffset.Y, Fix64.Zero)
                + direction * localOffset.Z;
        }

        private static FixVector3 ResolvePlanarRight(FixVector3 direction)
        {
            var right = new FixVector3(direction.Z, Fix64.Zero, -direction.X);
            return right.TryNormalize(out FixVector3 normalized)
                ? normalized
                : new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
        }

        private static Fix64 EvaluateSignedTriangleWave(Fix64 progress, int cycles)
        {
            if (cycles <= 0)
                return Fix64.Zero;

            long scaledRaw = checked(progress.RawValue * cycles);
            long phaseRaw = scaledRaw % Fix64.Scale;
            if (phaseRaw < 0)
                phaseRaw += Fix64.Scale;

            Fix64 phase = Fix64.FromRaw(phaseRaw);
            Fix64 quarter = Fix64.FromRatio(1, 4);
            Fix64 threeQuarters = Fix64.FromRatio(3, 4);
            Fix64 four = Fix64.FromInt(4);
            if (phase <= quarter)
                return phase * four;
            if (phase <= threeQuarters)
                return Fix64.FromInt(2) - phase * four;
            return phase * four - Fix64.FromInt(4);
        }

        private static FixVector3 ResolveMotionTargetPoint(
            CombatProjectileMotionConfig motion,
            ActiveProjectile projectile,
            int lifetimeFrames)
        {
            if (projectile.HasTargetPoint)
                return projectile.TargetPoint;

            return projectile.Origin
                + projectile.Direction * motion.SpeedPerFrame * Fix64.FromInt(lifetimeFrames);
        }

        private static int ResolveEffectiveLifetimeFrames(
            CombatProjectileMotionConfig motion,
            ActiveProjectile projectile)
        {
            return projectile.EffectiveLifetimeFrames > 0
                ? projectile.EffectiveLifetimeFrames
                : motion.LifetimeFrames;
        }

        private bool TryResolveImpact(
            CombatProjectileSnapshot projectile,
            CombatQueryResult hit,
            CombatFrame frame,
            CombatProjectileImpactContext context,
            out HitResolveResult result,
            out CombatProjectileImpactPayload payload,
            FixVector3 incomingDirection = default)
        {
            result = default;
            payload = default;
            if (!_impactResolver.TryResolveImpact(projectile, hit, frame, context, out payload))
                return false;

            _singleCandidate.Clear();
            _singleResult.Clear();
            _singleCandidate.Add(new HitCandidate(
                projectile.SourceEntityId,
                hit.TargetEntityId,
                projectile.ActionId,
                projectile.ActionInstanceId,
                projectile.TraceId,
                frame,
                hit,
                payload.Damage,
                payload.StaggerFrames,
                payload.Knockback,
                payload.TargetState,
                incomingDirection,
                payload.ResolvePriority).WithProducerIdentity(context.GetProducerIdentity(projectile.ProjectileId, projectile.SpawnSequence)));
            _hitResolve.Resolve(
                _singleCandidate,
                _hitOnceKeys,
                _singleResult,
                _impactResolver is ICombatProjectileImpactRelations relations
                    ? relations.ResolveTeamRelations(projectile, _teamRelationProvider) : _teamRelationProvider,
                _targetStateResolver,
                _allowFriendlyFire);
            if (_singleResult.Count == 0)
                return false;

            result = _singleResult[0];
            return true;
        }

        private void TriggerAreaPulse(
            CombatFrame frame,
            CombatProjectileDefinition definition,
            CombatProjectileSnapshot projectile,
            FixVector3 center,
            bool consumedProjectile,
            CombatProjectileAreaPulseTriggerKind triggerKind,
            bool hasDirectHit,
            CombatQueryResult directPhysicsHit,
            HitResolveResult directResolveResult,
            CombatProjectileImpactPayload directPayload)
        {
            _frameAreaPulseTriggers.Add(new CombatProjectileAreaPulseTriggerEvent(
                projectile,
                triggerKind,
                center,
                consumedProjectile,
                hasDirectHit,
                directPhysicsHit,
                directResolveResult,
                directPayload,
                definition.Impact.EffectTimelineId));
            TryAddAreaPulsePhysicalEvent(definition, projectile, center, triggerKind);

            var header = new CombatQueryHeader(
                queryId: projectile.ProjectileId,
                kind: CombatQueryKind.Sphere,
                sourceEntityId: projectile.SourceEntityId,
                traceId: projectile.TraceId,
                actionId: projectile.ActionId,
                sourceOrder: projectile.SpawnSequence,
                layerMask: definition.Impact.AreaLayerMask);
            _queryResults.Clear();
            _physicsWorld.QuerySphere(
                new CombatSphereQuery(header, center, definition.Impact.AreaRadius),
                _queryResults,
                includeSourceEntity: false);

            _hitOnceKeys.Clear();
            for (int i = 0; i < _queryResults.Count; i++)
            {
                CombatQueryResult hit = _queryResults[i];
                if (!TryResolveImpact(
                        projectile,
                        hit,
                        frame,
                        new CombatProjectileImpactContext(CombatProjectileImpactSourceKind.AreaPulse),
                        out HitResolveResult result,
                        out CombatProjectileImpactPayload payload))
                    continue;

                _frameImpacts.Add(new CombatProjectileImpactEvent(
                    projectile,
                    hit,
                    result,
                    payload,
                    new CombatProjectileImpactContext(CombatProjectileImpactSourceKind.AreaPulse),
                    consumedProjectile,
                    definition.Impact.EffectTimelineId));
            }
        }

        private static bool IsAcceptedImpact(HitResolveResult result, CombatProjectileCollisionConfig collision)
        {
            if (result.Kind == HitResolveKind.Damage)
                return true;
            return collision.StopOnBlockedImpact && result.Kind == HitResolveKind.Blocked;
        }

        private static bool ShouldTriggerAreaPulse(HitResolveResult result)
        {
            return result.Kind == HitResolveKind.Damage
                || result.Kind == HitResolveKind.Blocked;
        }

        private readonly struct ActiveProjectile
        {
            public ActiveProjectile(
                int projectileId,
                int spawnSequence,
                int definitionId,
                CombatEntityId sourceEntityId,
                int actionId,
                int actionInstanceId,
                int traceId,
                int localFrame,
                CombatFrame spawnFrame,
                CombatFrame lastFrame,
                int ageFrames,
                FixVector3 position,
                FixVector3 direction,
                FixVector3 origin,
                bool hasTargetPoint,
                FixVector3 targetPoint,
                int effectiveLifetimeFrames,
                Fix64 launchSpeedPerFrame,
                bool hasTrackingTarget,
                CombatEntityId trackingTargetEntityId,
                IReadOnlyList<CombatProjectileHitRecord> acceptedImpacts = null)
                : this(
                    projectileId,
                    spawnSequence,
                    definitionId,
                    sourceEntityId,
                    actionId,
                    actionInstanceId,
                    traceId,
                    localFrame,
                    spawnFrame,
                    lastFrame,
                    ageFrames,
                    position,
                    direction,
                    origin,
                    hasTargetPoint,
                    targetPoint,
                    effectiveLifetimeFrames,
                    launchSpeedPerFrame,
                    hasTrackingTarget,
                    trackingTargetEntityId,
                    CopyAcceptedImpacts(acceptedImpacts))
            {
            }

            private ActiveProjectile(
                int projectileId,
                int spawnSequence,
                int definitionId,
                CombatEntityId sourceEntityId,
                int actionId,
                int actionInstanceId,
                int traceId,
                int localFrame,
                CombatFrame spawnFrame,
                CombatFrame lastFrame,
                int ageFrames,
                FixVector3 position,
                FixVector3 direction,
                FixVector3 origin,
                bool hasTargetPoint,
                FixVector3 targetPoint,
                int effectiveLifetimeFrames,
                Fix64 launchSpeedPerFrame,
                bool hasTrackingTarget,
                CombatEntityId trackingTargetEntityId,
                CombatProjectileHitRecord[] acceptedImpacts)
            {
                ProjectileId = projectileId;
                SpawnSequence = spawnSequence;
                DefinitionId = definitionId;
                SourceEntityId = sourceEntityId;
                ActionId = actionId;
                ActionInstanceId = actionInstanceId;
                TraceId = traceId;
                LocalFrame = localFrame;
                SpawnFrame = spawnFrame;
                LastFrame = lastFrame;
                AgeFrames = ageFrames;
                Position = position;
                Direction = direction;
                Origin = origin;
                HasTargetPoint = hasTargetPoint;
                TargetPoint = hasTargetPoint ? targetPoint : FixVector3.Zero;
                EffectiveLifetimeFrames = Math.Max(0, effectiveLifetimeFrames);
                LaunchSpeedPerFrame = launchSpeedPerFrame < Fix64.Zero ? Fix64.Zero : launchSpeedPerFrame;
                HasTrackingTarget = hasTrackingTarget;
                TrackingTargetEntityId = hasTrackingTarget ? trackingTargetEntityId : default;
                _acceptedImpacts = acceptedImpacts ?? Array.Empty<CombatProjectileHitRecord>();
            }

            public int ProjectileId { get; }
            public int SpawnSequence { get; }
            public int DefinitionId { get; }
            public CombatEntityId SourceEntityId { get; }
            public int ActionId { get; }
            public int ActionInstanceId { get; }
            public int TraceId { get; }
            public int LocalFrame { get; }
            public CombatFrame SpawnFrame { get; }
            public CombatFrame LastFrame { get; }
            public int AgeFrames { get; }
            public FixVector3 Position { get; }
            public FixVector3 Direction { get; }
            public FixVector3 Origin { get; }
            public bool HasTargetPoint { get; }
            public FixVector3 TargetPoint { get; }
            public int EffectiveLifetimeFrames { get; }
            public Fix64 LaunchSpeedPerFrame { get; }
            public bool HasTrackingTarget { get; }
            public CombatEntityId TrackingTargetEntityId { get; }
            private readonly CombatProjectileHitRecord[] _acceptedImpacts;
            public IReadOnlyList<CombatProjectileHitRecord> AcceptedImpacts =>
                _acceptedImpacts ?? Array.Empty<CombatProjectileHitRecord>();
            public int AcceptedImpactCount => _acceptedImpacts?.Length ?? 0;

            public ActiveProjectile WithPosition(FixVector3 position, CombatFrame frame, int ageFrames)
            {
                return new ActiveProjectile(
                    ProjectileId,
                    SpawnSequence,
                    DefinitionId,
                    SourceEntityId,
                    ActionId,
                    ActionInstanceId,
                    TraceId,
                    LocalFrame,
                    SpawnFrame,
                    frame,
                    ageFrames,
                    position,
                    Direction,
                    Origin,
                    HasTargetPoint,
                    TargetPoint,
                    EffectiveLifetimeFrames,
                    LaunchSpeedPerFrame,
                    HasTrackingTarget,
                    TrackingTargetEntityId,
                    _acceptedImpacts);
            }

            public ActiveProjectile WithTrackingTarget(FixVector3 targetPoint, FixVector3 direction)
            {
                return new ActiveProjectile(
                    ProjectileId,
                    SpawnSequence,
                    DefinitionId,
                    SourceEntityId,
                    ActionId,
                    ActionInstanceId,
                    TraceId,
                    LocalFrame,
                    SpawnFrame,
                    LastFrame,
                    AgeFrames,
                    Position,
                    direction,
                    Origin,
                    true,
                    targetPoint,
                    EffectiveLifetimeFrames,
                    LaunchSpeedPerFrame,
                    true,
                    TrackingTargetEntityId,
                    _acceptedImpacts);
            }

            public bool HasAcceptedImpact(CombatEntityId targetEntityId)
            {
                for (int i = 0; i < AcceptedImpacts.Count; i++)
                {
                    if (AcceptedImpacts[i].TargetEntityId.Equals(targetEntityId))
                        return true;
                }

                return false;
            }

            public ActiveProjectile WithAcceptedImpact(CombatEntityId targetEntityId, CombatFrame frame)
            {
                int count = AcceptedImpactCount;
                var acceptedImpacts = new CombatProjectileHitRecord[count + 1];
                for (int i = 0; i < count; i++)
                    acceptedImpacts[i] = _acceptedImpacts[i];

                acceptedImpacts[count] = new CombatProjectileHitRecord(targetEntityId, frame);
                return new ActiveProjectile(
                    ProjectileId,
                    SpawnSequence,
                    DefinitionId,
                    SourceEntityId,
                    ActionId,
                    ActionInstanceId,
                    TraceId,
                    LocalFrame,
                    SpawnFrame,
                    LastFrame,
                    AgeFrames,
                    Position,
                    Direction,
                    Origin,
                    HasTargetPoint,
                    TargetPoint,
                    EffectiveLifetimeFrames,
                    LaunchSpeedPerFrame,
                    HasTrackingTarget,
                    TrackingTargetEntityId,
                    acceptedImpacts);
            }

            public CombatProjectileSnapshot ToSnapshot(
                string definitionStableId,
                RuntimeHashDigest definitionLabelDigest,
                bool borrow = false)
            {
                return new CombatProjectileSnapshot(
                    ProjectileId,
                    SpawnSequence,
                    DefinitionId,
                    definitionStableId,
                    SourceEntityId,
                    ActionId,
                    ActionInstanceId,
                    TraceId,
                    LocalFrame,
                    SpawnFrame,
                    LastFrame,
                    AgeFrames,
                    Position,
                    Direction,
                    Origin,
                    HasTargetPoint,
                    TargetPoint,
                    _acceptedImpacts,
                    EffectiveLifetimeFrames,
                    HasTrackingTarget,
                    TrackingTargetEntityId,
                    LaunchSpeedPerFrame,
                    borrow,
                    definitionLabelDigest);
            }

            public CombatProjectileActiveView ToActiveView(string definitionStableId)
            {
                return new CombatProjectileActiveView(
                    ProjectileId,
                    SpawnSequence,
                    DefinitionId,
                    definitionStableId,
                    SourceEntityId,
                    ActionId,
                    ActionInstanceId,
                    TraceId,
                    LocalFrame,
                    SpawnFrame,
                    LastFrame,
                    AgeFrames,
                    Position,
                    Direction,
                    Origin,
                    HasTargetPoint,
                    TargetPoint,
                    EffectiveLifetimeFrames,
                    HasTrackingTarget,
                    TrackingTargetEntityId,
                    AcceptedImpactCount,
                    LaunchSpeedPerFrame);
            }

            public static ActiveProjectile FromSnapshot(CombatProjectileSnapshot snapshot)
            {
                return new ActiveProjectile(
                    snapshot.ProjectileId,
                    snapshot.SpawnSequence,
                    snapshot.DefinitionId,
                    snapshot.SourceEntityId,
                    snapshot.ActionId,
                    snapshot.ActionInstanceId,
                    snapshot.TraceId,
                    snapshot.LocalFrame,
                    snapshot.SpawnFrame,
                    snapshot.LastFrame,
                    snapshot.AgeFrames,
                    snapshot.Position,
                    snapshot.Direction,
                    snapshot.Origin,
                    snapshot.HasTargetPoint,
                    snapshot.TargetPoint,
                    snapshot.EffectiveLifetimeFrames,
                    snapshot.LaunchSpeedPerFrame,
                    snapshot.HasTrackingTarget,
                    snapshot.TrackingTargetEntityId,
                    snapshot.AcceptedImpacts);
            }

            private static CombatProjectileHitRecord[] CopyAcceptedImpacts(
                IReadOnlyList<CombatProjectileHitRecord> acceptedImpacts)
            {
                if (acceptedImpacts == null || acceptedImpacts.Count == 0)
                    return Array.Empty<CombatProjectileHitRecord>();

                var copy = new CombatProjectileHitRecord[acceptedImpacts.Count];
                for (int i = 0; i < acceptedImpacts.Count; i++)
                    copy[i] = acceptedImpacts[i];
                return copy;
            }
        }

        private sealed class ActiveProjectileComparer : IComparer<ActiveProjectile>
        {
            public static readonly ActiveProjectileComparer Instance = new ActiveProjectileComparer();

            public int Compare(ActiveProjectile left, ActiveProjectile right)
            {
                return left.ProjectileId.CompareTo(right.ProjectileId);
            }
        }

        private sealed class CombatProjectileActiveViewComparer : IComparer<CombatProjectileActiveView>
        {
            public static readonly CombatProjectileActiveViewComparer Instance =
                new CombatProjectileActiveViewComparer();

            public int Compare(CombatProjectileActiveView left, CombatProjectileActiveView right)
            {
                return left.ProjectileId.CompareTo(right.ProjectileId);
            }
        }
    }
}
