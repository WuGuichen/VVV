using System;
using MxFramework.CharacterAction;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Resources;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public static partial class CharacterTestGenesisSurfaceMaterialHarness
    {
        public const int DefaultTickRate = 60;
        public const int DefaultSlideTicks = 60;
        public const int DefaultAccelerationTicks = 20;
        public const int DefaultTurnTicks = 30;
        public const float DefaultKnockbackSpeed = 6f;
        public const float DefaultMassKg = CharacterTestCharacterPhysicsComponent.DefaultMassKg;

        private const int TestCharacterId = 61401;
        private const int TestTeamId = 1;
        private const int GroundLayer = 1;
        private const int GroundBodyId = 100;
        private const int GroundColliderId = 1;
        private const float BaseDeceleration = 0.1f;
        private const float RunSpeed = 4f;
        private const float Gravity = -30f;
        private const float ReboundStartY = 0.15f;
        private const float ReboundImpactSpeed = -20f;

        public static CharacterTestGenesisSurfaceMaterialSample RunSample(CombatSurfaceMaterial material)
        {
            return RunSample(material, DefaultSlideTicks, DefaultTickRate, DefaultKnockbackSpeed, DefaultMassKg);
        }

        public static CharacterTestGenesisSurfaceMaterialSample RunSample(
            CombatSurfaceMaterial material,
            int slideTicks,
            int tickRate,
            float knockbackSpeed,
            float massKg)
        {
            int safeSlideTicks = Math.Max(1, slideTicks);
            int safeTickRate = Math.Max(1, tickRate);
            float safeKnockbackSpeed = Math.Max(0f, knockbackSpeed);
            float safeMassKg = massKg > 0f ? massKg : DefaultMassKg;
            float deltaTime = 1f / safeTickRate;
            CharacterTestCharacterPhysicsComponent physics = CreatePhysicsProfile(safeMassKg);
            CharacterTestCharacterRuntimeWorld slideWorld = CreateWorld(physics, 0f, out CharacterTestCharacterEntity slideEntity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(CreatePhysicsRuntime(material, physics));
            slideEntity.SetInput(CharacterTestCharacterInputComponent.None);
            slideEntity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.Impulse(
                new Vector3(0f, 0f, safeKnockbackSpeed),
                CharacterMovementMode.Run,
                CharacterActionId.None,
                CharacterTestAuthoredContentIds.D401_Id138));

            Vector2 start = ResolveHorizontalPosition(slideEntity);
            Vector2 previous = start;
            Vector2 contactDisplacement = Vector2.zero;
            Vector2 contactVelocity = Vector2.zero;
            float peakContactSpeed = 0f;
            for (int i = 0; i < safeSlideTicks; i++)
            {
                motion.Tick(slideWorld, deltaTime);
                Vector2 current = ResolveHorizontalPosition(slideEntity);
                contactDisplacement += current - previous;
                previous = current;
                contactVelocity = slideEntity.HorizontalVelocity;
                peakContactSpeed = Mathf.Max(peakContactSpeed, contactVelocity.magnitude);
            }

            CharacterTestCharacterRuntimeWorld reboundWorld = CreateWorld(
                physics,
                ReboundStartY,
                out CharacterTestCharacterEntity reboundEntity);
            var reboundMotion = new CharacterTestCharacterMotionSystem();
            reboundMotion.SetMovementProfile(CreateMovementRuntime());
            reboundMotion.SetPhysicsRuntime(CreatePhysicsRuntime(material, physics));
            reboundEntity.SetMotionState(
                ReboundImpactSpeed,
                Vector2.zero,
                isGrounded: false,
                horizontalVelocity: Vector2.zero,
                desiredVelocity: Vector2.zero,
                movementMode: CharacterMovementMode.Airborne,
                physicsEnabled: true);
            reboundEntity.SetInput(CharacterTestCharacterInputComponent.None);
            reboundMotion.Tick(reboundWorld, deltaTime);

            return new CharacterTestGenesisSurfaceMaterialSample(
                material.Kind,
                material.StableId,
                ToFloat(material.Friction),
                ToFloat(material.Elasticity),
                safeSlideTicks,
                safeTickRate,
                safeMassKg,
                safeKnockbackSpeed,
                contactVelocity.x,
                contactVelocity.y,
                peakContactSpeed,
                contactDisplacement.x,
                contactDisplacement.y,
                contactDisplacement.magnitude,
                slideEntity.HorizontalVelocity.magnitude,
                slideEntity.SurfaceFriction,
                reboundEntity.VerticalVelocity,
                reboundEntity.Transform.Y - ReboundStartY,
                reboundEntity.IsGrounded);
        }

        public static CharacterTestGenesisSurfaceAccelerationSample RunAccelerationSample(
            CombatSurfaceMaterial material)
        {
            return RunAccelerationSample(material, DefaultAccelerationTicks, DefaultTickRate);
        }

        public static CharacterTestGenesisSurfaceAccelerationSample RunAccelerationSample(
            CombatSurfaceMaterial material,
            int inputTicks,
            int tickRate)
        {
            int safeInputTicks = Math.Max(1, inputTicks);
            int safeTickRate = Math.Max(1, tickRate);
            float deltaTime = 1f / safeTickRate;
            CharacterTestCharacterPhysicsComponent physics = CreatePhysicsProfile(DefaultMassKg);
            CharacterTestCharacterRuntimeWorld world = CreateWorld(physics, 0f, out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(CreatePhysicsRuntime(material, physics));
            entity.SetMotionState(
                0f,
                Vector2.zero,
                isGrounded: true,
                horizontalVelocity: Vector2.zero,
                desiredVelocity: Vector2.zero,
                movementMode: CharacterMovementMode.Run,
                physicsEnabled: true,
                surfaceMaterial: material);
            entity.SetInput(CreateWorldMoveInput(Vector2.up));

            for (int i = 0; i < safeInputTicks; i++)
                motion.Tick(world, deltaTime);

            return new CharacterTestGenesisSurfaceAccelerationSample(
                material.Kind,
                material.StableId,
                ToFloat(material.Friction),
                safeInputTicks,
                safeTickRate,
                entity.HorizontalVelocity.x,
                entity.HorizontalVelocity.y,
                entity.HorizontalVelocity.magnitude,
                entity.Transform.Z);
        }

        public static CharacterTestGenesisSurfaceTurnSample RunTurnSample(
            CombatSurfaceMaterial material)
        {
            return RunTurnSample(material, DefaultTurnTicks, DefaultTickRate, RunSpeed);
        }

        public static CharacterTestGenesisSurfaceTurnSample RunTurnSample(
            CombatSurfaceMaterial material,
            int turnTicks,
            int tickRate,
            float initialForwardSpeed)
        {
            int safeTurnTicks = Math.Max(1, turnTicks);
            int safeTickRate = Math.Max(1, tickRate);
            float safeInitialForwardSpeed = Math.Max(0f, initialForwardSpeed);
            float deltaTime = 1f / safeTickRate;
            CharacterTestCharacterPhysicsComponent physics = CreatePhysicsProfile(DefaultMassKg);
            CharacterTestCharacterRuntimeWorld world = CreateWorld(physics, 0f, out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(CreatePhysicsRuntime(material, physics));
            Vector2 initialVelocity = new Vector2(0f, safeInitialForwardSpeed);
            entity.SetMotionState(
                0f,
                Vector2.up,
                isGrounded: true,
                horizontalVelocity: initialVelocity,
                desiredVelocity: initialVelocity,
                movementMode: CharacterMovementMode.Run,
                physicsEnabled: true,
                surfaceMaterial: material);
            entity.SetInput(CreateWorldMoveInput(Vector2.down));

            for (int i = 0; i < safeTurnTicks; i++)
                motion.Tick(world, deltaTime);

            return new CharacterTestGenesisSurfaceTurnSample(
                material.Kind,
                material.StableId,
                ToFloat(material.Friction),
                safeTurnTicks,
                safeTickRate,
                safeInitialForwardSpeed,
                entity.HorizontalVelocity.x,
                entity.HorizontalVelocity.y,
                entity.HorizontalVelocity.magnitude);
        }

        public static IReadOnlyList<CharacterTestGenesisSurfaceBounceFrame> RunBounceSequence(
            CombatSurfaceMaterial material,
            int bounceCount,
            int maxTicks,
            int tickRate)
        {
            return RunBounceSequence(
                material,
                bounceCount,
                maxTicks,
                tickRate,
                jumpImpulseOnBounceIndex: -1,
                jumpReleaseAfterBounceIndex: -1);
        }

        public static IReadOnlyList<CharacterTestGenesisSurfaceBounceFrame> RunBounceSequence(
            CombatSurfaceMaterial material,
            int bounceCount,
            int maxTicks,
            int tickRate,
            int jumpImpulseOnBounceIndex)
        {
            return RunBounceSequence(
                material,
                bounceCount,
                maxTicks,
                tickRate,
                jumpImpulseOnBounceIndex,
                jumpReleaseAfterBounceIndex: -1);
        }

        public static IReadOnlyList<CharacterTestGenesisSurfaceBounceFrame> RunBounceSequence(
            CombatSurfaceMaterial material,
            int bounceCount,
            int maxTicks,
            int tickRate,
            int jumpImpulseOnBounceIndex,
            int jumpReleaseAfterBounceIndex)
        {
            int safeBounceCount = Math.Max(1, bounceCount);
            int safeMaxTicks = Math.Max(1, maxTicks);
            int safeTickRate = Math.Max(1, tickRate);
            float deltaTime = 1f / safeTickRate;
            CharacterTestCharacterPhysicsComponent physics = CreatePhysicsProfile(DefaultMassKg);
            CharacterTestCharacterRuntimeWorld world = CreateWorld(
                physics,
                ReboundStartY,
                out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(CreatePhysicsRuntime(material, physics));
            entity.SetMotionState(
                ReboundImpactSpeed,
                Vector2.zero,
                isGrounded: false,
                horizontalVelocity: Vector2.zero,
                desiredVelocity: Vector2.zero,
                movementMode: CharacterMovementMode.Airborne,
                physicsEnabled: true);
            entity.SetInput(CharacterTestCharacterInputComponent.None);

            var frames = new List<CharacterTestGenesisSurfaceBounceFrame>(safeBounceCount);
            float peakY = entity.Transform.Y;
            bool jumpImpulseQueued = false;
            bool jumpReleaseQueued = false;
            for (int i = 0; i < safeMaxTicks && frames.Count < safeBounceCount; i++)
            {
                float rawImpactSpeed = entity.VerticalVelocity < 0f
                    ? -entity.VerticalVelocity
                    : 0f;
                float capBefore = entity.SurfaceBounceSpeedCapY;
                float effectiveImpactSpeed = capBefore > 0f && rawImpactSpeed > capBefore
                    ? capBefore
                    : rawImpactSpeed;
                if (!jumpImpulseQueued
                    && frames.Count == jumpImpulseOnBounceIndex
                    && rawImpactSpeed > 0f
                    && entity.Transform.Y <= ReboundStartY + 0.05f)
                {
                    entity.SetAction(entity.Action.WithJumpImpulse(
                        CharacterTestCharacterMovementProfileSource.DefaultProfile.JumpImpulse));
                    jumpImpulseQueued = true;
                }

                motion.Tick(world, deltaTime);
                if (entity.Transform.Y > peakY)
                    peakY = entity.Transform.Y;
                if (jumpReleaseQueued)
                {
                    entity.SetInput(CharacterTestCharacterInputComponent.None);
                    jumpReleaseQueued = false;
                }

                if (entity.VerticalVelocity > 0f
                    && entity.SurfaceMaterialKind == material.Kind
                    && (entity.SurfaceElasticity > 0f || entity.SurfaceFixedBounceHeight > 0f))
                {
                    frames.Add(new CharacterTestGenesisSurfaceBounceFrame(
                        i,
                        rawImpactSpeed,
                        effectiveImpactSpeed,
                        entity.VerticalVelocity,
                        peakY,
                        entity.SurfaceBounceSpeedCapY));
                    if (jumpImpulseQueued)
                    {
                        entity.SetAction(entity.Action.WithJumpImpulse(0f));
                        jumpImpulseQueued = false;
                    }

                    if (frames.Count - 1 == jumpReleaseAfterBounceIndex)
                    {
                        entity.SetInput(new CharacterTestCharacterInputComponent(
                            Vector2.zero,
                            jumpPressed: false,
                            jumpHeld: false,
                            jumpReleased: true));
                        jumpReleaseQueued = true;
                    }

                    peakY = entity.Transform.Y;
                }
            }

            return frames;
        }

        private static CharacterTestCharacterRuntimeWorld CreateWorld(
            CharacterTestCharacterPhysicsComponent physics,
            float y,
            out CharacterTestCharacterEntity entity)
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            var definition = new CharacterTestCharacterDefinition(
                TestCharacterId,
                "Genesis Surface Probe",
                default(ResourceKey),
                TestTeamId,
                default(CharacterRuntimeAnimationBinding),
                physics);
            entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    TestCharacterId,
                    TestTeamId,
                    0f,
                    y,
                    0f,
                    CharacterTestCharacterControlOwner.Scripted),
                definition);
            entity.SetMotionState(0f, Vector2.zero, isGrounded: y <= 0f);
            return world;
        }

        private static CharacterTestCharacterInputComponent CreateWorldMoveInput(Vector2 move)
        {
            return new CharacterTestCharacterInputComponent(
                move,
                jumpPressed: false,
                worldMove: move,
                hasWorldMove: true);
        }

        private static CharacterTestCharacterMovementProfileRuntime CreateMovementRuntime()
        {
            return new CharacterTestCharacterMovementProfileRuntime(
                new CharacterMovementProfileConfig(
                    CharacterTestCharacterMovementProfileSource.DefaultProfileId,
                    CharacterMovementMode.Run,
                    walkSpeed: 2f,
                    runSpeed: RunSpeed,
                    acceleration: 100f,
                    deceleration: BaseDeceleration,
                    turnSpeed: 720f,
                    groundFriction: 0f,
                    airControl: 0.35f,
                    gravity: Gravity,
                    jumpImpulse: 8f,
                    slopeLimitDegrees: 45f,
                    locomotionBlendId: "locomotion.move"),
                maxFallSpeed: -50f,
                jumpBufferSeconds: 0.12f,
                coyoteTimeSeconds: 0.1f,
                jumpReleaseVelocityMultiplier: 0.45f,
                apexGravityMultiplier: 0.75f,
                fallGravityMultiplier: 1.35f,
                usedFallback: false,
                sourceDescription: "genesis surface material harness");
        }

        private static CharacterTestCharacterMotionSystem CreateMotionSystem(
            CombatSurfaceMaterial material,
            CharacterTestCharacterPhysicsComponent physics)
        {
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(CreatePhysicsRuntime(material, physics));
            return motion;
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePhysicsRuntime(
            CombatSurfaceMaterial material,
            CharacterTestCharacterPhysicsComponent physics)
        {
            var physicsWorld = new CombatPhysicsWorld();
            var groundBodyId = new CombatBodyId(GroundBodyId);
            physicsWorld.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(GroundBodyId),
                groundBodyId,
                FixVector3.Zero));
            physicsWorld.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                groundBodyId,
                new CombatColliderId(GroundColliderId),
                GroundLayer,
                new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20)),
                material));

            return new CharacterTestCharacterPhysicsRuntime(
                physicsWorld,
                CombatPhysicsLayerMask.FromLayer(GroundLayer),
                ToHalfExtents(physics),
                Fix64.FromRatio(1, 100),
                Fix64.Zero,
                staticColliderCount: 1,
                sourceDescription: "genesis surface material harness");
        }

        private static CharacterTestCharacterPhysicsComponent CreatePhysicsProfile(float massKg)
        {
            return new CharacterTestCharacterPhysicsComponent(
                CharacterTestCharacterPhysicsComponent.DefaultCapsuleRadiusMeters,
                CharacterTestCharacterPhysicsComponent.DefaultCapsuleHeightMeters,
                CharacterTestCharacterPhysicsComponent.DefaultCapsuleHeightMeters * 0.5f,
                massKg);
        }

        private static FixVector3 ToHalfExtents(CharacterTestCharacterPhysicsComponent physics)
        {
            Fix64 radius = ToFix64(physics.EffectiveRadiusMeters);
            return new FixVector3(
                radius,
                ToFix64(physics.EffectiveHalfHeightMeters),
                radius);
        }

        private static Vector2 ResolveHorizontalPosition(CharacterTestCharacterEntity entity)
        {
            return new Vector2(entity.Transform.X, entity.Transform.Z);
        }

        private static Vector3 ResolvePosition(CharacterTestCharacterEntity entity)
        {
            return new Vector3(entity.Transform.X, entity.Transform.Y, entity.Transform.Z);
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)((double)value.RawValue / Fix64.Scale);
        }
    }

}
