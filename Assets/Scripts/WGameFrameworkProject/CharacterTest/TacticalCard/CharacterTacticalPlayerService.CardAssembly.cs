using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.CharacterApplication;
using MxFramework.Items;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class CharacterTacticalPlayerService
    {
        private CharacterTacticalAssemblyConfiguration _assemblyConfiguration;

        /// <summary>Shared single-grant entry point; each category retains its true ownership and existing transaction semantics.</summary>
        /// <remarks>LowFreqAlloc. No batch overload: callers must not represent repeated calls as an atomic multi-grant.</remarks>
        internal CharacterTacticalContentGrantReceipt GrantContentAtTick(CharacterTacticalContentGrantRequest request,
            RuntimeTickContext context, out string issue)
        {
            if (request == null || !Enum.IsDefined(typeof(CharacterTacticalContentKind), request.Kind) || request.Kind == CharacterTacticalContentKind.Unspecified)
                return new CharacterTacticalContentGrantReceipt(AcquisitionFailure("授予类别无效。", out issue));
            bool isRole = request.Kind == CharacterTacticalContentKind.Character, isAccessory = request.Kind == CharacterTacticalContentKind.Accessory;
            bool needsCharacter = request.Kind == CharacterTacticalContentKind.MainCard || request.Kind == CharacterTacticalContentKind.Action;
            if (needsCharacter != (request.CharacterItemInstanceId > 0) || !needsCharacter && request.CharacterItemInstanceId != 0
                || (isRole || isAccessory) != (request.ItemDefinitionId > 0 && request.TargetContainerId > 0)
                || needsCharacter && (request.ItemDefinitionId != 0 || request.TargetContainerId != 0)
                || (request.Kind == CharacterTacticalContentKind.MainCard) != (request.MainCard != null)
                || (request.Kind == CharacterTacticalContentKind.Action) != !string.IsNullOrWhiteSpace(request.ActionId)
                || request.Kind != CharacterTacticalContentKind.Action && request.ActionId != null
                || isRole != !string.IsNullOrWhiteSpace(request.DisplayName) || !isRole && request.DisplayName != null
                || !needsCharacter && request.ExpectedCharacterRevision != 0)
                return new CharacterTacticalContentGrantReceipt(AcquisitionFailure("授予载荷与类别不一致。", out issue));
            var requestId = new ItemAcquisitionRequestId(request.RequestId); var owner = new InventoryOwnerId(request.OwnerId);
            var character = new ItemInstanceId(request.CharacterItemInstanceId);
            switch (request.Kind)
            {
                case CharacterTacticalContentKind.MainCard:
                    var main = GrantCharacterCardAtTick(requestId, character, owner, request.MainCard, request.ExpectedCharacterRevision,
                        request.ExpectedPlayerRevision, request.Frame, context, out var card, out issue);
                    return new CharacterTacticalContentGrantReceipt(main, card: card);
                case CharacterTacticalContentKind.Accessory:
                    var accessory = AcquireAccessoryAtTick(requestId, new InventoryContainerId(request.TargetContainerId), owner,
                        new ItemDefinitionId(request.ItemDefinitionId), request.ExpectedPlayerRevision, request.Frame, context, out var item, out issue);
                    return new CharacterTacticalContentGrantReceipt(accessory, item);
                case CharacterTacticalContentKind.Action:
                    return new CharacterTacticalContentGrantReceipt(LearnCharacterActionAtTick(requestId, character, owner, request.ActionId,
                        request.ExpectedCharacterRevision, request.ExpectedPlayerRevision, request.Frame, context, out issue));
                default:
                    var role = AcquireCharacterItemAtTick(new ItemAcquisitionRequest(requestId, ItemAcquisitionSourceKind.DirectGrant, null, null),
                        new InventoryContainerId(request.TargetContainerId), owner, new ItemDefinitionId(request.ItemDefinitionId), request.DisplayName,
                        request.ExpectedPlayerRevision, request.Frame, context, out var roleId, out issue);
                    return new CharacterTacticalContentGrantReceipt(role, roleId);
            }
        }

        /// <remarks>LowFreqAlloc. A single detached projection; no directory entry becomes owned merely by reading it.</remarks>
        internal bool TryReadOwnedContent(InventoryOwnerId owner, out CharacterTacticalOwnedContent content, out string issue)
        {
            content = null;
            if (!TryReadOwnedCharacterItems(owner, out var characters, out issue)) return false;
            CharacterTacticalAccessoryItemState[] accessories = Array.Empty<CharacterTacticalAccessoryItemState>();
            if (_assemblyConfiguration != null)
            { if (!TryReadOwnedAccessories(owner, out accessories, out issue)) return false; }
            else if (_state.AccessoryItems?.Count > 0) return Fail("配件正式定义不可用，持有状态保留。", out issue);
            content = new CharacterTacticalOwnedContent { Characters = characters, Accessories = accessories };
            issue = string.Empty; return true;
        }

        /// <remarks>LowFreqAlloc. Detached definition query only; no ownership is created.</remarks>
        internal bool TryReadCardAssemblyConfiguration(out CharacterTacticalAssemblyConfiguration configuration, out string issue)
        {
            configuration = Clone(_assemblyConfiguration);
            if (configuration == null) return Fail("正式装配配置尚未加载。", out issue);
            issue = string.Empty; return true;
        }

        /// <summary>Stages opt-in authored definitions. No ownership, migration, payment or execution occurs.</summary>
        /// <remarks>LowFreqAlloc. A failed refresh keeps the previous configuration and all player data.</remarks>
        internal bool ConfigureCardAssembly(CharacterTacticalAssemblyConfiguration configuration, out string issue)
        {
            try
            {
                if (MutationBlocked || _characterItemDefinitions == null) return Fail("装配需先配置真实角色物品与背包。", out issue);
                var staged = Clone(configuration);
                CharacterTacticalAssemblyRules.ValidateConfiguration(staged, _catalog, _itemDefinitions, _characterItemDefinitions);
                if (_assemblyConfiguration != null && (staged.Revision < _assemblyConfiguration.Revision
                    || staged.Revision == _assemblyConfiguration.Revision && staged.ContentHash != _assemblyConfiguration.ContentHash))
                    return Fail("装配配置不能倒退或在相同版本中改写。", out issue);
                var inventory = CreateItemCandidate(_state.Items, _itemDefinitions, _itemContainers, out _);
                ValidateAssemblyInventory(_state, inventory, _itemDefinitions, _characterItemDefinitions, staged);
                foreach (var role in _state.CharacterItems ?? Enumerable.Empty<CharacterTacticalCharacterItemState>())
                    foreach (var assembly in role.CardAssemblies ?? Enumerable.Empty<CharacterTacticalCardAssembly>())
                        ResolveAssembly(_state, role, inventory, assembly, staged);
                _assemblyConfiguration = staged;
                CatalogRevision++; issue = string.Empty; return true;
            }
            catch (Exception error) { return Fail("装配配置拒绝，保留原状态：" + error.Message, out issue); }
        }

        /// <summary>Detached accessory projection; the existing inventory container proves ownership.</summary>
        /// <remarks>LowFreqAlloc. It does not create a draw result, reserve a copy or enable play.</remarks>
        internal bool TryReadOwnedAccessories(InventoryOwnerId owner, out CharacterTacticalAccessoryItemState[] accessories, out string issue)
        {
            accessories = Array.Empty<CharacterTacticalAccessoryItemState>();
            if (!owner.IsValid || _assemblyConfiguration == null) return Fail("配件归属查询参数或正式配置不可用。", out issue);
            if (!TryReadItemInventory(out var inventory, out issue)) return false;
            accessories = Clone((_state.AccessoryItems ?? new List<CharacterTacticalAccessoryItemState>())
                .Where(value => OwnsInstance(inventory, new ItemInstanceId(value.ItemInstanceId), owner))
                .OrderBy(value => value.ItemInstanceId).ToArray());
            issue = string.Empty; return true;
        }

        /// <summary>Explicit direct accessory grant joins one real Items instance and ledger receipt atomically.</summary>
        /// <remarks>LowFreqAlloc. Rewards, costs, transfers and consumption are outside this foundation API.
        /// AlreadyCommitted returns the original id; mismatched request reuse is rejected. Durability uses FlushAsync.</remarks>
        internal ItemAcquisitionResult AcquireAccessoryAtTick(ItemAcquisitionRequestId requestId, InventoryContainerId target,
            InventoryOwnerId owner, ItemDefinitionId definitionId, long expectedRevision, long frame,
            RuntimeTickContext context, out ItemInstanceId instanceId, out string issue)
        {
            instanceId = default; issue = string.Empty;
            var definition = _assemblyConfiguration?.Accessories.FirstOrDefault(value => value.ItemDefinitionId == definitionId.Value);
            if (MutationBlocked || definition == null || !requestId.IsValid || !owner.IsValid || frame < 0
                || _state.Characters.Count > 0 || _state.CharacterItems?.Any(value => value.TacticalSchema == null) == true
                || context.Stage != RuntimeTickStage.Simulation || context.FrameIndex != frame)
                return AcquisitionFailure("配件授予参数、配置或逻辑帧不可用。", out issue);
            CharacterTacticalPlayerState candidate; ItemInstanceId allocated;
            try
            {
                candidate = Clone(_state);
                var inventory = CreateItemCandidate(candidate.Items, _itemDefinitions, _itemContainers, out var ledger);
                ValidateCharacterItemContent(candidate, inventory, _characterItemDefinitions);
                ValidateAssemblyInventory(candidate, inventory, _itemDefinitions, _characterItemDefinitions);
                if (!inventory.TryGet(target, out var bag) || !bag.OwnerId.Equals(owner))
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidContainer);
                string fingerprint = Digest(JsonConvert.SerializeObject(new { Kind = "accessory", Target = target.Value,
                    Owner = owner.Value, Definition = definitionId.Value, definition.Reference }));
                if (ledger.Contains(requestId))
                {
                    var prior = candidate.AccessoryItems?.Find(value => value.AcquisitionRequestId == requestId.Value);
                    if (prior == null || prior.AcquisitionFingerprint != fingerprint)
                        return AcquisitionFailure("授予请求标识已用于其他载荷。", out issue);
                    instanceId = new ItemInstanceId(prior.ItemInstanceId);
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.AlreadyCommitted);
                }
                if (expectedRevision != Revision || candidate.Items != null && frame < candidate.Items.Frame)
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.StaleRequest);
                if (!inventory.TryAllocateInstanceId(out allocated)) return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.CommitFailed);
                var service = new InventoryService(_itemDefinitions, inventory);
                var inserted = service.TryInsertStack(target, new ItemStack(definitionId, 1, allocated));
                if (!inserted.Success) return ItemAcquisitionResult.Failed(inserted.Status == InventoryOperationStatus.InsufficientCapacity
                    ? ItemAcquisitionStatus.InsufficientCapacity : ItemAcquisitionStatus.CommitFailed, inventoryStatus: inserted.Status);
                var result = new InventoryTransactionService(service, ledger).Execute(
                    new ItemAcquisitionRequest(requestId, ItemAcquisitionSourceKind.DirectGrant, null, null), true);
                if (!result.Success) return result;
                EnableAssemblyState(candidate);
                candidate.AccessoryItems.Add(new CharacterTacticalAccessoryItemState { ItemInstanceId = allocated.Value,
                    ItemDefinitionId = definitionId.Value, Reference = Clone(definition.Reference), AcquisitionRequestId = requestId.Value,
                    AcquisitionFingerprint = fingerprint });
                CaptureItemCandidate(candidate, inventory, ledger, frame); ValidateState(candidate);
                ValidateAssemblyInventory(candidate, inventory, _itemDefinitions, _characterItemDefinitions);
            }
            catch (Exception error) { issue = "配件授予候选失败：" + error.Message; return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.CommitFailed); }
            PublishCharacterCandidate(candidate); instanceId = allocated; return ItemAcquisitionResult.Succeeded();
        }

        /// <summary>Explicit card grant targets a real character, exact publication and its configured acquisition scope.</summary>
        /// <remarks>LowFreqAlloc. Reuses the card allocator and item ledger; does not bind actions or change defaults.</remarks>
        internal ItemAcquisitionResult GrantCharacterCardAtTick(ItemAcquisitionRequestId requestId, ItemInstanceId characterId,
            InventoryOwnerId owner, CharacterTacticalCardReference reference, long expectedCharacterRevision, long expectedRevision,
            long frame, RuntimeTickContext context, out ulong cardInstanceId, out string issue)
        {
            cardInstanceId = 0; issue = string.Empty;
            if (!requestId.IsValid || reference == null || reference.InstanceId != 0)
                return AcquisitionFailure("主卡授予必须引用定义，不能携带持有实例。", out issue);
            if (!TryCharacterCommand(characterId, owner, frame, context, out var candidate,
                out var character, out var inventory, out var ledger, out issue)) return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidRequest);
            if (_catalog.ExactCard(reference)?.CanBeAssemblyMain != true || _catalog.Pool(character.CharacterStableId)?.Cards.Any(value => Same(value, reference)) != true)
                return AcquisitionFailure("卡定义或角色的明确获取范围不可用。", out issue);
            string fingerprint = Digest(JsonConvert.SerializeObject(new { Kind = "card", Character = characterId.Value,
                Owner = owner.Value, reference.CardId, reference.Revision, reference.ContentHash }));
            if (ledger.Contains(requestId))
            {
                var prior = character.CardAcquisitions?.Find(value => value.RequestId == requestId.Value);
                if (prior == null || prior.RequestFingerprint != fingerprint) return AcquisitionFailure("授予请求标识已用于其他载荷或角色。", out issue);
                cardInstanceId = prior.CardInstanceId; return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.AlreadyCommitted);
            }
            if (expectedRevision != Revision || expectedCharacterRevision != character.Revision || frame < candidate.Items.Frame)
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.StaleRequest);
            ulong allocated;
            try
            {
                var result = new InventoryTransactionService(new InventoryService(_itemDefinitions, inventory), ledger).Execute(
                    new ItemAcquisitionRequest(requestId, ItemAcquisitionSourceKind.DirectGrant, null, null), true);
                if (!result.Success) return result;
                allocated = AllocateInstanceId(candidate);
                var held = Clone(reference); held.InstanceId = allocated; character.Cards.Add(held);
                EnableAssemblyState(candidate);
                character.CardAcquisitions ??= new List<CharacterTacticalCardAcquisition>();
                character.CardAcquisitions.Add(new CharacterTacticalCardAcquisition { CardInstanceId = allocated,
                    RequestId = requestId.Value, RequestFingerprint = fingerprint });
            }
            catch (Exception error) { issue = "主卡授予候选失败：" + error.Message; return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.CommitFailed); }
            if (!CommitCharacterEdit(candidate, character, inventory, ledger, frame, out issue)) return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.CommitFailed);
            cardInstanceId = allocated; return ItemAcquisitionResult.Succeeded();
        }

        /// <summary>Common fixed/random data validation and detached freeze, without draw/play eligibility.</summary>
        /// <remarks>LowFreqAlloc. Players submit references only; fixed trigger/failure/lifetime contracts are resolved from authors.</remarks>
        internal bool TryFreezeCardAssembly(ItemInstanceId characterId, InventoryOwnerId owner, CharacterTacticalCardAssembly assembly,
            CharacterTacticalAssemblyMode mode, out CharacterTacticalAssemblySnapshot snapshot, out string issue)
        {
            snapshot = null;
            if (mode != CharacterTacticalAssemblyMode.FixedPreassembled && mode != CharacterTacticalAssemblyMode.RandomDrawAssembly)
                return Fail("装配模式无效。", out issue);
            if (!TryReadCharacterItem(characterId, owner, out var character, out issue)) return false;
            if (!TryReadItemInventory(out var inventory, out issue)) return false;
            try
            {
                snapshot = ResolveAssembly(_state, character, inventory, Clone(assembly), _assemblyConfiguration);
                snapshot.Mode = mode; issue = string.Empty; return true;
            }
            catch (Exception error) { return Fail("装配数据拒绝：" + error.Message, out issue); }
        }

        /// <summary>Freezes the stored selection for a real owned role/card into one deployment definition.</summary>
        /// <remarks>LowFreqAlloc. A missing selection preserves the original bare-card path; data-only selections
        /// cannot silently become executable. This neither admits play nor writes inventory or progress.</remarks>
        internal bool TryFreezeStoredCardAssembly(ItemInstanceId characterId, InventoryOwnerId owner,
            TacticalCardDefinition battleDefinition, IReadOnlyDictionary<string, int> bindings,
            out TacticalFixedAssemblyManifest manifest, out string issue)
        {
            manifest = null;
            if (battleDefinition == null || !TryReadCharacterItem(characterId, owner, out var character, out issue))
                return Fail("固定装配角色或主卡不可用。", out issue);
            var stored = character.CardAssemblies?.Find(value => value.MainCard.InstanceId == battleDefinition.DeckEntryInstanceId);
            if (stored == null) { issue = string.Empty; return true; }
            if (_assemblyConfiguration?.Schema != CharacterTacticalAssemblyConfiguration.ExecutableSchema)
                return Fail("固定装配尚无可执行版本配置，保留原进度。", out issue);
            if (!TryFreezeCardAssembly(characterId, owner, stored, CharacterTacticalAssemblyMode.FixedPreassembled,
                out var snapshot, out issue)) return false;
            if (!TacticalFixedAssemblyManifest.TryCreate(snapshot, battleDefinition, characterId.Value, owner.Value,
                character.Revision, bindings, out manifest, out issue))
                return Fail("固定装配缺少精确可执行配置，保留原进度。" + issue, out issue);
            return true;
        }

        /// <summary>Saves a fixed prebattle selection on this held card, in the existing character/player candidate.</summary>
        /// <remarks>LowFreqAlloc. This does not reserve copies for a battle or create a playable composite.</remarks>
        internal bool SetCharacterCardAssemblyAtTick(ItemInstanceId characterId, InventoryOwnerId owner, CharacterTacticalCardAssembly assembly,
            long expectedCharacterRevision, long expectedRevision, long frame, RuntimeTickContext context, out string issue)
        {
            if (!TryCharacterCommand(characterId, owner, frame, context, out var candidate,
                out var character, out var inventory, out var ledger, out issue)) return false;
            if (expectedRevision != Revision || expectedCharacterRevision != character.Revision || frame < candidate.Items.Frame)
                return Fail("角色或进度版本已变化。", out issue);
            try
            {
                var staged = ResolveAssembly(candidate, character, inventory, Clone(assembly), _assemblyConfiguration).Assembly;
                character.CardAssemblies ??= new List<CharacterTacticalCardAssembly>();
                var prior = character.CardAssemblies.Find(value => value.MainCard.InstanceId == staged.MainCard.InstanceId);
                if (JsonConvert.SerializeObject(prior) == JsonConvert.SerializeObject(staged)) { issue = string.Empty; return true; }
                character.CardAssemblies.RemoveAll(value => value.MainCard.InstanceId == staged.MainCard.InstanceId);
                character.CardAssemblies.Add(staged); character.CardAssemblies.Sort((a, b) => a.MainCard.InstanceId.CompareTo(b.MainCard.InstanceId));
                EnableAssemblyState(candidate);
            }
            catch (Exception error) { return Fail("装配候选拒绝，保留原状态：" + error.Message, out issue); }
            return CommitCharacterEdit(candidate, character, inventory, ledger, frame, out issue);
        }

        private static void EnableAssemblyState(CharacterTacticalPlayerState candidate)
        {
            candidate.AccessoryItems ??= new List<CharacterTacticalAccessoryItemState>();
            candidate.CharacterItems ??= new List<CharacterTacticalCharacterItemState>();
        }
        private static ItemAcquisitionResult AcquisitionFailure(string message, out string issue)
        { issue = message; return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidRequest); }

        private void ValidateAssemblyInventory(CharacterTacticalPlayerState state, InventoryStore inventory,
            ItemDefinitionRegistry definitions, Dictionary<int, string> characters, CharacterTacticalAssemblyConfiguration configuration = null)
        {
            var config = configuration ?? _assemblyConfiguration;
            if (config == null) return;
            CharacterTacticalAssemblyRules.ValidateConfiguration(config, _catalog, definitions, characters);
            var unmatched = (state.AccessoryItems ?? new List<CharacterTacticalAccessoryItemState>()).ToDictionary(value => value.ItemInstanceId);
            foreach (var bag in inventory.CreateContainerSnapshot())
                for (int slot = 0; slot < bag.SlotCount; slot++)
                {
                    var stack = bag.GetStack(slot);
                    var definition = config.Accessories.FirstOrDefault(value => value.ItemDefinitionId == stack.DefinitionId.Value);
                    if (definition == null) continue;
                    if (stack.Count != 1 || !stack.InstanceId.IsValid || !unmatched.TryGetValue(stack.InstanceId.Value, out var record)
                        || record.ItemDefinitionId != definition.ItemDefinitionId || !CharacterTacticalAssemblyRules.Same(record.Reference, definition.Reference))
                        throw new JsonSerializationException("TACTICAL_ACCESSORY_ITEM_JOIN_INVALID");
                    unmatched.Remove(stack.InstanceId.Value);
                }
            if (unmatched.Count != 0) throw new JsonSerializationException("TACTICAL_ACCESSORY_REFERENCE_UNAVAILABLE");
        }

        private CharacterTacticalAssemblySnapshot ResolveAssembly(CharacterTacticalPlayerState state, CharacterTacticalCharacterItemState character,
            InventoryStore inventory, CharacterTacticalCardAssembly assembly, CharacterTacticalAssemblyConfiguration config)
        {
            if (config == null || assembly == null || assembly.Schema != CharacterTacticalCardAssembly.CurrentSchema
                || assembly.ConfigurationRevision != config.Revision || assembly.ConfigurationHash != config.ContentHash
                || assembly.MainCard == null || assembly.MainCard.InstanceId == 0 || assembly.Accessories == null)
                throw new JsonSerializationException("TACTICAL_ASSEMBLY_VERSION_UNAVAILABLE");
            var held = character.Cards?.Find(value => value.InstanceId == assembly.MainCard.InstanceId);
            var main = config.MainCards.FirstOrDefault(value => CharacterTacticalAssemblyRules.Same(value.Card, assembly.MainCard));
            if (!CharacterTacticalAssemblyRules.Same(held, assembly.MainCard) || main == null || _catalog.ExactCard(held)?.CanBeAssemblyMain != true)
                throw new JsonSerializationException("TACTICAL_ASSEMBLY_MAIN_REFERENCE_UNAVAILABLE");
            var owner = inventory.CreateContainerSnapshot().FirstOrDefault(bag => Enumerable.Range(0, bag.SlotCount)
                .Any(slot => bag.GetStack(slot).InstanceId.Value == character.ItemInstanceId))?.OwnerId ?? default;
            var slots = new HashSet<int>(); var copies = new HashSet<int>(); var names = new HashSet<string>(StringComparer.Ordinal);
            var resolved = new List<CharacterTacticalAccessoryDefinition>();
            foreach (var slot in assembly.Accessories.OrderBy(value => value?.SlotIndex ?? -1))
            {
                if (slot == null || slot.SlotIndex < 0 || slot.SlotIndex >= main.AccessorySlotCount || !slots.Add(slot.SlotIndex))
                    throw new JsonSerializationException("TACTICAL_ASSEMBLY_CAPACITY_INVALID");
                var owned = state.AccessoryItems?.Find(value => value.ItemInstanceId == slot.AccessoryItemInstanceId);
                var definition = config.Accessories.FirstOrDefault(value => CharacterTacticalAssemblyRules.Same(value.Reference, slot.Reference));
                if (owned == null || definition == null || !CharacterTacticalAssemblyRules.Same(owned.Reference, slot.Reference)
                    || !OwnsInstance(inventory, new ItemInstanceId(owned.ItemInstanceId), owner))
                    throw new JsonSerializationException("TACTICAL_ASSEMBLY_ACCESSORY_REFERENCE_UNAVAILABLE");
                if (!copies.Add(owned.ItemInstanceId) || !names.Add(owned.Reference.AccessoryId))
                    throw new JsonSerializationException("配件同副本或同名重复规则待定，当前基础接口尚不支持此请求。");
                if (state.CharacterItems.Any(role => role.CardAssemblies?.Any(other => other.MainCard.InstanceId != assembly.MainCard.InstanceId
                    && other.Accessories.Any(value => value.AccessoryItemInstanceId == owned.ItemInstanceId)) == true))
                    throw new JsonSerializationException("跨牌共享配件副本规则待定，当前基础接口尚不支持此请求。");
                resolved.Add(Clone(definition));
            }
            var frozen = Clone(assembly); frozen.Accessories.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
            return new CharacterTacticalAssemblySnapshot { Assembly = frozen, Quality = main.Quality,
                AccessorySlotCount = main.AccessorySlotCount, Accessories = resolved.ToArray() };
        }
    }
}
