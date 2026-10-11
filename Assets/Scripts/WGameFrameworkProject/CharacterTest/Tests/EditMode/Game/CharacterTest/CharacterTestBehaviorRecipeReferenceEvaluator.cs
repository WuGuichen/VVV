using MxFramework.Game.CharacterTest;

namespace MxFramework.Tests.Game.CharacterTest
{
    internal static class CharacterTestBehaviorRecipeReferenceCompilation
    {
        internal static CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult Compile(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references = null)
            => payload != null && payload.Timelines.Count > 0
                ? CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(payload, references, executionOnly: false)
                : CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload, references);
    }
    // Historical one-owner oracle only. Production timeline bindings use ExecutionProgram and the new evaluator.
    internal sealed class CharacterTestBehaviorRecipeReferenceEvaluator
    {
        private readonly CharacterTestRuntimeAiLabBehaviorRecipeV6Evaluator _recipe;
        private readonly CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator _legacy;
        public CharacterTestBehaviorRecipeReferenceEvaluator(CharacterTestRuntimeAiLabCompiledLogicProgram program, ICharacterTestRuntimeAiLabBehaviorCardLogicRuntime runtime)
        {
            if (program.RecipeV6 != null) _recipe = new CharacterTestRuntimeAiLabBehaviorRecipeV6Evaluator(program.RecipeV6, runtime);
            else _legacy = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(program, runtime);
        }
        public CharacterTestRuntimeAiLabLogicRuntimeState State => _recipe?.State ?? _legacy.State;
        public int CurrentStageIndex => _recipe?.CurrentTimelineIndex ?? _legacy.CurrentStageIndex;
        public int CurrentTimelineIndex => _recipe?.CurrentTimelineIndex ?? _legacy.CurrentTimelineIndex;
        public int LocalFrame => _recipe?.LocalFrame ?? _legacy.LocalFrame;
        public long OwnedPrimaryActionInstanceId => _recipe?.OwnedPrimaryActionInstanceId ?? _legacy.OwnedPrimaryActionInstanceId;
        public string LastTriggeredEventId => _recipe?.LastTriggeredEventId ?? _legacy.LastTriggeredEventId;
        public bool Start(int frame, CharacterTestRuntimeAiLabLogicEventBuffer events) => _recipe != null ? _recipe.Start(frame, events) : _legacy.Start(frame, events);
        public bool StartCommitted(int frame, CharacterTestRuntimeAiLabLogicEventBuffer events) => _recipe != null ? _recipe.StartCommitted(frame, events) : _legacy.StartCommitted(frame, events);
        public void Tick(int frame, CharacterTestRuntimeAiLabLogicEventBuffer events) { if (_recipe != null) _recipe.Tick(frame, events); else _legacy.Tick(frame, events); }
        public void Reset() { if (_recipe != null) _recipe.Reset(); else _legacy.Reset(); }
        public CharacterTestRuntimeAiLabLogicEvaluatorSnapshot CreateSnapshot() => _recipe?.CreateSnapshot() ?? _legacy.CreateSnapshot();
        public bool TryRestoreSnapshot(CharacterTestRuntimeAiLabLogicEvaluatorSnapshot source) => _recipe != null ? _recipe.TryRestoreSnapshot(source) : _legacy.TryRestoreSnapshot(source);
    }

    public sealed partial class CharacterTestRuntimeAiLabBehaviorRecipeV5Tests
    {
        private static CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult CompileReference(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload source, CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references = null)
            => source != null && source.Timelines.Count > 0
                ? CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.Compile(source, references, executionOnly: false)
                : CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(source, references);
    }
}
