using MxFramework.Game.CharacterTest.TacticalCard;
namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class TacticalCardHud
    {
        private TacticalPointSelectionPreview _pointPreview;
        private TacticalCardInstance _behindPreviewCard;
        private TacticalCardInstance _cancelledPreviewCard;
        private uint _behindPreviewVersion;
        private bool _behindPreviewApplies;
        private CharacterTestBehaviorExecutionMovement _behindPreviewMovement;
        private string _behindPreviewIssue;
        private bool UpdateBehindDirectionPreview()
        {
            if (!CanPlan || _inventoryOpen || _automaticBattlePresentation || NeedsPoint)
            { _behindPreviewCard = null; _cancelledPreviewCard = null; return false; }
            var card = _coordinator.Deck.PendingAction;
            if (card == null) { _behindPreviewCard = null; _cancelledPreviewCard = null; return false; }
            if (ReferenceEquals(card, _cancelledPreviewCard)) return false;
            _cancelledPreviewCard = null;
            if (!ReferenceEquals(card, _behindPreviewCard) || card.Version != _behindPreviewVersion)
            {
                HidePointVisuals(); _behindPreviewCard = card; _behindPreviewVersion = card.Version;
                _behindPreviewApplies = _coordinator.Driver.TryPrepareBehindPreview(card, out _behindPreviewMovement, out _behindPreviewIssue);
            }
            if (!_behindPreviewApplies) return false;
            if (!string.IsNullOrEmpty(_behindPreviewIssue)
                || !_coordinator.Driver.TrySampleBehindPreview(card, _behindPreviewMovement, out var origin, out var sample))
            { HidePointVisuals(); SetPointFeedback("无有效初始移动方向。"); return true; }
            var preview = CharacterTestBehindTargetPreview.Create(_behindPreviewMovement, sample);
            if (preview.Status != CharacterTestBehindTargetPreviewStatus.Moving)
            {
                HidePointVisuals();
                SetPointFeedback(preview.Status == CharacterTestBehindTargetPreviewStatus.Arrived ? "已到位，无初始移动。"
                    : preview.Status == CharacterTestBehindTargetPreviewStatus.Holding ? "已在保持区，无初始移动。" : "无有效初始移动方向。");
                return true;
            }
            if (_pointPreview == null && !_pointVisualUnavailable)
            { _pointPreview = new TacticalPointSelectionPreview(transform); _pointVisualUnavailable = !_pointPreview.IsAvailable; }
            _pointPreview?.ShowDirection(origin, preview.Direction);
            SetPointFeedback(_pointVisualUnavailable ? "初始移动方向提示暂不可显示。" : "脚边箭头显示当前初始移动方向。");
            return true;
        }
        private TacticalWorldPoint _pointVisualCandidate;
        private bool _hasPointVisualCandidate, _pointVisualUnavailable;
        private void UpdatePointVisuals(TacticalPointMoveRules rules, in TacticalExecutionInput selected)
        {
            if (!_coordinator.Driver.TryGetPointOrigin(out var origin)) { HidePointVisuals(); return; }
            if (_pointPreview == null && !_pointVisualUnavailable)
            { _pointPreview = new TacticalPointSelectionPreview(transform); _pointVisualUnavailable = !_pointPreview.IsAvailable; }
            if (_pointPreview == null || !_pointPreview.IsAvailable) return;
            bool accepted = selected.Kind == TacticalExecutionInputKind.WorldPoint;
            _pointPreview.Show(origin, rules.Input.RadiusMillimeters, _hasPointVisualCandidate || accepted,
                _hasPointVisualCandidate ? _pointVisualCandidate : selected.Point, rules.ArrivalMillimeters,
                _pointPickStatus == TacticalInputStatus.Valid || (_pointPickStatus == TacticalInputStatus.MissingInput && accepted));
        }
        private void HidePointVisuals() { _pointPreview?.Hide(); _hasPointVisualCandidate = false; }
        private void DisposePointVisuals() { _pointPreview?.Dispose(); _pointPreview = null; }
    }
}
