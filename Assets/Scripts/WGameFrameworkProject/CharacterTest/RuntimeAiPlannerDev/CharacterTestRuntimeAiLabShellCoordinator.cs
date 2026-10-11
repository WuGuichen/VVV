using System;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabWorkspace
    {
        PlayerDeck,
        Setup,
        BehaviorGroup,
        BehaviorCard,
        Facts,
        RuntimeFrame,
        Replay,
        TacticalLibrary
    }

    internal enum CharacterTestRuntimeAiLabShellVisibility
    {
        Hidden,
        Visible
    }

    internal enum CharacterTestRuntimeAiLabPresentationMode
    {
        Workspace,
        SessionStartInProgress,
        Recovery
    }

    internal enum CharacterTestRuntimeAiLabTimelineMode
    {
        RuntimeTrace,
        BehaviorPlan,
        BehaviorLogic
    }

    internal enum CharacterTestRuntimeAiLabSelectedElementKind
    {
        None,
        Stage,
        Detector,
        Transition
    }

    internal enum CharacterTestRuntimeAiLabSessionStartOutcome
    {
        None,
        Succeeded,
        Failed
    }

    internal readonly struct CharacterTestRuntimeAiLabBehaviorCardViewport
    {
        internal static readonly CharacterTestRuntimeAiLabBehaviorCardViewport Default =
            new CharacterTestRuntimeAiLabBehaviorCardViewport(0, 240, 1f, 0f, true);

        internal CharacterTestRuntimeAiLabBehaviorCardViewport(
            int horizontalStartFrame,
            int horizontalEndFrame,
            float zoomLevel,
            float verticalScrollOffset,
            bool followCurrent)
        {
            Value = new BehaviorTimelineViewport(
                horizontalStartFrame,
                horizontalEndFrame,
                zoomLevel,
                verticalScrollOffset,
                followCurrent,
                1);
        }

        internal BehaviorTimelineViewport Value { get; }
        internal int HorizontalStartFrame => Value.StartRelativeFrame;
        internal int HorizontalEndFrame => Value.EndRelativeFrame;
        internal float ZoomLevel => Value.ZoomLevel;
        internal float VerticalScrollOffset => Value.VerticalScrollOffset;
        internal bool FollowCurrent => Value.FollowCurrent;
    }

    [Flags]
    internal enum CharacterTestRuntimeAiLabBehaviorCardLibraryFilter
    {
        All = 0,
        Installed = 1 << 0,
        UnlockedAvailable = 1 << 1,
        Locked = 1 << 2,
        UnavailableOrIncompatible = 1 << 3,
        CurrentDraft = 1 << 4,
        RuntimeActive = 1 << 5
    }

    internal readonly struct CharacterTestRuntimeAiLabBehaviorCardLibraryState
    {
        internal static readonly CharacterTestRuntimeAiLabBehaviorCardLibraryState Default =
            new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All,
                string.Empty,
                string.Empty,
                0f);

        internal CharacterTestRuntimeAiLabBehaviorCardLibraryState(
            string query,
            CharacterTestRuntimeAiLabBehaviorCardLibraryFilter filter,
            string firstVisibleStableCardId,
            string focusedStableCardId,
            float scrollOffsetY)
        {
            Query = CharacterTestRuntimeAiLabBehaviorCardLibraryQuery.Normalize(query);
            Filter = filter;
            FirstVisibleStableCardId = firstVisibleStableCardId?.Trim() ?? string.Empty;
            FocusedStableCardId = focusedStableCardId?.Trim() ?? string.Empty;
            ScrollOffsetY = Math.Max(0f, scrollOffsetY);
        }

        internal string Query { get; }
        internal CharacterTestRuntimeAiLabBehaviorCardLibraryFilter Filter { get; }
        internal string FirstVisibleStableCardId { get; }
        internal string FocusedStableCardId { get; }
        internal float ScrollOffsetY { get; }
    }

    internal static class CharacterTestRuntimeAiLabBehaviorCardLibraryQuery
    {
        /// <remarks>LowFreqAlloc: invoked only when the user changes the library query.</remarks>
        internal static string Normalize(string value)
        {
            string trimmed = value?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
                return string.Empty;

            bool previousWhitespace = false;
            bool requiresCollapse = false;
            for (int i = 0; i < trimmed.Length; i++)
            {
                bool whitespace = char.IsWhiteSpace(trimmed[i]);
                if (whitespace && (previousWhitespace || trimmed[i] != ' '))
                {
                    requiresCollapse = true;
                    break;
                }
                previousWhitespace = whitespace;
            }
            if (!requiresCollapse)
                return trimmed;

            var builder = new StringBuilder(trimmed.Length);
            previousWhitespace = false;
            for (int i = 0; i < trimmed.Length; i++)
            {
                bool whitespace = char.IsWhiteSpace(trimmed[i]);
                if (whitespace)
                {
                    if (!previousWhitespace)
                        builder.Append(' ');
                }
                else
                {
                    builder.Append(trimmed[i]);
                }
                previousWhitespace = whitespace;
            }
            return builder.ToString();
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabRuntimeFramePageContext
    {
        internal CharacterTestRuntimeAiLabRuntimeFramePageContext(
            CharacterTestRuntimeAiLabTimelineViewport viewport,
            CharacterTestRuntimeAiLabCommittedFrameReceipt committedReceipt,
            CharacterTestRuntimeAiLabTimelineItemLocator selectedLocator,
            bool detailsVisible)
        {
            Viewport = viewport;
            CommittedReceipt = committedReceipt;
            SelectedLocator = selectedLocator;
            DetailsVisible = detailsVisible;
        }

        internal CharacterTestRuntimeAiLabTimelineViewport Viewport { get; }
        internal CharacterTestRuntimeAiLabCommittedFrameReceipt CommittedReceipt { get; }
        internal CharacterTestRuntimeAiLabTimelineItemLocator SelectedLocator { get; }
        internal bool DetailsVisible { get; }
    }

    internal sealed class CharacterTestRuntimeAiLabPageContext
    {
        internal static CharacterTestRuntimeAiLabPageContext CreateDefault()
        {
            return new CharacterTestRuntimeAiLabPageContext(
                CharacterTestRuntimeAiLabWorkspace.PlayerDeck,
                CharacterTestRuntimeAiLabWorkspace.Setup,
                CharacterTestRuntimeAiLabWorkspace.PlayerDeck,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                CharacterTestRuntimeAiLabSelectedElementKind.None,
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorCardViewport.Default,
                CharacterTestRuntimeAiLabBehaviorCardLibraryState.Default,
                default,
                CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace,
                runtimeTraceExpanded: true,
                behaviorPlanExpanded: true);
        }

        internal CharacterTestRuntimeAiLabPageContext(
            CharacterTestRuntimeAiLabWorkspace activeWorkspace,
            CharacterTestRuntimeAiLabWorkspace returnWorkspace,
            CharacterTestRuntimeAiLabWorkspace behaviorCardReturnWorkspace,
            int playerDeckPageIndex,
            string replacementSourceStableCardId,
            string groupDraftId,
            string entryId,
            string cardDraftId,
            CharacterTestRuntimeAiLabSelectedElementKind selectedElementKind,
            string selectedElementStableId,
            CharacterTestRuntimeAiLabBehaviorCardViewport behaviorCardViewport,
            CharacterTestRuntimeAiLabBehaviorCardLibraryState behaviorCardLibraryState,
            CharacterTestRuntimeAiLabRuntimeFramePageContext runtimeFrameContext,
            CharacterTestRuntimeAiLabTimelineMode timelineMode,
            bool runtimeTraceExpanded,
            bool behaviorPlanExpanded)
        {
            ActiveWorkspace = activeWorkspace;
            ReturnWorkspace = returnWorkspace;
            BehaviorCardReturnWorkspace = behaviorCardReturnWorkspace;
            PlayerDeckPageIndex = Math.Max(0, playerDeckPageIndex);
            ReplacementSourceStableCardId = Normalize(replacementSourceStableCardId);
            GroupDraftId = Normalize(groupDraftId);
            EntryId = Normalize(entryId);
            CardDraftId = Normalize(cardDraftId);
            SelectedElementKind = selectedElementKind;
            SelectedElementStableId = selectedElementKind == CharacterTestRuntimeAiLabSelectedElementKind.None
                ? string.Empty
                : Normalize(selectedElementStableId);
            BehaviorCardViewport = behaviorCardViewport;
            BehaviorCardLibraryState = behaviorCardLibraryState;
            RuntimeFrameContext = runtimeFrameContext;
            TimelineMode = timelineMode;
            RuntimeTraceExpanded = runtimeTraceExpanded;
            BehaviorPlanExpanded = behaviorPlanExpanded;
        }

        internal CharacterTestRuntimeAiLabWorkspace ActiveWorkspace { get; }
        internal CharacterTestRuntimeAiLabWorkspace ReturnWorkspace { get; }
        internal CharacterTestRuntimeAiLabWorkspace BehaviorCardReturnWorkspace { get; }
        internal int PlayerDeckPageIndex { get; }
        internal string ReplacementSourceStableCardId { get; }
        internal string GroupDraftId { get; }
        internal string EntryId { get; }
        internal string CardDraftId { get; }
        internal CharacterTestRuntimeAiLabSelectedElementKind SelectedElementKind { get; }
        internal string SelectedElementStableId { get; }
        internal CharacterTestRuntimeAiLabBehaviorCardViewport BehaviorCardViewport { get; }
        internal CharacterTestRuntimeAiLabBehaviorCardLibraryState BehaviorCardLibraryState { get; }
        internal CharacterTestRuntimeAiLabRuntimeFramePageContext RuntimeFrameContext { get; }
        internal CharacterTestRuntimeAiLabTimelineMode TimelineMode { get; }
        internal bool RuntimeTraceExpanded { get; }
        internal bool BehaviorPlanExpanded { get; }

        internal CharacterTestRuntimeAiLabPageContext WithWorkspace(
            CharacterTestRuntimeAiLabWorkspace workspace)
        {
            return Copy(activeWorkspace: workspace);
        }

        internal CharacterTestRuntimeAiLabPageContext WithReturnWorkspace(
            CharacterTestRuntimeAiLabWorkspace workspace)
        {
            return Copy(returnWorkspace: workspace);
        }

        internal CharacterTestRuntimeAiLabPageContext WithBehaviorCardReturnWorkspace(
            CharacterTestRuntimeAiLabWorkspace workspace)
        {
            return Copy(behaviorCardReturnWorkspace: workspace);
        }

        internal CharacterTestRuntimeAiLabPageContext WithPlayerDeck(
            int pageIndex,
            string replacementSourceStableCardId)
        {
            return Copy(
                playerDeckPageIndex: pageIndex,
                replacementSourceStableCardId: replacementSourceStableCardId);
        }

        internal CharacterTestRuntimeAiLabPageContext WithBehaviorCard(
            string groupDraftId,
            string entryId,
            string cardDraftId,
            CharacterTestRuntimeAiLabSelectedElementKind selectedElementKind,
            string selectedElementStableId,
            CharacterTestRuntimeAiLabBehaviorCardViewport viewport)
        {
            return Copy(
                groupDraftId: groupDraftId,
                entryId: entryId,
                cardDraftId: cardDraftId,
                selectedElementKind: selectedElementKind,
                selectedElementStableId: selectedElementStableId,
                behaviorCardViewport: viewport);
        }

        internal CharacterTestRuntimeAiLabPageContext WithBehaviorCardLibrary(
            CharacterTestRuntimeAiLabBehaviorCardLibraryState state)
        {
            return Copy(behaviorCardLibraryState: state);
        }

        internal CharacterTestRuntimeAiLabPageContext WithRuntimeFrame(
            CharacterTestRuntimeAiLabRuntimeFramePageContext state)
        {
            return Copy(runtimeFrameContext: state);
        }

        internal CharacterTestRuntimeAiLabPageContext WithTimelineMode(
            CharacterTestRuntimeAiLabTimelineMode mode)
        {
            return Copy(timelineMode: mode);
        }

        internal CharacterTestRuntimeAiLabPageContext WithTimelineExpanded(
            CharacterTestRuntimeAiLabTimelineMode mode,
            bool expanded)
        {
            if (mode == CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace)
                return Copy(runtimeTraceExpanded: expanded);
            if (mode == CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan)
                return Copy(behaviorPlanExpanded: expanded);
            return this;
        }

        private CharacterTestRuntimeAiLabPageContext Copy(
            CharacterTestRuntimeAiLabWorkspace? activeWorkspace = null,
            CharacterTestRuntimeAiLabWorkspace? returnWorkspace = null,
            CharacterTestRuntimeAiLabWorkspace? behaviorCardReturnWorkspace = null,
            int? playerDeckPageIndex = null,
            string replacementSourceStableCardId = null,
            string groupDraftId = null,
            string entryId = null,
            string cardDraftId = null,
            CharacterTestRuntimeAiLabSelectedElementKind? selectedElementKind = null,
            string selectedElementStableId = null,
            CharacterTestRuntimeAiLabBehaviorCardViewport? behaviorCardViewport = null,
            CharacterTestRuntimeAiLabBehaviorCardLibraryState? behaviorCardLibraryState = null,
            CharacterTestRuntimeAiLabRuntimeFramePageContext? runtimeFrameContext = null,
            CharacterTestRuntimeAiLabTimelineMode? timelineMode = null,
            bool? runtimeTraceExpanded = null,
            bool? behaviorPlanExpanded = null)
        {
            CharacterTestRuntimeAiLabSelectedElementKind nextKind =
                selectedElementKind ?? SelectedElementKind;
            return new CharacterTestRuntimeAiLabPageContext(
                activeWorkspace ?? ActiveWorkspace,
                returnWorkspace ?? ReturnWorkspace,
                behaviorCardReturnWorkspace ?? BehaviorCardReturnWorkspace,
                playerDeckPageIndex ?? PlayerDeckPageIndex,
                replacementSourceStableCardId ?? ReplacementSourceStableCardId,
                groupDraftId ?? GroupDraftId,
                entryId ?? EntryId,
                cardDraftId ?? CardDraftId,
                nextKind,
                selectedElementStableId ?? SelectedElementStableId,
                behaviorCardViewport ?? BehaviorCardViewport,
                behaviorCardLibraryState ?? BehaviorCardLibraryState,
                runtimeFrameContext ?? RuntimeFrameContext,
                timelineMode ?? TimelineMode,
                runtimeTraceExpanded ?? RuntimeTraceExpanded,
                behaviorPlanExpanded ?? BehaviorPlanExpanded);
        }

        private static string Normalize(string value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabShellFaultContext
    {
        internal CharacterTestRuntimeAiLabShellFaultContext(
            string code,
            string title,
            string detail,
            CharacterTestRuntimeAiLabWorkspace failedTarget,
            bool canRetry,
            bool canResetSession)
        {
            Code = code ?? string.Empty;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            FailedTarget = failedTarget;
            CanRetry = canRetry;
            CanResetSession = canResetSession;
        }

        internal string Code { get; }
        internal string Title { get; }
        internal string Detail { get; }
        internal CharacterTestRuntimeAiLabWorkspace FailedTarget { get; }
        internal bool CanRetry { get; }
        internal bool CanResetSession { get; }
    }

    internal sealed class CharacterTestRuntimeAiLabSessionStartTransactionContext
    {
        internal CharacterTestRuntimeAiLabSessionStartTransactionContext(
            long transactionId,
            long generation,
            long beginRevision,
            CharacterTestRuntimeAiLabPageContext sourceContext,
            CharacterTestRuntimeAiLabWorkspace successTarget,
            object recoverySnapshot,
            CharacterTestRuntimeAiLabTimelineMode? successTimelineMode = null,
            string mountDiagnostic = "")
        {
            TransactionId = transactionId;
            Generation = generation;
            BeginRevision = beginRevision;
            SourceContext = sourceContext;
            SuccessTarget = successTarget;
            RecoverySnapshot = recoverySnapshot;
            SuccessTimelineMode = successTimelineMode;
            MountDiagnostic = mountDiagnostic ?? string.Empty;
        }

        internal long TransactionId { get; }
        internal long Generation { get; }
        internal long BeginRevision { get; }
        internal CharacterTestRuntimeAiLabPageContext SourceContext { get; }
        internal CharacterTestRuntimeAiLabWorkspace SuccessTarget { get; }
        internal object RecoverySnapshot { get; }
        internal CharacterTestRuntimeAiLabTimelineMode? SuccessTimelineMode { get; }
        internal string MountDiagnostic { get; }

        internal CharacterTestRuntimeAiLabSessionStartTransactionContext WithMountDiagnostic(
            string diagnostic)
        {
            return new CharacterTestRuntimeAiLabSessionStartTransactionContext(
                TransactionId,
                Generation,
                BeginRevision,
                SourceContext,
                SuccessTarget,
                RecoverySnapshot,
                SuccessTimelineMode,
                diagnostic);
        }
    }

    internal sealed class CharacterTestRuntimeAiLabShellState
    {
        internal CharacterTestRuntimeAiLabShellState(
            long generation,
            long revision,
            CharacterTestRuntimeAiLabShellVisibility visibility,
            CharacterTestRuntimeAiLabPresentationMode presentationMode,
            CharacterTestRuntimeAiLabPageContext pageContext,
            CharacterTestRuntimeAiLabShellFaultContext fault,
            CharacterTestRuntimeAiLabSessionStartTransactionContext transaction,
            long lastCompletedTransactionId,
            CharacterTestRuntimeAiLabSessionStartOutcome lastCompletedOutcome)
        {
            Generation = generation;
            Revision = revision;
            Visibility = visibility;
            PresentationMode = presentationMode;
            PageContext = pageContext;
            Fault = fault;
            Transaction = transaction;
            LastCompletedTransactionId = lastCompletedTransactionId;
            LastCompletedOutcome = lastCompletedOutcome;
        }

        internal long Generation { get; }
        internal long Revision { get; }
        internal CharacterTestRuntimeAiLabShellVisibility Visibility { get; }
        internal CharacterTestRuntimeAiLabPresentationMode PresentationMode { get; }
        internal CharacterTestRuntimeAiLabPageContext PageContext { get; }
        internal CharacterTestRuntimeAiLabWorkspace ActiveWorkspace => PageContext.ActiveWorkspace;
        internal CharacterTestRuntimeAiLabShellFaultContext Fault { get; }
        internal CharacterTestRuntimeAiLabSessionStartTransactionContext Transaction { get; }
        internal long LastCompletedTransactionId { get; }
        internal CharacterTestRuntimeAiLabSessionStartOutcome LastCompletedOutcome { get; }
    }

    internal readonly struct CharacterTestRuntimeAiLabNavigationResult
    {
        internal CharacterTestRuntimeAiLabNavigationResult(
            bool success,
            string code,
            string message,
            CharacterTestRuntimeAiLabWorkspace source,
            CharacterTestRuntimeAiLabWorkspace target,
            long revision)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Source = source;
            Target = target;
            Revision = revision;
        }

        internal bool Success { get; }
        internal string Code { get; }
        internal string Message { get; }
        internal CharacterTestRuntimeAiLabWorkspace Source { get; }
        internal CharacterTestRuntimeAiLabWorkspace Target { get; }
        internal long Revision { get; }
    }

    internal interface ICharacterTestRuntimeAiLabShellMountAdapter
    {
        bool TryPrepareForReopen(out string diagnostic);
        bool IsWorkspaceAvailable(CharacterTestRuntimeAiLabWorkspace workspace);
        bool TryNormalizeWorkspaceContext(
            CharacterTestRuntimeAiLabWorkspace workspace,
            CharacterTestRuntimeAiLabPageContext context,
            out CharacterTestRuntimeAiLabPageContext normalizedContext,
            out string diagnostic);
        bool IsSetupPhase { get; }
        bool HasRuntimeHistory { get; }
        bool TryMountWorkspace(CharacterTestRuntimeAiLabShellState state, out string diagnostic);
        bool TryMountTransition(CharacterTestRuntimeAiLabShellState state, out string diagnostic);
        bool TryMountRecovery(CharacterTestRuntimeAiLabShellState state, out string diagnostic);
        bool TryDismissTopModalForVisibilityToggle(out bool dismissed, out string diagnostic);
        void UnmountAll();
    }

    /// <summary>
    /// Instance-owned, low-frequency UI navigation authority. Allocations are by design and
    /// occur only for user navigation, mount/rebuild, or session-start transactions.
    /// </summary>
    internal sealed class CharacterTestRuntimeAiLabShellCoordinator
    {
        private ICharacterTestRuntimeAiLabShellMountAdapter _mountAdapter;
        private CharacterTestRuntimeAiLabPageContext _lastRestorableContext;
        private long _nextTransactionId;
        private readonly object _mountLease = new object();

        internal CharacterTestRuntimeAiLabShellCoordinator(
            long generation,
            bool playerDeckAvailable,
            bool labAvailable,
            CharacterTestRuntimeAiLabWorkspace initialWorkspace =
                CharacterTestRuntimeAiLabWorkspace.PlayerDeck)
        {
            if (generation <= 0)
                throw new ArgumentOutOfRangeException(nameof(generation));

            CharacterTestRuntimeAiLabPageContext pageContext =
                CharacterTestRuntimeAiLabPageContext.CreateDefault();
            CharacterTestRuntimeAiLabShellVisibility visibility =
                labAvailable
                    ? CharacterTestRuntimeAiLabShellVisibility.Visible
                    : CharacterTestRuntimeAiLabShellVisibility.Hidden;
            CharacterTestRuntimeAiLabWorkspace workspace =
                initialWorkspace == CharacterTestRuntimeAiLabWorkspace.Setup
                    ? CharacterTestRuntimeAiLabWorkspace.Setup
                    : playerDeckAvailable
                        ? CharacterTestRuntimeAiLabWorkspace.PlayerDeck
                        : CharacterTestRuntimeAiLabWorkspace.Setup;
            pageContext = pageContext.WithWorkspace(workspace);
            _lastRestorableContext = pageContext;
            CurrentState = new CharacterTestRuntimeAiLabShellState(
                generation,
                1,
                visibility,
                CharacterTestRuntimeAiLabPresentationMode.Workspace,
                pageContext,
                null,
                null,
                0,
                CharacterTestRuntimeAiLabSessionStartOutcome.None);
            LastNavigationResult = new CharacterTestRuntimeAiLabNavigationResult(
                true,
                workspace == CharacterTestRuntimeAiLabWorkspace.Setup
                    ? "INITIAL_SETUP"
                    : "INITIAL_PLAYER_DECK",
                string.Empty,
                workspace,
                workspace,
                CurrentState.Revision);
        }

        internal CharacterTestRuntimeAiLabShellState CurrentState { get; private set; }
        internal object MountLease => _mountLease;
        internal CharacterTestRuntimeAiLabPageContext LastRestorableContext => _lastRestorableContext;
        internal CharacterTestRuntimeAiLabNavigationResult LastNavigationResult { get; private set; }

        internal CharacterTestRuntimeAiLabNavigationResult AttachMountAdapter(
            long generation,
            ICharacterTestRuntimeAiLabShellMountAdapter adapter)
        {
            CharacterTestRuntimeAiLabWorkspace target = CurrentState.ActiveWorkspace;
            if (generation != CurrentState.Generation || adapter == null)
                return Fail("NAVIGATION_STALE_ADAPTER", "The shell mount adapter is stale.", target, target);

            _mountAdapter?.UnmountAll();
            _mountAdapter = adapter;
            if (CurrentState.Visibility == CharacterTestRuntimeAiLabShellVisibility.Hidden)
                return Succeed("ADAPTER_ATTACHED_HIDDEN", target, target);

            if (CurrentState.PresentationMode ==
                CharacterTestRuntimeAiLabPresentationMode.SessionStartInProgress)
            {
                if (adapter.TryMountTransition(CurrentState, out string transitionDiagnostic))
                    return Succeed("ADAPTER_ATTACHED_TRANSITION", target, target);

                CharacterTestRuntimeAiLabSessionStartTransactionContext transaction =
                    CurrentState.Transaction?.WithMountDiagnostic(transitionDiagnostic);
                Commit(
                    CharacterTestRuntimeAiLabShellVisibility.Hidden,
                    CharacterTestRuntimeAiLabPresentationMode.SessionStartInProgress,
                    CurrentState.PageContext,
                    null,
                    transaction,
                    CurrentState.LastCompletedTransactionId,
                    CurrentState.LastCompletedOutcome);
                return Fail(
                    "SESSION_START_TRANSITION_REATTACH_FAILED",
                    transitionDiagnostic,
                    target,
                    target);
            }

            if (CurrentState.PresentationMode == CharacterTestRuntimeAiLabPresentationMode.Recovery)
            {
                if (adapter.TryMountRecovery(CurrentState, out string recoveryDiagnostic))
                    return Succeed("ADAPTER_ATTACHED_RECOVERY", target, target);

                CharacterTestRuntimeAiLabShellFaultContext fault = CurrentState.Fault;
                EnterHiddenRecovery(
                    "RECOVERY_SURFACE_UNAVAILABLE",
                    recoveryDiagnostic,
                    fault?.FailedTarget ?? target,
                    fault?.CanRetry ?? true,
                    fault?.CanResetSession ?? false);
                return LastNavigationResult;
            }

            return MountWorkspaceWithCanonicalFallback("ADAPTER_ATTACHED");
        }

        internal void DetachMountAdapter(ICharacterTestRuntimeAiLabShellMountAdapter adapter)
        {
            if (!ReferenceEquals(_mountAdapter, adapter))
                return;
            adapter.UnmountAll();
            _mountAdapter = null;
        }

        internal CharacterTestRuntimeAiLabNavigationResult Navigate(
            long expectedGeneration,
            long expectedRevision,
            CharacterTestRuntimeAiLabWorkspace source,
            CharacterTestRuntimeAiLabWorkspace target)
        {
            CharacterTestRuntimeAiLabNavigationResult stale;
            if (!ValidateCommand(expectedGeneration, expectedRevision, source, target, out stale))
                return stale;
            if (CurrentState.PresentationMode != CharacterTestRuntimeAiLabPresentationMode.Workspace)
                return Fail("NAVIGATION_PRESENTATION_BUSY", "The shell is not displaying a workspace.", source, target);
            if (_mountAdapter == null || !_mountAdapter.IsWorkspaceAvailable(target))
                return Fail("NAVIGATION_TARGET_UNAVAILABLE", "The requested workspace is unavailable.", source, target);

            if (source == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                && !_mountAdapter.IsSetupPhase
                && (target == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                    || target == CharacterTestRuntimeAiLabWorkspace.BehaviorCard))
            {
                CharacterTestRuntimeAiLabTimelineMode aliasMode =
                    target == CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                        ? CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic
                        : CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace;
                if (aliasMode == CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic
                    && (string.IsNullOrEmpty(CurrentState.PageContext.GroupDraftId)
                        || string.IsNullOrEmpty(CurrentState.PageContext.EntryId)
                        || string.IsNullOrEmpty(CurrentState.PageContext.CardDraftId)))
                {
                    return Fail(
                        "BEHAVIOR_PLAN_CONTEXT_UNAVAILABLE",
                        "The BehaviorCard route has no stable card context to open.",
                        source,
                        target);
                }
                CharacterTestRuntimeAiLabPageContext aliasPage =
                    CurrentState.PageContext.WithTimelineMode(aliasMode);
                CommitWorkspaceContext(aliasPage);
                return Succeed(
                    aliasMode == CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace
                        ? "RUNTIME_FRAME_ROUTE_ALIASED"
                        : "BEHAVIOR_CARD_ROUTE_ALIASED",
                    source,
                    target);
            }

            CharacterTestRuntimeAiLabPageContext next = CurrentState.PageContext;
            if (target == CharacterTestRuntimeAiLabWorkspace.PlayerDeck)
            {
                next = CharacterTestRuntimeAiLabPageContext.CreateDefault().WithPlayerDeck(
                    CurrentState.PageContext.PlayerDeckPageIndex,
                    CurrentState.PageContext.ReplacementSourceStableCardId);
            }
            else if (target == CharacterTestRuntimeAiLabWorkspace.TacticalLibrary)
            {
                next = next.WithReturnWorkspace(source);
            }
            else if (IsDiagnostics(target)
                && (!IsDiagnostics(source)
                    || source == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame))
            {
                next = next.WithReturnWorkspace(source);
            }
            if (target == CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                && source != CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                && !IsDiagnostics(source))
            {
                next = next.WithBehaviorCardReturnWorkspace(source);
            }
            next = next.WithWorkspace(target);
            return TryCommitAndMountWorkspace(next, source, target, "NAVIGATION_OK");
        }

        internal CharacterTestRuntimeAiLabNavigationResult RestoreLibraryOrigin(long generation, long revision, CharacterTestRuntimeAiLabPageContext origin)
        {
            var source = CurrentState.ActiveWorkspace;
            if (!ValidateCommand(generation, revision, source, origin.ActiveWorkspace, out var stale)) return stale;
            if (source != CharacterTestRuntimeAiLabWorkspace.TacticalLibrary)
                return Fail("LIBRARY_RETURN_STALE", "The library is no longer open.", source, origin.ActiveWorkspace);
            return TryCommitAndMountWorkspace(origin, source, origin.ActiveWorkspace, "LIBRARY_RETURNED");
        }

        internal CharacterTestRuntimeAiLabNavigationResult Back(
            long expectedGeneration,
            long expectedRevision,
            CharacterTestRuntimeAiLabWorkspace source)
        {
            CharacterTestRuntimeAiLabWorkspace target;
            if (source == CharacterTestRuntimeAiLabWorkspace.TacticalLibrary)
                target = CurrentState.PageContext.ReturnWorkspace;
            else if (IsDiagnostics(source))
                target = CurrentState.PageContext.ReturnWorkspace;
            else if (source == CharacterTestRuntimeAiLabWorkspace.BehaviorCard)
            {
                target = CurrentState.PageContext.BehaviorCardReturnWorkspace;
                if (target == CharacterTestRuntimeAiLabWorkspace.BehaviorGroup)
                    target = CharacterTestRuntimeAiLabWorkspace.Setup;
            }
            else if (source == CharacterTestRuntimeAiLabWorkspace.BehaviorGroup)
                target = CharacterTestRuntimeAiLabWorkspace.Setup;
            else if (source == CharacterTestRuntimeAiLabWorkspace.Setup)
                target = CharacterTestRuntimeAiLabWorkspace.PlayerDeck;
            else
                target = source;
            return Navigate(expectedGeneration, expectedRevision, source, target);
        }

        internal CharacterTestRuntimeAiLabNavigationResult NavigateToBehaviorCard(
            long expectedGeneration,
            long expectedRevision,
            CharacterTestRuntimeAiLabWorkspace source,
            string groupDraftId,
            string entryId,
            string cardDraftId,
            CharacterTestRuntimeAiLabSelectedElementKind selectedKind,
            string selectedStableId,
            CharacterTestRuntimeAiLabBehaviorCardViewport viewport)
        {
            CharacterTestRuntimeAiLabWorkspace target = CharacterTestRuntimeAiLabWorkspace.BehaviorCard;
            CharacterTestRuntimeAiLabNavigationResult stale;
            if (!ValidateCommand(expectedGeneration, expectedRevision, source, target, out stale))
                return stale;
            if (_mountAdapter == null || !_mountAdapter.IsWorkspaceAvailable(target))
                return Fail("NAVIGATION_TARGET_UNAVAILABLE", "The behavior card workspace is unavailable.", source, target);
            if (string.IsNullOrWhiteSpace(groupDraftId)
                || string.IsNullOrWhiteSpace(entryId)
                || string.IsNullOrWhiteSpace(cardDraftId))
                return Fail("BEHAVIOR_CARD_CONTEXT_INVALID", "Stable behavior card identity is required.", source, target);

            CharacterTestRuntimeAiLabPageContext page = CurrentState.PageContext
                .WithBehaviorCardReturnWorkspace(source)
                .WithBehaviorCard(
                    groupDraftId,
                    entryId,
                    cardDraftId,
                    selectedKind,
                    selectedStableId,
                    viewport);
            if (_mountAdapter != null && !_mountAdapter.IsSetupPhase)
            {
                page = page
                    .WithTimelineMode(CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic)
                    .WithWorkspace(CharacterTestRuntimeAiLabWorkspace.RuntimeFrame);
                if (source == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame)
                {
                    CommitWorkspaceContext(page);
                    return Succeed("BEHAVIOR_CARD_ROUTE_ALIASED", source, target);
                }
                if (!_mountAdapter.IsWorkspaceAvailable(CharacterTestRuntimeAiLabWorkspace.RuntimeFrame))
                {
                    return Fail(
                        "NAVIGATION_TARGET_UNAVAILABLE",
                        "The Runtime Test Workbench is unavailable.",
                        source,
                        target);
                }
                return TryCommitAndMountWorkspace(
                    page,
                    source,
                    CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                    "BEHAVIOR_CARD_ROUTE_ALIASED");
            }

            page = page.WithTimelineMode(CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic).WithWorkspace(target);
            return TryCommitAndMountWorkspace(page, source, target, "BEHAVIOR_CARD_OPENED");
        }

        internal CharacterTestRuntimeAiLabNavigationResult ToggleVisibility()
        {
            CharacterTestRuntimeAiLabWorkspace workspace = CurrentState.ActiveWorkspace;
            if (CurrentState.Visibility == CharacterTestRuntimeAiLabShellVisibility.Visible)
            {
                if (_mountAdapter != null)
                {
                    if (!_mountAdapter.TryDismissTopModalForVisibilityToggle(
                            out bool dismissed,
                            out string diagnostic))
                    {
                        return Fail(
                            "ACTIVE_MODAL_DISMISS_FAILED",
                            diagnostic,
                            workspace,
                            workspace);
                    }
                    if (dismissed)
                        return Succeed("ACTIVE_MODAL_DISMISSED", workspace, workspace);
                }
                _mountAdapter?.UnmountAll();
                Commit(
                    CharacterTestRuntimeAiLabShellVisibility.Hidden,
                    CurrentState.PresentationMode,
                    CurrentState.PageContext,
                    CurrentState.Fault,
                    CurrentState.Transaction,
                    CurrentState.LastCompletedTransactionId,
                    CurrentState.LastCompletedOutcome);
                return Succeed("SHELL_HIDDEN", workspace, workspace);
            }

            if (_mountAdapter == null)
                return Fail("NAVIGATION_ADAPTER_UNAVAILABLE", "The shell UI is unavailable.", workspace, workspace);

            if (!_mountAdapter.TryPrepareForReopen(out string preflightDiagnostic))
                return HandleReopenPreflightFailure(workspace, preflightDiagnostic);

            switch (CurrentState.PresentationMode)
            {
                case CharacterTestRuntimeAiLabPresentationMode.SessionStartInProgress:
                    return TryReopenSessionStart(workspace);
                case CharacterTestRuntimeAiLabPresentationMode.Recovery:
                    return TryReopenRecovery(workspace);
                default:
                    return TryReopenWorkspace(workspace);
            }
        }

        internal CharacterTestRuntimeAiLabNavigationResult CloseToPlayerDeck()
        {
            CharacterTestRuntimeAiLabWorkspace source = CurrentState.ActiveWorkspace;
            if (CurrentState.Visibility == CharacterTestRuntimeAiLabShellVisibility.Hidden)
                return Succeed("SHELL_ALREADY_HIDDEN", source, source);

            if (_mountAdapter != null
                && !_mountAdapter.TryDismissTopModalForVisibilityToggle(
                    out _,
                    out string diagnostic))
            {
                return Fail(
                    "ACTIVE_MODAL_DISMISS_FAILED",
                    diagnostic,
                    source,
                    source);
            }
            _mountAdapter?.UnmountAll();
            CharacterTestRuntimeAiLabPageContext page =
                CharacterTestRuntimeAiLabPageContext.CreateDefault();
            _lastRestorableContext = page;
            Commit(
                CharacterTestRuntimeAiLabShellVisibility.Hidden,
                CharacterTestRuntimeAiLabPresentationMode.Workspace,
                page,
                null,
                null,
                CurrentState.LastCompletedTransactionId,
                CurrentState.LastCompletedOutcome);
            return Succeed(
                "SHELL_CLOSED_TO_PLAYER_DECK",
                source,
                CharacterTestRuntimeAiLabWorkspace.PlayerDeck);
        }

        internal CharacterTestRuntimeAiLabNavigationResult EnterUnavailableRecovery(
            string detail,
            CharacterTestRuntimeAiLabWorkspace failedTarget)
        {
            EnterHiddenRecovery(
                "RECOVERY_SURFACE_UNAVAILABLE",
                detail,
                failedTarget,
                true,
                false);
            return LastNavigationResult;
        }

        private CharacterTestRuntimeAiLabNavigationResult HandleReopenPreflightFailure(
            CharacterTestRuntimeAiLabWorkspace workspace,
            string diagnostic)
        {
            string detail = string.IsNullOrEmpty(diagnostic)
                ? "Runtime AI Lab package or contract preflight failed."
                : diagnostic;
            if (CurrentState.PresentationMode ==
                CharacterTestRuntimeAiLabPresentationMode.SessionStartInProgress)
            {
                CharacterTestRuntimeAiLabSessionStartTransactionContext transaction =
                    CurrentState.Transaction?.WithMountDiagnostic(detail);
                Commit(
                    CharacterTestRuntimeAiLabShellVisibility.Hidden,
                    CharacterTestRuntimeAiLabPresentationMode.SessionStartInProgress,
                    CurrentState.PageContext,
                    null,
                    transaction,
                    CurrentState.LastCompletedTransactionId,
                    CurrentState.LastCompletedOutcome);
                return Fail(
                    "SESSION_START_TRANSITION_REOPEN_FAILED",
                    detail,
                    workspace,
                    workspace);
            }

            EnterHiddenRecovery(
                "RECOVERY_SURFACE_UNAVAILABLE",
                detail,
                CurrentState.Fault?.FailedTarget ?? workspace,
                true,
                CurrentState.Fault?.CanResetSession ?? false);
            return LastNavigationResult;
        }

        private CharacterTestRuntimeAiLabNavigationResult TryReopenWorkspace(
            CharacterTestRuntimeAiLabWorkspace source)
        {
            if (!TryResolveCanonicalWorkspace(
                    out CharacterTestRuntimeAiLabPageContext page,
                    out string fallbackCode,
                    out string fallbackDiagnostic))
            {
                EnterHiddenRecovery(
                    fallbackCode,
                    fallbackDiagnostic,
                    source,
                    true,
                    true);
                return LastNavigationResult;
            }

            bool usedFallback = !SamePageContext(CurrentState.PageContext, page);
            CharacterTestRuntimeAiLabShellState next = CreateReopenState(
                CharacterTestRuntimeAiLabPresentationMode.Workspace,
                page,
                null,
                null);
            _mountAdapter.UnmountAll();
            if (_mountAdapter.TryMountWorkspace(next, out string diagnostic))
            {
                CurrentState = next;
                _lastRestorableContext = page;
                return Succeed(
                    usedFallback ? fallbackCode : "SHELL_REOPENED",
                    source,
                    page.ActiveWorkspace);
            }

            EnterHiddenRecovery(
                "WORKSPACE_RENDER_FAILED",
                diagnostic,
                page.ActiveWorkspace,
                true,
                true);
            return LastNavigationResult;
        }

        private CharacterTestRuntimeAiLabNavigationResult TryReopenSessionStart(
            CharacterTestRuntimeAiLabWorkspace workspace)
        {
            CharacterTestRuntimeAiLabShellState next = CreateReopenState(
                CharacterTestRuntimeAiLabPresentationMode.SessionStartInProgress,
                CurrentState.PageContext,
                null,
                CurrentState.Transaction);
            _mountAdapter.UnmountAll();
            if (_mountAdapter.TryMountTransition(next, out string diagnostic))
            {
                CurrentState = next;
                return Succeed("SHELL_REOPENED", workspace, workspace);
            }

            CharacterTestRuntimeAiLabSessionStartTransactionContext transaction =
                CurrentState.Transaction?.WithMountDiagnostic(diagnostic);
            Commit(
                CharacterTestRuntimeAiLabShellVisibility.Hidden,
                CharacterTestRuntimeAiLabPresentationMode.SessionStartInProgress,
                CurrentState.PageContext,
                null,
                transaction,
                CurrentState.LastCompletedTransactionId,
                CurrentState.LastCompletedOutcome);
            return Fail(
                "SESSION_START_TRANSITION_REOPEN_FAILED",
                diagnostic,
                workspace,
                workspace);
        }

        private CharacterTestRuntimeAiLabNavigationResult TryReopenRecovery(
            CharacterTestRuntimeAiLabWorkspace workspace)
        {
            CharacterTestRuntimeAiLabShellFaultContext fault = CurrentState.Fault;
            CharacterTestRuntimeAiLabShellState next = CreateReopenState(
                CharacterTestRuntimeAiLabPresentationMode.Recovery,
                CurrentState.PageContext,
                fault,
                null);
            _mountAdapter.UnmountAll();
            if (_mountAdapter.TryMountRecovery(next, out string diagnostic))
            {
                CurrentState = next;
                return Succeed("SHELL_REOPENED", workspace, workspace);
            }

            EnterHiddenRecovery(
                "RECOVERY_SURFACE_UNAVAILABLE",
                diagnostic,
                fault?.FailedTarget ?? workspace,
                fault?.CanRetry ?? true,
                fault?.CanResetSession ?? false);
            return LastNavigationResult;
        }

        private CharacterTestRuntimeAiLabShellState CreateReopenState(
            CharacterTestRuntimeAiLabPresentationMode presentationMode,
            CharacterTestRuntimeAiLabPageContext page,
            CharacterTestRuntimeAiLabShellFaultContext fault,
            CharacterTestRuntimeAiLabSessionStartTransactionContext transaction)
        {
            return new CharacterTestRuntimeAiLabShellState(
                CurrentState.Generation,
                CurrentState.Revision + 1,
                CharacterTestRuntimeAiLabShellVisibility.Visible,
                presentationMode,
                page,
                fault,
                transaction,
                CurrentState.LastCompletedTransactionId,
                CurrentState.LastCompletedOutcome);
        }

        internal CharacterTestRuntimeAiLabNavigationResult TryUpdatePlayerDeckContext(
            long expectedGeneration,
            long expectedRevision,
            int pageIndex,
            string replacementSourceStableCardId)
        {
            CharacterTestRuntimeAiLabWorkspace source = CharacterTestRuntimeAiLabWorkspace.PlayerDeck;
            CharacterTestRuntimeAiLabNavigationResult stale;
            if (!ValidateCommand(expectedGeneration, expectedRevision, source, source, out stale))
                return stale;
            CharacterTestRuntimeAiLabPageContext page =
                CurrentState.PageContext.WithPlayerDeck(pageIndex, replacementSourceStableCardId);
            CommitWorkspaceContext(page);
            return Succeed("PLAYER_DECK_CONTEXT_UPDATED", source, source);
        }

        internal CharacterTestRuntimeAiLabNavigationResult TryUpdateBehaviorCardContext(
            long expectedGeneration,
            long expectedRevision,
            string groupDraftId,
            string entryId,
            string cardDraftId,
            CharacterTestRuntimeAiLabSelectedElementKind selectedKind,
            string selectedStableId,
            CharacterTestRuntimeAiLabBehaviorCardViewport viewport)
        {
            CharacterTestRuntimeAiLabWorkspace source = CurrentState.ActiveWorkspace;
            CharacterTestRuntimeAiLabNavigationResult stale;
            if (!ValidateCommand(expectedGeneration, expectedRevision, source, source, out stale))
                return stale;
            if (source != CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                && source != CharacterTestRuntimeAiLabWorkspace.RuntimeFrame)
            {
                return Fail(
                    "BEHAVIOR_CARD_CONTEXT_HOST_INVALID",
                    "Behavior Card context can only be updated by its editor or the unified timeline host.",
                    source,
                    source);
            }
            if (string.IsNullOrWhiteSpace(groupDraftId)
                || string.IsNullOrWhiteSpace(entryId)
                || string.IsNullOrWhiteSpace(cardDraftId))
                return Fail("BEHAVIOR_CARD_CONTEXT_INVALID", "Stable behavior card identity is required.", source, source);

            CharacterTestRuntimeAiLabPageContext page = CurrentState.PageContext.WithBehaviorCard(
                groupDraftId,
                entryId,
                cardDraftId,
                selectedKind,
                selectedStableId,
                viewport);
            CommitWorkspaceContext(page);
            return Succeed("BEHAVIOR_CARD_CONTEXT_UPDATED", source, source);
        }

        internal CharacterTestRuntimeAiLabNavigationResult TryUpdateBehaviorCardLibraryState(
            long expectedGeneration,
            long expectedRevision,
            CharacterTestRuntimeAiLabBehaviorCardLibraryState libraryState)
        {
            CharacterTestRuntimeAiLabWorkspace source = CurrentState.ActiveWorkspace;
            CharacterTestRuntimeAiLabNavigationResult stale;
            if (!ValidateCommand(expectedGeneration, expectedRevision, source, source, out stale))
                return stale;
            if (source != CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                && (source != CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                    || (CurrentState.PageContext.TimelineMode
                            != CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan
                        && CurrentState.PageContext.TimelineMode
                            != CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic)))
            {
                return Fail(
                    "BEHAVIOR_CARD_LIBRARY_HOST_INVALID",
                    "Behavior Card library context is not active.",
                    source,
                    source);
            }
            CharacterTestRuntimeAiLabPageContext page = CurrentState.PageContext
                .WithBehaviorCardLibrary(libraryState);
            CommitWorkspaceContext(page);
            return Succeed("BEHAVIOR_CARD_LIBRARY_CONTEXT_UPDATED", source, source);
        }

        /// <remarks>
        /// LowFreqAlloc. Called only for explicit user mode switches; it commits presentation
        /// state without unmounting the active RuntimeFrame workspace.
        /// </remarks>
        internal CharacterTestRuntimeAiLabNavigationResult TrySetTimelineMode(
            long expectedGeneration,
            long expectedRevision,
            CharacterTestRuntimeAiLabWorkspace source,
            CharacterTestRuntimeAiLabTimelineMode mode,
            bool outgoingModeExpanded)
        {
            CharacterTestRuntimeAiLabNavigationResult stale;
            if (!ValidateCommand(expectedGeneration, expectedRevision, source, source, out stale))
                return stale;
            if (CurrentState.PresentationMode != CharacterTestRuntimeAiLabPresentationMode.Workspace)
            {
                return Fail(
                    "TIMELINE_MODE_PRESENTATION_BUSY",
                    "Timeline mode cannot change while the shell is transitioning or recovering.",
                    source,
                    source);
            }
            if (source != CharacterTestRuntimeAiLabWorkspace.RuntimeFrame)
            {
                return Fail(
                    "TIMELINE_MODE_HOST_INACTIVE",
                    "Timeline mode is only available in the Runtime Test Workbench.",
                    source,
                    source);
            }
            if (_mountAdapter == null
                || _mountAdapter.IsSetupPhase
                || !_mountAdapter.HasRuntimeHistory)
            {
                return Fail(
                    "TIMELINE_MODE_SESSION_UNAVAILABLE",
                    "Timeline mode requires an active session with retained RuntimeFrame history.",
                    source,
                    source);
            }
            if (mode == CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan
                && (string.IsNullOrEmpty(CurrentState.PageContext.GroupDraftId)
                    || string.IsNullOrEmpty(CurrentState.PageContext.EntryId)
                    || string.IsNullOrEmpty(CurrentState.PageContext.CardDraftId)))
            {
                return Fail(
                    "BEHAVIOR_CARD_CONTEXT_UNAVAILABLE",
                    "Behavior Plan requires a stable Behavior Card selection.",
                    source,
                    source);
            }

            CharacterTestRuntimeAiLabPageContext page = CurrentState.PageContext
                .WithTimelineExpanded(
                    CurrentState.PageContext.TimelineMode,
                    outgoingModeExpanded)
                .WithTimelineMode(mode);
            CommitWorkspaceContext(page);
            return Succeed(
                mode == CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace
                    ? "RUNTIME_TRACE_MODE_ACTIVE"
                    : mode == CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan
                        ? "BEHAVIOR_PLAN_MODE_ACTIVE"
                        : "BEHAVIOR_LOGIC_MODE_ACTIVE",
                source,
                source);
        }

        internal CharacterTestRuntimeAiLabNavigationResult TryUpdateRuntimeFrameContext(
            long expectedGeneration,
            long expectedRevision,
            CharacterTestRuntimeAiLabRuntimeFramePageContext runtimeFrameContext)
        {
            CharacterTestRuntimeAiLabWorkspace source = CharacterTestRuntimeAiLabWorkspace.RuntimeFrame;
            CharacterTestRuntimeAiLabNavigationResult stale;
            if (!ValidateCommand(expectedGeneration, expectedRevision, source, source, out stale))
                return stale;
            CharacterTestRuntimeAiLabPageContext page = CurrentState.PageContext
                .WithRuntimeFrame(runtimeFrameContext);
            CommitWorkspaceContext(page);
            return Succeed("RUNTIME_FRAME_CONTEXT_UPDATED", source, source);
        }

        internal CharacterTestRuntimeAiLabNavigationResult TryRestoreBehaviorCardPresentationContext(
            CharacterTestRuntimeAiLabPageContext savedContext)
        {
            CharacterTestRuntimeAiLabWorkspace behaviorCard = CharacterTestRuntimeAiLabWorkspace.BehaviorCard;
            CharacterTestRuntimeAiLabWorkspace source = CurrentState.ActiveWorkspace;
            if (_mountAdapter == null
                || savedContext == null
                || savedContext.ActiveWorkspace != behaviorCard)
            {
                return Fail(
                    "BEHAVIOR_CARD_CONTEXT_RESTORE_INVALID",
                    "The saved Behavior Card presentation context is unavailable.",
                    source,
                    source);
            }
            if (!_mountAdapter.TryNormalizeWorkspaceContext(
                    behaviorCard,
                    savedContext,
                    out CharacterTestRuntimeAiLabPageContext normalized,
                    out string diagnostic))
            {
                return Fail(
                    "BEHAVIOR_CARD_CONTEXT_RESTORE_FAILED",
                    diagnostic,
                    source,
                    source);
            }
            CharacterTestRuntimeAiLabPageContext restored = source == behaviorCard
                ? normalized
                : CurrentState.PageContext
                    .WithBehaviorCard(
                        normalized.GroupDraftId,
                        normalized.EntryId,
                        normalized.CardDraftId,
                        normalized.SelectedElementKind,
                        normalized.SelectedElementStableId,
                        normalized.BehaviorCardViewport)
                    .WithBehaviorCardLibrary(normalized.BehaviorCardLibraryState);
            CommitWorkspaceContext(restored);
            return Succeed("BEHAVIOR_CARD_CONTEXT_RESTORED", source, source);
        }

        internal CharacterTestRuntimeAiLabNavigationResult BeginSessionStart(
            long expectedGeneration,
            long expectedRevision,
            CharacterTestRuntimeAiLabWorkspace source,
            CharacterTestRuntimeAiLabWorkspace successTarget,
            object recoverySnapshot,
            Func<bool> startSession,
            Func<object, bool> recoverFailedStart,
            CharacterTestRuntimeAiLabTimelineMode? successTimelineMode = null)
        {
            CharacterTestRuntimeAiLabNavigationResult stale;
            if (!ValidateCommand(expectedGeneration, expectedRevision, source, successTarget, out stale))
                return stale;
            if (CurrentState.PresentationMode != CharacterTestRuntimeAiLabPresentationMode.Workspace)
                return Fail("SESSION_START_ALREADY_IN_PROGRESS", "A session-start transaction is already active.", source, successTarget);
            if (_mountAdapter == null)
                return Fail("NAVIGATION_ADAPTER_UNAVAILABLE", "The shell UI is unavailable.", source, successTarget);

            long transactionId = ++_nextTransactionId;
            var transaction = new CharacterTestRuntimeAiLabSessionStartTransactionContext(
                transactionId,
                CurrentState.Generation,
                CurrentState.Revision,
                CurrentState.PageContext,
                successTarget,
                recoverySnapshot,
                successTimelineMode);
            var transitionState = new CharacterTestRuntimeAiLabShellState(
                CurrentState.Generation,
                CurrentState.Revision + 1,
                CharacterTestRuntimeAiLabShellVisibility.Visible,
                CharacterTestRuntimeAiLabPresentationMode.SessionStartInProgress,
                CurrentState.PageContext,
                null,
                transaction,
                CurrentState.LastCompletedTransactionId,
                CurrentState.LastCompletedOutcome);
            _mountAdapter.UnmountAll();
            if (!_mountAdapter.TryMountTransition(transitionState, out string transitionDiagnostic))
            {
                return RestoreSourceAfterPreflightFailure(
                    source,
                    successTarget,
                    transitionDiagnostic);
            }

            CurrentState = transitionState;
            bool started = startSession != null && startSession();
            return CompleteSessionStart(transactionId, started, recoverFailedStart);
        }

        internal CharacterTestRuntimeAiLabNavigationResult CompleteSessionStart(
            long transactionId,
            bool succeeded,
            Func<object, bool> recoverFailedStart)
        {
            CharacterTestRuntimeAiLabWorkspace source = CurrentState.ActiveWorkspace;
            CharacterTestRuntimeAiLabSessionStartTransactionContext transaction = CurrentState.Transaction;
            if (transaction == null || transaction.TransactionId != transactionId)
            {
                string code = CurrentState.LastCompletedTransactionId == transactionId
                    ? "TRANSACTION_ALREADY_COMPLETED"
                    : "TRANSACTION_STALE";
                return Fail(code, "The session-start completion is stale.", source, source);
            }

            if (succeeded)
            {
                CharacterTestRuntimeAiLabPageContext page = transaction.SourceContext;
                if (transaction.SourceContext.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.Setup)
                    page = page.WithReturnWorkspace(CharacterTestRuntimeAiLabWorkspace.Setup);
                CharacterTestRuntimeAiLabWorkspace successWorkspace = transaction.SuccessTarget;
                if (successWorkspace == CharacterTestRuntimeAiLabWorkspace.BehaviorCard)
                {
                    page = page
                        .WithTimelineMode(CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic)
                        .WithWorkspace(CharacterTestRuntimeAiLabWorkspace.RuntimeFrame);
                    successWorkspace = CharacterTestRuntimeAiLabWorkspace.RuntimeFrame;
                }
                else
                {
                    page = page.WithWorkspace(successWorkspace);
                    if (successWorkspace == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame)
                    {
                        page = page.WithTimelineMode(
                            transaction.SuccessTimelineMode
                            ?? CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace);
                    }
                }
                CharacterTestRuntimeAiLabShellVisibility visibility = CurrentState.Visibility;
                Commit(
                    visibility,
                    CharacterTestRuntimeAiLabPresentationMode.Workspace,
                    page,
                    null,
                    null,
                    transaction.TransactionId,
                    CharacterTestRuntimeAiLabSessionStartOutcome.Succeeded);
                _lastRestorableContext = page;
                if (visibility == CharacterTestRuntimeAiLabShellVisibility.Hidden)
                    return Succeed("SESSION_START_COMPLETED_HIDDEN", source, successWorkspace);
                CharacterTestRuntimeAiLabNavigationResult mounted = MountCurrentState("SESSION_START_COMPLETED");
                if (!mounted.Success)
                    EnterHiddenRecovery("SESSION_START_RESULT_MOUNT_FAILED", mounted.Message, successWorkspace, true, true);
                return LastNavigationResult;
            }

            bool recovered = recoverFailedStart != null
                && recoverFailedStart(transaction.RecoverySnapshot);
            if (recovered)
            {
                CharacterTestRuntimeAiLabPageContext page = transaction.SourceContext;
                CharacterTestRuntimeAiLabShellVisibility visibility = CurrentState.Visibility;
                Commit(
                    visibility,
                    CharacterTestRuntimeAiLabPresentationMode.Workspace,
                    page,
                    null,
                    null,
                    transaction.TransactionId,
                    CharacterTestRuntimeAiLabSessionStartOutcome.Failed);
                _lastRestorableContext = page;
                if (visibility == CharacterTestRuntimeAiLabShellVisibility.Hidden)
                    return Succeed("SESSION_START_FAILED_RECOVERED_HIDDEN", source, page.ActiveWorkspace);
                CharacterTestRuntimeAiLabNavigationResult mounted = MountCurrentState("SESSION_START_FAILED_RECOVERED");
                if (!mounted.Success)
                    EnterHiddenRecovery("SESSION_START_SOURCE_MOUNT_FAILED", mounted.Message, page.ActiveWorkspace, true, true);
                return LastNavigationResult;
            }

            EnterHiddenRecovery(
                "SESSION_START_RECOVERY_REQUIRED",
                "Session start failed and the pre-start state could not be restored exactly.",
                transaction.SourceContext.ActiveWorkspace,
                true,
                true,
                transaction.TransactionId,
                CharacterTestRuntimeAiLabSessionStartOutcome.Failed);
            return LastNavigationResult;
        }

        internal CharacterTestRuntimeAiLabNavigationResult EnterRecovery(
            string code,
            string detail,
            CharacterTestRuntimeAiLabWorkspace failedTarget,
            bool canRetry,
            bool canResetSession)
        {
            _mountAdapter?.UnmountAll();
            var fault = new CharacterTestRuntimeAiLabShellFaultContext(
                code,
                "Runtime AI Lab needs recovery",
                detail,
                failedTarget,
                canRetry,
                canResetSession);
            Commit(
                CharacterTestRuntimeAiLabShellVisibility.Visible,
                CharacterTestRuntimeAiLabPresentationMode.Recovery,
                CurrentState.PageContext,
                fault,
                null,
                CurrentState.LastCompletedTransactionId,
                CurrentState.LastCompletedOutcome);
            string diagnostic = string.Empty;
            if (_mountAdapter == null || !_mountAdapter.TryMountRecovery(CurrentState, out diagnostic))
                EnterHiddenRecovery("RECOVERY_SURFACE_UNAVAILABLE", diagnostic, failedTarget, true, canResetSession);
            else
                Succeed("RECOVERY_VISIBLE", failedTarget, failedTarget);
            return LastNavigationResult;
        }

        internal CharacterTestRuntimeAiLabNavigationResult RestoreLastWorkspace(
            long expectedGeneration,
            long expectedRevision)
        {
            CharacterTestRuntimeAiLabWorkspace source = CurrentState.ActiveWorkspace;
            CharacterTestRuntimeAiLabShellFaultContext fault = CurrentState.Fault;
            if (expectedGeneration != CurrentState.Generation || expectedRevision != CurrentState.Revision)
                return Fail("NAVIGATION_STALE_COMMAND", "The recovery command is stale.", source, source);
            if (CurrentState.PresentationMode != CharacterTestRuntimeAiLabPresentationMode.Recovery
                || fault == null
                || !fault.CanRetry)
            {
                return Fail("RECOVERY_RESTORE_UNAVAILABLE", "No safe workspace can be restored.", source, source);
            }
            if (_mountAdapter == null)
                return Fail("NAVIGATION_ADAPTER_UNAVAILABLE", "The shell UI is unavailable.", source, source);

            CharacterTestRuntimeAiLabPageContext page = _lastRestorableContext;
            var next = new CharacterTestRuntimeAiLabShellState(
                CurrentState.Generation,
                CurrentState.Revision + 1,
                CharacterTestRuntimeAiLabShellVisibility.Visible,
                CharacterTestRuntimeAiLabPresentationMode.Workspace,
                page,
                null,
                null,
                CurrentState.LastCompletedTransactionId,
                CurrentState.LastCompletedOutcome);
            _mountAdapter.UnmountAll();
            string diagnostic = "The last safe workspace is unavailable.";
            if (_mountAdapter.IsWorkspaceAvailable(page.ActiveWorkspace)
                && _mountAdapter.TryMountWorkspace(next, out diagnostic))
            {
                CurrentState = next;
                return Succeed("RECOVERY_LAST_WORKSPACE_RESTORED", source, page.ActiveWorkspace);
            }

            if (!_mountAdapter.TryMountRecovery(CurrentState, out string recoveryDiagnostic))
            {
                EnterHiddenRecovery(
                    "RECOVERY_RESTORE_AND_SURFACE_FAILED",
                    recoveryDiagnostic,
                    fault.FailedTarget,
                    true,
                    fault.CanResetSession);
                return LastNavigationResult;
            }
            return Fail("RECOVERY_LAST_WORKSPACE_MOUNT_FAILED", diagnostic, source, page.ActiveWorkspace);
        }

        internal CharacterTestRuntimeAiLabNavigationResult ResetRecoveryToSetup(
            long expectedGeneration,
            long expectedRevision,
            Func<bool> resetSession)
        {
            CharacterTestRuntimeAiLabWorkspace source = CurrentState.ActiveWorkspace;
            CharacterTestRuntimeAiLabShellFaultContext fault = CurrentState.Fault;
            if (expectedGeneration != CurrentState.Generation || expectedRevision != CurrentState.Revision)
                return Fail("NAVIGATION_STALE_COMMAND", "The recovery command is stale.", source, source);
            if (CurrentState.PresentationMode != CharacterTestRuntimeAiLabPresentationMode.Recovery
                || fault == null
                || !fault.CanResetSession)
            {
                return Fail("RECOVERY_RESET_UNAVAILABLE", "Session reset is not available.", source, source);
            }
            if (resetSession == null || !resetSession())
                return Fail("RECOVERY_RESET_FAILED", "The Runtime AI Lab session could not be reset.", source, source);
            if (_mountAdapter == null)
                return Fail("NAVIGATION_ADAPTER_UNAVAILABLE", "The shell UI is unavailable.", source, source);

            CharacterTestRuntimeAiLabPageContext page = CurrentState.PageContext
                .WithReturnWorkspace(CharacterTestRuntimeAiLabWorkspace.Setup)
                .WithWorkspace(CharacterTestRuntimeAiLabWorkspace.Setup);
            var next = new CharacterTestRuntimeAiLabShellState(
                CurrentState.Generation,
                CurrentState.Revision + 1,
                CharacterTestRuntimeAiLabShellVisibility.Visible,
                CharacterTestRuntimeAiLabPresentationMode.Workspace,
                page,
                null,
                null,
                CurrentState.LastCompletedTransactionId,
                CurrentState.LastCompletedOutcome);
            _mountAdapter.UnmountAll();
            string diagnostic = "The Setup workspace is unavailable.";
            if (_mountAdapter.IsWorkspaceAvailable(CharacterTestRuntimeAiLabWorkspace.Setup)
                && _mountAdapter.TryMountWorkspace(next, out diagnostic))
            {
                CurrentState = next;
                _lastRestorableContext = page;
                return Succeed("RECOVERY_RESET_TO_SETUP", source, CharacterTestRuntimeAiLabWorkspace.Setup);
            }

            if (!_mountAdapter.TryMountRecovery(CurrentState, out string recoveryDiagnostic))
            {
                EnterHiddenRecovery(
                    "RECOVERY_RESET_MOUNT_AND_SURFACE_FAILED",
                    recoveryDiagnostic,
                    CharacterTestRuntimeAiLabWorkspace.Setup,
                    true,
                    true);
                return LastNavigationResult;
            }
            return Fail("RECOVERY_RESET_SETUP_MOUNT_FAILED", diagnostic, source, CharacterTestRuntimeAiLabWorkspace.Setup);
        }

        private CharacterTestRuntimeAiLabNavigationResult TryCommitAndMountWorkspace(
            CharacterTestRuntimeAiLabPageContext page,
            CharacterTestRuntimeAiLabWorkspace source,
            CharacterTestRuntimeAiLabWorkspace target,
            string successCode)
        {
            CharacterTestRuntimeAiLabShellState previous = CurrentState;
            _mountAdapter.UnmountAll();
            var next = new CharacterTestRuntimeAiLabShellState(
                previous.Generation,
                previous.Revision + 1,
                CharacterTestRuntimeAiLabShellVisibility.Visible,
                CharacterTestRuntimeAiLabPresentationMode.Workspace,
                page,
                null,
                null,
                previous.LastCompletedTransactionId,
                previous.LastCompletedOutcome);
            if (_mountAdapter.TryMountWorkspace(next, out string targetDiagnostic))
            {
                CurrentState = next;
                _lastRestorableContext = page;
                return Succeed(successCode, source, target);
            }

            if (_mountAdapter.TryMountWorkspace(previous, out string sourceDiagnostic))
            {
                return Fail(
                    "NAVIGATION_TARGET_MOUNT_FAILED",
                    targetDiagnostic,
                    source,
                    target);
            }

            CurrentState = new CharacterTestRuntimeAiLabShellState(
                previous.Generation,
                previous.Revision + 1,
                CharacterTestRuntimeAiLabShellVisibility.Hidden,
                CharacterTestRuntimeAiLabPresentationMode.Workspace,
                previous.PageContext,
                null,
                null,
                previous.LastCompletedTransactionId,
                previous.LastCompletedOutcome);
            return Fail(
                "NAVIGATION_SOURCE_RESTORE_FAILED",
                sourceDiagnostic,
                source,
                target);
        }

        private CharacterTestRuntimeAiLabNavigationResult RestoreSourceAfterPreflightFailure(
            CharacterTestRuntimeAiLabWorkspace source,
            CharacterTestRuntimeAiLabWorkspace target,
            string transitionDiagnostic)
        {
            if (_mountAdapter.TryMountWorkspace(CurrentState, out string sourceDiagnostic))
            {
                return Fail(
                    "SESSION_START_TRANSITION_MOUNT_FAILED",
                    transitionDiagnostic,
                    source,
                    target);
            }

            EnterHiddenRecovery(
                "SESSION_START_TRANSITION_AND_SOURCE_MOUNT_FAILED",
                sourceDiagnostic,
                target,
                true,
                false);
            return LastNavigationResult;
        }

        private CharacterTestRuntimeAiLabNavigationResult MountWorkspaceWithCanonicalFallback(
            string successCode)
        {
            CharacterTestRuntimeAiLabWorkspace source = CurrentState.ActiveWorkspace;
            if (_mountAdapter == null)
                return Fail("NAVIGATION_ADAPTER_UNAVAILABLE", "The shell UI is unavailable.", source, source);

            if (!TryResolveCanonicalWorkspace(
                    out CharacterTestRuntimeAiLabPageContext page,
                    out string fallbackCode,
                    out string fallbackDiagnostic))
            {
                _mountAdapter.UnmountAll();
                Commit(
                    CharacterTestRuntimeAiLabShellVisibility.Hidden,
                    CharacterTestRuntimeAiLabPresentationMode.Workspace,
                    CurrentState.PageContext,
                    null,
                    null,
                    CurrentState.LastCompletedTransactionId,
                    CurrentState.LastCompletedOutcome);
                return Fail(
                    fallbackCode,
                    fallbackDiagnostic,
                    source,
                    source);
            }

            bool usedFallback = !SamePageContext(CurrentState.PageContext, page);
            if (usedFallback)
            {
                Commit(
                    CharacterTestRuntimeAiLabShellVisibility.Visible,
                    CharacterTestRuntimeAiLabPresentationMode.Workspace,
                    page,
                    null,
                    null,
                    CurrentState.LastCompletedTransactionId,
                    CurrentState.LastCompletedOutcome);
            }

            _mountAdapter.UnmountAll();
            if (_mountAdapter.TryMountWorkspace(CurrentState, out string mountDiagnostic))
            {
                _lastRestorableContext = page;
                return Succeed(
                    usedFallback ? fallbackCode : successCode,
                    source,
                    page.ActiveWorkspace);
            }

            EnterRecovery(
                "WORKSPACE_RENDER_FAILED",
                mountDiagnostic,
                page.ActiveWorkspace,
                true,
                true);
            return LastNavigationResult;
        }

        private bool TryResolveCanonicalWorkspace(
            out CharacterTestRuntimeAiLabPageContext page,
            out string code,
            out string diagnostic)
        {
            CharacterTestRuntimeAiLabPageContext current = CurrentState.PageContext;
            CharacterTestRuntimeAiLabWorkspace active = current.ActiveWorkspace;
            if (TryResolveCandidate(active, current, out page, out diagnostic))
            {
                code = "WORKSPACE_CONTEXT_VALID";
                return true;
            }

            if (IsDiagnostics(active)
                && TryResolveCandidate(current.ReturnWorkspace, current, out page, out diagnostic))
            {
                code = "DIAGNOSTICS_CONTEXT_FALLBACK";
                return true;
            }

            if (active == CharacterTestRuntimeAiLabWorkspace.BehaviorCard)
            {
                CharacterTestRuntimeAiLabWorkspace returnWorkspace =
                    current.BehaviorCardReturnWorkspace;
                if (TryResolveCandidate(returnWorkspace, current, out page, out diagnostic))
                {
                    code = "BEHAVIOR_CARD_CONTEXT_FALLBACK";
                    return true;
                }
                if (returnWorkspace == CharacterTestRuntimeAiLabWorkspace.BehaviorGroup
                    && TryResolveCandidate(
                        CharacterTestRuntimeAiLabWorkspace.Setup,
                        current,
                        out page,
                        out diagnostic))
                {
                    code = "BEHAVIOR_GROUP_CONTEXT_FALLBACK";
                    return true;
                }
            }

            if (active == CharacterTestRuntimeAiLabWorkspace.BehaviorGroup
                && TryResolveCandidate(
                    CharacterTestRuntimeAiLabWorkspace.Setup,
                    current,
                    out page,
                    out diagnostic))
            {
                code = "BEHAVIOR_GROUP_WORKSPACE_RETIRED";
                return true;
            }

            if (active == CharacterTestRuntimeAiLabWorkspace.PlayerDeck
                && TryResolveCandidate(
                    CharacterTestRuntimeAiLabWorkspace.Setup,
                    current,
                    out page,
                    out diagnostic))
            {
                code = "PLAYER_DECK_WORKSPACE_UNAVAILABLE";
                return true;
            }

            if (_mountAdapter.IsSetupPhase)
            {
                if (TryResolveCandidate(
                        CharacterTestRuntimeAiLabWorkspace.PlayerDeck,
                        current,
                        out page,
                        out diagnostic))
                {
                    code = "SETUP_PHASE_PLAYER_DECK_FALLBACK";
                    return true;
                }
                if (TryResolveCandidate(
                        CharacterTestRuntimeAiLabWorkspace.Setup,
                        current,
                        out page,
                        out diagnostic))
                {
                    code = "SETUP_PHASE_SETUP_FALLBACK";
                    return true;
                }
            }
            else if (_mountAdapter.HasRuntimeHistory
                && TryResolveCandidate(
                    CharacterTestRuntimeAiLabWorkspace.Facts,
                    current,
                    out page,
                    out diagnostic))
            {
                code = "RUNNING_PHASE_FACTS_FALLBACK";
                return true;
            }

            page = null;
            code = _mountAdapter.IsSetupPhase
                ? "NO_RESTORABLE_WORKSPACE"
                : "SESSION_CONTEXT_UNAVAILABLE";
            diagnostic = string.IsNullOrEmpty(diagnostic)
                ? "No valid Runtime AI Lab workspace can be restored."
                : diagnostic;
            return false;
        }

        private bool TryResolveCandidate(
            CharacterTestRuntimeAiLabWorkspace workspace,
            CharacterTestRuntimeAiLabPageContext context,
            out CharacterTestRuntimeAiLabPageContext page,
            out string diagnostic)
        {
            page = null;
            diagnostic = string.Empty;
            if (_mountAdapter == null || !_mountAdapter.IsWorkspaceAvailable(workspace))
            {
                diagnostic = workspace + " workspace is unavailable.";
                return false;
            }

            CharacterTestRuntimeAiLabPageContext candidate = context.WithWorkspace(workspace);
            return _mountAdapter.TryNormalizeWorkspaceContext(
                workspace,
                candidate,
                out page,
                out diagnostic);
        }

        private static bool SamePageContext(
            CharacterTestRuntimeAiLabPageContext left,
            CharacterTestRuntimeAiLabPageContext right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;
            CharacterTestRuntimeAiLabBehaviorCardViewport leftViewport = left.BehaviorCardViewport;
            CharacterTestRuntimeAiLabBehaviorCardViewport rightViewport = right.BehaviorCardViewport;
            CharacterTestRuntimeAiLabBehaviorCardLibraryState leftLibrary = left.BehaviorCardLibraryState;
            CharacterTestRuntimeAiLabBehaviorCardLibraryState rightLibrary = right.BehaviorCardLibraryState;
            return left.ActiveWorkspace == right.ActiveWorkspace
                && left.ReturnWorkspace == right.ReturnWorkspace
                && left.BehaviorCardReturnWorkspace == right.BehaviorCardReturnWorkspace
                && left.PlayerDeckPageIndex == right.PlayerDeckPageIndex
                && string.Equals(left.ReplacementSourceStableCardId, right.ReplacementSourceStableCardId, StringComparison.Ordinal)
                && string.Equals(left.GroupDraftId, right.GroupDraftId, StringComparison.Ordinal)
                && string.Equals(left.EntryId, right.EntryId, StringComparison.Ordinal)
                && string.Equals(left.CardDraftId, right.CardDraftId, StringComparison.Ordinal)
                && left.SelectedElementKind == right.SelectedElementKind
                && string.Equals(left.SelectedElementStableId, right.SelectedElementStableId, StringComparison.Ordinal)
                && leftViewport.HorizontalStartFrame == rightViewport.HorizontalStartFrame
                && leftViewport.HorizontalEndFrame == rightViewport.HorizontalEndFrame
                && leftViewport.ZoomLevel.Equals(rightViewport.ZoomLevel)
                && leftViewport.VerticalScrollOffset.Equals(rightViewport.VerticalScrollOffset)
                && leftViewport.FollowCurrent == rightViewport.FollowCurrent
                && string.Equals(leftLibrary.Query, rightLibrary.Query, StringComparison.Ordinal)
                && leftLibrary.Filter == rightLibrary.Filter
                && string.Equals(leftLibrary.FirstVisibleStableCardId, rightLibrary.FirstVisibleStableCardId, StringComparison.Ordinal)
                && string.Equals(leftLibrary.FocusedStableCardId, rightLibrary.FocusedStableCardId, StringComparison.Ordinal)
                && leftLibrary.ScrollOffsetY.Equals(rightLibrary.ScrollOffsetY)
                && left.TimelineMode == right.TimelineMode
                && left.RuntimeTraceExpanded == right.RuntimeTraceExpanded
                && left.BehaviorPlanExpanded == right.BehaviorPlanExpanded
                && SameRuntimeFrameContext(left.RuntimeFrameContext, right.RuntimeFrameContext);
        }

        private static bool SameRuntimeFrameContext(
            CharacterTestRuntimeAiLabRuntimeFramePageContext left,
            CharacterTestRuntimeAiLabRuntimeFramePageContext right)
        {
            CharacterTestRuntimeAiLabTimelineViewport leftViewport = left.Viewport;
            CharacterTestRuntimeAiLabTimelineViewport rightViewport = right.Viewport;
            CharacterTestRuntimeAiLabCommittedFrameReceipt leftReceipt = left.CommittedReceipt;
            CharacterTestRuntimeAiLabCommittedFrameReceipt rightReceipt = right.CommittedReceipt;
            CharacterTestRuntimeAiLabTimelineItemLocator leftLocator = left.SelectedLocator;
            CharacterTestRuntimeAiLabTimelineItemLocator rightLocator = right.SelectedLocator;
            return leftViewport.StartFrame == rightViewport.StartFrame
                && leftViewport.EndFrame == rightViewport.EndFrame
                && leftViewport.ZoomLevel == rightViewport.ZoomLevel
                && leftViewport.PixelWidth.Equals(rightViewport.PixelWidth)
                && leftViewport.FollowLatest == rightViewport.FollowLatest
                && leftViewport.Mode == rightViewport.Mode
                && left.DetailsVisible == right.DetailsVisible
                && leftReceipt.IsValid == rightReceipt.IsValid
                && (!leftReceipt.IsValid
                    || (leftReceipt.CommitGeneration == rightReceipt.CommitGeneration
                        && leftReceipt.Frame == rightReceipt.Frame
                        && leftReceipt.Bundle.SessionIdentity.Generation == rightReceipt.Bundle.SessionIdentity.Generation
                        && leftReceipt.Bundle.RecordGeneration == rightReceipt.Bundle.RecordGeneration
                        && leftReceipt.AuthorityHash == rightReceipt.AuthorityHash))
                && leftLocator.IsValid == rightLocator.IsValid
                && (!leftLocator.IsValid
                    || (string.Equals(leftLocator.StableKey, rightLocator.StableKey, StringComparison.Ordinal)
                        && leftLocator.SessionGeneration == rightLocator.SessionGeneration
                        && leftLocator.RecordGeneration == rightLocator.RecordGeneration
                        && leftLocator.AuthorityHash == rightLocator.AuthorityHash
                        && leftLocator.StartFrame == rightLocator.StartFrame
                        && leftLocator.EndFrame == rightLocator.EndFrame));
        }

        private CharacterTestRuntimeAiLabNavigationResult MountCurrentState(string successCode)
        {
            CharacterTestRuntimeAiLabWorkspace workspace = CurrentState.ActiveWorkspace;
            if (_mountAdapter == null)
                return Fail("NAVIGATION_ADAPTER_UNAVAILABLE", "The shell UI is unavailable.", workspace, workspace);

            bool mounted;
            string diagnostic = string.Empty;
            switch (CurrentState.PresentationMode)
            {
                case CharacterTestRuntimeAiLabPresentationMode.SessionStartInProgress:
                    mounted = _mountAdapter.TryMountTransition(CurrentState, out diagnostic);
                    break;
                case CharacterTestRuntimeAiLabPresentationMode.Recovery:
                    mounted = _mountAdapter.TryMountRecovery(CurrentState, out diagnostic);
                    break;
                default:
                    mounted = _mountAdapter.IsWorkspaceAvailable(workspace)
                        && _mountAdapter.TryMountWorkspace(CurrentState, out diagnostic);
                    break;
            }
            return mounted
                ? Succeed(successCode, workspace, workspace)
                : Fail("NAVIGATION_MOUNT_FAILED", diagnostic, workspace, workspace);
        }

        private void CommitWorkspaceContext(CharacterTestRuntimeAiLabPageContext page)
        {
            Commit(
                CurrentState.Visibility,
                CurrentState.PresentationMode,
                page,
                CurrentState.Fault,
                CurrentState.Transaction,
                CurrentState.LastCompletedTransactionId,
                CurrentState.LastCompletedOutcome);
            _lastRestorableContext = page;
        }

        private void Commit(
            CharacterTestRuntimeAiLabShellVisibility visibility,
            CharacterTestRuntimeAiLabPresentationMode presentationMode,
            CharacterTestRuntimeAiLabPageContext pageContext,
            CharacterTestRuntimeAiLabShellFaultContext fault,
            CharacterTestRuntimeAiLabSessionStartTransactionContext transaction,
            long lastCompletedTransactionId,
            CharacterTestRuntimeAiLabSessionStartOutcome lastCompletedOutcome)
        {
            CurrentState = new CharacterTestRuntimeAiLabShellState(
                CurrentState.Generation,
                CurrentState.Revision + 1,
                visibility,
                presentationMode,
                pageContext,
                fault,
                transaction,
                lastCompletedTransactionId,
                lastCompletedOutcome);
        }

        private void EnterHiddenRecovery(
            string code,
            string detail,
            CharacterTestRuntimeAiLabWorkspace failedTarget,
            bool canRetry,
            bool canResetSession,
            long completedTransactionId = 0,
            CharacterTestRuntimeAiLabSessionStartOutcome completedOutcome =
                CharacterTestRuntimeAiLabSessionStartOutcome.None)
        {
            _mountAdapter?.UnmountAll();
            var fault = new CharacterTestRuntimeAiLabShellFaultContext(
                code,
                "Runtime AI Lab needs recovery",
                detail,
                failedTarget,
                canRetry,
                canResetSession);
            Commit(
                CharacterTestRuntimeAiLabShellVisibility.Hidden,
                CharacterTestRuntimeAiLabPresentationMode.Recovery,
                CurrentState.PageContext,
                fault,
                null,
                completedTransactionId != 0
                    ? completedTransactionId
                    : CurrentState.LastCompletedTransactionId,
                completedOutcome != CharacterTestRuntimeAiLabSessionStartOutcome.None
                    ? completedOutcome
                    : CurrentState.LastCompletedOutcome);
            Fail(code, detail, CurrentState.ActiveWorkspace, failedTarget);
        }

        private bool ValidateCommand(
            long expectedGeneration,
            long expectedRevision,
            CharacterTestRuntimeAiLabWorkspace source,
            CharacterTestRuntimeAiLabWorkspace target,
            out CharacterTestRuntimeAiLabNavigationResult failure)
        {
            if (expectedGeneration != CurrentState.Generation
                || expectedRevision != CurrentState.Revision
                || source != CurrentState.ActiveWorkspace)
            {
                failure = Fail(
                    "NAVIGATION_STALE_COMMAND",
                    "The navigation command no longer matches the visible shell state.",
                    source,
                    target);
                return false;
            }

            failure = default;
            return true;
        }

        private CharacterTestRuntimeAiLabNavigationResult Succeed(
            string code,
            CharacterTestRuntimeAiLabWorkspace source,
            CharacterTestRuntimeAiLabWorkspace target)
        {
            LastNavigationResult = new CharacterTestRuntimeAiLabNavigationResult(
                true,
                code,
                string.Empty,
                source,
                target,
                CurrentState.Revision);
            return LastNavigationResult;
        }

        private CharacterTestRuntimeAiLabNavigationResult Fail(
            string code,
            string message,
            CharacterTestRuntimeAiLabWorkspace source,
            CharacterTestRuntimeAiLabWorkspace target)
        {
            LastNavigationResult = new CharacterTestRuntimeAiLabNavigationResult(
                false,
                code,
                message,
                source,
                target,
                CurrentState.Revision);
            return LastNavigationResult;
        }

        private static bool IsDiagnostics(CharacterTestRuntimeAiLabWorkspace workspace)
        {
            return workspace == CharacterTestRuntimeAiLabWorkspace.Facts
                || workspace == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                || workspace == CharacterTestRuntimeAiLabWorkspace.Replay;
        }
    }
}
