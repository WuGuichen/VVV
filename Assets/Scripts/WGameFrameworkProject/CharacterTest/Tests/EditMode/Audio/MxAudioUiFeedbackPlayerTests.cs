using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.UI;
using MxFramework.UI.Audio;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public class MxAudioUiFeedbackPlayerTests
    {
        private static readonly MxUiFeedbackContext Context =
            new MxUiFeedbackContext(
                new MxUiViewId("ui.test"),
                "ui.test.command",
                7,
                MxUiFeedbackPhase.Activation);

        [Test]
        public void TryPlay_UsesStableCueMappingAndPreservesFullCueDefaults()
        {
            var defaults = new[] { new AudioParameterValue(500201, 0.75f) };
            var provider = new FakeCueProvider(
                Cue(MxAudioUiFeedbackCueKeys.Click, 500501, 500601, defaults),
                Cue(MxAudioUiFeedbackCueKeys.Confirm, 500502, 500602),
                Cue(MxAudioUiFeedbackCueKeys.Back, 500503, 500603),
                Cue(MxAudioUiFeedbackCueKeys.Error, 500504, 500604));
            var service = new FakeAudioService();
            var player = new MxAudioUiFeedbackPlayer(service, provider);

            Assert.IsTrue(player.TryPlay(MxUiFeedbackKind.Click, in Context));
            Assert.AreEqual(500601, service.LastRequest.EventId);
            Assert.AreEqual(MxAudioUiFeedbackPlayer.Priority, service.LastRequest.Priority);
            Assert.AreEqual(new AudioTraceIdentity(AudioTraceKindIds.UiClick), service.LastRequest.TraceId);
            Assert.AreSame(
                provider.Click.DefaultParameters,
                service.LastRequest.Parameters);
            Assert.AreEqual(500201, service.LastRequest.Parameters[0].ParameterId);

            Assert.IsTrue(player.TryPlay(MxUiFeedbackKind.Confirm, in Context));
            Assert.AreEqual(500602, service.LastRequest.EventId);
            Assert.AreEqual(new AudioTraceIdentity(AudioTraceKindIds.UiConfirm), service.LastRequest.TraceId);
            Assert.IsTrue(player.TryPlay(MxUiFeedbackKind.Back, in Context));
            Assert.AreEqual(500603, service.LastRequest.EventId);
            Assert.AreEqual(new AudioTraceIdentity(AudioTraceKindIds.UiBack), service.LastRequest.TraceId);
            Assert.IsTrue(player.TryPlay(MxUiFeedbackKind.Error, in Context));
            Assert.AreEqual(500604, service.LastRequest.EventId);
            Assert.AreEqual(new AudioTraceIdentity(AudioTraceKindIds.UiError), service.LastRequest.TraceId);
        }

        [Test]
        public void TryPlay_MissingOrInvalidDependenciesStaySilentWithDiagnostics()
        {
            var missingService = new MxAudioUiFeedbackPlayer(
                null,
                new FakeCueProvider(
                    Cue(MxAudioUiFeedbackCueKeys.Click, 500501, 500601)));
            Assert.IsFalse(missingService.TryPlay(MxUiFeedbackKind.Click, in Context));
            Assert.AreEqual(
                MxAudioUiFeedbackDiagnosticCode.AudioServiceMissing,
                missingService.CaptureDebugSnapshot().LastDiagnostic);

            var missingCue = new MxAudioUiFeedbackPlayer(
                new FakeAudioService(),
                new FakeCueProvider());
            Assert.IsFalse(missingCue.TryPlay(MxUiFeedbackKind.Error, in Context));
            Assert.AreEqual(
                MxAudioUiFeedbackDiagnosticCode.CueMissing,
                missingCue.CaptureDebugSnapshot().LastDiagnostic);

            var invalidCue = new MxAudioUiFeedbackPlayer(
                new FakeAudioService(),
                new FakeCueProvider(new AudioCueDefinition(
                    500501,
                    MxAudioUiFeedbackCueKeys.Click,
                    500601,
                    AudioCuePlayMode.StartEvent,
                    missingPolicy: AudioCueMissingPolicy.FailRequest)));
            Assert.IsFalse(invalidCue.TryPlay(MxUiFeedbackKind.Click, in Context));
            Assert.AreEqual(
                MxAudioUiFeedbackDiagnosticCode.CueContractInvalid,
                invalidCue.CaptureDebugSnapshot().LastDiagnostic);
        }

        [Test]
        public void TryPlay_AudioFailureOrExceptionNeverEscapes()
        {
            AudioCueDefinition click = Cue(
                MxAudioUiFeedbackCueKeys.Click,
                500501,
                500601);
            var rejectedService = new FakeAudioService
            {
                Result = AudioPlayResult.Fail(
                    AudioErrorCode.BackendUnavailable,
                    "offline")
            };
            var rejected = new MxAudioUiFeedbackPlayer(
                rejectedService,
                new FakeCueProvider(click));

            Assert.IsFalse(rejected.TryPlay(MxUiFeedbackKind.Click, in Context));
            MxAudioUiFeedbackDebugSnapshot rejectedSnapshot =
                rejected.CaptureDebugSnapshot();
            Assert.AreEqual(
                MxAudioUiFeedbackDiagnosticCode.PlaybackRejected,
                rejectedSnapshot.LastDiagnostic);
            Assert.AreEqual(
                AudioErrorCode.BackendUnavailable,
                rejectedSnapshot.LastAudioError);

            var throwing = new MxAudioUiFeedbackPlayer(
                new FakeAudioService { ThrowOnPlay = true },
                new FakeCueProvider(click));
            Assert.DoesNotThrow(() =>
                throwing.TryPlay(MxUiFeedbackKind.Click, in Context));
            Assert.AreEqual(
                MxAudioUiFeedbackDiagnosticCode.PlaybackException,
                throwing.CaptureDebugSnapshot().LastDiagnostic);
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

        private sealed class FakeCueProvider : IAudioCueKeyDefinitionProvider
        {
            private readonly Dictionary<string, AudioCueDefinition> _cues =
                new Dictionary<string, AudioCueDefinition>(StringComparer.Ordinal);

            public FakeCueProvider(params AudioCueDefinition[] cues)
            {
                for (int i = 0; i < cues.Length; i++)
                    _cues.Add(cues[i].Name, cues[i]);
                _cues.TryGetValue(
                    MxAudioUiFeedbackCueKeys.Click,
                    out AudioCueDefinition click);
                Click = click;
            }

            public AudioCueDefinition Click { get; }

            public bool TryGetCue(
                string audioCueId,
                out AudioCueDefinition definition)
            {
                return _cues.TryGetValue(audioCueId, out definition);
            }
        }

        private sealed class FakeAudioService : IAudioService
        {
            public AudioPlayResult Result { get; set; } =
                AudioPlayResult.Ok(AudioHandle.Invalid);
            public bool ThrowOnPlay { get; set; }
            public AudioPlayRequest LastRequest { get; private set; }

            public AudioPlayResult PlayOneShot(in AudioPlayRequest request)
            {
                if (ThrowOnPlay)
                    throw new InvalidOperationException("test");
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

            public AudioDebugSnapshot CaptureSnapshot() =>
                AudioDebugSnapshot.Empty;

            public void Tick(float deltaTime)
            {
            }
        }
    }
}
