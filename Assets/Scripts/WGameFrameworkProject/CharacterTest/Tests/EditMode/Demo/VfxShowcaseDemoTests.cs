using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MxFramework.Demo;
using MxFramework.Demo.VfxShowcase;
using MxFramework.Resources;
using MxFramework.Vfx;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MxFramework.Tests.Demo
{
    public sealed class VfxShowcaseDemoTests
    {
        [TearDown]
        public void TearDown()
        {
            if (!Application.isPlaying)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void Catalog_SeparatesRuntimeDefinitionsFromCatalogPrefabs()
        {
            ResourceCatalog catalog = TempImportedResourceCatalog.CreateCatalog();
            VfxDefinitionRegistry definitions = VfxConfigJsonImporter.ImportDefinitionFile(VfxShowcaseDemoBootstrap.GeneratedDefinitionsPath);
            var vfxCatalogKeys = new HashSet<ResourceKey>();
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (IsVfxGameObjectEntry(entry))
                    vfxCatalogKeys.Add(entry.CreateKey(catalog.PackageId));
            }

            int runtimeDefinitionCount = 0;
            IReadOnlyList<VfxDefinition> snapshot = definitions.CreateSnapshot();
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (vfxCatalogKeys.Contains(snapshot[i].PrefabResource))
                    runtimeDefinitionCount++;
            }

            int catalogPrefabCount = 0;
            foreach (ResourceKey key in vfxCatalogKeys)
            {
                if (!DefinitionUsesResource(snapshot, key))
                    catalogPrefabCount++;
            }

            Assert.Greater(runtimeDefinitionCount, 0);
            Assert.Greater(catalogPrefabCount, 0);
            Assert.IsTrue(vfxCatalogKeys.Contains(new ResourceKey("vfx.magic.fireball", ResourceTypeIds.GameObject, string.Empty, TempImportedResourceCatalog.PackageId)));
            Assert.IsTrue(vfxCatalogKeys.Contains(new ResourceKey("vfx.status_aura.burn", ResourceTypeIds.GameObject, string.Empty, TempImportedResourceCatalog.PackageId)));
        }

        [Test]
        public void AuthoringCatalog_EnrichesEntriesWithoutReplacingRuntimeCatalog()
        {
            var root = new GameObject("VfxShowcaseSemanticTest");
            try
            {
                VfxShowcaseDemoBootstrap bootstrap = root.AddComponent<VfxShowcaseDemoBootstrap>();
                bootstrap.InitializeForValidation();

                Assert.AreEqual(37, bootstrap.SemanticEffectCount);
                Assert.IsTrue(bootstrap.SelectEntryForValidation("vfx.magic.fireball"));
                StringAssert.Contains("role=Impact", bootstrap.SelectedSemanticText);
                StringAssert.Contains("backend=ParticleSystem", bootstrap.SelectedSemanticText);
                StringAssert.Contains("no Recipe registered", bootstrap.SelectedRecipeText);
                StringAssert.Contains("max=16", bootstrap.SelectedBudgetText);
                Assert.IsTrue(bootstrap.SelectEntryForValidation("vfx.magic.fireball_burst.flight"));
                StringAssert.Contains("fire-core", bootstrap.SelectedRecipeText);
                StringAssert.Contains("vfx.primitive.projectile.fire_core", bootstrap.SelectedRecipeText);
                Assert.AreEqual(0, bootstrap.SelectedRuntimeParameterCount, "StaticBake local_scale must not become a Runtime slot control.");
                bootstrap.DisposeForValidation();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AuthoringRecipeBakedPrefab_NestsSharedPrimitiveAndHasEvidence()
        {
            const string outputPath = "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball_Burst.prefab";
            const string primitivePath = "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball.prefab";
            const string evidencePath = "Assets/MxFrameworkGenerated/Vfx/AuthoringBakeEvidence/vfx_magic_fireball_burst_flight.json";
            GameObject output = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
            Assert.IsNotNull(output);
            Transform primitiveRoot = output.transform.Find("fire-core");
            Assert.IsNotNull(primitiveRoot);
            Assert.AreEqual(Vector3.one * 1.15f, primitiveRoot.localScale);
            UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(primitiveRoot.gameObject);
            Assert.AreEqual(primitivePath, AssetDatabase.GetAssetPath(source));
            Assert.IsTrue(File.Exists(evidencePath));
            string evidence = File.ReadAllText(evidencePath);
            StringAssert.Contains("vfx.magic.fireball_burst.flight", evidence);
            StringAssert.Contains("sha256:e5e375883d0bdef885341bd5e2495941a583e4091fe70d99a65a1cd676c886eb", evidence);
        }

        [Test]
        public void SceneGeneratorSmoke_BindsUiStageAndSerializedVfxRefs()
        {
            CreateVfxShowcaseScene.ValidateSmoke();
        }

        [Test]
        public void PlaybackController_ReplaysDefinitionCatalogPrefabAndLengthParameterizedArc()
        {
            CreateVfxShowcaseScene.ValidatePlayableSmoke();
        }

        [UnityTest]
        public IEnumerator Scene_PlayModeInitializesListAndReplaysRepresentativeVfx()
        {
            EditorSceneManager.OpenScene(VfxShowcaseDemoBootstrap.ScenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();

            VfxShowcaseDemoBootstrap bootstrap = UnityEngine.Object.FindAnyObjectByType<VfxShowcaseDemoBootstrap>();
            Assert.IsNotNull(bootstrap);
            bootstrap.InitializeForValidation();
            Assert.Greater(bootstrap.EntryCount, 0);
            Assert.Greater(bootstrap.RuntimeDefinitionCount, 0);
            Assert.Greater(bootstrap.CatalogPrefabCount, 0);
            Assert.IsTrue(bootstrap.SelectEntryForValidation("vfx.magic.fireball"));
            Assert.IsTrue(bootstrap.ReplayForValidation());
            yield return null;
            Assert.Greater(bootstrap.ActiveRuntimeVfxCount, 0);
            bootstrap.ClearForValidation();

            Assert.IsTrue(bootstrap.SelectEntryForValidation("vfx.magic.laser_lance.laser"));
            Assert.IsTrue(bootstrap.SetLengthForValidation(12f));
            Assert.IsTrue(bootstrap.ReplayForValidation());
            yield return null;
            Assert.Greater(bootstrap.ActiveRuntimeVfxCount, 0);
            bootstrap.ClearForValidation();

            Assert.IsTrue(bootstrap.SelectEntryForValidation("vfx.status_aura.burn"));
            Assert.IsTrue(bootstrap.ReplayForValidation());
            yield return null;
            Assert.Greater(bootstrap.ActiveDirectPrefabCount, 0);
            bootstrap.ClearForValidation();

            yield return new ExitPlayMode();
        }

        private static bool IsVfxGameObjectEntry(ResourceCatalogEntry entry)
        {
            if (entry == null || !string.Equals(entry.TypeId, ResourceTypeIds.GameObject, StringComparison.Ordinal))
                return false;

            for (int i = 0; i < entry.Labels.Count; i++)
            {
                if (string.Equals(entry.Labels[i], TempImportedResourceCatalog.DomainVfxLabel, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool DefinitionUsesResource(IReadOnlyList<VfxDefinition> definitions, ResourceKey key)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].PrefabResource == key)
                    return true;
            }

            return false;
        }
    }
}
