using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabTimelineSessionIdentity : IEquatable<CharacterTestRuntimeAiLabTimelineSessionIdentity>
    {
        public CharacterTestRuntimeAiLabTimelineSessionIdentity(int generation, int sessionStartFrame, int frameLimit)
        {
            Generation = Math.Max(1, generation);
            SessionStartFrame = Math.Max(0, sessionStartFrame);
            FrameLimit = Math.Max(1, Math.Min(CharacterTestRuntimeAiPlannerDevTimeline.DefaultCapacity, frameLimit));
        }

        public int Generation { get; }
        public int SessionStartFrame { get; }
        public int FrameLimit { get; }
        public bool IsValid => Generation > 0 && FrameLimit > 0;

        public bool Equals(CharacterTestRuntimeAiLabTimelineSessionIdentity other)
        {
            return Generation == other.Generation
                && SessionStartFrame == other.SessionStartFrame
                && FrameLimit == other.FrameLimit;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestRuntimeAiLabTimelineSessionIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Generation;
                hash = (hash * 397) ^ SessionStartFrame;
                return (hash * 397) ^ FrameLimit;
            }
        }
    }

    internal enum CharacterTestRuntimeAiLabTimelineTrackKind
    {
        Perception = 0,
        Revision = Perception,
        Decision = 1,
        Action = 2,
        Result = 3,
        Combat = Result,
        Warnings = 4
    }

    internal enum CharacterTestRuntimeAiLabTimelineItemKind
    {
        Marker = 0,
        Span = 1,
        ExactFrameStack = 2,
        ZoomDensityAggregate = 3,
        Cluster = ZoomDensityAggregate
    }

    internal enum CharacterTestRuntimeAiLabTimelineSeverity
    {
        Info = 0,
        Success = 1,
        Warning = 2,
        Error = 3
    }

    internal enum CharacterTestRuntimeAiLabTimelineValidity
    {
        Valid = 0,
        NoFrames = 1,
        SessionMismatch = 2,
        EventAfterLatest = 3,
        UncapturedRetainedFrame = 4,
        EventSlotOverflow = 5,
        CorruptEventSlot = 6,
        MissingRuntimeFrameRange = 7,
        RevisionAfterLatest = 8,
        RevisionRecordMissing = 9,
        RevisionHistoryMismatch = 10
    }

    internal enum CharacterTestRuntimeAiLabTimelineItemProvenance
    {
        RuntimeFrameRecord = 0,
        TypedEvent = 1,
        PresentationAggregate = 2
    }

    internal enum CharacterTestRuntimeAiLabTimelineEventKind
    {
        None = -1,
        Command = 0,
        ActionStarted = 1,
        ActionEnded = 2,
        ActionEvent = 3,
        CombatHit = 4,
        Damage = 5,
        Warning = 6,
        BehaviorStageEntered = 7,
        BehaviorDetectorTriggered = 8,
        BehaviorTransitionPending = 9,
        BehaviorTransitionCommitted = 10,
        BehaviorCardEnded = 11
    }

    internal readonly struct CharacterTestRuntimeAiLabTimelineDomain
    {
        public CharacterTestRuntimeAiLabTimelineDomain(
            int sessionStartFrame,
            int retainedStartFrame,
            int latestFrame,
            int selectedFrame,
            int frameLimit,
            int retainedRecordCount)
        {
            SessionStartFrame = sessionStartFrame;
            RetainedStartFrame = retainedStartFrame;
            LatestFrame = latestFrame;
            SelectedFrame = selectedFrame;
            FrameLimit = Math.Max(1, frameLimit);
            RetainedRecordCount = Math.Max(0, retainedRecordCount);
            SessionEndFrame = SessionStartFrame + FrameLimit - 1;
        }

        public int SessionStartFrame { get; }
        public int RetainedStartFrame { get; }
        public int LatestFrame { get; }
        public int SelectedFrame { get; }
        public int FrameLimit { get; }
        public int RetainedRecordCount { get; }
        public int SessionEndFrame { get; }
        public bool HasFrames => RetainedRecordCount > 0 && LatestFrame >= RetainedStartFrame;
    }

    internal readonly struct CharacterTestRuntimeAiLabTimelineViewport
    {
        public CharacterTestRuntimeAiLabTimelineViewport(
            int startFrame,
            int endFrame,
            int zoomLevel,
            float pixelWidth,
            bool followLatest,
            CharacterTestRuntimeAiLabTimelineViewportMode mode = CharacterTestRuntimeAiLabTimelineViewportMode.Manual)
        {
            StartFrame = startFrame;
            EndFrame = Math.Max(startFrame, endFrame);
            ZoomLevel = zoomLevel;
            PixelWidth = Math.Max(1f, pixelWidth);
            FollowLatest = followLatest;
            Mode = mode;
        }

        public int StartFrame { get; }
        public int EndFrame { get; }
        public int ZoomLevel { get; }
        public float PixelWidth { get; }
        public bool FollowLatest { get; }
        public CharacterTestRuntimeAiLabTimelineViewportMode Mode { get; }
        public int FrameSpan => Math.Max(1, EndFrame - StartFrame + 1);
    }

    internal enum CharacterTestRuntimeAiLabTimelineViewportMode
    {
        FitRetained = 0,
        Manual = 1,
        ShowFrameLimit = 2
    }

    internal sealed class CharacterTestRuntimeAiLabTimelineItem
    {
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabTimelineItem> _members;

        public CharacterTestRuntimeAiLabTimelineItem(
            string stableKey,
            CharacterTestRuntimeAiLabTimelineTrackKind trackKind,
            CharacterTestRuntimeAiLabTimelineItemKind itemKind,
            CharacterTestRuntimeAiLabTimelineItemProvenance provenance,
            int startFrame,
            int endFrame,
            bool isOpen,
            int agentEntityId,
            int relatedEntityId,
            string category,
            CharacterTestRuntimeAiLabTimelineSeverity severity,
            string label,
            string tooltip,
            int count,
            string sourceTraceId,
            int sourceOrder,
            int value = 0,
            CharacterTestRuntimeAiLabTimelineItem[] members = null,
            int behaviorLoadoutId = 0,
            string behaviorEntryId = "",
            string behaviorStepId = "",
            CharacterTestRuntimeAiLabTimelineEventKind eventKind = CharacterTestRuntimeAiLabTimelineEventKind.None,
            int occurrenceRank = 0,
            int profileSlot = 0,
            int cardInstanceStartFrame = -1,
            int sessionGeneration = 0,
            long recordGeneration = 0L,
            long authorityHash = 0L,
            CharacterTestRuntimeAiLabPlannerLogicCommitKind plannerLogicKind =
                CharacterTestRuntimeAiLabPlannerLogicCommitKind.None,
            CharacterTestRuntimeAiLabCommandEnqueueKind commandEnqueueKind =
                CharacterTestRuntimeAiLabCommandEnqueueKind.NotRequested,
            CharacterTestRuntimeAiLabDecisionTraceCommandKind commandKind =
                CharacterTestRuntimeAiLabDecisionTraceCommandKind.None,
            CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind runtimeEffectKind =
                CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind.NotObserved,
            CharacterTestRuntimeAiPlannerActionKind fallbackActionKind =
                CharacterTestRuntimeAiPlannerActionKind.Wait,
            string reasonCode = "",
            bool hasPerceptionSnapshot = false,
            float perceptionStartDistance = 0f,
            float perceptionEndDistance = 0f,
            bool perceptionTargetExists = false,
            bool perceptionTargetVisible = false,
            bool perceptionLineOfSightBlocked = false,
            bool perceptionInAttackRange = false,
            bool perceptionTargetDead = false,
            RuntimeTraceIdentity sourceTraceIdentity = default,
            int behaviorEntryNumericId = 0,
            int behaviorStepIndex = -1,
            int behaviorEventIndex = -1,
            int behaviorTransitionIndex = -1,
            int behaviorTargetTimelineIndex = -1)
        {
            StableKey = stableKey ?? string.Empty;
            TrackKind = trackKind;
            ItemKind = itemKind;
            Provenance = provenance;
            StartFrame = startFrame;
            EndFrame = Math.Max(startFrame, endFrame);
            IsOpen = isOpen;
            AgentEntityId = agentEntityId;
            RelatedEntityId = relatedEntityId;
            Category = category ?? string.Empty;
            Severity = severity;
            Label = label ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
            Count = Math.Max(1, count);
            SourceTraceId = sourceTraceId ?? string.Empty;
            SourceTraceIdentity = sourceTraceIdentity;
            BehaviorEntryNumericId = behaviorEntryNumericId;
            BehaviorStepIndex = behaviorStepIndex;
            BehaviorEventIndex = behaviorEventIndex;
            BehaviorTransitionIndex = behaviorTransitionIndex;
            BehaviorTargetTimelineIndex = behaviorTargetTimelineIndex;
            SourceOrder = Math.Max(0, sourceOrder);
            Value = value;
            BehaviorLoadoutId = Math.Max(0, behaviorLoadoutId);
            BehaviorEntryId = behaviorEntryId ?? string.Empty;
            BehaviorStepId = behaviorStepId ?? string.Empty;
            EventKind = eventKind;
            OccurrenceRank = Math.Max(0, occurrenceRank);
            ProfileSlot = Math.Max(0, profileSlot);
            CardInstanceStartFrame = cardInstanceStartFrame;
            SessionGeneration = Math.Max(0, sessionGeneration);
            RecordGeneration = Math.Max(0L, recordGeneration);
            AuthorityHash = authorityHash;
            PlannerLogicKind = plannerLogicKind;
            CommandEnqueueKind = commandEnqueueKind;
            CommandKind = commandKind;
            RuntimeEffectKind = runtimeEffectKind;
            FallbackActionKind = fallbackActionKind;
            ReasonCode = reasonCode ?? string.Empty;
            HasPerceptionSnapshot = hasPerceptionSnapshot;
            PerceptionStartDistance = perceptionStartDistance;
            PerceptionEndDistance = perceptionEndDistance;
            PerceptionTargetExists = perceptionTargetExists;
            PerceptionTargetVisible = perceptionTargetVisible;
            PerceptionTargetDead = perceptionTargetDead;
            PerceptionLineOfSightBlocked = perceptionLineOfSightBlocked;
            PerceptionInAttackRange = perceptionInAttackRange;
            _members = Array.AsReadOnly(members ?? Array.Empty<CharacterTestRuntimeAiLabTimelineItem>());
        }

        public string StableKey { get; }
        public CharacterTestRuntimeAiLabTimelineTrackKind TrackKind { get; }
        public CharacterTestRuntimeAiLabTimelineItemKind ItemKind { get; }
        public CharacterTestRuntimeAiLabTimelineItemProvenance Provenance { get; }
        public int StartFrame { get; }
        public int EndFrame { get; }
        public bool IsOpen { get; }
        public int AgentEntityId { get; }
        public int RelatedEntityId { get; }
        public string Category { get; }
        public CharacterTestRuntimeAiLabTimelineSeverity Severity { get; }
        public string Label { get; }
        public string Tooltip { get; }
        public int Count { get; }
        public string SourceTraceId { get; }
        public RuntimeTraceIdentity SourceTraceIdentity { get; }
        public int BehaviorEntryNumericId { get; }
        public int BehaviorStepIndex { get; }
        public int BehaviorEventIndex { get; }
        public int BehaviorTransitionIndex { get; }
        public int BehaviorTargetTimelineIndex { get; }
        public int SourceOrder { get; }
        public int Value { get; }
        public int BehaviorLoadoutId { get; }
        public string BehaviorEntryId { get; }
        public string BehaviorStepId { get; }
        public CharacterTestRuntimeAiLabTimelineEventKind EventKind { get; }
        public int OccurrenceRank { get; }
        public int ProfileSlot { get; }
        public int CardInstanceStartFrame { get; }
        public int SessionGeneration { get; }
        public long RecordGeneration { get; }
        public long AuthorityHash { get; }
        public CharacterTestRuntimeAiLabPlannerLogicCommitKind PlannerLogicKind { get; }
        public CharacterTestRuntimeAiLabCommandEnqueueKind CommandEnqueueKind { get; }
        public CharacterTestRuntimeAiLabDecisionTraceCommandKind CommandKind { get; }
        public CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind RuntimeEffectKind { get; }
        public CharacterTestRuntimeAiPlannerActionKind FallbackActionKind { get; }
        public string ReasonCode { get; }
        public bool HasPerceptionSnapshot { get; }
        public float PerceptionStartDistance { get; }
        public float PerceptionEndDistance { get; }
        public bool PerceptionTargetExists { get; }
        public bool PerceptionTargetVisible { get; }
        public bool PerceptionTargetDead { get; }
        public bool PerceptionLineOfSightBlocked { get; }
        public bool PerceptionInAttackRange { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabTimelineItem> Members => _members;

        internal CharacterTestRuntimeAiLabTimelineItemLocator CreateLocator()
        {
            return new CharacterTestRuntimeAiLabTimelineItemLocator(this);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabTimelineItemLocator
    {
        internal CharacterTestRuntimeAiLabTimelineItemLocator(CharacterTestRuntimeAiLabTimelineItem item)
        {
            StableKey = item?.StableKey ?? string.Empty;
            TrackKind = item?.TrackKind ?? default;
            ItemKind = item?.ItemKind ?? default;
            EventKind = item?.EventKind ?? CharacterTestRuntimeAiLabTimelineEventKind.None;
            StartFrame = item?.StartFrame ?? 0;
            EndFrame = item?.EndFrame ?? 0;
            OccurrenceRank = item?.OccurrenceRank ?? 0;
            AgentEntityId = item?.AgentEntityId ?? 0;
            RelatedEntityId = item?.RelatedEntityId ?? 0;
            SourceTraceId = item?.SourceTraceId ?? string.Empty;
            BehaviorLoadoutId = item?.BehaviorLoadoutId ?? 0;
            BehaviorEntryId = item?.BehaviorEntryId ?? string.Empty;
            BehaviorStepId = item?.BehaviorStepId ?? string.Empty;
            ProfileSlot = item?.ProfileSlot ?? 0;
            CardInstanceStartFrame = item?.CardInstanceStartFrame ?? -1;
            SessionGeneration = item?.SessionGeneration ?? 0;
            RecordGeneration = item?.RecordGeneration ?? 0L;
            AuthorityHash = item?.AuthorityHash ?? 0L;
        }

        internal string StableKey { get; }
        internal CharacterTestRuntimeAiLabTimelineTrackKind TrackKind { get; }
        internal CharacterTestRuntimeAiLabTimelineItemKind ItemKind { get; }
        internal CharacterTestRuntimeAiLabTimelineEventKind EventKind { get; }
        internal int StartFrame { get; }
        internal int EndFrame { get; }
        internal int OccurrenceRank { get; }
        internal int AgentEntityId { get; }
        internal int RelatedEntityId { get; }
        internal string SourceTraceId { get; }
        internal int BehaviorLoadoutId { get; }
        internal string BehaviorEntryId { get; }
        internal string BehaviorStepId { get; }
        internal int ProfileSlot { get; }
        internal int CardInstanceStartFrame { get; }
        internal int SessionGeneration { get; }
        internal long RecordGeneration { get; }
        internal long AuthorityHash { get; }
        internal bool IsValid => !string.IsNullOrEmpty(StableKey) && SessionGeneration > 0;

        internal bool Matches(CharacterTestRuntimeAiLabTimelineItem item)
        {
            return item != null
                && string.Equals(StableKey, item.StableKey, StringComparison.Ordinal)
                && TrackKind == item.TrackKind
                && ItemKind == item.ItemKind
                && EventKind == item.EventKind
                && StartFrame == item.StartFrame
                && EndFrame == item.EndFrame
                && OccurrenceRank == item.OccurrenceRank
                && AgentEntityId == item.AgentEntityId
                && RelatedEntityId == item.RelatedEntityId
                && string.Equals(SourceTraceId, item.SourceTraceId, StringComparison.Ordinal)
                && BehaviorLoadoutId == item.BehaviorLoadoutId
                && string.Equals(BehaviorEntryId, item.BehaviorEntryId, StringComparison.Ordinal)
                && string.Equals(BehaviorStepId, item.BehaviorStepId, StringComparison.Ordinal)
                && ProfileSlot == item.ProfileSlot
                && CardInstanceStartFrame == item.CardInstanceStartFrame
                && SessionGeneration == item.SessionGeneration
                && RecordGeneration == item.RecordGeneration
                && AuthorityHash == item.AuthorityHash;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabTimelineTrackProjection
    {
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabTimelineItem> _items;

        public CharacterTestRuntimeAiLabTimelineTrackProjection(
            CharacterTestRuntimeAiLabTimelineTrackKind kind,
            string label,
            CharacterTestRuntimeAiLabTimelineItem[] items,
            bool available = true,
            string unavailableMessage = "")
        {
            Kind = kind;
            Label = label ?? string.Empty;
            Available = available;
            UnavailableMessage = unavailableMessage ?? string.Empty;
            _items = Array.AsReadOnly(items ?? Array.Empty<CharacterTestRuntimeAiLabTimelineItem>());
        }

        public CharacterTestRuntimeAiLabTimelineTrackKind Kind { get; }
        public string Label { get; }
        public bool Available { get; }
        public string UnavailableMessage { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabTimelineItem> Items => _items;
    }

    internal sealed class CharacterTestRuntimeAiLabTimelineProjection
    {
        private readonly ReadOnlyCollection<CharacterTestRuntimeAiLabTimelineTrackProjection> _tracks;

        public CharacterTestRuntimeAiLabTimelineProjection(
            long contentRevision,
            CharacterTestRuntimeAiLabTimelineDomain domain,
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            CharacterTestRuntimeAiLabTimelineTrackProjection[] tracks,
            string statusMessage = "",
            CharacterTestRuntimeAiLabTimelineValidity validity = CharacterTestRuntimeAiLabTimelineValidity.Valid,
            int diagnosticFrame = 0,
            int diagnosticValue = 0)
        {
            ContentRevision = contentRevision;
            Domain = domain;
            Viewport = viewport;
            StatusMessage = statusMessage ?? string.Empty;
            Validity = validity;
            DiagnosticFrame = Math.Max(0, diagnosticFrame);
            DiagnosticValue = Math.Max(0, diagnosticValue);
            _tracks = Array.AsReadOnly(tracks ?? Array.Empty<CharacterTestRuntimeAiLabTimelineTrackProjection>());
        }

        public long ContentRevision { get; }
        public CharacterTestRuntimeAiLabTimelineDomain Domain { get; }
        public CharacterTestRuntimeAiLabTimelineViewport Viewport { get; }
        public string StatusMessage { get; }
        public CharacterTestRuntimeAiLabTimelineValidity Validity { get; }
        public int DiagnosticFrame { get; }
        public int DiagnosticValue { get; }
        public bool IsComplete => Validity == CharacterTestRuntimeAiLabTimelineValidity.Valid;
        public IReadOnlyList<CharacterTestRuntimeAiLabTimelineTrackProjection> Tracks => _tracks;

        public bool TryFindItem(string stableKey, out CharacterTestRuntimeAiLabTimelineItem item)
        {
            for (int trackIndex = 0; trackIndex < _tracks.Count; trackIndex++)
            {
                IReadOnlyList<CharacterTestRuntimeAiLabTimelineItem> items = _tracks[trackIndex].Items;
                for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                {
                    CharacterTestRuntimeAiLabTimelineItem candidate = items[itemIndex];
                    if (!string.Equals(candidate.StableKey, stableKey, StringComparison.Ordinal))
                    {
                        for (int memberIndex = 0; memberIndex < candidate.Members.Count; memberIndex++)
                        {
                            CharacterTestRuntimeAiLabTimelineItem member = candidate.Members[memberIndex];
                            if (!string.Equals(member.StableKey, stableKey, StringComparison.Ordinal))
                                continue;
                            item = member;
                            return true;
                        }
                        continue;
                    }
                    item = candidate;
                    return true;
                }
            }

            item = null;
            return false;
        }
    }
}
