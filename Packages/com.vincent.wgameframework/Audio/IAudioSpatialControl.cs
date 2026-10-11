namespace MxFramework.Audio
{
    /// <summary>Optional NoAlloc spatial-control capability for controlled 3D events and listeners.</summary>
    public interface IAudioSpatialControl
    {
        AudioResult Update3DAttributes(AudioHandle handle, in AudioTransform transform);
        AudioResult SetListenerAttributes(int listenerIndex, in AudioTransform transform, float weight = 1f);
    }
}
