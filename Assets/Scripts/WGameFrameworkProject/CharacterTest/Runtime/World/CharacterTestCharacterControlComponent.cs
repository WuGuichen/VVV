namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestCharacterControlComponent
    {
        public CharacterTestCharacterControlComponent(
            CharacterTestCharacterControlOwner owner,
            int runtimeAiPlannerProfileSlot = 0)
            : this(
                owner,
                runtimeAiPlannerProfileSlot,
                CharacterTestRuntimeAiBehaviorSelection.None)
        {
        }

        public CharacterTestCharacterControlComponent(
            CharacterTestCharacterControlOwner owner,
            int runtimeAiPlannerProfileSlot,
            CharacterTestRuntimeAiBehaviorSelection runtimeAiBehaviorSelection)
        {
            Owner = owner;
            RuntimeAiBehaviorSelection = runtimeAiBehaviorSelection;
            RuntimeAiPlannerProfileSlot = runtimeAiBehaviorSelection.HasSelection
                ? runtimeAiBehaviorSelection.ProfileSlot
                : runtimeAiPlannerProfileSlot < 0 ? 0 : runtimeAiPlannerProfileSlot;
        }

        public CharacterTestCharacterControlOwner Owner { get; }
        public int RuntimeAiPlannerProfileSlot { get; }
        public CharacterTestRuntimeAiBehaviorSelection RuntimeAiBehaviorSelection { get; }

        public CharacterTestCharacterControlComponent WithOwner(CharacterTestCharacterControlOwner owner)
        {
            return new CharacterTestCharacterControlComponent(
                owner,
                RuntimeAiPlannerProfileSlot,
                RuntimeAiBehaviorSelection);
        }

        public CharacterTestCharacterControlComponent WithRuntimeAiBehaviorSelection(
            CharacterTestRuntimeAiBehaviorSelection selection)
        {
            return new CharacterTestCharacterControlComponent(
                Owner,
                RuntimeAiPlannerProfileSlot,
                selection);
        }
    }
}
