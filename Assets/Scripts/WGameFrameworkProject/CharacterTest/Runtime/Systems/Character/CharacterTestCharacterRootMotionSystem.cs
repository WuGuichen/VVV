using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestCharacterRootMotionSystem
    {
        private const string LogCategory = "CharacterTest.RootMotion";
        private const float RootMotionDeltaEpsilon = 0.0000001f;

        private readonly IRuntimeLogger _logger;
        private readonly HashSet<string> _reportedErrors =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<int, RootMotionTraceSignature> _activeTraceSourceByEntity =
            new Dictionary<int, RootMotionTraceSignature>(16);
        private CharacterTestTraceRecorder _traceRecorder;

        public CharacterTestCharacterRootMotionSystem(IRuntimeLogger logger = null)
        {
            _logger = logger ?? NullRuntimeLogger.Instance;
        }

        public void SetTraceRecorder(CharacterTestTraceRecorder traceRecorder)
        {
            _traceRecorder = traceRecorder;
        }

        public void ClearCache()
        {
            _reportedErrors.Clear();
            _activeTraceSourceByEntity.Clear();
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            ICharacterTestStatusControlProvider statusControl = null)
        {
            if (world == null || actionRuntime == null || !actionRuntime.IsReady)
                return;

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.BattleState.LifeState == CharacterTestCharacterLifeState.Alive
                    && statusControl != null
                    && statusControl.IsActionFrozen(entity))
                {
                    ClearTraceSource(entity);
                    continue;
                }

                TickEntity(entity, actionRuntime, frame);
            }
        }

        private void TickEntity(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame)
        {
            if (entity == null || !IsRunningAction(entity.Action))
            {
                ClearTraceSource(entity);
                return;
            }

            if (!actionRuntime.Registry.TryGetAction(entity.Action.ActiveActionId, out CharacterActionConfig action))
                return;

            MotionTrackConfig motionTrack = action.MotionTrack;
            if (motionTrack == null)
            {
                ClearTraceSource(entity);
                return;
            }

            int localFrame = entity.Action.LocalFrame;
            if (motionTrack.IsVerticalRootMotionActive(localFrame))
            {
                if (!motionTrack.TryGetVerticalRootMotionDelta(localFrame, out float verticalDelta))
                {
                    ReportRootMotionError(
                        frame, entity, actionRuntime.ActionsSourcePath, action.ActionId,
                        motionTrack.VerticalRootMotion?.SourceAnimationClipResourceKey ?? string.Empty,
                        motionTrack.VerticalRootMotion?.SourceAnimationEventId ?? 0,
                        "VerticalRootMotionFrameMissing",
                        "SampleRootMotionY has no baked sample at actionFrame=" + localFrame.ToString(CultureInfo.InvariantCulture));
                    return;
                }
                if (verticalDelta != 0f)
                {
                    entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.RootMotionDelta(
                        new Vector3(0f, verticalDelta, 0f),
                        CharacterTestCharacterMotionRequestSpace.World,
                        action.ActionId,
                        motionTrack.VerticalRootMotion.SourceAnimationEventId).WithThirdSource(entity.CurrentThirdAttack, TacticalCard.TacticalThirdBatchMotionChannel.RootTranslation));
                }
            }
            if (!motionTrack.UsesRootMotion)
            {
                ClearTraceSource(entity);
                return;
            }

            CharacterActionRootMotionConfig rootMotion = motionTrack.RootMotion;
            if (rootMotion == null || !rootMotion.HasFrames)
            {
                ReportRootMotionError(
                    frame,
                    entity,
                    actionRuntime.ActionsSourcePath,
                    action.ActionId,
                    string.Empty,
                    0,
                    "RootMotionDataMissing",
                    "Action requests RootMotion but motionTrack.rootMotion.frames is empty.");
                return;
            }

            AnimationTrackEvent activeAnimationEvent = default;
            bool hasActiveAnimationEvent = TryResolveRootMotionAnimationEvent(
                action,
                rootMotion,
                entity.Action.LocalFrame,
                out activeAnimationEvent,
                out bool sourceEventConfiguredButMissing);
            if (sourceEventConfiguredButMissing)
            {
                ReportRootMotionError(
                    frame,
                    entity,
                    actionRuntime.ActionsSourcePath,
                    action.ActionId,
                    rootMotion.SourceAnimationClipResourceKey,
                    rootMotion.SourceAnimationEventId,
                    "RootMotionSourceEventMissing",
                    "motionTrack.rootMotion.sourceAnimationEventId does not match any animation event.");
                return;
            }

            if (!hasActiveAnimationEvent && rootMotion.SourceAnimationEventId != 0)
            {
                ClearTraceSource(entity);
                return;
            }

            string clipKey = ResolveDiagnosticClipKey(rootMotion, hasActiveAnimationEvent, activeAnimationEvent);
            if (hasActiveAnimationEvent
                && !string.IsNullOrWhiteSpace(rootMotion.SourceAnimationClipResourceKey)
                && !string.IsNullOrWhiteSpace(activeAnimationEvent.AnimationClipResourceKey)
                && !string.Equals(rootMotion.SourceAnimationClipResourceKey, activeAnimationEvent.AnimationClipResourceKey, StringComparison.Ordinal))
            {
                ReportRootMotionError(
                    frame,
                    entity,
                    actionRuntime.ActionsSourcePath,
                    action.ActionId,
                    clipKey,
                    rootMotion.SourceAnimationEventId,
                    "RootMotionSourceClipMismatch",
                    "RootMotion source clip does not match active animation event. activeClip="
                    + EmptyAsNone(activeAnimationEvent.AnimationClipResourceKey));
                return;
            }

            if (localFrame < rootMotion.StartFrame || localFrame > rootMotion.EndFrame)
            {
                ClearTraceSource(entity);
                return;
            }

            if (!rootMotion.TryGetDelta(localFrame, out float x, out float y, out float z))
            {
                ReportRootMotionError(
                    frame,
                    entity,
                    actionRuntime.ActionsSourcePath,
                    action.ActionId,
                    clipKey,
                    rootMotion.SourceAnimationEventId,
                    "RootMotionFrameMissing",
                    "RootMotion sample frame is missing. actionFrame="
                    + localFrame.ToString(CultureInfo.InvariantCulture));
                return;
            }

            var delta = new Vector3(x, y, z);
            TraceRootMotionSource(frame, entity, action.ActionId, rootMotion, clipKey);
            if (delta.sqrMagnitude <= RootMotionDeltaEpsilon)
                return;

            entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.RootMotionDelta(
                delta,
                rootMotion.Space == CharacterActionRootMotionSpace.World
                    ? CharacterTestCharacterMotionRequestSpace.World
                    : CharacterTestCharacterMotionRequestSpace.Local,
                action.ActionId,
                rootMotion.SourceAnimationEventId,
                ToMotionObstaclePolicy(rootMotion.ObstaclePolicy)).WithThirdSource(entity.CurrentThirdAttack, TacticalCard.TacticalThirdBatchMotionChannel.RootTranslation));
        }

        private static CharacterMotionObstaclePolicy ToMotionObstaclePolicy(
            CharacterActionRootMotionObstaclePolicy policy)
        {
            return policy == CharacterActionRootMotionObstaclePolicy.StaticOnly
                ? CharacterMotionObstaclePolicy.StaticOnly
                : CharacterMotionObstaclePolicy.Default;
        }

        private static bool TryResolveRootMotionAnimationEvent(
            CharacterActionConfig action,
            CharacterActionRootMotionConfig rootMotion,
            int localFrame,
            out AnimationTrackEvent activeEvent,
            out bool sourceEventConfiguredButMissing)
        {
            activeEvent = default;
            sourceEventConfiguredButMissing = false;
            AnimationTrackEvent[] events = action.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEvent>();
            int sourceEventId = rootMotion.SourceAnimationEventId;
            bool hasSourceEvent = false;

            for (int i = 0; i < events.Length; i++)
            {
                AnimationTrackEvent candidate = events[i];
                if (sourceEventId != 0
                    && candidate.StableEventId != sourceEventId)
                {
                    continue;
                }

                hasSourceEvent = true;
                if (localFrame < candidate.Frame || localFrame > candidate.EndFrame)
                    continue;

                if (activeEvent.StableEventId == 0 || candidate.Frame >= activeEvent.Frame)
                    activeEvent = candidate;
            }

            if (sourceEventId != 0 && !hasSourceEvent)
            {
                sourceEventConfiguredButMissing = true;
                return false;
            }

            return activeEvent.StableEventId != 0;
        }

        private void TraceRootMotionSource(
            RuntimeFrame frame,
            CharacterTestCharacterEntity entity,
            CharacterActionId actionId,
            CharacterActionRootMotionConfig rootMotion,
            string clipKey)
        {
            var source = new RootMotionTraceSignature(
                actionId,
                clipKey,
                rootMotion.SourceAnimationEventId,
                rootMotion.StartFrame,
                rootMotion.EndFrame);
            if (_activeTraceSourceByEntity.TryGetValue(entity.Id.Value, out RootMotionTraceSignature previousSource)
                && previousSource.Equals(source))
            {
                return;
            }

            _activeTraceSourceByEntity[entity.Id.Value] = source;
            _traceRecorder?.LocomotionRootMotionActive(
                frame,
                entity.Id.Value,
                new CharacterTestRootMotionActiveTraceEvent(
                    actionId,
                    clipKey,
                    rootMotion.StartFrame,
                    rootMotion.EndFrame));
        }

        private void ClearTraceSource(CharacterTestCharacterEntity entity)
        {
            if (entity != null)
                _activeTraceSourceByEntity.Remove(entity.Id.Value);
        }

        private void ReportRootMotionError(
            RuntimeFrame frame,
            CharacterTestCharacterEntity entity,
            string sourcePath,
            CharacterActionId actionId,
            string clipKey,
            int stableEventId,
            string code,
            string message)
        {
            string key = code
                + "|"
                + (entity != null ? entity.Id.Value.ToString(CultureInfo.InvariantCulture) : "0")
                + "|"
                + actionId
                + "|"
                + (clipKey ?? string.Empty)
                + "|"
                + stableEventId
                + "|"
                + NormalizePath(sourcePath);
            if (!_reportedErrors.Add(key))
                return;

            string formatted = code
                + " action="
                + EmptyAsNone(actionId)
                + " clipKey="
                + EmptyAsNone(clipKey)
                + " event="
                + EmptyAsNone(stableEventId)
                + " entity="
                + (entity != null ? entity.Id.Value.ToString(CultureInfo.InvariantCulture) : "none")
                + " source="
                + EmptyAsNone(NormalizePath(sourcePath))
                + " frame="
                + frame.Value.ToString(CultureInfo.InvariantCulture)
                + " "
                + (message ?? string.Empty);
            _logger.Error(LogCategory, formatted);
            if (entity != null)
                _traceRecorder?.Locomotion(frame, entity.Id.Value, formatted, warning: true);
        }

        private static string ResolveDiagnosticClipKey(
            CharacterActionRootMotionConfig rootMotion,
            bool hasActiveAnimationEvent,
            AnimationTrackEvent activeAnimationEvent)
        {
            if (!string.IsNullOrWhiteSpace(rootMotion.SourceAnimationClipResourceKey))
                return rootMotion.SourceAnimationClipResourceKey;
            return hasActiveAnimationEvent ? activeAnimationEvent.EffectiveAnimationKey : string.Empty;
        }

        private static bool IsRunningAction(CharacterTestCharacterActionComponent action)
        {
            return action.ActiveActionId.IsValid
                && (action.State == CharacterTestActionStateId.Running);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/');
        }

        private static string EmptyAsNone(int value) => value == 0 ? "none" : value.ToString(CultureInfo.InvariantCulture);

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }

        private static string EmptyAsNone(CharacterActionId value)
        {
            return value.IsValid ? value.ToString() : "none";
        }

        private readonly struct RootMotionTraceSignature : IEquatable<RootMotionTraceSignature>
        {
            private readonly CharacterActionId _actionId;
            private readonly string _clipKey;
            private readonly int _eventId;
            private readonly int _startFrame;
            private readonly int _endFrame;

            public RootMotionTraceSignature(
                CharacterActionId actionId,
                string clipKey,
                int eventId,
                int startFrame,
                int endFrame)
            {
                _actionId = actionId;
                _clipKey = clipKey ?? string.Empty;
                _eventId = eventId;
                _startFrame = startFrame;
                _endFrame = endFrame;
            }

            public bool Equals(RootMotionTraceSignature other)
            {
                return _startFrame == other._startFrame
                    && _endFrame == other._endFrame
                    && _actionId == other._actionId
                    && string.Equals(_clipKey, other._clipKey, StringComparison.Ordinal)
                    && _eventId == other._eventId;
            }

            public override bool Equals(object obj)
            {
                return obj is RootMotionTraceSignature other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + _actionId.GetHashCode();
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_clipKey);
                    hash = hash * 31 + _eventId;
                    hash = hash * 31 + _startFrame;
                    hash = hash * 31 + _endFrame;
                    return hash;
                }
            }
        }
    }
}
