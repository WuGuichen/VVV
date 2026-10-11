using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Shared semantic colors for Runtime AI Lab FairyGUI presentation.</summary>
    internal static class CharacterTestFairyGuiRuntimeAiLabSemanticColors
    {
        internal static readonly Color32 PrimaryText = new Color32(228, 239, 244, 255);
        internal static readonly Color32 RuntimeActiveText = new Color32(105, 215, 140, 255);

        internal static Color32 ResolveBehaviorCardTitleColor(
            CharacterTestRuntimeAiLabExecutionDisplayState executionState) =>
            executionState == CharacterTestRuntimeAiLabExecutionDisplayState.RunningNow
                ? RuntimeActiveText
                : PrimaryText;
    }
}
