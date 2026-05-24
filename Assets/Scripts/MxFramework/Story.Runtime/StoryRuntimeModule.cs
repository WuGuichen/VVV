using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Story.Runtime
{
    public sealed class StoryRuntimeModule : RuntimeModule
    {
        public const string DefaultModuleId = "mxframework.story.runtime";
        public const int DefaultPriority = -100;

        private readonly List<RuntimeCommand> _lastDrainedCommands = new List<RuntimeCommand>();
        private readonly List<RuntimeCommandError> _lastCommandErrors = new List<RuntimeCommandError>();
        private readonly IDisposable _eventSubscription;
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
            : base(DefaultModuleId, RuntimeTickStage.Simulation, DefaultPriority)
        {
            Director = director ?? throw new ArgumentNullException(nameof(director));
            CommandBuffer = commandBuffer ?? new RuntimeCommandBuffer();
            Events = events ?? new RuntimeEventQueue<StoryRuntimeEvent>();
            Validator = validator ?? new StoryRuntimeCommandValidator(Director);
            _eventSubscription = Director.Events.Subscribe(OnStoryEvent);
        }

        public StoryDirector Director { get; }
        public RuntimeCommandBuffer CommandBuffer { get; }
        public RuntimeEventQueue<StoryRuntimeEvent> Events { get; }
        public StoryRuntimeCommandValidator Validator { get; }
        public IReadOnlyList<RuntimeCommand> LastDrainedCommands => _lastDrainedCommands;
        public IReadOnlyList<RuntimeCommandError> LastCommandErrors => _lastCommandErrors;

        public override void Tick(RuntimeTickContext context)
        {
            _currentFrame = new RuntimeFrame(context.FrameIndex);
            _lastDrainedCommands.Clear();
            _lastCommandErrors.Clear();

            IReadOnlyList<RuntimeCommand> commands = CommandBuffer.DrainForFrame(_currentFrame);
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                _lastDrainedCommands.Add(command);
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
                    AddDirectorError(command, Director.TryRaiseTrigger(
                        command.Payload0,
                        new StoryActivationContext(
                            command.Frame.Value,
                            command.SourceId,
                            command.Payload0,
                            command.Payload1,
                            command.Payload2,
                            command.TargetId)));
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

        private void OnStoryEvent(StoryEvent evt)
        {
            Events.Enqueue(_currentFrame, StoryRuntimeEvent.FromStoryEvent(_currentFrame, evt));
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
