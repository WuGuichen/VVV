using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using MxFramework.Combat.Physics;
using MxFramework.Diagnostics;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestInteractionPerformanceCounterSource
    {
        public const string SourceName = "CharacterTestInteraction";
        public const string LastFrameEventsCounterId = "characterTest.interaction.lastFrame.events";
        public const string HistoryEventsCounterId = "history.events";
        public const string DispatcherExecutedCounterId = "dispatcher.executed";
        public const string PhysicsFieldsCounterId = "physics.fields";
        public const string PhysicsQueriesCounterId = "physics.queries";
        public const string PhysicsQueryResultsCounterId = "physics.queryResults";
        public const string PhysicsDuplicateBodySkipsCounterId = "physics.duplicateBodySkips";
        public const string PhysicsAppliedForcesCounterId = "physics.appliedForces";
        public const string PhysicsCharactersCounterId = "physics.characters";
        public const string PhysicsMovablesCounterId = "physics.movables";
        public const string VfxSpawnedCounterId = "vfx.spawned";
        public const string AudioPlayedCounterId = "audio.played";
        public const string RenderingPublishedCounterId = "rendering.published";

        private const string Category = "CharacterTestInteraction";

        private readonly CharacterTestCharacterRuntimeModule _runtime;

        public CharacterTestInteractionPerformanceCounterSource(
            CharacterTestCharacterRuntimeModule runtime)
        {
            _runtime = runtime;
        }

        public FrameworkPerformanceCounterSnapshot Capture()
        {
            if (_runtime == null)
                return Capture(
                    0,
                    0,
                    CharacterTestInteractionConsumerDispatchSummary.Empty,
                    CharacterTestInteractionPhysicsSummary.Empty,
                    CharacterTestInteractionVfxSummary.Empty,
                    CharacterTestInteractionAudioSummary.Empty,
                    CharacterTestInteractionRenderingSummary.Empty);

            return Capture(
                _runtime.LastFrameInteractionEvents != null ? _runtime.LastFrameInteractionEvents.Count : 0,
                _runtime.InteractionHistory != null ? _runtime.InteractionHistory.EventCount : 0,
                _runtime.InteractionConsumerSummary,
                _runtime.InteractionPhysicsSummary,
                _runtime.InteractionVfxSummary,
                _runtime.InteractionAudioSummary,
                _runtime.InteractionRenderingSummary);
        }

        public static FrameworkPerformanceCounterSnapshot Capture(
            int lastFrameEventCount,
            int historyEventCount,
            CharacterTestInteractionConsumerDispatchSummary dispatcher,
            CharacterTestInteractionPhysicsSummary physics,
            CharacterTestInteractionVfxSummary vfx,
            CharacterTestInteractionAudioSummary audio,
            CharacterTestInteractionRenderingSummary rendering)
        {
            return new FrameworkPerformanceCounterSnapshot(
                SourceName,
                enabled: true,
                new[]
                {
                    Sample(LastFrameEventsCounterId, "Last Frame Events", Math.Max(0, lastFrameEventCount), "events"),
                    Sample(HistoryEventsCounterId, "History Events", Math.Max(0, historyEventCount), "events"),
                    Sample(DispatcherExecutedCounterId, "Dispatcher Executed", dispatcher.ExecutedConsumerCount, "consumers"),
                    Sample(PhysicsFieldsCounterId, "Physics Fields", physics.ProcessedFieldCount, "fields"),
                    Sample(PhysicsQueriesCounterId, "Physics Queries", physics.QueryCount, "queries"),
                    Sample(PhysicsQueryResultsCounterId, "Physics Query Results", physics.QueryResultCount, "results"),
                    Sample(PhysicsDuplicateBodySkipsCounterId, "Physics Duplicate Body Skips", physics.DuplicateBodySkipCount, "skips"),
                    Sample(PhysicsAppliedForcesCounterId, "Physics Applied Forces", physics.AppliedForceCount, "forces"),
                    Sample(PhysicsCharactersCounterId, "Physics Character Targets", physics.CharacterTargetCount, "targets"),
                    Sample(PhysicsMovablesCounterId, "Physics Movable Targets", physics.MovableTargetCount, "targets"),
                    Sample(VfxSpawnedCounterId, "VFX Spawned", vfx.VfxSpawnedCount, "spawns"),
                    Sample(AudioPlayedCounterId, "Audio Played", audio.AudioPlayedCount, "plays"),
                    Sample(RenderingPublishedCounterId, "Rendering Published", rendering.PublishedFieldImpulseCount, "events")
                });
        }

        private static FrameworkPerformanceCounterSample Sample(
            string counterId,
            string displayName,
            long value,
            string unit)
        {
            return new FrameworkPerformanceCounterSample(
                counterId,
                displayName,
                Category,
                Math.Max(0L, value),
                unit,
                FrameworkPerformanceCounterCost.NoAlloc);
        }
    }

    internal sealed class CharacterTestRuntimePerformanceCounterSource
    {
        public const string SourceName = "CharacterTestRuntime";
        public const string EntityCountCounterId = "runtime.entities";
        public const string MovableObjectCountCounterId = "runtime.movableObjects";
        public const string SpawnPendingRequestsCounterId = "spawn.pendingRequests";
        public const string SpawnLastProcessedCounterId = "spawn.lastProcessedRequests";
        public const string SpawnLastSpawnedCounterId = "spawn.lastSpawned";
        public const string SpawnLastFailedCounterId = "spawn.lastFailed";
        public const string SpawnTotalSpawnedCounterId = "spawn.totalSpawned";
        public const string SpawnTotalFailedCounterId = "spawn.totalFailed";
        public const string ViewActiveViewsCounterId = "view.activeViews";
        public const string ViewLastSyncRequestsCounterId = "view.lastSyncRequests";
        public const string ViewLastSyncAppliedCounterId = "view.lastSyncApplied";
        public const string ViewLastSyncMissesCounterId = "view.lastSyncMisses";
        public const string ViewLastTransformAppliesCounterId = "view.lastTransformApplies";
        public const string ViewTotalSyncRequestsCounterId = "view.totalSyncRequests";
        public const string ViewTotalSyncMissesCounterId = "view.totalSyncMisses";
        public const string ViewTotalTransformAppliesCounterId = "view.totalTransformApplies";
        public const string PhysicsWorldRevisionCounterId = "physics.worldRevision";
        public const string PhysicsWorldBodiesCounterId = "physics.worldBodies";
        public const string PhysicsWorldCollidersCounterId = "physics.worldColliders";
        public const string PhysicsStaticCollidersCounterId = "physics.staticColliders";
        public const string PhysicsSyncedCharacterCollidersCounterId = "physics.syncedCharacterColliders";
        public const string PhysicsSyncedMovableCollidersCounterId = "physics.syncedMovableColliders";

        private const string RuntimeCategory = "CharacterTestRuntime";
        private const string SpawnCategory = "CharacterTestSpawn";
        private const string ViewCategory = "CharacterTestView";
        private const string PhysicsCategory = "CharacterTestPhysics";

        private readonly CharacterTestCharacterRuntimeModule _runtime;

        public CharacterTestRuntimePerformanceCounterSource(
            CharacterTestCharacterRuntimeModule runtime)
        {
            _runtime = runtime;
        }

        public FrameworkPerformanceCounterSnapshot Capture()
        {
            if (_runtime == null)
                return Capture(
                    0,
                    0,
                    CharacterTestCharacterSpawnPerformanceSummary.Empty,
                    CharacterTestCharacterViewSyncPerformanceSummary.Empty,
                    default,
                    0,
                    0,
                    0);

            CharacterTestCharacterPhysicsRuntime physics = _runtime.PhysicsRuntime;
            CombatPhysicsWorldStats physicsStats =
                physics != null && physics.World != null ? physics.World.CreateStats() : default;
            return Capture(
                _runtime.Entities != null ? _runtime.Entities.Count : 0,
                _runtime.MovableNaturalObjects != null ? _runtime.MovableNaturalObjects.Count : 0,
                _runtime.SpawnPerformanceSummary,
                _runtime.ViewSyncPerformanceSummary,
                physicsStats,
                physics != null ? physics.StaticColliderCount : 0,
                physics != null ? physics.SyncedCharacterColliderCount : 0,
                physics != null ? physics.SyncedMovableObjectColliderCount : 0);
        }

        internal static FrameworkPerformanceCounterSnapshot Capture(
            int entityCount,
            int movableObjectCount,
            CharacterTestCharacterSpawnPerformanceSummary spawn,
            CharacterTestCharacterViewSyncPerformanceSummary view,
            CombatPhysicsWorldStats physicsStats,
            int staticColliderCount,
            int syncedCharacterColliderCount,
            int syncedMovableColliderCount)
        {
            return new FrameworkPerformanceCounterSnapshot(
                SourceName,
                enabled: true,
                new[]
                {
                    Sample(EntityCountCounterId, "Runtime Entities", RuntimeCategory, entityCount, "entities"),
                    Sample(MovableObjectCountCounterId, "Runtime Movable Objects", RuntimeCategory, movableObjectCount, "objects"),
                    Sample(SpawnPendingRequestsCounterId, "Spawn Pending Requests", SpawnCategory, spawn.PendingRequestCount, "requests"),
                    Sample(SpawnLastProcessedCounterId, "Spawn Last Processed", SpawnCategory, spawn.LastProcessedRequestCount, "requests"),
                    Sample(SpawnLastSpawnedCounterId, "Spawn Last Spawned", SpawnCategory, spawn.LastSpawnedCount, "entities"),
                    Sample(SpawnLastFailedCounterId, "Spawn Last Failed", SpawnCategory, spawn.LastFailedCount, "requests"),
                    Sample(SpawnTotalSpawnedCounterId, "Spawn Total Spawned", SpawnCategory, spawn.TotalSpawnedCount, "entities"),
                    Sample(SpawnTotalFailedCounterId, "Spawn Total Failed", SpawnCategory, spawn.TotalFailedCount, "requests"),
                    Sample(ViewActiveViewsCounterId, "View Active Views", ViewCategory, view.ActiveViewCount, "views"),
                    Sample(ViewLastSyncRequestsCounterId, "View Last Sync Requests", ViewCategory, view.LastSyncRequestCount, "requests"),
                    Sample(ViewLastSyncAppliedCounterId, "View Last Sync Applied", ViewCategory, view.LastSyncAppliedCount, "views"),
                    Sample(ViewLastSyncMissesCounterId, "View Last Sync Misses", ViewCategory, view.LastSyncMissCount, "misses"),
                    Sample(ViewLastTransformAppliesCounterId, "View Last Transform Applies", ViewCategory, view.LastTransformApplyCount, "applies"),
                    Sample(ViewTotalSyncRequestsCounterId, "View Total Sync Requests", ViewCategory, view.TotalSyncRequestCount, "requests"),
                    Sample(ViewTotalSyncMissesCounterId, "View Total Sync Misses", ViewCategory, view.TotalSyncMissCount, "misses"),
                    Sample(ViewTotalTransformAppliesCounterId, "View Total Transform Applies", ViewCategory, view.TotalTransformApplyCount, "applies"),
                    Sample(PhysicsWorldRevisionCounterId, "Physics World Revision", PhysicsCategory, physicsStats.Revision, "revision"),
                    Sample(PhysicsWorldBodiesCounterId, "Physics World Bodies", PhysicsCategory, physicsStats.BodyCount, "bodies"),
                    Sample(PhysicsWorldCollidersCounterId, "Physics World Colliders", PhysicsCategory, physicsStats.ColliderCount, "colliders"),
                    Sample(PhysicsStaticCollidersCounterId, "Physics Static Colliders", PhysicsCategory, staticColliderCount, "colliders"),
                    Sample(PhysicsSyncedCharacterCollidersCounterId, "Physics Synced Character Colliders", PhysicsCategory, syncedCharacterColliderCount, "colliders"),
                    Sample(PhysicsSyncedMovableCollidersCounterId, "Physics Synced Movable Colliders", PhysicsCategory, syncedMovableColliderCount, "colliders")
                });
        }

        private static FrameworkPerformanceCounterSample Sample(
            string counterId,
            string displayName,
            string category,
            long value,
            string unit)
        {
            return new FrameworkPerformanceCounterSample(
                counterId,
                displayName,
                category,
                Math.Max(0L, value),
                unit,
                FrameworkPerformanceCounterCost.NoAlloc);
        }
    }

    internal static class CharacterTestPerformanceCounterReport
    {
        public const string DefaultScenarioName = "CharacterTestPerformanceSmoke";

        /// <remarks>AllocByDesign: creates a stable text report for explicit diagnostics/profile evidence capture.</remarks>
        public static string CreateText(
            string scenarioName,
            long frame,
            params FrameworkPerformanceCounterSnapshot[] snapshots)
        {
            string safeScenarioName = NormalizeScenarioName(scenarioName);
            var builder = new StringBuilder(1024);
            AppendBaseHeader(builder, safeScenarioName, frame);
            AppendSnapshots(builder, snapshots);
            return builder.ToString();
        }

        /// <remarks>AllocByDesign: creates a stable smoke scenario report for profile evidence capture.</remarks>
        public static string CreateSmokeScenarioText(
            string scenarioName,
            long startFrame,
            long captureStartFrame,
            long frame,
            int warmupFrameCount,
            int captureFrameCount,
            double deltaTimeSeconds,
            params FrameworkPerformanceCounterSnapshot[] snapshots)
        {
            string safeScenarioName = NormalizeScenarioName(scenarioName);
            var builder = new StringBuilder(1280);
            builder.Append("scenario: ").Append(safeScenarioName)
                .Append('\n')
                .Append("startFrame: ").Append(Math.Max(0L, startFrame))
                .Append('\n')
                .Append("captureStartFrame: ").Append(Math.Max(0L, captureStartFrame))
                .Append('\n')
                .Append("frame: ").Append(Math.Max(0L, frame))
                .Append('\n')
                .Append("warmupFrames: ").Append(Math.Max(0, warmupFrameCount))
                .Append('\n')
                .Append("captureFrames: ").Append(Math.Max(1, captureFrameCount))
                .Append('\n')
                .Append("deltaTimeSeconds: ").Append(FormatDeltaTime(deltaTimeSeconds));

            AppendSnapshots(builder, snapshots);
            return builder.ToString();
        }

        /// <remarks>AllocByDesign: creates a stable timing report for explicit diagnostics/profile evidence capture.</remarks>
        internal static string CreateWarmRunTimingText(
            string scenarioName,
            long startFrame,
            long captureStartFrame,
            long frame,
            int warmupFrameCount,
            int captureFrameCount,
            double deltaTimeSeconds,
            long stopwatchFrequency,
            long[] elapsedTicks,
            long[] allocatedBytes,
            params FrameworkPerformanceCounterSnapshot[] snapshots)
        {
            return CreateTimingText(
                scenarioName,
                "clean-editor-warm-run",
                startFrame,
                captureStartFrame,
                frame,
                warmupFrameCount,
                captureFrameCount,
                deltaTimeSeconds,
                stopwatchFrequency,
                elapsedTicks,
                allocatedBytes,
                snapshots);
        }

        /// <remarks>AllocByDesign: creates a stable timing report for explicit diagnostics/profile evidence capture.</remarks>
        internal static string CreateTimingText(
            string scenarioName,
            string profileKind,
            long startFrame,
            long captureStartFrame,
            long frame,
            int warmupFrameCount,
            int captureFrameCount,
            double deltaTimeSeconds,
            long stopwatchFrequency,
            long[] elapsedTicks,
            long[] allocatedBytes,
            params FrameworkPerformanceCounterSnapshot[] snapshots)
        {
            string safeScenarioName = NormalizeScenarioName(scenarioName);
            string safeProfileKind = string.IsNullOrWhiteSpace(profileKind)
                ? "timing"
                : profileKind;
            int sampleCount = Math.Max(0, Math.Min(
                elapsedTicks != null ? elapsedTicks.Length : 0,
                allocatedBytes != null ? allocatedBytes.Length : 0));
            var builder = new StringBuilder(1536);
            builder.Append("scenario: ").Append(safeScenarioName)
                .Append('\n')
                .Append("profileKind: ").Append(safeProfileKind)
                .Append('\n')
                .Append("startFrame: ").Append(Math.Max(0L, startFrame))
                .Append('\n')
                .Append("captureStartFrame: ").Append(Math.Max(0L, captureStartFrame))
                .Append('\n')
                .Append("frame: ").Append(Math.Max(0L, frame))
                .Append('\n')
                .Append("warmupFrames: ").Append(Math.Max(0, warmupFrameCount))
                .Append('\n')
                .Append("captureFrames: ").Append(Math.Max(1, captureFrameCount))
                .Append('\n')
                .Append("deltaTimeSeconds: ").Append(FormatDeltaTime(deltaTimeSeconds))
                .Append('\n')
                .Append("timingSamples: ").Append(sampleCount)
                .Append('\n')
                .Append("stopwatchFrequency: ").Append(Math.Max(1L, stopwatchFrequency));

            AppendTimingStats(builder, elapsedTicks, sampleCount, Math.Max(1L, stopwatchFrequency));
            AppendAllocationStats(builder, allocatedBytes, sampleCount);
            AppendSnapshots(builder, snapshots);
            return builder.ToString();
        }

        internal static string NormalizeScenarioName(string scenarioName)
        {
            return string.IsNullOrWhiteSpace(scenarioName)
                ? DefaultScenarioName
                : scenarioName;
        }

        private static void AppendBaseHeader(StringBuilder builder, string scenarioName, long frame)
        {
            builder.Append("scenario: ").Append(scenarioName)
                .Append('\n')
                .Append("frame: ").Append(Math.Max(0L, frame));
        }

        private static void AppendSnapshots(
            StringBuilder builder,
            FrameworkPerformanceCounterSnapshot[] snapshots)
        {
            int snapshotCount = CountSnapshots(snapshots);
            builder.Append('\n')
                .Append("snapshots: ").Append(snapshotCount);

            if (snapshotCount == 0)
                return;

            FrameworkPerformanceCounterSnapshot[] ordered = CopySnapshots(snapshots, snapshotCount);
            Array.Sort(ordered, CompareSnapshots);
            for (int i = 0; i < ordered.Length; i++)
                AppendSnapshot(builder, ordered[i]);
        }

        private static string FormatDeltaTime(double deltaTimeSeconds)
        {
            double safeDeltaTime = CharacterTestPerformanceSmokeScenario.NormalizeDeltaTime(deltaTimeSeconds);
            return safeDeltaTime.ToString("0.#########", CultureInfo.InvariantCulture);
        }

        private static void AppendTimingStats(
            StringBuilder builder,
            long[] elapsedTicks,
            int sampleCount,
            long stopwatchFrequency)
        {
            var sorted = CopyAndSort(elapsedTicks, sampleCount);
            double meanMs = CalculateMeanTicks(sorted, stopwatchFrequency);
            builder.Append('\n')
                .Append("elapsedMs.min: ").Append(FormatMilliseconds(sorted.Length > 0 ? sorted[0] : 0L, stopwatchFrequency))
                .Append('\n')
                .Append("elapsedMs.median: ").Append(FormatMilliseconds(Percentile(sorted, 0.5d), stopwatchFrequency))
                .Append('\n')
                .Append("elapsedMs.mean: ").Append(meanMs.ToString("0.###", CultureInfo.InvariantCulture))
                .Append('\n')
                .Append("elapsedMs.p75: ").Append(FormatMilliseconds(Percentile(sorted, 0.75d), stopwatchFrequency))
                .Append('\n')
                .Append("elapsedMs.p90: ").Append(FormatMilliseconds(Percentile(sorted, 0.9d), stopwatchFrequency))
                .Append('\n')
                .Append("elapsedMs.p95: ").Append(FormatMilliseconds(Percentile(sorted, 0.95d), stopwatchFrequency))
                .Append('\n')
                .Append("elapsedMs.max: ").Append(FormatMilliseconds(sorted.Length > 0 ? sorted[sorted.Length - 1] : 0L, stopwatchFrequency));
        }

        private static void AppendAllocationStats(
            StringBuilder builder,
            long[] allocatedBytes,
            int sampleCount)
        {
            var sorted = CopyAndSort(allocatedBytes, sampleCount);
            long total = 0L;
            for (int i = 0; i < sampleCount; i++)
                total += Math.Max(0L, allocatedBytes[i]);

            builder.Append('\n')
                .Append("allocatedBytes.total: ").Append(total)
                .Append('\n')
                .Append("allocatedBytes.median: ").Append(Percentile(sorted, 0.5d))
                .Append('\n')
                .Append("allocatedBytes.p75: ").Append(Percentile(sorted, 0.75d))
                .Append('\n')
                .Append("allocatedBytes.p95: ").Append(Percentile(sorted, 0.95d))
                .Append('\n')
                .Append("allocatedBytes.max: ").Append(sorted.Length > 0 ? sorted[sorted.Length - 1] : 0L);
        }

        private static long[] CopyAndSort(long[] values, int sampleCount)
        {
            int safeCount = Math.Max(0, sampleCount);
            var copy = new long[safeCount];
            for (int i = 0; i < safeCount; i++)
                copy[i] = Math.Max(0L, values[i]);

            Array.Sort(copy);
            return copy;
        }

        private static long Percentile(long[] sorted, double percentile)
        {
            if (sorted == null || sorted.Length == 0)
                return 0L;

            double clamped = Math.Max(0d, Math.Min(1d, percentile));
            int index = (int)Math.Round((sorted.Length - 1) * clamped, MidpointRounding.AwayFromZero);
            return sorted[Math.Max(0, Math.Min(sorted.Length - 1, index))];
        }

        private static double CalculateMeanTicks(long[] sorted, long stopwatchFrequency)
        {
            if (sorted == null || sorted.Length == 0)
                return 0d;

            double total = 0d;
            for (int i = 0; i < sorted.Length; i++)
                total += sorted[i];

            return total / sorted.Length * 1000d / Math.Max(1L, stopwatchFrequency);
        }

        private static string FormatMilliseconds(long elapsedTicks, long stopwatchFrequency)
        {
            double milliseconds = Math.Max(0L, elapsedTicks) * 1000d / Math.Max(1L, stopwatchFrequency);
            return milliseconds.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static int CountSnapshots(FrameworkPerformanceCounterSnapshot[] snapshots)
        {
            if (snapshots == null)
                return 0;

            int count = 0;
            for (int i = 0; i < snapshots.Length; i++)
            {
                if (snapshots[i] != null)
                    count++;
            }

            return count;
        }

        private static FrameworkPerformanceCounterSnapshot[] CopySnapshots(
            FrameworkPerformanceCounterSnapshot[] snapshots,
            int count)
        {
            var ordered = new FrameworkPerformanceCounterSnapshot[count];
            int index = 0;
            for (int i = 0; i < snapshots.Length; i++)
            {
                FrameworkPerformanceCounterSnapshot snapshot = snapshots[i];
                if (snapshot == null)
                    continue;

                ordered[index++] = snapshot;
            }

            return ordered;
        }

        private static int CompareSnapshots(
            FrameworkPerformanceCounterSnapshot left,
            FrameworkPerformanceCounterSnapshot right)
        {
            return string.Compare(
                left != null ? left.SourceName : string.Empty,
                right != null ? right.SourceName : string.Empty,
                StringComparison.Ordinal);
        }

        private static void AppendSnapshot(
            StringBuilder builder,
            FrameworkPerformanceCounterSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            builder.Append('\n')
                .Append("source: ").Append(snapshot.SourceName)
                .Append(" enabled=").Append(snapshot.Enabled ? "true" : "false")
                .Append(" counters=").Append(snapshot.Count);

            for (int i = 0; i < snapshot.Samples.Count; i++)
            {
                FrameworkPerformanceCounterSample sample = snapshot.Samples[i];
                builder.Append('\n')
                    .Append(sample.Category)
                    .Append('/')
                    .Append(sample.CounterId)
                    .Append('=')
                    .Append(sample.Value)
                    .Append(' ')
                    .Append(sample.Unit)
                    .Append(" cost=")
                    .Append(sample.Cost);
            }
        }
    }

    internal static class CharacterTestPerformanceSmokeScenario
    {
        public const int DefaultWarmupFrameCount = 60;
        public const int DefaultCaptureFrameCount = 120;
        public const double DefaultDeltaTimeSeconds = 1d / 60d;

        /// <remarks>AllocByDesign: explicit diagnostics scenario runner; do not call from per-frame gameplay paths.</remarks>
        public static CharacterTestPerformanceSmokeScenarioResult Run(
            GameSlice slice,
            string scenarioName = null,
            int warmupFrameCount = DefaultWarmupFrameCount,
            int captureFrameCount = DefaultCaptureFrameCount,
            double deltaTimeSeconds = DefaultDeltaTimeSeconds,
            Func<FrameworkPerformanceCounterSnapshot> uiSnapshotProvider = null)
        {
            if (slice == null)
                throw new ArgumentNullException(nameof(slice));

            string safeScenarioName = CharacterTestPerformanceCounterReport.NormalizeScenarioName(scenarioName);
            int safeWarmupFrameCount = Math.Max(0, warmupFrameCount);
            int safeCaptureFrameCount = Math.Max(1, captureFrameCount);
            double safeDeltaTimeSeconds = NormalizeDeltaTime(deltaTimeSeconds);
            long startFrame = slice.CurrentFrame.Value;

            TickFrames(slice, safeWarmupFrameCount, safeDeltaTimeSeconds);
            long captureStartFrame = slice.CurrentFrame.Value;
            TickFrames(slice, safeCaptureFrameCount, safeDeltaTimeSeconds);
            long endFrame = slice.CurrentFrame.Value;

            FrameworkPerformanceCounterSnapshot uiSnapshot =
                uiSnapshotProvider != null ? uiSnapshotProvider() : null;
            string reportText = CharacterTestPerformanceCounterReport.CreateSmokeScenarioText(
                safeScenarioName,
                startFrame,
                captureStartFrame,
                endFrame,
                safeWarmupFrameCount,
                safeCaptureFrameCount,
                safeDeltaTimeSeconds,
                slice.Services.CreateInteractionPerformanceCounterSnapshot(),
                slice.Services.CreateRuntimePerformanceCounterSnapshot(),
                uiSnapshot);

            return new CharacterTestPerformanceSmokeScenarioResult(
                safeScenarioName,
                startFrame,
                captureStartFrame,
                endFrame,
                safeWarmupFrameCount,
                safeCaptureFrameCount,
                safeDeltaTimeSeconds,
                reportText);
        }

        internal static double NormalizeDeltaTime(double deltaTimeSeconds)
        {
            if (double.IsNaN(deltaTimeSeconds)
                || double.IsInfinity(deltaTimeSeconds)
                || deltaTimeSeconds < 0d)
            {
                return DefaultDeltaTimeSeconds;
            }

            return deltaTimeSeconds;
        }

        private static void TickFrames(
            GameSlice slice,
            int frameCount,
            double deltaTimeSeconds)
        {
            for (int i = 0; i < frameCount; i++)
                slice.Tick(deltaTimeSeconds);
        }
    }

    internal static class CharacterTestPerformanceWarmRunScenario
    {
        public const string DefaultScenarioName = "character-test.performance.warm-run";
        public const int DefaultWarmupFrameCount = CharacterTestPerformanceSmokeScenario.DefaultWarmupFrameCount;
        public const int DefaultCaptureFrameCount = CharacterTestPerformanceSmokeScenario.DefaultCaptureFrameCount;
        public const double DefaultDeltaTimeSeconds = CharacterTestPerformanceSmokeScenario.DefaultDeltaTimeSeconds;

        /// <remarks>AllocByDesign: explicit diagnostics scenario runner; do not call from per-frame gameplay paths.</remarks>
        public static CharacterTestPerformanceWarmRunScenarioResult Run(
            GameSlice slice,
            string scenarioName = null,
            int warmupFrameCount = DefaultWarmupFrameCount,
            int captureFrameCount = DefaultCaptureFrameCount,
            double deltaTimeSeconds = DefaultDeltaTimeSeconds,
            Func<FrameworkPerformanceCounterSnapshot> uiSnapshotProvider = null)
        {
            if (slice == null)
                throw new ArgumentNullException(nameof(slice));

            string safeScenarioName = CharacterTestPerformanceCounterReport.NormalizeScenarioName(scenarioName);
            int safeWarmupFrameCount = Math.Max(0, warmupFrameCount);
            int safeCaptureFrameCount = Math.Max(1, captureFrameCount);
            double safeDeltaTimeSeconds = CharacterTestPerformanceSmokeScenario.NormalizeDeltaTime(deltaTimeSeconds);
            var elapsedTicks = new long[safeCaptureFrameCount];
            var allocatedBytes = new long[safeCaptureFrameCount];
            long startFrame = slice.CurrentFrame.Value;

            TickFrames(slice, safeWarmupFrameCount, safeDeltaTimeSeconds);
            long captureStartFrame = slice.CurrentFrame.Value;
            long stopwatchFrequency = Stopwatch.Frequency;
            for (int i = 0; i < safeCaptureFrameCount; i++)
            {
                _ = GC.GetAllocatedBytesForCurrentThread();
                long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
                long tickBefore = Stopwatch.GetTimestamp();
                slice.Tick(safeDeltaTimeSeconds);
                long tickAfter = Stopwatch.GetTimestamp();
                long allocationAfter = GC.GetAllocatedBytesForCurrentThread();
                elapsedTicks[i] = Math.Max(0L, tickAfter - tickBefore);
                allocatedBytes[i] = Math.Max(0L, allocationAfter - allocationBefore);
            }

            long endFrame = slice.CurrentFrame.Value;
            FrameworkPerformanceCounterSnapshot uiSnapshot =
                uiSnapshotProvider != null ? uiSnapshotProvider() : null;
            string reportText = CharacterTestPerformanceCounterReport.CreateWarmRunTimingText(
                safeScenarioName,
                startFrame,
                captureStartFrame,
                endFrame,
                safeWarmupFrameCount,
                safeCaptureFrameCount,
                safeDeltaTimeSeconds,
                stopwatchFrequency,
                elapsedTicks,
                allocatedBytes,
                slice.Services.CreateInteractionPerformanceCounterSnapshot(),
                slice.Services.CreateRuntimePerformanceCounterSnapshot(),
                uiSnapshot);

            return new CharacterTestPerformanceWarmRunScenarioResult(
                safeScenarioName,
                startFrame,
                captureStartFrame,
                endFrame,
                safeWarmupFrameCount,
                safeCaptureFrameCount,
                safeDeltaTimeSeconds,
                reportText);
        }

        private static void TickFrames(
            GameSlice slice,
            int frameCount,
            double deltaTimeSeconds)
        {
            for (int i = 0; i < frameCount; i++)
                slice.Tick(deltaTimeSeconds);
        }
    }

    internal readonly struct CharacterTestPerformanceWarmRunScenarioResult
    {
        public CharacterTestPerformanceWarmRunScenarioResult(
            string scenarioName,
            long startFrame,
            long captureStartFrame,
            long frame,
            int warmupFrameCount,
            int captureFrameCount,
            double deltaTimeSeconds,
            string reportText)
        {
            ScenarioName = CharacterTestPerformanceCounterReport.NormalizeScenarioName(scenarioName);
            StartFrame = Math.Max(0L, startFrame);
            CaptureStartFrame = Math.Max(0L, captureStartFrame);
            Frame = Math.Max(0L, frame);
            WarmupFrameCount = Math.Max(0, warmupFrameCount);
            CaptureFrameCount = Math.Max(1, captureFrameCount);
            DeltaTimeSeconds = CharacterTestPerformanceSmokeScenario.NormalizeDeltaTime(deltaTimeSeconds);
            ReportText = reportText ?? string.Empty;
        }

        public string ScenarioName { get; }
        public long StartFrame { get; }
        public long CaptureStartFrame { get; }
        public long Frame { get; }
        public int WarmupFrameCount { get; }
        public int CaptureFrameCount { get; }
        public double DeltaTimeSeconds { get; }
        public string ReportText { get; }
    }

    public static class CharacterTestPerformanceSmokeReport
    {
        public const string DefaultScenarioName = "character-test.performance.smoke";
        public const int DefaultWarmupFrameCount = CharacterTestPerformanceSmokeScenario.DefaultWarmupFrameCount;
        public const int DefaultCaptureFrameCount = CharacterTestPerformanceSmokeScenario.DefaultCaptureFrameCount;
        public const double DefaultDeltaTimeSeconds = CharacterTestPerformanceSmokeScenario.DefaultDeltaTimeSeconds;

        /// <remarks>AllocByDesign: explicit diagnostics/profile evidence capture; do not call from per-frame gameplay paths.</remarks>
        public static string CreateText(
            string scenarioName = DefaultScenarioName,
            int warmupFrameCount = DefaultWarmupFrameCount,
            int captureFrameCount = DefaultCaptureFrameCount,
            double deltaTimeSeconds = DefaultDeltaTimeSeconds)
        {
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false))
            {
                return CharacterTestPerformanceSmokeScenario.Run(
                    slice,
                    scenarioName,
                    warmupFrameCount,
                    captureFrameCount,
                    deltaTimeSeconds).ReportText;
            }
        }
    }

    internal static class CharacterTestPerformanceWarmRunTimingReport
    {
        public const string DefaultScenarioName = CharacterTestPerformanceWarmRunScenario.DefaultScenarioName;
        public const int DefaultWarmupFrameCount = CharacterTestPerformanceWarmRunScenario.DefaultWarmupFrameCount;
        public const int DefaultCaptureFrameCount = CharacterTestPerformanceWarmRunScenario.DefaultCaptureFrameCount;
        public const double DefaultDeltaTimeSeconds = CharacterTestPerformanceWarmRunScenario.DefaultDeltaTimeSeconds;

        /// <remarks>AllocByDesign: explicit diagnostics/profile evidence capture; do not call from per-frame gameplay paths.</remarks>
        public static string CreateText(
            string scenarioName = DefaultScenarioName,
            int warmupFrameCount = DefaultWarmupFrameCount,
            int captureFrameCount = DefaultCaptureFrameCount,
            double deltaTimeSeconds = DefaultDeltaTimeSeconds)
        {
            using (var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false))
            {
                return CharacterTestPerformanceWarmRunScenario.Run(
                    slice,
                    scenarioName,
                    warmupFrameCount,
                    captureFrameCount,
                    deltaTimeSeconds).ReportText;
            }
        }
    }

    internal readonly struct CharacterTestPerformanceSmokeScenarioResult
    {
        public CharacterTestPerformanceSmokeScenarioResult(
            string scenarioName,
            long startFrame,
            long captureStartFrame,
            long frame,
            int warmupFrameCount,
            int captureFrameCount,
            double deltaTimeSeconds,
            string reportText)
        {
            ScenarioName = CharacterTestPerformanceCounterReport.NormalizeScenarioName(scenarioName);
            StartFrame = Math.Max(0L, startFrame);
            CaptureStartFrame = Math.Max(0L, captureStartFrame);
            Frame = Math.Max(0L, frame);
            WarmupFrameCount = Math.Max(0, warmupFrameCount);
            CaptureFrameCount = Math.Max(1, captureFrameCount);
            DeltaTimeSeconds = CharacterTestPerformanceSmokeScenario.NormalizeDeltaTime(deltaTimeSeconds);
            ReportText = reportText ?? string.Empty;
        }

        public string ScenarioName { get; }
        public long StartFrame { get; }
        public long CaptureStartFrame { get; }
        public long Frame { get; }
        public int WarmupFrameCount { get; }
        public int CaptureFrameCount { get; }
        public double DeltaTimeSeconds { get; }
        public string ReportText { get; }
    }

    internal readonly struct CharacterTestCharacterSpawnPerformanceSummary
    {
        public CharacterTestCharacterSpawnPerformanceSummary(
            int pendingRequestCount,
            int lastProcessedRequestCount,
            int lastSpawnedCount,
            int lastFailedCount,
            int totalSpawnedCount,
            int totalFailedCount)
        {
            PendingRequestCount = Math.Max(0, pendingRequestCount);
            LastProcessedRequestCount = Math.Max(0, lastProcessedRequestCount);
            LastSpawnedCount = Math.Max(0, lastSpawnedCount);
            LastFailedCount = Math.Max(0, lastFailedCount);
            TotalSpawnedCount = Math.Max(0, totalSpawnedCount);
            TotalFailedCount = Math.Max(0, totalFailedCount);
        }

        public static CharacterTestCharacterSpawnPerformanceSummary Empty => default;
        public int PendingRequestCount { get; }
        public int LastProcessedRequestCount { get; }
        public int LastSpawnedCount { get; }
        public int LastFailedCount { get; }
        public int TotalSpawnedCount { get; }
        public int TotalFailedCount { get; }
    }

    internal readonly struct CharacterTestCharacterViewSyncPerformanceSummary
    {
        public CharacterTestCharacterViewSyncPerformanceSummary(
            int activeViewCount,
            int lastSyncRequestCount,
            int lastSyncAppliedCount,
            int lastSyncMissCount,
            int lastTransformApplyCount,
            int totalSyncRequestCount,
            int totalSyncAppliedCount,
            int totalSyncMissCount,
            int totalTransformApplyCount)
        {
            ActiveViewCount = Math.Max(0, activeViewCount);
            LastSyncRequestCount = Math.Max(0, lastSyncRequestCount);
            LastSyncAppliedCount = Math.Max(0, lastSyncAppliedCount);
            LastSyncMissCount = Math.Max(0, lastSyncMissCount);
            LastTransformApplyCount = Math.Max(0, lastTransformApplyCount);
            TotalSyncRequestCount = Math.Max(0, totalSyncRequestCount);
            TotalSyncAppliedCount = Math.Max(0, totalSyncAppliedCount);
            TotalSyncMissCount = Math.Max(0, totalSyncMissCount);
            TotalTransformApplyCount = Math.Max(0, totalTransformApplyCount);
        }

        public static CharacterTestCharacterViewSyncPerformanceSummary Empty => default;
        public int ActiveViewCount { get; }
        public int LastSyncRequestCount { get; }
        public int LastSyncAppliedCount { get; }
        public int LastSyncMissCount { get; }
        public int LastTransformApplyCount { get; }
        public int TotalSyncRequestCount { get; }
        public int TotalSyncAppliedCount { get; }
        public int TotalSyncMissCount { get; }
        public int TotalTransformApplyCount { get; }
    }

}
