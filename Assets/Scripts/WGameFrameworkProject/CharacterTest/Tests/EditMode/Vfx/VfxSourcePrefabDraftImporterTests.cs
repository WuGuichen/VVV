using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Editor.Tests
{
    public sealed class VfxSourcePrefabDraftImporterTests
    {
        private const string SourcePath = "Assets/Piloto Studio/Super Realistic FX Bundle/ARPG_Realistic Essentials Fire/Prefabs/Melee/Red_Fire/Hit_Nova_Light.prefab";
        private const string ManaProjectileRainPath = "Assets/Piloto Studio/Super Realistic FX Bundle/ARPG Mana theme pack/Complex/1) Mana Projectile Rain.prefab";
        private const string ManaCrossDamageAreaPath = "Assets/Piloto Studio/Super Realistic FX Bundle/ARPG Mana theme pack/Complex/2) Mana Cross Damage Area.prefab";
        private const string LightningProjectilePath = "Assets/Piloto Studio/Super Realistic FX Bundle/ARPG Lightning Starter Kit/Simple/2) Lightning Projectile.prefab";
        private const string BloodBuffUpPath = "Assets/Piloto Studio/Super Realistic FX Bundle/ARPG Bloody/Simple/Blood_Buff_Up.prefab";
        private const string BloodGigaExplosionPath = "Assets/Piloto Studio/Super Realistic FX Bundle/ARPG Bloody/Complex/Blood_Giga_Explosion.prefab";
        private const string ProjectileTrailPath = "Assets/Piloto Studio/Super Realistic FX Bundle/ARPG_Realistic Essentials Fire/Prefabs/Ranged/Red/Projectile_Trail.prefab";
        private const string SourceGuid = "34a4dd93a152d8a4a842b0b06e1834af";
        private string _testRoot;

        [SetUp]
        public void SetUp()
        {
            _testRoot = VfxSourceLibraryEditorService.SourceRoot + "/__ImporterFreshnessTests_" + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(_testRoot) && AssetDatabase.IsValidFolder(_testRoot))
                AssetDatabase.DeleteAsset(_testRoot);
        }

        [Test]
        public void HitNovaLightCreatesStablePrivateCompoundDraft()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            Assert.That(catalog.Available, Is.True, catalog.Message);
            string curationHash = VfxCompositionDraftService.HashFile(VfxSourceLibraryEditorService.CurationPath);
            var service = new VfxSourcePrefabDraftImportService();

            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(SourcePath, catalog);

            Assert.That(analysis.Valid, Is.True, string.Join("\n", analysis.Diagnostics));
            Assert.That(analysis.PrefabGuid, Is.EqualTo(SourceGuid));
            Assert.That(analysis.Members, Has.Count.EqualTo(7));
            Assert.That(analysis.EstimatedArtisticFrames, Is.GreaterThanOrEqualTo(300));
            Assert.That(service.CreateOrUpdate(analysis, catalog, null, out VfxCompositionDraftView draft, out string message), Is.True, message);
            Assert.That(draft.DraftId, Is.EqualTo("vfx.composition.source." + SourceGuid));
            Assert.That(draft.LocalElements, Has.Count.EqualTo(1));
            Assert.That(draft.LocalElements[0].MemberComponentIds, Is.EqualTo(analysis.Members.Select(value => value.ComponentId)));
            Assert.That(draft.Layers, Has.Count.EqualTo(1));
            Assert.That(draft.Layers[0].Phase, Is.EqualTo("Loop"));
            Assert.That(draft.LoopPlaybackMode, Is.EqualTo("OneShot"));
            Assert.That(draft.Layers[0].VisualStart.Mode, Is.EqualTo(nameof(VfxVisualStartMode.PreserveSource)));
            Assert.That(draft.Layers[0].Timing.RetimeMode, Is.EqualTo(nameof(VfxRetimeMode.PlaybackWindow)));
            Assert.That(draft.Layers[0].Timing.WindowEndPolicy, Is.EqualTo(nameof(UnityVfxLayerWindowEndPolicy.StopEmitting)));
            Assert.That(draft.Layers[0].Timing.TargetArtisticFrames, Is.EqualTo(analysis.EstimatedArtisticFrames));
            Assert.That(draft.Layers[0].Timing.MinimumBodyFrames, Is.EqualTo(1));
            Assert.That(VfxCompositionDraftService.HashFile(VfxSourceLibraryEditorService.CurationPath), Is.EqualTo(curationHash));

            string json = JsonConvert.SerializeObject(draft, Formatting.None, VfxCompositionDraftService.JsonSettings);
            VfxCompositionDraftView restored = JsonConvert.DeserializeObject<VfxCompositionDraftView>(json, VfxCompositionDraftService.JsonSettings);
            Assert.That(restored.LocalElements, Has.Count.EqualTo(1));
            Assert.That(restored.LocalElements[0].MemberComponentIds, Is.EqualTo(draft.LocalElements[0].MemberComponentIds));
        }

        [Test]
        public void MemberSelectionDefaultsEmptyAndClosesSubEmitterDependencyGroup()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis source = service.Analyze(SourcePath, catalog);

            VfxSourcePrefabMemberSelectionAnalysis empty = service.AnalyzeMemberSelection(source, Array.Empty<string>());
            Assert.That(empty.Valid, Is.False);
            Assert.That(empty.Members, Is.Empty);
            Assert.That(empty.Diagnostics, Has.Some.Contains("尚未选择"));

            string requested = source.Members[0].ComponentId;
            VfxSourcePrefabMemberSelectionAnalysis selection = service.AnalyzeMemberSelection(source, new[] { requested });
            VfxSourceMemberDependencyGroup group = selection.DependencyGroups.Single(value => value.MemberComponentIds.Contains(requested));

            Assert.That(selection.Valid, Is.True, string.Join("\n", selection.Diagnostics));
            Assert.That(selection.Members.Select(value => value.ComponentId), Is.EqualTo(
                source.Members.Where(value => group.MemberComponentIds.Contains(value.ComponentId)).Select(value => value.ComponentId)));
            Assert.That(selection.ElementId, Does.StartWith("vfx.element.authored.source-prefab." + SourceGuid + "."));
            Assert.That(selection.ElementId.Split('.').Last(), Has.Length.EqualTo(64));
        }

        [Test]
        public void EmptyMemberSelectionIsAnAwaitingInputStateForTheImportWorkspace()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis source = service.Analyze(SourcePath, catalog);
            VfxSourcePrefabMemberSelectionAnalysis empty = service.AnalyzeMemberSelection(source, Array.Empty<string>());
            VfxSourcePrefabMemberSelectionAnalysis selected = service.AnalyzeMemberSelection(
                source,
                new[] { source.Members[0].ComponentId });

            Assert.That(
                VfxManualCompositionStudioController.IsAwaitingSourceImportMemberSelection(source, empty),
                Is.True);
            Assert.That(
                VfxManualCompositionStudioController.IsAwaitingSourceImportMemberSelection(source, selected),
                Is.False);
        }

        [Test]
        public void AuthoredMemberIdentityIsStableForEquivalentRequestsAndDistinctForDifferentGroups()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis source = service.Analyze(SourcePath, catalog);
            VfxSourcePrefabMemberSelectionAnalysis topology = service.AnalyzeMemberSelection(source, Array.Empty<string>());
            Assert.That(topology.DependencyGroups.Count, Is.GreaterThan(1), "Hit_Nova_Light should expose independently reusable groups.");

            VfxSourceMemberDependencyGroup first = topology.DependencyGroups[0];
            VfxSourcePrefabMemberSelectionAnalysis forward = service.AnalyzeMemberSelection(source, first.MemberComponentIds);
            VfxSourcePrefabMemberSelectionAnalysis reverse = service.AnalyzeMemberSelection(source, first.MemberComponentIds.AsEnumerable().Reverse());
            VfxSourcePrefabMemberSelectionAnalysis second = service.AnalyzeMemberSelection(source, topology.DependencyGroups[1].MemberComponentIds);

            Assert.That(forward.ElementId, Is.EqualTo(reverse.ElementId));
            Assert.That(second.ElementId, Is.Not.EqualTo(forward.ElementId));
        }

        [Test]
        public void SelectedSubsetInstantiatesOnlyClosedMembers()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis source = service.Analyze(SourcePath, catalog);
            VfxSourcePrefabMemberSelectionAnalysis selection = service.AnalyzeMemberSelection(
                source,
                new[] { source.Members[0].ComponentId });
            VfxSourceElementView element = service.BuildAuthoredElement(selection, "测试派生素材");

            GameObject instance = VfxSourceElementInstantiation.Create(catalog, element, "Selected Subset Test");
            try
            {
                Assert.That(instance, Is.Not.Null);
                Assert.That(instance.GetComponentsInChildren<ParticleSystem>(true), Has.Length.EqualTo(selection.Members.Count));
                Assert.That(selection.Members.Count, Is.LessThan(source.Members.Count));
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void AuthoredLibraryUsesHashGuardAndDeduplicatesStableElementId()
        {
            string path = "Temp/MxFramework/VfxTests/authored-" + Guid.NewGuid().ToString("N") + ".json";
            var service = new VfxAuthoredElementLibraryService(path);
            var element = new VfxSourceElementView
            {
                ElementId = "vfx.element.authored.test." + Guid.NewGuid().ToString("N"),
                DisplayName = "原名称",
                CurationStatus = "ExplicitAuthored",
                MemberComponentIds = new System.Collections.Generic.List<string> { "member.a" }
            };
            try
            {
                VfxAuthoredElementLibraryView initial = service.Load(out _);
                Assert.That(service.SaveElement(element, initial.LibraryHash, false, out _, out bool created, out string message), Is.True, message);
                Assert.That(created, Is.True);
                VfxAuthoredElementLibraryView saved = service.Load(out message);
                Assert.That(saved.Elements, Has.Count.EqualTo(1));
                Assert.That(
                    Directory.GetFiles(service.StoreElementsDirectory, "*.json", SearchOption.AllDirectories),
                    Has.Length.EqualTo(1));

                element.DisplayName = "证据刷新不应覆盖人工名";
                Assert.That(service.SaveElement(element, saved.LibraryHash, true, out VfxSourceElementView updated, out created, out message), Is.True, message);
                Assert.That(created, Is.False);
                Assert.That(
                    Directory.GetFiles(service.StoreElementsDirectory, "*.json", SearchOption.AllDirectories),
                    Has.Length.EqualTo(1), "Updating evidence must clean the superseded immutable shard.");
                Assert.That(updated.DisplayName, Is.EqualTo("原名称"));
                Assert.That(service.Rename(element.ElementId, "新名称", service.Load(out _).LibraryHash, out message), Is.True, message);
                Assert.That(service.Load(out _).Elements.Single().DisplayName, Is.EqualTo("新名称"));
                Assert.That(service.Delete(element.ElementId, service.Load(out _).LibraryHash, out var references, out message), Is.True, message);
                Assert.That(references, Is.Empty);
                Assert.That(service.Load(out _).Elements, Is.Empty);
                Assert.That(
                    Directory.GetFiles(service.StoreElementsDirectory, "*.json", SearchOption.AllDirectories),
                    Is.Empty);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                string storeDirectory = Path.GetDirectoryName(service.StoreManifestPath);
                if (Directory.Exists(storeDirectory)) Directory.Delete(storeDirectory, true);
            }
        }

        [Test]
        public void AuthoredLibraryMigratesLegacyFileIntoIndependentElementShards()
        {
            string path = "Temp/MxFramework/VfxTests/authored-migration-" + Guid.NewGuid().ToString("N") + ".json";
            var service = new VfxAuthoredElementLibraryService(path);
            var legacy = new VfxAuthoredElementLibraryView
            {
                Elements = new List<VfxSourceElementView>
                {
                    new VfxSourceElementView
                    {
                        ElementId = "vfx.element.authored.migration.a",
                        DisplayName = "Migration A",
                        MemberComponentIds = new List<string> { "member.a" }
                    },
                    new VfxSourceElementView
                    {
                        ElementId = "vfx.element.authored.migration.b",
                        DisplayName = "Migration B",
                        MemberComponentIds = new List<string> { "member.b" }
                    }
                }
            };
            legacy.LibraryHash = VfxAuthoredElementLibraryService.ComputeLibraryHash(legacy);
            string legacyJson = JsonConvert.SerializeObject(
                legacy, Formatting.Indented, VfxCompositionDraftService.JsonSettings);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, legacyJson);

                VfxAuthoredElementLibraryView migrated = service.Load(out string message);

                Assert.That(migrated, Is.Not.Null, message);
                Assert.That(migrated.Elements, Has.Count.EqualTo(2));
                Assert.That(File.ReadAllText(path), Is.EqualTo(legacyJson), "Legacy input must remain unchanged.");
                Assert.That(File.Exists(service.StoreManifestPath), Is.True);
                VfxAuthoredElementManifestView manifest =
                    JsonConvert.DeserializeObject<VfxAuthoredElementManifestView>(
                        File.ReadAllText(service.StoreManifestPath),
                        VfxCompositionDraftService.JsonSettings);
                Assert.That(manifest.Elements, Has.Count.EqualTo(2));
                Assert.That(Directory.GetFiles(service.StoreElementsDirectory, "*.json", SearchOption.AllDirectories),
                    Has.Length.EqualTo(2));

                VfxAuthoredElementLibraryView reloaded = service.Load(out message);
                Assert.That(reloaded, Is.Not.Null, message);
                Assert.That(reloaded.LibraryHash, Is.EqualTo(migrated.LibraryHash));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                string storeDirectory = Path.GetDirectoryName(service.StoreManifestPath);
                if (Directory.Exists(storeDirectory)) Directory.Delete(storeDirectory, true);
            }
        }

        [Test]
        public void AuthoredLibraryFailsClosedWhenAnElementShardIsModified()
        {
            string path = "Temp/MxFramework/VfxTests/authored-corrupt-" + Guid.NewGuid().ToString("N") + ".json";
            var service = new VfxAuthoredElementLibraryService(path);
            try
            {
                VfxAuthoredElementLibraryView initial = service.Load(out _);
                var element = new VfxSourceElementView
                {
                    ElementId = "vfx.element.authored.corrupt",
                    DisplayName = "Original",
                    MemberComponentIds = new List<string> { "member.a" }
                };
                Assert.That(service.SaveElement(
                    element,
                    initial.LibraryHash,
                    false,
                    out _,
                    out _,
                    out string message), Is.True, message);

                VfxAuthoredElementManifestView manifest =
                    JsonConvert.DeserializeObject<VfxAuthoredElementManifestView>(
                        File.ReadAllText(service.StoreManifestPath),
                        VfxCompositionDraftService.JsonSettings);
                string shardPath = Path.Combine(
                    Path.GetDirectoryName(service.StoreManifestPath),
                    manifest.Elements.Single().Path.Replace('/', Path.DirectorySeparatorChar));
                VfxAuthoredElementDocumentView document =
                    JsonConvert.DeserializeObject<VfxAuthoredElementDocumentView>(
                        File.ReadAllText(shardPath),
                        VfxCompositionDraftService.JsonSettings);
                document.Element.DisplayName = "Tampered";
                File.WriteAllText(
                    shardPath,
                    JsonConvert.SerializeObject(
                        document, Formatting.Indented, VfxCompositionDraftService.JsonSettings));

                Assert.That(service.Load(out message), Is.Null);
                Assert.That(message, Does.Contain("hash"));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                string storeDirectory = Path.GetDirectoryName(service.StoreManifestPath);
                if (Directory.Exists(storeDirectory)) Directory.Delete(storeDirectory, true);
            }
        }

        [Test]
        public void ExplicitAuthoredCatalogUsesValidHashesAndDoesNotRestoreAutomaticElements()
        {
            VfxSourceCurationView curation = JsonConvert.DeserializeObject<VfxSourceCurationView>(
                File.ReadAllText(VfxSourceLibraryEditorService.CurationPath));
            string expectedCurationHash = curation.CurationHash;
            curation.CurationHash = string.Empty;

            Assert.That(curation.ExplicitAuthored, Is.True);
            Assert.That(curation.Elements, Is.Empty);
            Assert.That(VfxSourceElementCurationService.Hash(JsonConvert.SerializeObject(curation, Formatting.None)), Is.EqualTo(expectedCurationHash));

            VfxAuthoredElementLibraryView authored = new VfxAuthoredElementLibraryService().Load(out string message);
            Assert.That(authored, Is.Not.Null, message);
            Assert.That(authored.LibraryHash, Is.EqualTo(VfxAuthoredElementLibraryService.ComputeLibraryHash(authored)));
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            Assert.That(catalog.Curation.ExplicitAuthored, Is.True);
            Assert.That(catalog.Elements.All(value => value.CurationStatus == "ExplicitAuthored"), Is.True);
        }

        [Test]
        public void BloodBuffUpAcceptsSourceOnlyDistortionGrabberWithoutCopyingIt()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            Assert.That(catalog.Available, Is.True, catalog.Message);
            string sourceHash = VfxCompositionDraftService.HashFile(BloodBuffUpPath);
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(BloodBuffUpPath);
            Assert.That(source, Is.Not.Null);
            Assert.That(CountComponentsByFullName(source, "PilotoStudio.DistortionGrabber"), Is.EqualTo(2));

            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(BloodBuffUpPath, catalog);

            Assert.That(analysis.Valid, Is.True, string.Join("\n", analysis.Diagnostics));
            Assert.That(analysis.Members, Has.Count.EqualTo(31));
            var draft = new VfxCompositionDraftView { DraftId = "vfx.composition.test.blood-buff" };
            VfxSourcePrefabDraftImportPlan plan = service.PlanForCurrentDraft(analysis, draft);
            Assert.That(service.ApplyToCurrentDraft(
                analysis, catalog, draft, plan, false, "unused", "Loop",
                out VfxSourceElementView element, out _, out string message), Is.True, message);

            GameObject derived = VfxSourceElementInstantiation.Create(catalog, element, "Blood Buff Import Test");
            try
            {
                Assert.That(derived, Is.Not.Null);
                Assert.That(CountComponentsByFullName(derived, "PilotoStudio.DistortionGrabber"), Is.Zero);
                Assert.That(derived.GetComponentsInChildren<ParticleSystem>(true), Has.Length.EqualTo(analysis.Members.Count));
            }
            finally
            {
                if (derived != null) Object.DestroyImmediate(derived);
            }

            Assert.That(VfxCompositionDraftService.HashFile(BloodBuffUpPath), Is.EqualTo(sourceHash));
            Assert.That(CountComponentsByFullName(source, "PilotoStudio.DistortionGrabber"), Is.EqualTo(2));
        }

        private static int CountComponentsByFullName(GameObject root, string fullName)
        {
            return root.GetComponentsInChildren<MonoBehaviour>(true)
                .Count(component => component != null
                    && string.Equals(component.GetType().FullName, fullName, StringComparison.Ordinal));
        }

        [Test]
        public void BloodGigaExplosionNormalizesEnabledSubEmitterWithOnlyNullSlots()
        {
            string sourceHash = VfxCompositionDraftService.HashFile(BloodGigaExplosionPath);
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(BloodGigaExplosionPath, catalog);

            Assert.That(analysis.Valid, Is.True, string.Join("\n", analysis.Diagnostics));
            Assert.That(analysis.SubEmitterReferencesValid, Is.True);
            Assert.That(analysis.Members, Has.Count.EqualTo(51));
            Assert.That(service.CreateOrUpdate(
                analysis, catalog, null, out VfxCompositionDraftView draft, out string message), Is.True, message);
            catalog.SetLocalElements(draft.LocalElements);

            GameObject sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(BloodGigaExplosionPath);
            ParticleSystem[] sourceSystems = sourceRoot.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystem source = sourceSystems.First(value => value.name == "Spark2_Additive");
            int sourceIndex = Array.IndexOf(sourceSystems, source);
            Assert.That(source.subEmitters.enabled, Is.True);
            Assert.That(source.subEmitters.subEmittersCount, Is.GreaterThan(0));
            for (int i = 0; i < source.subEmitters.subEmittersCount; i++)
                Assert.That(source.subEmitters.GetSubEmitterSystem(i), Is.Null, "source slot " + i);

            GameObject derived = VfxSourceElementInstantiation.Create(catalog, draft.LocalElements[0], "Blood Giga Import Test");
            try
            {
                Assert.That(derived, Is.Not.Null);
                ParticleSystem target = derived.GetComponentsInChildren<ParticleSystem>(true)[sourceIndex];
                Assert.That(target.name, Is.EqualTo(source.name));
                Assert.That(target.subEmitters.enabled, Is.False);
                Assert.That(target.subEmitters.subEmittersCount, Is.Zero);
            }
            finally
            {
                if (derived != null) Object.DestroyImmediate(derived);
            }

            Assert.That(VfxCompositionDraftService.HashFile(BloodGigaExplosionPath), Is.EqualTo(sourceHash));
        }

        [Test]
        public void CurrentDraftImportDefaultsToElementOnlyAndDeduplicatesStableEvidence()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(SourcePath, catalog);
            var draft = new VfxCompositionDraftView { DraftId = "vfx.composition.test.current" };

            VfxSourcePrefabDraftImportPlan addPlan = service.PlanForCurrentDraft(analysis, draft);
            Assert.That(addPlan.Action, Is.EqualTo(VfxSourcePrefabDraftImportAction.Add));
            Assert.That(service.ApplyToCurrentDraft(
                analysis, catalog, draft, addPlan, false, "unused", "Loop",
                out VfxSourceElementView element, out VfxCompositionLayerView layer, out string message), Is.True, message);

            Assert.That(element.ElementKind, Is.EqualTo("CompoundGroup"));
            Assert.That(element.MemberComponentIds, Has.Count.EqualTo(7));
            Assert.That(layer, Is.Null);
            Assert.That(draft.LocalElements, Has.Count.EqualTo(1));
            Assert.That(draft.Layers, Is.Empty);

            VfxSourcePrefabDraftImportPlan duplicatePlan = service.PlanForCurrentDraft(analysis, draft);
            Assert.That(duplicatePlan.Action, Is.EqualTo(VfxSourcePrefabDraftImportAction.LocateExisting));
            Assert.That(service.ApplyToCurrentDraft(
                analysis, catalog, draft, duplicatePlan, false, "unused", "Loop",
                out VfxSourceElementView located, out layer, out message), Is.True, message);
            Assert.That(located, Is.SameAs(element));
            Assert.That(draft.LocalElements, Has.Count.EqualTo(1));
            Assert.That(draft.Layers, Is.Empty);
        }

        [Test]
        public void ExistingGlobalElementListFiltersByPrefabAndMatchesOrderedMembers()
        {
            var element = new VfxSourceElementView
            {
                ElementId = "vfx.element.authored.test",
                SourcePrefabGuids = new List<string> { "prefab-a" },
                MemberComponentIds = new List<string> { "member-a", "member-b" }
            };

            Assert.That(
                VfxManualCompositionStudioController.IsSourceImportElementForPrefab(element, "prefab-a"),
                Is.True);
            Assert.That(
                VfxManualCompositionStudioController.IsSourceImportElementForPrefab(element, "prefab-b"),
                Is.False);
            Assert.That(
                VfxManualCompositionStudioController.MatchesSourceImportMemberSelection(
                    element,
                    new[] { "member-a", "member-b" }),
                Is.True);
            Assert.That(
                VfxManualCompositionStudioController.MatchesSourceImportMemberSelection(
                    element,
                    new[] { "member-b", "member-a" }),
                Is.False);
        }

        [Test]
        public void OnlyCreateAndAddLayerReturnsToManualWorkspace()
        {
            Assert.That(
                VfxManualCompositionStudioController.ShouldReturnToManualAfterSourceImport(false),
                Is.False);
            Assert.That(
                VfxManualCompositionStudioController.ShouldReturnToManualAfterSourceImport(true),
                Is.True);
        }

        [Test]
        public void SourceImportWorkspaceState_RoundTripsAndRejectsCorruptOrOldData()
        {
            var state = new SourceImportWorkspaceState
            {
                version = 2,
                source = "favorite",
                search = "beam",
                prefabGuid = SourceGuid,
                selectedMemberIds = new List<string> { "member-a", "member-b" },
                expandedMemberPaths = new List<string> { "Root", "Root/Beam" },
                displayName = "My Beam",
                solo = true,
                normalizedTime = 0.42f,
                cameraDistance = 9f,
                motionConfigured = true,
                motionMode = "抛物线",
                motionDirection = 35f,
                motionDistance = 12f,
                motionSpeed = 6f,
                motionHeight = 5f,
                motionCamera = "跟随"
            };

            SourceImportWorkspaceState restored =
                VfxManualCompositionStudioController.DeserializeSourceImportWorkspaceState(JsonUtility.ToJson(state));

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.source, Is.EqualTo("favorite"));
            Assert.That(restored.selectedMemberIds, Is.EqualTo(state.selectedMemberIds));
            Assert.That(restored.expandedMemberPaths, Is.EqualTo(state.expandedMemberPaths));
            Assert.That(restored.normalizedTime, Is.EqualTo(0.42f).Within(0.0001f));
            Assert.That(restored.motionConfigured, Is.True);
            Assert.That(restored.motionMode, Is.EqualTo("抛物线"));
            Assert.That(restored.motionCamera, Is.EqualTo("跟随"));
            Assert.That(
                VfxManualCompositionStudioController.DeserializeSourceImportWorkspaceState("{broken"),
                Is.Null);
            state.version = 99;
            Assert.That(
                VfxManualCompositionStudioController.DeserializeSourceImportWorkspaceState(JsonUtility.ToJson(state)),
                Is.Null);
        }

        [Test]
        public void SourceImportWorkspaceState_MigratesV1WithMotionSuggestionUnconfigured()
        {
            const string v1 = "{\"version\":1,\"source\":\"recent\",\"search\":\"trail\",\"normalizedTime\":0.5}";

            SourceImportWorkspaceState restored =
                VfxManualCompositionStudioController.DeserializeSourceImportWorkspaceState(v1);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.version, Is.EqualTo(2));
            Assert.That(restored.source, Is.EqualTo("recent"));
            Assert.That(restored.search, Is.EqualTo("trail"));
            Assert.That(restored.normalizedTime, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(restored.motionConfigured, Is.False);
            Assert.That(restored.motionDistance, Is.EqualTo(8f));
            Assert.That(restored.motionSpeed, Is.EqualTo(4f));
            Assert.That(restored.motionHeight, Is.EqualTo(3f));
        }

        [Test]
        public void SourceImportMotionSuggestion_EnablesOnlyTrailEffects()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();

            VfxSourcePrefabDraftImportAnalysis trail = service.Analyze(ProjectileTrailPath, catalog);
            VfxSourcePrefabDraftImportAnalysis impact = service.Analyze(SourcePath, catalog);

            Assert.That(trail.Valid, Is.True, string.Join("\n", trail.Diagnostics));
            Assert.That(VfxManualCompositionStudioController.ShouldSuggestSourceImportMotion(trail), Is.True);
            Assert.That(VfxManualCompositionStudioController.ShouldSuggestSourceImportMotion(impact), Is.False);
        }

        [Test]
        public void SourceImportWorkspacePreferences_AreProjectIsolated()
        {
            string first = VfxManualCompositionStudioController.SourceImportWorkspaceStatePreferenceKey(
                @"E:\Unity\WxFramework\WGameFramework\Assets");
            string same = VfxManualCompositionStudioController.SourceImportWorkspaceStatePreferenceKey(
                "e:/unity/wxframework/wgameframework/assets/");
            string second = VfxManualCompositionStudioController.SourceImportWorkspaceStatePreferenceKey(
                @"E:\Unity\WxFramework\WGameFramework_1\Assets");

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(
                VfxAuthoringResourceLibraryWindow.SourceImportActivePreferenceKey(
                    @"E:\Unity\WxFramework\WGameFramework\Assets"),
                Is.Not.EqualTo(
                    VfxAuthoringResourceLibraryWindow.SourceImportActivePreferenceKey(
                        @"E:\Unity\WxFramework\WGameFramework_1\Assets")));

            string fakeProject = "X:/VfxStateTest/" + Guid.NewGuid().ToString("N") + "/Assets";
            string activeKey = VfxAuthoringResourceLibraryWindow.SourceImportActivePreferenceKey(fakeProject);
            try
            {
                EditorPrefs.SetBool(activeKey, true);
                Assert.That(
                    VfxAuthoringResourceLibraryWindow.ShouldRestoreSourceImportWorkspace(fakeProject),
                    Is.True);
                EditorPrefs.SetBool(activeKey, false);
                Assert.That(
                    VfxAuthoringResourceLibraryWindow.ShouldRestoreSourceImportWorkspace(fakeProject),
                    Is.False);
            }
            finally
            {
                EditorPrefs.DeleteKey(activeKey);
            }
        }

        [Test]
        public void SourceImportMemberRestore_RequiresExactOrderAndDependencySignature()
        {
            var source = new VfxSourcePrefabDraftImportAnalysis
            {
                Effect = new VfxSourceEffectView()
            };
            source.Members.Add(new VfxSourceComponentView { ComponentId = "member-a" });
            source.Members.Add(new VfxSourceComponentView { ComponentId = "member-b" });
            var selection = new VfxSourcePrefabMemberSelectionAnalysis { Source = source };
            selection.Members.Add(source.Members[0]);
            selection.Members.Add(source.Members[1]);
            var dependency = new VfxSourceMemberDependencyGroup { GroupId = "group-a" };
            dependency.MemberComponentIds.Add("member-a");
            dependency.MemberComponentIds.Add("member-b");
            selection.DependencyGroups.Add(dependency);
            string signature = VfxSourcePrefabDraftImportService.SelectedDependencyGroupSignature(selection);

            Assert.That(
                VfxManualCompositionStudioController.MatchesSourceImportRestoredMemberState(
                    new[] { "member-a", "member-b" }, signature, selection),
                Is.True);
            Assert.That(
                VfxManualCompositionStudioController.MatchesSourceImportRestoredMemberState(
                    new[] { "member-b", "member-a" }, signature, selection),
                Is.False);
            Assert.That(
                VfxManualCompositionStudioController.MatchesSourceImportRestoredMemberState(
                    new[] { "member-a", "member-b" }, "changed", selection),
                Is.False);
        }

        [Test]
        public void CurrentDraftImportWritesTrimmedCardDisplayNameInSameTransaction()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(SourcePath, catalog);
            var draft = new VfxCompositionDraftView { DraftId = "vfx.composition.test.display-name" };
            VfxSourcePrefabDraftImportPlan plan = service.PlanForCurrentDraft(analysis, draft);

            Assert.That(service.ApplyToCurrentDraft(
                analysis, catalog, draft, plan, "  火焰爆炸  ", false, "unused", "Loop",
                out VfxSourceElementView element, out _, out string message), Is.True, message);

            Assert.That(element.DisplayName, Is.EqualTo("火焰爆炸"));
            Assert.That(draft.LocalElements, Has.Count.EqualTo(1));
        }

        [TestCase("")]
        [TestCase("123456789012345678901234567890123")]
        public void CurrentDraftImportRejectsInvalidCardDisplayNameWithoutMutation(string displayName)
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(SourcePath, catalog);
            var draft = new VfxCompositionDraftView { DraftId = "vfx.composition.test.invalid-display-name" };
            VfxSourcePrefabDraftImportPlan plan = service.PlanForCurrentDraft(analysis, draft);

            Assert.That(service.ApplyToCurrentDraft(
                analysis, catalog, draft, plan, displayName, false, "unused", "Loop",
                out _, out _, out string message), Is.False);

            Assert.That(message, Does.Contain("1–32"));
            Assert.That(draft.LocalElements, Is.Empty);
        }

        [Test]
        public void AnalysisExposesStructuredValidationForFullScreenChecklist()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();

            VfxSourcePrefabDraftImportAnalysis analysis = new VfxSourcePrefabDraftImportService().Analyze(SourcePath, catalog);

            Assert.That(analysis.Valid, Is.True, string.Join("\n", analysis.Diagnostics));
            Assert.That(analysis.IndexIdentityMatched, Is.True);
            Assert.That(analysis.ContentHashMatched, Is.True);
            Assert.That(analysis.DependencyHashMatched, Is.True);
            Assert.That(analysis.MemberIdentityMatched, Is.True);
            Assert.That(analysis.SubEmitterReferencesValid, Is.True);
        }

        [Test]
        public void CurrentDraftSecondaryActionAddsSourceTimedLayer()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(SourcePath, catalog);
            var draft = new VfxCompositionDraftView { DraftId = "vfx.composition.test.layer" };
            VfxSourcePrefabDraftImportPlan plan = service.PlanForCurrentDraft(analysis, draft);

            Assert.That(service.ApplyToCurrentDraft(
                analysis, catalog, draft, plan, true, "layer_01_source", "Build",
                out _, out VfxCompositionLayerView layer, out string message), Is.True, message);

            Assert.That(draft.LocalElements, Has.Count.EqualTo(1));
            Assert.That(draft.Layers, Has.Count.EqualTo(1));
            Assert.That(layer.Phase, Is.EqualTo("Build"));
            Assert.That(layer.VisualStart.Mode, Is.EqualTo(nameof(VfxVisualStartMode.PreserveSource)));
            Assert.That(layer.Timing.TargetArtisticFrames, Is.EqualTo(analysis.EstimatedArtisticFrames));
        }

        [Test]
        public void CurrentDraftEvidenceUpdatePreservesManualSemanticsAndExistingLayers()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(SourcePath, catalog);
            var draft = new VfxCompositionDraftView { DraftId = "vfx.composition.test.update" };
            VfxSourcePrefabDraftImportPlan addPlan = service.PlanForCurrentDraft(analysis, draft);
            Assert.That(service.ApplyToCurrentDraft(
                analysis, catalog, draft, addPlan, true, "manual_layer", "Loop",
                out VfxSourceElementView element, out _, out string message), Is.True, message);
            element.DisplayName = "人工展示名";
            element.Theme = "Ice";
            element.SourcePrefabContentHash = "old-evidence";
            draft.Layers[0].StartFrame = 27;

            VfxSourcePrefabDraftImportPlan updatePlan = service.PlanForCurrentDraft(analysis, draft);
            Assert.That(updatePlan.Action, Is.EqualTo(VfxSourcePrefabDraftImportAction.UpdateEvidence));
            Assert.That(service.ApplyToCurrentDraft(
                analysis, catalog, draft, updatePlan, false, "unused", "Loop",
                out element, out _, out message), Is.True, message);

            Assert.That(element.DisplayName, Is.EqualTo("人工展示名"));
            Assert.That(element.Theme, Is.EqualTo("Ice"));
            Assert.That(element.SourcePrefabContentHash, Is.EqualTo(analysis.Effect.ContentHash));
            Assert.That(draft.Layers, Has.Count.EqualTo(1));
            Assert.That(draft.Layers[0].StartFrame, Is.EqualTo(27));
        }

        [Test]
        public void CurrentDraftMemberIdentityChangeBlocksEvidenceOverwrite()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(SourcePath, catalog);
            var draft = new VfxCompositionDraftView { DraftId = "vfx.composition.test.remap" };
            VfxSourcePrefabDraftImportPlan addPlan = service.PlanForCurrentDraft(analysis, draft);
            Assert.That(service.ApplyToCurrentDraft(
                analysis, catalog, draft, addPlan, false, "unused", "Loop",
                out VfxSourceElementView element, out _, out string message), Is.True, message);
            element.MemberComponentIds.Reverse();

            VfxSourcePrefabDraftImportPlan blocked = service.PlanForCurrentDraft(analysis, draft);

            Assert.That(blocked.Action, Is.EqualTo(VfxSourcePrefabDraftImportAction.Blocked));
            Assert.That(blocked.CanApply, Is.False);
            Assert.That(blocked.Message, Does.Contain("重映射"));
        }

        [Test]
        public void ReimportPreservesMappedManualOverrides()
        {
            VfxSourceElementCatalog catalog = CreateIndexedPrefab(out string sourcePath);
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(sourcePath, catalog);
            Assert.That(service.CreateOrUpdate(analysis, catalog, null, out VfxCompositionDraftView original, out string message), Is.True, message);
            Assert.That(original.Layers[0].Timing.RetimeMode, Is.EqualTo(nameof(VfxRetimeMode.PlaybackWindow)));
            original.Brief = "人工 Brief";
            original.Layers[0].Transform.Position[0] = 3f;
            original.Layers[0].StartFrame = 12;
            original.Layers[0].Timing.MinimumBodyFrames = 12;
            original.Layers[0].MemberParticipation.Add(new VfxMemberParticipationView
            {
                MemberId = analysis.Members[0].ComponentId,
                State = nameof(VfxMemberParticipationState.Override)
            });

            Assert.That(service.CreateOrUpdate(analysis, catalog, original, out VfxCompositionDraftView updated, out message), Is.True, message);
            Assert.That(updated.Brief, Is.EqualTo("人工 Brief"));
            Assert.That(updated.Layers[0].Transform.Position[0], Is.EqualTo(3f));
            Assert.That(updated.Layers[0].StartFrame, Is.EqualTo(12));
            Assert.That(updated.Layers[0].Timing.MinimumBodyFrames, Is.EqualTo(12));
            Assert.That(updated.Layers[0].MemberParticipation.Single().MemberId, Is.EqualTo(analysis.Members[0].ComponentId));
        }

        [Test]
        public void ReimportPreservesPlaybackWindowEndPolicy()
        {
            VfxSourceElementCatalog catalog = CreateIndexedPrefab(out string sourcePath);
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(sourcePath, catalog);
            Assert.That(service.CreateOrUpdate(analysis, catalog, null, out VfxCompositionDraftView original, out string message), Is.True, message);
            original.Layers[0].Timing.WindowEndPolicy = nameof(UnityVfxLayerWindowEndPolicy.DissipateThenClear);
            original.Layers[0].Timing.DissipateFrames = 12;

            Assert.That(service.CreateOrUpdate(analysis, catalog, original, out VfxCompositionDraftView updated, out message), Is.True, message);

            Assert.That(updated.Layers[0].Timing.RetimeMode, Is.EqualTo(nameof(VfxRetimeMode.PlaybackWindow)));
            Assert.That(updated.Layers[0].Timing.WindowEndPolicy, Is.EqualTo(nameof(UnityVfxLayerWindowEndPolicy.DissipateThenClear)));
            Assert.That(updated.Layers[0].Timing.DissipateFrames, Is.EqualTo(12));
        }

        [Test]
        public void ReimportPreservesVisualStartMode()
        {
            VfxSourceElementCatalog catalog = CreateIndexedPrefab(out string sourcePath);
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(sourcePath, catalog);
            Assert.That(service.CreateOrUpdate(analysis, catalog, null, out VfxCompositionDraftView original, out string message), Is.True, message);
            original.Layers[0].VisualStart.Mode = nameof(VfxVisualStartMode.AutoNormalize);

            Assert.That(service.CreateOrUpdate(analysis, catalog, original, out VfxCompositionDraftView updated, out message), Is.True, message);

            Assert.That(updated.Layers[0].VisualStart.Mode, Is.EqualTo(nameof(VfxVisualStartMode.AutoNormalize)));
        }

        [Test]
        public void ReimportPreservesManualLeadInFrames()
        {
            VfxSourceElementCatalog catalog = CreateIndexedPrefab(out string sourcePath);
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(sourcePath, catalog);
            Assert.That(service.CreateOrUpdate(analysis, catalog, null, out VfxCompositionDraftView original, out string message), Is.True, message);
            original.Layers[0].VisualStart.ManualLeadInFrames = 6;

            Assert.That(service.CreateOrUpdate(analysis, catalog, original, out VfxCompositionDraftView updated, out message), Is.True, message);

            Assert.That(updated.Layers[0].VisualStart.Mode, Is.EqualTo(nameof(VfxVisualStartMode.PreserveSource)));
            Assert.That(updated.Layers[0].VisualStart.ManualLeadInFrames, Is.EqualTo(6));
        }

        [Test]
        public void ReimportWithStartFrameRefreshesUpdatedSourceTimingBaseline()
        {
            VfxSourceElementCatalog catalog = CreateIndexedPrefab(out string sourcePath);
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis originalAnalysis = service.Analyze(sourcePath, catalog);
            Assert.That(service.CreateOrUpdate(originalAnalysis, catalog, null, out VfxCompositionDraftView original, out string message), Is.True, message);
            int originalSourceFrames = original.Layers[0].EstimatedSourceFrames;
            original.Layers[0].StartFrame = 12;
            IncreasePrefabDurationAndRefreshIndex(catalog, sourcePath, 2f);
            VfxSourcePrefabDraftImportAnalysis updatedAnalysis = service.Analyze(sourcePath, catalog);
            Assert.That(updatedAnalysis.Valid, Is.True, string.Join("\n", updatedAnalysis.Diagnostics));
            Assert.That(updatedAnalysis.EstimatedArtisticFrames, Is.GreaterThan(originalSourceFrames));

            Assert.That(service.CreateOrUpdate(updatedAnalysis, catalog, original, out VfxCompositionDraftView updated, out message), Is.True, message);

            Assert.That(updated.Layers[0].StartFrame, Is.EqualTo(12));
            Assert.That(updated.Layers[0].EstimatedSourceFrames, Is.EqualTo(updatedAnalysis.EstimatedArtisticFrames));
            AssertSourceSegments(updated.Layers[0].Timing.SourceSegments, updatedAnalysis.SourceSegments);
            Assert.That(updated.Layers[0].Timing.RetimeMode, Is.EqualTo(nameof(VfxRetimeMode.PlaybackWindow)));
            Assert.That(updated.Layers[0].Timing.TargetArtisticFrames, Is.EqualTo(updatedAnalysis.EstimatedArtisticFrames));
            Assert.That(updated.LocalElements[0].SourcePrefabContentHash, Is.EqualTo(catalog.Index.Effects.Single().ContentHash));
        }

        [Test]
        public void ReimportWithRetimeRefreshesSourceBaselineAndPreservesTarget()
        {
            VfxSourceElementCatalog catalog = CreateIndexedPrefab(out string sourcePath);
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis originalAnalysis = service.Analyze(sourcePath, catalog);
            Assert.That(service.CreateOrUpdate(originalAnalysis, catalog, null, out VfxCompositionDraftView original, out string message), Is.True, message);
            int manualTargetFrames = original.Layers[0].Timing.TargetArtisticFrames + 30;
            original.Layers[0].Timing.TargetArtisticFrames = manualTargetFrames;
            IncreasePrefabDurationAndRefreshIndex(catalog, sourcePath, 2f);
            VfxSourcePrefabDraftImportAnalysis updatedAnalysis = service.Analyze(sourcePath, catalog);
            Assert.That(updatedAnalysis.Valid, Is.True, string.Join("\n", updatedAnalysis.Diagnostics));

            Assert.That(service.CreateOrUpdate(updatedAnalysis, catalog, original, out VfxCompositionDraftView updated, out message), Is.True, message);

            Assert.That(updated.Layers[0].EstimatedSourceFrames, Is.EqualTo(updatedAnalysis.EstimatedArtisticFrames));
            AssertSourceSegments(updated.Layers[0].Timing.SourceSegments, updatedAnalysis.SourceSegments);
            Assert.That(updated.Layers[0].Timing.RetimeMode, Is.EqualTo(nameof(VfxRetimeMode.PlaybackWindow)));
            Assert.That(updated.Layers[0].Timing.TargetArtisticFrames, Is.EqualTo(manualTargetFrames));
        }

        [Test]
        public void AnalyzeIncludesUnweightedTangentPeakInSourceLifetime()
        {
            VfxSourceElementCatalog catalog = CreateIndexedPrefab(out string prefabPath);
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 10f),
                new Keyframe(1f, 0f, -10f, 0f));
            float midpointPeak = SetPrefabLifetimeCurveAndRefreshIndex(catalog, prefabPath, curve);

            VfxSourcePrefabDraftImportAnalysis analysis = new VfxSourcePrefabDraftImportService().Analyze(prefabPath, catalog);

            Assert.That(midpointPeak, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(analysis.Valid, Is.True, string.Join("\n", analysis.Diagnostics));
            Assert.That(analysis.SourceSegments.ExitFrames, Is.EqualTo(Mathf.CeilToInt(midpointPeak * VfxSourcePrefabDraftImportService.FramesPerSecond)));
            Assert.That(analysis.EstimatedArtisticFrames, Is.EqualTo(analysis.SourceSegments.ArtisticFrames));
        }

        [Test]
        public void AnalyzeIncludesWeightedTangentPeakInSourceLifetime()
        {
            VfxSourceElementCatalog catalog = CreateIndexedPrefab(out string prefabPath);
            var left = new Keyframe(0f, 0f, 0f, 10f) { outWeight = 0.1f, weightedMode = WeightedMode.Out };
            var right = new Keyframe(1f, 0f, -10f, 0f) { inWeight = 0.1f, weightedMode = WeightedMode.In };
            var curve = new AnimationCurve(left, right);
            float midpointPeak = SetPrefabLifetimeCurveAndRefreshIndex(catalog, prefabPath, curve);

            VfxSourcePrefabDraftImportAnalysis analysis = new VfxSourcePrefabDraftImportService().Analyze(prefabPath, catalog);

            Assert.That(midpointPeak, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(analysis.Valid, Is.True, string.Join("\n", analysis.Diagnostics));
            Assert.That(analysis.SourceSegments.ExitFrames, Is.EqualTo(Mathf.CeilToInt(midpointPeak * VfxSourcePrefabDraftImportService.FramesPerSecond)));
            Assert.That(analysis.EstimatedArtisticFrames, Is.EqualTo(analysis.SourceSegments.ArtisticFrames));
        }

        [Test]
        public void AnalyzeRejectsStaleIndexWhenPrefabContentChangesWithoutMemberStructureChange()
        {
            VfxSourceElementCatalog catalog = CreateIndexedPrefab(out string prefabPath);
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            GameObject sourceRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ParticleSystem.MainModule main = sourceRoot.GetComponent<ParticleSystem>().main;
                main.duration += 0.25f;
                PrefabUtility.SaveAsPrefabAsset(sourceRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(sourceRoot);
            }
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);

            GameObject changedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(AssetDatabase.AssetPathToGUID(prefabPath), Is.EqualTo(guid));
            Assert.That(changedPrefab.GetComponentsInChildren<ParticleSystem>(true), Has.Length.EqualTo(1));

            VfxSourcePrefabDraftImportAnalysis analysis = new VfxSourcePrefabDraftImportService().Analyze(prefabPath, catalog);

            Assert.That(analysis.Valid, Is.False);
            Assert.That(analysis.Members, Is.Empty);
            Assert.That(analysis.Diagnostics, Has.Some.Contains("内容 hash 已过期"));
            Assert.That(analysis.Diagnostics, Has.Some.Contains("重建 Source Index"));
        }

        [Test]
        public void AnalyzeRejectsStaleIndexWhenDependencyHashDiffersWithoutPrefabOrMemberStructureChange()
        {
            VfxSourceElementCatalog catalog = CreateIndexedPrefab(out string prefabPath);
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            string prefabContentHash = VfxCompositionDraftService.HashFile(prefabPath);
            string currentDependencyHash = AssetDatabase.GetAssetDependencyHash(prefabPath).ToString();
            catalog.Index.Effects.Single().DependencyHash = Hash128.Compute(currentDependencyHash + ":stale").ToString();

            GameObject changedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(AssetDatabase.AssetPathToGUID(prefabPath), Is.EqualTo(guid));
            Assert.That(VfxCompositionDraftService.HashFile(prefabPath), Is.EqualTo(prefabContentHash));
            Assert.That(catalog.Index.Effects.Single().DependencyHash, Is.Not.EqualTo(currentDependencyHash));
            Assert.That(changedPrefab.GetComponentsInChildren<ParticleSystem>(true), Has.Length.EqualTo(1));

            VfxSourcePrefabDraftImportAnalysis analysis = new VfxSourcePrefabDraftImportService().Analyze(prefabPath, catalog);

            Assert.That(analysis.Valid, Is.False);
            Assert.That(analysis.Members, Is.Empty);
            Assert.That(analysis.Diagnostics, Has.None.Contains("内容 hash 已过期"));
            Assert.That(analysis.Diagnostics, Has.Some.Contains("依赖 hash 已过期"));
            Assert.That(analysis.Diagnostics, Has.Some.Contains("重建 Source Index"));
        }

        [Test]
        public void AnalyzeMapsSameNamedSiblingParticleSystemsByLocalFileId()
        {
            VfxSourceElementCatalog catalog = CreateIndexedPrefabWithSameNamedSiblings(out string prefabPath, out long[] expectedLocalFileIds);

            VfxSourcePrefabDraftImportAnalysis analysis = new VfxSourcePrefabDraftImportService().Analyze(prefabPath, catalog);

            Assert.That(analysis.Valid, Is.True, string.Join("\n", analysis.Diagnostics));
            Assert.That(analysis.Members, Has.Count.EqualTo(2));
            Assert.That(analysis.Members.Select(value => value.ObjectPath), Is.All.EqualTo("Duplicate"));
            Assert.That(analysis.Members.Select(value => value.LocalFileId), Is.EqualTo(expectedLocalFileIds));
            Assert.That(analysis.Members.Select(value => value.ComponentId).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void PreviewInstantiationRetainsAllSevenSerializedParticleSystems()
        {
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(SourcePath, catalog);
            Assert.That(service.CreateOrUpdate(analysis, catalog, null, out VfxCompositionDraftView draft, out string message), Is.True, message);
            catalog.SetLocalElements(draft.LocalElements);
            GameObject sourceRoot = PrefabUtility.LoadPrefabContents(SourcePath);
            GameObject previewRoot = VfxSourceElementInstantiation.Create(catalog, draft.LocalElements[0], "Reverse Import Test");
            try
            {
                Assert.That(previewRoot, Is.Not.Null);
                ParticleSystem[] targets = previewRoot.GetComponentsInChildren<ParticleSystem>(true);
                Assert.That(targets, Has.Length.EqualTo(7));
                for (int i = 0; i < analysis.Members.Count; i++)
                {
                    VfxSourceComponentView member = analysis.Members[i];
                    ParticleSystem source = VfxSourceElementCurationService.FindSourceSystem(sourceRoot, member);
                    ParticleSystem target = targets[i];
                    Assert.That(source, Is.Not.Null, member.ComponentId);
                    NormalizeDisabledSerializedSubEmitterSlots(source);
                    Assert.That(EditorJsonUtility.ToJson(target), Is.EqualTo(EditorJsonUtility.ToJson(source)), member.DisplayName);
                    ParticleSystemRenderer sourceRenderer = source.GetComponent<ParticleSystemRenderer>();
                    ParticleSystemRenderer targetRenderer = target.GetComponent<ParticleSystemRenderer>();
                    Assert.That(EditorJsonUtility.ToJson(targetRenderer), Is.EqualTo(EditorJsonUtility.ToJson(sourceRenderer)), member.DisplayName + " renderer");
                    Matrix4x4 relative = sourceRoot.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                    Assert.That(Vector3.Distance(target.transform.localPosition, relative.GetColumn(3)), Is.LessThan(0.00001f));
                }
            }
            finally
            {
                if (previewRoot != null) Object.DestroyImmediate(previewRoot);
                PrefabUtility.UnloadPrefabContents(sourceRoot);
            }
        }

        private static void NormalizeDisabledSerializedSubEmitterSlots(ParticleSystem system)
        {
            if (system.subEmitters.enabled) return;
            var serialized = new SerializedObject(system);
            SerializedProperty slots = serialized.FindProperty("SubModule.subEmitters");
            if (slots == null || !slots.isArray || slots.arraySize == 0) return;
            slots.arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void ManaProjectileRainRemapsAllInternalSubEmittersWithoutChangingSource()
        {
            string sourceHash = VfxCompositionDraftService.HashFile(ManaProjectileRainPath);
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(ManaProjectileRainPath, catalog);

            Assert.That(analysis.Valid, Is.True, string.Join("\n", analysis.Diagnostics));
            Assert.That(analysis.SubEmitterReferencesValid, Is.True);
            Assert.That(analysis.Members, Has.Count.EqualTo(47));
            Assert.That(service.CreateOrUpdate(analysis, catalog, null, out VfxCompositionDraftView draft, out string message), Is.True, message);
            catalog.SetLocalElements(draft.LocalElements);

            GameObject sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ManaProjectileRainPath);
            GameObject previewRoot = VfxSourceElementInstantiation.Create(catalog, draft.LocalElements[0], "SubEmitter Remap Test");
            try
            {
                Assert.That(previewRoot, Is.Not.Null);
                ParticleSystem[] targets = previewRoot.GetComponentsInChildren<ParticleSystem>(true);
                Assert.That(targets, Has.Length.EqualTo(47));
                var sourceToTarget = new System.Collections.Generic.Dictionary<ParticleSystem, ParticleSystem>();
                for (int i = 0; i < analysis.Members.Count; i++)
                {
                    ParticleSystem source = VfxSourceElementCurationService.FindSourceSystem(sourceRoot, analysis.Members[i]);
                    Assert.That(source, Is.Not.Null, analysis.Members[i].ComponentId);
                    sourceToTarget.Add(source, targets[i]);
                }

                int subEmitterCount = 0;
                foreach (System.Collections.Generic.KeyValuePair<ParticleSystem, ParticleSystem> pair in sourceToTarget)
                {
                    ParticleSystem.SubEmittersModule sourceModule = pair.Key.subEmitters;
                    ParticleSystem.SubEmittersModule targetModule = pair.Value.subEmitters;
                    Assert.That(targetModule.enabled, Is.EqualTo(sourceModule.enabled), pair.Key.name);
                    Assert.That(targetModule.subEmittersCount, Is.EqualTo(sourceModule.subEmittersCount), pair.Key.name);
                    for (int i = 0; i < sourceModule.subEmittersCount; i++)
                    {
                        subEmitterCount++;
                        ParticleSystem sourceChild = sourceModule.GetSubEmitterSystem(i);
                        Assert.That(targetModule.GetSubEmitterSystem(i), Is.SameAs(sourceToTarget[sourceChild]), pair.Key.name + " / slot " + i);
                        Assert.That(targetModule.GetSubEmitterType(i), Is.EqualTo(sourceModule.GetSubEmitterType(i)), pair.Key.name + " / slot " + i);
                        Assert.That(targetModule.GetSubEmitterProperties(i), Is.EqualTo(sourceModule.GetSubEmitterProperties(i)), pair.Key.name + " / slot " + i);
                    }
                }
                Assert.That(subEmitterCount, Is.EqualTo(18));
            }
            finally
            {
                if (previewRoot != null) Object.DestroyImmediate(previewRoot);
            }

            VfxCompositionBakeReceipt receipt = null;
            try
            {
                Assert.That(new VfxCompositionCandidateBaker().Bake(draft, catalog, out receipt, out message), Is.True, message);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(receipt.OutputPrefabPath);
                Assert.That(prefab, Is.Not.Null);
                ParticleSystem[] bakedSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);
                Assert.That(bakedSystems, Has.Length.EqualTo(47));
                int bakedSubEmitterCount = 0;
                for (int systemIndex = 0; systemIndex < bakedSystems.Length; systemIndex++)
                {
                    ParticleSystem.SubEmittersModule module = bakedSystems[systemIndex].subEmitters;
                    for (int subEmitterIndex = 0; subEmitterIndex < module.subEmittersCount; subEmitterIndex++)
                    {
                        bakedSubEmitterCount++;
                        ParticleSystem child = module.GetSubEmitterSystem(subEmitterIndex);
                        Assert.That(child, Is.Not.Null, bakedSystems[systemIndex].name + " / slot " + subEmitterIndex);
                        Assert.That(child.transform.IsChildOf(prefab.transform), Is.True, bakedSystems[systemIndex].name + " / slot " + subEmitterIndex);
                    }
                }
                Assert.That(bakedSubEmitterCount, Is.EqualTo(18));
                Assert.That(receipt.SourceAssetsUnchanged, Is.True);
            }
            finally
            {
                if (receipt != null && !string.IsNullOrWhiteSpace(receipt.OutputPrefabPath))
                {
                    string folder = Path.GetDirectoryName(receipt.OutputPrefabPath)?.Replace('\\', '/');
                    if (!string.IsNullOrWhiteSpace(folder)) AssetDatabase.DeleteAsset(folder);
                }
                string token = VfxCompositionDraftService.FileToken(draft.DraftId);
                string receiptPath = VfxCompositionCandidateBaker.ReceiptRoot + "/" + token + ".json";
                if (File.Exists(receiptPath)) File.Delete(receiptPath);
            }
            Assert.That(VfxCompositionDraftService.HashFile(ManaProjectileRainPath), Is.EqualTo(sourceHash));
        }

        [Test]
        public void LightningProjectileAcceptsAndStripsDisabledLegacySubEmitterSlotsWithoutChangingSource()
        {
            string sourceHash = VfxCompositionDraftService.HashFile(LightningProjectilePath);
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(LightningProjectilePath, catalog);

            Assert.That(analysis.Valid, Is.True, string.Join("\n", analysis.Diagnostics));
            Assert.That(analysis.SubEmitterReferencesValid, Is.True);
            Assert.That(analysis.Members, Has.Count.EqualTo(9));
            Assert.That(service.CreateOrUpdate(analysis, catalog, null, out VfxCompositionDraftView draft, out string message), Is.True, message);
            catalog.SetLocalElements(draft.LocalElements);

            GameObject sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(LightningProjectilePath);
            ParticleSystem source = sourceRoot.GetComponentsInChildren<ParticleSystem>(true)
                .First(value => value.name == "Ice_Projectile_fill_add_soft");
            Assert.That(source.subEmitters.enabled, Is.False);
            Assert.That(source.subEmitters.subEmittersCount, Is.EqualTo(12));

            GameObject previewRoot = VfxSourceElementInstantiation.Create(catalog, draft.LocalElements[0], "Disabled SubEmitter Cleanup Test");
            try
            {
                Assert.That(previewRoot, Is.Not.Null);
                ParticleSystem target = previewRoot.GetComponentsInChildren<ParticleSystem>(true)
                    .First(value => value.name == "Ice_Projectile_fill_add_soft");
                Assert.That(target.subEmitters.enabled, Is.False);
                Assert.That(target.subEmitters.subEmittersCount, Is.Zero);
            }
            finally
            {
                if (previewRoot != null) Object.DestroyImmediate(previewRoot);
            }

            Assert.That(VfxCompositionDraftService.HashFile(LightningProjectilePath), Is.EqualTo(sourceHash));
        }

        [Test]
        public void ManaCrossDamageAreaCompactsSingleNullSlotAndRemapsRemainingEnabledSubEmitters()
        {
            string sourceHash = VfxCompositionDraftService.HashFile(ManaCrossDamageAreaPath);
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var service = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = service.Analyze(ManaCrossDamageAreaPath, catalog);

            Assert.That(analysis.Valid, Is.True, string.Join("\n", analysis.Diagnostics));
            Assert.That(analysis.SubEmitterReferencesValid, Is.True);
            Assert.That(analysis.Members, Has.Count.EqualTo(419));
            Assert.That(service.CreateOrUpdate(analysis, catalog, null, out VfxCompositionDraftView draft, out string message), Is.True, message);
            catalog.SetLocalElements(draft.LocalElements);

            GameObject sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ManaCrossDamageAreaPath);
            ParticleSystem[] sourceSystems = sourceRoot.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystem source = sourceSystems.First(value =>
                value.name == "Mana_Mine_Orb_Fill_Alpha_soft_Matcap"
                && value.transform.parent == sourceRoot.transform);
            int sourceIndex = Array.IndexOf(sourceSystems, source);
            Assert.That(source.subEmitters.enabled, Is.True);
            Assert.That(source.subEmitters.subEmittersCount, Is.EqualTo(7));
            Assert.That(source.subEmitters.GetSubEmitterSystem(6), Is.Null);

            GameObject previewRoot = VfxSourceElementInstantiation.Create(catalog, draft.LocalElements[0], "Enabled SubEmitter Null Slot Cleanup Test");
            try
            {
                Assert.That(previewRoot, Is.Not.Null);
                ParticleSystem[] targetSystems = previewRoot.GetComponentsInChildren<ParticleSystem>(true);
                Assert.That(targetSystems, Has.Length.EqualTo(419));
                ParticleSystem target = targetSystems[sourceIndex];
                Assert.That(target.subEmitters.enabled, Is.True);
                Assert.That(target.subEmitters.subEmittersCount, Is.EqualTo(6));
                for (int i = 0; i < target.subEmitters.subEmittersCount; i++)
                    Assert.That(target.subEmitters.GetSubEmitterSystem(i), Is.Not.Null, "slot " + i);
            }
            finally
            {
                if (previewRoot != null) Object.DestroyImmediate(previewRoot);
            }

            Assert.That(VfxCompositionDraftService.HashFile(ManaCrossDamageAreaPath), Is.EqualTo(sourceHash));
        }

        [Test]
        public void PrivateCompoundDraftBakesWithoutChangingSourcePrefab()
        {
            string sourceHash = VfxCompositionDraftService.HashFile(SourcePath);
            VfxSourceElementCatalog catalog = new VfxSourceElementCurationService().Load();
            var importService = new VfxSourcePrefabDraftImportService();
            VfxSourcePrefabDraftImportAnalysis analysis = importService.Analyze(SourcePath, catalog);
            Assert.That(importService.CreateOrUpdate(analysis, catalog, null, out VfxCompositionDraftView draft, out string message), Is.True, message);
            VfxCompositionBakeReceipt receipt = null;
            try
            {
                Assert.That(new VfxCompositionCandidateBaker().Bake(draft, catalog, out receipt, out message), Is.True, message);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(receipt.OutputPrefabPath);
                Assert.That(prefab, Is.Not.Null);
                Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true), Has.Length.EqualTo(7));
                Assert.That(receipt.SourceAssetsUnchanged, Is.True);
                Assert.That(VfxCompositionDraftService.HashFile(SourcePath), Is.EqualTo(sourceHash));
            }
            finally
            {
                if (receipt != null && !string.IsNullOrWhiteSpace(receipt.OutputPrefabPath))
                {
                    string folder = Path.GetDirectoryName(receipt.OutputPrefabPath)?.Replace('\\', '/');
                    if (!string.IsNullOrWhiteSpace(folder)) AssetDatabase.DeleteAsset(folder);
                }
                string token = VfxCompositionDraftService.FileToken(draft.DraftId);
                string receiptPath = VfxCompositionCandidateBaker.ReceiptRoot + "/" + token + ".json";
                if (File.Exists(receiptPath)) File.Delete(receiptPath);
            }
        }

        private VfxSourceElementCatalog CreateIndexedPrefab(out string prefabPath)
        {
            string folderName = Path.GetFileName(_testRoot);
            Assert.That(AssetDatabase.CreateFolder(VfxSourceLibraryEditorService.SourceRoot, folderName), Is.Not.Empty);
            prefabPath = _testRoot + "/Source.prefab";
            var root = new GameObject("Source");
            root.AddComponent<ParticleSystem>();
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            ParticleSystem prefabSystem = prefab.GetComponent<ParticleSystem>();
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefabSystem, out string guid, out long localFileId), Is.True);
            string effectId = "source.effect.test." + guid;
            string componentId = "source.component.test." + guid + "." + localFileId;
            string contentHash = VfxCompositionDraftService.HashFile(prefabPath);
            var effect = new VfxSourceEffectView
            {
                SourceEffectId = effectId,
                SourcePackageId = "piloto.test",
                DisplayName = prefab.name,
                PrefabGuid = guid,
                PrefabPath = prefabPath,
                ContentHash = contentHash,
                DependencyHash = AssetDatabase.GetAssetDependencyHash(prefabPath).ToString(),
                ParticleSystemCount = 1,
                ComponentIds = new System.Collections.Generic.List<string> { componentId }
            };
            var component = new VfxSourceComponentView
            {
                ComponentId = componentId,
                SourceEffectId = effectId,
                SourcePackageId = effect.SourcePackageId,
                PrefabGuid = guid,
                PrefabPath = prefabPath,
                LocalFileId = localFileId,
                ObjectPath = string.Empty,
                DisplayName = prefabSystem.name,
                SourceContentHash = contentHash
            };
            var index = new VfxSourceLibraryView();
            index.Effects.Add(effect);
            index.Components.Add(component);
            return new VfxSourceElementCatalog { Index = index, Available = true };
        }

        private VfxSourceElementCatalog CreateIndexedPrefabWithSameNamedSiblings(out string prefabPath, out long[] localFileIds)
        {
            string folderName = Path.GetFileName(_testRoot);
            Assert.That(AssetDatabase.CreateFolder(VfxSourceLibraryEditorService.SourceRoot, folderName), Is.Not.Empty);
            prefabPath = _testRoot + "/DuplicateSiblings.prefab";
            var root = new GameObject("Source");
            var first = new GameObject("Duplicate");
            var second = new GameObject("Duplicate");
            first.transform.SetParent(root.transform, false);
            second.transform.SetParent(root.transform, false);
            first.AddComponent<ParticleSystem>();
            second.AddComponent<ParticleSystem>();
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            ParticleSystem[] systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(systems, Has.Length.EqualTo(2));
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            string effectId = "source.effect.test." + guid;
            string contentHash = VfxCompositionDraftService.HashFile(prefabPath);
            var effect = new VfxSourceEffectView
            {
                SourceEffectId = effectId,
                SourcePackageId = "piloto.test",
                DisplayName = prefab.name,
                PrefabGuid = guid,
                PrefabPath = prefabPath,
                ContentHash = contentHash,
                DependencyHash = AssetDatabase.GetAssetDependencyHash(prefabPath).ToString(),
                ParticleSystemCount = systems.Length
            };
            var index = new VfxSourceLibraryView();
            index.Effects.Add(effect);
            localFileIds = new long[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(systems[i], out string systemGuid, out long localFileId), Is.True);
                Assert.That(systemGuid, Is.EqualTo(guid));
                localFileIds[i] = localFileId;
                string componentId = "source.component.test." + guid + "." + localFileId;
                effect.ComponentIds.Add(componentId);
                index.Components.Add(new VfxSourceComponentView
                {
                    ComponentId = componentId,
                    SourceEffectId = effectId,
                    SourcePackageId = effect.SourcePackageId,
                    PrefabGuid = guid,
                    PrefabPath = prefabPath,
                    LocalFileId = localFileId,
                    ObjectPath = "Duplicate",
                    DisplayName = systems[i].name,
                    SourceContentHash = contentHash
                });
            }
            return new VfxSourceElementCatalog { Index = index, Available = true };
        }

        private static void IncreasePrefabDurationAndRefreshIndex(VfxSourceElementCatalog catalog, string prefabPath, float seconds)
        {
            GameObject sourceRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ParticleSystem.MainModule main = sourceRoot.GetComponent<ParticleSystem>().main;
                main.duration += seconds;
                PrefabUtility.SaveAsPrefabAsset(sourceRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(sourceRoot);
            }
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            string contentHash = VfxCompositionDraftService.HashFile(prefabPath);
            VfxSourceEffectView effect = catalog.Index.Effects.Single();
            effect.ContentHash = contentHash;
            effect.DependencyHash = AssetDatabase.GetAssetDependencyHash(prefabPath).ToString();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            ParticleSystem system = prefab.GetComponent<ParticleSystem>();
            foreach (VfxSourceComponentView component in catalog.Index.Components)
            {
                component.SourceContentHash = contentHash;
                component.DurationSeconds = system.main.duration;
            }
        }

        private static float SetPrefabLifetimeCurveAndRefreshIndex(
            VfxSourceElementCatalog catalog,
            string prefabPath,
            AnimationCurve curve)
        {
            GameObject sourceRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ParticleSystem.MainModule main = sourceRoot.GetComponent<ParticleSystem>().main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1f, curve);
                PrefabUtility.SaveAsPrefabAsset(sourceRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(sourceRoot);
            }
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            string contentHash = VfxCompositionDraftService.HashFile(prefabPath);
            VfxSourceEffectView effect = catalog.Index.Effects.Single();
            effect.ContentHash = contentHash;
            effect.DependencyHash = AssetDatabase.GetAssetDependencyHash(prefabPath).ToString();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            ParticleSystem system = prefab.GetComponent<ParticleSystem>();
            foreach (VfxSourceComponentView component in catalog.Index.Components)
                component.SourceContentHash = contentHash;
            return system.main.startLifetime.curve.Evaluate(0.5f) * system.main.startLifetime.curveMultiplier;
        }

        private static void AssertSourceSegments(VfxTimeSegmentsView actual, VfxTimeSegmentsView expected)
        {
            Assert.That(actual.PreRollFrames, Is.EqualTo(expected.PreRollFrames));
            Assert.That(actual.EntryFrames, Is.EqualTo(expected.EntryFrames));
            Assert.That(actual.BodyFrames, Is.EqualTo(expected.BodyFrames));
            Assert.That(actual.ExitFrames, Is.EqualTo(expected.ExitFrames));
            Assert.That(actual.CleanupFrames, Is.EqualTo(expected.CleanupFrames));
            Assert.That(actual.MinimumBodyFrames, Is.EqualTo(expected.MinimumBodyFrames));
            Assert.That(actual.CycleFrames, Is.EqualTo(expected.CycleFrames));
        }
    }
}
