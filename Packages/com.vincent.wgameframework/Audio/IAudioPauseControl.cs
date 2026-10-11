namespace MxFramework.Audio
{
    /// <summary>Optional mix-state capability that pauses buses without overwriting player volume or mute settings.</summary>
    public interface IAudioPauseControl
    {
        AudioResult SetBusPaused(int busId, bool paused);
    }
}
