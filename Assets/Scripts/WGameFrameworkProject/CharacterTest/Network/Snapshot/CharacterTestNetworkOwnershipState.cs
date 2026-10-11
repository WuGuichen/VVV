using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Tracks optional peer-to-entity control grants.
    /// Connecting a peer never creates or assigns a character by itself.
    /// </summary>
    internal sealed class CharacterTestNetworkEntityControlGrantState
    {
        private readonly Dictionary<string, int> _peerToEntityId =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _connectedPeerIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _connectedPeers = new List<string>();
        private readonly List<string> _removePeers = new List<string>();

        public int AssignedPeerCount => _peerToEntityId.Count;
        public int ConnectedPeerCount => _connectedPeerIds.Count;

        public int GetControlledEntityId(string peerId)
        {
            if (string.IsNullOrEmpty(peerId))
                return 0;

            return _peerToEntityId.TryGetValue(peerId, out int entityId) ? entityId : 0;
        }

        public bool IsPeerConnected(string peerId)
        {
            return !string.IsNullOrEmpty(peerId) && _connectedPeerIds.Contains(peerId);
        }

        public bool IsEntityControlled(int entityId)
        {
            if (entityId <= 0)
                return false;

            foreach (KeyValuePair<string, int> grant in _peerToEntityId)
            {
                if (grant.Value == entityId)
                    return true;
            }

            return false;
        }

        public NetworkCommandAuthorizationResult AuthorizeCommand(
            string peerId,
            RuntimeCommand command)
        {
            if (command.CommandId == CharacterTestRuntimeCommandIds.SpawnCharacter)
            {
                return NetworkCommandAuthorizationResult.Reject(
                    "CharacterTest client spawn command is server-authoritative.");
            }

            if (!RequiresEntityControl(command))
                return NetworkCommandAuthorizationResult.Accept();

            int controlledEntityId = GetControlledEntityId(peerId);
            if (controlledEntityId <= 0)
            {
                return NetworkCommandAuthorizationResult.Reject(
                    "CharacterTest peer has no entity-control grant. peer=" + peerId);
            }

            if (command.TargetId == controlledEntityId)
                return NetworkCommandAuthorizationResult.Accept();

            return NetworkCommandAuthorizationResult.Reject(
                "CharacterTest peer cannot control command target. peer="
                + peerId
                + " controlledEntity="
                + controlledEntityId
                + " target="
                + command.TargetId);
        }

        public bool TryRewriteEntityControlCommand(
            RuntimeCommand command,
            string peerId,
            out RuntimeCommand rewritten)
        {
            rewritten = command;
            if (!RequiresEntityControl(command))
                return false;

            int controlledEntityId = GetControlledEntityId(peerId);
            if (controlledEntityId <= 0 || command.TargetId == controlledEntityId)
                return false;

            rewritten = new RuntimeCommand(
                command.Frame,
                command.SourceId,
                command.CommandId,
                controlledEntityId,
                command.Payload0,
                command.Payload1,
                command.Payload2,
                command.TraceId,
                command.Sequence,
                command.Envelope);
            return true;
        }

        public bool SyncConnectedPeers(
            NetworkSessionRunner session,
            CharacterTestCharacterRuntimeModule characterRuntimeModule,
            RuntimeCommandBuffer commandBuffer = null,
            ICharacterTestNetworkPeerControlPolicy peerControlPolicy = null)
        {
            if (session == null || characterRuntimeModule == null)
                return false;

            bool changed = false;
            _connectedPeers.Clear();
            session.CopyConnectedPeerIdsTo(_connectedPeers);

            _removePeers.Clear();
            foreach (string peerId in _connectedPeerIds)
            {
                if (!_connectedPeers.Contains(peerId))
                    _removePeers.Add(peerId);
            }

            for (int i = 0; i < _removePeers.Count; i++)
            {
                string peerId = _removePeers[i];
                int releasedEntityId =
                    ReleaseControlGrant(peerId, characterRuntimeModule, commandBuffer);
                _connectedPeerIds.Remove(peerId);
                peerControlPolicy?.OnPeerDisconnected(peerId, releasedEntityId);
                changed = true;
            }

            for (int i = 0; i < _connectedPeers.Count; i++)
            {
                if (_connectedPeerIds.Add(_connectedPeers[i]))
                    changed = true;
            }

            peerControlPolicy?.PruneDisconnectedPeers(this);

            for (int i = 0; i < _connectedPeers.Count; i++)
            {
                string peerId = _connectedPeers[i];
                if (TryValidateExistingGrant(
                        peerId,
                        characterRuntimeModule,
                        out bool grantChanged))
                {
                    changed |= grantChanged;
                    continue;
                }

                changed |= grantChanged;
                if (peerControlPolicy != null
                    && peerControlPolicy.TryEnsureControlGrant(
                        peerId,
                        this,
                        characterRuntimeModule))
                {
                    changed = true;
                }
            }

            if (changed)
                RequestFullSnapshotForConnectedPeers(session);

            return changed;
        }

        public CharacterTestNetworkOwnershipEntry[] CaptureSnapshot()
        {
            if (_peerToEntityId.Count == 0)
                return Array.Empty<CharacterTestNetworkOwnershipEntry>();

            var entries = new CharacterTestNetworkOwnershipEntry[_peerToEntityId.Count];
            int index = 0;
            foreach (KeyValuePair<string, int> grant in _peerToEntityId)
            {
                entries[index++] = new CharacterTestNetworkOwnershipEntry
                {
                    PeerId = grant.Key,
                    ControlledEntityId = grant.Value
                };
            }

            Array.Sort(entries, CompareOwnershipEntries);
            return entries;
        }

        public string CreateSummary(
            ICharacterTestNetworkPeerControlPolicy peerControlPolicy = null)
        {
            if (_connectedPeerIds.Count == 0)
                return "none";

            var builder = new StringBuilder();
            CharacterTestNetworkOwnershipEntry[] entries = CaptureSnapshot();
            for (int i = 0; i < entries.Length; i++)
            {
                if (builder.Length > 0)
                    builder.Append(',');
                builder.Append(entries[i].PeerId)
                    .Append("->")
                    .Append(entries[i].ControlledEntityId);
            }

            for (int i = 0; i < _connectedPeers.Count; i++)
            {
                string peerId = _connectedPeers[i];
                if (_peerToEntityId.ContainsKey(peerId))
                    continue;
                if (builder.Length > 0)
                    builder.Append(',');
                builder.Append(peerId).Append("->");
                builder.Append(
                    peerControlPolicy != null
                        && peerControlPolicy.IsControlGrantPending(peerId)
                            ? "pending"
                            : "unbound");
            }

            return builder.ToString();
        }

        public bool TryGrantControl(
            string peerId,
            int entityId,
            CharacterTestCharacterRuntimeModule characterRuntimeModule)
        {
            if (string.IsNullOrEmpty(peerId)
                || entityId <= 0
                || characterRuntimeModule == null
                || !_connectedPeerIds.Contains(peerId))
            {
                return false;
            }

            CharacterTestCharacterEntity entity =
                characterRuntimeModule.FindEntityById(entityId);
            if (entity == null || IsEntityControlledByAnotherPeer(peerId, entityId))
                return false;

            if (_peerToEntityId.TryGetValue(peerId, out int existingEntityId)
                && existingEntityId != entityId)
            {
                return false;
            }

            _peerToEntityId[peerId] = entityId;
            if (entity.Control.Owner != CharacterTestCharacterControlOwner.LocalPlayer)
            {
                characterRuntimeModule.TrySetControlOwner(
                    entityId,
                    CharacterTestCharacterControlOwner.LocalPlayer);
            }

            return true;
        }

        private bool TryValidateExistingGrant(
            string peerId,
            CharacterTestCharacterRuntimeModule characterRuntimeModule,
            out bool changed)
        {
            changed = false;
            if (!_peerToEntityId.TryGetValue(peerId, out int entityId))
                return false;

            CharacterTestCharacterEntity entity =
                characterRuntimeModule.FindEntityById(entityId);
            if (entity == null)
            {
                _peerToEntityId.Remove(peerId);
                changed = true;
                return false;
            }

            if (entity.Control.Owner != CharacterTestCharacterControlOwner.LocalPlayer)
            {
                characterRuntimeModule.TrySetControlOwner(
                    entityId,
                    CharacterTestCharacterControlOwner.LocalPlayer);
                changed = true;
            }

            return true;
        }

        private int ReleaseControlGrant(
            string peerId,
            CharacterTestCharacterRuntimeModule characterRuntimeModule,
            RuntimeCommandBuffer commandBuffer)
        {
            if (!_peerToEntityId.TryGetValue(peerId, out int entityId))
                return 0;

            _peerToEntityId.Remove(peerId);
            if (entityId <= 0 || characterRuntimeModule.FindEntityById(entityId) == null)
                return 0;

            PrunePendingEntityControlCommands(commandBuffer, entityId);
            characterRuntimeModule.TrySetControlOwner(
                entityId,
                CharacterTestCharacterControlOwner.None);
            return entityId;
        }

        private static int PrunePendingEntityControlCommands(
            RuntimeCommandBuffer commandBuffer,
            int entityId)
        {
            if (commandBuffer == null || entityId <= 0)
                return 0;

            return commandBuffer.RemoveWhere(command =>
                command.TargetId == entityId && RequiresEntityControl(command));
        }

        private bool IsEntityControlledByAnotherPeer(string peerId, int entityId)
        {
            foreach (KeyValuePair<string, int> grant in _peerToEntityId)
            {
                if (string.Equals(grant.Key, peerId, StringComparison.Ordinal))
                    continue;
                if (grant.Value == entityId)
                    return true;
            }

            return false;
        }

        public static bool RequiresEntityControl(RuntimeCommand command)
        {
            return command.CommandId == CharacterTestRuntimeCommandIds.SetControlIntent
                || command.CommandId == CharacterTestRuntimeCommandIds.CastAbilitySlot
                || command.CommandId == CharacterTestRuntimeCommandIds.RequestCharacterAction
                || command.CommandId == CharacterTestRuntimeCommandIds.SetRuntimeAiBehaviorSelection
                || command.CommandId == CharacterTestRuntimeCommandIds.ToggleTargetLock
                || command.CommandId == CharacterTestRuntimeCommandIds.ApplyAuthorityPosePreview
                || command.CommandId == CharacterTestRuntimeCommandIds.SurfaceProbe;
        }

        private static int CompareOwnershipEntries(
            CharacterTestNetworkOwnershipEntry left,
            CharacterTestNetworkOwnershipEntry right)
        {
            return string.Compare(left.PeerId, right.PeerId, StringComparison.Ordinal);
        }

        private void RequestFullSnapshotForConnectedPeers(NetworkSessionRunner session)
        {
            for (int i = 0; i < _connectedPeers.Count; i++)
                session.RequestFullSnapshotForPeer(_connectedPeers[i]);
        }
    }

    /// <summary>
    /// Selects whether a connected peer receives an automatic character-control grant.
    /// It does not change the peer's ability to observe snapshots or use application control.
    /// </summary>
    internal enum CharacterTestNetworkPeerControlMode
    {
        Unbound = 0,
        SingleCharacterPlayer = 1
    }

    internal interface ICharacterTestNetworkPeerControlPolicy
    {
        void OnPeerDisconnected(string peerId, int controlledEntityId);

        void PruneDisconnectedPeers(
            CharacterTestNetworkEntityControlGrantState controlGrants);

        bool TryEnsureControlGrant(
            string peerId,
            CharacterTestNetworkEntityControlGrantState controlGrants,
            CharacterTestCharacterRuntimeModule characterRuntimeModule);

        bool IsControlGrantPending(string peerId);
    }

    /// <summary>
    /// Character-game join policy that provisions one controllable character for each peer.
    /// This is installed explicitly by player-character sessions and is not a network-session invariant.
    /// </summary>
    internal sealed class CharacterTestSingleCharacterPeerControlPolicy :
        ICharacterTestNetworkPeerControlPolicy
    {
        private const float SpawnSpacing = 1.5f;

        private readonly HashSet<int> _retainedPlayerEntityIds = new HashSet<int>();
        private readonly HashSet<string> _peersAwaitingSpawn =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _removePeers = new List<string>();

        public void OnPeerDisconnected(string peerId, int controlledEntityId)
        {
            if (!string.IsNullOrEmpty(peerId))
                _peersAwaitingSpawn.Remove(peerId);
            if (controlledEntityId > 0)
                _retainedPlayerEntityIds.Add(controlledEntityId);
        }

        public void PruneDisconnectedPeers(
            CharacterTestNetworkEntityControlGrantState controlGrants)
        {
            if (controlGrants == null || _peersAwaitingSpawn.Count == 0)
                return;

            _removePeers.Clear();
            foreach (string peerId in _peersAwaitingSpawn)
            {
                if (!controlGrants.IsPeerConnected(peerId))
                    _removePeers.Add(peerId);
            }

            for (int i = 0; i < _removePeers.Count; i++)
                _peersAwaitingSpawn.Remove(_removePeers[i]);
        }

        public bool TryEnsureControlGrant(
            string peerId,
            CharacterTestNetworkEntityControlGrantState controlGrants,
            CharacterTestCharacterRuntimeModule characterRuntimeModule)
        {
            if (string.IsNullOrEmpty(peerId)
                || controlGrants == null
                || characterRuntimeModule == null
                || !controlGrants.IsPeerConnected(peerId)
                || controlGrants.GetControlledEntityId(peerId) > 0)
            {
                return false;
            }

            int entityId = FindReusablePlayerEntity(controlGrants, characterRuntimeModule);
            if (entityId > 0
                && controlGrants.TryGrantControl(
                    peerId,
                    entityId,
                    characterRuntimeModule))
            {
                _retainedPlayerEntityIds.Remove(entityId);
                _peersAwaitingSpawn.Remove(peerId);
                return true;
            }

            if (characterRuntimeModule.Entities.Count == 0
                || _peersAwaitingSpawn.Contains(peerId))
            {
                return false;
            }

            _peersAwaitingSpawn.Add(peerId);
            characterRuntimeModule.EnqueueSpawn(
                CreateSpawnRequest(characterRuntimeModule.Entities.Count));
            return true;
        }

        public bool IsControlGrantPending(string peerId)
        {
            return !string.IsNullOrEmpty(peerId)
                && _peersAwaitingSpawn.Contains(peerId);
        }

        private int FindReusablePlayerEntity(
            CharacterTestNetworkEntityControlGrantState controlGrants,
            CharacterTestCharacterRuntimeModule characterRuntimeModule)
        {
            foreach (int entityId in _retainedPlayerEntityIds)
            {
                CharacterTestCharacterEntity entity =
                    characterRuntimeModule.FindEntityById(entityId);
                if (entity != null
                    && entity.Control.Owner == CharacterTestCharacterControlOwner.None
                    && !controlGrants.IsEntityControlled(entityId))
                {
                    return entityId;
                }
            }

            IReadOnlyList<CharacterTestCharacterEntity> entities =
                characterRuntimeModule.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer
                    && !controlGrants.IsEntityControlled(entity.Id.Value))
                {
                    return entity.Id.Value;
                }
            }

            return 0;
        }

        private static CharacterTestCharacterSpawnRequest CreateSpawnRequest(
            int existingEntityCount)
        {
            int playerIndex = Math.Max(0, existingEntityCount - 1);
            return new CharacterTestCharacterSpawnRequest(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                teamId: 1,
                x: SpawnSpacing * playerIndex,
                y: 1f,
                z: 0f,
                controlOwner: CharacterTestCharacterControlOwner.LocalPlayer);
        }
    }

    internal static class CharacterTestNetworkPeerControlPolicyFactory
    {
        public static ICharacterTestNetworkPeerControlPolicy Create(
            CharacterTestNetworkPeerControlMode mode)
        {
            switch (mode)
            {
                case CharacterTestNetworkPeerControlMode.Unbound:
                    return null;
                case CharacterTestNetworkPeerControlMode.SingleCharacterPlayer:
                    return new CharacterTestSingleCharacterPeerControlPolicy();
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
    }
}
