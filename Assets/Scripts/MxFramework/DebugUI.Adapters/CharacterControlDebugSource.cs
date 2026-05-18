using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.CharacterControl;
using MxFramework.Diagnostics;
using MxFramework.Runtime;

namespace MxFramework.DebugUI.Adapters
{
    public sealed class CharacterControlDebugSource : IFrameworkDebugSource, IDisposable
    {
        public const int DefaultMaxRecentEvents = 16;

        private readonly CharacterControlStateMachine _stateMachine;
        private readonly CharacterActionController _actionController;
        private readonly CharacterPressureReactionController _pressureReactionController;
        private readonly List<string> _recentEvents;
        private readonly int _maxRecentEvents;

        private CharacterStateChangedEvent _lastStateChangedEvent;
        private bool _hasLastStateChangedEvent;
        private CharacterControlEvent _lastControlEvent;
        private bool _hasLastControlEvent;
        private CharacterCommand _lastCommand;
        private bool _hasLastCommand;
        private CharacterMotionResult _lastMotionResult;
        private bool _hasLastMotionResult;
        private CharacterActionEvent _lastActionEvent;
        private bool _hasLastActionEvent;
        private CharacterPressureReactionEvent _lastPressureEvent;
        private bool _hasLastPressureEvent;
        private bool _disposed;

        public CharacterControlDebugSource(
            CharacterControlStateMachine stateMachine,
            string name = "CharacterControl",
            CharacterActionController actionController = null,
            CharacterPressureReactionController pressureReactionController = null,
            int maxRecentEvents = DefaultMaxRecentEvents)
        {
            _stateMachine = stateMachine;
            _actionController = actionController;
            _pressureReactionController = pressureReactionController;
            _maxRecentEvents = Math.Max(1, maxRecentEvents);
            _recentEvents = new List<string>(_maxRecentEvents);
            Name = string.IsNullOrWhiteSpace(name) ? "CharacterControl" : name;

            if (_stateMachine != null)
            {
                _stateMachine.StateChanged += RecordStateChangedEvent;
                _stateMachine.ControlEvent += RecordControlEvent;
            }

            if (_actionController != null)
                _actionController.ActionEvent += RecordActionEvent;
            if (_pressureReactionController != null)
                _pressureReactionController.ReactionEvent += RecordPressureEvent;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _stateMachine != null;

        public void RecordCommand(CharacterCommand command)
        {
            _lastCommand = command;
            _hasLastCommand = true;
            AppendRecent("Command frame=" + command.Frame
                + " source=" + command.SourceId
                + " move=" + command.MoveDirection
                + " action=" + command.ActionRequest.Kind
                + " trace=" + command.TraceId);
        }

        public void RecordMotionResult(CharacterMotionResult result)
        {
            _lastMotionResult = result;
            _hasLastMotionResult = true;
            _lastCommand = result.Command;
            _hasLastCommand = true;
            AppendRecent("Motion frame=" + result.Command.Frame
                + " state=" + result.ControlState
                + " position=" + result.Position
                + " grounded=" + FormatBool(result.Grounded)
                + " flags=" + result.CollisionFlags);
        }

        public void RecordStateChangedEvent(CharacterStateChangedEvent evt)
        {
            _lastStateChangedEvent = evt;
            _hasLastStateChangedEvent = true;
            AppendRecent("State frame=" + evt.Frame
                + " previous=" + evt.PreviousState
                + " current=" + evt.CurrentState
                + " reason=" + evt.Reason
                + " message=" + evt.Message);
        }

        public void RecordControlEvent(CharacterControlEvent evt)
        {
            _lastControlEvent = evt;
            _hasLastControlEvent = true;
            AppendRecent("Control frame=" + evt.Frame
                + " type=" + evt.Type
                + " state=" + evt.State
                + " requested=" + evt.RequestedState
                + " reason=" + evt.Reason
                + " message=" + evt.Message);
        }

        public void RecordActionEvent(CharacterActionEvent evt)
        {
            _lastActionEvent = evt;
            _hasLastActionEvent = true;
            AppendRecent("Action frame=" + evt.Request.Frame
                + " type=" + evt.Type
                + " kind=" + evt.Request.Kind
                + " rejected=" + evt.RejectedReason
                + " message=" + evt.Message);
        }

        public void RecordPressureEvent(CharacterPressureReactionEvent evt)
        {
            _lastPressureEvent = evt;
            _hasLastPressureEvent = true;
            AppendRecent("Pressure frame=" + evt.Frame
                + " type=" + evt.Type
                + " kind=" + evt.Kind
                + " band=" + evt.PreviousBand + "->" + evt.NewBand
                + " rejected=" + evt.RejectedReason
                + " message=" + evt.Message);
        }

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            if (_stateMachine == null)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("Status", "unavailable") });
            }

            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Status", "available"),
                    new FrameworkDebugSection("State", CreateStateSection()),
                    new FrameworkDebugSection("Last Command", CreateCommandSection()),
                    new FrameworkDebugSection("Motion", CreateMotionSection()),
                    new FrameworkDebugSection("Action", CreateActionSection()),
                    new FrameworkDebugSection("Pressure", CreatePressureSection()),
                    new FrameworkDebugSection("Recent Events", CreateRecentEventsSection())
                });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_stateMachine != null)
            {
                _stateMachine.StateChanged -= RecordStateChangedEvent;
                _stateMachine.ControlEvent -= RecordControlEvent;
            }

            if (_actionController != null)
                _actionController.ActionEvent -= RecordActionEvent;
            if (_pressureReactionController != null)
                _pressureReactionController.ReactionEvent -= RecordPressureEvent;

            _disposed = true;
        }

        private string CreateStateSection()
        {
            var builder = new StringBuilder();
            builder.Append("entity: ").Append(_stateMachine.Entity).Append('\n');
            builder.Append("state: ").Append(_stateMachine.CurrentState).Append('\n');
            builder.Append("version: ").Append(_stateMachine.Version).Append('\n');
            builder.Append("lockMask: ").Append(_stateMachine.ControlLockMask).Append('\n');
            builder.Append("lastCommandFrame: ").Append(_stateMachine.LastCommandFrame).Append('\n');

            if (_hasLastStateChangedEvent)
            {
                builder.Append("lastTransitionReason: ").Append(_lastStateChangedEvent.Reason).Append('\n');
                builder.Append("lastTransitionFrame: ").Append(_lastStateChangedEvent.Frame).Append('\n');
                builder.Append("lastTransitionPreviousState: ").Append(_lastStateChangedEvent.PreviousState).Append('\n');
                builder.Append("lastTransitionCurrentState: ").Append(_lastStateChangedEvent.CurrentState).Append('\n');
                builder.Append("lastTransitionMessage: ").Append(_lastStateChangedEvent.Message).Append('\n');
            }
            else
            {
                builder.Append("lastTransitionReason: None\n");
            }

            if (_hasLastControlEvent)
            {
                builder.Append("lastControlEventType: ").Append(_lastControlEvent.Type).Append('\n');
                builder.Append("lastControlEventReason: ").Append(_lastControlEvent.Reason).Append('\n');
                builder.Append("lastControlEventFrame: ").Append(_lastControlEvent.Frame).Append('\n');
                builder.Append("lastControlRequestedState: ").Append(_lastControlEvent.RequestedState).Append('\n');
                builder.Append("lastControlEventMessage: ").Append(_lastControlEvent.Message);
            }
            else
            {
                builder.Append("lastControlEventType: none");
            }

            return builder.ToString();
        }

        private string CreateCommandSection()
        {
            if (!_hasLastCommand)
                return "last: none\nstateMachineLastCommandFrame: " + _stateMachine.LastCommandFrame;

            var builder = new StringBuilder();
            AppendCommand(builder, _lastCommand);
            return builder.ToString();
        }

        private string CreateMotionSection()
        {
            if (!_hasLastMotionResult)
            {
                return "position: unavailable\n"
                    + "velocity: unavailable\n"
                    + "grounded: unavailable\n"
                    + "collisionFlags: unavailable\n"
                    + "moveSpeedScale: unavailable";
            }

            CharacterMotionResult result = _lastMotionResult;
            var builder = new StringBuilder();
            builder.Append("frame: ").Append(result.Command.Frame).Append('\n');
            builder.Append("state: ").Append(result.ControlState).Append('\n');
            builder.Append("lockMask: ").Append(result.LockMask).Append('\n');
            builder.Append("position: ").Append(result.Position).Append('\n');
            builder.Append("velocity: ").Append(result.Velocity).Append('\n');
            builder.Append("grounded: ").Append(FormatBool(result.Grounded)).Append('\n');
            builder.Append("collisionFlags: ").Append(result.CollisionFlags).Append('\n');
            builder.Append("desiredDelta: ").Append(result.DesiredDelta).Append('\n');
            builder.Append("appliedDelta: ").Append(result.AppliedDelta).Append('\n');
            builder.Append("jumpStarted: ").Append(FormatBool(result.JumpStarted)).Append('\n');
            builder.Append("moveDirection: ").Append(result.MotionInput.MoveDirection).Append('\n');
            builder.Append("moveSpeedScale: ").Append(result.MotionInput.MoveSpeedScale).Append('\n');
            builder.Append("modifierMoveSpeedScale: ").Append(result.ModifierResult.FinalMoveSpeedScale).Append('\n');
            builder.Append("worldSynced: ").Append(FormatBool(result.WorldSynced)).Append('\n');
            builder.Append("worldRevision: ").Append(result.WorldRevision);
            return builder.ToString();
        }

        private string CreateActionSection()
        {
            var builder = new StringBuilder();
            if (_actionController == null)
            {
                builder.Append("controller: unavailable\n");
                builder.Append("hasQueuedRequest: unavailable\n");
            }
            else
            {
                builder.Append("controller: available\n");
                builder.Append("hasQueuedRequest: ").Append(FormatBool(_actionController.HasQueuedRequest)).Append('\n');
            }

            if (_hasLastActionEvent)
            {
                builder.Append("lastEventType: ").Append(_lastActionEvent.Type).Append('\n');
                builder.Append("lastRejectedReason: ").Append(_lastActionEvent.RejectedReason).Append('\n');
                builder.Append("lastActionInstanceId: ").Append(_lastActionEvent.ActionInstanceId).Append('\n');
                builder.Append("lastRuntimeCommand: ").Append(FormatRuntimeCommand(_lastActionEvent.RuntimeCommand)).Append('\n');
                builder.Append("lastMessage: ").Append(_lastActionEvent.Message).Append('\n');
                builder.Append("lastRequest:\n");
                AppendCommandRequest(builder, _lastActionEvent.Request, "  ");
            }
            else
            {
                builder.Append("lastEventType: none");
            }

            return builder.ToString();
        }

        private string CreatePressureSection()
        {
            var builder = new StringBuilder();
            if (_pressureReactionController == null)
            {
                builder.Append("controller: unavailable\n");
                builder.Append("activeReaction: unavailable\n");
            }
            else
            {
                builder.Append("controller: available\n");
                builder.Append("activeReaction: ").Append(FormatBool(_pressureReactionController.HasActiveReaction)).Append('\n');
                builder.Append("activeReactionKind: ").Append(_pressureReactionController.ActiveReactionKind).Append('\n');
                builder.Append("activeReactionEndFrame: ").Append(_pressureReactionController.ActiveReactionEndFrame).Append('\n');
            }

            if (_hasLastPressureEvent)
            {
                builder.Append("lastEventType: ").Append(_lastPressureEvent.Type).Append('\n');
                builder.Append("lastKind: ").Append(_lastPressureEvent.Kind).Append('\n');
                builder.Append("lastFrame: ").Append(_lastPressureEvent.Frame).Append('\n');
                builder.Append("lastReactionEndFrame: ").Append(_lastPressureEvent.ReactionEndFrame).Append('\n');
                builder.Append("lastBand: ").Append(_lastPressureEvent.PreviousBand).Append(" -> ").Append(_lastPressureEvent.NewBand).Append('\n');
                builder.Append("lastRejectedReason: ").Append(_lastPressureEvent.RejectedReason).Append('\n');
                builder.Append("lastLockMask: ").Append(_lastPressureEvent.LockMask).Append('\n');
                builder.Append("lastMessage: ").Append(_lastPressureEvent.Message);
            }
            else
            {
                builder.Append("lastEventType: none");
            }

            return builder.ToString();
        }

        private string CreateRecentEventsSection()
        {
            if (_recentEvents.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < _recentEvents.Count; i++)
            {
                builder.Append(_recentEvents[i]);
                if (i + 1 < _recentEvents.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private void AppendRecent(string line)
        {
            if (_recentEvents.Count == _maxRecentEvents)
                _recentEvents.RemoveAt(0);

            _recentEvents.Add(line ?? string.Empty);
        }

        private static void AppendCommand(StringBuilder builder, CharacterCommand command)
        {
            builder.Append("frame: ").Append(command.Frame).Append('\n');
            builder.Append("sourceId: ").Append(command.SourceId).Append('\n');
            builder.Append("entity: ").Append(command.Entity).Append('\n');
            builder.Append("moveDirection: ").Append(command.MoveDirection).Append('\n');
            builder.Append("worldMoveDirection: ").Append(command.GetWorldMoveDirection()).Append('\n');
            builder.Append("jumpPressed: ").Append(FormatBool(command.JumpPressed)).Append('\n');
            builder.Append("sprintHeld: ").Append(FormatBool(command.SprintHeld)).Append('\n');
            builder.Append("actionButtons: ").Append(command.ActionButtons).Append('\n');
            builder.Append("moveSpeedScale: ").Append(command.MoveSpeedScale).Append('\n');
            builder.Append("traceId: ").Append(command.TraceId).Append('\n');
            builder.Append("actionRequest:\n");
            AppendCommandRequest(builder, command.ActionRequest, "  ");
        }

        private static void AppendCommandRequest(StringBuilder builder, CharacterActionRequest request, string indent)
        {
            builder.Append(indent).Append("frame: ").Append(request.Frame).Append('\n');
            builder.Append(indent).Append("sourceId: ").Append(request.SourceId).Append('\n');
            builder.Append(indent).Append("entity: ").Append(request.Entity).Append('\n');
            builder.Append(indent).Append("kind: ").Append(request.Kind).Append('\n');
            builder.Append(indent).Append("combatActionId: ").Append(request.CombatActionId).Append('\n');
            builder.Append(indent).Append("gameplayAbilityId: ").Append(request.GameplayAbilityId).Append('\n');
            builder.Append(indent).Append("targetGameplayEntityId: ").Append(request.TargetGameplayEntityId).Append('\n');
            builder.Append(indent).Append("forceStart: ").Append(FormatBool(request.ForceStart)).Append('\n');
            builder.Append(indent).Append("queueIfBusy: ").Append(FormatBool(request.QueueIfBusy)).Append('\n');
            builder.Append(indent).Append("traceId: ").Append(request.TraceId);
        }

        private static string FormatRuntimeCommand(RuntimeCommand command)
        {
            return command.CommandId == 0
                ? "none"
                : "frame=" + command.Frame
                    + " source=" + command.SourceId
                    + " commandId=" + command.CommandId
                    + " target=" + command.TargetId
                    + " sequence=" + command.Sequence
                    + " trace=" + command.TraceId;
        }

        private static string FormatBool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
