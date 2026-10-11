using MxFramework.Editor;
using UnityEditor;

namespace MxFramework.Game.CharacterTest.Editor
{
    [InitializeOnLoad]
    internal static class CharacterTestRuntimeAiPlannerToolRegistration
    {
        static CharacterTestRuntimeAiPlannerToolRegistration()
        {
            FrameworkManagerToolRegistry.Register(new FrameworkManagerToolInfo(
                "character.runtime-ai-planner",
                "Runtime AI Planner",
                "Character",
                "编辑 CharacterTest Runtime AI Planner JSON profile，并只读监控 Play Mode 决策快照。",
                "菜单",
                CharacterTestRuntimeAiPlannerWindow.MenuPath,
                42,
                CharacterTestRuntimeAiPlannerWindow.Open));
        }
    }
}
