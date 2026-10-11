using MxFramework.Audio;

namespace MxFramework.Audio.FMOD
{
    public sealed partial class FmodAudioBackend
    {
#if MXFRAMEWORK_FMOD
        private bool TryAdmitVoice(AudioEventDefinition definition, in AudioPlayRequest request, out string error)
        {
            error = null;
            int globalCount = handles.Count;
            int busCount = 0;
            int eventCount = 0;
            int emitterCount = 0;
            for (int i = 0; i < activeHandleIds.Count; i++)
            {
                ActiveHandle active = handles[activeHandleIds[i]];
                if (active.Definition.BusId == definition.BusId)
                    busCount++;
                if (active.Definition.Id == definition.Id)
                    eventCount++;
                if (request.EmitterId != 0 && active.EmitterId == request.EmitterId)
                    emitterCount++;
            }

            bool globalExceeded = options.GlobalVoiceLimit > 0 && globalCount >= options.GlobalVoiceLimit;
            bool busExceeded = options.BusVoiceLimit > 0 && busCount >= options.BusVoiceLimit;
            bool eventExceeded = options.EventVoiceLimit > 0 && eventCount >= options.EventVoiceLimit;
            bool emitterExceeded = request.EmitterId != 0 && options.EmitterVoiceLimit > 0 && emitterCount >= options.EmitterVoiceLimit;
            if (!globalExceeded && !busExceeded && !eventExceeded && !emitterExceeded)
                return true;

            int candidateIndex = -1;
            for (int i = 0; i < activeHandleIds.Count; i++)
            {
                ActiveHandle active = handles[activeHandleIds[i]];
                if (active.Priority >= request.Priority)
                    continue;
                if (busExceeded && active.Definition.BusId != definition.BusId)
                    continue;
                if (eventExceeded && active.Definition.Id != definition.Id)
                    continue;
                if (emitterExceeded && active.EmitterId != request.EmitterId)
                    continue;
                if (candidateIndex < 0 || active.Priority < handles[activeHandleIds[candidateIndex]].Priority)
                    candidateIndex = i;
            }

            if (candidateIndex < 0)
            {
                error = "FMOD voice budget rejected event=" + definition.Id + " priority=" + request.Priority;
                return false;
            }

            int handleId = activeHandleIds[candidateIndex];
            ActiveHandle candidate = handles[handleId];
            candidate.Instance.stop(global::FMOD.Studio.STOP_MODE.IMMEDIATE);
            candidate.Instance.release();
            handles.Remove(handleId);
            activeHandleIds.RemoveAt(candidateIndex);
            totalVoiceSteals++;
            return true;
        }
#endif
    }
}
