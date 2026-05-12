using System;
using System.Collections.Generic;
using MxFramework.Audio;

#if MXFRAMEWORK_FMOD
using UnityEngine;
#endif

namespace MxFramework.Audio.FMOD
{
    public sealed class FmodAudioBackend : IAudioBackend
    {
        private readonly FmodAudioBackendOptions options;
#if MXFRAMEWORK_FMOD
        private readonly Dictionary<int, ActiveHandle> handles = new Dictionary<int, ActiveHandle>();
        private readonly List<int> stoppedHandles = new List<int>(16);
        private int nextHandleId = 1;
#endif
        private readonly Dictionary<int, BusEntry> busCache = new Dictionary<int, BusEntry>();
        private readonly Queue<string> recentCommands = new Queue<string>();
        private readonly Queue<AudioDebugError> recentErrors = new Queue<AudioDebugError>();
        private IAudioDefinitionProvider definitions;
        private bool initialized;
#if MXFRAMEWORK_FMOD
        private int totalPlayRequests;
        private int totalStopRequests;
#endif

        public FmodAudioBackend()
            : this(new FmodAudioBackendOptions())
        {
        }

        public FmodAudioBackend(FmodAudioBackendOptions options)
        {
            this.options = options ?? new FmodAudioBackendOptions();
        }

        public AudioResult Initialize(IAudioDefinitionProvider definitions)
        {
            if (definitions == null)
            {
                return Fail(AudioErrorCode.BackendUnavailable, "Audio definitions are required.");
            }

            this.definitions = definitions;

#if MXFRAMEWORK_FMOD
            initialized = true;
            if (options.PreloadBusIds != null)
            {
                for (int i = 0; i < options.PreloadBusIds.Length; i++)
                {
                    AudioResult busResult = EnsureBusCached(options.PreloadBusIds[i]);
                    if (busResult.Failed && options.FailOnMissingBus)
                    {
                        initialized = false;
                        return busResult;
                    }
                }
            }

            PushCommand("FMOD backend initialized.");
            return AudioResult.Ok();
#else
            initialized = false;
            return Fail(AudioErrorCode.BackendUnavailable, "FMOD backend is unavailable because MXFRAMEWORK_FMOD is not defined.");
#endif
        }

        public AudioPlayResult Play(in AudioPlayRequest request, out AudioHandle handle)
        {
            handle = AudioHandle.Invalid;

#if MXFRAMEWORK_FMOD
            if (!initialized)
            {
                return FailPlay(AudioErrorCode.NotInitialized, "FMOD backend is not initialized.", request.Frame, request.TraceId);
            }

            if (!definitions.TryGetEvent(request.EventId, out AudioEventDefinition definition))
            {
                return FailPlay(AudioErrorCode.InvalidEvent, "Audio event was not found: " + request.EventId, request.Frame, request.TraceId);
            }

            if (!TryCreateInstance(definition, out global::FMOD.Studio.EventInstance instance, out string error))
            {
                return FailPlay(AudioErrorCode.BackendFailed, error, request.Frame, request.TraceId);
            }

            Apply3DAttributes(instance, definition, request);
            AudioResult parameterResult = ApplyInitialParameters(instance, definition, request);
            if (parameterResult.Failed)
            {
                instance.release();
                return AudioPlayResult.Fail(parameterResult.ErrorCode, parameterResult.Message);
            }

            global::FMOD.RESULT startResult = instance.start();
            if (startResult != global::FMOD.RESULT.OK)
            {
                instance.release();
                return FailPlay(AudioErrorCode.BackendFailed, "FMOD event failed to start: " + startResult, request.Frame, request.TraceId);
            }

            totalPlayRequests++;
            bool keepHandle = request.PlayMode == AudioPlayMode.StartEvent || definition.IsLoop;
            if (!keepHandle)
            {
                instance.release();
                PushCommand("PlayOneShot eventId=" + definition.Id + " trace=" + request.TraceId);
                return AudioPlayResult.Ok(AudioHandle.Invalid);
            }

            int handleId = nextHandleId++;
            handle = new AudioHandle(handleId, definition.Id, request.EmitterId, AudioHandleState.Playing);
            handles.Add(handleId, new ActiveHandle(instance, definition, request.EmitterId, request.TraceId, 0f, AudioHandleState.Playing));
            PushCommand("StartEvent handle=" + handleId + " eventId=" + definition.Id + " trace=" + request.TraceId);
            return AudioPlayResult.Ok(handle);
#else
            return FailUnavailablePlay(out handle);
#endif
        }

        public AudioResult Stop(AudioHandle handle, AudioStopMode stopMode)
        {
#if MXFRAMEWORK_FMOD
            if (!initialized)
            {
                return Fail(AudioErrorCode.NotInitialized, "FMOD backend is not initialized.", 0, string.Empty);
            }

            if (!handle.IsValid || !handles.TryGetValue(handle.Id, out ActiveHandle active))
            {
                return Fail(AudioErrorCode.InvalidHandle, "Audio handle is not active: " + handle.Id, 0, string.Empty);
            }

            global::FMOD.Studio.STOP_MODE mode = stopMode == AudioStopMode.Immediate
                ? global::FMOD.Studio.STOP_MODE.IMMEDIATE
                : global::FMOD.Studio.STOP_MODE.ALLOWFADEOUT;
            global::FMOD.RESULT stopResult = active.Instance.stop(mode);
            if (stopResult != global::FMOD.RESULT.OK)
            {
                return Fail(AudioErrorCode.BackendFailed, "FMOD event failed to stop: " + stopResult, 0, string.Empty);
            }

            totalStopRequests++;
            active.State = AudioHandleState.Stopping;
            handles[handle.Id] = active;
            PushCommand("Stop handle=" + handle.Id + " mode=" + stopMode);
            return AudioResult.Ok();
#else
            return FailUnavailable();
#endif
        }

        public AudioResult SetParameter(AudioHandle handle, int parameterId, float value)
        {
#if MXFRAMEWORK_FMOD
            if (!initialized)
            {
                return Fail(AudioErrorCode.NotInitialized, "FMOD backend is not initialized.", 0, string.Empty);
            }

            if (!handle.IsValid || !handles.TryGetValue(handle.Id, out ActiveHandle active))
            {
                return Fail(AudioErrorCode.InvalidHandle, "Audio handle is not active: " + handle.Id, 0, string.Empty);
            }

            if (!definitions.TryGetParameter(active.Definition.Id, parameterId, out AudioParameterDefinition parameter))
            {
                return Fail(AudioErrorCode.InvalidParameter, "Audio parameter was not found: event=" + active.Definition.Id + " parameter=" + parameterId, 0, string.Empty);
            }

            global::FMOD.RESULT result = active.Instance.setParameterByName(parameter.Name, value);
            if (result != global::FMOD.RESULT.OK)
            {
                return Fail(AudioErrorCode.BackendFailed, "FMOD parameter failed: " + parameter.Name + " result=" + result, 0, string.Empty);
            }

            PushCommand("SetParameter handle=" + handle.Id + " parameter=" + parameter.Name + " value=" + value);
            return AudioResult.Ok();
#else
            return FailUnavailable();
#endif
        }

        public AudioResult SetBusVolume(int busId, float volume)
        {
#if MXFRAMEWORK_FMOD
            AudioResult cacheResult = EnsureBusCached(busId);
            if (cacheResult.Failed)
            {
                return cacheResult;
            }

            BusEntry entry = busCache[busId];
            float clamped = Mathf.Clamp01(volume);
            global::FMOD.RESULT result = entry.SetVolume(clamped);
            if (result != global::FMOD.RESULT.OK)
            {
                return Fail(AudioErrorCode.BackendFailed, "FMOD bus volume failed: bus=" + busId + " result=" + result, 0, string.Empty);
            }

            entry.Volume = clamped;
            busCache[busId] = entry;
            PushCommand("SetBusVolume bus=" + busId + " volume=" + clamped);
            return AudioResult.Ok();
#else
            return FailUnavailable();
#endif
        }

        public AudioResult SetBusMuted(int busId, bool muted)
        {
#if MXFRAMEWORK_FMOD
            AudioResult cacheResult = EnsureBusCached(busId);
            if (cacheResult.Failed)
            {
                return cacheResult;
            }

            BusEntry entry = busCache[busId];
            global::FMOD.RESULT result = entry.SetMuted(muted);
            if (result != global::FMOD.RESULT.OK)
            {
                return Fail(AudioErrorCode.BackendFailed, "FMOD bus mute failed: bus=" + busId + " result=" + result, 0, string.Empty);
            }

            entry.Muted = muted;
            busCache[busId] = entry;
            PushCommand("SetBusMuted bus=" + busId + " muted=" + muted);
            return AudioResult.Ok();
#else
            return FailUnavailable();
#endif
        }

        public void Tick(float deltaTime)
        {
#if MXFRAMEWORK_FMOD
            if (!initialized || handles.Count == 0)
            {
                return;
            }

            stoppedHandles.Clear();
            foreach (KeyValuePair<int, ActiveHandle> pair in handles)
            {
                ActiveHandle active = pair.Value;
                active.AgeSeconds += deltaTime;
                global::FMOD.RESULT stateResult = active.Instance.getPlaybackState(out global::FMOD.Studio.PLAYBACK_STATE playbackState);
                if (stateResult != global::FMOD.RESULT.OK || playbackState == global::FMOD.Studio.PLAYBACK_STATE.STOPPED)
                {
                    active.Instance.release();
                    stoppedHandles.Add(pair.Key);
                    continue;
                }

                handles[pair.Key] = active;
            }

            for (int i = 0; i < stoppedHandles.Count; i++)
            {
                handles.Remove(stoppedHandles[i]);
            }
#endif
        }

        public AudioDebugSnapshot CaptureSnapshot()
        {
            var buses = new List<AudioDebugBusState>(busCache.Count);
            foreach (BusEntry bus in busCache.Values)
            {
                buses.Add(new AudioDebugBusState(bus.Definition.Id, bus.Definition.Name, bus.Volume, bus.Muted));
            }

            List<AudioDebugActiveEvent> activeEvents;
#if MXFRAMEWORK_FMOD
            activeEvents = new List<AudioDebugActiveEvent>(handles.Count);
            foreach (KeyValuePair<int, ActiveHandle> pair in handles)
            {
                ActiveHandle active = pair.Value;
                var handle = new AudioHandle(pair.Key, active.Definition.Id, active.EmitterId, active.State);
                activeEvents.Add(new AudioDebugActiveEvent(handle, active.Definition.Name, active.Definition.BusId, active.EmitterId));
            }
#else
            activeEvents = new List<AudioDebugActiveEvent>(0);
#endif

            return new AudioDebugSnapshot(
                initialized,
#if MXFRAMEWORK_FMOD
                totalPlayRequests,
                totalStopRequests,
#else
                0,
                0,
#endif
#if MXFRAMEWORK_FMOD
                handles.Count,
#else
                0,
#endif
                activeEvents,
                buses,
                new List<AudioDebugError>(recentErrors));
        }

        public void Dispose()
        {
#if MXFRAMEWORK_FMOD
            foreach (ActiveHandle active in handles.Values)
            {
                active.Instance.stop(global::FMOD.Studio.STOP_MODE.IMMEDIATE);
                active.Instance.release();
            }
            handles.Clear();
#endif
            busCache.Clear();
            initialized = false;
        }

#if MXFRAMEWORK_FMOD
        private bool TryCreateInstance(AudioEventDefinition definition, out global::FMOD.Studio.EventInstance instance, out string error)
        {
            instance = default;
            error = null;

            if (TryResolveEventReference(definition, out ResolvedEventReference resolved))
            {
                try
                {
                    instance = global::FMODUnity.RuntimeManager.CreateInstance(resolved.EventReference);
                    if (instance.isValid())
                    {
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    error = "FMOD guid lookup failed for event " + definition.Id + ": " + exception.Message;
                }
            }

            if (!string.IsNullOrEmpty(definition.FmodEventPath))
            {
                try
                {
                    instance = global::FMODUnity.RuntimeManager.CreateInstance(definition.FmodEventPath);
                    if (instance.isValid())
                    {
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    error = "FMOD path lookup failed for event " + definition.Id + ": " + exception.Message;
                }
            }

            error = error ?? "FMOD event has no usable guid or path: " + definition.Id;
            return false;
        }

        private static bool TryResolveEventReference(AudioEventDefinition definition, out ResolvedEventReference resolved)
        {
            resolved = default;
            if (string.IsNullOrWhiteSpace(definition.FmodEventGuid))
            {
                return false;
            }

            if (!TryParseFmodGuid(definition.FmodEventGuid, out global::FMOD.GUID guid))
            {
                return false;
            }

            global::FMODUnity.EventReference eventReference = new global::FMODUnity.EventReference
            {
                Guid = guid
            };

            resolved = new ResolvedEventReference(eventReference, definition.FmodEventPath);
            return true;
        }

        private static bool TryParseFmodGuid(string value, out global::FMOD.GUID fmodGuid)
        {
            fmodGuid = default;
            if (!Guid.TryParse(value, out Guid guid))
            {
                return false;
            }

            byte[] bytes = guid.ToByteArray();
            fmodGuid.Data1 = BitConverter.ToInt32(bytes, 0);
            fmodGuid.Data2 = BitConverter.ToInt32(bytes, 4);
            fmodGuid.Data3 = BitConverter.ToInt32(bytes, 8);
            fmodGuid.Data4 = BitConverter.ToInt32(bytes, 12);
            return true;
        }

        private AudioResult ApplyInitialParameters(global::FMOD.Studio.EventInstance instance, AudioEventDefinition definition, in AudioPlayRequest request)
        {
            for (int i = 0; i < request.Parameters.Length; i++)
            {
                AudioParameterValue value = request.Parameters[i];
                if (!definitions.TryGetParameter(definition.Id, value.ParameterId, out AudioParameterDefinition parameter))
                {
                    return Fail(AudioErrorCode.InvalidParameter, "Audio parameter was not found: event=" + definition.Id + " parameter=" + value.ParameterId, request.Frame, request.TraceId);
                }

                global::FMOD.RESULT result = instance.setParameterByName(parameter.Name, value.Value);
                if (result != global::FMOD.RESULT.OK)
                {
                    return Fail(AudioErrorCode.BackendFailed, "FMOD parameter failed: " + parameter.Name + " result=" + result, request.Frame, request.TraceId);
                }
            }

            return AudioResult.Ok();
        }

        private static void Apply3DAttributes(global::FMOD.Studio.EventInstance instance, AudioEventDefinition definition, in AudioPlayRequest request)
        {
            if (!definition.Is3D)
            {
                return;
            }

            Vector3 position = new Vector3(request.Transform.X, request.Transform.Y, request.Transform.Z);
            instance.set3DAttributes(global::FMODUnity.RuntimeUtils.To3DAttributes(position));
        }

        private AudioResult EnsureBusCached(int busId)
        {
            if (!initialized)
            {
                return Fail(AudioErrorCode.NotInitialized, "FMOD backend is not initialized.", 0, string.Empty);
            }

            if (busCache.ContainsKey(busId))
            {
                return AudioResult.Ok();
            }

            if (!definitions.TryGetBus(busId, out AudioBusDefinition definition))
            {
                return Fail(AudioErrorCode.InvalidBus, "Audio bus was not found: " + busId, 0, string.Empty);
            }

            BusEntry entry = new BusEntry(definition);
            global::FMOD.RESULT result = entry.Resolve();
            if (result != global::FMOD.RESULT.OK)
            {
                return Fail(AudioErrorCode.InvalidBus, "FMOD bus/VCA lookup failed: bus=" + busId + " result=" + result, 0, string.Empty);
            }

            busCache.Add(busId, entry);
            return AudioResult.Ok();
        }
#endif

        private AudioPlayResult FailPlay(AudioErrorCode errorCode, string message, long frame = 0, string traceId = null)
        {
            PushError(errorCode, message, frame, traceId);
            return AudioPlayResult.Fail(errorCode, message);
        }

        private AudioResult Fail(AudioErrorCode errorCode, string message, long frame = 0, string traceId = null)
        {
            PushError(errorCode, message, frame, traceId);
            return AudioResult.Fail(errorCode, message);
        }

        private AudioResult FailUnavailable()
        {
            return Fail(AudioErrorCode.BackendUnavailable, "FMOD backend is unavailable because MXFRAMEWORK_FMOD is not defined.");
        }

        private AudioPlayResult FailUnavailablePlay(out AudioHandle handle)
        {
            handle = AudioHandle.Invalid;
            return FailPlay(AudioErrorCode.BackendUnavailable, "FMOD backend is unavailable because MXFRAMEWORK_FMOD is not defined.");
        }

        private void PushCommand(string message)
        {
            PushBounded(recentCommands, message);
        }

        private void PushError(AudioErrorCode errorCode, string message, long frame, string traceId)
        {
            PushBounded(recentErrors, new AudioDebugError(errorCode, message, frame, traceId));
        }

        private void PushBounded(Queue<string> queue, string message)
        {
            int capacity = options.RecentMessageCapacity <= 0 ? 32 : options.RecentMessageCapacity;
            while (queue.Count >= capacity)
            {
                queue.Dequeue();
            }

            queue.Enqueue(message);
        }

        private void PushBounded(Queue<AudioDebugError> queue, AudioDebugError error)
        {
            int capacity = options.RecentMessageCapacity <= 0 ? 32 : options.RecentMessageCapacity;
            while (queue.Count >= capacity)
            {
                queue.Dequeue();
            }

            queue.Enqueue(error);
        }

#if MXFRAMEWORK_FMOD
        private struct ActiveHandle
        {
            public ActiveHandle(global::FMOD.Studio.EventInstance instance, AudioEventDefinition definition, int emitterId, string traceId, float ageSeconds, AudioHandleState state)
            {
                Instance = instance;
                Definition = definition;
                EmitterId = emitterId;
                TraceId = traceId;
                AgeSeconds = ageSeconds;
                State = state;
            }

            public global::FMOD.Studio.EventInstance Instance;
            public AudioEventDefinition Definition;
            public int EmitterId;
            public string TraceId;
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
