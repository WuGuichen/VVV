using System;
using System.Globalization;
using System.Text;
using System.Collections.Generic;
using MxFramework.Runtime;
using MxFramework.Network;

namespace MxFramework.Game.CharacterTest
{
    internal static class TimelineHashFields
    {
        internal const int ItemTraceIdentity = 37;
    }

    internal static class ReplayCausalHashFields
    {
        internal const int ReplayFrame = 1, LiveFrame = 2, Agent = 3, Session = 4, Revision = 5, Authority = 6;
        internal const int PreviousCard = 7, PreviousStage = 8, PreviousAction = 9, PreviousPending = 10;
        internal const int CommitLogic = 11, CommitEnqueue = 12, CommitEffect = 13, CommitEvidenceFrame = 14;
        internal const int CommitPlannerFrame = 15, CommitCommandFrame = 16, CommitEffectFrame = 17;
        internal const int CommitCard = 18, CommitStage = 19, CommitAction = 20, CommitTransition = 21;
        internal const int CommitKind = 22, CommitActionId = 23, CommitReason = 24, CommitPlannerReason = 25;
        internal const int CommitCommandReason = 26, CommitEffectReason = 27, CommitEffectAction = 28;
        internal const int FactCount = 29, FactIndex = 30, FactId = 31, FactDomain = 32, FactPlanner = 33;
        internal const int FactLogic = 34, FactValue = 35, FactPrevious = 36, FactSource = 37;
        internal const int FactObservedFrame = 38, FactSourceFrame = 39, FactSourceGeneration = 40;
        internal const int FactChangedFrame = 41, FactAuthority = 42, FactLayers = 43, FactBaseline = 44;
        internal const int FactSourceUnknown = 45, FactDerivation = 46, FactDependencyCount = 47, FactDependency = 48;
        internal const int CandidateCount = 49, CandidateIndex = 50, CandidateId = 51, CandidateLayer = 52;
        internal const int CandidateState = 53, CandidateScoreAvailable = 54, CandidateScore = 55;
        internal const int CandidatePriorityAvailable = 56, CandidatePriority = 57, CandidateCostAvailable = 58;
        internal const int CandidateCost = 59, CandidateRank = 60, CandidateReason = 61, CandidateFactCount = 62;
        internal const int CandidateFact = 63, OverflowRegion = 64, OverflowActual = 65, OverflowLimit = 66;
        internal const int TraceValueKind = 67, TraceValueBool = 68, TraceValueInteger = 69;
        internal const int TraceValueNumber = 70, TraceValueText = 71;
        internal const int TimelineRevision = 80, TimelineSessionStart = 81, TimelineRetainedStart = 82;
        internal const int TimelineLatest = 83, TimelineTrackCount = 84, TimelineTrackIndex = 85;
        internal const int TimelineTrackKind = 86, TimelineItemCount = 87, TimelineItemIndex = 88;
        internal const int TimelineItemKey = 89, TimelineItemKind = 90, TimelineItemProvenance = 91;
        internal const int TimelineItemStart = 92, TimelineItemEnd = 93, TimelineItemEvent = 94;
        internal const int TimelineItemOccurrence = 95, TimelineItemAgent = 96, TimelineItemRelated = 97;
        internal const int TimelineItemTraceKind = 98, TimelineItemTraceSource = 99;
        internal const int TimelineItemTraceExecution = 100, TimelineItemTraceSequence = 101;
        internal const int TimelineItemBehaviorEntry = 102, TimelineItemBehaviorStep = 103;
        internal const int TimelineItemSession = 104, TimelineItemRecord = 105, TimelineItemAuthority = 106;
        internal const int TimelineCausalCount = 107, TimelineCausalIndex = 108, TimelineCausalFrame = 109;
        internal const int TimelineCausalAgent = 110, TimelineCausalContent = 111;
    }

    internal readonly struct CharacterTestRuntimeAiLabReplayValidationWorkspaceSnapshot
    {
        internal CharacterTestRuntimeAiLabReplayValidationWorkspaceSnapshot(
            bool hasCapture,
            CharacterTestReplayValidationState state,
            string code,
            string summary,
            int startFrame,
            int endFrame,
            int frameCount,
            int comparedFrames,
            int recordedFrameCommandCount,
            int authoritativePacketCommandCount,
            long authorityRecordHash,
            bool hasFailureFrame,
            int failureFrame,
            long expectedHash,
            long actualHash,
            long operationId,
            CharacterTestRuntimeAiLabTimelineProjection timelineProjection,
            string recordMetadata,
            string bootstrapIdentity,
            string authorityIdentity,
            string resultEvidence,
            int captureGeneration,
            long timelineEvidenceHash,
            string selectedVisualStableKey,
            bool canLocateDivergence,
            bool canOpenFacts,
            bool canOpenBehaviorCard)
        {
            HasCapture = hasCapture;
            State = state;
            Code = code ?? CharacterTestReplayValidationCodes.None;
            Summary = summary ?? string.Empty;
            StartFrame = startFrame;
            EndFrame = endFrame;
            FrameCount = frameCount;
            ComparedFrames = comparedFrames;
            RecordedFrameCommandCount = recordedFrameCommandCount;
            AuthoritativePacketCommandCount = authoritativePacketCommandCount;
            AuthorityRecordHash = authorityRecordHash;
            HasFailureFrame = hasFailureFrame;
            FailureFrame = failureFrame;
            ExpectedHash = expectedHash;
            ActualHash = actualHash;
            OperationId = operationId;
            TimelineProjection = timelineProjection;
            RecordMetadata = recordMetadata ?? string.Empty;
            BootstrapIdentity = bootstrapIdentity ?? string.Empty;
            AuthorityIdentity = authorityIdentity ?? string.Empty;
            ResultEvidence = resultEvidence ?? string.Empty;
            CaptureGeneration = captureGeneration;
            TimelineEvidenceHash = timelineEvidenceHash;
            SelectedVisualStableKey = selectedVisualStableKey ?? string.Empty;
            CanLocateDivergence = canLocateDivergence;
            CanOpenFacts = canOpenFacts;
            CanOpenBehaviorCard = canOpenBehaviorCard;
        }

        internal bool HasCapture { get; }
        internal CharacterTestReplayValidationState State { get; }
        internal string Code { get; }
        internal string Summary { get; }
        internal int StartFrame { get; }
        internal int EndFrame { get; }
        internal int FrameCount { get; }
        internal int ComparedFrames { get; }
        internal int RecordedFrameCommandCount { get; }
        internal int AuthoritativePacketCommandCount { get; }
        internal long AuthorityRecordHash { get; }
        internal bool HasFailureFrame { get; }
        internal int FailureFrame { get; }
        internal long ExpectedHash { get; }
        internal long ActualHash { get; }
        internal long OperationId { get; }
        internal CharacterTestRuntimeAiLabTimelineProjection TimelineProjection { get; }
        internal string RecordMetadata { get; }
        internal string BootstrapIdentity { get; }
        internal string AuthorityIdentity { get; }
        internal string ResultEvidence { get; }
        internal int CaptureGeneration { get; }
        internal long TimelineEvidenceHash { get; }
        internal string SelectedVisualStableKey { get; }
        internal bool CanLocateDivergence { get; }
        internal bool CanOpenFacts { get; }
        internal bool CanOpenBehaviorCard { get; }
        internal bool IsBusy => State == CharacterTestReplayValidationState.InspectingRecord
            || State == CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap
            || State == CharacterTestReplayValidationState.RestoringGameplayBootstrap
            || State == CharacterTestReplayValidationState.ComparingFrames;
    }

    internal enum CharacterTestRuntimeAiLabReplayEvidenceTargetKind
    {
        RuntimeFrame = 0,
        Divergence = 1,
        Facts = 2,
        BehaviorCard = 3
    }

    internal readonly struct CharacterTestRuntimeAiLabReplayEvidenceLocator
    {
        internal CharacterTestRuntimeAiLabReplayEvidenceLocator(
            CharacterTestRuntimeAiLabReplayEvidenceTargetKind targetKind,
            int captureGeneration,
            long authorityRecordHash,
            long timelineEvidenceHash,
            int replayFrame,
            int liveFrame,
            CharacterTestRuntimeAiLabTimelineItemLocator replayItem,
            CharacterTestRuntimeAiLabTimelineItemLocator liveItem,
            int agentEntityId,
            int behaviorLoadoutId,
            string behaviorEntryId,
            CharacterTestRuntimeAiLabLogicElementKind behaviorElementKind,
            string behaviorElementStableId)
        {
            TargetKind = targetKind;
            CaptureGeneration = captureGeneration;
            AuthorityRecordHash = authorityRecordHash;
            TimelineEvidenceHash = timelineEvidenceHash;
            ReplayFrame = replayFrame;
            LiveFrame = liveFrame;
            ReplayItem = replayItem;
            LiveItem = liveItem;
            AgentEntityId = agentEntityId;
            BehaviorLoadoutId = behaviorLoadoutId;
            BehaviorEntryId = behaviorEntryId ?? string.Empty;
            BehaviorElementKind = behaviorElementKind;
            BehaviorElementStableId = behaviorElementStableId ?? string.Empty;
        }

        internal CharacterTestRuntimeAiLabReplayEvidenceTargetKind TargetKind { get; }
        internal int CaptureGeneration { get; }
        internal long AuthorityRecordHash { get; }
        internal long TimelineEvidenceHash { get; }
        internal int ReplayFrame { get; }
        internal int LiveFrame { get; }
        internal CharacterTestRuntimeAiLabTimelineItemLocator ReplayItem { get; }
        internal CharacterTestRuntimeAiLabTimelineItemLocator LiveItem { get; }
        internal int AgentEntityId { get; }
        internal int BehaviorLoadoutId { get; }
        internal string BehaviorEntryId { get; }
        internal CharacterTestRuntimeAiLabLogicElementKind BehaviorElementKind { get; }
        internal string BehaviorElementStableId { get; }
        internal bool IsValid => CaptureGeneration > 0
            && AuthorityRecordHash != 0L
            && TimelineEvidenceHash != 0L
            && ReplayFrame >= 0
            && LiveFrame > ReplayFrame;
    }

    internal readonly struct CharacterTestRuntimeAiLabReplayReturnToken
    {
        internal CharacterTestRuntimeAiLabReplayReturnToken(
            int captureGeneration,
            long authorityRecordHash,
            long timelineEvidenceHash,
            int replayFrame,
            string selectedVisualStableKey)
        {
            CaptureGeneration = captureGeneration;
            AuthorityRecordHash = authorityRecordHash;
            TimelineEvidenceHash = timelineEvidenceHash;
            ReplayFrame = replayFrame;
            SelectedVisualStableKey = selectedVisualStableKey ?? string.Empty;
        }

        internal int CaptureGeneration { get; }
        internal long AuthorityRecordHash { get; }
        internal long TimelineEvidenceHash { get; }
        internal int ReplayFrame { get; }
        internal string SelectedVisualStableKey { get; }
        internal bool IsValid => CaptureGeneration > 0
            && AuthorityRecordHash != 0L
            && TimelineEvidenceHash != 0L;
    }

    /// <summary>
    /// Frozen, presentation-only causal evidence captured with a Replay record.
    /// </summary>
    /// <remarks>
    /// AllocByDesign: created only by the explicit Replay freeze command. The private trace arena is
    /// never exposed for mutation, so Facts can remain readable after the live timeline is evicted.
    /// </remarks>
    internal sealed class CharacterTestRuntimeAiLabReplayCausalSnapshot
    {
        private readonly CharacterTestRuntimeAiLabDecisionTraceStore _traceStore;
        private readonly CharacterTestRuntimeAiLabDecisionTraceHandle _traceHandle;

        internal CharacterTestRuntimeAiLabReplayCausalSnapshot(
            int captureGeneration,
            long authorityRecordHash,
            long timelineEvidenceHash,
            int replayFrame,
            int liveFrame,
            int latestFrame,
            int retainedStartFrame,
            int agentEntityId,
            long sessionGeneration,
            long appliedRevisionHash,
            long authorityHash,
            CharacterTestRuntimeAiLabDecisionTraceStore traceStore,
            CharacterTestRuntimeAiLabDecisionTraceHandle traceHandle)
        {
            CaptureGeneration = captureGeneration;
            AuthorityRecordHash = authorityRecordHash;
            TimelineEvidenceHash = timelineEvidenceHash;
            ReplayFrame = replayFrame;
            LiveFrame = liveFrame;
            LatestFrame = latestFrame;
            RetainedStartFrame = retainedStartFrame;
            AgentEntityId = agentEntityId;
            SessionGeneration = sessionGeneration;
            AppliedRevisionHash = appliedRevisionHash;
            AuthorityHash = authorityHash;
            _traceStore = traceStore;
            _traceHandle = traceHandle;
            EvidenceContentHash = ComputeContentHash();
        }

        internal int CaptureGeneration { get; }
        internal long AuthorityRecordHash { get; }
        internal long TimelineEvidenceHash { get; }
        internal int ReplayFrame { get; }
        internal int LiveFrame { get; }
        internal int LatestFrame { get; }
        internal int RetainedStartFrame { get; }
        internal int AgentEntityId { get; }
        internal long SessionGeneration { get; }
        internal long AppliedRevisionHash { get; }
        internal long AuthorityHash { get; }
        internal long EvidenceContentHash { get; }

        internal bool TryGetTrace(out CharacterTestRuntimeAiLabDecisionTraceAgentView trace)
        {
            trace = default;
            return _traceStore != null
                && _traceStore.TryGetAgent(_traceHandle, AgentEntityId, out trace);
        }

        internal bool Matches(
            int captureGeneration,
            long authorityRecordHash,
            long timelineEvidenceHash)
        {
            return captureGeneration == CaptureGeneration
                && authorityRecordHash == AuthorityRecordHash
                && timelineEvidenceHash == TimelineEvidenceHash;
        }

        internal CharacterTestRuntimeAiLabReplayCausalSnapshot BindTimelineEvidenceHash(
            long timelineEvidenceHash)
        {
            return new CharacterTestRuntimeAiLabReplayCausalSnapshot(
                CaptureGeneration,
                AuthorityRecordHash,
                timelineEvidenceHash,
                ReplayFrame,
                LiveFrame,
                LatestFrame,
                RetainedStartFrame,
                AgentEntityId,
                SessionGeneration,
                AppliedRevisionHash,
                AuthorityHash,
                _traceStore,
                _traceHandle);
        }

        private long ComputeContentHash()
        {
            if (!TryGetTrace(out CharacterTestRuntimeAiLabDecisionTraceAgentView trace))
                return 0L;
            var hash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerLabFrameState, RuntimeHashSchemaIds.Version1);
            hash.AddInt(ReplayCausalHashFields.ReplayFrame, ReplayFrame);
            hash.AddInt(ReplayCausalHashFields.LiveFrame, LiveFrame);
            hash.AddInt(ReplayCausalHashFields.Agent, AgentEntityId);
            hash.AddLong(ReplayCausalHashFields.Session, SessionGeneration);
            hash.AddLong(ReplayCausalHashFields.Revision, AppliedRevisionHash);
            hash.AddLong(ReplayCausalHashFields.Authority, AuthorityHash);
            CharacterTestRuntimeAiLabCommittedStateTrace previous = trace.PreviousCommitted;
            previous.CardIdentity.Contribute(hash, ReplayCausalHashFields.PreviousCard);
            previous.StageIdentity.Contribute(hash, ReplayCausalHashFields.PreviousStage);
            previous.MainActionIdentity.Contribute(hash, ReplayCausalHashFields.PreviousAction);
            previous.PendingTransitionIdentity.Contribute(hash, ReplayCausalHashFields.PreviousPending);
            CharacterTestRuntimeAiLabCommitTrace commit = trace.Commit;
            hash.AddInt(ReplayCausalHashFields.CommitLogic, (int)commit.PlannerLogicKind);
            hash.AddInt(ReplayCausalHashFields.CommitEnqueue, (int)commit.CommandEnqueueKind);
            hash.AddInt(ReplayCausalHashFields.CommitEffect, (int)commit.RuntimeEffectKind);
            hash.AddInt(ReplayCausalHashFields.CommitEvidenceFrame, commit.EvidenceFrame);
            hash.AddInt(ReplayCausalHashFields.CommitPlannerFrame, commit.PlannerEvidenceFrame);
            hash.AddInt(ReplayCausalHashFields.CommitCommandFrame, commit.CommandEvidenceFrame);
            hash.AddInt(ReplayCausalHashFields.CommitEffectFrame, commit.RuntimeEffectEvidenceFrame);
            commit.CardIdentity.Contribute(hash, ReplayCausalHashFields.CommitCard);
            commit.StageIdentity.Contribute(hash, ReplayCausalHashFields.CommitStage);
            commit.MainActionIdentity.Contribute(hash, ReplayCausalHashFields.CommitAction);
            commit.TransitionIdentity.Contribute(hash, ReplayCausalHashFields.CommitTransition);
            hash.AddInt(ReplayCausalHashFields.CommitKind, (int)commit.CommandKind);
            hash.AddInt(ReplayCausalHashFields.CommitActionId, commit.CommandActionId);
            hash.AddInt(ReplayCausalHashFields.CommitReason, commit.ReasonCode);
            hash.AddInt(ReplayCausalHashFields.CommitPlannerReason, commit.PlannerReasonCode);
            hash.AddInt(ReplayCausalHashFields.CommitCommandReason, commit.CommandReasonCode);
            hash.AddInt(ReplayCausalHashFields.CommitEffectReason, commit.RuntimeEffectReasonCode);
            commit.RuntimeEffectActionIdentity.Contribute(hash, ReplayCausalHashFields.CommitEffectAction);
            hash.AddInt(ReplayCausalHashFields.FactCount, trace.Facts.Count);
            for (int i = 0; i < trace.Facts.Count; i++)
            {
                CharacterTestRuntimeAiLabFactObservationTrace fact = trace.Facts[i];
                hash.AddInt(ReplayCausalHashFields.FactIndex, i);
                fact.Identity.Contribute(hash, ReplayCausalHashFields.FactId);
                hash.AddInt(ReplayCausalHashFields.FactDomain, (int)fact.Domain);
                hash.AddInt(ReplayCausalHashFields.FactPlanner, (int)fact.PlannerFact);
                hash.AddInt(ReplayCausalHashFields.FactLogic, (int)fact.Fact);
                AddTraceValue(hash, ReplayCausalHashFields.FactValue, fact.Value);
                AddTraceValue(hash, ReplayCausalHashFields.FactPrevious, fact.PreviousValue);
                fact.SourceIdentity.Contribute(hash, ReplayCausalHashFields.FactSource);
                hash.AddInt(ReplayCausalHashFields.FactObservedFrame, fact.ObservedFrame);
                hash.AddInt(ReplayCausalHashFields.FactSourceFrame, fact.SourceFrame);
                hash.AddLong(ReplayCausalHashFields.FactSourceGeneration, fact.SourceGeneration);
                hash.AddInt(ReplayCausalHashFields.FactChangedFrame, fact.LastChangedFrame);
                hash.AddInt(ReplayCausalHashFields.FactAuthority, (int)fact.AuthorityKind);
                hash.AddInt(ReplayCausalHashFields.FactLayers, (int)fact.ParticipatedLayers);
                hash.AddInt(ReplayCausalHashFields.FactBaseline, fact.BaselineUnknown ? 1 : 0);
                hash.AddInt(ReplayCausalHashFields.FactSourceUnknown, fact.SourceUnknown ? 1 : 0);
                hash.AddInt(ReplayCausalHashFields.FactDerivation, fact.DerivationCode);
                hash.AddInt(ReplayCausalHashFields.FactDependencyCount, fact.DependencyCount);
                for (int dependencyIndex = 0; dependencyIndex < fact.DependencyCount; dependencyIndex++)
                    fact.GetDependencyIdentity(dependencyIndex).Contribute(hash, ReplayCausalHashFields.FactDependency);
            }
            hash.AddInt(ReplayCausalHashFields.CandidateCount, trace.Candidates.Count);
            for (int i = 0; i < trace.Candidates.Count; i++)
            {
                CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = trace.Candidates[i];
                hash.AddInt(ReplayCausalHashFields.CandidateIndex, i);
                candidate.Identity.Contribute(hash, ReplayCausalHashFields.CandidateId);
                hash.AddInt(ReplayCausalHashFields.CandidateLayer, (int)candidate.Layer);
                hash.AddInt(ReplayCausalHashFields.CandidateState, (int)candidate.State);
                hash.AddInt(ReplayCausalHashFields.CandidateScoreAvailable, candidate.ScoreAvailable ? 1 : 0);
                hash.AddDoubleQuantized(ReplayCausalHashFields.CandidateScore, candidate.Score, 1000000d);
                hash.AddInt(ReplayCausalHashFields.CandidatePriorityAvailable, candidate.PriorityAvailable ? 1 : 0);
                hash.AddInt(ReplayCausalHashFields.CandidatePriority, candidate.Priority);
                hash.AddInt(ReplayCausalHashFields.CandidateCostAvailable, candidate.CostAvailable ? 1 : 0);
                hash.AddDoubleQuantized(ReplayCausalHashFields.CandidateCost, candidate.Cost, 1000000d);
                hash.AddInt(ReplayCausalHashFields.CandidateRank, candidate.StableRank);
                hash.AddInt(ReplayCausalHashFields.CandidateReason, candidate.ReasonCode);
                hash.AddInt(ReplayCausalHashFields.CandidateFactCount, candidate.RelatedFactCount);
                for (int factIndex = 0; factIndex < candidate.RelatedFactCount; factIndex++)
                    candidate.GetRelatedFactIdentity(factIndex).Contribute(hash, ReplayCausalHashFields.CandidateFact);
            }
            CharacterTestRuntimeAiLabDecisionTraceOverflow overflow = trace.Overflow;
            hash.AddInt(ReplayCausalHashFields.OverflowRegion, (int)overflow.Region);
            hash.AddInt(ReplayCausalHashFields.OverflowActual, overflow.ActualCount);
            hash.AddInt(ReplayCausalHashFields.OverflowLimit, overflow.Limit);
            return hash.ToHash();
        }

        private static void AddTraceValue(
            RuntimeHashAccumulator hash,
            int key,
            CharacterTestRuntimeAiLabDecisionTraceValue value)
        {
            hash.BeginObject(key, RuntimeHashSchemaIds.RuntimeAiPlannerTraceValue, 1);
            hash.AddInt(TraceValueHashFields.Kind, (int)value.Kind);
            hash.AddInt(TraceValueHashFields.Boolean, value.Boolean ? 1 : 0);
            hash.AddInt(TraceValueHashFields.Integer, value.Integer);
            hash.AddDoubleQuantized(TraceValueHashFields.Number, value.Number, 1000000d);
            hash.AddDigest(TraceValueHashFields.Text, value.TextDigest);
            hash.EndObject();
        }

        private static class TraceValueHashFields
        {
            internal const int Kind = 1, Boolean = 2, Integer = 3, Number = 4, Text = 5;
        }
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private const int ReplayValidationFramesPerUpdate = 64;
        private static readonly int[] ReplayTimelineZoomSpans = { 120, 300, 600, 900, 1800 };
        private CharacterTestReplayCapture _replayValidationCapture;
        private CharacterTestReplayRecordIdentity _replayValidationIdentity;
        private CharacterTestReplayValidationComparisonKernel _replayValidationKernel;
        private CharacterTestReplayValidationResult _replayValidationResult;
        private CharacterTestRuntimeAiLabTimelineProjection _replayValidationTimelineProjection;
        private CharacterTestRuntimeAiLabTimelineProjection _replayValidationBaseTimelineProjection;
        private string _replayValidationRecordMetadata = string.Empty;
        private string _replayValidationBootstrapIdentity = string.Empty;
        private string _replayValidationAuthorityIdentity = string.Empty;
        private string _replayValidationResultEvidence = string.Empty;
        private int _replayValidationCaptureGeneration;
        private long _replayValidationTimelineEvidenceHash;
        private CharacterTestRuntimeAiLabReplayCausalSnapshot[] _replayFactsCausalSnapshots =
            Array.Empty<CharacterTestRuntimeAiLabReplayCausalSnapshot>();
        private CharacterTestRuntimeAiLabReplayCausalSnapshot _activeReplayFactsCausalSnapshot;
        private string _replayValidationSelectedVisualStableKey = string.Empty;
        private int _replayValidationTimelineZoomLevel = -1;
        private int _replayValidationTimelineViewStart = int.MinValue;
        private CharacterTestReplayValidationState _replayValidationState =
            CharacterTestReplayValidationState.Empty;
        private long _replayValidationOperationId;

        internal bool CanFreezeReplayValidationCapture =>
            _phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup
            && _context?.NetworkRunner is ICharacterTestNetworkReplayRunner;
        internal bool ReplayValidationKernelActive => _replayValidationKernel != null;

        internal CharacterTestRuntimeAiLabReplayValidationWorkspaceSnapshot
            CaptureReplayValidationWorkspaceSnapshot()
        {
            CharacterTestReplayValidationResult result = _replayValidationResult;
            CharacterTestReplayRecordIdentity identity = _replayValidationIdentity;
            bool isBusy = _replayValidationState == CharacterTestReplayValidationState.InspectingRecord
                || _replayValidationState == CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap
                || _replayValidationState == CharacterTestReplayValidationState.RestoringGameplayBootstrap
                || _replayValidationState == CharacterTestReplayValidationState.ComparingFrames;
            string summary = result?.Summary;
            if (string.IsNullOrEmpty(summary))
            {
                summary = _context?.NetworkReplayStatus?.Summary
                    ?? "No replay recording is available for the active session.";
            }
            return new CharacterTestRuntimeAiLabReplayValidationWorkspaceSnapshot(
                _replayValidationCapture != null,
                _replayValidationState,
                result?.Code ?? CharacterTestReplayValidationCodes.None,
                summary,
                identity?.StartFrame ?? 0,
                identity?.EndFrame ?? 0,
                identity?.FrameCount ?? 0,
                result?.FramesCompared ?? _replayValidationKernel?.ComparedFrames ?? 0,
                identity?.RecordedFrameCommandCount ?? 0,
                identity?.AuthoritativePacketCommandCount ?? 0,
                identity?.AuthorityRecordHash ?? 0L,
                result?.HasFailureFrame ?? false,
                result != null && result.HasFailureFrame ? (int)result.FailureFrame.Value : 0,
                result?.ExpectedHash ?? 0L,
                result?.ActualHash ?? 0L,
                _replayValidationOperationId,
                _replayValidationTimelineProjection,
                _replayValidationRecordMetadata,
                _replayValidationBootstrapIdentity,
                _replayValidationAuthorityIdentity,
                _replayValidationResultEvidence,
                _replayValidationCaptureGeneration,
                _replayValidationTimelineEvidenceHash,
                _replayValidationSelectedVisualStableKey,
                !isBusy
                    && result != null && result.HasFailureFrame
                    && HasReplayEvidenceTarget(CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Divergence),
                !isBusy
                    && HasReplayEvidenceTarget(CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Facts),
                !isBusy
                    && HasReplayEvidenceTarget(CharacterTestRuntimeAiLabReplayEvidenceTargetKind.BehaviorCard));
        }

        internal CharacterTestRuntimeAiLabReplayReturnToken CaptureReplayReturnToken()
        {
            CharacterTestReplayRecordIdentity identity = _replayValidationIdentity;
            return new CharacterTestRuntimeAiLabReplayReturnToken(
                _replayValidationCaptureGeneration,
                identity?.AuthorityRecordHash ?? 0L,
                _replayValidationTimelineEvidenceHash,
                _replayValidationTimelineProjection?.Domain.SelectedFrame ?? 0,
                _replayValidationSelectedVisualStableKey);
        }

        internal bool MatchesReplayReturnToken(CharacterTestRuntimeAiLabReplayReturnToken token)
        {
            return token.IsValid
                && token.CaptureGeneration == _replayValidationCaptureGeneration
                && token.AuthorityRecordHash == (_replayValidationIdentity?.AuthorityRecordHash ?? 0L)
                && token.TimelineEvidenceHash == _replayValidationTimelineEvidenceHash;
        }

        internal bool TryActivateReplayFactsCausalSnapshot(
            CharacterTestRuntimeAiLabReplayEvidenceLocator locator,
            out string absenceCode)
        {
            absenceCode = "REPLAY_CAUSAL_EVIDENCE_NOT_CAPTURED";
            CharacterTestRuntimeAiLabReplayCausalSnapshot snapshot =
                FindReplayFactsCausalSnapshot(locator.ReplayFrame, locator.AgentEntityId);
            if (!MatchesReplayLocator(locator)
                || locator.TargetKind != CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Facts
                || snapshot == null
                || !snapshot.Matches(
                    locator.CaptureGeneration,
                    locator.AuthorityRecordHash,
                    locator.TimelineEvidenceHash)
                || snapshot.ReplayFrame != locator.ReplayFrame
                || snapshot.LiveFrame != locator.LiveFrame
                || snapshot.AgentEntityId != locator.AgentEntityId
                || !snapshot.TryGetTrace(out _))
            {
                _activeReplayFactsCausalSnapshot = null;
                return false;
            }
            _activeReplayFactsCausalSnapshot = snapshot;
            absenceCode = string.Empty;
            return true;
        }

        internal bool TryGetActiveReplayFactsCausalSnapshot(
            out CharacterTestRuntimeAiLabReplayCausalSnapshot snapshot)
        {
            snapshot = _activeReplayFactsCausalSnapshot;
            return snapshot != null
                && snapshot.Matches(
                    _replayValidationCaptureGeneration,
                    _replayValidationIdentity?.AuthorityRecordHash ?? 0L,
                    _replayValidationTimelineEvidenceHash)
                && snapshot.TryGetTrace(out _);
        }

        internal void ClearActiveReplayFactsCausalSnapshot()
        {
            _activeReplayFactsCausalSnapshot = null;
        }

        internal void ResetTransientUiContextForClose()
        {
            RestorePublishedCardAuthoringSlotBinding();
            CancelDeterministicReplay("Runtime AI Lab closed.");
            ResetReplayValidationWorkspace(clearCapture: true);
            ResetFactsWorkspaceTransientContext();
            ClearTimelineItemSelection();
            CancelBranchRecompute();
            ResetLoopPlaybackState();
            ResetHistoryPlaybackState();
        }

        internal int ReplayFactsCausalSnapshotCount => _replayFactsCausalSnapshots.Length;

        internal bool TryGetReplayFactsCausalSnapshot(
            int replayFrame,
            int agentEntityId,
            out CharacterTestRuntimeAiLabReplayCausalSnapshot snapshot)
        {
            snapshot = FindReplayFactsCausalSnapshot(replayFrame, agentEntityId);
            return snapshot != null
                && snapshot.Matches(
                    _replayValidationCaptureGeneration,
                    _replayValidationIdentity?.AuthorityRecordHash ?? 0L,
                    _replayValidationTimelineEvidenceHash)
                && snapshot.TryGetTrace(out _);
        }

        internal bool TryResolveReplayEvidenceLocator(
            CharacterTestRuntimeAiLabReplayEvidenceTargetKind targetKind,
            out CharacterTestRuntimeAiLabReplayEvidenceLocator locator,
            out string absenceCode)
        {
            locator = default;
            absenceCode = "REPLAY_EVIDENCE_UNAVAILABLE";
            CharacterTestReplayRecordIdentity identity = _replayValidationIdentity;
            if (_replayValidationCapture == null
                || identity == null
                || !identity.IsValid
                || _replayValidationTimelineProjection == null
                || _replayValidationTimelineEvidenceHash == 0L)
            {
                return false;
            }

            if (targetKind == CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Divergence)
            {
                CharacterTestReplayValidationResult result = _replayValidationResult;
                if (result == null
                    || !result.HasFailureFrame
                    || !TryMapReplayFrameToLive((int)result.FailureFrame.Value, out int liveFailureFrame))
                {
                    absenceCode = "REPLAY_DIVERGENCE_NOT_CAPTURED";
                    return false;
                }
                locator = new CharacterTestRuntimeAiLabReplayEvidenceLocator(
                    targetKind,
                    _replayValidationCaptureGeneration,
                    identity.AuthorityRecordHash,
                    _replayValidationTimelineEvidenceHash,
                    (int)result.FailureFrame.Value,
                    liveFailureFrame,
                    default,
                    default,
                    0,
                    0,
                    string.Empty,
                    CharacterTestRuntimeAiLabLogicElementKind.None,
                    string.Empty);
                absenceCode = string.Empty;
                return true;
            }

            if (targetKind == CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Facts)
            {
                int preferredFrame = _replayValidationTimelineProjection.Domain.SelectedFrame;
                int preferredAgent = ResolveSelectedReplayAgent(preferredFrame);
                CharacterTestRuntimeAiLabReplayCausalSnapshot causal =
                    FindReplayFactsCausalSnapshot(preferredFrame, preferredAgent);
                if (causal == null
                    || !causal.Matches(
                        _replayValidationCaptureGeneration,
                        identity.AuthorityRecordHash,
                        _replayValidationTimelineEvidenceHash)
                    || !causal.TryGetTrace(out _))
                {
                    absenceCode = "REPLAY_CAUSAL_EVIDENCE_NOT_CAPTURED";
                    return false;
                }
                locator = new CharacterTestRuntimeAiLabReplayEvidenceLocator(
                    targetKind,
                    causal.CaptureGeneration,
                    causal.AuthorityRecordHash,
                    causal.TimelineEvidenceHash,
                    causal.ReplayFrame,
                    causal.LiveFrame,
                    default,
                    default,
                    causal.AgentEntityId,
                    0,
                    string.Empty,
                    CharacterTestRuntimeAiLabLogicElementKind.None,
                    string.Empty);
                absenceCode = string.Empty;
                return true;
            }

            if (!TryFindReplayEvidenceItem(targetKind, out CharacterTestRuntimeAiLabTimelineItem replayItem)
                || !TryMapReplayItemToLive(replayItem, out CharacterTestRuntimeAiLabTimelineItem liveItem))
            {
                absenceCode = targetKind == CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Facts
                    ? "REPLAY_CAUSAL_EVIDENCE_NOT_CAPTURED"
                    : targetKind == CharacterTestRuntimeAiLabReplayEvidenceTargetKind.BehaviorCard
                        ? "REPLAY_BEHAVIOR_EVIDENCE_NOT_CAPTURED"
                        : "REPLAY_TIMELINE_PROVENANCE_MISMATCH";
                return false;
            }

            ResolveReplayBehaviorElement(
                replayItem,
                out CharacterTestRuntimeAiLabLogicElementKind behaviorKind,
                out string behaviorStableId);
            locator = new CharacterTestRuntimeAiLabReplayEvidenceLocator(
                targetKind,
                _replayValidationCaptureGeneration,
                identity.AuthorityRecordHash,
                _replayValidationTimelineEvidenceHash,
                replayItem.StartFrame,
                liveItem.StartFrame,
                replayItem.CreateLocator(),
                liveItem.CreateLocator(),
                replayItem.AgentEntityId,
                replayItem.BehaviorLoadoutId,
                replayItem.BehaviorEntryId,
                behaviorKind,
                behaviorStableId);
            absenceCode = string.Empty;
            return true;
        }

        internal bool TryResolveReplayBehaviorSelection(
            CharacterTestRuntimeAiLabReplayEvidenceLocator locator,
            out int entryIndex,
            out CharacterTestRuntimeAiLabLogicElementKind kind,
            out int elementIndex)
        {
            entryIndex = -1;
            kind = locator.BehaviorElementKind;
            elementIndex = -1;
            if (!MatchesReplayLocator(locator)
                || locator.TargetKind != CharacterTestRuntimeAiLabReplayEvidenceTargetKind.BehaviorCard
                || !TryResolveBehaviorEntry(
                    locator.BehaviorLoadoutId,
                    locator.BehaviorEntryId,
                    out _,
                    out entryIndex)
                || !TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out _)
                || card?.LogicPayload == null)
            {
                return false;
            }

            string stableId = locator.BehaviorElementStableId;
            if (kind == CharacterTestRuntimeAiLabLogicElementKind.Detector)
            {
                for (int i = 0; i < card.LogicPayload.Detectors.Count; i++)
                {
                    if (string.Equals(card.LogicPayload.Detectors[i].DetectorId, stableId, StringComparison.Ordinal))
                    {
                        elementIndex = i;
                        return true;
                    }
                }
                return false;
            }
            if (kind == CharacterTestRuntimeAiLabLogicElementKind.Transition)
            {
                for (int i = 0; i < card.LogicPayload.Transitions.Count; i++)
                {
                    if (string.Equals(card.LogicPayload.Transitions[i].TransitionId, stableId, StringComparison.Ordinal))
                    {
                        elementIndex = i;
                        return true;
                    }
                }
                return false;
            }
            for (int i = 0; i < card.LogicPayload.Stages.Count; i++)
            {
                if (string.Equals(card.LogicPayload.Stages[i].StageId, stableId, StringComparison.Ordinal))
                {
                    kind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
                    elementIndex = i;
                    return true;
                }
            }
            return false;
        }

        internal bool MatchesReplayLocator(CharacterTestRuntimeAiLabReplayEvidenceLocator locator)
        {
            return locator.IsValid
                && locator.CaptureGeneration == _replayValidationCaptureGeneration
                && locator.AuthorityRecordHash == (_replayValidationIdentity?.AuthorityRecordHash ?? 0L)
                && locator.TimelineEvidenceHash == _replayValidationTimelineEvidenceHash;
        }

        internal bool TryResolveReplayTimelineLocator(
            CharacterTestRuntimeAiLabTimelineItemLocator replayLocator,
            bool useEndFrame,
            out CharacterTestRuntimeAiLabReplayEvidenceLocator locator)
        {
            locator = default;
            CharacterTestRuntimeAiLabTimelineProjection projection =
                _replayValidationTimelineProjection;
            CharacterTestReplayRecordIdentity identity = _replayValidationIdentity;
            if (projection == null
                || identity == null
                || !identity.IsValid
                || !projection.TryFindItem(
                    replayLocator.StableKey,
                    out CharacterTestRuntimeAiLabTimelineItem replayItem)
                || !replayLocator.Matches(replayItem)
                || !TryMapReplayItemToLive(replayItem, out CharacterTestRuntimeAiLabTimelineItem liveItem))
            {
                return false;
            }
            int replayFrame = useEndFrame ? replayItem.EndFrame : replayItem.StartFrame;
            int liveFrame = useEndFrame ? liveItem.EndFrame : liveItem.StartFrame;
            locator = new CharacterTestRuntimeAiLabReplayEvidenceLocator(
                CharacterTestRuntimeAiLabReplayEvidenceTargetKind.RuntimeFrame,
                _replayValidationCaptureGeneration,
                identity.AuthorityRecordHash,
                _replayValidationTimelineEvidenceHash,
                replayFrame,
                liveFrame,
                replayItem.CreateLocator(),
                liveItem.CreateLocator(),
                replayItem.AgentEntityId,
                replayItem.BehaviorLoadoutId,
                replayItem.BehaviorEntryId,
                CharacterTestRuntimeAiLabLogicElementKind.None,
                string.Empty);
            return true;
        }

        private bool HasReplayEvidenceTarget(
            CharacterTestRuntimeAiLabReplayEvidenceTargetKind targetKind)
        {
            if (_replayValidationCapture == null
                || _replayValidationTimelineProjection == null
                || _replayValidationTimelineEvidenceHash == 0L)
            {
                return false;
            }
            if (targetKind == CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Divergence)
                return _replayValidationResult != null && _replayValidationResult.HasFailureFrame;
            if (targetKind == CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Facts)
            {
                int preferredFrame = _replayValidationTimelineProjection.Domain.SelectedFrame;
                CharacterTestRuntimeAiLabReplayCausalSnapshot causal =
                    FindReplayFactsCausalSnapshot(
                        preferredFrame,
                        ResolveSelectedReplayAgent(preferredFrame));
                return causal != null
                    && causal.Matches(
                        _replayValidationCaptureGeneration,
                        _replayValidationIdentity?.AuthorityRecordHash ?? 0L,
                        _replayValidationTimelineEvidenceHash)
                    && causal.TryGetTrace(out _);
            }
            return TryFindReplayEvidenceItem(targetKind, out _);
        }

        private bool TryFindReplayEvidenceItem(
            CharacterTestRuntimeAiLabReplayEvidenceTargetKind targetKind,
            out CharacterTestRuntimeAiLabTimelineItem result)
        {
            result = null;
            CharacterTestRuntimeAiLabTimelineProjection projection =
                _replayValidationTimelineProjection;
            if (projection == null)
                return false;
            int preferredFrame = projection.Domain.SelectedFrame;
            int bestFrame = int.MinValue;
            for (int trackIndex = 0; trackIndex < projection.Tracks.Count; trackIndex++)
            {
                IReadOnlyList<CharacterTestRuntimeAiLabTimelineItem> items =
                    projection.Tracks[trackIndex].Items;
                for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                {
                    CharacterTestRuntimeAiLabTimelineItem item = items[itemIndex];
                    ConsiderReplayEvidenceItem(item, targetKind, preferredFrame, ref result, ref bestFrame);
                    for (int memberIndex = 0; memberIndex < item.Members.Count; memberIndex++)
                    {
                        ConsiderReplayEvidenceItem(
                            item.Members[memberIndex],
                            targetKind,
                            preferredFrame,
                            ref result,
                            ref bestFrame);
                    }
                }
            }
            return result != null;
        }

        private static void ConsiderReplayEvidenceItem(
            CharacterTestRuntimeAiLabTimelineItem item,
            CharacterTestRuntimeAiLabReplayEvidenceTargetKind targetKind,
            int preferredFrame,
            ref CharacterTestRuntimeAiLabTimelineItem result,
            ref int bestFrame)
        {
            if (item == null || !IsReplayEvidenceItem(item, targetKind))
                return;
            bool preferred = item.StartFrame <= preferredFrame && item.EndFrame >= preferredFrame;
            bool currentPreferred = result != null
                && result.StartFrame <= preferredFrame
                && result.EndFrame >= preferredFrame;
            if (result != null
                && (currentPreferred && !preferred
                    || currentPreferred == preferred && item.StartFrame <= bestFrame))
            {
                return;
            }
            result = item;
            bestFrame = item.StartFrame;
        }

        private static bool IsReplayEvidenceItem(
            CharacterTestRuntimeAiLabTimelineItem item,
            CharacterTestRuntimeAiLabReplayEvidenceTargetKind targetKind)
        {
            if (targetKind == CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Facts)
            {
                return item.Provenance == CharacterTestRuntimeAiLabTimelineItemProvenance.TypedEvent
                    && item.AgentEntityId > 0
                    && !string.IsNullOrEmpty(item.SourceTraceId);
            }
            if (targetKind == CharacterTestRuntimeAiLabReplayEvidenceTargetKind.BehaviorCard)
            {
                return item.BehaviorLoadoutId > 0
                    && !string.IsNullOrEmpty(item.BehaviorEntryId)
                    && (!string.IsNullOrEmpty(item.BehaviorStepId)
                        || !string.IsNullOrEmpty(item.SourceTraceId)
                        || item.EventKind == CharacterTestRuntimeAiLabTimelineEventKind.BehaviorDetectorTriggered);
            }
            return item.Provenance != CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate;
        }

        private bool TryMapReplayItemToLive(
            CharacterTestRuntimeAiLabTimelineItem replayItem,
            out CharacterTestRuntimeAiLabTimelineItem liveItem)
        {
            liveItem = null;
            if (replayItem == null
                || !TryMapReplayFrameToLive(replayItem.StartFrame, out int liveFrame))
            {
                return false;
            }
            CharacterTestRuntimeAiLabTimelineProjection liveProjection = BuildTimelineProjection();
            if (liveProjection == null
                || !liveProjection.TryFindItem(replayItem.StableKey, out liveItem)
                || liveItem == null
                || liveItem.StartFrame != liveFrame
                || liveItem.EndFrame != replayItem.EndFrame + 1
                || liveItem.SessionGeneration != replayItem.SessionGeneration
                || liveItem.RecordGeneration != replayItem.RecordGeneration
                || liveItem.AuthorityHash != replayItem.AuthorityHash
                || liveItem.EventKind != replayItem.EventKind
                || !string.Equals(liveItem.SourceTraceId, replayItem.SourceTraceId, StringComparison.Ordinal)
                || !string.Equals(liveItem.BehaviorEntryId, replayItem.BehaviorEntryId, StringComparison.Ordinal)
                || !string.Equals(liveItem.BehaviorStepId, replayItem.BehaviorStepId, StringComparison.Ordinal))
            {
                liveItem = null;
                return false;
            }
            return true;
        }

        private bool TryMapReplayFrameToLive(int replayFrame, out int liveFrame)
        {
            liveFrame = replayFrame < int.MaxValue ? replayFrame + 1 : replayFrame;
            if (_replayValidationCapture?.RuntimeReplaySnapshot == null
                || !_timeline.TryGetFrame(
                    liveFrame,
                    out CharacterTestRuntimeAiPlannerDevTimelineRecord timelineRecord)
                || !timelineRecord.HasReplayAuthorityEvidence
                || timelineRecord.ReplayAuthorityFrame != replayFrame)
            {
                return false;
            }
            IReadOnlyList<RuntimeReplayFrameRecord> records =
                _replayValidationCapture.RuntimeReplaySnapshot.Records;
            for (int i = 0; i < records.Count; i++)
            {
                RuntimeReplayFrameRecord record = records[i];
                if (record.Frame.Value != replayFrame)
                    continue;
                return timelineRecord.ReplayAuthorityHash == record.ResultHash;
            }
            return false;
        }

        private static void ResolveReplayBehaviorElement(
            CharacterTestRuntimeAiLabTimelineItem item,
            out CharacterTestRuntimeAiLabLogicElementKind kind,
            out string stableId)
        {
            if (item.EventKind == CharacterTestRuntimeAiLabTimelineEventKind.BehaviorDetectorTriggered)
            {
                kind = CharacterTestRuntimeAiLabLogicElementKind.Detector;
                stableId = item.Label;
                return;
            }
            if (item.EventKind == CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionPending
                || item.EventKind == CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionCommitted)
            {
                kind = CharacterTestRuntimeAiLabLogicElementKind.Transition;
                stableId = item.SourceTraceId;
                return;
            }
            kind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            stableId = item.BehaviorStepId;
        }

        internal bool SelectReplayTimelineItem(
            CharacterTestRuntimeAiLabTimelineItemLocator locator)
        {
            CharacterTestRuntimeAiLabTimelineProjection projection =
                _replayValidationTimelineProjection;
            if (projection == null
                || !projection.TryFindItem(locator.StableKey, out CharacterTestRuntimeAiLabTimelineItem item)
                || !locator.Matches(item))
            {
                return false;
            }
            _replayValidationSelectedVisualStableKey = item.StableKey;
            _replayValidationTimelineProjection = CloneReplayProjectionWithSelectedFrame(
                projection,
                item.StartFrame);
            return true;
        }

        internal bool SelectReplayFrame(int replayFrame)
        {
            CharacterTestRuntimeAiLabTimelineProjection projection =
                _replayValidationTimelineProjection;
            if (projection == null
                || replayFrame < projection.Domain.SessionStartFrame
                || replayFrame > projection.Domain.SessionEndFrame
                || FindReplayFactsCausalSnapshot(replayFrame, 0) == null)
            {
                return false;
            }
            _replayValidationSelectedVisualStableKey = string.Empty;
            _replayValidationTimelineProjection = CloneReplayProjectionWithSelectedFrame(
                projection,
                replayFrame);
            return true;
        }

        internal void ClearReplayTimelineItemSelection()
        {
            _replayValidationSelectedVisualStableKey = string.Empty;
        }

        internal bool ZoomReplayTimeline(int direction)
        {
            CharacterTestRuntimeAiLabTimelineProjection projection =
                _replayValidationTimelineProjection;
            if (projection == null || direction == 0)
                return false;
            if (_replayValidationTimelineZoomLevel < 0)
                _replayValidationTimelineZoomLevel = direction > 0
                    ? ReplayTimelineZoomSpans.Length - 1
                    : 0;
            else
            {
                _replayValidationTimelineZoomLevel = Math.Max(
                    0,
                    Math.Min(
                        ReplayTimelineZoomSpans.Length - 1,
                        _replayValidationTimelineZoomLevel - Math.Sign(direction)));
            }
            _replayValidationTimelineViewStart = int.MinValue;
            ApplyReplayTimelineViewport();
            return true;
        }

        internal bool FitReplayTimeline()
        {
            if (_replayValidationTimelineProjection == null)
                return false;
            _replayValidationTimelineZoomLevel = -1;
            _replayValidationTimelineViewStart = int.MinValue;
            ApplyReplayTimelineViewport();
            return true;
        }

        internal bool PanReplayTimeline(int direction)
        {
            CharacterTestRuntimeAiLabTimelineProjection projection =
                _replayValidationTimelineProjection;
            if (projection == null || _replayValidationTimelineZoomLevel < 0 || direction == 0)
                return false;
            CharacterTestRuntimeAiLabTimelineViewport current = projection.Viewport;
            int delta = Math.Max(1, current.FrameSpan / 4) * Math.Sign(direction);
            _replayValidationTimelineViewStart = ClampReplayTimelineViewStart(
                current.StartFrame + delta,
                current.FrameSpan,
                projection.Domain);
            ApplyReplayTimelineViewport();
            return true;
        }

        /// <remarks>AllocByDesign: freezes an immutable replay capture on explicit user command.</remarks>
        internal bool FreezeReplayValidationCapture(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                diagnostic = "Start a Runtime AI Lab session before freezing Replay evidence.";
                return false;
            }

            CharacterTestNetworkCoordinator coordinator = _context?.NetworkCoordinator;
            CharacterTestReplayPlaybackStatus status = null;
            CharacterTestReplayCapture capture = null;
            bool captured = coordinator != null
                && coordinator.TryCreateRuntimeAiLabReplayCapture(out capture, out status);
            if (!captured || capture == null)
            {
                diagnostic = status?.Summary ?? "Replay capture is unavailable.";
                return false;
            }

            ResetReplayValidationWorkspace(clearCapture: true);
            _replayValidationCapture = capture;
            _replayValidationIdentity = CharacterTestReplayRecordIdentity.Inspect(capture);
            InvalidateTimelineProjection();
            _replayValidationTimelineProjection = BuildReplayTimelineSidecarProjection(
                BuildTimelineProjection(),
                _timeline,
                capture,
                _replayValidationIdentity,
                out string timelineEvidenceDiagnostic);
            _replayValidationBaseTimelineProjection = _replayValidationTimelineProjection;
            _replayValidationResultEvidence = timelineEvidenceDiagnostic;
            _replayValidationTimelineZoomLevel = -1;
            _replayValidationTimelineViewStart = int.MinValue;
            _replayValidationCaptureGeneration++;
            _replayFactsCausalSnapshots = CaptureReplayFactsCausalSnapshots();
            _replayValidationTimelineEvidenceHash = ComputeTimelineEvidenceHash(
                _replayValidationTimelineProjection,
                _replayFactsCausalSnapshots);
            for (int i = 0; i < _replayFactsCausalSnapshots.Length; i++)
            {
                _replayFactsCausalSnapshots[i] =
                    _replayFactsCausalSnapshots[i].BindTimelineEvidenceHash(
                        _replayValidationTimelineEvidenceHash);
            }
            _replayValidationRecordMetadata = FormatReplayRecordMetadata(capture);
            _replayValidationBootstrapIdentity = FormatReplayBootstrapIdentity(capture);
            _replayValidationAuthorityIdentity = FormatReplayAuthorityIdentity(capture);
            _replayValidationState = _replayValidationIdentity.IsValid
                ? CharacterTestReplayValidationState.Ready
                : CharacterTestReplayValidationState.Failed;
            _replayValidationResult = new CharacterTestReplayValidationResult
            {
                State = _replayValidationState,
                Code = _replayValidationIdentity.Code,
                Summary = _replayValidationIdentity.IsValid
                    ? "Replay evidence frozen and ready for deterministic validation."
                    : _replayValidationIdentity.Message
            };
            diagnostic = _replayValidationResult.Summary;
            return _replayValidationIdentity.IsValid;
        }

        /// <remarks>AllocByDesign: creates an isolated replay runtime only for explicit validation.</remarks>
        internal bool StartReplayValidation(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_replayValidationCapture == null
                && !FreezeReplayValidationCapture(out diagnostic))
            {
                return false;
            }
            if (_replayValidationState == CharacterTestReplayValidationState.ComparingFrames)
            {
                diagnostic = "Replay validation is already running.";
                return false;
            }

            DisposeReplayValidationKernel();
            _replayValidationOperationId++;
            if (_replayValidationTimelineProjection == null)
                _replayValidationTimelineProjection = _replayValidationBaseTimelineProjection;
            ApplyReplayTimelineViewport();
            _replayValidationResultEvidence = string.Empty;
            CharacterTestReplayRecordIdentity frozenIdentity = _replayValidationIdentity;
            CharacterTestReplayRecordIdentity currentIdentity =
                CharacterTestReplayRecordIdentity.Inspect(_replayValidationCapture);
            if (frozenIdentity == null
                || !frozenIdentity.IsValid
                || currentIdentity == null
                || !currentIdentity.IsValid
                || frozenIdentity.AuthorityRecordHash != currentIdentity.AuthorityRecordHash)
            {
                var staleResult = new CharacterTestReplayValidationResult
                {
                    State = CharacterTestReplayValidationState.Failed,
                    Code = CharacterTestReplayValidationCodes.ReplayRecordCorrupt,
                    Summary = "Frozen Replay evidence changed after capture. Freeze a new record before validating."
                };
                ApplyReplayValidationResult(staleResult);
                diagnostic = staleResult.Summary;
                return false;
            }
            _replayValidationKernel = new CharacterTestReplayValidationComparisonKernel(
                _replayValidationCapture);
            CharacterTestReplayValidationResult inspected = _replayValidationKernel.Inspect();
            _replayValidationIdentity = _replayValidationKernel.Identity;
            if (_replayValidationKernel.IsTerminal)
            {
                ApplyReplayValidationResult(inspected);
                diagnostic = inspected.Summary;
                return false;
            }

            _replayValidationState = CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap;
            CharacterTestReplayBootstrapPhaseResult authority =
                _replayValidationKernel.RestoreSessionAuthorityBootstrap();
            if (!authority.Success)
            {
                ApplyReplayValidationResult(_replayValidationKernel.BootstrapFailed(authority));
                diagnostic = authority.Message;
                return false;
            }

            _replayValidationState = CharacterTestReplayValidationState.RestoringGameplayBootstrap;
            CharacterTestReplayBootstrapPhaseResult gameplay =
                _replayValidationKernel.RestoreGameplayBootstrap();
            if (!gameplay.Success)
            {
                ApplyReplayValidationResult(_replayValidationKernel.BootstrapFailed(gameplay));
                diagnostic = gameplay.Message;
                return false;
            }

            _replayValidationState = CharacterTestReplayValidationState.ComparingFrames;
            _replayValidationResult = new CharacterTestReplayValidationResult
            {
                State = _replayValidationState,
                Code = CharacterTestReplayValidationCodes.None,
                Summary = "Comparing frozen authority frames..."
            };
            diagnostic = _replayValidationResult.Summary;
            return true;
        }

        internal bool CancelReplayValidation(out string diagnostic)
        {
            if (_replayValidationKernel == null || _replayValidationKernel.IsTerminal)
            {
                diagnostic = "Replay validation is not running.";
                return false;
            }
            ApplyReplayValidationResult(_replayValidationKernel.Cancel());
            diagnostic = _replayValidationResult.Summary;
            return true;
        }

        internal bool RetryReplayValidation(out string diagnostic)
        {
            return StartReplayValidation(out diagnostic);
        }

        private void ProcessReplayValidationWork()
        {
            CharacterTestReplayValidationComparisonKernel kernel = _replayValidationKernel;
            if (kernel == null
                || _replayValidationState != CharacterTestReplayValidationState.ComparingFrames)
            {
                return;
            }

            for (int i = 0; i < ReplayValidationFramesPerUpdate && !kernel.IsTerminal; i++)
                kernel.CompareNextFrame();
            if (kernel.IsTerminal)
            {
                ApplyReplayValidationResult(kernel.TerminalResult);
                return;
            }

            _replayValidationResult.FramesCompared = kernel.ComparedFrames;
        }

        private void ApplyReplayValidationResult(CharacterTestReplayValidationResult result)
        {
            _replayValidationResult = result;
            _replayValidationState = result?.State ?? CharacterTestReplayValidationState.Failed;
            _replayValidationResultEvidence = FormatReplayResultEvidence(result);
            if (result != null && result.HasFailureFrame)
            {
                _replayValidationTimelineProjection = BuildReplayDivergenceProjection(
                    _replayValidationBaseTimelineProjection,
                    result,
                    _replayValidationOperationId);
                ApplyReplayTimelineViewport();
            }
            if (result != null && IsReplayValidationTerminal(result.State))
                DisposeReplayValidationKernel();
        }

        private static bool IsReplayValidationTerminal(CharacterTestReplayValidationState state)
        {
            return state == CharacterTestReplayValidationState.Passed
                || state == CharacterTestReplayValidationState.Diverged
                || state == CharacterTestReplayValidationState.Failed
                || state == CharacterTestReplayValidationState.Cancelled
                || state == CharacterTestReplayValidationState.Empty;
        }

        private void ResetReplayValidationWorkspace(bool clearCapture)
        {
            DisposeReplayValidationKernel();
            _replayValidationResult = null;
            _replayValidationIdentity = null;
            _replayValidationState = CharacterTestReplayValidationState.Empty;
            if (clearCapture)
            {
                _replayValidationCapture = null;
                _replayValidationTimelineProjection = null;
                _replayValidationBaseTimelineProjection = null;
                _replayValidationRecordMetadata = string.Empty;
                _replayValidationBootstrapIdentity = string.Empty;
                _replayValidationAuthorityIdentity = string.Empty;
                _replayValidationResultEvidence = string.Empty;
                _replayValidationTimelineEvidenceHash = 0L;
                _replayFactsCausalSnapshots =
                    Array.Empty<CharacterTestRuntimeAiLabReplayCausalSnapshot>();
                _activeReplayFactsCausalSnapshot = null;
                _replayValidationSelectedVisualStableKey = string.Empty;
                _replayValidationTimelineZoomLevel = -1;
                _replayValidationTimelineViewStart = int.MinValue;
            }
        }

        private CharacterTestRuntimeAiLabReplayCausalSnapshot[] CaptureReplayFactsCausalSnapshots()
        {
            CharacterTestReplayRecordIdentity identity = _replayValidationIdentity;
            if (identity == null
                || !identity.IsValid
                || _replayValidationTimelineProjection == null)
            {
                return Array.Empty<CharacterTestRuntimeAiLabReplayCausalSnapshot>();
            }

            var agentIds = new List<int>();
            CollectReplayAgentIds(_replayValidationTimelineProjection, agentIds);
            if (agentIds.Count == 0)
                return Array.Empty<CharacterTestRuntimeAiLabReplayCausalSnapshot>();
            agentIds.Sort();
            var snapshots = new List<CharacterTestRuntimeAiLabReplayCausalSnapshot>(
                Math.Max(1, identity.FrameCount * agentIds.Count));
            for (int replayFrame = identity.StartFrame; replayFrame <= identity.EndFrame; replayFrame++)
            {
                if (!TryMapReplayFrameToLive(replayFrame, out int liveFrame)
                    || !_timeline.TryGetFrame(
                        liveFrame,
                        out CharacterTestRuntimeAiPlannerDevTimelineRecord record))
                {
                    continue;
                }
                for (int agentIndex = 0; agentIndex < agentIds.Count; agentIndex++)
                {
                    int agentEntityId = agentIds[agentIndex];
                    if (!_timeline.TryGetDecisionTrace(
                        record,
                        agentEntityId,
                        out CharacterTestRuntimeAiLabDecisionTraceAgentView sourceTrace))
                    {
                        continue;
                    }
                    CharacterTestRuntimeAiLabReplayCausalSnapshot snapshot =
                        CloneReplayCausalSnapshot(
                            identity,
                            replayFrame,
                            liveFrame,
                            record,
                            agentEntityId,
                            sourceTrace);
                    if (snapshot != null)
                        snapshots.Add(snapshot);
                }
            }
            return snapshots.ToArray();
        }

        private CharacterTestRuntimeAiLabReplayCausalSnapshot CloneReplayCausalSnapshot(
            CharacterTestReplayRecordIdentity identity,
            int replayFrame,
            int liveFrame,
            CharacterTestRuntimeAiPlannerDevTimelineRecord record,
            int agentEntityId,
            CharacterTestRuntimeAiLabDecisionTraceAgentView sourceTrace)
        {

            var store = new CharacterTestRuntimeAiLabDecisionTraceStore(
                1,
                1,
                Math.Max(1, sourceTrace.Facts.Count),
                Math.Max(1, sourceTrace.Candidates.Count));
            CharacterTestRuntimeAiLabDecisionTraceWriter writer = store.BeginCapture(
                record.DecisionTraceHandle.SessionGeneration,
                liveFrame,
                out CharacterTestRuntimeAiLabDecisionTraceHandle handle,
                false,
                record.DecisionTraceHandle.DecisionFrame);
            CharacterTestRuntimeAiLabDecisionTraceAgentWriter agent =
                writer.BeginAgent(agentEntityId);
            if (!writer.IsValid || !agent.IsValid || !handle.IsValid)
                return null;
            CharacterTestRuntimeAiLabCommittedStateTrace previous = sourceTrace.PreviousCommitted;
            CharacterTestRuntimeAiLabCommitTrace commit = sourceTrace.Commit;
            agent.SetPrevious(previous);
            agent.SetCommit(commit);
            for (int i = 0; i < sourceTrace.Facts.Count; i++)
            {
                CharacterTestRuntimeAiLabFactObservationTrace fact = sourceTrace.Facts[i];
                agent.AddFact(fact);
            }
            for (int i = 0; i < sourceTrace.Candidates.Count; i++)
            {
                CharacterTestRuntimeAiLabCandidateEvaluationTrace candidate = sourceTrace.Candidates[i];
                agent.AddCandidate(candidate);
            }
            if (sourceTrace.Overflowed)
            {
                CharacterTestRuntimeAiLabDecisionTraceOverflow overflow = sourceTrace.Overflow;
                agent.RecordOverflow(overflow.Region, overflow.ActualCount, overflow.Limit);
            }
            writer.Seal();
            if (!store.TryGetAgent(handle, agentEntityId, out _))
                return null;

            return new CharacterTestRuntimeAiLabReplayCausalSnapshot(
                _replayValidationCaptureGeneration,
                identity.AuthorityRecordHash,
                0L,
                replayFrame,
                liveFrame,
                _timeline.LatestFrame,
                _timeline.RetainedStartFrame,
                agentEntityId,
                record.DecisionTraceHandle.SessionGeneration,
                ComputeAppliedBehaviorRevisionHash(record.Frame),
                record.StateHash,
                store,
                handle);
        }

        private static void CollectReplayAgentIds(
            CharacterTestRuntimeAiLabTimelineProjection projection,
            List<int> agentIds)
        {
            for (int trackIndex = 0; trackIndex < projection.Tracks.Count; trackIndex++)
            {
                IReadOnlyList<CharacterTestRuntimeAiLabTimelineItem> items =
                    projection.Tracks[trackIndex].Items;
                for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                {
                    AddReplayAgentId(items[itemIndex], agentIds);
                    for (int memberIndex = 0; memberIndex < items[itemIndex].Members.Count; memberIndex++)
                        AddReplayAgentId(items[itemIndex].Members[memberIndex], agentIds);
                }
            }
        }

        private static void AddReplayAgentId(
            CharacterTestRuntimeAiLabTimelineItem item,
            List<int> agentIds)
        {
            int agentEntityId = item?.AgentEntityId ?? 0;
            if (agentEntityId <= 0)
                return;
            for (int i = 0; i < agentIds.Count; i++)
            {
                if (agentIds[i] == agentEntityId)
                    return;
            }
            agentIds.Add(agentEntityId);
        }

        private CharacterTestRuntimeAiLabReplayCausalSnapshot FindReplayFactsCausalSnapshot(
            int replayFrame,
            int preferredAgentEntityId)
        {
            CharacterTestRuntimeAiLabReplayCausalSnapshot fallback = null;
            for (int i = 0; i < _replayFactsCausalSnapshots.Length; i++)
            {
                CharacterTestRuntimeAiLabReplayCausalSnapshot candidate =
                    _replayFactsCausalSnapshots[i];
                if (candidate == null || candidate.ReplayFrame != replayFrame)
                    continue;
                if (preferredAgentEntityId <= 0)
                    fallback = fallback ?? candidate;
                else if (candidate.AgentEntityId == preferredAgentEntityId)
                    return candidate;
            }
            return fallback;
        }

        private int ResolveSelectedReplayAgent(int replayFrame)
        {
            CharacterTestRuntimeAiLabTimelineProjection projection =
                _replayValidationTimelineProjection;
            if (projection != null
                && !string.IsNullOrEmpty(_replayValidationSelectedVisualStableKey)
                && projection.TryFindItem(
                    _replayValidationSelectedVisualStableKey,
                    out CharacterTestRuntimeAiLabTimelineItem item)
                && item.StartFrame <= replayFrame
                && item.EndFrame >= replayFrame)
            {
                return item.AgentEntityId;
            }
            return 0;
        }

        private void ApplyReplayTimelineViewport()
        {
            CharacterTestRuntimeAiLabTimelineProjection source =
                _replayValidationTimelineProjection;
            if (source == null)
                return;
            CharacterTestRuntimeAiLabTimelineDomain domain = source.Domain;
            CharacterTestRuntimeAiLabTimelineViewport viewport;
            if (_replayValidationTimelineZoomLevel < 0)
            {
                viewport = new CharacterTestRuntimeAiLabTimelineViewport(
                    domain.RetainedStartFrame,
                    domain.LatestFrame,
                    0,
                    source.Viewport.PixelWidth,
                    false,
                    CharacterTestRuntimeAiLabTimelineViewportMode.FitRetained);
            }
            else
            {
                int span = Math.Min(
                    ReplayTimelineZoomSpans[_replayValidationTimelineZoomLevel],
                    Math.Max(1, domain.LatestFrame - domain.RetainedStartFrame));
                int start = _replayValidationTimelineViewStart == int.MinValue
                    ? Math.Max(domain.RetainedStartFrame, domain.LatestFrame - span)
                    : ClampReplayTimelineViewStart(
                        _replayValidationTimelineViewStart,
                        span,
                        domain);
                _replayValidationTimelineViewStart = start;
                viewport = new CharacterTestRuntimeAiLabTimelineViewport(
                    start,
                    Math.Min(domain.LatestFrame, start + span),
                    _replayValidationTimelineZoomLevel,
                    source.Viewport.PixelWidth,
                    false,
                    CharacterTestRuntimeAiLabTimelineViewportMode.Manual);
            }
            _replayValidationTimelineProjection = CloneReplayProjectionWithViewport(source, viewport);
        }

        private static int ClampReplayTimelineViewStart(
            int start,
            int span,
            CharacterTestRuntimeAiLabTimelineDomain domain)
        {
            int maximum = Math.Max(domain.RetainedStartFrame, domain.LatestFrame - span);
            return Math.Max(domain.RetainedStartFrame, Math.Min(maximum, start));
        }

        private static CharacterTestRuntimeAiLabTimelineProjection CloneReplayProjectionWithViewport(
            CharacterTestRuntimeAiLabTimelineProjection source,
            CharacterTestRuntimeAiLabTimelineViewport viewport)
        {
            var tracks = new CharacterTestRuntimeAiLabTimelineTrackProjection[source.Tracks.Count];
            for (int i = 0; i < tracks.Length; i++)
                tracks[i] = source.Tracks[i];
            return new CharacterTestRuntimeAiLabTimelineProjection(
                source.ContentRevision,
                source.Domain,
                viewport,
                tracks,
                source.StatusMessage,
                source.Validity,
                source.DiagnosticFrame,
                source.DiagnosticValue);
        }

        private static CharacterTestRuntimeAiLabTimelineProjection CloneReplayProjectionWithSelectedFrame(
            CharacterTestRuntimeAiLabTimelineProjection source,
            int selectedFrame)
        {
            if (source == null)
                return null;
            CharacterTestRuntimeAiLabTimelineDomain old = source.Domain;
            var domain = new CharacterTestRuntimeAiLabTimelineDomain(
                old.SessionStartFrame,
                old.RetainedStartFrame,
                old.LatestFrame,
                Math.Max(old.RetainedStartFrame, Math.Min(old.LatestFrame, selectedFrame)),
                old.FrameLimit,
                old.RetainedRecordCount);
            var tracks = new CharacterTestRuntimeAiLabTimelineTrackProjection[source.Tracks.Count];
            for (int i = 0; i < tracks.Length; i++)
                tracks[i] = source.Tracks[i];
            return new CharacterTestRuntimeAiLabTimelineProjection(
                source.ContentRevision,
                domain,
                source.Viewport,
                tracks,
                source.StatusMessage,
                source.Validity,
                source.DiagnosticFrame,
                source.DiagnosticValue);
        }

        private void DisposeReplayValidationKernel()
        {
            _replayValidationKernel?.Dispose();
            _replayValidationKernel = null;
        }

        private static string FormatReplayRecordMetadata(CharacterTestReplayCapture capture)
        {
            RuntimeReplayHeader header = capture.RuntimeReplaySnapshot.Header;
            string recordedAt = capture.RecordedAtUtc.HasValue
                ? capture.RecordedAtUtc.Value.ToString("u", CultureInfo.InvariantCulture)
                : "Unavailable";
            return "Schema " + header.SchemaVersion.ToString(CultureInfo.InvariantCulture)
                + " · Framework " + header.FrameworkVersion
                + "\nConfig " + header.ConfigHash
                + "\nResources " + header.ResourceCatalogHash
                + "\nRecorded " + recordedAt;
        }

        private static string FormatReplayBootstrapIdentity(CharacterTestReplayCapture capture)
        {
            RuntimeCustomState gameplay = capture.BootstrapSnapshot;
            RuntimeCustomState authority = capture.SessionAuthorityBootstrapSnapshot;
            return "Gameplay " + (gameplay?.TypeId ?? "Missing")
                + " v" + (gameplay?.SchemaVersion ?? -1).ToString(CultureInfo.InvariantCulture)
                + "\nSession authority " + (authority?.TypeId ?? "Missing")
                + " v" + (authority?.SchemaVersion ?? -1).ToString(CultureInfo.InvariantCulture)
                + " · hash " + capture.SessionAuthorityHash.ToString("X16", CultureInfo.InvariantCulture);
        }

        private static string FormatReplayAuthorityIdentity(CharacterTestReplayCapture capture)
        {
            CharacterTestReplaySessionAuthorityIdentity identity = capture.SessionAuthorityIdentity;
            if (identity == null)
                return "Catalog identity unavailable";
            var builder = new StringBuilder(256);
            builder.Append("Catalog ").Append(identity.CatalogId)
                .Append(" r").Append(identity.CatalogRevision)
                .Append(" · ").Append(identity.CatalogHash.ToString("X16", CultureInfo.InvariantCulture))
                .Append(" · contract ").Append(identity.RuntimeContractVersion)
                .Append("\nRoster ").Append(identity.AgentCount);
            for (int i = 0; i < identity.Decks.Count; i++)
            {
                CharacterTestReplayDeckIdentity deck = identity.Decks[i];
                builder.Append("\n").Append(deck.CharacterStableId)
                    .Append(" · deck ").Append(deck.DeckHash.ToString("X16", CultureInfo.InvariantCulture))
                    .Append(" · cards ").Append(deck.Cards.Count);
            }
            return builder.ToString();
        }

        private static string FormatReplayResultEvidence(CharacterTestReplayValidationResult result)
        {
            if (result == null)
                return string.Empty;
            var builder = new StringBuilder(192);
            if (result.HasFailureFrame)
                builder.Append("CombinedAuthority · Frame ").Append(result.FailureFrame.Value).Append('\n');
            builder.Append("Expected ")
                .Append(result.ExpectedHash.ToString("X16", CultureInfo.InvariantCulture))
                .Append(" · Actual ")
                .Append(result.ActualHash.ToString("X16", CultureInfo.InvariantCulture));
            if (result.CommandErrors.Count > 0)
                builder.Append("\nCommand errors ").Append(result.CommandErrors.Count);
            if (!string.IsNullOrEmpty(result.ExpectedDiagnostics))
                builder.Append("\nRecorded: ").Append(result.ExpectedDiagnostics);
            if (!string.IsNullOrEmpty(result.ActualDiagnostics))
                builder.Append("\nPlayback: ").Append(result.ActualDiagnostics);
            return builder.ToString();
        }

        internal static long ComputeTimelineEvidenceHash(
            CharacterTestRuntimeAiLabTimelineProjection projection,
            IReadOnlyList<CharacterTestRuntimeAiLabReplayCausalSnapshot> causalSnapshots = null)
        {
            if (projection == null)
                return 0L;
            var hash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerLabFrameState, RuntimeHashSchemaIds.Version1);
            hash.AddLong(ReplayCausalHashFields.TimelineRevision, projection.ContentRevision);
            hash.AddInt(ReplayCausalHashFields.TimelineSessionStart, projection.Domain.SessionStartFrame);
            hash.AddInt(ReplayCausalHashFields.TimelineRetainedStart, projection.Domain.RetainedStartFrame);
            hash.AddInt(ReplayCausalHashFields.TimelineLatest, projection.Domain.LatestFrame);
            hash.AddInt(ReplayCausalHashFields.TimelineTrackCount, projection.Tracks.Count);
            for (int trackIndex = 0; trackIndex < projection.Tracks.Count; trackIndex++)
            {
                CharacterTestRuntimeAiLabTimelineTrackProjection track = projection.Tracks[trackIndex];
                hash.AddInt(ReplayCausalHashFields.TimelineTrackIndex, trackIndex);
                hash.AddInt(ReplayCausalHashFields.TimelineTrackKind, (int)track.Kind);
                hash.AddInt(ReplayCausalHashFields.TimelineItemCount, track.Items.Count);
                for (int itemIndex = 0; itemIndex < track.Items.Count; itemIndex++)
                {
                    CharacterTestRuntimeAiLabTimelineItem item = track.Items[itemIndex];
                    hash.AddInt(ReplayCausalHashFields.TimelineItemIndex, itemIndex);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemKind, (int)item.ItemKind);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemProvenance, (int)item.Provenance);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemStart, item.StartFrame);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemEnd, item.EndFrame);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemEvent, (int)item.EventKind);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemOccurrence, item.OccurrenceRank);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemAgent, item.AgentEntityId);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemRelated, item.RelatedEntityId);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemTraceKind, item.SourceTraceIdentity.KindId);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemTraceSource, item.SourceTraceIdentity.SourceId);
                    hash.AddLong(ReplayCausalHashFields.TimelineItemTraceExecution, item.SourceTraceIdentity.ExecutionId);
                    hash.AddLong(ReplayCausalHashFields.TimelineItemTraceSequence, item.SourceTraceIdentity.Sequence);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemBehaviorEntry, item.BehaviorEntryNumericId);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemBehaviorStep, item.BehaviorStepIndex);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemEvent, item.BehaviorEventIndex);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemOccurrence, item.BehaviorTransitionIndex);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemProvenance, item.BehaviorTargetTimelineIndex);
                    hash.AddInt(ReplayCausalHashFields.TimelineItemSession, item.SessionGeneration);
                    hash.AddLong(ReplayCausalHashFields.TimelineItemRecord, item.RecordGeneration);
                    hash.AddLong(ReplayCausalHashFields.TimelineItemAuthority, item.AuthorityHash);
                }
            }
            int causalCount = causalSnapshots?.Count ?? 0;
            hash.AddInt(ReplayCausalHashFields.TimelineCausalCount, causalCount);
            for (int i = 0; i < causalCount; i++)
            {
                CharacterTestRuntimeAiLabReplayCausalSnapshot causal = causalSnapshots[i];
                hash.AddInt(ReplayCausalHashFields.TimelineCausalIndex, i);
                hash.AddInt(ReplayCausalHashFields.TimelineCausalFrame, causal?.ReplayFrame ?? -1);
                hash.AddInt(ReplayCausalHashFields.TimelineCausalAgent, causal?.AgentEntityId ?? 0);
                hash.AddLong(ReplayCausalHashFields.TimelineCausalContent, causal?.EvidenceContentHash ?? 0L);
            }
            return hash.ToHash();
        }

        internal static CharacterTestRuntimeAiLabTimelineProjection BuildReplayTimelineSidecarProjection(
            CharacterTestRuntimeAiLabTimelineProjection source,
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestReplayCapture capture,
            CharacterTestReplayRecordIdentity identity,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (source == null
                || timeline == null
                || capture?.RuntimeReplaySnapshot == null
                || identity == null
                || !identity.IsValid
                || !source.IsComplete
                || !timeline.SessionIdentity.IsValid
                || identity.StartFrame < source.Domain.RetainedStartFrame
                || identity.EndFrame > source.Domain.LatestFrame)
            {
                diagnostic = "Typed timeline evidence is outside the frozen Replay range.";
                return null;
            }

            int sessionGeneration = timeline.SessionIdentity.Generation;
            IReadOnlyList<RuntimeReplayFrameRecord> replayRecords =
                capture.RuntimeReplaySnapshot.Records;
            for (int recordIndex = 0; recordIndex < replayRecords.Count; recordIndex++)
            {
                RuntimeReplayFrameRecord replayRecord = replayRecords[recordIndex];
                int frame = (int)replayRecord.Frame.Value;
                int timelineFrame = frame < int.MaxValue ? frame + 1 : frame;
                if (!timeline.TryGetFrame(
                        timelineFrame,
                        out CharacterTestRuntimeAiPlannerDevTimelineRecord timelineRecord)
                    || timelineRecord.RecordGeneration <= 0L
                    || !timelineRecord.HasReplayAuthorityEvidence
                    || timelineRecord.ReplayAuthorityFrame != frame)
                {
                    diagnostic = "Typed timeline evidence is missing RuntimeFrame "
                        + frame.ToString(CultureInfo.InvariantCulture) + ".";
                    return null;
                }
                if (timelineRecord.ReplayAuthorityHash != replayRecord.ResultHash)
                {
                    diagnostic = "Typed timeline authority hash mismatch at RuntimeFrame "
                        + frame.ToString(CultureInfo.InvariantCulture) + ".";
                    return null;
                }
            }

            var tracks = new CharacterTestRuntimeAiLabTimelineTrackProjection[source.Tracks.Count];
            for (int trackIndex = 0; trackIndex < source.Tracks.Count; trackIndex++)
            {
                CharacterTestRuntimeAiLabTimelineTrackProjection track = source.Tracks[trackIndex];
                var items = new List<CharacterTestRuntimeAiLabTimelineItem>(track.Items.Count);
                for (int itemIndex = 0; itemIndex < track.Items.Count; itemIndex++)
                {
                    CharacterTestRuntimeAiLabTimelineItem item = track.Items[itemIndex];
                    int replayItemStart = Math.Max(0, item.StartFrame - 1);
                    int replayItemEnd = Math.Max(replayItemStart, item.EndFrame - 1);
                    if (replayItemEnd < identity.StartFrame || replayItemStart > identity.EndFrame)
                        continue;
                    if ((item.SessionGeneration > 0 && item.SessionGeneration != sessionGeneration)
                        || (item.RecordGeneration > 0L
                            && (!timeline.TryGetFrame(
                                    item.StartFrame,
                                    out CharacterTestRuntimeAiPlannerDevTimelineRecord itemRecord)
                                || itemRecord.RecordGeneration != item.RecordGeneration)))
                    {
                        diagnostic = "Typed timeline provenance is stale for " + item.StableKey + ".";
                        return null;
                    }
                    items.Add(CloneReplayTimelineItem(
                        item,
                        identity.StartFrame,
                        identity.EndFrame,
                        frameOffset: -1));
                }
                tracks[trackIndex] = new CharacterTestRuntimeAiLabTimelineTrackProjection(
                    track.Kind,
                    track.Label,
                    items.ToArray(),
                    track.Available,
                    track.UnavailableMessage);
            }

            int frameCount = Math.Max(1, identity.EndFrame - identity.StartFrame + 1);
            var domain = new CharacterTestRuntimeAiLabTimelineDomain(
                identity.StartFrame,
                identity.StartFrame,
                identity.EndFrame,
                identity.EndFrame,
                frameCount,
                identity.FrameCount);
            var viewport = new CharacterTestRuntimeAiLabTimelineViewport(
                identity.StartFrame,
                identity.EndFrame,
                0,
                source.Viewport.PixelWidth,
                false,
                CharacterTestRuntimeAiLabTimelineViewportMode.FitRetained);
            return new CharacterTestRuntimeAiLabTimelineProjection(
                source.ContentRevision,
                domain,
                viewport,
                tracks,
                source.StatusMessage,
                source.Validity,
                source.DiagnosticFrame,
                source.DiagnosticValue);
        }

        internal static CharacterTestRuntimeAiLabTimelineProjection BuildReplayDivergenceProjection(
            CharacterTestRuntimeAiLabTimelineProjection source,
            CharacterTestReplayValidationResult result,
            long operationId)
        {
            if (source == null || result == null || !result.HasFailureFrame)
                return source;
            int frame = (int)result.FailureFrame.Value;
            var tracks = new CharacterTestRuntimeAiLabTimelineTrackProjection[source.Tracks.Count];
            for (int trackIndex = 0; trackIndex < source.Tracks.Count; trackIndex++)
            {
                CharacterTestRuntimeAiLabTimelineTrackProjection track = source.Tracks[trackIndex];
                if (track.Kind != CharacterTestRuntimeAiLabTimelineTrackKind.Warnings)
                {
                    tracks[trackIndex] = track;
                    continue;
                }
                var items = new CharacterTestRuntimeAiLabTimelineItem[track.Items.Count + 1];
                for (int i = 0; i < track.Items.Count; i++)
                    items[i] = track.Items[i];
                items[items.Length - 1] = new CharacterTestRuntimeAiLabTimelineItem(
                    "replay-divergence:" + operationId.ToString(CultureInfo.InvariantCulture)
                        + ":" + frame.ToString(CultureInfo.InvariantCulture),
                    CharacterTestRuntimeAiLabTimelineTrackKind.Warnings,
                    CharacterTestRuntimeAiLabTimelineItemKind.Marker,
                    CharacterTestRuntimeAiLabTimelineItemProvenance.PresentationAggregate,
                    frame,
                    frame,
                    false,
                    0,
                    0,
                    "CombinedAuthority",
                    CharacterTestRuntimeAiLabTimelineSeverity.Error,
                    "回放结果不一致",
                    "回放结果与当前会话不一致，请定位到该帧查看事件。",
                    1,
                    "replay.validation",
                    0);
                tracks[trackIndex] = new CharacterTestRuntimeAiLabTimelineTrackProjection(
                    track.Kind,
                    track.Label,
                    items,
                    track.Available,
                    track.UnavailableMessage);
            }

            CharacterTestRuntimeAiLabTimelineDomain old = source.Domain;
            var domain = new CharacterTestRuntimeAiLabTimelineDomain(
                old.SessionStartFrame,
                old.RetainedStartFrame,
                old.LatestFrame,
                frame,
                old.FrameLimit,
                old.RetainedRecordCount);
            return new CharacterTestRuntimeAiLabTimelineProjection(
                source.ContentRevision ^ operationId,
                domain,
                source.Viewport,
                tracks,
                result.Summary,
                source.Validity,
                source.DiagnosticFrame,
                source.DiagnosticValue);
        }

        private static CharacterTestRuntimeAiLabTimelineItem CloneReplayTimelineItem(
            CharacterTestRuntimeAiLabTimelineItem item,
            int startFrame,
            int endFrame,
            int frameOffset)
        {
            var members = new CharacterTestRuntimeAiLabTimelineItem[item.Members.Count];
            for (int i = 0; i < members.Length; i++)
                members[i] = item.Members[i];
            return new CharacterTestRuntimeAiLabTimelineItem(
                item.StableKey,
                item.TrackKind,
                item.ItemKind,
                item.Provenance,
                Math.Max(startFrame, item.StartFrame + frameOffset),
                Math.Min(endFrame, item.EndFrame + frameOffset),
                item.IsOpen && item.EndFrame + frameOffset >= endFrame,
                item.AgentEntityId,
                item.RelatedEntityId,
                item.Category,
                item.Severity,
                item.Label,
                item.Tooltip,
                item.Count,
                item.SourceTraceId,
                item.SourceOrder,
                item.Value,
                members,
                item.BehaviorLoadoutId,
                item.BehaviorEntryId,
                item.BehaviorStepId,
                item.EventKind,
                item.OccurrenceRank,
                item.ProfileSlot,
                item.CardInstanceStartFrame,
                item.SessionGeneration,
                item.RecordGeneration,
                item.AuthorityHash);
        }
    }
}
