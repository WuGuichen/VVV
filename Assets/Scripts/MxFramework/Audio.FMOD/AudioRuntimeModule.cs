using System;
using MxFramework.Audio;
using MxFramework.Runtime;

namespace MxFramework.Audio.FMOD
{
    public sealed class AudioRuntimeModule : RuntimeModule
    {
        public const string DefaultModuleId = "audio.fmod";

        private readonly IAudioService _audioService;
        private readonly bool _disposeAudioService;
        private IAudioService _resolvedAudioService;

        public AudioRuntimeModule(
            IAudioService audioService = null,
            bool disposeAudioService = false,
            string moduleId = DefaultModuleId,
            RuntimeTickStage tickStage = RuntimeTickStage.PostSimulation,
            int priority = 0)
            : base(moduleId, tickStage, priority)
        {
            _audioService = audioService;
            _disposeAudioService = disposeAudioService;
        }

        public override void Initialize(RuntimeHostContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            _resolvedAudioService = _audioService;
            if (_resolvedAudioService == null)
            {
                context.Services.TryGet(out _resolvedAudioService);
            }

            if (_resolvedAudioService == null)
            {
                throw new InvalidOperationException("AudioRuntimeModule requires an IAudioService instance or a registered IAudioService runtime service.");
            }
        }

        public override void Tick(RuntimeTickContext context)
        {
            if (_resolvedAudioService == null)
            {
                return;
            }

            _resolvedAudioService.Tick((float)context.DeltaTime);
        }

        public override void Dispose()
        {
            if (_disposeAudioService && _audioService is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _resolvedAudioService = null;
        }
    }
}
