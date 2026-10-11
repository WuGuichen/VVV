using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using MxFramework.Diagnostics;
using Unity.Profiling;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestPerformancePlayerReportRunner : IDisposable
    {
        public const string ReportDirectoryArgument = "-character-test-performance-report-dir";
        public const string NoQuitArgument = "-character-test-performance-no-quit";
        public const string TimingReportFileName = "CharacterTestPerformancePlayerWarmRunReport.txt";
        public const string ProfilerRecorderReportFileName = "CharacterTestPerformancePlayerProfilerRecorderReport.txt";
        public const string AnimationReportFileName = "CharacterTestPerformancePlayerAnimationReport.txt";
        public const string RunReportFileName = "CharacterTestPerformancePlayerRunReport.txt";
        public const string TimingScenarioName = "character-test.player-development.warm-run";
        public const string ProfilerRecorderScenarioName = "character-test.player-development.profiler-recorder";
        public const string AnimationScenarioName = "character-test.player-development.animation-smoke";
        public const int DefaultWarmupFrameCount = 60;
        public const int DefaultCaptureFrameCount = 120;

        private static readonly string[] ProfilerMarkerNames =
        {
            "CharacterTest.GameManager.Simulation",
            "CharacterTest.GameManager.SlicePresentation",
            "CharacterTest.GameManager.UiRefresh",
            "CharacterTest.GameManager.UiTick",
            "CharacterTest.GameSlice.HostTick",
            "CharacterTest.RuntimeModule.CharacterTest.CharacterRuntime",
            "CharacterTest.CharacterRuntime.Motion",
            "CharacterTest.CharacterRuntime.ViewSync",
            "CharacterTest.CharacterRuntime.AnimationSync",
            "CharacterTest.UI.CombatHudRefresh",
            "CharacterTest.UI.FloatingTextRefresh",
            "CharacterTest.UI.StoryDialogRefresh"
        };

        private readonly CharacterTestRuntimeContext _context;
        private readonly string _reportDirectory;
        private readonly bool _quitOnComplete;
        private readonly int _warmupFrameCount;
        private readonly int _captureFrameCount;
        private readonly long[] _elapsedTicks;
        private readonly long[] _allocatedBytes;
        private CharacterTestPlayerProfilerRecorderCapture _profilerCapture;
        private CharacterTestPerformancePlayerReportRunState _state;
        private int _warmupFrames;
        private int _captureFrames;
        private long _startFrame;
        private long _captureStartFrame;
        private long _frameTickBefore;
        private long _frameAllocatedBytesBefore;
        private bool _frameStarted;
        private bool _disposed;
        private CharacterTestPlayerGameplayProbe _gameplayProbe;

        private CharacterTestPerformancePlayerReportRunner(
            CharacterTestRuntimeContext context,
            string reportDirectory,
            bool quitOnComplete,
            int warmupFrameCount,
            int captureFrameCount)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _reportDirectory = reportDirectory;
            _quitOnComplete = quitOnComplete;
            _warmupFrameCount = Math.Max(0, warmupFrameCount);
            _captureFrameCount = Math.Max(1, captureFrameCount);
            _elapsedTicks = new long[_captureFrameCount];
            _allocatedBytes = new long[_captureFrameCount];
        }

        public string ReportDirectory => _reportDirectory;

        /// <remarks>AllocByDesign: command-line diagnostics bootstrap; normal gameplay does not call this path.</remarks>
        public static CharacterTestPerformancePlayerReportRunner TryCreateFromCommandLine(
            CharacterTestRuntimeContext context)
        {
            if (!TryResolveReportDirectory(Environment.GetCommandLineArgs(), out string reportDirectory))
                return null;

            bool quitOnComplete = !HasArgument(Environment.GetCommandLineArgs(), NoQuitArgument);
            return new CharacterTestPerformancePlayerReportRunner(
                context,
                reportDirectory,
                quitOnComplete,
                DefaultWarmupFrameCount,
                DefaultCaptureFrameCount);
        }

        /// <remarks>AllocByDesign: command-line diagnostics argument parsing.</remarks>
        internal static bool TryResolveReportDirectory(string[] args, out string reportDirectory)
        {
            reportDirectory = null;
            if (!TryGetArgumentValue(args, ReportDirectoryArgument, out string value))
                return false;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            reportDirectory = Path.GetFullPath(value);
            return true;
        }

        public void BeginFrame()
        {
            if (_disposed || _state == CharacterTestPerformancePlayerReportRunState.Completed)
                return;

            try
            {
                BeginFrameCore();
            }
            catch (Exception ex)
            {
                CompleteFailed(ex.ToString());
            }
        }

        public void EndFrame()
        {
            if (_disposed || _state == CharacterTestPerformancePlayerReportRunState.Completed)
                return;

            try
            {
                EndFrameCore();
            }
            catch (Exception ex)
            {
                CompleteFailed(ex.ToString());
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _gameplayProbe?.Dispose();
            _profilerCapture?.Dispose();
            _profilerCapture = null;
        }

        private void BeginFrameCore()
        {
            if (_context.Slice == null)
                return;

            if (_state == CharacterTestPerformancePlayerReportRunState.WaitingForRuntime)
            {
                Directory.CreateDirectory(_reportDirectory);
                CharacterTestMainMenuCommandResult start =
                    _context.MainMenuCoordinator.CloseMainMenu();
                if (!start.Success)
                {
                    CompleteFailed(start.Message);
                    return;
                }

                _startFrame = _context.Slice.CurrentFrame.Value;
                _gameplayProbe = new CharacterTestPlayerGameplayProbe(_context);
                _state = CharacterTestPerformancePlayerReportRunState.GameplayProbe;
            }

            if (_state == CharacterTestPerformancePlayerReportRunState.GameplayProbe)
            {
                _gameplayProbe.BeginFrame();
                return;
            }

            if (_state == CharacterTestPerformancePlayerReportRunState.Warmup)
                return;

            if (_state == CharacterTestPerformancePlayerReportRunState.CapturePending)
                StartCapture();

            if (_state != CharacterTestPerformancePlayerReportRunState.Capturing)
                return;

            _frameStarted = true;
            _ = GC.GetAllocatedBytesForCurrentThread();
            _frameAllocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();
            _frameTickBefore = Stopwatch.GetTimestamp();
        }

        private void EndFrameCore()
        {
            if (_context.Slice == null)
                return;

            if (_state == CharacterTestPerformancePlayerReportRunState.GameplayProbe)
            {
                _gameplayProbe.EndFrame();
                if (!_gameplayProbe.Completed) return;
                _gameplayProbe.WriteTrace(_reportDirectory);
                if (!_gameplayProbe.Passed)
                {
                    CompleteFailed(_gameplayProbe.Issue);
                    return;
                }
                _state = CharacterTestPerformancePlayerReportRunState.Warmup;
                _startFrame = _context.Slice.CurrentFrame.Value;
                return;
            }

            if (_state == CharacterTestPerformancePlayerReportRunState.Warmup)
            {
                _warmupFrames++;
                if (_warmupFrames >= _warmupFrameCount)
                    _state = CharacterTestPerformancePlayerReportRunState.CapturePending;
                return;
            }

            if (_state != CharacterTestPerformancePlayerReportRunState.Capturing || !_frameStarted)
                return;

            long tickAfter = Stopwatch.GetTimestamp();
            long allocationAfter = GC.GetAllocatedBytesForCurrentThread();
            _elapsedTicks[_captureFrames] = Math.Max(0L, tickAfter - _frameTickBefore);
            _allocatedBytes[_captureFrames] = Math.Max(0L, allocationAfter - _frameAllocatedBytesBefore);
            _profilerCapture?.Sample();
            _frameStarted = false;
            _captureFrames++;
            if (_captureFrames >= _captureFrameCount)
                CompleteSucceeded();
        }

        private void StartCapture()
        {
            _captureStartFrame = _context.Slice.CurrentFrame.Value;
            _profilerCapture = CharacterTestPlayerProfilerRecorderCapture.Start(
                ProfilerMarkerNames,
                _captureFrameCount);
            _state = CharacterTestPerformancePlayerReportRunState.Capturing;
        }

        /// <remarks>AllocByDesign: writes explicit Player profile evidence into Temp.</remarks>
        private void CompleteSucceeded()
        {
            long endFrame = _context.Slice != null ? _context.Slice.CurrentFrame.Value : 0L;
            string timingPath = Path.Combine(_reportDirectory, TimingReportFileName);
            string profilerPath = Path.Combine(_reportDirectory, ProfilerRecorderReportFileName);
            string animationPath = Path.Combine(_reportDirectory, AnimationReportFileName);
            string runPath = Path.Combine(_reportDirectory, RunReportFileName);

            FrameworkPerformanceCounterSnapshot uiSnapshot =
                _context.UiRuntime?.CreatePerformanceCounterSnapshot();
            CharacterTestAnimationViewDiagnostics animationDiagnostics =
                _context.Slice.Services.CharacterRuntime.ViewBridge.CreateAnimationDiagnostics();
            string timingReport = CharacterTestPerformanceCounterReport.CreateTimingText(
                TimingScenarioName,
                "development-player-game-manager-update",
                _startFrame,
                _captureStartFrame,
                endFrame,
                _warmupFrameCount,
                _captureFrameCount,
                // TimeBoundary: this Player-only diagnostics runner reports the Unity fixed delta for the sampled scenario.
                Time.fixedDeltaTime,
                Stopwatch.Frequency,
                _elapsedTicks,
                _allocatedBytes,
                _context.Slice.Services.CreateInteractionPerformanceCounterSnapshot(),
                _context.Slice.Services.CreateRuntimePerformanceCounterSnapshot(),
                uiSnapshot);

            File.WriteAllText(timingPath, timingReport);
            File.WriteAllText(
                profilerPath,
                _profilerCapture.CreateReportText(
                    ProfilerRecorderScenarioName,
                    "development-player-profiler-recorder",
                    _startFrame,
                    _captureStartFrame,
                    endFrame,
                    _warmupFrameCount,
                    _captureFrameCount));
            File.WriteAllText(
                animationPath,
                animationDiagnostics.CreateReportText(AnimationScenarioName));
            File.WriteAllText(
                runPath,
                CreateRunReportText("finished", null, timingPath, profilerPath, animationPath));
            _state = CharacterTestPerformancePlayerReportRunState.Completed;
            Dispose();
            QuitIfNeeded();
        }

        /// <remarks>AllocByDesign: writes explicit Player profile failure evidence into Temp.</remarks>
        private void CompleteFailed(string issue)
        {
            try
            {
                Directory.CreateDirectory(_reportDirectory);
                string runPath = Path.Combine(_reportDirectory, RunReportFileName);
                File.WriteAllText(
                    runPath,
                    CreateRunReportText("failed", issue, null, null, null));
            }
            finally
            {
                _state = CharacterTestPerformancePlayerReportRunState.Completed;
                Dispose();
                QuitIfNeeded();
            }
        }

        private string CreateRunReportText(
            string status,
            string issue,
            string timingReportPath,
            string profilerRecorderReportPath,
            string animationReportPath)
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("scenario: character-test.player-development.build-run");
            builder.Append("status: ").AppendLine(string.IsNullOrWhiteSpace(status) ? "unknown" : status);
#if ENABLE_IL2CPP
            builder.AppendLine("scriptingBackend: IL2CPP");
#else
            builder.AppendLine("scriptingBackend: Mono");
#endif
            builder.Append("developmentBuild: ").AppendLine(UnityEngine.Debug.isDebugBuild ? "true" : "false");
            builder.Append("reportDirectory: ").AppendLine(_reportDirectory);
            builder.Append("warmupFrames: ").AppendLine(_warmupFrameCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("captureFrames: ").AppendLine(_captureFrameCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("startFrame: ").AppendLine(Math.Max(0L, _startFrame).ToString(CultureInfo.InvariantCulture));
            builder.Append("captureStartFrame: ")
                .AppendLine(Math.Max(0L, _captureStartFrame).ToString(CultureInfo.InvariantCulture));
            builder.Append("capturedFrames: ").AppendLine(_captureFrames.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(timingReportPath))
                builder.Append("timingReport: ").AppendLine(timingReportPath);
            if (!string.IsNullOrWhiteSpace(profilerRecorderReportPath))
                builder.Append("profilerRecorderReport: ").AppendLine(profilerRecorderReportPath);
            if (!string.IsNullOrWhiteSpace(animationReportPath))
                builder.Append("animationReport: ").AppendLine(animationReportPath);
            builder.Append(CharacterTestRuntimeCatalogLoadDiagnostics.CreateReportText());
            _gameplayProbe?.AppendTo(builder);
            if (!string.IsNullOrWhiteSpace(issue))
                builder.Append("issue: ").AppendLine(issue.Replace('\n', ' '));
            return builder.ToString();
        }

        private void QuitIfNeeded()
        {
            if (!_quitOnComplete || Application.isEditor)
                return;

            Application.Quit();
        }

        private static bool HasArgument(string[] args, string argument)
        {
            if (args == null || string.IsNullOrWhiteSpace(argument))
                return false;

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool TryGetArgumentValue(
            string[] args,
            string argument,
            out string value)
        {
            value = null;
            if (args == null || string.IsNullOrWhiteSpace(argument))
                return false;

            string assignmentPrefix = argument + "=";
            for (int i = 0; i < args.Length; i++)
            {
                string item = args[i];
                if (string.IsNullOrWhiteSpace(item))
                    continue;

                if (item.StartsWith(assignmentPrefix, StringComparison.Ordinal))
                {
                    value = item.Substring(assignmentPrefix.Length);
                    return true;
                }

                if (!string.Equals(item, argument, StringComparison.Ordinal))
                    continue;

                if (i + 1 >= args.Length)
                    return false;

                value = args[i + 1];
                return true;
            }

            return false;
        }

        private enum CharacterTestPerformancePlayerReportRunState
        {
            WaitingForRuntime,
            GameplayProbe,
            Warmup,
            CapturePending,
            Capturing,
            Completed
        }

        private sealed class CharacterTestPlayerProfilerRecorderCapture : IDisposable
        {
            private readonly string[] _markerNames;
            private readonly ProfilerRecorder[] _recorders;
            private readonly long[][] _samples;
            private readonly bool[] _valid;
            private int _sampleCount;

            private CharacterTestPlayerProfilerRecorderCapture(string[] markerNames, int sampleCapacity)
            {
                _markerNames = markerNames;
                _recorders = new ProfilerRecorder[markerNames.Length];
                _samples = new long[markerNames.Length][];
                _valid = new bool[markerNames.Length];
                for (int i = 0; i < markerNames.Length; i++)
                {
                    _recorders[i] = ProfilerRecorder.StartNew(
                        ProfilerCategory.Scripts,
                        markerNames[i],
                        capacity: 1);
                    _valid[i] = _recorders[i].Valid;
                    _samples[i] = new long[Math.Max(1, sampleCapacity)];
                }
            }

            /// <remarks>AllocByDesign: command-line Player profiler evidence capture.</remarks>
            public static CharacterTestPlayerProfilerRecorderCapture Start(
                string[] markerNames,
                int sampleCapacity)
            {
                return new CharacterTestPlayerProfilerRecorderCapture(
                    markerNames ?? Array.Empty<string>(),
                    Math.Max(1, sampleCapacity));
            }

            public void Sample()
            {
                if (_samples.Length == 0 || _sampleCount >= _samples[0].Length)
                    return;

                for (int i = 0; i < _recorders.Length; i++)
                {
                    long value = _valid[i] ? _recorders[i].LastValue : 0L;
                    _samples[i][_sampleCount] = Math.Max(0L, value);
                }

                _sampleCount++;
            }

            /// <remarks>AllocByDesign: creates an explicit profiler evidence report for Player/manual review.</remarks>
            public string CreateReportText(
                string scenarioName,
                string profileKind,
                long startFrame,
                long captureStartFrame,
                long frame,
                int warmupFrameCount,
                int captureFrameCount)
            {
                var builder = new StringBuilder(4096);
                builder.Append("scenario: ").AppendLine(scenarioName);
                builder.Append("profileKind: ").AppendLine(profileKind);
                builder.Append("startFrame: ")
                    .AppendLine(Math.Max(0L, startFrame).ToString(CultureInfo.InvariantCulture));
                builder.Append("captureStartFrame: ")
                    .AppendLine(Math.Max(0L, captureStartFrame).ToString(CultureInfo.InvariantCulture));
                builder.Append("frame: ").AppendLine(Math.Max(0L, frame).ToString(CultureInfo.InvariantCulture));
                builder.Append("warmupFrames: ").AppendLine(Math.Max(0, warmupFrameCount).ToString(CultureInfo.InvariantCulture));
                builder.Append("captureFrames: ").AppendLine(Math.Max(1, captureFrameCount).ToString(CultureInfo.InvariantCulture));
                builder.Append("sampledFrames: ").AppendLine(_sampleCount.ToString(CultureInfo.InvariantCulture));
                builder.Append("markerCount: ").AppendLine(_markerNames.Length.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("markers:");

                for (int i = 0; i < _markerNames.Length; i++)
                    AppendMarkerStats(builder, _markerNames[i], _valid[i], _samples[i], _sampleCount);

                return builder.ToString();
            }

            public void Dispose()
            {
                for (int i = 0; i < _recorders.Length; i++)
                    _recorders[i].Dispose();
            }

            private static void AppendMarkerStats(
                StringBuilder builder,
                string markerName,
                bool available,
                long[] samples,
                int sampleCount)
            {
                long[] sorted = CopyAndSort(samples, sampleCount);
                builder.Append("- marker: ").AppendLine(markerName);
                builder.Append("  available: ").AppendLine(available ? "true" : "false");
                builder.Append("  samples: ").AppendLine(Math.Max(0, sampleCount).ToString(CultureInfo.InvariantCulture));
                builder.Append("  elapsedMs.min: ").AppendLine(FormatNanoseconds(sorted.Length > 0 ? sorted[0] : 0L));
                builder.Append("  elapsedMs.median: ").AppendLine(FormatNanoseconds(Percentile(sorted, 0.5d)));
                builder.Append("  elapsedMs.mean: ").AppendLine(CalculateMeanMilliseconds(sorted).ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append("  elapsedMs.p75: ").AppendLine(FormatNanoseconds(Percentile(sorted, 0.75d)));
                builder.Append("  elapsedMs.p90: ").AppendLine(FormatNanoseconds(Percentile(sorted, 0.9d)));
                builder.Append("  elapsedMs.p95: ").AppendLine(FormatNanoseconds(Percentile(sorted, 0.95d)));
                builder.Append("  elapsedMs.max: ").AppendLine(FormatNanoseconds(sorted.Length > 0 ? sorted[sorted.Length - 1] : 0L));
            }

            private static long[] CopyAndSort(long[] samples, int sampleCount)
            {
                int safeCount = Math.Max(0, Math.Min(sampleCount, samples != null ? samples.Length : 0));
                var copy = new long[safeCount];
                for (int i = 0; i < safeCount; i++)
                    copy[i] = Math.Max(0L, samples[i]);

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

            private static double CalculateMeanMilliseconds(long[] sorted)
            {
                if (sorted == null || sorted.Length == 0)
                    return 0d;

                double total = 0d;
                for (int i = 0; i < sorted.Length; i++)
                    total += sorted[i];

                return total / sorted.Length / 1000000d;
            }

            private static string FormatNanoseconds(long nanoseconds)
            {
                double milliseconds = Math.Max(0L, nanoseconds) / 1000000d;
                return milliseconds.ToString("0.###", CultureInfo.InvariantCulture);
            }
        }
    }
}
