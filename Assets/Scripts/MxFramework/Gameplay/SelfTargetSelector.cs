using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Selects the caster itself as the only target.</summary>
    public sealed class SelfTargetSelector : ITargetSelector
    {
        private static readonly IReadOnlyList<IRuntimeEntity> Empty = new IRuntimeEntity[0];

        public IReadOnlyList<IRuntimeEntity> SelectTargets(AbilityContext context)
        {
            return context.Caster == null ? Empty : new IRuntimeEntity[] { context.Caster };
        }
    }
}
