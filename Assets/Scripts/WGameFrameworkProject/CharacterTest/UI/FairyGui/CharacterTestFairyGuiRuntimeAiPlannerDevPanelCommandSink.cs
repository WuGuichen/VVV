using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabThreatWindowEdit
    {
        public CharacterTestRuntimeAiLabThreatWindowEdit(
            int minimumImpactFrames,
            int maximumImpactFrames,
            int cooldownFrames)
        {
            MinimumImpactFrames = minimumImpactFrames;
            MaximumImpactFrames = maximumImpactFrames;
            CooldownFrames = cooldownFrames;
        }

        public int MinimumImpactFrames { get; }
        public int MaximumImpactFrames { get; }
        public int CooldownFrames { get; }
    }

    public sealed class CharacterTestRuntimeAiLabSetupBehaviorGroupBasicInfoPayload
    {
        public string GroupDraftId { get; set; } = string.Empty;
        public string StableGroupId { get; set; } = string.Empty;
        public int SourceRevision { get; set; }
        public long SourceContentHash { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TagsText { get; set; } = string.Empty;
        public bool IsLocalDraft { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabProjectCardMetadataEditPayload
    {
        internal CharacterTestRuntimeAiLabProjectCardMetadataEditPayload(int entryIndex, string value)
            : this(entryIndex, string.Empty, value)
        {
        }

        internal CharacterTestRuntimeAiLabProjectCardMetadataEditPayload(
            int entryIndex,
            string stableCardId,
            string value)
        {
            EntryIndex = entryIndex;
            StableCardId = stableCardId ?? string.Empty;
            Value = value ?? string.Empty;
        }

        internal int EntryIndex { get; }
        internal string StableCardId { get; }
        internal string Value { get; }
    }

    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink : IMxUiCommandSink
    {
        private readonly CharacterTestRuntimeAiPlannerDevSceneController _controller;
        private readonly Func<bool> _closePanel;
        private readonly Func<bool> _refreshPanel;
        private readonly Func<bool> _remountPanel;
        private readonly Action<string> _panelStatusSink;
        private readonly Action<string> _workbenchLogSink;
        private readonly Action<string, bool> _toastSink;
        private readonly CharacterTestRuntimeAiLabPanelNavigationState _navigation =
            new CharacterTestRuntimeAiLabPanelNavigationState();
        private CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler _behaviorCardWorkspaceCommands;
        private string _lastStatusMessage = string.Empty;
        private bool _behaviorRecipeSaveStatusPending;
        private bool _shellTransitionStatusPending;
        private string _selectedLibraryStableCardId = string.Empty;
        internal CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot CharacterActionWorkspace { get; private set; }

        internal void SetSelectedLibraryCard(string stableCardId)
        {
            string nextStableCardId = stableCardId?.Trim() ?? string.Empty;
            if (!string.Equals(
                    _selectedLibraryStableCardId,
                    nextStableCardId,
                    StringComparison.Ordinal))
            {
                _controller?.DiscardStandaloneProjectCardChanges(_selectedLibraryStableCardId);
            }
            _selectedLibraryStableCardId = nextStableCardId;
        }

        private void ShowToast(string message, bool isWarning = true)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _toastSink?.Invoke(message, isWarning);
            }
        }

        internal CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            Func<bool> closePanel = null,
            Func<bool> refreshPanel = null,
            Action<string> panelStatusSink = null,
            Action<string> workbenchLogSink = null,
            Action<string, bool> toastSink = null,
            Func<bool> remountPanel = null)
        {
            _controller = controller;
            _closePanel = closePanel;
            _refreshPanel = refreshPanel;
            _remountPanel = remountPanel ?? refreshPanel;
            _panelStatusSink = panelStatusSink;
            _workbenchLogSink = workbenchLogSink;
            _toastSink = toastSink;
#if UNITY_EDITOR
            RestoreOpenCharacterActionWorkspaceAfterHostRebind();
#endif
        }

        public int CommandCount { get; private set; }
        public string LastStatusMessage
        {
            get => _lastStatusMessage;
            private set
            {
                _lastStatusMessage = value ?? string.Empty;
                _behaviorRecipeSaveStatusPending = false;
                _shellTransitionStatusPending = false;
            }
        }
        public MxUiCommand LastCommand { get; private set; }
        internal int PendingSetupBehaviorGroupDeleteDraftId { get; private set; } = -1;
        internal CharacterTestRuntimeAiLabWorkspaceMode WorkspaceMode
        {
            get => _navigation.WorkspaceMode;
            private set => _navigation.WorkspaceMode = value;
        }
        internal CharacterTestRuntimeAiLabDiagnosticsTab DiagnosticsTab
        {
            get => _navigation.DiagnosticsTab;
            private set => _navigation.DiagnosticsTab = value;
        }
        internal CharacterTestRuntimeAiLabTimelineMode TimelineMode
        {
            get => _navigation.TimelineMode;
            private set => _navigation.TimelineMode = value;
        }
        internal CharacterTestRuntimeAiLabWorkspaceMode WorkspaceBeforeDiagnostics =>
            _navigation.WorkspaceBeforeDiagnostics;
        internal int SelectedWorkbenchEntryIndex
        {
            get => _navigation.SelectedWorkbenchEntryIndex;
            private set => _navigation.SelectedWorkbenchEntryIndex = value;
        }
        internal int WorkbenchScrollOffset
        {
            get => _navigation.WorkbenchScrollOffset;
            private set => _navigation.WorkbenchScrollOffset = Math.Max(0, value);
        }
        internal int SelectedCompiledStepIndex
        {
            get => _navigation.SelectedCompiledStepIndex;
            private set => _navigation.SelectedCompiledStepIndex = value;
        }
        internal CharacterTestRuntimeAiLabLogicElementKind SelectedLogicElementKind
        {
            get => _navigation.SelectedLogicElementKind;
            private set => _navigation.SelectedLogicElementKind = value;
        }
        internal int SelectedLogicElementIndex
        {
            get => _navigation.SelectedLogicElementIndex;
            private set => _navigation.SelectedLogicElementIndex = value;
        }
        internal bool IsSelectedCardExpanded =>
            WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
        internal bool HasReplayReturnToken => _replayReturnToken.IsValid;
        private CharacterTestRuntimeAiLabWorkspaceMode _workspaceBeforeDiagnostics
        {
            get => _navigation.WorkspaceBeforeDiagnostics;
            set => _navigation.WorkspaceBeforeDiagnostics = value;
        }
        private bool _historicalPresentationSelectionCaptured
        {
            get => _navigation.HistoricalPresentationSelectionCaptured;
            set => _navigation.HistoricalPresentationSelectionCaptured = value;
        }
        private int _latestSelectedWorkbenchEntryIndex
        {
            get => _navigation.LatestSelectedWorkbenchEntryIndex;
            set => _navigation.LatestSelectedWorkbenchEntryIndex = value;
        }
        private int _latestSelectedCompiledStepIndex
        {
            get => _navigation.LatestSelectedCompiledStepIndex;
            set => _navigation.LatestSelectedCompiledStepIndex = value;
        }
        private CharacterTestRuntimeAiLabLogicElementKind _latestSelectedLogicElementKind
        {
            get => _navigation.LatestSelectedLogicElementKind;
            set => _navigation.LatestSelectedLogicElementKind = value;
        }
        private int _latestSelectedLogicElementIndex
        {
            get => _navigation.LatestSelectedLogicElementIndex;
            set => _navigation.LatestSelectedLogicElementIndex = value;
        }
        private CharacterTestRuntimeAiLabReplayReturnToken _replayReturnToken
        {
            get => _navigation.ReplayReturnToken;
            set => _navigation.ReplayReturnToken = value;
        }
        private bool _followCurrentRuntimeBehavior
        {
            get => _navigation.FollowCurrentRuntimeBehavior;
            set => _navigation.FollowCurrentRuntimeBehavior = value;
        }
        private bool _openSelectedBehaviorStageAfterRefresh;

    }
}
