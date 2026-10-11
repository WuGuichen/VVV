namespace MxFramework.Gameplay
{
    public static class GameplayScatterPressureEvents
    {
        public static readonly GameplayScatterPressureReason ApplyPressureReason = new GameplayScatterPressureReason(GameplayScatterPressureReason.ApplyPressureCode);
        public static readonly GameplayScatterPressureReason UpdateCohesionLimitReason = new GameplayScatterPressureReason(GameplayScatterPressureReason.UpdateCohesionLimitCode);
        public static readonly GameplayScatterPressureReason RecoveryReason = new GameplayScatterPressureReason(GameplayScatterPressureReason.RecoveryCode);
    }
}
