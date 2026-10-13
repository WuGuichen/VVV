using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Items;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Explicit silver r1 trial definitions; installation and inventory grants remain a separate publication step.</summary>
    internal static class TacticalAccessoryThirdBatchPublication
    {
        internal const string NineSourcePath = "Authoring/TacticalAccessories/tactical_card_assembly.double-edge-nine.v3.json";

        // Keep the existing Editor assembly boundary: Items registry construction stays with this Runtime owner.
        internal static CharacterTacticalAssemblyPublication PrepareNineFromRoleConfiguration(string sourceJson, string previousJson,
            CharacterTacticalContentCatalog catalog, CharacterTacticalRoleInventoryConfig inventory, IEnumerable<string> knownTemplates)
        {
            if (inventory == null) throw new JsonSerializationException("TACTICAL_ROLE_INVENTORY_CONFIG_MISSING");
            inventory.Build(knownTemplates, out var roles, out _, out var characters);
            return PrepareNineForPublication(sourceJson, previousJson, catalog, roles,
                characters.ToDictionary(c => c.ItemDefinitionId, c => c.CharacterStableId));
        }

        /// <summary>Detached publication gate. Does not write files, grant inventory or change recipes.</summary>
        internal static CharacterTacticalAssemblyPublication PrepareNineForPublication(string sourceJson, string previousJson,
            CharacterTacticalContentCatalog catalog, ItemDefinitionRegistry roles, Dictionary<int, string> characters)
        {
            var source = JsonConvert.DeserializeObject<CharacterTacticalAssemblyPublication>(sourceJson);
            var mains = source?.Configuration?.MainCards;
            if (mains == null || mains.Length != 1 || mains[0]?.Card?.CardId != "tactical.bound-action"
                || mains[0].Quality != CharacterTacticalCardQuality.Silver)
                throw new JsonSerializationException("TACTICAL_NINE_DEFAULT_MAIN_CHANGED");
            var approved = CreateWithPreviousBatches(mains);
            if (!JToken.DeepEquals(JToken.FromObject(source), JToken.FromObject(approved)))
                throw new JsonSerializationException("TACTICAL_NINE_SOURCE_NOT_REVIEWED_DEFINITIONS");
            var definitions = source.BuildItemDefinitions(roles);
            CharacterTacticalAssemblyRules.ValidateConfiguration(source.Configuration, catalog, definitions, characters);
            if (previousJson != null)
            {
                var previous = JToken.FromObject(JsonConvert.DeserializeObject<CharacterTacticalAssemblyPublication>(previousJson));
                // Only exact published first/six/nine definitions with the same mains may be replaced.
                // An unknown publication, main binding, quality or changed definition is never overwritten.
                if (!JToken.DeepEquals(previous, JToken.FromObject(TacticalDoubleEdgePublication.Create(mains)))
                    && !JToken.DeepEquals(previous, JToken.FromObject(TacticalAccessorySecondBatchPublication.CreateWithFirstBatch(mains)))
                    && !JToken.DeepEquals(previous, JToken.FromObject(approved)))
                    throw new JsonSerializationException("TACTICAL_NINE_PREVIOUS_PUBLICATION_UNSUPPORTED");
            }
            return source;
        }

        internal static CharacterTacticalAssemblyPublication Create(CharacterTacticalMainCardDefinition[] mains)
        {
            var rows = new[] { Card(3005, "glass-cannon", TacticalAccessoryCombatKind.GlassCannon),
                Card(3014, "lucky-coin", TacticalAccessoryCombatKind.LuckyCoin),
                Card(3019, "iron-throne", TacticalAccessoryCombatKind.IronThrone) };
            var items = new CharacterTacticalAccessoryItemDefinitionConfig[rows.Length];
            for (int i = 0; i < rows.Length; i++) {
                string stable = "accessory." + rows[i].Reference.AccessoryId;
                items[i] = new CharacterTacticalAccessoryItemDefinitionConfig { ItemDefinitionId = rows[i].ItemDefinitionId,
                    StableId = stable, NameKey = stable + ".name", DescriptionKey = stable + ".description", MaxStackCount = 1 };
            }
            return Assemble(mains, rows, items);
        }
        internal static CharacterTacticalAssemblyPublication CreateWithPreviousBatches(CharacterTacticalMainCardDefinition[] mains)
        {
            var previous = TacticalAccessorySecondBatchPublication.CreateWithFirstBatch(mains); var third = Create(mains);
            var rows = new CharacterTacticalAccessoryDefinition[previous.Configuration.Accessories.Length + third.Configuration.Accessories.Length];
            var items = new CharacterTacticalAccessoryItemDefinitionConfig[previous.Items.Length + third.Items.Length];
            Array.Copy(previous.Configuration.Accessories, rows, previous.Configuration.Accessories.Length);
            Array.Copy(third.Configuration.Accessories, 0, rows, previous.Configuration.Accessories.Length, third.Configuration.Accessories.Length);
            Array.Copy(previous.Items, items, previous.Items.Length); Array.Copy(third.Items, 0, items, previous.Items.Length, third.Items.Length);
            return Assemble(mains, rows, items);
        }
        private static CharacterTacticalAssemblyPublication Assemble(CharacterTacticalMainCardDefinition[] mains,
            CharacterTacticalAccessoryDefinition[] rows, CharacterTacticalAccessoryItemDefinitionConfig[] items)
        {
            var config = new CharacterTacticalAssemblyConfiguration { Schema = CharacterTacticalAssemblyConfiguration.ExecutableSchema,
                Revision = 3, MainCards = mains ?? Array.Empty<CharacterTacticalMainCardDefinition>(), Accessories = rows };
            config.ContentHash = CharacterTacticalAssemblyRules.ConfigurationHash(config);
            return new CharacterTacticalAssemblyPublication { Schema = CharacterTacticalAssemblyPublication.CurrentSchema, Configuration = config, Items = items };
        }
        private static CharacterTacticalAccessoryDefinition Card(int item, string id, TacticalAccessoryCombatKind kind)
        {
            bool throne = kind == TacticalAccessoryCombatKind.IronThrone, coin = kind == TacticalAccessoryCombatKind.LuckyCoin;
            var row = new CharacterTacticalAccessoryDefinition { ItemDefinitionId = item, Quality = CharacterTacticalCardQuality.Silver,
                Reference = new CharacterTacticalAccessoryReference { AccessoryId = id, Revision = 1 },
                TriggerContractId = "paid-joint-submit.v1", FailureContractId = "no-confirmation-refund.v1",
                LifetimeContractId = throne ? "exact-ack-active-motion.v1" : coin ? "coin-receipt-next-full-settlement.v1" : "glass-conditional-next-full-settlement.v1",
                Execution = new TacticalAccessoryExecutionDefinition { Version = 4, Kind = kind,
                    Lifetime = throne ? TacticalAccessoryLifetimeKind.UntilBattleEnd : TacticalAccessoryLifetimeKind.LimitedTurns,
                    DurationTurns = throne ? 0 : 2, ConfirmationMomentumCost = 1, MomentumAmount = 0, ReferenceSpeed = 0,
                    ReferenceMass = 0, MinimumMassScale = 0, MaximumMassScale = 0, MaximumSpeed = 0,
                    ThirdBatch = new TacticalAccessoryThirdBatchExecutionDefinition { DamageScaleMilli = throne ? 0 : 1500,
                        CoinSelfWeight = coin ? 1 : 0, CoinEnemyWeight = coin ? 1 : 0 } } };
            var digest = RuntimeHashDigest.ComputeContentDigest(JsonConvert.SerializeObject(row));
            row.Reference.ContentHash = digest.Word0.ToString("x16") + digest.Word1.ToString("x16")
                + digest.Word2.ToString("x16") + digest.Word3.ToString("x16");
            return row;
        }
    }
}
