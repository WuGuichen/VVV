using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Explicit v3 author parameters; the joint manifest freezes the approved r2 rule values.</summary>
    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class TacticalAccessorySecondBatchExecutionDefinition
    {
        public int? RearScaleMilli { get; set; }
        public int? OnePunchScaleMilli { get; set; }

        internal bool IsValid(TacticalAccessoryCombatKind kind, TacticalAccessoryLifetimeKind lifetime, int turns)
        {
            if (!RearScaleMilli.HasValue || !OnePunchScaleMilli.HasValue) return false;
            if (kind == TacticalAccessoryCombatKind.NoTurningBack)
                return lifetime == TacticalAccessoryLifetimeKind.LimitedTurns && turns == 1
                    && RearScaleMilli.Value > 1000 && OnePunchScaleMilli.Value == 0;
            if (lifetime != TacticalAccessoryLifetimeKind.UntilBattleEnd || turns != 0 || RearScaleMilli.Value != 0) return false;
            return kind == TacticalAccessoryCombatKind.NonLethalOath && OnePunchScaleMilli.Value == 0
                || kind == TacticalAccessoryCombatKind.OnePunch && OnePunchScaleMilli.Value > 1000;
        }

        internal TacticalSecondBatchParameters Freeze(TacticalAccessoryCombatKind kind)
            => new TacticalSecondBatchParameters(kind == TacticalAccessoryCombatKind.NoTurningBack,
                kind == TacticalAccessoryCombatKind.NonLethalOath, kind == TacticalAccessoryCombatKind.OnePunch,
                RearScaleMilli.GetValueOrDefault(), OnePunchScaleMilli.GetValueOrDefault());
    }
}
