using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;

namespace MxFramework.Combat.Animation
{
    public sealed class ActionStateToHitTargetAdapter : IHitTargetStateResolver
    {
        private readonly CombatActionRunner _actionRunner;
        private readonly IHitTargetStateResolver _fallbackResolver;

        public ActionStateToHitTargetAdapter(CombatActionRunner actionRunner)
            : this(actionRunner, null)
        {
        }

        public ActionStateToHitTargetAdapter(CombatActionRunner actionRunner, IHitTargetStateResolver fallbackResolver)
        {
            _actionRunner = actionRunner ?? throw new ArgumentNullException(nameof(actionRunner));
            _fallbackResolver = fallbackResolver;
        }

        public HitTargetStateFlags ResolveTargetState(CombatEntityId targetId)
        {
            HitTargetStateFlags state = _fallbackResolver == null
                ? HitTargetStateFlags.Alive
                : _fallbackResolver.ResolveTargetState(targetId);

            if (_actionRunner.IsInInvincibleWindow(targetId))
            {
                state |= HitTargetStateFlags.Invincible;
            }

            if (_actionRunner.IsInParryWindow(targetId))
            {
                state |= HitTargetStateFlags.Parrying;
            }

            if (_actionRunner.IsInSuperArmorWindow(targetId))
            {
                state |= HitTargetStateFlags.SuperArmor;
            }

            return state;
        }
    }
}
