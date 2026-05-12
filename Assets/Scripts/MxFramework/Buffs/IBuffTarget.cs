using MxFramework.Attributes;
using MxFramework.Events;

namespace MxFramework.Buffs
{
    public interface IBuffTarget
    {
        IAttributeOwner Attributes { get; }
        IAttributeModifierOwner AttributeModifiers { get; }
        IEventBus<BuffEvent> BuffEvents { get; }
    }
}
