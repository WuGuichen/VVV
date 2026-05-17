namespace MxFramework.AI
{
    public static class RuntimeAiPressureFactKeys
    {
        public static readonly AiFactKey SelfPostureBand = new AiFactKey("self.posture.band");
        public static readonly AiFactKey SelfPostureRatio = new AiFactKey("self.posture.ratio");
        public static readonly AiFactKey SelfPostureBroken = new AiFactKey("self.posture.broken");
        public static readonly AiFactKey TargetPostureBand = new AiFactKey("target.posture.band");
        public static readonly AiFactKey TargetPostureRatio = new AiFactKey("target.posture.ratio");
        public static readonly AiFactKey TargetPostureBroken = new AiFactKey("target.posture.broken");

        public static readonly AiFactKey SelfGuardBand = new AiFactKey("self.guard.band");
        public static readonly AiFactKey SelfGuardRatio = new AiFactKey("self.guard.ratio");
        public static readonly AiFactKey SelfGuardBroken = new AiFactKey("self.guard.broken");
        public static readonly AiFactKey TargetGuardBand = new AiFactKey("target.guard.band");
        public static readonly AiFactKey TargetGuardRatio = new AiFactKey("target.guard.ratio");
        public static readonly AiFactKey TargetGuardBroken = new AiFactKey("target.guard.broken");

        public static readonly AiFactKey SelfArmorRatio = new AiFactKey("self.armor.ratio");
        public static readonly AiFactKey SelfArmorBroken = new AiFactKey("self.armor.broken");
        public static readonly AiFactKey TargetArmorRatio = new AiFactKey("target.armor.ratio");
        public static readonly AiFactKey TargetArmorBroken = new AiFactKey("target.armor.broken");

        public static readonly AiFactKey TargetPostureWeaknessExploited =
            new AiFactKey("target.posture.weakness.exploited");
    }
}
