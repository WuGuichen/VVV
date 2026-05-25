using System;
using System.Collections.Generic;
using MxFramework.Story;
using MxFramework.Story.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MxFramework.Demo.CharacterTest
{
    public static class CharacterTestStoryDraftJson
    {
        public const string Schema = "mx.story.config.draft.v1";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static bool TryLoad(
            string json,
            out CharacterTestStoryContent content,
            out string error,
            int graphId = 0,
            string sourcePath = null)
        {
            content = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Story draft JSON is empty.";
                return false;
            }

            StoryDraftDto draft;
            try
            {
                draft = JsonConvert.DeserializeObject<StoryDraftDto>(json, JsonSettings);
            }
            catch (JsonException ex)
            {
                error = "Story draft JSON parse failed: " + ex.Message;
                return false;
            }

            if (draft == null)
            {
                error = "Story draft JSON parse failed: payload is null.";
                return false;
            }

            if (!string.Equals(draft.Schema, Schema, StringComparison.Ordinal))
            {
                error = "Unsupported Story draft schema: " + (draft.Schema ?? string.Empty);
                return false;
            }

            int resolvedGraphId = graphId > 0 ? graphId : ResolveDefaultGraphId(draft.Graphs);
            if (resolvedGraphId <= 0)
            {
                error = "Story draft does not contain a valid graph id.";
                return false;
            }

            StoryConfigSet configSet = CreateConfigSet(draft);
            StoryConfigReferenceIndex references = CreateReferenceIndex(draft);
            string resolvedSourcePath = sourcePath ?? draft.SourcePath ?? string.Empty;
            StoryGraphConfigMappingResult result = StoryGraphConfigMapper.Map(
                configSet,
                resolvedGraphId,
                references,
                resolvedSourcePath);
            if (!result.IsValid)
            {
                error = FormatMappingFailure(result);
                return false;
            }

            content = new CharacterTestStoryContent(result.Definition, CreateTextMap(draft.Texts));
            return true;
        }

        private static StoryConfigSet CreateConfigSet(StoryDraftDto draft)
        {
            return new StoryConfigSet(
                draft.Graphs ?? Array.Empty<StoryGraphConfig>(),
                draft.Beats ?? Array.Empty<StoryBeatConfig>(),
                draft.Steps ?? Array.Empty<StoryStepConfig>(),
                draft.Branches ?? Array.Empty<StoryBranchConfig>(),
                draft.Choices ?? Array.Empty<StoryChoiceConfig>(),
                draft.Facts ?? Array.Empty<StoryFactConfig>());
        }

        private static StoryConfigReferenceIndex CreateReferenceIndex(StoryDraftDto draft)
        {
            var references = new StoryConfigReferenceIndex().AddTextKeys(draft.TextKeys);
            if (draft.Texts == null)
                return references;

            for (int i = 0; i < draft.Texts.Length; i++)
            {
                StoryTextDto text = draft.Texts[i];
                if (text != null)
                    references.AddTextKey(text.TextKey);
            }

            return references;
        }

        private static IReadOnlyDictionary<int, string> CreateTextMap(IReadOnlyList<StoryTextDto> texts)
        {
            if (texts == null || texts.Count == 0)
                return new Dictionary<int, string>();

            var result = new Dictionary<int, string>(texts.Count);
            for (int i = 0; i < texts.Count; i++)
            {
                StoryTextDto text = texts[i];
                if (text != null && text.TextKey > 0)
                    result[text.TextKey] = text.Text ?? string.Empty;
            }

            return result;
        }

        private static int ResolveDefaultGraphId(IReadOnlyList<StoryGraphConfig> graphs)
        {
            if (graphs == null || graphs.Count == 0 || graphs[0] == null)
                return 0;

            return graphs[0].Id;
        }

        private static string FormatMappingFailure(StoryGraphConfigMappingResult result)
        {
            if (result == null)
                return "Story draft config mapping failed: result is null.";

            if (result.DiagnosticCount == 0)
                return "Story draft config mapping failed.";

            StoryConfigValidationDiagnostic first = result.Diagnostics[0];
            return "Story draft config mapping failed: " + first.Message;
        }

        private sealed class StoryDraftDto
        {
            public string Schema { get; set; }
            public string SourcePath { get; set; }
            public StoryGraphConfig[] Graphs { get; set; }
            public StoryBeatConfig[] Beats { get; set; }
            public StoryStepConfig[] Steps { get; set; }
            public StoryBranchConfig[] Branches { get; set; }
            public StoryChoiceConfig[] Choices { get; set; }
            public StoryFactConfig[] Facts { get; set; }
            public int[] TextKeys { get; set; }
            public StoryTextDto[] Texts { get; set; }
        }

        private sealed class StoryTextDto
        {
            public int TextKey { get; set; }
            public string Text { get; set; }
        }
    }
}
