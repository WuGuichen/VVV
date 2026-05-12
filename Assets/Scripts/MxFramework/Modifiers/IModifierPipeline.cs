using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Events;

namespace MxFramework.Modifiers
{
    public interface IModifierPipeline
    {
        IAttributeOwner Owner { get; }
        IBuffPipeline Buffs { get; }
        ICounterStore Counters { get; }
        IEventBus<ModifierEvent> OnModifierEvent { get; }

        bool TryAddModifier(int modifierId, out IModifier modifier);
        void AddModifier(IModifier modifier);
        bool RemoveModifier(int modifierId);
        void RemoveAll();
        void ApplyAll(ModifierContext context);
        void UpdateAll(float deltaTime, ModifierContext context = null);

        IModifier GetModifier(int modifierId);
        bool TryGetModifier(int modifierId, out IModifier modifier);
        bool HasModifier(int modifierId);
        ModifierSnapshot[] CreateSnapshot();
    }
}
