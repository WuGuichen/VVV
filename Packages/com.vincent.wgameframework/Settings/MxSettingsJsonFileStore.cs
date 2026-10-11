using System;
using System.IO;

namespace MxFramework.Settings
{
    public interface IMxSettingsStore
    {
        MxSettingsLoadResult LoadOrDefault();
        MxSettingsSaveResult Save(MxSettingsProfile profile);
    }

    public sealed class MxSettingsJsonFileStore : IMxSettingsStore
    {
        public MxSettingsJsonFileStore(string filePath)
        {
            FilePath = filePath ?? string.Empty;
        }

        public string FilePath { get; }

        public MxSettingsLoadResult LoadOrDefault()
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                return MxSettingsLoadResult.WithIssue(
                    MxSettingsProfile.CreateDefault(),
                    new MxSettingsIssue(
                        MxSettingsIssueCode.InvalidPath,
                        string.Empty,
                        "Settings file path is null or empty."));
            }

            try
            {
                if (!File.Exists(FilePath))
                {
                    return MxSettingsLoadResult.WithIssue(
                        MxSettingsProfile.CreateDefault(),
                        new MxSettingsIssue(
                            MxSettingsIssueCode.MissingFile,
                            FilePath,
                            "Settings file does not exist; using defaults."));
                }

                return MxSettingsProfileJson.LoadFromJson(File.ReadAllText(FilePath));
            }
            catch (Exception ex)
            {
                return MxSettingsLoadResult.WithIssue(
                    MxSettingsProfile.CreateDefault(),
                    new MxSettingsIssue(
                        MxSettingsIssueCode.IoError,
                        FilePath,
                        "Failed to load settings file: " + ex.Message,
                        ex));
            }
        }

        public MxSettingsSaveResult Save(MxSettingsProfile profile)
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                return MxSettingsSaveResult.Failed(new MxSettingsIssue(
                    MxSettingsIssueCode.InvalidPath,
                    string.Empty,
                    "Settings file path is null or empty."));
            }

            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(FilePath, MxSettingsProfileJson.SaveToJson(profile));
                return MxSettingsSaveResult.Succeeded();
            }
            catch (Exception ex)
            {
                return MxSettingsSaveResult.Failed(new MxSettingsIssue(
                    MxSettingsIssueCode.IoError,
                    FilePath,
                    "Failed to save settings file: " + ex.Message,
                    ex));
            }
        }
    }
}
