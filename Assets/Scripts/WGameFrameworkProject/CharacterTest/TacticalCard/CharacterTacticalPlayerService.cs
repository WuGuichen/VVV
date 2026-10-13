using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MxFramework.CharacterApplication;
using MxFramework.Items;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal interface ICharacterTacticalStateStore
    {
        Task<string> LoadAsync();
        Task SaveAsync(string json);
        Task ResetAsync(string json);
    }

    /// <summary>Single owner of character inventory and card bindings. All mutation runs on the application thread.</summary>
    internal sealed partial class CharacterTacticalPlayerService
    {
        private readonly ICharacterTacticalStateStore _store;
        private readonly CharacterTacticalContentCatalog _catalog;
        private readonly Dictionary<string, CharacterTacticalDefaults> _defaults;
        private CharacterTacticalPlayerState _state;
        private Task<bool> _saving;
        private long _savedRevision;
        private bool _resetting;
        private ItemDefinitionRegistry _itemDefinitions;
        private InventoryContainer[] _itemContainers;
        private Dictionary<int, string> _characterItemDefinitions;
        private Dictionary<string, HashSet<string>> _usableCards = new(StringComparer.Ordinal);
        internal long Revision { get; private set; }
        internal bool HasUnsavedChanges => Revision != _savedRevision;
        internal string SaveIssue { get; private set; } = string.Empty;
        internal CharacterTacticalContentCatalog Catalog => _catalog;
        internal string CaptureProgressJson() => JsonConvert.SerializeObject(_state);
        internal bool HasImportedLegacyProgress => _state?.LegacyCardIds?.Count > 0;
        internal long CatalogRevision { get; private set; }
        /// <summary>Resume the authoritative item command clock at this frame or later after loading a checkpoint.</summary>
        internal long LastItemTransactionFrame => _state?.Items?.Frame ?? -1;
        internal bool HasCharacterItemProgress => _state?.CharacterItems != null;
        internal bool UsesCharacterItemTacticalProgress => _state?.Schema == CharacterTacticalPlayerState.CharacterTacticalSchema
            || _state?.Schema == CharacterTacticalPlayerState.CardAssemblySchema;
        internal event Action Changed;

        /// <remarks>LowFreqAlloc. Stages definitions before replacing the catalog; never repairs or saves ownership.</remarks>
        internal bool TryRefreshPublishedCards(CharacterTacticalCardDefinition[] cards, out int invalidOwnedReferences, out string issue)
        {
            invalidOwnedReferences = 0;
            if (MutationBlocked) { issue = "领奖或重置正在处理中。"; return false; }
            CharacterTacticalCardDefinition[] staged;
            Dictionary<string, HashSet<string>> availability;
            try
            {
                if (cards == null) throw new ArgumentException("发布卡库为空。");
                var candidate = Clone(_catalog);
                // Retained acquisition references may become stale; refresh must report, not rewrite them.
                candidate.Acquisition = Array.Empty<CharacterTacticalAcquisitionPool>();
                candidate.Cards = Clone(cards);
                candidate.Validate();
                if (_assemblyConfiguration != null)
                    CharacterTacticalAssemblyRules.ValidateConfiguration(_assemblyConfiguration, candidate, _itemDefinitions, _characterItemDefinitions);
                staged = candidate.Cards;
                invalidOwnedReferences = (UsesCharacterItemTacticalProgress ? 0 : _state.Characters.Sum(character =>
                    character.Cards.Count(reference => candidate.ExactCard(reference) == null)))
                    + (_state.CharacterItems?.Sum(character => character.Cards?.Count(reference => candidate.ExactCard(reference) == null) ?? 0) ?? 0);
                availability = BuildAvailability(candidate);
            }
            catch (Exception error)
            {
                issue = "刷新卡库失败，保留原卡库：" + error.Message;
                return false;
            }
            _catalog.Cards = staged;
            _usableCards = availability;
            CatalogRevision++;
            issue = string.Empty;
            return true;
        }

        private CharacterTacticalPlayerService(CharacterTacticalContentCatalog catalog,
            IEnumerable<CharacterTacticalDefaults> defaults, ICharacterTacticalStateStore store)
        {
            _catalog = Clone(catalog ?? throw new ArgumentNullException(nameof(catalog)));
            _catalog.Acquisition = Clone(catalog.Acquisition);
            _catalog.Cards = Clone(catalog.Cards);
            _catalog.Validate();
            _defaults = new Dictionary<string, CharacterTacticalDefaults>(StringComparer.Ordinal);
            foreach (var value in defaults)
            {
                if (value == null || !value.TryValidate(out _)) throw new ArgumentException("Invalid tactical defaults.");
                _defaults.Add(value.CharacterStableId, Clone(value));
            }
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        internal static async Task<CharacterTacticalPlayerService> OpenAsync(CharacterTacticalContentCatalog catalog,
            IEnumerable<CharacterTacticalDefaults> defaults, ICharacterTacticalStateStore store, bool addMissingDefaults = true, bool instantiateTemplateDefaults = true)
        {
            var service = new CharacterTacticalPlayerService(catalog, defaults, store);
            string json = await store.LoadAsync();
            service._state = json == null ? new CharacterTacticalPlayerState() : JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(json);
            bool migrated = service.MigrateV1State();
            service.ValidateState();
            bool added = false;
            foreach (var item in service._defaults)
                if (instantiateTemplateDefaults && service._state.CharacterItems == null && (json == null || addMissingDefaults) && service.Find(item.Key) == null)
                { service._state.Characters.Add(service.InstantiateDefaults(item.Value, service._state)); added = true; }
            bool repaired = service._state.CharacterItems == null && service.RepairRepublishedDefaultReferences();
            bool removedLegacyBindings = service._state.CharacterItems == null && service.RemoveBindingsWithoutPublishedSlots();
            service.RebuildAvailability();
            if (added || repaired || removedLegacyBindings || migrated || json == null) { service.Revision++; await service.FlushAsync(); }
            return service;
        }

        private bool MigrateV1State()
        {
            if (_state == null) return false;
            bool legacyShape = _state.Schema == "mx.character-test.tactical-player.v1"
                || (_state.Schema == "mx.character-test.tactical-player.v2"
                    && _state.Characters.Any(character => character.Cards.Any(card => card.InstanceId == 0)));
            if (!legacyShape) return false;
            bool hasUnassigned = false, hasAssigned = false;
            var assigned = new HashSet<ulong>();
            foreach (var character in _state.Characters)
            {
                if (character == null || !character.TryValidate(out _))
                    throw new JsonSerializationException("TACTICAL_SAVE_CHARACTER_INVALID");
                foreach (var card in character.Cards)
                {
                    if (card.InstanceId == 0) hasUnassigned = true;
                    else
                    {
                        hasAssigned = true;
                        if (!assigned.Add(card.InstanceId))
                            throw new JsonSerializationException("TACTICAL_SAVE_CARD_INSTANCE_INVALID");
                    }
                }
            }
            if (hasUnassigned && hasAssigned)
                throw new JsonSerializationException("TACTICAL_SAVE_INSTANCE_MIGRATION_AMBIGUOUS");
            if (!hasUnassigned)
            {
                _state.Schema = "mx.character-test.tactical-player.v2";
                return true;
            }
            _state.NextCardInstanceId = Math.Max(1UL, _state.NextCardInstanceId);
            foreach (CharacterTacticalDefaults character in _state.Characters)
            {
                var firstByCard = new Dictionary<string, ulong>(StringComparer.Ordinal);
                foreach (CharacterTacticalCardReference card in character.Cards)
                {
                    card.InstanceId = AllocateInstanceId();
                    if (!firstByCard.ContainsKey(card.CardId)) firstByCard.Add(card.CardId, card.InstanceId);
                }
                foreach (CharacterTacticalSlotBinding binding in character.Bindings)
                    if (binding.CardInstanceId == 0 && firstByCard.TryGetValue(binding.CardId ?? string.Empty, out ulong id))
                        binding.CardInstanceId = id;
            }
            _state.Schema = "mx.character-test.tactical-player.v2";
            return true;
        }

        private CharacterTacticalDefaults InstantiateDefaults(CharacterTacticalDefaults source, CharacterTacticalPlayerState destination)
        {
            CharacterTacticalDefaults result = Clone(source);
            var firstByCard = new Dictionary<string, ulong>(StringComparer.Ordinal);
            var bySourceInstance = new Dictionary<ulong, ulong>();
            foreach (CharacterTacticalCardReference card in result.Cards)
            {
                ulong sourceInstance = card.InstanceId;
                card.InstanceId = AllocateInstanceId(destination);
                if (sourceInstance != 0) bySourceInstance.Add(sourceInstance, card.InstanceId);
                if (!firstByCard.ContainsKey(card.CardId)) firstByCard.Add(card.CardId, card.InstanceId);
            }
            foreach (CharacterTacticalSlotBinding binding in result.Bindings)
            {
                if (binding.CardInstanceId != 0 && bySourceInstance.TryGetValue(binding.CardInstanceId, out ulong instance))
                    binding.CardInstanceId = instance;
                else if (firstByCard.TryGetValue(binding.CardId ?? string.Empty, out ulong id)) binding.CardInstanceId = id;
            }
            return result;
        }

        private ulong AllocateInstanceId() => AllocateInstanceId(_state);

        private static ulong AllocateInstanceId(CharacterTacticalPlayerState state)
        {
            if (state.NextCardInstanceId == 0 || state.NextCardInstanceId == ulong.MaxValue)
                throw new InvalidOperationException("TACTICAL_DECK_INSTANCE_ID_EXHAUSTED");
            return state.NextCardInstanceId++;
        }

        /// <summary>
        /// Repairs development saves created before a default card was republished without a revision bump.
        /// Only cards still present at the same default id/revision are migrated; removed cards are never restored
        /// and acquired non-default cards are never replaced.
        /// </summary>
        private bool RepairRepublishedDefaultReferences()
        {
            bool changed = false;
            foreach (var entry in _defaults)
            {
                CharacterTacticalDefaults state = Find(entry.Key);
                if (state == null) continue;
                foreach (var card in state.Cards)
                {
                    CharacterTacticalCardReference expected = entry.Value.Cards.Find(value => value.CardId == card.CardId
                        && value.Revision == card.Revision);
                    if (expected == null || string.Equals(card.ContentHash, expected.ContentHash, StringComparison.Ordinal)
                        || _catalog.ExactCard(expected) == null) continue;
                    card.ContentHash = expected.ContentHash;
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>
        /// Removes obsolete local bindings for cards whose published tactical source exposes no player binding slots.
        /// Unknown cards are preserved so a missing catalog never destroys recoverable player data.
        /// </summary>
        private bool RemoveBindingsWithoutPublishedSlots()
        {
            bool changed = false;
            foreach (CharacterTacticalDefaults state in _state.Characters)
            {
                for (int i = state.Bindings.Count - 1; i >= 0; i--)
                {
                    CharacterTacticalSlotBinding binding = state.Bindings[i];
                    CharacterTacticalCardReference reference = state.Cards.Find(card => card.InstanceId == binding.CardInstanceId);
                    CharacterTacticalCardDefinition card = _catalog.ExactCard(reference) ?? _catalog.Card(reference);
                    if (card == null || card.Slots == null || card.Slots.Length != 0) continue;
                    state.Bindings.RemoveAt(i);
                    changed = true;
                }
            }
            return changed;
        }

        // Returns an owned copy for low-frequency UI/compilation. No caller can edit authority by aliasing a collection.
        internal CharacterTacticalDefaults Read(string character) => UsesCharacterItemTacticalProgress ? null : Clone(Find(character));

        internal void ImportLegacyCards(IReadOnlyDictionary<string, string[]> cards)
        {
            if (UsesCharacterItemTacticalProgress) throw new InvalidOperationException("角色实例进度不能通过模板导入写入。");
            if (_state.LegacyImportCompleted) return;
            foreach (var pair in cards)
            {
                var character = Find(pair.Key);
                if (character == null) continue;
                _state.LegacyCardIds[pair.Key] = (string[])pair.Value.Clone();
                character.Cards.Clear(); character.Bindings.Clear();
                foreach (string id in pair.Value.Distinct())
                {
                    var versions = _catalog.Cards.Where(c => c.CardId == id).ToArray();
                    if (versions.Length == 1)
                    {
                        var card = versions[0];
                        character.Cards.Add(new CharacterTacticalCardReference { CardId = id, Revision = card.Revision, ContentHash = card.ContentHash,
                            InstanceId = AllocateInstanceId() });
                    }
                    else character.Diagnostics.Add("旧存档行为卡待修复：" + id + "；未猜测版本或动作槽绑定。");
                }
            }
            _state.LegacyImportCompleted = true; Touch();
        }
        internal bool OwnsCard(string character, string card) => Find(character)?.Cards.Any(c => c.CardId == card) == true;
        internal CharacterTacticalCardReference FindCard(string character, string card) => Clone(Find(character)?.Cards.Find(c => c.CardId == card));
        internal bool IsCardUsable(string character, string card) => !MutationBlocked && character != null
            && _usableCards.TryGetValue(character, out var cards) && cards.Contains(card);

        private void RebuildAvailability() => _usableCards = BuildAvailability(_catalog);

        private Dictionary<string, HashSet<string>> BuildAvailability(CharacterTacticalContentCatalog catalog)
            => BuildAvailability(catalog, _state);

        private static Dictionary<string, HashSet<string>> BuildAvailability(CharacterTacticalContentCatalog catalog,
            CharacterTacticalPlayerState state)
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var character in state.Characters)
            {
                var usable = new HashSet<string>(StringComparer.Ordinal);
                foreach (var card in character.Cards)
                {
                    CharacterTacticalCardDefinition definition = catalog.ExactCard(card);
                    if (definition == null) continue;
                    bool bound = definition.Slots.All(slot => {
                        var binding = character.Bindings.Find(value => value.CardInstanceId == card.InstanceId && value.SlotPath == slot);
                        var action = binding == null ? null : catalog.Action(binding.TacticalActionId);
                        return action != null && catalog.ContainsAction(character.ActionIds, action.Id)
                            && action.CharacterStableIds.Contains(character.CharacterStableId);
                    });
                    if (bound) usable.Add(card.CardId);
                }
                result.Add(character.CharacterStableId, usable);
            }
            return result;
        }

        internal bool TryGrantAction(string character, string id, out string issue)
        {
            if (!Ready(character, out var state, out issue)) return false;
            var action = _catalog.Action(id);
            if (action == null || !action.CharacterStableIds.Contains(character)
                || !_catalog.ContainsAction(_catalog.Pool(character)?.ActionIds, id)) return Fail("此动作不在该角色允许拥有的分类范围内。", out issue);
            if (_catalog.ContainsAction(state.ActionIds, id)) return true;
            state.ActionIds.Add(id); Touch(); return true;
        }

        internal bool TryGrantCard(string character, CharacterTacticalCardReference card, out string issue)
        {
            if (!Ready(character, out var state, out issue)) return false;
            if (_catalog.Card(card) == null || _catalog.Pool(character)?.Cards.Any(c => Same(c, card)) != true)
                return Fail("该角色的获取配置中没有此版本的行为卡。", out issue);
            return GrantCard(state, card, out issue);
        }

        /// <summary>Adds one Published tactical card to the local CharacterTest save for explicit slice testing.</summary>
        /// <remarks>LowFreqAlloc: developer-facing authoring/test command; does not change acquisition or character defaults.</remarks>
        internal bool TryGrantCardForTesting(string character, CharacterTacticalCardReference card, out string issue)
        {
            if (!Ready(character, out var state, out issue)) return false;
            if (_catalog.Card(card) == null)
                return Fail("正式行为卡版本不可用，不能加入测试牌库。", out issue);
            return GrantCard(state, card, out issue);
        }

        private bool GrantCard(CharacterTacticalDefaults state, CharacterTacticalCardReference card, out string issue)
        {
            return AddDeckEntry(state.CharacterStableId, card, out _, out issue);
        }

        internal bool AddDeckEntry(string character, CharacterTacticalCardReference card, out ulong instanceId, out string issue)
        {
            instanceId = 0;
            if (!Ready(character, out var state, out issue)) return false;
            CharacterTacticalCardDefinition definition = _catalog.ExactCard(card);
            if (definition == null) return Fail("正式行为卡版本不可用，不能加入卡组。", out issue);
            instanceId = AppendDeckEntry(_state, state, card); Touch(); issue = string.Empty; return true;
        }

        private static ulong AppendDeckEntry(CharacterTacticalPlayerState owner, CharacterTacticalDefaults character,
            CharacterTacticalCardReference card)
        {
            CharacterTacticalCardReference entry = Clone(card);
            entry.InstanceId = AllocateInstanceId(owner);
            character.Cards.Add(entry);
            return entry.InstanceId;
        }

        internal bool RemoveDeckEntry(string character, ulong instanceId, out string issue)
        {
            if (!Ready(character, out var state, out issue)) return false;
            if (state.Cards.RemoveAll(card => card.InstanceId == instanceId) == 0) return true;
            state.Bindings.RemoveAll(binding => binding.CardInstanceId == instanceId); Touch(); return true;
        }

        internal bool MoveDeckEntry(string character, ulong instanceId, int targetIndex, out string issue)
        {
            if (!Ready(character, out var state, out issue)) return false;
            int sourceIndex = state.Cards.FindIndex(card => card.InstanceId == instanceId);
            if (sourceIndex < 0 || targetIndex < 0 || targetIndex >= state.Cards.Count)
                return Fail("卡牌实例或目标位置无效。", out issue);
            if (sourceIndex == targetIndex) return true;
            CharacterTacticalCardReference entry = state.Cards[sourceIndex]; state.Cards.RemoveAt(sourceIndex);
            state.Cards.Insert(targetIndex, entry); Touch(); return true;
        }

        internal bool TryBind(string character, string cardId, string slot, string actionId, out string issue)
        {
            if (!Ready(character, out var state, out issue)) return false;
            CharacterTacticalCardReference entry = state.Cards.Find(c => c.CardId == cardId);
            return entry != null && TryBind(character, entry.InstanceId, slot, actionId, out issue);
        }

        internal bool TryBind(string character, ulong instanceId, string slot, string actionId, out string issue)
        {
            if (!Ready(character, out var state, out issue)) return false;
            CharacterTacticalCardReference entry = state.Cards.Find(c => c.InstanceId == instanceId);
            var card = _catalog.Card(entry);
            bool slotValid = card != null && card.Slots.Contains(slot);
            if (card == null || !slotValid) return Fail("行为卡或槽位引用不可用。", out issue);
            var action = _catalog.Action(actionId);
            if (!_catalog.ContainsAction(state.ActionIds, actionId) || action == null || !action.CharacterStableIds.Contains(character))
                return Fail("角色没有此可用战术动作。", out issue);
            var binding = state.Bindings.Find(b => b.CardInstanceId == instanceId && b.SlotPath == slot);
            if (binding != null && binding.TacticalActionId == actionId) return true;
            if (binding == null) state.Bindings.Add(new CharacterTacticalSlotBinding { CardInstanceId = instanceId,
                CardId = entry.CardId, SlotPath = slot, TacticalActionId = actionId });
            else binding.TacticalActionId = actionId;
            Touch(); return true;
        }

        internal bool TryUnbind(string character, string card, string slot, out string issue)
        {
            if (!Ready(character, out var state, out issue)) return false;
            if (state.Bindings.RemoveAll(b => b.CardId == card && b.SlotPath == slot) > 0) Touch();
            return true;
        }

        internal bool TryUnbind(string character, ulong instanceId, string slot, out string issue)
        {
            if (!Ready(character, out var state, out issue)) return false;
            if (state.Bindings.RemoveAll(b => b.CardInstanceId == instanceId && b.SlotPath == slot) > 0) Touch();
            return true;
        }

        internal string[] References(string character, string action) => Find(character)?.Bindings
            .Where(b => _catalog.ContainsAction(new[] { b.TacticalActionId }, action)).Select(b => b.CardId + " / " + b.SlotPath).ToArray() ?? Array.Empty<string>();

        internal bool TryRemoveAction(string character, string action, long expectedRevision, bool detachReferences, out string issue)
        {
            if (!Ready(character, out var state, out issue)) return false;
            if (expectedRevision != Revision) return Fail("清单已变化，请重新确认引用。", out issue);
            if (!detachReferences && state.Bindings.Any(b => _catalog.ContainsAction(new[] { b.TacticalActionId }, action)))
                return Fail("此动作被行为卡引用；确认后可一并解除引用并移除。", out issue);
            if (!_catalog.ContainsAction(state.ActionIds, action)) return true;
            state.Bindings.RemoveAll(b => _catalog.ContainsAction(new[] { b.TacticalActionId }, action));
            state.ActionIds.RemoveAll(id => _catalog.ContainsAction(new[] { id }, action)); Touch(); return true;
        }

        internal bool TryRemoveCard(string character, string card, out string issue)
        {
            if (!Ready(character, out var state, out issue)) return false;
            if (state.Cards.RemoveAll(c => c.CardId == card) == 0) return true;
            state.Bindings.RemoveAll(b => b.CardId == card); Touch(); return true;
        }

        internal bool TryGetBindings(string character, string cardId,
            out Dictionary<string, CharacterTacticalActionDefinition> bindings, out string issue)
        {
            bindings = new Dictionary<string, CharacterTacticalActionDefinition>(StringComparer.Ordinal);
            if (!Ready(character, out var state, out issue)) return false;
            CharacterTacticalCardReference entry = state.Cards.Find(c => c.CardId == cardId);
            if (entry == null) return Fail("行为卡未持有或正式版本已失效。", out issue);
            return TryGetBindings(character, entry.InstanceId, out bindings, out issue);
        }

        internal bool TryGetBindings(string character, ulong instanceId,
            out Dictionary<string, CharacterTacticalActionDefinition> bindings, out string issue)
            => TryGetBindings(character, instanceId, _catalog, out bindings, out issue);

        private bool TryGetBindings(string character, ulong instanceId, CharacterTacticalContentCatalog catalog,
            out Dictionary<string, CharacterTacticalActionDefinition> bindings, out string issue)
        {
            bindings = new Dictionary<string, CharacterTacticalActionDefinition>(StringComparer.Ordinal);
            if (!Ready(character, out var state, out issue)) return false;
            CharacterTacticalCardReference entry = state.Cards.Find(c => c.InstanceId == instanceId);
            var card = catalog.Card(entry);
            if (card == null) return Fail("行为卡未持有或正式版本已失效。", out issue);
            var slots = card.Slots;
            foreach (string slot in slots)
            {
                var binding = state.Bindings.Find(b => b.CardInstanceId == instanceId && b.SlotPath == slot);
                var action = binding == null ? null : catalog.Action(binding.TacticalActionId);
            if (action == null || !catalog.ContainsAction(state.ActionIds, action.Id) || !action.CharacterStableIds.Contains(character))
                    return Fail("槽位待装配：" + slot, out issue);
                bindings.Add(slot, Clone(action));
            }
            return true;
        }

        /// <summary>Configures ordinary item content without minting ownership or rewriting a v2 save.</summary>
        /// <remarks>LowFreqAlloc. Empty layouts only; initial grants must pass through the transaction boundary.</remarks>
        internal bool ConfigureItemInventory(ItemDefinitionRegistry definitions, InventoryContainer[] containers, out string issue)
            => ConfigureInventory(definitions, containers, null, out issue);

        /// <summary>Opt-in instance content; does not infer ownership from tactical defaults or migrate old progress.</summary>
        internal bool ConfigureCharacterItemInventory(ItemDefinitionRegistry definitions, InventoryContainer[] containers,
            CharacterTacticalCharacterItemDefinition[] characters, out string issue)
        {
            try
            {
                if (characters == null || characters.Length == 0 || definitions == null)
                    return Fail("角色物品定义未配置。", out issue);
                var mapping = new Dictionary<int, string>();
                foreach (var value in characters)
                {
                    // Defaults here are only the known template catalog, never a held character list.
                    if (value == null || string.IsNullOrWhiteSpace(value.CharacterStableId)
                        || !_defaults.ContainsKey(value.CharacterStableId)
                        || !definitions.TryGet(new ItemDefinitionId(value.ItemDefinitionId), out var definition)
                        || definition.Kind != ItemKind.Custom || definition.MaxStackCount != 1
                        || mapping.ContainsKey(value.ItemDefinitionId))
                        return Fail("角色物品必须引用已知模板及不可堆叠的显式定义。", out issue);
                    mapping.Add(value.ItemDefinitionId, value.CharacterStableId);
                }
                return ConfigureInventory(definitions, containers, mapping, out issue);
            }
            catch (Exception error) { return Fail("角色物品配置失败：" + error.Message, out issue); }
        }

        private bool ConfigureInventory(ItemDefinitionRegistry definitions, InventoryContainer[] containers,
            Dictionary<int, string> characterDefinitions, out string issue)
        {
            try
            {
                if (MutationBlocked || definitions == null || containers == null)
                    return Fail("普通物品配置不可用。", out issue);
                var layouts = new InventoryContainer[containers.Length];
                var ids = new HashSet<int>();
                for (int i = 0; i < containers.Length; i++)
                {
                    var source = containers[i];
                    if (source == null || !ids.Add(source.ContainerId.Value))
                        return Fail("物品容器重复或为空。", out issue);
                    for (int slot = 0; slot < source.SlotCount; slot++)
                        if (!source.GetStack(slot).IsEmpty) return Fail("容器配置不能自带持有物品。", out issue);
                    layouts[i] = new InventoryContainer(source.ContainerId, source.OwnerId, source.SlotCount, source.StableId);
                }
                // Stage restoration first. A missing definition/layout keeps the existing checkpoint intact.
                var inventory = CreateItemCandidate(_state.Items, definitions, layouts, out _);
                ValidateCharacterItemContent(_state, inventory, characterDefinitions);
                ValidateAssemblyInventory(_state, inventory, definitions, characterDefinitions);
                _itemDefinitions = definitions; _itemContainers = layouts;
                _characterItemDefinitions = characterDefinitions;
                issue = string.Empty; return true;
            }
            catch (Exception error) { return Fail("普通物品配置失败：" + error.Message, out issue); }
        }

        /// <summary>Returns a detached inventory for display/inspection; mutations cannot alter the player owner.</summary>
        internal bool TryReadItemInventory(out InventoryStore inventory, out string issue)
        {
            inventory = null;
            if (_itemDefinitions == null) return Fail("普通物品尚未配置。", out issue);
            try
            {
                inventory = CreateItemCandidate(_state.Items, _itemDefinitions, _itemContainers, out _);
                ValidateCharacterItemContent(_state, inventory, _characterItemDefinitions);
                ValidateAssemblyInventory(_state, inventory, _itemDefinitions, _characterItemDefinitions);
                issue = string.Empty; return true;
            }
            catch (Exception error) { return Fail("普通物品读取失败：" + error.Message, out issue); }
        }

        /// <summary>Detached display records filtered by an explicit inventory owner; never reads template grants.</summary>
        internal bool TryReadOwnedCharacterItems(InventoryOwnerId owner,
            out CharacterTacticalCharacterItemState[] characters, out string issue)
        {
            characters = Array.Empty<CharacterTacticalCharacterItemState>();
            if (!owner.IsValid) return Fail("库存归属尚未配置，无法显示持有角色。", out issue);
            if (!TryReadItemInventory(out var inventory, out issue)) return false;
            var held = new HashSet<int>();
            foreach (var container in inventory.CreateContainerSnapshot())
                if (container.OwnerId.Equals(owner))
                    for (int slot = 0; slot < container.SlotCount; slot++)
                    {
                        var stack = container.GetStack(slot);
                        if (stack.InstanceId.IsValid && _characterItemDefinitions?.ContainsKey(stack.DefinitionId.Value) == true)
                            held.Add(stack.InstanceId.Value);
                    }
            characters = (_state.CharacterItems ?? new List<CharacterTacticalCharacterItemState>())
                .Where(value => held.Contains(value.ItemInstanceId)).OrderBy(value => value.ItemInstanceId)
                .Select(value => Clone(value)).ToArray();
            issue = string.Empty; return true;
        }

        /// <summary>Ownership comes from the joined inventory container, not the template defaults.</summary>
        internal bool TryReadCharacterItem(ItemInstanceId instanceId, InventoryOwnerId ownerId,
            out CharacterTacticalCharacterItemState character, out string issue)
        {
            character = null;
            if (!instanceId.IsValid || !ownerId.IsValid || _characterItemDefinitions == null)
                return Fail("角色物品读取参数或配置不可用。", out issue);
            if (!TryReadItemInventory(out var inventory, out issue)) return false;
            var value = _state.CharacterItems?.Find(item => item.ItemInstanceId == instanceId.Value);
            if (value == null || !OwnsInstance(inventory, instanceId, ownerId))
                return Fail("角色物品不存在或不属于当前所有者。", out issue);
            character = Clone(value); return true;
        }

        /// <summary>First-step direct grant only: one existing Items instance, character record and acquisition ledger commit.</summary>
        /// <remarks>LowFreqAlloc. Logical commit is synchronous; FlushAsync/HasUnsavedChanges report durability.
        /// This does not enable battle deployment, trading, destruction, costs or initial default grants.</remarks>
        internal ItemAcquisitionResult AcquireCharacterItemAtTick(ItemAcquisitionRequest request,
            InventoryContainerId target, InventoryOwnerId ownerId, ItemDefinitionId definitionId, string displayName,
            long expectedRevision, long expectedFrame, RuntimeTickContext context, out ItemInstanceId instanceId, out string issue)
        {
            instanceId = default; issue = string.Empty;
            if (MutationBlocked || _characterItemDefinitions == null || request == null || !request.RequestId.IsValid
                || request.SourceKind != ItemAcquisitionSourceKind.DirectGrant || request.Costs.Length != 0 || request.Grants.Length != 0
                || !ownerId.IsValid || string.IsNullOrWhiteSpace(displayName)
                || expectedFrame < 0 || context.Stage != RuntimeTickStage.Simulation || context.FrameIndex != expectedFrame
                || !_characterItemDefinitions.TryGetValue(definitionId.Value, out string template))
            {
                issue = "角色获取必须是显式直接授予且在指定逻辑帧。";
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidRequest);
            }
            CharacterTacticalPlayerState candidate;
            ItemInstanceId allocated;
            try
            {
                candidate = Clone(_state);
                var inventory = CreateItemCandidate(candidate.Items, _itemDefinitions, _itemContainers, out var ledger);
                ValidateCharacterItemContent(candidate, inventory, _characterItemDefinitions);
                if (!inventory.TryGet(target, out var container) || !container.OwnerId.Equals(ownerId))
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidContainer);
                string fingerprint = CharacterAcquisitionFingerprint(request, target, ownerId, definitionId, template, displayName);
                if (ledger.Contains(request.RequestId))
                {
                    var previous = candidate.CharacterItems?.Find(value => value.AcquisitionRequestId == request.RequestId.Value);
                    if (previous == null || previous.AcquisitionFingerprint != fingerprint)
                    {
                        issue = "获取请求标识已用于不同载荷。";
                        return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidRequest);
                    }
                    instanceId = new ItemInstanceId(previous.ItemInstanceId);
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.AlreadyCommitted);
                }
                if (expectedRevision != Revision || candidate.Items != null && expectedFrame < candidate.Items.Frame)
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.StaleRequest);
                if (!inventory.TryAllocateInstanceId(out allocated))
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.CommitFailed);
                var service = new InventoryService(_itemDefinitions, inventory);
                var insertion = service.TryInsertStack(target, new ItemStack(definitionId, 1, allocated));
                if (!insertion.Success)
                    return ItemAcquisitionResult.Failed(insertion.Status == InventoryOperationStatus.InsufficientCapacity
                        ? ItemAcquisitionStatus.InsufficientCapacity : ItemAcquisitionStatus.CommitFailed, inventoryStatus: insertion.Status);
                var result = new InventoryTransactionService(service, ledger).Execute(request, true);
                if (!result.Success) return result;
                candidate.CharacterItems ??= new List<CharacterTacticalCharacterItemState>();
                var character = new CharacterTacticalCharacterItemState {
                    ItemInstanceId = allocated.Value, CharacterStableId = template, DisplayName = displayName,
                    Revision = 1, AcquisitionRequestId = request.RequestId.Value, AcquisitionFingerprint = fingerprint };
                InitializeCharacterTacticalData(character, candidate);
                candidate.CharacterItems.Add(character);
                CaptureItemCandidate(candidate, inventory, ledger, expectedFrame);
                ValidateState(candidate); ValidateCharacterItemContent(candidate, inventory, _characterItemDefinitions);
            }
            catch (Exception error)
            {
                issue = "角色获取候选校验失败：" + error.Message;
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.CommitFailed);
            }
            // Validation failures above cannot publish. Observer notifications happen after the logical commit.
            _state = candidate; Revision++;
            instanceId = allocated; _ = FlushAsync(); Changed?.Invoke();
            return ItemAcquisitionResult.Succeeded();
        }

        internal bool TryRenameCharacterItemAtTick(ItemInstanceId instanceId, InventoryOwnerId ownerId, string displayName,
            long expectedCharacterRevision, long expectedRevision, long expectedFrame, RuntimeTickContext context, out string issue)
        {
            if (MutationBlocked || _characterItemDefinitions == null || string.IsNullOrWhiteSpace(displayName)
                || !instanceId.IsValid || !ownerId.IsValid || expectedFrame < 0
                || context.Stage != RuntimeTickStage.Simulation || context.FrameIndex != expectedFrame)
                return Fail("角色改名参数或指定逻辑帧不可用。", out issue);
            CharacterTacticalPlayerState candidate;
            try
            {
                candidate = Clone(_state);
                var inventory = CreateItemCandidate(candidate.Items, _itemDefinitions, _itemContainers, out var ledger);
                ValidateCharacterItemContent(candidate, inventory, _characterItemDefinitions);
                var value = candidate.CharacterItems?.Find(item => item.ItemInstanceId == instanceId.Value);
                if (value == null || !OwnsInstance(inventory, instanceId, ownerId))
                    return Fail("角色物品不存在或不属于当前所有者。", out issue);
                if (expectedRevision != Revision || expectedCharacterRevision != value.Revision || expectedFrame < candidate.Items.Frame)
                    return Fail("角色数据或进度版本已变化，请重新读取。", out issue);
                if (value.DisplayName == displayName) { issue = string.Empty; return true; }
                value.DisplayName = displayName; value.Revision = checked(value.Revision + 1);
                CaptureItemCandidate(candidate, inventory, ledger, expectedFrame);
                ValidateState(candidate);
            }
            catch (Exception error) { return Fail("角色改名候选校验失败：" + error.Message, out issue); }
            _state = candidate; Revision++; issue = string.Empty;
            _ = FlushAsync(); Changed?.Invoke(); return true;
        }

        private void CaptureItemCandidate(CharacterTacticalPlayerState candidate, InventoryStore inventory,
            ItemAcquisitionLedger ledger, long frame)
        {
            candidate.Items = new CharacterTacticalItemState { Frame = frame,
                Modules = new[] {
                    new InventorySaveStateProvider(inventory, _itemDefinitions).CaptureSaveState().Value.ModuleStates[0],
                    new ItemAcquisitionSaveStateProvider(ledger).CaptureSaveState().Value.ModuleStates[0] },
                Settlements = candidate.Items?.Settlements ?? new List<CharacterTacticalItemSettlement>() };
            candidate.Schema = PlayerItemSchema(candidate);
        }

        private static string PlayerItemSchema(CharacterTacticalPlayerState state) => state.AccessoryItems != null
            ? CharacterTacticalPlayerState.CardAssemblySchema : state.CharacterItems == null
            ? CharacterTacticalPlayerState.ItemSchema : state.CharacterItems.Any(value => value.TacticalSchema != null)
                ? CharacterTacticalPlayerState.CharacterTacticalSchema : CharacterTacticalPlayerState.CharacterItemSchema;

        private void InitializeCharacterTacticalData(CharacterTacticalCharacterItemState character, CharacterTacticalPlayerState candidate)
        {
            var initial = InstantiateDefaults(_defaults[character.CharacterStableId], candidate);
            character.TacticalSchema = "mx.character-test.character-item-tactical.v1";
            character.ActionGrants = new List<CharacterTacticalActionGrant>();
            character.Cards = initial.Cards; character.Bindings = initial.Bindings;
            foreach (string id in initial.ActionIds)
            {
                var action = _catalog.Action(id);
                if (action == null || !action.CharacterStableIds.Contains(character.CharacterStableId))
                    throw new JsonSerializationException("TACTICAL_CHARACTER_INITIAL_ACTION_INVALID");
                if (character.ActionGrants.Any(value => value.ActionId == action.ActionId)) continue;
                character.ActionGrants.Add(new CharacterTacticalActionGrant { ActionId = action.ActionId, TacticalActionId = action.Id,
                    RequestId = character.AcquisitionRequestId, Source = "initial", ContentFingerprint = ActionFingerprint(action),
                    RequestFingerprint = character.AcquisitionFingerprint });
            }
            foreach (var card in character.Cards)
                if (_catalog.ExactCard(card) == null) throw new JsonSerializationException("TACTICAL_CHARACTER_INITIAL_CARD_INVALID");
            foreach (var binding in character.Bindings)
                if (!TryResolveCharacterBinding(character, binding.CardInstanceId, binding.SlotPath, binding.TacticalActionId, out _, out _))
                    throw new JsonSerializationException("TACTICAL_CHARACTER_INITIAL_BINDING_INVALID");
        }

        /// <summary>Detached projection from this instance's permanent grants and real cards. Defaults are not consulted.</summary>
        internal bool TryReadCharacterItemLoadout(ItemInstanceId id, InventoryOwnerId owner,
            out CharacterTacticalDefaults loadout, out long characterRevision, out string issue)
        {
            loadout = null; characterRevision = 0;
            if (!TryReadCharacterItem(id, owner, out var character, out issue)) return false;
            if (character.TacticalSchema == null) return Fail("旧角色实例尚无动作数据；需要明确升级方案，未从当前模板补发。", out issue);
            loadout = new CharacterTacticalDefaults { CharacterStableId = character.CharacterStableId,
                ActionIds = character.ActionGrants.GroupBy(value => value.ActionId).Select(group => {
                    var grant = group.First();
                    var current = _catalog.Actions.FirstOrDefault(value => value.ActionId == grant.ActionId
                        && group.Any(source => source.ContentFingerprint == ActionFingerprint(value)));
                    return current?.Id ?? grant.TacticalActionId;
                }).ToList(), Cards = Clone(character.Cards), Bindings = Clone(character.Bindings) };
            characterRevision = character.Revision; return true;
        }

        /// <summary>Explicit permanent learning only. Uses the existing acquisition ledger; learning does not bind any slot.</summary>
        internal ItemAcquisitionResult LearnCharacterActionAtTick(ItemAcquisitionRequestId requestId, ItemInstanceId id,
            InventoryOwnerId owner, string actionId, long expectedCharacterRevision, long expectedRevision,
            long expectedFrame, RuntimeTickContext context, out string issue)
        {
            issue = string.Empty;
            if (!requestId.IsValid) { issue = "学习请求标识无效。"; return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidRequest); }
            if (!TryCharacterCommand(id, owner, expectedFrame, context, out var candidate,
                out var character, out var inventory, out var ledger, out issue))
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidRequest);
            var action = _catalog.Action(actionId);
            if (action == null || !action.CharacterStableIds.Contains(character.CharacterStableId)
                || !_catalog.ContainsAction(_catalog.Pool(character.CharacterStableId)?.ActionIds, actionId))
            {
                issue = "动作定义、适用规则或明确学习范围不可用。";
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidRequest);
            }
            string fingerprint = Digest(JsonConvert.SerializeObject(new { Item = id.Value, Owner = owner.Value,
                Action = action.ActionId, Content = ActionFingerprint(action), Rule = 1 }));
            if (ledger.Contains(requestId))
            {
                var prior = character.ActionGrants.Find(value => value.RequestId == requestId.Value && value.Source == "direct");
                if (prior == null || prior.RequestFingerprint != fingerprint)
                { issue = "学习请求标识已用于其他载荷或角色。"; return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidRequest); }
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.AlreadyCommitted);
            }
            if (expectedRevision != Revision || expectedCharacterRevision != character.Revision || expectedFrame < candidate.Items.Frame)
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.StaleRequest);
            try
            {
                var transaction = new InventoryTransactionService(new InventoryService(_itemDefinitions, inventory), ledger);
                var result = transaction.Execute(new ItemAcquisitionRequest(requestId, ItemAcquisitionSourceKind.DirectGrant, null, null), true);
                if (!result.Success) return result;
                character.ActionGrants.Add(new CharacterTacticalActionGrant { ActionId = action.ActionId, TacticalActionId = action.Id,
                    RequestId = requestId.Value, Source = "direct", ContentFingerprint = ActionFingerprint(action), RequestFingerprint = fingerprint });
                character.Revision = checked(character.Revision + 1);
                CaptureItemCandidate(candidate, inventory, ledger, expectedFrame); ValidateState(candidate);
            }
            catch (Exception error) { issue = "学习候选失败：" + error.Message; return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.CommitFailed); }
            PublishCharacterCandidate(candidate); return ItemAcquisitionResult.Succeeded();
        }

        internal bool BindCharacterActionAtTick(ItemInstanceId id, InventoryOwnerId owner, ulong cardId, string slot, string actionId,
            long expectedCharacterRevision, long expectedRevision, long frame, RuntimeTickContext context, out string issue)
        {
            if (!TryCharacterCommand(id, owner, frame, context, out var candidate, out var character, out var inventory, out var ledger, out issue)) return false;
            if (expectedRevision != Revision || expectedCharacterRevision != character.Revision || frame < candidate.Items.Frame)
                return Fail("角色或进度版本已变化。", out issue);
            if (!TryResolveCharacterBinding(character, cardId, slot, actionId, out var card, out issue)) return false;
            var binding = character.Bindings.Find(value => value.CardInstanceId == cardId && value.SlotPath == slot);
            if (binding?.TacticalActionId == actionId) return true;
            if (binding == null) character.Bindings.Add(new CharacterTacticalSlotBinding { CardInstanceId = cardId, CardId = card.CardId,
                SlotPath = slot, TacticalActionId = actionId });
            else binding.TacticalActionId = actionId;
            return CommitCharacterEdit(candidate, character, inventory, ledger, frame, out issue);
        }

        internal bool UnbindCharacterActionAtTick(ItemInstanceId id, InventoryOwnerId owner, ulong cardId, string slot,
            long expectedCharacterRevision, long expectedRevision, long frame, RuntimeTickContext context, out string issue)
        {
            if (!TryCharacterCommand(id, owner, frame, context, out var candidate, out var character, out var inventory, out var ledger, out issue)) return false;
            if (expectedRevision != Revision || expectedCharacterRevision != character.Revision || frame < candidate.Items.Frame)
                return Fail("角色或进度版本已变化。", out issue);
            var card = character.Cards.Find(value => value.InstanceId == cardId);
            if (card == null || _catalog.ExactCard(card)?.Slots.Contains(slot) != true) return Fail("卡实例或精确版本槽位不可用。", out issue);
            if (character.Bindings.RemoveAll(value => value.CardInstanceId == cardId && value.SlotPath == slot) == 0) return true;
            return CommitCharacterEdit(candidate, character, inventory, ledger, frame, out issue);
        }

        internal bool TryGetCharacterItemBindings(ItemInstanceId id, InventoryOwnerId owner, ulong cardId,
            out Dictionary<string, CharacterTacticalActionDefinition> bindings, out string issue)
        {
            bindings = new Dictionary<string, CharacterTacticalActionDefinition>(StringComparer.Ordinal);
            if (!TryReadCharacterItem(id, owner, out var character, out issue)) return false;
            if (character.TacticalSchema == null) return Fail("旧角色实例需明确升级动作数据。", out issue);
            var card = character.Cards.Find(value => value.InstanceId == cardId);
            var metadata = _catalog.ExactCard(card);
            if (metadata == null) return Fail("角色未拥有此精确卡实例。", out issue);
            foreach (string slot in metadata.Slots)
            {
                var binding = character.Bindings.Find(value => value.CardInstanceId == cardId && value.SlotPath == slot);
                if (binding == null) return Fail("角色实例卡槽尚未绑定动作。", out issue);
                if (!TryResolveCharacterBinding(character, cardId, slot, binding.TacticalActionId, out _, out issue)) return false;
                bindings.Add(slot, Clone(_catalog.Action(binding.TacticalActionId)));
            }
            issue = string.Empty; return true;
        }

        private bool TryCharacterCommand(ItemInstanceId id, InventoryOwnerId owner, long frame, RuntimeTickContext context,
            out CharacterTacticalPlayerState candidate, out CharacterTacticalCharacterItemState character,
            out InventoryStore inventory, out ItemAcquisitionLedger ledger, out string issue)
        {
            candidate = null; character = null; inventory = null; ledger = null;
            if (MutationBlocked || _characterItemDefinitions == null || !id.IsValid || !owner.IsValid || frame < 0
                || context.Stage != RuntimeTickStage.Simulation || context.FrameIndex != frame)
                return Fail("角色实例命令参数或逻辑帧不可用。", out issue);
            try
            {
                candidate = Clone(_state); inventory = CreateItemCandidate(candidate.Items, _itemDefinitions, _itemContainers, out ledger);
                ValidateCharacterItemContent(candidate, inventory, _characterItemDefinitions);
                character = candidate.CharacterItems?.Find(value => value.ItemInstanceId == id.Value);
                if (character == null || !OwnsInstance(inventory, id, owner)) return Fail("角色实例不存在或归属不符。", out issue);
                if (character.TacticalSchema == null) return Fail("旧角色实例尚未明确升级；保留原档，不补发模板动作。", out issue);
                issue = string.Empty; return true;
            }
            catch (Exception error) { return Fail("角色命令候选失败：" + error.Message, out issue); }
        }

        private bool TryResolveCharacterBinding(CharacterTacticalCharacterItemState character, ulong cardId, string slot,
            string actionId, out CharacterTacticalCardReference card, out string issue)
        {
            card = character.Cards.Find(value => value.InstanceId == cardId);
            var metadata = _catalog.ExactCard(card); var action = _catalog.Action(actionId);
            if (metadata == null || metadata.Slots.Contains(slot) != true) return Fail("未拥有此卡实例或精确槽位无效。", out issue);
            if (action == null || !action.CharacterStableIds.Contains(character.CharacterStableId)
                || !character.ActionGrants.Any(value => value.ActionId == action.ActionId && value.ContentFingerprint == ActionFingerprint(action)))
                return Fail("此角色实例未学习当前适用版本的动作。", out issue);
            issue = string.Empty; return true;
        }

        private bool CommitCharacterEdit(CharacterTacticalPlayerState candidate, CharacterTacticalCharacterItemState character,
            InventoryStore inventory, ItemAcquisitionLedger ledger, long frame, out string issue)
        {
            try { character.Revision = checked(character.Revision + 1); CaptureItemCandidate(candidate, inventory, ledger, frame); ValidateState(candidate); }
            catch (Exception error) { return Fail("角色装配候选失败：" + error.Message, out issue); }
            issue = string.Empty; PublishCharacterCandidate(candidate); return true;
        }
        private void PublishCharacterCandidate(CharacterTacticalPlayerState candidate)
        { _state = candidate; Revision++; _ = FlushAsync(); Changed?.Invoke(); }
        private static string ActionFingerprint(CharacterTacticalActionDefinition action) => Digest(JsonConvert.SerializeObject(new {
            action.ActionId, action.ActionLibraryId, Applicable = action.CharacterStableIds.OrderBy(value => value, StringComparer.Ordinal).ToArray() }));
        private static bool ValidDigest(string value) => value?.Length == 64
            && value.All(character => character >= '0' && character <= '9' || character >= 'a' && character <= 'f');
        private static string Digest(string payload)
        {
            var hash = RuntimeHashDigest.ComputeContentDigest(payload);
            return hash.Word0.ToString("x16") + hash.Word1.ToString("x16") + hash.Word2.ToString("x16") + hash.Word3.ToString("x16");
        }

        private static string CharacterAcquisitionFingerprint(ItemAcquisitionRequest request, InventoryContainerId target,
            InventoryOwnerId ownerId, ItemDefinitionId definition, string template, string displayName)
        {
            var digest = RuntimeHashDigest.ComputeContentDigest(JsonConvert.SerializeObject(new {
                Source = (int)request.SourceKind, Container = target.Value, Owner = ownerId.Value,
                Definition = definition.Value, Template = template, Name = displayName }));
            return digest.Word0.ToString("x16") + digest.Word1.ToString("x16") + digest.Word2.ToString("x16") + digest.Word3.ToString("x16");
        }

        private static bool OwnsInstance(InventoryStore inventory, ItemInstanceId id, InventoryOwnerId owner)
        {
            foreach (var container in inventory.CreateContainerSnapshot())
                for (int slot = 0; slot < container.SlotCount; slot++)
                    if (container.GetStack(slot).InstanceId.Equals(id)) return container.OwnerId.Equals(owner);
            return false;
        }

        private static void ValidateCharacterItemContent(CharacterTacticalPlayerState state, InventoryStore inventory,
            Dictionary<int, string> mapping)
        {
            if (mapping == null)
            {
                if (state.CharacterItems != null) throw new JsonSerializationException("TACTICAL_CHARACTER_ITEM_CONTENT_REQUIRED");
                return;
            }
            var unmatched = state.CharacterItems?.ToDictionary(value => value.ItemInstanceId)
                ?? new Dictionary<int, CharacterTacticalCharacterItemState>();
            foreach (var container in inventory.CreateContainerSnapshot())
                for (int slot = 0; slot < container.SlotCount; slot++)
                {
                    var stack = container.GetStack(slot);
                    if (!mapping.TryGetValue(stack.DefinitionId.Value, out var template)) continue;
                    if (!stack.InstanceId.IsValid || stack.Count != 1 || !unmatched.TryGetValue(stack.InstanceId.Value, out var record)
                        || record.CharacterStableId != template)
                        throw new JsonSerializationException("TACTICAL_CHARACTER_ITEM_JOIN_INVALID");
                    unmatched.Remove(stack.InstanceId.Value);
                }
            if (unmatched.Count != 0) throw new JsonSerializationException("TACTICAL_CHARACTER_ITEM_REFERENCE_UNAVAILABLE");
        }

        /// <summary>Publishes ordinary items, ledger, optional settlement and card grants in one logical tick.</summary>
        /// <remarks>LowFreqAlloc; call only from a deterministically ordered Simulation command at expectedFrame.
        /// Success is a logical commit. HasUnsavedChanges/FlushAsync report durable checkpoint status separately.
        /// Card definitions/instances remain owned by this service and use its existing instance allocator.</remarks>
        internal ItemAcquisitionResult ExecuteItemTransactionAtTick(ItemAcquisitionRequest request,
            IReadOnlyList<CharacterTacticalCardGrant> cardGrants, long settlementId, long expectedFrame,
            RuntimeTickContext context, out string issue)
        {
            issue = string.Empty;
            if (MutationBlocked || _itemDefinitions == null || request == null || !request.RequestId.IsValid
                || settlementId < 0 || context.Stage != RuntimeTickStage.Simulation || context.FrameIndex != expectedFrame)
            {
                issue = "物品事务未配置或不在指定逻辑帧。";
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidRequest);
            }
            CharacterTacticalPlayerState candidate;
            Dictionary<string, HashSet<string>> availability;
            try
            {
                candidate = Clone(_state);
                InventoryStore inventory = CreateItemCandidate(candidate.Items, _itemDefinitions, _itemContainers, out var ledger);
                ValidateCharacterItemContent(candidate, inventory, _characterItemDefinitions);
                if (ledger.Contains(request.RequestId) || candidate.Items?.Settlements.Any(value => value.SettlementId == settlementId && settlementId > 0) == true)
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.AlreadyCommitted);
                if (candidate.Items != null && expectedFrame < candidate.Items.Frame)
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.StaleRequest);
                // Role definitions cannot bypass allocation/data creation or be consumed as ordinary costs.
                if (_characterItemDefinitions != null && (request.Grants.Any(line => _characterItemDefinitions.ContainsKey(line.ItemDefinitionId.Value))
                    || request.Costs.Any(line => _characterItemDefinitions.ContainsKey(line.ItemDefinitionId.Value))))
                {
                    issue = "角色物品必须经过角色实例事务。";
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidRequest);
                }
                if (_assemblyConfiguration != null && (request.Grants.Any(line => _assemblyConfiguration.Accessories.Any(value => value.ItemDefinitionId == line.ItemDefinitionId.Value))
                    || request.Costs.Any(line => _assemblyConfiguration.Accessories.Any(value => value.ItemDefinitionId == line.ItemDefinitionId.Value))))
                {
                    issue = "配件必须经过配件实例事务；消耗或批量变更接口尚未开放。";
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidRequest);
                }
                int cardCount = cardGrants?.Count ?? 0;
                if (UsesCharacterItemTacticalProgress && cardCount > 0)
                { issue = "实例进度的卡授予必须指定角色实例。"; return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidRequest); }
                for (int i = 0; i < cardCount; i++)
                {
                    var grant = cardGrants[i];
                    var character = grant == null ? null : candidate.Characters.Find(value => value.CharacterStableId == grant.CharacterStableId);
                    if (character == null || _catalog.ExactCard(grant.Card) == null
                        || _catalog.Pool(grant.CharacterStableId)?.Cards.Any(card => Same(card, grant.Card)) != true)
                    {
                        issue = "卡牌奖励不在角色允许的正式版本获取配置中。";
                        return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidRequest);
                    }
                    AppendDeckEntry(candidate, character, grant.Card);
                }
                var service = new InventoryTransactionService(new InventoryService(_itemDefinitions, inventory), ledger);
                ItemAcquisitionResult result = service.Execute(request, cardCount > 0);
                if (!result.Success) return result;
                var items = new CharacterTacticalItemState { Frame = expectedFrame,
                    Modules = new[] {
                        new InventorySaveStateProvider(inventory, _itemDefinitions).CaptureSaveState().Value.ModuleStates[0],
                        new ItemAcquisitionSaveStateProvider(ledger).CaptureSaveState().Value.ModuleStates[0] },
                    Settlements = candidate.Items?.Settlements ?? new List<CharacterTacticalItemSettlement>() };
                if (settlementId > 0) items.Settlements.Add(new CharacterTacticalItemSettlement {
                    SettlementId = settlementId, RequestId = request.RequestId.Value });
                candidate.Items = items; candidate.Schema = PlayerItemSchema(candidate);
                ValidateState(candidate);
                ValidateCharacterItemContent(candidate, inventory, _characterItemDefinitions);
                availability = BuildAvailability(_catalog, candidate);
            }
            catch (Exception error)
            {
                issue = "物品事务候选校验失败：" + error.Message;
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.CommitFailed);
            }
            // Only this synchronous boundary publishes authority. Async persistence never grants or reorders it.
            _state = candidate; _usableCards = availability; Revision++;
            _ = FlushAsync(); Changed?.Invoke();
            return ItemAcquisitionResult.Succeeded();
        }

        private static InventoryStore CreateItemCandidate(CharacterTacticalItemState saved, ItemDefinitionRegistry definitions,
            InventoryContainer[] layouts, out ItemAcquisitionLedger ledger)
        {
            var inventory = new InventoryStore();
            ledger = new ItemAcquisitionLedger();
            foreach (var layout in layouts)
                if (!inventory.Register(new InventoryContainer(layout.ContainerId, layout.OwnerId, layout.SlotCount, layout.StableId)))
                    throw new JsonSerializationException("TACTICAL_ITEM_LAYOUT_INVALID");
            if (saved == null) return inventory;
            CharacterTacticalItemState.Validate(saved);
            RuntimeSaveState runtime = saved.ToRuntimeState();
            if (!new InventorySaveStateProvider(inventory, definitions).RestoreSaveState(runtime).Success
                || !new ItemAcquisitionSaveStateProvider(ledger).RestoreSaveState(runtime).Success)
                throw new JsonSerializationException("TACTICAL_ITEM_CONTENT_UNAVAILABLE");
            if (inventory.Count != layouts.Length) throw new JsonSerializationException("TACTICAL_ITEM_LAYOUT_MISMATCH");
            foreach (var layout in layouts)
                if (!inventory.TryGet(layout.ContainerId, out var container) || !container.OwnerId.Equals(layout.OwnerId)
                    || container.SlotCount != layout.SlotCount || container.StableId != layout.StableId)
                    throw new JsonSerializationException("TACTICAL_ITEM_LAYOUT_MISMATCH");
            return inventory;
        }

        internal Task<bool> FlushAsync()
        {
            if (_rewardCommitInFlight) return Task.FromResult(false);
            if (_saving != null && !_saving.IsCompleted) return _saving;
            _saving = SaveLoopAsync(); return _saving;
        }

        private async Task<bool> SaveLoopAsync()
        {
            try
            {
                while (HasUnsavedChanges)
                {
                    long revision = Revision;
                    string json = JsonConvert.SerializeObject(_state);
                    await _store.SaveAsync(json);
                    _savedRevision = revision;
                }
                SaveIssue = string.Empty; return true;
            }
            catch (Exception error) { SaveIssue = "尚未保存：" + error.Message; return false; }
            finally { Changed?.Invoke(); }
        }

        /// <summary>The composition root must retire its active battle before calling this method.</summary>
        internal async Task<bool> ResetAsync()
        {
            if (MutationBlocked) return false;
            _resetting = true;
            try
            {
                if (_saving != null) await _saving;
                var initial = new CharacterTacticalPlayerState { LegacyImportCompleted = true };
                foreach (CharacterTacticalDefaults value in _defaults.Values) initial.Characters.Add(InstantiateDefaults(value, initial));
                ValidateState(initial);
                await _store.ResetAsync(JsonConvert.SerializeObject(initial));
                _state = initial; Revision++; _savedRevision = Revision; SaveIssue = string.Empty;
                return true;
            }
            catch (Exception error) { SaveIssue = "清档未完成：" + error.Message; return false; }
            finally { _resetting = false; RebuildAvailability(); Changed?.Invoke(); }
        }

        private void Touch() { RebuildAvailability(); Revision++; Changed?.Invoke(); _ = FlushAsync(); }
        private CharacterTacticalDefaults Find(string character) => _state.Characters.Find(c => c.CharacterStableId == character);
        private bool Ready(string character, out CharacterTacticalDefaults state, out string issue)
        {
            if (UsesCharacterItemTacticalProgress) { state = null; return Fail("请指定角色物品实例；模板不能写入实例进度。", out issue); }
            state = Find(character); issue = string.Empty;
            return !MutationBlocked && state != null || Fail("角色清单不可用或正在清档。", out issue);
        }
        private void ValidateState() => ValidateState(_state);

        private static void ValidateState(CharacterTacticalPlayerState state)
        {
            if (state == null || (state.Schema != "mx.character-test.tactical-player.v2" && state.Schema != CharacterTacticalPlayerState.ItemSchema
                    && state.Schema != CharacterTacticalPlayerState.CharacterItemSchema && state.Schema != CharacterTacticalPlayerState.CharacterTacticalSchema
                    && state.Schema != CharacterTacticalPlayerState.CardAssemblySchema)
                || state.Characters == null || state.NextCardInstanceId == 0
                || (state.Schema != "mx.character-test.tactical-player.v2") != (state.Items != null)
                || (state.Schema == CharacterTacticalPlayerState.CharacterItemSchema || state.Schema == CharacterTacticalPlayerState.CharacterTacticalSchema
                    || state.Schema == CharacterTacticalPlayerState.CardAssemblySchema) != (state.CharacterItems != null))
                throw new JsonSerializationException("TACTICAL_SAVE_INVALID");
            if (state.Items != null) CharacterTacticalItemState.Validate(state.Items);
            CharacterTacticalAssemblyRules.ValidateSave(state);
            if (state.CharacterItems != null)
            {
                var ledger = new ItemAcquisitionLedger();
                new ItemAcquisitionSaveStateProvider(ledger).RestoreSaveState(state.Items.ToRuntimeState());
                var inventory = JsonConvert.DeserializeObject<InventorySaveState>(state.Items.Modules.Single(
                    value => value.ModuleId == InventorySaveStateProvider.ModuleId).CustomState.PayloadJson);
                var itemIds = new HashSet<int>(); var requestIds = new HashSet<long>();
                foreach (var item in state.CharacterItems)
                    if (item == null || item.ItemInstanceId <= 0 || !itemIds.Add(item.ItemInstanceId)
                        || string.IsNullOrWhiteSpace(item.CharacterStableId) || string.IsNullOrWhiteSpace(item.DisplayName) || item.Revision <= 0
                        || !requestIds.Add(item.AcquisitionRequestId) || !ledger.Contains(new ItemAcquisitionRequestId(item.AcquisitionRequestId))
                        || item.AcquisitionFingerprint == null || item.AcquisitionFingerprint.Length != 64
                        || item.AcquisitionFingerprint.Any(value => !(value >= '0' && value <= '9' || value >= 'a' && value <= 'f'))
                        || inventory.Containers.SelectMany(value => value.Slots).Count(value => value.ItemInstanceId == item.ItemInstanceId && value.Count == 1) != 1)
                        throw new JsonSerializationException("TACTICAL_CHARACTER_ITEM_SAVE_INVALID");
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var instances = new HashSet<ulong>();
            ulong maximumInstanceId = 0;
            var directRequests = new HashSet<long>();
            var acquisitionRequests = new HashSet<long>(state.CharacterItems?.Select(value => value.AcquisitionRequestId) ?? Enumerable.Empty<long>());
            if (state.CharacterItems != null)
                foreach (var item in state.CharacterItems)
                {
                    if (item.TacticalSchema == null)
                    {
                        if (item.ActionGrants != null || item.Cards != null || item.Bindings != null || item.CardAssemblies != null || item.CardAcquisitions != null)
                            throw new JsonSerializationException("TACTICAL_CHARACTER_ITEM_UPGRADE_AMBIGUOUS");
                        continue;
                    }
                    if ((state.Schema != CharacterTacticalPlayerState.CharacterTacticalSchema && state.Schema != CharacterTacticalPlayerState.CardAssemblySchema)
                        || item.TacticalSchema != "mx.character-test.character-item-tactical.v1"
                        || item.ActionGrants == null || item.Cards == null || item.Bindings == null)
                        throw new JsonSerializationException("TACTICAL_CHARACTER_ITEM_TACTICAL_INVALID");
                    var ledger = new ItemAcquisitionLedger();
                    new ItemAcquisitionSaveStateProvider(ledger).RestoreSaveState(state.Items.ToRuntimeState());
                    var sources = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var grant in item.ActionGrants)
                        if (grant == null || grant.ActionId <= 0 || string.IsNullOrWhiteSpace(grant.TacticalActionId)
                            || !ledger.Contains(new ItemAcquisitionRequestId(grant.RequestId)) || grant.RuleVersion != 1
                            || !(grant.Source == "initial" && grant.RequestId == item.AcquisitionRequestId || grant.Source == "direct")
                            || !sources.Add(grant.ActionId + ":" + grant.RequestId) || !ValidDigest(grant.ContentFingerprint) || !ValidDigest(grant.RequestFingerprint)
                            || grant.Source == "initial" && grant.RequestFingerprint != item.AcquisitionFingerprint
                            || grant.Source == "direct" && (acquisitionRequests.Contains(grant.RequestId) || !directRequests.Add(grant.RequestId)))
                            throw new JsonSerializationException("TACTICAL_CHARACTER_ACTION_GRANT_INVALID");
                    var localCards = new HashSet<ulong>();
                    foreach (var card in item.Cards)
                    {
                        if (card == null || card.InstanceId == 0 || !instances.Add(card.InstanceId) || !localCards.Add(card.InstanceId)
                            || string.IsNullOrWhiteSpace(card.CardId) || card.Revision <= 0)
                            throw new JsonSerializationException("TACTICAL_CHARACTER_CARD_INVALID");
                        maximumInstanceId = Math.Max(maximumInstanceId, card.InstanceId);
                    }
                    var slots = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var binding in item.Bindings)
                        if (binding == null || !localCards.Contains(binding.CardInstanceId)
                            || item.Cards.Find(value => value.InstanceId == binding.CardInstanceId)?.CardId != binding.CardId
                            || string.IsNullOrWhiteSpace(binding.SlotPath) || string.IsNullOrWhiteSpace(binding.TacticalActionId)
                            || !slots.Add(binding.CardInstanceId + ":" + binding.SlotPath))
                            throw new JsonSerializationException("TACTICAL_CHARACTER_BINDING_INVALID");
                }
            foreach (var value in state.Characters)
            {
                if (value == null || !value.TryValidate(out _) || !ids.Add(value.CharacterStableId))
                    throw new JsonSerializationException("TACTICAL_SAVE_CHARACTER_INVALID");
                foreach (var card in value.Cards)
                {
                    if (card.InstanceId == 0 || !instances.Add(card.InstanceId))
                        throw new JsonSerializationException("TACTICAL_SAVE_CARD_INSTANCE_INVALID");
                    maximumInstanceId = Math.Max(maximumInstanceId, card.InstanceId);
                }
            }
            if (state.NextCardInstanceId <= maximumInstanceId)
                throw new JsonSerializationException("TACTICAL_SAVE_INSTANCE_COUNTER_INVALID");
        }
        private static bool Same(CharacterTacticalCardReference a, CharacterTacticalCardReference b) => a != null && b != null
            && a.CardId == b.CardId && a.Revision == b.Revision && a.ContentHash == b.ContentHash;
        // Published presentation source is immutable and deliberately excluded from save JSON.
        // Keep it attached to the corresponding exact definition through initial load and catalog refresh.
        private static CharacterTacticalCardDefinition[] Clone(CharacterTacticalCardDefinition[] cards)
        {
            var cloned = Clone<CharacterTacticalCardDefinition[]>(cards);
            if (cloned != null)
                for (int i = 0; i < cloned.Length; i++)
                    if (cloned[i] != null) cloned[i].PresentationData = cards[i]?.PresentationData;
            return cloned;
        }
        private static T Clone<T>(T value) => value == null ? default : JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        private static bool Fail(string text, out string issue) { issue = text; return false; }
    }
}
