using System;
using MxFramework.Items;
using MxFramework.Config;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Reviewed three-card authoring source. It creates a candidate publication; it never publishes or grants items.</summary>
    internal static class TacticalDoubleEdgePublication
    {
        internal static CharacterTacticalAssemblyPublication Create(CharacterTacticalMainCardDefinition[] mainCards)
        {
            var rows = new[] {
                Card(3003, "duel-invitation", TacticalAccessoryCombatKind.DuelInvitation, 1, 1, 1500),
                Card(3009, "shattered-fantasy", TacticalAccessoryCombatKind.ShatteredFantasy, 2, 0, 2000),
                Card(3021, "black-hole-pocket", TacticalAccessoryCombatKind.BlackHolePocket, 2, 2, 1000) };
            var config = new CharacterTacticalAssemblyConfiguration { Schema = CharacterTacticalAssemblyConfiguration.ExecutableSchema,
                Revision = 1, MainCards = mainCards ?? Array.Empty<CharacterTacticalMainCardDefinition>(), Accessories = rows };
            config.ContentHash = CharacterTacticalAssemblyRules.ConfigurationHash(config);
            var items = new CharacterTacticalAccessoryItemDefinitionConfig[rows.Length];
            for (int i = 0; i < rows.Length; i++) { var row = rows[i]; items[i] = new CharacterTacticalAccessoryItemDefinitionConfig {
                    ItemDefinitionId = row.ItemDefinitionId, StableId = "accessory." + row.Reference.AccessoryId,
                    NameKey = "accessory." + row.Reference.AccessoryId + ".name",
                    DescriptionKey = "accessory." + row.Reference.AccessoryId + ".description", MaxStackCount = 1 }; }
            return new CharacterTacticalAssemblyPublication { Schema = CharacterTacticalAssemblyPublication.CurrentSchema,
                Configuration = config, Items = items };
        }

        private static CharacterTacticalAccessoryDefinition Card(int item, string id, TacticalAccessoryCombatKind kind, int fee, int turns, int scale)
        {
            var parameters = new TacticalDoubleEdgeExecutionDefinition { DamageScaleMilli = scale };
            if (kind == TacticalAccessoryCombatKind.BlackHolePocket) {
                parameters.RadiusMillimeters = 3000; parameters.StopRadiusMillimeters = 250;
                parameters.AccelerationMillimetersPerSecondSquared = 3000; parameters.ReferenceMassGrams = 80000;
                parameters.MaximumAcceleration = 6000; parameters.MaximumRadialSpeed = 2000;
            }
            var row = new CharacterTacticalAccessoryDefinition { ItemDefinitionId = item, Quality = CharacterTacticalCardQuality.Silver,
                Reference = new CharacterTacticalAccessoryReference { AccessoryId = id, Revision = 1 },
                TriggerContractId = kind == TacticalAccessoryCombatKind.BlackHolePocket ? "whole-main-natural-success.v1" : "paid-joint-submit.v1",
                FailureContractId = kind == TacticalAccessoryCombatKind.ShatteredFantasy ? "paid-battle-copy-exhaust.v1" : "no-confirmation-refund.v1",
                LifetimeContractId = kind == TacticalAccessoryCombatKind.DuelInvitation ? "actual-turn-frozen-pair.v1"
                    : kind == TacticalAccessoryCombatKind.ShatteredFantasy ? "exact-ack-and-projectile-birth.v1" : "terminal-through-next-turn.v1",
                Execution = new TacticalAccessoryExecutionDefinition { Version = 2, Kind = kind,
                    Lifetime = turns == 0 ? TacticalAccessoryLifetimeKind.UntilBattleEnd : TacticalAccessoryLifetimeKind.LimitedTurns,
                    DurationTurns = turns, ConfirmationMomentumCost = fee, MomentumAmount = 0, ReferenceSpeed = 0,
                    ReferenceMass = 0, MinimumMassScale = 0, MaximumMassScale = 0, MaximumSpeed = 0, DoubleEdge = parameters } };
            var digest = RuntimeHashDigest.ComputeContentDigest(JsonConvert.SerializeObject(row));
            row.Reference.ContentHash = digest.Word0.ToString("x16") + digest.Word1.ToString("x16")
                + digest.Word2.ToString("x16") + digest.Word3.ToString("x16");
            return row;
        }

        /// <summary>One explicit old registry, matching the two published character items before these three accessories.</summary>
        internal static ItemDefinitionRegistry LegacyRegistry()
        {
            var registry = new ItemDefinitionRegistry();
            for (int id = 1; id <= 2; id++) {
                var digits = id.ToString("D6");
                if (!registry.Register(new ItemDefinition(new ItemDefinitionId(id), "character-item." + digits,
                    new LocalizedTextKey("character." + digits + ".name"), new LocalizedTextKey("character." + digits + ".desc"),
                    ItemKind.Custom, new ItemTagSet(null), new ItemStackPolicy(1))).Success)
                    throw new InvalidOperationException("double-edge:LegacyRegistry");
            }
            return registry;
        }
    }
}
