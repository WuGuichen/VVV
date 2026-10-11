using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MxFramework.Persistence.FileSystem
{
    internal enum FileSystemEntryKind
    {
        Missing = 0,
        File = 1,
        Directory = 2,
        ReparsePoint = 3,
        Other = 4
    }

    internal readonly struct FileSystemEntryStatus
    {
        public FileSystemEntryStatus(FileSystemEntryKind kind, long length)
        {
            Kind = kind;
            Length = length;
        }

        public FileSystemEntryKind Kind { get; }
        public long Length { get; }
    }

    internal readonly struct FileSystemDirectoryEntry
    {
        public FileSystemDirectoryEntry(string name, FileSystemEntryKind kind, long length)
        {
            Name = name ?? string.Empty;
            Kind = kind;
            Length = length;
        }

        public string Name { get; }
        public FileSystemEntryKind Kind { get; }
        public long Length { get; }
    }

    internal enum FileSystemExclusiveLockAttemptStatus
    {
        Acquired = 0,
        Contended = 1
    }

    internal enum FileSystemLockErrorPlatform
    {
        Unknown = 0,
        Windows = 1,
        LinuxOrAndroid = 2,
        AppleOrBsd = 3
    }

    internal readonly struct FileSystemExclusiveLockAttempt
    {
        public FileSystemExclusiveLockAttempt(
            FileSystemExclusiveLockAttemptStatus status,
            Stream stream)
        {
            Status = status;
            Stream = stream;
        }

        public FileSystemExclusiveLockAttemptStatus Status { get; }
        public Stream Stream { get; }
    }

    internal interface IFileSystemBackend
    {
        FileSystemEntryStatus GetStatus(string path);
        FileSystemDirectoryEntry[] GetEntries(string directoryPath);
        void CreateDirectory(string path);
        Stream OpenCreateNew(string path);
        Stream OpenRead(string path);
        FileSystemExclusiveLockAttempt TryOpenExclusiveLock(string path);
        void FlushDurable(Stream stream);
        void MoveNoOverwrite(string sourcePath, string destinationPath);
        void DeleteFile(string path);
        void DeleteDirectory(string path, bool recursive);
    }

    internal enum FileSystemIoFailureKind
    {
        Unknown = 0,
        QuotaExceeded = 1
    }

    internal interface IFileSystemIoFailureClassifier
    {
        FileSystemIoFailureKind Classify(IOException exception);
    }

    internal static class FileSystemIoErrorMapper
    {
        public static SaveGameErrorCode GetCode(IFileSystemBackend backend, Exception exception)
        {
            if (exception is UnauthorizedAccessException) return SaveGameErrorCode.PermissionDenied;
            var ioException = exception as IOException;
            var classifier = backend as IFileSystemIoFailureClassifier;
            return ioException != null
                && classifier != null
                && classifier.Classify(ioException) == FileSystemIoFailureKind.QuotaExceeded
                    ? SaveGameErrorCode.QuotaExceeded
                    : SaveGameErrorCode.IoFailure;
        }
    }

    internal interface IFileSystemDiagnosticSink
    {
        void Report(string boundary, string physicalPath, Exception exception);
    }

    internal sealed class NullFileSystemDiagnosticSink : IFileSystemDiagnosticSink
    {
        public static readonly NullFileSystemDiagnosticSink Instance = new NullFileSystemDiagnosticSink();
        private NullFileSystemDiagnosticSink() { }
        public void Report(string boundary, string physicalPath, Exception exception) { }
    }

    internal static class FileSystemDiagnosticReporter
    {
        public static void ReportBestEffort(
            IFileSystemDiagnosticSink diagnostics,
            string boundary,
            string physicalPath,
            Exception exception)
        {
            if (diagnostics == null) return;
            try
            {
                diagnostics.Report(boundary, physicalPath, exception);
            }
            catch (Exception)
            {
                // Provider-local diagnostics cannot alter the structured public outcome.
            }
        }
    }

    internal sealed class BclFileSystemBackend : IFileSystemBackend, IFileSystemIoFailureClassifier
    {
        public static readonly BclFileSystemBackend Instance = new BclFileSystemBackend();
        private BclFileSystemBackend() { }

        public FileSystemEntryStatus GetStatus(string path)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return new FileSystemEntryStatus(FileSystemEntryKind.ReparsePoint, 0);
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    return new FileSystemEntryStatus(FileSystemEntryKind.Directory, 0);
                }

                return new FileSystemEntryStatus(FileSystemEntryKind.File, new FileInfo(path).Length);
            }
            catch (FileNotFoundException)
            {
                return new FileSystemEntryStatus(FileSystemEntryKind.Missing, 0);
            }
            catch (DirectoryNotFoundException)
            {
                return new FileSystemEntryStatus(FileSystemEntryKind.Missing, 0);
            }
        }

        public FileSystemDirectoryEntry[] GetEntries(string directoryPath)
        {
            string[] paths = Directory.GetFileSystemEntries(directoryPath);
            var entries = new FileSystemDirectoryEntry[paths.Length];
            for (int index = 0; index < paths.Length; index++)
            {
                FileSystemEntryStatus status = GetStatus(paths[index]);
                entries[index] = new FileSystemDirectoryEntry(
                    Path.GetFileName(paths[index]),
                    status.Kind,
                    status.Length);
            }

            return entries;
        }

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public Stream OpenCreateNew(string path)
        {
            return new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                4096,
                FileOptions.SequentialScan);
        }

        public Stream OpenRead(string path)
        {
            return new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.SequentialScan);
        }

        public FileSystemExclusiveLockAttempt TryOpenExclusiveLock(string path)
        {
            try
            {
                return new FileSystemExclusiveLockAttempt(
                    FileSystemExclusiveLockAttemptStatus.Acquired,
                    new FileStream(
                        path,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        1,
                        FileOptions.None));
            }
            catch (IOException exception) when (IsSharingViolation(exception, DetectLockErrorPlatform()))
            {
                return new FileSystemExclusiveLockAttempt(
                    FileSystemExclusiveLockAttemptStatus.Contended,
                    null);
            }
        }

        internal static bool IsSharingViolation(
            IOException exception,
            FileSystemLockErrorPlatform platform)
        {
            if (exception == null)
            {
                return false;
            }

            int nativeCode = exception.HResult & 0xffff;
            switch (platform)
            {
                case FileSystemLockErrorPlatform.Windows:
                    return nativeCode == 32 || nativeCode == 33;
                case FileSystemLockErrorPlatform.LinuxOrAndroid:
                    return nativeCode == 11;
                case FileSystemLockErrorPlatform.AppleOrBsd:
                    return nativeCode == 35;
                default:
                    return false;
            }
        }

        internal static FileSystemLockErrorPlatform DetectLockErrorPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return FileSystemLockErrorPlatform.Windows;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                || RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID")))
            {
                return FileSystemLockErrorPlatform.LinuxOrAndroid;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                || RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS"))
                || RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")))
            {
                return FileSystemLockErrorPlatform.AppleOrBsd;
            }

            return FileSystemLockErrorPlatform.Unknown;
        }

        public FileSystemIoFailureKind Classify(IOException exception)
        {
            return ClassifyIoFailure(exception, DetectLockErrorPlatform());
        }

        internal static FileSystemIoFailureKind ClassifyIoFailure(
            IOException exception,
            FileSystemLockErrorPlatform platform)
        {
            if (exception == null) return FileSystemIoFailureKind.Unknown;
            int nativeCode = exception.HResult & 0xffff;
            switch (platform)
            {
                case FileSystemLockErrorPlatform.Windows:
                    return nativeCode == 39 || nativeCode == 112
                        ? FileSystemIoFailureKind.QuotaExceeded
                        : FileSystemIoFailureKind.Unknown;
                case FileSystemLockErrorPlatform.LinuxOrAndroid:
                    return nativeCode == 28 || nativeCode == 122
                        ? FileSystemIoFailureKind.QuotaExceeded
                        : FileSystemIoFailureKind.Unknown;
                case FileSystemLockErrorPlatform.AppleOrBsd:
                    return nativeCode == 28 || nativeCode == 69
                        ? FileSystemIoFailureKind.QuotaExceeded
                        : FileSystemIoFailureKind.Unknown;
                default:
                    return FileSystemIoFailureKind.Unknown;
            }
        }

        public void FlushDurable(Stream stream)
        {
            var fileStream = stream as FileStream;
            if (fileStream == null)
            {
                throw new NotSupportedException("The backend stream does not expose a durable file flush.");
            }

            fileStream.Flush(true);
        }

        public void MoveNoOverwrite(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath);
        }

        public void DeleteFile(string path) => File.Delete(path);
        public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);
    }
}
