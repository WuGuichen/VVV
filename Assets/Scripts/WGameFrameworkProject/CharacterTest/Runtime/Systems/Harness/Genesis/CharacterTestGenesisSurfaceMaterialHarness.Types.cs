using System.Globalization;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestGenesisSurfaceMaterialSample
    {
        public CharacterTestGenesisSurfaceMaterialSample(
            CombatSurfaceMaterialKind materialKind,
            string materialStableId,
            float surfaceFriction,
            float surfaceElasticity,
            int tickCount,
            int tickRate,
            float massKg,
            float knockbackSpeed,
            float contactVelocityX,
            float contactVelocityZ,
            float peakContactSpeed,
            float contactDisplacementX,
            float contactDisplacementZ,
            float contactDisplacementMeters,
            float finalHorizontalSpeed,
            float resolvedSurfaceFriction,
            float reboundVelocityY,
            float reboundDisplacementY,
            bool reboundGrounded)
        {
            MaterialKind = materialKind;
            MaterialStableId = materialStableId ?? string.Empty;
            SurfaceFriction = surfaceFriction < 0f ? 0f : surfaceFriction;
            SurfaceElasticity = Mathf.Clamp01(surfaceElasticity);
            TickCount = tickCount < 0 ? 0 : tickCount;
            TickRate = tickRate < 0 ? 0 : tickRate;
            MassKg = massKg < 0f ? 0f : massKg;
            KnockbackSpeed = knockbackSpeed < 0f ? 0f : knockbackSpeed;
            ContactVelocityX = contactVelocityX;
            ContactVelocityZ = contactVelocityZ;
            PeakContactSpeed = peakContactSpeed < 0f ? 0f : peakContactSpeed;
            ContactDisplacementX = contactDisplacementX;
            ContactDisplacementZ = contactDisplacementZ;
            ContactDisplacementMeters = contactDisplacementMeters < 0f ? 0f : contactDisplacementMeters;
            FinalHorizontalSpeed = finalHorizontalSpeed < 0f ? 0f : finalHorizontalSpeed;
            ResolvedSurfaceFriction = resolvedSurfaceFriction < 0f ? 0f : resolvedSurfaceFriction;
            ReboundVelocityY = reboundVelocityY;
            ReboundDisplacementY = reboundDisplacementY;
            ReboundGrounded = reboundGrounded;
        }

        public CombatSurfaceMaterialKind MaterialKind { get; }
        public string MaterialStableId { get; }
        public float SurfaceFriction { get; }
        public float SurfaceElasticity { get; }
        public int TickCount { get; }
        public int TickRate { get; }
        public float MassKg { get; }
        public float KnockbackSpeed { get; }
        public float ContactVelocityX { get; }
        public float ContactVelocityZ { get; }
        public float PeakContactSpeed { get; }
        public float ContactDisplacementX { get; }
        public float ContactDisplacementZ { get; }
        public float ContactDisplacementMeters { get; }
        public float FinalHorizontalSpeed { get; }
        public float ResolvedSurfaceFriction { get; }
        public float ReboundVelocityY { get; }
        public float ReboundDisplacementY { get; }
        public bool ReboundGrounded { get; }

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "material={0}/{1} friction={2:0.###} elasticity={3:0.###} ticks={4}@{5}Hz massKg={6:0.###} knockbackSpeed={7:0.###} contactVelocity=({8:0.###},{9:0.###}) peakContactSpeed={10:0.###} contactDisplacement=({11:0.###},{12:0.###}) distance={13:0.###} finalSpeed={14:0.###} reboundVelocityY={15:0.###}",
                MaterialKind,
                MaterialStableId,
                SurfaceFriction,
                SurfaceElasticity,
                TickCount,
                TickRate,
                MassKg,
                KnockbackSpeed,
                ContactVelocityX,
                ContactVelocityZ,
                PeakContactSpeed,
                ContactDisplacementX,
                ContactDisplacementZ,
                ContactDisplacementMeters,
                FinalHorizontalSpeed,
                ReboundVelocityY);
        }
    }

    public readonly struct CharacterTestGenesisSurfaceBounceFrame
    {
        public CharacterTestGenesisSurfaceBounceFrame(
            int tick,
            float rawImpactSpeed,
            float effectiveImpactSpeed,
            float reboundSpeed,
            float peakY,
            float speedCapY)
        {
            Tick = tick < 0 ? 0 : tick;
            RawImpactSpeed = rawImpactSpeed < 0f ? 0f : rawImpactSpeed;
            EffectiveImpactSpeed = effectiveImpactSpeed < 0f ? 0f : effectiveImpactSpeed;
            ReboundSpeed = reboundSpeed < 0f ? 0f : reboundSpeed;
            PeakY = peakY;
            SpeedCapY = speedCapY < 0f ? 0f : speedCapY;
        }

        public int Tick { get; }
        public float RawImpactSpeed { get; }
        public float EffectiveImpactSpeed { get; }
        public float ReboundSpeed { get; }
        public float PeakY { get; }
        public float SpeedCapY { get; }

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "tick={0} rawImpact={1:0.###} effectiveImpact={2:0.###} rebound={3:0.###} peakY={4:0.###} capY={5:0.###}",
                Tick,
                RawImpactSpeed,
                EffectiveImpactSpeed,
                ReboundSpeed,
                PeakY,
                SpeedCapY);
        }
    }

    public readonly struct CharacterTestGenesisSurfaceAccelerationSample
    {
        public CharacterTestGenesisSurfaceAccelerationSample(
            CombatSurfaceMaterialKind materialKind,
            string materialStableId,
            float surfaceFriction,
            int inputTicks,
            int tickRate,
            float velocityX,
            float velocityZ,
            float speed,
            float displacementZ)
        {
            MaterialKind = materialKind;
            MaterialStableId = materialStableId ?? string.Empty;
            SurfaceFriction = surfaceFriction < 0f ? 0f : surfaceFriction;
            InputTicks = inputTicks < 0 ? 0 : inputTicks;
            TickRate = tickRate < 0 ? 0 : tickRate;
            VelocityX = velocityX;
            VelocityZ = velocityZ;
            Speed = speed < 0f ? 0f : speed;
            DisplacementZ = displacementZ;
        }

        public CombatSurfaceMaterialKind MaterialKind { get; }
        public string MaterialStableId { get; }
        public float SurfaceFriction { get; }
        public int InputTicks { get; }
        public int TickRate { get; }
        public float VelocityX { get; }
        public float VelocityZ { get; }
        public float Speed { get; }
        public float DisplacementZ { get; }

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "material={0}/{1} friction={2:0.###} accelerationTicks={3}@{4}Hz velocity=({5:0.###},{6:0.###}) speed={7:0.###} displacementZ={8:0.###}",
                MaterialKind,
                MaterialStableId,
                SurfaceFriction,
                InputTicks,
                TickRate,
                VelocityX,
                VelocityZ,
                Speed,
                DisplacementZ);
        }
    }

    public readonly struct CharacterTestGenesisSurfaceTurnSample
    {
        public CharacterTestGenesisSurfaceTurnSample(
            CombatSurfaceMaterialKind materialKind,
            string materialStableId,
            float surfaceFriction,
            int turnTicks,
            int tickRate,
            float initialForwardSpeed,
            float velocityX,
            float velocityZ,
            float speed)
        {
            MaterialKind = materialKind;
            MaterialStableId = materialStableId ?? string.Empty;
            SurfaceFriction = surfaceFriction < 0f ? 0f : surfaceFriction;
            TurnTicks = turnTicks < 0 ? 0 : turnTicks;
            TickRate = tickRate < 0 ? 0 : tickRate;
            InitialForwardSpeed = initialForwardSpeed < 0f ? 0f : initialForwardSpeed;
            VelocityX = velocityX;
            VelocityZ = velocityZ;
            Speed = speed < 0f ? 0f : speed;
        }

        public CombatSurfaceMaterialKind MaterialKind { get; }
        public string MaterialStableId { get; }
        public float SurfaceFriction { get; }
        public int TurnTicks { get; }
        public int TickRate { get; }
        public float InitialForwardSpeed { get; }
        public float VelocityX { get; }
        public float VelocityZ { get; }
        public float Speed { get; }
        public float ReverseSpeed => VelocityZ < 0f ? -VelocityZ : 0f;

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "material={0}/{1} friction={2:0.###} turnTicks={3}@{4}Hz initialForwardSpeed={5:0.###} velocity=({6:0.###},{7:0.###}) speed={8:0.###} reverseSpeed={9:0.###}",
                MaterialKind,
                MaterialStableId,
                SurfaceFriction,
                TurnTicks,
                TickRate,
                InitialForwardSpeed,
                VelocityX,
                VelocityZ,
                Speed,
                ReverseSpeed);
        }
    }
}
