using MxFramework.Game.CharacterTest.TacticalCard;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class TacticalCardHud
    {
        private UnityEngine.Camera _pointCamera;
        private MxFramework.Input.IInputProvider _pointInputProvider;
        private string _pointFeedback = string.Empty;
        private TacticalInputStatus _pointPickStatus = TacticalInputStatus.MissingInput;
        private ulong _pointCardInstance;
        private ulong _pointFeedbackRevision;
        private TacticalInputStatus _pointFeedbackStatus;
        private bool _pointFeedbackHadSelection, _pointFeedbackVisualUnavailable;
        private bool NeedsPoint => CanPlan && _coordinator.Deck.PendingAction != null
            && _coordinator.Driver.GetRequiredInputKind(_coordinator.Deck.PendingAction) == TacticalExecutionInputKind.WorldPoint;

        /// <summary>The UI submits a copied input command. The stage alone accepts or rejects it.</summary>
        internal bool TrySelectWorldPoint(TacticalWorldPoint point)
        {
            if (!NeedsPoint || _nextSequence == 0 || !_coordinator.TryGetRoster(out var actor, out _)
                || !_coordinator.Driver.TryGetPointOrigin(out var origin)) return false;
            var card = _coordinator.Deck.PendingAction;
            var binding = new TacticalExecutionInputBinding(_coordinator.StageRunId, TurnIndex, card.InstanceId, card.Version, actor);
            var input = new TacticalExecutionInput(binding, TacticalExecutionInputSource.Manual, point, origin);
            _pointVisualCandidate = point; _hasPointVisualCandidate = true;
            _pointPickStatus = _coordinator.Driver.ValidatePendingInput(card, input);
            if (_pointPickStatus != TacticalInputStatus.Valid) { _dirty = true; return false; }
            var command = new TacticalStageCommand(_coordinator.StageRunId, TurnIndex, card.InstanceId, card.Version,
                _coordinator.SnapshotRevision, TacticalStageCommandKind.SetExecutionInput, input);
            LastSubmission = _commands.TrySubmit(command, source: 0, sequence: _nextSequence++);
            _dirty = true;
            return LastSubmission == TacticalStageRejection.None;
        }

        private void UpdatePointSelection()
        {
            if (!NeedsPoint || _inventoryOpen || _automaticBattlePresentation)
            {
                if (UpdateBehindDirectionPreview()) { _pointCardInstance = 0; return; }
                SetPointFeedback(string.Empty); _pointCardInstance = 0; HidePointVisuals(); return;
            }
            var card = _coordinator.Deck.PendingAction;
            bool selectionChanged = _pointCardInstance != card.InstanceId;
            if (selectionChanged)
            { _pointCardInstance = card.InstanceId; _pointPickStatus = TacticalInputStatus.MissingInput; _hasPointVisualCandidate = false; }
            var provider = _pointInputProvider ?? _preparationManager?.RuntimeContext?.InputProvider;
            if (provider != null)
            {
                var input = provider.Snapshot;
                if (input.CancelPressed) { if (DirectPlayActive) CancelDirectPlay(); else TryCancelPending(); return; }
                if (!selectionChanged && input.ClickPressed && !PointClickIsOnUi(input.Point))
                {
                    if (_pointCamera == null) _pointCamera = UnityEngine.Camera.main;
                    if (_pointCamera == null) _pointPickStatus = TacticalInputStatus.GroundUnavailable;
                    else
                    {
                        Ray ray = _pointCamera.ScreenPointToRay(input.Point);
                        if (!TacticalWorldPoint.TryFromMeters(ray.origin.x, ray.origin.y, ray.origin.z, out var rayOrigin)
                            || !TacticalWorldPoint.TryFromMeters(ray.direction.x, ray.direction.y, ray.direction.z, out var rayDirection))
                            _pointPickStatus = TacticalInputStatus.InvalidGround;
                        else
                        {
                            _pointPickStatus = _coordinator.Driver.TryPickGround(rayOrigin, rayDirection, out var point);
                            if (_pointPickStatus == TacticalInputStatus.Valid) TrySelectWorldPoint(point);
                        }
                    }
                }
            }
            var selected = _coordinator.Deck.PendingExecutionInput;
            var rules = ((TacticalActionCardDefinition)card.Definition).PointMove;
            UpdatePointVisuals(rules, selected);
            bool hasSelection = selected.Kind == TacticalExecutionInputKind.WorldPoint;
            if (_pointFeedbackRevision == _coordinator.SnapshotRevision && _pointFeedbackStatus == _pointPickStatus
                && _pointFeedbackHadSelection == hasSelection && _pointFeedbackVisualUnavailable == _pointVisualUnavailable
                && _pointFeedback.Length != 0) return;
            _pointFeedbackRevision = _coordinator.SnapshotRevision; _pointFeedbackStatus = _pointPickStatus;
            _pointFeedbackHadSelection = hasSelection; _pointFeedbackVisualUnavailable = _pointVisualUnavailable;
            string prompt = selected.Kind == TacticalExecutionInputKind.WorldPoint
                ? DirectPlayActive ? "正在提交地点…" : "已选择地点；确认或取消。"
                : DirectPlayActive ? "点击有效落点出牌；右键或 Esc 取消。" : "请选择范围内的有效地面点，再确认。";
            if (_pointPickStatus != TacticalInputStatus.Valid && _pointPickStatus != TacticalInputStatus.MissingInput)
                prompt = "地点未接纳：" + _pointPickStatus + "。请重新选点或取消。";
            if (_pointVisualUnavailable) prompt += " 范围与地点提示暂不可显示。";
            SetPointFeedback(prompt + " 范围 " + (rules.Input.RadiusMillimeters / 1000f).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " 米。");
        }

        private bool PointClickIsOnUi(Vector2 screenPoint)
        {
            if (UsesFairyBattlePresentation && _fairyBattleView.ContainsScreenPoint(screenPoint)) return true;
            if (FairyGUI.Stage.isTouchOnUI) return true;
            if (_root?.panel == null || UsesFairyBattlePresentation) return false;
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            for (var picked = _root.panel.Pick(panelPoint); picked != null; picked = picked.parent)
                if (picked is Button) return true;
            return false;
        }
        private void SetPointFeedback(string value)
        { if (_pointFeedback == value) return; _pointFeedback = value; _dirty = true; }
    }
}
