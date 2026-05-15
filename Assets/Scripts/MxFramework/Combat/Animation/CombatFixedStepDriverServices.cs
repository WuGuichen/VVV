using MxFramework.Combat.Core;
using MxFramework.Runtime;

namespace MxFramework.Combat.Animation
{
    internal static class CombatFixedStepDriverServices
    {
        public static CombatFixedStepDriver GetOrCreate(RuntimeHostContext context)
        {
            CombatFixedStepDriver driver;
            if (context.Services.TryGet<CombatFixedStepDriver>(out driver))
            {
                return driver;
            }

            driver = new CombatFixedStepDriver();
            if (context.Services is RuntimeServiceRegistry services)
            {
                services.Register(driver);
            }

            return driver;
        }

        public static CombatFixedStepActionHistory GetOrCreateActionHistory(RuntimeHostContext context)
        {
            CombatFixedStepActionHistory history;
            if (context.Services.TryGet<CombatFixedStepActionHistory>(out history))
            {
                return history;
            }

            history = new CombatFixedStepActionHistory();
            if (context.Services is RuntimeServiceRegistry services)
            {
                services.Register(history);
            }

            return history;
        }
    }
}
