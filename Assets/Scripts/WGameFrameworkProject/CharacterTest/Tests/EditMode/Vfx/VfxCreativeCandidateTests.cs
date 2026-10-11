using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests
{
    public sealed class VfxCreativeCandidateTests
    {
        private readonly List<string> _cleanupPaths = new List<string>();
        private readonly List<string> _cleanupDirectories = new List<string>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _cleanupPaths.Count - 1; i >= 0; i--)
            {
                string path = _cleanupPaths[i];
                if (AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path))
                    AssetDatabase.DeleteAsset(path);
            }
            _cleanupPaths.Clear();
            for (int i = _cleanupDirectories.Count - 1; i >= 0; i--)
            {
                if (Directory.Exists(_cleanupDirectories[i]))
                    Directory.Delete(_cleanupDirectories[i], true);
            }
            _cleanupDirectories.Clear();
            const string promotionTestRoot = "Assets/VFX/MxCandidatePromotionTests";
            if (AssetDatabase.IsValidFolder(promotionTestRoot)
                && Directory.Exists(promotionTestRoot)
                && Directory.GetFileSystemEntries(promotionTestRoot).Length == 0)
                AssetDatabase.DeleteAsset(promotionTestRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void SessionRoot_UsesImportablePhysicalFolder_AndRejectsTraversal()
        {
            Assert.That(VfxCandidateSandbox.TryGetSessionRoot("safe_session-1", out string root, out string error), Is.True, error);
            Assert.That(root, Is.EqualTo("Assets/VFX/MxCandidates/safe_session-1"));
            Assert.That(VfxCandidateProtocol.LogicalSandboxRoot, Is.EqualTo("Assets/VFX/.mx-candidates"));
            Assert.That(VfxCandidateSandbox.TryGetSessionRoot("../escape", out _, out _), Is.False);
        }

        [Test]
        public void FireballRoundZeroFactory_ProducesSixDirectionsForEveryLifecycleStage()
        {
            List<VfxCandidatePlan> plans = VfxCreativeCandidateMenu.CreateFireballRoundZeroPlans("factory_test");

            Assert.That(plans, Has.Count.EqualTo(18));
            Assert.That(plans.FindAll(value => value.PlanId.Contains(".charge.")), Has.Count.EqualTo(6));
            Assert.That(plans.FindAll(value => value.PlanId.Contains(".flight.")), Has.Count.EqualTo(6));
            Assert.That(plans.FindAll(value => value.PlanId.Contains(".impact.")), Has.Count.EqualTo(6));
            Assert.That(plans.TrueForAll(value => value.FixedSeed != 0u), Is.True);
            Assert.That(plans.TrueForAll(value => value.Provenance.Exists(entry => entry.Key == "authority")), Is.True);
        }

        [Test]
        public void ProceduralTextureSeedSalt_UsesStableFnvEncoding()
        {
            Assert.That(VfxDefaultSupportAssetProvider.ComputeStableTextureSalt("fireball_texture"), Is.EqualTo(0x8BDEA006u));
        }

        [Test]
        public void ProceduralSupportTextures_ImportAndRepeatDeterministically()
        {
            string sessionA = NewId("textures_a");
            string sessionB = NewId("textures_b");
            Assert.That(VfxCandidateSandbox.TryGetSessionRoot(sessionA, out string rootA, out string errorA), Is.True, errorA);
            Assert.That(VfxCandidateSandbox.TryGetSessionRoot(sessionB, out string rootB, out string errorB), Is.True, errorB);
            VfxCandidateSandbox.EnsureAssetFolder(rootA);
            VfxCandidateSandbox.EnsureAssetFolder(rootB);
            _cleanupPaths.Add(rootA);
            _cleanupPaths.Add(rootB);
            VfxCandidatePlan plan = CreateSupportTexturePlan(sessionA);
            var provider = new VfxDefaultSupportAssetProvider();

            Assert.That(provider.TryCreateAssets(plan, rootA, out VfxSupportAssetSet assetsA, out string providerErrorA), Is.True, providerErrorA);
            plan.SessionId = sessionB;
            Assert.That(provider.TryCreateAssets(plan, rootB, out VfxSupportAssetSet assetsB, out string providerErrorB), Is.True, providerErrorB);
            string[] ids = { "sdf", "flow", "radial", "flipbook" };
            for (int i = 0; i < ids.Length; i++)
            {
                Texture2D textureA = AssetDatabase.LoadAssetAtPath<Texture2D>(assetsA.TexturePaths[ids[i]]);
                Texture2D textureB = AssetDatabase.LoadAssetAtPath<Texture2D>(assetsB.TexturePaths[ids[i]]);
                Assert.That(textureA, Is.Not.Null, ids[i]);
                Assert.That(textureB, Is.Not.Null, ids[i]);
                CollectionAssert.AreEqual(textureA.GetPixels32(), textureB.GetPixels32(), ids[i]);
            }
        }

        [Test]
        public void GenerateParticleCandidate_WritesDraftReceiptAndFixedSeedPrefab()
        {
            string sessionId = NewId("generate");
            VfxCandidatePlan plan = VfxCreativeCandidateMenu.CreateFireballPlan(sessionId);
            VfxCandidateGenerationResult result = new VfxEditorCandidateGenerator().Generate(plan);
            _cleanupPaths.Add(result.SessionRoot);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Receipt.ReviewStatus, Is.EqualTo("Draft"));
            Assert.That(result.Receipt.RequestedSandboxRoot, Is.EqualTo(VfxCandidateProtocol.LogicalSandboxRoot));
            Assert.That(result.Receipt.ResolvedAssetRoot, Is.EqualTo(result.SessionRoot));
            Assert.That(result.Receipt.FixedSeed, Is.EqualTo(plan.FixedSeed));
            Assert.That(result.Receipt.PlanHash, Does.StartWith("sha256:"));
            Assert.That(VfxCandidateSandbox.ComputeAssetFileHash(result.ReceiptAssetPath), Does.StartWith("sha256:"));

            VfxCandidateAssetReceipt prefabReceipt = FindAsset(result.Receipt, "Prefab");
            Assert.That(prefabReceipt.Guid, Is.Not.Empty);
            Assert.That(prefabReceipt.ContentHash, Does.StartWith("sha256:"));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabReceipt.AssetPath);
            Assert.That(prefab, Is.Not.Null);
            ParticleSystem particle = prefab.GetComponent<ParticleSystem>();
            Assert.That(particle, Is.Not.Null);
            Assert.That(particle.useAutoRandomSeed, Is.False);
            Assert.That(particle.randomSeed, Is.EqualTo(plan.FixedSeed));
        }

        [Test]
        public void PreviewCapture_WritesDeterministicRebuildableEvidenceUnderTemp()
        {
            VfxCandidateGenerationResult generation = GenerateCandidate("preview");
            VfxCandidateAssetReceipt prefab = FindAsset(generation.Receipt, "Prefab");
            string outputDirectory = "Temp/MxFramework/VfxCandidates/" + generation.Receipt.SessionId;
            _cleanupDirectories.Add(outputDirectory);

            VfxPreviewCaptureResult result = new VfxDeterministicPreviewCapture().Capture(new VfxPreviewCaptureRequest
            {
                SessionId = generation.Receipt.SessionId,
                CandidateId = "candidate_a",
                CaptureId = "gameplay_single",
                PrefabAssetPath = prefab.AssetPath,
                FixedSeed = generation.Receipt.FixedSeed,
                SampleTimeSeconds = 0.35f,
                Width = 128,
                Height = 128
            });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.OutputPath, Is.EqualTo(
                outputDirectory + "/candidate_a/gameplay_single/Gameplay/DarkNeutral/single/preview.png"));
            Assert.That(File.Exists(result.OutputPath), Is.True);
            Assert.That(result.ContentHash, Does.StartWith("sha256:"));
            Assert.That(result.AlphaCoverage, Is.InRange(0f, 1f));
        }

        [Test]
        public void PreviewEvidencePaths_SeparateTimeSlicesAndRemainStableForRepeatedDimensions()
        {
            string sessionId = "preview_paths_" + Guid.NewGuid().ToString("N");
            string outputRoot = "Temp/MxFramework/VfxCandidates/" + sessionId;
            _cleanupDirectories.Add(outputRoot);
            string firstPath = VfxDeterministicPreviewCapture.BuildOutputPath(
                sessionId, "candidate_a", "gameplay", "Gameplay", "DarkNeutral", "build");
            string secondPath = VfxDeterministicPreviewCapture.BuildOutputPath(
                sessionId, "candidate_a", "gameplay", "Gameplay", "DarkNeutral", "impact");
            string repeatedPath = VfxDeterministicPreviewCapture.BuildOutputPath(
                sessionId, "candidate_a", "gameplay", "Gameplay", "DarkNeutral", "build");
            byte[] firstBytes = System.Text.Encoding.UTF8.GetBytes("first-time-slice");
            byte[] secondBytes = System.Text.Encoding.UTF8.GetBytes("second-time-slice");
            byte[] replacementBytes = System.Text.Encoding.UTF8.GetBytes("first-time-slice-rebuilt");

            Assert.That(firstPath, Is.Not.EqualTo(secondPath));
            Assert.That(repeatedPath, Is.EqualTo(firstPath));
            Assert.That(VfxPreviewEvidenceFileTransaction.Publish(firstPath, firstBytes, out string firstMessage), Is.True, firstMessage);
            Assert.That(VfxPreviewEvidenceFileTransaction.Publish(secondPath, secondBytes, out string secondMessage), Is.True, secondMessage);
            Assert.That(VfxPreviewEvidenceFileTransaction.Publish(repeatedPath, replacementBytes, out string repeatMessage), Is.True, repeatMessage);
            Assert.That(File.ReadAllBytes(firstPath), Is.EqualTo(replacementBytes));
            Assert.That(File.ReadAllBytes(secondPath), Is.EqualTo(secondBytes));
        }

        [Test]
        public void PreviewEvidencePublishFailure_PreservesExistingBytesAndCleansTemporaryFiles()
        {
            string sessionId = "preview_publish_failure_" + Guid.NewGuid().ToString("N");
            string outputRoot = "Temp/MxFramework/VfxCandidates/" + sessionId;
            _cleanupDirectories.Add(outputRoot);
            string outputPath = VfxDeterministicPreviewCapture.BuildOutputPath(
                sessionId, "candidate_a", "gameplay", "Gameplay", "DarkNeutral", "single");
            byte[] original = System.Text.Encoding.UTF8.GetBytes("existing-preview-bytes");
            byte[] replacement = System.Text.Encoding.UTF8.GetBytes("replacement-preview-bytes");
            Assert.That(VfxPreviewEvidenceFileTransaction.Publish(outputPath, original, out string initialMessage), Is.True, initialMessage);

            bool published = VfxPreviewEvidenceFileTransaction.Publish(
                outputPath,
                replacement,
                out string failureMessage,
                publisher: (_temporary, output, _backup) =>
                {
                    File.WriteAllBytes(output, System.Text.Encoding.UTF8.GetBytes("partial"));
                    throw new IOException("simulated preview publish failure");
                });

            Assert.That(published, Is.False);
            Assert.That(failureMessage, Does.Contain("preserved the prior file"));
            Assert.That(File.ReadAllBytes(outputPath), Is.EqualTo(original));
            string directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
            Assert.That(Directory.GetFiles(directory, ".preview.png.*.tmp"), Is.Empty);
            Assert.That(Directory.GetFiles(directory, ".preview.png.*.bak"), Is.Empty);
        }

        [Test]
        public void GraphTopologyPolicy_RejectsArbitraryMutationBeforeGeneration()
        {
            var spec = new VfxGraphTemplateCloneSpec
            {
                TemplateId = "unity.vfx17.simple_burst",
                TopologyAdapter = new VfxTopologyAdapterPolicy
                {
                    Mode = "ArbitraryTopology",
                    VersionLocked = true,
                    LockedPackageVersion = "17.5.0",
                    DraftOnly = true
                }
            };

            bool valid = VfxEditorCandidateGenerator.TryValidateGraphTemplatePolicy(spec, out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("unavailable"));
        }

        [Test]
        public void CertifiedGraphTemplate_ExistsInLockedPackageAndIsImportable()
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.visualeffectgraph");
            Assert.That(package, Is.Not.Null, "Visual Effect Graph package is required for certified template cloning.");
            Assert.That(package.version, Is.EqualTo("17.5.0"));
            string resolvedPath = Path.Combine(package.resolvedPath, "Editor", "Templates", "Simple_Burst.vfx");
            Assert.That(File.Exists(resolvedPath), Is.True, resolvedPath);
            Assert.That(AssetDatabase.LoadMainAssetAtPath("Packages/com.unity.visualeffectgraph/Editor/Templates/Simple_Burst.vfx"), Is.Not.Null);
        }

        [Test]
        public void VisualEvaluator_PassingEvidenceStillRequiresHumanReview()
        {
            VfxVisualEvaluationResult result = new VfxDefaultVisualEvaluator().Evaluate(
                VfxCreativeCandidateMenu.CreateFireballPlan("evaluation_test"),
                new VfxPreviewCaptureResult
                {
                    Success = true,
                    AlphaCoverage = 0.18f,
                    MeanLuminance = 0.4f,
                    PeakLuminance = 0.9f
                });

            Assert.That(result.PassedHardGates, Is.True);
            Assert.That(result.Score, Is.GreaterThan(0));
            Assert.That(result.ReviewStatus, Is.EqualTo("ReviewNeeded"));
            Assert.That(result.Diagnostics, Has.Some.Contains("human review"));
        }

        [Test]
        public void CoreReceiptAdapter_ExportsCanonicalDraftEvidence_AndRejectsTamper()
        {
            CoreCandidateFixture fixture = CreateCoreCandidateFixture("receipt");

            Assert.That(fixture.Export.Success, Is.True, fixture.Export.Message);
            Assert.That(fixture.Export.Manifest.ReviewStatus, Is.EqualTo("Draft"));
            Assert.That(fixture.Export.Manifest.ManifestHash, Is.EqualTo(
                VfxCandidateSandbox.ComputeCoreDocumentHash(fixture.Export.Manifest, "manifestHash")));
            Assert.That(fixture.Export.Receipt.ReceiptHash, Is.EqualTo(
                VfxCandidateSandbox.ComputeCoreDocumentHash(fixture.Export.Receipt, "receiptHash")));
            VfxCandidateAssetReceipt localPrefab = FindAsset(fixture.Generation.Receipt, "Prefab");
            VfxCoreCandidateAssetRecord corePrefab = FindCoreAsset(fixture.Export.Receipt, localPrefab.AssetPath);
            Assert.That(corePrefab.UnityGuid, Is.EqualTo(localPrefab.Guid));
            Assert.That(corePrefab.DependencyHash, Is.EqualTo(localPrefab.DependencyHash));

            VfxCandidateAssetReceipt tampered = fixture.Generation.Receipt.Assets[0];
            string originalHash = tampered.ContentHash;
            tampered.ContentHash = HashText("tampered");
            VfxCoreReceiptExportResult rejected = VfxCoreCandidateReceiptAdapter.Export(
                fixture.GenerationPlanPath,
                new[] { fixture.Generation });
            tampered.ContentHash = originalHash;

            Assert.That(rejected.Success, Is.False);
            Assert.That(rejected.Message, Does.Contain("stale"));

            string originalGuid = tampered.Guid;
            tampered.Guid = new string('f', 32);
            rejected = VfxCoreCandidateReceiptAdapter.Export(fixture.GenerationPlanPath, new[] { fixture.Generation });
            tampered.Guid = originalGuid;
            Assert.That(rejected.Success, Is.False);
            Assert.That(rejected.Message, Does.Contain("GUID"));

            string originalDependencyHash = tampered.DependencyHash;
            tampered.DependencyHash = new string('e', 32);
            rejected = VfxCoreCandidateReceiptAdapter.Export(fixture.GenerationPlanPath, new[] { fixture.Generation });
            tampered.DependencyHash = originalDependencyHash;
            Assert.That(rejected.Success, Is.False);
            Assert.That(rejected.Message, Does.Contain("dependency"));
        }

        [Test]
        public void Promotion_AuthorizedCoreEvidence_PreservesGuidAndStopsAtReviewNeeded()
        {
            CoreCandidateFixture fixture = CreateCoreCandidateFixture("promotion");
            VfxCandidateAssetReceipt prefab = FindAsset(fixture.Generation.Receipt, "Prefab");
            string originalGuid = prefab.Guid;
            string destinationRoot = "Assets/VFX/MxCandidatePromotionTests/" + fixture.SessionId;
            _cleanupPaths.Add(destinationRoot);
            AuthorizedPromotionFixture promotion = CreateAuthorizedPromotionFixture(fixture, destinationRoot);

            var confirmation = new FakePromotionConfirmation(true);
            VfxPromotionResult result = new VfxGuardedPromotionExecutor(
                new FakeSelectionAuthorityVerifier(),
                confirmation).Promote(promotion.Request);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Status, Is.EqualTo("Committed"));
            Assert.That(result.CorePromotionReceiptHash, Does.StartWith("sha256:"));
            Assert.That(result.PromotedAssetPaths, Has.Count.EqualTo(fixture.Export.Receipt.Assets.Count));
            string destinationPrefab = FindDestinationForSource(promotion.Plan, prefab.AssetPath);
            Assert.That(AssetDatabase.AssetPathToGUID(destinationPrefab), Is.EqualTo(originalGuid));
            Assert.That(VfxCandidateSandbox.ComputeAssetFileHash(destinationPrefab), Is.EqualTo(prefab.ContentHash));
            VfxCorePromotionReceipt receipt = VfxCandidateSandbox.ReadJsonAsset<VfxCorePromotionReceipt>(result.CorePromotionReceiptAssetPath);
            Assert.That(receipt.ResultReviewStatus, Is.EqualTo("ReviewNeeded"));
            Assert.That(confirmation.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Promotion_CommittedTransactionWriteFailure_RollsBackMovesAndProvisionalCoreReceipt()
        {
            CoreCandidateFixture fixture = CreateCoreCandidateFixture("promotion_commit_write_failure");
            string destinationRoot = "Assets/VFX/MxCandidatePromotionTests/" + fixture.SessionId;
            _cleanupPaths.Add(destinationRoot);
            AuthorizedPromotionFixture promotion = CreateAuthorizedPromotionFixture(fixture, destinationRoot);
            int coreReceiptWrites = 0;
            Action<string, VfxCorePromotionReceipt> coreWriter = (path, receipt) =>
            {
                coreReceiptWrites++;
                VfxCandidateSandbox.WriteJsonAsset(path, receipt);
            };
            Action<string, VfxPromotionTransactionReceipt> transactionWriter = (path, receipt) =>
            {
                if (string.Equals(receipt.Status, "Committed", StringComparison.Ordinal))
                    throw new IOException("simulated committed transaction write failure");
                VfxCandidateSandbox.WriteJsonAsset(path, receipt);
            };

            VfxPromotionResult result = new VfxGuardedPromotionExecutor(
                new FakeSelectionAuthorityVerifier(),
                new FakePromotionConfirmation(true),
                coreWriter,
                transactionWriter).Promote(promotion.Request);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo("RolledBack").Or.EqualTo("RecoveryRequired"));
            Assert.That(coreReceiptWrites, Is.EqualTo(1));
            AssertPromotionFailureState(fixture, promotion, result);
        }

        [Test]
        public void Promotion_CoreReceiptPartialWriteFailure_RemovesPartialAssetAndRollsBackMoves()
        {
            CoreCandidateFixture fixture = CreateCoreCandidateFixture("promotion_receipt_write_failure");
            string destinationRoot = "Assets/VFX/MxCandidatePromotionTests/" + fixture.SessionId;
            _cleanupPaths.Add(destinationRoot);
            AuthorizedPromotionFixture promotion = CreateAuthorizedPromotionFixture(fixture, destinationRoot);
            Action<string, VfxCorePromotionReceipt> partialWriter = (path, _) =>
            {
                File.WriteAllText(path, "{\"partial\":true");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                throw new IOException("simulated partial Core receipt write failure");
            };

            VfxPromotionResult result = new VfxGuardedPromotionExecutor(
                new FakeSelectionAuthorityVerifier(),
                new FakePromotionConfirmation(true),
                partialWriter).Promote(promotion.Request);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo("RolledBack").Or.EqualTo("RecoveryRequired"));
            AssertPromotionFailureState(fixture, promotion, result);
        }

        [Test]
        public void Promotion_ExplicitHumanConfirmationCancel_DoesNotMoveAssets()
        {
            CoreCandidateFixture fixture = CreateCoreCandidateFixture("cancel");
            VfxCandidateAssetReceipt prefab = FindAsset(fixture.Generation.Receipt, "Prefab");
            string destinationRoot = "Assets/VFX/MxCandidatePromotionTests/" + fixture.SessionId;
            _cleanupPaths.Add(destinationRoot);
            AuthorizedPromotionFixture promotion = CreateAuthorizedPromotionFixture(fixture, destinationRoot);
            var confirmation = new FakePromotionConfirmation(false);

            VfxPromotionResult result = new VfxGuardedPromotionExecutor(
                new FakeSelectionAuthorityVerifier(),
                confirmation).Promote(promotion.Request);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo("Cancelled"));
            Assert.That(confirmation.CallCount, Is.EqualTo(1));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(prefab.AssetPath), Is.Not.Null);
            Assert.That(AssetDatabase.IsValidFolder(destinationRoot), Is.False);
        }

        private static void AssertPromotionFailureState(
            CoreCandidateFixture fixture,
            AuthorizedPromotionFixture promotion,
            VfxPromotionResult result)
        {
            string coreReceiptPath = fixture.SessionRoot + "/promotion_receipt_" + promotion.Request.TransactionId + ".json";
            Assert.That(File.Exists(coreReceiptPath), Is.False);
            Assert.That(File.Exists(coreReceiptPath + ".meta"), Is.False);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(coreReceiptPath), Is.Null);
            for (int i = 0; i < promotion.Plan.Moves.Count; i++)
            {
                VfxCorePromotionMove move = promotion.Plan.Moves[i];
                Assert.That(AssetDatabase.LoadMainAssetAtPath(move.SourceAssetPath), Is.Not.Null, move.SourceAssetPath);
                Assert.That(AssetDatabase.LoadMainAssetAtPath(move.DestinationAssetPath), Is.Null, move.DestinationAssetPath);
            }

            VfxPromotionTransactionReceipt transaction =
                VfxCandidateSandbox.ReadJsonAsset<VfxPromotionTransactionReceipt>(result.TransactionReceiptAssetPath);
            Assert.That(transaction.Status, Is.EqualTo("RolledBack").Or.EqualTo("RecoveryRequired"));
            string snapshotRoot = fixture.SessionRoot + "/PromotionEvidence/" + promotion.Request.TransactionId;
            Assert.That(AssetDatabase.IsValidFolder(snapshotRoot), Is.True);
            Assert.That(File.Exists(snapshotRoot + "/generation_plan.json"), Is.True);
            Assert.That(File.Exists(snapshotRoot + "/promotion_plan.json"), Is.True);
            Assert.That(File.Exists(snapshotRoot + "/human_selection.json"), Is.True);
        }

        [Test]
        public void Promotion_InvalidOrUntrustedSelectionAuthority_FailsClosedBeforeConfirmation()
        {
            CoreCandidateFixture fixture = CreateCoreCandidateFixture("authority");
            string destinationRoot = "Assets/VFX/MxCandidatePromotionTests/" + fixture.SessionId;
            _cleanupPaths.Add(destinationRoot);
            AuthorizedPromotionFixture promotion = CreateAuthorizedPromotionFixture(fixture, destinationRoot);
            promotion.Plan.SelectionAuthorityId = string.Empty;
            promotion.Plan.PlanHash = VfxCandidateSandbox.ComputeCoreDocumentHash(promotion.Plan, "planHash", "diagnostics", "blocked");
            VfxCandidateSandbox.WriteJsonFile(promotion.PromotionPlanPath, promotion.Plan);
            var confirmation = new FakePromotionConfirmation(true);

            VfxPromotionResult invalid = new VfxGuardedPromotionExecutor(
                new FakeSelectionAuthorityVerifier(),
                confirmation).Promote(promotion.Request);

            Assert.That(invalid.Success, Is.False);
            Assert.That(invalid.Message, Does.Contain("selection-authority"));
            Assert.That(confirmation.CallCount, Is.EqualTo(0));

            promotion.Plan.SelectionAuthorityId = FakeSelectionAuthorityVerifier.TrustedAuthorityId;
            promotion.Plan.PlanHash = VfxCandidateSandbox.ComputeCoreDocumentHash(promotion.Plan, "planHash", "diagnostics", "blocked");
            VfxCandidateSandbox.WriteJsonFile(promotion.PromotionPlanPath, promotion.Plan);
            VfxPromotionResult unconfigured = new VfxGuardedPromotionExecutor().Promote(promotion.Request);
            Assert.That(unconfigured.Success, Is.False);
            Assert.That(unconfigured.Message, Does.Contain("no trusted selection-authority verifier"));
            Assert.That(AssetDatabase.IsValidFolder(destinationRoot), Is.False);
        }

        [Test]
        public void Promotion_RejectsMissingAuthoritativeCoreEvidenceWithoutMovingAssets()
        {
            VfxCandidateGenerationResult generation = GenerateCandidate("stale");
            VfxCandidateAssetReceipt prefab = FindAsset(generation.Receipt, "Prefab");

            VfxPromotionResult result = new VfxGuardedPromotionExecutor().Promote(new VfxAuthorizedPromotionRequest
            {
                GenerationPlanPath = "Temp/MxFramework/VfxAuthoring/missing/generation_plan.json",
                CandidateReceiptPath = generation.SessionRoot + "/candidate_receipt.json",
                EvaluationReportPath = "Temp/MxFramework/VfxAuthoring/missing/evaluation_report.json",
                PromotionPlanPath = "Temp/MxFramework/VfxAuthoring/missing/promotion_plan.json",
                HumanSelectionEvidencePath = "Temp/MxFramework/VfxAuthoring/missing/human_selection.json",
                TransactionId = "stale_tx"
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo("Rejected"));
            Assert.That(result.Message, Does.Contain("missing"));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(prefab.AssetPath), Is.Not.Null);
        }

        private CoreCandidateFixture CreateCoreCandidateFixture(string prefix)
        {
            string sessionId = NewId(prefix);
            Assert.That(VfxCandidateSandbox.TryGetSessionRoot(sessionId, out string sessionRoot, out string sessionError), Is.True, sessionError);
            string candidateId = "candidate_a";
            string candidateRoot = sessionRoot + "/" + candidateId;
            string evidenceRoot = VfxCandidateProtocol.AuthoritativeEvidenceRoot + "/" + sessionId;
            string generationPlanPath = evidenceRoot + "/generation_plan.json";
            _cleanupPaths.Add(sessionRoot);
            _cleanupDirectories.Add(evidenceRoot);

            VfxCandidatePlan localPlan = VfxCreativeCandidateMenu.CreateFireballPlan(sessionId);
            localPlan.CoreCandidateId = candidateId;
            localPlan.CandidateOutputRoot = candidateRoot;
            var generationPlan = new VfxCoreGenerationPlan
            {
                PlanId = "plan_" + sessionId,
                SessionId = sessionId,
                BriefHash = HashText("brief:" + sessionId),
                ReuseDecisionHash = HashText("reuse:" + sessionId),
                CatalogHash = VfxCandidateSandbox.ComputeAssetFileHash(VfxCandidateProtocol.AuthoringCatalogPath),
                GenerationTemplateCatalogHash = VfxCandidateSandbox.ComputeAssetFileHash(VfxCandidateProtocol.GenerationTemplateCatalogPath),
                SessionCapabilitiesHash = HashText("capabilities:" + sessionId),
                GenerationMode = "ControlledTemplate",
                Round = 0,
                QualityProfileId = "windows.d3d12.1080p60",
                QualityProfileVersion = "1",
                TargetCostTier = "Medium",
                SandboxRoot = sessionRoot
            };
            generationPlan.FixedSeeds.Add(unchecked((int)localPlan.FixedSeed));
            generationPlan.Candidates.Add(new VfxCoreGenerationCandidateSpec
            {
                CandidateId = candidateId,
                VariantKind = "ParticleSystemPrimitive",
                Seed = unchecked((int)localPlan.FixedSeed),
                ProviderId = "mx.vfx.provider.procedural",
                ProviderVersion = "1.0.0",
                ModelId = "mx.procedural.vfx",
                ModelVersion = "1",
                TemplateId = "mx.vfx.template.particle.fireball",
                TemplateVersion = "1",
                CostTier = "Medium",
                OutputRoot = candidateRoot,
                DraftOnly = true
            });
            generationPlan.PlanHash = VfxCandidateSandbox.ComputeCoreDocumentHash(
                generationPlan,
                "planHash",
                "diagnostics",
                "blocked");
            VfxCandidateSandbox.WriteJsonFile(generationPlanPath, generationPlan);

            VfxCandidateGenerationResult generation = new VfxEditorCandidateGenerator().Generate(localPlan);
            Assert.That(generation.Success, Is.True, generation.Message);
            VfxCoreReceiptExportResult export = VfxCoreCandidateReceiptAdapter.Export(
                generationPlanPath,
                new[] { generation });
            Assert.That(export.Success, Is.True, export.Message);
            return new CoreCandidateFixture
            {
                SessionId = sessionId,
                SessionRoot = sessionRoot,
                CandidateId = candidateId,
                EvidenceRoot = evidenceRoot,
                GenerationPlanPath = generationPlanPath,
                GenerationPlan = generationPlan,
                Generation = generation,
                Export = export
            };
        }

        private AuthorizedPromotionFixture CreateAuthorizedPromotionFixture(CoreCandidateFixture fixture, string destinationRoot)
        {
            string evaluationPath = fixture.EvidenceRoot + "/evaluation_report.json";
            JObject evaluation = CreateEvaluationDocument(fixture);
            VfxCandidateSandbox.WriteJsonFile(evaluationPath, evaluation);
            string evaluationHash = evaluation.Value<string>("reportHash");

            string selectionPath = fixture.EvidenceRoot + "/human_selection.json";
            const string selectedBy = "human.test-reviewer";
            var selection = new VfxHumanSelectionEvidence
            {
                CandidateId = fixture.CandidateId,
                SelectedBy = selectedBy,
                GenerationPlanHash = fixture.GenerationPlan.PlanHash,
                CandidateReceiptHash = fixture.Export.Receipt.ReceiptHash,
                EvaluationReportHash = evaluationHash,
                SelectionReason = "Focused test approval for ReviewNeeded promotion only."
            };
            VfxCandidateSandbox.WriteJsonFile(selectionPath, selection);

            var plan = new VfxCorePromotionPlan
            {
                CandidateId = fixture.CandidateId,
                TargetKind = "Effect",
                TargetStableId = "vfx.test." + fixture.SessionId,
                ExpectedCatalogHash = VfxCandidateSandbox.ComputeAssetFileHash(VfxCandidateProtocol.AuthoringCatalogPath),
                GenerationPlanHash = fixture.GenerationPlan.PlanHash,
                CandidateReceiptHash = fixture.Export.Receipt.ReceiptHash,
                EvaluationReportHash = evaluationHash,
                SelectedBy = selectedBy,
                SelectionEvidenceHash = VfxCandidateSandbox.ComputeAssetFileHash(selectionPath),
                SelectionAuthorityId = FakeSelectionAuthorityVerifier.TrustedAuthorityId,
                SelectionAuthorityVersion = FakeSelectionAuthorityVerifier.TrustedAuthorityVersion,
                SelectionAuthorityReceiptHash = FakeSelectionAuthorityVerifier.TrustedReceiptHash,
                ResultReviewStatus = "ReviewNeeded",
                RequiresUnityEditor = true,
                RequiresHumanConfirmation = true
            };
            for (int i = 0; i < fixture.Export.Receipt.Assets.Count; i++)
            {
                VfxCoreCandidateAssetRecord asset = fixture.Export.Receipt.Assets[i];
                plan.Moves.Add(new VfxCorePromotionMove
                {
                    SourceAssetPath = asset.AssetPath,
                    DestinationAssetPath = destinationRoot + "/" + asset.AssetKind + "/" + Path.GetFileName(asset.AssetPath),
                    ExpectedContentHash = asset.ContentHash,
                    ExpectedUnityGuid = asset.UnityGuid,
                    ExpectedDependencyHash = asset.DependencyHash
                });
            }
            plan.PlanHash = VfxCandidateSandbox.ComputeCoreDocumentHash(plan, "planHash", "diagnostics", "blocked");
            string promotionPlanPath = fixture.EvidenceRoot + "/promotion_plan.json";
            VfxCandidateSandbox.WriteJsonFile(promotionPlanPath, plan);
            return new AuthorizedPromotionFixture
            {
                Plan = plan,
                PromotionPlanPath = promotionPlanPath,
                Request = new VfxAuthorizedPromotionRequest
                {
                    GenerationPlanPath = fixture.GenerationPlanPath,
                    CandidateReceiptPath = fixture.Export.ReceiptAssetPath,
                    EvaluationReportPath = evaluationPath,
                    PromotionPlanPath = promotionPlanPath,
                    HumanSelectionEvidencePath = selectionPath,
                    TransactionId = "tx_" + fixture.SessionId
                }
            };
        }

        private static JObject CreateEvaluationDocument(CoreCandidateFixture fixture)
        {
            var document = new JObject
            {
                ["schemaVersion"] = VfxCandidateProtocol.CoreEvaluationReportSchemaVersion,
                ["generationPlanHash"] = fixture.GenerationPlan.PlanHash,
                ["candidateReceiptHash"] = fixture.Export.Receipt.ReceiptHash,
                ["qualityProfileId"] = fixture.GenerationPlan.QualityProfileId,
                ["qualityProfileVersion"] = fixture.GenerationPlan.QualityProfileVersion,
                ["round"] = fixture.GenerationPlan.Round,
                ["candidates"] = new JArray(new JObject
                {
                    ["candidateId"] = fixture.CandidateId,
                    ["costTier"] = "Medium",
                    ["hardGatesPassed"] = true,
                    ["shortlisted"] = true,
                    ["cpuP95Milliseconds"] = 0.1,
                    ["gpuP95Milliseconds"] = 0.2,
                    ["score"] = new JObject
                    {
                        ["briefMatch"] = 90,
                        ["readability"] = 90,
                        ["timing"] = 90,
                        ["spatialMotion"] = 90,
                        ["materialAndColor"] = 90,
                        ["polish"] = 90,
                        ["total"] = 90,
                        ["reviewerSpread"] = 0
                    },
                    ["hardGates"] = new JArray(),
                    ["reasons"] = new JArray()
                }),
                ["paretoFrontierCandidateIds"] = new JArray(fixture.CandidateId),
                ["medianShortlistScore"] = 90,
                ["improvementFromPreviousRound"] = 90,
                ["consecutiveLowImprovementRounds"] = 0,
                ["stopEvolution"] = false,
                ["primitiveBriefs"] = new JArray(),
                ["reportHash"] = string.Empty,
                ["diagnostics"] = new JArray(),
                ["blocked"] = false
            };
            document["reportHash"] = VfxCandidateSandbox.ComputeCoreDocumentHash(
                document,
                "reportHash",
                "diagnostics",
                "blocked");
            return document;
        }

        private static VfxCandidatePlan CreateSupportTexturePlan(string sessionId)
        {
            var plan = new VfxCandidatePlan { SessionId = sessionId, FixedSeed = 424242u };
            plan.Textures.Add(CreateTextureSpec("sdf", VfxProceduralTextureKind.Sdf));
            plan.Textures.Add(CreateTextureSpec("flow", VfxProceduralTextureKind.Flow));
            plan.Textures.Add(CreateTextureSpec("radial", VfxProceduralTextureKind.RadialMask));
            VfxProceduralTextureSpec flipbook = CreateTextureSpec("flipbook", VfxProceduralTextureKind.Flipbook);
            flipbook.FrameColumns = 4;
            flipbook.FrameRows = 2;
            flipbook.FrameCount = 8;
            plan.Textures.Add(flipbook);
            return plan;
        }

        private static VfxProceduralTextureSpec CreateTextureSpec(string id, VfxProceduralTextureKind kind)
        {
            return new VfxProceduralTextureSpec
            {
                TextureId = id,
                AssetName = id,
                Kind = kind,
                Width = 64,
                Height = 64,
                Color = new VfxColorSpec { R = 0.8f, G = 0.4f, B = 0.2f, A = 1f }
            };
        }

        private static string FindDestinationForSource(VfxCorePromotionPlan plan, string source)
        {
            for (int i = 0; i < plan.Moves.Count; i++)
                if (string.Equals(plan.Moves[i].SourceAssetPath, source, StringComparison.Ordinal))
                    return plan.Moves[i].DestinationAssetPath;
            Assert.Fail("Promotion plan did not contain source " + source);
            return string.Empty;
        }

        private static VfxCoreCandidateAssetRecord FindCoreAsset(VfxCoreCandidateReceipt receipt, string assetPath)
        {
            for (int i = 0; i < receipt.Assets.Count; i++)
                if (string.Equals(receipt.Assets[i].AssetPath, assetPath, StringComparison.Ordinal))
                    return receipt.Assets[i];
            Assert.Fail("Core candidate receipt did not contain asset " + assetPath);
            return null;
        }

        private static string HashText(string value)
        {
            return VfxCandidateSandbox.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
        }

        private VfxCandidateGenerationResult GenerateCandidate(string prefix)
        {
            string sessionId = NewId(prefix);
            VfxCandidateGenerationResult result = new VfxEditorCandidateGenerator().Generate(VfxCreativeCandidateMenu.CreateFireballPlan(sessionId));
            _cleanupPaths.Add(result.SessionRoot);
            Assert.That(result.Success, Is.True, result.Message);
            return result;
        }

        private static string NewId(string prefix)
        {
            return "test_" + prefix + "_" + Guid.NewGuid().ToString("N");
        }

        private static VfxCandidateAssetReceipt FindAsset(VfxCandidateReceipt receipt, string kind)
        {
            for (int i = 0; i < receipt.Assets.Count; i++)
            {
                if (string.Equals(receipt.Assets[i].Kind, kind, StringComparison.Ordinal))
                    return receipt.Assets[i];
            }
            Assert.Fail("Candidate receipt did not contain asset kind " + kind);
            return null;
        }

        private sealed class CoreCandidateFixture
        {
            public string SessionId;
            public string SessionRoot;
            public string CandidateId;
            public string EvidenceRoot;
            public string GenerationPlanPath;
            public VfxCoreGenerationPlan GenerationPlan;
            public VfxCandidateGenerationResult Generation;
            public VfxCoreReceiptExportResult Export;
        }

        private sealed class AuthorizedPromotionFixture
        {
            public VfxCorePromotionPlan Plan;
            public string PromotionPlanPath;
            public VfxAuthorizedPromotionRequest Request;
        }

        private sealed class FakeSelectionAuthorityVerifier : IVfxPromotionSelectionAuthorityVerifier
        {
            public const string TrustedAuthorityId = "test.vfx.selection-authority";
            public const string TrustedAuthorityVersion = "1";
            public static readonly string TrustedReceiptHash = HashText("trusted-selection-receipt");

            public string AuthorityId => TrustedAuthorityId;
            public string AuthorityVersion => TrustedAuthorityVersion;

            public bool Verify(VfxPromotionSelectionAuthorityClaims claims, out string rejectionReason)
            {
                bool valid = claims != null
                    && string.Equals(claims.AuthorityReceiptHash, TrustedReceiptHash, StringComparison.OrdinalIgnoreCase)
                    && VfxCandidateSandbox.IsSha256(claims.GenerationPlanHash)
                    && VfxCandidateSandbox.IsSha256(claims.CandidateReceiptHash)
                    && VfxCandidateSandbox.IsSha256(claims.EvaluationReportHash)
                    && VfxCandidateSandbox.IsSha256(claims.SelectionEvidenceHash)
                    && !string.IsNullOrWhiteSpace(claims.SelectedBy);
                rejectionReason = valid ? string.Empty : "test authority claim mismatch";
                return valid;
            }
        }

        private sealed class FakePromotionConfirmation : IVfxPromotionHumanConfirmation
        {
            private readonly bool _result;
            public int CallCount { get; private set; }

            public FakePromotionConfirmation(bool result)
            {
                _result = result;
            }

            public bool Confirm(VfxCorePromotionPlan plan)
            {
                CallCount++;
                return _result;
            }
        }
    }
}
