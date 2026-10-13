using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MxFramework.CharacterApplication;
using MxFramework.Items;
using Newtonsoft.Json;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Application composition root; neither package definitions nor scenes own player progress.</summary>
    internal static class CharacterTacticalServices
    {
        private static readonly CharacterTacticalServiceOpening Opening = new CharacterTacticalServiceOpening(OpenCoreAsync);
        private static CharacterTacticalProgressStore _progress;
        internal static void RegisterLegacy(CharacterTestRuntimeAiPlannerLocalPlayerDeckService legacy)
        {
            // The legacy deck is a projection only; retired tactical data is never imported.
        }
        internal static CharacterTacticalPlayerService Player => Opening.Published?.Player;
        internal static TacticalBattlePreparationService Preparation => Opening.Published?.Preparation;
        internal static string Issue => Opening.Issue;
        internal static CharacterTacticalRoleBindingQueue RoleBindings => Opening.Published?.RoleBindings;
        internal static InventoryOwnerId InventoryOwner => Opening.Published?.InventoryOwner ?? default;
        internal static Task<CharacterTacticalPlayerService> OpenAsync() => Opening.OpenAsync();
        internal static bool HasCheckpoint => _progress?.HasCheckpoint == true || Opening.Published?.Progress?.HasSourceProgress == true
            || Opening.Published?.Player.HasImportedLegacyProgress == true;
        internal static async Task<bool> SaveProgressAsync(CharacterTacticalBattleSettlement settlement = null)
        {
            CharacterTacticalServiceSet current = Opening.Published;
            if (current?.Progress == null) return false;
            try { await current.Progress.SaveAsync(settlement: settlement); current.Progress.EnableAutosave(); current.ProgressIssue = string.Empty; return true; }
            catch (Exception error) { current.ProgressIssue = "进度尚未保存：" + error.Message; return false; }
        }
        internal static CharacterTacticalRewardRecipe RewardRecipe => Opening.Published?.RewardRecipe;
        internal static CharacterTacticalBattleSettlement ReadPendingReward() => Opening.Published?.Progress?.ReadPendingReward();
        internal static Task<CharacterTacticalRewardClaimResult> ClaimRewardsAsync(string battleId, MxFramework.Runtime.RuntimeTickContext context)
        {
            var current = Opening.Published;
            return current?.Progress == null ? Task.FromResult(new CharacterTacticalRewardClaimResult(CharacterTacticalRewardClaimStatus.Rejected,
                "领奖进度尚未就绪。")) : current.Player.ClaimBattleRewardsAsync(current.Progress, battleId, context);
        }
        internal static string ProgressIssue => Opening.Published?.ProgressIssue ?? Issue;

        internal static async Task<CharacterTacticalServiceSet> PrepareNewGameAsync()
        {
            if (_progress == null) await OpenAsync();
            return await BuildCandidateAsync(_progress.Create());
        }

        internal static CharacterTacticalProgressProfile[] Profiles => _progress?.ListProfiles() ?? Array.Empty<CharacterTacticalProgressProfile>();
        internal static string ActiveProfileId => _progress?.ActiveProfileId ?? string.Empty;

        internal static async Task<CharacterTacticalServiceSet> PrepareProfileAsync(string profileId)
        {
            if (_progress == null) await OpenAsync();
            return await BuildCandidateAsync(_progress.OpenProfile(profileId));
        }

        internal static async Task<bool> CommitProfileAsync(CharacterTacticalServiceSet candidate)
        {
            if (candidate?.Progress == null) return false;
            try
            {
                var previous = Opening.Published?.Progress;
                await candidate.Progress.SaveAsync(activate: true,
                    archive: previous != null && previous.ProfileId != candidate.Progress.ProfileId ? previous.CaptureSnapshot() : null);
                candidate.ProgressIssue = string.Empty; return true;
            }
            catch (Exception error) { candidate.ProgressIssue = "所选进度尚未保存，原进度保留：" + error.Message; return false; }
        }

        internal static async Task<bool> CommitNewGameAsync(CharacterTacticalServiceSet candidate)
        {
            if (candidate?.Progress == null) return false;
            try
            {
                // A valid legacy import may not yet be in the composite document. Archive it in
                // the SAME commit as the new profile, so direct Home -> New Game preserves a selectable old profile.
                var previous = Opening.Published?.Progress;
                await candidate.Progress.SaveAsync(activate: true,
                    archive: previous != null && (previous.HasSourceProgress || _progress.HasCheckpoint) ? previous.CaptureSnapshot() : null);
                return true;
            }
            catch (Exception error) { candidate.ProgressIssue = "新进度尚未保存：" + error.Message; return false; }
        }

        internal static void PublishNewGame(CharacterTacticalServiceSet candidate)
        {
            if (candidate?.Progress == null || _progress?.ActiveProfileId != candidate.Progress.ProfileId)
                throw new InvalidOperationException("TACTICAL_NEW_GAME_NOT_COMMITTED_OR_SUPERSEDED");
            Opening.Published?.RoleBindings.Cancel("角色进度已切换。");
            Opening.Published?.Progress?.Retire();
            Opening.Publish(candidate);
            candidate.Progress.EnableAutosave();
        }

        internal static bool RefreshPublishedCatalog(TacticalPreparationState phase, out string message)
        {
            if (Opening.Published == null)
            {
                message = "角色与整备服务尚未加载，无法刷新卡库。";
                return false;
            }
            return Opening.Published.RefreshPublishedCatalog(phase, () =>
            {
                var published = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
                if (!published.Success) throw new InvalidOperationException(published.Diagnostic);
                var projection = new CharacterTacticalContentCatalog();
                projection.SetPublishedCards(published.CardCatalog.Cards);
                return projection.Cards;
            }, out message);
        }

        // Editor acceptance uses its own process-scoped persistence root, including legacy import.
        // Player builds always retain the normal persistentDataPath.
        internal static string PersistentPath(params string[] segments)
        {
            string root = Application.persistentDataPath;
#if UNITY_EDITOR
            string validationRoot = Environment.GetEnvironmentVariable("MX_CHARACTERTEST_TACTICAL_SAVE_ROOT");
            if (!string.IsNullOrWhiteSpace(validationRoot)) root = Path.GetFullPath(validationRoot);
#endif
            foreach (string segment in segments) root = Path.Combine(root, segment);
            return root;
        }

        private static async Task<CharacterTacticalServiceSet> OpenCoreAsync()
        {
            string root = PersistentPath("MxFramework", "CharacterTest", "SaveGame");
            var store = await CharacterTacticalSaveStore.CreateAsync(root, "tactical-slice-progress", "character-tactical-slice-json-v1");
            _progress = new CharacterTacticalProgressStore(store, await store.LoadAsync());
            CharacterTacticalProgressCandidate candidate = _progress.OpenActive();
            candidate ??= _progress.Create();
            return await OpenCandidateCoreAsync(candidate, Path.Combine(Application.streamingAssetsPath, "MxFramework/CharacterTest"));
        }

        // Same startup boundary for the real application and isolated content/checkpoint validation.
        // The caller's Opening cannot publish until the migrated aggregate passes the existing CAS save.
        internal static async Task<CharacterTacticalServiceSet> OpenCandidateCoreAsync(
            CharacterTacticalProgressCandidate candidate, string contentDirectory)
        {
            var services = await BuildCandidateFromDirectoryAsync(candidate, contentDirectory);
            if (!candidate.HasSourceProgress || services.InventoryRegistryMigrated) await candidate.SaveAsync(activate: true);
            candidate.EnableAutosave();
            return services;
        }

        private static Task<CharacterTacticalServiceSet> BuildCandidateAsync(CharacterTacticalProgressCandidate candidate)
            => BuildCandidateFromDirectoryAsync(candidate, Path.Combine(Application.streamingAssetsPath, "MxFramework/CharacterTest"));

        private static async Task<CharacterTacticalServiceSet> BuildCandidateFromDirectoryAsync(
            CharacterTacticalProgressCandidate candidate, string contentDirectory)
        {
            string path = Path.Combine(contentDirectory, "tactical_content.json");
            var catalog = JsonConvert.DeserializeObject<CharacterTacticalContentCatalog>(File.ReadAllText(path));
            string acquisitionPath = Path.Combine(Path.GetDirectoryName(path), "tactical_acquisition.json");
            var acquisition = JsonConvert.DeserializeObject<CharacterTacticalAcquisitionConfig>(File.ReadAllText(acquisitionPath));
            if (acquisition?.Schema != "mx.character-test.tactical-acquisition.v1" || acquisition.Characters == null)
                throw new JsonSerializationException("TACTICAL_ACQUISITION_INVALID");
            catalog.Acquisition = acquisition.Characters;
            CharacterTacticalActionEligibility.Load().Project(catalog, CharacterTestActionBrowserCatalog.Get());
            var published = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!published.Success) throw new InvalidOperationException(published.Diagnostic);
            catalog.SetPublishedCards(published.CardCatalog.Cards);
            var defaults = CharacterTestPlayableCharacterCatalog.Discover().Where(c => c.TacticalDefaults != null)
                .Select(c => c.TacticalDefaults).ToArray();
            string directory = Path.GetDirectoryName(path);
            string playerJson = await candidate.PlayerStore.LoadAsync();
            bool isNew = playerJson == null;
            string inventoryPath = Path.Combine(directory, "character_item_inventory.json");
            if (!File.Exists(inventoryPath))
                throw new InvalidOperationException("正式角色物品目录未发布（character_item_inventory.json）；旧进度保留，未创建或切换新进度。");
            var inventoryConfig = JsonConvert.DeserializeObject<CharacterTacticalRoleInventoryConfig>(File.ReadAllText(inventoryPath));
            if (inventoryConfig == null) throw new JsonSerializationException("TACTICAL_ROLE_INVENTORY_CONFIG_MISSING");
            inventoryConfig.Build(defaults.Select(row => row.CharacterStableId), out var itemDefinitions,
                out var containers, out var mapping);
            string assemblyPath = Path.Combine(directory, CharacterTacticalAssemblyPublication.FileName);
            CharacterTacticalAssemblyPublication assemblyPublication = null;
            if (File.Exists(assemblyPath))
            {
                assemblyPublication = JsonConvert.DeserializeObject<CharacterTacticalAssemblyPublication>(File.ReadAllText(assemblyPath));
                if (assemblyPublication == null) throw new JsonSerializationException("TACTICAL_ASSEMBLY_PUBLICATION_MISSING");
                itemDefinitions = assemblyPublication.BuildItemDefinitions(itemDefinitions);
            }
            CharacterTacticalRoleNewGameConfig newGame = null;
            if (isNew)
            {
                string newGamePath = Path.Combine(directory, "tactical_new_game.json");
                if (!File.Exists(newGamePath)) throw new InvalidOperationException("正式角色物品新档配方未发布，未创建新进度。");
                newGame = JsonConvert.DeserializeObject<CharacterTacticalRoleNewGameConfig>(File.ReadAllText(newGamePath));
                if (newGame == null) throw new JsonSerializationException("TACTICAL_ROLE_NEW_GAME_CONFIG_MISSING");
                newGame.Validate(inventoryConfig, catalog, defaults.Select(row => row.CharacterStableId));
                // Recipes validate against discovered templates, but only explicit grants create ownership.
                // Include ungranted templates in the definition catalog without granting their default cards.
                var explicitRecipes = newGame.Characters.ToDictionary(row => row.CharacterStableId, StringComparer.Ordinal);
                defaults = defaults.Select(row => explicitRecipes.TryGetValue(row.CharacterStableId, out var recipe) ? recipe : row).ToArray();
            }
            else
            {
                var saved = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(playerJson);
                // Gate before OpenAsync: it must not repair/import old schemas using today's initial defaults.
                if (!CharacterTacticalAssemblyRules.HasCurrentCharacterItems(saved))
                    throw new InvalidOperationException("旧角色进度需要独立升级评审；未迁移、未清空、未补发，原存档保留。");
                if (assemblyPublication == null && CharacterTacticalAssemblyRules.RequiresConfiguration(saved))
                    throw new InvalidOperationException("此进度的配件或装配需要正式装配配置；原存档保留，未补造定义。");
            }
            bool inventoryRegistryMigrated = false;
            if (!isNew && assemblyPublication != null) {
                string migrated = TacticalDoubleEdgeInventoryMigration.Prepare(playerJson, itemDefinitions, assemblyPublication.Configuration);
                if (migrated != playerJson) {
                    await candidate.PlayerStore.SaveAsync(migrated);
                    inventoryRegistryMigrated = true;
                }
            }
            var player = await CharacterTacticalPlayerService.OpenAsync(catalog, defaults, candidate.PlayerStore,
                addMissingDefaults: false, instantiateTemplateDefaults: false);
            if (!player.ConfigureCharacterItemInventory(itemDefinitions, containers, mapping, out string inventoryIssue))
                throw new InvalidOperationException("正式角色物品配置无效：" + inventoryIssue);
            if (assemblyPublication != null && !player.ConfigureCardAssembly(assemblyPublication.Configuration, out string assemblyIssue))
                throw new InvalidOperationException("正式装配配置无效：" + assemblyIssue);
            IReadOnlyDictionary<long, ItemInstanceId> instances = isNew
                ? CharacterTacticalRoleNewGameBootstrap.Execute(player, inventoryConfig, newGame) : null;
            string preparationJson = await candidate.PreparationStore.LoadAsync();
            if (!isNew && (preparationJson == null || JsonConvert.DeserializeObject<TacticalBattlePreparationState>(preparationJson)?.Schema
                != TacticalBattlePreparationState.CharacterItemSchema))
                throw new InvalidOperationException("旧整备需要明确选择角色物品实例；未迁移或重建原进度。");
            var preparation = await TacticalBattlePreparationService.OpenAsync(player, candidate.PreparationStore);
            if (isNew && !preparation.ConfigureCharacterItemNewGame(newGame, instances, out string setupIssue))
                throw new InvalidOperationException("正式初始实例整备无效：" + setupIssue);
            if (!preparation.EnsurePlayerControlledEntity(out string controlIssue))
                throw new InvalidOperationException("正式进度没有可控角色或可用卡：" + controlIssue);
            candidate.Bind(player.CaptureProgressJson, preparation.CaptureProgressJson);
            foreach (var settlement in candidate.ReadSettlements()) player.ValidateRewardReceipts(settlement.Rewards);
            string rewardsPath = Path.Combine(directory, CharacterTacticalRewardRecipe.FileName);
            var rewards = File.Exists(rewardsPath) ? JsonConvert.DeserializeObject<CharacterTacticalRewardRecipe>(File.ReadAllText(rewardsPath),
                new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }) : null;
            return new CharacterTacticalServiceSet(player, preparation, candidate, new InventoryOwnerId(inventoryConfig.PlayerOwnerId),
                inventoryRegistryMigrated) { RewardRecipe = rewards };
        }

    }

    internal sealed class CharacterTacticalServiceSet
    {
        internal CharacterTacticalServiceSet(CharacterTacticalPlayerService player, TacticalBattlePreparationService preparation,
            CharacterTacticalProgressCandidate progress = null, InventoryOwnerId inventoryOwner = default, bool inventoryRegistryMigrated = false)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
            Progress = progress; InventoryOwner = inventoryOwner; InventoryRegistryMigrated = inventoryRegistryMigrated;
            RoleBindings = new CharacterTacticalRoleBindingQueue(Player, Preparation, inventoryOwner);
        }
        internal CharacterTacticalPlayerService Player { get; }
        internal TacticalBattlePreparationService Preparation { get; }
        internal CharacterTacticalProgressCandidate Progress { get; }
        internal InventoryOwnerId InventoryOwner { get; }
        internal bool InventoryRegistryMigrated { get; }
        internal CharacterTacticalRoleBindingQueue RoleBindings { get; }
        internal string ProgressIssue { get; set; } = string.Empty;
        internal CharacterTacticalRewardRecipe RewardRecipe { get; set; }

        /// <remarks>Application-thread preparation transaction. No save, instance replacement or deployment mutation.</remarks>
        internal bool RefreshPublishedCatalog(TacticalPreparationState phase,
            Func<CharacterTacticalCardDefinition[]> loadCards, out string message)
        {
            if (phase != TacticalPreparationState.Preparation)
            {
                message = "出战或战斗中不能刷新卡库，请先返回整备。";
                return false;
            }
            try
            {
                CharacterTacticalCardDefinition[] cards = loadCards();
                int workingInvalid = CountInvalid(Preparation.Working, cards);
                int presetInvalid = Preparation.Presets.Sum(preset => CountInvalid(preset.Snapshot, cards));
                if (!Player.TryRefreshPublishedCards(cards, out int ownedInvalid, out message)) return false;
                message = "卡库已刷新，可添加新发布卡。";
                if (ownedInvalid + workingInvalid + presetInvalid > 0)
                    message += $" 失效引用：持有 {ownedInvalid}、工作配置 {workingInvalid}、预设 {presetInvalid}；原引用已保留，请按需重新添加。";
                return true;
            }
            catch (Exception error)
            {
                message = "刷新卡库失败，保留原卡库：" + error.Message;
                return false;
            }
        }

        private static int CountInvalid(TacticalBattleSetup setup, CharacterTacticalCardDefinition[] cards) => setup.Entities.Sum(entity =>
            entity.Loadout.Cards.Count(reference => !cards.Any(card => card != null && card.CardId == reference.CardId
                && card.Revision == reference.Revision && string.Equals(card.ContentHash, reference.ContentHash, StringComparison.Ordinal))));
    }

    /// <summary>Application-thread opening owner. Publishes both services through one immutable reference.</summary>
    internal sealed class CharacterTacticalServiceOpening
    {
        private readonly Func<Task<CharacterTacticalServiceSet>> _open;
        private Task<CharacterTacticalPlayerService> _opening;
        internal CharacterTacticalServiceOpening(Func<Task<CharacterTacticalServiceSet>> open)
        {
            _open = open ?? throw new ArgumentNullException(nameof(open));
        }
        internal CharacterTacticalServiceSet Published { get; private set; }
        internal string Issue { get; private set; } = string.Empty;
        internal void Publish(CharacterTacticalServiceSet candidate)
        {
            Published = candidate ?? throw new ArgumentNullException(nameof(candidate));
            Issue = candidate.ProgressIssue;
        }
        internal Task<CharacterTacticalPlayerService> OpenAsync()
        {
            if (Published != null) return Task.FromResult(Published.Player);
            if (_opening != null && !_opening.IsCompleted) return _opening;
            var completion = new TaskCompletionSource<CharacterTacticalPlayerService>(TaskCreationOptions.RunContinuationsAsynchronously);
            _opening = completion.Task;
            _ = CompleteOpenAsync(completion);
            return _opening;
        }
        private async Task CompleteOpenAsync(TaskCompletionSource<CharacterTacticalPlayerService> completion)
        {
            try
            {
                var candidate = await _open();
                if (candidate == null) throw new InvalidOperationException("TACTICAL_SERVICES_NOT_READY");
                Published = candidate;
                Issue = candidate.Player.SaveIssue;
                completion.SetResult(candidate.Player);
            }
            catch (Exception error)
            {
                Issue = "角色战术清单加载失败：" + error.Message;
                completion.SetException(error);
            }
        }
    }

    // CPU-testable production checkpoint barriers. Engine ports remain at the caller.
    internal enum TacticalCheckpointTransition { Published, SaveRejected, Superseded, SavedWithoutPublication }
    internal static class TacticalFlowCheckpointBarrier
    {
        internal static async Task<TacticalCheckpointTransition> ReplaceAsync<T>(Func<T> prepare,
            Func<Task<bool>> commit, Action<T> publish, Func<bool> isCurrent) where T : class, IDisposable
        {
            if (!isCurrent()) return TacticalCheckpointTransition.Superseded;
            using T candidate = prepare();
            if (!isCurrent()) return TacticalCheckpointTransition.Superseded;
            if (!await commit()) return TacticalCheckpointTransition.SaveRejected;
            if (!isCurrent()) return TacticalCheckpointTransition.SavedWithoutPublication;
            try { publish(candidate); }
            catch (Exception error) { throw new TacticalCheckpointPublicationException(error); }
            return TacticalCheckpointTransition.Published;
        }

        internal static async Task<TacticalCheckpointTransition> ExitAsync(Func<Task<bool>> commit,
            Action exit, Func<bool> isCurrent)
        {
            if (!isCurrent()) return TacticalCheckpointTransition.Superseded;
            if (!await commit()) return TacticalCheckpointTransition.SaveRejected;
            if (!isCurrent()) return TacticalCheckpointTransition.SavedWithoutPublication;
            exit();
            return TacticalCheckpointTransition.Published;
        }
    }
    internal sealed class TacticalCheckpointPublicationException : InvalidOperationException
    {
        internal TacticalCheckpointPublicationException(Exception inner)
            : base("进度已保存，但会话/界面发布失败：" + inner.Message, inner) { }
    }
    // End checkpoint barriers.

    // Published projection stays at the application composition boundary; saved contracts remain pure.
    internal sealed partial class CharacterTacticalCardDefinition
    {
        [JsonIgnore] internal bool CanBeAssemblyMain => HasActionSlot || PresentationSource != null && InteractionPreparation == null;
        [JsonIgnore] internal InteractionPreparationPayload InteractionPreparation => PresentationSource?.InteractionPreparation;
        [JsonIgnore] internal TacticalPositionRecallRules PositionRecall => PresentationSource?.PositionRecall;
        [JsonIgnore] internal TacticalPointMoveRules PointMove => PresentationSource?.PointMove;
        [JsonIgnore] internal CharacterTestTacticalPublishedSource PresentationSource
        {
            get => PresentationData as CharacterTestTacticalPublishedSource;
            set => PresentationData = value;
        }
    }

    internal sealed partial class CharacterTacticalContentCatalog
    {
        internal void SetPublishedCards(IEnumerable<CharacterTestRuntimeAiPlannerPublishedBehaviorCard> published)
        {
            Cards = published.Where(card => card.TacticalSource != null).Select(card => new CharacterTacticalCardDefinition {
                CardId = card.StableCardId, Revision = card.Revision,
                ContentHash = card.ContentHash.ToString(System.Globalization.CultureInfo.InvariantCulture),
                DisplayName = card.Metadata.DisplayName, Description = card.Metadata.ShortDescription,
                IconKey = card.Metadata.IconKey,
                MotionCost = card.Metadata.Cost, CooldownSeconds = card.Metadata.CooldownSeconds,
                Slots = PublishedSlots(card.TacticalSource), PresentationSource = card.TacticalSource
            }).ToArray();
        }

        private static string[] PublishedSlots(CharacterTestTacticalPublishedSource source)
        {
            if (source.IsLegacy) return source.CloneLegacy().Slots
                .Where(slot => !slot.IsFunctional)
                .Select(slot => slot.SlotId).Take(1).ToArray();
            var definitions = source.CloneDefinitions().ToDictionary(d => d.DefinitionId, StringComparer.Ordinal);
            return CharacterTestTacticalCompositionEdits.Nodes(source.CloneComposition().Root).Where(n => n.Kind == "Reference")
                .SelectMany(n => definitions[n.DefinitionId].Source.Slots
                    .Where(slot => !slot.IsFunctional)
                    .Select(slot => n.NodeId + "/" + slot.SlotId)).Take(1).ToArray();
        }

    }
}
