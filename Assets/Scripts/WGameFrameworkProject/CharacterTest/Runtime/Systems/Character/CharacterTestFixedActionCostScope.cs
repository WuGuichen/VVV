using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Derived synchronous command wiring. Original Ability owner still pays and writes cooldown.</summary>
    internal sealed class CharacterTestFixedActionCostScope
    {
        private readonly TacticalFixedCardPaymentCommit _payment;
        private CharacterTestCharacterEntity _entity;
        private IReadOnlyList<AbilityCostConfig> _costs;
        private int _action, _amount;
        private bool _entered, _committed;
        internal CharacterTestRuntimeAiPlannerFactState Facts { get; private set; }
        internal CharacterTestFixedActionCostScope(TacticalFixedCardPaymentCommit payment) { _payment = payment; }
        internal bool Enter(CharacterTestCharacterEntity entity, int action, IReadOnlyList<AbilityCostConfig> costs,
            int amount, CharacterTestRuntimeAiPlannerFactState facts)
        {
            if (_entered || entity?.GameplayEntityId != _payment.Record.Actor || !_payment.CanPayAction(action, amount)) return false;
            _entity = entity; _action = action; _costs = costs; _amount = amount; Facts = facts;
            _committed = false; _entered = true; return true;
        }
        internal void Exit() { _entered = false; _entity = null; _costs = null; Facts = null; }
        internal int Credit(CharacterTestCharacterEntity entity, int action) => _entered
            && ReferenceEquals(entity, _entity) && action == _action && _payment.HasReservation ? _payment.Ledger.Reserved : 0;
        internal bool CanAfford(CharacterTestCharacterEntity entity, IReadOnlyList<AbilityCostConfig> costs, out int missing)
        {
            missing = CharacterTestCharacterAttributeIds.Motion;
            if (!_entered || _committed || !ReferenceEquals(entity, _entity) || !ReferenceEquals(costs, _costs)
                || !_payment.CanPayAction(_action, _amount)) return false;
            missing = 0; return true;
        }
        internal bool TryCommit(CharacterTestCharacterEntity entity, IReadOnlyList<AbilityCostConfig> costs, out int missing)
        {
            if (!CanAfford(entity, costs, out missing) || !_payment.TrySpendActualAction(_action, _amount)) return false;
            _committed = true; return true;
        }
    }

    internal sealed partial class CharacterTestAbilityCostAuthority
    {
        private CharacterTestFixedActionCostScope _fixedScope;
        internal bool EnterFixedScope(CharacterTestFixedActionCostScope scope)
        { if (_fixedScope != null || scope == null) return false; _fixedScope = scope; return true; }
        internal void ExitFixedScope() { _fixedScope = null; }
    }

    internal interface ICharacterTestFixedBehaviorActionContext
    {
        bool TryGetFixedActionPayment(RuntimeFrame frame, int entity, int resource,
            out TacticalFixedCardPaymentCommit payment, out CharacterTestRuntimeAiPlannerFactState facts);
    }

    public sealed partial class CharacterTestCharacterRuntimeModule
    {
        private CharacterTestAbilityCostAuthority _abilityCostAuthority;
        private CharacterTestFixedActionCostScope _fixedActionScope;
        internal int FixedActionReservationCredit(CharacterTestCharacterEntity entity, int action)
            => _fixedActionScope?.Credit(entity, action) ?? 0;
        private bool TryEnterFixedActionScope(RuntimeFrame frame, int entityId, int actionId, int source,
            RuntimeTraceIdentity trace, in RuntimeCommandEnvelope envelope)
        {
            var actor = FindEntityById(entityId);
            if (actor == null || _fixedActionScope != null || _abilityCostAuthority == null
                || !_world.TryGetTacticalMotion(entityId, out var ledger)) return false;
            for (int i = _behaviorReceiptObservers.Count - 1; i >= 0; i--)
            {
                if (!_behaviorReceiptObservers[i].TryGetTarget(out var receiver)
                    || !(receiver is ICharacterTestFixedBehaviorActionContext context)
                    || !context.TryGetFixedActionPayment(frame, entityId, actionId, out var payment, out var facts)
                    || !ReferenceEquals(payment.Ledger, ledger) || !payment.MatchesCommand(frame, entityId, actionId, source, trace, envelope)
                    || actor.GameplayEntityId != payment.Record.Actor) continue;
                if (!_actionUseAuthority.TryGetFixedActionOwnerCosts(entityId, actionId, out var costs, out var amount)) return false;
                var scope = payment.ActionScope;
                if (!scope.Enter(actor, actionId, costs, amount, facts)) return false;
                if (!_abilityCostAuthority.EnterFixedScope(scope)) { scope.Exit(); return false; }
                _fixedActionScope = scope; return true;
            }
            return false;
        }
        private void ExitFixedActionScope()
        { _abilityCostAuthority?.ExitFixedScope(); _fixedActionScope?.Exit(); _fixedActionScope = null; }
    }

    internal sealed partial class CharacterTestActionUseAuthority
    {
        internal bool TryGetFixedActionOwnerCosts(int entity, int actionId, out IReadOnlyList<AbilityCostConfig> costs, out int amount)
        {
            costs = null;
            if (!TryGetFixedActionMomentumCost(entity, actionId, out amount, out _)
                || !_runtime.ActionRuntime.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(actionId), out var action)) return false;
            var result = ResolveCooldownOwner(entity, action, null, out var owner);
            if (!result.Available) return false;
            costs = owner?.Costs; return true;
        }
    }
}
