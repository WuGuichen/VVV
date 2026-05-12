using System;
using System.Collections.Generic;

namespace MxFramework.AI
{
    public sealed class AiAction : IAiAction
    {
        private readonly List<IAiCondition> _preconditions;
        private readonly List<IAiEffect> _effects;

        public AiAction(
            int id,
            float cost,
            IEnumerable<IAiCondition> preconditions = null,
            IEnumerable<IAiEffect> effects = null)
        {
            Id = id;
            Cost = cost;
            _preconditions = preconditions != null ? new List<IAiCondition>(preconditions) : new List<IAiCondition>();
            _effects = effects != null ? new List<IAiEffect>(effects) : new List<IAiEffect>();
        }

        public int Id { get; }
        public float Cost { get; }
        public IReadOnlyList<IAiCondition> Preconditions => _preconditions;
        public IReadOnlyList<IAiEffect> Effects => _effects;

        public bool CanExecute(IAiWorldState worldState)
        {
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));

            for (int i = 0; i < _preconditions.Count; i++)
            {
                if (!_preconditions[i].IsSatisfied(worldState))
                    return false;
            }

            return true;
        }

        public void Apply(IAiWorldState worldState)
        {
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));

            for (int i = 0; i < _effects.Count; i++)
                _effects[i].Apply(worldState);
        }
    }
}
