using System;
using System.Collections.Generic;
using MxFramework.Audio;

#if MXFRAMEWORK_FMOD
using UnityEngine;
#endif

namespace MxFramework.Audio.FMOD
{
    public sealed partial class FmodAudioBackend : IAudioBackend, IAudioHandleStateProvider, IAudioSpatialControl, IAudioPauseControl, IAudioPerformanceDiagnostics
    {
        private const string PhysicalVolumeParameterName = "MxPhysicalVolume";
        private const string PhysicalPitchParameterName = "MxPhysicalPitch";
        private const string PhysicalLowPassParameterName = "MxPhysicalLowPass";
        private const string PhysicalBrightnessParameterName = "MxPhysicalBrightness";
        private const string PhysicalScrapeParameterName = "MxPhysicalScrape";

        private readonly FmodAudioBackendOptions options;
#if MXFRAMEWORK_FMOD
        private readonly Dictionary<int, ActiveHandle> handles = new Dictionary<int, ActiveHandle>();
        private readonly List<int> activeHandleIds = new List<int>(16);
        private readonly List<int> stoppedHandles = new List<int>(16);
        private int nextHandleId = 1;
#endif
        private readonly Dictionary<int, BusEntry> busCache = new Dictionary<int, BusEntry>();
        private readonly FixedRing<string> recentCommands;
        private readonly FixedRing<AudioDebugError> recentErrors;
        private AudioBankLifecycleManager bankLifecycle;
        private readonly AudioDebugListenerState[] listenerStates = new AudioDebugListenerState[8];
        private readonly bool[] listenerStateValid = new bool[8];
        private IAudioDefinitionProvider definitions;
        private bool initialized;
#if MXFRAMEWORK_FMOD
        private int totalPlayRequests;
        private int totalStopRequests;
        private int totalVoiceRejections;
        private int totalVoiceSteals;
#endif

        public FmodAudioBackend()
            : this(new FmodAudioBackendOptions())
        {
        }

        public FmodAudioBackend(FmodAudioBackendOptions options)
        {
            this.options = options ?? new FmodAudioBackendOptions();
            int recentCapacity = this.options.RecentMessageCapacity <= 0 ? 32 : this.options.RecentMessageCapacity;
            recentCommands = new FixedRing<string>(recentCapacity);
            recentErrors = new FixedRing<AudioDebugError>(recentCapacity);
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
            if (options.BankManifest != null)
            {
                bankLifecycle = new AudioBankLifecycleManager(options.BankManifest, new FmodAudioBankBackend());
                AudioResult warmup = bankLifecycle.WarmupGroup(options.BootstrapBankGroup);
                if (warmup.Failed)
                {
                    bankLifecycle.Dispose();
                    bankLifecycle = null;
                    initialized = false;
                    return Fail(warmup.ErrorCode, warmup.Message);
                }
            }
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

            if (!TryAdmitVoice(definition, request, out string admissionError))
            {
                totalVoiceRejections++;
                return FailPlay(AudioErrorCode.RequestRejected, admissionError, request.Frame, request.TraceId);
            }

            if (!TryCreateInstance(definition, out global::FMOD.Studio.EventInstance instance, out string error))
            {
                return FailPlay(AudioErrorCode.BackendFailed, error, request.Frame, request.TraceId);
            }

            AudioResult attributesResult = Apply3DAttributes(instance, definition, request);
            if (attributesResult.Failed)
            {
                instance.release();
                return AudioPlayResult.Fail(attributesResult.ErrorCode, attributesResult.Message);
            }
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
            int handleId = nextHandleId++;
            AudioHandle internalHandle = new AudioHandle(handleId, definition.Id, request.EmitterId, AudioHandleState.Playing);
            handles.Add(handleId, new ActiveHandle(
                instance,
                definition,
                request.EmitterId,
                request.Priority,
                keepHandle,
                request.TraceId,
                0f,
                AudioHandleState.Playing));
            activeHandleIds.Add(handleId);
            if (!keepHandle)
            {
                if (options.CaptureRecentCommands)
                    PushCommand("PlayOneShot eventId=" + definition.Id + " trace=" + request.TraceId);
                return AudioPlayResult.Ok(AudioHandle.Invalid);
            }

            handle = internalHandle;
            if (options.CaptureRecentCommands)
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
                return Fail(AudioErrorCode.NotInitialized, "FMOD backend is not initialized.", 0, AudioTraceIdentity.None);
            }

            if (!handle.IsValid || !handles.TryGetValue(handle.Id, out ActiveHandle active))
            {
                return Fail(AudioErrorCode.InvalidHandle, "Audio handle is not active: " + handle.Id, 0, AudioTraceIdentity.None);
            }

            global::FMOD.Studio.STOP_MODE mode = stopMode == AudioStopMode.Immediate
                ? global::FMOD.Studio.STOP_MODE.IMMEDIATE
                : global::FMOD.Studio.STOP_MODE.ALLOWFADEOUT;
            global::FMOD.RESULT stopResult = active.Instance.stop(mode);
            if (stopResult != global::FMOD.RESULT.OK)
            {
                return Fail(AudioErrorCode.BackendFailed, "FMOD event failed to stop: " + stopResult, 0, AudioTraceIdentity.None);
            }

            totalStopRequests++;
            active.State = AudioHandleState.Stopping;
            handles[handle.Id] = active;
            if (options.CaptureRecentCommands)
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
                return Fail(AudioErrorCode.NotInitialized, "FMOD backend is not initialized.", 0, AudioTraceIdentity.None);
            }

            if (!handle.IsValid || !handles.TryGetValue(handle.Id, out ActiveHandle active))
            {
                return Fail(AudioErrorCode.InvalidHandle, "Audio handle is not active: " + handle.Id, 0, AudioTraceIdentity.None);
            }

            if (!definitions.TryGetParameter(active.Definition.Id, parameterId, out AudioParameterDefinition parameter))
            {
                return Fail(AudioErrorCode.InvalidParameter, "Audio parameter was not found: event=" + active.Definition.Id + " parameter=" + parameterId, 0, AudioTraceIdentity.None);
            }

            AudioResult backendParameterResult = TryApplyBackendParameter(
                active.Instance,
                parameter,
                value,
                0,
                AudioTraceIdentity.None,
                out bool handled);
            if (handled)
            {
                return backendParameterResult;
            }

            global::FMOD.RESULT result = active.Instance.setParameterByName(parameter.Name, value);
            if (result != global::FMOD.RESULT.OK)
            {
                if (IsOptionalPhysicalParameter(parameter.Name)
                    && (result == global::FMOD.RESULT.ERR_EVENT_NOTFOUND
                        || result == global::FMOD.RESULT.ERR_INVALID_PARAM))
                {
                    return AudioResult.Ok();
                }

                return Fail(AudioErrorCode.BackendFailed, "FMOD parameter failed: " + parameter.Name + " result=" + result, 0, AudioTraceIdentity.None);
            }

            if (options.CaptureRecentCommands)
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
            global::FMOD.RESULT result = entry.UsesVca && entry.Muted
                ? global::FMOD.RESULT.OK
                : entry.SetVolume(clamped);
            if (result != global::FMOD.RESULT.OK)
            {
                return Fail(AudioErrorCode.BackendFailed, "FMOD bus volume failed: bus=" + busId + " result=" + result, 0, AudioTraceIdentity.None);
            }

            entry.Volume = clamped;
            busCache[busId] = entry;
            if (options.CaptureRecentCommands)
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
                return Fail(AudioErrorCode.BackendFailed, "FMOD bus mute failed: bus=" + busId + " result=" + result, 0, AudioTraceIdentity.None);
            }

            entry.Muted = muted;
            busCache[busId] = entry;
            if (options.CaptureRecentCommands)
                PushCommand("SetBusMuted bus=" + busId + " muted=" + muted);
            return AudioResult.Ok();
#else
            return FailUnavailable();
#endif
        }

        public bool IsHandleActive(AudioHandle handle)
        {
#if MXFRAMEWORK_FMOD
            return initialized && handle.IsValid && handles.ContainsKey(handle.Id);
#else
            return false;
#endif
        }

        public AudioResult Update3DAttributes(AudioHandle handle, in AudioTransform transform)
        {
#if MXFRAMEWORK_FMOD
            if (!initialized || !handle.IsValid || !handles.TryGetValue(handle.Id, out ActiveHandle active))
                return Fail(AudioErrorCode.InvalidHandle, "Audio handle is not active: " + handle.Id, 0, AudioTraceIdentity.None);
            global::FMOD.RESULT result = active.Instance.set3DAttributes(ToFmodAttributes(transform));
            return result == global::FMOD.RESULT.OK
                ? AudioResult.Ok()
                : Fail(AudioErrorCode.BackendFailed, "FMOD emitter update failed: " + result, 0, active.TraceId);
#else
            return FailUnavailable();
#endif
        }

        public AudioResult SetListenerAttributes(int listenerIndex, in AudioTransform transform, float weight = 1f)
        {
#if MXFRAMEWORK_FMOD
            if (!initialized)
                return Fail(AudioErrorCode.NotInitialized, "FMOD backend is not initialized.", 0, AudioTraceIdentity.None);
            global::FMOD.RESULT result = global::FMODUnity.RuntimeManager.StudioSystem.setListenerAttributes(
                listenerIndex,
                ToFmodAttributes(transform));
            if (result == global::FMOD.RESULT.OK)
                result = global::FMODUnity.RuntimeManager.StudioSystem.setListenerWeight(listenerIndex, Clamp(weight, 0f, 1f));
            if (result == global::FMOD.RESULT.OK && listenerIndex >= 0 && listenerIndex < listenerStates.Length)
            {
                float clampedWeight = Clamp(weight, 0f, 1f);
                listenerStates[listenerIndex] = new AudioDebugListenerState(listenerIndex, transform, clampedWeight);
                listenerStateValid[listenerIndex] = true;
                if (options.CaptureRecentCommands)
                    PushCommand("SetListener index=" + listenerIndex + " weight=" + clampedWeight);
            }
            return result == global::FMOD.RESULT.OK
                ? AudioResult.Ok()
                : Fail(AudioErrorCode.BackendFailed, "FMOD listener update failed: " + result, 0, AudioTraceIdentity.None);
#else
            return FailUnavailable();
#endif
        }

        public AudioResult SetBusPaused(int busId, bool paused)
        {
#if MXFRAMEWORK_FMOD
            AudioResult cacheResult = EnsureBusCached(busId);
            if (cacheResult.Failed)
                return cacheResult;
            BusEntry entry = busCache[busId];
            global::FMOD.RESULT result = entry.SetPaused(paused);
            if (result != global::FMOD.RESULT.OK)
                return Fail(AudioErrorCode.BackendFailed, "FMOD bus pause failed: bus=" + busId + " result=" + result, 0, AudioTraceIdentity.None);
            entry.Paused = paused;
            busCache[busId] = entry;
            return AudioResult.Ok();
#else
            return FailUnavailable();
#endif
        }

        /// <remarks>LowFreqAlloc at the Unity or external-backend boundary; managed steady-state work avoids temporary collections with stable inputs.</remarks>
        public void Tick(float deltaTime)
        {
#if MXFRAMEWORK_FMOD
            if (!initialized || handles.Count == 0)
            {
                return;
            }

            stoppedHandles.Clear();
            for (int i = 0; i < activeHandleIds.Count; i++)
            {
                int handleId = activeHandleIds[i];
                ActiveHandle active = handles[handleId];
                active.AgeSeconds += deltaTime;
                global::FMOD.RESULT stateResult = active.Instance.getPlaybackState(out global::FMOD.Studio.PLAYBACK_STATE playbackState);
                if (stateResult != global::FMOD.RESULT.OK || playbackState == global::FMOD.Studio.PLAYBACK_STATE.STOPPED)
                {
                    active.Instance.release();
                    stoppedHandles.Add(handleId);
                    continue;
                }

                handles[handleId] = active;
            }

            for (int i = 0; i < stoppedHandles.Count; i++)
            {
                handles.Remove(stoppedHandles[i]);
                activeHandleIds.Remove(stoppedHandles[i]);
            }
#endif
        }

        public AudioDebugSnapshot CaptureSnapshot()
        {
            var buses = new List<AudioDebugBusState>(busCache.Count);
            foreach (BusEntry cachedBus in busCache.Values)
            {
                BusEntry bus = cachedBus;
#if MXFRAMEWORK_FMOD
                bus.QueryState(out float effectiveVolume, out bool actualMuted, out bool actualPaused);
                buses.Add(new AudioDebugBusState(
                    bus.Definition.Id,
                    bus.Definition.Name,
                    bus.Volume,
                    bus.UsesVca ? bus.Muted : actualMuted,
                    bus.UsesVca ? bus.Paused : actualPaused,
                    effectiveVolume));
#else
                buses.Add(new AudioDebugBusState(bus.Definition.Id, bus.Definition.Name, bus.Volume, bus.Muted, bus.Paused));
#endif
            }

            List<AudioDebugActiveEvent> activeEvents;
#if MXFRAMEWORK_FMOD
            activeEvents = new List<AudioDebugActiveEvent>(handles.Count);
            foreach (KeyValuePair<int, ActiveHandle> pair in handles)
            {
                ActiveHandle active = pair.Value;
                var handle = new AudioHandle(pair.Key, active.Definition.Id, active.EmitterId, active.State);
                activeEvents.Add(new AudioDebugActiveEvent(
                    handle,
                    active.Definition.Name,
                    active.Definition.BusId,
                    active.EmitterId,
                    active.Priority,
                    active.Controlled,
                    active.AgeSeconds));
            }
#else
            activeEvents = new List<AudioDebugActiveEvent>(0);
#endif

            var listeners = new List<AudioDebugListenerState>(listenerStates.Length);
            for (int i = 0; i < listenerStates.Length; i++)
            {
                if (listenerStateValid[i])
                    listeners.Add(listenerStates[i]);
            }
            AudioBankRuntimeState[] banks = bankLifecycle != null
                ? bankLifecycle.CaptureDebugStates()
                : Array.Empty<AudioBankRuntimeState>();

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
                recentErrors.ToList(),
                CapturePerformanceMetrics(),
#if MXFRAMEWORK_FMOD
                totalVoiceRejections,
                totalVoiceSteals
#else
                0,
                0
#endif
                , banks,
                listeners,
                recentCommands.ToList()
                );
        }

        /// <remarks>AllocByDesign diagnostics path. Passing true may synchronously flush FMOD commands.</remarks>
        public AudioBackendPerformanceMetrics CapturePerformanceMetrics(bool flushCommands = false)
        {
#if MXFRAMEWORK_FMOD
            if (!initialized)
                return default;
            global::FMOD.Studio.System studio = global::FMODUnity.RuntimeManager.StudioSystem;
            if (flushCommands)
                studio.flushCommands();
            if (studio.getCoreSystem(out global::FMOD.System core) != global::FMOD.RESULT.OK)
                return default;
            core.getChannelsPlaying(out int channels, out int realChannels);
            studio.getCPUUsage(out global::FMOD.Studio.CPU_USAGE studioCpu, out global::FMOD.CPU_USAGE coreCpu);
            studio.getMemoryUsage(out global::FMOD.Studio.MEMORY_USAGE memory);
            global::FMOD.Memory.GetStats(out int totalMemory, out _);
            return new AudioBackendPerformanceMetrics(
                channels,
                realChannels,
                memory.inclusive > 0 ? memory.inclusive : totalMemory,
                memory.sampledata,
                studioCpu.update,
                coreCpu.dsp,
                coreCpu.stream,
                coreCpu.update);
#else
            return default;
#endif
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
            activeHandleIds.Clear();
#endif
            bankLifecycle?.Dispose();
            bankLifecycle = null;
            busCache.Clear();
            Array.Clear(listenerStateValid, 0, listenerStateValid.Length);
            initialized = false;
        }

    }
}
