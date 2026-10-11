using System;
using System.IO;
using System.Text;
using UnityEditor;

namespace MxFramework.Game.CharacterTest.Editor
{
    public static class CharacterTestWorldObjectDefinitionArtifactsBuilder
    {
        [MenuItem("MxFramework/Character Test/Definitions/Export Built-in World Objects")]
        public static void ExportBuiltInWorldObjects()
        {
            CharacterTestWorldObjectDefinitionAuthoringDocument authoring =
                CharacterTestWorldObjectDefinitionMigration.ExportBuiltInCatalog();
            Save(authoring);
            AssetDatabase.Refresh();
        }

        [MenuItem("MxFramework/Character Test/Definitions/Rebuild World Object Artifacts")]
        public static void RebuildFromAuthoring()
        {
            string path = CharacterTestWorldObjectDefinitionCatalogLoader.DefaultAuthoringPath;
            CharacterTestWorldObjectDefinitionValidationReport report = null;
            if (!File.Exists(path)
                || !CharacterTestWorldObjectDefinitionJson.TryLoadAuthoring(
                    File.ReadAllText(path),
                    out CharacterTestWorldObjectDefinitionAuthoringDocument authoring,
                    out report))
            {
                throw new InvalidDataException(
                    "World object authoring JSON is invalid: "
                    + (report?.Issues.Count ?? 0)
                    + " issue(s).");
            }

            Save(authoring);
        }

        internal static void Save(
            CharacterTestWorldObjectDefinitionAuthoringDocument authoring)
        {
            CharacterTestWorldObjectDefinitionCompileResult compiled =
                CharacterTestWorldObjectDefinitionCompiler.Compile(authoring);
            if (!compiled.Success)
                throw new InvalidOperationException("World object definition compilation failed.");

            WriteProjectFile(
                CharacterTestWorldObjectDefinitionCatalogLoader.DefaultAuthoringPath,
                CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(authoring));
            string runtimeJson = CharacterTestWorldObjectDefinitionJson.SerializeRuntime(
                compiled.RuntimeDocument);
            WriteProjectFile(
                CharacterTestWorldObjectDefinitionCatalogLoader.DefaultGeneratedRuntimePath,
                runtimeJson);
            WriteProjectFile(
                "Assets/StreamingAssets/"
                    + CharacterTestWorldObjectDefinitionCatalogLoader.DefaultPlayerMirrorRelativePath,
                runtimeJson);
            AssetDatabase.ImportAsset(
                CharacterTestWorldObjectDefinitionCatalogLoader.DefaultAuthoringPath,
                ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(
                CharacterTestWorldObjectDefinitionCatalogLoader.DefaultGeneratedRuntimePath,
                ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(
                "Assets/StreamingAssets/"
                    + CharacterTestWorldObjectDefinitionCatalogLoader.DefaultPlayerMirrorRelativePath,
                ImportAssetOptions.ForceUpdate);
        }

        private static void WriteProjectFile(string projectPath, string contents)
        {
            string fullPath = Path.GetFullPath(projectPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, contents + Environment.NewLine, new UTF8Encoding(false));
        }
    }
}
