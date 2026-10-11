namespace MxFramework.Gameplay
{
    /// <summary>Fixed lifecycle stages published by the default ability cast implementation.</summary>
    public enum AbilityEventType
    {
        CastStarted,
        TargetSelected,
        EffectApplied,
        CastFinished,
        CastFailed
    }
}
