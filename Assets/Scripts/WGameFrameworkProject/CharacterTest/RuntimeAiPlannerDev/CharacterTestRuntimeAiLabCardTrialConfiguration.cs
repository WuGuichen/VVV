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
        internal CharacterTestRuntimeAiLabCardTrialSelection(string cardId, string actionId = "")
        { CardId = cardId; TacticalActionId = actionId; }
        internal CharacterTestRuntimeAiLabCardTrialSelection Clone() => new(CardId, TacticalActionId);
    }

    /// <summary>Authoring-only initial conditions. Published content is read-only; no player service or persistent store is opened.</summary>
    internal sealed class CharacterTestRuntimeAiLabCardTrialConfiguration
    {
        internal int PlayerCharacterId, OpponentCharacterId, InitialMomentum = 50, Seed = 73, PreparationCost = 5;
        internal readonly List<CharacterTestRuntimeAiLabCardTrialSelection> InitialHand = new();
        internal CharacterTestRuntimeAiLabCardTrialConfiguration Clone()
        {
            var value = new CharacterTestRuntimeAiLabCardTrialConfiguration
            { PlayerCharacterId = PlayerCharacterId, OpponentCharacterId = OpponentCharacterId,
                InitialMomentum = InitialMomentum, Seed = Seed, PreparationCost = PreparationCost };
            value.InitialHand.AddRange(InitialHand.Select(item => item.Clone())); return value;
        }
        internal bool HasSameConditions(CharacterTestRuntimeAiLabCardTrialConfiguration other)
        {
            if (other == null || PlayerCharacterId != other.PlayerCharacterId || OpponentCharacterId != other.OpponentCharacterId
                || InitialMomentum != other.InitialMomentum || Seed != other.Seed || PreparationCost != other.PreparationCost
                || InitialHand.Count != other.InitialHand.Count) return false;
            for (int i = 0; i < InitialHand.Count; i++)
                if (InitialHand[i].CardId != other.InitialHand[i].CardId || InitialHand[i].TacticalActionId != other.InitialHand[i].TacticalActionId)
                    return false;
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
            value.InitialHand.Add(new(CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId));
            value.InitialHand.Add(action.Clone());
            var point = Catalog.Cards.FirstOrDefault(card => card.PointMove != null && !card.HasActionSlot);
            value.InitialHand.Add(point == null ? action.Clone() : new(point.CardId));
            value.InitialHand.Add(action.Clone());
            return value;
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
            if (player.TacticalDefaults == null || opponent.TacticalDefaults == null) throw new InvalidOperationException("请选择有效玩家与对手。");
            var playerCards = new List<TacticalCardDefinition>();
            var playerBindings = new Dictionary<ulong, ReadOnlyDictionary<string, int>>();
            ulong entry = 0;
            foreach (var selection in config.InitialHand) AddCard(selection, player, config.PreparationCost, ++entry, playerCards, playerBindings);
            // Test reserve pile is an explicit copy of configured Action entries; preparation probes are not refilled.
            foreach (var selection in config.InitialHand)
                if (selection.CardId != CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId && !TacticalInteractionTrialCards.TryCreate(selection.CardId, out _) && Catalog.Card(selection.CardId)?.InteractionPreparation == null)
                    AddCard(selection, player, config.PreparationCost, ++entry, playerCards, playerBindings);
            var enemyCards = new List<TacticalCardDefinition>();
            var enemyBindings = new Dictionary<ulong, ReadOnlyDictionary<string, int>>();
            var enemySelection = DefaultActionCard(opponent.CharacterId);
            AddCard(enemySelection, opponent, config.PreparationCost, ++entry, enemyCards, enemyBindings);
            var participants = new[]
            {
                new TacticalDeploymentParticipant("lab-trial-player", player.CharacterId, player.TacticalDefaults.CharacterStableId,
                    player.DisplayName, 1, true, new Vector3(-2, 1, -1), 45, playerCards.ToArray(), playerBindings),
                new TacticalDeploymentParticipant("lab-trial-opponent", opponent.CharacterId, opponent.TacticalDefaults.CharacterStableId,
                    opponent.DisplayName, 2, false, new Vector3(2, 1, 1), 225, enemyCards.ToArray(), enemyBindings)
            };
            return TacticalDeployment.CreateLabTrial(participants, config.InitialMomentum,
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
            cards.Add(TacticalDeployment.CreateDefinition(new CharacterTacticalCardReference
                { InstanceId = entry, CardId = metadata.CardId, Revision = metadata.Revision, ContentHash = metadata.ContentHash }, metadata));
        }
    }
}
