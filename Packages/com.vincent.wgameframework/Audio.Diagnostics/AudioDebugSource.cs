using System;
using System.Globalization;
using System.Text;
using MxFramework.Diagnostics;

namespace MxFramework.Audio.Diagnostics
{
    /// <summary>AllocByDesign runtime diagnostics for backend, voice, bus and presentation state.</summary>
    public sealed class AudioDebugSource : IFrameworkDebugSource
    {
        private readonly IAudioService _service;
        private readonly AudioPresentationDrain _presentation;

        public AudioDebugSource(IAudioService service, AudioPresentationDrain presentation = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _presentation = presentation;
        }

        public string Name => "Audio";
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => true;

        /// <remarks>AllocByDesign. Call only from diagnostics capture surfaces.</remarks>
        public FrameworkDebugSnapshot CreateSnapshot()
        {
            AudioDebugSnapshot audio = _service.CaptureSnapshot();
            AudioBackendPerformanceMetrics metrics = audio.PerformanceMetrics;
            var status = new StringBuilder()
                .Append("initialized=").Append(audio.Initialized)
                .Append(" plays=").Append(audio.TotalPlayRequests)
                .Append(" stops=").Append(audio.TotalStopRequests)
                .Append(" active=").Append(audio.ActiveEventCount)
                .Append(" rejected=").Append(audio.TotalVoiceRejections)
                .Append(" stolen=").Append(audio.TotalVoiceSteals)
                .Append(" channels=").Append(metrics.Channels)
                .Append(" realChannels=").Append(metrics.RealChannels)
                .Append(" memoryBytes=").Append(metrics.MemoryBytes)
                .Append(" studioCpu=").Append(metrics.StudioUpdateCpuPercent.ToString("F3", CultureInfo.InvariantCulture))
                .Append(" dspCpu=").Append(metrics.DspCpuPercent.ToString("F3", CultureInfo.InvariantCulture));

            var buses = new StringBuilder();
            for (int i = 0; i < audio.BusStates.Count; i++)
            {
                AudioDebugBusState bus = audio.BusStates[i];
                if (i > 0)
                    buses.Append('\n');
                buses.Append("id=").Append(bus.BusId)
                    .Append(" name=").Append(bus.Name)
                    .Append(" volume=").Append(bus.Volume.ToString("F3", CultureInfo.InvariantCulture))
                    .Append(" muted=").Append(bus.Muted)
                    .Append(" paused=").Append(bus.Paused);
            }

            var voices = new StringBuilder();
            for (int i = 0; i < audio.ActiveEvents.Count; i++)
            {
                AudioDebugActiveEvent voice = audio.ActiveEvents[i];
                if (i > 0)
                    voices.Append('\n');
                voices.Append("handle=").Append(voice.Handle.Id)
                    .Append(" event=").Append(voice.Handle.EventId)
                    .Append(" bus=").Append(voice.BusId)
                    .Append(" emitter=").Append(voice.EmitterId)
                    .Append(" priority=").Append(voice.Priority)
                    .Append(" controlled=").Append(voice.Controlled)
                    .Append(" age=").Append(voice.AgeSeconds.ToString("F3", CultureInfo.InvariantCulture));
            }

            var errors = new StringBuilder();
            for (int i = 0; i < audio.RecentErrors.Count; i++)
            {
                AudioDebugError error = audio.RecentErrors[i];
                if (i > 0)
                    errors.Append('\n');
                errors.Append(error.Code).Append(" frame=").Append(error.Frame).Append(" trace=").Append(error.TraceId).Append(" ").Append(error.Message);
            }

            var banks = new StringBuilder();
            for (int i = 0; i < audio.Banks.Count; i++)
            {
                AudioBankRuntimeState bank = audio.Banks[i];
                if (i > 0)
                    banks.Append('\n');
                banks.Append("name=").Append(bank.StableName)
                    .Append(" group=").Append(bank.Group)
                    .Append(" scope=").Append(bank.Scope)
                    .Append(" refs=").Append(bank.References)
                    .Append(" required=").Append(bank.Required);
            }

            var listeners = new StringBuilder();
            for (int i = 0; i < audio.Listeners.Count; i++)
            {
                AudioDebugListenerState listener = audio.Listeners[i];
                if (i > 0)
                    listeners.Append('\n');
                listeners.Append("index=").Append(listener.Index)
                    .Append(" weight=").Append(listener.Weight.ToString("F3", CultureInfo.InvariantCulture))
                    .Append(" position=(")
                    .Append(listener.Transform.X.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                    .Append(listener.Transform.Y.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                    .Append(listener.Transform.Z.ToString("F2", CultureInfo.InvariantCulture)).Append(')');
            }

            var decisions = new StringBuilder();
            for (int i = 0; i < audio.RecentDecisions.Count; i++)
            {
                if (i > 0)
                    decisions.Append('\n');
                decisions.Append(audio.RecentDecisions[i]);
            }

            string presentation = "unavailable";
            if (_presentation != null)
            {
                AudioPresentationDrainSnapshot drain = _presentation.Snapshot;
                presentation = "accepted=" + drain.Accepted
                    + " failed=" + drain.Failed
                    + " replaySuppressed=" + drain.SuppressedReplay
                    + " awaitingConfirmation=" + drain.AwaitingConfirmation
                    + " deduped=" + drain.Deduped
                    + " budgetRejected=" + drain.BudgetRejected
                    + " stolen=" + drain.Stolen
                    + " controlled=" + drain.ControlledHandles
                    + " sourceDropped=" + drain.SourceDropped
                    + " scopes(frame/entity/scene/app)="
                    + drain.TransientFrameHandles + "/"
                    + drain.EntityHandles + "/"
                    + drain.SceneHandles + "/"
                    + drain.AppPersistentHandles;
            }

            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Status", status.ToString()),
                    new FrameworkDebugSection("Buses", buses.ToString()),
                    new FrameworkDebugSection("Banks", banks.ToString()),
                    new FrameworkDebugSection("Listeners", listeners.ToString()),
                    new FrameworkDebugSection("Voices", voices.ToString()),
                    new FrameworkDebugSection("Presentation", presentation),
                    new FrameworkDebugSection("Errors", errors.ToString()),
                    new FrameworkDebugSection("Recent Decisions", decisions.ToString())
                });
        }
    }
}
