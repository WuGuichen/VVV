using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Editor
{
    internal static class VfxQualityEvidenceProtocol
    {
        public const string EvidenceSchemaVersion = "mx.vfx.quality-evidence.v1";
        public const string EvaluationInputSchemaVersion = "mx.vfx.evaluation-input.v1";
        public const string OutputRoot = "Temp/MxFramework/VfxAuthoring/QualityEvidence";
        public const string ReviewStatus = "Draft";
        public const string Measured = "Measured";
        public const string NotApplicable = "NotApplicable";
        public const string NotMeasured = "NotMeasured";

        public static readonly string[] RequiredSeeds = { "17", "73", "211" };
        public static readonly string[] RequiredViews = { "Gameplay", "FrontThreeQuarter", "Side" };
        public static readonly string[] RequiredBackgrounds = { "DarkNeutral", "LightNeutral", "GameplayBusy", "SameColorConflict" };
        public static readonly string[] RequiredOneShotSlices = { "0", "10", "25", "50", "75", "100" };
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxQualityTemplateCatalogTransport
    {
        [JsonProperty("qualityProfiles")] public List<VfxQualityProfileTransport> QualityProfiles { get; set; } = new List<VfxQualityProfileTransport>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxQualityProfileTransport
    {
        [JsonProperty("qualityProfileId", Order = 1)] public string QualityProfileId { get; set; } = string.Empty;
        [JsonProperty("version", Order = 2)] public string Version { get; set; } = string.Empty;
        [JsonProperty("platform", Order = 3)] public string Platform { get; set; } = string.Empty;
        [JsonProperty("graphicsApi", Order = 4)] public string GraphicsApi { get; set; } = string.Empty;
        [JsonProperty("width", Order = 5)] public int Width { get; set; }
        [JsonProperty("height", Order = 6)] public int Height { get; set; }
        [JsonProperty("framesPerSecond", Order = 7)] public int FramesPerSecond { get; set; }
        [JsonProperty("fixedStepSeconds", Order = 8)] public double FixedStepSeconds { get; set; }
        [JsonProperty("fixedSeeds", Order = 9)] public List<int> FixedSeeds { get; set; } = new List<int>();
        [JsonProperty("oneShotSlicePercents", Order = 10)] public List<int> OneShotSlicePercents { get; set; } = new List<int>();
        [JsonProperty("loopWarmupSeconds", Order = 11)] public float LoopWarmupSeconds { get; set; }
        [JsonProperty("loopSteadySeconds", Order = 12)] public float LoopSteadySeconds { get; set; }
        [JsonProperty("loopCleanupSeconds", Order = 13)] public float LoopCleanupSeconds { get; set; }
        [JsonProperty("captureViewIds", Order = 14)] public List<string> CaptureViewIds { get; set; } = new List<string>();
        [JsonProperty("captureBackgroundIds", Order = 15)] public List<string> CaptureBackgroundIds { get; set; } = new List<string>();
        [JsonProperty("maximumCropPercent", Order = 16)] public float MaximumCropPercent { get; set; }
        [JsonProperty("maximumAnchorErrorPercent", Order = 17)] public float MaximumAnchorErrorPercent { get; set; }
        [JsonProperty("maximumPathEndpointErrorPercent", Order = 18)] public float MaximumPathEndpointErrorPercent { get; set; }
        [JsonProperty("minimumTelegraphFootprintIou", Order = 19)] public float MinimumTelegraphFootprintIou { get; set; }
        [JsonProperty("maximumFallbackPeakTimeDeltaPercent", Order = 20)] public float MaximumFallbackPeakTimeDeltaPercent { get; set; }
        [JsonProperty("maximumFallbackVisibleRangeDeltaPercent", Order = 21)] public float MaximumFallbackVisibleRangeDeltaPercent { get; set; }
        [JsonProperty("maximumActiveParticlesAfterCleanup", Order = 22)] public int MaximumActiveParticlesAfterCleanup { get; set; }
        [JsonProperty("maximumBrightPixelPercentAfterCleanup", Order = 23)] public float MaximumBrightPixelPercentAfterCleanup { get; set; }
        [JsonProperty("ghostCycleCount", Order = 24)] public int GhostCycleCount { get; set; }
        [JsonProperty("maximumSteadyStateGcBytesPerFrame", Order = 25)] public int MaximumSteadyStateGcBytesPerFrame { get; set; }
        [JsonProperty("performanceWarmupFrames", Order = 26)] public int PerformanceWarmupFrames { get; set; }
        [JsonProperty("performanceSampleFrames", Order = 27)] public int PerformanceSampleFrames { get; set; }
        [JsonProperty("performanceTierBudgets", Order = 28)] public List<VfxQualityPerformanceBudgetTransport> PerformanceTierBudgets { get; set; } = new List<VfxQualityPerformanceBudgetTransport>();
        [JsonProperty("requiredHardGateIds", Order = 29)] public List<string> RequiredHardGateIds { get; set; } = new List<string>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxQualityPerformanceBudgetTransport
    {
        [JsonProperty("costTier")] public string CostTier { get; set; } = string.Empty;
        [JsonProperty("cpuP95Milliseconds")] public float CpuP95Milliseconds { get; set; }
        [JsonProperty("gpuP95Milliseconds")] public float GpuP95Milliseconds { get; set; }
    }

    internal static class VfxQualityProfileLoader
    {
        /// <remarks>LowFreqAlloc. Reads the Authoring-only generation template Catalog.</remarks>
        public static bool TryLoad(string path, string profileId, string version, out VfxQualityProfileTransport profile, out string error)
        {
            profile = null;
            error = string.Empty;
            try
            {
                string normalized = VfxCandidateSandbox.NormalizeAssetPath(path);
                if (!string.Equals(normalized, VfxCandidateProtocol.GenerationTemplateCatalogPath, StringComparison.Ordinal) || !File.Exists(normalized))
                    throw new InvalidOperationException("Quality profile must come from the authoritative VFX generation template Catalog.");
                VfxQualityTemplateCatalogTransport catalog = JsonConvert.DeserializeObject<VfxQualityTemplateCatalogTransport>(File.ReadAllText(normalized));
                if (catalog?.QualityProfiles == null)
                    throw new InvalidOperationException("Generation template Catalog has no quality profiles.");
                for (int i = 0; i < catalog.QualityProfiles.Count; i++)
                {
                    VfxQualityProfileTransport candidate = catalog.QualityProfiles[i];
                    if (candidate != null && string.Equals(candidate.QualityProfileId, profileId, StringComparison.Ordinal)
                        && string.Equals(candidate.Version, version, StringComparison.Ordinal))
                    {
                        ValidateRequiredContract(candidate);
                        profile = candidate;
                        return true;
                    }
                }
                throw new InvalidOperationException("Generation template Catalog does not contain the plan-bound quality profile/version.");
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        internal static void ValidateRequiredContract(VfxQualityProfileTransport profile)
        {
            if (profile.FramesPerSecond != 60 || Math.Abs(profile.FixedStepSeconds - 1d / 60d) > 0.000000001d)
                throw new InvalidOperationException("Formal VFX quality evidence requires a fixed 1/60 second simulation step.");
            RequireExactInts(profile.FixedSeeds, new[] { 17, 73, 211 }, "fixed seeds");
            RequireExactInts(profile.OneShotSlicePercents, new[] { 0, 10, 25, 50, 75, 100 }, "OneShot slices");
            RequireContains(profile.CaptureViewIds, VfxQualityEvidenceProtocol.RequiredViews, "capture views");
            RequireContains(profile.CaptureBackgroundIds, VfxQualityEvidenceProtocol.RequiredBackgrounds, "capture backgrounds");
            if (profile.GhostCycleCount != 20 || profile.LoopCleanupSeconds < 1f)
                throw new InvalidOperationException("Formal VFX quality evidence requires 20 Play/Stop cycles and at least one second cleanup.");
        }

        private static void RequireExactInts(List<int> actual, int[] expected, string label)
        {
            if (actual == null || actual.Count != expected.Length)
                throw new InvalidOperationException("Quality profile has an invalid " + label + " contract.");
            for (int i = 0; i < expected.Length; i++)
                if (actual[i] != expected[i])
                    throw new InvalidOperationException("Quality profile has an invalid " + label + " contract.");
        }

        private static void RequireContains(List<string> actual, string[] expected, string label)
        {
            if (actual == null)
                throw new InvalidOperationException("Quality profile has no " + label + ".");
            for (int i = 0; i < expected.Length; i++)
                if (!actual.Contains(expected[i]))
                    throw new InvalidOperationException("Quality profile is missing required " + label + ": " + expected[i]);
        }
    }

    internal sealed class VfxQualityCandidateContext
    {
        public string CandidateId { get; set; } = string.Empty;
        public string LifecycleMode { get; set; } = "OneShot";
        public float DurationSeconds { get; set; } = 1f;
        public float TargetScaleMeters { get; set; } = 3f;
        public bool RequiresPathMeasurement { get; set; }
        public Vector3 ExpectedPathStart { get; set; } = Vector3.zero;
        public Vector3 ExpectedPathEnd { get; set; } = Vector3.forward;
        public bool RequiresTelegraphMeasurement { get; set; }
        public float TelegraphRadiusNormalized { get; set; } = 0.25f;
        public string FallbackCandidateId { get; set; } = string.Empty;
        public bool IncludePathView { get; set; }
        public bool IncludeTopView { get; set; }
    }

    internal sealed class VfxQualityCaptureRequest
    {
        public string GenerationPlanPath { get; set; } = string.Empty;
        public string CandidateReceiptPath { get; set; } = string.Empty;
        public string GenerationTemplateCatalogPath { get; set; } = VfxCandidateProtocol.GenerationTemplateCatalogPath;
        public int PreviousMedianScore { get; set; }
        public int ConsecutiveLowImprovementRounds { get; set; }
        public List<VfxQualityCandidateContext> CandidateContexts { get; set; } = new List<VfxQualityCandidateContext>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxQualityCapturePoint
    {
        [JsonProperty("candidateId", Order = 1)] public string CandidateId { get; set; } = string.Empty;
        [JsonProperty("seed", Order = 2)] public int Seed { get; set; }
        [JsonProperty("viewId", Order = 3)] public string ViewId { get; set; } = string.Empty;
        [JsonProperty("backgroundId", Order = 4)] public string BackgroundId { get; set; } = string.Empty;
        [JsonProperty("phaseId", Order = 5)] public string PhaseId { get; set; } = string.Empty;
        [JsonProperty("simulationSeconds", Order = 6)] public float SimulationSeconds { get; set; }
        [JsonProperty("simulationFrame", Order = 7)] public int SimulationFrame { get; set; }
        [JsonProperty("lifecycleAction", Order = 8)] public string LifecycleAction { get; set; } = "Play";
        [JsonProperty("outputPath", Order = 9)] public string OutputPath { get; set; } = string.Empty;
    }

    internal static class VfxQualityCaptureMatrix
    {
        /// <remarks>AllocByDesign. Creates the deterministic, profile-bound capture matrix.</remarks>
        public static List<VfxQualityCapturePoint> Build(string sessionId, VfxQualityCandidateContext context, VfxQualityProfileTransport profile)
        {
            if (context == null || profile == null)
                throw new ArgumentNullException(context == null ? nameof(context) : nameof(profile));
            string candidateId = SafeSegment(context.CandidateId);
            SafeSegment(sessionId);
            var views = new List<string>(profile.CaptureViewIds);
            if (context.IncludePathView && !views.Contains("Path")) views.Add("Path");
            if ((context.IncludeTopView || context.RequiresTelegraphMeasurement) && !views.Contains("Top")) views.Add("Top");
            views.Sort(StringComparer.Ordinal);
            var backgrounds = new List<string>(profile.CaptureBackgroundIds);
            backgrounds.Sort(StringComparer.Ordinal);
            var result = new List<VfxQualityCapturePoint>();
            bool loop = string.Equals(context.LifecycleMode, "Loop", StringComparison.Ordinal)
                || string.Equals(context.LifecycleMode, "Persistent", StringComparison.Ordinal)
                || string.Equals(context.LifecycleMode, "ManualStop", StringComparison.Ordinal);
            for (int seedIndex = 0; seedIndex < profile.FixedSeeds.Count; seedIndex++)
                for (int viewIndex = 0; viewIndex < views.Count; viewIndex++)
                    for (int backgroundIndex = 0; backgroundIndex < backgrounds.Count; backgroundIndex++)
                    {
                        if (loop)
                        {
                            Add(result, sessionId, candidateId, profile, profile.FixedSeeds[seedIndex], views[viewIndex], backgrounds[backgroundIndex], "warmup", profile.LoopWarmupSeconds, "Play");
                            Add(result, sessionId, candidateId, profile, profile.FixedSeeds[seedIndex], views[viewIndex], backgrounds[backgroundIndex], "steady", profile.LoopWarmupSeconds + profile.LoopSteadySeconds, "Play");
                            Add(result, sessionId, candidateId, profile, profile.FixedSeeds[seedIndex], views[viewIndex], backgrounds[backgroundIndex], "stop", profile.LoopWarmupSeconds + profile.LoopSteadySeconds, "Stop");
                            Add(result, sessionId, candidateId, profile, profile.FixedSeeds[seedIndex], views[viewIndex], backgrounds[backgroundIndex], "cleanup", profile.LoopWarmupSeconds + profile.LoopSteadySeconds + profile.LoopCleanupSeconds, "Cleanup");
                        }
                        else
                        {
                            for (int sliceIndex = 0; sliceIndex < profile.OneShotSlicePercents.Count; sliceIndex++)
                            {
                                int percent = profile.OneShotSlicePercents[sliceIndex];
                                Add(result, sessionId, candidateId, profile, profile.FixedSeeds[seedIndex], views[viewIndex], backgrounds[backgroundIndex], "oneshot-" + percent, Mathf.Max(0f, context.DurationSeconds) * percent / 100f, "Play");
                            }
                        }
                    }
            return result;
        }

        private static void Add(List<VfxQualityCapturePoint> target, string sessionId, string candidateId, VfxQualityProfileTransport profile, int seed, string view, string background, string phase, float seconds, string action)
        {
            string safeView = SafeSegment(view);
            string safeBackground = SafeSegment(background);
            string safePhase = SafeSegment(phase);
            string output = VfxQualityEvidenceProtocol.OutputRoot + "/" + SafeSegment(sessionId) + "/" + candidateId + "/seed-" + seed
                + "/" + safeView + "/" + safeBackground + "/" + safePhase + ".png";
            target.Add(new VfxQualityCapturePoint
            {
                CandidateId = candidateId,
                Seed = seed,
                ViewId = safeView,
                BackgroundId = safeBackground,
                PhaseId = safePhase,
                SimulationSeconds = seconds,
                SimulationFrame = Mathf.RoundToInt(seconds / (float)profile.FixedStepSeconds),
                LifecycleAction = action,
                OutputPath = output
            });
        }

        internal static string SafeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("VFX quality evidence path segment is required.");
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_' && character != '.')
                    throw new InvalidOperationException("Unsafe VFX quality evidence path segment: " + value);
            }
            if (string.Equals(value, ".", StringComparison.Ordinal) || string.Equals(value, "..", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe VFX quality evidence path segment: " + value);
            return value;
        }
    }

    internal sealed class VfxQualityFrameObservation
    {
        public string Status { get; set; } = VfxQualityEvidenceProtocol.NotMeasured;
        public string Message { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public int MissingFrameCount { get; set; }
        public int MagentaPixelCount { get; set; }
        public int NonFinitePixelCount { get; set; }
        public int RenderErrorCount { get; set; }
        public float CropPercent { get; set; }
        public bool AnchorMeasured { get; set; }
        public float AnchorErrorPercent { get; set; }
        public bool PathMeasured { get; set; }
        public float PathEndpointErrorPercent { get; set; }
        public bool TelegraphMeasured { get; set; }
        public float TelegraphFootprintIou { get; set; }
        public float AlphaCoverage { get; set; }
        public float VisibleRangeMeters { get; set; }
    }

    internal sealed class VfxQualityLifecycleObservation
    {
        public string Status { get; set; } = VfxQualityEvidenceProtocol.NotMeasured;
        public string Message { get; set; } = string.Empty;
        public int ActiveParticlesAfterCleanup { get; set; }
        public float BrightPixelPercentAfterCleanup { get; set; }
        public int GhostCyclesExecuted { get; set; }
        public int GhostCount { get; set; }
    }

    internal sealed class VfxQualityPerformanceObservation
    {
        public string Status { get; set; } = VfxQualityEvidenceProtocol.NotMeasured;
        public string Message { get; set; } = string.Empty;
        public float CpuP95Milliseconds { get; set; } = -1f;
        public float GpuP95Milliseconds { get; set; } = -1f;
        public long SteadyStateGcBytesPerFrame { get; set; } = -1L;
    }

    internal interface IVfxQualityCaptureBackend : IDisposable
    {
        string ProviderId { get; }
        string ProviderVersion { get; }
        void BeginCandidate(VfxQualityCandidateContext context, string prefabAssetPath, VfxQualityProfileTransport profile);
        VfxQualityFrameObservation Capture(VfxQualityCapturePoint point);
        VfxQualityLifecycleObservation MeasureLifecycle(VfxQualityCandidateContext context, VfxQualityProfileTransport profile);
        void EndCandidate();
    }

    internal interface IVfxQualityMeasurementProvider : IDisposable
    {
        string ProviderId { get; }
        string ProviderVersion { get; }
        VfxQualityPerformanceObservation Measure(VfxQualityCandidateContext context, string prefabAssetPath, VfxQualityProfileTransport profile);
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxQualityCaptureRecord
    {
        [JsonProperty("point", Order = 1)] public VfxQualityCapturePoint Point { get; set; } = new VfxQualityCapturePoint();
        [JsonProperty("status", Order = 2)] public string Status { get; set; } = VfxQualityEvidenceProtocol.NotMeasured;
        [JsonProperty("contentHash", Order = 3)] public string ContentHash { get; set; } = string.Empty;
        [JsonProperty("missingFrameCount", Order = 4)] public int MissingFrameCount { get; set; }
        [JsonProperty("magentaPixelCount", Order = 5)] public int MagentaPixelCount { get; set; }
        [JsonProperty("nonFinitePixelCount", Order = 6)] public int NonFinitePixelCount { get; set; }
        [JsonProperty("renderErrorCount", Order = 7)] public int RenderErrorCount { get; set; }
        [JsonProperty("cropPercent", Order = 8)] public float CropPercent { get; set; }
        [JsonProperty("anchorMeasured", Order = 9)] public bool AnchorMeasured { get; set; }
        [JsonProperty("anchorErrorPercent", Order = 10)] public float AnchorErrorPercent { get; set; }
        [JsonProperty("pathMeasured", Order = 11)] public bool PathMeasured { get; set; }
        [JsonProperty("pathEndpointErrorPercent", Order = 12)] public float PathEndpointErrorPercent { get; set; }
        [JsonProperty("telegraphMeasured", Order = 13)] public bool TelegraphMeasured { get; set; }
        [JsonProperty("telegraphFootprintIou", Order = 14)] public float TelegraphFootprintIou { get; set; }
        [JsonProperty("alphaCoverage", Order = 15)] public float AlphaCoverage { get; set; }
        [JsonProperty("visibleRangeMeters", Order = 16)] public float VisibleRangeMeters { get; set; }
        [JsonProperty("message", Order = 17)] public string Message { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxQualityGateEvidence
    {
        [JsonProperty("gateId", Order = 1)] public string GateId { get; set; } = string.Empty;
        [JsonProperty("status", Order = 2)] public string Status { get; set; } = VfxQualityEvidenceProtocol.NotMeasured;
        [JsonProperty("passed", Order = 3)] public bool Passed { get; set; }
        [JsonProperty("message", Order = 4)] public string Message { get; set; } = string.Empty;
        [JsonProperty("metrics", Order = 5)] public SortedDictionary<string, string> Metrics { get; set; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
        [JsonProperty("evidenceHash", Order = 6)] public string EvidenceHash { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxQualityCandidateEvidence
    {
        [JsonProperty("candidateId", Order = 1)] public string CandidateId { get; set; } = string.Empty;
        [JsonProperty("lifecycleMode", Order = 2)] public string LifecycleMode { get; set; } = string.Empty;
        [JsonProperty("prefabAssetPath", Order = 3)] public string PrefabAssetPath { get; set; } = string.Empty;
        [JsonProperty("captures", Order = 4)] public List<VfxQualityCaptureRecord> Captures { get; set; } = new List<VfxQualityCaptureRecord>();
        [JsonProperty("lifecycle", Order = 5)] public VfxQualityLifecycleObservation Lifecycle { get; set; } = new VfxQualityLifecycleObservation();
        [JsonProperty("performance", Order = 6)] public VfxQualityPerformanceObservation Performance { get; set; } = new VfxQualityPerformanceObservation();
        [JsonProperty("hardGates", Order = 7)] public List<VfxQualityGateEvidence> HardGates { get; set; } = new List<VfxQualityGateEvidence>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxQualityEvidenceManifest
    {
        [JsonProperty("schemaVersion", Order = 1)] public string SchemaVersion { get; set; } = VfxQualityEvidenceProtocol.EvidenceSchemaVersion;
        [JsonProperty("generationPlanHash", Order = 2)] public string GenerationPlanHash { get; set; } = string.Empty;
        [JsonProperty("candidateReceiptHash", Order = 3)] public string CandidateReceiptHash { get; set; } = string.Empty;
        [JsonProperty("qualityProfileId", Order = 4)] public string QualityProfileId { get; set; } = string.Empty;
        [JsonProperty("qualityProfileVersion", Order = 5)] public string QualityProfileVersion { get; set; } = string.Empty;
        [JsonProperty("captureProviderId", Order = 6)] public string CaptureProviderId { get; set; } = string.Empty;
        [JsonProperty("captureProviderVersion", Order = 7)] public string CaptureProviderVersion { get; set; } = string.Empty;
        [JsonProperty("measurementProviderId", Order = 8)] public string MeasurementProviderId { get; set; } = string.Empty;
        [JsonProperty("measurementProviderVersion", Order = 9)] public string MeasurementProviderVersion { get; set; } = string.Empty;
        [JsonProperty("reviewStatus", Order = 10)] public string ReviewStatus { get; set; } = VfxQualityEvidenceProtocol.ReviewStatus;
        [JsonProperty("complete", Order = 11)] public bool Complete { get; set; }
        [JsonProperty("candidates", Order = 12)] public List<VfxQualityCandidateEvidence> Candidates { get; set; } = new List<VfxQualityCandidateEvidence>();
        [JsonProperty("manifestHash", Order = 13)] public string ManifestHash { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCoreHardGateInput
    {
        [JsonProperty("gateId", Order = 1)] public string GateId { get; set; } = string.Empty;
        [JsonProperty("passed", Order = 2)] public bool Passed { get; set; }
        [JsonProperty("evidenceHash", Order = 3)] public string EvidenceHash { get; set; } = string.Empty;
        [JsonProperty("message", Order = 4)] public string Message { get; set; } = string.Empty;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCoreReviewerScoreInput
    {
        [JsonProperty("reviewerId", Order = 1)] public string ReviewerId { get; set; } = string.Empty;
        [JsonProperty("briefMatch", Order = 2)] public int BriefMatch { get; set; }
        [JsonProperty("readability", Order = 3)] public int Readability { get; set; }
        [JsonProperty("timing", Order = 4)] public int Timing { get; set; }
        [JsonProperty("spatialMotion", Order = 5)] public int SpatialMotion { get; set; }
        [JsonProperty("materialAndColor", Order = 6)] public int MaterialAndColor { get; set; }
        [JsonProperty("polish", Order = 7)] public int Polish { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCoreCandidateEvaluationInputTransport
    {
        [JsonProperty("candidateId", Order = 1)] public string CandidateId { get; set; } = string.Empty;
        [JsonProperty("cpuP95Milliseconds", Order = 2)] public float CpuP95Milliseconds { get; set; } = -1f;
        [JsonProperty("gpuP95Milliseconds", Order = 3)] public float GpuP95Milliseconds { get; set; } = -1f;
        [JsonProperty("hardGates", Order = 4)] public List<VfxCoreHardGateInput> HardGates { get; set; } = new List<VfxCoreHardGateInput>();
        [JsonProperty("reviewerScores", Order = 5)] public List<VfxCoreReviewerScoreInput> ReviewerScores { get; set; } = new List<VfxCoreReviewerScoreInput>();
    }

    /// <summary>Exact Editor transport mirror of Core mx.vfx.evaluation-input.v1.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class VfxCoreEvaluationInputTransport
    {
        [JsonProperty("schemaVersion", Order = 1)] public string SchemaVersion { get; set; } = VfxQualityEvidenceProtocol.EvaluationInputSchemaVersion;
        [JsonProperty("generationPlanHash", Order = 2)] public string GenerationPlanHash { get; set; } = string.Empty;
        [JsonProperty("candidateReceiptHash", Order = 3)] public string CandidateReceiptHash { get; set; } = string.Empty;
        [JsonProperty("qualityProfileId", Order = 4)] public string QualityProfileId { get; set; } = string.Empty;
        [JsonProperty("qualityProfileVersion", Order = 5)] public string QualityProfileVersion { get; set; } = string.Empty;
        [JsonProperty("previousMedianScore", Order = 6)] public int PreviousMedianScore { get; set; }
        [JsonProperty("consecutiveLowImprovementRounds", Order = 7)] public int ConsecutiveLowImprovementRounds { get; set; }
        [JsonProperty("candidates", Order = 8)] public List<VfxCoreCandidateEvaluationInputTransport> Candidates { get; set; } = new List<VfxCoreCandidateEvaluationInputTransport>();
    }

    internal sealed class VfxQualityCaptureResult
    {
        public bool Success { get; set; }
        public bool Complete { get; set; }
        public string Message { get; set; } = string.Empty;
        public string EvidenceManifestPath { get; set; } = string.Empty;
        public string EvidenceManifestHash { get; set; } = string.Empty;
        public string EvaluationInputPath { get; set; } = string.Empty;
        public string EvaluationInputHash { get; set; } = string.Empty;
        public VfxQualityEvidenceManifest Manifest { get; set; }
        public VfxCoreEvaluationInputTransport EvaluationInput { get; set; }
    }
}
