using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class CharacterTacticalProgressCandidate
    {
        /// <remarks>LowFreqAlloc. Detached; the published manifest is the only retry/restore authority.</remarks>
        internal CharacterTacticalBattleSettlement ReadSettlement(string battleId)
            => CharacterTacticalRewardRules.Copy(_profile.Settlements.Find(value => value.BattleId == battleId));
        internal CharacterTacticalBattleSettlement[] ReadSettlements() => CharacterTacticalRewardRules.Copy(_profile.Settlements.ToArray());
        internal CharacterTacticalBattleSettlement ReadPendingReward()
            => CharacterTacticalRewardRules.Copy(_profile.Settlements.Find(value => value.Rewards?.State == CharacterTacticalRewardState.Pending));

        /// <summary>One profile write contains all grants, allocator/ledger state and the terminal claim receipt.</summary>
        /// <remarks>LowFreqAlloc. The caller holds the player mutation gate; publication occurs while this write gate is held.
        /// Ordinary saves capture after this gate, so queued stale snapshots cannot erase a claim.</remarks>
        internal async Task<CharacterTacticalRewardClaimResult> ClaimRewardsAsync(string battleId,
            Func<CharacterTacticalRewardBundle, CharacterTacticalRewardBatch> stage,
            Action<CharacterTacticalRewardBatch> publish)
        {
            await _writes.WaitAsync();
            try
            {
                var prior = _profile.Settlements.Find(value => value.BattleId == battleId);
                if (prior?.Rewards == null) return new(CharacterTacticalRewardClaimStatus.Rejected, "此结算没有待领取奖励。");
                CharacterTacticalRewardRules.Validate(prior);
                if (prior.Rewards.State == CharacterTacticalRewardState.Claimed)
                    return new(CharacterTacticalRewardClaimStatus.AlreadyClaimed, settlement: CharacterTacticalRewardRules.Copy(prior));
                var snapshot = CharacterTacticalRewardRules.Copy(_profile);
                var settlement = snapshot.Settlements.Find(value => value.BattleId == battleId);
                CharacterTacticalRewardBatch batch;
                try { batch = stage(CharacterTacticalRewardRules.Copy(settlement.Rewards)); }
                catch (Exception error) { return new(CharacterTacticalRewardClaimStatus.Rejected, "整批奖励未发放：" + error.Message); }
                snapshot.PlayerJson = JsonConvert.SerializeObject(batch.Player);
                snapshot.PreparationJson = _capturePreparation();
                settlement.Rewards.State = CharacterTacticalRewardState.Claimed;
                settlement.Rewards.ClaimedAtUtc = DateTime.UtcNow.ToString("O");
                settlement.Rewards.Receipts = batch.Receipts;
                CharacterTacticalRewardRules.Validate(settlement);
                try { await _owner.CommitAsync(snapshot, false); }
                catch (Exception error) { return new(CharacterTacticalRewardClaimStatus.SaveFailed, "奖励尚未领取，保存失败可重试：" + error.Message); }
                _profile = snapshot;
                publish(batch); // State assignments only. Observer/UI callbacks run after this gate is released.
                return new(CharacterTacticalRewardClaimStatus.Claimed, settlement: CharacterTacticalRewardRules.Copy(settlement));
            }
            finally { _writes.Release(); }
        }
    }
}
