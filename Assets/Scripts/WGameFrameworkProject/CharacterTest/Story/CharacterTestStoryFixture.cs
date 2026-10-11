using System;
using MxFramework.Story;
using MxFramework.Story.Config;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// CharacterTest Story 配置 fixture：内置
    /// <see cref="StoryConfigSet"/>，经
    /// <see cref="StoryGraphConfigMapper"/> 映射为运行时图。
    /// </summary>
    public static class CharacterTestStoryFixture
    {
        public static StoryConfigSet CreateStoryConfigSet()
        {
            return new StoryConfigSet(
                CreateGraphs(),
                CreateBeats(),
                CreateSteps(),
                Array.Empty<StoryBranchConfig>(),
                Array.Empty<StoryChoiceConfig>(),
                Array.Empty<StoryFactConfig>());
        }

        public static StoryGraphDefinition CreateBootstrapGraph()
        {
            if (!TryCreateBootstrapGraph(out StoryGraphDefinition definition, out StoryGraphConfigMappingResult result))
                throw new InvalidOperationException(FormatMappingFailure(result));

            return definition;
        }

        public static CharacterTestStoryContent CreateBootstrapContent()
        {
            return new CharacterTestStoryContent(
                CreateBootstrapGraph(),
                CharacterTestStoryText.CreateTextMap());
        }

        /// <summary>LowFreqAlloc. Per-battle graph variant, preserving the ordinary CharacterTest bootstrap contract.</summary>
        public static StoryGraphDefinition CreateTacticalBattleGraph()
        {
            var beats = new System.Collections.Generic.List<StoryBeatConfig>(CreateBeats());
            var steps = new System.Collections.Generic.List<StoryStepConfig>(CreateSteps());
            beats.Add(new StoryBeatConfig(CharacterTestStoryIds.Beats.TacticalBattleVictory, CharacterTestStoryIds.BootstrapGraph,
                sortOrder: 30, triggerIds: new[] { CharacterTestStoryIds.Triggers.TacticalBattleVictory }));
            beats.Add(new StoryBeatConfig(CharacterTestStoryIds.Beats.TacticalBattleDefeat, CharacterTestStoryIds.BootstrapGraph,
                sortOrder: 40, triggerIds: new[] { CharacterTestStoryIds.Triggers.TacticalBattleDefeat }));
            steps.Add(new StoryStepConfig(CharacterTestStoryIds.Steps.TacticalBattleVictory, CharacterTestStoryIds.BootstrapGraph,
                CharacterTestStoryIds.Beats.TacticalBattleVictory, StoryStepKind.Presentation,
                waitPolicy: StoryPresentationWaitPolicy.WaitForCommand, auxId: CharacterTestStoryIds.Actions.TacticalBattleVictory));
            steps.Add(new StoryStepConfig(CharacterTestStoryIds.Steps.TacticalBattleDefeat, CharacterTestStoryIds.BootstrapGraph,
                CharacterTestStoryIds.Beats.TacticalBattleDefeat, StoryStepKind.Presentation,
                waitPolicy: StoryPresentationWaitPolicy.WaitForCommand, auxId: CharacterTestStoryIds.Actions.TacticalBattleDefeat));
            var config = new StoryConfigSet(CreateGraphs(), beats.ToArray(), steps.ToArray(), Array.Empty<StoryBranchConfig>(),
                Array.Empty<StoryChoiceConfig>(), Array.Empty<StoryFactConfig>());
            if (!StoryGraphConfigMapper.TryMap(config, CharacterTestStoryIds.BootstrapGraph, out var graph, out var result,
                CreateReferenceIndex(), CharacterTestStoryIds.BootstrapSourcePath))
                throw new InvalidOperationException(FormatMappingFailure(result));
            return graph;
        }

        public static bool TryCreateBootstrapGraph(
            out StoryGraphDefinition definition,
            out StoryGraphConfigMappingResult result)
        {
            StoryConfigSet config = CreateStoryConfigSet();
            StoryConfigReferenceIndex references = CreateReferenceIndex();
            return StoryGraphConfigMapper.TryMap(
                config,
                CharacterTestStoryIds.BootstrapGraph,
                out definition,
                out result,
                references,
                CharacterTestStoryIds.BootstrapSourcePath);
        }

        private static StoryGraphConfig[] CreateGraphs()
        {
            return new[]
            {
                new StoryGraphConfig(
                    CharacterTestStoryIds.BootstrapGraph,
                    CharacterTestStoryIds.Beats.Entry,
                    sourcePath: CharacterTestStoryIds.BootstrapSourcePath)
            };
        }

        private static StoryBeatConfig[] CreateBeats()
        {
            return new[]
            {
                new StoryBeatConfig(
                    CharacterTestStoryIds.Beats.Entry,
                    CharacterTestStoryIds.BootstrapGraph),
                new StoryBeatConfig(
                    CharacterTestStoryIds.Beats.LevelStartRegion,
                    CharacterTestStoryIds.BootstrapGraph,
                    sortOrder: 10,
                    triggerIds: new[]
                    {
                        CharacterTestStoryIds.Triggers.LevelStartRegion
                    }),
                new StoryBeatConfig(
                    CharacterTestStoryIds.Beats.TrainingComplete,
                    CharacterTestStoryIds.BootstrapGraph,
                    sortOrder: 20,
                    triggerIds: new[]
                    {
                        CharacterTestStoryIds.Triggers.TrainingComplete
                    })
            };
        }

        private static StoryStepConfig[] CreateSteps()
        {
            return new[]
            {
                new StoryStepConfig(
                    CharacterTestStoryIds.Steps.OpenLoadingUi,
                    CharacterTestStoryIds.BootstrapGraph,
                    CharacterTestStoryIds.Beats.Entry,
                    StoryStepKind.Presentation,
                    sortOrder: 10,
                    waitPolicy: StoryPresentationWaitPolicy.NoWait,
                    auxId: CharacterTestStoryIds.Actions.OpenLoadingUi),
                new StoryStepConfig(
                    CharacterTestStoryIds.Steps.LoadBaseResources,
                    CharacterTestStoryIds.BootstrapGraph,
                    CharacterTestStoryIds.Beats.Entry,
                    StoryStepKind.Presentation,
                    sortOrder: 20,
                    waitPolicy: StoryPresentationWaitPolicy.WaitForCommand,
                    auxId: CharacterTestStoryIds.Actions.LoadBaseResources),
                new StoryStepConfig(
                    CharacterTestStoryIds.Steps.OpenStartupUi,
                    CharacterTestStoryIds.BootstrapGraph,
                    CharacterTestStoryIds.Beats.Entry,
                    StoryStepKind.Presentation,
                    sortOrder: 30,
                    waitPolicy: StoryPresentationWaitPolicy.NoWait,
                    auxId: CharacterTestStoryIds.Actions.OpenStartupUi),
                new StoryStepConfig(
                    CharacterTestStoryIds.Steps.WelcomeLine,
                    CharacterTestStoryIds.BootstrapGraph,
                    CharacterTestStoryIds.Beats.Entry,
                    StoryStepKind.Line,
                    sortOrder: 40,
                    textKey: CharacterTestStoryIds.Text.WelcomeLine),
                new StoryStepConfig(
                    CharacterTestStoryIds.Steps.LevelStartRegionLine,
                    CharacterTestStoryIds.BootstrapGraph,
                    CharacterTestStoryIds.Beats.LevelStartRegion,
                    StoryStepKind.Line,
                    sortOrder: 10,
                    textKey:
                        CharacterTestStoryIds.Text.LevelStartRegionLine),
                new StoryStepConfig(
                    CharacterTestStoryIds.Steps.TrainingCompleteLine,
                    CharacterTestStoryIds.BootstrapGraph,
                    CharacterTestStoryIds.Beats.TrainingComplete,
                    StoryStepKind.Line,
                    sortOrder: 10,
                    textKey:
                        CharacterTestStoryIds.Text.TrainingCompleteLine)
            };
        }

        private static StoryConfigReferenceIndex CreateReferenceIndex()
        {
            return new StoryConfigReferenceIndex()
                .AddTextKey(CharacterTestStoryIds.Text.WelcomeLine)
                .AddTextKey(
                    CharacterTestStoryIds.Text.LevelStartRegionLine)
                .AddTextKey(
                    CharacterTestStoryIds.Text.TrainingCompleteLine);
        }

        private static string FormatMappingFailure(StoryGraphConfigMappingResult result)
        {
            if (result == null)
                return "CharacterTest Story config mapping failed: result is null.";

            if (result.DiagnosticCount == 0)
                return "CharacterTest Story config mapping failed.";

            StoryConfigValidationDiagnostic first = result.Diagnostics[0];
            return "CharacterTest Story config mapping failed: " + first.Message;
        }
    }
}
