using System.Collections.Generic;
using MxFramework.Audio;

namespace MxFramework.Settings.Audio
{
    public sealed class MxAudioSettingsApplier : IMxSettingsApplier
    {
        private readonly IAudioService _audioService;

        public MxAudioSettingsApplier(IAudioService audioService, string applierId = "settings.audio")
        {
            _audioService = audioService;
            ApplierId = string.IsNullOrWhiteSpace(applierId) ? "settings.audio" : applierId;
        }

        public string ApplierId { get; }

        public MxSettingsApplyResult Apply(MxSettingsProfile profile)
        {
            if (_audioService == null)
            {
                return MxSettingsApplyResult.Failed(new MxSettingsIssue(
                    MxSettingsIssueCode.AdapterUnavailable,
                    ApplierId,
                    "Audio service is not configured."));
            }

            IReadOnlyList<MxAudioBusSetting> buses =
                profile?.Audio?.BusSettings ?? System.Array.Empty<MxAudioBusSetting>();
            if (buses.Count == 0)
                return MxSettingsApplyResult.Succeeded();

            var issues = new List<MxSettingsIssue>();
            for (int i = 0; i < buses.Count; i++)
            {
                MxAudioBusSetting bus = buses[i];
                string busPath = "$.audio.busSettings[" + i + "]";
                AudioResult volumeResult = _audioService.SetBusVolume(bus.BusId, bus.Volume);
                if (volumeResult.Failed)
                {
                    issues.Add(ToIssue(busPath + ".volume", bus.BusId, volumeResult));
                    if (volumeResult.ErrorCode == AudioErrorCode.InvalidBus)
                        continue;
                }

                AudioResult mutedResult = _audioService.SetBusMuted(bus.BusId, bus.Muted);
                if (mutedResult.Failed)
                    issues.Add(ToIssue(busPath + ".muted", bus.BusId, mutedResult));
            }

            return issues.Count == 0
                ? MxSettingsApplyResult.Succeeded()
                : MxSettingsApplyResult.Failed(issues);
        }

        private static MxSettingsIssue ToIssue(string path, int busId, AudioResult result)
        {
            return new MxSettingsIssue(
                MxSettingsIssueCode.ApplyFailed,
                path,
                "Failed to apply audio bus " + busId + ": " + result.ErrorCode + " " + result.Message);
        }
    }
}
