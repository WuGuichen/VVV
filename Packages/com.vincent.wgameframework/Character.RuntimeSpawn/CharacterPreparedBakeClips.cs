using System;
using System.Collections.Generic;
using MxFramework.Animation;

namespace MxFramework.CharacterRuntimeSpawn
{
    /// <summary>Immutable preparation snapshot for one resource clip; never a serialized content identity.</summary>
    public sealed class CharacterPreparedSocketClip
    {
        private readonly Dictionary<int, CharacterSocketBakeTrack> _tracks;
        internal CharacterPreparedSocketClip(string sourceClipLabel, IEnumerable<KeyValuePair<CharacterSocketBakeKey, CharacterSocketBakeTrack>> source)
        {
            SourceClipLabel = sourceClipLabel;
            _tracks = new Dictionary<int, CharacterSocketBakeTrack>();
            foreach (var pair in source)
                if (string.Equals(pair.Key.ClipKey, sourceClipLabel, StringComparison.Ordinal)) _tracks.Add(pair.Key.SocketId, pair.Value);
        }
        public string SourceClipLabel { get; }
        public int TrackCount => _tracks.Count;
        public bool TryGetTrack(int socketId, out CharacterSocketBakeTrack track) => _tracks.TryGetValue(socketId, out track);
        public bool TryGetFrame(int socketId, int localFrame, out MxAnimationBakedSocketFrame frame)
        {
            frame = null;
            return localFrame >= 0 && _tracks.TryGetValue(socketId, out var track) && track.TryGetFrame(localFrame, out frame);
        }
        public bool TryGetBasis(int socketId, int localFrame, out CharacterSocketBakeBasisFrame basis)
        {
            basis = null;
            return localFrame >= 0 && _tracks.TryGetValue(socketId, out var track) && track.TryGetBasis(localFrame, out basis);
        }
    }

    /// <summary>Immutable preparation snapshot for one resource clip; Tick indexes collider tracks by integer identity.</summary>
    public sealed class CharacterPreparedHurtboxClip
    {
        private readonly Dictionary<int, CharacterHurtboxBakeTrack> _tracks;
        internal CharacterPreparedHurtboxClip(string sourceClipLabel, IEnumerable<KeyValuePair<CharacterHurtboxBakeKey, CharacterHurtboxBakeTrack>> source)
        {
            SourceClipLabel = sourceClipLabel;
            _tracks = new Dictionary<int, CharacterHurtboxBakeTrack>();
            foreach (var pair in source)
                if (string.Equals(pair.Key.ClipKey, sourceClipLabel, StringComparison.Ordinal)) _tracks.Add(pair.Key.ColliderId, pair.Value);
        }
        public string SourceClipLabel { get; }
        public int TrackCount => _tracks.Count;
        public bool TryGetTrack(int colliderId, out CharacterHurtboxBakeTrack track) => _tracks.TryGetValue(colliderId, out track);
        public bool TryGetPose(int colliderId, int localFrame, out CharacterHurtboxBakePose pose)
        {
            pose = default;
            return localFrame >= 0 && _tracks.TryGetValue(colliderId, out var track) && track.TryGetPose(localFrame, out pose);
        }
    }
}
