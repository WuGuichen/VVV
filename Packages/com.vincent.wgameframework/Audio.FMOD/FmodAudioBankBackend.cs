using System;
using MxFramework.Audio;

namespace MxFramework.Audio.FMOD
{
    /// <summary>FMOD Unity adapter for manifest-owned Bank loading.</summary>
    public sealed class FmodAudioBankBackend : IAudioBankBackend
    {
        public AudioResult LoadBank(string stableName, bool preloadSamples)
        {
#if MXFRAMEWORK_FMOD
            try
            {
                global::FMODUnity.RuntimeManager.LoadBank(stableName, preloadSamples);
                return AudioResult.Ok();
            }
            catch (Exception exception)
            {
                return AudioResult.Fail(AudioErrorCode.BackendUnavailable, "FMOD Bank load failed for " + stableName + ": " + exception.Message);
            }
#else
            return AudioResult.Fail(AudioErrorCode.BackendUnavailable, "FMOD backend is unavailable because MXFRAMEWORK_FMOD is not defined.");
#endif
        }

        public AudioResult UnloadBank(string stableName)
        {
#if MXFRAMEWORK_FMOD
            try
            {
                global::FMODUnity.RuntimeManager.UnloadBank(stableName);
                return AudioResult.Ok();
            }
            catch (Exception exception)
            {
                return AudioResult.Fail(AudioErrorCode.BackendUnavailable, "FMOD Bank unload failed for " + stableName + ": " + exception.Message);
            }
#else
            return AudioResult.Fail(AudioErrorCode.BackendUnavailable, "FMOD backend is unavailable because MXFRAMEWORK_FMOD is not defined.");
#endif
        }
    }
}
