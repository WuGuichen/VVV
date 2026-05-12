namespace MxFramework.Gameplay
{
    /// <summary>Stable component snapshot entry keyed by generation-based entity id.</summary>
    public readonly struct GameplayComponentSnapshot<T> where T : struct, IGameplayComponent
    {
        public GameplayComponentSnapshot(GameplayEntityId entityId, T component)
        {
            EntityId = entityId;
            Component = component;
        }

        public GameplayEntityId EntityId { get; }
        public T Component { get; }
    }
}
