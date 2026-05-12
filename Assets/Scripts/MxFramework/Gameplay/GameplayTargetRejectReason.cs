namespace MxFramework.Gameplay
{
    /// <summary>Stable reason code explaining why a target candidate was rejected.</summary>
    public enum GameplayTargetRejectReason
    {
        None = 0,
        NullCandidate = 1,
        Dead = 2,
        SameTeam = 3,
        DifferentTeam = 4,
        NotCaster = 5,
        MissingRequiredTag = 6,
        BlockedStatus = 7,
        MaxTargetsReached = 8,
        NeutralTeam = 9,
    }
}
