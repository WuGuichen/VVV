using System;
using System.Linq;
using MxFramework.Runtime;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterRuntimeModule
    {
        private int[] _pausedProjectionRuntimeIds;
        internal void ImportPausedCharacterProjection(CharacterTestNetworkSnapshotDocument document,
            CharacterTestPausedActorMap[] map, int nextRuntimeEntityId, long completedFrame)
        {
            SessionSnapshotCheck.Require(!_createViews && !_hasCompletedRuntimeFrame && _world.Entities.Count == 0,
                "fresh-headless-runtime-required");
            SessionSnapshotCheck.Require(document.Characters != null && document.Characters.Length == map.Length && map.Length == 2 &&
                map.Select(m => m.Runtime).Distinct().Count() == map.Length && map.All(m => m.Runtime > 0 && m.Runtime < nextRuntimeEntityId) &&
                document.Characters.Select(r => r.EntityId).SequenceEqual(document.Characters.Select(r => r.EntityId).OrderBy(id => id)) &&
                document.Characters.All(r => map.Any(m => m.Runtime == r.EntityId)), "exact-runtime-projection-map");
            _pausedProjectionRuntimeIds = document.Characters.Select(r => r.EntityId).ToArray();
            var validation = ValidateNetworkCharacterSnapshots(document.Characters);
            SessionSnapshotCheck.Require(validation.Success, "character-network-validation:" + validation.Message);
            foreach (var row in document.Characters)
            {
                SessionSnapshotCheck.Require(_definitions.TryGet(row.CharacterId, out var definition), "character-definition");
                var identity = map.Single(m => m.Runtime == row.EntityId);
                var entity = _world.ImportPausedCharacter(row, identity, definition);
                // Cold production registrations are explicit; no spawn event or external observer is emitted.
                PreparePhysicalAudioForSpawnedEntity(entity);
                CharacterTestDefaultBakedWeaponTraceSource.PrepareSpawnedEntity(entity);
                _authorityPoseTerrainSystem.Register(entity);
                ApplyNetworkCharacterSnapshot(entity, row, new RuntimeFrame(completedFrame));
            }
            _world.CompletePausedProjectionImport(nextRuntimeEntityId);
            // ApplyRollbackDocument will now use its in-place path, preserving this exact identity map.
            _lastRuntimeFrame = new RuntimeFrame(completedFrame);
            _hasCompletedRuntimeFrame = true;
            PerceptionFrameCache.ResetForAuthorityRestore(new RuntimeFrame(checked(completedFrame + 1)));
        }
    }
}
