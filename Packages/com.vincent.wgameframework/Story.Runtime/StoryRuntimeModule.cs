using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Story.Runtime
{
    public sealed class StoryRuntimeModule : RuntimeModule
    {
        public const string DefaultModuleId = "mxframework.story.runtime";
        public const int DefaultPriority = -100;
        public const int DefaultRecentEventCapacity = 32;

        private readonly List<RuntimeCommand> _lastDrainedCommands = new List<RuntimeCommand>();
        private readonly List<RuntimeCommandError> _lastCommandErrors = new List<RuntimeCommandError>();
        private readonly List<StoryRuntimeEvent> _recentEvents = new List<StoryRuntimeEvent>();
        private readonly IDisposable _eventSubscription;
        private readonly IStoryRuntimeCommandResultSink _commandResultSink;
        private RuntimeFrame _currentFrame;

        public StoryRuntimeModule()
            : this(new StoryDirector())
        {
        }

        public StoryRuntimeModule(
            StoryDirector director,
            RuntimeCommandBuffer commandBuffer = null,
            RuntimeEventQueue<StoryRuntimeEvent> events = null,
            StoryRuntimeCommandValidator validator = null)
            : this(director, commandBuffer, events, validator, null)
        {
        }

        /// <remarks>AllocByDesign during composition-root initialization.</remarks>
        public StoryRuntimeModule(
            StoryDirector director,
            RuntimeCommandBuffer commandBuffer,
            RuntimeEventQueue<StoryRuntimeEvent> events,
            StoryRuntimeCommandValidator validator,
            IStoryRuntimeCommandResultSink commandResultSink)
            : base(DefaultModuleId, RuntimeTickStage.Simulation, DefaultPriority)
        {
            Director = director ?? throw new ArgumentNullException(nameof(director));
            CommandBuffer = commandBuffer ?? new RuntimeCommandBuffer();
            Events = events ?? new RuntimeEventQueue<StoryRuntimeEvent>();
            Validator = validator ?? new StoryRuntimeCommandValidator(Director);
            _commandResultSink = commandResultSink;
            _eventSubscription = Director.SubscribeEvents(OnStoryEvent);
        }

        public StoryDirector Director { get; }
        public RuntimeCommandBuffer CommandBuffer { get; }
        public RuntimeEventQueue<StoryRuntimeEvent> Events { get; }
        public StoryRuntimeCommandValidator Validator { get; }
        public IReadOnlyList<RuntimeCommand> LastDrainedCommands => _lastDrainedCommands;
        public IReadOnlyList<RuntimeCommandError> LastCommandErrors => _lastCommandErrors;
        public IReadOnlyList<StoryRuntimeEvent> RecentEvents => _recentEvents;

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            _currentFrame = new RuntimeFrame(context.FrameIndex);
            _lastDrainedCommands.Clear();
            _lastCommandErrors.Clear();

            CommandBuffer.DrainForFrame(_currentFrame, _lastDrainedCommands);
            for (int i = 0; i < _lastDrainedCommands.Count; i++)
            {
                RuntimeCommand command = _lastDrainedCommands[i];
                RuntimeCommandValidationResult validation = Validator.Validate(command);
                if (!validation.Success)
                {
                    _lastCommandErrors.Add(validation.Error);
                    continue;
                }

                ApplyCommand(command);
            }

            Director.Tick(new StoryTickContext(context.FrameIndex, context.DeltaTime));
        }

        public override void Dispose()
        {
            _eventSubscription.Dispose();
        }

        private void ApplyCommand(RuntimeCommand command)
        {
            switch (command.CommandId)
            {
                case StoryRuntimeCommandIds.RaiseTrigger:
                    StoryTriggerResult triggerResult = Director.TryRaiseTrigger(
                        command.Payload0,
                        new StoryActivationContext(
                            command.Frame.Value,
                            command.SourceId,
                            command.Payload0,
                            command.Payload1,
                            command.Payload2,
                            command.TargetId));
                    AddDirectorError(command, triggerResult);
                    PublishCommandResult(command, triggerResult);
                    break;
                case StoryRuntimeCommandIds.SelectChoice:
                    AddDirectorError(command, Director.TryResolveChoice(command.Payload0, command.Payload1));
                    break;
                case StoryRuntimeCommandIds.CompletePresentation:
                    AddDirectorError(command, Director.CompletePresentation(command.Payload0, command.Payload1));
                    break;
                case StoryRuntimeCommandIds.RequestEnterBeat:
                    AddDirectorError(command, Director.TryEnterBeat(
                        command.Payload0,
                        command.Payload1,
                        new StoryActivationContext(command.Frame.Value, command.SourceId)));
                    break;
                case StoryRuntimeCommandIds.AbortGraph:
                    AddDirectorError(command, Director.AbortGraph(command.Payload0, command.Payload1));
                    break;
            }
        }

        private void PublishCommandResult(
            RuntimeCommand command,
            StoryTriggerResult triggerResult)
        {
            if (_commandResultSink == null || !command.Envelope.IsPresent)
                return;

            StoryRuntimeCommandResultStatus status = triggerResult.Success
                ? StoryRuntimeCommandResultStatus.Succeeded
                : StoryRuntimeCommandResultStatus.Rejected;
            long resultPayload0 = triggerResult.Success
                ? triggerResult.GraphId
                : triggerResult.TriggerId;
            long resultPayload1 = triggerResult.Success ? triggerResult.BeatId : 0L;
            long resultPayload2 = triggerResult.Success ? triggerResult.BeatInstanceId : 0L;
            var result = new StoryRuntimeCommandResult(
                _currentFrame,
                command.SourceId,
                command.CommandId,
                command.TargetId,
                command.Payload0,
                command.Payload1,
                command.Payload2,
                command.Sequence,
                command.TraceId.IsNone,
                command.Envelope,
                status,
                triggerResult.Code,
                resultPayload0,
                resultPayload1,
                resultPayload2);
            _commandResultSink.Publish(in result);
        }

        private void OnStoryEvent(StoryEvent evt)
        {
            StoryRuntimeEvent runtimeEvent = StoryRuntimeEvent.FromStoryEvent(_currentFrame, evt);
            Events.Enqueue(_currentFrame, runtimeEvent);
            RecordRecentEvent(runtimeEvent);
        }

        private void RecordRecentEvent(StoryRuntimeEvent evt)
        {
            _recentEvents.Add(evt);
            while (_recentEvents.Count > DefaultRecentEventCapacity)
            {
                _recentEvents.RemoveAt(0);
            }
        }

        private void AddDirectorError(RuntimeCommand command, StoryTriggerResult result)
        {
            if (!result.Success)
            {
                _lastCommandErrors.Add(ToCommandError(command, result.Code, result.Message));
            }
        }

        private void AddDirectorError(RuntimeCommand command, StoryEnterBeatResult result)
        {
            if (!result.Success)
            {
                _lastCommandErrors.Add(ToCommandError(command, result.Code, result.Message));
            }
        }

        private void AddDirectorError(RuntimeCommand command, StoryChoiceResult result)
        {
            if (!result.Success)
            {
                _lastCommandErrors.Add(ToCommandError(command, result.Code, result.Message));
            }
        }

        private void AddDirectorError(RuntimeCommand command, StoryPresentationResult result)
        {
            if (!result.Success)
            {
                _lastCommandErrors.Add(ToCommandError(command, result.Code, result.Message));
            }
        }

        private void AddDirectorError(RuntimeCommand command, StoryAbortResult result)
        {
            if (!result.Success)
            {
                _lastCommandErrors.Add(ToCommandError(command, result.Code, result.Message));
            }
        }

        private static RuntimeCommandError ToCommandError(RuntimeCommand command, StoryDirectorResultCode code, string message)
        {
            return new RuntimeCommandError(
                RuntimeCommandErrorCode.InvalidPayload,
                command,
                RuntimeFrame.Zero,
                "Story director rejected command: " + code + ". " + (message ?? string.Empty));
        }
    }
}


