using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiAiLabCardTrialSurface
    {
        private readonly Dictionary<(TacticalCardDefinition Definition, uint Version), TacticalFixedCardFeePlan> _fixedFeeViews = new();
        private TacticalStageFlowCoordinator _fixedFeeViewStage;
        private string _fixedObservationTooltip = "";

        // LowFreqAlloc: the UI queries a compiled budget once per frozen definition/version in a session.
        // Confirmation always rebuilds its own proof; this display cache never admits or pays a card.
        private string FixedCardDetail(TacticalCardInstance card, out TacticalFixedCardFeePlan fees)
        {
            fees = null;
            var stage = _session.Runtime?.Stage;
            if (card?.Definition.FixedAssembly == null || stage == null) return "";
            if (!ReferenceEquals(_fixedFeeViewStage, stage)) { _fixedFeeViews.Clear(); _fixedFeeViewStage = stage; }
            var key = (card.Definition, card.Version);
            if (!_fixedFeeViews.TryGetValue(key, out fees)) {
                stage.Driver.TryReadFixedFees(card, out fees, out _);
                // Before ingress completes the real actor/resources can still become available.
                if (fees != null || stage.Phase == TacticalStagePhase.Planning) _fixedFeeViews[key] = fees;
            }
            string detail = TacticalFixedAssemblyReadout.TryDescribe(card.Definition, fees, out _, out var original) ? original : "";
            foreach (var part in card.Definition.FixedAssembly.Accessories) if (part.IsSecondBatch || part.IsThirdBatch) {
                var display = CharacterTestTacticalAccessoryDisplay.From(part);
                detail += "\n" + display.Name + " · " + display.FeeText + "\n" + display.RuleText;
            }
            return detail;
        }

        private void RefreshFixedReadout(TacticalStageFlowCoordinator stage)
        {
            if (stage == null) return;
            // Keep the authored conditions area for current participants, card zones and
            // payment/effect state. Initial resources, seed and hand count remain inspectable.
            string detail = "本次冻结条件\n" + _session.FrozenConditions;
            var pending = stage.Deck.PendingAction;
            if (pending?.Definition.FixedAssembly != null) {
                string pendingDetail = FixedCardDetail(pending, out var fees);
                if (pendingDetail.Length != 0) detail += "\n\n" + pendingDetail;
                if (fees != null) _readout.text += " · 确认付 " + fees.ConfirmationCost + "／行动预留 " + fees.MaximumActionBudget;
            }
            var effects = stage.InteractionEffects;
            var journal = effects?.ConfirmedFixedAssembly;
            if (journal != null && journal.CommitCount != 0) {
                var paid = journal.CommitAt(journal.CommitCount - 1);
                string payment = TacticalFixedAssemblyReadout.DescribeCommitted(paid, stage.Ledger as MomentumLedger);
                _conditionsDetail.text += "\n" + TacticalFixedAssemblyReadout.DescribeCommittedCompact(paid, stage.Ledger as MomentumLedger);
                detail += (detail.Length == 0 ? "" : "\n\n") + payment;
            }
            var battle = _slice?.Services.Modules.CharacterRuntime?.World.DoubleEdgeState;
            CharacterTestTacticalAccessoryStateDisplay.TryDescribe(battle, journal, effects?.Actor ?? default,
                out string thirdSummary, out string thirdDetail);
            if (TacticalFixedAssemblyReadout.TryDescribeEffects(effects, out var summary, out var effectDetail)) {
                if (thirdSummary.Length != 0) summary += " · " + thirdSummary;
                _readout.text += " · " + summary;
                // Keep the authored 204px conditions field compact; full per-accessory state,
                // actual deltas and lifetime remain in the same read-only observation tooltip.
                _conditionsDetail.text += "\n" + summary;
                detail += (detail.Length == 0 ? "" : "\n\n") + summary + "\n" + effectDetail;
            }
            else if (thirdSummary.Length != 0) {
                _readout.text += " · " + thirdSummary;
                _conditionsDetail.text += "\n" + thirdSummary;
            }
            if (thirdDetail.Length != 0) detail += "\n\n" + thirdDetail;
            _readout.autoSize = _conditionsDetail.autoSize = Fgui.AutoSizeType.Shrink;
            _readout.touchable = true;
            if (_fixedObservationTooltip != detail) {
                _fixedObservationTooltip = detail;
                _tooltips.Bind(_readout, detail, new MxFairyGuiTooltipOptions(preferredPlacement: MxFairyGuiTooltipPlacement.Above,
                    minimumTextWidth: 320, maximumTextWidth: 560, allowPin: false));
            }
        }

        private void RefreshPreparationFeeControls()
        {
            bool fixedMode = _session.Draft?.InitialHand.Exists(card => card.FixedAssembly) == true;
            bool visible = false; // Legacy probes are not the experiment entry or assembly workflow.
            Required("costLabel").visible = Required("preparationCostInputBackground").visible = _cost.visible = visible;
            Required("costLabel").text = "旧测试准备费用";
        }
    }
}
