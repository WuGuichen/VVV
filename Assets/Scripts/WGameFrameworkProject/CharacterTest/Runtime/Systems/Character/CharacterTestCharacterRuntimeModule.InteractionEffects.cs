using MxFramework.Runtime;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterRuntimeModule
    {
        // Called after this frame's motion producers and immediately before Motion consumption; authority geometry rebuild follows.
        private void ProcessInteractionReturns(RuntimeFrame frame)
        {
            for (int i = 0; i < _world.Entities.Count; i++)
            {
                var actor = _world.Entities[i]; var effect = actor.InteractionEffects;
                if (effect == null || effect.Stage == 0) continue;
                if (effect.Actor != actor.GameplayEntityId || !actor.CombatState.IsAlive) { effect.Clear(); continue; }
                effect.Expire(frame.Value);
                if (!effect.TryGetReturnPoint(frame.Value, out var point)) continue;
                bool released = actor.IsGrounded && actor.BattleState.CanStartNormalAction
                    && actor.BattleState.DominantReason == CharacterTestBattleStateDominantReason.None
                    && actor.Action.State != CharacterTestActionStateId.Running && !actor.Input.HasActiveMove && !actor.GroundPointTraction.IsActive
                    && actor.HorizontalVelocityXMillimetersPerSecond == 0 && actor.HorizontalVelocityYMillimetersPerSecond == 0
                    && actor.VerticalVelocityMillimetersPerSecond == 0
                    && actor.PendingMotionRequestCount == 0 && actor.PendingCombatRequestCount == 0;
                if (!released) continue; // Fixed delayed-return contract; never cancel another action/force owner.
                bool returned = _motionSystem.TryCommitStoppedGroundRelocation(_world, actor, point);
                effect.CompleteReturnAttempt(returned);
                if (returned) _combatTraceSystem.ResetPositionRecallWitnesses(actor.Id.Value);
            }
        }
    }
}
