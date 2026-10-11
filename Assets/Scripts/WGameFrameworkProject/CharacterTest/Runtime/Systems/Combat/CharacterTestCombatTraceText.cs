namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestCombatTraceText
    {
        public static string EmptyAsNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }

        public static string EmptyAsNone(MxFramework.CharacterAction.CharacterActionId value)
        {
            return value.IsValid ? value.ToString() : "none";
        }

        public static string EmptyAsNone(int value)
        {
            return value > 0 ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none";
        }
    }
}
