using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestStoryText
    {
        public const string WelcomeLine = "欢迎进入 Character Test";
        public const string LevelStartRegionLine =
            "已进入关卡起始区域。";
        public const string TrainingCompleteLine =
            "训练目标已完成。";

        public static IReadOnlyDictionary<int, string> CreateTextMap()
        {
            return new Dictionary<int, string>
            {
                { CharacterTestStoryIds.Text.WelcomeLine, WelcomeLine },
                {
                    CharacterTestStoryIds.Text.LevelStartRegionLine,
                    LevelStartRegionLine
                },
                {
                    CharacterTestStoryIds.Text.TrainingCompleteLine,
                    TrainingCompleteLine
                }
            };
        }

    }
}
