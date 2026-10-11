using System;

namespace MxFramework.Audio
{
    /// <summary>Coordinates pause, entity, scene and session boundaries for presentation-owned audio.</summary>
    public sealed class AudioLifecycleController
    {
        private readonly IAudioService _service;
        private readonly AudioPresentationDrain _drain;
        private readonly int[] _pauseBusIds;
        private bool _paused;

        /// <remarks>LowFreqAlloc during composition; lifecycle methods are NoAlloc on successful backend paths.</remarks>
        public AudioLifecycleController(IAudioService service, AudioPresentationDrain drain, int[] pauseBusIds = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _drain = drain ?? throw new ArgumentNullException(nameof(drain));
            _pauseBusIds = pauseBusIds == null ? Array.Empty<int>() : (int[])pauseBusIds.Clone();
        }

        public bool Paused => _paused;

        public AudioResult SetPaused(bool paused)
        {
            if (_paused == paused)
                return AudioResult.Ok();
            if (!(_service is IAudioPauseControl pauseControl))
                return AudioResult.Fail(AudioErrorCode.BackendUnavailable, "Audio backend does not support bus pause state.");

            for (int i = 0; i < _pauseBusIds.Length; i++)
            {
                AudioResult result = pauseControl.SetBusPaused(_pauseBusIds[i], paused);
                if (result.Failed)
                    return result;
            }
            _paused = paused;
            return AudioResult.Ok();
        }

        public int OnEntityDestroyed(int ownerId)
        {
            return _drain.StopAllByScope(_service, AudioScope.Entity, ownerId, AudioStopMode.Immediate);
        }

        public int OnSceneExit()
        {
            int stopped = _drain.StopAllByScope(_service, AudioScope.TransientFrame, stopMode: AudioStopMode.Immediate);
            stopped += _drain.StopAllByScope(_service, AudioScope.Entity, stopMode: AudioStopMode.Immediate);
            stopped += _drain.StopAllByScope(_service, AudioScope.Scene, stopMode: AudioStopMode.AllowFadeout);
            return stopped;
        }

        public int OnSessionEnded()
        {
            if (_paused)
                SetPaused(false);
            int stopped = OnSceneExit();
            stopped += _drain.StopAllByScope(_service, AudioScope.AppPersistent, stopMode: AudioStopMode.AllowFadeout);
            _drain.ResetDedupeHistory();
            return stopped;
        }
    }
}
