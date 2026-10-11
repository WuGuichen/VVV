using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.CharacterApplication;
using MxFramework.Config;
using MxFramework.Items;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal enum CharacterTacticalCardQuality { Unspecified = 0, Silver = 1, Gold = 2, Rainbow = 3 }
    internal enum CharacterTacticalAssemblyMode { Unspecified = 0, FixedPreassembled = 1, RandomDrawAssembly = 2 }
    internal enum CharacterTacticalContentKind { Unspecified = 0, MainCard = 1, Accessory = 2, Action = 3, Character = 4 }

    /// <summary>One explicit grant routed to the existing semantic owner. No reward selection or batch semantics.</summary>
    internal sealed class CharacterTacticalContentGrantRequest
    {
        public CharacterTacticalContentKind Kind { get; set; }
        public long RequestId { get; set; }
        public int OwnerId { get; set; }
        public int CharacterItemInstanceId { get; set; }
        public int TargetContainerId { get; set; }
        public int ItemDefinitionId { get; set; }
        public CharacterTacticalCardReference MainCard { get; set; }
        public string ActionId { get; set; }
        public string DisplayName { get; set; }
        public long ExpectedCharacterRevision { get; set; }
        public long ExpectedPlayerRevision { get; set; }
        public long Frame { get; set; }
    }

    internal readonly struct CharacterTacticalContentGrantReceipt
    {
        internal ItemAcquisitionResult Result { get; }
        internal ItemInstanceId ItemInstanceId { get; }
        internal ulong CardInstanceId { get; }
        internal CharacterTacticalContentGrantReceipt(ItemAcquisitionResult result, ItemInstanceId item = default, ulong card = 0)
        { Result = result; ItemInstanceId = item; CardInstanceId = card; }
    }

    /// <summary>Detached backpack view. Cards and learned actions remain nested in their real character instance.</summary>
    internal sealed class CharacterTacticalOwnedContent
    {
        public CharacterTacticalCharacterItemState[] Characters { get; set; }
        public CharacterTacticalAccessoryItemState[] Accessories { get; set; }
    }

    /// <summary>Optional formal publication, loaded by the existing composition root without granting content.</summary>
    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class CharacterTacticalAssemblyPublication
    {
        public const string CurrentSchema = "mx.character-test.card-assembly-publication.v1";
        public const string FileName = "tactical_card_assembly.json";
        public string Schema { get; set; }
        public CharacterTacticalAssemblyConfiguration Configuration { get; set; }
        public CharacterTacticalAccessoryItemDefinitionConfig[] Items { get; set; }

        /// <remarks>LowFreqAlloc. Builds a detached registry; failure cannot partially mutate the original.</remarks>
        internal ItemDefinitionRegistry BuildItemDefinitions(ItemDefinitionRegistry existing)
        {
            if (Schema != CurrentSchema || Configuration == null || Items == null || existing == null)
                throw new JsonSerializationException("TACTICAL_ASSEMBLY_PUBLICATION_INVALID");
            var staged = new ItemDefinitionRegistry();
            foreach (var definition in existing.Definitions)
                if (!staged.Register(definition).Success) throw new JsonSerializationException("TACTICAL_ASSEMBLY_ITEM_DUPLICATE");
            foreach (var item in Items)
            {
                if (item == null || item.ItemDefinitionId <= 0 || string.IsNullOrWhiteSpace(item.StableId)
                    || string.IsNullOrWhiteSpace(item.NameKey) || string.IsNullOrWhiteSpace(item.DescriptionKey) || item.MaxStackCount <= 0)
                    throw new JsonSerializationException("TACTICAL_ACCESSORY_ITEM_DEFINITION_INVALID");
                if (!staged.Register(new ItemDefinition(new ItemDefinitionId(item.ItemDefinitionId), item.StableId,
                    new LocalizedTextKey(item.NameKey), new LocalizedTextKey(item.DescriptionKey), ItemKind.Custom,
                    new ItemTagSet(null), new ItemStackPolicy(item.MaxStackCount))).Success)
                    throw new JsonSerializationException("TACTICAL_ASSEMBLY_ITEM_DUPLICATE");
            }
            if (Configuration.Accessories == null || Items.Length != Configuration.Accessories.Length
                || Items.Any(item => Configuration.Accessories.Count(value => value?.ItemDefinitionId == item.ItemDefinitionId) != 1))
                throw new JsonSerializationException("TACTICAL_ACCESSORY_ITEM_MAPPING_INVALID");
            return staged;
        }
    }

    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class CharacterTacticalAccessoryItemDefinitionConfig
    {
        public int ItemDefinitionId { get; set; }
        public string StableId { get; set; }
        public string NameKey { get; set; }
        public string DescriptionKey { get; set; }
        public int MaxStackCount { get; set; }
    }

    /// <summary>Opt-in author configuration. No definition grants ownership or enables execution.</summary>
    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class CharacterTacticalAssemblyConfiguration
    {
        public const string CurrentSchema = "mx.character-test.card-assembly-config.v1";
        // v1 remains a data-only contract; executable parameters cannot be hidden in that schema.
        public const string ExecutableSchema = "mx.character-test.card-assembly-config.v2";
        public string Schema { get; set; }
        public int Revision { get; set; }
        public string ContentHash { get; set; }
        public CharacterTacticalMainCardDefinition[] MainCards { get; set; } = Array.Empty<CharacterTacticalMainCardDefinition>();
        public CharacterTacticalAccessoryDefinition[] Accessories { get; set; } = Array.Empty<CharacterTacticalAccessoryDefinition>();
    }

    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class CharacterTacticalMainCardDefinition
    {
        public CharacterTacticalCardReference Card { get; set; }
        public CharacterTacticalCardQuality Quality { get; set; }
        [JsonIgnore] internal int AccessorySlotCount => CharacterTacticalAssemblyRules.SlotCount(Quality);
    }

    /// <summary>Fixed contract identifiers and explicit executable parameters belong to authors.</summary>
    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class CharacterTacticalAccessoryDefinition
    {
        public int ItemDefinitionId { get; set; }
        public CharacterTacticalAccessoryReference Reference { get; set; }
        public CharacterTacticalCardQuality Quality { get; set; }
        public string TriggerContractId { get; set; }
        public string FailureContractId { get; set; }
        public string LifetimeContractId { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TacticalAccessoryExecutionDefinition Execution { get; set; }
    }

    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class CharacterTacticalAccessoryReference
    {
        public string AccessoryId { get; set; }
        public int Revision { get; set; }
        public string ContentHash { get; set; }
    }

    /// <summary>Metadata joins the real Items instance; its container is the ownership authority.</summary>
    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class CharacterTacticalAccessoryItemState
    {
        public int ItemInstanceId { get; set; }
        public int ItemDefinitionId { get; set; }
        public CharacterTacticalAccessoryReference Reference { get; set; }
        public long AcquisitionRequestId { get; set; }
        public string AcquisitionFingerprint { get; set; }
    }

    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class CharacterTacticalCardAcquisition
    {
        public ulong CardInstanceId { get; set; }
        public long RequestId { get; set; }
        public string RequestFingerprint { get; set; }
    }

    /// <summary>Concrete main-card selection. This is neither an effect host nor a battle deck.</summary>
    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class CharacterTacticalCardAssembly
    {
        public const string CurrentSchema = "mx.character-test.card-assembly.v1";
        public string Schema { get; set; }
        public int ConfigurationRevision { get; set; }
        public string ConfigurationHash { get; set; }
        public CharacterTacticalCardReference MainCard { get; set; }
        public List<CharacterTacticalAccessorySlot> Accessories { get; set; }
    }

    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class CharacterTacticalAccessorySlot
    {
        public int SlotIndex { get; set; }
        public int AccessoryItemInstanceId { get; set; }
        public CharacterTacticalAccessoryReference Reference { get; set; }
    }

    /// <summary>Detached common data snapshot. Mode does not prove draw eligibility or play admission.</summary>
    internal sealed class CharacterTacticalAssemblySnapshot
    {
        public CharacterTacticalAssemblyMode Mode { get; set; }
        public CharacterTacticalCardAssembly Assembly { get; set; }
        public CharacterTacticalCardQuality Quality { get; set; }
        public int AccessorySlotCount { get; set; }
        public CharacterTacticalAccessoryDefinition[] Accessories { get; set; }
    }

    internal static class CharacterTacticalAssemblyRules
    {
        internal static bool HasCurrentCharacterItems(CharacterTacticalPlayerState state) => state?.CharacterItems != null
            && (state.Schema == CharacterTacticalPlayerState.CharacterTacticalSchema || state.Schema == CharacterTacticalPlayerState.CardAssemblySchema);
        internal static bool RequiresConfiguration(CharacterTacticalPlayerState state) => state?.AccessoryItems?.Count > 0
            || state?.CharacterItems?.Any(role => role?.CardAssemblies?.Count > 0) == true;
        internal static int SlotCount(CharacterTacticalCardQuality quality) => quality switch
        {
            CharacterTacticalCardQuality.Silver => 1,
            CharacterTacticalCardQuality.Gold => 2,
            CharacterTacticalCardQuality.Rainbow => 3,
            _ => throw new JsonSerializationException("TACTICAL_ASSEMBLY_QUALITY_INVALID")
        };

        internal static bool Same(CharacterTacticalAccessoryReference a, CharacterTacticalAccessoryReference b)
            => a != null && b != null && a.AccessoryId == b.AccessoryId && a.Revision == b.Revision && a.ContentHash == b.ContentHash;
        internal static bool Same(CharacterTacticalCardReference a, CharacterTacticalCardReference b)
            => a != null && b != null && a.CardId == b.CardId && a.Revision == b.Revision && a.ContentHash == b.ContentHash;
        internal static bool Valid(CharacterTacticalAccessoryReference value) => value != null
            && !string.IsNullOrWhiteSpace(value.AccessoryId) && value.Revision > 0 && !string.IsNullOrWhiteSpace(value.ContentHash);
        private static bool Valid(CharacterTacticalCardReference value) => value != null
            && !string.IsNullOrWhiteSpace(value.CardId) && value.Revision > 0 && !string.IsNullOrWhiteSpace(value.ContentHash);
        private static bool Digest(string value) => value?.Length == 64
            && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');

        /// <summary>Canonical authored configuration digest; excludes the digest field itself and collection order.</summary>
        /// <remarks>LowFreqAlloc. Publication tools must calculate this after assigning the explicit revision.</remarks>
        internal static string ConfigurationHash(CharacterTacticalAssemblyConfiguration config)
        {
            var dataOnly = new { config.Schema, config.Revision,
                MainCards = config.MainCards.OrderBy(value => value.Card.CardId, StringComparer.Ordinal).ThenBy(value => value.Card.Revision)
                    .Select(value => new { value.Card.CardId, value.Card.Revision, value.Card.ContentHash, value.Quality }).ToArray(),
                Accessories = config.Accessories.OrderBy(value => value.Reference.AccessoryId, StringComparer.Ordinal).ThenBy(value => value.Reference.Revision)
                    .Select(value => new { value.ItemDefinitionId, value.Reference, value.Quality,
                        value.TriggerContractId, value.FailureContractId, value.LifetimeContractId }).ToArray() };
            // Retain the exact v1 digest. v2 explicitly includes every execution parameter.
            string payload = config.Schema == CharacterTacticalAssemblyConfiguration.ExecutableSchema
                ? JsonConvert.SerializeObject(new { Data = dataOnly, Execution = config.Accessories
                    .OrderBy(value => value.Reference.AccessoryId, StringComparer.Ordinal).ThenBy(value => value.Reference.Revision)
                    .Select(value => new { value.Reference.AccessoryId, value.Reference.Revision, value.Execution }).ToArray() })
                : JsonConvert.SerializeObject(dataOnly);
            var digest = RuntimeHashDigest.ComputeContentDigest(payload);
            return digest.Word0.ToString("x16") + digest.Word1.ToString("x16") + digest.Word2.ToString("x16") + digest.Word3.ToString("x16");
        }

        /// <remarks>LowFreqAlloc. Exact versions only; no compatibility whitelist is consulted.</remarks>
        internal static void ValidateConfiguration(CharacterTacticalAssemblyConfiguration config,
            CharacterTacticalContentCatalog catalog, ItemDefinitionRegistry definitions, Dictionary<int, string> characters)
        {
            if (config == null || (config.Schema != CharacterTacticalAssemblyConfiguration.CurrentSchema
                    && config.Schema != CharacterTacticalAssemblyConfiguration.ExecutableSchema) || config.Revision <= 0
                || string.IsNullOrWhiteSpace(config.ContentHash) || config.MainCards == null || config.Accessories == null || definitions == null)
                throw new JsonSerializationException("TACTICAL_ASSEMBLY_CONFIG_INVALID");
            var mainIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var main in config.MainCards)
            {
                if (main == null || !Valid(main.Card) || main.Card.InstanceId != 0 || catalog.ExactCard(main.Card)?.HasActionSlot != true
                    || !mainIds.Add(main.Card.CardId + "@" + main.Card.Revision))
                    throw new JsonSerializationException("TACTICAL_ASSEMBLY_MAIN_DEFINITION_INVALID");
                SlotCount(main.Quality);
            }
            var itemIds = new HashSet<int>(); var accessoryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var accessory in config.Accessories)
            {
                if (accessory == null || !Valid(accessory.Reference) || !itemIds.Add(accessory.ItemDefinitionId)
                    || !accessoryIds.Add(accessory.Reference.AccessoryId + "@" + accessory.Reference.Revision)
                    || characters?.ContainsKey(accessory.ItemDefinitionId) == true
                    || !definitions.TryGet(new ItemDefinitionId(accessory.ItemDefinitionId), out var item)
                    || item.Kind != ItemKind.Custom
                    || string.IsNullOrWhiteSpace(accessory.TriggerContractId) || string.IsNullOrWhiteSpace(accessory.FailureContractId)
                    || string.IsNullOrWhiteSpace(accessory.LifetimeContractId))
                    throw new JsonSerializationException("TACTICAL_ACCESSORY_DEFINITION_INVALID");
                SlotCount(accessory.Quality);
                if (config.Schema == CharacterTacticalAssemblyConfiguration.CurrentSchema ? accessory.Execution != null
                    : accessory.Execution?.IsValid != true)
                    throw new JsonSerializationException("TACTICAL_ACCESSORY_EXECUTION_SCHEMA_INVALID");
            }
            if (config.ContentHash != ConfigurationHash(config))
                throw new JsonSerializationException("TACTICAL_ASSEMBLY_CONFIG_HASH_INVALID");
        }

        /// <summary>Structural restore only. Missing live configuration never replaces or discards owned records.</summary>
        /// <remarks>LowFreqAlloc. Resolving exact authored content is a separate explicit configuration gate.</remarks>
        internal static void ValidateSave(CharacterTacticalPlayerState state)
        {
            bool enabled = state.Schema == CharacterTacticalPlayerState.CardAssemblySchema;
            if (enabled != (state.AccessoryItems != null) || !enabled && state.CharacterItems?.Any(
                value => value?.CardAcquisitions != null || value?.CardAssemblies != null) == true)
                throw new JsonSerializationException("TACTICAL_ASSEMBLY_SAVE_SCHEMA_INVALID");
            if (!enabled) return;
            if (state.CharacterItems.Any(value => value == null || value.Cards?.Any(card => card == null) == true
                || value.ActionGrants?.Any(action => action == null) == true))
                throw new JsonSerializationException("TACTICAL_ASSEMBLY_CHARACTER_INVALID");
            var ledger = new ItemAcquisitionLedger();
            new ItemAcquisitionSaveStateProvider(ledger).RestoreSaveState(state.Items.ToRuntimeState());
            var inventory = JsonConvert.DeserializeObject<InventorySaveState>(state.Items.Modules.Single(
                value => value.ModuleId == InventorySaveStateProvider.ModuleId).CustomState.PayloadJson);
            var records = new HashSet<int>();
            var requests = new HashSet<long>(state.CharacterItems.Select(value => value.AcquisitionRequestId));
            foreach (var role in state.CharacterItems)
                foreach (var action in role.ActionGrants ?? Enumerable.Empty<CharacterTacticalActionGrant>())
                    if (action.Source == "direct") requests.Add(action.RequestId);
            foreach (var accessory in state.AccessoryItems)
            {
                if (accessory == null || accessory.ItemInstanceId <= 0 || accessory.ItemDefinitionId <= 0
                    || !records.Add(accessory.ItemInstanceId) || !requests.Add(accessory.AcquisitionRequestId)
                    || !ledger.Contains(new ItemAcquisitionRequestId(accessory.AcquisitionRequestId)) || !Digest(accessory.AcquisitionFingerprint)
                    || !Valid(accessory.Reference) || state.CharacterItems.Any(value => value.ItemInstanceId == accessory.ItemInstanceId)
                    || inventory.Containers.SelectMany(value => value.Slots).Count(value => value.ItemInstanceId == accessory.ItemInstanceId
                        && value.ItemDefinitionId == accessory.ItemDefinitionId && value.Count == 1) != 1)
                    throw new JsonSerializationException("TACTICAL_ACCESSORY_SAVE_INVALID");
            }
            foreach (var role in state.CharacterItems)
            {
                var grantedCards = new HashSet<ulong>();
                foreach (var grant in role.CardAcquisitions ?? Enumerable.Empty<CharacterTacticalCardAcquisition>())
                    if (grant == null || role.Cards?.Any(card => card.InstanceId == grant.CardInstanceId) != true
                        || !grantedCards.Add(grant.CardInstanceId)
                        || !requests.Add(grant.RequestId) || !ledger.Contains(new ItemAcquisitionRequestId(grant.RequestId)) || !Digest(grant.RequestFingerprint))
                        throw new JsonSerializationException("TACTICAL_CARD_ACQUISITION_INVALID");
                var cards = new HashSet<ulong>();
                foreach (var assembly in role.CardAssemblies ?? Enumerable.Empty<CharacterTacticalCardAssembly>())
                {
                    if (assembly == null || assembly.Schema != CharacterTacticalCardAssembly.CurrentSchema || assembly.ConfigurationRevision <= 0
                        || string.IsNullOrWhiteSpace(assembly.ConfigurationHash) || !Valid(assembly.MainCard) || assembly.MainCard.InstanceId == 0
                        || !cards.Add(assembly.MainCard.InstanceId) || role.Cards?.Any(card => card.InstanceId == assembly.MainCard.InstanceId
                            && Same(card, assembly.MainCard)) != true || assembly.Accessories == null)
                        throw new JsonSerializationException("TACTICAL_ASSEMBLY_SAVE_INVALID");
                    var slots = new HashSet<int>();
                    foreach (var slot in assembly.Accessories)
                        if (slot == null || slot.SlotIndex < 0 || !slots.Add(slot.SlotIndex) || !Valid(slot.Reference)
                            || !state.AccessoryItems.Any(item => item.ItemInstanceId == slot.AccessoryItemInstanceId && Same(item.Reference, slot.Reference)))
                            throw new JsonSerializationException("TACTICAL_ASSEMBLY_SLOT_INVALID");
                }
            }
        }
    }
}
