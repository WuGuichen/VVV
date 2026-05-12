using MxFramework.Buffs;
using MxFramework.Events;

namespace MxFramework.Gameplay
{
    /// <summary>Minimal gameplay entity contract combining identity, team, attributes, buffs, and ability events.</summary>
    public interface IRuntimeEntity : IBuffTarget
    {
        int EntityId { get; }
        int TeamId { get; }
        bool IsAlive { get; }
        IBuffPipeline BuffPipeline { get; }
        IEventBus<AbilityEvent> AbilityEvents { get; }
    }
}
