using System;
using MxFramework.Attributes;
using MxFramework.Buffs;
using MxFramework.Modifiers;
using MxFramework.Runtime;
using MxFramework.Story;

namespace MxFramework.Story.GameplayBridge
{
    public interface IStoryCondition
    {
        bool Evaluate(in StoryEvaluationContext context);
    }

    public readonly struct StoryEvaluationContext
    {
        public StoryEvaluationContext(
            int graphId,
            int beatId,
            int beatInstanceId,
            StoryGameplayEntityRef targetRef,
            IStoryBlackboard blackboard = null,
            RuntimeFrame currentFrame = default,
            object source = null)
        {
            GraphId = graphId;
            BeatId = beatId;
            BeatInstanceId = beatInstanceId;
            TargetRef = targetRef;
            Blackboard = blackboard;
            CurrentFrame = currentFrame;
            Source = source;
        }

        public int GraphId { get; }
        public int BeatId { get; }
        public int BeatInstanceId { get; }
        public StoryGameplayEntityRef TargetRef { get; }
        public IStoryBlackboard Blackboard { get; }
        public RuntimeFrame CurrentFrame { get; }
        public object Source { get; }
    }

    public readonly struct StoryModifierContextData
    {
        public StoryModifierContextData(
            IAttributeOwner target,
            IBuffPipeline buffs = null,
            ICounterStore counters = null,
            int[] parameters = null,
            int compareId = 0,
            int compareValue1 = 0,
            int compareValue2 = 0,
            object source = null)
        {
            Target = target;
            Buffs = buffs;
            Counters = counters;
            Parameters = parameters;
            CompareId = compareId;
            CompareValue1 = compareValue1;
            CompareValue2 = compareValue2;
            Source = source;
        }

        public IAttributeOwner Target { get; }
        public IBuffPipeline Buffs { get; }
        public ICounterStore Counters { get; }
        public int[] Parameters { get; }
        public int CompareId { get; }
        public int CompareValue1 { get; }
        public int CompareValue2 { get; }
        public object Source { get; }
    }

    public interface IStoryModifierContextResolver
    {
        bool TryCreateContext(
            in StoryEvaluationContext storyContext,
            out ModifierContext modifierContext,
            out StoryGameplayBridgeDiagnostic diagnostic);
    }

    public sealed class StoryModifierConditionAdapter : IStoryCondition
    {
        private readonly IModifierCondition _condition;
        private readonly IStoryModifierContextResolver _resolver;
        private readonly StoryGameplayBridgeDiagnostics _diagnostics;

        public StoryModifierConditionAdapter(
            IModifierCondition condition,
            IStoryModifierContextResolver resolver,
            StoryGameplayBridgeDiagnostics diagnostics = null)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _diagnostics = diagnostics;
            LastDiagnostic = StoryGameplayBridgeDiagnostic.None;
        }

        public StoryGameplayBridgeDiagnostic LastDiagnostic { get; private set; }

        public bool Evaluate(in StoryEvaluationContext context)
        {
            if (!_resolver.TryCreateContext(context, out ModifierContext modifierContext, out StoryGameplayBridgeDiagnostic diagnostic))
            {
                LastDiagnostic = diagnostic;
                _diagnostics?.RecordConditionFailure(diagnostic);
                return false;
            }

            try
            {
                bool result = _condition.Evaluate(modifierContext);
                LastDiagnostic = StoryGameplayBridgeDiagnostic.None;
                return result;
            }
            catch (Exception)
            {
                var failure = new StoryGameplayBridgeDiagnostic(
                    StoryGameplayBridgeDiagnosticCode.ConditionEvaluationFailed,
                    "Modifier condition evaluation failed.",
                    context.TargetRef);
                LastDiagnostic = failure;
                _diagnostics?.RecordConditionFailure(failure);
                return false;
            }
            finally
            {
                ModifierContext.Push(modifierContext);
            }
        }
    }
}
