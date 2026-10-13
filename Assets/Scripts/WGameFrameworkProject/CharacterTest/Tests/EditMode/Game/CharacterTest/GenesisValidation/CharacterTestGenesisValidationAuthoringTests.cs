using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest.GenesisValidation
{
    public sealed class CharacterTestGenesisValidationAuthoringTests
    {
        [Test]
        public void NormalizeOrdersGroupsAndStationsDeterministically()
        {
            CharacterTestGenesisValidationAuthoringDocument document = CreateValidDocument();
            document.Groups.Insert(0, new CharacterTestGenesisValidationGroup
            {
                StableId = "first",
                DisplayName = "First",
                SortOrder = -10
            });
            document.Stations.Add(CreatePotencyStation("z", "movables", 20));
            document.Stations.Add(CreatePotencyStation("a", "movables", 20));
            document.Stations.Add(CreatePotencyStation("first-station", "first", 100));

            CharacterTestGenesisValidationAuthoringDocument normalized = document.Normalize();

            Assert.That(normalized.Groups[0].StableId, Is.EqualTo("first"));
            Assert.That(normalized.Stations[0].StableId, Is.EqualTo("first-station"));
            Assert.That(normalized.Stations[1].StableId, Is.EqualTo("station"));
            Assert.That(normalized.Stations[2].StableId, Is.EqualTo("a"));
            Assert.That(normalized.Stations[3].StableId, Is.EqualTo("z"));
        }

        [Test]
        public void RoundTripPreservesSupportedExtensionFields()
        {
            CharacterTestGenesisValidationAuthoringDocument document = CreateValidDocument();
            document.ExtensionData = new Dictionary<string, JToken>
            {
                ["futureRoot"] = new JObject { ["value"] = 7 }
            };
            document.Stations[0].ExtensionData = new Dictionary<string, JToken>
            {
                ["futureStation"] = "kept"
            };
            document.Stations[0].Scenario.Payload["futurePayload"] = 13;
            document.Stations[0].Bindings.TargetMovableObject.ExtensionData =
                new Dictionary<string, JToken> { ["futureRef"] = "kept" };

            JObject json = JObject.FromObject(document.Normalize());
            CharacterTestGenesisValidationAuthoringDocument restored =
                json.ToObject<CharacterTestGenesisValidationAuthoringDocument>().Normalize();

            Assert.That((int)restored.ExtensionData["futureRoot"]["value"], Is.EqualTo(7));
            Assert.That((string)restored.Stations[0].ExtensionData["futureStation"], Is.EqualTo("kept"));
            Assert.That((int)restored.Stations[0].Scenario.Payload["futurePayload"], Is.EqualTo(13));
            Assert.That(
                (string)restored.Stations[0].Bindings.TargetMovableObject.ExtensionData["futureRef"],
                Is.EqualTo("kept"));
        }

        [Test]
        public void EveryBindingReferenceNormalizePreservesExtensionFields()
        {
            IDictionary<string, JToken> extension =
                new Dictionary<string, JToken> { ["future"] = 7 };
            var bindings = new CharacterTestGenesisValidationBindings
            {
                SubjectCharacter = new CharacterTestGenesisValidationCharacterRef
                    { StableId = "character", ExtensionData = extension },
                TargetMovableObject = new CharacterTestGenesisValidationMovableObjectRef
                    { InstanceStableId = "movable", ExtensionData = extension },
                StartPoint = new CharacterTestGenesisValidationPointRef
                    { StableId = "start", ExtensionData = extension },
                ResetPoint = new CharacterTestGenesisValidationPointRef
                    { StableId = "reset", ExtensionData = extension },
                ObservationRegion = new CharacterTestGenesisValidationRegionRef
                    { StableId = "region", ExtensionData = extension },
                TargetStaticCollider = new CharacterTestGenesisValidationStaticColliderRef
                {
                    SourceKind = CharacterTestGenesisValidationStaticSourceKind.SingleStatic,
                    BodyId = 1,
                    ColliderId = 1,
                    ExpectedDefinitionId = 1,
                    ExtensionData = extension
                }
            }.Normalize();

            Assert.That((int)bindings.SubjectCharacter.ExtensionData["future"], Is.EqualTo(7));
            Assert.That((int)bindings.TargetMovableObject.ExtensionData["future"], Is.EqualTo(7));
            Assert.That((int)bindings.StartPoint.ExtensionData["future"], Is.EqualTo(7));
            Assert.That((int)bindings.ResetPoint.ExtensionData["future"], Is.EqualTo(7));
            Assert.That((int)bindings.ObservationRegion.ExtensionData["future"], Is.EqualTo(7));
            Assert.That((int)bindings.TargetStaticCollider.ExtensionData["future"], Is.EqualTo(7));
        }

        [Test]
        public void AuthoringDescriptorDoesNotRequireExecutableAdapter()
        {
            CharacterTestGenesisValidationAuthoringDocument document = CreateValidDocument();
            CharacterTestGenesisValidationOwnerCatalog owners = CreateOwners();

            CharacterTestGenesisValidationAuthoringReport report =
                CharacterTestGenesisValidationAuthoringValidator.Validate(document, owners);

            Assert.That(report.HasErrors, Is.False, JoinIssues(report));
            Assert.That(
                CharacterTestGenesisValidationCapabilityResolver.ResolveAuthority(document.Stations[0]),
                Is.EqualTo(CharacterTestGenesisValidationAuthorityStatus.HarnessOnly));
            Assert.That(
                CharacterTestGenesisValidationCapabilityResolver.ResolveSceneReadiness(document.Stations[0]),
                Is.EqualTo(CharacterTestGenesisValidationSceneReadiness.NotRunnable));
            Assert.That(
                CharacterTestGenesisValidationCapabilityResolver.ResolveCacheHealth(),
                Is.EqualTo(CharacterTestGenesisValidationCacheHealth.NotBuilt));
        }

        [Test]
        public void UnknownSchemaAndScenarioFailClosed()
        {
            CharacterTestGenesisValidationAuthoringDocument unknownSchema = CreateValidDocument();
            unknownSchema.SchemaVersion = 2;
            CharacterTestGenesisValidationAuthoringReport schemaReport =
                CharacterTestGenesisValidationAuthoringValidator.Validate(unknownSchema, CreateOwners());

            CharacterTestGenesisValidationAuthoringDocument unknownScenario = CreateValidDocument();
            unknownScenario.Stations[0].Scenario.TypeId = "UnknownScenario";
            CharacterTestGenesisValidationAuthoringReport scenarioReport =
                CharacterTestGenesisValidationAuthoringValidator.Validate(unknownScenario, CreateOwners());

            AssertIssue(schemaReport, "UnknownSchemaVersion");
            AssertIssue(scenarioReport, "UnknownScenarioType");
        }

        [Test]
        public void UnknownModuleSchemaFailsClosedInsteadOfFallingBackToEmptyAuthoring()
        {
            var envelope = new MxLevelAuthoringDocument();
            envelope.UpsertModulePayload(
                CharacterTestGenesisValidationAuthoringAdapter.ModuleId,
                99,
                CreateValidDocument(),
                CharacterTestGenesisValidationAuthoringAdapter.DisplayName);

            CharacterTestGenesisValidationAuthoringDocument extracted =
                CharacterTestGenesisValidationAuthoringAdapter.Extract(envelope);
            CharacterTestGenesisValidationAuthoringReport report =
                CharacterTestGenesisValidationAuthoringValidator.Validate(extracted, CreateOwners());

            AssertIssue(report, "UnknownSchemaVersion");
        }

        [TestCase("sourceKind", "FutureStaticKind")]
        [TestCase("resetPolicy", "FutureResetPolicy")]
        public void UnknownEnumStringBecomesDiagnosticAndOriginalEnvelopeRemainsUntouched(
            string propertyName,
            string unknownValue)
        {
            CharacterTestGenesisValidationAuthoringDocument document = CreateValidDocument();
            document.Stations[0].Bindings.TargetStaticCollider =
                new CharacterTestGenesisValidationStaticColliderRef
                {
                    SourceKind = CharacterTestGenesisValidationStaticSourceKind.SingleStatic,
                    BodyId = 61710,
                    ColliderId = 1,
                    ExpectedDefinitionId = 1011
                };
            var envelope = new MxLevelAuthoringDocument();
            envelope.UpsertModulePayload(
                CharacterTestGenesisValidationAuthoringAdapter.ModuleId,
                1,
                document,
                CharacterTestGenesisValidationAuthoringAdapter.DisplayName);
            envelope.TryGetModule(
                CharacterTestGenesisValidationAuthoringAdapter.ModuleId,
                out MxLevelAuthoringModuleDocument module);
            JObject station = (JObject)((JArray)module.Payload["stations"])[0];
            JObject target = string.Equals(propertyName, "resetPolicy", StringComparison.Ordinal)
                ? station
                : (JObject)station["bindings"]["targetStaticCollider"];
            target[propertyName] = unknownValue;
            JObject original = (JObject)module.Payload.DeepClone();

            CharacterTestGenesisValidationAuthoringDocument extracted =
                CharacterTestGenesisValidationAuthoringAdapter.Extract(envelope);
            CharacterTestGenesisValidationAuthoringReport report =
                CharacterTestGenesisValidationAuthoringValidator.Validate(extracted, CreateOwners());

            AssertIssue(report, "AuthoringPayloadReadFailed");
            Assert.That(JToken.DeepEquals(module.Payload, original), Is.True);
        }

        [Test]
        public void DescriptorOwnsCommonAndScenarioSpecificInspectorMetadata()
        {
            var expectedSpecific = new Dictionary<string, string>
            {
                [CharacterTestGenesisValidationAuthoringScenarioRegistry.SurfaceContactResponse] =
                    "PreserveComparisonOrder",
                [CharacterTestGenesisValidationAuthoringScenarioRegistry.BoundaryBodyResponse] =
                    "ApproachDirectionMilli",
                [CharacterTestGenesisValidationAuthoringScenarioRegistry.MovableNaturalObjectResponse] =
                    "PushDirectionMilli",
                [CharacterTestGenesisValidationAuthoringScenarioRegistry.PotencyReceptionDebug] =
                    "InfluenceProfileId"
            };

            for (int i = 0; i < CharacterTestGenesisValidationAuthoringScenarioRegistry.All.Count; i++)
            {
                ICharacterTestGenesisValidationAuthoringScenarioDescriptor descriptor =
                    CharacterTestGenesisValidationAuthoringScenarioRegistry.All[i];
                AssertInspectorField(descriptor, "RequestProfileId");
                AssertInspectorField(descriptor, "DurationFrames");
                AssertInspectorField(descriptor, "ObservationFrames");
                AssertInspectorField(descriptor, "ComparisonOrder");
                AssertInspectorField(descriptor, expectedSpecific[descriptor.TypeId]);
                Assert.That(descriptor.BindingSlots.Count, Is.GreaterThan(0));
                for (int slotIndex = 0; slotIndex < descriptor.BindingSlots.Count; slotIndex++)
                    Assert.That(descriptor.BindingSlots[slotIndex].Required, Is.True);
            }
        }

        [Test]
        public void EnvelopeRoundTripPreservesValidationPayloadAndUnrelatedModule()
        {
            CharacterTestGenesisValidationAuthoringDocument document = CreateValidDocument();
            document.Stations[0].Scenario.Payload["futurePayload"] = 21;
            var envelope = new MxLevelAuthoringDocument();
            envelope.UpsertModulePayload("unrelated.module", 3, new JObject { ["kept"] = true }, "Unrelated");

            MxLevelAuthoringDocument written =
                CharacterTestGenesisValidationAuthoringAdapter.Upsert(envelope, document);
            string json = MxLevelAuthoringJson.SaveToJson(written);
            MxLevelAuthoringDocument reopened = MxLevelAuthoringJson.LoadFromJson(json);
            CharacterTestGenesisValidationAuthoringDocument restored =
                CharacterTestGenesisValidationAuthoringAdapter.Extract(reopened);

            Assert.That((int)restored.Stations[0].Scenario.Payload["futurePayload"], Is.EqualTo(21));
            Assert.That(reopened.TryGetModule("unrelated.module", out MxLevelAuthoringModuleDocument unrelated), Is.True);
            Assert.That((bool)unrelated.Payload["kept"], Is.True);
        }

        [Test]
        public void DuplicateIdsMissingGroupAndMissingOwnerReferenceFailClosed()
        {
            CharacterTestGenesisValidationAuthoringDocument document = CreateValidDocument();
            document.Groups.Add(new CharacterTestGenesisValidationGroup
            {
                StableId = "movables",
                DisplayName = "Duplicate",
                SortOrder = 20
            });
            document.Stations.Add(CreatePotencyStation("station", "missing-group", 20));
            document.Stations[0].Bindings.TargetMovableObject.InstanceStableId = "missing-movable";

            CharacterTestGenesisValidationAuthoringReport report =
                CharacterTestGenesisValidationAuthoringValidator.Validate(document, CreateOwners());

            AssertIssue(report, "DuplicateGroupStableId");
            AssertIssue(report, "DuplicateStationStableId");
            AssertIssue(report, "MissingStationGroup");
            AssertIssue(report, "MovableObjectRefNotFound");
        }

        [Test]
        public void SingleStaticRejectsBatchKindDefinitionMismatchAndExpandedCollision()
        {
            LevelWorldObjectAuthoringDocument world = CreateWorldObjects();
            CharacterTestGenesisValidationOwnerCatalog normal =
                CharacterTestGenesisValidationOwnerCatalog.Build(null, world);
            var reference = new CharacterTestGenesisValidationStaticColliderRef
            {
                SourceKind = CharacterTestGenesisValidationStaticSourceKind.SingleStatic,
                BodyId = 61710,
                ColliderId = 1,
                ExpectedDefinitionId = 1011
            };

            Assert.That(normal.TryResolveSingleStatic(reference, out _), Is.True);

            reference.SourceKind = CharacterTestGenesisValidationStaticSourceKind.Unknown;
            Assert.That(normal.TryResolveSingleStatic(reference, out string batchCode), Is.False);
            Assert.That(batchCode, Is.EqualTo("UnsupportedStaticSourceKind"));

            reference.SourceKind = CharacterTestGenesisValidationStaticSourceKind.SingleStatic;
            reference.ExpectedDefinitionId = 9999;
            Assert.That(normal.TryResolveSingleStatic(reference, out string definitionCode), Is.False);
            Assert.That(definitionCode, Is.EqualTo("StaticColliderDefinitionMismatch"));

            reference.ExpectedDefinitionId = 1011;
            var expanded = new List<CharacterTestWorldObjectSceneSource.StaticColliderInstance>
            {
                new CharacterTestWorldObjectSceneSource.StaticColliderInstance(true, 1011, null, 61710, 1, CharacterTestWorldObjectInstanceScaleSource.SceneTransform),
                new CharacterTestWorldObjectSceneSource.StaticColliderInstance(true, 1011, null, 61710, 1, CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
            };
            CharacterTestGenesisValidationOwnerCatalog conflict =
                CharacterTestGenesisValidationOwnerCatalog.Build(null, world, expanded);
            Assert.That(conflict.TryResolveSingleStatic(reference, out string conflictCode), Is.False);
            Assert.That(conflictCode, Is.EqualTo("ExpandedStaticColliderKeyConflict"));
        }

        [Test]
        public void RepositoryFixturesResolveAsSingleStaticsAndBatchChildrenStayOutOfPickerSource()
        {
            string json = File.ReadAllText(CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath);
            MxLevelAuthoringDocument envelope = MxLevelAuthoringJson.LoadFromJson(json);
            Assert.That(
                CharacterTestWorldObjectAuthoringAdapter.TryExtractWorldObjectsDocument(
                    envelope,
                    out LevelWorldObjectAuthoringDocument world,
                    out _),
                Is.True);
            CharacterTestGenesisValidationOwnerCatalog owners =
                CharacterTestGenesisValidationOwnerCatalog.Build(null, world);

            AssertFixture(owners, 61710, 1011);
            AssertFixture(owners, 61711, 1012);
            AssertFixture(owners, 61712, 1013);
            AssertFixture(owners, 10001, 1001);
            Assert.That(world.StaticColliderBatches.Count, Is.GreaterThan(0));
        }

        [Test]
        public void ScenePlanAndStationTemplateContractsFailClosedWithoutRequiringRuntime()
        {
            Assert.That(
                CharacterTestGenesisValidationStationTemplateRegistry.All.Count,
                Is.EqualTo(5));
            CharacterTestGenesisValidationAuthoringDocument document = CreateValidDocument();
            CharacterTestGenesisValidationZone second =
                document.ScenePlan.Zones[0].Normalize();
            second.StableId = "zone.second";
            second.SortOrder = 20;
            document.ScenePlan.Zones.Add(second);

            CharacterTestGenesisValidationAuthoringReport disconnected =
                CharacterTestGenesisValidationAuthoringValidator.Validate(
                    document,
                    CreateOwners());
            AssertIssue(disconnected, "DisconnectedScenePlan");

            document.ScenePlan.Routes.Add(new CharacterTestGenesisValidationRoute
            {
                StableId = "route.valid-to-second",
                FromZoneId = "zone.valid",
                ToZoneId = "zone.second",
                WidthMillimeters = 1800
            });
            document.ScenePlan.Routes.Add(new CharacterTestGenesisValidationRoute
            {
                StableId = "route.duplicate",
                FromZoneId = "zone.second",
                ToZoneId = "zone.valid",
                WidthMillimeters = 1800
            });
            document.Stations[0].Scenario.TypeId =
                CharacterTestGenesisValidationAuthoringScenarioRegistry.BoundaryBodyResponse;

            CharacterTestGenesisValidationAuthoringReport invalid =
                CharacterTestGenesisValidationAuthoringValidator.Validate(
                    document,
                    CreateOwners());
            AssertIssue(invalid, "DuplicateZoneRoute");
            AssertIssue(invalid, "StationTemplateScenarioMismatch");
        }

        [Test]
        public void ResolvedLayoutValidatorDetectsOverlapAndHashesQuantizedSpatialInputs()
        {
            var root = new GameObject("LayoutValidatorRoot");
            try
            {
                Transform bounds = CreateChild(root.transform, "Bounds", Vector3.zero, new Vector3(30f, 1f, 20f));
                Transform footprintA = CreateChild(root.transform, "FootprintA", Vector3.zero, new Vector3(10f, 1f, 10f));
                Transform footprintB = CreateChild(root.transform, "FootprintB", new Vector3(5f, 0f, 0f), new Vector3(10f, 1f, 10f));
                Transform origin = CreateChild(root.transform, "Origin", Vector3.zero, Vector3.one);
                Transform entryA = CreateChild(root.transform, "EntryA", new Vector3(-4f, 0f, 0f), Vector3.one);
                Transform exitA = CreateChild(root.transform, "ExitA", new Vector3(4f, 0f, 0f), Vector3.one);
                Transform entryB = CreateChild(root.transform, "EntryB", new Vector3(1f, 0f, 0f), Vector3.one);
                Transform exitB = CreateChild(root.transform, "ExitB", new Vector3(9f, 0f, 0f), Vector3.one);
                Transform waypoint = CreateChild(root.transform, "RouteWaypoint", new Vector3(2f, 0f, 2f), Vector3.one);
                LevelWorldObjectAuthoringDocument world = new LevelWorldObjectAuthoringDocument
                {
                    Regions = new List<LevelWorldObjectRegionDefinition>
                    {
                        CreateRegionWithTransform("region.bounds", root.transform, bounds),
                        CreateRegionWithTransform("region.a", root.transform, footprintA),
                        CreateRegionWithTransform("region.b", root.transform, footprintB)
                    },
                    Points = new List<LevelWorldObjectPointDefinition>
                    {
                        CreatePointWithTransform("point.origin", root.transform, origin),
                        CreatePointWithTransform("point.entry.a", root.transform, entryA),
                        CreatePointWithTransform("point.exit.a", root.transform, exitA),
                        CreatePointWithTransform("point.entry.b", root.transform, entryB),
                        CreatePointWithTransform("point.exit.b", root.transform, exitB),
                        CreatePointWithTransform("point.route-waypoint", root.transform, waypoint)
                    }
                };
                var document = new CharacterTestGenesisValidationAuthoringDocument
                {
                    ScenePlan = new CharacterTestGenesisValidationScenePlan
                    {
                        SceneBounds = new CharacterTestGenesisValidationRegionRef { StableId = "region.bounds" },
                        OriginPoint = new CharacterTestGenesisValidationPointRef { StableId = "point.origin" },
                        Zones = new List<CharacterTestGenesisValidationZone>
                        {
                            CreateZone("zone.a", "region.a", "point.entry.a", "point.exit.a", 10),
                            CreateZone("zone.b", "region.b", "point.entry.b", "point.exit.b", 20)
                        },
                        Routes = new List<CharacterTestGenesisValidationRoute>
                        {
                            new CharacterTestGenesisValidationRoute
                            {
                                StableId = "route.a-b",
                                FromZoneId = "zone.a",
                                ToZoneId = "zone.b",
                                WidthMillimeters = 1800,
                                Waypoints = new List<CharacterTestGenesisValidationPointRef>
                                {
                                    new CharacterTestGenesisValidationPointRef
                                    {
                                        StableId = "point.route-waypoint"
                                    }
                                }
                            }
                        }
                    },
                    Groups = new List<CharacterTestGenesisValidationGroup>
                    {
                        new CharacterTestGenesisValidationGroup
                        {
                            StableId = "layout",
                            DisplayName = "Layout",
                            SortOrder = 10
                        }
                    }
                };

                IReadOnlyList<CharacterTestGenesisValidationAuthoringIssue> first =
                    CharacterTestGenesisValidationEditorLayoutValidator.Validate(
                        document.Normalize(),
                        world.Normalize(),
                        root.transform,
                        out string firstHash);
                AssertLayoutIssue(first, "ZoneFootprintsOverlap");
                Assert.That(firstHash, Is.Not.Empty);

                waypoint.position += new Vector3(0.001f, 0f, 0f);
                CharacterTestGenesisValidationEditorLayoutValidator.Validate(
                    document.Normalize(),
                    world.Normalize(),
                    root.transform,
                    out string secondHash);
                Assert.That(secondHash, Is.Not.EqualTo(firstHash));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator FoundationScaffoldUsesOwnerDocumentsAndCollapsesToOneUndo()
        {
            string assetPath = "Assets/Temp/GenesisValidationScaffold_"
                + Guid.NewGuid().ToString("N") + "/level.json";
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject managerObject = null;
            try
            {
                string sourcePath = Path.GetFullPath(
                    CharacterTestWorldObjectAuthoringAdapter.DefaultDocumentPath);
                MxLevelAuthoringDocument envelope = MxLevelAuthoringJson.LoadFromJson(
                    File.ReadAllText(sourcePath));
                envelope = CharacterTestGenesisValidationAuthoringAdapter.Upsert(
                    envelope,
                    CharacterTestGenesisValidationAuthoringAdapter.CreateDefaultDocument());
                string fullPath = Path.GetFullPath(assetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, MxLevelAuthoringJson.SaveToJson(envelope));
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                managerObject = new GameObject("GenesisScaffoldManager");
                managerObject.AddComponent<GameManager>();
                var worldModule = new CharacterTestWorldObjectLevelEditorModule();
                var genesisModule = new CharacterTestGenesisValidationLevelEditorModule(worldModule);
                genesisModule.SetDocumentPathForTests(assetPath);
                worldModule.OnEnable();
                genesisModule.OnEnable();

                genesisModule.CreateFoundationSceneScaffoldForTests();
                yield return null;

                Assert.That(genesisModule.DocumentForTests.ScenePlan.Zones.Count, Is.EqualTo(3), genesisModule.Status);
                Assert.That(genesisModule.DocumentForTests.ScenePlan.Routes.Count, Is.EqualTo(2));
                Assert.That(genesisModule.DocumentForTests.Stations.Count, Is.EqualTo(3));
                Assert.That(managerObject.transform.Find("GenesisValidationLayout"), Is.Not.Null);
                LevelWorldObjectAuthoringDocument world =
                    worldModule.GetWorldObjectDocumentSnapshotForComposition();
                Assert.That(
                    world.Points.Exists(point => point.StableId == "genesis.surface.approach-start"),
                    Is.True);
                Assert.That(
                    world.Regions.Exists(region => region.StableId == "genesis.surface.footprint"),
                    Is.True);
                Assert.That(
                    genesisModule.DocumentForTests.Stations[0].Bindings.SampleStaticColliders.Count,
                    Is.EqualTo(3));

                Undo.PerformUndo();
                yield return null;
                Assert.That(managerObject.transform.Find("GenesisValidationLayout"), Is.Null);
                Assert.That(genesisModule.DocumentForTests.Stations.Count, Is.EqualTo(0));
            }
            finally
            {
                if (managerObject != null)
                    Object.DestroyImmediate(managerObject);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                DeleteTempAssetDirectory(assetPath);
            }
        }

        [Test]
        public void ReadOnlyEditorProjectionDoesNotCreateDirtyState()
        {
            var module = new CharacterTestGenesisValidationLevelEditorModule();
            module.OnEnable();

            module.BuildTree(null);
            module.BuildRows(null, "document");
            module.BuildDiagnostics(null, new VisualElement());

            Assert.That(module.IsDirty, Is.False);
            Assert.That(module.IsManagedSceneDirty, Is.False);
        }

        [UnityTest]
        public IEnumerator EditorInspectorCrudPickerUndoAndSceneSelectionSmoke()
        {
            string assetPath = "Assets/Temp/GenesisValidationInspector_"
                + Guid.NewGuid().ToString("N") + "/level.json";
            GameObject managerObject = null;
            GenesisValidationInspectorTestWindow window = null;
            try
            {
                CharacterTestGenesisValidationAuthoringDocument document =
                    CreateSurfaceEditorDocument();
                LevelWorldObjectAuthoringDocument world = CreateWorldObjects();
                world.SingleStaticColliders[0].TransformRef =
                    LevelTransformReference.FromPath("Static");
                WriteTempEnvelope(assetPath, document, world);

                managerObject = new GameObject("GenesisValidationEditorSmoke");
                managerObject.AddComponent<GameManager>();
                var staticObject = new GameObject("Static");
                staticObject.transform.SetParent(managerObject.transform, false);

                var module = new CharacterTestGenesisValidationLevelEditorModule();
                module.SetDocumentPathForTests(assetPath);
                module.OnEnable();
                IReadOnlyList<MxLevelEditorListRow> rows = module.BuildRows(null, "document");
                Assert.That(rows.Count, Is.EqualTo(1));

                var inspector = new VisualElement();
                module.BuildInspector(null, rows[0], inspector);
                window = ScriptableObject.CreateInstance<GenesisValidationInspectorTestWindow>();
                window.rootVisualElement.Add(inspector);
                window.position = new Rect(100f, 100f, 640f, 720f);
                window.Show();
                yield return null;
                Assert.That(
                    FindField<Toggle>(inspector, "保持比较顺序"),
                    Is.Not.Null,
                    "Scenario-specific descriptor metadata was not projected.");

                DropdownField firstSignal = null;
                List<DropdownField> dropdowns = inspector.Query<DropdownField>().ToList();
                for (int i = 0; i < dropdowns.Count; i++)
                {
                    if (string.Equals(dropdowns[i].value, "contact-state", StringComparison.Ordinal))
                    {
                        firstSignal = dropdowns[i];
                        break;
                    }
                }
                Assert.That(firstSignal, Is.Not.Null);
                firstSignal.value = "surface-comparison";
                yield return null;
                Assert.That(module.DocumentForTests.Stations[0].Observation.RequiredSignalIds,
                    Is.EqualTo(new[] { "surface-comparison", "motion-response" }));

                rows = module.BuildRows(null, "document");
                inspector = new VisualElement();
                module.BuildInspector(null, rows[0], inspector);
                window.rootVisualElement.Clear();
                window.rootVisualElement.Add(inspector);
                yield return null;

                TextField firstComparison = null;
                List<TextField> textFields = inspector.Query<TextField>().ToList();
                for (int i = 0; i < textFields.Count; i++)
                {
                    if (string.Equals(textFields[i].value, "hard", StringComparison.Ordinal))
                    {
                        firstComparison = textFields[i];
                        break;
                    }
                }
                Assert.That(firstComparison, Is.Not.Null);
                firstComparison.value = "slick";
                yield return null;
                Assert.That(
                    module.DocumentForTests.Stations[0].Scenario.Payload["ComparisonOrder"][1].Value<string>(),
                    Is.EqualTo("elastic"));
                Undo.PerformUndo();
                yield return null;
                Assert.That(
                    module.DocumentForTests.Stations[0].Scenario.Payload["ComparisonOrder"][0].Value<string>(),
                    Is.EqualTo("hard"));

                DropdownField staticPicker = FindField<DropdownField>(inspector, "目标静态碰撞体");
                Assert.That(staticPicker, Is.Not.Null);
                Assert.That(staticPicker.choices, Has.Some.Contains("61710:1"));

                Assert.That(module.TryResolveRowObject(null, rows[0], out Object target), Is.True);
                Assert.That(target, Is.EqualTo(staticObject));
                Assert.That(
                    module.TryResolveSelection(null, staticObject, out MxLevelEditorSelectionTarget selection),
                    Is.True);
                Assert.That(selection.RowKey, Is.EqualTo("station:surface"));

                int initialCount = module.DocumentForTests.Stations.Count;
                module.DuplicateSelectedStationForTests();
                Assert.That(module.DocumentForTests.Stations.Count, Is.EqualTo(initialCount + 1));
                module.MoveSelectedStationForTests(-1);
                module.DeleteSelectedStationForTests();
                Assert.That(module.DocumentForTests.Stations.Count, Is.EqualTo(initialCount));
                module.AddStationForTests();
                Assert.That(module.DocumentForTests.Stations.Count, Is.EqualTo(initialCount + 1));

                var toolbar = new VisualElement();
                module.BuildModuleToolbar(null, toolbar);
                Assert.That(toolbar.Query<Button>().ToList().Count, Is.EqualTo(7));
            }
            finally
            {
                if (window != null)
                    window.Close();
                if (managerObject != null)
                    Object.DestroyImmediate(managerObject);
                DeleteTempAssetDirectory(assetPath);
            }
        }

        [Test]
        public void CoordinatedSaveWritesAndReopensAuthoringInGeneratedEditorScene()
        {
            string token = Guid.NewGuid().ToString("N");
            string directory = "Assets/Temp/GenesisValidationSave_" + token;
            string assetPath = directory + "/level.json";
            string scenePath = directory + "/save-smoke.unity";
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var managerObject = new GameObject("GenesisValidationSaveSmoke");
                managerObject.AddComponent<GameManager>();
                var movable = new GameObject("Movable");
                movable.transform.SetParent(managerObject.transform, false);

                LevelWorldObjectAuthoringDocument world = CreateWorldObjects();
                world.MovableInstances[0].TransformRef = LevelTransformReference.FromPath("Movable");
                WriteTempEnvelope(assetPath, CreateValidDocument(), world);
                Assert.That(EditorSceneManager.SaveScene(
                    SceneManager.GetActiveScene(),
                    scenePath), Is.True);

                var module = new CharacterTestGenesisValidationLevelEditorModule();
                module.SetDocumentPathForTests(assetPath);
                module.OnEnable();
                module.MutateForTests(
                    "save smoke",
                    document => document.Stations[0].Notes = "saved-and-reopened");
                module.Save();

                Assert.That(module.IsDirty, Is.False, module.Status);
                var reopened = new CharacterTestGenesisValidationLevelEditorModule();
                reopened.SetDocumentPathForTests(assetPath);
                reopened.OnEnable();
                Assert.That(
                    reopened.DocumentForTests.Stations[0].Notes,
                    Is.EqualTo("saved-and-reopened"));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                DeleteTempAssetDirectory(assetPath);
            }
        }

        [Test]
        public void SaveCompletionDistinguishesPreCommitFailureFromTrueThenFalseTailFailure()
        {
            var module = new CharacterTestGenesisValidationLevelEditorModule();
            module.OnEnable();
            FieldInfo stateField = typeof(CharacterTestGenesisValidationLevelEditorModule)
                .GetField("_saveState", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo dirtyField = typeof(CharacterTestGenesisValidationLevelEditorModule)
                .GetField("_dirty", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo activeGenerationField = typeof(CharacterTestGenesisValidationLevelEditorModule)
                .GetField("_activeSaveGeneration", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(stateField, Is.Not.Null);
            Assert.That(dirtyField, Is.Not.Null);
            Assert.That(activeGenerationField, Is.Not.Null);

            dirtyField.SetValue(module, true);
            ((IMxLevelEditorSaveParticipant)module).CompleteCoordinatedSave(false, "coordinator failed before prepare");
            Assert.That(module.SaveStateForTests, Is.EqualTo("Idle"));
            Assert.That(module.IsDirty, Is.True);

            stateField.SetValue(module, Enum.Parse(stateField.FieldType, "Written"));
            activeGenerationField.SetValue(module, 1);
            ((IMxLevelEditorSaveParticipant)module).CompleteCoordinatedSave(true, "committed");
            Assert.That(module.SaveStateForTests, Is.EqualTo("Committed"));
            Assert.That(module.IsDirty, Is.False);

            ((IMxLevelEditorSaveParticipant)module).CompleteCoordinatedSave(false, "tail failed");
            Assert.That(module.SaveStateForTests, Is.EqualTo("TailFailed"));
            Assert.That(module.IsDirty, Is.False);

            ((IMxLevelEditorSaveParticipant)module).CompleteCoordinatedSave(false, "duplicate");
            Assert.That(module.SaveStateForTests, Is.EqualTo("TailFailed"));

            stateField.SetValue(module, Enum.Parse(stateField.FieldType, "Written"));
            activeGenerationField.SetValue(module, 2);
            ((IMxLevelEditorSaveParticipant)module).CompleteCoordinatedSave(true, "second commit");
            module.MutateForTests(
                "edit after save",
                document => document.Groups[0].DisplayName = "unsaved");
            Assert.That(module.SaveStateForTests, Is.EqualTo("Idle"));
            Assert.That(module.IsDirty, Is.True);

            ((IMxLevelEditorSaveParticipant)module).CompleteCoordinatedSave(
                false,
                "next coordination failed before prepare");
            Assert.That(module.SaveStateForTests, Is.EqualTo("Idle"));
            Assert.That(module.IsDirty, Is.True);
        }

        [Test]
        public void FixtureReadinessDistinguishesUnappliedAppliedAndUnresolvedOwnerState()
        {
            var rootObject = new GameObject("GenesisFixtureReadinessRoot");
            var movableObject = new GameObject("Movable");
            var staticObject = new GameObject("Static");
            movableObject.transform.SetParent(rootObject.transform, false);
            staticObject.transform.SetParent(rootObject.transform, false);
            try
            {
                CharacterTestGenesisValidationAuthoringDocument document = CreateValidDocument();
                document.Stations[0].Bindings.TargetStaticCollider =
                    new CharacterTestGenesisValidationStaticColliderRef
                    {
                        SourceKind = CharacterTestGenesisValidationStaticSourceKind.SingleStatic,
                        BodyId = 61710,
                        ColliderId = 1,
                        ExpectedDefinitionId = 1011
                    };
                LevelWorldObjectAuthoringDocument world = CreateWorldObjects();
                world.MovableInstances[0].TransformRef = LevelTransformReference.FromPath("Movable");
                world.SingleStaticColliders[0].TransformRef = LevelTransformReference.FromPath("Static");
                var cache = new CharacterTestWorldObjectSceneCache();

                CharacterTestGenesisValidationFixtureReadinessReport unapplied =
                    CharacterTestGenesisValidationFixtureReadiness.Evaluate(
                        document,
                        world,
                        cache,
                        rootObject.transform);

                Assert.That(unapplied.AppliedCount, Is.Zero);
                Assert.That(unapplied.UnappliedCount, Is.EqualTo(2));
                Assert.That(unapplied.UnresolvedCount, Is.Zero);
                Assert.That(unapplied.BlocksPlay, Is.True);

                cache.Configure(
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.MovableInstance(
                            true,
                            world.MovableInstances[0].DefinitionId,
                            world.MovableInstances[0].NumericInstanceId,
                            world.MovableInstances[0].InstanceStableId,
                            movableObject.transform,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
                    },
                    new[]
                    {
                        new CharacterTestWorldObjectSceneSource.StaticColliderInstance(
                            true,
                            1011,
                            staticObject.transform,
                            61710,
                            1,
                            CharacterTestWorldObjectInstanceScaleSource.SceneTransform)
                    });

                CharacterTestGenesisValidationFixtureReadinessReport applied =
                    CharacterTestGenesisValidationFixtureReadiness.Evaluate(
                        document,
                        world,
                        cache,
                        rootObject.transform);

                Assert.That(applied.AppliedCount, Is.EqualTo(2));
                Assert.That(applied.BlocksPlay, Is.False);
                Assert.That(CharacterTestGenesisValidationPlayModeGuard.TryBuildBlockingMessage(
                    document,
                    world,
                    cache,
                    rootObject.transform,
                    out _,
                    out _), Is.False);

                world.SingleStaticColliders[0].TransformRef =
                    LevelTransformReference.FromPath("MissingStatic");
                CharacterTestGenesisValidationFixtureReadinessReport unresolved =
                    CharacterTestGenesisValidationFixtureReadiness.Evaluate(
                        document,
                        world,
                        cache,
                        rootObject.transform);

                Assert.That(unresolved.AppliedCount, Is.EqualTo(1));
                Assert.That(unresolved.UnresolvedCount, Is.EqualTo(1));
                Assert.That(unresolved.BlocksPlay, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PlayGuardExplainsUnappliedFixtureWithoutMutatingSceneOrCache()
        {
            var rootObject = new GameObject("GenesisFixturePlayGuardRoot");
            var movableObject = new GameObject("Movable");
            movableObject.transform.SetParent(rootObject.transform, false);
            try
            {
                CharacterTestGenesisValidationAuthoringDocument document = CreateValidDocument();
                LevelWorldObjectAuthoringDocument world = CreateWorldObjects();
                world.MovableInstances[0].TransformRef = LevelTransformReference.FromPath("Movable");
                var cache = new CharacterTestWorldObjectSceneCache();

                bool blocked = CharacterTestGenesisValidationPlayModeGuard.TryBuildBlockingMessage(
                    document,
                    world,
                    cache,
                    rootObject.transform,
                    out string message,
                    out Transform firstProblem);

                Assert.That(blocked, Is.True);
                Assert.That(message, Does.Contain("已阻止进入 Play"));
                Assert.That(message, Does.Contain("1 个未应用夹具"));
                Assert.That(firstProblem, Is.SameAs(movableObject.transform));
                Assert.That(cache.MovableInstanceCount, Is.Zero);
                Assert.That(cache.StaticColliderInstanceCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PlayGuardOnlyInspectsTheSceneOwnedByTheAuthoringEnvelope()
        {
            var envelope = new MxLevelAuthoringDocument
            {
                ScenePath = "Assets/Scenes/CharacterTestScene.unity"
            };

            Assert.That(
                CharacterTestGenesisValidationPlayModeGuard.OwnsScene(
                    envelope,
                    "Assets/Scenes/CharacterTestScene.unity"),
                Is.True);
            Assert.That(
                CharacterTestGenesisValidationPlayModeGuard.OwnsScene(
                    envelope,
                    "Assets\\Scenes\\CharacterTestScene.unity"),
                Is.True);
            Assert.That(
                CharacterTestGenesisValidationPlayModeGuard.OwnsScene(
                    envelope,
                    "Assets/Scenes/CharacterTestRuntimeAiLabScene.unity"),
                Is.False,
                "Genesis fixtures from CharacterTestScene must not block an unrelated Runtime AI Lab scene.");
        }

        private static CharacterTestGenesisValidationAuthoringDocument CreateValidDocument()
        {
            var document = new CharacterTestGenesisValidationAuthoringDocument
            {
                ScenePlan = CreateValidScenePlan("movables"),
                Groups = new List<CharacterTestGenesisValidationGroup>
                {
                    new CharacterTestGenesisValidationGroup
                    {
                        StableId = "movables",
                        DisplayName = "Movables",
                        SortOrder = 10
                    }
                },
                Stations = new List<CharacterTestGenesisValidationStation>
                {
                    CreatePotencyStation("station", "movables", 10)
                }
            };
            return document;
        }

        private static CharacterTestGenesisValidationAuthoringDocument CreateSurfaceEditorDocument()
        {
            ICharacterTestGenesisValidationAuthoringScenarioDescriptor descriptor =
                CharacterTestGenesisValidationAuthoringScenarioRegistry.All[0];
            CharacterTestGenesisValidationScenario scenario = descriptor.CreateDefaultScenario();
            scenario.Payload["ComparisonOrder"] = new JArray("hard", "elastic");
            return new CharacterTestGenesisValidationAuthoringDocument
            {
                Groups = new List<CharacterTestGenesisValidationGroup>
                {
                    new CharacterTestGenesisValidationGroup
                    {
                        StableId = "surfaces",
                        DisplayName = "Surfaces",
                        SortOrder = 10
                    }
                },
                Stations = new List<CharacterTestGenesisValidationStation>
                {
                    new CharacterTestGenesisValidationStation
                    {
                        StableId = "surface",
                        DisplayName = "Surface",
                        GroupId = "surfaces",
                        SortOrder = 10,
                        Intent = "Editor smoke",
                        Bindings = new CharacterTestGenesisValidationBindings
                        {
                            SampleStaticColliders = new List<CharacterTestGenesisValidationStaticColliderRef>
                            {
                                new CharacterTestGenesisValidationStaticColliderRef
                                {
                                    SourceKind = CharacterTestGenesisValidationStaticSourceKind.SingleStatic,
                                    BodyId = 61710,
                                    ColliderId = 1,
                                    ExpectedDefinitionId = 1011
                                }
                            }
                        },
                        Scenario = scenario,
                        Observation = new CharacterTestGenesisValidationObservation
                        {
                            BodyPrompt = "Observe contact.",
                            RequiredSignalIds = new List<string>
                            {
                                "contact-state",
                                "motion-response"
                            }
                        }
                    }
                }
            };
        }

        private static void WriteTempEnvelope(
            string assetPath,
            CharacterTestGenesisValidationAuthoringDocument document,
            LevelWorldObjectAuthoringDocument world)
        {
            string fullPath = Path.GetFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            MxLevelAuthoringDocument envelope =
                CharacterTestWorldObjectAuthoringAdapter.UpsertWorldObjectsModule(
                    new MxLevelAuthoringDocument(),
                    world);
            envelope = CharacterTestGenesisValidationAuthoringAdapter.Upsert(envelope, document);
            File.WriteAllText(fullPath, MxLevelAuthoringJson.SaveToJson(envelope));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void DeleteTempAssetDirectory(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory))
                return;
            AssetDatabase.DeleteAsset(directory);
            if (Directory.Exists(directory) || File.Exists(directory + ".meta"))
            {
                FileUtil.DeleteFileOrDirectory(directory);
                FileUtil.DeleteFileOrDirectory(directory + ".meta");
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static T FindField<T>(VisualElement root, string label)
            where T : VisualElement
        {
            List<T> fields = root.Query<T>().ToList();
            for (int i = 0; i < fields.Count; i++)
            {
                string actual = string.Empty;
                if (fields[i] is Toggle toggle) actual = toggle.label;
                else if (fields[i] is DropdownField dropdown) actual = dropdown.label;
                else if (fields[i] is TextField text) actual = text.label;
                else if (fields[i] is IntegerField integer) actual = integer.label;
                if (string.Equals(actual, label, StringComparison.Ordinal))
                    return fields[i];
            }
            return null;
        }

        private static void AssertInspectorField(
            ICharacterTestGenesisValidationAuthoringScenarioDescriptor descriptor,
            string propertyName)
        {
            for (int i = 0; i < descriptor.InspectorFields.Count; i++)
            {
                if (string.Equals(
                        descriptor.InspectorFields[i].PropertyName,
                        propertyName,
                        StringComparison.Ordinal))
                    return;
            }
            Assert.Fail(descriptor.TypeId + " is missing inspector metadata for " + propertyName + ".");
        }

        private sealed class GenesisValidationInspectorTestWindow : EditorWindow
        {
        }

        private static CharacterTestGenesisValidationStation CreatePotencyStation(
            string id,
            string groupId,
            int order)
        {
            ICharacterTestGenesisValidationAuthoringScenarioDescriptor descriptor =
                CharacterTestGenesisValidationAuthoringScenarioRegistry.All[3];
            return new CharacterTestGenesisValidationStation
            {
                StableId = id,
                DisplayName = id,
                GroupId = groupId,
                SortOrder = order,
                Intent = "Observe potency",
                Bindings = new CharacterTestGenesisValidationBindings
                {
                    TargetMovableObject = new CharacterTestGenesisValidationMovableObjectRef
                    {
                        InstanceStableId = "movable.01"
                    }
                },
                SpatialProgram = new CharacterTestGenesisValidationStationSpatialProgram
                {
                    TemplateId = CharacterTestGenesisValidationStationTemplateIds.PotencyDebugBay,
                    ZoneId = "zone.valid",
                    FootprintRegion = new CharacterTestGenesisValidationRegionRef
                    {
                        StableId = "region.footprint"
                    },
                    EntryPoint = new CharacterTestGenesisValidationPointRef
                    {
                        StableId = "point.entry"
                    },
                    ObservationPoint = new CharacterTestGenesisValidationPointRef
                    {
                        StableId = "point.observation"
                    },
                    ExitPoint = new CharacterTestGenesisValidationPointRef
                    {
                        StableId = "point.exit"
                    },
                    MinimumApproachLengthMillimeters = 0,
                    MinimumClearanceMillimeters = 1500
                },
                Scenario = descriptor.CreateDefaultScenario(),
                Observation = new CharacterTestGenesisValidationObservation
                {
                    BodyPrompt = "Observe typed potency state.",
                    RequiredSignalIds = new List<string> { "potency-state" }
                }
            };
        }

        private static CharacterTestGenesisValidationScenePlan CreateValidScenePlan(
            string groupId)
        {
            return new CharacterTestGenesisValidationScenePlan
            {
                SceneBounds = new CharacterTestGenesisValidationRegionRef
                {
                    StableId = "region.bounds"
                },
                OriginPoint = new CharacterTestGenesisValidationPointRef
                {
                    StableId = "point.origin"
                },
                Zones = new List<CharacterTestGenesisValidationZone>
                {
                    new CharacterTestGenesisValidationZone
                    {
                        StableId = "zone.valid",
                        DisplayName = "Valid Zone",
                        GroupId = groupId,
                        SortOrder = 10,
                        LayoutPattern = CharacterTestGenesisValidationZoneLayoutPattern.DebugBay,
                        FootprintRegion = new CharacterTestGenesisValidationRegionRef
                        {
                            StableId = "region.footprint"
                        },
                        EntryPoint = new CharacterTestGenesisValidationPointRef
                        {
                            StableId = "point.entry"
                        },
                        ExitPoint = new CharacterTestGenesisValidationPointRef
                        {
                            StableId = "point.exit"
                        }
                    }
                }
            };
        }

        private static CharacterTestGenesisValidationOwnerCatalog CreateOwners()
        {
            return CharacterTestGenesisValidationOwnerCatalog.Build(
                null,
                CreateWorldObjects());
        }

        private static LevelWorldObjectAuthoringDocument CreateWorldObjects()
        {
            return new LevelWorldObjectAuthoringDocument
            {
                MovableInstances = new List<LevelWorldObjectInstanceDefinition>
                {
                    new LevelWorldObjectInstanceDefinition
                    {
                        Enabled = true,
                        Role = LevelWorldObjectRole.Movable,
                        DefinitionId = 2001,
                        NumericInstanceId = 1,
                        InstanceStableId = "movable.01"
                    }
                },
                SingleStaticColliders = new List<LevelWorldObjectInstanceDefinition>
                {
                    new LevelWorldObjectInstanceDefinition
                    {
                        Enabled = true,
                        Role = LevelWorldObjectRole.StaticCollider,
                        DefinitionId = 1011,
                        BodyId = 61710,
                        ColliderId = 1
                    }
                },
                Points = new List<LevelWorldObjectPointDefinition>
                {
                    CreatePoint("point.origin"),
                    CreatePoint("point.entry"),
                    CreatePoint("point.observation"),
                    CreatePoint("point.exit")
                },
                Regions = new List<LevelWorldObjectRegionDefinition>
                {
                    CreateRegion("region.bounds"),
                    CreateRegion("region.footprint")
                }
            };
        }

        private static LevelWorldObjectPointDefinition CreatePoint(string stableId)
        {
            return new LevelWorldObjectPointDefinition
            {
                Enabled = true,
                DefinitionId = CharacterTestWorldObjectDefinitionIds.PointValidationMarker,
                StableId = stableId,
                PointKind = LevelWorldObjectPointKind.Marker
            };
        }

        private static LevelWorldObjectRegionDefinition CreateRegion(string stableId)
        {
            return new LevelWorldObjectRegionDefinition
            {
                Enabled = true,
                DefinitionId = CharacterTestWorldObjectDefinitionIds.RegionTestCourse,
                StableId = stableId,
                RegionKind = LevelWorldObjectRegionKind.TestRegion
            };
        }

        private static Transform CreateChild(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.position = position;
            child.transform.localScale = scale;
            return child.transform;
        }

        private static LevelWorldObjectPointDefinition CreatePointWithTransform(
            string stableId,
            Transform root,
            Transform transform)
        {
            LevelWorldObjectPointDefinition result = CreatePoint(stableId);
            result.TransformRef = CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, transform);
            return result;
        }

        private static LevelWorldObjectRegionDefinition CreateRegionWithTransform(
            string stableId,
            Transform root,
            Transform transform)
        {
            LevelWorldObjectRegionDefinition result = CreateRegion(stableId);
            result.TransformRef = CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, transform);
            return result;
        }

        private static CharacterTestGenesisValidationZone CreateZone(
            string stableId,
            string regionId,
            string entryId,
            string exitId,
            int order)
        {
            return new CharacterTestGenesisValidationZone
            {
                StableId = stableId,
                DisplayName = stableId,
                GroupId = "layout",
                SortOrder = order,
                LayoutPattern = CharacterTestGenesisValidationZoneLayoutPattern.LinearApproach,
                FootprintRegion = new CharacterTestGenesisValidationRegionRef { StableId = regionId },
                EntryPoint = new CharacterTestGenesisValidationPointRef { StableId = entryId },
                ExitPoint = new CharacterTestGenesisValidationPointRef { StableId = exitId }
            };
        }

        private static void AssertLayoutIssue(
            IReadOnlyList<CharacterTestGenesisValidationAuthoringIssue> issues,
            string code)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (string.Equals(issues[i].Code, code, StringComparison.Ordinal))
                    return;
            }
            Assert.Fail("Expected layout issue " + code + ".");
        }

        private static void AssertFixture(
            CharacterTestGenesisValidationOwnerCatalog owners,
            int bodyId,
            int definitionId)
        {
            Assert.That(owners.TryResolveSingleStatic(
                new CharacterTestGenesisValidationStaticColliderRef
                {
                    SourceKind = CharacterTestGenesisValidationStaticSourceKind.SingleStatic,
                    BodyId = bodyId,
                    ColliderId = 1,
                    ExpectedDefinitionId = definitionId
                },
                out string code),
                Is.True,
                code);
        }

        private static void AssertIssue(
            CharacterTestGenesisValidationAuthoringReport report,
            string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
                if (string.Equals(report.Issues[i].Code, code, StringComparison.Ordinal)) return;
            Assert.Fail("Expected issue " + code + ". Actual: " + JoinIssues(report));
        }

        private static string JoinIssues(CharacterTestGenesisValidationAuthoringReport report)
        {
            var values = new List<string>();
            for (int i = 0; i < report.Issues.Count; i++)
                values.Add(report.Issues[i].Code + "@" + report.Issues[i].Path);
            return string.Join(", ", values);
        }
    }
}
