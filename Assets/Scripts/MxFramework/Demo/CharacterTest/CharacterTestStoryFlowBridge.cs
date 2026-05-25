using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;

namespace MxFramework.Demo.CharacterTest
{
    public sealed class CharacterTestStoryFlowBridge : IDisposable
    {
        private readonly StoryRuntimeModule _storyModule;
        private readonly CharacterTestResourceServices _resources;
        private readonly IRuntimeLogger _logger;
        private readonly Func<RuntimeFrame> _currentFrameProvider;
        private readonly IDisposable _subscription;
        private readonly RuntimeLogBuffer _logBuffer = new RuntimeLogBuffer(128);
        private bool _disposed;

        public CharacterTestStoryFlowBridge(
            StoryRuntimeModule storyModule,
            CharacterTestResourceServices resources,
            IRuntimeLogger logger,
            Func<RuntimeFrame> currentFrameProvider)
        {
            _storyModule = storyModule ?? throw new ArgumentNullException(nameof(storyModule));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _logger = logger ?? NullRuntimeLogger.Instance;
            _currentFrameProvider = currentFrameProvider ?? (() => RuntimeFrame.Zero);
            _subscription = _storyModule.Director.SubscribeEvents(OnStoryEvent);
            _logger.Info("StoryFlow", "Bridge attached");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _subscription.Dispose();
            _disposed = true;
        }

        private void OnStoryEvent(StoryEvent evt)
        {
            if (evt.Kind != StoryEventKind.StepStarted)
                return;

            switch (evt.AuxId)
            {
                case CharacterTestStoryIds.Actions.OpenLoadingUi:
                    OpenLoadingUi(evt);
                    break;
                case CharacterTestStoryIds.Actions.LoadBaseResources:
                    LoadBaseResources(evt);
                    break;
                case CharacterTestStoryIds.Actions.OpenStartupUi:
                    OpenStartupUi(evt);
                    break;
            }
        }

        private void OpenLoadingUi(StoryEvent evt)
        {
            _logBuffer.Clear()
                .Append("OpenLoadingUi requested. stepId=")
                .Append(evt.StepId);
            _logger.Info("StoryFlow", _logBuffer);
        }

        private void LoadBaseResources(StoryEvent evt)
        {
            _logBuffer.Clear()
                .Append("LoadBaseResources requested. stepId=")
                .Append(evt.StepId)
                .Append(", beatInstanceId=")
                .Append(evt.BeatInstanceId);
            _logger.Info("StoryFlow", _logBuffer);

            if (_resources.LoadBaseResources())
                CompletePresentationNextFrame(evt, "character-test.base-resources");
        }

        private void OpenStartupUi(StoryEvent evt)
        {
            _logBuffer.Clear()
                .Append("OpenStartupUi requested. stepId=")
                .Append(evt.StepId);
            _logger.Info("StoryFlow", _logBuffer);
        }

        private void CompletePresentationNextFrame(StoryEvent evt, string traceId)
        {
            RuntimeFrame current = _currentFrameProvider();
            var nextFrame = new RuntimeFrame(current.Value + 1);
            RuntimeCommandValidationResult result = _storyModule.CommandBuffer.Enqueue(
                StoryRuntimeCommandFactory.CompletePresentation(
                    nextFrame,
                    StoryRuntimeCommandSources.System,
                    evt.BeatInstanceId,
                    evt.StepId,
                    evt.GraphId,
                    traceId));

            if (result.Success)
            {
                _logBuffer.Clear()
                    .Append("CompletePresentation enqueued. stepId=")
                    .Append(evt.StepId)
                    .Append(", frame=")
                    .Append(nextFrame.Value);
                _logger.Info("StoryFlow", _logBuffer);
                return;
            }

            _logBuffer.Clear()
                .Append("CompletePresentation rejected. stepId=")
                .Append(evt.StepId)
                .Append(", error=")
                .Append(result.Error.Message);
            _logger.Warning("StoryFlow", _logBuffer);
        }
    }
}
