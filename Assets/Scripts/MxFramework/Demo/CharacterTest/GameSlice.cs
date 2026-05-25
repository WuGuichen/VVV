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
        private readonly StoryDirector _storyDirector;
        private readonly StoryRuntimeModule _storyModule;
        private readonly RuntimeHost _host;
        private double _elapsedSeconds;
        private bool _disposed;

        public GameSlice()
        {
            _clock = new RuntimeClock(RuntimeFrame.Zero);
            _storyDirector = new StoryDirector();
            _storyModule = new StoryRuntimeModule(_storyDirector, new RuntimeCommandBuffer(null, RuntimeFrame.Zero));

            _host = new RuntimeHost(new RuntimeHostOptions
            {
                ErrorPolicy = RuntimeHostErrorPolicy.CollectAndContinue
            });
            RegisterRuntimeModules();
            _host.Initialize();
            _host.Start();
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
            return _storyModule.CommandBuffer.Enqueue(command);
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

            _host?.Dispose();
            _disposed = true;
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
