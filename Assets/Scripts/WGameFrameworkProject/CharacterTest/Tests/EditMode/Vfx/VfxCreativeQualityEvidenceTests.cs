using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Editor;
using NUnit.Framework;
using UnityEditor;

namespace MxFramework.Tests
{
    public sealed class VfxCreativeQualityEvidenceTests
    {
        private readonly List<string> _assetCleanup = new List<string>();
        private readonly List<string> _directoryCleanup = new List<string>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _assetCleanup.Count - 1; i >= 0; i--)
                if (AssetDatabase.LoadMainAssetAtPath(_assetCleanup[i]) != null || AssetDatabase.IsValidFolder(_assetCleanup[i])) AssetDatabase.DeleteAsset(_assetCleanup[i]);
            for (int i = _directoryCleanup.Count - 1; i >= 0; i--)
                if (Directory.Exists(_directoryCleanup[i])) Directory.Delete(_directoryCleanup[i], true);
            _assetCleanup.Clear();
            _directoryCleanup.Clear();
            AssetDatabase.Refresh();
        }

        [Test]
        public void QualityProfile_LoadsFormalFixedContract()
        {
            bool loaded = VfxQualityProfileLoader.TryLoad(
                VfxCandidateProtocol.GenerationTemplateCatalogPath,
                "windows.d3d12.1080p60",
                "1",
                out VfxQualityProfileTransport profile,
                out string error);

            Assert.That(loaded, Is.True, error);
            Assert.That(profile.FixedStepSeconds, Is.EqualTo(1d / 60d).Within(0.000000001d));
            Assert.That(profile.FixedSeeds, Is.EqualTo(new[] { 17, 73, 211 }));
            Assert.That(profile.OneShotSlicePercents, Is.EqualTo(new[] { 0, 10, 25, 50, 75, 100 }));
            Assert.That(profile.GhostCycleCount, Is.EqualTo(20));
        }

        [Test]
        public void CaptureMatrix_IsCompleteOrderedAndDeterministic()
        {
            VfxQualityProfileTransport profile = LoadProfile();
            var oneShot = new VfxQualityCandidateContext { CandidateId = "candidate.oneshot", LifecycleMode = "OneShot", DurationSeconds = 1.2f };
            List<VfxQualityCapturePoint> first = VfxQualityCaptureMatrix.Build("matrix_session", oneShot, profile);
            List<VfxQualityCapturePoint> second = VfxQualityCaptureMatrix.Build("matrix_session", oneShot, profile);

            Assert.That(first, Has.Count.EqualTo(3 * 3 * 4 * 6));
            Assert.That(second, Has.Count.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].OutputPath, Is.EqualTo(first[i].OutputPath));
                Assert.That(first[i].OutputPath, Does.StartWith("Temp/MxFramework/VfxAuthoring/QualityEvidence/matrix_session/candidate.oneshot/seed-"));
            }

            var loop = new VfxQualityCandidateContext
            {
                CandidateId = "candidate.loop",
                LifecycleMode = "Loop",
                DurationSeconds = 1f,
                IncludePathView = true,
                RequiresTelegraphMeasurement = true
            };
            List<VfxQualityCapturePoint> loopPoints = VfxQualityCaptureMatrix.Build("matrix_session", loop, profile);
            Assert.That(loopPoints, Has.Count.EqualTo(3 * 5 * 4 * 4));
            Assert.That(loopPoints.Exists(value => value.PhaseId == "warmup"), Is.True);
            Assert.That(loopPoints.Exists(value => value.PhaseId == "steady"), Is.True);
            Assert.That(loopPoints.Exists(value => value.PhaseId == "stop"), Is.True);
            Assert.That(loopPoints.Exists(value => value.PhaseId == "cleanup"), Is.True);
            Assert.That(loopPoints.Exists(value => value.ViewId == "Path"), Is.True);
            Assert.That(loopPoints.Exists(value => value.ViewId == "Top"), Is.True);
        }

        [Test]
        public void Runner_ExportsExactCoreInputWithStableEvidenceAndDisposesProviders()
        {
            QualityFixture fixture = CreateFixture("complete");
            var capture = new FakeCaptureBackend();
            var measurement = new FakeMeasurementProvider(true);
            VfxQualityCaptureResult result;
            using (var runner = new VfxQualityEvidenceCaptureRunner(capture, measurement))
                result = runner.Capture(fixture.Request);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Complete, Is.True, result.Message);
            Assert.That(result.Manifest.ReviewStatus, Is.EqualTo("Draft"));
            Assert.That(result.Manifest.Candidates, Has.Count.EqualTo(1));
            Assert.That(result.Manifest.Candidates[0].Captures, Has.Count.EqualTo(216));
            Assert.That(result.Manifest.Candidates[0].HardGates, Has.Count.EqualTo(11));
            Assert.That(result.EvaluationInput.SchemaVersion, Is.EqualTo("mx.vfx.evaluation-input.v1"));
            Assert.That(result.EvaluationInput.GenerationPlanHash, Is.EqualTo(fixture.Plan.PlanHash));
            Assert.That(result.EvaluationInput.CandidateReceiptHash, Is.EqualTo(fixture.Receipt.ReceiptHash));
            Assert.That(result.EvaluationInput.Candidates[0].ReviewerScores, Is.Empty, "Machine capture must not forge human review scores.");
            Assert.That(result.EvaluationInput.Candidates[0].HardGates.TrueForAll(value => VfxCandidateSandbox.IsSha256(value.EvidenceHash)), Is.True);
            Assert.That(VfxCandidateSandbox.IsSha256(result.EvidenceManifestHash), Is.True);
            Assert.That(VfxCandidateSandbox.IsSha256(result.EvaluationInputHash), Is.True);
            Assert.That(capture.EndCount, Is.EqualTo(1));
            Assert.That(capture.Disposed, Is.True);
            Assert.That(measurement.Disposed, Is.True);
        }

        [Test]
        public void Runner_MissingTargetMeasurementFailsClosedAsNotMeasured()
        {
            QualityFixture fixture = CreateFixture("not_measured");
            var capture = new FakeCaptureBackend();
            var measurement = new FakeMeasurementProvider(false);
            VfxQualityCaptureResult result;
            using (var runner = new VfxQualityEvidenceCaptureRunner(capture, measurement))
                result = runner.Capture(fixture.Request);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Complete, Is.False);
            VfxQualityGateEvidence zeroGc = result.Manifest.Candidates[0].HardGates.Find(value => value.GateId == "ZeroSteadyStateGc");
            Assert.That(zeroGc, Is.Not.Null);
            Assert.That(zeroGc.Status, Is.EqualTo("NotMeasured"));
            Assert.That(zeroGc.Passed, Is.False);
            Assert.That(result.EvaluationInput.Candidates[0].CpuP95Milliseconds, Is.EqualTo(-1f));
            Assert.That(result.EvaluationInput.Candidates[0].GpuP95Milliseconds, Is.EqualTo(-1f));
            Assert.That(result.EvaluationInput.Candidates[0].ReviewerScores, Is.Empty);
        }

        [Test]
        public void UnityCaptureBackend_ActualCandidatePrefabSmokeWritesAndReleasesFrame()
        {
            QualityFixture fixture = CreateFixture("unity_smoke");
            VfxQualityProfileTransport profile = LoadProfile();
            profile.Width = 128;
            profile.Height = 128;
            var point = VfxQualityCaptureMatrix.Build(fixture.Plan.SessionId, fixture.Request.CandidateContexts[0], profile)[0];
            _directoryCleanup.Add(VfxQualityEvidenceProtocol.OutputRoot + "/" + fixture.Plan.SessionId);

            VfxQualityFrameObservation observation;
            using (var backend = new VfxUnityQualityCaptureBackend())
            {
                backend.BeginCandidate(fixture.Request.CandidateContexts[0], fixture.PrefabPath, profile);
                observation = backend.Capture(point);
                backend.EndCandidate();
            }

            Assert.That(observation.Status, Is.EqualTo("Measured"), observation.Message);
            Assert.That(File.Exists(point.OutputPath), Is.True);
            Assert.That(VfxCandidateSandbox.IsSha256(observation.ContentHash), Is.True);
        }

        private QualityFixture CreateFixture(string prefix)
        {
            string sessionId = "quality_" + prefix + "_" + Guid.NewGuid().ToString("N");
            VfxCandidateGenerationResult generation = new VfxEditorCandidateGenerator().Generate(VfxCreativeCandidateMenu.CreateFireballPlan(sessionId));
            Assert.That(generation.Success, Is.True, generation.Message);
            _assetCleanup.Add(generation.SessionRoot);
            VfxCandidateAssetReceipt localPrefab = generation.Receipt.Assets.Find(value => value.Kind == "Prefab");
            Assert.That(localPrefab, Is.Not.Null);

            var plan = new VfxCoreGenerationPlan
            {
                PlanId = "plan." + sessionId,
                SessionId = sessionId,
                BriefHash = VfxCandidateSandbox.ComputeHash(new byte[] { 1 }),
                ReuseDecisionHash = VfxCandidateSandbox.ComputeHash(new byte[] { 2 }),
                CatalogHash = VfxCandidateSandbox.ComputeAssetFileHash(VfxCandidateProtocol.AuthoringCatalogPath),
                GenerationTemplateCatalogHash = VfxCandidateSandbox.ComputeAssetFileHash(VfxCandidateProtocol.GenerationTemplateCatalogPath),
                SessionCapabilitiesHash = VfxCandidateSandbox.ComputeHash(new byte[] { 3 }),
                GenerationMode = "ControlledTemplate",
                QualityProfileId = "windows.d3d12.1080p60",
                QualityProfileVersion = "1",
                TargetCostTier = "Medium",
                SandboxRoot = generation.SessionRoot,
                FixedSeeds = new List<int> { 17, 73, 211 },
                Candidates = new List<VfxCoreGenerationCandidateSpec>
                {
                    new VfxCoreGenerationCandidateSpec
                    {
                        CandidateId = "candidate." + prefix,
                        VariantKind = "ConservativeReuse",
                        Seed = 17,
                        ProviderId = "mx.vfx.provider.procedural",
                        ProviderVersion = "1.0.0",
                        TemplateId = "mx.vfx.template.particle.fireball",
                        TemplateVersion = "1",
                        CostTier = "Medium",
                        OutputRoot = generation.SessionRoot,
                        DraftOnly = true
                    }
                }
            };
            plan.PlanHash = VfxCandidateSandbox.ComputeCoreDocumentHash(plan, "planHash", "diagnostics", "blocked");
            string planDirectory = "Temp/MxFramework/VfxAuthoring/QualityTests/" + sessionId;
            string planPath = planDirectory + "/generation_plan.json";
            _directoryCleanup.Add(planDirectory);
            VfxCandidateSandbox.WriteJsonFile(planPath, plan);

            var receipt = new VfxCoreCandidateReceipt
            {
                SessionId = sessionId,
                GenerationPlanHash = plan.PlanHash,
                CatalogHash = plan.CatalogHash,
                GenerationTemplateCatalogHash = plan.GenerationTemplateCatalogHash,
                Assets = new List<VfxCoreCandidateAssetRecord>
                {
                    new VfxCoreCandidateAssetRecord
                    {
                        CandidateId = plan.Candidates[0].CandidateId,
                        AssetKind = "Prefab",
                        AssetPath = localPrefab.AssetPath,
                        ContentHash = localPrefab.ContentHash,
                        UnityGuid = localPrefab.Guid,
                        DependencyHash = localPrefab.DependencyHash,
                        Provenance = new VfxCoreAssetProvenance
                        {
                            ProviderId = plan.Candidates[0].ProviderId,
                            ProviderVersion = plan.Candidates[0].ProviderVersion,
                            TemplateId = plan.Candidates[0].TemplateId,
                            TemplateVersion = plan.Candidates[0].TemplateVersion,
                            LicenseId = "project-owned",
                            SourceUri = "mx://quality-test/" + sessionId,
                            SourceContentHash = localPrefab.ContentHash
                        }
                    }
                }
            };
            receipt.ReceiptHash = VfxCandidateSandbox.ComputeCoreDocumentHash(receipt, "receiptHash");
            string receiptPath = generation.SessionRoot + "/core_candidate_receipt.json";
            VfxCandidateSandbox.WriteJsonAsset(receiptPath, receipt);
            string outputRoot = VfxQualityEvidenceProtocol.OutputRoot + "/" + sessionId;
            _directoryCleanup.Add(outputRoot);

            return new QualityFixture
            {
                Plan = plan,
                Receipt = receipt,
                PrefabPath = localPrefab.AssetPath,
                Request = new VfxQualityCaptureRequest
                {
                    GenerationPlanPath = planPath,
                    CandidateReceiptPath = receiptPath,
                    CandidateContexts = new List<VfxQualityCandidateContext>
                    {
                        new VfxQualityCandidateContext
                        {
                            CandidateId = plan.Candidates[0].CandidateId,
                            LifecycleMode = "OneShot",
                            DurationSeconds = 0.5f,
                            TargetScaleMeters = 3f
                        }
                    }
                }
            };
        }

        private static VfxQualityProfileTransport LoadProfile()
        {
            Assert.That(VfxQualityProfileLoader.TryLoad(VfxCandidateProtocol.GenerationTemplateCatalogPath, "windows.d3d12.1080p60", "1", out VfxQualityProfileTransport profile, out string error), Is.True, error);
            return profile;
        }

        private sealed class QualityFixture
        {
            public VfxCoreGenerationPlan Plan;
            public VfxCoreCandidateReceipt Receipt;
            public string PrefabPath;
            public VfxQualityCaptureRequest Request;
        }

        private sealed class FakeCaptureBackend : IVfxQualityCaptureBackend
        {
            private VfxQualityCandidateContext _context;
            public int EndCount { get; private set; }
            public bool Disposed { get; private set; }
            public string ProviderId => "test.capture";
            public string ProviderVersion => "1";

            public void BeginCandidate(VfxQualityCandidateContext context, string prefabAssetPath, VfxQualityProfileTransport profile) => _context = context;

            public VfxQualityFrameObservation Capture(VfxQualityCapturePoint point)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(point.CandidateId + "|" + point.Seed + "|" + point.ViewId + "|" + point.BackgroundId + "|" + point.PhaseId);
                string parent = Path.GetDirectoryName(point.OutputPath);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                File.WriteAllBytes(point.OutputPath, bytes);
                return new VfxQualityFrameObservation
                {
                    Status = "Measured",
                    OutputPath = point.OutputPath,
                    ContentHash = VfxCandidateSandbox.ComputeHash(bytes),
                    AnchorMeasured = true,
                    AnchorErrorPercent = 0f,
                    PathMeasured = _context.RequiresPathMeasurement,
                    PathEndpointErrorPercent = 0f,
                    TelegraphMeasured = _context.RequiresTelegraphMeasurement && point.ViewId == "Top",
                    TelegraphFootprintIou = 1f,
                    AlphaCoverage = point.SimulationSeconds,
                    VisibleRangeMeters = 3f
                };
            }

            public VfxQualityLifecycleObservation MeasureLifecycle(VfxQualityCandidateContext context, VfxQualityProfileTransport profile)
            {
                return new VfxQualityLifecycleObservation
                {
                    Status = "Measured",
                    ActiveParticlesAfterCleanup = 0,
                    BrightPixelPercentAfterCleanup = 0f,
                    GhostCyclesExecuted = profile.GhostCycleCount,
                    GhostCount = 0
                };
            }

            public void EndCandidate() { EndCount++; _context = null; }
            public void Dispose() => Disposed = true;
        }

        private sealed class FakeMeasurementProvider : IVfxQualityMeasurementProvider
        {
            private readonly bool _measured;
            public FakeMeasurementProvider(bool measured) => _measured = measured;
            public bool Disposed { get; private set; }
            public string ProviderId => "test.measurement";
            public string ProviderVersion => "1";

            public VfxQualityPerformanceObservation Measure(VfxQualityCandidateContext context, string prefabAssetPath, VfxQualityProfileTransport profile)
            {
                return _measured
                    ? new VfxQualityPerformanceObservation { Status = "Measured", CpuP95Milliseconds = 0.1f, GpuP95Milliseconds = 0.2f, SteadyStateGcBytesPerFrame = 0L }
                    : new VfxQualityPerformanceObservation { Status = "NotMeasured", CpuP95Milliseconds = -1f, GpuP95Milliseconds = -1f, SteadyStateGcBytesPerFrame = -1L };
            }

            public void Dispose() => Disposed = true;
        }
    }
}
