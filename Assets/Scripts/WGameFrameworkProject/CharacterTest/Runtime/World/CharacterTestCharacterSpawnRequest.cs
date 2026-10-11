namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestCharacterSpawnRequest
    {
        public CharacterTestCharacterSpawnRequest(
            int characterId,
            int teamId,
            float x,
            float y,
            float z,
            CharacterTestCharacterControlOwner controlOwner,
            int runtimeAiPlannerProfileSlot = 0,
            CharacterTestRuntimeAiBehaviorSelection runtimeAiBehaviorSelection = default)
        {
            CharacterId = characterId;
            TeamId = teamId;
            XMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(x);
            YMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(y);
            ZMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(z);
            ControlOwner = controlOwner;
            RuntimeAiBehaviorSelection = runtimeAiBehaviorSelection;
            RuntimeAiPlannerProfileSlot = runtimeAiBehaviorSelection.HasSelection
                ? runtimeAiBehaviorSelection.ProfileSlot
                : runtimeAiPlannerProfileSlot < 0 ? 0 : runtimeAiPlannerProfileSlot;
        }

        private CharacterTestCharacterSpawnRequest(
            int characterId,
            int teamId,
            int xMillimeters,
            int yMillimeters,
            int zMillimeters,
            CharacterTestCharacterControlOwner controlOwner,
            int runtimeAiPlannerProfileSlot,
            CharacterTestRuntimeAiBehaviorSelection runtimeAiBehaviorSelection)
        {
            CharacterId = characterId;
            TeamId = teamId;
            XMillimeters = xMillimeters;
            YMillimeters = yMillimeters;
            ZMillimeters = zMillimeters;
            ControlOwner = controlOwner;
            RuntimeAiBehaviorSelection = runtimeAiBehaviorSelection;
            RuntimeAiPlannerProfileSlot = runtimeAiBehaviorSelection.HasSelection
                ? runtimeAiBehaviorSelection.ProfileSlot
                : runtimeAiPlannerProfileSlot < 0 ? 0 : runtimeAiPlannerProfileSlot;
        }

        public int CharacterId { get; }
        public int TeamId { get; }
        public int XMillimeters { get; }
        public int YMillimeters { get; }
        public int ZMillimeters { get; }
        public float X => CharacterTestDeterministicUnits.MillimetersToMeters(XMillimeters);
        public float Y => CharacterTestDeterministicUnits.MillimetersToMeters(YMillimeters);
        public float Z => CharacterTestDeterministicUnits.MillimetersToMeters(ZMillimeters);
        public CharacterTestCharacterControlOwner ControlOwner { get; }
        public int RuntimeAiPlannerProfileSlot { get; }
        public CharacterTestRuntimeAiBehaviorSelection RuntimeAiBehaviorSelection { get; }

        public static CharacterTestCharacterSpawnRequest FromDeterministic(
            int characterId,
            int teamId,
            int xMillimeters,
            int yMillimeters,
            int zMillimeters,
            CharacterTestCharacterControlOwner controlOwner,
            int runtimeAiPlannerProfileSlot = 0,
            CharacterTestRuntimeAiBehaviorSelection runtimeAiBehaviorSelection = default)
        {
            return new CharacterTestCharacterSpawnRequest(
                characterId,
                teamId,
                xMillimeters,
                yMillimeters,
                zMillimeters,
                controlOwner,
                runtimeAiPlannerProfileSlot,
                runtimeAiBehaviorSelection);
        }

        public static CharacterTestCharacterSpawnRequest FromSpec(CharacterTestCharacterSpawnSpec spec)
        {
            return new CharacterTestCharacterSpawnRequest(
                spec.CharacterId,
                spec.TeamId,
                spec.X,
                spec.Y,
                spec.Z,
                spec.ControlOwner,
                spec.RuntimeAiPlannerProfileSlot,
                spec.RuntimeAiBehaviorSelection);
        }
    }
}
