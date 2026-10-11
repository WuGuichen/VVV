using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterRuntimeModule
    {
        private readonly Dictionary<string, int> _levelActors = new Dictionary<string, int>(StringComparer.Ordinal);
        internal IReadOnlyDictionary<string, int> LevelActorIds => _levelActors;
        internal CharacterTestCharacterEntity FindLevelActor(string stableId)
            => _levelActors.TryGetValue(stableId, out int id) ? FindEntityById(id) : null;

        // Candidate-only startup boundary. Spawn each declared instance individually;
        // map its resulting entity immediately, never match by template or proximity.
        internal void PrepareLevelActors(IReadOnlyList<CharacterTestCharacterSpawnSpec> specs)
        {
            if (Entities.Count != 0 || _levelActors.Count != 0) throw new InvalidOperationException("Fresh level runtime required.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var spec in specs)
            {
                if (string.IsNullOrWhiteSpace(spec.InstanceStableId) || !ids.Add(spec.InstanceStableId)) throw new InvalidOperationException("Missing or duplicate actor instance stableId.");
                var previous = new HashSet<int>(Entities.Select(e => e.Id.Value));
                if (!EnqueueSpawn(new CharacterTestCharacterSpawnRequest(spec.CharacterId, spec.TeamId, spec.X, spec.Y, spec.Z,
                    spec.ControlOwner, spec.RuntimeAiPlannerProfileSlot, spec.RuntimeAiBehaviorSelection)))
                    throw new InvalidOperationException(spec.InstanceStableId + "/spawn rejected");
                _spawnSystem.Tick();
                var created = Entities.Where(e => !previous.Contains(e.Id.Value)).ToArray();
                if (created.Length != 1) throw new InvalidOperationException(spec.InstanceStableId + "/spawn count");
                _levelActors.Add(spec.InstanceStableId, created[0].Id.Value);
            }
        }
    }
}
