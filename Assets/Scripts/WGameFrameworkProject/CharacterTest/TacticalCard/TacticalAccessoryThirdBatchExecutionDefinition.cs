using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Explicit v4 trial contract. Values match the independently reviewed rules; no implicit defaults.</summary>
    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class TacticalAccessoryThirdBatchExecutionDefinition
    {
        public int? DamageScaleMilli { get; set; }
        public int? CoinSelfWeight { get; set; }
        public int? CoinEnemyWeight { get; set; }
        internal bool IsValid(TacticalAccessoryCombatKind kind, TacticalAccessoryLifetimeKind lifetime, int turns)
        {
            if (!DamageScaleMilli.HasValue || !CoinSelfWeight.HasValue || !CoinEnemyWeight.HasValue) return false;
            bool coin = kind == TacticalAccessoryCombatKind.LuckyCoin;
            if (kind == TacticalAccessoryCombatKind.IronThrone)
                return DamageScaleMilli == 0 && CoinSelfWeight == 0 && CoinEnemyWeight == 0
                    && lifetime == TacticalAccessoryLifetimeKind.UntilBattleEnd && turns == 0;
            return (coin || kind == TacticalAccessoryCombatKind.GlassCannon)
                && DamageScaleMilli == TacticalAccessoryThirdBatchRules.TrialScaleMilli
                && CoinSelfWeight == (coin ? 1 : 0) && CoinEnemyWeight == (coin ? 1 : 0)
                && lifetime == TacticalAccessoryLifetimeKind.LimitedTurns && turns == 2;
        }
        internal TacticalThirdBatchParameters Freeze(TacticalAccessoryCombatKind kind)
            => new TacticalThirdBatchParameters(kind == TacticalAccessoryCombatKind.GlassCannon,
                kind == TacticalAccessoryCombatKind.LuckyCoin, kind == TacticalAccessoryCombatKind.IronThrone);
    }

    internal readonly struct TacticalThirdBatchParameters
    {
        internal TacticalThirdBatchParameters(bool glass, bool coin, bool throne)
        { GlassCannon = glass; LuckyCoin = coin; IronThrone = throne; }
        internal bool GlassCannon { get; }
        internal bool LuckyCoin { get; }
        internal bool IronThrone { get; }
        internal bool IsValid => GlassCannon || LuckyCoin || IronThrone;
    }
}
