using System;
using MxFramework.Audio.Physical;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestPhysicalAudioSystem
    {
        private const float MillimetersPerSecondToMetersPerSecond = 0.001f;
        private const float GramsToKilograms = 0.001f;
        private const float OrdinaryFootMassFraction = 0.5f;
        private const float CirclePi = 3.14159265358979323846f;
        private const float MinimumContactAreaSquareMeters = 0.0001f;

        private void AddBodyPartContactAudioEvents(
            CombatPhysicalContactEventBuffer contactEvents,
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterBodyPartContactRegistration registration,
            CharacterBodyPartContactAudioState contactAudioState,
            RuntimeFrame frame,
            int tickRate,
            ref PhysicalAudioFrameState state)
        {
            if (contactEvents == null || registration == null)
                return;

            int startedCount = 0;
            for (int i = 0; i < contactEvents.Count; i++)
            {
                CombatPhysicalContactEvent contactEvent = contactEvents[i];
                if (contactEvent.Kind == CombatPhysicalContactKind.BodyPartContact)
                    UpdateBodyPartContactDebugEntry(contactEvent, entity);

                if (contactEvent.Kind != CombatPhysicalContactKind.BodyPartContact
                    || contactEvent.Phase != CombatPhysicalContactPhase.Started
                    || !IsBodyPartContactAllowed(entity, CreateBodyPartContactHit(contactEvent)))
                {
                    continue;
                }

                if (!TryFindContactKinematics(
                        registration,
                        contactEvent.ContactPointId,
                        contactEvent.BodyPartId,
                        out CharacterBodyPartContactKinematicState kinematics,
                        out int bindingIndex)
                    || !kinematics.HasContinuousVelocity)
                {
                    state.ContactDynamicsBaselineSuppressCount++;
                    continue;
                }

                if (!TryResolveEstimatedFootDynamics(
                        contactEvent,
                        entity,
                        world,
                        physicsRuntime,
                        registration.Bindings[bindingIndex],
                        kinematics,
                        tickRate,
                        ref state,
                        out PhysicalAudioContactDynamics dynamics))
                {
                    state.SkippedCount++;
                    continue;
                }

                if (startedCount >= _startedBodyPartContactEventIndices.Length)
                {
                    state.SkippedCount++;
                    continue;
                }
                _startedBodyPartContactEventIndices[startedCount] = i;
                _startedBodyPartContactDynamics[startedCount] = dynamics;
                startedCount++;
            }

            if (startedCount == 0)
                return;

            PhysicalAudioContactDynamics totalLandingDynamics = default;
            bool solverLanding = contactAudioState.HasMotionState
                && contactAudioState.LastFrame + 1 == frame.Value
                && !contactAudioState.WasGrounded
                && entity.IsGrounded
                && TryResolveLandingDynamics(
                    contactEvents,
                    entity,
                    world,
                    physicsRuntime,
                    registration,
                    contactAudioState,
                    tickRate,
                    startedCount,
                    ref state,
                    out totalLandingDynamics);

            float weightSum = 0f;
            if (solverLanding)
            {
                for (int i = 0; i < startedCount; i++)
                    weightSum += _startedBodyPartContactDynamics[i].NormalSpeedMetersPerSecond;
            }

            CombatFrame combatFrame = new CombatFrame(ClampFrame(frame.Value));
            for (int i = 0; i < startedCount; i++)
            {
                CombatPhysicalContactEvent contactEvent =
                    contactEvents[_startedBodyPartContactEventIndices[i]];
                PhysicalAudioContactDynamics dynamics = _startedBodyPartContactDynamics[i];
                if (solverLanding)
                {
                    float share = weightSum > 0f
                        ? dynamics.NormalSpeedMetersPerSecond / weightSum
                        : 1f / startedCount;
                    dynamics = totalLandingDynamics.WithImpulseScale(share);
                    state.SolverVelocityDeltaCount++;
                }
                else
                {
                    state.ContactPointEstimatedCount++;
                }

                if (AddAudioContactEvent(contactEvent, combatFrame, dynamics))
                {
                    MarkBodyPartContactDebugAudioQueued(contactEvent);
                    UpdateBodyPartContactDebugDynamics(contactEvent, dynamics);
                }
            }
        }

        private static bool TryFindContactKinematics(
            CharacterBodyPartContactRegistration registration,
            int contactPointId,
            int bodyPartId,
            out CharacterBodyPartContactKinematicState kinematics,
            out int bindingIndex)
        {
            CharacterBodyPartContactRuntimeBinding[] bindings = registration.Bindings;
            for (int i = 0; i < bindings.Length; i++)
            {
                BodyPartContactDerivedDefinition derived = bindings[i].Derived;
                if (derived.ContactPointId == contactPointId
                    && derived.BodyPartId == bodyPartId)
                {
                    kinematics = registration.Kinematics[i];
                    bindingIndex = i;
                    return true;
                }
            }

            kinematics = default;
            bindingIndex = -1;
            return false;
        }

        private static bool TryResolveEstimatedFootDynamics(
            in CombatPhysicalContactEvent contactEvent,
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            in CharacterBodyPartContactRuntimeBinding binding,
            in CharacterBodyPartContactKinematicState kinematics,
            int tickRate,
            ref PhysicalAudioFrameState state,
            out PhysicalAudioContactDynamics dynamics)
        {
            CharacterTestContactTargetDynamics target = ResolveContactTargetDynamics(
                contactEvent.TargetBodyId,
                world,
                physicsRuntime,
                ref state);
            PhysicalAudioDirection normal = CreateDynamicsDirection(contactEvent.Normal);
            float sourceMassKilograms = Math.Max(
                CharacterTestCharacterPhysicsComponent.MinimumMassGrams,
                entity.Physics.EffectiveMassGrams)
                * GramsToKilograms
                * OrdinaryFootMassFraction;
            float contactArea = ResolveContactAreaSquareMeters(binding.Derived.Radius);
            var input = new PhysicalAudioContactDynamicsInput(
                PhysicalAudioContactDynamicsSource.ContactPointEstimated,
                kinematics.VelocityX - target.VelocityX,
                kinematics.VelocityY - target.VelocityY,
                kinematics.VelocityZ - target.VelocityZ,
                0f,
                0f,
                0f,
                normal,
                sourceMassKilograms,
                target.MassKilograms,
                target.IsStatic,
                ToFloat(contactEvent.SurfaceMaterial.Friction),
                ToFloat(contactEvent.SurfaceMaterial.Elasticity),
                contactArea,
                1f / tickRate);
            return PhysicalAudioContactDynamicsResolver.TryResolve(input, out dynamics);
        }

        private bool TryResolveLandingDynamics(
            CombatPhysicalContactEventBuffer contactEvents,
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterBodyPartContactRegistration registration,
            in CharacterBodyPartContactAudioState contactAudioState,
            int tickRate,
            int startedCount,
            ref PhysicalAudioFrameState state,
            out PhysicalAudioContactDynamics dynamics)
        {
            float normalX = 0f;
            float normalY = 0f;
            float normalZ = 0f;
            for (int i = 0; i < startedCount; i++)
            {
                FixVector3 normal = contactEvents[_startedBodyPartContactEventIndices[i]].Normal;
                normalX += ToFloat(normal.X);
                normalY += ToFloat(normal.Y);
                normalZ += ToFloat(normal.Z);
            }
            var aggregateNormal = new PhysicalAudioDirection(normalX, normalY, normalZ);
            if (aggregateNormal.IsZero)
            {
                dynamics = default;
                return false;
            }

            CombatPhysicalContactEvent firstContact =
                contactEvents[_startedBodyPartContactEventIndices[0]];
            CharacterTestContactTargetDynamics target = ResolveContactTargetDynamics(
                firstContact.TargetBodyId,
                world,
                physicsRuntime,
                ref state);
            if (!TryFindContactKinematics(
                    registration,
                    firstContact.ContactPointId,
                    firstContact.BodyPartId,
                    out _,
                    out int bindingIndex))
            {
                dynamics = default;
                return false;
            }

            float preVelocityX = contactAudioState.LastHorizontalVelocityXMillimetersPerSecond
                * MillimetersPerSecondToMetersPerSecond;
            float preVelocityY = contactAudioState.LastVerticalVelocityMillimetersPerSecond
                * MillimetersPerSecondToMetersPerSecond;
            float preVelocityZ = contactAudioState.LastHorizontalVelocityZMillimetersPerSecond
                * MillimetersPerSecondToMetersPerSecond;
            float postVelocityX = entity.HorizontalVelocityXMillimetersPerSecond
                * MillimetersPerSecondToMetersPerSecond;
            float postVelocityY = entity.VerticalVelocityMillimetersPerSecond
                * MillimetersPerSecondToMetersPerSecond;
            float postVelocityZ = entity.HorizontalVelocityYMillimetersPerSecond
                * MillimetersPerSecondToMetersPerSecond;
            float sourceMassKilograms = Math.Max(
                CharacterTestCharacterPhysicsComponent.MinimumMassGrams,
                entity.Physics.EffectiveMassGrams)
                * GramsToKilograms;
            var input = new PhysicalAudioContactDynamicsInput(
                PhysicalAudioContactDynamicsSource.SolverVelocityDelta,
                preVelocityX - target.VelocityX,
                preVelocityY - target.VelocityY,
                preVelocityZ - target.VelocityZ,
                postVelocityX - target.VelocityX,
                postVelocityY - target.VelocityY,
                postVelocityZ - target.VelocityZ,
                aggregateNormal,
                sourceMassKilograms,
                target.MassKilograms,
                target.IsStatic,
                ToFloat(firstContact.SurfaceMaterial.Friction),
                ToFloat(firstContact.SurfaceMaterial.Elasticity),
                ResolveContactAreaSquareMeters(registration.Bindings[bindingIndex].Derived.Radius),
                1f / tickRate);
            return PhysicalAudioContactDynamicsResolver.TryResolve(input, out dynamics)
                && dynamics.NormalImpulseNewtonSeconds > 0f;
        }

        private static CharacterTestContactTargetDynamics ResolveContactTargetDynamics(
            CombatBodyId targetBodyId,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            ref PhysicalAudioFrameState state)
        {
            if (physicsRuntime != null
                && physicsRuntime.TryResolveMovableObjectNumericId(targetBodyId, out int movableObjectId)
                && world != null
                && world.TryFindMovableNaturalObject(
                    movableObjectId,
                    out CharacterTestMovableNaturalObjectState movableObject))
            {
                return new CharacterTestContactTargetDynamics(
                    movableObject.VelocityXMillimetersPerSecond * MillimetersPerSecondToMetersPerSecond,
                    movableObject.VelocityYMillimetersPerSecond * MillimetersPerSecondToMetersPerSecond,
                    movableObject.VelocityZMillimetersPerSecond * MillimetersPerSecondToMetersPerSecond,
                    Math.Max(1, movableObject.MassGrams) * GramsToKilograms,
                    false);
            }

            if (targetBodyId.Value >= CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart
                && targetBodyId.Value < CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectBodyIdStart)
            {
                int entityId = targetBodyId.Value - CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart;
                if (world?.Entities != null)
                {
                    for (int i = 0; i < world.Entities.Count; i++)
                    {
                        CharacterTestCharacterEntity target = world.Entities[i];
                        if (target == null || target.Id.Value != entityId)
                            continue;
                        return new CharacterTestContactTargetDynamics(
                            target.HorizontalVelocityXMillimetersPerSecond
                                * MillimetersPerSecondToMetersPerSecond,
                            target.VerticalVelocityMillimetersPerSecond
                                * MillimetersPerSecondToMetersPerSecond,
                            target.HorizontalVelocityYMillimetersPerSecond
                                * MillimetersPerSecondToMetersPerSecond,
                            Math.Max(1, target.Physics.EffectiveMassGrams) * GramsToKilograms,
                            false);
                    }
                }

                state.DynamicTargetFallbackCount++;
            }
            else if (targetBodyId.Value >= CharacterTestCharacterPhysicsRuntime.DynamicMovableObjectBodyIdStart)
            {
                state.DynamicTargetFallbackCount++;
            }

            return CharacterTestContactTargetDynamics.Static;
        }

        private static float ResolveContactAreaSquareMeters(Fix64 radius)
        {
            float radiusMeters = ToFloat(radius);
            float area = CirclePi * radiusMeters * radiusMeters;
            return area >= MinimumContactAreaSquareMeters
                ? area
                : MinimumContactAreaSquareMeters;
        }

        private static PhysicalAudioDirection CreateDynamicsDirection(FixVector3 value)
        {
            return new PhysicalAudioDirection(
                ToFloat(value.X),
                ToFloat(value.Y),
                ToFloat(value.Z));
        }

        private static float ToFloat(Fix64 value)
        {
            return value.RawValue / (float)Fix64.Scale;
        }

        private readonly struct CharacterTestContactTargetDynamics
        {
            public static readonly CharacterTestContactTargetDynamics Static =
                new CharacterTestContactTargetDynamics(0f, 0f, 0f, 0f, true);

            public CharacterTestContactTargetDynamics(
                float velocityX,
                float velocityY,
                float velocityZ,
                float massKilograms,
                bool isStatic)
            {
                VelocityX = velocityX;
                VelocityY = velocityY;
                VelocityZ = velocityZ;
                MassKilograms = massKilograms;
                IsStatic = isStatic;
            }

            public float VelocityX { get; }

            public float VelocityY { get; }

            public float VelocityZ { get; }

            public float MassKilograms { get; }

            public bool IsStatic { get; }
        }
    }
}
