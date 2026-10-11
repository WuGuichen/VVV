using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestCharacterViewSyncSystem
    {
        private const int HeadPadBuffBadgeCount = 4;

        private readonly CharacterTestCharacterUnityViewBridge _viewBridge;
        private readonly GameplayBuffDefinitionRegistry _buffDefinitions;
        private readonly GameplayBuffPresentationDefinitionRegistry _buffPresentationDefinitions;
        private readonly List<GameplayBuffPresentationItem> _headPadBuffBadges =
            new List<GameplayBuffPresentationItem>(HeadPadBuffBadgeCount);
        private readonly List<GameplayComponentBuffEntry> _headPadBuffEntries =
            new List<GameplayComponentBuffEntry>(HeadPadBuffBadgeCount);

        public CharacterTestCharacterViewSyncSystem(
            CharacterTestCharacterUnityViewBridge viewBridge,
            GameplayBuffDefinitionRegistry buffDefinitions = null,
            GameplayBuffPresentationDefinitionRegistry buffPresentationDefinitions = null)
        {
            _viewBridge = viewBridge;
            _buffDefinitions = buffDefinitions;
            _buffPresentationDefinitions = buffPresentationDefinitions;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            bool smoothNetworkSnapshots = false,
            RuntimeFrame frame = default,
            int presentationAllyTeamId = 0)
        {
            Tick(
                world,
                lockedTargetEntityId: 0,
                CharacterTestHeadPadDisplayMode.CombatReadout,
                smoothNetworkSnapshots,
                frame,
                presentationAllyTeamId);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            int lockedTargetEntityId,
            bool smoothNetworkSnapshots = false,
            RuntimeFrame frame = default,
            int presentationAllyTeamId = 0)
        {
            Tick(
                world,
                lockedTargetEntityId,
                CharacterTestHeadPadDisplayMode.CombatReadout,
                smoothNetworkSnapshots,
                frame,
                presentationAllyTeamId);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            int lockedTargetEntityId,
            CharacterTestHeadPadDisplayMode headPadDisplayMode,
            bool smoothNetworkSnapshots = false,
            RuntimeFrame frame = default,
            int presentationAllyTeamId = 0,
            bool forceHeadPadsVisible = false)
        {
            _viewBridge.BeginViewSyncFrame();
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            bool hasLocalPlayerTeam = TryResolvePresentationTeam(
                entities,
                presentationAllyTeamId,
                out int localPlayerTeamId);
            CharacterTestHeadPadDisplayMode displayMode =
                CharacterTestHeadPadDisplayModes.Normalize(headPadDisplayMode);
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (!entity.Presence.IsActive)
                    continue;

                GameplayBuffPresentationSnapshotBuilder.BuildInto(
                    world.GameplayComponentWorld,
                    entity.GameplayEntityId,
                    _buffDefinitions,
                    _buffPresentationDefinitions,
                    frame,
                    GameplayBuffPresentationSurface.HeadPad,
                    _headPadBuffBadges,
                    _headPadBuffEntries,
                    HeadPadBuffBadgeCount);
                _viewBridge.SyncView(
                    entity,
                    hasLocalPlayerTeam,
                    localPlayerTeamId,
                    smoothNetworkSnapshots,
                    frame,
                    ResolveHeadPadLockedVisible(
                        lockedTargetEntityId,
                        entity.Id.Value,
                        entity.CombatState.IsAlive,
                        forceHeadPadsVisible),
                    displayMode,
                    _headPadBuffBadges);
            }
        }

        internal static bool ResolveHeadPadLockedVisible(
            int lockedTargetEntityId,
            int entityId,
            bool isAlive,
            bool forceHeadPadsVisible)
        {
            return isAlive
                && (forceHeadPadsVisible
                    || (lockedTargetEntityId > 0 && entityId == lockedTargetEntityId));
        }

        internal static bool TryResolvePresentationTeam(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int presentationAllyTeamId,
            out int localPlayerTeamId)
        {
            if (TryResolveLocalPlayerTeam(entities, out localPlayerTeamId))
                return true;

            if (presentationAllyTeamId > 0)
            {
                localPlayerTeamId = presentationAllyTeamId;
                return true;
            }

            return false;
        }

        private static bool TryResolveLocalPlayerTeam(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            out int localPlayerTeamId)
        {
            localPlayerTeamId = 0;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (!entity.Presence.IsActive)
                    continue;

                if (entity.Control.Owner != CharacterTestCharacterControlOwner.LocalPlayer)
                    continue;

                localPlayerTeamId = entity.Team.TeamId;
                return true;
            }

            return false;
        }
    }
}
