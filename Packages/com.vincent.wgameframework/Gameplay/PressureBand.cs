namespace MxFramework.Gameplay
{
    /// <summary>
    /// Stable pressure state bands used by pressure-style gameplay components.
    /// </summary>
    public enum PressureBand
    {
        Stable = 0,
        Pressed = 1,
        Cracked = 2,
        Critical = 3,
        Broken = 4
    }

    internal static class PressureBandUtility
    {
        public static bool IsDefined(PressureBand band)
        {
            int value = (int)band;
            return IsDefinedValue(value);
        }

        public static bool IsDefinedValue(int band)
        {
            return band >= (int)PressureBand.Stable && band <= (int)PressureBand.Broken;
        }
    }
}
