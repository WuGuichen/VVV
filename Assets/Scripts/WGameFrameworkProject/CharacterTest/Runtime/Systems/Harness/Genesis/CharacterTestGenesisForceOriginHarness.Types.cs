using System.Globalization;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestGenesisForceOriginKind
    {
        SelfWeight = 1,
        ActiveForce = 2,
        ContinuousExternalForce = 3,
        InstantImpulse = 4,
    }

    internal readonly struct CharacterTestGenesisForceOriginSample
    {
        public CharacterTestGenesisForceOriginSample(
            CharacterTestGenesisForceOriginKind originKind,
            string sourceId,
            CombatSurfaceMaterialKind materialKind,
            string materialStableId,
            float surfaceFriction,
            float surfaceElasticity,
            int tickCount,
            int tickRate,
            float massKg,
            int sourceTickCount,
            int externalForceTicks,
            bool impulseApplied,
            bool controlInputApplied,
            float initialX,
            float initialY,
            float initialZ,
            float finalX,
            float finalY,
            float finalZ,
            float firstTickHorizontalSpeed,
            float peakHorizontalSpeed,
            float finalVelocityX,
            float finalVelocityZ,
            float finalHorizontalSpeed,
            float finalVerticalVelocity,
            float minVerticalVelocity,
            bool finalGrounded,
            int landingTick,
            CombatSurfaceMaterialKind resolvedSurfaceKind,
            float resolvedSurfaceFriction,
            int contactCount)
        {
            OriginKind = originKind;
            SourceId = sourceId ?? string.Empty;
            MaterialKind = materialKind;
            MaterialStableId = materialStableId ?? string.Empty;
            SurfaceFriction = surfaceFriction < 0f ? 0f : surfaceFriction;
            SurfaceElasticity = Mathf.Clamp01(surfaceElasticity);
            TickCount = tickCount < 0 ? 0 : tickCount;
            TickRate = tickRate < 0 ? 0 : tickRate;
            MassKg = massKg < 0f ? 0f : massKg;
            SourceTickCount = sourceTickCount < 0 ? 0 : sourceTickCount;
            ExternalForceTicks = externalForceTicks < 0 ? 0 : externalForceTicks;
            ImpulseApplied = impulseApplied;
            ControlInputApplied = controlInputApplied;
            InitialX = initialX;
            InitialY = initialY;
            InitialZ = initialZ;
            FinalX = finalX;
            FinalY = finalY;
            FinalZ = finalZ;
            FirstTickHorizontalSpeed = firstTickHorizontalSpeed < 0f ? 0f : firstTickHorizontalSpeed;
            PeakHorizontalSpeed = peakHorizontalSpeed < 0f ? 0f : peakHorizontalSpeed;
            FinalVelocityX = finalVelocityX;
            FinalVelocityZ = finalVelocityZ;
            FinalHorizontalSpeed = finalHorizontalSpeed < 0f ? 0f : finalHorizontalSpeed;
            FinalVerticalVelocity = finalVerticalVelocity;
            MinVerticalVelocity = minVerticalVelocity;
            FinalGrounded = finalGrounded;
            LandingTick = landingTick < 0 ? -1 : landingTick;
            ResolvedSurfaceKind = resolvedSurfaceKind;
            ResolvedSurfaceFriction = resolvedSurfaceFriction < 0f ? 0f : resolvedSurfaceFriction;
            ContactCount = contactCount < 0 ? 0 : contactCount;
        }

        public CharacterTestGenesisForceOriginKind OriginKind { get; }
        public string SourceId { get; }
        public CombatSurfaceMaterialKind MaterialKind { get; }
        public string MaterialStableId { get; }
        public float SurfaceFriction { get; }
        public float SurfaceElasticity { get; }
        public int TickCount { get; }
        public int TickRate { get; }
        public float MassKg { get; }
        public int SourceTickCount { get; }
        public int ExternalForceTicks { get; }
        public bool ImpulseApplied { get; }
        public bool ControlInputApplied { get; }
        public float InitialX { get; }
        public float InitialY { get; }
        public float InitialZ { get; }
        public float FinalX { get; }
        public float FinalY { get; }
        public float FinalZ { get; }
        public float DisplacementX => FinalX - InitialX;
        public float DisplacementY => FinalY - InitialY;
        public float DisplacementZ => FinalZ - InitialZ;
        public float FirstTickHorizontalSpeed { get; }
        public float PeakHorizontalSpeed { get; }
        public float FinalVelocityX { get; }
        public float FinalVelocityZ { get; }
        public float FinalHorizontalSpeed { get; }
        public float FinalVerticalVelocity { get; }
        public float MinVerticalVelocity { get; }
        public bool FinalGrounded { get; }
        public int LandingTick { get; }
        public bool Landed => LandingTick >= 0;
        public CombatSurfaceMaterialKind ResolvedSurfaceKind { get; }
        public float ResolvedSurfaceFriction { get; }
        public int ContactCount { get; }

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "origin={0} source={1} material={2}/{3} ticks={4}@{5}Hz sourceTicks={6} externalTicks={7} impulse={8} controlInput={9} massKg={10:0.###} displacement=({11:0.###},{12:0.###},{13:0.###}) firstSpeed={14:0.###} peakSpeed={15:0.###} finalVelocity=({16:0.###},{17:0.###}) finalSpeed={18:0.###} finalVertical={19:0.###} minVertical={20:0.###} grounded={21} landingTick={22} resolvedSurface={23}/{24:0.###} contacts={25}",
                OriginKind,
                SourceId,
                MaterialKind,
                MaterialStableId,
                TickCount,
                TickRate,
                SourceTickCount,
                ExternalForceTicks,
                ImpulseApplied,
                ControlInputApplied,
                MassKg,
                DisplacementX,
                DisplacementY,
                DisplacementZ,
                FirstTickHorizontalSpeed,
                PeakHorizontalSpeed,
                FinalVelocityX,
                FinalVelocityZ,
                FinalHorizontalSpeed,
                FinalVerticalVelocity,
                MinVerticalVelocity,
                FinalGrounded,
                LandingTick,
                ResolvedSurfaceKind,
                ResolvedSurfaceFriction,
                ContactCount);
        }
    }
}
