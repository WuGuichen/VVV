using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler
    {
        internal void DuplicateSelectedStages()
        {
            if (RejectHistoricalAuthoring())
                return;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CurrentTimeline();
            ReconcileStageEditing(timeline);
            if (!TryPrepareStageBatch(out var identity, out int count))
                return;
            if (!_controller.TryDuplicateLogicStages(
                    _stageEntryIndex,
                    identity,
                    _stageSelectionBuffer,
                    count,
                    out string[] createdIds,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                ClearStageSelectionBuffer(count);
                LastMessage = diagnostic;
                _refresh?.Invoke();
                return;
            }
            ClearStageSelectionBuffer(count);
            string focus = createdIds.Length > 0 ? createdIds[0] : string.Empty;
            LastMessage = diagnostic;
            PublishStageContext(focus, resultingCardDraftId);
            _refresh?.Invoke();
            SelectCreatedStages(createdIds);
        }

        internal void DeleteSelectedStages()
        {
            if (RejectHistoricalAuthoring())
                return;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CurrentTimeline();
            ReconcileStageEditing(timeline);
            if (!TryPrepareStageBatch(out var identity, out int count))
                return;
            if (!_controller.TryDeleteLogicStages(
                    _stageEntryIndex,
                    identity,
                    _stageSelectionBuffer,
                    count,
                    out string focusStageId,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                ClearStageSelectionBuffer(count);
                LastMessage = diagnostic;
                _refresh?.Invoke();
                return;
            }
            ClearStageSelectionBuffer(count);
            _stageSelection.Clear();
            LastMessage = diagnostic;
            PublishStageContext(focusStageId, resultingCardDraftId);
            _refresh?.Invoke();
        }

        internal void UndoStageEdit()
        {
            var tactical = _workspaceView?.Invoke()?.Timeline?.TacticalPreview?.Editor;
            if (tactical != null) { tactical.Undo(false); _refresh?.Invoke(); return; }
            if (!RejectHistoricalAuthoring())
                ExecuteStageHistory(redo: false);
        }

        internal void RedoStageEdit()
        {
            var tactical = _workspaceView?.Invoke()?.Timeline?.TacticalPreview?.Editor;
            if (tactical != null) { tactical.Undo(true); _refresh?.Invoke(); return; }
            if (!RejectHistoricalAuthoring())
                ExecuteStageHistory(redo: true);
        }

        internal void ToggleStageSnap()
        {
            if (RejectHistoricalAuthoring())
                return;
            _stageSnapEnabled = !_stageSnapEnabled;
            LastMessage = _stageSnapEnabled ? "Stage 吸附：15f。" : "Stage 吸附：1f。";
            _refresh?.Invoke();
        }

        internal bool NudgeSelectedStages(int frameDelta)
        {
            if (RejectHistoricalAuthoring())
                return false;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CurrentTimeline();
            ReconcileStageEditing(timeline);
            if (frameDelta == 0 || !TryPrepareStageBatch(out var identity, out int count))
                return false;
            bool changed = _controller.TryMoveLogicStages(
                _stageEntryIndex,
                identity,
                _stageSelectionBuffer,
                count,
                frameDelta,
                out string resultingCardDraftId,
                out string diagnostic);
            ClearStageSelectionBuffer(count);
            LastMessage = diagnostic;
            if (changed)
                PublishStageContext(_stageSelection.PrimaryStageId, resultingCardDraftId);
            _refresh?.Invoke();
            return changed;
        }

        internal void CancelStageEditSessions(bool clearSelection)
        {
            _stageGesture.Cancel();
            _stageFieldEdit.Cancel(deactivating: true);
            CancelStageMarqueeCore();
            if (clearSelection)
            {
                _stageSelection.Clear();
                _stageHistoryRouteIdentity = default;
            }
        }

        internal bool SetSelectedStageKind(
            string stageId,
            CharacterTestRuntimeAiLabCardStepKind kind)
        {
            if (kind != CharacterTestRuntimeAiLabCardStepKind.UseAbility
                && kind != CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                && kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                && kind != CharacterTestRuntimeAiLabCardStepKind.Wait
                && kind != CharacterTestRuntimeAiLabCardStepKind.Logic)
            {
                LastMessage = "Stage 类型无效。";
                return false;
            }
            if (RejectHistoricalAuthoring())
                return false;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CurrentTimeline();
            ReconcileStageEditing(timeline);
            string diagnostic = null;
            if (_controller == null
                || _stageEntryIndex < 0
                || !_controller.TryResolveStageEditIdentity(
                    _stageEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity identity,
                    out diagnostic)
                || !_controller.TryEditLogicStageKind(
                    _stageEntryIndex,
                    identity,
                    stageId,
                    kind,
                    out string resultingCardDraftId,
                    out diagnostic))
            {
                LastMessage = diagnostic;
                return false;
            }
            LastMessage = diagnostic;
            PublishStageContext(stageId, resultingCardDraftId);
            _refresh?.Invoke();
            return true;
        }

        internal bool SetSelectedStageDurationMode(
            string stageId,
            CharacterTestRuntimeAiLabLogicDurationMode mode)
        {
            if (mode != CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                && mode != CharacterTestRuntimeAiLabLogicDurationMode.UntilEvent
                && mode != CharacterTestRuntimeAiLabLogicDurationMode.Infinite)
            {
                LastMessage = "Stage 持续模式无效。";
                return false;
            }
            return TryEditStageField(
                stageId,
                CharacterTestRuntimeAiLabStageFieldKind.DurationMode,
                new CharacterTestRuntimeAiLabStageFieldValue(durationMode: mode));
        }

        internal bool SetSelectedLegacyActionKind(
            string stageId,
            CharacterTestRuntimeAiPlannerActionKind actionKind)
        {
            CharacterTestRuntimeAiLabLogicStageViewModel stage = FindStage(CurrentTimeline(), stageId);
            if (stage == null || stage.Kind != CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly)
            {
                LastMessage = "当前 Stage 不是可迁移的旧动作类型。";
                return false;
            }
            if (actionKind != CharacterTestRuntimeAiPlannerActionKind.Wait
                && actionKind != CharacterTestRuntimeAiPlannerActionKind.MoveToTarget
                && actionKind != CharacterTestRuntimeAiPlannerActionKind.Strafe
                && actionKind != CharacterTestRuntimeAiPlannerActionKind.Retreat
                && actionKind != CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack
                && actionKind != CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot
                && actionKind != CharacterTestRuntimeAiPlannerActionKind.Dodge)
            {
                LastMessage = "Legacy Action 类型无效。";
                return false;
            }
            return TryEditStageField(
                stageId,
                CharacterTestRuntimeAiLabStageFieldKind.LegacyActionKind,
                new CharacterTestRuntimeAiLabStageFieldValue(legacyActionKind: actionKind));
        }

        internal bool SetSelectedMovementStrategy(
            string stageId,
            CharacterTestRuntimeAiLabMovementStrategy strategy)
        {
            CharacterTestRuntimeAiLabLogicStageViewModel stage = FindStage(CurrentTimeline(), stageId);
            if (stage == null || stage.Kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget)
            {
                LastMessage = "当前 Stage 不是移动策略。";
                return false;
            }
            if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabMovementStrategy), strategy))
            {
                LastMessage = "移动策略类型无效。";
                return false;
            }
            return TryEditStageField(
                stageId,
                CharacterTestRuntimeAiLabStageFieldKind.MovementStrategy,
                new CharacterTestRuntimeAiLabStageFieldValue(movementStrategy: strategy));
        }

        internal bool SetSelectedMovementTargetKind(
            string stageId,
            CharacterTestRuntimeAiLabMovementTargetKind targetKind)
        {
            CharacterTestRuntimeAiLabLogicStageViewModel stage = FindStage(CurrentTimeline(), stageId);
            if (stage == null || stage.Kind != CharacterTestRuntimeAiLabCardStepKind.MoveToTarget)
            {
                LastMessage = "Current stage is not a movement stage.";
                return false;
            }
            if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiLabMovementTargetKind), targetKind))
            {
                LastMessage = "Movement target kind is invalid.";
                return false;
            }
            return TryEditStageField(
                stageId,
                CharacterTestRuntimeAiLabStageFieldKind.MovementTargetKind,
                new CharacterTestRuntimeAiLabStageFieldValue(movementTargetKind: targetKind));
        }

        private void ExecuteStageHistory(bool redo)
        {
            if (HasActiveStageEditSession)
            {
                CancelStageEditSessions(clearSelection: false);
                LastMessage = "已取消当前 Stage 编辑预览；再次执行可进行历史操作。";
                _refresh?.Invoke();
                return;
            }
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CurrentTimeline();
            ReconcileStageEditing(timeline);
            string diagnostic = null;
            if (_controller == null
                || !_stageHistoryRouteIdentity.IsValid
                || !_controller.TryResolveStageEditIdentityByRoute(
                    _stageHistoryRouteIdentity,
                    out _stageEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity identity,
                    out diagnostic))
            {
                LastMessage = diagnostic ?? "Stage 历史身份不可用。";
                return;
            }
            CancelStageEditSessions(clearSelection: false);
            CharacterTestRuntimeAiLabStageEditIdentity resultingIdentity;
            int resultingEntryIndex;
            bool changed = redo
                ? _controller.TryRedoBehaviorCardEdit(
                    _stageEntryIndex,
                    identity,
                    out resultingEntryIndex,
                    out resultingIdentity,
                    out diagnostic)
                : _controller.TryUndoBehaviorCardEdit(
                    _stageEntryIndex,
                    identity,
                    out resultingEntryIndex,
                    out resultingIdentity,
                    out diagnostic);
            LastMessage = diagnostic;
            if (!changed)
            {
                _refresh?.Invoke();
                return;
            }
            if (!resultingIdentity.IsValid || resultingEntryIndex < 0)
            {
                _stageEntryIndex = -1;
                _stageHistoryRouteIdentity = default;
                _stageSelection.Clear();
                _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.None, -1);
                ReturnToBehaviorGroupAfterHistory(diagnostic);
                _refresh?.Invoke();
                return;
            }
            _stageEntryIndex = resultingEntryIndex;
            _stageHistoryRouteIdentity = resultingIdentity;
            PublishStageContext(_stageSelection.PrimaryStageId, resultingIdentity.CardDraftId);
            _refresh?.Invoke();
            ReconcileStageEditing(CurrentTimeline());
        }

        private void ReturnToBehaviorGroupAfterHistory(string diagnostic)
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
            CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
            if (state == null || state.ActiveWorkspace != CharacterTestRuntimeAiLabWorkspace.BehaviorCard)
            {
                LastMessage = diagnostic ?? "原行为卡条目已不存在。";
                return;
            }
            CharacterTestRuntimeAiLabWorkspace target =
                state.PageContext.BehaviorCardReturnWorkspace;
            if (target == CharacterTestRuntimeAiLabWorkspace.BehaviorGroup
                || target == CharacterTestRuntimeAiLabWorkspace.BehaviorCard)
            {
                target = CharacterTestRuntimeAiLabWorkspace.Setup;
            }
            CharacterTestRuntimeAiLabNavigationResult result = coordinator.Navigate(
                state.Generation,
                state.Revision,
                CharacterTestRuntimeAiLabWorkspace.BehaviorCard,
                target);
            LastMessage = result.Success
                ? (diagnostic ?? "原行为卡条目已不存在。") + " 已返回来源页面。"
                : (diagnostic ?? "原行为卡条目已不存在。") + " " + result.Message;
        }

        private bool TryPrepareStageBatch(
            out CharacterTestRuntimeAiLabStageEditIdentity identity,
            out int count)
        {
            identity = default;
            count = _stageSelection.Count;
            if (_controller == null || _stageEntryIndex < 0 || count <= 0)
            {
                LastMessage = "请先选择至少一个 Stage。";
                return false;
            }
            if (!_controller.TryResolveStageEditIdentity(_stageEntryIndex, out identity, out string diagnostic))
            {
                LastMessage = diagnostic;
                return false;
            }
            EnsureStageSelectionBuffer(count);
            for (int i = 0; i < count; i++)
                _stageSelectionBuffer[i] = _stageSelection.GetSelectedId(i);
            return true;
        }

        private void SelectCreatedStages(string[] stageIds)
        {
            if (stageIds == null || stageIds.Length == 0)
                return;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CurrentTimeline();
            ReconcileStageEditing(timeline);
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = timeline?.Stages
                ?? Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
            int count = Math.Max(0, Math.Min(timeline?.StageCount ?? 0, stages.Length));
            if (!_stageSelection.Select(
                    stageIds[0],
                    CharacterTestRuntimeAiLabStageSelectionModifier.None,
                    stages,
                    count))
                return;
            for (int i = 1; i < stageIds.Length; i++)
            {
                _stageSelection.Select(
                    stageIds[i],
                    CharacterTestRuntimeAiLabStageSelectionModifier.Toggle,
                    stages,
                    count);
            }
            int index = FindStageIndex(stages, count, _stageSelection.PrimaryStageId);
            if (index >= 0)
                _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Stage, index);
            _refresh?.Invoke();
        }

        private void PublishStageContext(string stageId, string resultingCardDraftId)
        {
            if (IsHistorical)
                return;
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
            CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
            if (!HostsBehaviorCardContext(state))
                return;
            CharacterTestRuntimeAiLabPageContext page = state.PageContext;
            coordinator.TryUpdateBehaviorCardContext(
                state.Generation,
                state.Revision,
                page.GroupDraftId,
                page.EntryId,
                string.IsNullOrWhiteSpace(resultingCardDraftId) ? page.CardDraftId : resultingCardDraftId,
                string.IsNullOrWhiteSpace(stageId)
                    ? CharacterTestRuntimeAiLabSelectedElementKind.None
                    : CharacterTestRuntimeAiLabSelectedElementKind.Stage,
                stageId ?? string.Empty,
                page.BehaviorCardViewport);
        }

        private CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel CurrentTimeline() =>
            _workspaceView?.Invoke()?.Timeline;

        private static CharacterTestRuntimeAiLabLogicStageViewModel FindStage(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            string stageId)
        {
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages = timeline?.Stages;
            int count = Math.Max(0, Math.Min(timeline?.StageCount ?? 0, stages?.Length ?? 0));
            int index = FindStageIndex(stages, count, stageId);
            return index >= 0 ? stages[index] : null;
        }

        private void EnsureStageSelectionBuffer(int count)
        {
            if (_stageSelectionBuffer.Length < count)
                Array.Resize(ref _stageSelectionBuffer, Math.Max(count, Math.Max(8, _stageSelectionBuffer.Length * 2)));
        }

        private void ClearStageSelectionBuffer(int count)
        {
            for (int i = 0; i < count && i < _stageSelectionBuffer.Length; i++)
                _stageSelectionBuffer[i] = string.Empty;
        }

        private bool TryShiftTimelineViewportByFrames(int frameDelta)
        {
            if (frameDelta == 0)
                return true;
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
            CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
            if (state == null
                || state.ActiveWorkspace != CharacterTestRuntimeAiLabWorkspace.BehaviorCard)
            {
                return false;
            }

            CharacterTestRuntimeAiLabPageContext page = state.PageContext;
            CharacterTestRuntimeAiLabBehaviorCardViewport current =
                _controller.BehaviorCardPresentationViewport;
            int span = Math.Max(1, current.HorizontalEndFrame - current.HorizontalStartFrame);
            int maximumStart = Math.Max(0, 36000 - Math.Min(36000, span));
            long requested = (long)current.HorizontalStartFrame + frameDelta;
            int start = requested <= 0L
                ? 0
                : requested >= maximumStart ? maximumStart : (int)requested;
            if (start == current.HorizontalStartFrame)
                return true;

            var next = new CharacterTestRuntimeAiLabBehaviorCardViewport(
                start,
                start + span,
                current.ZoomLevel,
                current.VerticalScrollOffset,
                current.FollowCurrent);
            if (_controller.HistoricalBehaviorPresentationActive)
            {
                _controller.UpdateHistoricalBehaviorCardViewport(next);
                return true;
            }
            return coordinator.TryUpdateBehaviorCardContext(
                state.Generation,
                state.Revision,
                page.GroupDraftId,
                page.EntryId,
                page.CardDraftId,
                page.SelectedElementKind,
                page.SelectedElementStableId,
                next).Success;
        }

        private static int FindStageIndex(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int count,
            string stageId)
        {
            for (int i = 0; i < count; i++)
            {
                if (string.Equals(stages[i]?.StageId, stageId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }
    }
}
