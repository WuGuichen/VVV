using MxFramework.Runtime;

namespace MxFramework.Story.Runtime
{
    public sealed class StoryRuntimeHashContributor : IRuntimeHashContributor
    {
        public const string DefaultContributorId = StoryRuntimeModule.DefaultModuleId;

        private readonly StoryDirector _director;

        public StoryRuntimeHashContributor(StoryDirector director)
        {
            _director = director ?? throw new System.ArgumentNullException(nameof(director));
        }

        public string ContributorId => DefaultContributorId;

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            StoryDirectorSnapshot snapshot = _director.CreateSnapshot();
            accumulator.AddInt("story.schema", snapshot.SchemaVersion);
            accumulator.AddInt("story.nextBeatInstanceId", snapshot.NextBeatInstanceId);

            accumulator.AddInt("story.graph.count", snapshot.Graphs.Count);
            for (int i = 0; i < snapshot.Graphs.Count; i++)
            {
                StoryGraphRuntimeSnapshot graph = snapshot.Graphs[i];
                accumulator.AddInt("story.graph.id", graph.GraphId);
                accumulator.AddInt("story.graph.version", graph.Version);
                accumulator.AddInt("story.graph.status", (int)graph.Status);
            }

            accumulator.AddInt("story.beat.count", snapshot.ActiveBeatInstances.Count);
            for (int i = 0; i < snapshot.ActiveBeatInstances.Count; i++)
            {
                StoryBeatInstanceSnapshot beat = snapshot.ActiveBeatInstances[i];
                accumulator.AddInt("story.beat.graphId", beat.GraphId);
                accumulator.AddInt("story.beat.beatId", beat.BeatId);
                accumulator.AddInt("story.beat.instanceId", beat.BeatInstanceId);
                accumulator.AddInt("story.beat.stepIndex", beat.CurrentStepIndex);
                accumulator.AddInt("story.beat.pendingStepId", beat.PendingPresentationStepId);
                accumulator.AddInt("story.beat.pendingPolicy", (int)beat.PendingPresentationPolicy);
                accumulator.AddInt("story.beat.choiceSetId", beat.AwaitingChoiceSetId);
            }

            accumulator.AddInt("story.fact.count", snapshot.Facts.Count);
            for (int i = 0; i < snapshot.Facts.Count; i++)
            {
                StoryFactEntry fact = snapshot.Facts[i];
                accumulator.AddInt("story.fact.namespace", fact.Key.Namespace);
                accumulator.AddInt("story.fact.id", fact.Key.Id);
                accumulator.AddInt("story.fact.kind", (int)fact.Value.Kind);
                accumulator.AddLong("story.fact.raw", fact.Value.Raw);
            }

            accumulator.AddInt("story.stringTable.count", 0);
        }
    }
}
