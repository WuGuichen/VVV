using System.Collections.Generic;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterMotionSystem
    {
        // Mechanism mutations run before capturing contact witnesses. A teleport never becomes a swept contact displacement.
        private void ProcessPositionRecalls(IReadOnlyList<CharacterTestCharacterEntity> entities, long frame)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i]; var state = entity.PositionRecall;
                if (state.StageRun == 0) continue;
                if (state.Actor != entity.GameplayEntityId || !entity.CombatState.IsAlive)
                {
                    state.ClearRecord(); state.Complete(CharacterTestPositionRecallResult.ActorUnavailable, frame);
                    entity.SetPositionRecall(state); continue;
                }
                state.Expire(frame);
                if (state.Pending && frame >= state.RequestedTick)
                {
                    bool free = entity.IsGrounded && entity.BattleState.CanStartNormalAction
                        && entity.BattleState.DominantReason == CharacterTestBattleStateDominantReason.None
                        && entity.Action.State != CharacterTestActionStateId.Running && !entity.GroundPointTraction.IsActive
                        && entity.PendingMotionRequestCount == 0 && entity.PendingCombatRequestCount == 0;
                    if (!free) state.Complete(CharacterTestPositionRecallResult.ActorBusy, frame);
                    else if (state.Operation == CharacterTestPositionRecallOperation.Return && !state.RecordValid(frame))
                        state.Complete(CharacterTestPositionRecallResult.NoRecord, frame);
                    else
                    {
                        var destination = state.Operation == CharacterTestPositionRecallOperation.Record
                            ? new TacticalWorldPoint(entity.Transform.XMillimeters, entity.Transform.YMillimeters, entity.Transform.ZMillimeters)
                            : new TacticalWorldPoint(state.X, state.Y, state.Z);
                        if (state.Operation == CharacterTestPositionRecallOperation.Record)
                        {
                            if (TryValidateGroundPlacement(_recallWorld, entity, destination, out _))
                                state.CompleteRecord(destination.X, destination.Y, destination.Z, frame);
                            else state.Complete(CharacterTestPositionRecallResult.DestinationBlocked, frame);
                        }
                        else if (TryCommitStoppedGroundRelocation(_recallWorld, entity, destination))
                            state.CompleteReturn(frame);
                        else state.Complete(CharacterTestPositionRecallResult.DestinationBlocked, frame);
                    }
                }
                entity.SetPositionRecall(state);
            }
        }

        /// <remarks>LowFreqAlloc: uses the configured navigation factory at the mechanism activation boundary.</remarks>
        private bool TryValidateGroundPlacement(CharacterTestCharacterRuntimeWorld world, CharacterTestCharacterEntity entity,
            TacticalWorldPoint destination, out CombatSurfaceMaterial material)
        {
            material = default;
            if (world == null || entity == null || _physicsRuntime == null
                || !ReferenceEquals(world.FindEntity(entity.Id.Value), entity) || !entity.GameplayEntityId.IsValid
                || !entity.Presence.IsActive || !entity.CombatState.IsAlive) return false;
            CharacterTestMovableNaturalObjectSystem.SyncAllCombatColliders(world.MovableNaturalObjects, _physicsRuntime);
            SyncAllCharacterCombatColliders(world.Entities);
            var navigation = _recallNavigation?.Invoke(entity);
            return navigation != null && navigation.TryValidateGroundPlacement(entity.GameplayEntityId, destination, out material);
        }

        /// <summary>Motion-owner commit after fresh placement queries; preserves yaw and stops all current motion.</summary>
        /// <remarks>
        /// LowFreqAlloc via the navigation factory. The caller owns activation permission, force/action cancellation policy,
        /// mechanism receipts and combat witness invalidation/rebuild. This method does not select card or turn behavior.
        /// </remarks>
        internal bool TryCommitStoppedGroundRelocation(CharacterTestCharacterRuntimeWorld world, CharacterTestCharacterEntity entity,
            TacticalWorldPoint destination)
        {
            if (!TryValidateGroundPlacement(world, entity, destination, out var material)) return false;
            entity.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(destination.X, destination.Y, destination.Z, entity.Transform.YawMilliDegrees));
            entity.SetMotionStateDeterministic(0, 0, 0, true, movementProfileId: _movementProfile.Id,
                movementProfileFallback: _movementProfile.UsedFallback, physicsEnabled: true,
                collisionFlags: CombatMotionCollisionFlags.Grounded, surfaceMaterial: material);
            entity.SetPhysics(entity.Physics.WithContactState(Vector2.zero, Vector2.zero, 0));
            entity.ClearMotionRequests();
            SyncCharacterCombatCollider(entity);
            _physicsCandidateCache.Invalidate();
            RemoveHurtbox(entity.Id); // Drops previous Presence/appendage witnesses; the caller rebuilds at the destination.
            return true;
        }

        private CharacterTestCharacterRuntimeWorld _recallWorld;
        private System.Func<CharacterTestCharacterEntity, CharacterTestTacticalPointNavigation> _recallNavigation;
        internal void ConfigurePositionRecall(System.Func<CharacterTestCharacterEntity, CharacterTestTacticalPointNavigation> navigation) => _recallNavigation = navigation;
    }
}
