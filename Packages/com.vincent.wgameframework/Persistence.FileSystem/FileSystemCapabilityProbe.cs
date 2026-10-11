using System;
using System.IO;

namespace MxFramework.Persistence.FileSystem
{
    internal static class FileSystemCapabilityProbe
    {
        private static readonly byte[] FirstPayload = { 0x4d, 0x58, 0x31 };
        private static readonly byte[] SecondPayload = { 0x4d, 0x58, 0x32 };
        private static readonly byte[] ChallengerPayload = { 0x4d, 0x58, 0x33 };

        public static bool TryProbe(
            string rootPath,
            IFileSystemBackend backend,
            IFileSystemDiagnosticSink diagnostics,
            out FileSystemCapabilityEvidence evidence,
            out SaveGameError error)
        {
            bool createNew = false;
            bool sameDirectoryMove = false;
            bool nonOverwritingMove = false;
            bool durableFlush = false;
            bool exclusiveWriterLock = false;
            string probePath = string.Empty;

            try
            {
                backend.CreateDirectory(rootPath);
                if (!FileSystemSaveGameLayout.TryValidateContainedPath(rootPath, rootPath, backend, out error))
                {
                    evidence = default;
                    return false;
                }

                probePath = Path.Combine(rootPath, ".mx-capability-" + Guid.NewGuid().ToString("N"));
                if (!FileSystemSaveGameLayout.TryValidateContainedPath(rootPath, probePath, backend, out error))
                {
                    evidence = default;
                    return false;
                }

                backend.CreateDirectory(probePath);
                if (!FileSystemSaveGameLayout.TryValidateContainedPath(rootPath, probePath, backend, out error))
                {
                    evidence = default;
                    return false;
                }

                string staging = Path.Combine(probePath, "staging");
                string moved = Path.Combine(probePath, "moved");
                string existing = Path.Combine(probePath, "existing");
                string challenger = Path.Combine(probePath, "challenger");
                string writerLock = Path.Combine(probePath, ".writer.lock");
                if (!ValidateProbePath(rootPath, staging, backend, out error)
                    || !ValidateProbePath(rootPath, moved, backend, out error)
                    || !ValidateProbePath(rootPath, existing, backend, out error)
                    || !ValidateProbePath(rootPath, challenger, backend, out error)
                    || !ValidateProbePath(rootPath, writerLock, backend, out error))
                {
                    evidence = default;
                    return false;
                }

                WriteCreateNew(staging, FirstPayload, backend, out durableFlush);
                bool rejectedExistingCreate = false;
                try
                {
                    using (backend.OpenCreateNew(staging)) { }
                }
                catch (IOException)
                {
                    rejectedExistingCreate = true;
                }

                createNew = rejectedExistingCreate && ReadExact(staging, FirstPayload, backend);
                if (!createNew)
                {
                    evidence = new FileSystemCapabilityEvidence(false, false, false, durableFlush, false);
                    error = Unsupported("staging", "Create-new did not reject an existing path while preserving complete bytes.");
                    return false;
                }

                backend.MoveNoOverwrite(staging, moved);
                sameDirectoryMove = ReadExact(moved, FirstPayload, backend)
                    && backend.GetStatus(staging).Kind == FileSystemEntryKind.Missing;
                if (!sameDirectoryMove)
                {
                    evidence = new FileSystemCapabilityEvidence(createNew, false, false, durableFlush, false);
                    error = Unsupported("publish", "Same-directory move did not preserve complete bytes.");
                    return false;
                }

                bool ignoredFlush;
                WriteCreateNew(existing, SecondPayload, backend, out ignoredFlush);
                WriteCreateNew(challenger, ChallengerPayload, backend, out ignoredFlush);
                try
                {
                    backend.MoveNoOverwrite(challenger, existing);
                }
                catch (IOException)
                {
                    // A two-argument move must reject an existing destination. Preservation is verified below.
                }

                nonOverwritingMove = ReadExact(existing, SecondPayload, backend)
                    && ReadExact(challenger, ChallengerPayload, backend);
                exclusiveWriterLock = VerifyExclusiveWriterLock(
                    writerLock,
                    backend,
                    out SaveGameError lockEvidenceError);
                evidence = new FileSystemCapabilityEvidence(
                    createNew,
                    sameDirectoryMove,
                    nonOverwritingMove,
                    durableFlush,
                    exclusiveWriterLock);
                if (!evidence.HasRequiredPublicationPrimitives)
                {
                    error = exclusiveWriterLock
                        ? Unsupported("publish", "Filesystem publication primitives are not supported.")
                        : lockEvidenceError;
                    return false;
                }

                backend.DeleteDirectory(probePath, true);
                probePath = string.Empty;
                error = SaveGameError.None;
                return true;
            }
            catch (Exception exception)
            {
                FileSystemDiagnosticReporter.ReportBestEffort(diagnostics, "probe", probePath, exception);
                evidence = new FileSystemCapabilityEvidence(
                    createNew,
                    sameDirectoryMove,
                    nonOverwritingMove,
                    durableFlush,
                    exclusiveWriterLock);
                error = MapProbeException(backend, exception);
                return false;
            }
            finally
            {
                if (!string.IsNullOrEmpty(probePath))
                {
                    try
                    {
                        FileSystemEntryStatus status = backend.GetStatus(probePath);
                        if (status.Kind != FileSystemEntryKind.Missing)
                        {
                            backend.DeleteDirectory(probePath, true);
                        }
                    }
                    catch (Exception cleanupException)
                    {
                        FileSystemDiagnosticReporter.ReportBestEffort(
                            diagnostics,
                            "probe-cleanup",
                            probePath,
                            cleanupException);
                    }
                }
            }
        }

        private static bool ValidateProbePath(
            string rootPath,
            string path,
            IFileSystemBackend backend,
            out SaveGameError error)
        {
            return FileSystemSaveGameLayout.TryValidateContainedPath(rootPath, path, backend, out error);
        }

        private static void WriteCreateNew(
            string path,
            byte[] payload,
            IFileSystemBackend backend,
            out bool durableFlush)
        {
            using (Stream stream = backend.OpenCreateNew(path))
            {
                stream.Write(payload, 0, payload.Length);
                backend.FlushDurable(stream);
                durableFlush = true;
            }
        }

        private static bool ReadExact(string path, byte[] expected, IFileSystemBackend backend)
        {
            FileSystemEntryStatus status = backend.GetStatus(path);
            if (status.Kind != FileSystemEntryKind.File || status.Length != expected.Length)
            {
                return false;
            }

            var bytes = new byte[expected.Length];
            using (Stream stream = backend.OpenRead(path))
            {
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0)
                    {
                        return false;
                    }

                    offset += read;
                }

                if (stream.ReadByte() != -1)
                {
                    return false;
                }
            }

            for (int index = 0; index < bytes.Length; index++)
            {
                if (bytes[index] != expected[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool VerifyExclusiveWriterLock(
            string lockPath,
            IFileSystemBackend backend,
            out SaveGameError error)
        {
            Stream first = null;
            Stream unexpectedSecond = null;
            Stream reacquired = null;
            try
            {
                if (!TryOpenLockEvidenceAttempt(
                        lockPath,
                        backend,
                        out FileSystemExclusiveLockAttempt firstAttempt,
                        out error))
                {
                    return false;
                }

                first = firstAttempt.Stream;
                if (firstAttempt.Status != FileSystemExclusiveLockAttemptStatus.Acquired
                    || first == null)
                {
                    error = Unsupported("lock", "Exclusive writer lock acquisition is not supported.");
                    return false;
                }

                if (!TryOpenLockEvidenceAttempt(
                        lockPath,
                        backend,
                        out FileSystemExclusiveLockAttempt secondAttempt,
                        out error))
                {
                    return false;
                }

                unexpectedSecond = secondAttempt.Stream;
                if (secondAttempt.Status != FileSystemExclusiveLockAttemptStatus.Contended
                    || unexpectedSecond != null)
                {
                    error = Unsupported("lock", "Exclusive writer lock contention is not supported.");
                    return false;
                }

                first.Dispose();
                first = null;
                if (!TryOpenLockEvidenceAttempt(
                        lockPath,
                        backend,
                        out FileSystemExclusiveLockAttempt reacquireAttempt,
                        out error))
                {
                    return false;
                }

                reacquired = reacquireAttempt.Stream;
                if (reacquireAttempt.Status != FileSystemExclusiveLockAttemptStatus.Acquired
                    || reacquired == null)
                {
                    error = Unsupported("lock", "Exclusive writer lock reacquisition is not supported.");
                    return false;
                }

                error = SaveGameError.None;
                return true;
            }
            finally
            {
                if (unexpectedSecond != null) unexpectedSecond.Dispose();
                if (reacquired != null) reacquired.Dispose();
                if (first != null) first.Dispose();
            }
        }

        private static bool TryOpenLockEvidenceAttempt(
            string lockPath,
            IFileSystemBackend backend,
            out FileSystemExclusiveLockAttempt attempt,
            out SaveGameError error)
        {
            try
            {
                attempt = backend.TryOpenExclusiveLock(lockPath);
                error = SaveGameError.None;
                return true;
            }
            catch (IOException exception)
            {
                attempt = default;
                SaveGameErrorCode code = FileSystemIoErrorMapper.GetCode(backend, exception);
                error = code == SaveGameErrorCode.QuotaExceeded
                    ? new SaveGameError(
                        code,
                        SaveGameOperationPhase.Validation,
                        "lock",
                        "Filesystem quota prevented writer lock capability probing.")
                    : Unsupported("lock", "Exclusive writer lock semantics could not be proven.");
                return false;
            }
            catch (NotSupportedException)
            {
                attempt = default;
                error = Unsupported("lock", "Exclusive writer lock semantics are not supported.");
                return false;
            }
        }

        private static SaveGameError MapProbeException(IFileSystemBackend backend, Exception exception)
        {
            if (exception is UnauthorizedAccessException)
            {
                return new SaveGameError(
                    SaveGameErrorCode.PermissionDenied,
                    SaveGameOperationPhase.Validation,
                    "location",
                    "Filesystem capability probe was denied.");
            }

            if (exception is NotSupportedException)
            {
                return Unsupported("flush", "Durable file flush is not supported.");
            }

            return new SaveGameError(
                FileSystemIoErrorMapper.GetCode(backend, exception),
                SaveGameOperationPhase.Publication,
                "publish",
                "Filesystem capability probe failed.");
        }

        private static SaveGameError Unsupported(string path, string message)
        {
            return new SaveGameError(
                SaveGameErrorCode.AtomicCommitUnsupported,
                SaveGameOperationPhase.Publication,
                path,
                message);
        }
    }
}
