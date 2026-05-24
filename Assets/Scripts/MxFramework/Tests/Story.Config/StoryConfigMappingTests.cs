using MxFramework.Story;
using MxFramework.Story.Config;
using NUnit.Framework;

namespace MxFramework.Tests.StoryConfig
{
    public sealed class StoryConfigMappingTests
    {
        private const int GraphId = 1001;
        private const int EntryBeatId = 2001;
        private const int ChoiceBeatId = 2002;
        private const int EndBeatId = 2003;
        private const int EntryLineStepId = 4001;
        private const int EntrySetFactStepId = 4002;
        private const int ChoiceLineStepId = 4003;
        private const int EndStepId = 4004;
        private const int GlobalConditionFactId = 7001;
        private const int GraphFactId = 7002;

        [Test]
        public void Map_ValidRows_CreatesStoryGraphWithDeterministicOrder()
        {
            StoryConfigSet configSet = CreateValidConfigSet();
            StoryConfigReferenceIndex references = CreateTextReferences();

            StoryGraphConfigMappingResult result = StoryGraphConfigMapper.Map(configSet, GraphId, references);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.DiagnosticCount);
            Assert.IsNotNull(result.Definition);
            Assert.AreEqual(GraphId, result.Definition.GraphId);
            Assert.AreEqual(EntryBeatId, result.Definition.EntryBeatId);
            CollectionAssert.AreEqual(new[] { EntryBeatId, ChoiceBeatId, EndBeatId }, BeatIds(result.Definition.Beats));

            StoryBeatDefinition entry = result.Definition.Beats[0];
            CollectionAssert.AreEqual(new[] { 301, 302 }, entry.TriggerIds);
            CollectionAssert.AreEqual(new[] { EntryLineStepId, EntrySetFactStepId }, StepIds(entry.Steps));
            CollectionAssert.AreEqual(new[] { 2, 3 }, BranchIds(entry.Branches));
            Assert.AreEqual(GraphId, entry.Steps[1].FactKey.Namespace);
            Assert.AreEqual(GraphFactId, entry.Steps[1].FactKey.Id);
            Assert.AreEqual(StoryValue.FromInt32(7), entry.Steps[1].FactValue);

            StoryBeatDefinition choiceBeat = result.Definition.Beats[1];
            CollectionAssert.AreEqual(new[] { 6001, 6002 }, ChoiceIds(choiceBeat.Choices));
            CollectionAssert.AreEqual(new[] { 11, 12 }, choiceBeat.Choices[0].EffectIds);

            var director = new StoryDirector();
            Assert.IsTrue(director.LoadGraph(result.Definition));
        }

        [Test]
        public void Validate_MissingEntryBeat_ReportsDiagnostic()
        {
            StoryConfigSet configSet = CreateValidConfigSet(
                graphs: new[] { new StoryGraphConfig(GraphId, entryBeatId: 9999, sourcePath: "story://missing-entry") });

            StoryConfigValidationResult result = StoryConfigValidator.Validate(configSet, GraphId, CreateTextReferences());

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Contains(StoryConfigValidationDiagnosticCode.MissingEntryBeat));
        }

        [Test]
        public void Validate_InvalidBranchTarget_ReportsDiagnostic()
        {
            StoryConfigSet configSet = CreateValidConfigSet(
                branches: new[]
                {
                    new StoryBranchConfig(1, GraphId, EntryBeatId, targetBeatId: 9999, isFallback: true)
                });

            StoryConfigValidationResult result = StoryConfigValidator.Validate(configSet, GraphId, CreateTextReferences());

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Contains(StoryConfigValidationDiagnosticCode.InvalidBranchTarget));
        }

        [Test]
        public void Validate_DuplicateStableIds_ReportsDiagnostic()
        {
            StoryConfigSet configSet = CreateValidConfigSet(
                beats: new[]
                {
                    new StoryBeatConfig(EntryBeatId, GraphId, sortOrder: 10),
                    new StoryBeatConfig(EntryBeatId, GraphId, sortOrder: 20)
                });

            StoryConfigValidationResult result = StoryConfigValidator.Validate(configSet, GraphId, CreateTextReferences());

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Contains(StoryConfigValidationDiagnosticCode.DuplicateStableId));
        }

        [Test]
        public void Validate_UnsupportedStepKind_ReportsDiagnostic()
        {
            StoryConfigSet configSet = CreateValidConfigSet(
                steps: new[]
                {
                    new StoryStepConfig(
                        4999,
                        GraphId,
                        EntryBeatId,
                        (StoryStepKind)99,
                        textKey: 9001)
                });

            StoryConfigValidationResult result = StoryConfigValidator.Validate(configSet, GraphId, CreateTextReferences());

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Contains(StoryConfigValidationDiagnosticCode.UnsupportedStepKind));
        }

        [Test]
        public void Validate_InvalidTextAndFactReferences_ReportDiagnostics()
        {
            StoryConfigSet configSet = CreateValidConfigSet(
                steps: new[]
                {
                    new StoryStepConfig(
                        EntryLineStepId,
                        GraphId,
                        EntryBeatId,
                        StoryStepKind.Line,
                        textKey: 9999),
                    new StoryStepConfig(
                        EntrySetFactStepId,
                        GraphId,
                        EntryBeatId,
                        StoryStepKind.SetFact,
                        sortOrder: 10,
                        factNamespace: GraphId,
                        factId: 9998,
                        factValueKind: StoryValueKind.Int32,
                        factValueRaw: 7L)
                });

            StoryConfigValidationResult result = StoryConfigValidator.Validate(configSet, GraphId, CreateTextReferences());

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Contains(StoryConfigValidationDiagnosticCode.InvalidTextReference));
            Assert.IsTrue(result.Contains(StoryConfigValidationDiagnosticCode.InvalidFactReference));
        }

        private static StoryConfigReferenceIndex CreateTextReferences()
        {
            return new StoryConfigReferenceIndex()
                .AddTextKey(9001)
                .AddTextKey(9002)
                .AddTextKey(9003)
                .AddTextKey(9004);
        }

        private static StoryConfigSet CreateValidConfigSet(
            StoryGraphConfig[] graphs = null,
            StoryBeatConfig[] beats = null,
            StoryStepConfig[] steps = null,
            StoryBranchConfig[] branches = null,
            StoryChoiceConfig[] choices = null,
            StoryFactConfig[] facts = null)
        {
            return new StoryConfigSet(
                graphs ?? new[]
                {
                    new StoryGraphConfig(GraphId, EntryBeatId, sourcePath: "story://valid")
                },
                beats ?? new[]
                {
                    new StoryBeatConfig(ChoiceBeatId, GraphId, sortOrder: 20, choiceSetId: 5001),
                    new StoryBeatConfig(EndBeatId, GraphId, sortOrder: 30),
                    new StoryBeatConfig(EntryBeatId, GraphId, sortOrder: 10, triggerIds: new[] { 302, 301 })
                },
                steps ?? new[]
                {
                    new StoryStepConfig(ChoiceLineStepId, GraphId, ChoiceBeatId, StoryStepKind.Line, textKey: 9003),
                    new StoryStepConfig(EntrySetFactStepId, GraphId, EntryBeatId, StoryStepKind.SetFact, sortOrder: 20, factNamespace: GraphId, factId: GraphFactId, factValueKind: StoryValueKind.Int32, factValueRaw: 7L),
                    new StoryStepConfig(EndStepId, GraphId, EndBeatId, StoryStepKind.Line, textKey: 9004),
                    new StoryStepConfig(EntryLineStepId, GraphId, EntryBeatId, StoryStepKind.Line, sortOrder: 10, textKey: 9001)
                },
                branches ?? new[]
                {
                    new StoryBranchConfig(3, GraphId, EntryBeatId, targetBeatId: 0, conditionFactId: GlobalConditionFactId, priority: 20),
                    new StoryBranchConfig(2, GraphId, EntryBeatId, targetBeatId: ChoiceBeatId, priority: 10, isFallback: true)
                },
                choices ?? new[]
                {
                    new StoryChoiceConfig(6002, GraphId, ChoiceBeatId, labelTextKey: 9002, targetBeatId: 0, sortOrder: 20),
                    new StoryChoiceConfig(6001, GraphId, ChoiceBeatId, labelTextKey: 9001, targetBeatId: EndBeatId, sortOrder: 10, conditionFactId: GlobalConditionFactId, effectIds: new[] { 12, 11 })
                },
                facts ?? new[]
                {
                    new StoryFactConfig(GlobalConditionFactId, 0, StoryValueKind.Bool),
                    new StoryFactConfig(GraphFactId, GraphId, StoryValueKind.Int32)
                });
        }

        private static int[] BeatIds(System.Collections.Generic.IReadOnlyList<StoryBeatDefinition> beats)
        {
            var ids = new int[beats.Count];
            for (int i = 0; i < beats.Count; i++)
                ids[i] = beats[i].BeatId;
            return ids;
        }

        private static int[] StepIds(System.Collections.Generic.IReadOnlyList<StoryStepDefinition> steps)
        {
            var ids = new int[steps.Count];
            for (int i = 0; i < steps.Count; i++)
                ids[i] = steps[i].StepId;
            return ids;
        }

        private static int[] BranchIds(System.Collections.Generic.IReadOnlyList<StoryBranchDefinition> branches)
        {
            var ids = new int[branches.Count];
            for (int i = 0; i < branches.Count; i++)
                ids[i] = branches[i].BranchId;
            return ids;
        }

        private static int[] ChoiceIds(System.Collections.Generic.IReadOnlyList<StoryChoiceDefinition> choices)
        {
            var ids = new int[choices.Count];
            for (int i = 0; i < choices.Count; i++)
                ids[i] = choices[i].ChoiceId;
            return ids;
        }
    }
}
