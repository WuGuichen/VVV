using System.Text;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest
{
    // LowFreqAlloc: immutable committed projections only. Never publishes a coin, admits a card or mutates Stage state.
    internal static class CharacterTestTacticalAccessoryStateDisplay
    {
        internal static string Describe(TacticalAccessoryThirdBatchReadout value, TacticalThirdBatchParameters parts, out string compact)
        {
            var text = new StringBuilder(); var small = new StringBuilder();
            if (parts.GlassCannon) {
                string output = value.GlassPhase == TacticalThirdBatchGlassPhase.OutputAndRiskPending ? "输出待兑现 · 风险待付"
                    : value.GlassPhase == TacticalThirdBatchGlassPhase.RiskPaidOutputPending ? "输出待兑现 · 风险已付"
                    : value.GlassPhase == TacticalThirdBatchGlassPhase.Settled ? "已结算 · 风险关闭" : "无已提交状态";
                text.Append("玻璃大炮 · ").Append(output).Append(" · 至完整回合 ").Append(value.ExpiresAfterFullTurn).Append(" 结算\n");
                small.Append(value.GlassPhase == TacticalThirdBatchGlassPhase.Settled ? "玻璃已结算" : "玻璃待兑现");
            }
            if (parts.LuckyCoin) {
                var coin = value.Coin;
                string phase = value.CoinPhase == TacticalThirdBatchCoinPhase.WaitingEnemy ? "pending · 等待下一合格攻击"
                    : value.CoinPhase == TacticalThirdBatchCoinPhase.EnemyAttack ? "bound · 已绑定敌人攻击"
                    : value.CoinPhase == TacticalThirdBatchCoinPhase.SelfExecution ? "bound · 已绑定自身执行"
                    : value.CoinPhase == TacticalThirdBatchCoinPhase.Ended ? "绑定动作结束／效果终止；既有来源按期限结算" : "未发布";
                text.Append("幸运硬币 · ").Append(coin.IsValid ? coin.Face == TacticalThirdBatchCoinFace.SelfExecution ? "自身面" : "敌方面" : "无真实回执")
                    .Append(" · ").Append(phase);
                if (coin.IsValid) {
                    text.Append(" · 受益者 ").Append(coin.Face == TacticalThirdBatchCoinFace.SelfExecution ? "持有者 " + Entity(value.Key.Actor) : "指定敌人 " + Entity(coin.Enemy));
                    if (value.BoundEnemyAttack.IsValid) text.Append(" · 执行 ").Append(value.BoundEnemyAttack.Execution)
                        .Append("／攻击 ").Append(value.BoundEnemyAttack.ActionInstance).Append("／revision ").Append(value.BoundEnemyAttack.Revision);
                    else if (value.CoinPhase == TacticalThirdBatchCoinPhase.SelfExecution) text.Append(" · 执行 ").Append(value.Key.Execution);
                    text.Append(" · 至完整回合 ").Append(coin.ExpiresAfterFullTurn).Append(" 结算");
                }
                text.Append('\n'); if (small.Length != 0) small.Append(" · ");
                small.Append(!coin.IsValid ? "硬币未发布" : coin.Face == TacticalThirdBatchCoinFace.SelfExecution ? "硬币自身面" : "硬币敌方面")
                    .Append(value.CoinPhase == TacticalThirdBatchCoinPhase.WaitingEnemy ? " · 敌人待绑定"
                    : value.CoinPhase == TacticalThirdBatchCoinPhase.Ended ? " · 动作结束" : coin.IsValid ? " · 已绑定" : "");
                if (coin.IsValid) small.Append(" · 至回合 ").Append(coin.ExpiresAfterFullTurn).Append(" 结算");
            }
            if (parts.IronThrone) {
                text.Append("铁王座 · 绑定执行 ").Append(value.Key.Execution).Append(value.IronThroneActive ? " · 当前生效" : " · 当前未生效（等待ACK或执行已终止）").Append('\n');
                if (small.Length != 0) small.Append(" · "); small.Append(value.IronThroneActive ? "王座执行中" : "王座绑定执行");
            }
            compact = small.ToString(); return text.ToString().TrimEnd();
        }
        internal static bool TryDescribe(TacticalDoubleEdgeBattleState world, TacticalFixedAccessoryAdmissionAuthority journal,
            GameplayEntityId actor, out string compact, out string detail)
        {
            compact = detail = "";
            if (world == null || journal == null || !actor.IsValid) return false;
            var text = new StringBuilder(); ulong latest = 0;
            for (int index = 0; index < world.ThirdReadoutCount(actor); index++) {
                if (!world.TryGetThirdReadout(actor, index, out var value)) continue;
                TacticalFixedCardCommitManifest paid = null;
                for (int i = 0; i < journal.CommitCount; i++) {
                    var row = journal.CommitAt(i);
                    if (row.IsConfirmed && row.DoubleEdgeKey.Matches(value.Key)) { paid = row; break; }
                }
                if (paid?.Assembly.HasThirdBatch != true) continue;
                string rowText = Describe(value, paid.Assembly.ThirdBatchParameters, out string rowCompact);
                if (text.Length != 0) text.Append('\n'); text.Append("牌 ").Append(value.Key.BattleCardInstance).Append(" · 提交 ").Append(value.Key.Commit).Append('\n').Append(rowText);
                if (value.Key.Commit >= latest) { latest = value.Key.Commit; compact = rowCompact; }
            }
            detail = text.ToString(); return detail.Length != 0;
        }
        private static string Entity(GameplayEntityId actor) => actor.Index + ":" + actor.Generation;
    }
}
