using System;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Candidate authoring data only. Installation requires the complete joint combat owner.</summary>
    internal static class TacticalAccessorySecondBatchPublication
    {
        internal static CharacterTacticalAssemblyPublication Create(CharacterTacticalMainCardDefinition[] mainCards)
        {
            var rows = new[] {
                Card(3004, "no-turning-back", TacticalAccessoryCombatKind.NoTurningBack, 1, 1, 1500, 0),
                Card(3008, "non-lethal-oath", TacticalAccessoryCombatKind.NonLethalOath, 1, 0, 0, 0),
                Card(3018, "one-punch-contract", TacticalAccessoryCombatKind.OnePunch, 2, 0, 0, 2000) };
            var items = new CharacterTacticalAccessoryItemDefinitionConfig[rows.Length];
            for (int i = 0; i < rows.Length; i++) {
                string stable = "accessory." + rows[i].Reference.AccessoryId;
                items[i] = new CharacterTacticalAccessoryItemDefinitionConfig { ItemDefinitionId = rows[i].ItemDefinitionId,
                    StableId = stable, NameKey = stable + ".name", DescriptionKey = stable + ".description", MaxStackCount = 1 };
            }
            return Assemble(mainCards, rows, items, 1);
        }

        internal static CharacterTacticalAssemblyPublication CreateWithFirstBatch(CharacterTacticalMainCardDefinition[] mainCards)
        {
            var first = TacticalDoubleEdgePublication.Create(mainCards); var second = Create(mainCards);
            var rows = new CharacterTacticalAccessoryDefinition[first.Configuration.Accessories.Length + second.Configuration.Accessories.Length];
            var items = new CharacterTacticalAccessoryItemDefinitionConfig[first.Items.Length + second.Items.Length];
            Array.Copy(first.Configuration.Accessories, rows, first.Configuration.Accessories.Length);
            Array.Copy(second.Configuration.Accessories, 0, rows, first.Configuration.Accessories.Length, second.Configuration.Accessories.Length);
            Array.Copy(first.Items, items, first.Items.Length); Array.Copy(second.Items, 0, items, first.Items.Length, second.Items.Length);
            return Assemble(mainCards, rows, items, 2);
        }

        private static CharacterTacticalAssemblyPublication Assemble(CharacterTacticalMainCardDefinition[] mains,
            CharacterTacticalAccessoryDefinition[] rows, CharacterTacticalAccessoryItemDefinitionConfig[] items, int revision)
        {
            var config = new CharacterTacticalAssemblyConfiguration { Schema = CharacterTacticalAssemblyConfiguration.ExecutableSchema,
                Revision = revision, MainCards = mains ?? Array.Empty<CharacterTacticalMainCardDefinition>(), Accessories = rows };
            config.ContentHash = CharacterTacticalAssemblyRules.ConfigurationHash(config);
            return new CharacterTacticalAssemblyPublication { Schema = CharacterTacticalAssemblyPublication.CurrentSchema,
                Configuration = config, Items = items };
        }

        private static CharacterTacticalAccessoryDefinition Card(int item, string id, TacticalAccessoryCombatKind kind,
            int fee, int turns, int rearScale, int punchScale)
        {
            var row = new CharacterTacticalAccessoryDefinition { ItemDefinitionId = item, Quality = CharacterTacticalCardQuality.Silver,
                Reference = new CharacterTacticalAccessoryReference { AccessoryId = id, Revision = 1 },
                TriggerContractId = "paid-joint-submit.v1", FailureContractId = "no-confirmation-refund.v1",
                LifetimeContractId = kind == TacticalAccessoryCombatKind.NoTurningBack ? "actual-turn-symmetric-rear.v1"
                    : kind == TacticalAccessoryCombatKind.NonLethalOath ? "exact-commit-lineage-nonlethal.v1" : "exact-commit-first-contact.v1",
                Execution = new TacticalAccessoryExecutionDefinition { Version = 3, Kind = kind,
                    Lifetime = turns == 0 ? TacticalAccessoryLifetimeKind.UntilBattleEnd : TacticalAccessoryLifetimeKind.LimitedTurns,
                    DurationTurns = turns, ConfirmationMomentumCost = fee, MomentumAmount = 0, ReferenceSpeed = 0,
                    ReferenceMass = 0, MinimumMassScale = 0, MaximumMassScale = 0, MaximumSpeed = 0,
                    SecondBatch = new TacticalAccessorySecondBatchExecutionDefinition { RearScaleMilli = rearScale, OnePunchScaleMilli = punchScale } } };
            var digest = RuntimeHashDigest.ComputeContentDigest(JsonConvert.SerializeObject(row));
            row.Reference.ContentHash = digest.Word0.ToString("x16") + digest.Word1.ToString("x16")
                + digest.Word2.ToString("x16") + digest.Word3.ToString("x16");
            return row;
        }
    }
}
