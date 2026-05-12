using System.Collections.Generic;

namespace MxFramework.AI
{
    public interface IAiAction
    {
        int Id { get; }
        float Cost { get; }
        IReadOnlyList<IAiCondition> Preconditions { get; }
        IReadOnlyList<IAiEffect> Effects { get; }
        bool CanExecute(IAiWorldState worldState);
        void Apply(IAiWorldState worldState);
    }
}
