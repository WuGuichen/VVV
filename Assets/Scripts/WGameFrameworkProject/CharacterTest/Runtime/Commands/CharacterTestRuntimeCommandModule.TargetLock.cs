using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeCommandModule
    {
        private void ApplyToggleTargetLock(RuntimeCommand command)
        {
            if (command.Payload0 == CharacterTestRuntimeCommandPayloadCodec.TargetLockToggleModeViewSwitch)
            {
                ApplyTargetLockViewSwitch(command);
                return;
            }

            if (TargetLockEnabled)
            {
                SetTargetLock(command.Frame, command, null, false, 0, "disabled");
                return;
            }

            CharacterTestCharacterEntity primary = ResolvePrimaryTarget(command.TargetId);
            if (primary == null)
            {
                RejectTargetLock(command, "primary target was not found");
                return;
            }

            CharacterTestCharacterEntity target = FindNearestTarget(command.Frame, primary);
            if (target == null)
            {
                RejectTargetLock(command, "lock target was not found");
                return;
            }

            SetTargetLock(command.Frame, command, primary, true, target.Id.Value, "enabled");
        }

        private void ApplyTargetLockViewSwitch(RuntimeCommand command)
        {
            CharacterTestCharacterEntity primary = ResolvePrimaryTarget(command.TargetId);
            if (primary == null)
            {
                RejectTargetLock(command, "primary target was not found");
                return;
            }

            if (!TargetLockEnabled)
            {
                if (command.SourceId == CharacterTestRuntimeCommandSources.CameraInput)
                    return;

                RejectTargetLock(command, "target lock is not enabled");
                return;
            }

            CharacterTestCharacterEntity lockedTarget = FindEntity(LockedTargetEntityId);
            if (!IsValidTargetLockTarget(primary, lockedTarget, out string lockedTargetMessage))
            {
                SetTargetLock(command.Frame, command, null, false, 0, lockedTargetMessage);
                return;
            }

            if (command.Payload1 <= 0)
            {
                if (command.SourceId == CharacterTestRuntimeCommandSources.CameraInput)
                    return;

                RejectTargetLock(command, "target entity was not found");
                return;
            }

            CharacterTestCharacterEntity target = FindEntity(command.Payload1);
            if (target != null && target.Id.Value == LockedTargetEntityId)
            {
                RejectTargetLock(command, "target entity is already locked");
                return;
            }

            if (!IsValidTargetLockTarget(primary, target, out string message))
            {
                RejectTargetLock(command, message);
                return;
            }

            SetTargetLock(command.Frame, command, primary, true, target.Id.Value, "switched");
        }

        private void ApplySetTargetLock(RuntimeCommand command)
        {
            if (command.Payload0 == 0)
            {
                SetTargetLock(command.Frame, command, null, false, 0, "disabled");
                return;
            }

            CharacterTestCharacterEntity primary = ResolvePrimaryTarget(0);
            if (primary == null)
            {
                RejectTargetLock(command, "primary target was not found");
                return;
            }

            CharacterTestCharacterEntity target = FindEntity(command.TargetId);
            if (!IsValidTargetLockTarget(primary, target, out string message))
            {
                RejectTargetLock(command, message);
                return;
            }

            SetTargetLock(command.Frame, command, primary, true, command.TargetId, "enabled");
        }


        private void SetTargetLock(
            RuntimeFrame frame,
            RuntimeCommand command,
            CharacterTestCharacterEntity source,
            bool enabled,
            int targetEntityId,
            string message)
        {
            CharacterTestCombatTargetHandle combatTarget = default;
            if (enabled)
            {
                CharacterTestCharacterEntity target = FindEntity(targetEntityId);
                bool hasFormalDamageZones = target != null
                    && _characterRuntimeModule != null
                    && _characterRuntimeModule.TryGetDamageZoneSnapshot(
                        target.Id.Value,
                        out _);
                bool acquired = source != null
                    && target != null
                    && _perceptionFrameCache != null
                    && _perceptionFrameCache.TryAcquireNearestHurtbox(
                        frame,
                        source,
                        target,
                        out combatTarget,
                        out _);
                if (!acquired && hasFormalDamageZones)
                {
                    RejectTargetLock(command, "target has no available combat Hurtbox");
                    return;
                }
            }

            TargetLockEnabled = enabled;
            LockedTargetEntityId = enabled ? targetEntityId : 0;
            LockedCombatTarget = enabled ? combatTarget : default;
            RecordEvent(new CharacterTestRuntimeEvent(
                frame,
                CharacterTestRuntimeEventType.TargetLockChanged,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                entityId: LockedTargetEntityId,
                message: message));
        }

        private void RejectTargetLock(RuntimeCommand command, string message)
        {
            AddCommandError(command, message);
            RecordEvent(new CharacterTestRuntimeEvent(
                command.Frame,
                CharacterTestRuntimeEventType.TargetLockRejected,
                command.CommandId,
                command.SourceId,
                command.TargetId,
                message: message));
        }


        private CharacterTestCharacterEntity ResolvePrimaryTarget(int requestedEntityId)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _characterRuntimeModule != null
                ? _characterRuntimeModule.Entities
                : null;
            if (entities == null)
                return null;

            CharacterTestCharacterEntity fallback = null;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (fallback == null)
                    fallback = entity;

                if (requestedEntityId > 0 && entity.Id.Value == requestedEntityId)
                    return entity;

                if (requestedEntityId <= 0 && entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entity;
            }

            return requestedEntityId <= 0 ? fallback : null;
        }

        private CharacterTestCharacterEntity FindNearestTarget(
            RuntimeFrame frame,
            CharacterTestCharacterEntity primaryTarget)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _characterRuntimeModule != null
                ? _characterRuntimeModule.Entities
                : null;
            if (entities == null || primaryTarget == null)
                return null;

            if (_perceptionFrameCache != null
                && _perceptionFrameCache.TryFindNearestTarget(
                    frame,
                    primaryTarget,
                    CharacterTestPerceptionTargetRelationFilter.OpposingTeam,
                    CharacterTestPerceptionTargetStateFilter.Alive,
                    _targetLockMaxDistance,
                    CharacterTestPerceptionTargetTiePolicy.Last,
                    out _,
                    out CharacterTestCharacterEntity cachedNearest))
            {
                if (IsTargetLockCandidate(primaryTarget, cachedNearest))
                    return cachedNearest;
            }

            CharacterTestCharacterEntity nearest = null;
            float nearestSqrDistance = _targetLockMaxDistance * _targetLockMaxDistance;
            float primaryX = primaryTarget.Transform.X;
            float primaryZ = primaryTarget.Transform.Z;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (!IsTargetLockCandidate(primaryTarget, entity))
                    continue;

                float dx = entity.Transform.X - primaryX;
                float dz = entity.Transform.Z - primaryZ;
                float sqrDistance = dx * dx + dz * dz;
                if (sqrDistance <= nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = entity;
                }
            }

            return nearest;
        }

        private void PruneInvalidTargetLock(RuntimeFrame frame)
        {
            if (!TargetLockEnabled || LockedTargetEntityId <= 0)
                return;

            CharacterTestCharacterEntity primary = ResolvePrimaryTarget(0);
            CharacterTestCharacterEntity target = FindEntity(LockedTargetEntityId);
            if (IsValidTargetLockTarget(primary, target, out string message))
                return;

            var synthetic = new RuntimeCommand(
                frame,
                CharacterTestRuntimeCommandSources.System,
                CharacterTestRuntimeCommandIds.SetTargetLock,
                LockedTargetEntityId,
                payload0: 0,
                traceId: new RuntimeTraceIdentity(CharacterTestRuntimeInputTraceKinds.TargetLockPrune, CharacterTestRuntimeCommandSources.System, frame.Value, LockedTargetEntityId));
            SetTargetLock(frame, synthetic, null, false, 0, message);
        }

        private CharacterTestCharacterEntity FindEntity(int entityId)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _characterRuntimeModule != null
                ? _characterRuntimeModule.Entities
                : null;
            if (entities == null)
                return null;

            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Id.Value == entityId)
                    return entities[i];
            }

            return null;
        }

        private bool IsTargetLockCandidate(
            CharacterTestCharacterEntity primary,
            CharacterTestCharacterEntity target)
        {
            if (primary == null || target == null)
                return false;
            if (target.Id.Value == primary.Id.Value)
                return false;
            if (target.Team.TeamId == primary.Team.TeamId)
                return false;
            return target.BattleState.CountsAsAlive && target.BattleState.CanReceiveHpDamage;
        }

        private bool IsValidTargetLockTarget(
            CharacterTestCharacterEntity primary,
            CharacterTestCharacterEntity target,
            out string message)
        {
            if (primary == null)
            {
                message = "primary target was not found";
                return false;
            }

            if (target == null)
            {
                message = "target entity was not found";
                return false;
            }

            if (target.Id.Value == primary.Id.Value)
            {
                message = "target entity is the primary target";
                return false;
            }

            if (target.Team.TeamId == primary.Team.TeamId)
            {
                message = "target entity is friendly";
                return false;
            }

            if (!target.BattleState.CountsAsAlive || !target.BattleState.CanReceiveHpDamage)
            {
                message = "target entity is not alive";
                return false;
            }

            if (!IsWithinTargetLockDistance(primary, target))
            {
                message = "target entity is outside target lock range";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private bool IsWithinTargetLockDistance(
            CharacterTestCharacterEntity primary,
            CharacterTestCharacterEntity target)
        {
            if (_perceptionFrameCache != null
                && _perceptionFrameCache.TryGetCommittedPlanarDistanceSquaredMeters(
                    primary,
                    target,
                    out float committedSqrDistance))
            {
                float committedMaxDistance = _targetLockMaxDistance > 0f
                    ? _targetLockMaxDistance
                    : 0f;
                return committedSqrDistance <= committedMaxDistance * committedMaxDistance;
            }

            float dx = target.Transform.X - primary.Transform.X;
            float dz = target.Transform.Z - primary.Transform.Z;
            float maxDistance = _targetLockMaxDistance > 0f ? _targetLockMaxDistance : 0f;
            return (dx * dx) + (dz * dz) <= maxDistance * maxDistance;
        }

    }
}
