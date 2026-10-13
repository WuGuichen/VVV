using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiLabCardTrialSelection
    {
        internal const string TestPreparationId = "lab.test-preparation";
        internal string CardId, TacticalActionId;
        internal bool FixedAssembly;
        internal CharacterTacticalCardQuality FixedQuality = CharacterTacticalCardQuality.Silver;
        internal TacticalAccessoryCombatKind[] AccessorySlots = new TacticalAccessoryCombatKind[3];
        internal CharacterTestRuntimeAiLabCardTrialSelection(string cardId, string actionId = "")
        { CardId = cardId; TacticalActionId = actionId; }
        internal CharacterTestRuntimeAiLabCardTrialSelection Clone() => new(CardId, TacticalActionId)
        { FixedAssembly = FixedAssembly, FixedQuality = FixedQuality, AccessorySlots = (TacticalAccessoryCombatKind[])AccessorySlots.Clone() };
    }

    internal sealed class CharacterTestRuntimeAiLabTrialParticipant
    {
        internal int CharacterId;
        internal Vector3 Position = new(0, 1, 1);
        internal float Facing;
        internal readonly List<CharacterTestRuntimeAiLabCardTrialSelection> Cards = new();
        internal CharacterTestRuntimeAiLabTrialParticipant Clone()
        {
            var copy = new CharacterTestRuntimeAiLabTrialParticipant { CharacterId = CharacterId, Position = Position, Facing = Facing };
            copy.Cards.AddRange(Cards.Select(card => card.Clone())); return copy;
        }
    }

    /// <summary>Authoring-only initial conditions. Published content is read-only; no player service or persistent store is opened.</summary>
    internal sealed class CharacterTestRuntimeAiLabCardTrialConfiguration
    {
        internal int PlayerCharacterId, OpponentCharacterId, InitialMomentum = 50, Seed = 73, PreparationCost = 5;
        internal CharacterTestRuntimeAiLabCardTrialSelection OpponentCard;
        internal bool CloseStart;
        internal readonly List<CharacterTestRuntimeAiLabCardTrialSelection> InitialHand = new();
        internal bool CustomPlacement;
        internal bool UseExplicitDecks;
        internal Vector3 PlayerPosition = new(-.6f, 1, 0), OpponentPosition = new(.6f, 1, 0);
        internal float PlayerFacing = 90, OpponentFacing = 270;
        internal readonly List<CharacterTestRuntimeAiLabCardTrialSelection> OpponentDeck = new();
        internal readonly List<CharacterTestRuntimeAiLabTrialParticipant> AdditionalParticipants = new();
        internal int ParticipantCount => 2 + AdditionalParticipants.Count;
        internal int CharacterAt(int index) => index == 0 ? PlayerCharacterId : index == 1 ? OpponentCharacterId : AdditionalParticipants[index - 2].CharacterId;
        internal List<CharacterTestRuntimeAiLabCardTrialSelection> DeckAt(int index) => index == 0 ? InitialHand : index == 1 ? OpponentDeck : AdditionalParticipants[index - 2].Cards;
        internal Vector3 PositionAt(int index) => index > 1 ? AdditionalParticipants[index - 2].Position : CustomPlacement
            ? (index == 0 ? PlayerPosition : OpponentPosition) : CloseStart
            ? new Vector3(index == 0 ? -.6f : .6f, 1, 0) : new Vector3(index == 0 ? -2 : 2, 1, index == 0 ? -1 : 1);
        internal float FacingAt(int index) => index > 1 ? AdditionalParticipants[index - 2].Facing : CustomPlacement
            ? (index == 0 ? PlayerFacing : OpponentFacing) : index == 0 ? (CloseStart ? 90 : 45) : (CloseStart ? 270 : 225);
        internal void SetCharacterAt(int index, int characterId)
        { if (index == 0) PlayerCharacterId = characterId; else if (index == 1) OpponentCharacterId = characterId; else AdditionalParticipants[index - 2].CharacterId = characterId; }
        internal void SetPlacement(int index, Vector3 position, float facing)
        {
            if (!CustomPlacement) { PlayerPosition = PositionAt(0); OpponentPosition = PositionAt(1); PlayerFacing = FacingAt(0); OpponentFacing = FacingAt(1); CustomPlacement = true; }
            if (index == 0) { PlayerPosition = position; PlayerFacing = facing; }
            else if (index == 1) { OpponentPosition = position; OpponentFacing = facing; }
            else { AdditionalParticipants[index - 2].Position = position; AdditionalParticipants[index - 2].Facing = facing; }
        }
        internal CharacterTestRuntimeAiLabCardTrialConfiguration Clone()
        {
            var value = new CharacterTestRuntimeAiLabCardTrialConfiguration
            { PlayerCharacterId = PlayerCharacterId, OpponentCharacterId = OpponentCharacterId,
                InitialMomentum = InitialMomentum, Seed = Seed, PreparationCost = PreparationCost,
                OpponentCard = OpponentCard?.Clone(), CloseStart = CloseStart, CustomPlacement = CustomPlacement, UseExplicitDecks = UseExplicitDecks,
                PlayerPosition = PlayerPosition, OpponentPosition = OpponentPosition, PlayerFacing = PlayerFacing, OpponentFacing = OpponentFacing };
            value.OpponentDeck.AddRange(OpponentDeck.Select(card => card.Clone()));
            value.AdditionalParticipants.AddRange(AdditionalParticipants.Select(role => role.Clone()));
            value.InitialHand.AddRange(InitialHand.Select(item => item.Clone())); return value;
        }
        internal bool HasSameConditions(CharacterTestRuntimeAiLabCardTrialConfiguration other)
        {
            if (other == null || PlayerCharacterId != other.PlayerCharacterId || OpponentCharacterId != other.OpponentCharacterId
                || InitialMomentum != other.InitialMomentum || Seed != other.Seed || PreparationCost != other.PreparationCost
                || CloseStart != other.CloseStart || InitialHand.Count != other.InitialHand.Count
                || CustomPlacement != other.CustomPlacement || UseExplicitDecks != other.UseExplicitDecks || ParticipantCount != other.ParticipantCount
                || !HasSameParticipantConditions(other)
                || (OpponentCard == null) != (other.OpponentCard == null)
                || OpponentCard != null && (OpponentCard.CardId != other.OpponentCard.CardId
                    || OpponentCard.TacticalActionId != other.OpponentCard.TacticalActionId
                    || OpponentCard.FixedAssembly != other.OpponentCard.FixedAssembly
                    || OpponentCard.FixedQuality != other.OpponentCard.FixedQuality
                    || !OpponentCard.AccessorySlots.SequenceEqual(other.OpponentCard.AccessorySlots))) return false;
            for (int i = 0; i < InitialHand.Count; i++)
                if (InitialHand[i].CardId != other.InitialHand[i].CardId || InitialHand[i].TacticalActionId != other.InitialHand[i].TacticalActionId
                    || InitialHand[i].FixedAssembly != other.InitialHand[i].FixedAssembly
                    || InitialHand[i].FixedAssembly && (InitialHand[i].FixedQuality != other.InitialHand[i].FixedQuality
                        || !InitialHand[i].AccessorySlots.SequenceEqual(other.InitialHand[i].AccessorySlots)))
                    return false;
            return true;
        }
        private bool HasSameParticipantConditions(CharacterTestRuntimeAiLabCardTrialConfiguration other)
        {
            for (int role = 0; role < ParticipantCount; role++)
            {
                if (CharacterAt(role) != other.CharacterAt(role) || PositionAt(role) != other.PositionAt(role)
                    || FacingAt(role) != other.FacingAt(role)) return false;
                var left = DeckAt(role); var right = other.DeckAt(role);
                if (left.Count != right.Count) return false;
                for (int i = 0; i < left.Count; i++)
                    if (left[i].CardId != right[i].CardId || left[i].TacticalActionId != right[i].TacticalActionId
                        || left[i].FixedAssembly != right[i].FixedAssembly || left[i].FixedQuality != right[i].FixedQuality
                        || !left[i].AccessorySlots.SequenceEqual(right[i].AccessorySlots)) return false;
            }
            return true;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabCardTrialContent
    {
        internal readonly CharacterTacticalContentCatalog Catalog;
        internal readonly CharacterTestRuntimeAiPlannerProjectCatalogState Published;
        internal readonly CharacterTestCharacterDefinition[] Characters;
        internal CharacterTestRuntimeAiLabCardTrialContent(CharacterTacticalContentCatalog catalog,
            CharacterTestRuntimeAiPlannerProjectCatalogState published, CharacterTestCharacterDefinition[] characters)
        { Catalog = catalog; Published = published; Characters = characters; }

        internal static CharacterTestRuntimeAiLabCardTrialContent Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "MxFramework/CharacterTest/tactical_content.json");
            var catalog = JsonConvert.DeserializeObject<CharacterTacticalContentCatalog>(File.ReadAllText(path));
            if (catalog == null) throw new InvalidOperationException("真实出牌测试的正式内容配置缺失。");
            CharacterTacticalActionEligibility.Load().Project(catalog, CharacterTestActionBrowserCatalog.Get());
            var published = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!published.Success) throw new InvalidOperationException(published.Diagnostic);
            catalog.SetPublishedCards(published.CardCatalog.Cards); catalog.Validate();
            var characters = CharacterTestPlayableCharacterCatalog.Discover().Where(item => item.TacticalDefaults != null).ToArray();
            if (characters.Length == 0) throw new InvalidOperationException("没有可用于真实出牌测试的已导入角色。");
            return new CharacterTestRuntimeAiLabCardTrialContent(catalog, published, characters);
        }

        internal CharacterTestRuntimeAiLabCardTrialConfiguration CreateDefault()
        {
            var value = new CharacterTestRuntimeAiLabCardTrialConfiguration
            { PlayerCharacterId = Characters[0].CharacterId, OpponentCharacterId = Characters[Math.Min(1, Characters.Length - 1)].CharacterId };
            var action = DefaultActionCard(value.PlayerCharacterId);
            value.InitialHand.Add(action.Clone());
            var point = Catalog.Cards.FirstOrDefault(card => card.PointMove != null && !card.HasActionSlot);
            value.InitialHand.Add(point == null ? action.Clone() : new(point.CardId));
            value.InitialHand.Add(action.Clone());
            value.OpponentDeck.Add(DefaultActionCard(value.OpponentCharacterId));
            value.CustomPlacement = true;
            value.UseExplicitDecks = true;
            return value;
        }

        // Explicit authoring fixtures, selected by the user. These never open a player inventory or save store.
        internal void ConfigureAccessoryManualTrial(CharacterTestRuntimeAiLabCardTrialConfiguration config, TacticalAccessoryCombatKind kind)
        {
            if (config == null || CharacterTestRuntimeAiLabFixedAssembly.Definition(kind) == null)
                throw new InvalidOperationException("请选择已支持的配件手测预设。");
            const int roleId = 668367834;
            const string guardCard = "bc_w7wtzq7jfr", attackCard = "tactical.bound-action";
            if (!Characters.Any(character => character.CharacterId == roleId) || Catalog.Card(guardCard) == null || Catalog.Card(attackCard) == null
                || !EligibleActions(roleId).Any(action => action.Id == "action:459")
                || !EligibleActions(roleId).Any(action => action.Id == "action:455"))
                throw new InvalidOperationException("手测预设需要已发布的 Forest Ranger、迎面固守、常规移动接敌出招和普通防御／持剑四连击第1段。");
            var main = new CharacterTestRuntimeAiLabCardTrialSelection(
                kind == TacticalAccessoryCombatKind.BorrowMomentum ? guardCard : attackCard,
                kind == TacticalAccessoryCombatKind.BorrowMomentum ? "action:459" : "action:455")
                { FixedAssembly = true, FixedQuality = CharacterTacticalCardQuality.Silver };
            main.AccessorySlots[0] = kind;
            config.PlayerCharacterId = config.OpponentCharacterId = roleId;
            config.InitialMomentum = 50; config.Seed = 73; config.CloseStart = true;
            config.CustomPlacement = false; config.UseExplicitDecks = false; config.OpponentDeck.Clear(); config.AdditionalParticipants.Clear();
            config.OpponentCard = new CharacterTestRuntimeAiLabCardTrialSelection(
                kind == TacticalAccessoryCombatKind.BreakMomentum ? guardCard : attackCard,
                kind == TacticalAccessoryCombatKind.BreakMomentum ? "action:459" : "action:455");
            config.InitialHand.Clear(); config.InitialHand.Add(main);
        }

        internal CharacterTestRuntimeAiLabCardTrialSelection DefaultActionCard(int characterId)
        {
            var character = Character(characterId);
            foreach (var reference in character.TacticalDefaults.Cards)
            {
                var metadata = Catalog.ExactCard(reference);
                if (metadata == null) continue;
                var binding = character.TacticalDefaults.Bindings.FirstOrDefault(item => item.CardId == reference.CardId && item.SlotPath == metadata.ActionSlotPath);
                if (!metadata.HasActionSlot || binding != null)
                    return new(reference.CardId, binding?.TacticalActionId ?? "");
            }
            var card = Catalog.Cards.FirstOrDefault(item => !item.HasActionSlot) ?? Catalog.Cards.FirstOrDefault();
            if (card == null) throw new InvalidOperationException("没有正式发布的行动卡。");
            return new(card.CardId, EligibleActions(characterId).FirstOrDefault()?.Id ?? "");
        }

        internal CharacterTestCharacterDefinition Character(int id) => Characters.FirstOrDefault(item => item.CharacterId == id);
        internal CharacterTacticalActionDefinition[] EligibleActions(int characterId)
        {
            string stable = Character(characterId).TacticalDefaults?.CharacterStableId;
            return Catalog.Actions.Where(item => item.CharacterStableIds.Contains(stable)).OrderBy(item => item.DisplayName, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        }

        // Draft selection only: retain a compatible choice, then use the role's card default or first eligible action.
        internal string ResolveActionBinding(string cardId, int characterId, string preferredActionId = "")
        {
            var card = Catalog.Card(cardId);
            if (card?.HasActionSlot != true) return "";
            var actions = EligibleActions(characterId);
            if (actions.Any(item => item.Id == preferredActionId)) return preferredActionId;
            var binding = Character(characterId).TacticalDefaults.Bindings.FirstOrDefault(item => item.CardId == cardId
                && item.SlotPath == card.ActionSlotPath && actions.Any(action => action.Id == item.TacticalActionId));
            return binding?.TacticalActionId ?? actions.FirstOrDefault()?.Id ?? "";
        }

        internal string Describe(CharacterTestRuntimeAiLabCardTrialSelection selection)
        {
            if (TacticalInteractionTrialCards.TryCreate(selection.CardId, out var trial)) return trial.Name;
            if (selection.CardId == CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId) return "测试准备（无保命／缩放效果）";
            var card = Catalog.Card(selection.CardId);
            var action = Catalog.Action(selection.TacticalActionId);
            return card == null ? "失效卡引用" : card.DisplayName + " r" + card.Revision + (card.HasActionSlot ? "\n装配动作：" + (action?.DisplayName ?? "请选择动作") : "");
        }

        internal TacticalDeployment Freeze(CharacterTestRuntimeAiLabCardTrialConfiguration config, ulong stageRunId)
        {
            if (config == null || config.InitialHand.Count == 0) throw new InvalidOperationException("请至少配置一张初始手牌。");
            if (config.InitialMomentum < 0 || config.InitialMomentum > MomentumLedger.Maximum || config.PreparationCost < 0)
                throw new InvalidOperationException("初始资源须为0至100，测试准备费用须为非负整数。");
            var player = Character(config.PlayerCharacterId); var opponent = Character(config.OpponentCharacterId);
            var playerPosition = config.PositionAt(0); float playerFacing = config.FacingAt(0);
            if (float.IsNaN(playerPosition.x) || float.IsInfinity(playerPosition.x) || float.IsNaN(playerPosition.z) || float.IsInfinity(playerPosition.z)
                || float.IsNaN(playerFacing) || float.IsInfinity(playerFacing)) throw new InvalidOperationException("角色位置与朝向须为有限数值。");
            if (player.TacticalDefaults == null || opponent.TacticalDefaults == null) throw new InvalidOperationException("请选择有效玩家与对手。");
            if (config.InitialHand.Any(item => item.FixedAssembly) && config.InitialHand.Any(item =>
                item.CardId == CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId || TacticalInteractionTrialCards.TryCreate(item.CardId, out _)
                    || Catalog.Card(item.CardId)?.InteractionPreparation != null))
                throw new InvalidOperationException("固定组合试验只允许行动主卡；请移除旧独立准备／测试工具卡。");
            var playerCards = new List<TacticalCardDefinition>();
            var playerBindings = new Dictionary<ulong, ReadOnlyDictionary<string, int>>();
            ulong entry = 0;
            foreach (var selection in config.InitialHand) AddCard(selection, player, config.PreparationCost, ++entry, playerCards, playerBindings);
            // Test reserve pile is an explicit copy of configured Action entries; preparation probes are not refilled.
            foreach (var selection in config.InitialHand)
                if (selection.CardId != CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId && !TacticalInteractionTrialCards.TryCreate(selection.CardId, out _) && Catalog.Card(selection.CardId)?.InteractionPreparation == null)
                    AddCard(selection, player, config.PreparationCost, ++entry, playerCards, playerBindings);
            var participants = new List<TacticalDeploymentParticipant> {
                new TacticalDeploymentParticipant("lab-trial-player", player.CharacterId, player.TacticalDefaults.CharacterStableId,
                    player.DisplayName, 1, true, config.PositionAt(0), config.FacingAt(0), playerCards.ToArray(), playerBindings)
            };
            for (int role = 1; role < config.ParticipantCount; role++)
            {
                var definition = Character(config.CharacterAt(role));
                if (definition.TacticalDefaults == null) throw new InvalidOperationException("试验角色引用失效。");
                var selections = config.DeckAt(role);
                var enemyCards = new List<TacticalCardDefinition>();
                var enemyBindings = new Dictionary<ulong, ReadOnlyDictionary<string, int>>();
                IEnumerable<CharacterTestRuntimeAiLabCardTrialSelection> cards = selections.Count > 0 || config.UseExplicitDecks ? selections
                    : new[] { role == 1 ? config.OpponentCard?.Clone() ?? DefaultActionCard(definition.CharacterId) : DefaultActionCard(definition.CharacterId) };
                foreach (var selected in cards)
                {
                    if (selected.FixedAssembly || selected.CardId == CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId
                        || TacticalInteractionTrialCards.TryCreate(selected.CardId, out _) || Catalog.Card(selected.CardId)?.InteractionPreparation != null
                        || Catalog.Card(selected.CardId)?.PointMove != null || Catalog.Card(selected.CardId)?.PositionRecall != null)
                        throw new InvalidOperationException("AI 角色的组合执行尚未接入；当前真实试验仅支持其普通行为牌。配置不会被丢弃。");
                    AddCard(selected, definition, config.PreparationCost, ++entry, enemyCards, enemyBindings);
                }
                var position = config.PositionAt(role); float facing = config.FacingAt(role);
                if (float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.z) || float.IsInfinity(position.z)
                    || float.IsNaN(facing) || float.IsInfinity(facing)) throw new InvalidOperationException("角色位置与朝向须为有限数值。");
                participants.Add(new TacticalDeploymentParticipant(role == 1 ? "lab-trial-opponent" : "lab-trial-opponent-" + role, definition.CharacterId,
                    definition.TacticalDefaults.CharacterStableId, definition.DisplayName, 2, false, position, facing, enemyCards.ToArray(), enemyBindings));
            }
            return TacticalDeployment.CreateLabTrial(participants.ToArray(), config.InitialMomentum,
                new TacticalCardTrialOptions(stageRunId, config.InitialHand.Count, config.Seed, Published));
        }

        private void AddCard(CharacterTestRuntimeAiLabCardTrialSelection selection, CharacterTestCharacterDefinition character,
            int preparationCost, ulong entry, List<TacticalCardDefinition> cards, Dictionary<ulong, ReadOnlyDictionary<string, int>> bindings)
        {
            if (TacticalInteractionTrialCards.TryCreate(selection.CardId, out var trial)) { cards.Add(trial); return; }
            if (selection.CardId == CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId)
            {
                cards.Add(new TacticalUtilityCardDefinition(CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId,
                    "测试准备（无保命效果）", "仅观察真实出牌事务；不避伤、不换位、不改变体型。退战或重置清理。", preparationCost,
                    new TestPreparationPayload(), CardDestination.Exhaust)); return;
            }
            var metadata = Catalog.Card(selection.CardId) ?? throw new InvalidOperationException("正式卡引用失效：" + selection.CardId);
            if (metadata.PresentationSource == null || !metadata.PresentationSource.TryValidatePlayerExecution(out string issue))
                throw new InvalidOperationException("该卡暂不能通过正式玩家执行校验：" + metadata.DisplayName);
            var resolved = new Dictionary<string, int>(StringComparer.Ordinal);
            if (metadata.HasActionSlot)
            {
                var action = Catalog.Action(selection.TacticalActionId);
                if (action == null || !action.CharacterStableIds.Contains(character.TacticalDefaults.CharacterStableId))
                    throw new InvalidOperationException("请为“" + metadata.DisplayName + "”选择该角色可用的正式动作。");
                resolved.Add(metadata.ActionSlotPath, action.ActionId);
            }
            bindings.Add(entry, new ReadOnlyDictionary<string, int>(resolved));
            var main = TacticalDeployment.CreateDefinition(new CharacterTacticalCardReference
                { InstanceId = entry, CardId = metadata.CardId, Revision = metadata.Revision, ContentHash = metadata.ContentHash }, metadata);
            CharacterTestRuntimeAiLabFixedAssembly.Attach(selection, main, character.CharacterId, resolved);
            cards.Add(main);
        }
    }
}
