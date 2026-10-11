using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Selects ability targets from the cast context candidates.</summary>
    public interface ITargetSelector
    {
        IReadOnlyList<IRuntimeEntity> SelectTargets(AbilityContext context);
    }
}
