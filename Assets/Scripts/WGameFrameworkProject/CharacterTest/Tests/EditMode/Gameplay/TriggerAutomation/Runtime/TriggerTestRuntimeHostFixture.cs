using System;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal sealed class TriggerTestRuntimeHostFixture : ITriggerTestFixture
    {
        private readonly RuntimeHost _host;
        private bool _initialized;
        private bool _disposed;

        internal TriggerTestRuntimeHostFixture(
            GameplayTriggerRuntime triggerRuntime,
            RuntimeHost host,
            TriggerTestServiceRegistry services = null)
        {
            TriggerRuntime = triggerRuntime ??
                throw new ArgumentNullException(nameof(triggerRuntime));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            Services = services ?? new TriggerTestServiceRegistry();
        }

        public GameplayTriggerRuntime TriggerRuntime { get; }
        public TriggerTestServiceRegistry Services { get; }

        public void Initialize()
        {
            if (_initialized)
                throw new InvalidOperationException("Trigger test fixture is already initialized.");

            _host.Initialize();
            _host.Start();
            _initialized = true;
        }

        public void Tick(long frame, double deltaTime)
        {
            if (!_initialized || _disposed)
                throw new InvalidOperationException("Trigger test fixture is not running.");

            _host.Tick(frame, deltaTime);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_initialized)
                _host.Stop();
            _host.Dispose();
        }
    }
}
