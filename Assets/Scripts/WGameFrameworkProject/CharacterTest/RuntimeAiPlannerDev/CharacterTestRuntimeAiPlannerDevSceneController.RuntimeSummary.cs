using System;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestRuntimeSummaryJob _runtimeSummary;
        private readonly CharacterTestCardSummaryPreparation _cardUiSummaries = new();
        internal long CardUiSummaryRevision => _cardUiSummaries.Revision;
        internal void CancelCardUiSummaries() => _cardUiSummaries.Dispose();
        private string _runtimeSummaryContext;
        private int _runtimeSummaryCharacter;
        private CharacterTestCharacterEquipmentComponent _runtimeSummaryEquipment;
        private PendingTacticalAutoSummaryPlay _pendingTacticalAutoSummaryPlay;
        private long _tacticalAutoSummarySerial;
        private string _tacticalAutoSummaryCardId;
        private bool _resumingTacticalAutoSummaryPlay;

        private sealed class PendingTacticalAutoSummaryPlay
        {
            internal string CardId, Role, BindingContext, WorkspaceId;
            internal int WorkspaceRevision, EntryIndex;
            internal bool PlayAfterStart;
            internal long AutomaticJobSerial;
            internal CharacterTestRuntimeAiLabWorkspace ShellWorkspace;
        }
        internal long RuntimeSummarySerial { get; private set; }
        internal bool RuntimeSummaryRunning => _runtimeSummary?.Running == true;
        internal string RuntimeSummaryStatus => _runtimeSummary?.Status ?? "";
        internal string RuntimeSummaryDetails => _runtimeSummary?.Details ?? "";

        private string RuntimeSummaryFailure(int actionId)
        {
            if (_runtimeSummary == null || _runtimeSummaryContext != TacticalPreviewBindingContextKey) return null;
            var equipment = CurrentSummaryEquipment(_runtimeSummaryCharacter);
            if (equipment.UseDefaultLoadout != _runtimeSummaryEquipment.UseDefaultLoadout
                || equipment.EquipmentStateStableId != _runtimeSummaryEquipment.EquipmentStateStableId) return null;
            return _runtimeSummary.Results.LastOrDefault(r => r.ActionId == actionId && r.Status == "失败")?.Diagnostic;
        }

        private CharacterTestCharacterEquipmentComponent CurrentSummaryEquipment(int character)
        {
            var runtime = _context?.Slice?.CharacterRuntime;
            CharacterTestCharacterEntity entity = null;
            if (runtime != null && _sessionBuilder != null
                && _sessionBuilder.TryResolveArenaRuntimeAiPlannerProfileSlot(_selectedSetupTeamId, _selectedSetupSlotIndex, out int profile))
                entity = runtime.Entities.FirstOrDefault(e => e.Control.RuntimeAiPlannerProfileSlot == profile && e.Identity.CharacterId == character);
            return (entity ?? CharacterTestTacticalPreparationCatalog.Role(character, runtime?.ActionRuntime).Source).Equipment;
        }

        internal void StartRuntimeSummary(int actionId, bool batch)
        {
            if (!TryStartRuntimeSummary(batch ? null : new[] { actionId }, out string issue))
                _statusMessage = issue;
        }

        private bool TryStartRuntimeSummary(int[] actionIds, out string issue)
        {
            issue = string.Empty;
            if (RuntimeSummaryRunning) { issue = "已有摘要重建正在进行。"; return false; }
            if (IsHistoricalFrameSelected || DraftWorkspace == null || DraftWorkspace.IsReadOnly)
            { issue = "当前编辑状态不能重建摘要。"; return false; }
            int character = _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0;
            if (character <= 0) { issue = "请先选择测试角色。"; return false; }
            try
            {
                var catalog = CharacterTestActionBrowserCatalog.Get(refresh: true);
                if (catalog.Issues.Length > 0) { issue = "动作目录存在问题：" + catalog.Issues[0]; return false; }
                var actions = actionIds == null ? catalog.Actions
                    : catalog.Actions.Where(a => actionIds.Contains(a.ActionId)).ToArray();
                if (actions.Length == 0 || actionIds != null && actions.Length != actionIds.Distinct().Count())
                { issue = "要重建的动作已不在当前目录。"; return false; }
                string context = TacticalPreviewBindingContextKey, fingerprint = catalog.Fingerprint;
                var equipment = CurrentSummaryEquipment(character);
                _runtimeSummaryContext = context; _runtimeSummaryCharacter = character; _runtimeSummaryEquipment = equipment;
                _runtimeSummary = new CharacterTestRuntimeSummaryJob(character, equipment,
                    CharacterTestTacticalSummaryPreparation.StandardMeasurement(), actions, () => {
                        if (IsHistoricalFrameSelected || context != TacticalPreviewBindingContextKey
                            || CharacterTestActionBrowserCatalog.Get().Fingerprint != fingerprint) return false;
                        var current = CurrentSummaryEquipment(character);
                        return current.UseDefaultLoadout == equipment.UseDefaultLoadout
                            && current.EquipmentStateStableId == equipment.EquipmentStateStableId;
                    });
                RuntimeSummarySerial++;
                _statusMessage = "正在当前界面重建摘要；独立模拟，不改变战场或卡片配置。";
                RefreshTacticalPreview();
                return true;
            }
            catch (Exception error) { issue = "摘要重建未开始：" + error.Message; return false; }
        }

        private static bool NeedsAutomaticSummaryRebuild(CharacterTestActionCompatibility compatibility)
            => !compatibility.CanSelect && CharacterTestCardSummaryPreparation.NeedsMeasurement(compatibility.Diagnostic);

        private int[] CurrentTacticalAutoSummaryActions()
        {
            if (!TacticalCardTestMode || !IsConfiguredTestCardEditing || !HasStandaloneTacticalCard)
                return Array.Empty<int>();
            return TacticalPreviewSlots(TacticalCardContext.CardId)
                .Select(path => ResolveAuthoringActionId(TacticalPreviewAction(TacticalCardContext.CardId, path).id))
                .Where(id => id > 0 && NeedsAutomaticSummaryRebuild(EvaluateBrowserAction(id)))
                .Distinct().ToArray();
        }

        internal void PrepareCurrentTacticalCardSummaries()
        {
            if (RuntimeSummaryRunning) return;
            int[] actions = CurrentTacticalAutoSummaryActions();
            if (actions.Length == 0) return;
            if (!TryStartRuntimeSummary(actions, out string issue))
            { _statusMessage = "自动摘要准备未开始：" + issue; return; }
            _tacticalAutoSummarySerial = RuntimeSummarySerial;
            _tacticalAutoSummaryCardId = TacticalCardContext.CardId;
            _statusMessage = "正在按需重建当前卡的战术摘要；完成后可使用此卡。";
            RefreshTacticalPreview();
        }

        private bool TryDeferTacticalPlayForSummary(int entryIndex, bool playAfterStart, out string message)
        {
            message = string.Empty;
            if (_resumingTacticalAutoSummaryPlay || !TacticalCardTestMode || !IsConfiguredTestCardEditing
                || !HasStandaloneTacticalCard || IsHistoricalFrameSelected
                || SessionCompleted || TacticalCardTestInFlight
                || PendingTacticalTestCardId != TacticalCardContext.CardId)
                return false;
            if (!RuntimeSummaryRunning) PrepareCurrentTacticalCardSummaries();
            if (!RuntimeSummaryRunning) return false;
            _pendingTacticalAutoSummaryPlay = new PendingTacticalAutoSummaryPlay {
                CardId = TacticalCardContext.CardId, Role = TestCardRole,
                BindingContext = TacticalPreviewBindingContextKey,
                WorkspaceId = DraftWorkspace.WorkspaceId, WorkspaceRevision = DraftWorkspace.CurrentRevision,
                EntryIndex = entryIndex, PlayAfterStart = playAfterStart,
                ShellWorkspace = ShellCoordinator.CurrentState.ActiveWorkspace,
                AutomaticJobSerial = _tacticalAutoSummaryCardId == TacticalCardContext.CardId
                    ? _tacticalAutoSummarySerial : 0
            };
            message = "正在准备当前卡的战术摘要；完成后自动继续出牌。";
            _statusMessage = message;
            RefreshTacticalPreview();
            return true;
        }

        private void ResumeTacticalPlayAfterSummary(long completedSerial)
        {
            PendingTacticalAutoSummaryPlay pending = _pendingTacticalAutoSummaryPlay;
            _pendingTacticalAutoSummaryPlay = null;
            if (pending == null) return;
            if (!TacticalCardTestMode || !IsConfiguredTestCardEditing
                || pending.CardId != TacticalCardContext.CardId || pending.CardId != PendingTacticalTestCardId
                || pending.Role != TestCardRole || pending.BindingContext != TacticalPreviewBindingContextKey
                || pending.WorkspaceId != DraftWorkspace.WorkspaceId
                || pending.WorkspaceRevision != DraftWorkspace.CurrentRevision
                || pending.ShellWorkspace != ShellCoordinator.CurrentState.ActiveWorkspace)
            { _statusMessage = "卡片或角色配置已变化；自动出牌已取消。"; RefreshTacticalPreview(); return; }
            if (pending.AutomaticJobSerial == completedSerial)
            {
                var failed = _runtimeSummary.Results.FirstOrDefault(r => r.Status != "成功");
                if (failed != null)
                { _statusMessage = "自动摘要准备失败：" + failed.Reason; RefreshTacticalPreview(); return; }
            }
            _resumingTacticalAutoSummaryPlay = pending.AutomaticJobSerial == completedSerial;
            try
            {
                // Summary completion invalidates the cached whole-card program. Compile at this
                // explicit use boundary before the play validator reads its cached result.
                if (DraftWorkspace.Cards.TryGetValue(_configuredDraftId, out var card))
                    CompileAuthoredCard(card, false);
                QueueTestCurrentChanges(pending.EntryIndex, out string message, pending.PlayAfterStart);
                _statusMessage = message;
            }
            finally { _resumingTacticalAutoSummaryPlay = false; }
            RefreshTacticalPreview();
        }

        private void TickRuntimeSummary()
        {
            if (!RuntimeSummaryRunning) _cardUiSummaries.Tick();
            if (!RuntimeSummaryRunning) return;
            int committed = _runtimeSummary.CommittedCount;
            _runtimeSummary.Tick();
            _statusMessage = _runtimeSummary.Status;
            if (committed != _runtimeSummary.CommittedCount || !_runtimeSummary.Running)
            {
                _actionBrowserCompatibility.Clear(); _actionBrowserContext = null;
                _tacticalPrepared.Clear(); _tacticalCompiled.Clear();
                _configuredFacesKey = _configuredRowsIdentity = null;
                RefreshTacticalPreview();
            }
            if (!_runtimeSummary.Running)
            {
                long completedSerial = RuntimeSummarySerial;
                ResumeTacticalPlayAfterSummary(completedSerial);
                if (_tacticalAutoSummarySerial == completedSerial)
                { _tacticalAutoSummarySerial = 0; _tacticalAutoSummaryCardId = null; }
            }
        }

        internal void CancelRuntimeSummary()
        {
            _cardUiSummaries.Dispose();
            if (!RuntimeSummaryRunning) return;
            _runtimeSummary.Cancel(); _statusMessage = _runtimeSummary.Status; RefreshTacticalPreview();
            _pendingTacticalAutoSummaryPlay = null;
            _tacticalAutoSummarySerial = 0;
            _tacticalAutoSummaryCardId = null;
        }
    }
}
