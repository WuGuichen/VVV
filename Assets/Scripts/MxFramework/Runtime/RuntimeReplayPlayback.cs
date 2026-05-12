using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Runtime
{
    public interface IRuntimeReplayFrameDriver
    {
        void Reset(RuntimeReplayHeader header);
        RuntimeReplayPlaybackFrameResult RunFrame(RuntimeReplayFrameRecord record);
    }

    public enum RuntimeReplayPlaybackFailureCode
    {
        None = 0,
        HashMismatch = 1,
        DriverException = 2
    }

    public sealed class RuntimeReplayPlaybackFrameResult
    {
        private readonly ReadOnlyCollection<RuntimeCommandError> _commandErrors;

        public RuntimeReplayPlaybackFrameResult(
            RuntimeFrame frame,
            long actualResultHash,
            string diagnosticsSummary = "",
            IReadOnlyList<RuntimeCommandError> commandErrors = null)
        {
            Frame = frame;
            ActualResultHash = actualResultHash;
            DiagnosticsSummary = diagnosticsSummary ?? string.Empty;
            _commandErrors = CopyCommandErrors(commandErrors);
        }

        public RuntimeFrame Frame { get; }
        public long ActualResultHash { get; }
        public string DiagnosticsSummary { get; }
        public IReadOnlyList<RuntimeCommandError> CommandErrors => _commandErrors;

        private static ReadOnlyCollection<RuntimeCommandError> CopyCommandErrors(IReadOnlyList<RuntimeCommandError> commandErrors)
        {
            if (commandErrors == null || commandErrors.Count == 0)
            {
                return new ReadOnlyCollection<RuntimeCommandError>(new List<RuntimeCommandError>());
            }

            var copy = new List<RuntimeCommandError>(commandErrors.Count);
            for (int i = 0; i < commandErrors.Count; i++)
            {
                copy.Add(commandErrors[i]);
            }

            return new ReadOnlyCollection<RuntimeCommandError>(copy);
        }
    }

    public sealed class RuntimeReplayPlaybackResult
    {
        private readonly ReadOnlyCollection<RuntimeCommand> _failureCommands;
        private readonly ReadOnlyCollection<RuntimeCommandError> _commandErrors;
        private readonly ReadOnlyCollection<RuntimeReplayPlaybackFrameResult> _frameResults;

        private RuntimeReplayPlaybackResult(
            bool success,
            int framesPlayed,
            RuntimeReplayPlaybackFailureCode failureCode,
            bool hasFailureFrame,
            RuntimeFrame failureFrame,
            long expectedResultHash,
            long actualResultHash,
            IReadOnlyList<RuntimeCommand> failureCommands,
            string expectedDiagnosticsSummary,
            string actualDiagnosticsSummary,
            IReadOnlyList<RuntimeCommandError> commandErrors,
            IReadOnlyList<RuntimeReplayPlaybackFrameResult> frameResults,
            string diagnosticsSummary,
            string failureMessage,
            Exception exception)
        {
            if (framesPlayed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(framesPlayed), "Replay playback frames played cannot be negative.");
            }

            Success = success;
            FramesPlayed = framesPlayed;
            FailureCode = failureCode;
            HasFailureFrame = hasFailureFrame;
            FailureFrame = failureFrame;
            ExpectedResultHash = expectedResultHash;
            ActualResultHash = actualResultHash;
            _failureCommands = CopyCommands(failureCommands);
            ExpectedDiagnosticsSummary = expectedDiagnosticsSummary ?? string.Empty;
            ActualDiagnosticsSummary = actualDiagnosticsSummary ?? string.Empty;
            _commandErrors = CopyCommandErrors(commandErrors);
            _frameResults = CopyFrameResults(frameResults);
            DiagnosticsSummary = diagnosticsSummary ?? string.Empty;
            FailureMessage = failureMessage ?? string.Empty;
            Exception = exception;
        }

        public bool Success { get; }
        public int FramesPlayed { get; }
        public RuntimeReplayPlaybackFailureCode FailureCode { get; }
        public bool HasFailureFrame { get; }
        public RuntimeFrame FailureFrame { get; }
        public long ExpectedResultHash { get; }
        public long ActualResultHash { get; }
        public IReadOnlyList<RuntimeCommand> FailureCommands => _failureCommands;
        public string ExpectedDiagnosticsSummary { get; }
        public string ActualDiagnosticsSummary { get; }
        public IReadOnlyList<RuntimeCommandError> CommandErrors => _commandErrors;
        public IReadOnlyList<RuntimeReplayPlaybackFrameResult> FrameResults => _frameResults;
        public string DiagnosticsSummary { get; }
        public string FailureMessage { get; }
        public Exception Exception { get; }

        internal static RuntimeReplayPlaybackResult Succeeded(
            int framesPlayed,
            string diagnosticsSummary,
            IReadOnlyList<RuntimeReplayPlaybackFrameResult> frameResults)
        {
            return new RuntimeReplayPlaybackResult(
                true,
                framesPlayed,
                RuntimeReplayPlaybackFailureCode.None,
                false,
                RuntimeFrame.Zero,
                0L,
                0L,
                null,
                string.Empty,
                string.Empty,
                null,
                frameResults,
                diagnosticsSummary,
                string.Empty,
                null);
        }

        internal static RuntimeReplayPlaybackResult HashMismatch(
            int framesPlayed,
            RuntimeReplayFrameRecord expected,
            RuntimeReplayPlaybackFrameResult actual,
            IReadOnlyList<RuntimeReplayPlaybackFrameResult> frameResults)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            if (actual == null)
            {
                throw new ArgumentNullException(nameof(actual));
            }

            string message = "Replay playback hash mismatch at frame "
                + expected.Frame
                + ". Expected="
                + expected.ResultHash
                + " Actual="
                + actual.ActualResultHash
                + ".";

            return new RuntimeReplayPlaybackResult(
                false,
                framesPlayed,
                RuntimeReplayPlaybackFailureCode.HashMismatch,
                true,
                expected.Frame,
                expected.ResultHash,
                actual.ActualResultHash,
                expected.Commands,
                expected.DiagnosticsSummary,
                actual.DiagnosticsSummary,
                actual.CommandErrors,
                frameResults,
                message,
                message,
                null);
        }

        internal static RuntimeReplayPlaybackResult DriverException(
            int framesPlayed,
            RuntimeReplayFrameRecord record,
            Exception exception,
            IReadOnlyList<RuntimeReplayPlaybackFrameResult> frameResults)
        {
            Exception failure = exception ?? new InvalidOperationException("Replay playback driver failed.");
            bool hasFailureFrame = record != null;
            RuntimeFrame failureFrame = hasFailureFrame ? record.Frame : RuntimeFrame.Zero;
            string frameText = hasFailureFrame ? " at frame " + failureFrame : " during reset";
            string message = "Replay playback driver exception"
                + frameText
                + ": "
                + failure.Message;

            return new RuntimeReplayPlaybackResult(
                false,
                framesPlayed,
                RuntimeReplayPlaybackFailureCode.DriverException,
                hasFailureFrame,
                failureFrame,
                hasFailureFrame ? record.ResultHash : 0L,
                0L,
                hasFailureFrame ? record.Commands : null,
                hasFailureFrame ? record.DiagnosticsSummary : string.Empty,
                string.Empty,
                null,
                frameResults,
                message,
                message,
                failure);
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

        private static ReadOnlyCollection<RuntimeCommandError> CopyCommandErrors(IReadOnlyList<RuntimeCommandError> commandErrors)
        {
            if (commandErrors == null || commandErrors.Count == 0)
            {
                return new ReadOnlyCollection<RuntimeCommandError>(new List<RuntimeCommandError>());
            }

            var copy = new List<RuntimeCommandError>(commandErrors.Count);
            for (int i = 0; i < commandErrors.Count; i++)
            {
                copy.Add(commandErrors[i]);
            }

            return new ReadOnlyCollection<RuntimeCommandError>(copy);
        }

        private static ReadOnlyCollection<RuntimeReplayPlaybackFrameResult> CopyFrameResults(IReadOnlyList<RuntimeReplayPlaybackFrameResult> frameResults)
        {
            if (frameResults == null || frameResults.Count == 0)
            {
                return new ReadOnlyCollection<RuntimeReplayPlaybackFrameResult>(new List<RuntimeReplayPlaybackFrameResult>());
            }

            var copy = new List<RuntimeReplayPlaybackFrameResult>(frameResults.Count);
            for (int i = 0; i < frameResults.Count; i++)
            {
                RuntimeReplayPlaybackFrameResult frameResult = frameResults[i];
                if (frameResult != null)
                {
                    copy.Add(frameResult);
                }
            }

            return new ReadOnlyCollection<RuntimeReplayPlaybackFrameResult>(copy);
        }
    }

    public sealed class RuntimeReplayPlaybackRunner
    {
        public RuntimeReplayPlaybackResult Play(RuntimeReplaySnapshot snapshot, IRuntimeReplayFrameDriver driver)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (driver == null)
            {
                throw new ArgumentNullException(nameof(driver));
            }

            var frameResults = new List<RuntimeReplayPlaybackFrameResult>(snapshot.Count);
            try
            {
                driver.Reset(snapshot.Header);
            }
            catch (Exception exception)
            {
                return RuntimeReplayPlaybackResult.DriverException(0, null, exception, frameResults);
            }

            if (snapshot.Count == 0)
            {
                return RuntimeReplayPlaybackResult.Succeeded(
                    0,
                    "Replay playback completed successfully: no frame records.",
                    frameResults);
            }

            for (int i = 0; i < snapshot.Records.Count; i++)
            {
                RuntimeReplayFrameRecord record = snapshot.Records[i];
                RuntimeReplayPlaybackFrameResult frameResult;
                try
                {
                    frameResult = driver.RunFrame(record);
                }
                catch (Exception exception)
                {
                    return RuntimeReplayPlaybackResult.DriverException(frameResults.Count, record, exception, frameResults);
                }

                if (frameResult == null)
                {
                    return RuntimeReplayPlaybackResult.DriverException(
                        frameResults.Count,
                        record,
                        new InvalidOperationException("Replay playback driver returned a null frame result."),
                        frameResults);
                }

                frameResults.Add(frameResult);
                int framesPlayed = frameResults.Count;
                if (record.ResultHash != frameResult.ActualResultHash)
                {
                    return RuntimeReplayPlaybackResult.HashMismatch(framesPlayed, record, frameResult, frameResults);
                }
            }

            return RuntimeReplayPlaybackResult.Succeeded(
                frameResults.Count,
                "Replay playback completed successfully. FramesPlayed=" + frameResults.Count + ".",
                frameResults);
        }
    }
}
