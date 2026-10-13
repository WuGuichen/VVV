using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    // Explicit Lab test definitions. They neither publish catalog content nor grant player inventory.
    internal static class CharacterTestRuntimeAiLabFixedAssembly
    {
        internal static string Name(TacticalAccessoryCombatKind kind) => CharacterTestTacticalAccessoryDisplay.Unresolved(kind).Name;
        // Only definitions admitted by this Lab baseline enter the visible offer list. Later batches extend this source.
        internal static IReadOnlyList<CharacterTacticalAccessoryDefinition> AvailableDefinitions()
        {
            var definitions = new List<CharacterTacticalAccessoryDefinition> {
                LegacyDefinition(TacticalAccessoryCombatKind.BorrowMomentum), LegacyDefinition(TacticalAccessoryCombatKind.PushAway),
                LegacyDefinition(TacticalAccessoryCombatKind.BreakMomentum) };
            definitions.AddRange(TacticalAccessoryThirdBatchPublication.CreateWithPreviousBatches(null).Configuration.Accessories);
            return definitions;
        }

        // Shared by click and drop; this edits the transient instance only. Attach and joint payment remain authoritative.
        internal static bool TryEquip(CharacterTestRuntimeAiLabCardTrialSelection card, TacticalAccessoryCombatKind kind,
            int slot, out string issue)
        {
            issue = null;
            if (Definition(kind) == null) { issue = "配件引用尚未接入当前试验。"; return false; }
            int count = Slots(card.FixedQuality);
            if (slot < 0) for (int i = 0; i < count; i++) if (card.AccessorySlots[i] == 0) { slot = i; break; }
            if (slot < 0 || slot >= count) { issue = "槽位已满，请先选定要替换的槽位。"; return false; }
            for (int i = 0; i < count; i++) if (i != slot && card.AccessorySlots[i] == kind)
            { issue = "同名重复装配合同尚未确定；当前试验请使用不同配件。"; return false; }
            card.FixedAssembly = true; card.AccessorySlots[slot] = kind;
            return true;
        }

        internal static CharacterTestTacticalAccessoryDisplay Display(TacticalAccessoryCombatKind kind)
            => kind == 0 ? CharacterTestTacticalAccessoryDisplay.Unresolved(kind)
                : Definition(kind) is CharacterTacticalAccessoryDefinition definition
                ? CharacterTestTacticalAccessoryDisplay.From(definition) : CharacterTestTacticalAccessoryDisplay.Unresolved(kind);
        internal static int ConfirmationCost(CharacterTestRuntimeAiLabCardTrialSelection selection)
        {
            if (!selection.FixedAssembly) return 0;
            int total = 0;
            foreach (var kind in selection.AccessorySlots) {
                var fee = Display(kind).ConfirmationCost;
                if (!fee.HasValue || total > int.MaxValue - fee.Value) return -1;
                total += fee.Value;
            }
            return total;
        }
        internal static int Slots(CharacterTacticalCardQuality quality) => quality == CharacterTacticalCardQuality.Silver ? 1
            : quality == CharacterTacticalCardQuality.Gold ? 2 : quality == CharacterTacticalCardQuality.Rainbow ? 3 : 0;
        internal static string Summary(CharacterTestRuntimeAiLabCardTrialSelection selection)
        {
            if (!selection.FixedAssembly) return "未装配配件";
            int count = Slots(selection.FixedQuality);
            string text = selection.FixedQuality == CharacterTacticalCardQuality.Silver ? "银" : selection.FixedQuality == CharacterTacticalCardQuality.Gold ? "金" : "彩";
            for (int i = 0; i < count; i++) text += (i == 0 ? " · " : "／") + Name(selection.AccessorySlots[i]);
            return text;
        }
        internal static string Validate(CharacterTestRuntimeAiLabCardTrialSelection selection)
        {
            if (!selection.FixedAssembly) return null;
            int count = Slots(selection.FixedQuality);
            if (count == 0 || selection.AccessorySlots == null || selection.AccessorySlots.Length != 3) return "配件测试品质或槽位失效。";
            for (int i = 0; i < 3; i++)
            {
                var kind = selection.AccessorySlots[i];
                if (kind != 0 && (i >= count || Definition(kind) == null))
                    return "配件超出测试品质槽数或引用失效。";
                for (int j = 0; kind != 0 && j < i; j++) if (kind == selection.AccessorySlots[j])
                    return "同名重复配件合同尚未确定；当前测试不支持重复。";
            }
            return null;
        }
        // Explicit Lab copies consume the exact reviewed first-batch definitions; no inventory grant or formal publication.
        internal static CharacterTacticalAccessoryDefinition Definition(TacticalAccessoryCombatKind kind)
        {
            foreach (var definition in AvailableDefinitions()) if (definition.Execution.Kind == kind) return definition;
            return null;
        }
        private static CharacterTacticalAccessoryDefinition LegacyDefinition(TacticalAccessoryCombatKind kind)
        {
            if (kind < TacticalAccessoryCombatKind.BorrowMomentum || kind > TacticalAccessoryCombatKind.BreakMomentum) return null;
            string id = kind == TacticalAccessoryCombatKind.BorrowMomentum ? "borrow" : kind == TacticalAccessoryCombatKind.PushAway ? "push" : "break";
            var reference = new CharacterTacticalAccessoryReference { AccessoryId = "lab.fixed." + id, Revision = 1, ContentHash = "lab.fixed." + id + ".test.v1" };
            return new CharacterTacticalAccessoryDefinition { ItemDefinitionId = 970100 + (int)kind,
                    Reference = reference, Quality = CharacterTacticalCardQuality.Silver, TriggerContractId = "lab.fixed." + id + ".first-qualified.v1",
                    FailureContractId = "lab.fixed." + id + ".retain-on-unqualified.v1", LifetimeContractId = "lab.fixed.until-battle-end-or-host-death.v1",
                    Execution = new TacticalAccessoryExecutionDefinition { Version = 1, Kind = kind, Lifetime = TacticalAccessoryLifetimeKind.UntilBattleEnd, DurationTurns = 0,
                        ConfirmationMomentumCost = 1, MomentumAmount = kind == TacticalAccessoryCombatKind.PushAway ? 0 : 2,
                        ReferenceSpeed = kind == TacticalAccessoryCombatKind.PushAway ? 4000 : 0,
                        ReferenceMass = kind == TacticalAccessoryCombatKind.PushAway ? 80000 : 0,
                        MinimumMassScale = kind == TacticalAccessoryCombatKind.PushAway ? 100 : 0,
                        MaximumMassScale = kind == TacticalAccessoryCombatKind.PushAway ? 4000 : 0,
                        MaximumSpeed = kind == TacticalAccessoryCombatKind.PushAway ? 12000 : 0 } };
        }
        internal static void Attach(CharacterTestRuntimeAiLabCardTrialSelection selection, TacticalCardDefinition main,
            int characterId, IReadOnlyDictionary<string, int> bindings)
        {
            if (!selection.FixedAssembly) return;
            string issue = Validate(selection); if (issue != null) throw new InvalidOperationException(issue);
            if (!(main is TacticalActionCardDefinition action) || !action.HasPublishedBehavior || action.PointMove != null || action.PositionRecall != null)
                throw new InvalidOperationException("固定配件测试当前只支持已发布行为主卡；点移／回返接缝尚未支持。");
            var definitions = new List<CharacterTacticalAccessoryDefinition>();
            var slots = new List<CharacterTacticalAccessorySlot>();
            for (int i = 0; i < Slots(selection.FixedQuality); i++)
            {
                var kind = selection.AccessorySlots[i]; if (kind == 0) continue;
                var definition = Definition(kind); var reference = definition.Reference;
                definitions.Add(definition);
                // Each explicit Lab entry has its own test copies; these numbers never address player Items.
                slots.Add(new CharacterTacticalAccessorySlot { SlotIndex = i,
                    AccessoryItemInstanceId = checked(100000 + (int)main.DeckEntryInstanceId * 4 + i), Reference = reference });
            }
            var snapshot = new CharacterTacticalAssemblySnapshot { Mode = CharacterTacticalAssemblyMode.FixedPreassembled,
                Quality = selection.FixedQuality, AccessorySlotCount = Slots(selection.FixedQuality), Accessories = definitions.ToArray(),
                Assembly = new CharacterTacticalCardAssembly { Schema = CharacterTacticalCardAssembly.CurrentSchema,
                    ConfigurationRevision = 1, ConfigurationHash = "lab.fixed.approved-test.v1", MainCard = new CharacterTacticalCardReference
                    { InstanceId = main.DeckEntryInstanceId, CardId = main.PublishedCardId, Revision = main.PublishedRevision, ContentHash = main.PublishedContentHash },
                    Accessories = slots } };
            if (!TacticalFixedAssemblyManifest.TryCreate(snapshot, main, characterId, 1, 1, bindings, out var manifest, out issue))
                throw new InvalidOperationException(issue);
            main.AttachFixedAssembly(manifest);
        }
    }
}
