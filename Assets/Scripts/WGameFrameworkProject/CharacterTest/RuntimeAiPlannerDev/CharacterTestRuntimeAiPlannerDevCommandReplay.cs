using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestRuntimeAiPlannerDevCommandReplay
    {
        public static bool ShouldReplayCommand(
            RuntimeCommand command,
            bool localInputSuppressed = false)
        {
            if (command.CommandId == CharacterTestRuntimeCommandIds.SetControlIntent)
            {
                switch (command.SourceId)
                {
                    case CharacterTestRuntimeCommandSources.PlayerInput:
                        return localInputSuppressed;
                    case CharacterTestRuntimeCommandSources.RuntimeAiPlanner:
                        return false;
                }
            }

            switch (command.SourceId)
            {
                // A clean session rebuild queues bootstrap/system commands again.
                // Reinjecting their historical copies would duplicate spawns and setup.
                case CharacterTestRuntimeCommandSources.System:
                case CharacterTestRuntimeCommandSources.RuntimeAiPlanner:
                    return false;
                default:
                    return true;
            }
        }
    }
}
