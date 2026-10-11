using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge
    {
        [MenuItem("MxFramework/Character Test/Runtime AI Planner/Migrate Missing Catalog Numeric Fingerprint")]
        public static void MigrateMissingNumericIndexFingerprint()
        {
            string directory = Path.Combine(Application.streamingAssetsPath,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.RelativeDirectory);
            string path = Path.Combine(directory, CharacterTestRuntimeAiPlannerProjectCatalogLoader.CardCatalogFileName);
            string before = File.ReadAllText(path, Encoding.UTF8);
            JObject index = JObject.Parse(before);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                CharacterTestAbilityCatalogSource.CreateRegistry(), actionRuntime.Registry);
            var loaded = CharacterTestRuntimeAiPlannerProjectCardStorage.LoadForExplicitMigration(before, directory, references);
            if (!loaded.Success) throw new InvalidOperationException(FirstCardDiagnostic(loaded));
            if ((long?)index["catalogHash"] != loaded.Catalog.CatalogHash)
                throw new InvalidOperationException("Catalog content fingerprint changed; numeric index migration cannot repair content.");
            if (index["numericIdentityHash"] != null)
            {
                Debug.Log("Published card index numeric fingerprint is already current.");
                return;
            }
            index["numericIdentityHash"] = loaded.Catalog.NumericIdentityHash;
            string candidate = index.ToString(Formatting.Indented) + "\n";
            var validated = CharacterTestRuntimeAiPlannerProjectCardStorage.Load(candidate, directory, references);
            if (!validated.Success) throw new InvalidOperationException(FirstCardDiagnostic(validated));
            if (!string.Equals(before, File.ReadAllText(path, Encoding.UTF8), StringComparison.Ordinal))
                throw new InvalidOperationException("Published card index changed while migration was preparing.");
            string backupDirectory = Path.Combine(Application.dataPath, "../Library/NumericHashMigration");
            Directory.CreateDirectory(backupDirectory);
            string backup = Path.Combine(backupDirectory, "behavior_card_catalog.before-numeric-fingerprint.json");
            if (!File.Exists(backup)) File.WriteAllText(backup, before, new UTF8Encoding(false));
            string temporary = path + ".numeric-migration.tmp";
            try
            {
                File.WriteAllText(temporary, candidate, new UTF8Encoding(false));
                File.Replace(temporary, path, null);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            AssetDatabase.ImportAsset("Assets/StreamingAssets/" +
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.RelativeDirectory + "/" +
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.CardCatalogFileName,
                ImportAssetOptions.ForceUpdate);
            Debug.Log("Published card numeric index fingerprint migrated; catalog revision and content fingerprint preserved.");
        }
    }
}
