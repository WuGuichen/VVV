using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.CombatBridge;
using Unity.Profiling;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem
    {
        private bool ResolveProjectileSpawnPose(
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            CharacterTestCharacterActionRuntime actionRuntime,
            CombatProjectileSpawnProfile spawnProfile,
            CombatProjectileDefinition definition,
            out FixVector3 origin,
            out FixVector3 direction,
            out FixVector3 targetPoint,
            out int effectiveLifetimeFrames,
            out bool hasTrackingTarget,
            out CombatEntityId trackingTargetEntityId)
        {
            if (CharacterTestFireballProjectileAdapter.IsMeteorBarrageProjectile(definition))
            {
                return ResolveMeteorBarrageProjectileSpawn(
                    entity,
                    request,
                    definition,
                    out origin,
                    out direction,
                    out targetPoint,
                    out effectiveLifetimeFrames,
                    out hasTrackingTarget,
                    out trackingTargetEntityId);
            }

            origin = ResolveProjectileOrigin(entity, request, actionRuntime, spawnProfile);
            if (IsChannelBeamProjectile(definition))
            {
                return ResolveChannelBeamProjectileAim(
                    entity,
                    request,
                    spawnProfile,
                    definition,
                    origin,
                    out direction,
                    out targetPoint,
                    out effectiveLifetimeFrames,
                    out hasTrackingTarget,
                    out trackingTargetEntityId);
            }

            return ResolveProjectileAim(
                entity,
                request,
                spawnProfile,
                definition,
                origin,
                out direction,
                out targetPoint,
                out effectiveLifetimeFrames,
                out hasTrackingTarget,
                out trackingTargetEntityId);
        }

        private bool ResolveMeteorBarrageProjectileSpawn(
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            CombatProjectileDefinition definition,
            out FixVector3 origin,
            out FixVector3 direction,
            out FixVector3 targetPoint,
            out int effectiveLifetimeFrames,
            out bool hasTrackingTarget,
            out CombatEntityId trackingTargetEntityId)
        {
            origin = FixVector3.Zero;
            direction = CharacterTestFireballProjectileAdapter.ResolveDirection(entity);
            targetPoint = FixVector3.Zero;
            effectiveLifetimeFrames = 0;
            hasTrackingTarget = false;
            trackingTargetEntityId = default;

            if (!CharacterTestFireballProjectileAdapter.IsMeteorBarrageRequestId(
                    request.RequestId,
                    out int meteorIndex))
            {
                ReportHitTraceError(
                    entity.Id.Value,
                    request.Metadata.ActionId,
                    request.RequestId,
                    string.Empty,
                    request.Metadata.LocalFrame,
                    "MeteorBarrageRequestInvalid",
                    "Meteor barrage projectile request id is not one of the configured launch slots.");
                return false;
            }

            if (!TryGetOrCreateMeteorBarrageCastContext(entity, request, _frame, out MeteorBarrageCastState state))
                return false;

            targetPoint = state.ResolveLandingPoint(meteorIndex);
            origin = targetPoint
                + new FixVector3(Fix64.Zero, CharacterTestFireballProjectileAdapter.MeteorBarrageSkyHeight, Fix64.Zero)
                - state.Forward * CharacterTestFireballProjectileAdapter.MeteorBarrageSkyBackstep;
            FixVector3 delta = targetPoint - origin;
            direction = delta.TryNormalize(out FixVector3 normalized)
                ? normalized
                : new FixVector3(Fix64.Zero, Fix64.Zero - Fix64.One, Fix64.Zero);
            effectiveLifetimeFrames = ResolveProjectileEffectiveLifetimeFrames(
                definition,
                origin,
                targetPoint);
            return true;
        }

        private bool ResolveChannelBeamProjectileAim(
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            CombatProjectileSpawnProfile spawnProfile,
            CombatProjectileDefinition definition,
            FixVector3 origin,
            out FixVector3 direction,
            out FixVector3 targetPoint,
            out int effectiveLifetimeFrames,
            out bool hasTrackingTarget,
            out CombatEntityId trackingTargetEntityId)
        {
            hasTrackingTarget = false;
            trackingTargetEntityId = default;

            var key = new ChannelBeamAimKey(entity.Id.Value, request.Metadata.InstanceId);
            Fix64 range = ResolveProjectileAimRange(spawnProfile.Aim, definition);
            if (_channelBeamAimCache.TryGetValue(key, out ChannelBeamAimState cached))
            {
                direction = cached.Direction;
                targetPoint = origin + direction * range;
                effectiveLifetimeFrames = ResolveProjectileEffectiveLifetimeFrames(
                    definition,
                    origin,
                    targetPoint);
                return true;
            }

            if (!ResolveProjectileAim(
                    entity,
                    request,
                    spawnProfile,
                    definition,
                    origin,
                    out direction,
                    out targetPoint,
                    out effectiveLifetimeFrames,
                    out _,
                    out _))
            {
                return false;
            }

            if (!direction.TryNormalize(out FixVector3 normalized))
            {
                direction = CharacterTestFireballProjectileAdapter.ResolveDirection(entity);
            }
            else
            {
                direction = normalized;
            }

            targetPoint = origin + direction * range;
            effectiveLifetimeFrames = ResolveProjectileEffectiveLifetimeFrames(
                definition,
                origin,
                targetPoint);
            _channelBeamAimCache[key] = new ChannelBeamAimState(direction, _frame.Value);
            _traceRecorder?.AttackCombat(
                _frame,
                entity.Id.Value,
                "channelBeamAim fixed instance="
                + request.Metadata.InstanceId.ToString(CultureInfo.InvariantCulture)
                + " direction=("
                + direction.X.ToString()
                + ","
                + direction.Y.ToString()
                + ","
                + direction.Z.ToString()
                + ")");
            return true;
        }

        private bool ResolveProjectileAim(
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            CombatProjectileSpawnProfile spawnProfile,
            CombatProjectileDefinition definition,
            FixVector3 origin,
            out FixVector3 direction,
            out FixVector3 targetPoint,
            out int effectiveLifetimeFrames,
            out bool hasTrackingTarget,
            out CombatEntityId trackingTargetEntityId)
        {
            hasTrackingTarget = false;
            trackingTargetEntityId = default;
            CombatProjectileAimConfig aim = spawnProfile.Aim;
            Fix64 range = ResolveProjectileAimRange(aim, definition);
            if (aim.Kind == CombatProjectileAimKind.LockedTargetCenter
                || aim.Kind == CombatProjectileAimKind.LockedTargetOrCasterForwardRange)
            {
                if (TryResolveLockedTargetAim(
                        entity,
                        request,
                        spawnProfile,
                        aim,
                        definition,
                        origin,
                        range,
                        allowFallback: aim.Kind == CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                        out direction,
                        out targetPoint,
                        out effectiveLifetimeFrames,
                        out CombatEntityId resolvedTrackingTargetEntityId))
                {
                    if (definition.Motion.Kind == CombatProjectileMotionKind.HomingToTarget)
                    {
                        hasTrackingTarget = true;
                        trackingTargetEntityId = resolvedTrackingTargetEntityId;
                    }

                    return true;
                }

                if (aim.Kind == CombatProjectileAimKind.LockedTargetCenter)
                {
                    effectiveLifetimeFrames = 0;
                    direction = CharacterTestFireballProjectileAdapter.ResolveDirection(entity);
                    targetPoint = origin;
                    return false;
                }
            }

            ResolveCasterForwardAim(entity, origin, range, out direction, out targetPoint);
            effectiveLifetimeFrames = ResolveProjectileEffectiveLifetimeFrames(definition, origin, targetPoint);
            return true;
        }

        private bool TryResolveLockedTargetAim(
            CharacterTestCharacterEntity source,
            CharacterActionGameplayRequest request,
            CombatProjectileSpawnProfile spawnProfile,
            CombatProjectileAimConfig aim,
            CombatProjectileDefinition definition,
            FixVector3 origin,
            Fix64 maxRange,
            bool allowFallback,
            out FixVector3 direction,
            out FixVector3 targetPoint,
            out int effectiveLifetimeFrames,
            out CombatEntityId trackingTargetEntityId)
        {
            direction = FixVector3.Zero;
            targetPoint = FixVector3.Zero;
            effectiveLifetimeFrames = 0;
            trackingTargetEntityId = default;
            bool usesCompositeTarget = _lockedCombatTargetProvider != null;
            bool hasCompositeTarget = TryResolveLockedCombatTarget(
                source,
                out CharacterTestCombatTargetHandle combatTarget,
                out CharacterTestLockedHurtboxDistanceSample lockedHurtbox);
            int targetEntityId = hasCompositeTarget
                ? combatTarget.TargetEntityId
                : ResolveLockedTargetEntityId();
            if (targetEntityId <= 0)
            {
                ReportProjectileAimFailure(source, request, spawnProfile, "ProjectileAimTargetMissing", "No locked target is available.", allowFallback);
                return false;
            }

            CharacterTestCharacterEntity target = _entityLookup.FindEntity(targetEntityId);
            if (target == null)
            {
                ReportProjectileAimFailure(source, request, spawnProfile, "ProjectileAimTargetMissing", "Locked target entity was not found: " + targetEntityId.ToString(CultureInfo.InvariantCulture) + ".", allowFallback);
                return false;
            }

            if (target.Id.Value == source.Id.Value)
            {
                ReportProjectileAimFailure(source, request, spawnProfile, "ProjectileAimTargetInvalid", "Locked target is the caster.", allowFallback);
                return false;
            }

            if (target.Team.TeamId == source.Team.TeamId)
            {
                ReportProjectileAimFailure(source, request, spawnProfile, "ProjectileAimTargetFriendly", "Locked target is friendly: " + targetEntityId.ToString(CultureInfo.InvariantCulture) + ".", allowFallback);
                return false;
            }

            if (!target.BattleState.CanReceiveHpDamage
                || _entityLookup.ResolveTargetState(_physicsRuntime.GetEntityId(target)) == HitTargetStateFlags.None)
            {
                ReportProjectileAimFailure(source, request, spawnProfile, "ProjectileAimTargetDead", "Locked target is not alive: " + targetEntityId.ToString(CultureInfo.InvariantCulture) + ".", allowFallback);
                return false;
            }

            if (hasCompositeTarget)
            {
                targetPoint = lockedHurtbox.AimPoint;
            }
            else if (usesCompositeTarget)
            {
                ReportProjectileAimFailure(source, request, spawnProfile, "ProjectileAimHurtboxUnavailable", "Locked target Hurtbox geometry is unavailable.", allowFallback);
                return false;
            }
            else if (_perceptionFrameCache != null
                && _perceptionFrameCache.TryGetTargetGeometry(
                    _frame,
                    source,
                    target,
                    out CharacterTestTargetGeometrySample targetGeometry))
            {
                targetPoint = targetGeometry.ResolveTargetPoint(aim.TargetHeightOffset);
            }
            else
            {
                targetPoint = ResolveProjectileAimTargetPoint(target, aim.TargetHeightOffset);
            }
            trackingTargetEntityId = _physicsRuntime.GetEntityId(target);
            FixVector3 delta = targetPoint - origin;
            bool isBallistic = definition.Motion.Kind == CombatProjectileMotionKind.BallisticArcToTarget;
            if (!isBallistic && maxRange > Fix64.Zero && delta.LengthSquared() > maxRange * maxRange)
            {
                ReportProjectileAimFailure(source, request, spawnProfile, "ProjectileAimTargetOutOfRange", "Locked target is outside projectile aim maxRange.", allowFallback);
                return false;
            }

            if (!delta.TryNormalize(out direction))
            {
                ReportProjectileAimFailure(source, request, spawnProfile, "ProjectileAimTargetDirectionInvalid", "Locked target targetPoint equals projectile origin.", allowFallback);
                return false;
            }

            if (isBallistic
                && !CombatProjectileBallistics.TrySolveLaunchArc(
                    origin,
                    targetPoint,
                    definition.Motion.SpeedPerFrame,
                    definition.Motion.GravityPerFrameSquared,
                    definition.Motion.MaxLaunchAngleTangent,
                    definition.Motion.LifetimeFrames,
                    out _,
                    out _,
                    out _))
            {
                ReportProjectileAimFailure(source, request, spawnProfile, "ProjectileAimTargetOutOfRange", "Locked target is outside ballistic projectile reach.", allowFallback);
                return false;
            }

            effectiveLifetimeFrames = ResolveProjectileEffectiveLifetimeFrames(
                definition,
                origin,
                targetPoint);
            return true;
        }

        private bool TryGetOrCreateMeteorBarrageCastContext(
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            RuntimeFrame frame,
            out MeteorBarrageCastState state)
        {
            state = default;
            if (entity == null || _physicsRuntime == null || _physicsRuntime.World == null)
                return false;

            var key = new MeteorBarrageCastKey(entity.Id.Value, request.Metadata.InstanceId);
            if (_meteorBarrageCastContexts.TryGetValue(key, out state))
                return true;

            FixVector3 forward = CharacterTestFireballProjectileAdapter.ResolveDirection(entity);
            if (!forward.TryNormalize(out FixVector3 normalizedForward))
                normalizedForward = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

            FixVector3 right = new FixVector3(
                normalizedForward.Z,
                Fix64.Zero,
                Fix64.Zero - normalizedForward.X);
            if (!right.TryNormalize(out FixVector3 normalizedRight))
                normalizedRight = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);

            FixVector3 sourceGround = ResolveEntityGroundPoint(entity);
            FixVector3 center = sourceGround + normalizedForward * CharacterTestFireballProjectileAdapter.MeteorBarrageRange;
            if (TryResolveMeteorBarrageLockedTargetCenter(
                    entity,
                    sourceGround,
                    CharacterTestFireballProjectileAdapter.MeteorBarrageRange,
                    out FixVector3 targetCenter))
            {
                center = targetCenter;
            }

            state = new MeteorBarrageCastState(center, normalizedForward, normalizedRight, frame.Value);
            _meteorBarrageCastContexts[key] = state;
            return true;
        }

        private bool TryResolveMeteorBarrageLockedTargetCenter(
            CharacterTestCharacterEntity source,
            FixVector3 sourceGround,
            Fix64 maxRange,
            out FixVector3 center)
        {
            center = FixVector3.Zero;
            int targetEntityId = ResolveLockedTargetEntityId();
            if (targetEntityId <= 0)
                return false;

            CharacterTestCharacterEntity target = _entityLookup.FindEntity(targetEntityId);
            if (target == null || target.Id.Value == source.Id.Value || !target.BattleState.CanReceiveHpDamage)
                return false;

            CombatEntityId sourceCombatId = _physicsRuntime.GetEntityId(source);
            CombatEntityId targetCombatId = _physicsRuntime.GetEntityId(target);
            if (targetCombatId.Equals(sourceCombatId)
                || _entityLookup.ResolveTargetState(targetCombatId) == HitTargetStateFlags.None
                || !_teamRelationProvider.AreHostile(sourceCombatId, targetCombatId))
            {
                return false;
            }

            center = ResolveEntityGroundPoint(target);
            FixVector3 delta = center - sourceGround;
            return maxRange <= Fix64.Zero || delta.LengthSquared() <= maxRange * maxRange;
        }

        private FixVector3 ResolveEntityGroundPoint(CharacterTestCharacterEntity entity)
        {
            CombatBodyId bodyId = _physicsRuntime.GetBodyId(entity);
            if (_physicsRuntime.World.TryGetBody(bodyId, out CombatPhysicsBody body))
            {
                return new FixVector3(
                    body.Position.X,
                    body.Position.Y - _physicsRuntime.GetCharacterCenterOffsetY(entity),
                    body.Position.Z);
            }

            return new FixVector3(
                ToFix64(entity.Transform.X),
                ToFix64(entity.Transform.Y),
                ToFix64(entity.Transform.Z));
        }

        public bool TryResolveTrackingTarget(
            CombatProjectileTrackingTargetQuery query,
            out FixVector3 targetPoint)
        {
            targetPoint = FixVector3.Zero;
            if (_physicsRuntime == null || _physicsRuntime.World == null)
                return false;

            CharacterTestCharacterEntity target = _entityLookup.FindEntityByCombatId(query.TargetEntityId);
            if (target == null)
                return false;

            CharacterTestCharacterEntity source = _entityLookup.FindEntityByCombatId(query.SourceEntityId);
            if (source != null)
            {
                if (target.Id.Value == source.Id.Value)
                    return false;
                if (target.Team.TeamId == source.Team.TeamId)
                    return false;
            }

            if (!target.BattleState.CanReceiveHpDamage
                || _entityLookup.ResolveTargetState(query.TargetEntityId) == HitTargetStateFlags.None)
            {
                return false;
            }

            targetPoint = ResolveProjectileAimTargetPoint(target, Fix64.Zero);
            return true;
        }

        private FixVector3 ResolveProjectileAimTargetPoint(
            CharacterTestCharacterEntity target,
            Fix64 targetHeightOffset)
        {
            CombatBodyId bodyId = _physicsRuntime.GetBodyId(target);
            if (_physicsRuntime.World.TryGetBody(bodyId, out CombatPhysicsBody body))
            {
                return body.Position + new FixVector3(Fix64.Zero, targetHeightOffset, Fix64.Zero);
            }

            return new FixVector3(
                ToFix64(target.Transform.X),
                ToFix64(target.Transform.Y) + _physicsRuntime.CharacterHalfExtents.Y + targetHeightOffset,
                ToFix64(target.Transform.Z));
        }

        private void ResolveCasterForwardAim(
            CharacterTestCharacterEntity entity,
            FixVector3 origin,
            Fix64 range,
            out FixVector3 direction,
            out FixVector3 targetPoint)
        {
            direction = CharacterTestFireballProjectileAdapter.ResolveDirection(entity);
            targetPoint = origin + direction * range;
        }

        private Fix64 ResolveProjectileAimRange(
            CombatProjectileAimConfig aim,
            CombatProjectileDefinition definition)
        {
            if (definition != null && definition.Motion.Kind == CombatProjectileMotionKind.BallisticArcToTarget)
            {
                return CombatProjectileBallistics.CalculateSameHeightMaxRange(
                    definition.Motion.SpeedPerFrame,
                    definition.Motion.GravityPerFrameSquared,
                    definition.Motion.MaxLaunchAngleTangent);
            }

            if (aim.MaxRange > Fix64.Zero)
                return aim.MaxRange;
            return definition.Motion.SpeedPerFrame * Fix64.FromInt(definition.Motion.LifetimeFrames);
        }

        private static bool IsChannelBeamProjectile(CombatProjectileDefinition definition)
        {
            return definition != null
                && string.Equals(
                    definition.StableId,
                    CharacterTestFireballProjectileAdapter.ChannelBeamStableId,
                    StringComparison.Ordinal);
        }

        private void PruneChannelBeamAimCache(CharacterTestCharacterRuntimeWorld world, long frame)
        {
            if (_channelBeamAimCache.Count == 0)
                return;

            if (world == null)
            {
                _channelBeamAimCache.Clear();
                _channelBeamAimCacheRemovals.Clear();
                return;
            }

            _channelBeamAimCacheRemovals.Clear();
            foreach (KeyValuePair<ChannelBeamAimKey, ChannelBeamAimState> entry in _channelBeamAimCache)
            {
                if (frame - entry.Value.CreatedFrame > ChannelBeamAimCacheMaxAgeFrames
                    || !IsChannelBeamAimCasterActive(world, entry.Key.EntityId))
                {
                    _channelBeamAimCacheRemovals.Add(entry.Key);
                }
            }

            for (int i = 0; i < _channelBeamAimCacheRemovals.Count; i++)
                _channelBeamAimCache.Remove(_channelBeamAimCacheRemovals[i]);
            _channelBeamAimCacheRemovals.Clear();
        }

        private static bool IsChannelBeamAimCasterActive(
            CharacterTestCharacterRuntimeWorld world,
            int entityId)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null || entity.Id.Value != entityId)
                    continue;

                return entity.BattleState.CountsAsAlive
                    && entity.Action.ActiveActionId == CharacterTestCharacterActionConfigSource.ChannelBeamActionId;
            }

            return false;
        }

        private void PruneMeteorBarrageCastContexts(CharacterTestCharacterRuntimeWorld world, long frame)
        {
            if (_meteorBarrageCastContexts.Count == 0)
                return;

            if (world == null)
            {
                _meteorBarrageCastContexts.Clear();
                _meteorBarrageCastContextRemovals.Clear();
                return;
            }

            _meteorBarrageCastContextRemovals.Clear();
            foreach (KeyValuePair<MeteorBarrageCastKey, MeteorBarrageCastState> entry in _meteorBarrageCastContexts)
            {
                if (frame - entry.Value.CreatedFrame > MeteorBarrageCastContextMaxAgeFrames
                    || !IsMeteorBarrageCasterActive(world, entry.Key.EntityId))
                {
                    _meteorBarrageCastContextRemovals.Add(entry.Key);
                }
            }

            for (int i = 0; i < _meteorBarrageCastContextRemovals.Count; i++)
                _meteorBarrageCastContexts.Remove(_meteorBarrageCastContextRemovals[i]);
            _meteorBarrageCastContextRemovals.Clear();
        }

        private static bool IsMeteorBarrageCasterActive(
            CharacterTestCharacterRuntimeWorld world,
            int entityId)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null || entity.Id.Value != entityId)
                    continue;

                return entity.BattleState.CountsAsAlive
                    && entity.Action.ActiveActionId == CharacterTestCharacterActionConfigSource.MeteorBarrageActionId;
            }

            return false;
        }

        private static int ResolveProjectileEffectiveLifetimeFrames(
            CombatProjectileDefinition definition,
            FixVector3 origin,
            FixVector3 targetPoint)
        {
            if (definition.Motion.Kind == CombatProjectileMotionKind.BallisticArcToTarget
                && CombatProjectileBallistics.TrySolveLaunchArc(
                    origin,
                    targetPoint,
                    definition.Motion.SpeedPerFrame,
                    definition.Motion.GravityPerFrameSquared,
                    definition.Motion.MaxLaunchAngleTangent,
                    definition.Motion.LifetimeFrames,
                    out _,
                    out _,
                    out int ballisticLifetimeFrames))
            {
                return ballisticLifetimeFrames;
            }

            return ResolveProjectileEffectiveLifetimeFrames(definition.Motion, origin, targetPoint);
        }

        private static int ResolveProjectileEffectiveLifetimeFrames(
            CombatProjectileMotionConfig motion,
            FixVector3 origin,
            FixVector3 targetPoint)
        {
            Fix64 distance = (targetPoint - origin).LengthSquared().Sqrt();
            if (distance <= Fix64.Zero || motion.SpeedPerFrame <= Fix64.Zero)
                return 1;

            Fix64 frames = distance / motion.SpeedPerFrame;
            long whole = frames.RawValue / Fix64.Scale;
            long remainder = frames.RawValue % Fix64.Scale;
            if (remainder > 0)
                whole++;
            if (whole <= 0)
                return 1;
            if (whole > int.MaxValue)
                return int.MaxValue;
            return (int)whole;
        }

        private int ResolveLockedTargetEntityId()
        {
            return _lockedTargetEntityIdProvider == null
                ? 0
                : Math.Max(0, _lockedTargetEntityIdProvider());
        }

        private bool TryResolveLockedCombatTarget(
            CharacterTestCharacterEntity source,
            out CharacterTestCombatTargetHandle handle,
            out CharacterTestLockedHurtboxDistanceSample sample)
        {
            handle = _lockedCombatTargetProvider != null
                ? _lockedCombatTargetProvider()
                : default;
            sample = default;
            return source != null
                && handle.HasTarget
                && handle.SourceEntityId == source.Id.Value
                && _perceptionFrameCache != null
                && _perceptionFrameCache.TryGetLockedHurtboxDistance(
                    _frame,
                    handle,
                    out sample)
                && sample.HasDistance;
        }

        private void ReportProjectileAimFailure(
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            CombatProjectileSpawnProfile spawnProfile,
            string code,
            string message,
            bool allowFallback)
        {
            ReportHitTraceWarning(
                entity.Id.Value,
                request.Metadata.ActionId,
                spawnProfile.StableId,
                string.Empty,
                request.Metadata.LocalFrame,
                code,
                allowFallback
                    ? message + " CasterForwardRange fallback will be used."
                    : message + " Projectile spawn will be rejected.");
        }

        private FixVector3 ResolveProjectileOrigin(
            CharacterTestCharacterEntity entity,
            CharacterActionGameplayRequest request,
            CharacterTestCharacterActionRuntime actionRuntime,
            CombatProjectileSpawnProfile spawnProfile)
        {
            CombatProjectileSpawnOriginConfig origin = spawnProfile.Origin;
            if (origin.Kind == CombatProjectileSpawnOriginKind.Socket
                && TryResolveActiveAnimationClipKey(
                    actionRuntime,
                    request.Metadata.ActionId,
                    request.Metadata.LocalFrame,
                    out string clipKey)
                && TryGetProjectileSocketFrame(entity, clipKey, origin.SocketId,
                    request.Metadata.LocalFrame, out MxAnimationBakedSocketFrame socketFrame))
            {
                FixVector3 local = ToFixVector3(socketFrame.Position)
                    + ResolveAuthorityPoseSocketOffset(entity, origin.SocketId)
                    + origin.LocalOffset;
                return TransformLocal(entity.Transform, local);
            }

            ReportHitTraceWarning(
                entity.Id.Value,
                request.Metadata.ActionId,
                spawnProfile.StableId,
                string.Empty,
                request.Metadata.LocalFrame,
                "ProjectileSpawnSocketFallback",
                "Projectile spawn socket frame is missing. Fallback root offset will be used. socket="
                + CharacterTestCombatTraceText.EmptyAsNone(origin.SocketLabel)
                + ".");
            return TransformLocal(entity.Transform, origin.FallbackRootOffset + origin.LocalOffset);
        }

        private bool TryGetProjectileSocketFrame(
            CharacterTestCharacterEntity entity,
            string clipKey,
            int socketId,
            int localFrame,
            out MxAnimationBakedSocketFrame frame)
        {
            if (_bakedTraceSource is ICharacterTestPackageBakedSocketSource packages)
                return packages.TryGetSocketFrame(entity.Identity.GeneratedPackageRoot,
                    clipKey, socketId, localFrame, out frame);
            return _bakedTraceSource.TryGetSocketFrame(clipKey, socketId, localFrame, out frame);
        }

        private static FixVector3 ResolveAuthorityPoseSocketOffset(
            CharacterTestCharacterEntity entity,
            int socketId)
        {
            return entity?.AuthorityPose != null
                && entity.AuthorityPose.TryGetSocketOffset(socketId, out FixVector3 offset)
                    ? offset
                    : FixVector3.Zero;
        }

    }
}
