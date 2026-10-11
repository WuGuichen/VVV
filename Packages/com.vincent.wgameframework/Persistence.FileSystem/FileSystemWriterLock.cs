using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace MxFramework.Persistence.FileSystem
{
    internal enum FileSystemExclusiveLockAcquireStatus
    {
        Acquired = 0,
        Cancelled = 1,
        Failed = 2
    }

    internal readonly struct FileSystemExclusiveLockAcquireResult
    {
        public FileSystemExclusiveLockAcquireResult(
            FileSystemExclusiveLockAcquireStatus status,
            Stream stream,
            SaveGameError error)
        {
            Status = status;
            Stream = stream;
            Error = error;
        }

        public FileSystemExclusiveLockAcquireStatus Status { get; }
        public Stream Stream { get; }
        public SaveGameError Error { get; }
    }

    internal static class FileSystemWriterLockValidator
    {
        private const string WriterLockName = ".writer.lock";

        public static bool TryValidateBeforeOpen(
            string rootPath,
            string lockPath,
            IFileSystemBackend backend,
            out SaveGameError error)
        {
            string normalizedLock;
            string parent;
            if (!TryNormalizeLockPath(lockPath, out normalizedLock, out parent, out error))
            {
                return false;
            }

            if (!FileSystemSaveGameLayout.TryValidateContainedPath(rootPath, parent, backend, out error))
            {
                return false;
            }

            if (!IsContained(rootPath, normalizedLock))
            {
                error = InvalidLock("Writer lock escapes the configured root.");
                return false;
            }

            try
            {
                FileSystemEntryStatus status = backend.GetStatus(normalizedLock);
                if (status.Kind != FileSystemEntryKind.Missing && status.Kind != FileSystemEntryKind.File)
                {
                    error = InvalidLock("Writer lock path is not a regular file or missing path.");
                    return false;
                }
            }
            catch (Exception)
            {
                error = IoLock("Writer lock path could not be inspected.");
                return false;
            }

            error = SaveGameError.None;
            return true;
        }

        public static bool TryValidateAfterOpen(
            string rootPath,
            string lockPath,
            IFileSystemBackend backend,
            out SaveGameError error)
        {
            if (!TryValidateBeforeOpen(rootPath, lockPath, backend, out error))
            {
                return false;
            }

            try
            {
                if (backend.GetStatus(Path.GetFullPath(lockPath)).Kind != FileSystemEntryKind.File)
                {
                    error = InvalidLock("Opened writer lock is not a regular file.");
                    return false;
                }
            }
            catch (Exception)
            {
                error = IoLock("Opened writer lock could not be inspected.");
                return false;
            }

            error = SaveGameError.None;
            return true;
        }

        private static bool TryNormalizeLockPath(
            string lockPath,
            out string normalizedLock,
            out string parent,
            out SaveGameError error)
        {
            try
            {
                normalizedLock = Path.GetFullPath(lockPath);
                parent = Path.GetDirectoryName(normalizedLock);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                normalizedLock = string.Empty;
                parent = string.Empty;
                error = InvalidLock("Writer lock path is invalid.");
                return false;
            }

            if (string.IsNullOrEmpty(parent)
                || !string.Equals(Path.GetFileName(normalizedLock), WriterLockName, StringComparison.Ordinal))
            {
                error = InvalidLock("Writer lock path must use the reserved lock filename.");
                return false;
            }

            error = SaveGameError.None;
            return true;
        }

        private static bool IsContained(string rootPath, string candidatePath)
        {
            string root = Path.GetFullPath(rootPath);
            string candidate = Path.GetFullPath(candidatePath);
            StringComparison comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            string boundedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return candidate.StartsWith(boundedRoot, comparison);
        }

        private static SaveGameError InvalidLock(string message) =>
            new SaveGameError(
                SaveGameErrorCode.InvalidLocation,
                SaveGameOperationPhase.Publication,
                "lock",
                message);

        private static SaveGameError IoLock(string message) =>
            new SaveGameError(
                SaveGameErrorCode.IoFailure,
                SaveGameOperationPhase.Publication,
                "lock",
                message);
    }

    internal static class FileSystemExclusiveLockAcquirer
    {
        public static FileSystemExclusiveLockAcquireResult WaitAcquire(
            string rootPath,
            string lockPath,
            IFileSystemBackend backend,
            IFileSystemDiagnosticSink diagnostics,
            FileSystemPublicationGate publicationGate,
            CancellationToken cancellationToken)
        {
            if (backend == null || publicationGate == null)
            {
                return Failed(new SaveGameError(
                    SaveGameErrorCode.InvalidLocation,
                    SaveGameOperationPhase.Validation,
                    "lock",
                    "Writer lock dependencies are unavailable."));
            }

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    publicationGate.RequestCancel();
                }

                if (publicationGate.IsCancellationRequested)
                {
                    return new FileSystemExclusiveLockAcquireResult(
                        FileSystemExclusiveLockAcquireStatus.Cancelled,
                        null,
                        SaveGameError.None);
                }

                SaveGameError validationError;
                if (!FileSystemWriterLockValidator.TryValidateBeforeOpen(
                        rootPath,
                        lockPath,
                        backend,
                        out validationError))
                {
                    return Failed(validationError);
                }

                FileSystemExclusiveLockAttempt attempt;
                try
                {
                    attempt = backend.TryOpenExclusiveLock(lockPath);
                }
                catch (Exception exception)
                {
                    FileSystemDiagnosticReporter.ReportBestEffort(
                        diagnostics,
                        "lock-open",
                        lockPath,
                        exception);
                    return Failed(new SaveGameError(
                        FileSystemIoErrorMapper.GetCode(backend, exception),
                        SaveGameOperationPhase.Publication,
                        "lock",
                        "Writer lock could not be opened."));
                }

                if (attempt.Status == FileSystemExclusiveLockAttemptStatus.Contended)
                {
                    Thread.Sleep(1);
                    continue;
                }

                if (attempt.Status != FileSystemExclusiveLockAttemptStatus.Acquired || attempt.Stream == null)
                {
                    return Failed(new SaveGameError(
                        SaveGameErrorCode.IoFailure,
                        SaveGameOperationPhase.Publication,
                        "lock",
                        "Writer lock backend returned an invalid result."));
                }

                if (!FileSystemWriterLockValidator.TryValidateAfterOpen(
                        rootPath,
                        lockPath,
                        backend,
                        out validationError))
                {
                    attempt.Stream.Dispose();
                    return Failed(validationError);
                }

                return new FileSystemExclusiveLockAcquireResult(
                    FileSystemExclusiveLockAcquireStatus.Acquired,
                    attempt.Stream,
                    SaveGameError.None);
            }
        }

        private static FileSystemExclusiveLockAcquireResult Failed(SaveGameError error) =>
            new FileSystemExclusiveLockAcquireResult(
                FileSystemExclusiveLockAcquireStatus.Failed,
                null,
                error);
    }
}
