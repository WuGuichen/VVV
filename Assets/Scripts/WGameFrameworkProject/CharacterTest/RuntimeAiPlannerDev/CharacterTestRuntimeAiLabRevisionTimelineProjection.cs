using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabRevisionChange
    {
        internal CharacterTestRuntimeAiLabRevisionChange(
            int frame,
            int profileSlot,
            int revision,
            long authorityHash,
            string groupDraftId)
        {
            Frame = Math.Max(0, frame);
            ProfileSlot = Math.Max(0, profileSlot);
            Revision = Math.Max(1, revision);
            AuthorityHash = authorityHash;
            GroupDraftId = groupDraftId ?? string.Empty;
        }

        internal int Frame { get; }
        internal int ProfileSlot { get; }
        internal int Revision { get; }
        internal long AuthorityHash { get; }
        internal string GroupDraftId { get; }
    }

    internal readonly struct CharacterTestRuntimeAiLabRevisionFrame
    {
        internal CharacterTestRuntimeAiLabRevisionFrame(
            int frame,
            long recordGeneration,
            long authorityHash,
            long appliedRevisionHash,
            int changeStart,
            int changeCount)
        {
            Frame = Math.Max(0, frame);
            RecordGeneration = Math.Max(1L, recordGeneration);
            AuthorityHash = authorityHash;
            AppliedRevisionHash = appliedRevisionHash;
            ChangeStart = Math.Max(0, changeStart);
            ChangeCount = Math.Max(0, changeCount);
        }

        internal int Frame { get; }
        internal long RecordGeneration { get; }
        internal long AuthorityHash { get; }
        internal long AppliedRevisionHash { get; }
        internal int ChangeStart { get; }
        internal int ChangeCount { get; }
    }

    /// <summary>Immutable low-frequency adapter from Apply history to retained RuntimeFrame truth.</summary>
    /// <remarks>LowFreqAlloc: rebuilt only when the timeline projection is invalidated.</remarks>
    internal sealed class CharacterTestRuntimeAiLabRevisionProjectionInput
    {
        private readonly CharacterTestRuntimeAiLabRevisionFrame[] _frames;
        private readonly CharacterTestRuntimeAiLabRevisionChange[] _changes;

        internal CharacterTestRuntimeAiLabRevisionProjectionInput(
            CharacterTestRuntimeAiLabTimelineSessionIdentity sessionIdentity,
            long contentRevision,
            CharacterTestRuntimeAiLabRevisionFrame[] frames,
            CharacterTestRuntimeAiLabRevisionChange[] changes,
            CharacterTestRuntimeAiLabTimelineValidity validity,
            int diagnosticFrame,
            string diagnosticMessage)
            : this(
                sessionIdentity,
                contentRevision,
                frames,
                frames?.Length ?? 0,
                changes,
                changes?.Length ?? 0,
                validity,
                diagnosticFrame,
                diagnosticMessage)
        {
        }

        internal CharacterTestRuntimeAiLabRevisionProjectionInput(
            CharacterTestRuntimeAiLabTimelineSessionIdentity sessionIdentity,
            long contentRevision,
            CharacterTestRuntimeAiLabRevisionFrame[] frames,
            int frameCount,
            CharacterTestRuntimeAiLabRevisionChange[] changes,
            int changeCount,
            CharacterTestRuntimeAiLabTimelineValidity validity,
            int diagnosticFrame,
            string diagnosticMessage)
        {
            SessionIdentity = sessionIdentity;
            ContentRevision = contentRevision;
            _frames = frames ?? Array.Empty<CharacterTestRuntimeAiLabRevisionFrame>();
            _changes = changes ?? Array.Empty<CharacterTestRuntimeAiLabRevisionChange>();
            FrameCount = Math.Max(0, Math.Min(frameCount, _frames.Length));
            ChangeCount = Math.Max(0, Math.Min(changeCount, _changes.Length));
            Validity = validity;
            DiagnosticFrame = Math.Max(0, diagnosticFrame);
            DiagnosticMessage = diagnosticMessage ?? string.Empty;
        }

        internal CharacterTestRuntimeAiLabTimelineSessionIdentity SessionIdentity { get; }
        internal long ContentRevision { get; }
        internal CharacterTestRuntimeAiLabTimelineValidity Validity { get; }
        internal int DiagnosticFrame { get; }
        internal string DiagnosticMessage { get; }
        internal int FrameCount { get; }
        internal int ChangeCount { get; }
        internal CharacterTestRuntimeAiLabRevisionFrame FrameAt(int index) => _frames[index];
        internal CharacterTestRuntimeAiLabRevisionChange ChangeAt(int index) => _changes[index];
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestRuntimeAiLabRevisionFrame[] _revisionProjectionFrames =
            new CharacterTestRuntimeAiLabRevisionFrame[DefaultRuntimeAiLabFrameLimit];
        private CharacterTestRuntimeAiLabRevisionChange[] _revisionProjectionChanges =
            new CharacterTestRuntimeAiLabRevisionChange[DefaultRuntimeAiLabFrameLimit * 6];

        private CharacterTestRuntimeAiLabRevisionProjectionInput BuildRevisionProjectionInput()
        {
            CharacterTestRuntimeAiLabTimelineSessionIdentity identity = _timeline?.SessionIdentity ?? default;
            if (_timeline == null || _timeline.Count <= 0 || !identity.IsValid)
            {
                return new CharacterTestRuntimeAiLabRevisionProjectionInput(
                    identity,
                    0L,
                    Array.Empty<CharacterTestRuntimeAiLabRevisionFrame>(),
                    0,
                    Array.Empty<CharacterTestRuntimeAiLabRevisionChange>(),
                    0,
                    CharacterTestRuntimeAiLabTimelineValidity.NoFrames,
                    0,
                    "尚未捕获 revision 对应的 RuntimeFrame。");
            }

            int retainedStart = _timeline.Records[0].Frame;
            int latest = _timeline.LatestFrame;
            EnsureRevisionProjectionCapacity(_timeline.Count, Math.Max(_frameLimit, _appliedRevisionEvents.Count));
            int retainedChangeCount = 0;
            CharacterTestRuntimeAiLabTimelineValidity validity = CharacterTestRuntimeAiLabTimelineValidity.Valid;
            int diagnosticFrame = 0;
            string diagnostic = string.Empty;
            for (int i = 0; i < _appliedRevisionEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabAppliedRevision applied = _appliedRevisionEvents[i];
                if (applied == null)
                    continue;
                if (applied.SessionGeneration != identity.Generation || !applied.HasBoundRuntimeFrame)
                {
                    validity = CharacterTestRuntimeAiLabTimelineValidity.RevisionHistoryMismatch;
                    diagnosticFrame = applied.ApplicationFrame;
                    diagnostic = "Revision Apply 不属于当前 RuntimeFrame session/capture boundary。";
                    break;
                }
                if (applied.ApplicationFrame > latest)
                {
                    validity = CharacterTestRuntimeAiLabTimelineValidity.RevisionAfterLatest;
                    diagnosticFrame = applied.ApplicationFrame;
                    diagnostic = "检测到 latest 之后的 revision Apply 记录。";
                    break;
                }
                if (applied.ApplicationFrame < retainedStart)
                    continue;
                if (!_timeline.TryGetFrame(applied.ApplicationFrame, out CharacterTestRuntimeAiPlannerDevTimelineRecord boundaryRecord))
                {
                    validity = CharacterTestRuntimeAiLabTimelineValidity.RevisionRecordMissing;
                    diagnosticFrame = applied.ApplicationFrame;
                    diagnostic = "Revision Apply 缺少对应的 retained RuntimeFrame。";
                    break;
                }
                long appliedHash = ComputeAppliedBehaviorRevisionHash(applied.ApplicationFrame);
                if (!applied.MatchesRuntimeFrame(identity, boundaryRecord, appliedHash))
                {
                    validity = CharacterTestRuntimeAiLabTimelineValidity.RevisionHistoryMismatch;
                    diagnosticFrame = applied.ApplicationFrame;
                    diagnostic = "Revision Apply metadata 与冻结的 RuntimeFrame boundary 不一致。";
                    break;
                }
                retainedChangeCount++;
            }

            CharacterTestRuntimeAiLabRevisionChange[] changes = _revisionProjectionChanges;
            CharacterTestRuntimeAiLabRevisionFrame[] frames = _revisionProjectionFrames;
            int changeOffset = 0;
            long contentRevision = _timeline.ContentRevision;
            for (int recordIndex = 0; recordIndex < _timeline.Count; recordIndex++)
            {
                CharacterTestRuntimeAiPlannerDevTimelineRecord record = _timeline.Records[recordIndex];
                int changeStart = changeOffset;
                for (int appliedIndex = 0; appliedIndex < _appliedRevisionEvents.Count; appliedIndex++)
                {
                    CharacterTestRuntimeAiLabAppliedRevision applied = _appliedRevisionEvents[appliedIndex];
                    if (applied == null || applied.ApplicationFrame != record.Frame || changeOffset >= changes.Length)
                        continue;
                    CharacterTestRuntimeAiLabCandidateRevision revision = applied.Revision;
                    changes[changeOffset++] = new CharacterTestRuntimeAiLabRevisionChange(
                        record.Frame,
                        applied.ProfileSlot,
                        revision?.Revision ?? 1,
                        applied.AuthorityHash,
                        revision?.GroupDraftId);
                    unchecked
                    {
                        contentRevision = contentRevision * 397L ^ record.Frame;
                        contentRevision = contentRevision * 397L ^ applied.ProfileSlot;
                        contentRevision = contentRevision * 397L ^ (revision?.Revision ?? 0);
                        contentRevision = contentRevision * 397L ^ applied.AuthorityHash;
                        contentRevision = contentRevision * 397L ^ applied.SessionGeneration;
                        contentRevision = contentRevision * 397L ^ applied.BoundaryRecordGeneration;
                        contentRevision = contentRevision * 397L ^ applied.BoundaryAuthorityHash;
                        contentRevision = contentRevision * 397L ^ applied.AppliedRevisionHash;
                    }
                }

                long appliedRevisionHash = ComputeAppliedBehaviorRevisionHash(record.Frame);
                frames[recordIndex] = new CharacterTestRuntimeAiLabRevisionFrame(
                    record.Frame,
                    record.RecordGeneration,
                    record.StateHash,
                    appliedRevisionHash,
                    changeStart,
                    changeOffset - changeStart);
                unchecked
                {
                    contentRevision = contentRevision * 397L ^ record.RecordGeneration;
                    contentRevision = contentRevision * 397L ^ appliedRevisionHash;
                }
            }

            Array.Sort(changes, 0, retainedChangeCount, RevisionChangeComparer.Instance);
            RebuildRevisionChangeRanges(frames, _timeline.Count, changes, retainedChangeCount);

            return new CharacterTestRuntimeAiLabRevisionProjectionInput(
                identity,
                contentRevision,
                frames,
                _timeline.Count,
                changes,
                retainedChangeCount,
                validity,
                diagnosticFrame,
                diagnostic);
        }

        internal static void RebuildRevisionChangeRanges(
            CharacterTestRuntimeAiLabRevisionFrame[] frames,
            int frameCount,
            CharacterTestRuntimeAiLabRevisionChange[] changes,
            int retainedChangeCount)
        {
            int safeFrameCount = Math.Max(0, Math.Min(frameCount, frames?.Length ?? 0));
            int safeChangeCount = Math.Max(0, Math.Min(retainedChangeCount, changes?.Length ?? 0));
            int sortedChangeIndex = 0;
            for (int recordIndex = 0; recordIndex < safeFrameCount; recordIndex++)
            {
                CharacterTestRuntimeAiLabRevisionFrame frame = frames[recordIndex];
                while (sortedChangeIndex < safeChangeCount && changes[sortedChangeIndex].Frame < frame.Frame)
                    sortedChangeIndex++;
                int sortedStart = sortedChangeIndex;
                while (sortedChangeIndex < safeChangeCount && changes[sortedChangeIndex].Frame == frame.Frame)
                    sortedChangeIndex++;
                frames[recordIndex] = new CharacterTestRuntimeAiLabRevisionFrame(
                    frame.Frame,
                    frame.RecordGeneration,
                    frame.AuthorityHash,
                    frame.AppliedRevisionHash,
                    sortedStart,
                    sortedChangeIndex - sortedStart);
            }
        }

        private void EnsureRevisionProjectionCapacity(int frameCount, int changeCount)
        {
            if (_revisionProjectionFrames.Length < frameCount)
                Array.Resize(ref _revisionProjectionFrames, Math.Max(frameCount, _frameLimit));
            int requiredChanges = Math.Max(changeCount, _frameLimit * 6);
            if (_revisionProjectionChanges.Length < requiredChanges)
                Array.Resize(ref _revisionProjectionChanges, requiredChanges);
        }

        private static int CompareRevisionChanges(
            CharacterTestRuntimeAiLabRevisionChange left,
            CharacterTestRuntimeAiLabRevisionChange right)
        {
            int value = left.Frame.CompareTo(right.Frame);
            if (value != 0)
                return value;
            value = left.ProfileSlot.CompareTo(right.ProfileSlot);
            if (value != 0)
                return value;
            value = left.Revision.CompareTo(right.Revision);
            if (value != 0)
                return value;
            value = left.AuthorityHash.CompareTo(right.AuthorityHash);
            return value != 0 ? value : string.CompareOrdinal(left.GroupDraftId, right.GroupDraftId);
        }

        private sealed class RevisionChangeComparer : IComparer<CharacterTestRuntimeAiLabRevisionChange>
        {
            internal static readonly RevisionChangeComparer Instance = new RevisionChangeComparer();

            public int Compare(
                CharacterTestRuntimeAiLabRevisionChange left,
                CharacterTestRuntimeAiLabRevisionChange right)
            {
                return CompareRevisionChanges(left, right);
            }
        }
    }
}
