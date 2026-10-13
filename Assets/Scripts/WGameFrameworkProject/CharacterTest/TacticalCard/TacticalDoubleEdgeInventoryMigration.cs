using System;
using System.Linq;
using MxFramework.Items;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Finite, detached migration: exact legacy or first-batch registry to a reviewed accessory registry.</summary>
    internal static class TacticalDoubleEdgeInventoryMigration
    {
        /// <summary>Retains exact fixed selections when the reviewed three/six publication grows to nine.</summary>
        internal static string Prepare(string playerJson, ItemDefinitionRegistry destination,
            CharacterTacticalAssemblyConfiguration configuration)
        {
            string migrated = Prepare(playerJson, destination);
            if (migrated == null) return null;
            var state = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(migrated);
            var assemblies = state?.CharacterItems?.SelectMany(role => role.CardAssemblies
                ?? Enumerable.Empty<CharacterTacticalCardAssembly>()).ToArray();
            if (assemblies == null || assemblies.Length == 0) return migrated;
            CharacterTacticalAssemblyRules.ValidateSave(state);
            bool changed = false;
            foreach (var assembly in assemblies)
            {
                if (configuration != null && assembly.ConfigurationRevision == configuration.Revision
                    && assembly.ConfigurationHash == configuration.ContentHash) continue;
                if (configuration?.MainCards == null || configuration.Revision != 3
                    || configuration.ContentHash != CharacterTacticalAssemblyRules.ConfigurationHash(configuration))
                    throw new JsonSerializationException("TACTICAL_NINE_ASSEMBLY_MIGRATION_UNSUPPORTED");
                var reviewed = TacticalAccessoryThirdBatchPublication.CreateWithPreviousBatches(configuration.MainCards);
                if (configuration.ContentHash != reviewed.Configuration.ContentHash
                    || destination.ComputeStableHash() != reviewed.BuildItemDefinitions(TacticalDoubleEdgePublication.LegacyRegistry()).ComputeStableHash())
                    throw new JsonSerializationException("TACTICAL_NINE_ASSEMBLY_MIGRATION_UNSUPPORTED");
                // Recreate only the two exact prior configurations using unchanged destination mains.
                // Changed quality, main revision/hash, unknown old config or legacy Lab test hashes fail closed.
                var first = TacticalDoubleEdgePublication.Create(configuration.MainCards).Configuration;
                var second = TacticalAccessorySecondBatchPublication.CreateWithFirstBatch(configuration.MainCards).Configuration;
                var prior = assembly.ConfigurationRevision == first.Revision && assembly.ConfigurationHash == first.ContentHash ? first
                    : assembly.ConfigurationRevision == second.Revision && assembly.ConfigurationHash == second.ContentHash ? second : null;
                if (prior == null)
                    throw new JsonSerializationException("TACTICAL_NINE_ASSEMBLY_SOURCE_UNSUPPORTED");
                var main = prior.MainCards.SingleOrDefault(row => CharacterTacticalAssemblyRules.Same(row.Card, assembly.MainCard));
                if (main == null || assembly.Accessories.Any(slot => slot.SlotIndex >= main.AccessorySlotCount
                    || !prior.Accessories.Any(row => CharacterTacticalAssemblyRules.Same(row.Reference, slot.Reference)
                        && state.AccessoryItems.Any(owned => owned.ItemInstanceId == slot.AccessoryItemInstanceId
                            && owned.ItemDefinitionId == row.ItemDefinitionId))))
                    throw new JsonSerializationException("TACTICAL_NINE_ASSEMBLY_PRIOR_SELECTION_INVALID");
                assembly.ConfigurationRevision = configuration.Revision;
                assembly.ConfigurationHash = configuration.ContentHash;
                changed = true;
            }
            // Slots, owned copies, acquisition receipts, cards, bindings and revisions are retained.
            // The composition root still validates exact ownership/capacity and commits the aggregate by CAS.
            return changed ? JsonConvert.SerializeObject(state) : migrated;
        }

        internal static string Prepare(string playerJson, ItemDefinitionRegistry destination)
        {
            if (playerJson == null) return null;
            var state = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(playerJson);
            if (state?.Items == null) return playerJson;
            CharacterTacticalItemState.Validate(state.Items);
            var runtime = state.Items.ToRuntimeState();
            var restored = new InventoryStore();
            if (new InventorySaveStateProvider(restored, destination).RestoreSaveState(runtime).Success) return playerJson;
            var legacy = TacticalDoubleEdgePublication.LegacyRegistry();
            var first = TacticalDoubleEdgePublication.Create(null).BuildItemDefinitions(legacy);
            var second = TacticalAccessorySecondBatchPublication.CreateWithFirstBatch(null).BuildItemDefinitions(legacy);
            var third = TacticalAccessoryThirdBatchPublication.CreateWithPreviousBatches(null).BuildItemDefinitions(legacy);
            bool thirdDestination = destination.ComputeStableHash() == third.ComputeStableHash();
            bool firstDestination = destination.ComputeStableHash() == first.ComputeStableHash();
            bool secondDestination = destination.ComputeStableHash() == second.ComputeStableHash();
            if (!firstDestination && !secondDestination && !thirdDestination)
                throw new JsonSerializationException("TACTICAL_DOUBLE_EDGE_REGISTRY_MIGRATION_UNSUPPORTED");
            bool legacySource = new InventorySaveStateProvider(restored, legacy).RestoreSaveState(runtime).Success;
            if (!legacySource) {
                restored = new InventoryStore();
                bool restoredPrior = (secondDestination || thirdDestination) && new InventorySaveStateProvider(restored, first).RestoreSaveState(runtime).Success;
                if (!restoredPrior && thirdDestination) {
                    restored = new InventoryStore();
                    restoredPrior = new InventorySaveStateProvider(restored, second).RestoreSaveState(runtime).Success;
                }
                if (!restoredPrior) throw new JsonSerializationException("TACTICAL_DOUBLE_EDGE_REGISTRY_MIGRATION_UNSUPPORTED");
            }
            // The provider validates stacks, allocator, layout and registry before recapture. Ledger and settlements stay byte-for-byte.
            var captured = new InventorySaveStateProvider(restored, destination).CaptureSaveState();
            if (!captured.Success) throw new JsonSerializationException("TACTICAL_DOUBLE_EDGE_REGISTRY_RECAPTURE_FAILED");
            for (int i = 0; i < state.Items.Modules.Length; i++)
                if (state.Items.Modules[i].ModuleId == InventorySaveStateProvider.ModuleId) state.Items.Modules[i] = captured.Value.ModuleStates[0];
            if (!new InventorySaveStateProvider(new InventoryStore(), destination).RestoreSaveState(state.Items.ToRuntimeState()).Success)
                throw new JsonSerializationException("TACTICAL_DOUBLE_EDGE_REGISTRY_REVALIDATION_FAILED");
            return JsonConvert.SerializeObject(state);
        }
    }
}
