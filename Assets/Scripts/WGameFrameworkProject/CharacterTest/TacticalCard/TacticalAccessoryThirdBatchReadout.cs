using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Immutable projection of a published Stage entry. Reading never prepares or draws a coin.
    /// CoinPhase.Ended closes the action; frozen born sources can still resolve until explicit revocation/settlement.</summary>
    internal readonly struct TacticalAccessoryThirdBatchReadout
    {
        internal TacticalAccessoryThirdBatchReadout(TacticalDoubleEdgeCommitKey key, TacticalAccessoryThirdBatchState state, bool throneActive)
        {
            Key = key; GlassPhase = state.GlassPhase; CoinPhase = state.CoinPhase; Coin = state.Coin;
            BoundEnemyAttack = state.BoundEnemyAttack; ExpiresAfterFullTurn = state.Commit.ExpiresAfterFullTurn;
            IronThroneActive = throneActive;
        }
        internal TacticalDoubleEdgeCommitKey Key { get; }
        internal TacticalThirdBatchGlassPhase GlassPhase { get; }
        internal TacticalThirdBatchCoinPhase CoinPhase { get; }
        internal TacticalThirdBatchCoinReceipt Coin { get; }
        internal TacticalThirdBatchAttack BoundEnemyAttack { get; }
        internal ulong ExpiresAfterFullTurn { get; }
        internal bool IronThroneActive { get; }
    }
    internal sealed partial class TacticalDoubleEdgeBattleState
    {
        internal int ThirdReadoutCount(GameplayEntityId actor)
        { int count = 0; foreach (var e in _entries) if (e.Key.Actor == actor && e.ThirdBatch != null) count++; return count; }
        internal bool TryGetThirdReadout(GameplayEntityId actor, int index, out TacticalAccessoryThirdBatchReadout value)
        {
            value = default;
            if (index < 0) return false;
            foreach (var e in _entries) {
                if (e.Key.Actor != actor || e.ThirdBatch == null) continue;
                if (index-- != 0) continue;
                var current = CurrentThirdAttack(actor);
                bool active = e.ThirdBatchParameters.IronThrone && !e.Terminal && current.Execution == e.Key.Execution
                    && SuppressesThirdMotion(actor, current, TacticalThirdBatchMotionChannel.ActiveTranslation);
                value = new TacticalAccessoryThirdBatchReadout(e.Key, e.ThirdBatch, active); return true;
            }
            return false;
        }
    }
}
