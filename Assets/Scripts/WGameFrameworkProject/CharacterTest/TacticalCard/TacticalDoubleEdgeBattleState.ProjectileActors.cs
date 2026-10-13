using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalDoubleEdgeBattleState
    {
        private readonly List<ProjectileActor> _projectileActors = new List<ProjectileActor>();
        // Immutable numeric birth evidence. No mutable entity or component store survives removal/reuse.
        internal sealed class ProjectileActor
        {
            internal readonly int Id, Sequence, CombatSource, Team;
            internal readonly GameplayEntityId Actor;
            private readonly GameplayAttributeValue[] _attributes;
            internal readonly Func<int, int> ReadAttribute;
            internal ProjectileActor(int id, int sequence, int combatSource, int team, GameplayEntityId actor, GameplayAttributeValue[] attributes)
            {
                Id = id; Sequence = sequence; CombatSource = combatSource; Team = team; Actor = actor;
                _attributes = (GameplayAttributeValue[])attributes.Clone();
                Array.Sort(_attributes, (a, b) => a.AttributeId.CompareTo(b.AttributeId));
                ReadAttribute = Read;
            }
            private int Read(int id)
            { foreach (var a in _attributes) if (a.AttributeId == id) return a.CurrentValue; return 0; }
            internal ProjectileActor WithIdentity(int id, int sequence) => new ProjectileActor(id, sequence, CombatSource, Team, Actor, _attributes);
            internal bool Matches(ProjectileActor other)
            {
                if (other == null || Actor != other.Actor || Team != other.Team || CombatSource != other.CombatSource || AttributeCount != other.AttributeCount) return false;
                for (int i = 0; i < AttributeCount; i++) if (_attributes[i].AttributeId != other._attributes[i].AttributeId
                    || _attributes[i].CurrentValue != other._attributes[i].CurrentValue) return false;
                return true;
            }
            internal int AttributeCount => _attributes.Length;
            internal GameplayAttributeValue AttributeAt(int index) => _attributes[index];
        }
        internal ProjectileActor FindProjectileActor(int id, int sequence)
        { foreach (var a in _projectileActors) if (a.Id == id && a.Sequence == sequence) return a; return null; }
        internal bool FreezeProjectileActor(ProjectileActor actor)
        {
            if (actor == null || actor.Id <= 0 || !actor.Actor.IsValid) return false;
            var previous = FindProjectileActor(actor.Id, actor.Sequence);
            if (previous != null) return ReferenceEquals(previous, actor);
            int index = 0;
            while (index < _projectileActors.Count && (_projectileActors[index].Id < actor.Id
                || _projectileActors[index].Id == actor.Id && _projectileActors[index].Sequence < actor.Sequence)) index++;
            _projectileActors.Insert(index, actor); _revision++; return true;
        }
        private void AddProjectileActorHash(ref RuntimeHashAccumulator hash)
        {
            hash.BeginArray(10, _projectileActors.Count);
            for (int i = 0; i < _projectileActors.Count; i++) {
                var a = _projectileActors[i]; hash.BeginElement(i);
                hash.AddInt(1, a.Id); hash.AddInt(2, a.Sequence); hash.AddInt(3, a.CombatSource); hash.AddInt(4, a.Team);
                hash.AddInt(5, a.Actor.Index); hash.AddInt(6, a.Actor.Generation); hash.BeginArray(7, a.AttributeCount);
                for (int j = 0; j < a.AttributeCount; j++) { var value = a.AttributeAt(j); hash.BeginElement(j);
                    hash.AddInt(1, value.AttributeId); hash.AddInt(2, value.CurrentValue); hash.EndElement(); }
                hash.EndArray(); hash.EndElement();
            }
            hash.EndArray();
        }
        private void AppendProjectileActorProjection(List<long[]> rows)
        {
            foreach (var a in _projectileActors) {
                var row = new long[7 + a.AttributeCount * 2]; row[0] = 14; row[1] = a.Id; row[2] = a.Sequence;
                row[3] = a.CombatSource; row[4] = a.Team; row[5] = a.Actor.Index; row[6] = a.Actor.Generation;
                for (int i = 0; i < a.AttributeCount; i++) { row[7 + i * 2] = a.AttributeAt(i).AttributeId; row[8 + i * 2] = a.AttributeAt(i).CurrentValue; }
                rows.Add(row);
            }
        }
    }
}
