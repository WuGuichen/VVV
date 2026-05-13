using MxFramework.Combat.Core;

namespace MxFramework.Demo.CombatAnimation
{
    public static class CombatAnimationDemoIds
    {
        public const int LightAttackActionId = 1;
        public const int HeavyAttackActionId = 2;
        public const int DodgeRollActionId = 3;
        public const int HurtboxLayer = 1;

        public static readonly CombatEntityId PlayerEntityId = new CombatEntityId(1);
        public static readonly CombatEntityId DummyEntityId = new CombatEntityId(2);
        public static readonly CombatBodyId PlayerBodyId = new CombatBodyId(1);
        public static readonly CombatBodyId DummyBodyId = new CombatBodyId(2);
        public static readonly CombatColliderId HurtboxColliderId = new CombatColliderId(1);
    }
}
