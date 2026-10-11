using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using MxFramework.CharacterApplication;
using MxFramework.Items;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public enum TacticalPreparationState { Preparation, Deploying, Battle, Home, Completed }

    internal sealed class TacticalDeploymentParticipant
    {
        internal TacticalDeploymentParticipant(string entityId, int characterId, string characterStableId,
            string displayName, int teamId, bool isPlayerControlled, Vector3 position, float yawDegrees,
            TacticalCardDefinition[] cards, Dictionary<ulong, ReadOnlyDictionary<string, int>> bindings,
            int characterItemInstanceId = 0, int inventoryOwnerId = 0, long characterItemRevision = 0)
        {
            EntityId = entityId; CharacterId = characterId; CharacterStableId = characterStableId;
            DisplayName = displayName; TeamId = teamId; IsPlayerControlled = isPlayerControlled;
            Position = position; YawDegrees = yawDegrees;
            CharacterItemInstanceId = characterItemInstanceId; InventoryOwnerId = inventoryOwnerId; CharacterItemRevision = characterItemRevision;
            Cards = Array.AsReadOnly((cards ?? Array.Empty<TacticalCardDefinition>()).ToArray());
            var frozen = new Dictionary<ulong, ReadOnlyDictionary<string, int>>();
            foreach (var pair in bindings ?? new Dictionary<ulong, ReadOnlyDictionary<string, int>>())
                frozen.Add(pair.Key, new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(pair.Value)));
            Bindings = new ReadOnlyDictionary<ulong, ReadOnlyDictionary<string, int>>(frozen);
        }
        internal int CharacterItemInstanceId { get; }
        internal int InventoryOwnerId { get; }
        internal long CharacterItemRevision { get; }
        internal string EntityId { get; }
        internal int CharacterId { get; }
        internal string CharacterStableId { get; }
        internal string DisplayName { get; }
        internal int TeamId { get; }
        internal bool IsPlayerControlled { get; }
        internal Vector3 Position { get; }
        internal float YawDegrees { get; }
        internal IReadOnlyList<TacticalCardDefinition> Cards { get; }
        internal ReadOnlyDictionary<ulong, ReadOnlyDictionary<string, int>> Bindings { get; }
        internal bool TryGetBindings(ulong instanceId, out IReadOnlyDictionary<string, int> bindings)
        {
            bool found = Bindings.TryGetValue(instanceId, out ReadOnlyDictionary<string, int> value);
            bindings = value; return found;
        }
        internal bool TryGetFixedAssembly(ulong entryInstance, out TacticalFixedAssemblyManifest assembly)
        {
            foreach (var card in Cards)
                if (card.DeckEntryInstanceId == entryInstance && card.FixedAssembly != null)
                { assembly = card.FixedAssembly; return true; }
            assembly = null; return false;
        }
    }

    /// <summary>Owns the exact character identities, published versions and slot bindings confirmed for one battle.</summary>
    internal sealed class TacticalDeployment
    {
        private readonly IReadOnlyDictionary<ulong, ReadOnlyDictionary<string, int>> _bindings;
        private readonly ReadOnlyCollection<TacticalCardDefinition> _cards;
        private readonly ReadOnlyCollection<TacticalDeploymentParticipant> _participants;
        private TacticalDeployment(int player, int opponent, string character, int initialMotion,
            TacticalCardDefinition[] cards, Dictionary<ulong, ReadOnlyDictionary<string, int>> bindings,
            bool usesLegacyFunctionalResolverFallback)
        {
            PlayerCharacterId = player; OpponentCharacterId = opponent; CharacterStableId = character;
            InitialMotion = initialMotion; _cards = Array.AsReadOnly(cards); _bindings = bindings;
            UsesLegacyFunctionalResolverFallback = usesLegacyFunctionalResolverFallback;
            _participants = Array.AsReadOnly(new[]
            {
                new TacticalDeploymentParticipant("legacy-player", player, character, "Player", 1,
                    true, Vector3.zero, 0f, cards, bindings),
                new TacticalDeploymentParticipant("legacy-opponent", opponent, string.Empty, "Opponent", 2,
                    false, Vector3.zero, 180f, Array.Empty<TacticalCardDefinition>(), new Dictionary<ulong, ReadOnlyDictionary<string, int>>())
            });
        }
        private TacticalDeployment(TacticalDeploymentParticipant[] participants, int initialMotion)
        {
            _participants = Array.AsReadOnly((participants ?? Array.Empty<TacticalDeploymentParticipant>()).ToArray());
            InitialMotion = initialMotion;
            TacticalDeploymentParticipant first = _participants.FirstOrDefault(value => value.IsPlayerControlled)
                ?? _participants.FirstOrDefault(value => value.TeamId == 1) ?? _participants.FirstOrDefault();
            TacticalDeploymentParticipant opponent = _participants.FirstOrDefault(value => first != null && value.TeamId != first.TeamId);
            PlayerCharacterId = first?.CharacterId ?? 0;
            OpponentCharacterId = opponent?.CharacterId ?? 0;
            CharacterStableId = first?.CharacterStableId ?? string.Empty;
            TacticalCardDefinition[] cards = first?.Cards?.ToArray() ?? Array.Empty<TacticalCardDefinition>();
            _cards = Array.AsReadOnly(cards);
            _bindings = first?.Bindings ?? new ReadOnlyDictionary<ulong, ReadOnlyDictionary<string, int>>(new Dictionary<ulong, ReadOnlyDictionary<string, int>>());
        }
        internal static TacticalDeployment CreateLevel(TacticalDeploymentParticipant[] participants, int initialMotion)
            => new TacticalDeployment(participants, initialMotion);

        internal int PlayerCharacterId { get; }
        internal int OpponentCharacterId { get; }
        internal string CharacterStableId { get; }
        internal int InitialMotion { get; }
        internal TacticalCardTrialOptions CardTrial { get; private set; }

        internal static TacticalDeployment CreateLabTrial(TacticalDeploymentParticipant[] participants, int initialMomentum,
            TacticalCardTrialOptions options)
        {
            if (options == null || participants == null || participants.Count(item => item.IsPlayerControlled) != 1
                || participants.Length < 2 || !participants.Any(item => item.TeamId == 1) || !participants.Any(item => item.TeamId == 2))
                throw new ArgumentException("A Lab trial requires one player and an opposing roster.");
            return new TacticalDeployment(participants, initialMomentum) { CardTrial = options };
        }
        internal bool UsesLegacyFunctionalResolverFallback { get; }
        internal IReadOnlyList<TacticalCardDefinition> Cards => _cards;
        internal IReadOnlyList<TacticalDeploymentParticipant> Participants => _participants;
        internal TacticalDeploymentParticipant PlayerParticipant =>
            _participants.FirstOrDefault(value => value.IsPlayerControlled);
        internal bool HasPlayerControlledParticipant => PlayerParticipant != null;
        internal bool TryGetBindings(ulong instanceId, out IReadOnlyDictionary<string, int> bindings)
        {
            bool found = _bindings.TryGetValue(instanceId, out var result);
            bindings = result; return found;
        }
        internal bool Contains(TacticalActionCardDefinition candidate)
        {
            foreach (TacticalDeploymentParticipant participant in _participants)
            foreach (var definition in participant.Cards)
                if (definition is TacticalActionCardDefinition card && card.PublishedCardId == candidate.PublishedCardId
                    && card.PublishedRevision == candidate.PublishedRevision && card.PublishedContentHash == candidate.PublishedContentHash
                    && card.DeckEntryInstanceId == candidate.DeckEntryInstanceId) return true;
            return false;
        }

        internal bool TryGetBindings(string entityId, ulong instanceId, out IReadOnlyDictionary<string, int> bindings)
        {
            TacticalDeploymentParticipant participant = _participants.FirstOrDefault(value => value.EntityId == entityId);
            if (participant != null && participant.Bindings.TryGetValue(instanceId, out ReadOnlyDictionary<string, int> result))
            { bindings = result; return true; }
            bindings = null; return false;
        }

        internal static TacticalCardDefinition CreateDefinition(CharacterTacticalCardReference reference, CharacterTacticalCardDefinition metadata)
            => metadata?.InteractionPreparation != null ? TacticalUtilityCardDefinition.CreatePublishedPreparation(reference, metadata) : CreateCard(reference, metadata);

        internal bool ContainsDefinition(TacticalCardDefinition candidate)
        {
            if (candidate == null || !candidate.HasPublishedBehavior) return false;
            foreach (var participant in _participants)
                foreach (var card in participant.Cards)
                    if (card.PublishedCardId == candidate.PublishedCardId && card.PublishedRevision == candidate.PublishedRevision
                        && card.PublishedContentHash == candidate.PublishedContentHash && card.DeckEntryInstanceId == candidate.DeckEntryInstanceId)
                        return true;
            return false;
        }

        internal static TacticalActionCardDefinition CreateCard(CharacterTacticalCardReference reference, CharacterTacticalCardDefinition metadata)
        {
            if (reference == null || metadata == null || reference.CardId != metadata.CardId
                || reference.Revision != metadata.Revision || reference.ContentHash != metadata.ContentHash)
                throw new ArgumentException("Exact published card metadata required.");
            string name = string.IsNullOrWhiteSpace(metadata.DisplayName) ? reference.CardId : metadata.DisplayName;
            if (metadata.PositionRecall != null)
                return TacticalActionCardDefinition.CreatePositionRecall(reference.CardId, name, metadata.Description, metadata.MotionCost,
                    metadata.PositionRecall, reference.CardId, reference.Revision, reference.ContentHash, reference.InstanceId, metadata.IconKey);
            if (metadata.PointMove != null)
                return TacticalActionCardDefinition.CreatePublishedPointMove(reference.CardId, name, metadata.Description,
                    metadata.MotionCost, metadata.PointMove, metadata.CooldownSeconds, reference.CardId,
                    metadata.Revision, metadata.ContentHash, reference.InstanceId, metadata.IconKey);
            return new TacticalActionCardDefinition(reference.CardId, name, metadata.Description, metadata.MotionCost,
                reference.CardId, metadata.Revision, metadata.ContentHash, reference.InstanceId, metadata.IconKey, metadata.CooldownSeconds);
        }

        internal static bool TryCreate(TacticalBattleSetup setup, out TacticalDeployment deployment, out string issue)
            => TryCreate(CharacterTacticalServices.Player, setup, out deployment, out issue);

        internal static bool TryCreate(CharacterTacticalPlayerService service, TacticalBattleSetup setup,
            out TacticalDeployment deployment, out string issue, CharacterTestCharacterDefinition[] characterDefinitions = null)
        {
            deployment = null; issue = string.Empty;
            if (service == null || setup?.Entities == null)
            { issue = "战术服务或整备配置尚未加载。"; return false; }
            if (setup.Entities.Any(value => value == null) || setup.Entities.Count == 0 || !setup.Entities.Any(value => value.TeamId == 1)
                || !setup.Entities.Any(value => value.TeamId == 2))
            { issue = "阵营 A 和阵营 B 都至少需要一个实体。"; return false; }
            var participants = new List<TacticalDeploymentParticipant>(setup.Entities.Count);
            var entityIds = new HashSet<string>(StringComparer.Ordinal);
            var itemIds = new HashSet<int>();
            foreach (TacticalBattleEntitySetup entity in setup.Entities)
            {
                if (entity == null || string.IsNullOrWhiteSpace(entity.EntityId) || !entityIds.Add(entity.EntityId)
                    || entity.TeamId <= 0 || entity.Loadout == null)
                { issue = "整备实体数据无效。"; return false; }
                bool itemBound = entity.CharacterItemInstanceId.HasValue || entity.InventoryOwnerId.HasValue;
                CharacterTacticalDefaults loadout = entity.Loadout;
                int itemId = entity.CharacterItemInstanceId.GetValueOrDefault(), ownerId = entity.InventoryOwnerId.GetValueOrDefault();
                long itemRevision = 0;
                if (itemBound)
                {
                    if (itemId <= 0 || ownerId <= 0 || !itemIds.Add(itemId))
                    { issue = "角色物品引用重复或不完整。"; return false; }
                    if (!service.TryReadCharacterItemLoadout(new ItemInstanceId(itemId), new InventoryOwnerId(ownerId),
                        out loadout, out itemRevision, out issue)) return false;
                    var definitions = characterDefinitions ?? CharacterTestPlayableCharacterCatalog.Discover().ToArray();
                    if (definitions.Count(value => value.CharacterId == entity.CharacterId
                            && value.TacticalDefaults?.CharacterStableId == loadout.CharacterStableId) != 1
                        || !TacticalBattlefieldLayout.ContainsPlacement(entity.Position) || !float.IsFinite(entity.Y)
                        || !float.IsFinite(entity.YawDegrees))
                    { issue = "出战角色模板或位置无效。"; return false; }
                    if (entity.CharacterStableId != loadout.CharacterStableId || entity.CharacterItemRevision != itemRevision
                        || JsonConvert.SerializeObject(entity.Loadout) != JsonConvert.SerializeObject(loadout))
                    { issue = "角色物品配置已变化或快照不一致，请重新确认整备。"; return false; }
                }
                else if (service.UsesCharacterItemTacticalProgress)
                { issue = "出战必须明确选择角色物品实例。"; return false; }
                var cards = new List<TacticalCardDefinition>();
                var bindings = new Dictionary<ulong, ReadOnlyDictionary<string, int>>();
                foreach (CharacterTacticalCardReference reference in loadout.Cards)
                {
                    CharacterTacticalCardDefinition metadata = service.Catalog.ExactCard(reference);
                    if (metadata == null) { issue = entity.DisplayName + " 存在失效行为卡。"; return false; }
                    if (entity.EntityId == setup.PlayerControlledEntityId && metadata.PresentationSource != null
                        && !metadata.PresentationSource.TryValidatePlayerExecution(out issue))
                    { issue = entity.DisplayName + " / " + metadata.DisplayName + "：" + issue; return false; }
                    var resolved = new Dictionary<string, int>(StringComparer.Ordinal);
                    if (itemBound)
                    {
                        if (!service.TryGetCharacterItemBindings(new ItemInstanceId(itemId), new InventoryOwnerId(ownerId),
                            reference.InstanceId, out var actions, out issue)) return false;
                        foreach (var pair in actions) resolved.Add(pair.Key, pair.Value.ActionId);
                    }
                    else if (metadata.HasActionSlot)
                    {
                        CharacterTacticalSlotBinding binding = entity.Loadout.Bindings.Find(value =>
                            value.CardInstanceId == reference.InstanceId && value.SlotPath == metadata.ActionSlotPath);
                        CharacterTacticalActionDefinition action = binding == null ? null : service.Catalog.Action(binding.TacticalActionId);
                        if (action == null || !action.CharacterStableIds.Contains(entity.CharacterStableId)
                            || !service.Catalog.ContainsAction(service.Read(entity.CharacterStableId)?.ActionIds, action.Id))
                        { issue = entity.DisplayName + " 的“" + metadata.DisplayName + "”尚未配置有效动作。"; return false; }
                        resolved.Add(metadata.ActionSlotPath, action.ActionId);
                    }
                    var definition = CreateDefinition(reference, metadata);
                    if (itemBound)
                    {
                        if (!service.TryFreezeStoredCardAssembly(new ItemInstanceId(itemId), new InventoryOwnerId(ownerId),
                            definition, resolved, out var fixedAssembly, out issue)) return false;
                        if (fixedAssembly != null) definition.AttachFixedAssembly(fixedAssembly);
                    }
                    bindings.Add(reference.InstanceId, new ReadOnlyDictionary<string, int>(resolved));
                    cards.Add(definition);
                }
                participants.Add(new TacticalDeploymentParticipant(entity.EntityId, entity.CharacterId,
                    entity.CharacterStableId, entity.DisplayName, entity.TeamId,
                    entity.EntityId == setup.PlayerControlledEntityId, entity.Position,
                    entity.YawDegrees, cards.ToArray(), bindings, itemId, ownerId, itemRevision));
            }
            deployment = new TacticalDeployment(participants.ToArray(), service.Catalog.InitialMotion);
            return true;
        }

        internal static bool TryCreate(int playerId, int opponentId, out TacticalDeployment deployment, out string issue)
        {
            deployment = null; issue = string.Empty;
            var service = CharacterTacticalServices.Player;
            if (service == null) { issue = string.IsNullOrEmpty(CharacterTacticalServices.Issue) ? "正在加载角色和存档，请稍候。" : CharacterTacticalServices.Issue; return false; }
            var characters = CharacterTestPlayableCharacterCatalog.Discover();
            var player = characters.FirstOrDefault(c => c.CharacterId == playerId);
            if (player.TacticalDefaults == null || !characters.Any(c => c.CharacterId == opponentId))
            { issue = "请选择已导入的出战角色和对手，并为出战角色配置战术内容。"; return false; }
            var owned = service.Read(player.TacticalDefaults.CharacterStableId);
            if (owned == null || owned.Cards.Count == 0) { issue = "当前牌库没有行为卡，请先编辑牌库。"; return false; }
            var cards = new List<TacticalCardDefinition>();
            var bindings = new Dictionary<ulong, ReadOnlyDictionary<string, int>>();
            bool usesLegacyFunctionalResolverFallback = false;
            foreach (var reference in owned.Cards)
            {
                var metadata = service.Catalog.ExactCard(reference);
                if (metadata == null) { issue = "行为卡版本已失效：" + reference.CardId; return false; }
                if (metadata.PresentationSource != null && !metadata.PresentationSource.TryValidatePlayerExecution(out issue))
                { issue = metadata.DisplayName + "：" + issue; return false; }
                var slots = new Dictionary<string, int>(StringComparer.Ordinal);
                if (service.TryGetBindings(owned.CharacterStableId, reference.InstanceId, out var actions, out _))
                    foreach (var pair in actions) slots.Add(pair.Key, pair.Value.ActionId);
                else
                { issue = "行为卡缺少当前已拥有动作的明确绑定：" + reference.CardId; return false; }
                bindings.Add(reference.InstanceId, new ReadOnlyDictionary<string, int>(slots));
                cards.Add(CreateDefinition(reference, metadata));
            }
            deployment = new TacticalDeployment(playerId, opponentId, owned.CharacterStableId, service.Catalog.InitialMotion,
                cards.ToArray(), bindings, usesLegacyFunctionalResolverFallback);
            return true;
        }
    }
}
