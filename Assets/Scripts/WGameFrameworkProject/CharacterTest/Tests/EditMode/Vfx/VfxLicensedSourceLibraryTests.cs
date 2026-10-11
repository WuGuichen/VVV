using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests
{
    public sealed class VfxLicensedSourceLibraryTests
    {
        private const string CatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";
        private const string ExtractionTestRoot = "Assets/VFX/MxFramework/Review/Piloto/Tests";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(ExtractionTestRoot)) AssetDatabase.DeleteAsset(ExtractionTestRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void SourceLibraryDeepIndex_MatchesCurrentAssetDatabaseWithoutFixedBaseline()
        {
            VfxSourceLibraryEditorStatus status = new VfxSourceLibraryEditorService().Load();
            Assert.That(status.Available, Is.True, status.Message);
            Assert.That(status.Index.UsagePolicy, Is.EqualTo("LicensedReusable"));
            Assert.That(status.Index.PrefabCount, Is.EqualTo(AssetDatabase.FindAssets("t:Prefab", new[] { VfxSourceLibraryEditorService.SourceRoot }).Length));
            Assert.That(status.Index.PrefabCount, Is.GreaterThanOrEqualTo(506));
            Assert.That(status.Index.ParticleSystemCount, Is.EqualTo(status.Index.Components.Count));
            Assert.That(status.Index.ParticleSystemCount, Is.GreaterThanOrEqualTo(13405));
            Assert.That(status.Index.Components.Select(value => value.ComponentId).Distinct().Count(), Is.EqualTo(status.Index.Components.Count));
            Assert.That(status.Index.Components.All(value => value.LocalFileId != 0 && value.ComponentId.Contains(value.PrefabGuid)), Is.True);
            Assert.That(status.Index.Packages.Select(value => value.SourcePackageId), Does.Contain("piloto.super-realistic-fx-bundle"));
            Assert.That(status.Index.Packages.Select(value => value.SourcePackageId), Does.Contain("piloto.super-realistic-fx-bundle-02"));
        }

        [Test]
        public void RejectedLicensedPrimitiveSet_IsAbsentFromCatalogAndRegistry()
        {
            JObject catalog = JObject.Parse(File.ReadAllText(CatalogPath));
            JObject[] primitives = ((JArray)catalog["primitives"]).OfType<JObject>()
                .Where(item => item.Value<string>("primitiveId").StartsWith("vfx.primitive.piloto."))
                .ToArray();
            Assert.That(primitives, Is.Empty);
            VfxSourceLibraryEditorStatus status = new VfxSourceLibraryEditorService().Load();
            VfxSourceExtractionRegistryView registry = new VfxSourceExtractionRegistryService().Load(status.Index);
            Assert.That(registry.Entries, Is.Empty);
        }

        [Test]
        public void GenericExtractor_PreservesParticleConfigurationAndDirectSupportReferences()
        {
            VfxSourceLibraryEditorStatus status = new VfxSourceLibraryEditorService().Load();
            Assert.That(status.Available, Is.True, status.Message);
            VfxSourceExtractionRegistryView registry = new VfxSourceExtractionRegistryService().Load(status.Index);
            VfxSourceComponentView component = status.Index.Components.First(value => VfxSourceExtractionRegistryService.Find(registry, value) == null && value.MaterialGuids.Count > 0);
            string output = ExtractionTestRoot + "/source_extract_test.prefab";
            var request = new VfxSourceExtractionRequestView
            {
                PrimitiveId = "vfx.primitive.source.test.glow",
                DisplayName = "Source extract test",
                OutputPrefabPath = output,
                ComponentIds = new List<string> { component.ComponentId }
            };

            bool success = new VfxSourceComponentExtractionService().PlanAndExtract(status.Index, request, false, out VfxSourceExtractionReceiptView receipt, out string message);
            Assert.That(success, Is.True, message);
            Assert.That(receipt.DerivedAssetPaths, Is.Empty);
            GameObject targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(output);
            Assert.That(targetPrefab, Is.Not.Null);
            ParticleSystem target = targetPrefab.GetComponentInChildren<ParticleSystem>(true);
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(component.PrefabPath);
            ParticleSystem source = sourcePrefab.GetComponentsInChildren<ParticleSystem>(true).First(value => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out _, out long localId) && localId == component.LocalFileId);
            Assert.That(target.main.maxParticles, Is.EqualTo(source.main.maxParticles));
            Assert.That(target.main.duration, Is.EqualTo(source.main.duration).Within(0.0001f));
            Assert.That(target.main.simulationSpace, Is.EqualTo(source.main.simulationSpace));
            Assert.That(target.GetComponent<ParticleSystemRenderer>().sharedMaterials, Is.EqualTo(source.GetComponent<ParticleSystemRenderer>().sharedMaterials));
            Assert.That(target.GetComponents<MonoBehaviour>(), Is.Empty);
        }

        [TestCase("second-component")]
        [TestCase("prefab-save")]
        [TestCase("exception")]
        [TestCase("success")]
        public void GenericExtractor_AssetTransaction_RollsBackFailuresAndRetainsOnlySuccess(string failure)
        {
            string token = Guid.NewGuid().ToString("N");
            string sourceRoot = ExtractionTestRoot + "/SourceAssets";
            EnsureFolder(sourceRoot);
            string sourceMaterialPath = sourceRoot + "/source_" + token + ".mat";
            string sourcePrefabPath = sourceRoot + "/source_" + token + ".prefab";
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            AssetDatabase.CreateAsset(new Material(shader), sourceMaterialPath);
            Material sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(sourceMaterialPath);
            string materialGuid = AssetDatabase.AssetPathToGUID(sourceMaterialPath);
            byte[] materialBytes = File.ReadAllBytes(sourceMaterialPath);
            long sourceLocalId = CreateParticleSourcePrefab(sourcePrefabPath, sourceMaterial);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePrefabPath);
            byte[] sourceBytes = File.ReadAllBytes(sourcePrefabPath);
            string outputTransactionRoot = ExtractionTestRoot + "/Txn_" + token;
            string outputPath = outputTransactionRoot + "/Output/extracted.prefab";
            var first = new VfxSourceComponentView
            {
                ComponentId = "source.component.first",
                PrefabPath = sourcePrefabPath,
                LocalFileId = sourceLocalId,
                ObjectPath = string.Empty,
                DisplayName = "First",
                MaterialGuids = new List<string> { AssetDatabase.AssetPathToGUID(sourceMaterialPath) }
            };
            var plan = new VfxSourceExtractionPlanView
            {
                PlanHash = "sha256:test",
                PrimitiveId = "vfx.primitive.test.transaction",
                DisplayName = "Transaction extraction",
                OutputPrefabPath = outputPath,
                DeriveMaterials = true,
                LocalScale = 1f,
                ThemeTint = new List<float> { 0.5f, 0.75f, 1f, 1f },
                Components = new List<VfxSourceComponentView> { first }
            };
            if (failure == "second-component")
            {
                plan.Components.Add(new VfxSourceComponentView
                {
                    ComponentId = "source.component.missing",
                    PrefabPath = sourcePrefabPath,
                    LocalFileId = -1,
                    ObjectPath = "Missing/ParticleSystem",
                    DisplayName = "Missing"
                });
            }

            Func<GameObject, string, bool> saver = failure == "prefab-save"
                ? (root, path) =>
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    return false;
                }
                : null;
            Action<int> afterComponent = failure == "exception"
                ? _ => throw new InvalidOperationException("simulated extraction exception")
                : null;

            bool success = VfxSourceComponentExtractionService.Execute(
                plan, out VfxSourceExtractionReceiptView receipt, out string message, saver, afterComponent);

            Assert.That(receipt.DerivedAssetPaths, Has.Count.EqualTo(1));
            if (failure == "success")
            {
                Assert.That(success, Is.True, message);
                Assert.That(message, Is.Empty);
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(outputPath), Is.Not.Null);
                Assert.That(receipt.DerivedAssetPaths.All(path => AssetDatabase.LoadAssetAtPath<Material>(path) != null), Is.True);
            }
            else
            {
                Assert.That(success, Is.False);
                Assert.That(message, Is.Not.Empty);
                Assert.That(receipt.DerivedAssetPaths.All(path => !File.Exists(path) && !File.Exists(path + ".meta")), Is.True);
                Assert.That(AssetDatabase.LoadMainAssetAtPath(outputPath), Is.Null);
                Assert.That(File.Exists(outputPath), Is.False);
                Assert.That(File.Exists(outputPath + ".meta"), Is.False);
                Assert.That(AssetDatabase.IsValidFolder(outputTransactionRoot), Is.False);
                Assert.That(Directory.Exists(outputTransactionRoot), Is.False);
                Assert.That(File.Exists(outputTransactionRoot + ".meta"), Is.False);
            }
            Assert.That(AssetDatabase.AssetPathToGUID(sourcePrefabPath), Is.EqualTo(sourceGuid));
            Assert.That(File.ReadAllBytes(sourcePrefabPath), Is.EqualTo(sourceBytes));
            Assert.That(AssetDatabase.AssetPathToGUID(sourceMaterialPath), Is.EqualTo(materialGuid));
            Assert.That(File.ReadAllBytes(sourceMaterialPath), Is.EqualTo(materialBytes));
        }

        [TestCase("registration-false")]
        [TestCase("registration-exception")]
        [TestCase("success")]
        [TestCase("existing-target")]
        public void RecipeAssembly_RegistrationTransaction_RollsBackOrCommitsAtomically(string outcome)
        {
            string token = Guid.NewGuid().ToString("N");
            string sourceRoot = ExtractionTestRoot + "/RecipeSources";
            EnsureFolder(sourceRoot);
            string materialPath = sourceRoot + "/material_" + token + ".mat";
            string primitivePath = sourceRoot + "/primitive_" + token + ".prefab";
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            AssetDatabase.CreateAsset(new Material(shader), materialPath);
            CreateParticleSourcePrefab(primitivePath, AssetDatabase.LoadAssetAtPath<Material>(materialPath));
            string sourceGuid = AssetDatabase.AssetPathToGUID(primitivePath);
            byte[] sourceBytes = File.ReadAllBytes(primitivePath);
            string outputRoot = ExtractionTestRoot + "/RecipeTxn_" + token;
            string outputPath = outputRoot + "/Output/recipe.prefab";
            var plan = new VfxSourceCompositionPlanView
            {
                RecipeId = "vfx.recipe.test.transaction",
                DisplayName = "Recipe transaction",
                OutputPrefabPath = outputPath,
                Layers = new List<VfxSourceCompositionLayerView>
                {
                    new VfxSourceCompositionLayerView { LayerId = "layer_a", PrimitiveId = "primitive.a", Order = 0 }
                }
            };
            var primitivePaths = new Dictionary<string, string> { ["primitive.a"] = primitivePath };
            string existingGuid = string.Empty;
            byte[] existingBytes = null;
            byte[] existingMetaBytes = null;
            if (outcome == "existing-target")
            {
                EnsureFolder(outputRoot + "/Output");
                CreateParticleSourcePrefab(outputPath, AssetDatabase.LoadAssetAtPath<Material>(materialPath));
                existingGuid = AssetDatabase.AssetPathToGUID(outputPath);
                existingBytes = File.ReadAllBytes(outputPath);
                existingMetaBytes = File.ReadAllBytes(outputPath + ".meta");
            }

            int registrationCalls = 0;
            VfxRecipeRegistration registration;
            if (outcome == "registration-false")
            {
                registration = (VfxSourceCompositionPlanView _, out string registrationMessage) =>
                {
                    registrationCalls++;
                    registrationMessage = "simulated registration rejection";
                    return false;
                };
            }
            else if (outcome == "registration-exception")
            {
                registration = (VfxSourceCompositionPlanView _, out string registrationMessage) =>
                {
                    registrationCalls++;
                    registrationMessage = string.Empty;
                    throw new InvalidOperationException("simulated registration exception");
                };
            }
            else
            {
                registration = (VfxSourceCompositionPlanView _, out string registrationMessage) =>
                {
                    registrationCalls++;
                    registrationMessage = "registered by test seam";
                    return true;
                };
            }

            bool success = new VfxSourceRecipeAssemblyService().ExecutePlan(
                plan, primitivePaths, out string message, registration);

            Assert.That(AssetDatabase.AssetPathToGUID(primitivePath), Is.EqualTo(sourceGuid));
            Assert.That(File.ReadAllBytes(primitivePath), Is.EqualTo(sourceBytes));
            if (outcome == "success")
            {
                Assert.That(success, Is.True, message);
                Assert.That(registrationCalls, Is.EqualTo(1));
                GameObject recipe = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
                Assert.That(recipe, Is.Not.Null);
                Assert.That(recipe.transform.Find("layer_a"), Is.Not.Null);
            }
            else if (outcome == "existing-target")
            {
                Assert.That(success, Is.False);
                Assert.That(message, Does.Contain("already exists"));
                Assert.That(registrationCalls, Is.EqualTo(0));
                Assert.That(AssetDatabase.AssetPathToGUID(outputPath), Is.EqualTo(existingGuid));
                Assert.That(File.ReadAllBytes(outputPath), Is.EqualTo(existingBytes));
                Assert.That(File.ReadAllBytes(outputPath + ".meta"), Is.EqualTo(existingMetaBytes));
            }
            else
            {
                Assert.That(success, Is.False);
                Assert.That(message, Is.Not.Empty);
                Assert.That(registrationCalls, Is.EqualTo(1));
                Assert.That(AssetDatabase.LoadMainAssetAtPath(outputPath), Is.Null);
                Assert.That(File.Exists(outputPath), Is.False);
                Assert.That(File.Exists(outputPath + ".meta"), Is.False);
                Assert.That(AssetDatabase.IsValidFolder(outputRoot), Is.False);
                Assert.That(Directory.Exists(outputRoot), Is.False);
                Assert.That(File.Exists(outputRoot + ".meta"), Is.False);
            }
        }

        [TestCase("prepare")]
        [TestCase("replace")]
        [TestCase("import")]
        [TestCase("success")]
        public void CatalogPublishTransaction_RestoresFailuresAndAtomicallyCommitsSuccess(string outcome)
        {
            string token = Guid.NewGuid().ToString("N");
            string root = ExtractionTestRoot + "/CatalogTxn_" + token;
            EnsureFolder(root);
            string catalogPath = root + "/catalog.json";
            var original = new JObject
            {
                ["schemaVersion"] = "test.v1",
                ["revision"] = 1,
                ["entries"] = new JArray("original")
            };
            File.WriteAllText(catalogPath, original.ToString());
            AssetDatabase.ImportAsset(catalogPath, ImportAssetOptions.ForceSynchronousImport);
            byte[] originalBytes = File.ReadAllBytes(catalogPath);
            byte[] originalMetaBytes = File.ReadAllBytes(catalogPath + ".meta");
            string originalGuid = AssetDatabase.AssetPathToGUID(catalogPath);
            var proposed = new JObject
            {
                ["schemaVersion"] = "test.v1",
                ["revision"] = 2,
                ["entries"] = new JArray("updated")
            };
            Action<string, string> writer = outcome == "prepare"
                ? (path, _) =>
                {
                    File.WriteAllText(path, "{\"partial\":");
                    throw new IOException("simulated temp write failure");
                }
                : null;
            Action<string, string, string> replacer = outcome == "replace"
                ? (_temporary, output, _backup) =>
                {
                    File.WriteAllText(output, "{\"partial\":true}");
                    throw new IOException("simulated replace failure");
                }
                : null;
            int importAttempts = 0;
            Func<string, bool> importer = outcome == "import"
                ? path =>
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    importAttempts++;
                    return importAttempts > 1;
                }
                : null;
            string temporaryPath;
            string backupPath;
            using (var transaction = new VfxCatalogAssetPublishTransaction(catalogPath, writer, replacer, importer))
            {
                temporaryPath = transaction.TemporaryPath;
                backupPath = transaction.BackupPath;
                bool prepared = transaction.Prepare(proposed, out string prepareMessage);
                if (outcome == "prepare")
                {
                    Assert.That(prepared, Is.False);
                    Assert.That(prepareMessage, Does.Contain("prepare failed"));
                }
                else
                {
                    Assert.That(prepared, Is.True, prepareMessage);
                    bool committed = transaction.Commit(out string commitMessage);
                    Assert.That(committed, Is.EqualTo(outcome == "success"), commitMessage);
                }
            }

            Assert.That(AssetDatabase.AssetPathToGUID(catalogPath), Is.EqualTo(originalGuid));
            Assert.That(File.ReadAllBytes(catalogPath + ".meta"), Is.EqualTo(originalMetaBytes));
            if (outcome == "success")
                Assert.That(JToken.DeepEquals(JObject.Parse(File.ReadAllText(catalogPath)), proposed), Is.True);
            else
                Assert.That(File.ReadAllBytes(catalogPath), Is.EqualTo(originalBytes));
            if (outcome == "import") Assert.That(importAttempts, Is.EqualTo(2));
            Assert.That(File.Exists(temporaryPath), Is.False);
            Assert.That(File.Exists(temporaryPath + ".meta"), Is.False);
            Assert.That(File.Exists(backupPath), Is.False);
            Assert.That(File.Exists(backupPath + ".meta"), Is.False);
        }

        [Test]
        public void RejectedFireballRecipesAndReviewOutputs_AreAbsent()
        {
            JObject catalog = JObject.Parse(File.ReadAllText(CatalogPath));
            JObject[] recipes = ((JArray)catalog["recipes"]).OfType<JObject>()
                .Where(item => item.Value<string>("recipeId").StartsWith("vfx.recipe.piloto.fireball."))
                .ToArray();
            Assert.That(recipes, Is.Empty);
            Assert.That(AssetDatabase.IsValidFolder("Assets/VFX/MxFramework/Review/Piloto/Primitives/Fireball"), Is.False);
            Assert.That(AssetDatabase.IsValidFolder("Assets/VFX/MxFramework/Review/Piloto/Recipes/Fireball"), Is.False);
        }

        private static long CreateParticleSourcePrefab(string path, Material material)
        {
            var root = new GameObject("Source");
            try
            {
                ParticleSystem system = root.AddComponent<ParticleSystem>();
                system.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, path), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            ParticleSystem saved = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ParticleSystem>();
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(saved, out _, out long localId), Is.True);
            return localId;
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string part in path.Substring(6).Trim('/').Split('/'))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }
}
