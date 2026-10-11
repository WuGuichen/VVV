using System;
using System.Globalization;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestGenesisMovableNaturalObjectValidation
    {
        public static readonly CharacterTestGenesisMovableNaturalObjectValidation Empty =
            new CharacterTestGenesisMovableNaturalObjectValidation(
                CharacterTestGenesisMovableObjectProbeKind.None,
                CharacterTestGenesisForceOriginKind.InstantImpulse,
                default,
                "harness-only",
                CharacterTestGenesisMovableObjectSample.Empty,
                CharacterTestGenesisMovableObjectSample.Empty,
                CharacterTestGenesisMovableObjectSample.Empty,
                CharacterTestGenesisMovableObjectSample.Empty,
                CharacterTestGenesisMovableObjectSample.Empty,
                CharacterTestGenesisMovableObjectSample.Empty,
                CharacterTestGenesisMovableObjectSample.Empty,
                CharacterTestGenesisMovableObjectSample.Empty);

        public CharacterTestGenesisMovableNaturalObjectValidation(
            CharacterTestGenesisMovableObjectProbeKind scenario,
            CharacterTestGenesisForceOriginKind forceOrigin,
            RuntimeFrame frame,
            string stateModel,
            CharacterTestGenesisMovableObjectSample pushHardSlick,
            CharacterTestGenesisMovableObjectSample pushRough,
            CharacterTestGenesisMovableObjectSample pressureLight,
            CharacterTestGenesisMovableObjectSample pressureHeavy,
            CharacterTestGenesisMovableObjectSample reboundElastic,
            CharacterTestGenesisMovableObjectSample reboundRough,
            CharacterTestGenesisMovableObjectSample contactA,
            CharacterTestGenesisMovableObjectSample contactB)
        {
            Scenario = scenario;
            ForceOrigin = forceOrigin;
            Frame = frame;
            StateModel = string.IsNullOrWhiteSpace(stateModel) ? "harness-only" : stateModel;
            PushHardSlick = pushHardSlick;
            PushRough = pushRough;
            PressureLight = pressureLight;
            PressureHeavy = pressureHeavy;
            ReboundElastic = reboundElastic;
            ReboundRough = reboundRough;
            ContactA = contactA;
            ContactB = contactB;
        }

        public CharacterTestGenesisMovableObjectProbeKind Scenario { get; }
        public CharacterTestGenesisForceOriginKind ForceOrigin { get; }
        public RuntimeFrame Frame { get; }
        public string StateModel { get; }
        public CharacterTestGenesisMovableObjectSample PushHardSlick { get; }
        public CharacterTestGenesisMovableObjectSample PushRough { get; }
        public CharacterTestGenesisMovableObjectSample PressureLight { get; }
        public CharacterTestGenesisMovableObjectSample PressureHeavy { get; }
        public CharacterTestGenesisMovableObjectSample ReboundElastic { get; }
        public CharacterTestGenesisMovableObjectSample ReboundRough { get; }
        public CharacterTestGenesisMovableObjectSample ContactA { get; }
        public CharacterTestGenesisMovableObjectSample ContactB { get; }
        public bool IsHarnessOnly => string.Equals(StateModel, "harness-only", StringComparison.Ordinal);

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "stateModel={0} scenario={1} forceOrigin={2} frame={3} pushHardSlick=[{4}] pushRough=[{5}] pressureLight=[{6}] pressureHeavy=[{7}] reboundElastic=[{8}] reboundRough=[{9}] objectContactA=[{10}] objectContactB=[{11}]",
                StateModel,
                Scenario,
                ForceOrigin,
                Frame.Value,
                PushHardSlick.ToSummaryLine(),
                PushRough.ToSummaryLine(),
                PressureLight.ToSummaryLine(),
                PressureHeavy.ToSummaryLine(),
                ReboundElastic.ToSummaryLine(),
                ReboundRough.ToSummaryLine(),
                ContactA.ToSummaryLine(),
                ContactB.ToSummaryLine());
        }
    }

    internal readonly struct CharacterTestGenesisMovableObjectSample
    {
        public static readonly CharacterTestGenesisMovableObjectSample Empty =
            new CharacterTestGenesisMovableObjectSample(
                string.Empty,
                0,
                string.Empty,
                0f,
                CombatSurfaceMaterialKind.Default,
                "default",
                0f,
                0f,
                0,
                0,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                "none",
                string.Empty,
                "harness-only",
                CharacterTestGenesisForceOriginKind.InstantImpulse,
                0,
                0,
                0);

        public CharacterTestGenesisMovableObjectSample(
            string objectStableId,
            int objectStableNumericId,
            string category,
            float massKg,
            CombatSurfaceMaterialKind materialKind,
            string materialStableId,
            float surfaceFriction,
            float surfaceElasticity,
            int tickCount,
            int tickRate,
            float positionX,
            float positionZ,
            float displacementX,
            float displacementZ,
            float velocityX,
            float velocityZ,
            float contactVelocityX,
            float contactVelocityZ,
            float contactDisplacementX,
            float contactDisplacementZ,
            float contactPressure,
            string behaviorState,
            string contactPair,
            string stateModel,
            CharacterTestGenesisForceOriginKind forceOrigin,
            int physicsBodyCount,
            int physicsColliderCount,
            int physicsQueryHitCount)
        {
            ObjectStableId = objectStableId ?? string.Empty;
            ObjectStableNumericId = objectStableNumericId;
            Category = category ?? string.Empty;
            MassKg = massKg < 0f ? 0f : massKg;
            MaterialKind = materialKind;
            MaterialStableId = materialStableId ?? string.Empty;
            SurfaceFriction = surfaceFriction < 0f ? 0f : surfaceFriction;
            SurfaceElasticity = Mathf.Clamp01(surfaceElasticity);
            TickCount = tickCount < 0 ? 0 : tickCount;
            TickRate = tickRate < 0 ? 0 : tickRate;
            PositionX = positionX;
            PositionZ = positionZ;
            DisplacementX = displacementX;
            DisplacementZ = displacementZ;
            VelocityX = velocityX;
            VelocityZ = velocityZ;
            ContactVelocityX = contactVelocityX;
            ContactVelocityZ = contactVelocityZ;
            ContactDisplacementX = contactDisplacementX;
            ContactDisplacementZ = contactDisplacementZ;
            ContactPressure = contactPressure < 0f ? 0f : contactPressure;
            BehaviorState = string.IsNullOrWhiteSpace(behaviorState) ? "sampled" : behaviorState;
            ContactPair = contactPair ?? string.Empty;
            StateModel = string.IsNullOrWhiteSpace(stateModel) ? "harness-only" : stateModel;
            ForceOrigin = forceOrigin;
            PhysicsBodyCount = physicsBodyCount < 0 ? 0 : physicsBodyCount;
            PhysicsColliderCount = physicsColliderCount < 0 ? 0 : physicsColliderCount;
            PhysicsQueryHitCount = physicsQueryHitCount < 0 ? 0 : physicsQueryHitCount;
        }

        public string ObjectStableId { get; }
        public int ObjectStableNumericId { get; }
        public string Category { get; }
        public float MassKg { get; }
        public CombatSurfaceMaterialKind MaterialKind { get; }
        public string MaterialStableId { get; }
        public float SurfaceFriction { get; }
        public float SurfaceElasticity { get; }
        public int TickCount { get; }
        public int TickRate { get; }
        public float PositionX { get; }
        public float PositionZ { get; }
        public float DisplacementX { get; }
        public float DisplacementZ { get; }
        public float VelocityX { get; }
        public float VelocityZ { get; }
        public float ContactVelocityX { get; }
        public float ContactVelocityZ { get; }
        public float ContactDisplacementX { get; }
        public float ContactDisplacementZ { get; }
        public float ContactPressure { get; }
        public string BehaviorState { get; }
        public string ContactPair { get; }
        public string StateModel { get; }
        public CharacterTestGenesisForceOriginKind ForceOrigin { get; }
        public int PhysicsBodyCount { get; }
        public int PhysicsColliderCount { get; }
        public int PhysicsQueryHitCount { get; }
        public float DisplacementMeters => Mathf.Sqrt(DisplacementX * DisplacementX + DisplacementZ * DisplacementZ);
        public float ContactDisplacementMeters =>
            Mathf.Sqrt(ContactDisplacementX * ContactDisplacementX + ContactDisplacementZ * ContactDisplacementZ);
        public float ContactSpeed =>
            Mathf.Sqrt(ContactVelocityX * ContactVelocityX + ContactVelocityZ * ContactVelocityZ);

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "objectStableId={0} numericId={1} category={2} massKg={3:0.###} material={4}/{5} position=({6:0.###},{7:0.###}) displacement=({8:0.###},{9:0.###}) velocity=({10:0.###},{11:0.###}) contactVelocity=({12:0.###},{13:0.###}) contactDisplacement=({14:0.###},{15:0.###}) contactPressure={16:0.###} behavior={17} contactPair={18} stateModel={19} forceOrigin={20} aabbBodies={21} aabbColliders={22} aabbQueryHits={23}",
                ObjectStableId,
                ObjectStableNumericId,
                Category,
                MassKg,
                MaterialKind,
                MaterialStableId,
                PositionX,
                PositionZ,
                DisplacementX,
                DisplacementZ,
                VelocityX,
                VelocityZ,
                ContactVelocityX,
                ContactVelocityZ,
                ContactDisplacementX,
                ContactDisplacementZ,
                ContactPressure,
                BehaviorState,
                ContactPair,
                StateModel,
                ForceOrigin,
                PhysicsBodyCount,
                PhysicsColliderCount,
                PhysicsQueryHitCount);
        }
    }
}
