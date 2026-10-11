using System;
using System.Collections;
using System.IO;
using MxFramework.Audio;
using MxFramework.Audio.Config;
using MxFramework.Audio.FMOD;
using MxFramework.Input;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Demo
{
    public sealed partial class FmodAudioDemoRunner
    {
        public void RunAutomatedSmoke()
        {
            if (_smokeStatus == FmodAudioSmokeStatus.Running)
                return;

            StartCoroutine(RunAutomatedSmokeRoutine());
        }

        public void ConfigureValidationDefaults()
        {
            _oneShotEventPath = "event:/MxFramework/Demo/OneShot";
            _oneShotEventGuid = "{532d60e2-a86b-468f-a2f8-5d69efcf7207}";
            _oneShotIs3D = false;
            _oneShot3DEventPath = "event:/MxFramework/Demo/OneShot3D";
            _oneShot3DEventGuid = "{5b50605b-36ec-4999-96c8-ec6cc5fcaa28}";
            _loopEventPath = "event:/MxFramework/Demo/Loop";
            _loopEventGuid = "{7d11a16f-e7b2-452b-8cc0-6e553d1dfd10}";
            _loopIs3D = false;
            _busPath = "bus:/";
            _vcaPath = string.Empty;
            _busVolume = 1f;
            _useParameter = false;
            _initializeOnAwake = true;
            _playOneShotOnStart = false;
            _startLoopOnStart = false;
            _runAutomatedSmokeOnStart = true;
            _quitPlayerAfterAutomatedSmoke = true;
            _automatedSmokeDelaySeconds = 0.25f;
            _loopStopMode = AudioStopMode.Immediate;
        }

        private IEnumerator RunAutomatedSmokeRoutine()
        {
            _smokeStatus = FmodAudioSmokeStatus.Running;
            _lastSmokeReport = "status: Running";
            if (_automatedSmokeDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(_automatedSmokeDelaySeconds);
            else
                yield return null;

            string failure = null;
            try
            {
                RunAutomatedSmokeNow();
            }
            catch (Exception exception)
            {
                failure = exception.Message;
            }

            if (failure == null)
            {
                yield return new WaitForSecondsRealtime(0.65f);
                if (_runtimeHost != null && _runtimeHost.State == RuntimeLifecycleState.Started)
                    _runtimeHost.Tick(_frameIndex++, 0f, _elapsedTime);
            }
            if (failure == null && HasActiveControlledEvent(Snapshot))
                failure = "controlled handle did not leave the active set after Stop";

            CompleteSmoke(
                failure == null ? FmodAudioSmokeStatus.Passed : FmodAudioSmokeStatus.Failed,
                failure ?? "all runtime operations succeeded");
        }

        private void RunAutomatedSmokeNow()
        {
            EnsureAudio();
            Require(_audioService.CaptureSnapshot().Initialized, "initialize", "FMOD backend is unavailable.");

            long firstPlayStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            AudioPlayResult oneShot2D = _audioService.PlayOneShot(
                AudioPlayRequest.Create2D(OneShotEventId, frame: Time.frameCount,
                    traceId: new AudioTraceIdentity(OneShotEventId, 0, Time.frameCount, 2)));
            _firstPlayCallMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - firstPlayStarted)
                * 1000d
                / System.Diagnostics.Stopwatch.Frequency;
            Require(oneShot2D.Success, "oneShot2D", oneShot2D.Message);

            Vector3 position = transform.position;
            Vector3 forward = transform.forward;
            var audioTransform = new AudioTransform(position.x, position.y, position.z, forward.x, forward.y, forward.z);
            AudioPlayResult oneShot3D = _audioService.PlayOneShot(AudioPlayRequest.Create3D(
                OneShot3DEventId,
                audioTransform,
                GetEmitterId(gameObject),
                frame: Time.frameCount,
                traceId: new AudioTraceIdentity(OneShot3DEventId, GetEmitterId(gameObject), Time.frameCount, 3)));
            Require(oneShot3D.Success, "oneShot3D", oneShot3D.Message);

            AudioPlayResult start = _audioService.StartEvent(
                AudioPlayRequest.Create3D(
                    OneShot3DEventId,
                    audioTransform,
                    GetEmitterId(gameObject),
                    frame: Time.frameCount,
                    traceId: new AudioTraceIdentity(OneShot3DEventId, GetEmitterId(gameObject), Time.frameCount, 4)),
                out AudioHandle controlledHandle);
            Require(start.Success && controlledHandle.IsValid, "startEvent", start.Message);
            _smokePerformanceMetrics = _audioService.CapturePerformanceMetrics(flushCommands: true);

            var movedTransform = new AudioTransform(
                position.x + 1f,
                position.y,
                position.z,
                forward.x,
                forward.y,
                forward.z,
                velocityX: 1f);
            Require(_audioService.Update3DAttributes(controlledHandle, movedTransform).Success, "emitterUpdate", "Update3DAttributes failed.");
            Require(_audioService.SetListenerAttributes(0, AudioTransform.Origin).Success, "listenerUpdate", "SetListenerAttributes failed.");

            Require(_audioService.SetBusVolume(DemoBusId, 0.35f).Success, "busVolumeLow", "SetBusVolume failed.");
            Require(_audioService.SetBusMuted(DemoBusId, true).Success, "busMute", "SetBusMuted(true) failed.");
            Require(_audioService.SetBusVolume(DemoBusId, 0.8f).Success, "busVolumeWhileMuted", "Muted volume update failed.");
            Require(_audioService.SetBusMuted(DemoBusId, false).Success, "busUnmute", "SetBusMuted(false) failed.");
            Require(_audioService.SetBusPaused(DemoBusId, true).Success, "busPause", "SetBusPaused(true) failed.");
            Require(_audioService.SetBusPaused(DemoBusId, false).Success, "busResume", "SetBusPaused(false) failed.");

            AudioDebugSnapshot snapshot = _audioService.CaptureSnapshot();
            Require(snapshot.BusStates.Count == 1, "busSnapshot", "Expected one cached bus.");
            Require(Mathf.Abs(snapshot.BusStates[0].Volume - 0.8f) < 0.001f, "busSnapshot", "Cached bus volume mismatch.");
            Require(!snapshot.BusStates[0].Muted, "busSnapshot", "Cached bus mute mismatch.");
            Require(!snapshot.BusStates[0].Paused, "busSnapshot", "Cached bus pause mismatch.");

            AudioResult stop = _audioService.Stop(controlledHandle, AudioStopMode.Immediate);
            Require(stop.Success, "stopEvent", stop.Message);
        }

        private void CompleteSmoke(FmodAudioSmokeStatus status, string detail)
        {
            _smokeStatus = status;
            AudioDebugSnapshot snapshot = Snapshot;
            AudioBackendPerformanceMetrics metrics = _smokePerformanceMetrics;
            _lastSmokeReport =
                "status: " + status + "\n"
                + "detail: " + (detail ?? string.Empty) + "\n"
                + "totalPlayRequests: " + snapshot.TotalPlayRequests + "\n"
                + "totalStopRequests: " + snapshot.TotalStopRequests + "\n"
                + "activeEventCount: " + snapshot.ActiveEventCount + "\n"
                + "recentErrorCount: " + snapshot.RecentErrors.Count + "\n"
                + "firstPlayCallMilliseconds: " + _firstPlayCallMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "\n"
                + "channels: " + metrics.Channels + "\n"
                + "realChannels: " + metrics.RealChannels + "\n"
                + "memoryBytes: " + metrics.MemoryBytes + "\n"
                + "sampleMemoryBytes: " + metrics.SampleMemoryBytes + "\n"
                + "studioUpdateCpuPercent: " + metrics.StudioUpdateCpuPercent.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "\n"
                + "dspCpuPercent: " + metrics.DspCpuPercent.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "\n";
            WriteSmokeReport(_lastSmokeReport);

            if (status == FmodAudioSmokeStatus.Passed)
                Debug.Log("[MxAudioValidation] status=Passed report=" + _lastSmokeReportPath);
            else
                Debug.LogError("[MxAudioValidation] status=Failed detail=" + detail + " report=" + _lastSmokeReportPath);

            if (_quitPlayerAfterAutomatedSmoke && !Application.isEditor)
                StartCoroutine(QuitPlayer(status));
        }

        private IEnumerator QuitPlayer(FmodAudioSmokeStatus status)
        {
            yield return null;
            DisposeAudio();
            yield return null;
            Application.Quit(status == FmodAudioSmokeStatus.Passed ? 0 : 1);
        }

        private void WriteSmokeReport(string report)
        {
            string directory = Path.Combine(Application.persistentDataPath, "MxFramework", "AudioValidation");
            Directory.CreateDirectory(directory);
            _lastSmokeReportPath = Path.Combine(directory, "audio-smoke-report.txt");
            File.WriteAllText(_lastSmokeReportPath, report ?? string.Empty);
        }

        private static bool HasActiveControlledEvent(AudioDebugSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.ActiveEvents.Count; i++)
            {
                if (snapshot.ActiveEvents[i].Controlled)
                    return true;
            }

            return false;
        }

        private static void Require(bool condition, string step, string message)
        {
            if (!condition)
                throw new InvalidOperationException(step + ": " + (message ?? "failed"));
        }
    }
}
