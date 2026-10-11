using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler
    {
        private readonly CharacterTestRuntimeAiLabStageSelectionState _stageSelection =
            new CharacterTestRuntimeAiLabStageSelectionState();
        private readonly CharacterTestRuntimeAiLabStageGestureSession _stageGesture =
            new CharacterTestRuntimeAiLabStageGestureSession();
        private readonly CharacterTestRuntimeAiLabStageFieldEditSession _stageFieldEdit =
            new CharacterTestRuntimeAiLabStageFieldEditSession();
        private CharacterTestRuntimeAiLabStageEditIdentity _stageMarqueeIdentity;
        private long _stageMarqueeEpoch;
        private bool _stageMarqueeActive;
        private string[] _stageSelectionBuffer = Array.Empty<string>();
        private int _stageEntryIndex = -1;
        private CharacterTestRuntimeAiLabStageEditIdentity _stageHistoryRouteIdentity;
        private bool _stageSnapEnabled = true;

        internal int SelectedStageCount => _stageSelection.Count;
        internal string PrimaryStageId => _stageSelection.PrimaryStageId;
        internal string ExpandedStageId => _stageSelection.ExpandedStageId;
        internal bool StageSnapEnabled => _stageSnapEnabled;
        internal bool CanUndoStageEdit => _controller?.DraftWorkspace?.CanUndo ?? false;
        internal bool CanRedoStageEdit => _controller?.DraftWorkspace?.CanRedo ?? false;
        internal bool HasActiveStageGesture => _stageGesture.Active;
        internal bool HasActiveStageFieldEdit => _stageFieldEdit.Active;
        internal bool HasActiveStageMarquee => _stageMarqueeActive;
        internal bool HasActiveStageEditSession =>
            _stageGesture.Active || _stageFieldEdit.Active || _stageMarqueeActive;
        internal long StagePresentationRevision => unchecked(
            (_stageSelection.Revision << 1) ^ (_stageSnapEnabled ? 1L : 0L));

        internal bool IsStageSelected(string stageId) => _stageSelection.IsSelected(stageId);

        internal void ReconcileStageEditing(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            int selectedEntryIndex = _selectedEntryIndex?.Invoke() ?? -1;
            int entryIndex = -1;
            CharacterTestRuntimeAiLabStageEditIdentity identity = default;
            CharacterTestRuntimeAiLabStageEditIdentity selectedIdentity = default;
            bool selectedResolved = _controller != null
                && timeline != null
                && selectedEntryIndex >= 0
                && _controller.TryResolveStageEditIdentity(
                    selectedEntryIndex,
                    out selectedIdentity,
                    out _);
            CharacterTestRuntimeAiLabShellState shell = _controller?.ShellCoordinator?.CurrentState;
            CharacterTestRuntimeAiLabPageContext page = shell?.PageContext;
            bool hasPageRoute = shell != null
                && shell.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                && page != null
                && page.GroupDraftId.Length > 0
                && page.EntryId.Length > 0
                && page.CardDraftId.Length > 0;
            bool selectedMatchesPage = hasPageRoute
                && selectedResolved
                && string.Equals(selectedIdentity.GroupDraftId, page.GroupDraftId, StringComparison.Ordinal)
                && string.Equals(selectedIdentity.EntryId, page.EntryId, StringComparison.Ordinal)
                && string.Equals(selectedIdentity.CardDraftId, page.CardDraftId, StringComparison.Ordinal);
            bool retainedMatchesPage = hasPageRoute
                && _stageHistoryRouteIdentity.IsValid
                && string.Equals(
                    _stageHistoryRouteIdentity.GroupDraftId,
                    page.GroupDraftId,
                    StringComparison.Ordinal)
                && string.Equals(
                    _stageHistoryRouteIdentity.EntryId,
                    page.EntryId,
                    StringComparison.Ordinal)
                && string.Equals(
                    _stageHistoryRouteIdentity.CardDraftId,
                    page.CardDraftId,
                    StringComparison.Ordinal);

            bool resolved = false;
            if (_controller != null
                && timeline != null
                && !string.IsNullOrWhiteSpace(timeline.BehaviorEntryId))
            {
                resolved = _controller.TryResolveStageEditIdentityByEntryId(
                    timeline.BehaviorEntryId,
                    out entryIndex,
                    out identity,
                    out _);
            }
            if (!resolved && selectedMatchesPage)
            {
                identity = selectedIdentity;
                entryIndex = selectedEntryIndex;
                resolved = true;
            }
            else if ((!hasPageRoute || retainedMatchesPage)
                && _controller != null
                && timeline != null
                && _stageHistoryRouteIdentity.IsValid)
            {
                resolved = _controller.TryResolveStageEditIdentityByRoute(
                    _stageHistoryRouteIdentity,
                    out entryIndex,
                    out identity,
                    out _);
            }
            if (!resolved && !hasPageRoute && selectedResolved)
            {
                identity = selectedIdentity;
                entryIndex = selectedEntryIndex;
                resolved = true;
            }
            if (!resolved)
            {
                CancelStageEditSessions(clearSelection: true);
                _stageEntryIndex = -1;
                return;
            }

            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = timeline.Stages
                ?? Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
            int stageCount = Math.Max(0, Math.Min(timeline.StageCount, stages.Length));
            _stageSelection.Reconcile(identity, stages, stageCount);
            _stageEntryIndex = entryIndex;
            _stageHistoryRouteIdentity = identity;

            if (_stageSelection.Count == 0
                && timeline.SelectedElementKind == CharacterTestRuntimeAiLabLogicElementKind.Stage
                && timeline.SelectedElementIndex >= 0
                && timeline.SelectedElementIndex < stageCount)
            {
                string selectedId = stages[timeline.SelectedElementIndex]?.StageId ?? string.Empty;
                _stageSelection.Select(
                    selectedId,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None,
                    stages,
                    stageCount);
            }
        }

        internal bool SelectStage(
            string stageId,
            CharacterTestRuntimeAiLabStageSelectionModifier modifier)
        {
            CancelGraphFieldEdit(deactivating: false);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CurrentTimeline();
            ReconcileStageEditing(timeline);
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = timeline?.Stages
                ?? Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
            int count = Math.Max(0, Math.Min(timeline?.StageCount ?? 0, stages.Length));
            if (!_stageSelection.Select(stageId, modifier, stages, count))
            {
                LastMessage = "所选 Stage 已失效。";
                return false;
            }

            int index = FindStageIndex(stages, count, _stageSelection.PrimaryStageId);
            if (index >= 0)
                _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Stage, index);
            PublishStageContext(_stageSelection.PrimaryStageId, null);
            LastMessage = _stageSelection.Count > 1
                ? "已选择 " + _stageSelection.Count + " 个 Stage。"
                : "已选择 Stage。";
            _refresh?.Invoke();
            return true;
        }

        internal bool ToggleStageExpanded(string stageId)
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CurrentTimeline();
            ReconcileStageEditing(timeline);
            if (!_stageSelection.ToggleExpanded(stageId))
            {
                LastMessage = "请先选择要展开的 Stage。";
                return false;
            }
            PublishStageContext(stageId, null);
            LastMessage = string.Equals(_stageSelection.ExpandedStageId, stageId, StringComparison.Ordinal)
                ? "已展开 Stage 原地编辑器。"
                : "已收起 Stage 原地编辑器。";
            _refresh?.Invoke();
            return true;
        }

        internal bool OpenStageEditor(int stageIndex)
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CurrentTimeline();
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = timeline?.Stages
                ?? Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
            int count = Math.Max(0, Math.Min(timeline?.StageCount ?? 0, stages.Length));
            if (stageIndex < 0 || stageIndex >= count || stages[stageIndex] == null)
                return false;

            string stageId = stages[stageIndex].StageId ?? string.Empty;
            if (!SelectStage(stageId, CharacterTestRuntimeAiLabStageSelectionModifier.None))
                return false;
            if (string.Equals(_stageSelection.ExpandedStageId, stageId, StringComparison.Ordinal))
                return true;
            return ToggleStageExpanded(stageId);
        }

        internal void ClearStageSelection()
        {
            CancelStageEditSessions(clearSelection: true);
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.None, -1);
            PublishStageContext(string.Empty, null);
            LastMessage = "已清空 Stage 选择。";
            _refresh?.Invoke();
        }

        internal bool BeginStageMarquee(out long epoch)
        {
            epoch = 0L;
            string diagnostic = null;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CurrentTimeline();
            ReconcileStageEditing(timeline);
            if (timeline == null || (!timeline.CanEdit && !timeline.Historical)
                || _controller == null
                || _stageEntryIndex < 0
                || !_controller.TryResolveStageEditIdentity(
                    _stageEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity identity,
                    out diagnostic))
            {
                LastMessage = diagnostic ?? "当前状态不允许开始 Stage 框选。";
                return false;
            }
            CancelStageMarqueeCore();
            _stageMarqueeIdentity = identity;
            _stageMarqueeActive = true;
            AdvanceStageMarqueeEpoch();
            epoch = _stageMarqueeEpoch;
            LastMessage = "拖动以框选 Stage。";
            return true;
        }

        internal bool CommitStageMarquee(
            long epoch,
            string[] stageIds,
            int selectedCount,
            CharacterTestRuntimeAiLabStageSelectionModifier modifier,
            int viewportFrameDelta)
        {
            string diagnostic = null;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CurrentTimeline();
            if (!_stageMarqueeActive
                || epoch != _stageMarqueeEpoch
                || _controller == null
                || _stageEntryIndex < 0
                || !_controller.TryResolveStageEditIdentity(
                    _stageEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity identity,
                    out diagnostic)
                || !_stageMarqueeIdentity.Matches(identity))
            {
                CancelStageMarqueeCore();
                LastMessage = string.IsNullOrEmpty(diagnostic)
                    ? "Stage 框选事件已经过期，选择未改变。"
                    : diagnostic;
                _refresh?.Invoke();
                return false;
            }

            _stageMarqueeActive = false;
            _stageMarqueeIdentity = default;
            AdvanceStageMarqueeEpoch();
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = timeline?.Stages
                ?? Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
            int count = Math.Max(0, Math.Min(timeline?.StageCount ?? 0, stages.Length));
            if (!_stageSelection.ReplaceFromStableIds(
                    stages,
                    count,
                    stageIds,
                    selectedCount,
                    modifier))
            {
                LastMessage = "Stage 框选包含失效 stable ID，选择未改变。";
                _refresh?.Invoke();
                return false;
            }
            if (viewportFrameDelta != 0)
                TryShiftTimelineViewportByFrames(viewportFrameDelta);
            int index = FindStageIndex(stages, count, _stageSelection.PrimaryStageId);
            _setSelection?.Invoke(
                index >= 0
                    ? CharacterTestRuntimeAiLabLogicElementKind.Stage
                    : CharacterTestRuntimeAiLabLogicElementKind.None,
                index);
            PublishStageContext(_stageSelection.PrimaryStageId, null);
            LastMessage = _stageSelection.Count > 0
                ? "框选了 " + _stageSelection.Count + " 个 Stage。"
                : "框选未命中 Stage。";
            _refresh?.Invoke();
            return true;
        }

        internal void CancelStageMarquee(long epoch)
        {
            if (!_stageMarqueeActive || epoch != _stageMarqueeEpoch)
                return;
            CancelStageMarqueeCore();
        }

        private void CancelStageMarqueeCore()
        {
            if (_stageMarqueeActive)
                AdvanceStageMarqueeEpoch();
            _stageMarqueeActive = false;
            _stageMarqueeIdentity = default;
        }

        private void AdvanceStageMarqueeEpoch()
        {
            _stageMarqueeEpoch = _stageMarqueeEpoch == long.MaxValue
                ? 1L
                : _stageMarqueeEpoch + 1L;
        }

        internal bool BeginStageGesture(
            CharacterTestRuntimeAiLabStageGestureKind kind,
            string primaryStageId,
            float pointerX,
            float pixelsPerFrame,
            CharacterTestRuntimeAiLabStageSelectionModifier modifier,
            out long epoch)
        {
            epoch = 0L;
            if (RejectHistoricalAuthoring())
                return false;
            CancelGraphFieldEdit(deactivating: false);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CurrentTimeline();
            ReconcileStageEditing(timeline);
            if (timeline == null || !timeline.CanEdit)
            {
                LastMessage = "当前状态不允许编辑 Stage。";
                return false;
            }

            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = timeline.Stages
                ?? Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
            int count = Math.Max(0, Math.Min(timeline.StageCount, stages.Length));
            if (!_stageSelection.IsSelected(primaryStageId)
                && !_stageSelection.Select(primaryStageId, modifier, stages, count))
            {
                LastMessage = "待编辑 Stage 已失效。";
                return false;
            }

            int primaryIndex = FindStageIndex(stages, count, primaryStageId);
            if (primaryIndex < 0)
                return false;
            if (kind == CharacterTestRuntimeAiLabStageGestureKind.Resize
                && stages[primaryIndex].DurationMode != CharacterTestRuntimeAiLabLogicDurationMode.Fixed)
            {
                LastMessage = "只有 Fixed Stage 可以拖动持续时间。";
                return false;
            }
            if (!_controller.TryResolveStageEditIdentity(
                    _stageEntryIndex,
                    out var identity,
                    out string identityDiagnostic))
            {
                LastMessage = identityDiagnostic;
                return false;
            }
            if (!_stageGesture.Begin(
                    kind,
                    identity,
                    _stageSelection,
                    stages,
                    count,
                    primaryStageId,
                    pointerX,
                    pixelsPerFrame,
                    _stageSnapEnabled))
            {
                LastMessage = "无法开始 Stage 编辑手势。";
                return false;
            }
            epoch = _stageGesture.Epoch;
            LastMessage = kind == CharacterTestRuntimeAiLabStageGestureKind.Resize
                ? "拖动以调整 Fixed duration。"
                : "拖动以移动所选 Stage。";
            return true;
        }

        internal CharacterTestRuntimeAiLabStageGesturePreview UpdateStageGesture(
            long epoch,
            float pointerX,
            bool invertSnap)
        {
            if (_controller == null
                || _stageEntryIndex < 0
                || !_controller.TryResolveStageEditIdentity(_stageEntryIndex, out var identity, out _))
            {
                _stageGesture.Cancel();
                return default;
            }
            return _stageGesture.Update(identity, epoch, pointerX, invertSnap);
        }

        internal bool CommitStageGesture(long epoch, int viewportFrameDelta = 0)
        {
            if (RejectHistoricalAuthoring())
            {
                _stageGesture.Cancel();
                return false;
            }
            string diagnostic = null;
            if (_controller == null
                || _stageEntryIndex < 0
                || !_controller.TryResolveStageEditIdentity(
                    _stageEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity identity,
                    out diagnostic))
            {
                _stageGesture.Cancel();
                LastMessage = diagnostic ?? "Stage 编辑身份已失效。";
                _refresh?.Invoke();
                return false;
            }

            int selectedCount = _stageGesture.StageCount;
            EnsureStageSelectionBuffer(selectedCount);
            for (int i = 0; i < selectedCount; i++)
                _stageSelectionBuffer[i] = _stageGesture.GetStageId(i);
            string primaryStageId = _stageSelection.PrimaryStageId;
            if (!_stageGesture.TryFinish(identity, epoch, out var kind, out int frameDelta))
            {
                LastMessage = "Stage 编辑事件已过期，草稿未改变。";
                _refresh?.Invoke();
                return false;
            }
            if (frameDelta == 0)
            {
                if (viewportFrameDelta != 0)
                    TryShiftTimelineViewportByFrames(viewportFrameDelta);
                LastMessage = "Stage 时间未改变。";
                _refresh?.Invoke();
                return true;
            }

            bool changed;
            string resultingCardDraftId;
            if (kind == CharacterTestRuntimeAiLabStageGestureKind.Resize)
            {
                changed = _controller.TryResizeLogicStage(
                    _stageEntryIndex,
                    identity,
                    primaryStageId,
                    frameDelta,
                    out resultingCardDraftId,
                    out diagnostic);
            }
            else
            {
                changed = _controller.TryMoveLogicStages(
                    _stageEntryIndex,
                    identity,
                    _stageSelectionBuffer,
                    selectedCount,
                    frameDelta,
                    out resultingCardDraftId,
                    out diagnostic);
            }
            ClearStageSelectionBuffer(selectedCount);
            LastMessage = diagnostic;
            if (!changed)
            {
                _refresh?.Invoke();
                return false;
            }
            PublishStageContext(primaryStageId, resultingCardDraftId);
            if (viewportFrameDelta != 0)
                TryShiftTimelineViewportByFrames(viewportFrameDelta);
            _refresh?.Invoke();
            return true;
        }

        internal void CancelStageGesture()
        {
            _stageGesture.Cancel();
            LastMessage = "Stage 编辑已取消。";
            _refresh?.Invoke();
        }

        internal bool TryEditStageField(
            string stageId,
            CharacterTestRuntimeAiLabStageFieldKind field,
            in CharacterTestRuntimeAiLabStageFieldValue value)
        {
            if (RejectHistoricalAuthoring())
                return false;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CurrentTimeline();
            ReconcileStageEditing(timeline);
            string diagnostic = null;
            if (_controller == null
                || _stageEntryIndex < 0
                || !_controller.TryResolveStageEditIdentity(_stageEntryIndex, out var identity, out diagnostic))
            {
                LastMessage = diagnostic ?? "Stage 编辑身份不可用。";
                return false;
            }
            if (!_controller.TryEditLogicStageField(
                    _stageEntryIndex,
                    identity,
                    stageId,
                    field,
                    value,
                    out string resultingCardDraftId,
                    out diagnostic))
            {
                LastMessage = diagnostic;
                _refresh?.Invoke();
                return false;
            }
            LastMessage = diagnostic;
            PublishStageContext(stageId, resultingCardDraftId);
            _refresh?.Invoke();
            return true;
        }

        internal bool BeginStageFieldEdit(
            string stageId,
            CharacterTestRuntimeAiLabStageFieldKind field,
            out long epoch)
        {
            epoch = 0L;
            if (RejectHistoricalAuthoring())
                return false;
            CancelGraphFieldEdit(deactivating: false);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CurrentTimeline();
            ReconcileStageEditing(timeline);
            if (timeline == null || !timeline.CanEdit)
            {
                LastMessage = "当前状态不允许编辑 Stage 字段。";
                return false;
            }
            string diagnostic = null;
            if (_controller == null
                || _stageEntryIndex < 0
                || !_controller.TryResolveStageEditIdentity(
                    _stageEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity identity,
                    out diagnostic))
            {
                LastMessage = diagnostic ?? "Stage 字段身份不可用。";
                return false;
            }
            if (!_stageFieldEdit.Begin(identity, stageId, field, out epoch))
            {
                LastMessage = "无法开始 Stage 字段编辑。";
                return false;
            }
            return true;
        }

        internal bool CommitStageFieldEdit(
            long epoch,
            string stageId,
            CharacterTestRuntimeAiLabStageFieldKind field,
            in CharacterTestRuntimeAiLabStageFieldValue value)
        {
            if (RejectHistoricalAuthoring())
            {
                _stageFieldEdit.Cancel(deactivating: false);
                return false;
            }
            string diagnostic = null;
            if (_controller == null
                || _stageEntryIndex < 0
                || !_controller.TryResolveStageEditIdentity(
                    _stageEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity identity,
                    out diagnostic)
                || !_stageFieldEdit.CanCommit(identity, epoch, stageId, field))
            {
                _stageFieldEdit.Cancel(deactivating: false);
                LastMessage = diagnostic ?? "Stage 字段编辑事件已经过期，草稿未改变。";
                _refresh?.Invoke();
                return false;
            }
            if (!_controller.TryEditLogicStageField(
                    _stageEntryIndex,
                    identity,
                    stageId,
                    field,
                    value,
                    out string resultingCardDraftId,
                    out diagnostic))
            {
                LastMessage = diagnostic;
                return false;
            }
            _stageFieldEdit.Complete(epoch);
            LastMessage = diagnostic;
            PublishStageContext(stageId, resultingCardDraftId);
            _refresh?.Invoke();
            return true;
        }

        internal void CancelStageFieldEdit(bool deactivating)
        {
            _stageFieldEdit.Cancel(deactivating);
        }

    }
}
