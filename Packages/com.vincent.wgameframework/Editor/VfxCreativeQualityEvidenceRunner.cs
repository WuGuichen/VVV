using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>Runs the formal, profile-bound Unity candidate evidence matrix and exports exact Core evaluation input.</summary>
    /// <remarks>Editor-only and AllocByDesign. This runner owns and disposes its capture and measurement providers.</remarks>
    internal sealed class VfxQualityEvidenceCaptureRunner : IDisposable
    {
        private readonly IVfxQualityCaptureBackend _captureBackend;
        private readonly IVfxQualityMeasurementProvider _measurementProvider;
        private bool _disposed;

        public VfxQualityEvidenceCaptureRunner(IVfxQualityCaptureBackend captureBackend, IVfxQualityMeasurementProvider measurementProvider)
        {
            _captureBackend = captureBackend ?? throw new ArgumentNullException(nameof(captureBackend));
            _measurementProvider = measurementProvider ?? throw new ArgumentNullException(nameof(measurementProvider));
        }

        /// <remarks>AllocByDesign. Writes PNG/JSON evidence only below Temp/MxFramework/VfxAuthoring.</remarks>
        public VfxQualityCaptureResult Capture(VfxQualityCaptureRequest request)
        {
            if (_disposed)
                return Failure("VFX quality evidence runner is disposed.");
            try
            {
                if (request == null)
                    throw new InvalidOperationException("VFX quality capture request is required.");
                VfxCoreGenerationPlan plan = LoadAndValidatePlan(request.GenerationPlanPath);
                VfxCoreCandidateReceipt receipt = LoadAndValidateReceipt(request.CandidateReceiptPath, plan);
                if (!VfxQualityProfileLoader.TryLoad(request.GenerationTemplateCatalogPath, plan.QualityProfileId, plan.QualityProfileVersion, out VfxQualityProfileTransport profile, out string profileError))
                    throw new InvalidOperationException(profileError);
                ValidatePlanSeeds(plan, profile);
                Dictionary<string, VfxQualityCandidateContext> contexts = BuildContexts(request.CandidateContexts, plan);
                Dictionary<string, string> prefabPaths = ResolveCandidatePrefabs(receipt, plan);

                var manifest = new VfxQualityEvidenceManifest
                {
                    GenerationPlanHash = plan.PlanHash,
                    CandidateReceiptHash = receipt.ReceiptHash,
                    QualityProfileId = profile.QualityProfileId,
                    QualityProfileVersion = profile.Version,
                    CaptureProviderId = _captureBackend.ProviderId,
                    CaptureProviderVersion = _captureBackend.ProviderVersion,
                    MeasurementProviderId = _measurementProvider.ProviderId,
                    MeasurementProviderVersion = _measurementProvider.ProviderVersion,
                    ReviewStatus = VfxQualityEvidenceProtocol.ReviewStatus
                };

                var logCollector = new VfxQualityLogCollector();
                try
                {
                    for (int i = 0; i < plan.Candidates.Count; i++)
                    {
                        VfxCoreGenerationCandidateSpec candidate = plan.Candidates[i];
                        manifest.Candidates.Add(CaptureCandidate(plan.SessionId, candidate, contexts[candidate.CandidateId], prefabPaths[candidate.CandidateId], profile, logCollector));
                    }
                }
                finally
                {
                    logCollector.Dispose();
                }
                manifest.Candidates.Sort((left, right) => string.Compare(left.CandidateId, right.CandidateId, StringComparison.Ordinal));
                ApplyFallbackGates(manifest.Candidates, contexts, profile);
                manifest.Complete = IsComplete(manifest.Candidates);
                manifest.ManifestHash = VfxCandidateSandbox.ComputeCoreDocumentHash(manifest, "manifestHash");

                VfxCoreEvaluationInputTransport evaluationInput = BuildEvaluationInput(request, plan, receipt, manifest);
                string outputRoot = VfxQualityEvidenceProtocol.OutputRoot + "/" + VfxQualityCaptureMatrix.SafeSegment(plan.SessionId);
                string manifestPath = outputRoot + "/quality_evidence_manifest.json";
                string inputPath = outputRoot + "/evaluation_input.json";
                VfxCandidateSandbox.WriteJsonFile(manifestPath, manifest);
                VfxCandidateSandbox.WriteJsonFile(inputPath, evaluationInput);
                return new VfxQualityCaptureResult
                {
                    Success = true,
                    Complete = manifest.Complete,
                    Message = manifest.Complete
                        ? "Formal machine evidence captured. Human reviewerScores remain empty and review status remains Draft."
                        : "Fail-closed machine evidence exported with one or more NotMeasured gates. Human reviewerScores remain empty and review status remains Draft.",
                    EvidenceManifestPath = manifestPath,
                    EvidenceManifestHash = manifest.ManifestHash,
                    EvaluationInputPath = inputPath,
                    EvaluationInputHash = VfxCandidateSandbox.ComputeAssetFileHash(inputPath),
                    Manifest = manifest,
                    EvaluationInput = evaluationInput
                };
            }
            catch (Exception exception)
            {
                return Failure(exception.Message);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _captureBackend.Dispose();
            _measurementProvider.Dispose();
        }

        private VfxQualityCandidateEvidence CaptureCandidate(
            string sessionId,
            VfxCoreGenerationCandidateSpec candidate,
            VfxQualityCandidateContext context,
            string prefabPath,
            VfxQualityProfileTransport profile,
            VfxQualityLogCollector logCollector)
        {
            var evidence = new VfxQualityCandidateEvidence
            {
                CandidateId = candidate.CandidateId,
                LifecycleMode = context.LifecycleMode,
                PrefabAssetPath = prefabPath
            };
            List<VfxQualityCapturePoint> matrix = VfxQualityCaptureMatrix.Build(sessionId, context, profile);
            int errorsBefore = logCollector.ErrorCount;
            bool began = false;
            string beginError = string.Empty;
            try
            {
                _captureBackend.BeginCandidate(context, prefabPath, profile);
                began = true;
            }
            catch (Exception exception)
            {
                beginError = exception.Message;
            }

            try
            {
                for (int i = 0; i < matrix.Count; i++)
                {
                    VfxQualityFrameObservation observation;
                    if (!began)
                    {
                        observation = NotMeasuredFrame(matrix[i], beginError);
                    }
                    else
                    {
                        try { observation = _captureBackend.Capture(matrix[i]) ?? NotMeasuredFrame(matrix[i], "Capture provider returned no observation."); }
                        catch (Exception exception) { observation = NotMeasuredFrame(matrix[i], exception.Message); }
                    }
                    evidence.Captures.Add(ToRecord(matrix[i], observation));
                }
                if (began)
                {
                    try { evidence.Lifecycle = _captureBackend.MeasureLifecycle(context, profile) ?? new VfxQualityLifecycleObservation { Message = "Capture provider returned no lifecycle observation." }; }
                    catch (Exception exception) { evidence.Lifecycle = new VfxQualityLifecycleObservation { Message = exception.Message }; }
                }
                else evidence.Lifecycle.Message = beginError;
                try { evidence.Performance = _measurementProvider.Measure(context, prefabPath, profile) ?? new VfxQualityPerformanceObservation { Message = "Measurement provider returned no observation." }; }
                catch (Exception exception) { evidence.Performance = new VfxQualityPerformanceObservation { Message = exception.Message }; }
            }
            finally
            {
                try { _captureBackend.EndCandidate(); }
                catch (Exception exception)
                {
                    evidence.Lifecycle.Status = VfxQualityEvidenceProtocol.NotMeasured;
                    evidence.Lifecycle.Message = "EndCandidate failed: " + exception.Message;
                }
            }

            BuildNonFallbackGates(evidence, context, profile, logCollector.ErrorCount - errorsBefore);
            return evidence;
        }

        private static void BuildNonFallbackGates(VfxQualityCandidateEvidence evidence, VfxQualityCandidateContext context, VfxQualityProfileTransport profile, int loggedErrors)
        {
            bool framesMeasured = evidence.Captures.Count > 0;
            int missing = 0, magenta = 0, nonFinite = 0, renderErrors = Math.Max(0, loggedErrors);
            float maximumCrop = 0f, maximumAnchor = 0f, maximumPath = 0f, minimumIou = 1f;
            bool anchorMeasured = true, pathMeasured = !context.RequiresPathMeasurement, telegraphMeasured = !context.RequiresTelegraphMeasurement;
            for (int i = 0; i < evidence.Captures.Count; i++)
            {
                VfxQualityCaptureRecord capture = evidence.Captures[i];
                if (!string.Equals(capture.Status, VfxQualityEvidenceProtocol.Measured, StringComparison.Ordinal)) framesMeasured = false;
                missing += capture.MissingFrameCount;
                magenta += capture.MagentaPixelCount;
                nonFinite += capture.NonFinitePixelCount;
                renderErrors += capture.RenderErrorCount;
                maximumCrop = Mathf.Max(maximumCrop, capture.CropPercent);
                anchorMeasured &= capture.AnchorMeasured;
                if (capture.AnchorMeasured) maximumAnchor = Mathf.Max(maximumAnchor, capture.AnchorErrorPercent);
                if (context.RequiresPathMeasurement && capture.PathMeasured) { pathMeasured = true; maximumPath = Mathf.Max(maximumPath, capture.PathEndpointErrorPercent); }
                if (context.RequiresTelegraphMeasurement && capture.TelegraphMeasured) { telegraphMeasured = true; minimumIou = Mathf.Min(minimumIou, capture.TelegraphFootprintIou); }
            }

            AddGate(evidence, "NoConsoleOrRenderErrors", VfxQualityEvidenceProtocol.Measured, renderErrors == 0,
                renderErrors == 0 ? "No Console or render errors were observed during the bounded capture session." : "Console/render errors were observed.", "errorCount", renderErrors);
            AddGate(evidence, "NoMagentaMaterial", framesMeasured ? VfxQualityEvidenceProtocol.Measured : VfxQualityEvidenceProtocol.NotMeasured,
                framesMeasured && magenta == 0, magenta == 0 ? "No magenta/error-shader pixels were observed." : "Magenta/error-shader pixels were observed.", "magentaPixelCount", magenta);
            AddGate(evidence, "NoNanOrMissingFrames", framesMeasured ? VfxQualityEvidenceProtocol.Measured : VfxQualityEvidenceProtocol.NotMeasured,
                framesMeasured && missing == 0 && nonFinite == 0, "All expected frames must exist and contain finite pixel values.", "missingFrameCount", missing, "nonFinitePixelCount", nonFinite);
            AddGate(evidence, "CropWithinLimit", framesMeasured ? VfxQualityEvidenceProtocol.Measured : VfxQualityEvidenceProtocol.NotMeasured,
                framesMeasured && maximumCrop <= profile.MaximumCropPercent, "Maximum cropped foreground must remain within the profile threshold.", "maximumCropPercent", maximumCrop, "limitPercent", profile.MaximumCropPercent);
            AddGate(evidence, "AnchorWithinLimit", anchorMeasured && framesMeasured ? VfxQualityEvidenceProtocol.Measured : VfxQualityEvidenceProtocol.NotMeasured,
                anchorMeasured && framesMeasured && maximumAnchor <= profile.MaximumAnchorErrorPercent, "Anchor displacement is normalized by the scenario target scale.", "maximumAnchorErrorPercent", maximumAnchor, "limitPercent", profile.MaximumAnchorErrorPercent);

            if (context.RequiresPathMeasurement)
                AddGate(evidence, "PathEndpointWithinLimit", pathMeasured && framesMeasured ? VfxQualityEvidenceProtocol.Measured : VfxQualityEvidenceProtocol.NotMeasured,
                    pathMeasured && framesMeasured && maximumPath <= profile.MaximumPathEndpointErrorPercent, "Path endpoint error is normalized by the expected path length.", "maximumPathEndpointErrorPercent", maximumPath, "limitPercent", profile.MaximumPathEndpointErrorPercent);
            else AddGate(evidence, "PathEndpointWithinLimit", VfxQualityEvidenceProtocol.NotApplicable, true, "Candidate scenario does not declare a path endpoint contract.", "applicable", false);

            if (context.RequiresTelegraphMeasurement)
                AddGate(evidence, "TelegraphFootprint", telegraphMeasured && framesMeasured ? VfxQualityEvidenceProtocol.Measured : VfxQualityEvidenceProtocol.NotMeasured,
                    telegraphMeasured && framesMeasured && minimumIou >= profile.MinimumTelegraphFootprintIou, "Telegraph footprint IoU must meet the versioned profile threshold.", "minimumIou", minimumIou, "limitIou", profile.MinimumTelegraphFootprintIou);
            else AddGate(evidence, "TelegraphFootprint", VfxQualityEvidenceProtocol.NotApplicable, true, "Candidate scenario is not a gameplay telegraph.", "applicable", false);

            bool lifecycleMeasured = string.Equals(evidence.Lifecycle.Status, VfxQualityEvidenceProtocol.Measured, StringComparison.Ordinal);
            AddGate(evidence, "CleanupWithinOneSecond", lifecycleMeasured ? VfxQualityEvidenceProtocol.Measured : VfxQualityEvidenceProtocol.NotMeasured,
                lifecycleMeasured && evidence.Lifecycle.ActiveParticlesAfterCleanup <= profile.MaximumActiveParticlesAfterCleanup
                && evidence.Lifecycle.BrightPixelPercentAfterCleanup <= profile.MaximumBrightPixelPercentAfterCleanup,
                "Stop/cleanup evidence covers active particles and visible residuals.", "activeParticles", evidence.Lifecycle.ActiveParticlesAfterCleanup,
                "brightPixelPercent", evidence.Lifecycle.BrightPixelPercentAfterCleanup);
            AddGate(evidence, "NoGhostAfterTwentyCycles", lifecycleMeasured ? VfxQualityEvidenceProtocol.Measured : VfxQualityEvidenceProtocol.NotMeasured,
                lifecycleMeasured && evidence.Lifecycle.GhostCyclesExecuted == profile.GhostCycleCount && evidence.Lifecycle.GhostCount == 0,
                "Repeated Play/Stop evidence must execute the profile cycle count without ghosts.", "cycles", evidence.Lifecycle.GhostCyclesExecuted, "ghostCount", evidence.Lifecycle.GhostCount);

            bool performanceMeasured = string.Equals(evidence.Performance.Status, VfxQualityEvidenceProtocol.Measured, StringComparison.Ordinal);
            AddGate(evidence, "ZeroSteadyStateGc", performanceMeasured ? VfxQualityEvidenceProtocol.Measured : VfxQualityEvidenceProtocol.NotMeasured,
                performanceMeasured && evidence.Performance.SteadyStateGcBytesPerFrame >= 0 && evidence.Performance.SteadyStateGcBytesPerFrame <= profile.MaximumSteadyStateGcBytesPerFrame,
                "Steady-state GC is supplied by the versioned target-platform measurement provider.", "gcBytesPerFrame", evidence.Performance.SteadyStateGcBytesPerFrame,
                "limitBytesPerFrame", profile.MaximumSteadyStateGcBytesPerFrame);
        }

        private static void ApplyFallbackGates(List<VfxQualityCandidateEvidence> candidates, Dictionary<string, VfxQualityCandidateContext> contexts, VfxQualityProfileTransport profile)
        {
            var byId = new Dictionary<string, VfxQualityCandidateEvidence>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++) byId[candidates[i].CandidateId] = candidates[i];
            for (int i = 0; i < candidates.Count; i++)
            {
                VfxQualityCandidateEvidence candidate = candidates[i];
                VfxQualityCandidateContext context = contexts[candidate.CandidateId];
                if (string.IsNullOrWhiteSpace(context.FallbackCandidateId))
                {
                    AddGate(candidate, "FallbackTimingAndRange", VfxQualityEvidenceProtocol.NotApplicable, true, "Candidate scenario declares no Graph/fallback comparison.", "applicable", false);
                    continue;
                }
                if (!byId.TryGetValue(context.FallbackCandidateId, out VfxQualityCandidateEvidence fallback)
                    || !TryPeakAndRange(candidate, out float candidatePeak, out float candidateRange)
                    || !TryPeakAndRange(fallback, out float fallbackPeak, out float fallbackRange))
                {
                    AddGate(candidate, "FallbackTimingAndRange", VfxQualityEvidenceProtocol.NotMeasured, false, "Fallback pair is missing or lacks complete capture evidence.", "fallbackCandidateId", context.FallbackCandidateId);
                    continue;
                }
                float peakDelta = PercentDelta(candidatePeak, fallbackPeak);
                float rangeDelta = PercentDelta(candidateRange, fallbackRange);
                AddGate(candidate, "FallbackTimingAndRange", VfxQualityEvidenceProtocol.Measured,
                    peakDelta <= profile.MaximumFallbackPeakTimeDeltaPercent && rangeDelta <= profile.MaximumFallbackVisibleRangeDeltaPercent,
                    "Graph and fallback peak timing/visible range are compared from the same profile matrix.", "peakTimeDeltaPercent", peakDelta,
                    "rangeDeltaPercent", rangeDelta, "fallbackCandidateId", context.FallbackCandidateId);
            }
        }

        private static bool TryPeakAndRange(VfxQualityCandidateEvidence evidence, out float peakTime, out float visibleRange)
        {
            peakTime = 0f;
            visibleRange = 0f;
            float peakCoverage = -1f;
            bool any = false;
            for (int i = 0; i < evidence.Captures.Count; i++)
            {
                VfxQualityCaptureRecord capture = evidence.Captures[i];
                if (!string.Equals(capture.Status, VfxQualityEvidenceProtocol.Measured, StringComparison.Ordinal)) return false;
                any = true;
                visibleRange = Mathf.Max(visibleRange, capture.VisibleRangeMeters);
                if (capture.AlphaCoverage > peakCoverage)
                {
                    peakCoverage = capture.AlphaCoverage;
                    peakTime = capture.Point.SimulationSeconds;
                }
            }
            return any && visibleRange >= 0f;
        }

        private static float PercentDelta(float left, float right)
        {
            float denominator = Mathf.Max(Mathf.Abs(left), Mathf.Abs(right), 0.0001f);
            return Mathf.Abs(left - right) / denominator * 100f;
        }

        private static void AddGate(VfxQualityCandidateEvidence candidate, string gateId, string status, bool passed, string message, params object[] metrics)
        {
            for (int i = candidate.HardGates.Count - 1; i >= 0; i--)
                if (string.Equals(candidate.HardGates[i].GateId, gateId, StringComparison.Ordinal)) candidate.HardGates.RemoveAt(i);
            var gate = new VfxQualityGateEvidence { GateId = gateId, Status = status, Passed = passed, Message = message };
            for (int i = 0; i + 1 < metrics.Length; i += 2)
                gate.Metrics[Convert.ToString(metrics[i], CultureInfo.InvariantCulture)] = ToInvariant(metrics[i + 1]);
            gate.EvidenceHash = VfxCandidateSandbox.ComputeCoreDocumentHash(gate, "evidenceHash");
            candidate.HardGates.Add(gate);
            candidate.HardGates.Sort((left, right) => string.Compare(left.GateId, right.GateId, StringComparison.Ordinal));
        }

        private static string ToInvariant(object value)
        {
            if (value is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture);
            return value?.ToString() ?? string.Empty;
        }

        private static VfxCoreEvaluationInputTransport BuildEvaluationInput(VfxQualityCaptureRequest request, VfxCoreGenerationPlan plan, VfxCoreCandidateReceipt receipt, VfxQualityEvidenceManifest manifest)
        {
            var input = new VfxCoreEvaluationInputTransport
            {
                GenerationPlanHash = plan.PlanHash,
                CandidateReceiptHash = receipt.ReceiptHash,
                QualityProfileId = manifest.QualityProfileId,
                QualityProfileVersion = manifest.QualityProfileVersion,
                PreviousMedianScore = request.PreviousMedianScore,
                ConsecutiveLowImprovementRounds = request.ConsecutiveLowImprovementRounds
            };
            for (int i = 0; i < manifest.Candidates.Count; i++)
            {
                VfxQualityCandidateEvidence evidence = manifest.Candidates[i];
                var candidate = new VfxCoreCandidateEvaluationInputTransport
                {
                    CandidateId = evidence.CandidateId,
                    CpuP95Milliseconds = evidence.Performance.CpuP95Milliseconds,
                    GpuP95Milliseconds = evidence.Performance.GpuP95Milliseconds
                };
                for (int gateIndex = 0; gateIndex < evidence.HardGates.Count; gateIndex++)
                {
                    VfxQualityGateEvidence gate = evidence.HardGates[gateIndex];
                    candidate.HardGates.Add(new VfxCoreHardGateInput { GateId = gate.GateId, Passed = gate.Passed, EvidenceHash = gate.EvidenceHash, Message = gate.Message });
                }
                input.Candidates.Add(candidate);
            }
            input.Candidates.Sort((left, right) => string.Compare(left.CandidateId, right.CandidateId, StringComparison.Ordinal));
            return input;
        }

        private static bool IsComplete(List<VfxQualityCandidateEvidence> candidates)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!string.Equals(candidates[i].Performance.Status, VfxQualityEvidenceProtocol.Measured, StringComparison.Ordinal)) return false;
                for (int gate = 0; gate < candidates[i].HardGates.Count; gate++)
                    if (string.Equals(candidates[i].HardGates[gate].Status, VfxQualityEvidenceProtocol.NotMeasured, StringComparison.Ordinal)) return false;
            }
            return candidates.Count > 0;
        }

        private static VfxCoreGenerationPlan LoadAndValidatePlan(string path)
        {
            string normalized = VfxCandidateSandbox.NormalizeAssetPath(path);
            if (!VfxCandidateSandbox.IsControlledEvidencePath(normalized) || !File.Exists(normalized))
                throw new InvalidOperationException("Generation plan must be an existing controlled Authoring evidence file.");
            JObject document = JObject.Parse(File.ReadAllText(normalized));
            VfxCoreGenerationPlan plan = document.ToObject<VfxCoreGenerationPlan>();
            VfxCoreCandidateReceiptAdapter.ValidatePlan(plan, document);
            return plan;
        }

        private static VfxCoreCandidateReceipt LoadAndValidateReceipt(string path, VfxCoreGenerationPlan plan)
        {
            string normalized = VfxCandidateSandbox.NormalizeAssetPath(path);
            if (!VfxCandidateSandbox.IsCandidateRoot(normalized) || !File.Exists(normalized))
                throw new InvalidOperationException("Candidate receipt must be an existing Unity candidate sandbox asset.");
            JObject document = JObject.Parse(File.ReadAllText(normalized));
            VfxCoreCandidateReceipt receipt = document.ToObject<VfxCoreCandidateReceipt>();
            string hash = VfxCandidateSandbox.ComputeCoreDocumentHash(document, "receiptHash");
            if (receipt == null || !string.Equals(receipt.SchemaVersion, VfxCandidateProtocol.CoreCandidateReceiptSchemaVersion, StringComparison.Ordinal)
                || !VfxCandidateSandbox.IsSha256(receipt.ReceiptHash) || !string.Equals(receipt.ReceiptHash, hash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(receipt.SessionId, plan.SessionId, StringComparison.Ordinal)
                || !string.Equals(receipt.GenerationPlanHash, plan.PlanHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(receipt.CatalogHash, plan.CatalogHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(receipt.GenerationTemplateCatalogHash, plan.GenerationTemplateCatalogHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Candidate receipt is invalid, stale, or bound to a different plan/Catalog.");
            for (int i = 0; i < receipt.Assets.Count; i++)
            {
                VfxCoreCandidateAssetRecord asset = receipt.Assets[i];
                string assetPath = VfxCandidateSandbox.NormalizeAssetPath(asset?.AssetPath);
                if (asset == null || !VfxCandidateSandbox.IsCandidateRoot(assetPath) || !File.Exists(assetPath)
                    || !string.Equals(VfxCandidateSandbox.ComputeAssetFileHash(assetPath), asset.ContentHash, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(AssetDatabase.AssetPathToGUID(assetPath), asset.UnityGuid, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(AssetDatabase.GetAssetDependencyHash(assetPath).ToString(), asset.DependencyHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Candidate receipt asset content/GUID/dependency evidence is missing or stale: " + assetPath);
            }
            return receipt;
        }

        private static Dictionary<string, VfxQualityCandidateContext> BuildContexts(List<VfxQualityCandidateContext> supplied, VfxCoreGenerationPlan plan)
        {
            var result = new Dictionary<string, VfxQualityCandidateContext>(StringComparer.Ordinal);
            if (supplied != null)
                for (int i = 0; i < supplied.Count; i++)
                {
                    VfxQualityCandidateContext context = supplied[i];
                    if (context == null || string.IsNullOrWhiteSpace(context.CandidateId) || result.ContainsKey(context.CandidateId))
                        throw new InvalidOperationException("Candidate quality contexts require unique stable candidate ids.");
                    if (context.DurationSeconds <= 0f || context.TargetScaleMeters <= 0f)
                        throw new InvalidOperationException("Candidate quality context duration/target scale must be positive: " + context.CandidateId);
                    result.Add(context.CandidateId, context);
                }
            for (int i = 0; i < plan.Candidates.Count; i++)
                if (!result.ContainsKey(plan.Candidates[i].CandidateId))
                    throw new InvalidOperationException("Formal quality capture requires explicit lifecycle/anchor scenario context for candidate: " + plan.Candidates[i].CandidateId);
            return result;
        }

        private static Dictionary<string, string> ResolveCandidatePrefabs(VfxCoreCandidateReceipt receipt, VfxCoreGenerationPlan plan)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < receipt.Assets.Count; i++)
            {
                VfxCoreCandidateAssetRecord asset = receipt.Assets[i];
                if (asset == null || !string.Equals(asset.AssetKind, "Prefab", StringComparison.Ordinal)) continue;
                string path = VfxCandidateSandbox.NormalizeAssetPath(asset.AssetPath);
                if (!VfxCandidateSandbox.IsCandidateRoot(path) || !File.Exists(path)
                    || !string.Equals(VfxCandidateSandbox.ComputeAssetFileHash(path), asset.ContentHash, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(AssetDatabase.AssetPathToGUID(path), asset.UnityGuid, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(AssetDatabase.GetAssetDependencyHash(path).ToString(), asset.DependencyHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Candidate Prefab evidence is missing or stale: " + path);
                if (result.ContainsKey(asset.CandidateId)) throw new InvalidOperationException("Candidate has multiple published Prefab evidence records: " + asset.CandidateId);
                result.Add(asset.CandidateId, path);
            }
            for (int i = 0; i < plan.Candidates.Count; i++)
                if (!result.ContainsKey(plan.Candidates[i].CandidateId))
                    throw new InvalidOperationException("Formal Unity quality capture requires a candidate Prefab wrapper: " + plan.Candidates[i].CandidateId);
            return result;
        }

        private static void ValidatePlanSeeds(VfxCoreGenerationPlan plan, VfxQualityProfileTransport profile)
        {
            if (plan.FixedSeeds == null || plan.FixedSeeds.Count != profile.FixedSeeds.Count)
                throw new InvalidOperationException("Generation plan fixed seeds differ from its quality profile.");
            for (int i = 0; i < plan.FixedSeeds.Count; i++)
                if (plan.FixedSeeds[i] != profile.FixedSeeds[i])
                    throw new InvalidOperationException("Generation plan fixed seeds differ from its quality profile.");
        }

        private static VfxQualityCaptureRecord ToRecord(VfxQualityCapturePoint point, VfxQualityFrameObservation observation)
        {
            return new VfxQualityCaptureRecord
            {
                Point = point,
                Status = observation.Status,
                ContentHash = observation.ContentHash,
                MissingFrameCount = observation.MissingFrameCount,
                MagentaPixelCount = observation.MagentaPixelCount,
                NonFinitePixelCount = observation.NonFinitePixelCount,
                RenderErrorCount = observation.RenderErrorCount,
                CropPercent = observation.CropPercent,
                AnchorMeasured = observation.AnchorMeasured,
                AnchorErrorPercent = observation.AnchorErrorPercent,
                PathMeasured = observation.PathMeasured,
                PathEndpointErrorPercent = observation.PathEndpointErrorPercent,
                TelegraphMeasured = observation.TelegraphMeasured,
                TelegraphFootprintIou = observation.TelegraphFootprintIou,
                AlphaCoverage = observation.AlphaCoverage,
                VisibleRangeMeters = observation.VisibleRangeMeters,
                Message = observation.Message
            };
        }

        private static VfxQualityFrameObservation NotMeasuredFrame(VfxQualityCapturePoint point, string message)
        {
            return new VfxQualityFrameObservation
            {
                Status = VfxQualityEvidenceProtocol.NotMeasured,
                Message = message ?? string.Empty,
                OutputPath = point.OutputPath,
                MissingFrameCount = 1
            };
        }

        private static VfxQualityCaptureResult Failure(string message) => new VfxQualityCaptureResult { Success = false, Complete = false, Message = message ?? string.Empty };
    }

    internal sealed class VfxQualityLogCollector : IDisposable
    {
        public int ErrorCount { get; private set; }

        public VfxQualityLogCollector() => Application.logMessageReceived += OnLog;

        public void Dispose() => Application.logMessageReceived -= OnLog;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) ErrorCount++;
        }
    }
}
