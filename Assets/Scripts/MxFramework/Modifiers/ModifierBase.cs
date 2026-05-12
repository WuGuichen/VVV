using System;

namespace MxFramework.Modifiers
{
    public class ModifierBase : IModifier
    {
        private readonly IModifierCondition[] _conditions;
        private readonly IModifierEffect[] _effects;

        public ModifierBase(
            int id,
            IModifierCondition[] conditions = null,
            IModifierEffect[] effects = null,
            int paramIndex = 0)
        {
            Id = id;
            ParamIndex = paramIndex;
            _conditions = conditions ?? Array.Empty<IModifierCondition>();
            _effects = effects ?? Array.Empty<IModifierEffect>();
        }

        public int Id { get; }
        public int ParamIndex { get; private set; }

        public void SetParamIndex(int index)
        {
            ParamIndex = index;
        }

        public virtual void Apply(ModifierContext context)
        {
            if (!EvaluateConditions(context))
                return;

            for (int i = 0; i < _effects.Length; i++)
                _effects[i].Execute(context);
        }

        public virtual void Update(float deltaTime, ModifierContext context)
        {
        }

        public virtual void Remove(ModifierContext context)
        {
        }

        protected bool EvaluateConditions(ModifierContext context)
        {
            for (int i = 0; i < _conditions.Length; i++)
            {
                if (!_conditions[i].Evaluate(context))
                    return false;
            }

            return true;
        }
    }
}
