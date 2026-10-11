using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Editor;
using MxFramework.Story.Config;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private readonly struct StoryTriggerCatalogSource
        {
            public StoryTriggerCatalogSource(
                int graphId,
                int beatId,
                string sourcePath)
            {
                GraphId = graphId;
                BeatId = beatId;
                SourcePath = sourcePath ?? string.Empty;
            }

            public int GraphId { get; }

            public int BeatId { get; }

            public string SourcePath { get; }
        }

        private VisualElement CreateTriggerStoryReferenceField(
            CharacterTestLevelGameplayTriggerEntry entry,
            long payload0,
            long payload1,
            long payload2)
        {
            TryBuildTriggerStoryReferenceChoices(
                out List<MxAuthoringReferenceOption<long>> choices,
                out string catalogDiagnostic);
            return MxAuthoringReferenceField.Create(
                "剧情触发",
                payload0,
                choices,
                value =>
                {
                    if (UpdateTriggerPayloads(
                            entry.StableId,
                            value,
                            payload1,
                            payload2))
                    {
                        RefreshTriggerInspectorAfterMutation();
                    }
                },
                name: "character-test-trigger-story-reference",
                emptyDiagnostic:
                    string.IsNullOrWhiteSpace(catalogDiagnostic)
                        ? "当前 CharacterTest Story 配置没有可用的触发 ID。"
                        : catalogDiagnostic,
                unknownValueFormatter:
                    value => "未登记剧情触发 " + value,
                unknownDiagnostic:
                    "当前剧情触发 ID 不在 CharacterTest Story 配置中；"
                    + "原值已保留，请选择正式候选。");
        }

        private static long ResolveDefaultTriggerStoryReferenceId()
        {
            return TryBuildTriggerStoryReferenceChoices(
                    out List<MxAuthoringReferenceOption<long>> choices,
                    out _)
                && choices.Count > 0
                    ? choices[0].Value
                    : 0L;
        }

        private static string ResolveTriggerStoryReferenceLabel(
            long triggerId)
        {
            if (TryBuildTriggerStoryReferenceChoices(
                    out List<MxAuthoringReferenceOption<long>> choices,
                    out _))
            {
                for (int i = 0; i < choices.Count; i++)
                {
                    if (choices[i].Value == triggerId)
                        return choices[i].DisplayName;
                }
            }

            return "未登记剧情触发 " + triggerId;
        }

        private static bool TryBuildTriggerStoryReferenceChoices(
            out List<MxAuthoringReferenceOption<long>> choices,
            out string diagnostic)
        {
            choices = new List<MxAuthoringReferenceOption<long>>();
            diagnostic = string.Empty;

            StoryConfigSet config =
                CharacterTestStoryFixture.CreateStoryConfigSet();
            if (config?.Beats == null || config.Beats.Count == 0)
            {
                diagnostic =
                    "CharacterTest Story 配置中没有可创作的剧情触发。";
                return false;
            }

            var sources =
                new Dictionary<int, StoryTriggerCatalogSource>();
            var conflicts = new HashSet<int>();
            for (int beatIndex = 0;
                 beatIndex < config.Beats.Count;
                 beatIndex++)
            {
                StoryBeatConfig beat = config.Beats[beatIndex];
                if (beat?.TriggerIds == null)
                    continue;

                string sourcePath =
                    ResolveTriggerStoryGraphSourcePath(
                        config.Graphs,
                        beat.GraphId);
                for (int triggerIndex = 0;
                     triggerIndex < beat.TriggerIds.Length;
                     triggerIndex++)
                {
                    int triggerId = beat.TriggerIds[triggerIndex];
                    if (triggerId <= 0)
                        continue;

                    var next = new StoryTriggerCatalogSource(
                        beat.GraphId,
                        beat.Id,
                        sourcePath);
                    if (sources.TryGetValue(
                            triggerId,
                            out StoryTriggerCatalogSource existing)
                        && (existing.GraphId != next.GraphId
                            || existing.BeatId != next.BeatId))
                    {
                        conflicts.Add(triggerId);
                        continue;
                    }

                    sources[triggerId] = next;
                }
            }

            foreach (int conflict in conflicts)
                sources.Remove(conflict);

            var ids = new List<int>(sources.Keys);
            ids.Sort();
            for (int i = 0; i < ids.Count; i++)
            {
                int triggerId = ids[i];
                StoryTriggerCatalogSource source = sources[triggerId];
                choices.Add(
                    new MxAuthoringReferenceOption<long>(
                        "story-trigger:"
                        + triggerId.ToString(
                            CultureInfo.InvariantCulture),
                        triggerId,
                        triggerId
                        + " · "
                        + ResolveTriggerStoryDisplayName(triggerId),
                        "Story Graph "
                        + source.GraphId
                        + " · Beat "
                        + source.BeatId,
                        "来自 "
                        + (string.IsNullOrWhiteSpace(source.SourcePath)
                            ? "CharacterTest Story 配置"
                            : source.SourcePath)
                        + "；Runtime 由 Story RaiseTrigger 消费。"));
            }

            if (conflicts.Count > 0)
            {
                var conflictingIds = new List<int>(conflicts);
                conflictingIds.Sort();
                diagnostic =
                    "CharacterTest Story 配置存在重复 Trigger ID："
                    + string.Join(", ", conflictingIds)
                    + "。冲突项已从候选中移除。";
            }
            else if (choices.Count == 0)
            {
                diagnostic =
                    "CharacterTest Story 配置中没有可创作的剧情触发。";
            }

            return choices.Count > 0;
        }

        private static string ResolveTriggerStoryGraphSourcePath(
            IReadOnlyList<StoryGraphConfig> graphs,
            int graphId)
        {
            for (int i = 0; graphs != null && i < graphs.Count; i++)
            {
                StoryGraphConfig graph = graphs[i];
                if (graph?.Id == graphId)
                    return graph.SourcePath ?? string.Empty;
            }

            return string.Empty;
        }

        private static string ResolveTriggerStoryDisplayName(int triggerId)
        {
            switch (triggerId)
            {
                case CharacterTestStoryIds.Triggers.LevelStartRegion:
                    return "起始区域引导";
                case CharacterTestStoryIds.Triggers.TrainingComplete:
                    return "训练完成";
                default:
                    return "剧情触发";
            }
        }
    }
}
