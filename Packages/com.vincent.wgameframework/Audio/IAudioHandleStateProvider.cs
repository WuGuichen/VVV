namespace MxFramework.Audio
{
    /// <summary>Optional NoAlloc handle-liveness capability used by lifecycle owners.</summary>
    public interface IAudioHandleStateProvider
    {
        bool IsHandleActive(AudioHandle handle);
    }
}
