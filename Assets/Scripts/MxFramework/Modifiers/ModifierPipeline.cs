using System;
using System.Collections.Generic;
using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Events;

namespace MxFramework.Modifiers
{
    /// <summary>
    /// Generic modifier lifecycle extracted from WGame EntryManager.
    /// </summary>
    public sealed class ModifierPipeline : IModifierPipeline
    {
        private readonly List<IModifier> _modifiers;
        private readonly Dictionary<int, IModifier> _modifierMap;
        private readonly IModifierFactory _factory;
        private readonly EventBus<ModifierEvent> _events;

        public ModifierPipeline(
            IAttributeOwner owner,
            IModifierFactory factory = null,
            IBuffPipeline buffs = null,
            ICounterStore counters = null,
            int initialCapacity = 8)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (initialCapacity <= 0)
                initialCapacity = 8;

            Owner = owner;
            Buffs = buffs;
            Counters = counters ?? new CounterStore();
            _factory = factory;
            _modifiers = new List<IModifier>(initialCapacity);
            _modifierMap = new Dictionary<int, IModifier>(initialCapacity);
            _events = new EventBus<ModifierEvent>();
        }

        public IAttributeOwner Owner { get; }
        public IBuffPipeline Buffs { get; }
        public ICounterStore Counters { get; }
        public IEventBus<ModifierEvent> OnModifierEvent => _events;

        public bool TryAddModifier(int modifierId, out IModifier modifier)
        {
            modifier = null;
            if (_factory == null || !_factory.TryCreate(modifierId, out modifier))
                return false;

            AddModifier(modifier);
            return true;
        }

        public void AddModifier(IModifier modifier)
        {
            if (modifier == null)
                throw new ArgumentNullException(nameof(modifier));

            RemoveModifier(modifier.Id);
            _modifiers.Add(modifier);
            _modifierMap[modifier.Id] = modifier;
            _events.Publish(new ModifierEvent(modifier.Id, ModifierEventType.Added, modifier));
        }

        public bool RemoveModifier(int modifierId)
        {
            if (!_modifierMap.TryGetValue(modifierId, out IModifier modifier))
                return false;

            _modifierMap.Remove(modifierId);
            _modifiers.Remove(modifier);

            ModifierContext context = CreateContext(null);
            try
            {
                modifier.Remove(context);
            }
            finally
            {
                ModifierContext.Push(context);
            }

            _events.Publish(new ModifierEvent(modifierId, ModifierEventType.Removed, modifier));
            return true;
        }

        public void RemoveAll()
        {
            if (_modifiers.Count == 0)
                return;

            var ids = new int[_modifiers.Count];
            for (int i = 0; i < _modifiers.Count; i++)
                ids[i] = _modifiers[i].Id;

            for (int i = 0; i < ids.Length; i++)
                RemoveModifier(ids[i]);
        }

        public void ApplyAll(ModifierContext context)
        {
            ModifierContext resolved = PrepareContext(context);
            bool ownsContext = context == null;
            try
            {
                for (int i = 0; i < _modifiers.Count; i++)
                {
                    IModifier modifier = _modifiers[i];
                    modifier.Apply(resolved);
                    _events.Publish(new ModifierEvent(modifier.Id, ModifierEventType.Applied, modifier));
                }
            }
            finally
            {
                if (ownsContext)
                    ModifierContext.Push(resolved);
            }
        }

        public void UpdateAll(float deltaTime, ModifierContext context = null)
        {
            ModifierContext resolved = PrepareContext(context);
            bool ownsContext = context == null;
            try
            {
                for (int i = 0; i < _modifiers.Count; i++)
                {
                    IModifier modifier = _modifiers[i];
                    modifier.Update(deltaTime, resolved);
                    _events.Publish(new ModifierEvent(modifier.Id, ModifierEventType.Updated, modifier));
                }
            }
            finally
            {
                if (ownsContext)
                    ModifierContext.Push(resolved);
            }
        }

        public IModifier GetModifier(int modifierId)
        {
            return _modifierMap.TryGetValue(modifierId, out IModifier modifier) ? modifier : null;
        }

        public bool TryGetModifier(int modifierId, out IModifier modifier)
        {
            return _modifierMap.TryGetValue(modifierId, out modifier);
        }

        public bool HasModifier(int modifierId)
        {
            return _modifierMap.ContainsKey(modifierId);
        }

        public ModifierSnapshot[] CreateSnapshot()
        {
            var snapshots = new ModifierSnapshot[_modifiers.Count];
            for (int i = 0; i < _modifiers.Count; i++)
                snapshots[i] = new ModifierSnapshot(_modifiers[i]);
            return snapshots;
        }

        private ModifierContext PrepareContext(ModifierContext context)
        {
            return context == null ? CreateContext(null) : FillContext(context);
        }

        private ModifierContext CreateContext(object source)
        {
            ModifierContext context = ModifierContext.Get();
            context.Source = source;
            return FillContext(context);
        }

        private ModifierContext FillContext(ModifierContext context)
        {
            if (context.Target == null)
                context.Target = Owner;
            if (context.Buffs == null)
                context.Buffs = Buffs;
            if (context.Counters == null)
                context.Counters = Counters;
            return context;
        }
    }
}
