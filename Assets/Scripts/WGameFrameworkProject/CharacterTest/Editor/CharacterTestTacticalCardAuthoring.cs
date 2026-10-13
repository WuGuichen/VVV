using System;
using System.IO;
using System.Linq;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Minimal source-file compilation entry for the default validation loadout.</summary>
    internal static class CharacterTestTacticalCardAuthoring
    {
        private const string CompileMenu = "MxFramework/CharacterTest/Tactical Card/Compile Default Loadout";
        private const string MeasureMenu = "MxFramework/CharacterTest/Tactical Card/Measure and Compile Default Loadout";
        private const string ValidateAccessoriesMenu = "MxFramework/CharacterTest/Tactical Card/Validate Nine Accessories";
        private const string PublishAccessoriesMenu = "MxFramework/CharacterTest/Tactical Card/Publish Nine Accessories";
        internal const string SourceRelativePath = "StorySource/玩法基石/任务/任务03首卡源示例.json";

        [MenuItem(CompileMenu)]
        private static void Compile() => CompileDefault(false);

        [MenuItem(MeasureMenu)]
        private static void MeasureAndCompile() => CompileDefault(true);

        [MenuItem(ValidateAccessoriesMenu)]
        private static void ValidateAccessories() => PublishNineAccessories(false);

        [MenuItem(PublishAccessoriesMenu)]
        private static void PublishAccessories() => PublishNineAccessories(true);

        [MenuItem(CompileMenu, true)]
        [MenuItem(MeasureMenu, true)]
        [MenuItem(ValidateAccessoriesMenu, true)]
        [MenuItem(PublishAccessoriesMenu, true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling && !EditorApplication.isUpdating;

        /// <remarks>Explicit Editor publication only. Never opens a player store or grants owned items.</remarks>
        internal static bool PublishNineAccessories(bool write)
        {
            if (!CanRun()) return false;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string directory = Path.Combine(Application.streamingAssetsPath, "MxFramework/CharacterTest");
            string path = Path.Combine(directory, CharacterTacticalAssemblyPublication.FileName);
            try
            {
                string previous = File.Exists(path) ? File.ReadAllText(path) : null;
                var published = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
                if (!published.Success) throw new InvalidOperationException(published.Diagnostic);
                var catalog = new CharacterTacticalContentCatalog(); catalog.SetPublishedCards(published.CardCatalog.Cards);
                var inventory = JsonConvert.DeserializeObject<CharacterTacticalRoleInventoryConfig>(File.ReadAllText(
                    Path.Combine(directory, "character_item_inventory.json")));
                var prepared = TacticalAccessoryThirdBatchPublication.PrepareNineFromRoleConfiguration(
                    File.ReadAllText(Path.Combine(root, TacticalAccessoryThirdBatchPublication.NineSourcePath)), previous,
                    catalog, inventory, CharacterTestPlayableCharacterCatalog.Discover().Where(c => c.TacticalDefaults != null)
                        .Select(c => c.TacticalDefaults.CharacterStableId));
                string json = JsonConvert.SerializeObject(prepared, Formatting.Indented);
                if (write && previous != json)
                {
                    // Validation finishes before any write. Replacement is atomic; existing meta remains authoritative.
                    string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        File.WriteAllText(temporary, json, new UTF8Encoding(false));
                        if ((File.Exists(path) ? File.ReadAllText(path) : null) != previous)
                            throw new IOException("TACTICAL_NINE_PUBLICATION_CHANGED_DURING_PREPARE");
                        if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
                // Explicit publication also imports an identical existing JSON, so its meta is Editor-owned.
                if (write) AssetDatabase.ImportAsset("Assets/StreamingAssets/MxFramework/CharacterTest/" + CharacterTacticalAssemblyPublication.FileName,
                    ImportAssetOptions.ForceSynchronousImport);
                Debug.Log((write ? "Published" : "Validated") + " nine tactical accessories; config "
                    + prepared.Configuration.Revision + "/" + prepared.Configuration.ContentHash
                    + ". Default recipes and player ownership were not changed.");
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError("Nine-accessory publication rejected: " + error.Message);
                return false;
            }
        }

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
