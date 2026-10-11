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
    public enum CharacterTestGenesisBoundaryKind
    {
        Ground = 0,
        Wall = 1,
        LowStep = 2,
        Edge = 3
    }

    public static class CharacterTestGenesisBoundaryFormsHarness
    {
        public const int DefaultTickRate = 60;
        public const int DefaultRunTicks = 60;

        private const int BlockingLayer = 1;
        private const int PlayerEntity = 61801;
        private const int PlayerBody = 61801;
        private const int GroundEntity = 61810;
        private const int GroundBody = 61810;
        private const int GroundCollider = 1;
        private const int BoundaryCollider = 1;

        private static readonly CombatEntityId PlayerEntityId = new CombatEntityId(PlayerEntity);
        private static readonly CombatBodyId PlayerBodyId = new CombatBodyId(PlayerBody);
        private static readonly CombatPhysicsLayerMask BlockingMask = CombatPhysicsLayerMask.FromLayer(BlockingLayer);
        private static readonly FixVector3 CharacterHalfExtents = new FixVector3(Fix64.Half, Fix64.One, Fix64.Half);
        private static readonly FixVector3 GroundedStart = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
        private static readonly FixVector3 UnitX = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);

        public static CharacterTestGenesisBoundaryFormsSample RunGroundSample(CombatSurfaceMaterial material)
        {
            CombatPhysicsWorld world = CreateWorld(GroundedStart);
            RegisterStaticAabb(
                world,
                GroundEntity,
                GroundBody,
                GroundCollider,
                new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20)),
                material);

            return RunScenario(
                CharacterTestGenesisBoundaryKind.Ground,
                world,
                CreateConfig(),
                GroundedStart,
                UnitX,
                ticks: 12,
                startFullySupported: true,
                expectedBoundaryMaterial: material);
        }

        public static CharacterTestGenesisBoundaryFormsSample RunWallSample(CombatSurfaceMaterial wallMaterial)
        {
            CombatPhysicsWorld world = CreateWorld(GroundedStart);
            RegisterDefaultGround(world);
            RegisterStaticAabb(
                world,
                entity: 61820,
                body: 61820,
                collider: BoundaryCollider,
                localMin: new FixVector3(Fix64.FromInt(2), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(3), Fix64.FromInt(4), Fix64.FromInt(2)),
                surfaceMaterial: wallMaterial);

            return RunScenario(
                CharacterTestGenesisBoundaryKind.Wall,
                world,
                CreateConfig(),
                GroundedStart,
                UnitX,
                ticks: 40,
                startFullySupported: true,
                expectedBoundaryMaterial: wallMaterial);
        }

        public static CharacterTestGenesisBoundaryFormsSample RunLowStepSample()
        {
            CombatPhysicsWorld world = CreateWorld(GroundedStart);
            RegisterDefaultGround(world);
            RegisterStaticAabb(
                world,
                entity: 61830,
                body: 61830,
                collider: BoundaryCollider,
                localMin: new FixVector3(Fix64.FromInt(1), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(4), Fix64.FromRatio(1, 4), Fix64.FromInt(2)),
                surfaceMaterial: CombatSurfaceMaterial.Default);

            return RunScenario(
                CharacterTestGenesisBoundaryKind.LowStep,
                world,
                CreateConfig(maxStepHeight: Fix64.FromRatio(35, 100)),
                GroundedStart,
                UnitX,
                ticks: 25,
                startFullySupported: true,
                expectedBoundaryMaterial: CombatSurfaceMaterial.Default);
        }

        public static CharacterTestGenesisBoundaryFormsSample RunHighStepBlockedSample()
        {
            CombatPhysicsWorld world = CreateWorld(GroundedStart);
            RegisterDefaultGround(world);
            RegisterStaticAabb(
                world,
                entity: 61831,
                body: 61831,
                collider: BoundaryCollider,
                localMin: new FixVector3(Fix64.FromInt(1), Fix64.Zero, -Fix64.FromInt(2)),
                localMax: new FixVector3(Fix64.FromInt(4), Fix64.FromRatio(1, 2), Fix64.FromInt(2)),
                surfaceMaterial: CombatSurfaceMaterial.Default);

            return RunScenario(
                CharacterTestGenesisBoundaryKind.LowStep,
                world,
                CreateConfig(maxStepHeight: Fix64.FromRatio(35, 100)),
                GroundedStart,
                UnitX,
                ticks: 25,
                startFullySupported: true,
                expectedBoundaryMaterial: CombatSurfaceMaterial.Default);
        }

        public static CharacterTestGenesisBoundaryFormsSample RunEdgeSample()
        {
            CombatPhysicsWorld world = CreateWorld(GroundedStart);
            FixVector3 groundMin = new FixVector3(-Fix64.FromInt(2), -Fix64.One, -Fix64.FromInt(2));
            FixVector3 groundMax = new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.FromInt(2));
            RegisterStaticAabb(
                world,
                GroundEntity,
                GroundBody,
                GroundCollider,
                groundMin,
                groundMax,
                CombatSurfaceMaterial.Default);

            bool startFullySupported = IsHorizontalProjectionFullyInside(GroundedStart, CharacterHalfExtents, groundMin, groundMax);
            return RunScenario(
                CharacterTestGenesisBoundaryKind.Edge,
                world,
                CreateConfig(),
                GroundedStart,
                UnitX,
                ticks: DefaultRunTicks,
                startFullySupported: startFullySupported,
                expectedBoundaryMaterial: CombatSurfaceMaterial.Default);
        }

        private static CharacterTestGenesisBoundaryFormsSample RunScenario(
            CharacterTestGenesisBoundaryKind boundaryKind,
            CombatPhysicsWorld world,
            CombatMotionConfig config,
            FixVector3 startPosition,
            FixVector3 moveDirection,
            int ticks,
            bool startFullySupported,
            CombatSurfaceMaterial expectedBoundaryMaterial)
        {
            var motor = new CombatKinematicMotor(config);
            CombatMotionState state = CreateState(0, startPosition, FixVector3.Zero, grounded: true);
            CombatMotionInput input = new CombatMotionInput(moveDirection, jumpPressed: false);
            CombatMotionCollisionFlags observedFlags = CombatMotionCollisionFlags.None;
            FixVector3 contactDisplacement = FixVector3.Zero;
            FixVector3 lastContactNormal = FixVector3.Zero;
            FixVector3 lastBoundaryNormal = FixVector3.Zero;
            CombatSurfaceMaterial boundaryMaterial = expectedBoundaryMaterial;
            FixVector3 lastAppliedDelta = FixVector3.Zero;
            int groundedFrames = 0;
            int airborneFrames = 0;
            int currentAirborneStreak = 0;
            int maxAirborneStreak = 0;
            int firstAirborneFrame = -1;
            Fix64 minY = startPosition.Y;
            Fix64 maxY = startPosition.Y;

            for (int i = 0; i < Math.Max(1, ticks); i++)
            {
                CombatMotionStepResult result = motor.Step(world, PlayerBodyId, state, input);
                state = result.State;
                observedFlags |= result.CollisionFlags;
                contactDisplacement += result.AppliedDelta;
                lastAppliedDelta = result.AppliedDelta;
                minY = Min(minY, state.Position.Y);
                maxY = Max(maxY, state.Position.Y);

                if (state.Grounded)
                {
                    groundedFrames++;
                    currentAirborneStreak = 0;
                }
                else
                {
                    airborneFrames++;
                    currentAirborneStreak++;
                    maxAirborneStreak = Math.Max(maxAirborneStreak, currentAirborneStreak);
                    if (firstAirborneFrame < 0)
                        firstAirborneFrame = i;
                }

                for (int collisionIndex = 0; collisionIndex < result.CollisionCount; collisionIndex++)
                {
                    CombatMotionCollision collision = result.Collisions[collisionIndex];
                    if (!collision.Normal.IsZero)
                        lastContactNormal = collision.Normal;

                    if (MatchesBoundary(boundaryKind, collision.Flags))
                    {
                        boundaryMaterial = collision.SurfaceMaterial;
                        lastBoundaryNormal = collision.Normal;
                    }
                }
            }

            return new CharacterTestGenesisBoundaryFormsSample(
                boundaryKind,
                boundaryMaterial.Kind,
                boundaryMaterial.StableId,
                Math.Max(1, ticks),
                DefaultTickRate,
                startFullySupported,
                startGrounded: true,
                state.Grounded,
                groundedFrames,
                airborneFrames,
                maxAirborneStreak,
                firstAirborneFrame,
                observedFlags,
                state.CollisionFlags,
                ToFloat(startPosition.X),
                ToFloat(startPosition.Y),
                ToFloat(startPosition.Z),
                ToFloat(state.Position.X),
                ToFloat(state.Position.Y),
                ToFloat(state.Position.Z),
                ToFloat(state.Velocity.X),
                ToFloat(state.Velocity.Y),
                ToFloat(state.Velocity.Z),
                ToFloat(contactDisplacement.X),
                ToFloat(contactDisplacement.Y),
                ToFloat(contactDisplacement.Z),
                ToFloat(lastAppliedDelta.X),
                ToFloat(lastAppliedDelta.Y),
                ToFloat(lastAppliedDelta.Z),
                ToFloat(lastContactNormal.X),
                ToFloat(lastContactNormal.Y),
                ToFloat(lastContactNormal.Z),
                ToFloat(lastBoundaryNormal.X),
                ToFloat(lastBoundaryNormal.Y),
                ToFloat(lastBoundaryNormal.Z),
                ToFloat(minY),
                ToFloat(maxY));
        }

        private static bool MatchesBoundary(
            CharacterTestGenesisBoundaryKind boundaryKind,
            CombatMotionCollisionFlags flags)
        {
            switch (boundaryKind)
            {
                case CharacterTestGenesisBoundaryKind.Ground:
                case CharacterTestGenesisBoundaryKind.Edge:
                    return (flags & CombatMotionCollisionFlags.Grounded) != 0
                        && (flags & CombatMotionCollisionFlags.StepUp) == 0;
                case CharacterTestGenesisBoundaryKind.Wall:
                    return (flags & CombatMotionCollisionFlags.Wall) != 0;
                case CharacterTestGenesisBoundaryKind.LowStep:
                    return (flags & CombatMotionCollisionFlags.StepUp) != 0
                        || (flags & CombatMotionCollisionFlags.Wall) != 0;
                default:
                    return false;
            }
        }

        private static CombatMotionConfig CreateConfig(Fix64 maxStepHeight = default)
        {
            return new CombatMotionConfig(
                CombatStepConfig.Default,
                CharacterHalfExtents,
                moveSpeed: Fix64.FromInt(6),
                gravityPerSecond: -Fix64.FromInt(30),
                jumpSpeed: Fix64.FromInt(12),
                maxFallSpeed: Fix64.FromInt(40),
                skinWidth: Fix64.FromRatio(1, 100),
                groundMinNormalY: Fix64.Half,
                ceilingMinNormalY: Fix64.Half,
                collisionLayerMask: BlockingMask.Value,
                maxSlideIterations: 3,
                maxStepHeight: maxStepHeight);
        }

        private static CombatMotionState CreateState(
            int frame,
            FixVector3 position,
            FixVector3 velocity,
            bool grounded)
        {
            return new CombatMotionState(
                new CombatFrame(frame),
                position,
                velocity,
                grounded,
                FixVector3.Zero,
                grounded ? CombatMotionCollisionFlags.Grounded : CombatMotionCollisionFlags.None);
        }

        private static CombatPhysicsWorld CreateWorld(FixVector3 startPosition)
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(PlayerEntityId, PlayerBodyId, startPosition));
            return world;
        }

        private static void RegisterDefaultGround(CombatPhysicsWorld world)
        {
            RegisterStaticAabb(
                world,
                GroundEntity,
                GroundBody,
                GroundCollider,
                new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20)),
                CombatSurfaceMaterial.Default);
        }

        private static void RegisterStaticAabb(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            FixVector3 localMin,
            FixVector3 localMax,
            CombatSurfaceMaterial surfaceMaterial)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(collider),
                BlockingLayer,
                localMin,
                localMax,
                surfaceMaterial));
        }

        private static bool IsHorizontalProjectionFullyInside(
            FixVector3 center,
            FixVector3 halfExtents,
            FixVector3 supportMin,
            FixVector3 supportMax)
        {
            return center.X - halfExtents.X >= supportMin.X
                && center.X + halfExtents.X <= supportMax.X
                && center.Z - halfExtents.Z >= supportMin.Z
                && center.Z + halfExtents.Z <= supportMax.Z;
        }

        private static Fix64 Min(Fix64 left, Fix64 right)
        {
            return left <= right ? left : right;
        }

        private static Fix64 Max(Fix64 left, Fix64 right)
        {
            return left >= right ? left : right;
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)((double)value.RawValue / Fix64.Scale);
        }
    }

}
