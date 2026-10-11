using System;
using MxFramework.Combat.Hit;
using static MxFramework.CharacterAction.CharacterActionAdapterShared;

namespace MxFramework.CharacterAction
{
    public sealed class CharacterActionTrackAdapter
    {
        private readonly ICharacterActionMotionRequestSink _motionSink;
        private readonly ICharacterActionRootMotionRequestSink _rootMotionSink;
        private readonly ICharacterActionCombatRequestSink _combatSink;
        private readonly ICharacterActionGameplayRequestSink _gameplaySink;
        private readonly ICharacterActionPressureOnlyReactionRequestSink _pressureOnlyReactionSink;
        private readonly ICharacterActionDebugRequestSink _debugSink;

        public CharacterActionTrackAdapter(
            ICharacterActionMotionRequestSink motionSink = null,
            ICharacterActionCombatRequestSink combatSink = null,
            ICharacterActionGameplayRequestSink gameplaySink = null,
            ICharacterActionPressureOnlyReactionRequestSink pressureOnlyReactionSink = null,
            ICharacterActionRootMotionRequestSink rootMotionSink = null,
            ICharacterActionDebugRequestSink debugSink = null)
        {
            _motionSink = motionSink;
            _rootMotionSink = rootMotionSink ?? motionSink as ICharacterActionRootMotionRequestSink;
            _combatSink = combatSink;
            _gameplaySink = gameplaySink;
            _pressureOnlyReactionSink = pressureOnlyReactionSink;
            _debugSink = debugSink;
        }

        public bool HasDebugSink => _debugSink != null;

        /// <remarks>NoAlloc after the caller-owned buffer and downstream sinks are warm.</remarks>
        public CharacterActionAdapterResult Adapt(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionAdapterContext context,
            CharacterActionDispatchBuffer buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            buffer.Clear();

            int motionCount = 0;
            int combatCount = 0;
            int gameplayCount = 0;
            int debugCount = 0;
            AdaptOne(
                runnerEvent,
                context,
                buffer,
                ref motionCount,
                ref combatCount,
                ref gameplayCount,
                ref debugCount);
            return new CharacterActionAdapterResult(
                motionCount,
                combatCount,
                gameplayCount,
                0,
                buffer,
                debugCount);
        }

        /// <remarks>NoAlloc after the caller-owned buffer and downstream sinks are warm.</remarks>
        public CharacterActionAdapterResult AdaptMany(
            CharacterActionRunnerEvent[] runnerEvents,
            CharacterActionAdapterContext context,
            CharacterActionDispatchBuffer buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            buffer.Clear();
            runnerEvents = runnerEvents ?? Array.Empty<CharacterActionRunnerEvent>();

            int motionCount = 0;
            int combatCount = 0;
            int gameplayCount = 0;
            int debugCount = 0;
            for (int i = 0; i < runnerEvents.Length; i++)
            {
                AdaptOne(
                    runnerEvents[i],
                    context,
                    buffer,
                    ref motionCount,
                    ref combatCount,
                    ref gameplayCount,
                    ref debugCount);
            }

            return new CharacterActionAdapterResult(
                motionCount,
                combatCount,
                gameplayCount,
                0,
                buffer,
                debugCount);
        }

        /// <remarks>NoAlloc after the caller-owned buffer and downstream sink are warm.</remarks>
        public CharacterActionAdapterResult SubmitPressureOnlyReaction(
            CharacterReactionContext reactionContext,
            CharacterActionDispatchBuffer buffer,
            CharacterActionId requestedActionId = default)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            buffer.Clear();

            if (reactionContext.Completeness != CharacterReactionContextCompleteness.PressureOnly)
            {
                buffer.Add(CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.ReactionContextIncomplete,
                    "PressureOnly reaction request requires PressureOnly context."));
                return new CharacterActionAdapterResult(0, 0, 0, 0, buffer);
            }

            if (!reactionContext.EntityId.IsValid)
            {
                buffer.Add(PayloadMissing("PressureOnly reaction request requires a gameplay entity id."));
                return new CharacterActionAdapterResult(0, 0, 0, 0, buffer);
            }

            if (_pressureOnlyReactionSink == null)
            {
                buffer.Add(MissingSink("PressureOnlyReaction"));
                return new CharacterActionAdapterResult(0, 0, 0, 0, buffer);
            }

            var request = new CharacterActionPressureOnlyReactionRequest(reactionContext, requestedActionId);
            CharacterActionAdapterSinkResult sinkResult;
            try
            {
                sinkResult = _pressureOnlyReactionSink.SubmitPressureOnlyReactionRequest(request);
            }
            catch (Exception ex)
            {
                sinkResult = SinkFailure("PressureOnlyReaction", ex);
            }

            buffer.AddRange(in sinkResult);
            return new CharacterActionAdapterResult(0, 0, 0, sinkResult.Accepted ? 1 : 0, buffer);
        }

        /// <remarks>NoAlloc after initialization on accepted submissions.</remarks>
        public CharacterActionAdapterSinkResult SubmitRootMotionRequest(CharacterActionRootMotionRequest request)
        {
            if (_rootMotionSink == null)
                return CharacterActionAdapterSinkResult.Rejected(MissingSink("RootMotion"));

            try
            {
                return _rootMotionSink.SubmitRootMotionRequest(request);
            }
            catch (Exception ex)
            {
                return SinkFailure("RootMotion", ex);
            }
        }

        private void AdaptOne(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionAdapterContext context,
            CharacterActionDispatchBuffer buffer,
            ref int motionCount,
            ref int combatCount,
            ref int gameplayCount,
            ref int debugCount)
        {
            if ((runnerEvent.Kind != CharacterActionRunnerEventKind.TrackEventFired
                    && runnerEvent.Kind != CharacterActionRunnerEventKind.TrackLifecycle)
                || !runnerEvent.TrackDispatch.HasEvent)
                return;

            CharacterActionTrackDispatchEvent dispatch = runnerEvent.TrackDispatch;
            switch (dispatch.TrackKind)
            {
                case CharacterActionTrackKind.Motion:
                    if (TryAdaptMotion(runnerEvent, dispatch, context, buffer)) motionCount++;
                    break;
                case CharacterActionTrackKind.Combat:
                    if (TryAdaptCombat(runnerEvent, dispatch, context, buffer)) combatCount++;
                    break;
                case CharacterActionTrackKind.Gameplay:
                    if (TryAdaptGameplay(runnerEvent, dispatch, context, buffer)) gameplayCount++;
                    break;
                case CharacterActionTrackKind.Debug:
                    if (TryAdaptDebug(runnerEvent, dispatch, context, buffer)) debugCount++;
                    break;
            }
        }

        private bool TryAdaptMotion(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionAdapterContext context,
            CharacterActionDispatchBuffer diagnostics)
        {
            // Vertical root samples are dispatched through the action's baked motion stream.
            if (dispatch.EventKind == CharacterActionTrackEventKind.SampleRootMotionY)
                return true;
            if ((dispatch.EventKind == CharacterActionTrackEventKind.SetVerticalVelocity
                    || dispatch.EventKind == CharacterActionTrackEventKind.IgnoreGravity)
                && runnerEvent.TrackLifecycle.HasEvent
                && runnerEvent.TrackLifecycle.Phase != CharacterActionTrackLifecyclePhase.Active)
                return true;
            if (_motionSink == null)
            {
                diagnostics.Add(MissingSink("Motion"));
                return false;
            }
            if (!context.HasGameplayEntity && !context.HasCombatBody)
            {
                diagnostics.Add(PayloadMissing("Motion request requires a gameplay entity id or combat body id."));
                return false;
            }
            if (dispatch.EventKind == CharacterActionTrackEventKind.ApplyImpulse
                && dispatch.X.Equals(0f) && dispatch.Y.Equals(0f) && dispatch.Z.Equals(0f))
            {
                diagnostics.Add(PayloadMissing("Motion ApplyImpulse request requires a non-zero vector."));
                return false;
            }

            var request = new CharacterActionMotionRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.GameplayEntityId,
                context.CombatBodyId,
                dispatch.EventKind,
                dispatch.MovementMode,
                dispatch.X,
                dispatch.Y,
                dispatch.Z,
                dispatch.MovementControlScale,
                dispatch.RotationControlScale,
                dispatch.MotionVectorSpace,
                dispatch.HasMovementControlScale,
                dispatch.HasRotationControlScale,
                dispatch.CollisionProfileId,
                dispatch.CollisionRestorePolicy,
                dispatch.CollisionTransitionFrames);
            CharacterActionAdapterSinkResult result;
            try { result = _motionSink.SubmitMotionRequest(request); }
            catch (Exception ex) { result = SinkFailure("Motion", ex); }
            diagnostics.AddRange(in result);
            return result.Accepted;
        }

        private bool TryAdaptCombat(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionAdapterContext context,
            CharacterActionDispatchBuffer diagnostics)
        {
            if (_combatSink == null)
            {
                diagnostics.Add(MissingSink("Combat"));
                return false;
            }
            if (!context.HasCombatEntity)
            {
                diagnostics.Add(PayloadMissing("Combat request requires a combat entity id."));
                return false;
            }
            if (dispatch.EventKind == CharacterActionTrackEventKind.StartCombatAction
                && (dispatch.CombatActionId <= 0))
            {
                diagnostics.Add(PayloadMissing("Combat StartCombatAction request requires a combat action id."));
                return false;
            }
            if (dispatch.EventKind == CharacterActionTrackEventKind.HitTraceWindow
                && (dispatch.TraceProfileId <= 0)
                && (dispatch.EquipmentSlotId <= 0)
                && !dispatch.HitVolume.HasVolume)
            {
                diagnostics.Add(PayloadMissing("Combat HitTraceWindow requires an equipment slot id, trace profile id, or authored hit volume."));
                return false;
            }
            if (dispatch.EventKind == CharacterActionTrackEventKind.SetHitTargetState
                && dispatch.TargetStateFlags == HitTargetStateFlags.None)
            {
                diagnostics.Add(PayloadMissing("Combat SetHitTargetState requires a non-zero target-state mask."));
                return false;
            }

            CharacterActionCombatRequest request =
                dispatch.EventKind == CharacterActionTrackEventKind.SetHitTargetState
                    ? new CharacterActionCombatRequest(
                        CreateMetadata(runnerEvent, dispatch, context),
                        context.CombatEntityId,
                        dispatch.EventKind,
                        dispatch.TargetStateFlags)
                    : new CharacterActionCombatRequest(
                        CreateMetadata(runnerEvent, dispatch, context),
                        context.CombatEntityId,
                        dispatch.EventKind,
                        dispatch.CombatActionId,
                        dispatch.TraceProfileId,
                        dispatch.EquipmentSlotId,
                        dispatch.DamagePayload,
                        dispatch.HitEvent,
                        dispatch.HitVolume);
            CharacterActionAdapterSinkResult result;
            try { result = _combatSink.SubmitCombatRequest(request); }
            catch (Exception ex) { result = SinkFailure("Combat", ex); }
            diagnostics.AddRange(in result);
            return result.Accepted;
        }

        private bool TryAdaptGameplay(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionAdapterContext context,
            CharacterActionDispatchBuffer diagnostics)
        {
            if (_gameplaySink == null)
            {
                diagnostics.Add(MissingSink("Gameplay"));
                return false;
            }
            if (!context.HasGameplayEntity)
            {
                diagnostics.Add(PayloadMissing("Gameplay request requires a gameplay entity id."));
                return false;
            }
            if (dispatch.EventKind == CharacterActionTrackEventKind.SendGameplayRequest
                && (dispatch.GameplayRequestId <= 0))
            {
                diagnostics.Add(PayloadMissing("Gameplay SendGameplayRequest track event requires a request id."));
                return false;
            }
            if (dispatch.EventKind == CharacterActionTrackEventKind.ApplyGameplayEffect
                && (dispatch.GameplayRequestId <= 0)
                && !dispatch.DamagePayload.HasInlineData)
            {
                diagnostics.Add(PayloadMissing("Gameplay ApplyGameplayEffect track event requires a request id or hitEvent.damage payload."));
                return false;
            }
            if (dispatch.EventKind == CharacterActionTrackEventKind.CastAbility
                && (dispatch.AbilityStableId <= 0))
            {
                diagnostics.Add(PayloadMissing("Gameplay CastAbility request requires an ability stable id."));
                return false;
            }
            if (dispatch.EventKind == CharacterActionTrackEventKind.StartEffectTimeline
                && (dispatch.EffectTimelineId <= 0))
            {
                diagnostics.Add(PayloadMissing("Gameplay StartEffectTimeline request requires an effect timeline id."));
                return false;
            }

            var request = new CharacterActionGameplayRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                context.GameplayEntityId,
                dispatch.EventKind,
                dispatch.GameplayRequestId,
                dispatch.AbilityStableId,
                dispatch.TargetPolicyId,
                dispatch.EffectTimelineId,
                dispatch.DamagePayload,
                dispatch.HitEvent);
            CharacterActionAdapterSinkResult result;
            try { result = _gameplaySink.SubmitGameplayRequest(request); }
            catch (Exception ex) { result = SinkFailure("Gameplay", ex); }
            diagnostics.AddRange(in result);
            return result.Accepted;
        }

        private bool TryAdaptDebug(
            CharacterActionRunnerEvent runnerEvent,
            CharacterActionTrackDispatchEvent dispatch,
            CharacterActionAdapterContext context,
            CharacterActionDispatchBuffer diagnostics)
        {
            if (_debugSink == null)
            {
                diagnostics.Add(MissingSink("Debug"));
                return false;
            }
            if (string.IsNullOrEmpty(dispatch.DebugMarkerId)
                && (dispatch.StableEventId <= 0))
            {
                diagnostics.Add(PayloadMissing("Debug EmitDebugMarker request requires a marker id or stable event id."));
                return false;
            }

            var request = new CharacterActionDebugRequest(
                CreateMetadata(runnerEvent, dispatch, context),
                dispatch.EventKind,
                dispatch.DebugMarkerId);
            CharacterActionAdapterSinkResult result;
            try { result = _debugSink.SubmitDebugRequest(request); }
            catch (Exception ex) { result = SinkFailure("Debug", ex); }
            diagnostics.AddRange(in result);
            return result.Accepted;
        }

        private static CharacterActionAdapterSinkResult SinkFailure(string sinkName, Exception exception)
        {
            return CharacterActionAdapterSinkResult.Rejected(CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCode.AdapterSinkFailure,
                sinkName + " sink failed: " + exception.Message));
        }
    }
}
