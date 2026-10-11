using MxFramework.Attributes;
using MxFramework.Events;
using MxFramework.Modifiers;

namespace MxFramework.Gameplay
{
    /// <summary>Default pure C# gameplay entity implementation for attributes, modifiers, and ability events.</summary>
    public sealed class RuntimeEntity : IRuntimeEntity
    {
        private readonly AttributeStore _attributeStore;
        private readonly ModifierPipeline _modifierPipeline;
        private readonly EventBus<AbilityEvent> _abilityEvents = new EventBus<AbilityEvent>();
        private readonly CounterStore _counters = new CounterStore();
        private readonly int _hpAttributeId;

        public RuntimeEntity(int entityId, int teamId, int hpAttributeId, int initialCapacity = 8)
        {
            EntityId = entityId;
            TeamId = teamId;
            _hpAttributeId = hpAttributeId;
            _attributeStore = new AttributeStore(initialCapacity);
            _modifierPipeline = new ModifierPipeline(_attributeStore, counters: _counters, initialCapacity: initialCapacity);
        }

        public int EntityId { get; }
        public int TeamId { get; }

        public bool IsAlive
        {
            get
            {
                _attributeStore.TryGetAttribute(_hpAttributeId, out int hp);
                return hp > 0;
            }
        }

        public AttributeStore AttributeStore => _attributeStore;
        public ModifierPipeline ModifierPipeline => _modifierPipeline;
        public IEventBus<AbilityEvent> AbilityEvents => _abilityEvents;

        public AttributeStore Store => _attributeStore;
        public ModifierPipeline Modifiers => _modifierPipeline;
        public IAttributeOwner Attributes => _attributeStore;
    }
}
