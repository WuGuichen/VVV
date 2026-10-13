using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestActionUseAuthority
    {
        /// <summary>NoAlloc for a stable catalog. Exact definition cost through the same Ability owner
        /// resolution as real action commit. Does not evaluate current balance, pay or begin cooldown.</summary>
        internal bool TryGetFixedActionMomentumCost(int entityId, int actionId, out int amount, out string issue)
        {
            amount = 0; issue = "budget:ActionOrActorMissing";
            if (_runtime?.FindEntityById(entityId) == null || _runtime.ActionRuntime?.Registry == null
                || !_runtime.ActionRuntime.Registry.TryGetAction(new CharacterActionId(actionId), out var action)) return false;
            var resolution = ResolveCooldownOwner(entityId, action, null, out var owner);
            if (!resolution.Available) { issue = "budget:ActionCostOwnerUnavailable"; return false; }
            for (int i = 0; owner?.Costs != null && i < owner.Costs.Count; i++)
            {
                var cost = owner.Costs[i];
                if (cost == null || cost.Amount < 0) { issue = "budget:InvalidActionCost"; return false; }
                if (cost.Amount > 0 && cost.AttributeId != CharacterTestCharacterAttributeIds.Motion)
                { issue = "budget:UnsupportedAdditionalResource"; return false; }
            }
            if (!CharacterTestAbilityCosts.TryGetMotionCost(owner?.Costs, out amount))
            { issue = "budget:Overflow"; return false; }
            issue = null; return true;
        }
    }
}
