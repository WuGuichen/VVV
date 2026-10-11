using MxFramework.Settings;

namespace MxFramework.Settings.Graphics
{
    public interface IMxHdrOutputRuntime
    {
        MxHdrOutputStatus Status { get; }
        bool TryApply(MxHdrOutputSettings settings, out string message);
    }

    public sealed class MxHdrOutputSettingsApplier : IMxSettingsApplier
    {
        private readonly IMxHdrOutputRuntime _runtime;
        public MxHdrOutputSettingsApplier(IMxHdrOutputRuntime runtime) { _runtime = runtime; }
        public string ApplierId => "settings.graphics.hdr";

        public MxSettingsApplyResult Apply(MxSettingsProfile profile)
        {
            var settings = profile?.Graphics?.HdrOutput ?? MxHdrOutputSettings.Default;
            if (_runtime == null)
                return settings.Mode == MxHdrOutputMode.Default ? MxSettingsApplyResult.Succeeded() :
                    MxSettingsApplyResult.Failed(new MxSettingsIssue(MxSettingsIssueCode.AdapterUnavailable,
                        "graphics.hdrOutput", "HDR display adapter is unavailable."));
            return _runtime.TryApply(settings, out string message) ? MxSettingsApplyResult.Succeeded() :
                MxSettingsApplyResult.Failed(new MxSettingsIssue(MxSettingsIssueCode.ApplyFailed,
                    "graphics.hdrOutput", message));
        }
    }
}
