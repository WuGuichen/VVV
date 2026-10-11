using System;
using System.IO;
using MxFramework.CharacterAction;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Minimal source-file compilation entry for the default validation loadout.</summary>
    internal static class CharacterTestTacticalCardAuthoring
    {
        private const string CompileMenu = "MxFramework/CharacterTest/Tactical Card/Compile Default Loadout";
        private const string MeasureMenu = "MxFramework/CharacterTest/Tactical Card/Measure and Compile Default Loadout";
        internal const string SourceRelativePath = "StorySource/玩法基石/任务/任务03首卡源示例.json";

        [MenuItem(CompileMenu)]
        private static void Compile() => CompileDefault(false);

        [MenuItem(MeasureMenu)]
        private static void MeasureAndCompile() => CompileDefault(true);

        [MenuItem(CompileMenu, true)]
        [MenuItem(MeasureMenu, true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling && !EditorApplication.isUpdating;

        /// <remarks>LowFreqAlloc; EditMode only. Report generation does not claim runtime acceptance.</remarks>
        internal static bool CompileDefault(bool remeasure)
        {
            if (!CanRun()) return false;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(root, SourceRelativePath);
            try
            {
                if (!CharacterTestTacticalCardDefinition.TryRead(File.ReadAllText(path), out var definition, out string issue))
                    return ReportFailure(issue);
                CharacterTestGeneratedPackageSource.InvalidateCache();
                CharacterTestDefaultBakedWeaponTraceSource.InvalidateCachedInstanceForMeasurement();
                CharacterTestDefaultWeaponTraceBindingResolver.InvalidateCachedInstanceForMeasurement();
                var measurement = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 50, 0, 5000,
                    new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
                var store = new CharacterActionTacticalMetricsStore();
                // This is the existing default-loadout validation host, not a scene's selected entity.
                using (var session = new CharacterActionReferenceProbeSession(new CharacterActionId(301), measurement.ReferenceDummy))
                {
                    if (remeasure)
                    {
                        var measured = store.Refresh(session, measurement);
                        if (measured.Status != CharacterActionTacticalMetricsLoadStatus.Valid)
                            return ReportFailure(measured.Status + ":" + measured.Issue);
                    }
                    var current = new CharacterTestTacticalCardRuntimeInputs(session.Runtime, session.Source.Id.Value, store, measurement);
                    var binding = new CharacterTestTacticalCardBinding(current);
                    if (!binding.TryCompile(definition)) return ReportFailure(binding.LastIssue);
                    string directory = Path.Combine(root, "Library", "Task03ExecutionEvidence");
                    Directory.CreateDirectory(directory);
                    string report = Path.Combine(directory, "default-loadout-compilation.txt");
                    File.WriteAllText(report, "Source: " + path + Environment.NewLine
                        + "Evidence: compilation only; not runtime acceptance." + Environment.NewLine
                        + binding.RetainedCompilation.Describe());
                    Debug.Log("Tactical card compiled for the default validation loadout. " + report);
                    return true;
                }
            }
            catch (IOException error) { return ReportFailure(error.Message); }
            catch (UnauthorizedAccessException error) { return ReportFailure(error.Message); }
            catch (ArgumentException error) { return ReportFailure(error.Message); }
        }

        private static bool ReportFailure(string issue)
        {
            Debug.LogWarning("Tactical card compilation rejected: " + issue);
            return false;
        }
    }
}
