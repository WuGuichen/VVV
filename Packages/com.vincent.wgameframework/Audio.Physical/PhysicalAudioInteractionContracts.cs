using System;

namespace MxFramework.Audio.Physical
{
    /// <summary>Stable runtime identity for an acoustic material profile.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct AcousticMaterialId : IEquatable<AcousticMaterialId>, IComparable<AcousticMaterialId>
    {
        public static readonly AcousticMaterialId None = default;

        public AcousticMaterialId(int value) => Value = value;

        public int Value { get; }

        public bool IsValid => Value > 0;

        public int CompareTo(AcousticMaterialId other) => Value.CompareTo(other.Value);

        public bool Equals(AcousticMaterialId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is AcousticMaterialId other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(AcousticMaterialId left, AcousticMaterialId right) => left.Equals(right);

        public static bool operator !=(AcousticMaterialId left, AcousticMaterialId right) => !left.Equals(right);
    }

    /// <summary>Stable runtime identity for one unordered acoustic-material pair.</summary>
    /// <remarks>NoAlloc. The catalog owns canonical material ordering.</remarks>
    public readonly struct AcousticMaterialPairId : IEquatable<AcousticMaterialPairId>, IComparable<AcousticMaterialPairId>
    {
        public static readonly AcousticMaterialPairId None = default;

        public AcousticMaterialPairId(int value) => Value = value;

        public int Value { get; }

        public bool IsValid => Value > 0;

        public int CompareTo(AcousticMaterialPairId other) => Value.CompareTo(other.Value);

        public bool Equals(AcousticMaterialPairId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is AcousticMaterialPairId other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(AcousticMaterialPairId left, AcousticMaterialPairId right) => left.Equals(right);

        public static bool operator !=(AcousticMaterialPairId left, AcousticMaterialPairId right) => !left.Equals(right);
    }

    /// <summary>Stable runtime identity for a physical-audio selection family.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct PhysicalAudioFamilyId : IEquatable<PhysicalAudioFamilyId>, IComparable<PhysicalAudioFamilyId>
    {
        public static readonly PhysicalAudioFamilyId None = default;

        public PhysicalAudioFamilyId(int value) => Value = value;

        public int Value { get; }

        public bool IsValid => Value > 0;

        public int CompareTo(PhysicalAudioFamilyId other) => Value.CompareTo(other.Value);

        public bool Equals(PhysicalAudioFamilyId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is PhysicalAudioFamilyId other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(PhysicalAudioFamilyId left, PhysicalAudioFamilyId right) => left.Equals(right);

        public static bool operator !=(PhysicalAudioFamilyId left, PhysicalAudioFamilyId right) => !left.Equals(right);
    }

    /// <summary>Stable runtime identity for a complete selectable Basis Sound.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct AudioBasisId : IEquatable<AudioBasisId>, IComparable<AudioBasisId>
    {
        public static readonly AudioBasisId None = default;

        public AudioBasisId(int value) => Value = value;

        public int Value { get; }

        public bool IsValid => Value > 0;

        public int CompareTo(AudioBasisId other) => Value.CompareTo(other.Value);

        public bool Equals(AudioBasisId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is AudioBasisId other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(AudioBasisId left, AudioBasisId right) => left.Equals(right);

        public static bool operator !=(AudioBasisId left, AudioBasisId right) => !left.Equals(right);
    }

    /// <summary>Stable runtime identity for a parameter response profile.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct PhysicalAudioResponseProfileId : IEquatable<PhysicalAudioResponseProfileId>
    {
        public static readonly PhysicalAudioResponseProfileId None = default;

        public PhysicalAudioResponseProfileId(int value) => Value = value;

        public int Value { get; }

        public bool IsValid => Value > 0;

        public bool Equals(PhysicalAudioResponseProfileId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is PhysicalAudioResponseProfileId other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(PhysicalAudioResponseProfileId left, PhysicalAudioResponseProfileId right) => left.Equals(right);

        public static bool operator !=(PhysicalAudioResponseProfileId left, PhysicalAudioResponseProfileId right) => !left.Equals(right);
    }

    public enum PhysicalAudioInteractionKind
    {
        Unknown = 0,
        Footstep = 1,
        Landing = 2,
        ObjectImpact = 3,
        WeaponHit = 4,
        WeaponBlock = 5,
        AirDisplacement = 6,
        Scrape = 7,
        Roll = 8,
        Compression = 9
    }

    public enum PhysicalAudioInteractionMode
    {
        Any = 0,
        Step = 1,
        Landing = 2,
        Impact = 3,
        Hit = 4,
        Block = 5,
        Sweep = 6,
        Scrape = 7,
        Roll = 8,
        Compression = 9
    }

    /// <summary>Action semantics available inside a shared material-pair library.</summary>
    public enum PhysicalAudioVariantKind
    {
        Unknown = 0,
        Impact = 1,
        Step = 2,
        Landing = 3,
        Hit = 4,
        Block = 5,
        Sweep = 6,
        Scrape = 7,
        Roll = 8,
        Compression = 9
    }

    public enum PhysicalAudioInteractionPhase
    {
        Started = 0,
        Stayed = 1,
        Ended = 2
    }

    public enum AcousticSurfaceKind
    {
        Any = 0,
        Polished = 1,
        Smooth = 2,
        Normal = 3,
        Rough = 4,
        Granular = 5
    }

    public enum AcousticConstructionKind
    {
        Any = 0,
        Solid = 1,
        Hollow = 2,
        ThinShell = 3,
        SoftFilled = 4,
        Gas = 5
    }

    /// <summary>A normalized world-space direction carried by a physical-audio interaction.</summary>
    /// <remarks>
    /// NoAlloc. Construction normalizes finite non-zero input and stores zero for invalid or degenerate input.
    /// </remarks>
    public readonly struct PhysicalAudioDirection : IEquatable<PhysicalAudioDirection>
    {
        private const float MinimumLengthSquared = 0.000000000001f;

        /// <summary>Represents an unavailable or degenerate direction.</summary>
        public static readonly PhysicalAudioDirection Zero = default;

        /// <summary>Creates a safely normalized direction.</summary>
        /// <remarks>NoAlloc.</remarks>
        public PhysicalAudioDirection(float x, float y, float z)
        {
            float lengthSquared = (x * x) + (y * y) + (z * z);
            if (float.IsNaN(lengthSquared)
                || float.IsInfinity(lengthSquared)
                || lengthSquared <= MinimumLengthSquared)
            {
                X = 0f;
                Y = 0f;
                Z = 0f;
                return;
            }

            float inverseLength = 1f / (float)Math.Sqrt(lengthSquared);
            X = x * inverseLength;
            Y = y * inverseLength;
            Z = z * inverseLength;
        }

        /// <summary>Normalized world-space X component.</summary>
        public float X { get; }

        /// <summary>Normalized world-space Y component.</summary>
        public float Y { get; }

        /// <summary>Normalized world-space Z component.</summary>
        public float Z { get; }

        /// <summary>Whether no meaningful direction was supplied.</summary>
        public bool IsZero => X == 0f && Y == 0f && Z == 0f;

        public bool Equals(PhysicalAudioDirection other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj) => obj is PhysicalAudioDirection other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                return (hash * 397) ^ Z.GetHashCode();
            }
        }

        public static bool operator ==(PhysicalAudioDirection left, PhysicalAudioDirection right) => left.Equals(right);

        public static bool operator !=(PhysicalAudioDirection left, PhysicalAudioDirection right) => !left.Equals(right);
    }

    /// <summary>Runtime acoustic properties for one side of a physical interaction.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct AcousticBodyProperties
    {
        public AcousticBodyProperties(
            AcousticMaterialId materialId,
            float mass,
            float contactArea,
            AcousticSurfaceKind surface = AcousticSurfaceKind.Normal,
            AcousticConstructionKind construction = AcousticConstructionKind.Solid)
        {
            MaterialId = materialId;
            Mass = mass < 0f ? 0f : mass;
            ContactArea = contactArea < 0f ? 0f : contactArea;
            Surface = surface;
            Construction = construction;
        }

        public AcousticMaterialId MaterialId { get; }

        /// <summary>Project-normalized mass. Catalog ranges define its scale.</summary>
        public float Mass { get; }

        /// <summary>Project-normalized contact area. Catalog ranges define its scale.</summary>
        public float ContactArea { get; }

        public AcousticSurfaceKind Surface { get; }

        public AcousticConstructionKind Construction { get; }
    }

    /// <summary>Normalized noEngine fact consumed by the physical-audio resolver.</summary>
    /// <remarks>NoAlloc. Project bridges may construct this value in Tick paths.</remarks>
    public readonly struct PhysicalAudioInteractionFact
    {
        /// <summary>Creates a fact without an authored contact direction.</summary>
        /// <remarks>
        /// NoAlloc. Compatibility overload for callers that predate <see cref="ContactNormal"/>; the normal is zero.
        /// </remarks>
        public PhysicalAudioInteractionFact(
            PhysicalAudioInteractionKind kind,
            PhysicalAudioInteractionMode mode,
            PhysicalAudioInteractionPhase phase,
            in AcousticBodyProperties impactor,
            in AcousticBodyProperties receiver,
            float relativeSpeed,
            float normalSpeed,
            float tangentialSpeed,
            float pressure,
            float impulse,
            float angleDegrees,
            float intensity,
            in AudioTransform transform,
            int emitterId = 0,
            long frame = 0,
            long sequence = 0)
            : this(
                kind,
                mode,
                phase,
                impactor,
                receiver,
                relativeSpeed,
                normalSpeed,
                tangentialSpeed,
                pressure,
                impulse,
                angleDegrees,
                intensity,
                PhysicalAudioDirection.Zero,
                transform,
                emitterId,
                frame,
                sequence)
        {
        }

        /// <summary>Creates a fact with an explicit normalized world-space contact direction.</summary>
        /// <remarks>NoAlloc. Invalid or degenerate direction input is represented by <see cref="PhysicalAudioDirection.Zero"/>.</remarks>
        public PhysicalAudioInteractionFact(
            PhysicalAudioInteractionKind kind,
            PhysicalAudioInteractionMode mode,
            PhysicalAudioInteractionPhase phase,
            in AcousticBodyProperties impactor,
            in AcousticBodyProperties receiver,
            float relativeSpeed,
            float normalSpeed,
            float tangentialSpeed,
            float pressure,
            float impulse,
            float angleDegrees,
            float intensity,
            in PhysicalAudioDirection contactNormal,
            in AudioTransform transform,
            int emitterId = 0,
            long frame = 0,
            long sequence = 0)
            : this(
                kind,
                mode,
                phase,
                impactor,
                receiver,
                relativeSpeed,
                normalSpeed,
                tangentialSpeed,
                pressure,
                impulse,
                angleDegrees,
                intensity,
                contactNormal,
                transform,
                PhysicalAudioContactDynamicsSource.Unknown,
                emitterId,
                frame,
                sequence)
        {
        }

        /// <summary>Creates a fact with an explicit contact direction and dynamics provenance.</summary>
        /// <remarks>NoAlloc. Dynamics provenance is presentation-only and does not participate in gameplay authority.</remarks>
        public PhysicalAudioInteractionFact(
            PhysicalAudioInteractionKind kind,
            PhysicalAudioInteractionMode mode,
            PhysicalAudioInteractionPhase phase,
            in AcousticBodyProperties impactor,
            in AcousticBodyProperties receiver,
            float relativeSpeed,
            float normalSpeed,
            float tangentialSpeed,
            float pressure,
            float impulse,
            float angleDegrees,
            float intensity,
            in PhysicalAudioDirection contactNormal,
            in AudioTransform transform,
            PhysicalAudioContactDynamicsSource dynamicsSource,
            int emitterId = 0,
            long frame = 0,
            long sequence = 0)
        {
            Kind = kind;
            Mode = mode;
            Phase = phase;
            Impactor = impactor;
            Receiver = receiver;
            RelativeSpeed = NonNegative(relativeSpeed);
            NormalSpeed = NonNegative(normalSpeed);
            TangentialSpeed = NonNegative(tangentialSpeed);
            Pressure = NonNegative(pressure);
            Impulse = NonNegative(impulse);
            AngleDegrees = Clamp(angleDegrees, 0f, 180f);
            Intensity = Clamp(intensity, 0f, 1f);
            ContactNormal = contactNormal;
            Transform = transform;
            DynamicsSource = dynamicsSource;
            EmitterId = emitterId;
            Frame = frame;
            Sequence = sequence;
        }

        public PhysicalAudioInteractionKind Kind { get; }

        public PhysicalAudioInteractionMode Mode { get; }

        public PhysicalAudioInteractionPhase Phase { get; }

        public AcousticBodyProperties Impactor { get; }

        public AcousticBodyProperties Receiver { get; }

        public float RelativeSpeed { get; }

        public float NormalSpeed { get; }

        public float TangentialSpeed { get; }

        public float Pressure { get; }

        public float Impulse { get; }

        public float AngleDegrees { get; }

        public float Intensity { get; }

        /// <summary>
        /// Normalized world-space contact normal. Air-displacement facts use the sweep/displacement direction;
        /// zero means the source did not provide a meaningful direction.
        /// </summary>
        public PhysicalAudioDirection ContactNormal { get; }

        /// <summary>Playback position and emitter orientation. It does not carry contact geometry.</summary>
        public AudioTransform Transform { get; }

        /// <summary>How the physical quantities in this presentation-only fact were derived.</summary>
        public PhysicalAudioContactDynamicsSource DynamicsSource { get; }

        public int EmitterId { get; }

        public long Frame { get; }

        public long Sequence { get; }

        /// <remarks>NoAlloc.</remarks>
        public PhysicalAudioInteractionFact WithPhase(PhysicalAudioInteractionPhase phase)
        {
            return new PhysicalAudioInteractionFact(
                Kind,
                Mode,
                phase,
                Impactor,
                Receiver,
                RelativeSpeed,
                NormalSpeed,
                TangentialSpeed,
                Pressure,
                Impulse,
                AngleDegrees,
                Intensity,
                ContactNormal,
                Transform,
                DynamicsSource,
                EmitterId,
                Frame,
                Sequence);
        }

        private static float NonNegative(float value) => value < 0f || float.IsNaN(value) ? 0f : value;

        private static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value) || value <= min)
                return min;
            return value >= max ? max : value;
        }
    }

    /// <summary>Project extension point mapping gameplay objects to stable acoustic properties.</summary>
    public interface IAcousticBodyPropertyProvider
    {
        /// <remarks>NoAlloc.</remarks>
        bool TryGetProperties(int subjectId, out AcousticBodyProperties properties);
    }
}
