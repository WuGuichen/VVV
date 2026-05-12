namespace MxFramework.Audio.FMOD
{
    public sealed class FmodAudioBackendOptions
    {
        public bool FailOnMissingBus { get; set; }
        public int[] PreloadBusIds { get; set; } = System.Array.Empty<int>();
        public int RecentMessageCapacity { get; set; } = 32;
    }
}
