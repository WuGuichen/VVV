using MxFramework.Combat.Core;
using MxFramework.Gameplay;

namespace MxFramework.Demo.CombatAnimation
{
    public static class CombatAnimationDemoIds
    {
        public const int LightAttackActionId = 1;
        public const int HeavyAttackActionId = 2;
        public const int DodgeRollActionId = 3;
        public const int HurtboxLayer = 1;
        public const int HpAttributeId = 100;
        public const int PlayerMaxHp = 100;
        public const int DummyMaxHp = 100;
        public const int PlayerTeamId = 1;
        public const int DummyTeamId = 2;
        public const int PlayerDefinitionId = 1001;
        public const int DummyDefinitionId = 1002;

        public static readonly CombatEntityId PlayerEntityId = new CombatEntityId(1);
        public static readonly CombatEntityId DummyEntityId = new CombatEntityId(2);
        public static readonly CombatBodyId PlayerBodyId = new CombatBodyId(1);
        public static readonly CombatBodyId DummyBodyId = new CombatBodyId(2);
        public static readonly CombatColliderId HurtboxColliderId = new CombatColliderId(1);
        public static readonly GameplayEntityId PlayerGameplayEntityId = new GameplayEntityId(1, 1);
        public static readonly GameplayEntityId DummyGameplayEntityId = new GameplayEntityId(2, 1);
    }
}
