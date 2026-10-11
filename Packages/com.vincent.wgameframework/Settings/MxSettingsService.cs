using System;
using System.Collections.Generic;

namespace MxFramework.Settings
{
    public interface IMxSettingsApplier
    {
        string ApplierId { get; }
        MxSettingsApplyResult Apply(MxSettingsProfile profile);
    }

    public sealed class MxSettingsService
    {
        private readonly IMxSettingsStore _store;
        private readonly List<IMxSettingsApplier> _appliers = new List<IMxSettingsApplier>();

        public MxSettingsService(IMxSettingsStore store, MxSettingsProfile initialProfile = null)
        {
            _store = store;
            CurrentProfile = initialProfile ?? MxSettingsProfile.CreateDefault();
        }

        public MxSettingsProfile CurrentProfile { get; private set; }
        public IReadOnlyList<IMxSettingsApplier> Appliers => _appliers;

        public void RegisterApplier(IMxSettingsApplier applier)
        {
            if (applier == null)
                throw new ArgumentNullException(nameof(applier));

            _appliers.Add(applier);
        }

        public MxSettingsLoadResult Load()
        {
            MxSettingsLoadResult result = _store != null
                ? _store.LoadOrDefault()
                : MxSettingsLoadResult.WithIssue(
                    MxSettingsProfile.CreateDefault(),
                    new MxSettingsIssue(
                        MxSettingsIssueCode.AdapterUnavailable,
                        "settings.store",
                        "Settings store is not configured."));

            CurrentProfile = result.Profile ?? MxSettingsProfile.CreateDefault();
            return result;
        }

        public MxSettingsSaveResult Save()
        {
            return Save(CurrentProfile);
        }

        public MxSettingsSaveResult Save(MxSettingsProfile profile)
        {
            CurrentProfile = (profile ?? MxSettingsProfile.CreateDefault()).WithUpdatedUtcNow();
            if (_store == null)
            {
                return MxSettingsSaveResult.Failed(new MxSettingsIssue(
                    MxSettingsIssueCode.AdapterUnavailable,
                    "settings.store",
                    "Settings store is not configured."));
            }

            return _store.Save(CurrentProfile);
        }

        public MxSettingsApplyResult Apply()
        {
            return Apply(CurrentProfile);
        }

        public MxSettingsApplyResult Apply(MxSettingsProfile profile)
        {
            CurrentProfile = profile ?? MxSettingsProfile.CreateDefault();
            if (_appliers.Count == 0)
                return MxSettingsApplyResult.Succeeded();

            var results = new MxSettingsApplyResult[_appliers.Count];
            for (int i = 0; i < _appliers.Count; i++)
            {
                IMxSettingsApplier applier = _appliers[i];
                if (applier == null)
                {
                    results[i] = MxSettingsApplyResult.Failed(new MxSettingsIssue(
                        MxSettingsIssueCode.AdapterUnavailable,
                        "settings.appliers[" + i + "]",
                        "Settings applier is null."));
                    continue;
                }

                try
                {
                    results[i] = applier.Apply(CurrentProfile);
                }
                catch (Exception ex)
                {
                    results[i] = MxSettingsApplyResult.Failed(new MxSettingsIssue(
                        MxSettingsIssueCode.ApplyFailed,
                        applier.ApplierId,
                        "Settings applier threw: " + ex.Message,
                        ex));
                }
            }

            return MxSettingsApplyResult.Combine(results);
        }
    }
}
