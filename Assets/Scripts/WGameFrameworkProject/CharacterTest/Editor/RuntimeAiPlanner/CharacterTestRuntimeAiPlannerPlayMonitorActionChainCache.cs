namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterTestRuntimeAiPlannerPlayMonitorActionChainCache
    {
        private static CharacterTestAbilityHotbarRuntime s_hotbar;
        private static CharacterTestCharacterActionRuntime s_actionRuntime;

        public static string BuildSelectedActionChainText(
            CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem item =
                CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewBuilder.BuildSelectedAction(
                    agent,
                    GetHotbar(),
                    GetActionRuntime());
            string warning = item.HasWarning ? "\nwarning: " + item.WarningText : string.Empty;
            string keyFrames = string.IsNullOrWhiteSpace(item.KeyFramesText)
                ? string.Empty
                : "\nkey frames: " + item.KeyFramesText;
            return item.PlayerSummary + warning + keyFrames;
        }

        public static CharacterTestRuntimeAiPlannerBehaviorActionChainPreview BuildPreview(
            CharacterTestRuntimeAiPlannerProfile profile)
        {
            return CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewBuilder.Build(
                profile,
                GetHotbar(),
                GetActionRuntime());
        }

        private static CharacterTestAbilityHotbarRuntime GetHotbar()
        {
            if (s_hotbar == null)
                s_hotbar = new CharacterTestAbilityHotbarRuntime(CharacterTestAbilityCatalogSource.CreateRegistry());
            return s_hotbar;
        }

        private static CharacterTestCharacterActionRuntime GetActionRuntime()
        {
            if (s_actionRuntime == null)
                s_actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            return s_actionRuntime;
        }
    }
}
