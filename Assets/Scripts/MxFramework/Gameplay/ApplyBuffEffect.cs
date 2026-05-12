using System;
using MxFramework.Buffs;

namespace MxFramework.Gameplay
{
    /// <summary>Creates a fresh buff with a factory and adds it to the target buff pipeline.</summary>
    public sealed class ApplyBuffEffect : IAbilityEffect
    {
        private readonly Func<IBuff> _buffFactory;

        public ApplyBuffEffect(Func<IBuff> buffFactory)
        {
            _buffFactory = buffFactory ?? throw new ArgumentNullException(nameof(buffFactory));
        }

        public void Apply(AbilityContext context, IRuntimeEntity target)
        {
            IBuff buff = _buffFactory();
            target.BuffPipeline.AddBuff(buff, target);
        }
    }
}
