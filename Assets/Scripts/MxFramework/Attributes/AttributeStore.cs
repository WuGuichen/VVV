using System.Collections.Generic;
using MxFramework.Events;

namespace MxFramework.Attributes
{
    /// <summary>
    /// Generic attribute storage extracted from WGame's WAttribute core.
    /// </summary>
    public sealed class AttributeStore : IAttributeOwner, IAttributeModifierOwner
    {
        private readonly Dictionary<int, AttributeValue> _attributes;
        private readonly List<IAttributeModifier> _modifiers;
        private readonly EventBus<AttributeChangedEvent> _attributeChanged;
        private readonly EventBus<AttributeModifierEvent> _modifierChanged;

        public AttributeStore(int initialAttributeCapacity = 16, int initialModifierCapacity = 8)
        {
            if (initialAttributeCapacity <= 0)
                initialAttributeCapacity = 16;
            if (initialModifierCapacity <= 0)
                initialModifierCapacity = 8;

            _attributes = new Dictionary<int, AttributeValue>(initialAttributeCapacity);
            _modifiers = new List<IAttributeModifier>(initialModifierCapacity);
            _attributeChanged = new EventBus<AttributeChangedEvent>();
            _modifierChanged = new EventBus<AttributeModifierEvent>();
        }

        public IEventBus<AttributeChangedEvent> OnAttributeChanged => _attributeChanged;

        public IEventBus<AttributeModifierEvent> OnModifierChanged => _modifierChanged;

        public int GetAttribute(int attributeId)
        {
            return _attributes.TryGetValue(attributeId, out AttributeValue value) ? value.FinalValue : 0;
        }

        public bool TryGetAttribute(int attributeId, out int finalValue)
        {
            if (_attributes.TryGetValue(attributeId, out AttributeValue value))
            {
                finalValue = value.FinalValue;
                return true;
            }

            finalValue = 0;
            return false;
        }

        public bool TryGetAttributeValue(int attributeId, out AttributeValue value)
        {
            return _attributes.TryGetValue(attributeId, out value);
        }

        public void RegisterAttribute(int attributeId, int initialValue)
        {
            if (_attributes.ContainsKey(attributeId))
                return;

            int finalValue = ComputeFinalValue(attributeId, initialValue);
            _attributes.Add(attributeId, new AttributeValue(initialValue, finalValue));
        }

        public void SetAttribute(int attributeId, int baseValue, object source = null)
        {
            if (!_attributes.TryGetValue(attributeId, out AttributeValue oldValue))
            {
                int finalValue = ComputeFinalValue(attributeId, baseValue);
                _attributes.Add(attributeId, new AttributeValue(baseValue, finalValue));
                PublishAttributeChanged(attributeId, baseValue, 0, finalValue, source);
                return;
            }

            int newFinalValue = ComputeFinalValue(attributeId, baseValue);
            if (oldValue.BaseValue == baseValue && oldValue.FinalValue == newFinalValue)
                return;

            _attributes[attributeId] = new AttributeValue(baseValue, newFinalValue);
            if (oldValue.FinalValue != newFinalValue)
                PublishAttributeChanged(attributeId, baseValue, oldValue.FinalValue, newFinalValue, source);
        }

        public void AddAttribute(int attributeId, int delta, object source = null)
        {
            if (delta == 0)
                return;

            int baseValue = _attributes.TryGetValue(attributeId, out AttributeValue oldValue)
                ? oldValue.BaseValue + delta
                : delta;
            SetAttribute(attributeId, baseValue, source);
        }

        public void AddModifier(IAttributeModifier modifier)
        {
            if (modifier == null)
                throw new System.ArgumentNullException(nameof(modifier));

            RemoveModifierInternal(modifier.Id, publishEvent: false);

            int index = FindInsertIndex(modifier);
            _modifiers.Insert(index, modifier);
            RecomputeAttribute(modifier.AttributeId, source: modifier);
            _modifierChanged.Publish(new AttributeModifierEvent(modifier.Id, modifier.AttributeId, isAdded: true));
        }

        public bool RemoveModifier(int modifierId)
        {
            return RemoveModifierInternal(modifierId, publishEvent: true);
        }

        public void ClearModifiers()
        {
            if (_modifiers.Count == 0)
                return;

            var affected = new List<int>(_modifiers.Count);
            for (int i = 0; i < _modifiers.Count; i++)
                AddUnique(affected, _modifiers[i].AttributeId);

            _modifiers.Clear();

            for (int i = 0; i < affected.Count; i++)
                RecomputeAttribute(affected[i], source: null);
        }

        private bool RemoveModifierInternal(int modifierId, bool publishEvent)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                IAttributeModifier modifier = _modifiers[i];
                if (modifier.Id != modifierId)
                    continue;

                _modifiers.RemoveAt(i);
                RecomputeAttribute(modifier.AttributeId, source: modifier);
                if (publishEvent)
                    _modifierChanged.Publish(new AttributeModifierEvent(modifier.Id, modifier.AttributeId, isAdded: false));
                return true;
            }

            return false;
        }

        private void RecomputeAttribute(int attributeId, object source)
        {
            if (!_attributes.TryGetValue(attributeId, out AttributeValue oldValue))
                return;

            int newFinalValue = ComputeFinalValue(attributeId, oldValue.BaseValue);
            if (oldValue.FinalValue == newFinalValue)
                return;

            _attributes[attributeId] = new AttributeValue(oldValue.BaseValue, newFinalValue);
            PublishAttributeChanged(attributeId, oldValue.BaseValue, oldValue.FinalValue, newFinalValue, source);
        }

        private int ComputeFinalValue(int attributeId, int baseValue)
        {
            int value = baseValue;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                IAttributeModifier modifier = _modifiers[i];
                if (modifier.AttributeId == attributeId)
                    value = modifier.Modify(value, this);
            }

            return value;
        }

        private int FindInsertIndex(IAttributeModifier modifier)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                IAttributeModifier current = _modifiers[i];
                if (modifier.Phase < current.Phase)
                    return i;
                if (modifier.Phase == current.Phase && modifier.Priority < current.Priority)
                    return i;
                if (modifier.Phase == current.Phase && modifier.Priority == current.Priority && modifier.Id < current.Id)
                    return i;
            }

            return _modifiers.Count;
        }

        private void PublishAttributeChanged(int attributeId, int baseValue, int oldValue, int newValue, object source)
        {
            if (oldValue == newValue)
                return;

            _attributeChanged.Publish(new AttributeChangedEvent(attributeId, baseValue, oldValue, newValue, source));
        }

        private static void AddUnique(List<int> values, int value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return;
            }

            values.Add(value);
        }
    }
}
