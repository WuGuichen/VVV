using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Story.Runtime
{
    public sealed class StoryRuntimeHashContributor : IRuntimeHashContributor
    {
        private const int F_schema = 1;
        private const int F_nextBeatInstanceId = 2;
        private const int F_graph_count = 3;
        private const int F_graph_id = 4;
        private const int F_graph_version = 5;
        private const int F_graph_status = 6;
        private const int F_beat_count = 7;
        private const int F_beat_graphId = 8;
        private const int F_beat_beatId = 9;
        private const int F_beat_instanceId = 10;
        private const int F_beat_stepIndex = 11;
        private const int F_beat_pendingStepId = 12;
        private const int F_beat_pendingPolicy = 13;
        private const int F_beat_choiceSetId = 14;
        private const int F_fact_count = 15;
        private const int F_fact_namespace = 16;
        private const int F_fact_id = 17;
        private const int F_fact_kind = 18;
        private const int F_fact_raw = 19;
        private const int F_stringTable_count = 20;

        public const int DefaultContributorId = RuntimeHashContributorIds.StoryRuntime;
        public int HashSchemaId => RuntimeHashSchemaIds.StoryRuntime;
        public int HashSchemaVersion => 1;
        private readonly List<StoryGraphRuntimeSnapshot> _graphs = new List<StoryGraphRuntimeSnapshot>();
        private readonly List<StoryBeatHashState> _beats = new List<StoryBeatHashState>();
        private readonly List<StoryFactEntry> _facts = new List<StoryFactEntry>();

        private readonly StoryDirector _director;

        public StoryRuntimeHashContributor(StoryDirector director)
        {
            _director = director ?? throw new System.ArgumentNullException(nameof(director));
        }

        public int ContributorId => DefaultContributorId;

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            _director.CopyHashState(_graphs, _beats, _facts);
            accumulator.AddInt(F_schema, StoryDirector.SchemaVersion);
            accumulator.AddInt(F_nextBeatInstanceId, _director.NextBeatInstanceId);

            accumulator.BeginArray(F_graph_count, _graphs.Count);
            for (int i = 0; i < _graphs.Count; i++)
            {
                accumulator.BeginElement(i);
                StoryGraphRuntimeSnapshot graph = _graphs[i];
                accumulator.AddInt(F_graph_id, graph.GraphId);
                accumulator.AddInt(F_graph_version, graph.Version);
                accumulator.AddInt(F_graph_status, (int)graph.Status);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            accumulator.BeginArray(F_beat_count, _beats.Count);
            for (int i = 0; i < _beats.Count; i++)
            {
                accumulator.BeginElement(i);
                StoryBeatHashState beat = _beats[i];
                accumulator.AddInt(F_beat_graphId, beat.GraphId);
                accumulator.AddInt(F_beat_beatId, beat.BeatId);
                accumulator.AddInt(F_beat_instanceId, beat.BeatInstanceId);
                accumulator.AddInt(F_beat_stepIndex, beat.CurrentStepIndex);
                accumulator.AddInt(F_beat_pendingStepId, beat.PendingPresentationStepId);
                accumulator.AddInt(F_beat_pendingPolicy, (int)beat.PendingPresentationPolicy);
                accumulator.AddInt(F_beat_choiceSetId, beat.AwaitingChoiceSetId);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            accumulator.BeginArray(F_fact_count, _facts.Count);
            for (int i = 0; i < _facts.Count; i++)
            {
                accumulator.BeginElement(i);
                StoryFactEntry fact = _facts[i];
                accumulator.AddInt(F_fact_namespace, fact.Key.Namespace);
                accumulator.AddInt(F_fact_id, fact.Key.Id);
                accumulator.AddInt(F_fact_kind, (int)fact.Value.Kind);
                accumulator.AddLong(F_fact_raw, fact.Value.Raw);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            accumulator.AddInt(F_stringTable_count, 0);
        }
    }
}

