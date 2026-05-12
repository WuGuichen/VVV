using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using MxFramework.Runtime;

namespace MxFramework.Tests.Runtime
{
    internal sealed class RuntimeGoldenReplayFixture
    {
        private readonly ReadOnlyCollection<RuntimeGoldenReplayFrameFixture> _frames;
        private readonly ReadOnlyCollection<string> _expectedDiagnosticsKeywords;

        public RuntimeGoldenReplayFixture(
            string name,
            RuntimeReplayHeader header,
            IReadOnlyList<RuntimeGoldenReplayFrameFixture> frames,
            long expectedFinalHash,
            IReadOnlyList<string> expectedDiagnosticsKeywords)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Golden replay fixture name cannot be empty.", nameof(name));
            }

            Name = name;
            Header = header;
            _frames = Copy(frames);
            ExpectedFinalHash = expectedFinalHash;
            _expectedDiagnosticsKeywords = Copy(expectedDiagnosticsKeywords);
        }

        public string Name { get; }
        public RuntimeReplayHeader Header { get; }
        public IReadOnlyList<RuntimeGoldenReplayFrameFixture> Frames => _frames;
        public long ExpectedFinalHash { get; }
        public IReadOnlyList<string> ExpectedDiagnosticsKeywords => _expectedDiagnosticsKeywords;

        public RuntimeReplaySnapshot CreateSnapshot()
        {
            var records = new List<RuntimeReplayFrameRecord>(_frames.Count);
            for (int i = 0; i < _frames.Count; i++)
            {
                records.Add(_frames[i].CreateRecord());
            }

            return new RuntimeReplaySnapshot(Header, records);
        }

        public RuntimeGoldenReplayFixture WithExpectedFrameHash(int frameIndex, long expectedHash)
        {
            if (frameIndex < 0 || frameIndex >= _frames.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex));
            }

            var frames = new List<RuntimeGoldenReplayFrameFixture>(_frames.Count);
            for (int i = 0; i < _frames.Count; i++)
            {
                frames.Add(i == frameIndex ? _frames[i].WithExpectedHash(expectedHash) : _frames[i]);
            }

            return new RuntimeGoldenReplayFixture(Name, Header, frames, ExpectedFinalHash, _expectedDiagnosticsKeywords);
        }

        private static ReadOnlyCollection<T> Copy<T>(IReadOnlyList<T> values)
        {
            if (values == null || values.Count == 0)
            {
                return new ReadOnlyCollection<T>(new List<T>());
            }

            var copy = new List<T>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                copy.Add(values[i]);
            }

            return new ReadOnlyCollection<T>(copy);
        }
    }

    internal sealed class RuntimeGoldenReplayFrameFixture
    {
        private readonly ReadOnlyCollection<RuntimeCommand> _commands;

        public RuntimeGoldenReplayFrameFixture(
            RuntimeFrame frame,
            IReadOnlyList<RuntimeCommand> commands,
            long expectedHash,
            string diagnosticsSummary)
        {
            Frame = frame;
            _commands = CopyCommands(commands);
            ExpectedHash = expectedHash;
            DiagnosticsSummary = diagnosticsSummary ?? string.Empty;
        }

        public RuntimeFrame Frame { get; }
        public IReadOnlyList<RuntimeCommand> Commands => _commands;
        public long ExpectedHash { get; }
        public string DiagnosticsSummary { get; }

        public RuntimeReplayFrameRecord CreateRecord()
        {
            return new RuntimeReplayFrameRecord(Frame, _commands, ExpectedHash, DiagnosticsSummary);
        }

        public RuntimeGoldenReplayFrameFixture WithExpectedHash(long expectedHash)
        {
            return new RuntimeGoldenReplayFrameFixture(Frame, _commands, expectedHash, DiagnosticsSummary);
        }

        private static ReadOnlyCollection<RuntimeCommand> CopyCommands(IReadOnlyList<RuntimeCommand> commands)
        {
            if (commands == null || commands.Count == 0)
            {
                return new ReadOnlyCollection<RuntimeCommand>(new List<RuntimeCommand>());
            }

            var copy = new List<RuntimeCommand>(commands.Count);
            for (int i = 0; i < commands.Count; i++)
            {
                copy.Add(commands[i]);
            }

            return new ReadOnlyCollection<RuntimeCommand>(copy);
        }
    }

    internal sealed class RuntimeGoldenReplayHarness
    {
        private readonly IRuntimeGoldenReplayDriver _driver;

        public RuntimeGoldenReplayHarness(IRuntimeGoldenReplayDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public RuntimeGoldenReplayHarnessResult Run(RuntimeGoldenReplayFixture fixture)
        {
            if (fixture == null)
            {
                throw new ArgumentNullException(nameof(fixture));
            }

            RuntimeReplaySnapshot snapshot = fixture.CreateSnapshot();
            RuntimeGoldenReplayPlaybackResult playback = _driver.Play(snapshot);

            if (playback.FrameResults.Count != snapshot.Count)
            {
                return RuntimeGoldenReplayHarnessResult.Failed(
                    playback.FinalHash,
                    playback.DiagnosticsSummary,
                    "fixture=" + fixture.Name
                    + " frameCountMismatch expected=" + snapshot.Count
                    + " actual=" + playback.FrameResults.Count);
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                RuntimeReplayFrameRecord expected = snapshot.Records[i];
                RuntimeGoldenReplayFrameResult actual = playback.FrameResults[i];

                if (expected.Frame != actual.Frame)
                {
                    return RuntimeGoldenReplayHarnessResult.Failed(
                        playback.FinalHash,
                        playback.DiagnosticsSummary,
                        "fixture=" + fixture.Name
                        + " frameIndex=" + i
                        + " expectedFrame=" + expected.Frame
                        + " actualFrame=" + actual.Frame);
                }

                if (expected.ResultHash != actual.ResultHash)
                {
                    return RuntimeGoldenReplayHarnessResult.Failed(
                        playback.FinalHash,
                        playback.DiagnosticsSummary,
                        BuildFrameMismatchReport(fixture.Name, expected, actual));
                }
            }

            if (fixture.ExpectedFinalHash != playback.FinalHash)
            {
                return RuntimeGoldenReplayHarnessResult.Failed(
                    playback.FinalHash,
                    playback.DiagnosticsSummary,
                    "fixture=" + fixture.Name
                    + " finalHashMismatch expected=" + fixture.ExpectedFinalHash
                    + " actual=" + playback.FinalHash
                    + " diagnostics=" + playback.DiagnosticsSummary);
            }

            for (int i = 0; i < fixture.ExpectedDiagnosticsKeywords.Count; i++)
            {
                string keyword = fixture.ExpectedDiagnosticsKeywords[i];
                if (!ContainsOrdinal(playback.DiagnosticsSummary, keyword))
                {
                    return RuntimeGoldenReplayHarnessResult.Failed(
                        playback.FinalHash,
                        playback.DiagnosticsSummary,
                        "fixture=" + fixture.Name
                        + " diagnosticsKeywordMissing keyword=" + keyword
                        + " diagnostics=" + playback.DiagnosticsSummary);
                }
            }

            return RuntimeGoldenReplayHarnessResult.Passed(playback.FinalHash, playback.DiagnosticsSummary);
        }

        private static string BuildFrameMismatchReport(
            string fixtureName,
            RuntimeReplayFrameRecord expected,
            RuntimeGoldenReplayFrameResult actual)
        {
            return "fixture=" + fixtureName
                + " frame=" + expected.Frame
                + " hashMismatch expected=" + expected.ResultHash
                + " actual=" + actual.ResultHash
                + " commands=" + JoinTraceIds(expected.Commands)
                + " expectedDiagnostics=" + expected.DiagnosticsSummary
                + " actualDiagnostics=" + actual.DiagnosticsSummary;
        }

        private static string JoinTraceIds(IReadOnlyList<RuntimeCommand> commands)
        {
            if (commands == null || commands.Count == 0)
            {
                return string.Empty;
            }

            var traceIds = new string[commands.Count];
            for (int i = 0; i < commands.Count; i++)
            {
                traceIds[i] = commands[i].TraceId;
            }

            return string.Join(",", traceIds);
        }

        private static bool ContainsOrdinal(string text, string value)
        {
            return text != null
                && value != null
                && text.IndexOf(value, StringComparison.Ordinal) >= 0;
        }
    }

    internal interface IRuntimeGoldenReplayDriver
    {
        RuntimeGoldenReplayPlaybackResult Play(RuntimeReplaySnapshot snapshot);
    }

    internal sealed class RuntimeGoldenReplayPlaybackResult
    {
        private readonly ReadOnlyCollection<RuntimeGoldenReplayFrameResult> _frameResults;

        public RuntimeGoldenReplayPlaybackResult(
            long finalHash,
            IReadOnlyList<RuntimeGoldenReplayFrameResult> frameResults,
            string diagnosticsSummary)
        {
            FinalHash = finalHash;
            _frameResults = Copy(frameResults);
            DiagnosticsSummary = diagnosticsSummary ?? string.Empty;
        }

        public long FinalHash { get; }
        public IReadOnlyList<RuntimeGoldenReplayFrameResult> FrameResults => _frameResults;
        public string DiagnosticsSummary { get; }

        private static ReadOnlyCollection<RuntimeGoldenReplayFrameResult> Copy(IReadOnlyList<RuntimeGoldenReplayFrameResult> values)
        {
            if (values == null || values.Count == 0)
            {
                return new ReadOnlyCollection<RuntimeGoldenReplayFrameResult>(new List<RuntimeGoldenReplayFrameResult>());
            }

            var copy = new List<RuntimeGoldenReplayFrameResult>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                copy.Add(values[i]);
            }

            return new ReadOnlyCollection<RuntimeGoldenReplayFrameResult>(copy);
        }
    }

    internal sealed class RuntimeGoldenReplayFrameResult
    {
        public RuntimeGoldenReplayFrameResult(RuntimeFrame frame, long resultHash, string diagnosticsSummary)
        {
            Frame = frame;
            ResultHash = resultHash;
            DiagnosticsSummary = diagnosticsSummary ?? string.Empty;
        }

        public RuntimeFrame Frame { get; }
        public long ResultHash { get; }
        public string DiagnosticsSummary { get; }
    }

    internal sealed class RuntimeGoldenReplayHarnessResult
    {
        private RuntimeGoldenReplayHarnessResult(
            bool success,
            long finalHash,
            string diagnosticsSummary,
            string failureReport)
        {
            Success = success;
            FinalHash = finalHash;
            DiagnosticsSummary = diagnosticsSummary ?? string.Empty;
            FailureReport = failureReport ?? string.Empty;
        }

        public bool Success { get; }
        public long FinalHash { get; }
        public string DiagnosticsSummary { get; }
        public string FailureReport { get; }

        public static RuntimeGoldenReplayHarnessResult Passed(long finalHash, string diagnosticsSummary)
        {
            return new RuntimeGoldenReplayHarnessResult(true, finalHash, diagnosticsSummary, string.Empty);
        }

        public static RuntimeGoldenReplayHarnessResult Failed(long finalHash, string diagnosticsSummary, string failureReport)
        {
            return new RuntimeGoldenReplayHarnessResult(false, finalHash, diagnosticsSummary, failureReport);
        }
    }

    internal sealed class FakeRuntimeGoldenReplayDriver : IRuntimeGoldenReplayDriver
    {
        private long _counter;

        public RuntimeGoldenReplayPlaybackResult Play(RuntimeReplaySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            _counter = 0L;
            var frameResults = new List<RuntimeGoldenReplayFrameResult>(snapshot.Count);
            var diagnostics = new StringBuilder();

            for (int i = 0; i < snapshot.Count; i++)
            {
                RuntimeReplayFrameRecord record = snapshot.Records[i];
                ApplyCommands(record.Commands);

                long resultHash = (record.Frame.Value * 1000L) + _counter;
                string frameDiagnostics = "frame=" + record.Frame.Value
                    + " counter=" + _counter
                    + " commands=" + record.Commands.Count
                    + " traces=" + JoinTraceIds(record.Commands);

                frameResults.Add(new RuntimeGoldenReplayFrameResult(record.Frame, resultHash, frameDiagnostics));

                if (diagnostics.Length > 0)
                {
                    diagnostics.Append("; ");
                }

                diagnostics.Append(frameDiagnostics);
            }

            long finalHash = frameResults.Count == 0 ? 0L : frameResults[frameResults.Count - 1].ResultHash;
            return new RuntimeGoldenReplayPlaybackResult(finalHash, frameResults, diagnostics.ToString());
        }

        private void ApplyCommands(IReadOnlyList<RuntimeCommand> commands)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                switch (command.CommandId)
                {
                    case RuntimeGoldenReplaySyntheticCommands.Add:
                        _counter += command.Payload0;
                        break;
                    case RuntimeGoldenReplaySyntheticCommands.Multiply:
                        _counter *= command.Payload0;
                        break;
                    case RuntimeGoldenReplaySyntheticCommands.Subtract:
                        _counter -= command.Payload0;
                        break;
                }
            }
        }

        private static string JoinTraceIds(IReadOnlyList<RuntimeCommand> commands)
        {
            if (commands == null || commands.Count == 0)
            {
                return string.Empty;
            }

            var traceIds = new string[commands.Count];
            for (int i = 0; i < commands.Count; i++)
            {
                traceIds[i] = commands[i].TraceId;
            }

            return string.Join(",", traceIds);
        }
    }

    internal static class RuntimeGoldenReplayFixtures
    {
        public static RuntimeGoldenReplayFixture SyntheticCounter()
        {
            RuntimeFrame frame0 = RuntimeFrame.Zero;
            var frame1 = new RuntimeFrame(1);
            var header = new RuntimeReplayHeader(
                schemaVersion: 1,
                frameworkVersion: "golden-harness-test",
                configHash: "synthetic-config",
                resourceCatalogHash: "synthetic-resources",
                startFrame: frame0);

            return new RuntimeGoldenReplayFixture(
                "synthetic-counter",
                header,
                new[]
                {
                    new RuntimeGoldenReplayFrameFixture(
                        frame0,
                        new[]
                        {
                            Command(frame0, RuntimeGoldenReplaySyntheticCommands.Add, 3, "add-three", 0L),
                            Command(frame0, RuntimeGoldenReplaySyntheticCommands.Multiply, 5, "multiply-five", 1L)
                        },
                        expectedHash: 15L,
                        diagnosticsSummary: "frame=0 counter=15 commands=2"),
                    new RuntimeGoldenReplayFrameFixture(
                        frame1,
                        new[]
                        {
                            Command(frame1, RuntimeGoldenReplaySyntheticCommands.Add, 4, "add-four", 2L),
                            Command(frame1, RuntimeGoldenReplaySyntheticCommands.Subtract, 2, "subtract-two", 3L)
                        },
                        expectedHash: 1017L,
                        diagnosticsSummary: "frame=1 counter=17 commands=2")
                },
                expectedFinalHash: 1017L,
                expectedDiagnosticsKeywords: new[] { "counter=17", "commands=2" });
        }

        public static RuntimeGoldenReplayFixture SyntheticCounterWithFrameHashMismatch()
        {
            return SyntheticCounter().WithExpectedFrameHash(frameIndex: 1, expectedHash: 9999L);
        }

        private static RuntimeCommand Command(
            RuntimeFrame frame,
            int commandId,
            int payload0,
            string traceId,
            long sequence)
        {
            return new RuntimeCommand(
                frame,
                sourceId: 1,
                commandId: commandId,
                targetId: 100,
                payload0: payload0,
                payload1: 0,
                payload2: 0,
                traceId: traceId,
                sequence: sequence);
        }
    }

    internal static class RuntimeGoldenReplaySyntheticCommands
    {
        public const int Add = 1;
        public const int Multiply = 2;
        public const int Subtract = 3;
    }
}
