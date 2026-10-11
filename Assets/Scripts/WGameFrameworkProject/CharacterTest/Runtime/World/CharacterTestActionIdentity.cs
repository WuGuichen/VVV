namespace MxFramework.Game.CharacterTest
{
    // Wire identities: append explicitly numbered values; never renumber existing entries.
    public enum CharacterTestActionStateId
    {
        None = 0,
        Running = 1,
        Finished = 2,
        Cancelled = 3,
        Interrupted = 4,
        Completed = 5,
        Active = 6,
    }

    public enum CharacterTestActionPhaseId
    {
        None = 0,
        Startup = 1,
        Active = 2,
        Recovery = 3,
        Loop = 4,
        Airborne = 5,
        Landing = 6,
        Channel = 7,
        Hold = 8,
        Exit = 9,
        Custom = 100,
        Death = 101,
        Action = 102,
        Reaction = 103,
    }
}
