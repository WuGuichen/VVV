using System;

namespace MxFramework.Settings
{
    /// <summary>Merges legacy audio profiles into the current stable Bus schema and retires unknown ids.</summary>
    public static class MxAudioSettingsMigration
    {
        /// <remarks>LowFreqAlloc during profile load/migration.</remarks>
        public static MxAudioSettings Migrate(MxAudioSettings source, MxAudioBusSetting[] currentDefaults)
        {
            currentDefaults = currentDefaults ?? Array.Empty<MxAudioBusSetting>();
            var migrated = new MxAudioBusSetting[currentDefaults.Length];
            var existing = source?.BusSettings ?? Array.Empty<MxAudioBusSetting>();
            for (int i = 0; i < currentDefaults.Length; i++)
            {
                MxAudioBusSetting value = currentDefaults[i];
                for (int sourceIndex = 0; sourceIndex < existing.Count; sourceIndex++)
                {
                    if (existing[sourceIndex].BusId == value.BusId)
                    {
                        value = existing[sourceIndex];
                        break;
                    }
                }
                migrated[i] = value;
            }
            return new MxAudioSettings(migrated);
        }
    }
}
