using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FMODUnity;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Audio.FMOD.Editor
{
    public static class FmodAudioSetupValidator
    {
        private const string MasterBankName = "Master.bank";
        private const string MasterStringsBankName = "Master.strings.bank";

        [MenuItem("MxFramework/Audio/Validate FMOD Setup", priority = 1000)]
        public static void ValidateCurrentProjectMenu()
        {
            FmodAudioSetupReport report = ValidateCurrentProject();
            string text = CreateReportText(report);
            if (report.HasErrors)
            {
                Debug.LogError(text);
            }
            else if (report.WarningCount > 0)
            {
                Debug.LogWarning(text);
            }
            else
            {
                Debug.Log(text);
            }

            EditorUtility.DisplayDialog(
                "FMOD Setup Validation",
                report.HasErrors ? "FMOD setup has errors. See Console for details." : "FMOD setup validation completed. See Console for details.",
                "OK");
        }

        [MenuItem("MxFramework/Audio/Refresh FMOD Banks", priority = 1001)]
        public static void RefreshFmodBanksMenu()
        {
            bool executed = EditorApplication.ExecuteMenuItem("FMOD/Refresh Banks");
            if (!executed)
            {
                Debug.LogError("[MxAudio] FMOD/Refresh Banks menu item was not available.");
                return;
            }

            Debug.Log("[MxAudio] Requested FMOD bank refresh.");
        }

        public static FmodAudioSetupReport ValidateCurrentProject()
        {
            var report = new FmodAudioSetupReport();
            Settings settings = Settings.Instance;
            if (settings == null)
            {
                report.AddError("FmodSettingsMissing", "FMOD Settings asset was not found.");
                return report;
            }

            ValidateSettings(settings, report);
            string bankRoot = ResolveRuntimeBankRoot(settings);
            ValidateBankDirectory(
                bankRoot,
                settings.MasterBanks,
                settings.Banks,
                settings.BanksToLoad,
                settings.BankLoadType,
                report);
            return report;
        }

        public static void ValidateSettings(Settings settings, FmodAudioSetupReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (settings == null)
            {
                report.AddError("FmodSettingsMissing", "FMOD Settings asset was not found.");
                return;
            }

            if (settings.ImportType != ImportType.StreamingAssets)
            {
                report.AddWarning("FmodImportTypeUnsupported", "Current validation only checks StreamingAssets bank layout. ImportType=" + settings.ImportType + ".");
            }

            if (settings.HasSourceProject && string.IsNullOrWhiteSpace(settings.SourceProjectPath))
            {
                report.AddWarning("FmodSourceProjectMissing", "FMOD Settings is configured for a source project, but SourceProjectPath is empty.");
            }

            if (!settings.HasSourceProject && string.IsNullOrWhiteSpace(settings.SourceBankPath))
            {
                report.AddWarning("FmodSourceBankPathMissing", "FMOD Settings is configured without a source project, but SourceBankPath is empty.");
            }

            if (settings.BankLoadType == BankLoadType.Specified && (settings.BanksToLoad == null || settings.BanksToLoad.Count == 0))
            {
                report.AddError("FmodSpecifiedBanksEmpty", "BankLoadType is Specified, but BanksToLoad is empty.");
            }
        }

        public static FmodAudioSetupReport ValidateBankDirectory(
            string bankRoot,
            IEnumerable<string> masterBanks = null,
            IEnumerable<string> banks = null,
            IEnumerable<string> banksToLoad = null,
            BankLoadType bankLoadType = BankLoadType.All)
        {
            var report = new FmodAudioSetupReport();
            ValidateBankDirectory(bankRoot, masterBanks, banks, banksToLoad, bankLoadType, report);
            return report;
        }

        public static void ValidateBankDirectory(
            string bankRoot,
            IEnumerable<string> masterBanks,
            IEnumerable<string> banks,
            IEnumerable<string> banksToLoad,
            BankLoadType bankLoadType,
            FmodAudioSetupReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (string.IsNullOrWhiteSpace(bankRoot))
            {
                report.AddError("FmodBankRootMissing", "Runtime bank root is empty.");
                return;
            }

            string fullRoot = Path.GetFullPath(bankRoot);
            if (!Directory.Exists(fullRoot))
            {
                report.AddError("FmodBankRootMissing", "Runtime bank root does not exist: " + fullRoot + ".");
                return;
            }

            string[] bankFiles = Directory.GetFiles(fullRoot, "*.bank", SearchOption.AllDirectories);
            if (bankFiles.Length == 0)
            {
                report.AddError("FmodBanksMissing", "No .bank files were found under: " + fullRoot + ".");
                return;
            }

            bool hasMasterBank = HasBankFile(bankFiles, MasterBankName) || HasConfiguredBank(masterBanks, "Master");
            bool hasStringsBank = HasBankFile(bankFiles, MasterStringsBankName);
            if (!hasMasterBank)
            {
                report.AddError("FmodMasterBankMissing", "Master.bank was not found under: " + fullRoot + ".");
            }

            if (!hasStringsBank)
            {
                report.AddError("FmodMasterStringsBankMissing", "Master.strings.bank was not found under: " + fullRoot + ".");
            }

            if (bankLoadType == BankLoadType.All)
            {
                if (!HasAnyConfiguredBank(masterBanks) && !HasAnyConfiguredBank(banks))
                {
                    report.AddWarning("FmodBankCacheEmpty", "FMOD Settings has no cached MasterBanks/Banks. Use FMOD/Refresh Banks after banks are exported.");
                }
            }
            else if (bankLoadType == BankLoadType.Specified && !HasAnyConfiguredBank(banksToLoad))
            {
                report.AddError("FmodSpecifiedBanksEmpty", "BankLoadType is Specified, but BanksToLoad is empty.");
            }

            report.AddInfo("FmodBankFilesFound", "Found " + bankFiles.Length + " bank file(s) under: " + fullRoot + ".");
        }

        public static string ResolveRuntimeBankRoot(Settings settings)
        {
            if (settings == null)
            {
                return string.Empty;
            }

            if (settings.ImportType == ImportType.AssetBundle)
            {
                return string.IsNullOrEmpty(settings.TargetAssetPath)
                    ? Application.dataPath
                    : Path.Combine(Application.dataPath, settings.TargetAssetPath);
            }

            return string.IsNullOrEmpty(settings.TargetBankFolder)
                ? Application.streamingAssetsPath
                : Path.Combine(Application.streamingAssetsPath, settings.TargetBankFolder);
        }

        public static string CreateReportText(FmodAudioSetupReport report)
        {
            var builder = new StringBuilder();
            builder.Append("MxFramework FMOD Setup Validation Report\n");
            builder.Append("errors: ").Append(report != null ? report.ErrorCount : 0).Append('\n');
            builder.Append("warnings: ").Append(report != null ? report.WarningCount : 0).Append('\n');
            builder.Append("issues:\n");

            if (report == null || report.Issues.Count == 0)
            {
                builder.Append("- none\n");
                return builder.ToString();
            }

            for (int i = 0; i < report.Issues.Count; i++)
            {
                FmodAudioSetupIssue issue = report.Issues[i];
                builder.Append("- ")
                    .Append(issue.Severity)
                    .Append(' ')
                    .Append(issue.Code)
                    .Append(" message=")
                    .Append(issue.Message)
                    .Append('\n');
            }

            return builder.ToString();
        }

        private static bool HasBankFile(string[] bankFiles, string expectedFileName)
        {
            for (int i = 0; i < bankFiles.Length; i++)
            {
                if (string.Equals(Path.GetFileName(bankFiles[i]), expectedFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnyConfiguredBank(IEnumerable<string> names)
        {
            if (names == null)
            {
                return false;
            }

            foreach (string name in names)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasConfiguredBank(IEnumerable<string> names, string expected)
        {
            if (names == null)
            {
                return false;
            }

            foreach (string name in names)
            {
                if (string.Equals(name, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
