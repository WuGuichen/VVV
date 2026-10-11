namespace MxFramework.Audio.FMOD
{
    public sealed class FmodAudioBackendOptions
    {
        public MxFramework.Audio.AudioBankManifest BankManifest { get; set; }
        public string BootstrapBankGroup { get; set; } = "bootstrap";
        public bool FailOnMissingBus { get; set; }
        public int[] PreloadBusIds { get; set; } = System.Array.Empty<int>();
        public int RecentMessageCapacity { get; set; } = 32;
        public bool CaptureRecentCommands { get; set; }
        public int GlobalVoiceLimit { get; set; } = 128;
        public int BusVoiceLimit { get; set; } = 64;
        public int EventVoiceLimit { get; set; } = 16;
        public int EmitterVoiceLimit { get; set; } = 8;
    }
}
