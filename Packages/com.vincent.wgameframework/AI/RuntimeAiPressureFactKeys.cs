namespace MxFramework.AI
{
    public static class RuntimeAiPressureFactKeys
    {
        public static readonly AiFactKey SelfScatterPressureBand = new AiFactKey("self.scatter_pressure.band");
        public static readonly AiFactKey SelfScatterPressureRatio = new AiFactKey("self.scatter_pressure.ratio");
        public static readonly AiFactKey SelfScatterPressureBroken = new AiFactKey("self.scatter_pressure.broken");
        public static readonly AiFactKey TargetScatterPressureBand = new AiFactKey("target.scatter_pressure.band");
        public static readonly AiFactKey TargetScatterPressureRatio = new AiFactKey("target.scatter_pressure.ratio");
        public static readonly AiFactKey TargetScatterPressureBroken = new AiFactKey("target.scatter_pressure.broken");

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

        public static readonly AiFactKey TargetScatterPressureWeaknessExploited =
            new AiFactKey("target.scatter_pressure.weakness.exploited");
    }
}
