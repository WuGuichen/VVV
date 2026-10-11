using System;
using System.Collections.Generic;
using MxFramework.Audio;

#if MXFRAMEWORK_FMOD
using UnityEngine;
#endif

namespace MxFramework.Audio.FMOD
{
    public sealed partial class FmodAudioBackend
    {
#if MXFRAMEWORK_FMOD
        private struct ActiveHandle
        {
            public ActiveHandle(
                global::FMOD.Studio.EventInstance instance,
                AudioEventDefinition definition,
                int emitterId,
                int priority,
                bool controlled,
                AudioTraceIdentity traceId,
                float ageSeconds,
                AudioHandleState state)
            {
                Instance = instance;
                Definition = definition;
                EmitterId = emitterId;
                Priority = priority;
                Controlled = controlled;
                TraceId = traceId;
                AgeSeconds = ageSeconds;
                State = state;
            }

            public global::FMOD.Studio.EventInstance Instance;
            public AudioEventDefinition Definition;
            public int EmitterId;
            public int Priority;
            public bool Controlled;
            public AudioTraceIdentity TraceId;
            public float AgeSeconds;
            public AudioHandleState State;
        }
#endif

        private struct BusEntry
        {
            public BusEntry(AudioBusDefinition definition)
            {
                Definition = definition;
                Path = !string.IsNullOrEmpty(definition.FmodBusPath) ? definition.FmodBusPath : definition.FmodVcaPath;
                Volume = definition.DefaultVolume;
                Muted = definition.DefaultMuted;
                Paused = false;
#if MXFRAMEWORK_FMOD
                Bus = default;
                Vca = default;
                UsesVca = false;
#endif
            }

            public AudioBusDefinition Definition;
            public string Path;
            public float Volume;
            public bool Muted;
            public bool Paused;

#if MXFRAMEWORK_FMOD
            public global::FMOD.Studio.Bus Bus;
            public global::FMOD.Studio.VCA Vca;
            public bool UsesVca;

            public global::FMOD.RESULT Resolve()
            {
                if (!string.IsNullOrEmpty(Definition.FmodBusPath))
                {
                    global::FMOD.RESULT result = global::FMODUnity.RuntimeManager.StudioSystem.getBus(Definition.FmodBusPath, out Bus);
                    if (result != global::FMOD.RESULT.OK)
                    {
                        return result;
                    }

                    UsesVca = false;
                    return global::FMOD.RESULT.OK;
                }

                if (!string.IsNullOrEmpty(Definition.FmodVcaPath))
                {
                    global::FMOD.RESULT result = global::FMODUnity.RuntimeManager.StudioSystem.getVCA(Definition.FmodVcaPath, out Vca);
                    if (result != global::FMOD.RESULT.OK)
                    {
                        return result;
                    }

                    UsesVca = true;
                    return global::FMOD.RESULT.OK;
                }

                return global::FMOD.RESULT.ERR_INVALID_PARAM;
            }

            public global::FMOD.RESULT SetVolume(float volume)
            {
                return UsesVca ? Vca.setVolume(volume) : Bus.setVolume(volume);
            }

            public global::FMOD.RESULT SetMuted(bool muted)
            {
                return UsesVca ? Vca.setVolume(muted ? 0f : Volume) : Bus.setMute(muted);
            }

            public global::FMOD.RESULT SetPaused(bool paused)
            {
                return UsesVca ? global::FMOD.RESULT.ERR_UNSUPPORTED : Bus.setPaused(paused);
            }

            public void QueryState(out float effectiveVolume, out bool muted, out bool paused)
            {
                effectiveVolume = Volume;
                muted = Muted;
                paused = Paused;
                if (UsesVca)
                {
                    if (Vca.getVolume(out float current, out _) == global::FMOD.RESULT.OK)
                        effectiveVolume = current;
                    return;
                }

                if (Bus.getVolume(out float busVolume, out _) == global::FMOD.RESULT.OK)
                    effectiveVolume = busVolume;
                Bus.getMute(out muted);
                Bus.getPaused(out paused);
            }
#endif
        }

#if MXFRAMEWORK_FMOD
        private readonly struct ResolvedEventReference
        {
            public ResolvedEventReference(global::FMODUnity.EventReference eventReference, string fallbackPath)
            {
                EventReference = eventReference;
                FallbackPath = fallbackPath;
            }

            public global::FMODUnity.EventReference EventReference { get; }
            public string FallbackPath { get; }
        }
#endif
    }
}
