using System;
using System.Collections.Generic;
using MxFramework.Network;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        internal bool IsShellSetupPhase =>
            _phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup;

        internal bool HasShellRuntimeHistory =>
            _frameBuffer.Count > 0 || _timeline.Count > 0;

        internal bool TryNormalizeShellWorkspaceContext(
            CharacterTestRuntimeAiLabWorkspace workspace,
            CharacterTestRuntimeAiLabPageContext context,
            out CharacterTestRuntimeAiLabPageContext normalizedContext,
            out string diagnostic)
        {
            normalizedContext = context;
            diagnostic = string.Empty;
            if (context == null)
            {
                diagnostic = "Runtime AI Lab page context is unavailable.";
                return false;
            }

            switch (workspace)
            {
                case CharacterTestRuntimeAiLabWorkspace.PlayerDeck:
                case CharacterTestRuntimeAiLabWorkspace.TacticalLibrary:
                    return true;
                case CharacterTestRuntimeAiLabWorkspace.Setup:
                    if (IsShellSetupPhase)
                        return true;
                    diagnostic = "Setup is unavailable after the runtime session starts.";
                    return false;
                case CharacterTestRuntimeAiLabWorkspace.BehaviorGroup:
                    if (IsShellSetupPhase
                        && DraftWorkspace.TryGetGroupForSlot(
                            _selectedSetupTeamId,
                            _selectedSetupSlotIndex,
                            out CharacterTestRuntimeAiLabBehaviorGroupDraft selectedGroup)
                        && selectedGroup != null
                        && selectedGroup.HasProjectSource)
                    {
                        return true;
                    }
                    diagnostic = "A valid project JSON behavior-group revision is required.";
                    return false;
                case CharacterTestRuntimeAiLabWorkspace.BehaviorCard:
                    if (!TryResolveBehaviorCardShellContext(
                            context,
                            out _,
                            out _,
                            out int selectedIndex))
                    {
                        diagnostic = "The selected behavior-card draft no longer exists.";
                        return false;
                    }
                    if (context.SelectedElementKind != CharacterTestRuntimeAiLabSelectedElementKind.None
                        && selectedIndex < 0)
                    {
                        normalizedContext = context.WithBehaviorCard(
                            context.GroupDraftId,
                            context.EntryId,
                            context.CardDraftId,
                            CharacterTestRuntimeAiLabSelectedElementKind.None,
                            string.Empty,
                            context.BehaviorCardViewport);
                    }
                    return true;
                case CharacterTestRuntimeAiLabWorkspace.Facts:
                    if (!IsShellSetupPhase)
                        return true;
                    diagnostic = "Facts requires an active runtime session.";
                    return false;
                case CharacterTestRuntimeAiLabWorkspace.RuntimeFrame:
                case CharacterTestRuntimeAiLabWorkspace.Replay:
                    if (!IsShellSetupPhase && HasShellRuntimeHistory)
                        return true;
                    diagnostic = workspace + " requires retained RuntimeFrame history.";
                    return false;
                default:
                    diagnostic = "Unknown Runtime AI Lab workspace.";
                    return false;
            }
        }

        internal bool StartSessionThroughShell(
            CharacterTestRuntimeAiLabTimelineMode? successTimelineMode = null)
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _shellCoordinator;
            if (coordinator == null)
            {
                bool success = StartArenaSession();
                if (!success) LogTestStartFailure("start-session", "StartArenaSession", _statusMessage);
                return success;
            }

            CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
            CharacterTestRuntimeAiLabWorkspace successTarget =
                state.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                    ? CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                    : CharacterTestRuntimeAiLabWorkspace.RuntimeFrame;
            if (!TryGetAvailableDraftWorkspace(out _, out string workspaceDiagnostic))
            {
                _statusMessage = workspaceDiagnostic;
                LogTestStartFailure("start-session", "Workspace", workspaceDiagnostic);
                return false;
            }
            SessionStartRecoverySnapshot recovery = CaptureSessionStartRecoverySnapshot();
            CharacterTestRuntimeAiLabNavigationResult result = coordinator.BeginSessionStart(
                state.Generation,
                state.Revision,
                state.ActiveWorkspace,
                successTarget,
                recovery,
                StartArenaSession,
                RecoverFailedSessionStart,
                successTimelineMode);
            bool started = result.Success
                && coordinator.CurrentState.LastCompletedOutcome ==
                    CharacterTestRuntimeAiLabSessionStartOutcome.Succeeded;
            if (!started)
            {
                LogTestStartFailure("start-session", result.Code, _statusMessage);
                return false;
            }

            return true;
        }

        // Explicit request/terminal-result boundary only; never called by per-frame eligibility checks.
        private void LogTestStartFailure(string operation, string stage, string message, string cardId = null)
        {
            UnityEngine.Debug.LogError("[Runtime AI Lab][TestStartFailed] operation=" + operation
                + " stage=" + stage + " workspace=" + DraftWorkspace?.WorkspaceId
                + " team=" + _selectedSetupTeamId + " slot=" + _selectedSetupSlotIndex
                + " character=" + (_matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0)
                + " card=" + (cardId ?? _activeTacticalCardId ?? string.Empty)
                + " reason=" + (string.IsNullOrWhiteSpace(message) ? "启动未完成，未提供诊断。" : message.Replace('\r', ' ').Replace('\n', ' ')), this);
        }

        internal bool TryCaptureBehaviorCardShellContext(
            int entryIndex,
            CharacterTestRuntimeAiLabLogicElementKind selectedKind,
            int selectedIndex,
            CharacterTestRuntimeAiLabBehaviorCardViewport viewport,
            out CharacterTestRuntimeAiLabPageContext context)
        {
            context = null;
            CharacterTestRuntimeAiLabBehaviorGroupDraft group;
            if (!TryGetSelectedAuthoringGroup(out group)
                || group == null
                || entryIndex < 0
                || entryIndex >= group.Entries.Count)
            {
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
            CharacterTestRuntimeAiLabBehaviorCardDraft card;
            if (entry == null
                || !DraftWorkspace.Cards.TryGetValue(entry.CardDraftId, out card)
                || card == null)
            {
                return false;
            }

            CharacterTestRuntimeAiLabSelectedElementKind shellKind;
            string stableId;
            ResolveSelectedElementIdentity(card, selectedKind, selectedIndex, out shellKind, out stableId);
            CharacterTestRuntimeAiLabPageContext current = _shellCoordinator != null
                ? _shellCoordinator.CurrentState.PageContext
                : CharacterTestRuntimeAiLabPageContext.CreateDefault();
            context = current.WithBehaviorCard(
                group.GroupDraftId,
                entry.EntryId,
                card.CardDraftId,
                shellKind,
                stableId,
                viewport);
            return true;
        }

        internal bool TryResolveBehaviorCardShellContext(
            CharacterTestRuntimeAiLabPageContext context,
            out int entryIndex,
            out CharacterTestRuntimeAiLabLogicElementKind selectedKind,
            out int selectedIndex)
        {
            entryIndex = -1;
            selectedKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            selectedIndex = -1;
            if (context == null
                || string.IsNullOrEmpty(context.GroupDraftId)
                || string.IsNullOrEmpty(context.EntryId)
                || string.IsNullOrEmpty(context.CardDraftId)
                || !TryGetAuthoringGroup(
                    context.GroupDraftId,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || !DraftWorkspace.Cards.TryGetValue(
                    context.CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card))
            {
                return false;
            }

            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (entry != null
                    && string.Equals(entry.EntryId, context.EntryId, StringComparison.Ordinal)
                    && string.Equals(entry.CardDraftId, context.CardDraftId, StringComparison.Ordinal))
                {
                    entryIndex = i;
                    break;
                }
            }
            if (entryIndex < 0)
                return false;

            if (context.SelectedElementKind == CharacterTestRuntimeAiLabSelectedElementKind.None
                || string.IsNullOrEmpty(context.SelectedElementStableId))
            {
                selectedIndex = -1;
                return true;
            }

            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic = card.LogicPayload;
            switch (context.SelectedElementKind)
            {
                case CharacterTestRuntimeAiLabSelectedElementKind.Detector:
                    selectedKind = CharacterTestRuntimeAiLabLogicElementKind.Detector;
                    selectedIndex = FindShellDetectorIndex(logic, context.SelectedElementStableId);
                    break;
                case CharacterTestRuntimeAiLabSelectedElementKind.Transition:
                    selectedKind = CharacterTestRuntimeAiLabLogicElementKind.Transition;
                    selectedIndex = FindShellTransitionIndex(logic, context.SelectedElementStableId);
                    break;
                default:
                    selectedKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
                    selectedIndex = FindShellStageIndex(logic, context.SelectedElementStableId);
                    break;
            }

            // Stable card identity remains valid when a selected element was deleted. Do not
            // clamp to another element and silently change the user's selection.
            if (selectedIndex < 0)
                selectedKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            return true;
        }

        private static void ResolveSelectedElementIdentity(
            CharacterTestRuntimeAiLabBehaviorCardDraft card,
            CharacterTestRuntimeAiLabLogicElementKind selectedKind,
            int selectedIndex,
            out CharacterTestRuntimeAiLabSelectedElementKind shellKind,
            out string stableId)
        {
            shellKind = CharacterTestRuntimeAiLabSelectedElementKind.None;
            stableId = string.Empty;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic = card?.LogicPayload;
            if (logic == null || selectedIndex < 0)
                return;

            switch (selectedKind)
            {
                case CharacterTestRuntimeAiLabLogicElementKind.Detector:
                    if (logic.Detectors != null && selectedIndex < logic.Detectors.Count)
                    {
                        shellKind = CharacterTestRuntimeAiLabSelectedElementKind.Detector;
                        stableId = logic.Detectors[selectedIndex]?.DetectorId ?? string.Empty;
                    }
                    break;
                case CharacterTestRuntimeAiLabLogicElementKind.Transition:
                    if (logic.Transitions != null && selectedIndex < logic.Transitions.Count)
                    {
                        shellKind = CharacterTestRuntimeAiLabSelectedElementKind.Transition;
                        stableId = logic.Transitions[selectedIndex]?.TransitionId ?? string.Empty;
                    }
                    break;
                default:
                    if (logic.Stages != null && selectedIndex < logic.Stages.Count)
                    {
                        shellKind = CharacterTestRuntimeAiLabSelectedElementKind.Stage;
                        stableId = logic.Stages[selectedIndex]?.StageId ?? string.Empty;
                    }
                    break;
            }
            if (string.IsNullOrEmpty(stableId))
                shellKind = CharacterTestRuntimeAiLabSelectedElementKind.None;
        }

        private static int FindShellStageIndex(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic,
            string stableId)
        {
            for (int i = 0; logic?.Stages != null && i < logic.Stages.Count; i++)
            {
                if (string.Equals(logic.Stages[i]?.StageId, stableId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static int FindShellDetectorIndex(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic,
            string stableId)
        {
            for (int i = 0; logic?.Detectors != null && i < logic.Detectors.Count; i++)
            {
                if (string.Equals(logic.Detectors[i]?.DetectorId, stableId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static int FindShellTransitionIndex(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic,
            string stableId)
        {
            for (int i = 0; logic?.Transitions != null && i < logic.Transitions.Count; i++)
            {
                if (string.Equals(logic.Transitions[i]?.TransitionId, stableId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private SessionStartRecoverySnapshot CaptureSessionStartRecoverySnapshot()
        {
            using var startupProfile = CharacterTestStartupProfiler.CaptureRecovery.Auto();
            return new SessionStartRecoverySnapshot(this);
        }

        private bool RecoverFailedSessionStart(object candidate)
        {
            var snapshot = candidate as SessionStartRecoverySnapshot;
            if (snapshot == null)
                return false;

            string failureStatus = _statusMessage;
            if (!snapshot.TryRestore(this, out string diagnostic))
            {
                _statusMessage = string.IsNullOrWhiteSpace(failureStatus)
                    ? diagnostic
                    : failureStatus + "\n" + diagnostic;
                return false;
            }
            bool exactRecovery = string.Equals(
                snapshot.Fingerprint,
                BuildSessionStartRecoveryFingerprint(),
                StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(failureStatus))
                _statusMessage = failureStatus;
            return exactRecovery;
        }

        private string BuildSessionStartRecoveryFingerprint()
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                AddHash(ref hash, (int)_phase);
                AddHash(ref hash, _selectedAgentIndex);
                AddHash(ref hash, _selectedSetupTeamId);
                AddHash(ref hash, _selectedSetupSlotIndex);
                AddHash(ref hash, _setupSnapEnabled ? 1 : 0);
                AddHash(ref hash, _debugEditGateEnabled ? 1 : 0);
                AddHash(ref hash, _previewOverlayEnabled ? 1 : 0);
                AddHash(ref hash, _detailsExpanded ? 1 : 0);
                AddHash(ref hash, (int)_authoringMode);
                AddHash(ref hash, (int)_candidateState);
                AddCandidateHash(ref hash, _candidateRevision);
                AddCandidateHash(ref hash, _activeRevision);
                AddHash(ref hash, _candidateApplyQueued ? 1 : 0);
                AddHash(ref hash, _candidateApplyFrame);
                AddCandidateHash(ref hash, _queuedCandidateRevision);
                AddHash(ref hash, _queuedApplyTarget.TimelineSessionGeneration);
                AddHash(ref hash, _queuedApplyTarget.SessionAuthorityHash);
                AddHash(ref hash, _queuedApplyTarget.SelectedAgentEntityId);
                AddHash(ref hash, _queuedApplyTarget.SelectedCharacterId);
                AddHash(ref hash, _queuedApplyTarget.RuntimeProfileSlot);
                AddHash(ref hash, _queuedApplyTarget.SetupTeamId);
                AddHash(ref hash, _queuedApplyTarget.SetupSlotIndex);
                AddHash(ref hash, _queuedApplyTarget.SetupCharacterId);
                AddHash(ref hash, _queuedApplyTarget.RuntimeLoadoutId);
                AddHash(ref hash, _queuedApplyTarget.RuntimeVariantId);
                AddHash(ref hash, _queuedApplyTarget.RuntimeCatalogHash);
                AddHash(ref hash, _queuedApplyTarget.GroupDraftId);
                AddHash(ref hash, _queuedApplyTarget.CandidateAuthorityHash);
                AddAppliedRevisionHash(ref hash, _baselineRevision);
                AddHash(ref hash, _baselineRevisionPeers.Count);
                for (int i = 0; i < _baselineRevisionPeers.Count; i++)
                    AddAppliedRevisionHash(ref hash, _baselineRevisionPeers[i]);
                AddHash(ref hash, _appliedRevisionEvents.Count);
                for (int i = 0; i < _appliedRevisionEvents.Count; i++)
                    AddAppliedRevisionHash(ref hash, _appliedRevisionEvents[i]);
                AddHash(ref hash, _draftWorkspace?.CurrentRevision ?? -1);
                AddMatchConfigHash(ref hash, _matchConfig);
                AddFrameBufferHash(ref hash, _frameBuffer);
                AddTimelineHash(ref hash, _timeline);
                AddHash(ref hash, _timeline?.ComputeDecisionTraceRecoveryHash() ?? 0L);
                AddHash(ref hash, _timelineEventIndex.ComputeRecoveryHash());
                AddHash(ref hash, _testStateCaptures.ComputeRecoveryHash());
                AddHash(ref hash, _lastCapturedRuntimeFrame);
                AddHash(ref hash, _lastTestStateCapturedFrame);
                AddHash(ref hash, _replayBootstrapEndFrame);
                AddHash(ref hash, _replayConfigurationRevision);
                AddHash(ref hash, _replayBootstrapConfig != null ? 1 : 0);
                AddMatchConfigHash(ref hash, _replayBootstrapConfig);
                AddHash(ref hash, _initialReplayBaseline?.AuthorityHash ?? 0L);
                AddHash(ref hash, _initialReplayBaseline != null
                    ? _initialReplayBaseline.Frame.Value
                    : -1L);
                AddHash(ref hash, _initialReplayBaseline?.ConfigurationRevision ?? -1);
                AddHash(ref hash, _lastReplayResult.Frame.Value);
                AddStringHash(ref hash, _lastReplayResult.Authority?.SummaryText);
                AddStringHash(ref hash, _lastReplayResult.Presentation?.SummaryText);
                AddHash(ref hash, _snapshotParticipant != null ? 1 : 0);
                AddHash(ref hash, _frameLimit);
                AddHash(ref hash, _timelineSessionStarted ? 1 : 0);
                AddHash(ref hash, _timelineSessionGeneration);
                AddHash(ref hash, _sessionContinuable ? 1 : 0);
                AddHash(ref hash, _timelineZoomLevel);
                AddHash(ref hash, _timelineViewStartOverride);
                AddHash(ref hash, _timelineExpandedOverride.HasValue
                    ? (_timelineExpandedOverride.Value ? 1 : 0)
                    : -1);
                AddHash(ref hash, _timelineScrubActive ? 1 : 0);
                AddHash(ref hash, _timelineScrubStartedFromHistory ? 1 : 0);
                AddHash(ref hash, _hasTimelineScrubDesiredFrame ? 1 : 0);
                AddHash(ref hash, _timelineScrubDesiredFrame);
                AddHash(ref hash, _futureAdvanceStartFrame);
                AddStringHash(ref hash, _selectedTimelineVisualStableKey);
                AddHash(ref hash, _loopPlaybackActive ? 1 : 0);
                AddHash(ref hash, _loopPlaybackAccumulatorSeconds);
                AddHash(ref hash, _branchRecomputeArmed ? 1 : 0);
                AddHash(ref hash, _branchConfirmationPending ? 1 : 0);
                AddHash(ref hash, _branchConfirmationGeneration);
                AddHash(ref hash, _branchConfirmationSessionGeneration);
                AddHash(ref hash, _branchConfirmationBaseAuthorityHash);
                AddHash(ref hash, _branchTruncatePending ? 1 : 0);
                AddHash(ref hash, _branchBaseFrame);
                AddHash(ref hash, _branchOriginalLatestFrame);
                AddHash(ref hash, _historyPlaybackActive ? 1 : 0);
                AddHash(ref hash, _historyPlaybackAccumulatorSeconds);
                AddHash(ref hash, _seekInProgress ? 1 : 0);
                AddHash(ref hash, _deterministicReplayWork != null ? 1 : 0);
                AddHash(ref hash, _lastSeekSucceeded ? 1 : 0);
                AddHash(ref hash, _resumeLiveAfterReplay ? 1 : 0);
                AddHash(ref hash, _activeSeekRequest.TargetFrame);
                AddHash(ref hash, (int)_activeSeekRequest.Origin);
                AddHash(ref hash, _activeSeekRequest.StayPaused ? 1 : 0);
                AddHash(ref hash, _activeSeekRequest.AgentEntityId);
                AddStringHash(ref hash, _activeSeekRequest.ItemStableKey);
                AddHash(ref hash, _committedFrameBeforeSeek);
                AddHash(ref hash, _committedAgentBeforeSeek);
                AddStringHash(ref hash, _failedSeekDiagnostic);
                AddHash(ref hash, _frameSelection.CommittedReceipt.CommitGeneration);
                AddHash(ref hash, _frameSelection.CommittedReceipt.Frame);
                AddHash(ref hash, _frameSelection.CommittedReceipt.ResolvedAgentEntityId);
                AddHash(ref hash, _frameSelection.CommittedReceipt.AuthorityHash);
                AddHash(ref hash, _context != null && _context.Paused ? 1 : 0);
                AddHash(ref hash, _context != null && _context.MainMenuVisible ? 1 : 0);
                AddHash(ref hash, _context != null && _context.ManualPaused ? 1 : 0);
                bool selectionUnlocked = _context?.Slice?.Services.RuntimeCommands != null
                    && _context.Slice.Services.RuntimeCommands.RuntimeAiBehaviorSelectionUnlocked;
                AddHash(ref hash, selectionUnlocked ? 1 : 0);
                CharacterTestNetworkSnapshotParticipant participant =
                    _context?.Slice?.Services.Network.CreateSnapshotParticipant();
                RuntimeFrame authorityFrame = _context?.Slice != null
                    ? _context.Slice.CurrentFrame
                    : default;
                AddHash(ref hash, authorityFrame.Value);
                AddHash(ref hash, participant != null
                    ? participant.ComputeStateHash(authorityFrame)
                    : 0L);
                AddStringHash(ref hash, _statusMessage);
                AddStringHash(ref hash, _candidateDiagnostic);
                return hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private static void AddHash(ref ulong hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 1099511628211UL;
            }
        }

        private static void AddHash(ref ulong hash, long value)
        {
            unchecked
            {
                hash ^= (ulong)value;
                hash *= 1099511628211UL;
            }
        }

        private static void AddHash(ref ulong hash, double value)
        {
            AddHash(ref hash, BitConverter.DoubleToInt64Bits(value));
        }

        private static void AddHash(ref ulong hash, float value)
        {
            AddHash(ref hash, BitConverter.SingleToInt32Bits(value));
        }

        private static void AddStringHash(ref ulong hash, string value)
        {
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                unchecked
                {
                    hash ^= text[i];
                    hash *= 1099511628211UL;
                }
            }
            AddHash(ref hash, -1);
        }

        private static void AddTraceHash(ref ulong hash, in RuntimeTraceIdentity trace)
        {
            AddHash(ref hash, trace.KindId); AddHash(ref hash, trace.SourceId);
            AddHash(ref hash, trace.ExecutionId); AddHash(ref hash, trace.Sequence);
        }

        private static void AddCandidateHash(
            ref ulong hash,
            CharacterTestRuntimeAiLabCandidateRevision revision)
        {
            AddHash(ref hash, revision?.Revision ?? 0);
            AddHash(ref hash, revision?.AuthorityHash ?? 0L);
            AddStringHash(ref hash, revision?.GroupDraftId);
        }

        private static void AddAppliedRevisionHash(
            ref ulong hash,
            CharacterTestRuntimeAiLabAppliedRevision revision)
        {
            AddHash(ref hash, revision?.ApplicationFrame ?? -1);
            AddHash(ref hash, revision?.ProfileSlot ?? -1);
            AddCandidateHash(ref hash, revision?.Revision);
        }

        private static void AddFrameBufferHash(
            ref ulong hash,
            CharacterTestRuntimeAiPlannerDevFrameBuffer buffer)
        {
            AddHash(ref hash, buffer?.Capacity ?? 0);
            AddHash(ref hash, buffer?.Count ?? 0);
            AddHash(ref hash, buffer?.SelectedIndex ?? -1);
            for (int i = 0; buffer != null && i < buffer.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDevFrameSnapshot frame = buffer.GetAt(i);
                AddFrameSnapshotHash(ref hash, frame);
            }
        }

        private static void AddTimelineHash(
            ref ulong hash,
            CharacterTestRuntimeAiPlannerDevTimeline timeline)
        {
            AddHash(ref hash, timeline?.Capacity ?? 0);
            AddHash(ref hash, timeline?.Count ?? 0);
            AddHash(ref hash, timeline?.SelectedIndex ?? -1);
            AddHash(ref hash, timeline != null ? (int)timeline.Mode : -1);
            AddHash(ref hash, timeline?.PlaybackSpeedIndex ?? -1);
            AddHash(ref hash, timeline != null && timeline.LoopEnabled ? 1 : 0);
            AddHash(ref hash, timeline?.LoopStartFrame ?? 0);
            AddHash(ref hash, timeline?.LoopEndFrame ?? 0);
            AddHash(ref hash, timeline?.EditBranchStartFrame ?? -1);
            AddHash(ref hash, timeline?.ContentRevision ?? 0L);
            IReadOnlyList<CharacterTestRuntimeAiPlannerDevTimelineRecord> records = timeline?.Records;
            for (int i = 0; records != null && i < records.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDevTimelineRecord record = records[i];
                AddHash(ref hash, record.Frame);
                AddHash(ref hash, BitConverter.SingleToInt32Bits(record.RealtimeSeconds));
                AddHash(ref hash, record.ElapsedSeconds);
                AddHash(ref hash, record.StateHash);
                AddHash(ref hash, record.HasCheckpoint ? 1 : 0);
                AddDecisionTraceHandleHash(ref hash, record.DecisionTraceHandle);
                AddFrameSnapshotHash(ref hash, record.FrameSnapshot);
                AddAuthorityCheckpointHash(ref hash, record.AuthorityCheckpoint);
                AddAnimationInputsHash(ref hash, record.AnimationInputs);
                AddHash(ref hash, record.DrainedCommands.Count);
                for (int j = 0; j < record.DrainedCommands.Count; j++)
                {
                    RuntimeCommand command = record.DrainedCommands[j];
                    AddHash(ref hash, command.Frame.Value);
                    AddHash(ref hash, command.SourceId);
                    AddHash(ref hash, command.CommandId);
                    AddHash(ref hash, command.TargetId);
                    AddHash(ref hash, command.Payload0);
                    AddHash(ref hash, command.Payload1);
                    AddHash(ref hash, command.Payload2);
                    AddHash(ref hash, command.Sequence);
                    AddTraceHash(ref hash, command.TraceId);
                }
            }
        }

        internal static long ComputeTimelineRecordContentHash(
            CharacterTestRuntimeAiPlannerDevTimelineRecord record)
        {
            if (record == null)
                return 0L;

            unchecked
            {
                ulong hash = 1469598103934665603UL;
                AddHash(ref hash, record.Frame);
                AddHash(ref hash, record.RecordGeneration);
                AddHash(ref hash, record.RealtimeSeconds);
                AddHash(ref hash, record.ElapsedSeconds);
                AddHash(ref hash, record.StateHash);
                AddHash(ref hash, record.HasCheckpoint ? 1 : 0);
                AddDecisionTraceHandleHash(ref hash, record.DecisionTraceHandle);
                AddFrameSnapshotHash(ref hash, record.FrameSnapshot);
                AddAuthorityCheckpointHash(ref hash, record.AuthorityCheckpoint);
                AddAnimationInputsHash(ref hash, record.AnimationInputs);
                AddHash(ref hash, record.DrainedCommands.Count);
                for (int i = 0; i < record.DrainedCommands.Count; i++)
                {
                    RuntimeCommand command = record.DrainedCommands[i];
                    AddHash(ref hash, command.Frame.Value);
                    AddHash(ref hash, command.SourceId);
                    AddHash(ref hash, command.CommandId);
                    AddHash(ref hash, command.TargetId);
                    AddHash(ref hash, command.Payload0);
                    AddHash(ref hash, command.Payload1);
                    AddHash(ref hash, command.Payload2);
                    AddHash(ref hash, command.Sequence);
                    AddTraceHash(ref hash, command.TraceId);
                }
                return (long)hash;
            }
        }

        private static void AddDecisionTraceHandleHash(
            ref ulong hash,
            in CharacterTestRuntimeAiLabDecisionTraceHandle handle)
        {
            AddHash(ref hash, handle.Slot);
            AddHash(ref hash, handle.SlotGeneration);
            AddHash(ref hash, handle.SessionGeneration);
            AddHash(ref hash, handle.Frame);
            AddHash(ref hash, handle.DecisionFrame);
        }

        private long ComputeAppliedBehaviorRevisionHash(int frame)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                AddAppliedRevisionHash(ref hash, _baselineRevision);
                for (int i = 0; i < _baselineRevisionPeers.Count; i++)
                    AddAppliedRevisionHash(ref hash, _baselineRevisionPeers[i]);
                for (int i = 0; i < _appliedRevisionEvents.Count; i++)
                {
                    CharacterTestRuntimeAiLabAppliedRevision applied = _appliedRevisionEvents[i];
                    if (applied != null && applied.ApplicationFrame <= frame)
                        AddAppliedRevisionHash(ref hash, applied);
                }
                return (long)hash;
            }
        }

        private CharacterTestRuntimeAiLabCommittedFrameBundle CreateCommittedFrameBundle(
            CharacterTestRuntimeAiPlannerDevTimelineRecord record)
        {
            bool presentationAvailable = _testStateCaptures.TryGet(
                record.Frame,
                out RuntimeAiLabTestStateCapture capture)
                && capture?.PresentationDiagnostics != null;
            long presentationGeneration = presentationAvailable ? record.RecordGeneration : 0L;
            long presentationHash = presentationAvailable
                ? CharacterTestRuntimeAiLabPresentationDiagnosticProbe.ComputeContentHash(
                    capture.PresentationDiagnostics)
                : 0L;
            var bundle = record.PreparedBundle;
            if (bundle == null || bundle.IsInitialized) bundle = new CharacterTestRuntimeAiLabCommittedFrameBundle();
            bundle.Initialize(
                _timeline.SessionIdentity,
                record,
                ComputeTimelineRecordContentHash(record),
                _timelineEventIndex.ComputeFrameContentHash(record.Frame),
                ComputeAppliedBehaviorRevisionHash(record.Frame),
                presentationGeneration,
                presentationHash,
                presentationAvailable);
            return bundle;
        }

        [ThreadStatic] private static RuntimeHashAccumulator _frameSnapshotHashAccumulator;

        private static void AddFrameSnapshotHash(
            ref ulong hash,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot frame)
        {
            if (frame == null)
            {
                AddHash(ref hash, 0);
                return;
            }

            AddHash(ref hash, 1);
            AddHash(ref hash, frame.Frame);
            AddHash(ref hash, frame.RealtimeSeconds);
            AddRuntimeAiPlannerSnapshotHash(ref hash, frame.RuntimeAiPlannerSnapshot);
            AddRuntimeEventSampleHash(ref hash, frame.RuntimeEvents);
            var stateHash = _frameSnapshotHashAccumulator ??= new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerLabFrameState, RuntimeHashSchemaIds.Version1);
            stateHash.Reset();
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(
                stateHash,
                frame.RuntimeAiPlannerStateSnapshot);
            AddHash(ref hash, stateHash.ToHash());
        }

        private static void AddRuntimeAiPlannerSnapshotHash(
            ref ulong hash,
            CharacterTestRuntimeAiPlannerSnapshot snapshot)
        {
            if (snapshot == null)
            {
                AddHash(ref hash, 0);
                return;
            }

            AddHash(ref hash, 1);
            AddHash(ref hash, snapshot.Available ? 1 : 0);
            AddStringHash(ref hash, snapshot.ProfileId);
            AddStringHash(ref hash, snapshot.ValidationReport);
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents = snapshot.Agents;
            AddHash(ref hash, agents?.Count ?? 0);
            for (int i = 0; agents != null && i < agents.Count; i++)
                AddRuntimeAiPlannerAgentSnapshotHash(ref hash, agents[i]);
        }

        private static void AddRuntimeAiPlannerAgentSnapshotHash(
            ref ulong hash,
            CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            AddHash(ref hash, agent.EntityId);
            AddHash(ref hash, agent.TargetEntityId);
            AddStringHash(ref hash, agent.ActiveStatePath);
            AddHash(ref hash, agent.TargetExists ? 1 : 0);
            AddHash(ref hash, agent.TargetInAttackRange ? 1 : 0);
            AddHash(ref hash, agent.TargetDistance);
            AddHash(ref hash, agent.TargetDistanceAvailable ? 1 : 0);
            AddHash(ref hash, agent.TargetRootDistance);
            AddHash(ref hash, agent.HasScenePreviewDistanceWitness ? 1 : 0);
            AddHash(ref hash, agent.ScenePreviewDistanceSourceX);
            AddHash(ref hash, agent.ScenePreviewDistanceSourceY);
            AddHash(ref hash, agent.ScenePreviewDistanceSourceZ);
            AddHash(ref hash, agent.ScenePreviewDistanceTargetX);
            AddHash(ref hash, agent.ScenePreviewDistanceTargetY);
            AddHash(ref hash, agent.ScenePreviewDistanceTargetZ);
            AddHash(ref hash, agent.ScenePreviewDistanceGeometryFrame);
            AddHash(ref hash, agent.TargetLogicalHurtboxId);
            AddHash(ref hash, agent.MovePathAvailable ? 1 : 0);
            AddHash(ref hash, agent.TargetVisible ? 1 : 0);
            AddHash(ref hash, agent.TargetDead ? 1 : 0);
            AddHash(ref hash, agent.TargetLineOfSightBlocked ? 1 : 0);
            AddHash(ref hash, agent.TargetLastSeenAgeFrames);
            AddHash(ref hash, agent.TargetThreatScore);
            AddHash(ref hash, agent.SelfSurfaceKind);
            AddHash(ref hash, agent.SelfOnHardSlick ? 1 : 0);
            AddHash(ref hash, agent.SelfNearMovableObject ? 1 : 0);
            AddHash(ref hash, agent.MoveDirectPathClear ? 1 : 0);
            AddHash(ref hash, agent.MoveCandidateIndex);
            CharacterTestRuntimeAiPlannerNavigationDiagnostics navigationDiagnostics =
                agent.NavigationDiagnostics;
            AddHash(ref hash, navigationDiagnostics.HasSample ? 1 : 0);
            if (navigationDiagnostics.HasSample)
            {
                CharacterTestNavigationSample navigation = navigationDiagnostics.Sample;
                AddHash(ref hash, navigation.Frame.Value);
                AddHash(ref hash, navigation.SourceEntityId);
                AddHash(ref hash, navigation.TargetEntityId);
                AddHash(ref hash, navigation.PhysicsWorldRevision);
                AddHash(ref hash, (int)navigation.SessionBuildStatus);
                AddHash(ref hash, (int)navigation.SceneInputStatus);
                AddHash(ref hash, (int)navigation.PathStatus);
                AddHash(ref hash, navigation.GraphRevision);
                AddHash(ref hash, navigation.GraphContentHash);
                AddHash(ref hash, navigation.HasNextWaypoint ? 1 : 0);
                AddHash(ref hash, navigation.NextWaypoint.X.RawValue);
                AddHash(ref hash, navigation.NextWaypoint.Y.RawValue);
                AddHash(ref hash, navigation.NextWaypoint.Z.RawValue);
                AddHash(ref hash, navigation.RemainingDistanceMillimeters);
                AddHash(ref hash, navigation.TotalCostMillimeters);
                AddHash(ref hash, navigation.ExpandedNodeCount);
                AddHash(ref hash, navigation.PathQueryCount);
                AddHash(ref hash, navigation.CombatQueryCount);
                AddHash(ref hash, navigation.SegmentPathHitCount);
                AddHash(ref hash, navigation.SegmentObstacleEntityId.Value);
                AddHash(ref hash, navigation.SegmentObstacleBodyId.Value);
                AddHash(ref hash, navigation.SegmentObstacleDistance.RawValue);
                AddHash(ref hash, (int)navigation.ResolutionStatus);
                AddHash(ref hash, (int)navigation.ReplanReason);
                AddHash(ref hash, (int)navigation.BlockedReason);
                AddHash(ref hash, (int)navigation.FallbackReason);
            }
            AddHash(ref hash, agent.EnvironmentNearObstacle ? 1 : 0);
            AddHash(ref hash, agent.EnvironmentNearHazard ? 1 : 0);
            AddHash(ref hash, agent.EnvironmentNearUsefulObject ? 1 : 0);
            AddHash(ref hash, agent.MovementSpeedScale);
            AddHash(ref hash, agent.MovementStrafeSide);
            AddHash(ref hash, agent.MovementDirectionHoldFrames);
            AddHash(ref hash, agent.MovementReactionDelayFrames);
            AddHash(ref hash, agent.HasScenePreviewGeometry ? 1 : 0);
            AddHash(ref hash, agent.ScenePreviewSourceCenterX);
            AddHash(ref hash, agent.ScenePreviewSourceCenterY);
            AddHash(ref hash, agent.ScenePreviewSourceCenterZ);
            AddHash(ref hash, agent.ScenePreviewTargetCenterX);
            AddHash(ref hash, agent.ScenePreviewTargetCenterY);
            AddHash(ref hash, agent.ScenePreviewTargetCenterZ);
            AddHash(ref hash, agent.ScenePreviewTargetDirectionX);
            AddHash(ref hash, agent.ScenePreviewTargetDirectionZ);
            AddHash(ref hash, agent.ScenePreviewTargetBearingMilliDegrees);
            AddHash(ref hash, agent.ScenePreviewFacingDeltaMilliDegrees);
            AddHash(ref hash, agent.ScenePreviewFacingDot);
            AddHash(ref hash, agent.ScenePreviewDecisionMoveX);
            AddHash(ref hash, agent.ScenePreviewDecisionMoveZ);
            AddHash(ref hash, agent.ScenePreviewAttackRange);
            AddHash(ref hash, agent.RuntimeAiPlannerProfileSlot);
            AddHash(ref hash, agent.RuntimeAiBehaviorLoadoutId);
            AddHash(ref hash, agent.RuntimeAiBehaviorVariantId);
            AddHash(ref hash, agent.RuntimeAiBehaviorCatalogHash);
            AddHash(ref hash, agent.RuntimeAiBehaviorProfileSlot);
            AddHash(ref hash, agent.SelectedAbilitySlot);
            AddHash(ref hash, agent.TargetAttackActionId.Value);
            AddHash(ref hash, agent.TargetAttackSequence);
            AddHash(ref hash, agent.TargetAttackImpactFrames);
            AddHash(ref hash, agent.TargetAttackWillHitSelf ? 1 : 0);
            AddHash(ref hash, agent.TargetAttackInstanceResponded ? 1 : 0);
            AddHash(ref hash, agent.SelfDodgeCooldownReady ? 1 : 0);
            AddHash(ref hash, agent.NextDodgeAllowedFrame);
            AddHash(ref hash, agent.SelectedUtilityScore);
            AddHash(ref hash, agent.SelectedActionCost);
            AddHash(ref hash, agent.SelectedActionId);
            AddStringHash(ref hash, agent.SelectedActionName);
            AddHash(ref hash, (int)agent.SelectedActionKind);
            AddStringHash(ref hash, agent.LastCommand);
            AddStringHash(ref hash, agent.SuppressedReason);
            AddStringHash(ref hash, agent.RejectReason);
            AddHash(ref hash, agent.TransitionFromStateId);
            AddHash(ref hash, agent.TransitionToStateId);
        }

        private static void AddRuntimeEventSampleHash(
            ref ulong hash,
            CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample sample)
        {
            AddHash(ref hash, sample.Available ? 1 : 0);
            AddHash(ref hash, sample.PendingCount);
            AddHash(ref hash, sample.OldestFrame);
            AddHash(ref hash, sample.NewestFrame);
            AddHash(ref hash, sample.NextSequence);
            AddStringHash(ref hash, sample.EventTypeName);
            AddHash(ref hash, sample.RecentCount);
            AddHash(ref hash, sample.RecentHash);
            AddHash(ref hash, sample.LastFrame);
            AddHash(ref hash, sample.LastType);
            AddHash(ref hash, sample.LastCommandId);
            AddHash(ref hash, sample.LastSourceId);
            AddHash(ref hash, sample.LastTargetId);
            AddHash(ref hash, sample.LastEntityId);
            AddHash(ref hash, sample.LastPayload0);
            AddHash(ref hash, sample.LastPayload1);
            AddHash(ref hash, sample.LastPayload2);
            AddStringHash(ref hash, sample.LastMessage);
        }

        private static void AddAuthorityCheckpointHash(
            ref ulong hash,
            CharacterTestAuthorityRollbackCheckpoint checkpoint)
        {
            if (checkpoint == null)
            {
                AddHash(ref hash, 0);
                return;
            }

            AddHash(ref hash, 1);
            AddHash(ref hash, checkpoint.Frame.Value);
            AddStringHash(
                ref hash,
                checkpoint.Document != null
                    ? JsonConvert.SerializeObject(checkpoint.Document, Formatting.None)
                    : string.Empty);
        }

        private static void AddAnimationInputsHash(
            ref ulong hash,
            CharacterTestRuntimeAiLabFrozenArray<CharacterTestAnimationInputPresentationState> inputs)
        {
            AddHash(ref hash, inputs.Count);
            for (int i = 0; i < inputs.Count; i++)
            {
                CharacterTestAnimationInputPresentationState input = inputs[i];
                CharacterTestCharacterAnimationComponent animation = input.Animation;
                AddHash(ref hash, input.EntityId);
                AddHash(ref hash, animation.LocomotionBlend.x);
                AddHash(ref hash, animation.LocomotionBlend.y);
                AddHash(ref hash, animation.LocomotionSpeed01);
                AddHash(ref hash, animation.Grounded ? 1 : 0);
                AddHash(ref hash, animation.Airborne ? 1 : 0);
                AddHash(ref hash, animation.ActionActive ? 1 : 0);
                AddStringHash(ref hash, animation.ActionKey);
                AddHash(ref hash, animation.ActionTransitionSeconds);
                AddHash(ref hash, animation.ActionHoldSeconds);
            }
        }

        private static void AddMatchConfigHash(
            ref ulong hash,
            CharacterTestArenaMatchConfig config)
        {
            if (config == null)
            {
                AddHash(ref hash, 0);
                return;
            }
            AddHash(ref hash, 1);
            AddHash(ref hash, (int)config.PreferredCameraMode);
            AddHash(ref hash, config.PresentationTeamId);
            for (int teamId = CharacterTestArenaMatchConfig.Team1Id;
                teamId <= CharacterTestArenaMatchConfig.Team2Id;
                teamId++)
            {
                for (int slotIndex = 0;
                    slotIndex < CharacterTestArenaMatchConfig.SlotsPerTeam;
                    slotIndex++)
                {
                    CharacterTestArenaSlotConfig slot = config.TryGetSlot(teamId, slotIndex);
                    AddHash(ref hash, slot != null && slot.Enabled ? 1 : 0);
                    AddHash(ref hash, slot?.CharacterId ?? 0);
                    AddHash(ref hash, slot?.LoadoutId ?? 0);
                    AddHash(ref hash, slot?.VariantId ?? 0);
                    AddHash(ref hash, slot?.BehaviorGroupDraftId ?? 0);
                    if (config.TryGetSpawnPosition(teamId, slotIndex, out UnityEngine.Vector2 position))
                    {
                        AddHash(ref hash, BitConverter.SingleToInt32Bits(position.x));
                        AddHash(ref hash, BitConverter.SingleToInt32Bits(position.y));
                    }
                }
            }
        }

        private sealed class SessionStartRecoverySnapshot
        {
            internal SessionStartRecoverySnapshot(
                CharacterTestRuntimeAiPlannerDevSceneController controller)
            {
                Phase = controller._phase;
                StatusMessage = controller._statusMessage;
                Paused = controller._context != null && controller._context.Paused;
                MainMenuVisible = controller._context != null && controller._context.MainMenuVisible;
                ManualPaused = controller._context != null && controller._context.ManualPaused;
                SelectedAgentIndex = controller._selectedAgentIndex;
                SelectedSetupTeamId = controller._selectedSetupTeamId;
                SelectedSetupSlotIndex = controller._selectedSetupSlotIndex;
                SetupSnapEnabled = controller._setupSnapEnabled;
                DebugEditGateEnabled = controller._debugEditGateEnabled;
                PreviewOverlayEnabled = controller._previewOverlayEnabled;
                DetailsExpanded = controller._detailsExpanded;
                AuthoringMode = controller._authoringMode;
                CandidateState = controller._candidateState;
                CandidateRevision = controller._candidateRevision;
                ActiveRevision = controller._activeRevision;
                CandidateDiagnostic = controller._candidateDiagnostic;
                CandidateApplyQueued = controller._candidateApplyQueued;
                CandidateApplyFrame = controller._candidateApplyFrame;
                QueuedCandidateRevision = controller._queuedCandidateRevision;
                QueuedApplyTarget = controller._queuedApplyTarget;
                BaselineRevision = controller._baselineRevision;
                BaselineRevisionPeers = controller._baselineRevisionPeers.ToArray();
                AppliedRevisionEvents = controller._appliedRevisionEvents.ToArray();
                FrameBuffer = controller._frameBuffer?.Clone();
                Timeline = controller._timeline?.Clone();
                TimelineEvents = controller._timelineEventIndex.CaptureRecoveryState();
                TestStateCaptures = controller._testStateCaptures.CaptureRecoveryState();
                LastCapturedRuntimeFrame = controller._lastCapturedRuntimeFrame;
                SnapshotParticipant = controller._snapshotParticipant;
                ReplayBootstrapConfig = controller._replayBootstrapConfig;
                LastTestStateCapturedFrame = controller._lastTestStateCapturedFrame;
                LastReplayResult = controller._lastReplayResult;
                ReplayBootstrapEndFrame = controller._replayBootstrapEndFrame;
                InitialReplayBaseline = controller._initialReplayBaseline;
                ReplayConfigurationRevision = controller._replayConfigurationRevision;
                TimelineSessionStarted = controller._timelineSessionStarted;
                TimelineSessionGeneration = controller._timelineSessionGeneration;
                SessionContinuable = controller._sessionContinuable;
                TimelineZoomLevel = controller._timelineZoomLevel;
                TimelineViewStartOverride = controller._timelineViewStartOverride;
                TimelineExpandedOverride = controller._timelineExpandedOverride;
                CachedTimelineProjection = controller._cachedTimelineProjection;
                CachedTimelineRevision = controller._cachedTimelineRevision;
                CachedTimelineAgentId = controller._cachedTimelineAgentId;
                CachedTimelineSelectedFrame = controller._cachedTimelineSelectedFrame;
                CachedTimelineViewport = controller._cachedTimelineViewport;
                LastTimelineProjectionBuildTime = controller._lastTimelineProjectionBuildTime;
                TimelineScrubActive = controller._timelineScrubActive;
                TimelineScrubStartedFromHistory = controller._timelineScrubStartedFromHistory;
                HasTimelineScrubDesiredFrame = controller._hasTimelineScrubDesiredFrame;
                TimelineScrubDesiredFrame = controller._timelineScrubDesiredFrame;
                FutureAdvanceStartFrame = controller._futureAdvanceStartFrame;
                LastFutureAdvancePresentationTime = controller._lastFutureAdvancePresentationTime;
                SelectedTimelineItem = controller._selectedTimelineItem;
                SelectedTimelineVisualStableKey = controller._selectedTimelineVisualStableKey;
                LoopPlaybackActive = controller._loopPlaybackActive;
                LoopPlaybackAccumulatorSeconds = controller._loopPlaybackAccumulatorSeconds;
                BranchRecomputeArmed = controller._branchRecomputeArmed;
                BranchConfirmationPending = controller._branchConfirmationPending;
                BranchConfirmationGeneration = controller._branchConfirmationGeneration;
                BranchConfirmationSessionGeneration = controller._branchConfirmationSessionGeneration;
                BranchConfirmationBaseAuthorityHash = controller._branchConfirmationBaseAuthorityHash;
                BranchTruncatePending = controller._branchTruncatePending;
                BranchBaseFrame = controller._branchBaseFrame;
                BranchOriginalLatestFrame = controller._branchOriginalLatestFrame;
                HistoryPlaybackActive = controller._historyPlaybackActive;
                HistoryPlaybackAccumulatorSeconds = controller._historyPlaybackAccumulatorSeconds;
                SeekInProgress = controller._seekInProgress;
                DeterministicReplayWork = controller._deterministicReplayWork;
                LastSeekSucceeded = controller._lastSeekSucceeded;
                ResumeLiveAfterReplay = controller._resumeLiveAfterReplay;
                ActiveSeekRequest = controller._activeSeekRequest;
                CommittedFrameBeforeSeek = controller._committedFrameBeforeSeek;
                CommittedAgentBeforeSeek = controller._committedAgentBeforeSeek;
                FailedSeekDiagnostic = controller._failedSeekDiagnostic;
                CommittedFrameReceipt = controller._frameSelection.CommittedReceipt;

                GameSlice slice = controller._context?.Slice;
                AuthorityFrame = slice != null ? slice.CurrentFrame : default;
                ElapsedSeconds = slice?.Services != null ? slice.Services.ElapsedSeconds : 0d;
                CharacterTestNetworkSnapshotParticipant participant =
                    slice?.Services.Network.CreateSnapshotParticipant();
                AuthorityCheckpoint = CharacterTestAuthorityRollbackCheckpoint.Capture(
                    participant,
                    AuthorityFrame);
                AuthorityHash = participant != null
                    ? participant.ComputeStateHash(AuthorityFrame)
                    : 0L;
                RuntimeSelectionUnlocked = slice?.Services.RuntimeCommands != null
                    && slice.Services.RuntimeCommands.RuntimeAiBehaviorSelectionUnlocked;
                Fingerprint = controller.BuildSessionStartRecoveryFingerprint();
            }

            internal bool TryRestore(
                CharacterTestRuntimeAiPlannerDevSceneController controller,
                out string diagnostic)
            {
                diagnostic = string.Empty;
                if (controller._sessionBuilder == null || controller._context == null)
                {
                    diagnostic = "Runtime AI Lab setup recovery dependencies are unavailable.";
                    return false;
                }

                controller._sessionBuilder.RebuildArenaSetupSession(
                    controller._matchConfig,
                    "Runtime AI Lab session start recovery.");
                GameSlice slice = controller._context.Slice;
                CharacterTestSliceNetworkServices network = slice?.Services.Network;
                CharacterTestNetworkSnapshotParticipant participant =
                    network?.CreateSnapshotParticipant();
                network?.RestoreNetworkRollbackRuntimeState(AuthorityFrame, ElapsedSeconds);
                if (AuthorityCheckpoint != null)
                {
                    NetworkSnapshotApplyResult result = AuthorityCheckpoint.Restore(
                        participant,
                        AuthorityFrame,
                        preserveLocalPredictedEntity: false);
                    if (!result.Success)
                    {
                        diagnostic = "Runtime AI Lab authority recovery failed: " + result.Message;
                        return false;
                    }
                }
                slice?.Services.RuntimeCommands?.SetRuntimeAiBehaviorSelectionUnlocked(
                    RuntimeSelectionUnlocked);
                long restoredAuthorityHash = participant != null
                    ? participant.ComputeStateHash(AuthorityFrame)
                    : 0L;
                if (restoredAuthorityHash != AuthorityHash)
                {
                    diagnostic = "Runtime AI Lab authority recovery fingerprint mismatch.";
                    return false;
                }

                controller._phase = Phase;
                controller._statusMessage = StatusMessage;
                controller._selectedAgentIndex = SelectedAgentIndex;
                controller._selectedSetupTeamId = SelectedSetupTeamId;
                controller._selectedSetupSlotIndex = SelectedSetupSlotIndex;
                controller._setupSnapEnabled = SetupSnapEnabled;
                controller._debugEditGateEnabled = DebugEditGateEnabled;
                controller._previewOverlayEnabled = PreviewOverlayEnabled;
                controller._detailsExpanded = DetailsExpanded;
                controller._authoringMode = AuthoringMode;
                controller._candidateState = CandidateState;
                controller._candidateRevision = CandidateRevision;
                controller._activeRevision = ActiveRevision;
                controller._candidateDiagnostic = CandidateDiagnostic;
                controller._candidateApplyQueued = CandidateApplyQueued;
                controller._candidateApplyFrame = CandidateApplyFrame;
                controller._queuedCandidateRevision = QueuedCandidateRevision;
                controller._queuedApplyTarget = QueuedApplyTarget;
                controller._baselineRevision = BaselineRevision;
                controller._baselineRevisionPeers.Clear();
                controller._baselineRevisionPeers.AddRange(BaselineRevisionPeers);
                controller._appliedRevisionEvents.Clear();
                controller._appliedRevisionEvents.AddRange(AppliedRevisionEvents);
                controller._frameBuffer = FrameBuffer;
                controller._timeline = Timeline;
                controller._timelineEventIndex.RestoreRecoveryState(TimelineEvents);
                controller._testStateCaptures.RestoreRecoveryState(TestStateCaptures);
                controller._lastCapturedRuntimeFrame = LastCapturedRuntimeFrame;
                controller._snapshotParticipant = SnapshotParticipant;
                controller._replayBootstrapConfig = ReplayBootstrapConfig;
                controller._lastTestStateCapturedFrame = LastTestStateCapturedFrame;
                controller._lastReplayResult = LastReplayResult;
                controller._replayBootstrapEndFrame = ReplayBootstrapEndFrame;
                controller._initialReplayBaseline = InitialReplayBaseline;
                controller._replayConfigurationRevision = ReplayConfigurationRevision;
                controller._timelineSessionStarted = TimelineSessionStarted;
                controller._timelineSessionGeneration = TimelineSessionGeneration;
                controller._sessionContinuable = SessionContinuable;
                controller._timelineZoomLevel = TimelineZoomLevel;
                controller._timelineViewStartOverride = TimelineViewStartOverride;
                controller._timelineExpandedOverride = TimelineExpandedOverride;
                controller._cachedTimelineProjection = CachedTimelineProjection;
                controller._cachedTimelineRevision = CachedTimelineRevision;
                controller._cachedTimelineAgentId = CachedTimelineAgentId;
                controller._cachedTimelineSelectedFrame = CachedTimelineSelectedFrame;
                controller._cachedTimelineViewport = CachedTimelineViewport;
                controller._lastTimelineProjectionBuildTime = LastTimelineProjectionBuildTime;
                controller._timelineScrubActive = TimelineScrubActive;
                controller._timelineScrubStartedFromHistory = TimelineScrubStartedFromHistory;
                controller._hasTimelineScrubDesiredFrame = HasTimelineScrubDesiredFrame;
                controller._timelineScrubDesiredFrame = TimelineScrubDesiredFrame;
                controller._futureAdvanceStartFrame = FutureAdvanceStartFrame;
                controller._lastFutureAdvancePresentationTime = LastFutureAdvancePresentationTime;
                controller._selectedTimelineItem = SelectedTimelineItem;
                controller._selectedTimelineVisualStableKey = SelectedTimelineVisualStableKey;
                controller._loopPlaybackActive = LoopPlaybackActive;
                controller._loopPlaybackAccumulatorSeconds = LoopPlaybackAccumulatorSeconds;
                controller._branchRecomputeArmed = BranchRecomputeArmed;
                controller._branchConfirmationPending = BranchConfirmationPending;
                controller._branchConfirmationGeneration = BranchConfirmationGeneration;
                controller._branchConfirmationSessionGeneration = BranchConfirmationSessionGeneration;
                controller._branchConfirmationBaseAuthorityHash = BranchConfirmationBaseAuthorityHash;
                controller._branchTruncatePending = BranchTruncatePending;
                controller._branchBaseFrame = BranchBaseFrame;
                controller._branchOriginalLatestFrame = BranchOriginalLatestFrame;
                controller._historyPlaybackActive = HistoryPlaybackActive;
                controller._historyPlaybackAccumulatorSeconds = HistoryPlaybackAccumulatorSeconds;
                controller._seekInProgress = SeekInProgress;
                controller._deterministicReplayWork = DeterministicReplayWork;
                controller._lastSeekSucceeded = LastSeekSucceeded;
                controller._resumeLiveAfterReplay = ResumeLiveAfterReplay;
                controller._activeSeekRequest = ActiveSeekRequest;
                controller._committedFrameBeforeSeek = CommittedFrameBeforeSeek;
                controller._committedAgentBeforeSeek = CommittedAgentBeforeSeek;
                controller._failedSeekDiagnostic = FailedSeekDiagnostic;
                controller._frameSelection.Reset(CommittedFrameReceipt);
                if (CommittedFrameReceipt.IsValid)
                {
                    controller._context?.Slice?.Services?.Modules?.RuntimeAiPlannerCommands
                        ?.RestoreDecisionTraceHistory(
                            controller._timeline,
                            CommittedFrameReceipt.Bundle.Record);
                }
                controller._context.MainMenuVisible = MainMenuVisible;
                controller._context.ManualPaused = ManualPaused;
                controller._context.MainMenuCoordinator?.RefreshEffectivePauseState();
                controller.SetPaused(Paused);
                return true;
            }

            internal string Fingerprint { get; }
            internal CharacterTestRuntimeAiPlannerDevScenePhase Phase { get; }
            internal string StatusMessage { get; }
            internal bool Paused { get; }
            internal bool MainMenuVisible { get; }
            internal bool ManualPaused { get; }
            internal int SelectedAgentIndex { get; }
            internal int SelectedSetupTeamId { get; }
            internal int SelectedSetupSlotIndex { get; }
            internal bool SetupSnapEnabled { get; }
            internal bool DebugEditGateEnabled { get; }
            internal bool PreviewOverlayEnabled { get; }
            internal bool DetailsExpanded { get; }
            internal CharacterTestRuntimeAiLabAuthoringMode AuthoringMode { get; }
            internal CharacterTestRuntimeAiLabCandidateState CandidateState { get; }
            internal CharacterTestRuntimeAiLabCandidateRevision CandidateRevision { get; }
            internal CharacterTestRuntimeAiLabCandidateRevision ActiveRevision { get; }
            internal string CandidateDiagnostic { get; }
            internal bool CandidateApplyQueued { get; }
            internal int CandidateApplyFrame { get; }
            internal CharacterTestRuntimeAiLabCandidateRevision QueuedCandidateRevision { get; }
            internal CharacterTestRuntimeAiLabQueuedApplyTargetIdentity QueuedApplyTarget { get; }
            internal CharacterTestRuntimeAiLabAppliedRevision BaselineRevision { get; }
            internal CharacterTestRuntimeAiLabAppliedRevision[] BaselineRevisionPeers { get; }
            internal CharacterTestRuntimeAiLabAppliedRevision[] AppliedRevisionEvents { get; }
            internal CharacterTestRuntimeAiPlannerDevFrameBuffer FrameBuffer { get; }
            internal CharacterTestRuntimeAiPlannerDevTimeline Timeline { get; }
            internal CharacterTestRuntimeAiLabTimelineEventIndex.RecoveryState TimelineEvents { get; }
            internal RuntimeAiLabTestStateCaptureStore.RecoveryState TestStateCaptures { get; }
            internal int LastCapturedRuntimeFrame { get; }
            internal CharacterTestNetworkSnapshotParticipant SnapshotParticipant { get; }
            internal CharacterTestArenaMatchConfig ReplayBootstrapConfig { get; }
            internal int LastTestStateCapturedFrame { get; }
            internal RuntimeAiLabDeterministicReplayResult LastReplayResult { get; }
            internal int ReplayBootstrapEndFrame { get; }
            internal RuntimeAiLabInitialReplayBaseline InitialReplayBaseline { get; }
            internal int ReplayConfigurationRevision { get; }
            internal bool TimelineSessionStarted { get; }
            internal int TimelineSessionGeneration { get; }
            internal bool SessionContinuable { get; }
            internal int TimelineZoomLevel { get; }
            internal int TimelineViewStartOverride { get; }
            internal bool? TimelineExpandedOverride { get; }
            internal CharacterTestRuntimeAiLabTimelineProjection CachedTimelineProjection { get; }
            internal long CachedTimelineRevision { get; }
            internal int CachedTimelineAgentId { get; }
            internal int CachedTimelineSelectedFrame { get; }
            internal CharacterTestRuntimeAiLabTimelineViewport CachedTimelineViewport { get; }
            internal float LastTimelineProjectionBuildTime { get; }
            internal bool TimelineScrubActive { get; }
            internal bool TimelineScrubStartedFromHistory { get; }
            internal bool HasTimelineScrubDesiredFrame { get; }
            internal int TimelineScrubDesiredFrame { get; }
            internal int FutureAdvanceStartFrame { get; }
            internal float LastFutureAdvancePresentationTime { get; }
            internal CharacterTestRuntimeAiLabTimelineItem SelectedTimelineItem { get; }
            internal string SelectedTimelineVisualStableKey { get; }
            internal bool LoopPlaybackActive { get; }
            internal double LoopPlaybackAccumulatorSeconds { get; }
            internal bool BranchRecomputeArmed { get; }
            internal bool BranchConfirmationPending { get; }
            internal long BranchConfirmationGeneration { get; }
            internal long BranchConfirmationSessionGeneration { get; }
            internal long BranchConfirmationBaseAuthorityHash { get; }
            internal bool BranchTruncatePending { get; }
            internal int BranchBaseFrame { get; }
            internal int BranchOriginalLatestFrame { get; }
            internal bool HistoryPlaybackActive { get; }
            internal double HistoryPlaybackAccumulatorSeconds { get; }
            internal bool SeekInProgress { get; }
            internal DeterministicReplayWork DeterministicReplayWork { get; }
            internal bool LastSeekSucceeded { get; }
            internal bool ResumeLiveAfterReplay { get; }
            internal CharacterTestRuntimeAiLabSeekRequest ActiveSeekRequest { get; }
            internal int CommittedFrameBeforeSeek { get; }
            internal int CommittedAgentBeforeSeek { get; }
            internal string FailedSeekDiagnostic { get; }
            internal CharacterTestRuntimeAiLabCommittedFrameReceipt CommittedFrameReceipt { get; }
            internal RuntimeFrame AuthorityFrame { get; }
            internal double ElapsedSeconds { get; }
            internal CharacterTestAuthorityRollbackCheckpoint AuthorityCheckpoint { get; }
            internal long AuthorityHash { get; }
            internal bool RuntimeSelectionUnlocked { get; }
        }
    }
}
