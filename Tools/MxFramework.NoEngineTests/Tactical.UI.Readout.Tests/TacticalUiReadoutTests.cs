using System;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.NoEngineTests.TacticalUiReadout
{
    internal static class TacticalUiReadoutTests
    {
        internal static void Run(Action<string, bool> check)
        {
            bool Near(float actual, float expected) => Math.Abs(actual - expected) < 0.00001f;
            var full = TacticalCohesionReadout.FromScatterPressure(0, 100, false);
            check("No scatter leaves full bearing capacity", full.IsAvailable && Near(full.Remaining01, 1f));
            check("Partial scatter decreases remaining capacity", Near(TacticalCohesionReadout.FromScatterPressure(25, 100, false).Remaining01, 0.75f));
            check("Limit reached leaves zero capacity", Near(TacticalCohesionReadout.FromScatterPressure(100, 100, false).Remaining01, 0f));
            var excess = TacticalCohesionReadout.FromScatterPressure(150, 100, false);
            check("Over-limit facts clamp display without becoming invalid", excess.IsAvailable && excess.Remaining01 == 0f);
            var broken = TacticalCohesionReadout.FromScatterPressure(0, 100, true);
            check("Broken overrides even zero scatter", broken.IsAvailable && broken.IsBroken && broken.Remaining01 == 0f);
            check("Increased limit cannot visually clear Broken", TacticalCohesionReadout.FromScatterPressure(25, 200, true).Remaining01 == 0f);
            check("Authoritative recovery releases Broken display", TacticalCohesionReadout.FromScatterPressure(0, 100, false).Remaining01 == 1f);
            check("Current dynamic limit controls denominator", Near(TacticalCohesionReadout.FromScatterPressure(25, 50, false).Remaining01, 0.5f)
                && Near(TacticalCohesionReadout.FromScatterPressure(25, 200, false).Remaining01, 0.875f));
            foreach (int limit in new[] { 0, -1, int.MinValue })
            {
                var invalid = TacticalCohesionReadout.FromScatterPressure(0, limit, false);
                check("Invalid current limit is unavailable: " + limit, !invalid.IsAvailable && invalid.Remaining01 == 0f && !float.IsNaN(invalid.Remaining01));
            }
            check("Negative scatter is unavailable", !TacticalCohesionReadout.FromScatterPressure(-1, 100, false).IsAvailable);
            check("Maximum integer facts stay finite and bounded", TacticalCohesionReadout.FromScatterPressure(int.MaxValue, int.MaxValue, false).Remaining01 == 0f);

            var card = new TacticalActionCardDefinition("card", "name", "description", 10, "card", 3, "9223372036854775807", ulong.MaxValue - 1, "ui.behavior-card.custom");
            var display = TacticalCardDisplayData.FromDefinition(card);
            check("Display retains wide frozen identity", display.PublishedCardId == card.PublishedCardId && display.PublishedRevision == card.PublishedRevision
                && display.PublishedContentHash == card.PublishedContentHash && display.DeckEntryInstanceId == card.DeckEntryInstanceId);
            check("Display reads existing name, description, fee and icon", display.Name == "name" && display.Description == "description" && display.Cost == 10
                && display.IconKey == "ui.behavior-card.custom" && display.HasAuthoredIcon);
            check("Behavior card has unspecified quality", display.Quality == TacticalCardQualityPresentation.Unspecified);
            var utility = new TacticalUtilityCardDefinition("utility", "utility", "description", 0, new AddMomentumPayload(15));
            var utilityDisplay = TacticalCardDisplayData.FromDefinition(utility);
            check("Utility has the same unspecified quality without inferred icon", utilityDisplay.Quality == TacticalCardQualityPresentation.Unspecified
                && utilityDisplay.Kind == TacticalCardKind.Utility && !utilityDisplay.HasAuthoredIcon);
            check("Missing card is unavailable", !TacticalCardDisplayData.FromDefinition(null).IsAvailable);
            var missing = new TacticalActionCardDefinition("missing", "name", "description", 0, "missing", 1, "1", 1, null);
            check("Missing icon is explicit; no name-based guess", !TacticalCardDisplayData.FromDefinition(missing).HasAuthoredIcon);
            check("Quality introduces no configured rarity values", Enum.GetValues(typeof(TacticalCardQualityPresentation)).Length == 1);

            var ledger = new MomentumLedger(50);
            var before = TacticalMotionReadout.FromLedger(ledger, 50);
            check("Existing initial and capacity are owner-sourced", before.IsAvailable && before.Initial == 50 && before.Capacity == MomentumLedger.Maximum
                && before.Current == 50 && before.Reserved == 0 && before.Spendable == 50);
            check("Configured initial is not replaced with UI default", TacticalMotionReadout.FromLedger(ledger, 37).Initial == 37);
            bool reserved = ledger.TryReserve(10, out ulong token);
            var pending = TacticalMotionReadout.FromLedger(ledger, 50);
            check("Readout reflects reservation without paying", reserved && pending.Current == 50 && pending.Reserved == 10 && pending.Spendable == 40);
            check("Readout did not write ledger", ledger.Current == 50 && ledger.Reserved == 10 && ledger.Spendable == 40);
            ledger.ReleaseReservation(token);
            check("Frozen readout stays unchanged after owner cancellation", pending.Reserved == 10 && pending.Spendable == 40
                && TacticalMotionReadout.FromLedger(ledger, 50).Reserved == 0);
            check("Missing ledger has no available reading", !TacticalMotionReadout.FromLedger(null, 50).IsAvailable);
            check("Invalid initial is not silently clamped by display", !TacticalMotionReadout.FromLedger(ledger, -1).IsAvailable
                && !TacticalMotionReadout.FromLedger(ledger, MomentumLedger.Maximum + 1).IsAvailable);
            check("Empty and full owner balances are valid", TacticalMotionReadout.FromLedger(new MomentumLedger(0), 0).IsAvailable
                && TacticalMotionReadout.FromLedger(new MomentumLedger(MomentumLedger.Maximum), MomentumLedger.Maximum).IsAvailable);
        }
    }
}
