using System;
using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    [Flags]
    public enum CharacterTestAuthorityPoseSampleSourceMask
    {
        None = 0,
        Base = 1 << 0,
        TerrainIk = 1 << 1,
        Secondary = 1 << 2
    }

    public enum CharacterTestAuthorityPoseFailure
    {
        Success = 0,
        MotionDisabled = 1,
        PostureDisabled = 2,
        ActionDisabled = 3,
        Flying = 4,
        KnockedBack = 5,
        Dead = 6,
        Severed = 7,
        DefinitionStale = 8,
        SourceStale = 9,
        ChannelConflict = 10,
        DegeneratePole = 11,
        OutOfReach = 12,
        InvalidResolvedBasis = 13,
        NoHit = 14,
        TooSteep = 15,
        InvalidProbe = 16
    }

    /// <summary>One orthonormal ModelRoot-local basis.</summary>
    public readonly struct CharacterTestResolvedAuthorityPoseBasis
    {
        public static CharacterTestResolvedAuthorityPoseBasis Identity { get; } =
            new CharacterTestResolvedAuthorityPoseBasis(
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One));

        public CharacterTestResolvedAuthorityPoseBasis(
            FixVector3 right,
            FixVector3 up,
            FixVector3 forward)
        {
            Right = right;
            Up = up;
            Forward = forward;
        }

        public FixVector3 Right { get; }
        public FixVector3 Up { get; }
        public FixVector3 Forward { get; }
    }

    /// <summary>One stable ModelRoot-local channel sample in the resolved authority frame.</summary>
    public readonly struct CharacterTestResolvedAuthorityPoseSample
    {
        public CharacterTestResolvedAuthorityPoseSample(
            int ordinal,
            int channelId,
            FixVector3 position,
            CharacterTestResolvedAuthorityPoseBasis basis,
            bool valid,
            CharacterTestAuthorityPoseSampleSourceMask sourceMask,
            CharacterTestAuthorityPoseFailure failure)
        {
            Ordinal = ordinal;
            ChannelId = channelId;
            Position = position;
            Basis = basis;
            Valid = valid;
            SourceMask = sourceMask;
            Failure = failure;
        }

        public int Ordinal { get; }
        public int ChannelId { get; }
        public FixVector3 Position { get; }
        public CharacterTestResolvedAuthorityPoseBasis Basis { get; }
        public bool Valid { get; }
        public CharacterTestAuthorityPoseSampleSourceMask SourceMask { get; }
        public CharacterTestAuthorityPoseFailure Failure { get; }
    }

    /// <summary>
    /// The sole CharacterTest terrain-authority pose product. Samples are fixed-capacity,
    /// sorted by authored channel id, and remain in ModelRoot-local space.
    /// </summary>
    /// <remarks>NoAlloc after the owning component is constructed.</remarks>
    public sealed class CharacterTestResolvedAuthorityPoseFrame
    {
        public const int MaximumSampleCount = 32;

        private readonly CharacterTestResolvedAuthorityPoseSample[] _samples =
            new CharacterTestResolvedAuthorityPoseSample[MaximumSampleCount];

        public long RuntimeFrame { get; private set; } = -1L;
        public long Revision { get; private set; }
        public int DefinitionRevision { get; private set; }
        public string DefinitionContentHash { get; private set; } = string.Empty;
        public int BaseSourceRevision { get; private set; }
        public string BaseSourceContentHash { get; private set; } = string.Empty;
        public int Count { get; private set; }
        public bool IsPublished => RuntimeFrame >= 0L;

        public CharacterTestResolvedAuthorityPoseSample this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _samples[index];
            }
        }

        internal void Begin(
            long runtimeFrame,
            long revision,
            int definitionRevision,
            string definitionContentHash,
            int baseSourceRevision,
            string baseSourceContentHash)
        {
            RuntimeFrame = runtimeFrame;
            Revision = revision;
            DefinitionRevision = definitionRevision;
            DefinitionContentHash = definitionContentHash ?? string.Empty;
            BaseSourceRevision = baseSourceRevision;
            BaseSourceContentHash = baseSourceContentHash ?? string.Empty;
            Count = 0;
        }

        internal bool TryAdd(in CharacterTestResolvedAuthorityPoseSample sample)
        {
            if (Count >= MaximumSampleCount)
                return false;
            _samples[Count++] = sample;
            return true;
        }

        internal void ClearDerived(long retainedRevision)
        {
            RuntimeFrame = -1L;
            Revision = retainedRevision < 0L ? 0L : retainedRevision;
            DefinitionRevision = 0;
            DefinitionContentHash = string.Empty;
            BaseSourceRevision = 0;
            BaseSourceContentHash = string.Empty;
            Count = 0;
        }

        public bool TryGetSample(
            int channelId,
            out CharacterTestResolvedAuthorityPoseSample sample)
        {
            int low = 0;
            int high = Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int compare = _samples[middle].ChannelId.CompareTo(channelId);
                if (compare == 0)
                {
                    sample = _samples[middle];
                    return true;
                }
                if (compare < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            sample = default;
            return false;
        }
    }
}
