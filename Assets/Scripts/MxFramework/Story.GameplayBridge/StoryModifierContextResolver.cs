using System.Collections.Generic;
using MxFramework.Modifiers;

namespace MxFramework.Story.GameplayBridge
{
    public sealed class StoryModifierContextResolver : IStoryModifierContextResolver
    {
        private readonly Dictionary<StoryGameplayEntityRef, StoryModifierContextData> _contexts =
            new Dictionary<StoryGameplayEntityRef, StoryModifierContextData>();

        public int Count => _contexts.Count;

        public void Register(StoryGameplayEntityRef entityRef, StoryModifierContextData data)
        {
            _contexts[entityRef] = data;
        }

        public bool Remove(StoryGameplayEntityRef entityRef)
        {
            return _contexts.Remove(entityRef);
        }

        public void Clear()
        {
            _contexts.Clear();
        }

        public bool TryCreateContext(
            in StoryEvaluationContext storyContext,
            out ModifierContext modifierContext,
            out StoryGameplayBridgeDiagnostic diagnostic)
        {
            modifierContext = null;

            if (!_contexts.TryGetValue(storyContext.TargetRef, out StoryModifierContextData data))
            {
                diagnostic = new StoryGameplayBridgeDiagnostic(
                    StoryGameplayBridgeDiagnosticCode.ConditionResolverFailed,
                    "No modifier context data is registered for the Story target ref.",
                    storyContext.TargetRef);
                return false;
            }

            if (data.Target == null)
            {
                diagnostic = new StoryGameplayBridgeDiagnostic(
                    StoryGameplayBridgeDiagnosticCode.MissingModifierContextTarget,
                    "Resolved modifier context data has no attribute target.",
                    storyContext.TargetRef);
                return false;
            }

            ModifierContext context = ModifierContext.Get();
            context.Target = data.Target;
            context.Buffs = data.Buffs;
            context.Counters = data.Counters;
            context.Parameters = data.Parameters;
            context.CompareId = data.CompareId;
            context.CompareValue1 = data.CompareValue1;
            context.CompareValue2 = data.CompareValue2;
            context.Source = data.Source ?? storyContext.Source;
            modifierContext = context;
            diagnostic = StoryGameplayBridgeDiagnostic.None;
            return true;
        }
    }
}
