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
    /// <summary>Reference-only tactical data. Physical tracks remain owned by CharacterAction.</summary>
    internal sealed class CharacterTacticalActionDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string ActionLibraryId { get; set; }
        public int ActionId { get; set; }
        // Resource/cooldown eligibility comes from the referenced action's existing requirements.
        public string[] CharacterStableIds { get; set; } = Array.Empty<string>();
        public string Source { get; set; }
    }

    internal sealed partial class CharacterTacticalCardDefinition
    {
        public string CardId { get; set; }
        public int Revision { get; set; }
        public string ContentHash { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string IconKey { get; set; } = string.Empty;
        public string[] Slots { get; set; } = Array.Empty<string>();
        [JsonIgnore] public bool HasActionSlot => Slots != null && Slots.Length > 0;
        [JsonIgnore] public string ActionSlotPath => HasActionSlot ? Slots[0] : string.Empty;
        [JsonIgnore] internal object PresentationData { get; set; }
        public int MotionCost { get; set; }
        public float CooldownSeconds { get; set; }
    }

    internal sealed class CharacterTacticalAcquisitionPool
    {
        public string CharacterStableId { get; set; }
        public string[] ActionIds { get; set; } = Array.Empty<string>();
        public CharacterTacticalCardReference[] Cards { get; set; } = Array.Empty<CharacterTacticalCardReference>();
    }

    internal sealed partial class CharacterTacticalContentCatalog
    {
        public string Schema { get; set; } = "mx.character-test.tactical-content.v1";
        public CharacterTacticalActionDefinition[] Actions { get; set; } = Array.Empty<CharacterTacticalActionDefinition>();
        [JsonIgnore] public CharacterTacticalCardDefinition[] Cards { get; set; } = Array.Empty<CharacterTacticalCardDefinition>();
        [JsonIgnore] public CharacterTacticalAcquisitionPool[] Acquisition { get; set; } = Array.Empty<CharacterTacticalAcquisitionPool>();
        // Explicit temporary battle rule, not a fabricated character attribute.
        public int InitialMotion { get; set; } = 50;

        internal void Validate()
        {
            if (Schema != "mx.character-test.tactical-content.v1" || Actions == null || Cards == null || Acquisition == null
                || InitialMotion < 0 || InitialMotion > 100) throw new JsonSerializationException("TACTICAL_CATALOG_INVALID");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var action in Actions)
                if (action == null || string.IsNullOrWhiteSpace(action.Id) || !ids.Add(action.Id) || action.ActionId <= 0
                    || string.IsNullOrWhiteSpace(action.ActionLibraryId)
                    || action.CharacterStableIds == null)
                    throw new JsonSerializationException("TACTICAL_ACTION_INVALID");
            ids.Clear();
            foreach (var card in Cards)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.CardId) || card.Revision <= 0
                    || !ids.Add(card.CardId + "@" + card.Revision) || card.Slots == null || card.MotionCost < 0
                    || card.Slots.Any(string.IsNullOrWhiteSpace) || card.Slots.Distinct(StringComparer.Ordinal).Count() != card.Slots.Length)
                    throw new JsonSerializationException("TACTICAL_CARD_INVALID");
            }
            ids.Clear();
            foreach (var pool in Acquisition)
                if (pool == null || string.IsNullOrWhiteSpace(pool.CharacterStableId) || !ids.Add(pool.CharacterStableId)
                    || pool.ActionIds == null || pool.Cards == null || pool.ActionIds.Any(id => Action(id) == null)
                    || pool.Cards.Any(card => Card(card) == null))
                    throw new JsonSerializationException("TACTICAL_ACQUISITION_INVALID");
        }

        internal CharacterTacticalActionDefinition Action(string id) => Array.Find(Actions, value => value.Id == id);
        // Legacy ta.* references and global action:* references address the same physical action.
        internal bool ContainsAction(IEnumerable<string> owned, string id)
        {
            var action = Action(id);
            return owned != null && owned.Any(value => value == id || action != null && Action(value)?.ActionId == action.ActionId);
        }
        internal CharacterTacticalCardDefinition Card(CharacterTacticalCardReference reference) => reference == null ? null
            : Array.Find(Cards, value => value.CardId == reference.CardId && value.Revision == reference.Revision
                && string.Equals(value.ContentHash ?? string.Empty, reference.ContentHash ?? string.Empty, StringComparison.Ordinal))
            ?? Array.Find(Cards, value => value.CardId == reference.CardId && value.Revision == reference.Revision)
            ?? Array.Find(Cards, value => value.CardId == reference.CardId);
        internal CharacterTacticalCardDefinition ExactCard(CharacterTacticalCardReference reference) => reference == null ? null
            : Array.Find(Cards, value => value.CardId == reference.CardId && value.Revision == reference.Revision
                && string.Equals(value.ContentHash ?? string.Empty, reference.ContentHash ?? string.Empty, StringComparison.Ordinal));
        internal CharacterTacticalCardDefinition Card(string cardId) => string.IsNullOrEmpty(cardId) ? null
            : Array.Find(Cards, value => value.CardId == cardId);
        internal CharacterTacticalAcquisitionPool Pool(string character) => Array.Find(Acquisition, value => value.CharacterStableId == character);
    }

    internal sealed class CharacterTacticalPlayerState
    {
        public const string ItemSchema = "mx.character-test.tactical-player.v3";
        public const string CharacterItemSchema = "mx.character-test.tactical-player.v4";
        public const string CharacterTacticalSchema = "mx.character-test.tactical-player.v5";
        public const string CardAssemblySchema = "mx.character-test.tactical-player.v6";
        public string Schema { get; set; } = "mx.character-test.tactical-player.v2";
        public List<CharacterTacticalDefaults> Characters { get; set; } = new List<CharacterTacticalDefaults>();
        public ulong NextCardInstanceId { get; set; } = 1;
        public bool LegacyImportCompleted { get; set; }
        public Dictionary<string, string[]> LegacyCardIds { get; set; } = new Dictionary<string, string[]>();
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public CharacterTacticalItemState Items { get; set; }
        // Opt-in only. Legacy Characters remain template-keyed until the later preparation migration.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<CharacterTacticalCharacterItemState> CharacterItems { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<CharacterTacticalAccessoryItemState> AccessoryItems { get; set; }
    }

    /// <summary>Explicit definition reference; configuration alone grants no ownership.</summary>
    internal sealed class CharacterTacticalCharacterItemDefinition
    {
        public int ItemDefinitionId { get; set; }
        public string CharacterStableId { get; set; }
    }

    /// <summary>Independent character data keyed by the existing Items instance allocator.</summary>
    internal sealed class CharacterTacticalCharacterItemState
    {
        public int ItemInstanceId { get; set; }
        public string CharacterStableId { get; set; }
        public string DisplayName { get; set; }
        public long Revision { get; set; }
        public long AcquisitionRequestId { get; set; }
        // Immutable acquisition payload digest. A rename must not change retry identity.
        public string AcquisitionFingerprint { get; set; }
        // Absent in v4. Loading old identities must not invent initial grants from today's defaults.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string TacticalSchema { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public List<CharacterTacticalActionGrant> ActionGrants { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public List<CharacterTacticalCardReference> Cards { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public List<CharacterTacticalSlotBinding> Bindings { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public List<CharacterTacticalCardAcquisition> CardAcquisitions { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public List<CharacterTacticalCardAssembly> CardAssemblies { get; set; }
    }

    /// <summary>Permanent grant provenance. The owned physical-action set is derived; this is not an action inventory item.</summary>
    internal sealed class CharacterTacticalActionGrant
    {
        public int ActionId { get; set; }
        public string TacticalActionId { get; set; }
        public long RequestId { get; set; }
        public string Source { get; set; }
        public int RuleVersion { get; set; } = 1;
        public string ContentFingerprint { get; set; }
        public string RequestFingerprint { get; set; }
    }

    internal sealed class CharacterTacticalAcquisitionConfig
    {
        public string Schema { get; set; } = "mx.character-test.tactical-acquisition.v1";
        public CharacterTacticalAcquisitionPool[] Characters { get; set; } = Array.Empty<CharacterTacticalAcquisitionPool>();
    }

    /// <summary>Ordinary inventory and its ledger live inside the existing player checkpoint; cards stay in Characters.</summary>
    internal sealed class CharacterTacticalItemState
    {
        public string Schema { get; set; } = "mx.character-test.items.v1";
        public long Frame { get; set; }
        public RuntimeModuleSaveState[] Modules { get; set; } = Array.Empty<RuntimeModuleSaveState>();
        public List<CharacterTacticalItemSettlement> Settlements { get; set; } = new();

        internal RuntimeSaveState ToRuntimeState() => new RuntimeSaveState(RuntimeSaveState.CurrentSchemaVersion,
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), string.Empty, string.Empty, string.Empty,
            Frame, null, null, Modules);

        internal static void Validate(CharacterTacticalItemState state)
        {
            if (state == null || state.Schema != "mx.character-test.items.v1" || state.Frame < 0
                || state.Modules == null || state.Modules.Length != 2 || state.Settlements == null
                || state.Modules.Any(module => module?.CustomState == null)
                || state.Modules.Count(module => module.ModuleId == InventorySaveStateProvider.ModuleId) != 1
                || state.Modules.Count(module => module.ModuleId == ItemAcquisitionSaveStateProvider.ModuleId) != 1)
                throw new JsonSerializationException("TACTICAL_ITEM_SAVE_INVALID");
            var ledger = new ItemAcquisitionLedger();
            if (!new ItemAcquisitionSaveStateProvider(ledger).RestoreSaveState(state.ToRuntimeState()).Success)
                throw new JsonSerializationException("TACTICAL_ITEM_LEDGER_INVALID");
            var settlementIds = new HashSet<long>();
            var requestIds = new HashSet<long>();
            foreach (var value in state.Settlements)
                if (value == null || value.SettlementId <= 0 || !settlementIds.Add(value.SettlementId)
                    || !requestIds.Add(value.RequestId) || !ledger.Contains(new ItemAcquisitionRequestId(value.RequestId)))
                    throw new JsonSerializationException("TACTICAL_ITEM_SETTLEMENT_INVALID");
            // Definitions are supplied explicitly at configuration; do not guess missing content on load.
            // Validate the independent inventory structure before allowing the card owner to publish it.
            var module = state.Modules.Single(value => value.ModuleId == InventorySaveStateProvider.ModuleId);
            if (module.SchemaVersion != InventorySaveState.CurrentSchemaVersion
                || module.CustomState.SchemaVersion != module.SchemaVersion
                || module.CustomState.TypeId != InventorySaveStateProvider.ModuleId)
                throw new JsonSerializationException("TACTICAL_ITEM_INVENTORY_SCHEMA_INVALID");
            var inventory = JsonConvert.DeserializeObject<InventorySaveState>(module.CustomState.PayloadJson);
            if (inventory == null || inventory.SchemaVersion != module.SchemaVersion || inventory.Containers == null
                || inventory.NextItemInstanceId < 1 || inventory.NextItemInstanceId > (long)int.MaxValue + 1)
                throw new JsonSerializationException("TACTICAL_ITEM_INVENTORY_INVALID");
            var containers = new HashSet<int>();
            var instances = new HashSet<int>();
            foreach (var container in inventory.Containers)
            {
                if (container == null || container.ContainerId <= 0 || !containers.Add(container.ContainerId)
                    || container.OwnerId <= 0 || container.SlotCount <= 0 || container.Slots == null)
                    throw new JsonSerializationException("TACTICAL_ITEM_CONTAINER_INVALID");
                var slots = new HashSet<int>();
                foreach (var slot in container.Slots)
                    if (slot == null || slot.SlotIndex < 0 || slot.SlotIndex >= container.SlotCount || !slots.Add(slot.SlotIndex)
                        || slot.ItemDefinitionId <= 0 || slot.Count <= 0 || slot.ItemInstanceId < 0
                        || slot.ItemInstanceId > 0 && (!instances.Add(slot.ItemInstanceId) || slot.ItemInstanceId >= inventory.NextItemInstanceId))
                        throw new JsonSerializationException("TACTICAL_ITEM_SLOT_INVALID");
            }
        }
    }

    internal sealed class CharacterTacticalItemSettlement
    {
        public long SettlementId { get; set; }
        public long RequestId { get; set; }
    }

    internal sealed class CharacterTacticalCardGrant
    {
        public string CharacterStableId { get; set; }
        public CharacterTacticalCardReference Card { get; set; }
    }
    /// <summary>Published role definitions and empty profile-local layouts. This configuration owns no instances.</summary>
    internal sealed class CharacterTacticalRoleInventoryConfig
    {
        public const string CurrentSchema = "mx.character-test.character-item-inventory.v1";
        public string Schema { get; set; }
        public string OwnerScope { get; set; }
        public int PlayerOwnerId { get; set; }
        public CharacterTacticalRoleDefinitionConfig[] Roles { get; set; }
        public CharacterTacticalRoleContainerConfig[] Containers { get; set; }

        // LowFreqAlloc. Use the existing Items registry/layout types, never allocate content IDs here.
        internal void Build(IEnumerable<string> knownTemplates, out ItemDefinitionRegistry definitions,
            out InventoryContainer[] containers, out CharacterTacticalCharacterItemDefinition[] mapping)
        {
            if (Schema != CurrentSchema || OwnerScope != "profile-local" || PlayerOwnerId <= 0
                || Roles == null || Roles.Length == 0 || Containers == null || Containers.Length == 0)
                throw new JsonSerializationException("TACTICAL_ROLE_INVENTORY_CONFIG_INVALID");
            var known = new HashSet<string>(knownTemplates ?? Array.Empty<string>(), StringComparer.Ordinal);
            definitions = new ItemDefinitionRegistry();
            mapping = new CharacterTacticalCharacterItemDefinition[Roles.Length];
            var templates = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Roles.Length; i++)
            {
                var role = Roles[i];
                if (role == null || role.ItemDefinitionId <= 0 || string.IsNullOrWhiteSpace(role.StableId)
                    || !known.Contains(role.CharacterStableId ?? string.Empty) || !templates.Add(role.CharacterStableId)
                    || role.Kind != "Custom" || role.MaxStackCount != 1
                    || string.IsNullOrWhiteSpace(role.NameKey) || string.IsNullOrWhiteSpace(role.DescriptionKey))
                    throw new JsonSerializationException("TACTICAL_ROLE_DEFINITION_CONFIG_INVALID_OR_UNREGISTERED");
                var definition = new ItemDefinition(new ItemDefinitionId(role.ItemDefinitionId), role.StableId,
                    new LocalizedTextKey(role.NameKey), new LocalizedTextKey(role.DescriptionKey), ItemKind.Custom,
                    new ItemTagSet(null), new ItemStackPolicy(role.MaxStackCount));
                if (!definitions.Register(definition).Success)
                    throw new JsonSerializationException("TACTICAL_ROLE_DEFINITION_CONFIG_DUPLICATE");
                mapping[i] = new CharacterTacticalCharacterItemDefinition {
                    ItemDefinitionId = role.ItemDefinitionId, CharacterStableId = role.CharacterStableId };
            }
            containers = new InventoryContainer[Containers.Length];
            var ids = new HashSet<int>(); var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Containers.Length; i++)
            {
                var row = Containers[i];
                if (row == null || row.ContainerId <= 0 || row.OwnerId <= 0 || row.Capacity <= 0
                    || row.Capacity > 1024 || string.IsNullOrWhiteSpace(row.StableId)
                    || !ids.Add(row.ContainerId) || !keys.Add(row.StableId))
                    throw new JsonSerializationException("TACTICAL_ROLE_CONTAINER_CONFIG_INVALID");
                containers[i] = new InventoryContainer(new InventoryContainerId(row.ContainerId),
                    new InventoryOwnerId(row.OwnerId), row.Capacity, row.StableId);
            }
            if (!Containers.Any(row => row.OwnerId == PlayerOwnerId))
                throw new JsonSerializationException("TACTICAL_ROLE_PLAYER_OWNER_NOT_CONFIGURED");
        }
    }

    internal sealed class CharacterTacticalRoleDefinitionConfig
    {
        public int ItemDefinitionId { get; set; }
        public string StableId { get; set; }
        public string CharacterStableId { get; set; }
        public string NameKey { get; set; }
        public string DescriptionKey { get; set; }
        public string Kind { get; set; }
        public int MaxStackCount { get; set; }
    }

    internal sealed class CharacterTacticalRoleContainerConfig
    {
        public int ContainerId { get; set; }
        public int OwnerId { get; set; }
        public string StableId { get; set; }
        public int Capacity { get; set; }
    }

    /// <summary>Explicit initial grants only. Existing profiles never execute this recipe.</summary>
    internal sealed class CharacterTacticalRoleNewGameConfig
    {
        public const string CurrentSchema = "mx.character-test.tactical-new-game.v2";
        public string Schema { get; set; }
        public CharacterTacticalDefaults[] Characters { get; set; }
        public CharacterTacticalRoleGrantConfig[] CharacterItemGrants { get; set; }
        public long PlayerControlledGrantRequestId { get; set; }

        internal void Validate(CharacterTacticalRoleInventoryConfig inventory,
            CharacterTacticalContentCatalog catalog, IEnumerable<string> knownTemplates)
        {
            if (Schema != CurrentSchema || Characters == null || Characters.Length == 0
                || CharacterItemGrants == null || CharacterItemGrants.Length == 0 || PlayerControlledGrantRequestId <= 0)
                throw new JsonSerializationException("TACTICAL_ROLE_NEW_GAME_CONFIG_INVALID");
            inventory.Build(knownTemplates, out _, out _, out _);
            var known = new HashSet<string>(knownTemplates ?? Array.Empty<string>(), StringComparer.Ordinal);
            var templates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var defaults in Characters)
                if (defaults == null || !defaults.TryValidate(out _) || !known.Contains(defaults.CharacterStableId)
                    || !templates.Add(defaults.CharacterStableId) || defaults.Cards.Count == 0
                    || defaults.ActionIds.Any(id => catalog.Action(id) == null
                        || !catalog.Action(id).CharacterStableIds.Contains(defaults.CharacterStableId))
                    || defaults.Cards.Any(card => card.InstanceId != 0 || string.IsNullOrWhiteSpace(card.ContentHash)
                        || catalog.ExactCard(card) == null)
                    || defaults.Bindings.Any(binding => binding.CardInstanceId != 0
                        || !defaults.Cards.Any(card => card.CardId == binding.CardId
                            && catalog.ExactCard(card).Slots.Contains(binding.SlotPath))
                        || !catalog.ContainsAction(defaults.ActionIds, binding.TacticalActionId)))
                    throw new JsonSerializationException("TACTICAL_ROLE_NEW_GAME_RECIPE_NOT_PUBLISHED_OR_INVALID");
            var requests = new HashSet<long>(); var counts = new Dictionary<int, int>();
            foreach (var grant in CharacterItemGrants)
            {
                var role = grant == null ? null : inventory.Roles.SingleOrDefault(row => row.StableId == grant.RoleStableId);
                var container = grant == null ? null : inventory.Containers.SingleOrDefault(row => row.ContainerId == grant.ContainerId);
                if (grant == null || grant.RequestId <= 0 || !requests.Add(grant.RequestId)
                    || role == null || !templates.Contains(role.CharacterStableId) || container == null
                    || grant.OwnerId != container.OwnerId || string.IsNullOrWhiteSpace(grant.DisplayName)
                    || grant.Deployment != null && (!TacticalBattlefieldLayout.ContainsPlacement(
                        new UnityEngine.Vector3(grant.Deployment.X, grant.Deployment.Y, grant.Deployment.Z))
                        || float.IsNaN(grant.Deployment.Y) || float.IsInfinity(grant.Deployment.Y)
                        || float.IsNaN(grant.Deployment.YawDegrees) || float.IsInfinity(grant.Deployment.YawDegrees)
                        || grant.Deployment.TeamId <= 0))
                    throw new JsonSerializationException("TACTICAL_ROLE_NEW_GAME_GRANT_INVALID");
                counts.TryGetValue(grant.ContainerId, out int count); counts[grant.ContainerId] = count + 1;
                if (count + 1 > container.Capacity)
                    throw new JsonSerializationException("TACTICAL_ROLE_NEW_GAME_CAPACITY_INSUFFICIENT");
            }
            var controlled = CharacterItemGrants.SingleOrDefault(row => row.RequestId == PlayerControlledGrantRequestId);
            if (controlled?.Deployment == null || controlled.OwnerId != inventory.PlayerOwnerId)
                throw new JsonSerializationException("TACTICAL_ROLE_NEW_GAME_CONTROLLED_OWNER_INVALID");
        }
    }

    internal sealed class CharacterTacticalRoleGrantConfig
    {
        // A profile-local, explicit idempotency key. Not a definition, item instance, peer or frame ID.
        public long RequestId { get; set; }
        public string RoleStableId { get; set; }
        public int ContainerId { get; set; }
        public int OwnerId { get; set; }
        public string DisplayName { get; set; }
        public CharacterTacticalRoleDeploymentConfig Deployment { get; set; }
    }

    internal sealed class CharacterTacticalRoleDeploymentConfig
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float YawDegrees { get; set; }
        public int TeamId { get; set; }
    }

    /// <summary>One ordered bootstrap tick against a detached new-profile candidate, before aggregate activation.</summary>
    internal sealed class CharacterTacticalRoleNewGameBootstrap : IRuntimeModule
    {
        private readonly CharacterTacticalPlayerService _player;
        private readonly CharacterTacticalRoleInventoryConfig _inventory;
        private readonly CharacterTacticalRoleNewGameConfig _recipe;
        private readonly Dictionary<long, ItemInstanceId> _instances = new();
        private bool _executed;
        private CharacterTacticalRoleNewGameBootstrap(CharacterTacticalPlayerService player,
            CharacterTacticalRoleInventoryConfig inventory, CharacterTacticalRoleNewGameConfig recipe)
        { _player = player; _inventory = inventory; _recipe = recipe; }
        internal static IReadOnlyDictionary<long, ItemInstanceId> Execute(CharacterTacticalPlayerService player,
            CharacterTacticalRoleInventoryConfig inventory, CharacterTacticalRoleNewGameConfig recipe)
        {
            if (player == null || player.HasCharacterItemProgress || player.LastItemTransactionFrame >= 0
                || JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(player.CaptureProgressJson()).Characters.Count != 0)
                throw new InvalidOperationException("TACTICAL_ROLE_BOOTSTRAP_REQUIRES_EMPTY_NEW_PROFILE");
            var module = new CharacterTacticalRoleNewGameBootstrap(player, inventory, recipe);
            using var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.FailFast });
            host.RegisterModule(module); host.Initialize(); host.Start();
            // The existing RuntimeHost supplies the Simulation context. No UI creates a fake tick.
            host.Tick(0, 0d, 0d);
            if (!host.LastTickAttemptSucceeded || module._instances.Count != recipe.CharacterItemGrants.Length)
                throw new InvalidOperationException("TACTICAL_ROLE_NEW_GAME_BOOTSTRAP_INCOMPLETE");
            return new System.Collections.ObjectModel.ReadOnlyDictionary<long, ItemInstanceId>(module._instances);
        }
        public string ModuleId => "character-test.new-game-role-grants";
        public int Priority => 0;
        public RuntimeTickStage TickStage => RuntimeTickStage.Simulation;
        public void Initialize(RuntimeHostContext context) { }
        public void Start(RuntimeHostContext context) { }
        // LowFreqAlloc: this composition-owned module runs once before any candidate is published.
        public void Tick(RuntimeTickContext context)
        {
            if (_executed) throw new InvalidOperationException("TACTICAL_ROLE_NEW_GAME_BOOTSTRAP_ALREADY_RUN");
            _executed = true;
            foreach (var grant in _recipe.CharacterItemGrants.OrderBy(row => row.RequestId))
            {
                var role = _inventory.Roles.Single(row => row.StableId == grant.RoleStableId);
                var result = _player.AcquireCharacterItemAtTick(new ItemAcquisitionRequest(
                    new ItemAcquisitionRequestId(grant.RequestId), ItemAcquisitionSourceKind.DirectGrant, null, null),
                    new InventoryContainerId(grant.ContainerId), new InventoryOwnerId(grant.OwnerId),
                    new ItemDefinitionId(role.ItemDefinitionId), grant.DisplayName,
                    _player.Revision, context.FrameIndex, context, out var instance, out string issue);
                if (!result.Success) throw new InvalidOperationException("TACTICAL_ROLE_NEW_GAME_GRANT_FAILED:" + result.Status + ":" + issue);
                _instances.Add(grant.RequestId, instance);
            }
        }
        public void Stop(RuntimeHostContext context) { }
        public void Dispose() { }
    }

    /// <summary>One low-frequency preparation edit, consumed only by the actual application Simulation host.</summary>
    internal sealed class CharacterTacticalRoleBindingQueue
    {
        private readonly CharacterTacticalPlayerService _player;
        private readonly TacticalBattlePreparationService _preparation;
        private readonly InventoryOwnerId _owner;
        private Action<RuntimeTickContext> _pending;
        private Action<bool, string> _completion;
        private bool _draining;
        internal CharacterTacticalRoleBindingQueue(CharacterTacticalPlayerService player,
            TacticalBattlePreparationService preparation, InventoryOwnerId owner)
        { _player = player; _preparation = preparation; _owner = owner; }
        internal int PendingCount => _pending == null ? 0 : 1;
        internal bool Matches(CharacterTacticalPlayerService player, TacticalBattlePreparationService preparation,
            InventoryOwnerId owner) => ReferenceEquals(_player, player) && ReferenceEquals(_preparation, preparation) && _owner.Equals(owner);
        internal bool Enqueue(string entityId, ItemInstanceId item, InventoryOwnerId owner, ulong card, string slot,
            string action, long roleRevision, long playerRevision, long preparationRevision, long catalogRevision,
            Func<bool> isCurrent, Action<bool, string> completion, out string issue)
        {
            issue = string.Empty;
            if (_pending != null || _draining || !_owner.IsValid || !_owner.Equals(owner) || isCurrent == null
                || completion == null || !isCurrent()) { issue = "装配入口已变化或仍有待处理命令。"; return false; }
            _completion = completion;
            _pending = context => {
                bool success = false; string resultIssue = string.Empty;
                var entity = _preparation.Find(entityId);
                if (!isCurrent() || _preparation.Revision != preparationRevision || _player.Revision != playerRevision
                    || _player.CatalogRevision != catalogRevision || entity?.CharacterItemInstanceId != item.Value
                    || entity.InventoryOwnerId != owner.Value || entity.CharacterItemRevision != roleRevision)
                    resultIssue = "角色、进度、卡库或整备版本已变化，请重新选择。";
                else if (context.FrameIndex < _player.LastItemTransactionFrame)
                    resultIssue = "装配逻辑帧早于已保存进度。";
                else if (string.IsNullOrEmpty(action))
                    success = _player.UnbindCharacterActionAtTick(item, owner, card, slot, roleRevision,
                        playerRevision, context.FrameIndex, context, out resultIssue);
                else success = _player.BindCharacterActionAtTick(item, owner, card, slot, action, roleRevision,
                    playerRevision, context.FrameIndex, context, out resultIssue);
                _completion(success, resultIssue);
            };
            return true;
        }
        internal void Drain(RuntimeTickContext context, bool preparationActive)
        {
            if (context.Stage != RuntimeTickStage.Simulation) throw new InvalidOperationException("TACTICAL_ROLE_EDIT_REQUIRES_SIMULATION");
            if (!preparationActive) { Cancel("已离开整备，装配请求未执行。"); return; }
            if (_pending == null) return;
            var edit = _pending; _pending = null; _draining = true;
            try { edit(context); }
            finally { _draining = false; _completion = null; }
        }
        internal void Cancel(string issue)
        {
            if (_pending == null) return;
            var completion = _completion; _pending = null; _completion = null;
            completion?.Invoke(false, issue);
        }
    }

    internal sealed class CharacterTacticalRoleBindingRuntimeModule : RuntimeModule
    {
        private readonly Func<CharacterTacticalRoleBindingQueue> _current;
        private readonly Func<bool> _preparationActive;
        internal CharacterTacticalRoleBindingRuntimeModule(Func<CharacterTacticalRoleBindingQueue> current,
            Func<bool> preparationActive) : base("character-test.preparation-role-bindings", RuntimeTickStage.Simulation, 0)
        { _current = current; _preparationActive = preparationActive; }
        public override void Tick(RuntimeTickContext context) => _current()?.Drain(context, _preparationActive());
        public override void Dispose() => _current()?.Cancel("整备会话已关闭。");
    }

}
