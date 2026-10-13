using System;
using System.Linq;
using System.Threading.Tasks;
using MxFramework.Items;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed partial class TacticalCardSliceCoordinator
    {
        private int _rewardCharacterItem, _rewardOwner, _rewardPlayerTeam;
        private bool _rewardClaimQueued, _rewardClaimFailed;
        internal int RewardState => _settlement?.Rewards == null ? 0 : (int)_settlement.Rewards.State;
        internal bool RewardClaimFailed => _rewardClaimFailed;
        internal bool RewardClaiming => _rewardClaimQueued || CharacterTacticalServices.Player?.RewardCommitInFlight == true;
        internal string RewardSummary { get; private set; } = string.Empty;

        private void RememberRewardRecipient(TacticalDeployment deployment)
        {
            var controlled = deployment.Participants.Where(value => value.IsPlayerControlled).ToArray();
            var player = controlled.Length == 1 ? controlled[0] : null;
            _rewardPlayerTeam = player?.CharacterItemInstanceId > 0 && player.InventoryOwnerId > 0 ? player.TeamId : 0;
            _rewardCharacterItem = player?.CharacterItemInstanceId ?? 0;
            _rewardOwner = player?.InventoryOwnerId ?? 0;
            _rewardClaimFailed = false; RewardSummary = string.Empty;
        }

        private void FreezeSettlementRewards()
        {
            if (_settlement.Rewards != null || _settlement.Draw || _settlement.PlayerTeamId <= 0
                || _settlement.WinningTeamId != _settlement.PlayerTeamId) return;
            _settlement.Rewards = CharacterTacticalServices.Player.FreezeBattleRewards(CharacterTacticalServices.RewardRecipe,
                _settlement.BattleId, new ItemInstanceId(_rewardCharacterItem), new InventoryOwnerId(_rewardOwner));
            RefreshRewardSummary();
        }

        private void RefreshRewardSummary()
        {
            var reward = _settlement?.Rewards;
            if (reward == null) { RewardSummary = string.Empty; return; }
            var labels = reward.Lines.GroupBy(value => value.Label, StringComparer.Ordinal)
                .Select(group => group.Key + " ×" + group.Count());
            RewardSummary = "固定奖励：" + string.Join("、", labels) + "\n"
                + "归属：" + reward.CharacterDisplayName + "（角色实例 " + reward.CharacterItemInstanceId + "）";
        }

        private bool RestorePendingReward()
        {
            var pending = CharacterTacticalServices.ReadPendingReward();
            if (pending == null) return false;
            _settlement = pending; _battleId = pending.BattleId; _settlementSaved = true;
            _rewardClaimFailed = false; _rewardClaimQueued = false;
            MoveToPhase(TacticalPreparationState.Completed); Time.timeScale = 0f;
            StatusMessage = "已恢复上次待领取奖励，请确认领取。";
            RefreshRewardSummary(); ShowResultMenu(); return true;
        }

        private void RequestClaimRewards()
        {
            if (IsBusy || Loading.Running || !_settlementSaved || RewardState != (int)CharacterTacticalRewardState.Pending) return;
            _rewardClaimQueued = true; _busy = true; StatusMessage = "正在领取奖励并保存…";
            ShowResultMenu();
        }

        private void DrainRewardClaim(RuntimeTickContext context)
        {
            if (!_rewardClaimQueued) return;
            _rewardClaimQueued = false;
            _ = ClaimRewardsAsync(context);
        }

        private async Task ClaimRewardsAsync(RuntimeTickContext context)
        {
            var player = CharacterTacticalServices.Player;
            string battleId = _settlement?.BattleId;
            try
            {
                var result = await CharacterTacticalServices.ClaimRewardsAsync(battleId, context);
                // Persistence always finishes for its original owner. Late UI callbacks cannot touch a different profile/session.
                if (this == null || !isActiveAndEnabled || !ReferenceEquals(CharacterTacticalServices.Player, player)
                    || _settlement?.BattleId != battleId) return;
                _rewardClaimFailed = !result.Success;
                if (result.Success)
                {
                    _settlement = result.Settlement; _settlementSaved = true;
                    StatusMessage = "奖励已整批领取并保存。"; RefreshRewardSummary();
                }
                else StatusMessage = result.Issue;
            }
            catch (Exception error)
            {
                if (this != null && isActiveAndEnabled && ReferenceEquals(CharacterTacticalServices.Player, player) && _settlement?.BattleId == battleId)
                { _rewardClaimFailed = true; StatusMessage = "领取未完成，请重试：" + error.Message; }
            }
            finally
            {
                if (this != null && isActiveAndEnabled && ReferenceEquals(CharacterTacticalServices.Player, player) && _settlement?.BattleId == battleId)
                { _busy = false; ShowResultMenu(); }
            }
        }

        private sealed class RewardClaimRuntimeModule : RuntimeModule
        {
            private readonly TacticalCardSliceCoordinator _owner;
            internal RewardClaimRuntimeModule(TacticalCardSliceCoordinator owner)
                : base("character-test.reward-claims", RuntimeTickStage.Simulation, 1) => _owner = owner;
            public override void Tick(RuntimeTickContext context) => _owner.DrainRewardClaim(context);
        }
    }
}
