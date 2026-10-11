using System.Collections.Generic;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Animation
{
    public interface ICombatActionTraceProvider
    {
        void GetActiveTraces(
            CombatEntityId entityId,
            int actionId,
            int actionInstanceId,
            int localFrame,
            List<WeaponTraceFrame> results);
    }
}
