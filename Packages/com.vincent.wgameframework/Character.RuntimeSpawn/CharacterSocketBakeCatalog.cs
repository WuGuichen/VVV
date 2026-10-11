using System;
using System.Collections.Generic;
using MxFramework.Animation;

namespace MxFramework.CharacterRuntimeSpawn
{
    /// <summary>Package-local deterministic animation socket tracks.</summary>
    public sealed class CharacterSocketBakeCatalog
    {
        private readonly Dictionary<CharacterSocketBakeKey, CharacterSocketBakeTrack> _tracks =
            new Dictionary<CharacterSocketBakeKey, CharacterSocketBakeTrack>();

        public int TrackCount => _tracks.Count;

        /// <summary>LowFreqAlloc: prepare after catalog load/replacement; Tick must retain the returned snapshot.</summary>
        public CharacterPreparedSocketClip PrepareClip(string sourceClipKey)
        {
            if (string.IsNullOrWhiteSpace(sourceClipKey)) return null;
            return new CharacterPreparedSocketClip(sourceClipKey, _tracks);
        }


        public void RegisterTrack(string clipKey, int socketId, int quantizationScale, MxAnimationBakedSocketFrame[] frames)
        {
            RegisterTrack(clipKey, socketId, quantizationScale, frames, 0, null);
        }

        public void RegisterTrack(
            string clipKey,
            int socketId,
            int quantizationScale,
            MxAnimationBakedSocketFrame[] frames,
            int basisQuantizationScale,
            CharacterSocketBakeBasisFrame[] basisFrames)
        {
            if (string.IsNullOrWhiteSpace(clipKey))
                throw new ArgumentException("Clip key is required.", nameof(clipKey));
            if (socketId <= 0)
                throw new ArgumentException("Socket id is required.", nameof(socketId));
            _tracks[new CharacterSocketBakeKey(clipKey, socketId)] =
                new CharacterSocketBakeTrack(
                    quantizationScale,
                    frames,
                    basisQuantizationScale,
                    basisFrames);
        }

        public bool TryGetFrame(string clipKey, int socketId, int localFrame, out MxAnimationBakedSocketFrame frame)
        {
            frame = null;
            return localFrame >= 0
                && _tracks.TryGetValue(new CharacterSocketBakeKey(clipKey, socketId), out CharacterSocketBakeTrack track)
                && track.TryGetFrame(localFrame, out frame);
        }

        public bool TryGetTrack(string clipKey, int socketId, out CharacterSocketBakeTrack track)
        {
            return _tracks.TryGetValue(new CharacterSocketBakeKey(clipKey, socketId), out track);
        }


        public bool TryGetBasis(
            string clipKey,
            int socketId,
            int localFrame,
            out CharacterSocketBakeBasisFrame basis)
        {
            basis = null;
            return localFrame >= 0
                && _tracks.TryGetValue(new CharacterSocketBakeKey(clipKey, socketId), out CharacterSocketBakeTrack track)
                && track.TryGetBasis(localFrame, out basis);
        }

        public IEnumerable<KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack>> EnumerateTracks() => _tracks;
    }

    public sealed class CharacterSocketBakeTrack
    {
        private readonly MxAnimationBakedSocketFrame[] _frames;
        private readonly CharacterSocketBakeBasisFrame[] _basisFrames;

        public CharacterSocketBakeTrack(
            int quantizationScale,
            MxAnimationBakedSocketFrame[] frames,
            int basisQuantizationScale = 0,
            CharacterSocketBakeBasisFrame[] basisFrames = null)
        {
            QuantizationScale = quantizationScale > 0
                ? quantizationScale
                : CharacterWeaponTraceBakeArtifactBuilder.DefaultQuantizationScale;
            _frames = frames ?? Array.Empty<MxAnimationBakedSocketFrame>();
            BasisQuantizationScale = basisQuantizationScale > 0 ? basisQuantizationScale : 0;
            _basisFrames = basisFrames ?? Array.Empty<CharacterSocketBakeBasisFrame>();
        }

        public int QuantizationScale { get; }
        public int BasisQuantizationScale { get; }
        public bool HasBasis => BasisQuantizationScale > 0 && _basisFrames.Length > 0;
        public IReadOnlyList<MxAnimationBakedSocketFrame> Frames => _frames;
        public IReadOnlyList<CharacterSocketBakeBasisFrame> BasisFrames => _basisFrames;

        public bool TryGetFrame(int localFrame, out MxAnimationBakedSocketFrame frame)
        {
            if ((uint)localFrame < (uint)_frames.Length)
            {
                frame = _frames[localFrame];
                return frame != null;
            }
            frame = null;
            return false;
        }


        public bool TryGetBasis(int localFrame, out CharacterSocketBakeBasisFrame basis)
        {
            if (HasBasis && (uint)localFrame < (uint)_basisFrames.Length)
            {
                basis = _basisFrames[localFrame];
                return basis != null;
            }
            basis = null;
            return false;
        }
    }

    /// <summary>
    /// Optional ModelRoot-local basis sampled beside an existing position frame.
    /// Position-only tracks remain valid and expose <see cref="CharacterSocketBakeTrack.HasBasis"/> as false.
    /// </summary>
    public sealed class CharacterSocketBakeBasisFrame
    {
        public CharacterSocketBakeBasisFrame(
            int localFrame,
            MxAnimationBakedVector3 right,
            MxAnimationBakedVector3 up,
            MxAnimationBakedVector3 forward)
        {
            LocalFrame = localFrame;
            Right = right;
            Up = up;
            Forward = forward;
        }

        public int LocalFrame { get; }
        public MxAnimationBakedVector3 Right { get; }
        public MxAnimationBakedVector3 Up { get; }
        public MxAnimationBakedVector3 Forward { get; }
    }

    public readonly struct CharacterSocketBakeKey : IEquatable<CharacterSocketBakeKey>
    {
        public CharacterSocketBakeKey(string clipKey, int socketId)
        {
            ClipKey = clipKey ?? string.Empty;
            SocketId = socketId;
        }

        public string ClipKey { get; }
        public int SocketId { get; }
        public bool Equals(CharacterSocketBakeKey other) =>
            string.Equals(ClipKey, other.ClipKey, StringComparison.Ordinal)
            && SocketId == other.SocketId;
        public override bool Equals(object obj) => obj is CharacterSocketBakeKey other && Equals(other);
        public override int GetHashCode() => unchecked((StringComparer.Ordinal.GetHashCode(ClipKey) * 397) ^ SocketId);
    }
}
