using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MxFramework.Items;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class CharacterTacticalPlayerService
    {
        private bool _rewardCommitInFlight;
        internal bool RewardCommitInFlight => _rewardCommitInFlight;
        private bool MutationBlocked => _resetting || _rewardCommitInFlight;

        /// <remarks>LowFreqAlloc. Resolves fixed authored entries once, on the application thread. Never grants ownership.</remarks>
        internal CharacterTacticalRewardBundle FreezeBattleRewards(CharacterTacticalRewardRecipe recipe, string battleId,
            ItemInstanceId characterId, InventoryOwnerId owner)
        {
            if (MutationBlocked || recipe?.Schema != "mx.character-test.tactical-fixed-rewards.v1"
                || string.IsNullOrWhiteSpace(recipe.RecipeId) || recipe.Revision <= 0 || recipe.Entries?.Length == 0
                || recipe.Entries == null || string.IsNullOrWhiteSpace(battleId))
                throw new InvalidOperationException("固定奖励配方不可用，结算保留待重试。");
            if (!TryReadCharacterItem(characterId, owner, out var character, out string issue)) throw new InvalidOperationException(issue);
            var value = new CharacterTacticalRewardBundle { BattleId = battleId, RecipeId = recipe.RecipeId,
                RecipeRevision = recipe.Revision, OwnerId = owner.Value, CharacterItemInstanceId = characterId.Value,
                CharacterStableId = character.CharacterStableId, CharacterDisplayName = character.DisplayName };
            var lines = new List<CharacterTacticalRewardLine>();
            for (int index = 0; index < recipe.Entries.Length; index++)
            {
                var request = Clone(recipe.Entries[index]);
                if (request == null || request.RequestId != 0 || request.OwnerId != 0 || request.CharacterItemInstanceId != 0
                    || request.ExpectedPlayerRevision != 0 || request.ExpectedCharacterRevision != 0 || request.Frame != 0)
                    throw new InvalidOperationException("固定配方不能携带玩家、请求或运行帧身份。");
                request.RequestId = CharacterTacticalRewardRules.RequestId(battleId, index);
                request.OwnerId = owner.Value;
                if (request.Kind == CharacterTacticalContentKind.MainCard || request.Kind == CharacterTacticalContentKind.Action)
                    request.CharacterItemInstanceId = characterId.Value;
                lines.Add(DescribeReward(request, character));
            }
            value.Lines = lines.ToArray(); value.Fingerprint = CharacterTacticalRewardRules.Fingerprint(value);
            CharacterTacticalRewardRules.Validate(new CharacterTacticalBattleSettlement {
                BattleId = battleId, PlayerTeamId = 1, WinningTeamId = 1, Rewards = value });
            return value;
        }

        private CharacterTacticalRewardLine DescribeReward(CharacterTacticalContentGrantRequest request, CharacterTacticalCharacterItemState target)
        {
            bool role = request.Kind == CharacterTacticalContentKind.Character, accessory = request.Kind == CharacterTacticalContentKind.Accessory;
            bool nested = request.Kind == CharacterTacticalContentKind.MainCard || request.Kind == CharacterTacticalContentKind.Action;
            if (!Enum.IsDefined(typeof(CharacterTacticalContentKind), request.Kind) || request.Kind == CharacterTacticalContentKind.Unspecified
                || nested != (request.CharacterItemInstanceId > 0) || !nested && request.CharacterItemInstanceId != 0
                || (role || accessory) != (request.ItemDefinitionId > 0 && request.TargetContainerId > 0)
                || nested && (request.ItemDefinitionId != 0 || request.TargetContainerId != 0)
                || (request.Kind == CharacterTacticalContentKind.MainCard) != (request.MainCard != null)
                || (request.Kind == CharacterTacticalContentKind.Action) != !string.IsNullOrWhiteSpace(request.ActionId)
                || request.Kind != CharacterTacticalContentKind.Action && request.ActionId != null
                || role != !string.IsNullOrWhiteSpace(request.DisplayName) || !role && request.DisplayName != null)
                throw new InvalidOperationException("奖励载荷与四类授予合同不一致。");
            var line = new CharacterTacticalRewardLine { Grant = Clone(request) };
            switch (request.Kind)
            {
                case CharacterTacticalContentKind.MainCard:
                    var card = _catalog.ExactCard(request.MainCard);
                    if (request.MainCard.InstanceId != 0 || card?.CanBeAssemblyMain != true
                        || _catalog.Pool(target.CharacterStableId)?.Cards.Any(value => Same(value, request.MainCard)) != true)
                        throw new InvalidOperationException("固定主卡不在此角色的精确正式获取范围。");
                    line.Label = string.IsNullOrWhiteSpace(card.DisplayName) ? card.CardId : card.DisplayName;
                    line.ContentFingerprint = Digest(JsonConvert.SerializeObject(request.MainCard));
                    line.RequestFingerprint = Digest(JsonConvert.SerializeObject(new { Kind = "card", Character = request.CharacterItemInstanceId,
                        Owner = request.OwnerId, request.MainCard.CardId, request.MainCard.Revision, request.MainCard.ContentHash }));
                    break;
                case CharacterTacticalContentKind.Action:
                    var action = _catalog.Action(request.ActionId);
                    if (action == null || !action.CharacterStableIds.Contains(target.CharacterStableId)
                        || !_catalog.ContainsAction(_catalog.Pool(target.CharacterStableId)?.ActionIds, request.ActionId))
                        throw new InvalidOperationException("固定动作不在此角色明确学习范围。");
                    line.Label = string.IsNullOrWhiteSpace(action.DisplayName) ? action.Id : action.DisplayName;
                    line.ContentFingerprint = ActionFingerprint(action);
                    line.RequestFingerprint = Digest(JsonConvert.SerializeObject(new { Item = request.CharacterItemInstanceId, Owner = request.OwnerId,
                        Action = action.ActionId, Content = ActionFingerprint(action), Rule = 1 }));
                    break;
                case CharacterTacticalContentKind.Accessory:
                    var definition = _assemblyConfiguration?.Accessories.FirstOrDefault(value => value.ItemDefinitionId == request.ItemDefinitionId);
                    if (definition == null) throw new InvalidOperationException("奖励配件尚无正式配置。");
                    line.Label = definition.Reference.AccessoryId;
                    line.ContentFingerprint = Digest(JsonConvert.SerializeObject(definition));
                    line.RequestFingerprint = Digest(JsonConvert.SerializeObject(new { Kind = "accessory", Target = request.TargetContainerId,
                        Owner = request.OwnerId, Definition = request.ItemDefinitionId, definition.Reference }));
                    break;
                case CharacterTacticalContentKind.Character:
                    if (_characterItemDefinitions == null || !_characterItemDefinitions.TryGetValue(request.ItemDefinitionId, out var template)
                        || !_defaults.TryGetValue(template, out var initial)) throw new InvalidOperationException("奖励角色缺少正式定义与初始配方。");
                    line.Label = request.DisplayName;
                    line.ContentFingerprint = Digest(JsonConvert.SerializeObject(new { Template = template, Initial = initial }));
                    line.RequestFingerprint = CharacterAcquisitionFingerprint(new ItemAcquisitionRequest(new(request.RequestId),
                        ItemAcquisitionSourceKind.DirectGrant, null, null), new(request.TargetContainerId), new(request.OwnerId),
                        new(request.ItemDefinitionId), template, request.DisplayName);
                    break;
            }
            if (!nested && (_itemContainers == null || !_itemContainers.Any(value => value.ContainerId.Value == request.TargetContainerId
                && value.OwnerId.Value == request.OwnerId))) throw new InvalidOperationException("奖励背包不属于固定的玩家所有者。");
            return line;
        }

        /// <summary>Durable whole-batch transaction; preparation and reward receipt share the existing profile checkpoint.</summary>
        /// <remarks>LowFreqAlloc. Must originate from the application's Simulation command tick. All existing mutations
        /// are gated until persistence/publication finishes. Detached single-grant calls perform validation only on a private
        /// staging service with a no-I/O store; no partial live publish or per-entry disk write is possible.</remarks>
        internal async Task<CharacterTacticalRewardClaimResult> ClaimBattleRewardsAsync(CharacterTacticalProgressCandidate progress,
            string battleId, RuntimeTickContext context)
        {
            if (MutationBlocked) return new(CharacterTacticalRewardClaimStatus.Busy, "领奖正在处理中，请稍候。");
            if (progress == null || context.Stage != RuntimeTickStage.Simulation || context.FrameIndex < LastItemTransactionFrame)
                return new(CharacterTacticalRewardClaimStatus.Rejected, "领奖进度或逻辑帧不可用。");
            _rewardCommitInFlight = true;
            try
            {
                if (_saving != null && !_saving.IsCompleted) await _saving;
                if (HasUnsavedChanges && !await SaveLoopAsync()) return new(CharacterTacticalRewardClaimStatus.SaveFailed, SaveIssue);
                var result = await progress.ClaimRewardsAsync(battleId, bundle => StageRewardBatch(bundle, context), batch => {
                    _state = batch.Player; Revision = checked(Revision + 1); _savedRevision = Revision; SaveIssue = string.Empty;
                });
                if (result.Success) ValidateRewardReceipts(result.Settlement.Rewards);
                return result;
            }
            catch (Exception error) { return new(CharacterTacticalRewardClaimStatus.Rejected, "领奖校验失败，保留原进度：" + error.Message); }
            finally
            {
                _rewardCommitInFlight = false;
                // Notification failures must not turn a committed reward into a retryable grant.
                try { Changed?.Invoke(); } catch (Exception error) { SaveIssue = "奖励状态已保存，界面通知失败：" + error.Message; }
            }
        }

        private CharacterTacticalRewardBatch StageRewardBatch(CharacterTacticalRewardBundle bundle, RuntimeTickContext context)
        {
            if (Revision > long.MaxValue - bundle.Lines.Length) throw new InvalidOperationException("玩家版本已用尽。");
            if (!TryReadCharacterItem(new(bundle.CharacterItemInstanceId), new(bundle.OwnerId), out var target, out var issue)
                || target.CharacterStableId != bundle.CharacterStableId) throw new InvalidOperationException(issue ?? "奖励目标角色已变更。");
            var staged = new CharacterTacticalPlayerService(_catalog, _defaults.Values, new DetachedRewardStore()) {
                _state = Clone(_state), Revision = Revision, _savedRevision = Revision,
                _itemDefinitions = _itemDefinitions, _itemContainers = _itemContainers,
                _characterItemDefinitions = new Dictionary<int, string>(_characterItemDefinitions),
                _assemblyConfiguration = Clone(_assemblyConfiguration)
            };
            var receipts = new List<CharacterTacticalRewardGrantReceipt>();
            foreach (var line in bundle.Lines)
            {
                var resolved = staged.DescribeReward(line.Grant, target);
                if (resolved.ContentFingerprint != line.ContentFingerprint || resolved.RequestFingerprint != line.RequestFingerprint)
                    throw new InvalidOperationException("奖励正式定义已变化；保留原奖励，未替换或部分发放。");
                var request = Clone(line.Grant); request.ExpectedPlayerRevision = staged.Revision; request.Frame = context.FrameIndex;
                if (request.CharacterItemInstanceId > 0)
                {
                    if (!staged.TryReadCharacterItem(new(request.CharacterItemInstanceId), new(request.OwnerId), out var current, out issue))
                        throw new InvalidOperationException(issue);
                    request.ExpectedCharacterRevision = current.Revision;
                }
                var receipt = staged.GrantContentAtTick(request, context, out issue);
                if (!receipt.Result.Success) throw new InvalidOperationException(string.IsNullOrWhiteSpace(issue) ? receipt.Result.Status.ToString() : issue);
                receipts.Add(new CharacterTacticalRewardGrantReceipt { RequestId = request.RequestId, Kind = request.Kind,
                    ItemInstanceId = receipt.ItemInstanceId.Value, CardInstanceId = receipt.CardInstanceId });
            }
            staged.ValidateState();
            var inventory = CreateItemCandidate(staged._state.Items, _itemDefinitions, _itemContainers, out _);
            ValidateCharacterItemContent(staged._state, inventory, _characterItemDefinitions);
            ValidateAssemblyInventory(staged._state, inventory, _itemDefinitions, _characterItemDefinitions, _assemblyConfiguration);
            var complete = Clone(bundle); complete.State = CharacterTacticalRewardState.Claimed;
            complete.Receipts = receipts.ToArray(); complete.ClaimedAtUtc = DateTime.UtcNow.ToString("O");
            staged.ValidateRewardReceipts(complete);
            return new CharacterTacticalRewardBatch { Player = staged._state, Receipts = receipts.ToArray() };
        }

        /// <remarks>LowFreqAlloc. Reopen validates the persisted acquisition join; it never executes grants.</remarks>
        internal void ValidateRewardReceipts(CharacterTacticalRewardBundle bundle)
        {
            if (bundle == null) return;
            var inventory = CreateItemCandidate(_state.Items, _itemDefinitions, _itemContainers, out var ledger);
            if (bundle.State == CharacterTacticalRewardState.Pending)
            {
                if (bundle.Lines.Any(line => ledger.Contains(new(line.Grant.RequestId))))
                    throw new JsonSerializationException("TACTICAL_REWARD_PENDING_HAS_GRANTS");
                return;
            }
            for (int index = 0; index < bundle.Lines.Length; index++)
            {
                var line = bundle.Lines[index]; var receipt = bundle.Receipts[index]; var request = line.Grant;
                if (!ledger.Contains(new(request.RequestId))) throw new JsonSerializationException("TACTICAL_REWARD_LEDGER_MISSING");
                string fingerprint = null;
                switch (request.Kind)
                {
                    case CharacterTacticalContentKind.MainCard:
                        var character = _state.CharacterItems.Find(value => value.ItemInstanceId == request.CharacterItemInstanceId);
                        var acquisition = character?.CardAcquisitions?.Find(value => value.RequestId == request.RequestId && value.CardInstanceId == receipt.CardInstanceId);
                        if (character?.Cards.Any(value => value.InstanceId == receipt.CardInstanceId && Same(value, request.MainCard)) == true)
                            fingerprint = acquisition?.RequestFingerprint;
                        break;
                    case CharacterTacticalContentKind.Action:
                        fingerprint = _state.CharacterItems.Find(value => value.ItemInstanceId == request.CharacterItemInstanceId)?.ActionGrants
                            ?.Find(value => value.RequestId == request.RequestId && value.Source == "direct")?.RequestFingerprint;
                        break;
                    case CharacterTacticalContentKind.Accessory:
                        fingerprint = _state.AccessoryItems?.Find(value => value.AcquisitionRequestId == request.RequestId
                            && value.ItemInstanceId == receipt.ItemInstanceId)?.AcquisitionFingerprint;
                        break;
                    case CharacterTacticalContentKind.Character:
                        fingerprint = _state.CharacterItems.Find(value => value.AcquisitionRequestId == request.RequestId
                            && value.ItemInstanceId == receipt.ItemInstanceId)?.AcquisitionFingerprint;
                        break;
                }
                if (fingerprint != line.RequestFingerprint) throw new JsonSerializationException("TACTICAL_REWARD_RECEIPT_JOIN_INVALID");
            }
        }

        private sealed class DetachedRewardStore : ICharacterTacticalStateStore
        {
            public Task<string> LoadAsync() => Task.FromResult<string>(null);
            public Task SaveAsync(string json) => Task.CompletedTask;
            public Task ResetAsync(string json) => throw new InvalidOperationException("Detached reward staging cannot reset progress.");
        }
    }
}
