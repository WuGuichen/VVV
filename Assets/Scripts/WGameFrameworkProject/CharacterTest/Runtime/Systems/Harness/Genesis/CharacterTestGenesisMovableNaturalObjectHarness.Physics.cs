using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestGenesisMovableNaturalObjectHarness
    {
        private static void AdvanceFree(MovableObjectState obj, float deltaTime)
        {
            float beforeX = obj.PositionX;
            obj.PositionX += obj.VelocityX * deltaTime;
            obj.DisplacementX += obj.PositionX - beforeX;
        }

        private static void ApplyForceOrigin(
            MovableObjectState obj,
            CharacterTestGenesisForceOriginKind forceOrigin,
            int tick,
            float deltaTime)
        {
            switch (forceOrigin)
            {
                case CharacterTestGenesisForceOriginKind.SelfWeight:
                    obj.ContactPressure += obj.MassKg * 9.8f * deltaTime;
                    if (tick == 0)
                        obj.VelocityX += PushImpulseSpeed * 0.25f;
                    break;
                case CharacterTestGenesisForceOriginKind.ActiveForce:
                    obj.VelocityX += PushAcceleration * deltaTime;
                    break;
                case CharacterTestGenesisForceOriginKind.ContinuousExternalForce:
                    if (tick < 12)
                        obj.VelocityX += PushAcceleration * deltaTime;
                    break;
                case CharacterTestGenesisForceOriginKind.InstantImpulse:
                    if (tick == 0)
                        obj.VelocityX += PushImpulseSpeed;
                    break;
            }
        }

        private static void ApplySurfaceFriction(
            MovableObjectState obj,
            float deltaTime,
            CombatSurfaceMaterial contactMaterial)
        {
            float deceleration = 0.5f + ToFloat(contactMaterial.Friction) * 0.35f;
            obj.VelocityX = MoveToward(obj.VelocityX, 0f, deceleration * deltaTime);
        }

        private static float MoveToward(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
                return target;

            return current + Math.Sign(target - current) * maxDelta;
        }

        private static CombatPhysicsWorld BuildMovableObjectWorld(params MovableObjectState[] objects)
        {
            var world = new CombatPhysicsWorld();
            for (int i = 0; i < objects.Length; i++)
            {
                MovableObjectState obj = objects[i];
                var bodyId = new CombatBodyId(obj.NumericStableId);
                world.UpsertBody(new CombatPhysicsBody(
                    new CombatEntityId(obj.NumericStableId),
                    bodyId,
                    new FixVector3(ToFix64(obj.PositionX), Fix64.Zero, ToFix64(obj.PositionZ))));
                world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                    bodyId,
                    new CombatColliderId(obj.NumericStableId),
                    MovableLayer,
                    new FixVector3(
                        -ToFix64(HalfExtentMeters),
                        Fix64.Zero,
                        -ToFix64(HalfExtentMeters)),
                    new FixVector3(
                        ToFix64(HalfExtentMeters),
                        ToFix64(ObjectHeightMeters),
                        ToFix64(HalfExtentMeters)),
                    obj.Material));
            }

            return world;
        }

        private static CombatPhysicsWorld BuildMovableObjectWorld(
            MovableStaticAabb staticAabb,
            params MovableObjectState[] objects)
        {
            CombatPhysicsWorld world = BuildMovableObjectWorld(objects);
            var bodyId = new CombatBodyId(staticAabb.BodyId);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(staticAabb.EntityId),
                bodyId,
                FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(staticAabb.ColliderId),
                MovableLayer,
                staticAabb.LocalMin,
                staticAabb.LocalMax,
                staticAabb.Material));
            return world;
        }

        private static MovablePhysicsProbe QueryMovableContacts(
            CombatPhysicsWorld world,
            MovableObjectState source,
            int tick)
        {
            var hits = new List<CombatQueryResult>();
            var header = new CombatQueryHeader(
                queryId: source.NumericStableId + tick,
                kind: CombatQueryKind.Aabb,
                sourceEntityId: new CombatEntityId(source.NumericStableId),
                traceId: MovableQueryTraceId,
                actionId: source.NumericStableId,
                sourceOrder: tick,
                layerMask: CombatPhysicsLayerMask.FromLayer(MovableLayer));
            FixVector3 min = new FixVector3(
                ToFix64(source.PositionX - HalfExtentMeters),
                Fix64.Zero,
                ToFix64(source.PositionZ - HalfExtentMeters));
            FixVector3 max = new FixVector3(
                ToFix64(source.PositionX + HalfExtentMeters),
                ToFix64(ObjectHeightMeters),
                ToFix64(source.PositionZ + HalfExtentMeters));
            CombatPhysicsQuery query = CombatPhysicsQuery.From(
                new CombatAabbQuery(header, min, max),
                new CombatPhysicsQueryFilter(includeSourceEntity: false));
            world.Query(query, hits);

            CombatPhysicsWorldStats stats = world.CreateStats();
            for (int i = 0; i < hits.Count; i++)
            {
                CombatQueryResult hit = hits[i];
                if (!world.TryGetBody(hit.TargetBodyId, out CombatPhysicsBody targetBody)
                    || !world.TryGetAabbCollider(
                        hit.TargetBodyId,
                        hit.TargetColliderId,
                        out CombatPhysicsAabbCollider targetCollider))
                {
                    continue;
                }

                FixVector3 contactMin = targetCollider.GetWorldMin(targetBody.Position);
                FixVector3 contactMax = targetCollider.GetWorldMax(targetBody.Position);
                return new MovablePhysicsProbe(
                    stats,
                    hits.Count,
                    hasContact: true,
                    targetBody.BodyId.Value,
                    targetCollider.SurfaceMaterial,
                    ToFloat(contactMin.X),
                    ToFloat(contactMax.X));
            }

            return new MovablePhysicsProbe(
                stats,
                hits.Count,
                hasContact: false,
                0,
                CombatSurfaceMaterial.Default,
                0f,
                0f);
        }

        private static MovableStaticAabb CreateSupportSurface(CombatSurfaceMaterial material)
        {
            return new MovableStaticAabb(
                StaticSupportEntity,
                StaticSupportBody,
                StaticSupportCollider,
                new FixVector3(-Fix64.FromInt(20), -Fix64.FromRatio(1, 10), -Fix64.FromInt(2)),
                new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(2)),
                material);
        }

        private static MovableStaticAabb CreateRightBoundary(float centerLimitX, CombatSurfaceMaterial material)
        {
            float wallLeft = centerLimitX + HalfExtentMeters;
            return new MovableStaticAabb(
                StaticBoundaryEntity,
                StaticBoundaryBody,
                StaticBoundaryCollider,
                new FixVector3(ToFix64(wallLeft), Fix64.Zero, -Fix64.FromInt(2)),
                new FixVector3(ToFix64(wallLeft + HalfExtentMeters), ToFix64(ObjectHeightMeters), Fix64.FromInt(2)),
                material);
        }

        private static MovableStaticAabb CreateLeftBoundary(float centerLimitX, CombatSurfaceMaterial material)
        {
            float wallRight = centerLimitX - HalfExtentMeters;
            return new MovableStaticAabb(
                StaticBoundaryEntity,
                StaticBoundaryBody,
                StaticBoundaryCollider,
                new FixVector3(ToFix64(wallRight - HalfExtentMeters), Fix64.Zero, -Fix64.FromInt(2)),
                new FixVector3(ToFix64(wallRight), ToFix64(ObjectHeightMeters), Fix64.FromInt(2)),
                material);
        }

        private static string CreateStableContactPair(MovableObjectState left, MovableObjectState right)
        {
            if (left.NumericStableId <= right.NumericStableId)
                return left.StableId + "->" + right.StableId;

            return right.StableId + "->" + left.StableId;
        }

        private static CharacterTestGenesisMovableObjectSample CreateSample(
            MovableObjectState obj,
            CharacterTestGenesisForceOriginKind forceOrigin,
            int tickCount,
            CombatPhysicsWorldStats stats,
            int queryHitCount)
        {
            return new CharacterTestGenesisMovableObjectSample(
                obj.StableId,
                obj.NumericStableId,
                obj.Category,
                obj.MassKg,
                obj.Material.Kind,
                obj.Material.StableId,
                ToFloat(obj.Material.Friction),
                ToFloat(obj.Material.Elasticity),
                tickCount,
                DefaultTickRate,
                obj.PositionX,
                obj.PositionZ,
                obj.DisplacementX,
                obj.DisplacementZ,
                obj.VelocityX,
                obj.VelocityZ,
                obj.ContactVelocityX,
                obj.ContactVelocityZ,
                obj.ContactDisplacementX,
                obj.ContactDisplacementZ,
                obj.ContactPressure,
                string.IsNullOrWhiteSpace(obj.BehaviorState) ? "sampled" : obj.BehaviorState,
                obj.ContactPair,
                "harness-only",
                forceOrigin,
                stats.BodyCount,
                stats.ColliderCount,
                queryHitCount);
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)((double)value.RawValue / Fix64.Scale);
        }

        private sealed class MovableObjectState
        {
            public MovableObjectState(
                int numericStableId,
                string stableId,
                string category,
                float massKg,
                CombatSurfaceMaterial material,
                float positionX,
                float positionZ)
            {
                NumericStableId = numericStableId;
                StableId = stableId ?? string.Empty;
                Category = category ?? string.Empty;
                MassKg = massKg > 0f ? massKg : 1f;
                Material = material;
                PositionX = positionX;
                PositionZ = positionZ;
            }

            public int NumericStableId { get; }
            public string StableId { get; }
            public string Category { get; }
            public float MassKg { get; }
            public CombatSurfaceMaterial Material { get; }
            public float PositionX { get; set; }
            public float PositionZ { get; }
            public float VelocityX { get; set; }
            public float VelocityZ { get; }
            public float DisplacementX { get; set; }
            public float DisplacementZ { get; }
            public float ContactVelocityX { get; set; }
            public float ContactVelocityZ { get; }
            public float ContactDisplacementX { get; set; }
            public float ContactDisplacementZ { get; }
            public float ContactPressure { get; set; }
            public string BehaviorState { get; set; }
            public string ContactPair { get; set; }
        }

        private readonly struct MovableStaticAabb
        {
            public MovableStaticAabb(
                int entityId,
                int bodyId,
                int colliderId,
                FixVector3 localMin,
                FixVector3 localMax,
                CombatSurfaceMaterial material)
            {
                EntityId = entityId;
                BodyId = bodyId;
                ColliderId = colliderId;
                LocalMin = localMin;
                LocalMax = localMax;
                Material = material;
            }

            public int EntityId { get; }
            public int BodyId { get; }
            public int ColliderId { get; }
            public FixVector3 LocalMin { get; }
            public FixVector3 LocalMax { get; }
            public CombatSurfaceMaterial Material { get; }
        }

        private readonly struct MovablePhysicsProbe
        {
            public MovablePhysicsProbe(
                CombatPhysicsWorldStats stats,
                int queryHitCount,
                bool hasContact,
                int contactBodyId,
                CombatSurfaceMaterial contactMaterial,
                float contactMinX,
                float contactMaxX)
            {
                Stats = stats;
                QueryHitCount = queryHitCount < 0 ? 0 : queryHitCount;
                HasContact = hasContact;
                ContactBodyId = contactBodyId;
                ContactMaterial = contactMaterial;
                ContactMinX = contactMinX;
                ContactMaxX = contactMaxX;
            }

            public CombatPhysicsWorldStats Stats { get; }
            public int QueryHitCount { get; }
            public bool HasContact { get; }
            public int ContactBodyId { get; }
            public CombatSurfaceMaterial ContactMaterial { get; }
            public float ContactMinX { get; }
            public float ContactMaxX { get; }
        }
    }
}
