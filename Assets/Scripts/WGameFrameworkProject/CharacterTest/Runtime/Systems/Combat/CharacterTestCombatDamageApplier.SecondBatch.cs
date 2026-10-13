using System;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestCombatDamageApplier : ICombatHpDebitPolicy
    {
        private bool _nonLethalHp;
        private Func<HitResolveResult, TacticalSecondBatchHitDecision> _secondBatchDecisions;

        public int LimitHpDebit(in HitResolveResult hit, GameplayEntityId target, int currentHp, int maximumDebit)
        {
            if (!TacticalAccessorySecondBatchRules.TryLimitHpDebit(_nonLethalHp, currentHp, maximumDebit, out int debit))
                throw new InvalidOperationException("accessory:InvalidHpBudget");
            return debit;
        }
    }
}
