using MxFramework.Combat.Core;

namespace MxFramework.Combat.Hit
{
    public interface ICombatEventDispatcher
    {
        void DispatchHitResolved(in HitResolveResult result);

        void DispatchHitBlocked(CombatEntityId attackerId, CombatEntityId targetId, int actionId, CombatFrame frame);
    }
}
