using System;
using MxFramework.Story;
using MxFramework.Story.Config;

namespace MxFramework.Demo.CharacterTest
{
    /// <summary>
    /// CharacterTest Story 配置 fixture：内联 <see cref="StoryConfigSet"/>，经 <see cref="StoryGraphConfigMapper"/> 映射为运行时图。
    /// Phase 1 仅入口 beat 一行欢迎台词；后续可改为 <see cref="StoryConfigSet.FromProvider"/> 加载 JSON。
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

        public static string ResolveText(int textKey)
        {
            return CharacterTestStoryText.TryResolve(textKey, out string text) ? text : string.Empty;
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
                    CharacterTestStoryIds.BootstrapGraph)
            };
        }

        private static StoryStepConfig[] CreateSteps()
        {
            return new[]
            {
                new StoryStepConfig(
                    CharacterTestStoryIds.Steps.WelcomeLine,
                    CharacterTestStoryIds.BootstrapGraph,
                    CharacterTestStoryIds.Beats.Entry,
                    StoryStepKind.Line,
                    textKey: CharacterTestStoryIds.Text.WelcomeLine)
            };
        }

        private static StoryConfigReferenceIndex CreateReferenceIndex()
        {
            return new StoryConfigReferenceIndex()
                .AddTextKey(CharacterTestStoryIds.Text.WelcomeLine);
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
