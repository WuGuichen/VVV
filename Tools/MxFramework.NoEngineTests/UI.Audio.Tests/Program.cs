using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.UI;
using MxFramework.UI.Audio;

namespace MxFramework.NoEngineTests.UI.Audio
{
    internal static class Program
    {
        private static readonly MxUiFeedbackContext Context =
            new MxUiFeedbackContext(
                new MxUiViewId("ui.test"),
                "ui.test.click",
                1,
                MxUiFeedbackPhase.Activation);

        private static int Main()
        {
            FullCueSemanticsAreForwarded();
            MissingAndFailurePathsStaySilent();
            WarmTryPlayDoesNotAllocate();
            Console.WriteLine("UI Audio feedback bridge noEngine tests passed.");
            return 0;
        }

        private static void FullCueSemanticsAreForwarded()
        {
            var defaults = new[] { new AudioParameterValue(500201, 0.5f) };
            var provider = new CueProvider(
                Cue(MxAudioUiFeedbackCueKeys.Click, 500501, 500601, defaults),
                Cue(MxAudioUiFeedbackCueKeys.Confirm, 500502, 500602),
                Cue(MxAudioUiFeedbackCueKeys.Back, 500503, 500603),
                Cue(MxAudioUiFeedbackCueKeys.Error, 500504, 500604));
            var service = new FakeAudioService();
            var player = new MxAudioUiFeedbackPlayer(service, provider);

            Require(
                player.TryPlay(MxUiFeedbackKind.Click, in Context),
                "Click did not play.");
            Require(service.LastRequest.EventId == 500601, "Click event mismatch.");
            Require(service.LastRequest.Priority == 80, "Priority mismatch.");
            Require(service.LastRequest.TraceId == "audio.ui.click", "Trace mismatch.");
            Require(
                ReferenceEquals(
                    provider.Click.DefaultParameters,
                    service.LastRequest.Parameters),
                "Cue default parameter view was not forwarded intact.");
            Require(
                service.LastRequest.Parameters[0].ParameterId == 500201,
                "Cue default parameter was lost.");
        }

        private static void MissingAndFailurePathsStaySilent()
        {
            var service = new FakeAudioService();
            var missing = new MxAudioUiFeedbackPlayer(
                service,
                new CueProvider());
            Require(
                !missing.TryPlay(MxUiFeedbackKind.Error, in Context),
                "Missing cue should stay silent.");
            Require(
                missing.CaptureDebugSnapshot().LastDiagnostic ==
                MxAudioUiFeedbackDiagnosticCode.CueMissing,
                "Missing cue diagnostic mismatch.");

            service.Result = AudioPlayResult.Fail(
                AudioErrorCode.BackendUnavailable,
                "offline");
            var rejected = new MxAudioUiFeedbackPlayer(
                service,
                new CueProvider(Cue(
                    MxAudioUiFeedbackCueKeys.Click,
                    500501,
                    500601)));
            Require(
                !rejected.TryPlay(MxUiFeedbackKind.Click, in Context),
                "Rejected audio should stay silent.");
            Require(
                rejected.CaptureDebugSnapshot().LastAudioError ==
                AudioErrorCode.BackendUnavailable,
                "Audio error diagnostic mismatch.");
        }

        private static void WarmTryPlayDoesNotAllocate()
        {
            var player = new MxAudioUiFeedbackPlayer(
                new FakeAudioService(),
                new CueProvider(Cue(
                    MxAudioUiFeedbackCueKeys.Click,
                    500501,
                    500601)));
            player.TryPlay(MxUiFeedbackKind.Click, in Context);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4096; i++)
                player.TryPlay(MxUiFeedbackKind.Click, in Context);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Require(after == before, "Warm TryPlay allocated managed memory.");
        }

        private static AudioCueDefinition Cue(
            string key,
            int cueId,
            int eventId,
            AudioParameterValue[] defaults = null)
        {
            return new AudioCueDefinition(
                cueId,
                key,
                eventId,
                AudioCuePlayMode.OneShot,
                defaults,
                AudioCueMissingPolicy.Mute);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class CueProvider : IAudioCueKeyDefinitionProvider
        {
            private readonly Dictionary<string, AudioCueDefinition> _values =
                new Dictionary<string, AudioCueDefinition>(StringComparer.Ordinal);

            public CueProvider(params AudioCueDefinition[] values)
            {
                for (int i = 0; i < values.Length; i++)
                    _values.Add(values[i].Name, values[i]);
                _values.TryGetValue(
                    MxAudioUiFeedbackCueKeys.Click,
                    out AudioCueDefinition click);
                Click = click;
            }

            public AudioCueDefinition Click { get; }

            public bool TryGetCue(
                string audioCueId,
                out AudioCueDefinition definition)
            {
                return _values.TryGetValue(audioCueId, out definition);
            }
        }

        private sealed class FakeAudioService : IAudioService
        {
            public AudioPlayResult Result { get; set; } =
                AudioPlayResult.Ok(AudioHandle.Invalid);
            public AudioPlayRequest LastRequest { get; private set; }

            public AudioPlayResult PlayOneShot(in AudioPlayRequest request)
            {
                LastRequest = request;
                return Result;
            }

            public AudioPlayResult StartEvent(
                in AudioPlayRequest request,
                out AudioHandle handle)
            {
                handle = AudioHandle.Invalid;
                return Result;
            }

            public AudioResult Stop(
                AudioHandle handle,
                AudioStopMode stopMode) => AudioResult.Ok();

            public AudioResult SetParameter(
                AudioHandle handle,
                int parameterId,
                float value) => AudioResult.Ok();

            public AudioResult SetBusVolume(
                int busId,
                float volume) => AudioResult.Ok();

            public AudioResult SetBusMuted(
                int busId,
                bool muted) => AudioResult.Ok();

            public AudioDebugSnapshot CaptureSnapshot() => null;

            public void Tick(float deltaTime)
            {
            }
        }
    }
}
