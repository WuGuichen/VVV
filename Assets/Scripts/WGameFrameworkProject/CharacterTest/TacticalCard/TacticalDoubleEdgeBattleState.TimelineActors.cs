using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalDoubleEdgeBattleState
    {
        private readonly List<TimelineActor> _timelineActors = new List<TimelineActor>();
        private readonly struct TimelineActor
        {
            internal readonly int Instance;
            internal readonly ProjectileActor Actor;
            internal TimelineActor(int instance, ProjectileActor actor) { Instance = instance; Actor = actor; }
        }
        internal ProjectileActor FindTimelineActor(int instance)
        { foreach (var entry in _timelineActors) if (entry.Instance == instance) return entry.Actor; return null; }
        internal bool FreezeTimelineActor(int instance, ProjectileActor actor)
        {
            if (instance <= 0 || actor == null || !actor.Actor.IsValid) return false;
            var existing = FindTimelineActor(instance);
            if (existing != null) return existing.Matches(actor);
            int at = 0; while (at < _timelineActors.Count && _timelineActors[at].Instance < instance) at++;
            _timelineActors.Insert(at, new TimelineActor(instance, actor)); _revision++; return true;
        }
        private void AddTimelineActorHash(ref RuntimeHashAccumulator hash)
        {
            hash.BeginArray(12, _timelineActors.Count);
            for (int i = 0; i < _timelineActors.Count; i++) {
                var entry = _timelineActors[i]; var actor = entry.Actor; hash.BeginElement(i);
                hash.AddInt(1, entry.Instance); hash.AddInt(2, actor.CombatSource); hash.AddInt(3, actor.Team);
                hash.AddInt(4, actor.Actor.Index); hash.AddInt(5, actor.Actor.Generation);
                hash.BeginArray(6, actor.AttributeCount);
                for (int j = 0; j < actor.AttributeCount; j++) { var value = actor.AttributeAt(j); hash.BeginElement(j);
                    hash.AddInt(1, value.AttributeId); hash.AddInt(2, value.CurrentValue); hash.EndElement(); }
                hash.EndArray(); hash.EndElement();
            }
            hash.EndArray();
        }
        private void AppendTimelineActorProjection(List<long[]> rows)
        {
            foreach (var entry in _timelineActors) {
                var actor = entry.Actor; var row = new long[6 + actor.AttributeCount * 2];
                row[0] = 16; row[1] = entry.Instance; row[2] = actor.CombatSource; row[3] = actor.Team;
                row[4] = actor.Actor.Index; row[5] = actor.Actor.Generation;
                for (int j = 0; j < actor.AttributeCount; j++) {
                    row[6 + j * 2] = actor.AttributeAt(j).AttributeId; row[7 + j * 2] = actor.AttributeAt(j).CurrentValue; }
                rows.Add(row);
            }
        }
    }
}
