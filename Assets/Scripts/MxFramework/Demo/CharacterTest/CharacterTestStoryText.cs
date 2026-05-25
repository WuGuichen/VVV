using System.Collections.Generic;

namespace MxFramework.Demo.CharacterTest
{
    public static class CharacterTestStoryText
    {
        public const string WelcomeLine = "欢迎进入 Character Test";

        public static IReadOnlyDictionary<int, string> CreateTextMap()
        {
            return new Dictionary<int, string>
            {
                { CharacterTestStoryIds.Text.WelcomeLine, WelcomeLine }
            };
        }

        public static bool TryResolve(int textKey, out string text)
        {
            switch (textKey)
            {
                case CharacterTestStoryIds.Text.WelcomeLine:
                    text = WelcomeLine;
                    return true;
                default:
                    text = string.Empty;
                    return false;
            }
        }
    }
}
