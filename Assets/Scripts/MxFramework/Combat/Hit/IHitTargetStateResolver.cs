using MxFramework.Combat.Core;

namespace MxFramework.Combat.Hit
{
    public interface IHitTargetStateResolver
    {
        HitTargetStateFlags ResolveTargetState(CombatEntityId targetId);
    }
}
