using System;
using MxFramework.Runtime;

namespace MxFramework.VoiceInput
{
    public sealed class VoiceInputRuntimeModule : RuntimeModule
    {
        public const string DefaultModuleId = "voice.input";

        private readonly IVoiceInputService _voiceInputService;
        private readonly bool _startOnHostStart;
        private readonly bool _stopOnHostStop;
        private readonly bool _disposeVoiceInputService;
        private IVoiceInputService _resolvedVoiceInputService;

        public VoiceInputRuntimeModule(
            IVoiceInputService voiceInputService = null,
            bool startOnHostStart = false,
            bool stopOnHostStop = true,
            bool disposeVoiceInputService = false,
            string moduleId = DefaultModuleId,
            RuntimeTickStage tickStage = RuntimeTickStage.PreSimulation,
            int priority = 0)
            : base(moduleId, tickStage, priority)
        {
            _voiceInputService = voiceInputService;
            _startOnHostStart = startOnHostStart;
            _stopOnHostStop = stopOnHostStop;
            _disposeVoiceInputService = disposeVoiceInputService;
        }

        public override void Initialize(RuntimeHostContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            _resolvedVoiceInputService = _voiceInputService;
            if (_resolvedVoiceInputService == null)
            {
                context.Services.TryGet(out _resolvedVoiceInputService);
            }

            if (_resolvedVoiceInputService == null)
            {
                throw new InvalidOperationException("VoiceInputRuntimeModule requires an IVoiceInputService instance or a registered IVoiceInputService runtime service.");
            }
        }

        public override void Start(RuntimeHostContext context)
        {
            if (_startOnHostStart)
            {
                _resolvedVoiceInputService.Start();
            }
        }

        /// <remarks>NoAlloc after initialization when the service/provider remain within configured capacities.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            if (_resolvedVoiceInputService == null)
            {
                return;
            }

            _resolvedVoiceInputService.Tick(new RuntimeFrame(context.FrameIndex), context.DeltaTime);
        }

        public override void Stop(RuntimeHostContext context)
        {
            if (_stopOnHostStop && _resolvedVoiceInputService != null)
            {
                _resolvedVoiceInputService.Stop();
            }
        }

        public override void Dispose()
        {
            if (_disposeVoiceInputService && _resolvedVoiceInputService != null)
            {
                _resolvedVoiceInputService.Dispose();
            }

            _resolvedVoiceInputService = null;
        }
    }
}
