using MxFramework.Events;

namespace MxFramework.Attributes
{
    public interface IAttributeOwner
    {
        int GetAttribute(int attributeId);
        bool TryGetAttribute(int attributeId, out int finalValue);
        bool TryGetAttributeValue(int attributeId, out AttributeValue value);
        void RegisterAttribute(int attributeId, int initialValue);
        void SetAttribute(int attributeId, int baseValue, object source = null);
        void AddAttribute(int attributeId, int delta, object source = null);

        IEventBus<AttributeChangedEvent> OnAttributeChanged { get; }
        IEventBus<AttributeModifierEvent> OnModifierChanged { get; }
    }
}
