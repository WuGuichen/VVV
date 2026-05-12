namespace MxFramework.Gameplay
{
    /// <summary>Stable two-component query result keyed by generation-based entity id.</summary>
    public readonly struct GameplayComponentPair<TPrimary, TSecondary>
        where TPrimary : struct, IGameplayComponent
        where TSecondary : struct, IGameplayComponent
    {
        public GameplayComponentPair(GameplayEntityId entityId, TPrimary primary, TSecondary secondary)
        {
            EntityId = entityId;
            Primary = primary;
            Secondary = secondary;
        }

        public GameplayEntityId EntityId { get; }
        public TPrimary Primary { get; }
        public TSecondary Secondary { get; }
    }
}
