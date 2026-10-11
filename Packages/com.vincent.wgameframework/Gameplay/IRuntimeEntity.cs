using MxFramework.Attributes;
using MxFramework.Events;

namespace MxFramework.Gameplay
{
    /// <summary>Minimal gameplay entity contract combining identity, team, attributes, and ability events.</summary>
    public interface IRuntimeEntity
    {
        int EntityId { get; }
        int TeamId { get; }
        bool IsAlive { get; }
        IAttributeOwner Attributes { get; }
        IEventBus<AbilityEvent> AbilityEvents { get; }
    }
}
