using MxFramework.Audio;

namespace MxFramework.Audio.FMOD
{
    public sealed partial class FmodAudioBackend
    {
#if MXFRAMEWORK_FMOD
        private AudioResult EnsureBusCached(int busId)
        {
            if (!initialized)
            {
                return Fail(AudioErrorCode.NotInitialized, "FMOD backend is not initialized.", 0, AudioTraceIdentity.None);
            }

            if (busCache.ContainsKey(busId))
            {
                return AudioResult.Ok();
            }

            if (!definitions.TryGetBus(busId, out AudioBusDefinition definition))
            {
                return Fail(AudioErrorCode.InvalidBus, "Audio bus was not found: " + busId, 0, AudioTraceIdentity.None);
            }

            BusEntry entry = new BusEntry(definition);
            global::FMOD.RESULT result = entry.Resolve();
            if (result != global::FMOD.RESULT.OK)
            {
                return Fail(AudioErrorCode.InvalidBus, "FMOD bus/VCA lookup failed: bus=" + busId + " result=" + result, 0, AudioTraceIdentity.None);
            }

            result = entry.SetVolume(Clamp(entry.Volume, 0f, 1f));
            if (result == global::FMOD.RESULT.OK && entry.Muted)
                result = entry.SetMuted(true);
            if (result != global::FMOD.RESULT.OK)
            {
                return Fail(AudioErrorCode.BackendFailed, "FMOD bus default state failed: bus=" + busId + " result=" + result, 0, AudioTraceIdentity.None);
            }

            busCache.Add(busId, entry);
            return AudioResult.Ok();
        }
#endif
    }
}
