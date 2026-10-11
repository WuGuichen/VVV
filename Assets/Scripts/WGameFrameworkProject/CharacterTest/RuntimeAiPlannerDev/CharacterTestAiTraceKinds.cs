using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestAiTraceKinds
    {
        internal const int PlannerControl = 2000;
        internal const int AbilitySlot = 2001;
        internal const int CardAbility = 2002;
        internal const int CardAction = 2003;
        internal const int RecipeOrchestration = 2004;
        internal const int CardControl = 2005;
        internal const int ExecutionControl = 2006;
        internal const int ExecutionAction = 2007;

        internal static RuntimeTraceIdentity AtFrame(int kind, int entityId, RuntimeFrame frame) =>
            new RuntimeTraceIdentity(kind, entityId, sequence: frame.Value);
    }
}
