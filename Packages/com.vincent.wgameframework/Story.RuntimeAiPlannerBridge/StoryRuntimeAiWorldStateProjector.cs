using System;
using System.Collections.Generic;
using MxFramework.AI;

namespace MxFramework.Story.RuntimeAiPlannerBridge
{
    public static class StoryRuntimeAiWorldStateProjector
    {
        public static StoryRuntimeAiProjectionResult Project(
            IStoryBlackboard blackboard,
            IAiWorldState worldState,
            StoryRuntimeAiProjectionProfile profile,
            StoryRuntimeAiProjectionDiagnostics diagnostics = null)
        {
            if (blackboard == null)
                throw new ArgumentNullException(nameof(blackboard));

            StoryFactEntry[] facts = blackboard.Count > 0
                ? new StoryFactEntry[blackboard.Count]
                : Array.Empty<StoryFactEntry>();
            StoryFactCopyResult copy = blackboard.CopyOrdered(facts);
            if (!copy.Complete)
            {
                facts = new StoryFactEntry[copy.RequiredCount];
                copy = blackboard.CopyOrdered(facts);
                if (!copy.Complete)
                    throw new InvalidOperationException("Story blackboard could not copy a complete ordered fact snapshot.");
            }

            return Project(facts, worldState, profile, diagnostics);
        }

        public static StoryRuntimeAiProjectionResult Project(
            StoryDirectorSnapshot snapshot,
            IAiWorldState worldState,
            StoryRuntimeAiProjectionProfile profile,
            StoryRuntimeAiProjectionDiagnostics diagnostics = null)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            return Project(snapshot.Facts, worldState, profile, diagnostics);
        }

        public static StoryRuntimeAiProjectionResult Project(
            IReadOnlyList<StoryFactEntry> facts,
            IAiWorldState worldState,
            StoryRuntimeAiProjectionProfile profile,
            StoryRuntimeAiProjectionDiagnostics diagnostics = null)
        {
            if (facts == null)
                throw new ArgumentNullException(nameof(facts));
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            var matched = profile.Count > 0 ? new bool[profile.Count] : Array.Empty<bool>();
            int projected = 0;
            int skipped = 0;
            int unsupported = 0;

            for (int i = 0; i < facts.Count; i++)
            {
                StoryFactEntry fact = facts[i];
                int mappingIndex = profile.IndexOf(fact.Key);
                if (mappingIndex < 0)
                {
                    skipped++;
                    diagnostics?.Record(new StoryRuntimeAiProjectionDiagnostic(
                        StoryRuntimeAiProjectionDiagnosticCode.SkippedUnlistedStoryFact,
                        fact.Key,
                        default,
                        fact.Value.Kind,
                        "Story fact is not whitelisted for Runtime AI Planner projection."));
                    continue;
                }

                matched[mappingIndex] = true;
                StoryRuntimeAiFactMapping mapping = profile.Mappings[mappingIndex];
                if (TryWrite(worldState, mapping.AiKey, fact.Value))
                {
                    projected++;
                    continue;
                }

                unsupported++;
                worldState.Remove(mapping.AiKey);
                diagnostics?.Record(new StoryRuntimeAiProjectionDiagnostic(
                    StoryRuntimeAiProjectionDiagnosticCode.UnsupportedStoryValueKind,
                    fact.Key,
                    mapping.AiKey,
                    fact.Value.Kind,
                    "Story value kind is not supported by the Runtime AI Planner projection bridge."));
            }

            int missing = 0;
            for (int i = 0; i < matched.Length; i++)
            {
                if (matched[i])
                    continue;

                missing++;
                StoryRuntimeAiFactMapping mapping = profile.Mappings[i];
                worldState.Remove(mapping.AiKey);
                diagnostics?.Record(new StoryRuntimeAiProjectionDiagnostic(
                    StoryRuntimeAiProjectionDiagnosticCode.MissingStoryFact,
                    mapping.StoryKey,
                    mapping.AiKey,
                    StoryValueKind.None,
                    "Whitelisted Story fact is missing; stale Runtime AI Planner fact was removed."));
            }

            return new StoryRuntimeAiProjectionResult(projected, missing, skipped, unsupported);
        }

        private static bool TryWrite(IAiWorldState worldState, AiFactKey aiKey, StoryValue value)
        {
            switch (value.Kind)
            {
                case StoryValueKind.Bool:
                    worldState.SetValue(aiKey, value.Raw != 0L);
                    return true;
                case StoryValueKind.Int32:
                    if (value.Raw < int.MinValue || value.Raw > int.MaxValue)
                        return false;

                    worldState.SetValue(aiKey, (int)value.Raw);
                    return true;
                case StoryValueKind.Int64:
                    worldState.SetValue(aiKey, value.Raw);
                    return true;
                case StoryValueKind.Fix64:
                    worldState.SetValue(aiKey, value.Raw);
                    return true;
                default:
                    return false;
            }
        }
    }
}
