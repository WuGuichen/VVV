using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Explicit v2 author parameters, copied into the immutable joint manifest.</summary>
    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class TacticalDoubleEdgeExecutionDefinition
    {
        public int DamageScaleMilli { get; set; }
        public int RadiusMillimeters { get; set; }
        public int StopRadiusMillimeters { get; set; }
        public int AccelerationMillimetersPerSecondSquared { get; set; }
        public int ReferenceMassGrams { get; set; }
        public int MaximumAcceleration { get; set; }
        public int MaximumRadialSpeed { get; set; }

        internal bool IsValid(TacticalAccessoryCombatKind kind, TacticalAccessoryLifetimeKind lifetime, int turns)
        {
            if (kind == TacticalAccessoryCombatKind.BlackHolePocket)
                return DamageScaleMilli == 1000 && lifetime == TacticalAccessoryLifetimeKind.LimitedTurns && turns == 2
                    && Freeze(kind).IsValid;
            return (kind == TacticalAccessoryCombatKind.DuelInvitation
                    && lifetime == TacticalAccessoryLifetimeKind.LimitedTurns && turns == 1
                || kind == TacticalAccessoryCombatKind.ShatteredFantasy
                    && lifetime == TacticalAccessoryLifetimeKind.UntilBattleEnd && turns == 0)
                && DamageScaleMilli > 1000 && RadiusMillimeters == 0 && StopRadiusMillimeters == 0
                && AccelerationMillimetersPerSecondSquared == 0 && ReferenceMassGrams == 0
                && MaximumAcceleration == 0 && MaximumRadialSpeed == 0;
        }
        internal TacticalDoubleEdgeParameters Freeze(TacticalAccessoryCombatKind kind)
            => new TacticalDoubleEdgeParameters(kind == TacticalAccessoryCombatKind.DuelInvitation ? DamageScaleMilli : 1000,
                kind == TacticalAccessoryCombatKind.ShatteredFantasy ? DamageScaleMilli : 1000,
                kind == TacticalAccessoryCombatKind.BlackHolePocket,
                new TacticalBlackHolePullParameters(RadiusMillimeters, StopRadiusMillimeters,
                    AccelerationMillimetersPerSecondSquared, ReferenceMassGrams), MaximumAcceleration, MaximumRadialSpeed);
    }
}
