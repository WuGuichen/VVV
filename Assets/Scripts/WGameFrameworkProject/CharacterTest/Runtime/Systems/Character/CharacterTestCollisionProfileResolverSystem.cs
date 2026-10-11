using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestCollisionProfileResolverSystem
    {
        private const int CrouchHeightScaleMilli = 600;
        private const int LowHeightScaleMilli = 450;
        private const int CorpseHeightScaleMilli = 220;
        private readonly List<CombatQueryResult> _clearanceHits = new List<CombatQueryResult>(8);

        /// <remarks>NoAlloc after initialization; reuses the clearance query result list.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            RuntimeFrame frame,
            CharacterTestTraceRecorder traceRecorder)
        {
            if (world == null)
                return;

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
                ResolveEntity(world.CharacterComponents, entities[i], physicsRuntime, frame, traceRecorder);
        }

        private void ResolveEntity(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            RuntimeFrame frame,
            CharacterTestTraceRecorder traceRecorder)
        {
            if (components == null || entity == null)
                return;

            CharacterTestColliderPostureComponent previous = entity.ColliderPosture;
            CharacterTestCharacterActionComponent action = entity.Action;
            if (ShouldClearActionOverride(previous, action))
                previous = previous.WithoutActionOverride();

            CharacterTestColliderPostureComponent resolved = ResolveCandidate(previous, entity, frame.Value);
            if (RequiresClearance(previous, resolved)
                && !HasExpansionClearance(entity, previous, resolved, physicsRuntime))
            {
                traceRecorder?.LocomotionCollisionProfileBlocked(
                    frame,
                    entity.Id.Value,
                    new CharacterTestCollisionProfileBlockedTraceEvent(
                        resolved.ProfileId,
                        previous.ProfileId,
                        _clearanceHits.Count));
                resolved = previous.WithResolved(
                    previous.ProfileId,
                    previous.HeightMillimeters,
                    previous.RadiusMillimeters,
                    previous.CenterYMillimeters,
                    previous.BlocksCharacters,
                    previous.CanBePushed,
                    CharacterTestColliderPostureSource.ClearanceBlocked,
                    previous.Priority,
                    frame.Value);
            }

            components.SetColliderPosture(entity.Id, resolved);
        }

        private static bool ShouldClearActionOverride(
            CharacterTestColliderPostureComponent posture,
            CharacterTestCharacterActionComponent action)
        {
            return posture.HasActionOverride
                && posture.ActionRestorePolicy == CharacterActionCollisionProfileRestorePolicy.OnActionEnd
                && posture.ActionSourceActionId != action.ActiveActionId;
        }

        private static CharacterTestColliderPostureComponent ResolveCandidate(
            CharacterTestColliderPostureComponent previous,
            CharacterTestCharacterEntity entity,
            long frame)
        {
            CharacterTestEntityPresenceComponent presence = entity.Presence;
            if (!presence.IsActive)
                return ResolveProfile(
                    previous,
                    entity.Physics,
                    CharacterTestColliderPostureComponent.CorpseProfileId,
                    CharacterTestColliderPostureSource.Presence,
                    priority: 100,
                    frame);

            CharacterTestCharacterLifeState lifeState = entity.CombatState.LifeState;
            if (lifeState == CharacterTestCharacterLifeState.Dead)
                return ResolveProfile(
                    previous,
                    entity.Physics,
                    CharacterTestColliderPostureComponent.CorpseProfileId,
                    CharacterTestColliderPostureSource.Lifecycle,
                    priority: 90,
                    frame);

            if (lifeState == CharacterTestCharacterLifeState.Reviving)
                return ResolveProfile(
                    previous,
                    entity.Physics,
                    CharacterTestColliderPostureComponent.ReviveLowProfileId,
                    CharacterTestColliderPostureSource.Lifecycle,
                    priority: 80,
                    frame);

            if (previous.HasActionOverride)
                return ResolveProfile(
                    previous,
                    entity.Physics,
                    previous.ActionProfileId,
                    CharacterTestColliderPostureSource.ActionTimeline,
                    priority: 60,
                    frame);

            return ResolveProfile(
                previous,
                entity.Physics,
                CharacterTestColliderPostureComponent.StandingProfileId,
                CharacterTestColliderPostureSource.Standing,
                priority: 0,
                frame);
        }

        private static CharacterTestColliderPostureComponent ResolveProfile(
            CharacterTestColliderPostureComponent previous,
            CharacterTestCharacterPhysicsComponent physics,
            int profileId,
            CharacterTestColliderPostureSource source,
            int priority,
            long frame)
        {
            physics = physics.IsConfigured ? physics : CharacterTestCharacterPhysicsComponent.Default;
            int baseHeight = physics.EffectiveHeightMillimeters;
            int height = baseHeight;
            int radius = physics.EffectiveRadiusMillimeters;
            int centerY = physics.CapsuleCenterYMillimeters > 0
                ? physics.CapsuleCenterYMillimeters
                : baseHeight / 2;
            bool blocks = physics.BlocksCharacters;
            bool canBePushed = physics.CanBePushed;

            if (profileId == CharacterTestColliderPostureComponent.CrouchProfileId)
            {
                height = ScaleHeight(baseHeight, CrouchHeightScaleMilli);
                centerY = height / 2;
            }
            else if (profileId == CharacterTestColliderPostureComponent.LowProfileId)
            {
                height = ScaleHeight(baseHeight, LowHeightScaleMilli);
                centerY = height / 2;
            }
            else if (profileId == CharacterTestColliderPostureComponent.ReviveLowProfileId)
            {
                height = ScaleHeight(baseHeight, LowHeightScaleMilli);
                centerY = height / 2;
                canBePushed = false;
            }
            else if (profileId == CharacterTestColliderPostureComponent.CorpseProfileId)
            {
                height = ScaleHeight(baseHeight, CorpseHeightScaleMilli);
                centerY = height / 2;
                blocks = false;
                canBePushed = false;
            }
            else
            {
                profileId = CharacterTestColliderPostureComponent.StandingProfileId;
            }

            ClampCapsuleDimensions(ref height, ref radius, ref centerY);
            return previous.WithResolved(
                profileId,
                height,
                radius,
                centerY,
                blocks,
                canBePushed,
                source,
                priority,
                frame);
        }

        private static int ScaleHeight(int baseHeightMillimeters, int scaleMilli)
        {
            int height = CharacterTestDeterministicMath.MulDivRoundToInt(
                baseHeightMillimeters,
                scaleMilli,
                CharacterTestDeterministicUnits.ScaleFactorScale);
            return System.Math.Max(CharacterTestCharacterPhysicsComponent.MinimumHeightMillimeters, height);
        }

        private static void ClampCapsuleDimensions(ref int height, ref int radius, ref int centerY)
        {
            if (height < CharacterTestCharacterPhysicsComponent.MinimumHeightMillimeters)
                height = CharacterTestCharacterPhysicsComponent.MinimumHeightMillimeters;
            if (radius < CharacterTestCharacterPhysicsComponent.MinimumRadiusMillimeters)
                radius = CharacterTestCharacterPhysicsComponent.MinimumRadiusMillimeters;

            int maxRadius = height / 2;
            if (maxRadius < CharacterTestCharacterPhysicsComponent.MinimumRadiusMillimeters)
            {
                height = CharacterTestCharacterPhysicsComponent.MinimumRadiusMillimeters * 2;
                maxRadius = CharacterTestCharacterPhysicsComponent.MinimumRadiusMillimeters;
            }

            if (radius > maxRadius)
                radius = maxRadius;
            if (centerY < radius)
                centerY = radius;
            int maxCenterY = height - radius;
            if (centerY > maxCenterY)
                centerY = maxCenterY;
        }

        private static bool RequiresClearance(
            CharacterTestColliderPostureComponent previous,
            CharacterTestColliderPostureComponent next)
        {
            if (!next.BlocksCharacters)
                return false;

            return (!previous.BlocksCharacters && next.BlocksCharacters)
                || next.HeightMillimeters > previous.HeightMillimeters
                || next.RadiusMillimeters > previous.RadiusMillimeters;
        }

        private bool HasExpansionClearance(
            CharacterTestCharacterEntity entity,
            CharacterTestColliderPostureComponent previous,
            CharacterTestColliderPostureComponent next,
            CharacterTestCharacterPhysicsRuntime physicsRuntime)
        {
            _clearanceHits.Clear();
            if (entity == null
                || physicsRuntime == null
                || physicsRuntime.World == null
                || physicsRuntime.MotionObstacleLayerMask.Value == 0)
            {
                return true;
            }

            int lowerHeight = previous.BlocksCharacters
                ? previous.HeightMillimeters
                : 0;
            if (next.HeightMillimeters <= lowerHeight && next.RadiusMillimeters <= previous.RadiusMillimeters)
                return true;

            Fix64 x = CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.XMillimeters);
            Fix64 y = CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.YMillimeters);
            Fix64 z = CharacterTestDeterministicUnits.MillimetersToFix64Meters(entity.Transform.ZMillimeters);
            Fix64 radius = CharacterTestDeterministicUnits.MillimetersToFix64Meters(next.RadiusMillimeters);
            Fix64 minY = y + CharacterTestDeterministicUnits.MillimetersToFix64Meters(lowerHeight);
            Fix64 maxY = y + CharacterTestDeterministicUnits.MillimetersToFix64Meters(next.HeightMillimeters);
            if (maxY <= minY)
                return true;

            var header = new CombatQueryHeader(
                0,
                CombatQueryKind.Aabb,
                physicsRuntime.GetEntityId(entity),
                0,
                0,
                0,
                physicsRuntime.MotionObstacleLayerMask);
            var query = new CombatAabbQuery(
                header,
                new FixVector3(x - radius, minY, z - radius),
                new FixVector3(x + radius, maxY, z + radius));
            CombatPhysicsQuery physicsQuery = CombatPhysicsQuery.From(
                query,
                new CombatPhysicsQueryFilter(includeSourceEntity: false));
            physicsRuntime.World.Query(physicsQuery, _clearanceHits);
            return _clearanceHits.Count == 0;
        }
    }
}
