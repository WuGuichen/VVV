using MxFramework.Game.CharacterTest.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Rendering
{
    public sealed class WGameUrpLitUsageAuditTests
    {
        [Test]
        public void FirstPartyContent_HasNoStockUrpLitUsage()
        {
            var violations = WGameUrpLitUsageAudit.Scan();

            Assert.That(
                violations,
                Is.Empty,
                "First-party stock URP Lit usage must remain at zero:\n" + string.Join("\n", violations));
        }

        [Test]
        public void GeneratedModels_DisableEmbeddedMaterialImport()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Model",
                new[] { "Assets/MxFrameworkGenerated" });

            Assert.That(guids, Is.Not.Empty);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(
                    importer.materialImportMode,
                    Is.EqualTo(ModelImporterMaterialImportMode.None),
                    path);
            }
        }

        [Test]
        public void GeneratedCharacterPreviewMaterials_UseExpectedCharacterShader()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Material",
                new[] { "Assets/MxFrameworkGenerated/CharacterPackages" });
            int previewCount = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.IndexOf("/preview_materials/", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material, Is.Not.Null, path);
                Assert.That(material.shader, Is.Not.Null, path);
                string expected = path.IndexOf(
                    "/CharacterPackages/000003/",
                    System.StringComparison.OrdinalIgnoreCase) >= 0
                    ? "WGame/CharacterTraditionalLit"
                    : "WGame/CharacterPaletteLegacy";
                Assert.That(material.shader.name, Is.EqualTo(expected), path);
                previewCount++;
            }

            Assert.That(previewCount, Is.GreaterThanOrEqualTo(4));
        }
    }
}
