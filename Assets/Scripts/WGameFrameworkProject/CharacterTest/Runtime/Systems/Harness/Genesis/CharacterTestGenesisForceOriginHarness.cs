using System;
using MxFramework.CharacterAction;
using System.Collections.Generic;
using System.Globalization;
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
        internal const int DefaultForceOriginTicks = 24;

        private const float ForceOriginStartY = 1.6f;
        private const float ContinuousExternalAcceleration = 30f;
        private const float InstantImpulseSpeed = 6f;

        internal static CharacterTestGenesisForceOriginSample RunSelfWeightForceOriginSample(
            CombatSurfaceMaterial material)
        {
            return RunSelfWeightForceOriginSample(
                material,
                DefaultForceOriginTicks * 6,
                DefaultTickRate,
                ForceOriginStartY,
                DefaultMassKg);
        }

        internal static CharacterTestGenesisForceOriginSample RunSelfWeightForceOriginSample(
            CombatSurfaceMaterial material,
            int maxTicks,
            int tickRate,
            float startY,
            float massKg)
        {
            int safeMaxTicks = Math.Max(1, maxTicks);
            int safeTickRate = Math.Max(1, tickRate);
            float safeStartY = Math.Max(0f, startY);
            float safeMassKg = massKg > 0f ? massKg : DefaultMassKg;
            float deltaTime = 1f / safeTickRate;
            CharacterTestCharacterPhysicsComponent physics = CreatePhysicsProfile(safeMassKg);
            CharacterTestCharacterRuntimeWorld world = CreateWorld(
                physics,
                safeStartY,
                out CharacterTestCharacterEntity entity);
            var motion = CreateMotionSystem(material, physics);
            entity.SetMotionState(
                0f,
                Vector2.zero,
                isGrounded: false,
                horizontalVelocity: Vector2.zero,
                desiredVelocity: Vector2.zero,
                movementMode: CharacterMovementMode.Airborne,
                physicsEnabled: true);
            entity.SetInput(CharacterTestCharacterInputComponent.None);

            float minVerticalVelocity = entity.VerticalVelocity;
            float firstTickHorizontalSpeed = 0f;
            float peakHorizontalSpeed = 0f;
            int landingTick = -1;
            Vector3 start = ResolvePosition(entity);
            for (int i = 0; i < safeMaxTicks; i++)
            {
                motion.Tick(world, deltaTime);
                float speed = entity.HorizontalVelocity.magnitude;
                if (i == 0)
                    firstTickHorizontalSpeed = speed;
                peakHorizontalSpeed = Mathf.Max(peakHorizontalSpeed, speed);
                minVerticalVelocity = Mathf.Min(minVerticalVelocity, entity.VerticalVelocity);
                if (landingTick < 0 && entity.IsGrounded)
                    landingTick = i;
            }

            return CreateForceOriginSample(
                CharacterTestGenesisForceOriginKind.SelfWeight,
                "self_weight.gravity.support",
                material,
                safeMaxTicks,
                safeTickRate,
                safeMassKg,
                sourceTickCount: safeMaxTicks,
                start,
                entity,
                firstTickHorizontalSpeed,
                peakHorizontalSpeed,
                minVerticalVelocity,
                landingTick,
                externalForceTicks: 0,
                impulseApplied: false,
                controlInputApplied: false);
        }

        internal static CharacterTestGenesisForceOriginSample RunActiveForceOriginSample(
            CombatSurfaceMaterial material)
        {
            return RunActiveForceOriginSample(material, DefaultAccelerationTicks, DefaultTickRate);
        }

        internal static CharacterTestGenesisForceOriginSample RunActiveForceOriginSample(
            CombatSurfaceMaterial material,
            int inputTicks,
            int tickRate)
        {
            int safeInputTicks = Math.Max(1, inputTicks);
            int safeTickRate = Math.Max(1, tickRate);
            float deltaTime = 1f / safeTickRate;
            CharacterTestCharacterPhysicsComponent physics = CreatePhysicsProfile(DefaultMassKg);
            CharacterTestCharacterRuntimeWorld world = CreateWorld(
                physics,
                0f,
                out CharacterTestCharacterEntity entity);
            var motion = CreateMotionSystem(material, physics);
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

            Vector3 start = ResolvePosition(entity);
            float minVerticalVelocity = entity.VerticalVelocity;
            float firstTickHorizontalSpeed = 0f;
            float peakHorizontalSpeed = 0f;
            for (int i = 0; i < safeInputTicks; i++)
            {
                motion.Tick(world, deltaTime);
                float speed = entity.HorizontalVelocity.magnitude;
                if (i == 0)
                    firstTickHorizontalSpeed = speed;
                peakHorizontalSpeed = Mathf.Max(peakHorizontalSpeed, speed);
                minVerticalVelocity = Mathf.Min(minVerticalVelocity, entity.VerticalVelocity);
            }

            return CreateForceOriginSample(
                CharacterTestGenesisForceOriginKind.ActiveForce,
                "active_force.control_input",
                material,
                safeInputTicks,
                safeTickRate,
                DefaultMassKg,
                sourceTickCount: safeInputTicks,
                start,
                entity,
                firstTickHorizontalSpeed,
                peakHorizontalSpeed,
                minVerticalVelocity,
                landingTick: entity.IsGrounded ? 0 : -1,
                externalForceTicks: 0,
                impulseApplied: false,
                controlInputApplied: true);
        }

        internal static CharacterTestGenesisForceOriginSample RunContinuousExternalForceOriginSample(
            CombatSurfaceMaterial material)
        {
            return RunContinuousExternalForceOriginSample(
                material,
                DefaultForceOriginTicks,
                DefaultTickRate,
                ContinuousExternalAcceleration);
        }

        internal static CharacterTestGenesisForceOriginSample RunContinuousExternalForceOriginSample(
            CombatSurfaceMaterial material,
            int forceTicks,
            int tickRate,
            float accelerationX)
        {
            int safeForceTicks = Math.Max(1, forceTicks);
            int safeTickRate = Math.Max(1, tickRate);
            float safeAccelerationX = accelerationX;
            float deltaTime = 1f / safeTickRate;
            CharacterTestCharacterPhysicsComponent physics = CreatePhysicsProfile(DefaultMassKg);
            CharacterTestCharacterRuntimeWorld world = CreateWorld(
                physics,
                ForceOriginStartY,
                out CharacterTestCharacterEntity entity);
            var motion = CreateMotionSystem(material, physics);
            entity.SetMotionState(
                0f,
                Vector2.zero,
                isGrounded: false,
                horizontalVelocity: Vector2.zero,
                desiredVelocity: Vector2.zero,
                movementMode: CharacterMovementMode.Airborne,
                physicsEnabled: true);
            entity.SetInput(CharacterTestCharacterInputComponent.None);

            Vector3 start = ResolvePosition(entity);
            float minVerticalVelocity = entity.VerticalVelocity;
            float firstTickHorizontalSpeed = 0f;
            float peakHorizontalSpeed = 0f;
            int landingTick = -1;
            for (int i = 0; i < safeForceTicks; i++)
            {
                entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ExternalForce(
                    new Vector3(safeAccelerationX, 0f, 0f),
                    CharacterTestCharacterMotionRequestSpace.World,
                    CharacterActionId.None,
                    CharacterTestAuthoredContentIds.D401_Id136));
                motion.Tick(world, deltaTime);
                float speed = entity.HorizontalVelocity.magnitude;
                if (i == 0)
                    firstTickHorizontalSpeed = speed;
                peakHorizontalSpeed = Mathf.Max(peakHorizontalSpeed, speed);
                minVerticalVelocity = Mathf.Min(minVerticalVelocity, entity.VerticalVelocity);
                if (landingTick < 0 && entity.IsGrounded)
                    landingTick = i;
            }

            return CreateForceOriginSample(
                CharacterTestGenesisForceOriginKind.ContinuousExternalForce,
                "continuous_external_force.acceleration",
                material,
                safeForceTicks,
                safeTickRate,
                DefaultMassKg,
                sourceTickCount: safeForceTicks,
                start,
                entity,
                firstTickHorizontalSpeed,
                peakHorizontalSpeed,
                minVerticalVelocity,
                landingTick,
                externalForceTicks: safeForceTicks,
                impulseApplied: false,
                controlInputApplied: false);
        }

        internal static CharacterTestGenesisForceOriginSample RunInstantImpulseForceOriginSample(
            CombatSurfaceMaterial material)
        {
            return RunInstantImpulseForceOriginSample(
                material,
                DefaultForceOriginTicks,
                DefaultTickRate,
                InstantImpulseSpeed);
        }

        internal static CharacterTestGenesisForceOriginSample RunInstantImpulseForceOriginSample(
            CombatSurfaceMaterial material,
            int followTicks,
            int tickRate,
            float impulseSpeedX)
        {
            int safeFollowTicks = Math.Max(1, followTicks);
            int safeTickRate = Math.Max(1, tickRate);
            float safeImpulseSpeedX = Math.Max(0f, impulseSpeedX);
            float deltaTime = 1f / safeTickRate;
            CharacterTestCharacterPhysicsComponent physics = CreatePhysicsProfile(DefaultMassKg);
            CharacterTestCharacterRuntimeWorld world = CreateWorld(
                physics,
                0f,
                out CharacterTestCharacterEntity entity);
            var motion = CreateMotionSystem(material, physics);
            entity.SetMotionState(
                0f,
                Vector2.zero,
                isGrounded: true,
                horizontalVelocity: Vector2.zero,
                desiredVelocity: Vector2.zero,
                movementMode: CharacterMovementMode.Run,
                physicsEnabled: true,
                surfaceMaterial: material);
            entity.SetInput(CharacterTestCharacterInputComponent.None);
            entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.Impulse(
                new Vector3(safeImpulseSpeedX, 0f, 0f),
                CharacterMovementMode.Run,
                CharacterActionId.None,
                CharacterTestAuthoredContentIds.D401_Id137));

            Vector3 start = ResolvePosition(entity);
            float minVerticalVelocity = entity.VerticalVelocity;
            float firstTickHorizontalSpeed = 0f;
            float peakHorizontalSpeed = 0f;
            for (int i = 0; i < safeFollowTicks; i++)
            {
                motion.Tick(world, deltaTime);
                float speed = entity.HorizontalVelocity.magnitude;
                if (i == 0)
                    firstTickHorizontalSpeed = speed;
                peakHorizontalSpeed = Mathf.Max(peakHorizontalSpeed, speed);
                minVerticalVelocity = Mathf.Min(minVerticalVelocity, entity.VerticalVelocity);
            }

            return CreateForceOriginSample(
                CharacterTestGenesisForceOriginKind.InstantImpulse,
                "instant_impulse.velocity_change",
                material,
                safeFollowTicks,
                safeTickRate,
                DefaultMassKg,
                sourceTickCount: 1,
                start,
                entity,
                firstTickHorizontalSpeed,
                peakHorizontalSpeed,
                minVerticalVelocity,
                landingTick: entity.IsGrounded ? 0 : -1,
                externalForceTicks: 0,
                impulseApplied: true,
                controlInputApplied: false);
        }

        private static CharacterTestGenesisForceOriginSample CreateForceOriginSample(
            CharacterTestGenesisForceOriginKind originKind,
            string sourceId,
            CombatSurfaceMaterial material,
            int tickCount,
            int tickRate,
            float massKg,
            int sourceTickCount,
            Vector3 start,
            CharacterTestCharacterEntity entity,
            float firstTickHorizontalSpeed,
            float peakHorizontalSpeed,
            float minVerticalVelocity,
            int landingTick,
            int externalForceTicks,
            bool impulseApplied,
            bool controlInputApplied)
        {
            return new CharacterTestGenesisForceOriginSample(
                originKind,
                sourceId,
                material.Kind,
                material.StableId,
                ToFloat(material.Friction),
                ToFloat(material.Elasticity),
                tickCount,
                tickRate,
                massKg,
                sourceTickCount,
                externalForceTicks,
                impulseApplied,
                controlInputApplied,
                start.x,
                start.y,
                start.z,
                entity.Transform.X,
                entity.Transform.Y,
                entity.Transform.Z,
                firstTickHorizontalSpeed,
                peakHorizontalSpeed,
                entity.HorizontalVelocity.x,
                entity.HorizontalVelocity.y,
                entity.HorizontalVelocity.magnitude,
                entity.VerticalVelocity,
                minVerticalVelocity,
                entity.IsGrounded,
                landingTick,
                entity.SurfaceMaterialKind,
                entity.SurfaceFriction,
                entity.Physics.ContactCount);
        }
    }

}
