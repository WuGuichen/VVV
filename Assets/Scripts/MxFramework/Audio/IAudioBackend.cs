using System;

namespace MxFramework.Audio
{
    public interface IAudioBackend : IDisposable
    {
        AudioResult Initialize(IAudioDefinitionProvider definitions);
        AudioPlayResult Play(in AudioPlayRequest request, out AudioHandle handle);
        AudioResult Stop(AudioHandle handle, AudioStopMode stopMode);
        AudioResult SetParameter(AudioHandle handle, int parameterId, float value);
        AudioResult SetBusVolume(int busId, float volume);
        AudioResult SetBusMuted(int busId, bool muted);
        AudioDebugSnapshot CaptureSnapshot();
        void Tick(float deltaTime);
    }
}
