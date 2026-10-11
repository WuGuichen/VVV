using System;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        internal bool TacticalCardTestMode { get; private set; }
        internal bool TacticalCardTestInFlight { get; private set; }
        internal string PendingTacticalTestCardId { get; private set; }
        internal string RunningTacticalTestCardName { get; private set; }
        private int _tacticalTestTeam, _tacticalTestSlot, _tacticalTestProfile;
        private bool _tacticalTestObservedExecution;
        private CharacterTestBehaviorExecutionEndReason _tacticalTestEndReason;
        private static string TacticalEndLabel(CharacterTestBehaviorExecutionEndReason reason) => reason switch {
            CharacterTestBehaviorExecutionEndReason.ConditionRejected => "动作条件不满足",
            CharacterTestBehaviorExecutionEndReason.TargetMissing => "目标已失效",
            CharacterTestBehaviorExecutionEndReason.TimedOut => "执行超时",
            CharacterTestBehaviorExecutionEndReason.ActionRejected => "动作提交失败",
            CharacterTestBehaviorExecutionEndReason.CleanupTimedOut => "清理超时，请重新开始测试",
            CharacterTestBehaviorExecutionEndReason.CleanupFailed => "清理失败，请重新开始测试",
            _ => "执行完成" };

        internal bool CanChooseTacticalTestCard => !TacticalCardTestInFlight && _tacticalTestEndReason < CharacterTestBehaviorExecutionEndReason.CleanupTimedOut
            && !TestCurrentChangesQueued && !TestCurrentChangesInProgress && !_candidateApplyQueued;
        internal string TacticalCardTestPlaybackLabel => TacticalCardTestInFlight
            ? _context?.Paused == true ? "继续" : "暂停" : "出牌并播放";
        internal string TacticalCardTestStatus => SessionCompleted ? "已达测试帧上限 · 请重新开始" : TacticalCardTestInFlight
            ? (_context?.Paused == true ? "已暂停：" : _tacticalTestObservedExecution ? "正在执行：" : "等待启用条件：")
                + RunningTacticalTestCardName
            : _tacticalTestEndReason > CharacterTestBehaviorExecutionEndReason.Completed ? TacticalEndLabel(_tacticalTestEndReason) + " · 已暂停"
            : string.IsNullOrEmpty(PendingTacticalTestCardId) ? "本次出牌已结束 · 请选择下一张"
                : "待出牌：" + (DraftWorkspace?.Cards.TryGetValue(PendingTacticalTestCardId, out var card) == true ? card.DisplayName : "");

        internal bool BeginTacticalCardTest(out string key, out string issue)
        {
            key = null; issue = string.Empty;
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            { issue = "请先返回实验设置。"; return false; }
            TestCardLibraryMode = true; TestCardTemplatePicking = false; TestCardLibraryFilter = "all";
            var cards = GetTacticalLibraryEntries();
            string last = UnityEngine.PlayerPrefs.GetString("RuntimeAiLab.TestCard.Last." + TestCardRole, "");
            var selected = IsConfiguredTestCardEditing ? cards.FirstOrDefault(c => c.Key == "test:" + ConfiguredTestCardId) : null;
            selected ??= cards.FirstOrDefault(c => c.Key == last) ?? cards.FirstOrDefault();
            TacticalCardTestMode = true;
            TacticalCardTestInFlight = false;
            _tacticalTestEndReason = CharacterTestBehaviorExecutionEndReason.None;
            _tacticalTestTeam = SelectedSetupTeamId; _tacticalTestSlot = SelectedSetupSlotIndex;
            PendingTacticalTestCardId = null; RunningTacticalTestCardName = null;
            key = selected?.Key;
            SetPaused(true);
            RefreshTacticalPreview();
            return true;
        }

        internal bool CanSelectTacticalTestCard(out string issue)
        {
            issue = string.Empty;
            if (!TacticalCardTestMode) return true;
            if (!CanChooseTacticalTestCard)
            { issue = _tacticalTestEndReason >= CharacterTestBehaviorExecutionEndReason.CleanupTimedOut
                ? TacticalEndLabel(_tacticalTestEndReason) : "当前卡尚未结束，请继续执行；结束后可以选择下一张。"; return false; }
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup
                && (_context?.Paused != true || IsHistoricalFrameSelected))
            { issue = "请在暂停的最新帧选择下一张卡。"; return false; }
            if (SelectedSetupTeamId != _tacticalTestTeam || SelectedSetupSlotIndex != _tacticalTestSlot)
            { issue = "测试角色已变化，请返回实验设置重新进入战术卡测试。"; return false; }
            return true;
        }

        internal void SelectTacticalTestCard()
        {
            if (TacticalCardTestMode && HasStandaloneTacticalCard)
            {
                PendingTacticalTestCardId = TacticalCardContext.CardId;
                _statusMessage = TacticalCardTestStatus;
                RefreshTacticalPreview();
            }
        }

        internal void ClearPendingTacticalTestCard()
        {
            if (TacticalCardTestMode && !TacticalCardTestInFlight) PendingTacticalTestCardId = null;
        }

        internal void EndTacticalCardTest()
        {
            TacticalCardTestMode = false; TacticalCardTestInFlight = false;
            PendingTacticalTestCardId = null; RunningTacticalTestCardName = null;
            _tacticalTestObservedExecution = false;
            RefreshTacticalPreview();
        }

        internal bool ToggleTacticalCardTestPlayback(out string issue)
        {
            bool result = TacticalCardTestInFlight ? TogglePause() : QueueTestCurrentChanges(0, out _, playAfterStart: true);
            issue = _statusMessage;
            RefreshTacticalPreview();
            return result;
        }

        private bool ValidateTacticalCardPlay(out string issue)
        {
            issue = string.Empty;
            if (!IsConfiguredTestCardEditing)
            { issue = "请选择配置好的测试单卡，或先从基础卡创建。"; return false; }
            if (SessionCompleted)
            { issue = "已达到本次测试帧上限，请重新开始。"; return false; }
            if (TacticalCardTestInFlight)
            { issue = "当前卡正在执行；请使用暂停或继续。"; return false; }
            if (!HasStandaloneTacticalCard || PendingTacticalTestCardId != TacticalCardContext.CardId)
            { issue = "请先选择下一张战术卡；可以再次选择同一张。"; return false; }
            if (!ConfiguredCardCanPlay(out issue)) return false;
            return CanSelectTacticalTestCard(out issue);
        }

        private void MarkTacticalCardPlayed(string cardId)
        {
            _tacticalTestEndReason = CharacterTestBehaviorExecutionEndReason.None;
            TacticalCardTestInFlight = true; _tacticalTestObservedExecution = false;
            PendingTacticalTestCardId = null;
            RunningTacticalTestCardName = DraftWorkspace.Cards.TryGetValue(cardId, out var card) ? card.DisplayName : cardId;
            _sessionBuilder.TryResolveArenaRuntimeAiPlannerProfileSlot(_tacticalTestTeam, _tacticalTestSlot, out _tacticalTestProfile);
            RefreshTacticalPreview();
        }

        private bool PlayNextTacticalCard(in CharacterTestRuntimeAiLabTestCurrentChangesRequest request, out string issue)
        {
            issue = string.Empty;
            if (!ValidateTacticalCardPlay(out issue)) return false;
            var previous = _singleCardTestRequest;
            try
            {
                _singleCardTestRequest = request;
                if (!FlushDraftWorkspace(out issue)) return false;
                BuildCandidateRevision();
                if (_candidateRevision == null || _candidateState != CharacterTestRuntimeAiLabCandidateState.PreviewComplete)
                { issue = _candidateDiagnostic; return false; }
                int frame = checked((int)_context.Slice.CurrentFrame.Value + 1);
                if (!TryQueueCandidateApply(_candidateRevision, frame, out issue)) return false;
                // Install while paused: unpausing first would let FixedUpdate reselect the old card
                // before the ordinary Update queue drains. The journal still records F+1.
                ProcessPendingCandidateApply();
                if (_candidateState != CharacterTestRuntimeAiLabCandidateState.Applied)
                { issue = _candidateDiagnostic; return false; }
                MarkTacticalCardPlayed(request.CardDraftId);
                if (!TogglePause())
                {
                    CancelQueuedApply(CharacterTestRuntimeAiLabQueuedApplyCancelReason.TargetUnavailable, _statusMessage);
                    TacticalCardTestInFlight = false; PendingTacticalTestCardId = request.CardDraftId;
                    RefreshTacticalPreview();
                    issue = _statusMessage; return false;
                }
                issue = "已出牌：" + RunningTacticalTestCardName;
                return true;
            }
            finally { _singleCardTestRequest = previous; }
        }

        private void ObserveTacticalCardTestFrame()
        {
            if (!TacticalCardTestMode || !TacticalCardTestInFlight || _seekInProgress || _loopPlaybackActive) return;
            var module = _context?.Slice?.Services.Modules.RuntimeAiPlannerCommands;
            bool active = false, ended = false;
            module?.GetBehaviorCardExecutionProgress(_tacticalTestProfile, out active, out ended);
            if (active && !_tacticalTestObservedExecution) RefreshTacticalPreview();
            _tacticalTestObservedExecution |= active;
            if (!ended && _candidateState != CharacterTestRuntimeAiLabCandidateState.ApplyFailed
                && _phase != CharacterTestRuntimeAiPlannerDevScenePhase.Completed) return;
            TacticalCardTestInFlight = false;
            if (ended) _tacticalTestEndReason = module.GetBehaviorCardExecutionEndReason(_tacticalTestProfile);
            if (ended) PendingTacticalTestCardId = TacticalCardContext?.CardId;
            SetPaused(true);
            _timeline?.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Paused);
            _statusMessage = ended ? TacticalEndLabel(_tacticalTestEndReason) + "；本次出牌已结束并暂停。"
                : _phase == CharacterTestRuntimeAiPlannerDevScenePhase.Completed ? "已达到本次实验帧上限，请重新开始。" : _candidateDiagnostic;
            // The retained editor skips full binding on runtime-only frame changes.
            // Its command buttons must be rebound when this execution ends.
            RefreshTacticalPreview();
        }

        private void StopFailedTacticalCardApply()
        {
            if (!TacticalCardTestMode || !TacticalCardTestInFlight
                || _candidateState != CharacterTestRuntimeAiLabCandidateState.ApplyFailed) return;
            TacticalCardTestInFlight = false; PendingTacticalTestCardId = TacticalCardContext?.CardId;
            SetPaused(true);
            _timeline?.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Paused);
            _statusMessage = "出牌失败：" + _candidateDiagnostic;
            RefreshTacticalPreview();
        }

        private void ResetTacticalCardTestPlayback()
        {
            _tacticalTestEndReason = CharacterTestBehaviorExecutionEndReason.None;
            if (!TacticalCardTestMode) return;
            TacticalCardTestInFlight = false; _tacticalTestObservedExecution = false;
            RunningTacticalTestCardName = null;
            PendingTacticalTestCardId = TacticalCardContext?.CardId;
            _selectedSetupTeamId = _tacticalTestTeam; _selectedSetupSlotIndex = _tacticalTestSlot;
            RefreshTacticalPreview();
        }
    }
}
