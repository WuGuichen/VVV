using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Resources;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestGenesisMovableNaturalObjectHarness
    {
        public const int DefaultTickRate = 60;
        public const int DefaultTickCount = 36;

        private const int MovableLayer = 2;
        private const int StaticSupportEntity = 621900;
        private const int StaticSupportBody = 621900;
        private const int StaticSupportCollider = 621901;
        private const int StaticBoundaryEntity = 621910;
        private const int StaticBoundaryBody = 621910;
        private const int StaticBoundaryCollider = 621911;
        private const int MovableQueryTraceId = 621;
        private const float HalfExtentMeters = 0.25f;
        private const float ObjectHeightMeters = 0.5f;
        private const float PushImpulseSpeed = 6f;
        private const float PushAcceleration = 18f;
        private const float PressureStartX = 0.65f;
        private const float PressureBoundaryX = 0.67f;
        private const float PressureForce = 100f;
        private const float ReboundBoundaryX = 0.25f;
        private const float ReboundImpactSpeed = -6f;

        internal static CharacterTestGenesisMovableNaturalObjectValidation RunProbe(
            CharacterTestGenesisMovableObjectProbeKind scenario,
            CharacterTestGenesisForceOriginKind forceOrigin,
            RuntimeFrame frame)
        {
            return RunValidationSuite(scenario, forceOrigin, frame);
        }

        internal static CharacterTestGenesisMovableNaturalObjectValidation RunValidationSuite(
            CharacterTestGenesisMovableObjectProbeKind scenario =
                CharacterTestGenesisMovableObjectProbeKind.ValidationSuite,
            CharacterTestGenesisForceOriginKind forceOrigin =
                CharacterTestGenesisForceOriginKind.InstantImpulse,
            RuntimeFrame frame = default)
        {
            CharacterTestGenesisMovableObjectSample pushHardSlick = RunPushSample(
                621101,
                "movable.natural.push.hard_slick",
                CombatSurfaceMaterial.HardSlick,
                forceOrigin);
            CharacterTestGenesisMovableObjectSample pushRough = RunPushSample(
                621102,
                "movable.natural.push.rough",
                CombatSurfaceMaterial.Rough,
                forceOrigin);
            CharacterTestGenesisMovableObjectSample pressureLight = RunPressureSample(
                621201,
                "movable.natural.pressure.light",
                massKg: 25f);
            CharacterTestGenesisMovableObjectSample pressureHeavy = RunPressureSample(
                621202,
                "movable.natural.pressure.heavy",
                massKg: 120f);
            CharacterTestGenesisMovableObjectSample reboundElastic = RunReboundSample(
                621301,
                "movable.natural.rebound.elastic",
                CombatSurfaceMaterial.Elastic);
            CharacterTestGenesisMovableObjectSample reboundRough = RunReboundSample(
                621302,
                "movable.natural.rebound.rough",
                CombatSurfaceMaterial.Rough);
            RunObjectContactSample(
                out CharacterTestGenesisMovableObjectSample contactA,
                out CharacterTestGenesisMovableObjectSample contactB);

            return new CharacterTestGenesisMovableNaturalObjectValidation(
                scenario,
                forceOrigin,
                frame,
                stateModel: "harness-only",
                pushHardSlick,
                pushRough,
                pressureLight,
                pressureHeavy,
                reboundElastic,
                reboundRough,
                contactA,
                contactB);
        }

        private static CharacterTestGenesisMovableObjectSample RunPushSample(
            int numericId,
            string stableId,
            CombatSurfaceMaterial material,
            CharacterTestGenesisForceOriginKind forceOrigin)
        {
            var obj = new MovableObjectState(
                numericId,
                stableId,
                "movable-natural-object",
                massKg: 40f,
                material,
                positionX: 0f,
                positionZ: 0f);
            float deltaTime = 1f / DefaultTickRate;
            CombatPhysicsWorldStats stats = default;
            int queryHitCount = 0;
            MovableStaticAabb support = CreateSupportSurface(material);

            for (int tick = 0; tick < DefaultTickCount; tick++)
            {
                ApplyForceOrigin(obj, forceOrigin, tick, deltaTime);
                float beforeX = obj.PositionX;
                obj.PositionX += obj.VelocityX * deltaTime;
                obj.DisplacementX += obj.PositionX - beforeX;
                obj.ContactVelocityX = obj.VelocityX;
                obj.ContactDisplacementX = obj.DisplacementX;
                CombatPhysicsWorld world = BuildMovableObjectWorld(support, obj);
                MovablePhysicsProbe probe = QueryMovableContacts(world, obj, tick);
                stats = probe.Stats;
                queryHitCount += probe.QueryHitCount;
                ApplySurfaceFriction(obj, deltaTime, probe.HasContact ? probe.ContactMaterial : obj.Material);
            }

            obj.BehaviorState = Math.Abs(obj.VelocityX) > 0.05f ? "push-sliding" : "push-stopped";
            return CreateSample(obj, forceOrigin, DefaultTickCount, stats, queryHitCount);
        }

        private static CharacterTestGenesisMovableObjectSample RunPressureSample(
            int numericId,
            string stableId,
            float massKg)
        {
            var obj = new MovableObjectState(
                numericId,
                stableId,
                "movable-natural-object",
                massKg,
                CombatSurfaceMaterial.Rough,
                PressureStartX,
                positionZ: 0f);
            float deltaTime = 1f / DefaultTickRate;
            CombatPhysicsWorldStats stats = default;
            int queryHitCount = 0;
            MovableStaticAabb boundary = CreateRightBoundary(PressureBoundaryX, CombatSurfaceMaterial.Rough);

            for (int tick = 0; tick < DefaultTickCount; tick++)
            {
                obj.VelocityX += (PressureForce / obj.MassKg) * deltaTime;
                float beforeX = obj.PositionX;
                float attemptedX = obj.PositionX + obj.VelocityX * deltaTime;
                obj.PositionX = attemptedX;
                CombatPhysicsWorld world = BuildMovableObjectWorld(boundary, obj);
                MovablePhysicsProbe probe = QueryMovableContacts(world, obj, tick);
                stats = probe.Stats;
                queryHitCount += probe.QueryHitCount;
                if (probe.HasContact)
                {
                    float resolvedX = probe.ContactMinX - HalfExtentMeters;
                    float overrun = Math.Max(0f, attemptedX - resolvedX);
                    obj.PositionX = resolvedX;
                    obj.ContactPair = obj.StableId + "->boundary:rough_wall";
                    obj.ContactDisplacementX += overrun;
                    obj.ContactVelocityX = Math.Max(obj.ContactVelocityX, obj.VelocityX);
                    obj.ContactPressure += PressureForce * deltaTime;
                    obj.VelocityX = 0f;
                }

                obj.DisplacementX += obj.PositionX - beforeX;
                ApplySurfaceFriction(obj, deltaTime, obj.Material);
            }

            obj.BehaviorState = obj.ContactPressure > 0f ? "pressure-blocked" : "pressure-resisting";
            return CreateSample(
                obj,
                CharacterTestGenesisForceOriginKind.ContinuousExternalForce,
                DefaultTickCount,
                stats,
                queryHitCount);
        }

        private static CharacterTestGenesisMovableObjectSample RunReboundSample(
            int numericId,
            string stableId,
            CombatSurfaceMaterial material)
        {
            var obj = new MovableObjectState(
                numericId,
                stableId,
                "movable-natural-object",
                massKg: 35f,
                material,
                positionX: 0.35f,
                positionZ: 0f)
            {
                VelocityX = ReboundImpactSpeed
            };
            float deltaTime = 1f / DefaultTickRate;
            CombatPhysicsWorldStats stats = default;
            int queryHitCount = 0;
            MovableStaticAabb boundary = CreateLeftBoundary(ReboundBoundaryX, material);

            for (int tick = 0; tick < 12; tick++)
            {
                float beforeX = obj.PositionX;
                float attemptedX = obj.PositionX + obj.VelocityX * deltaTime;
                obj.PositionX = attemptedX;
                CombatPhysicsWorld world = BuildMovableObjectWorld(boundary, obj);
                MovablePhysicsProbe probe = QueryMovableContacts(world, obj, tick);
                stats = probe.Stats;
                queryHitCount += probe.QueryHitCount;
                if (probe.HasContact)
                {
                    float resolvedX = probe.ContactMaxX + HalfExtentMeters;
                    float overrun = Math.Max(0f, resolvedX - attemptedX);
                    float incomingSpeed = Math.Abs(obj.VelocityX);
                    obj.PositionX = resolvedX;
                    obj.ContactPair = obj.StableId + "->boundary:" + probe.ContactMaterial.StableId;
                    obj.ContactVelocityX = -incomingSpeed;
                    obj.ContactDisplacementX += overrun;
                    obj.ContactPressure += incomingSpeed * obj.MassKg;
                    obj.VelocityX = incomingSpeed * ToFloat(probe.ContactMaterial.Elasticity);
                }

                obj.DisplacementX += obj.PositionX - beforeX;
                ApplySurfaceFriction(obj, deltaTime, obj.Material);
            }

            obj.BehaviorState = obj.VelocityX > 0.05f ? "rebound-returned-force" : "rebound-absorbed";
            return CreateSample(
                obj,
                CharacterTestGenesisForceOriginKind.InstantImpulse,
                tickCount: 12,
                stats,
                queryHitCount);
        }

        private static void RunObjectContactSample(
            out CharacterTestGenesisMovableObjectSample contactA,
            out CharacterTestGenesisMovableObjectSample contactB)
        {
            var objectA = new MovableObjectState(
                621401,
                "movable.natural.contact.a",
                "movable-natural-object",
                massKg: 40f,
                CombatSurfaceMaterial.HardSlick,
                positionX: 0f,
                positionZ: 0f)
            {
                VelocityX = 5.5f
            };
            var objectB = new MovableObjectState(
                621402,
                "movable.natural.contact.b",
                "movable-natural-object",
                massKg: 40f,
                CombatSurfaceMaterial.Rough,
                positionX: 1.1f,
                positionZ: 0f);
            float deltaTime = 1f / DefaultTickRate;
            CombatPhysicsWorldStats stats = default;
            int queryHitCount = 0;
            string stablePair = CreateStableContactPair(objectA, objectB);

            for (int tick = 0; tick < DefaultTickCount; tick++)
            {
                AdvanceFree(objectA, deltaTime);
                AdvanceFree(objectB, deltaTime);
                CombatPhysicsWorld world = BuildMovableObjectWorld(objectA, objectB);
                MovablePhysicsProbe probe = QueryMovableContacts(world, objectA, tick);
                stats = probe.Stats;
                queryHitCount += probe.QueryHitCount;
                if (probe.HasContact && probe.ContactBodyId == objectB.NumericStableId)
                {
                    float overlap = Math.Max(0f, (objectA.PositionX + HalfExtentMeters) - probe.ContactMinX);
                    objectA.PositionX -= overlap * 0.5f;
                    objectB.PositionX += overlap * 0.5f;
                    objectA.ContactPair = stablePair;
                    objectB.ContactPair = stablePair;
                    objectA.ContactDisplacementX += overlap * 0.5f;
                    objectB.ContactDisplacementX += overlap * 0.5f;
                    float transferVelocity = Math.Max(0f, objectA.VelocityX)
                        * objectA.MassKg
                        / (objectA.MassKg + objectB.MassKg);
                    objectA.ContactVelocityX = Math.Max(objectA.ContactVelocityX, objectA.VelocityX);
                    objectB.ContactVelocityX = Math.Max(objectB.ContactVelocityX, transferVelocity);
                    objectB.VelocityX += transferVelocity;
                    objectA.VelocityX = Math.Max(0f, objectA.VelocityX - transferVelocity);
                    objectA.ContactPressure += transferVelocity * objectA.MassKg;
                    objectB.ContactPressure += transferVelocity * objectB.MassKg;
                }

                ApplySurfaceFriction(objectA, deltaTime, objectA.Material);
                ApplySurfaceFriction(objectB, deltaTime, objectB.Material);
            }

            objectA.BehaviorState = "object-contact-source";
            objectB.BehaviorState = objectB.DisplacementX > 0f ? "object-contact-received" : "object-contact-idle";
            contactA = CreateSample(
                objectA,
                CharacterTestGenesisForceOriginKind.InstantImpulse,
                DefaultTickCount,
                stats,
                queryHitCount);
            contactB = CreateSample(
                objectB,
                CharacterTestGenesisForceOriginKind.InstantImpulse,
                DefaultTickCount,
                stats,
                queryHitCount);
        }

    }
}
