namespace MxFramework.Audio
{
    /// <summary>Backend boundary for explicit Bank ownership.</summary>
    public interface IAudioBankBackend
    {
        /// <remarks>LowFreqAlloc at the external backend boundary.</remarks>
        AudioResult LoadBank(string stableName, bool preloadSamples);

        /// <remarks>LowFreqAlloc at the external backend boundary.</remarks>
        AudioResult UnloadBank(string stableName);
    }
}
