namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestCharacterTransformComponent
    {
        public CharacterTestCharacterTransformComponent(float x, float y, float z, float yawDegrees = 0f)
        {
            XMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(x);
            YMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(y);
            ZMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(z);
            YawMilliDegrees = CharacterTestDeterministicUnits.DegreesToMilliDegrees(yawDegrees);
        }

        private CharacterTestCharacterTransformComponent(
            int xMillimeters,
            int yMillimeters,
            int zMillimeters,
            int yawMilliDegrees)
        {
            XMillimeters = xMillimeters;
            YMillimeters = yMillimeters;
            ZMillimeters = zMillimeters;
            YawMilliDegrees = CharacterTestDeterministicUnits.NormalizeMilliDegrees(yawMilliDegrees);
        }

        public int XMillimeters { get; }
        public int YMillimeters { get; }
        public int ZMillimeters { get; }
        public int YawMilliDegrees { get; }

        public float X => CharacterTestDeterministicUnits.MillimetersToMeters(XMillimeters);
        public float Y => CharacterTestDeterministicUnits.MillimetersToMeters(YMillimeters);
        public float Z => CharacterTestDeterministicUnits.MillimetersToMeters(ZMillimeters);
        public float YawDegrees => CharacterTestDeterministicUnits.MilliDegreesToDegrees(YawMilliDegrees);

        public static CharacterTestCharacterTransformComponent FromDeterministic(
            int xMillimeters,
            int yMillimeters,
            int zMillimeters,
            int yawMilliDegrees = 0)
        {
            return new CharacterTestCharacterTransformComponent(
                xMillimeters,
                yMillimeters,
                zMillimeters,
                yawMilliDegrees);
        }
    }
}
