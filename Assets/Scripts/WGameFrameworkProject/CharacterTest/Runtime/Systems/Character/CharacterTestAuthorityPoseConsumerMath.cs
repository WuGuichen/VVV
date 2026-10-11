using System;
using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// CharacterTest-only attachment delta resolved from the single T3 authority frame.
    /// It is a transient value and does not own, copy, or republish pose state.
    /// </summary>
    internal readonly struct CharacterTestAuthorityPoseAttachmentDelta
    {
        internal CharacterTestAuthorityPoseAttachmentDelta(
            int channelId,
            long runtimeFrame,
            long revision,
            FixVector3 basePosition,
            CharacterTestResolvedAuthorityPoseBasis baseBasis,
            CharacterTestResolvedAuthorityPoseSample resolvedSample)
        {
            ChannelId = channelId;
            RuntimeFrame = runtimeFrame;
            Revision = revision;
            BasePosition = basePosition;
            BaseBasis = baseBasis;
            ResolvedSample = resolvedSample;
        }

        internal int ChannelId { get; }
        internal long RuntimeFrame { get; }
        internal long Revision { get; }
        internal FixVector3 BasePosition { get; }
        internal CharacterTestResolvedAuthorityPoseBasis BaseBasis { get; }
        internal CharacterTestResolvedAuthorityPoseSample ResolvedSample { get; }

        internal FixVector3 TransformPoint(FixVector3 modelRootLocalPoint)
        {
            FixVector3 channelLocal = ToChannelLocal(modelRootLocalPoint - BasePosition);
            return ResolvedSample.Position + FromChannelLocal(channelLocal);
        }

        internal FixVector3 TransformAxis(FixVector3 modelRootLocalAxis)
        {
            return FromChannelLocal(ToChannelLocal(modelRootLocalAxis));
        }

        private FixVector3 ToChannelLocal(FixVector3 value)
        {
            return new FixVector3(
                value.Dot(BaseBasis.Right),
                value.Dot(BaseBasis.Up),
                value.Dot(BaseBasis.Forward));
        }

        private FixVector3 FromChannelLocal(FixVector3 value)
        {
            return ResolvedSample.Basis.Right * value.X
                + ResolvedSample.Basis.Up * value.Y
                + ResolvedSample.Basis.Forward * value.Z;
        }
    }

    /// <summary>
    /// Narrow, allocation-free math entry used by CharacterTest authority-pose consumers.
    /// Missing, unpublished, stale-witness, or invalid samples return false so callers retain their baked base pose.
    /// A valid T3 sample remains authoritative during retained/fading recovery regardless of its failure/source tags.
    /// </summary>
    internal static class CharacterTestAuthorityPoseConsumerMath
    {
        private static readonly Fix64 BasisTolerance = Fix64.FromRatio(1, 100);

        internal static bool HasAuthoredAttachment(
            CharacterAuthorityPoseRigRuntimeBinding rig,
            CharacterAuthorityPoseRigAttachmentKind kind,
            int stableId)
        {
            return TryFindAttachment(rig, kind, stableId, out _);
        }

        internal static bool TryResolveAttachmentDelta(
            CharacterTestCharacterEntity entity,
            CharacterAuthorityPoseRigRuntimeBinding rig,
            CharacterSocketBakeCatalog catalog,
            CharacterAuthorityPoseRigAttachmentKind kind,
            int stableId,
            string clipKey,
            int localFrame,
            bool loop,
            RuntimeFrame expectedRuntimeFrame,
            out CharacterTestAuthorityPoseAttachmentDelta delta)
        {
            delta = default;
            if (entity == null
                || rig == null
                || !rig.IsAuthored
                || catalog == null
                || string.IsNullOrWhiteSpace(clipKey)
                || localFrame < 0
                || !TryFindAttachment(rig, kind, stableId, out CharacterAuthorityPoseAttachmentRuntimeBinding attachment))
            {
                return false;
            }

            CharacterTestTerrainIkStateComponent state = entity.TerrainIkState;
            CharacterTestResolvedAuthorityPoseFrame frame = entity.ResolvedAuthorityPoseFrame;
            if (state == null
                || !state.IsConfigured
                || frame == null
                || !frame.IsPublished
                || frame.RuntimeFrame != expectedRuntimeFrame.Value
                || frame.Revision <= 0L
                || frame.Revision != state.LastPublishedRevision
                || frame.DefinitionRevision != state.DefinitionRevision
                || frame.DefinitionRevision != rig.Revision
                || !string.Equals(frame.DefinitionContentHash, state.DefinitionContentHash, StringComparison.Ordinal)
                || !string.Equals(frame.DefinitionContentHash, rig.ContentHash, StringComparison.Ordinal)
                || frame.BaseSourceRevision <= 0
                || frame.BaseSourceRevision != state.BaseSourceRevision
                || string.IsNullOrWhiteSpace(frame.BaseSourceContentHash)
                || !string.Equals(frame.BaseSourceContentHash, state.BaseSourceContentHash, StringComparison.Ordinal)
                || !frame.TryGetSample(
                    attachment.ChannelId,
                    out CharacterTestResolvedAuthorityPoseSample resolved)
                || !resolved.Valid
                || !TryNormalizeBasis(resolved.Basis, out CharacterTestResolvedAuthorityPoseBasis resolvedBasis))
            {
                return false;
            }

            if (!catalog.TryGetTrack(clipKey, attachment.ChannelId, out CharacterSocketBakeTrack track)
                || track == null
                || track.Frames.Count == 0)
            {
                return false;
            }

            int frameIndex = localFrame;
            if (loop)
                frameIndex %= track.Frames.Count;
            else if ((uint)frameIndex >= (uint)track.Frames.Count)
                return false;

            if (!track.TryGetFrame(frameIndex, out MxAnimationBakedSocketFrame baseFrame)
                || !track.TryGetBasis(frameIndex, out CharacterSocketBakeBasisFrame baseBasisFrame)
                || baseFrame == null
                || baseBasisFrame == null
                || !TryReadBaseBasis(track, baseBasisFrame, out CharacterTestResolvedAuthorityPoseBasis baseBasis))
            {
                return false;
            }

            FixVector3 basePosition = ToFixVector3(baseFrame.Position, track.QuantizationScale);
            var normalizedResolved = new CharacterTestResolvedAuthorityPoseSample(
                resolved.Ordinal,
                resolved.ChannelId,
                resolved.Position,
                resolvedBasis,
                resolved.Valid,
                resolved.SourceMask,
                resolved.Failure);
            delta = new CharacterTestAuthorityPoseAttachmentDelta(
                attachment.ChannelId,
                frame.RuntimeFrame,
                frame.Revision,
                basePosition,
                baseBasis,
                normalizedResolved);
            return true;
        }

        internal static bool TryResolveAttachmentDelta(
            CharacterTestCharacterEntity entity,
            CharacterAuthorityPoseRigRuntimeBinding rig,
            CharacterPreparedSocketClip clip,
            CharacterAuthorityPoseRigAttachmentKind kind,
            int stableId,
            int localFrame,
            bool loop,
            RuntimeFrame expectedRuntimeFrame,
            out CharacterTestAuthorityPoseAttachmentDelta delta)
        {
            delta = default;
            if (entity == null
                || rig == null
                || !rig.IsAuthored
                || clip == null
                || localFrame < 0
                || !TryFindAttachment(rig, kind, stableId, out CharacterAuthorityPoseAttachmentRuntimeBinding attachment))
            {
                return false;
            }

            CharacterTestTerrainIkStateComponent state = entity.TerrainIkState;
            CharacterTestResolvedAuthorityPoseFrame frame = entity.ResolvedAuthorityPoseFrame;
            if (state == null
                || !state.IsConfigured
                || frame == null
                || !frame.IsPublished
                || frame.RuntimeFrame != expectedRuntimeFrame.Value
                || frame.Revision <= 0L
                || frame.Revision != state.LastPublishedRevision
                || frame.DefinitionRevision != state.DefinitionRevision
                || frame.DefinitionRevision != rig.Revision
                || !string.Equals(frame.DefinitionContentHash, state.DefinitionContentHash, StringComparison.Ordinal)
                || !string.Equals(frame.DefinitionContentHash, rig.ContentHash, StringComparison.Ordinal)
                || frame.BaseSourceRevision <= 0
                || frame.BaseSourceRevision != state.BaseSourceRevision
                || string.IsNullOrWhiteSpace(frame.BaseSourceContentHash)
                || !string.Equals(frame.BaseSourceContentHash, state.BaseSourceContentHash, StringComparison.Ordinal)
                || !frame.TryGetSample(
                    attachment.ChannelId,
                    out CharacterTestResolvedAuthorityPoseSample resolved)
                || !resolved.Valid
                || !TryNormalizeBasis(resolved.Basis, out CharacterTestResolvedAuthorityPoseBasis resolvedBasis))
            {
                return false;
            }

            if (!clip.TryGetTrack(attachment.ChannelId, out CharacterSocketBakeTrack track)
                || track == null
                || track.Frames.Count == 0)
            {
                return false;
            }

            int frameIndex = localFrame;
            if (loop)
                frameIndex %= track.Frames.Count;
            else if ((uint)frameIndex >= (uint)track.Frames.Count)
                return false;

            if (!track.TryGetFrame(frameIndex, out MxAnimationBakedSocketFrame baseFrame)
                || !track.TryGetBasis(frameIndex, out CharacterSocketBakeBasisFrame baseBasisFrame)
                || baseFrame == null
                || baseBasisFrame == null
                || !TryReadBaseBasis(track, baseBasisFrame, out CharacterTestResolvedAuthorityPoseBasis baseBasis))
            {
                return false;
            }

            FixVector3 basePosition = ToFixVector3(baseFrame.Position, track.QuantizationScale);
            var normalizedResolved = new CharacterTestResolvedAuthorityPoseSample(
                resolved.Ordinal,
                resolved.ChannelId,
                resolved.Position,
                resolvedBasis,
                resolved.Valid,
                resolved.SourceMask,
                resolved.Failure);
            delta = new CharacterTestAuthorityPoseAttachmentDelta(
                attachment.ChannelId,
                frame.RuntimeFrame,
                frame.Revision,
                basePosition,
                baseBasis,
                normalizedResolved);
            return true;
        }

        private static bool TryFindAttachment(
            CharacterAuthorityPoseRigRuntimeBinding rig,
            CharacterAuthorityPoseRigAttachmentKind kind,
            int stableId,
            out CharacterAuthorityPoseAttachmentRuntimeBinding attachment)
        {
            attachment = null;
            if (rig == null || !rig.IsAuthored || (stableId <= 0))
                return false;

            CharacterAuthorityPoseAttachmentRuntimeBinding[] attachments = rig.Attachments;
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterAuthorityPoseAttachmentRuntimeBinding candidate = attachments[i];
                if (candidate != null
                    && candidate.Kind == kind
                    && (candidate.StableId == stableId))
                {
                    attachment = candidate;
                    return true;
                }
            }
            return false;
        }

        private static bool TryReadBaseBasis(
            CharacterSocketBakeTrack track,
            CharacterSocketBakeBasisFrame source,
            out CharacterTestResolvedAuthorityPoseBasis basis)
        {
            basis = default;
            if (track == null || source == null || track.BasisQuantizationScale <= 0)
                return false;

            var candidate = new CharacterTestResolvedAuthorityPoseBasis(
                ToFixVector3(source.Right, track.BasisQuantizationScale),
                ToFixVector3(source.Up, track.BasisQuantizationScale),
                ToFixVector3(source.Forward, track.BasisQuantizationScale));
            return TryNormalizeBasis(candidate, out basis);
        }

        internal static bool TryNormalizeBasis(
            CharacterTestResolvedAuthorityPoseBasis source,
            out CharacterTestResolvedAuthorityPoseBasis basis)
        {
            basis = default;
            if (!source.Right.TryNormalize(out FixVector3 right)
                || !source.Up.TryNormalize(out FixVector3 up)
                || !source.Forward.TryNormalize(out FixVector3 forward)
                || Abs(right.Dot(up)) > BasisTolerance
                || Abs(right.Dot(forward)) > BasisTolerance
                || Abs(up.Dot(forward)) > BasisTolerance
                || Cross(right, up).Dot(forward) <= Fix64.Half)
            {
                return false;
            }

            basis = new CharacterTestResolvedAuthorityPoseBasis(right, up, forward);
            return true;
        }

        private static FixVector3 ToFixVector3(MxAnimationBakedVector3 value, int scale)
        {
            if (scale <= 0)
                return FixVector3.Zero;
            return new FixVector3(
                Fix64.FromRaw(value.X * Fix64.Scale / scale),
                Fix64.FromRaw(value.Y * Fix64.Scale / scale),
                Fix64.FromRaw(value.Z * Fix64.Scale / scale));
        }

        private static FixVector3 Cross(FixVector3 left, FixVector3 right)
        {
            return new FixVector3(
                left.Y * right.Z - left.Z * right.Y,
                left.Z * right.X - left.X * right.Z,
                left.X * right.Y - left.Y * right.X);
        }

        private static Fix64 Abs(Fix64 value)
        {
            return value < Fix64.Zero ? -value : value;
        }
    }
}
