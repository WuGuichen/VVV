using System;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;

namespace MxFramework.Demo.CharacterTest
{
    /// <summary>
    /// CharacterTest 纯 C# 组合根：注册 RuntimeHost 模块、维护帧时钟与命令缓冲，
    /// 后续在此接入 Gameplay / CharacterControl / Combat / Replay，不由 MonoBehaviour 承载规则。
    /// </summary>
    public sealed class GameSlice : IDisposable
    {
        private readonly RuntimeClock _clock;
        private readonly IRuntimeLogger _logger;
        private readonly StoryDirector _storyDirector;
        private readonly StoryRuntimeModule _storyModule;
        private readonly RuntimeHost _host;
        private readonly IDisposable _storyEventSubscription;
        private readonly RuntimeLogBuffer _logBuffer = new RuntimeLogBuffer(160);
        private double _elapsedSeconds;
        private bool _disposed;

        public GameSlice(IRuntimeLogger logger = null)
        {
            _logger = logger ?? NullRuntimeLogger.Instance;
            _logger.Info("GameSlice", "Construct");

            _clock = new RuntimeClock(RuntimeFrame.Zero);
            _storyDirector = new StoryDirector();
            _storyModule = new StoryRuntimeModule(_storyDirector, new RuntimeCommandBuffer(null, RuntimeFrame.Zero));
            _storyEventSubscription = _storyDirector.SubscribeEvents(OnStoryDirectorEvent);

            _host = new RuntimeHost(new RuntimeHostOptions
            {
                ErrorPolicy = RuntimeHostErrorPolicy.CollectAndContinue
            });
            RegisterRuntimeModules();
            _host.Initialize();
            _host.Start();
            _logger.Info("GameSlice", "RuntimeHost started");
            BootstrapStorySession();
        }

        public RuntimeHost Host => _host;
        public RuntimeClock Clock => _clock;
        public StoryDirector StoryDirector => _storyDirector;
        public StoryRuntimeModule StoryModule => _storyModule;
        public RuntimeFrame CurrentFrame => _clock.CurrentFrame;
        public double ElapsedSeconds => _elapsedSeconds;
        public StoryDirectorSnapshot StorySnapshot => _storyDirector.CreateSnapshot();

        public void Tick(double deltaTime)
        {
            ThrowIfDisposed();
            if (deltaTime < 0d)
                deltaTime = 0d;

            RuntimeFrame frame = _clock.CurrentFrame;
            _host.Tick(frame.Value, deltaTime, _elapsedSeconds);
            if (_storyModule.LastCommandErrors.Count > 0)
            {
                _logBuffer.Clear()
                    .Append("Story command errors=")
                    .Append(_storyModule.LastCommandErrors.Count)
                    .Append(" frame=")
                    .Append(frame.Value);
                _logger.Warning("GameSlice", _logBuffer);
            }

            _elapsedSeconds += deltaTime;
            _clock.Step();
        }

        public RuntimeCommandValidationResult RaiseTrigger(int triggerId, int sourceId = StoryRuntimeCommandSources.Input)
        {
            ThrowIfDisposed();
            var command = StoryRuntimeCommandFactory.RaiseTrigger(
                CurrentFrame,
                sourceId,
                triggerId,
                traceId: "character-test.trigger");
            RuntimeCommandValidationResult result = _storyModule.CommandBuffer.Enqueue(command);
            if (result.Success)
            {
                _logBuffer.Clear()
                    .Append("RaiseTrigger enqueued. triggerId=")
                    .Append(triggerId)
                    .Append(", frame=")
                    .Append(CurrentFrame.Value);
                _logger.Info("GameSlice", _logBuffer);
            }
            else
            {
                _logBuffer.Clear()
                    .Append("RaiseTrigger rejected. triggerId=")
                    .Append(triggerId)
                    .Append(", error=")
                    .Append(result.Error.Message);
                _logger.Warning("GameSlice", _logBuffer);
            }

            return result;
        }

        public RuntimeHostDiagnostics CaptureDiagnostics()
        {
            ThrowIfDisposed();
            return _host.CaptureDiagnostics();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _logger.Info("GameSlice", "Dispose");
            _storyEventSubscription?.Dispose();
            _host?.Dispose();
            _disposed = true;
        }

        private void BootstrapStorySession()
        {
            StoryLoadGraphResult load = _storyDirector.TryLoadGraph(CharacterTestStoryFixture.CreateBootstrapGraph());
            if (!load.Success)
            {
                _logBuffer.Clear().Append("CharacterTest graph load failed: ").Append(load.Message);
                _logger.Warning("Story", _logBuffer);
                return;
            }

            _logBuffer.Clear()
                .Append("CharacterTest graph loaded. graphId=")
                .Append(CharacterTestStoryFixture.GraphId);
            _logger.Info("Story", _logBuffer);

            RuntimeCommandValidationResult enter = _storyModule.CommandBuffer.Enqueue(
                StoryRuntimeCommandFactory.RequestEnterBeat(
                    RuntimeFrame.Zero,
                    StoryRuntimeCommandSources.System,
                    CharacterTestStoryFixture.GraphId,
                    CharacterTestStoryFixture.EntryBeatId,
                    traceId: "character-test.bootstrap"));
            if (!enter.Success)
            {
                _logBuffer.Clear().Append("CharacterTest entry beat enqueue failed: ").Append(enter.Error.Message);
                _logger.Warning("Story", _logBuffer);
                return;
            }

            _logBuffer.Clear()
                .Append("Entry beat enqueued. beatId=")
                .Append(CharacterTestStoryFixture.EntryBeatId);
            _logger.Info("Story", _logBuffer);

            Tick(0d);
        }

        private void OnStoryDirectorEvent(StoryEvent evt)
        {
            if (evt.Kind != StoryEventKind.StepStarted || evt.StepId != CharacterTestStoryFixture.WelcomeLineStepId)
                return;

            string text = CharacterTestStoryFixture.ResolveText(CharacterTestStoryFixture.WelcomeTextKey);
            if (string.IsNullOrEmpty(text))
                return;

            _logger.Info("Story", text);
        }

        private void RegisterRuntimeModules()
        {
            // 模块注册顺序与 CHARACTER_TEST_SCENE_DESIGN 运行循环一致；优先级由 RuntimeModule 定义。
            // Phase 1：Story
            _host.RegisterModule(_storyModule);

            // Phase 2+（待接入）示例：
            // _host.RegisterModule(_gameplayModule);           // Simulation, priority 100
            // _host.RegisterModule(_characterControlBridge);   // PreSimulation / PostSimulation
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameSlice));
        }
    }
}
