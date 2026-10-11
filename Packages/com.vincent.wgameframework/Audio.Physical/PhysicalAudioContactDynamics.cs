using System;

namespace MxFramework.Audio.Physical
{
    /// <summary>Identifies how physical contact dynamics were derived for presentation audio.</summary>
    public enum PhysicalAudioContactDynamicsSource
    {
        Unknown = 0,
        ContactPointEstimated = 1,
        SolverVelocityDelta = 2
    }

    /// <summary>SI-unit input used to derive one presentation-only physical-audio contact.</summary>
    /// <remarks>NoAlloc. Velocities are meters per second, masses kilograms, area square meters, and time seconds.</remarks>
    public readonly struct PhysicalAudioContactDynamicsInput
    {
        public PhysicalAudioContactDynamicsInput(
            PhysicalAudioContactDynamicsSource source,
            float preRelativeVelocityX,
            float preRelativeVelocityY,
            float preRelativeVelocityZ,
            float postRelativeVelocityX,
            float postRelativeVelocityY,
            float postRelativeVelocityZ,
            in PhysicalAudioDirection contactNormal,
            float sourceMassKilograms,
            float receiverMassKilograms,
            bool receiverIsStatic,
            float friction,
            float elasticity,
            float contactAreaSquareMeters,
            float tickDurationSeconds)
        {
            Source = source;
            PreRelativeVelocityX = preRelativeVelocityX;
            PreRelativeVelocityY = preRelativeVelocityY;
            PreRelativeVelocityZ = preRelativeVelocityZ;
            PostRelativeVelocityX = postRelativeVelocityX;
            PostRelativeVelocityY = postRelativeVelocityY;
            PostRelativeVelocityZ = postRelativeVelocityZ;
            ContactNormal = contactNormal;
            SourceMassKilograms = sourceMassKilograms;
            ReceiverMassKilograms = receiverMassKilograms;
            ReceiverIsStatic = receiverIsStatic;
            Friction = friction;
            Elasticity = elasticity;
            ContactAreaSquareMeters = contactAreaSquareMeters;
            TickDurationSeconds = tickDurationSeconds;
        }

        public PhysicalAudioContactDynamicsSource Source { get; }

        public float PreRelativeVelocityX { get; }

        public float PreRelativeVelocityY { get; }

        public float PreRelativeVelocityZ { get; }

        public float PostRelativeVelocityX { get; }

        public float PostRelativeVelocityY { get; }

        public float PostRelativeVelocityZ { get; }

        public PhysicalAudioDirection ContactNormal { get; }

        public float SourceMassKilograms { get; }

        public float ReceiverMassKilograms { get; }

        public bool ReceiverIsStatic { get; }

        public float Friction { get; }

        public float Elasticity { get; }

        public float ContactAreaSquareMeters { get; }

        public float TickDurationSeconds { get; }
    }

    /// <summary>Resolved SI-unit contact dynamics used by presentation audio.</summary>
    /// <remarks>NoAlloc. This value is not gameplay, replay, save-state, network, or deterministic-hash authority.</remarks>
    public readonly struct PhysicalAudioContactDynamics
    {
        internal PhysicalAudioContactDynamics(
            PhysicalAudioContactDynamicsSource source,
            float relativeSpeedMetersPerSecond,
            float normalSpeedMetersPerSecond,
            float tangentialSpeedMetersPerSecond,
            float effectiveMassKilograms,
            float normalImpulseNewtonSeconds,
            float tangentialImpulseNewtonSeconds,
            float averagePressurePascals,
            float angleDegrees)
        {
            Source = source;
            RelativeSpeedMetersPerSecond = relativeSpeedMetersPerSecond;
            NormalSpeedMetersPerSecond = normalSpeedMetersPerSecond;
            TangentialSpeedMetersPerSecond = tangentialSpeedMetersPerSecond;
            EffectiveMassKilograms = effectiveMassKilograms;
            NormalImpulseNewtonSeconds = normalImpulseNewtonSeconds;
            TangentialImpulseNewtonSeconds = tangentialImpulseNewtonSeconds;
            AveragePressurePascals = averagePressurePascals;
            AngleDegrees = angleDegrees;
        }

        public PhysicalAudioContactDynamicsSource Source { get; }

        public bool IsResolved => Source != PhysicalAudioContactDynamicsSource.Unknown;

        public float RelativeSpeedMetersPerSecond { get; }

        public float NormalSpeedMetersPerSecond { get; }

        public float TangentialSpeedMetersPerSecond { get; }

        public float EffectiveMassKilograms { get; }

        public float NormalImpulseNewtonSeconds { get; }

        public float TangentialImpulseNewtonSeconds { get; }

        public float AveragePressurePascals { get; }

        public float AngleDegrees { get; }

        /// <summary>Returns a contact carrying a proportional share of the resolved impulse and pressure.</summary>
        /// <remarks>NoAlloc. Used when one solver impulse is distributed across simultaneous contact points.</remarks>
        public PhysicalAudioContactDynamics WithImpulseScale(float scale)
        {
            float safeScale = IsFinite(scale) && scale > 0f
                ? (scale >= 1f ? 1f : scale)
                : 0f;
            return new PhysicalAudioContactDynamics(
                Source,
                RelativeSpeedMetersPerSecond,
                NormalSpeedMetersPerSecond,
                TangentialSpeedMetersPerSecond,
                EffectiveMassKilograms,
                NormalImpulseNewtonSeconds * safeScale,
                TangentialImpulseNewtonSeconds * safeScale,
                AveragePressurePascals * safeScale,
                AngleDegrees);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>Derives low-cost presentation-only contact dynamics from caller-supplied physical state.</summary>
    public static class PhysicalAudioContactDynamicsResolver
    {
        private const float RadiansToDegrees = 57.29577951308232f;

        /// <summary>Resolves one contact without allocations or engine dependencies.</summary>
        /// <remarks>NoAlloc. Returns false for invalid normals, masses, areas, tick durations, or non-finite input.</remarks>
        public static bool TryResolve(
            in PhysicalAudioContactDynamicsInput input,
            out PhysicalAudioContactDynamics dynamics)
        {
            if (input.Source == PhysicalAudioContactDynamicsSource.Unknown
                || input.ContactNormal.IsZero
                || !IsPositiveFinite(input.SourceMassKilograms)
                || (!input.ReceiverIsStatic && !IsPositiveFinite(input.ReceiverMassKilograms))
                || !IsNonNegativeFinite(input.Friction)
                || !IsNonNegativeFinite(input.Elasticity)
                || !IsPositiveFinite(input.ContactAreaSquareMeters)
                || !IsPositiveFinite(input.TickDurationSeconds)
                || !IsFinite(input.PreRelativeVelocityX)
                || !IsFinite(input.PreRelativeVelocityY)
                || !IsFinite(input.PreRelativeVelocityZ)
                || !IsFinite(input.PostRelativeVelocityX)
                || !IsFinite(input.PostRelativeVelocityY)
                || !IsFinite(input.PostRelativeVelocityZ))
            {
                dynamics = default;
                return false;
            }

            PhysicalAudioDirection normal = input.ContactNormal;
            float preNormalComponent = Dot(
                input.PreRelativeVelocityX,
                input.PreRelativeVelocityY,
                input.PreRelativeVelocityZ,
                normal);
            float normalSpeed = Math.Max(0f, -preNormalComponent);
            float tangentialX = input.PreRelativeVelocityX - (normal.X * preNormalComponent);
            float tangentialY = input.PreRelativeVelocityY - (normal.Y * preNormalComponent);
            float tangentialZ = input.PreRelativeVelocityZ - (normal.Z * preNormalComponent);
            float tangentialSpeed = Length(tangentialX, tangentialY, tangentialZ);
            float relativeSpeed = Length(
                input.PreRelativeVelocityX,
                input.PreRelativeVelocityY,
                input.PreRelativeVelocityZ);
            float effectiveMass = input.ReceiverIsStatic
                ? input.SourceMassKilograms
                : (input.SourceMassKilograms * input.ReceiverMassKilograms)
                    / (input.SourceMassKilograms + input.ReceiverMassKilograms);

            float normalImpulse;
            if (input.Source == PhysicalAudioContactDynamicsSource.SolverVelocityDelta)
            {
                float deltaNormal = Dot(
                    input.PostRelativeVelocityX - input.PreRelativeVelocityX,
                    input.PostRelativeVelocityY - input.PreRelativeVelocityY,
                    input.PostRelativeVelocityZ - input.PreRelativeVelocityZ,
                    normal);
                normalImpulse = effectiveMass * Math.Max(0f, deltaNormal);
            }
            else
            {
                normalImpulse = effectiveMass * (1f + input.Elasticity) * normalSpeed;
            }

            float tangentialImpulse = Math.Min(
                effectiveMass * tangentialSpeed,
                input.Friction * normalImpulse);
            float pressure = normalImpulse
                / input.TickDurationSeconds
                / input.ContactAreaSquareMeters;
            float angleDegrees = relativeSpeed > 0f
                ? (float)Math.Acos(Clamp01(normalSpeed / relativeSpeed)) * RadiansToDegrees
                : 0f;

            dynamics = new PhysicalAudioContactDynamics(
                input.Source,
                relativeSpeed,
                normalSpeed,
                tangentialSpeed,
                effectiveMass,
                normalImpulse,
                tangentialImpulse,
                pressure,
                angleDegrees);
            return true;
        }

        private static float Dot(float x, float y, float z, in PhysicalAudioDirection normal)
        {
            return (x * normal.X) + (y * normal.Y) + (z * normal.Z);
        }

        private static float Length(float x, float y, float z)
        {
            double square = ((double)x * x) + ((double)y * y) + ((double)z * z);
            return square <= 0d ? 0f : (float)Math.Sqrt(square);
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && IsFinite(value);
        }

        private static bool IsNonNegativeFinite(float value)
        {
            return value >= 0f && IsFinite(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
