using System;
using System.Linq;
using System.Threading.Tasks;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalPreparationView
    {
        // Explicit published slice identities; names never select mechanics or grant arbitrary catalog cards.
        private static readonly CharacterTacticalCardReference[] MechanismTrialReferences =
        {
            new() { CardId = "bc_gyj7wk6j1v", Revision = 1, ContentHash = "-3773259354468707649" },
            new() { CardId = "bc_3gdyznnddp", Revision = 1, ContentHash = "3702156180467248237" },
            new() { CardId = "bc_j6919g51v2", Revision = 1, ContentHash = "-924035983968617525" }
        };
        private string _mechanismTrialCharacter = "", _mechanismTrialName = "", _mechanismTrialStatus = "";
        private int _mechanismTrialPopupSequence;

        private bool MechanismTrialOwnerMatches() => _controller != null && _preparation != null
            && _controller.SelectedEntityId == _popupEntity
            && _preparation.Find(_popupEntity)?.CharacterStableId == _mechanismTrialCharacter;

        // LowFreqAlloc: explicit developer trial popup; uses the existing published choice component and save owner.
        private void ShowAcquireMechanismTrials()
        {
            var entity = _preparation.Find(_controller.SelectedEntityId);
            if (_busy || entity == null) return;
            _popupEntity = entity.EntityId; _mechanismTrialCharacter = entity.CharacterStableId;
            _mechanismTrialName = entity.DisplayName; _mechanismTrialStatus = "";
            _mechanismTrialPopupSequence++;
            OpenPopup(PopupKind.AcquireMechanismTrials, "领取机制试用卡", "");
            Required(_popup, "input").visible = false;
            Required(_popup, "cancel").asButton.title = "完成";
            Button(_popup, "confirm", RetryMechanismTrialSave).title = "重试保存";
            RenderMechanismTrials();
        }

        private bool OwnsMechanismTrial(CharacterTacticalCardReference reference) =>
            _player.Read(_mechanismTrialCharacter)?.Cards.Any(card => card.CardId == reference.CardId
                && card.Revision == reference.Revision && card.ContentHash == reference.ContentHash) == true;

        // LowFreqAlloc: renders three entries only when this popup opens or a grant/save completes.
        private void RenderMechanismTrials()
        {
            if (_root == null || _popupKind != PopupKind.AcquireMechanismTrials) return;
            Label(_popup, "context", _mechanismTrialName + "\n只领取到当前进度的持有卡库，不自动加入出战。\n关闭后点出战卡组旁的＋，再点卡面＋。三卡仅供玩家控制角色使用。");
            Label(_popup, "issue", _busy ? "正在保存…" : !string.IsNullOrEmpty(_player.SaveIssue)
                ? _player.SaveIssue : _mechanismTrialStatus);
            Required(_popup, "confirm").visible = !_busy && _player.HasUnsavedChanges && !string.IsNullOrEmpty(_player.SaveIssue);
            int generation = _generation;
            int popupSequence = _mechanismTrialPopupSequence;
            _popupList.itemRenderer = (index, item) =>
            {
                var reference = MechanismTrialReferences[index];
                var definition = _player.Catalog.ExactCard(reference);
                bool owned = OwnsMechanismTrial(reference);
                var row = item.asButton;
                row.title = definition == null ? "版本不可用 · " + (index == 0 ? "定点牵引" : index == 1 ? "记录回返点" : "返回记录点")
                    : (owned ? "✓ 已领取   " : "+ 领取   ") + definition.DisplayName;
                row.tooltips = definition == null ? "请先从更多菜单刷新卡库；正式试用版本必须可用。"
                    : owned ? "该角色已经持有这一正式版本，不重复领取。" : definition.Description;
                RowButton(row, () => AcquireMechanismTrial(index, generation, popupSequence));
                row.enabled = !_busy && definition != null && !owned && MechanismTrialOwnerMatches();
            };
            _popupList.numItems = MechanismTrialReferences.Length; _popupList.RefreshVirtualList(); LayoutPopup();
        }

        private async void AcquireMechanismTrial(int index, int generation, int popupSequence)
        {
            if (_busy || generation != _generation || popupSequence != _mechanismTrialPopupSequence
                || _root == null || _popupKind != PopupKind.AcquireMechanismTrials
                || !MechanismTrialOwnerMatches() || index < 0 || index >= MechanismTrialReferences.Length) return;
            var reference = MechanismTrialReferences[index];
            if (OwnsMechanismTrial(reference)) { RenderMechanismTrials(); return; }
            var definition = _player.Catalog.ExactCard(reference);
            if (definition == null)
            { _mechanismTrialStatus = "正式试用版本不可用，请先刷新卡库。"; RenderMechanismTrials(); return; }
            if (!_player.TryGrantCardForTesting(_mechanismTrialCharacter, reference, out string issue))
            { _mechanismTrialStatus = issue; RenderMechanismTrials(); return; }
            if (generation != _generation) return;
            _mechanismTrialStatus = "已领取：" + definition.DisplayName;
            await PersistMechanismTrials();
        }

        private async void RetryMechanismTrialSave() => await PersistMechanismTrials();

        private async Task PersistMechanismTrials()
        {
            if (_busy || _root == null || _popupKind != PopupKind.AcquireMechanismTrials || !MechanismTrialOwnerMatches()) return;
            var service = _player; int generation = _generation;
            _busy = true; _revision = -1; SetPopupBusy(true); RenderMechanismTrials();
            try
            {
                bool saved = await service.FlushAsync();
                if (_root == null || generation != _generation) return;
                if (saved) _mechanismTrialStatus = "✓ 已保存到当前进度。关闭后从卡库加入出战。";
            }
            catch (Exception error)
            { if (_root != null && generation == _generation) _mechanismTrialStatus = "保存失败：" + error.Message; }
            finally
            {
                if (_root != null && generation == _generation)
                {
                    _busy = false; _revision = -1; SetPopupBusy(false);
                    if (!MechanismTrialOwnerMatches()) ClosePopup(true); else RenderMechanismTrials();
                }
            }
        }
    }
}
