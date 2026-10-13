using System;
using System.Collections;
using System.IO;
using System.Reflection;
using MxFramework.Config.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Resources;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestDefinitionDraftUndoNativeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private CharacterTestWorldObjectLevelEditorModule _module;
        private Scene _originalScene;
        private Scene _testScene;
        private bool _originalDirty;
        private int _undoBoundary = -1;
        private CharacterTestWorldObjectDefinitionAuthoringDocument Draft =>
            (CharacterTestWorldObjectDefinitionAuthoringDocument)Field("_definitionDocument").GetValue(_module);
        private string Snapshot => CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(Draft);
        private static FieldInfo Field(string name) => typeof(CharacterTestWorldObjectLevelEditorModule).GetField(name, Private);
        private void Invoke(string name, params object[] args) =>
            typeof(CharacterTestWorldObjectLevelEditorModule).GetMethod(name, Private).Invoke(_module, args);

        [SetUp]
        public void SetUp()
        {
            _module = null;
            _testScene = default;
            _undoBoundary = -1;
            _originalScene = SceneManager.GetActiveScene();
            _originalDirty = _originalScene.isDirty;
            // Preview scenes do not require saving the native Runner's untitled scene.
            _testScene = EditorSceneManager.NewPreviewScene();
            Undo.IncrementCurrentGroup();
            _undoBoundary = Undo.GetCurrentGroup();
            _module = new CharacterTestWorldObjectLevelEditorModule();
            Field("_definitionDocument").SetValue(_module, CharacterTestWorldObjectDefinitionMigration.ExportBuiltInCatalog());
            Invoke("CompileDefinitions");
            Invoke("EnsureDefinitionUndoSession");
        }

        [TearDown]
        public void TearDown()
        {
            var session = _module == null ? null
                : (CharacterTestLevelEditorDocumentUndoSession)Field("_definitionUndoSession").GetValue(_module);
            session?.Retire();
            if (_undoBoundary >= 0)
                Undo.RevertAllDownToGroup(_undoBoundary);
            if (session != null)
            {
                var state = (UnityEngine.Object)typeof(CharacterTestLevelEditorDocumentUndoSession)
                    .GetField("_state", Private).GetValue(session);
                UnityEngine.Object.DestroyImmediate(state);
            }
            if (_originalScene.IsValid() && _originalScene.isLoaded)
                SceneManager.SetActiveScene(_originalScene);
            if (_testScene.IsValid() && _testScene.isLoaded)
                EditorSceneManager.ClosePreviewScene(_testScene);
            Assert.AreEqual(_originalDirty, _originalScene.isDirty, "User scene dirty state must be preserved.");
        }

        private void Mutate(Action<CharacterTestWorldObjectDefinitionAuthoringEntry> edit)
        {
            Invoke("MutateDefinition", "Definition draft regression", Draft.Definitions[0], edit);
            MoveOwnedMarkersToPreview();
        }

        private void MoveOwnedMarkersToPreview()
        {
            object session = Field("_definitionUndoSession").GetValue(_module);
            var records = (IEnumerable)typeof(CharacterTestLevelEditorDocumentUndoSession)
                .GetField("_transactions", Private).GetValue(session);
            foreach (object record in records)
            {
                var marker = (CharacterTestLevelEditorDocumentUndoMarker)record.GetType()
                    .GetField("Marker").GetValue(record);
                if (marker != null && marker.gameObject.scene != _testScene)
                    SceneManager.MoveGameObjectToScene(marker.gameObject, _testScene);
            }
        }

        private void AssertSnapshot(string expected, bool dirty)
        {
            Assert.AreEqual(expected, Snapshot);
            Assert.AreEqual(dirty, _module.IsDocumentDirty);
            var session = Field("_definitionUndoSession").GetValue(_module);
            var state = (CharacterTestLevelEditorDocumentUndoState)typeof(CharacterTestLevelEditorDocumentUndoSession)
                .GetField("_state", Private).GetValue(session);
            Assert.AreEqual(Snapshot, state.Snapshot, "Undo state and the real draft must stay identical.");
        }

        [UnityTest]
        public IEnumerator DefinitionUndoRedo_RestoresIncompleteNameAndDescriptionExactly()
        {
            var snapshots = new string[4];
            snapshots[0] = Snapshot;
            Mutate(d => d.DisplayName = string.Empty);
            snapshots[1] = Snapshot;
            Mutate(d => d.Description = "note after second edit");
            snapshots[2] = Snapshot;
            Mutate(d => d.DisplayName = "valid repaired name");
            snapshots[3] = Snapshot;
            for (int i = 2; i >= 0; i--)
            {
                Undo.PerformUndo();
                yield return null;
                MoveOwnedMarkersToPreview();
                AssertSnapshot(snapshots[i], dirty: i != 0);
                if (i > 0)
                {
                    Assert.That(CharacterTestWorldObjectDefinitionValidator.Validate(Draft).Issues,
                        Has.Some.Matches<CharacterTestWorldObjectDefinitionValidationIssue>(x => x.Code == "WOD_DISPLAY_NAME_MISSING"));
                    Assert.Throws<InvalidOperationException>(() => CharacterTestWorldObjectDefinitionArtifactsBuilder.Save(Draft));
                }
            }
            for (int i = 1; i <= 3; i++)
            {
                Undo.PerformRedo();
                yield return null;
                MoveOwnedMarkersToPreview();
                AssertSnapshot(snapshots[i], dirty: true);
            }
        }

        [UnityTest]
        public IEnumerator DefinitionUndoRedo_InvalidStableIdStillRestoresOtherFields()
        {
            Mutate(d => d.StableId = string.Empty);
            string beforeDescriptionEdit = Snapshot;
            Mutate(d => d.Description = "description while the stable ID is incomplete");
            string afterDescriptionEdit = Snapshot;
            Undo.PerformUndo();
            yield return null;
            MoveOwnedMarkersToPreview();
            AssertSnapshot(beforeDescriptionEdit, dirty: true);
            Assert.IsFalse(CharacterTestWorldObjectDefinitionCompiler.Compile(Draft).Success);
            Assert.Throws<InvalidOperationException>(() => CharacterTestWorldObjectDefinitionArtifactsBuilder.Save(Draft));
            Undo.PerformRedo();
            yield return null;
            MoveOwnedMarkersToPreview();
            AssertSnapshot(afterDescriptionEdit, dirty: true);
            Assert.AreEqual(string.Empty, Draft.Definitions[0].StableId);
        }

        [TestCase("malformed")]
        [TestCase("null")]
        [TestCase("format")]
        [TestCase("schema")]
        public void DefinitionSnapshot_RejectsMalformedOrForeignDataWithoutOverwritingDraft(string kind)
        {
            string before = Snapshot;
            var json = JObject.Parse(before);
            if (kind == "format") json["format"] = "foreign.definition.format";
            if (kind == "schema") json["schemaVersion"] = 999;
            string snapshot = kind == "malformed" ? "{" : kind == "null" ? "null" : json.ToString();
            var exception = Assert.Throws<TargetInvocationException>(() => Invoke("RestoreDefinitionSnapshot", snapshot));
            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
            AssertSnapshot(before, dirty: false);
        }

        [Test]
        public void DefinitionSnapshot_ValidDataRestoresAndCompiles()
        {
            var json = JObject.Parse(Snapshot);
            json["definitions"][0]["description"] = "valid history entry";
            Invoke("RestoreDefinitionSnapshot", json.ToString());
            Assert.AreEqual("valid history entry", Draft.Definitions[0].Description);
            Assert.IsTrue(CharacterTestWorldObjectDefinitionCompiler.Compile(Draft).Success);
        }

        [Test]
        public void DefinitionReload_RetainsInvalidDraftSourceHashAndSavedUndoBaseline()
        {
            string saved = Snapshot;
            Field("_definitionSourceHash").SetValue(_module, "original-source-generation");
            Mutate(d => d.DisplayName = string.Empty);
            string draft = Snapshot;
            var state = (JObject)typeof(CharacterTestWorldObjectLevelEditorModule)
                .GetMethod("CaptureDefinitionReloadState", Private).Invoke(_module, null);
            var restored = new CharacterTestWorldObjectLevelEditorModule();
            try
            {
                typeof(CharacterTestWorldObjectLevelEditorModule).GetMethod("RestoreDefinitionReloadState", Private)
                    .Invoke(restored, new object[] { state });
                var restoredDocument = (CharacterTestWorldObjectDefinitionAuthoringDocument)
                    Field("_definitionDocument").GetValue(restored);
                Assert.AreEqual(draft, CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(restoredDocument));
                Assert.IsTrue(restored.IsDocumentDirty);
                Assert.AreEqual("original-source-generation", Field("_definitionSourceHash").GetValue(restored));
                var undo = (CharacterTestLevelEditorDocumentUndoSession)Field("_definitionUndoSession").GetValue(restored);
                Assert.AreEqual(saved, undo.SavedBaseline);
                var failure = Assert.Throws<TargetInvocationException>(() =>
                    typeof(CharacterTestWorldObjectLevelEditorModule).GetMethod("RequireDefinitionSourceUnchanged", Private)
                        .Invoke(restored, null));
                Assert.IsInstanceOf<InvalidOperationException>(failure.InnerException);
                Assert.AreEqual(draft, CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(restoredDocument));
            }
            finally
            {
                var undo = (CharacterTestLevelEditorDocumentUndoSession)Field("_definitionUndoSession").GetValue(restored);
                if (undo != null)
                {
                    undo.Retire();
                    UnityEngine.Object.DestroyImmediate((UnityEngine.Object)typeof(CharacterTestLevelEditorDocumentUndoSession)
                        .GetField("_state", Private).GetValue(undo));
                }
            }
        }
    }

    public sealed class CharacterTestWorldObjectDefinitionAuthoringTests
    {
        [Test]
        public void BuiltInMigration_CompilesToFieldEquivalentCatalog()
        {
            CharacterTestWorldObjectDefinitionCatalog builtIn =
                CharacterTestWorldObjectDefinitionCatalog.CreateCharacterTestSceneCatalog();
            CharacterTestWorldObjectDefinitionAuthoringDocument authoring =
                CharacterTestWorldObjectDefinitionMigration.Export(builtIn);
            CharacterTestWorldObjectDefinitionCompileResult compiled =
                CharacterTestWorldObjectDefinitionCompiler.Compile(authoring);

            Assert.IsTrue(compiled.Success, JoinIssues(compiled.Validation));
            CollectionAssert.AreEqual(builtIn.Definitions, compiled.Catalog.Definitions);
            Assert.AreEqual(builtIn.Definitions.Count, compiled.Presentations.Bindings.Count);
            for (int i = 0; i < compiled.Presentations.Bindings.Count; i++)
            {
                Assert.AreEqual(
                    CharacterTestWorldObjectPresentationKind.PrimitiveAuthoring,
                    compiled.Presentations.Bindings[i].Kind);
                Assert.AreEqual(builtIn.Definitions[i].DefinitionId, compiled.Presentations.Bindings[i].DefinitionId);
            }
        }

        [Test]
        public void CanonicalJson_RoundTripAndRuntimeHashesAreStable()
        {
            CharacterTestWorldObjectDefinitionAuthoringDocument authoring =
                CharacterTestWorldObjectDefinitionMigration.ExportBuiltInCatalog();
            string canonical = CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(authoring);
            Assert.IsTrue(
                CharacterTestWorldObjectDefinitionJson.TryLoadAuthoring(
                    canonical,
                    out CharacterTestWorldObjectDefinitionAuthoringDocument roundTrip,
                    out CharacterTestWorldObjectDefinitionValidationReport report),
                JoinIssues(report));
            Assert.AreEqual(canonical, CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(roundTrip));
            Assert.AreEqual(
                CharacterTestWorldObjectDefinitionJson.ComputeAuthoringHash(authoring),
                CharacterTestWorldObjectDefinitionJson.ComputeAuthoringHash(roundTrip));

            CharacterTestWorldObjectDefinitionCompileResult compiled =
                CharacterTestWorldObjectDefinitionCompiler.Compile(roundTrip);
            roundTrip.Definitions[0].Description = "Editor search metadata";
            roundTrip.Definitions[0].Tags.Add("test-tag");
            CharacterTestWorldObjectDefinitionCompileResult metadataChanged =
                CharacterTestWorldObjectDefinitionCompiler.Compile(roundTrip);
            Assert.AreNotEqual(compiled.SourceHash, metadataChanged.SourceHash);
            Assert.AreEqual(compiled.ContentHash, metadataChanged.ContentHash);
            CollectionAssert.AreEqual(compiled.Catalog.Definitions, metadataChanged.Catalog.Definitions);

            string runtimeJson = CharacterTestWorldObjectDefinitionJson.SerializeRuntime(compiled.RuntimeDocument);
            Assert.IsTrue(
                CharacterTestWorldObjectDefinitionJson.TryLoadRuntime(
                    runtimeJson,
                    out CharacterTestWorldObjectDefinitionRuntimeDocument runtime,
                    out report),
                JoinIssues(report));
            Assert.AreEqual(compiled.SourceHash, runtime.SourceHash);
            Assert.AreEqual(compiled.ContentHash, runtime.ContentHash);
            Assert.AreEqual(runtimeJson, CharacterTestWorldObjectDefinitionJson.SerializeRuntime(runtime));

            string tampered = runtimeJson.Replace(
                "\"displayName\": \"Slick Light\"",
                "\"displayName\": \"Tampered\"");
            Assert.IsFalse(
                CharacterTestWorldObjectDefinitionJson.TryLoadRuntime(
                    tampered,
                    out _,
                    out report));
            AssertCode(report, "WOD_CONTENT_HASH_MISMATCH");
        }

        [Test]
        public void Validator_RejectsDuplicateInvalidFacetAndBadResourceKey()
        {
            CharacterTestWorldObjectDefinitionAuthoringDocument document =
                CharacterTestWorldObjectDefinitionMigration.ExportBuiltInCatalog();
            document.Definitions[1].DefinitionId = document.Definitions[0].DefinitionId;
            document.Definitions[2].StableId = document.Definitions[0].StableId;
            document.Definitions[3].Shape.HalfExtentXMillimeters = 0;
            document.Definitions[4].Material.Kind = "NotMaterial";
            document.Definitions[5].StaticCollider.Enabled = true;
            document.Definitions[6].Presentation.Kind =
                CharacterTestWorldObjectPresentationKind.PrefabResource.ToString();
            document.Definitions[6].Presentation.ResourceKey = new CharacterTestResourceKeyAuthoring
            {
                Id = "Bad Key",
                TypeId = ResourceTypeIds.Texture2D
            };

            CharacterTestWorldObjectDefinitionValidationReport report =
                CharacterTestWorldObjectDefinitionValidator.Validate(document);
            Assert.IsFalse(report.Success);
            AssertCode(report, "WOD_ID_DUPLICATE");
            AssertCode(report, "WOD_STABLE_ID_DUPLICATE");
            AssertCode(report, "WOD_SHAPE_EXTENTS_INVALID");
            AssertCode(report, "WOD_MATERIAL_KIND_INVALID");
            AssertCode(report, "WOD_FACET_MISMATCH");
            AssertCode(report, "WOD_RESOURCE_KEY_INVALID");
        }

        [Test]
        public void Validator_SpatialPointsDoNotCarrySpawnIdentity()
        {
            CharacterTestWorldObjectDefinitionAuthoringDocument document =
                CharacterTestWorldObjectDefinitionMigration.ExportBuiltInCatalog();
            CharacterTestWorldObjectDefinitionAuthoringEntry checkpoint = document.Definitions.Find(
                entry => entry.DefinitionId == CharacterTestWorldObjectDefinitionIds.PointValidationCheckpoint);

            Assert.NotNull(checkpoint);
            Assert.AreEqual(0, checkpoint.Point.CharacterId);
            Assert.AreEqual(0, checkpoint.Point.TeamId);
            Assert.AreEqual(
                CharacterTestCharacterControlOwner.None.ToString(),
                checkpoint.Point.ControlOwner);
            Assert.IsTrue(CharacterTestWorldObjectDefinitionValidator.Validate(document).Success);

            var normalizedMarker = new CharacterTestWorldObjectPointFacet(
                LevelWorldObjectPointKind.Marker,
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                2,
                CharacterTestCharacterControlOwner.LocalPlayer);
            Assert.AreEqual(0, normalizedMarker.CharacterId);
            Assert.AreEqual(0, normalizedMarker.TeamId);
            Assert.AreEqual(CharacterTestCharacterControlOwner.None, normalizedMarker.ControlOwner);

            checkpoint.Point.CharacterId = CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId;
            checkpoint.Point.TeamId = 1;
            CharacterTestWorldObjectDefinitionValidationReport report =
                CharacterTestWorldObjectDefinitionValidator.Validate(document);
            Assert.IsFalse(report.Success);
            AssertCode(report, "WOD_POINT_INVALID");
        }

        [Test]
        public void StrictJson_RejectsUnknownMember()
        {
            string json = CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(
                CharacterTestWorldObjectDefinitionMigration.ExportBuiltInCatalog(),
                indented: false);
            json = json.Insert(json.Length - 1, ",\"unknownP21Field\":true");
            Assert.IsFalse(
                CharacterTestWorldObjectDefinitionJson.TryLoadAuthoring(
                    json,
                    out _,
                    out CharacterTestWorldObjectDefinitionValidationReport report));
            AssertCode(report, "WOD_JSON_INVALID");
        }

        [Test]
        public void Loader_UsesValidRuntimeAndFallsBackWhenGeneratedIsStale()
        {
            string root = Path.Combine(Path.GetTempPath(), "mx-p21-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                CharacterTestWorldObjectDefinitionCompileResult compiled =
                    CharacterTestWorldObjectDefinitionCompiler.Compile(
                        CharacterTestWorldObjectDefinitionMigration.ExportBuiltInCatalog());
                string path = Path.Combine(root, "world.runtime.json");
                File.WriteAllText(
                    path,
                    CharacterTestWorldObjectDefinitionJson.SerializeRuntime(compiled.RuntimeDocument));
                CharacterTestWorldObjectDefinitionCatalogLoadResult loaded =
                    CharacterTestWorldObjectDefinitionCatalogLoader.LoadOrBuiltIn(
                        new[] { path },
                        new[] { CharacterTestWorldObjectDefinitionCatalogSourceKind.Generated },
                        compiled.SourceHash);
                Assert.IsFalse(loaded.UsedFallback);
                CollectionAssert.AreEqual(compiled.Catalog.Definitions, loaded.Catalog.Definitions);
                Assert.AreEqual(compiled.ContentHash, loaded.ContentHash);

                CharacterTestWorldObjectDefinitionCatalogLoadResult stale =
                    CharacterTestWorldObjectDefinitionCatalogLoader.LoadOrBuiltIn(
                        new[] { path },
                        new[] { CharacterTestWorldObjectDefinitionCatalogSourceKind.Generated },
                        "sha256:stale");
                Assert.IsTrue(stale.UsedFallback);
                AssertDiagnostic(stale, "WOD_RUNTIME_STALE");
                AssertDiagnostic(stale, "WOD_BUILTIN_FALLBACK");
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void OfficialAuthoringGeneratedAndPlayerMirror_CompileToSameCatalogAndHash()
        {
            string root = Directory.GetCurrentDirectory();
            string authoringPath = Path.Combine(
                root,
                CharacterTestWorldObjectDefinitionCatalogLoader.DefaultAuthoringPath);
            string generatedPath = Path.Combine(
                root,
                CharacterTestWorldObjectDefinitionCatalogLoader.DefaultGeneratedRuntimePath);
            string mirrorPath = Path.Combine(
                root,
                "Assets",
                "StreamingAssets",
                CharacterTestWorldObjectDefinitionCatalogLoader.DefaultPlayerMirrorRelativePath);
            Assert.IsTrue(CharacterTestWorldObjectDefinitionJson.TryLoadAuthoring(
                File.ReadAllText(authoringPath),
                out CharacterTestWorldObjectDefinitionAuthoringDocument authoring,
                out CharacterTestWorldObjectDefinitionValidationReport report), JoinIssues(report));
            CharacterTestWorldObjectDefinitionCompileResult compiled =
                CharacterTestWorldObjectDefinitionCompiler.Compile(authoring);
            Assert.IsTrue(compiled.Success, JoinIssues(compiled.Validation));

            CharacterTestWorldObjectDefinitionCatalogLoadResult generated =
                CharacterTestWorldObjectDefinitionCatalogLoader.LoadOrBuiltIn(
                    new[] { generatedPath },
                    new[] { CharacterTestWorldObjectDefinitionCatalogSourceKind.Generated },
                    compiled.SourceHash);
            CharacterTestWorldObjectDefinitionCatalogLoadResult mirror =
                CharacterTestWorldObjectDefinitionCatalogLoader.LoadOrBuiltIn(
                    new[] { mirrorPath },
                    new[] { CharacterTestWorldObjectDefinitionCatalogSourceKind.PlayerMirror },
                    compiled.SourceHash);
            Assert.IsFalse(generated.UsedFallback);
            Assert.IsFalse(mirror.UsedFallback);
            Assert.AreEqual(compiled.ContentHash, generated.ContentHash);
            Assert.AreEqual(generated.ContentHash, mirror.ContentHash);
            CollectionAssert.AreEqual(compiled.Catalog.Definitions, generated.Catalog.Definitions);
            CollectionAssert.AreEqual(generated.Catalog.Definitions, mirror.Catalog.Definitions);
        }

        private static void AssertCode(
            CharacterTestWorldObjectDefinitionValidationReport report,
            string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (string.Equals(report.Issues[i].Code, code, StringComparison.Ordinal))
                    return;
            }
            Assert.Fail("Missing validation code " + code + ". " + JoinIssues(report));
        }

        private static void AssertDiagnostic(
            CharacterTestWorldObjectDefinitionCatalogLoadResult result,
            string code)
        {
            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                if (string.Equals(result.Diagnostics[i].Code, code, StringComparison.Ordinal))
                    return;
            }
            Assert.Fail("Missing loader diagnostic " + code + ".");
        }

        private static string JoinIssues(CharacterTestWorldObjectDefinitionValidationReport report)
        {
            if (report == null)
                return "No validation report.";
            var lines = new string[report.Issues.Count];
            for (int i = 0; i < report.Issues.Count; i++)
            {
                lines[i] = report.Issues[i].Code
                    + " "
                    + report.Issues[i].Path
                    + ": "
                    + report.Issues[i].Message;
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
