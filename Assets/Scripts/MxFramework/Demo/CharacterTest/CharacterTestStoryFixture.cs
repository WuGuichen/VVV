using MxFramework.Story;

namespace MxFramework.Demo.CharacterTest
{
    /// <summary>
    /// CharacterTest 启动用 Story 图定义；Phase 1 仅入口 beat 输出欢迎台词。
    /// </summary>
    public static class CharacterTestStoryFixture
    {
        public const int GraphId = 450001;
        public const int EntryBeatId = 450101;
        public const int WelcomeLineStepId = 450201;
        public const int WelcomeTextKey = 450301;

        public const string WelcomeMessage = "欢迎进入 Character Test";

        public static StoryGraphDefinition CreateBootstrapGraph()
        {
            return new StoryGraphDefinition(
                GraphId,
                version: 1,
                EntryBeatId,
                new[]
                {
                    new StoryBeatDefinition(
                        EntryBeatId,
                        new[]
                        {
                            new StoryStepDefinition(
                                WelcomeLineStepId,
                                StoryStepKind.Line,
                                textKey: WelcomeTextKey)
                        })
                });
        }

        public static string ResolveText(int textKey)
        {
            return textKey == WelcomeTextKey ? WelcomeMessage : string.Empty;
        }
    }
}
