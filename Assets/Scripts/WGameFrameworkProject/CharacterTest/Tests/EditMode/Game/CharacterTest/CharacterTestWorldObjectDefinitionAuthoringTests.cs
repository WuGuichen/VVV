using System;
using System.IO;
using MxFramework.Config.Levels;
using MxFramework.Game.CharacterTest;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
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
