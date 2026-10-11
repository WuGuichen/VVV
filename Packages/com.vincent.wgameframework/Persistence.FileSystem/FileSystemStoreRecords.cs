using System;
using System.Collections.Generic;
using System.IO;

namespace MxFramework.Persistence.FileSystem
{
    internal enum FileSystemRecordReadStatus
    {
        Success = 0,
        RecoverableFailure = 1,
        TerminalFailure = 2
    }

    internal readonly struct FileSystemRecordReadResult
    {
        public FileSystemRecordReadResult(
            FileSystemRecordReadStatus status,
            SaveGameEncodedEnvelope encoded,
            SaveGameEnvelope envelope,
            SaveGameError error)
        {
            Status = status;
            Encoded = encoded;
            Envelope = envelope;
            Error = error;
        }

        public FileSystemRecordReadStatus Status { get; }
        public SaveGameEncodedEnvelope Encoded { get; }
        public SaveGameEnvelope Envelope { get; }
        public SaveGameError Error { get; }
    }

    internal static class FileSystemStoreRecords
    {
        public static bool TryCollectGenerationsDescending(
            FileSystemDirectoryEntry[] entries,
            SaveGameOperationPhase phase,
            out SaveGameGeneration[] generations,
            out SaveGameError error)
        {
            generations = null;
            if (!FileSystemGenerationLayout.TryFindHighest(
                    entries,
                    phase,
                    out _,
                    out error))
            {
                return false;
            }

            var collected = new List<SaveGameGeneration>(entries.Length);
            for (int index = 0; index < entries.Length; index++)
            {
                if (FileSystemGenerationLayout.TryParse(
                        entries[index].Name,
                        out SaveGameGeneration generation,
                        out _))
                {
                    collected.Add(generation);
                }
            }

            collected.Sort((left, right) => right.Value.CompareTo(left.Value));
            generations = collected.ToArray();
            error = SaveGameError.None;
            return true;
        }

        public static FileSystemRecordReadResult Read(
            string path,
            SaveGameProfileId expectedProfile,
            SaveGameSlotId expectedSlot,
            SaveGameGeneration expectedGeneration,
            IFileSystemBackend backend,
            IFileSystemDiagnosticSink diagnostics,
            SaveGameOperationPhase phase,
            string logicalPath)
        {
            try
            {
                FileSystemEntryStatus status = backend.GetStatus(path);
                if (status.Kind == FileSystemEntryKind.Missing)
                {
                    return Terminal(
                        SaveGameErrorCode.GenerationNotFound,
                        phase,
                        "generation",
                        "Generation file does not exist.");
                }

                if (status.Kind != FileSystemEntryKind.File)
                {
                    return Recoverable(
                        SaveGameErrorCode.InvalidEnvelope,
                        phase,
                        logicalPath,
                        "Generation entry is not a regular file.");
                }

                if (status.Length > SaveGameLimits.MaximumEnvelopeBytes)
                {
                    return Recoverable(
                        SaveGameErrorCode.LimitExceeded,
                        phase,
                        logicalPath,
                        "Stored envelope exceeds the hard read bound.");
                }

                if (status.Length <= 0)
                {
                    return Recoverable(
                        SaveGameErrorCode.InvalidEnvelope,
                        phase,
                        logicalPath,
                        "Stored envelope length is invalid.");
                }

                var bytes = new byte[(int)status.Length];
                using (Stream stream = backend.OpenRead(path))
                {
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read <= 0)
                        {
                            return Recoverable(
                                SaveGameErrorCode.InvalidEnvelope,
                                phase,
                                logicalPath,
                                "Stored envelope ended before its observed length.");
                        }

                        offset += read;
                    }

                    if (stream.ReadByte() != -1)
                    {
                        return Recoverable(
                            SaveGameErrorCode.InvalidEnvelope,
                            phase,
                            logicalPath,
                            "Stored envelope exceeded its observed length.");
                    }
                }

                SaveGameEnvelopeResult decoded = SaveGameEnvelopeCodec.Decode(bytes);
                if (!decoded.IsSuccess)
                {
                    SaveGameError sanitized = Sanitize(decoded.Error);
                    return IsCodecRecoverable(sanitized.Code)
                        ? new FileSystemRecordReadResult(
                            FileSystemRecordReadStatus.RecoverableFailure,
                            null,
                            null,
                            sanitized)
                        : new FileSystemRecordReadResult(
                            FileSystemRecordReadStatus.TerminalFailure,
                            null,
                            null,
                            sanitized);
                }

                SaveGameEnvelope envelope = decoded.Envelope;
                if (envelope.Profile != expectedProfile
                    || envelope.Slot != expectedSlot
                    || envelope.Generation != expectedGeneration)
                {
                    return Recoverable(
                        SaveGameErrorCode.EnvelopePayloadMismatch,
                        phase,
                        logicalPath,
                        "Generation filename and envelope identity do not match.");
                }

                return new FileSystemRecordReadResult(
                    FileSystemRecordReadStatus.Success,
                    decoded.Encoded,
                    envelope,
                    SaveGameError.None);
            }
            catch (Exception exception)
            {
                FileSystemDiagnosticReporter.ReportBestEffort(
                    diagnostics,
                    "record-read",
                    path,
                    exception);
                return Terminal(
                    FileSystemIoErrorMapper.GetCode(backend, exception),
                    phase,
                    logicalPath,
                    "Generation file could not be read.");
            }
        }

        private static bool IsCodecRecoverable(SaveGameErrorCode code)
        {
            return code == SaveGameErrorCode.InvalidEnvelope
                || code == SaveGameErrorCode.IntegrityFailed
                || code == SaveGameErrorCode.EnvelopePayloadMismatch
                || code == SaveGameErrorCode.LimitExceeded;
        }

        private static FileSystemRecordReadResult Recoverable(
            SaveGameErrorCode code,
            SaveGameOperationPhase phase,
            string path,
            string message)
        {
            return new FileSystemRecordReadResult(
                FileSystemRecordReadStatus.RecoverableFailure,
                null,
                null,
                new SaveGameError(code, phase, path, message));
        }

        private static FileSystemRecordReadResult Terminal(
            SaveGameErrorCode code,
            SaveGameOperationPhase phase,
            string path,
            string message)
        {
            return new FileSystemRecordReadResult(
                FileSystemRecordReadStatus.TerminalFailure,
                null,
                null,
                new SaveGameError(code, phase, path, message));
        }

        private static SaveGameError Sanitize(SaveGameError error)
        {
            return error.IsNone
                ? error
                : new SaveGameError(error.Code, error.Phase, error.Path, error.Message);
        }
    }
}
