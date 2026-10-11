using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using MxFramework.Persistence;
using MxFramework.Persistence.FileSystem;
using MxFramework.Platform;
using NUnit.Framework;

namespace MxFramework.Tests.PersistenceFileSystem
{
    public sealed class FileSystemFactoryAndLayoutTests
    {
        private string _temporaryRoot;
        private List<string> _junctionPaths;

        [SetUp]
        public void SetUp()
        {
            _temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "mx-persistence-filesystem-" + Guid.NewGuid().ToString("N"));
            _junctionPaths = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = _junctionPaths.Count - 1; index >= 0; index--)
            {
                RemoveJunction(_junctionPaths[index]);
            }

            if (Directory.Exists(_temporaryRoot))
            {
                Directory.Delete(_temporaryRoot, true);
            }
        }

        [Test]
        public void StrictHexLayout_RoundTripsAndKeepsCaseDistinct()
        {
            string upper = FileSystemSaveGameLayout.EncodeIdentifier("A.slot");
            string lower = FileSystemSaveGameLayout.EncodeIdentifier("a.slot");

            Assert.That(upper, Is.EqualTo("412e736c6f74"));
            Assert.That(lower, Is.EqualTo("612e736c6f74"));
            Assert.That(upper, Is.Not.EqualTo(lower));
            Assert.That(FileSystemSaveGameLayout.TryDecodeSlotSegment("s-" + upper, out SaveGameSlotId decoded), Is.True);
            Assert.That(decoded, Is.EqualTo(new SaveGameSlotId("A.slot")));
            Assert.That(FileSystemSaveGameLayout.TryDecodeSlotSegment("s-412E736c6f74", out _), Is.False);
            Assert.That(FileSystemSaveGameLayout.TryDecodeSlotSegment("s-c0af", out _), Is.False);
        }

        [Test]
        public void Layout_UsesSeparatorBoundedContainmentAndRejectsReparseComponents()
        {
            Directory.CreateDirectory(_temporaryRoot);
            var backend = new RecordingBackend(BclFileSystemBackend.Instance);
            string root;
            Assert.That(FileSystemSaveGameLayout.TryNormalizeRoot(_temporaryRoot, backend, out root, out SaveGameError rootError), Is.True, rootError.ToString());
            Assert.That(
                FileSystemSaveGameLayout.TryValidateContainedPath(
                    root,
                    root + "-escape",
                    backend,
                    out SaveGameError escapeError),
                Is.False);
            Assert.That(escapeError.Code, Is.EqualTo(SaveGameErrorCode.InvalidLocation));

            string profilePath = Path.Combine(root, "profiles", "p-70");
            backend.ReparsePath = Path.Combine(root, "profiles");
            Assert.That(
                FileSystemSaveGameLayout.TryValidateContainedPath(root, profilePath, backend, out SaveGameError reparseError),
                Is.False);
            Assert.That(reparseError.Code, Is.EqualTo(SaveGameErrorCode.InvalidLocation));
            Assert.That(reparseError.Exception, Is.Null);
        }

        [Test]
        public void Factory_InvalidKindsAndReadonlyLocationPerformZeroWrites()
        {
            var backend = new RecordingBackend(BclFileSystemBackend.Instance);
            PlatformLocation uri = PlatformLocation.TryCreate(
                PlatformLocationKind.Uri,
                "https://example.invalid/saves",
                true,
                true,
                true).Location;
            PlatformLocation providerOwned = PlatformLocation.TryCreate(
                PlatformLocationKind.ProviderOwned,
                "opaque-store",
                true,
                true,
                true).Location;
            PlatformLocation readOnly = PlatformLocation.TryCreate(
                PlatformLocationKind.FileSystemPath,
                _temporaryRoot,
                true,
                false,
                true).Location;

            AssertInvalidWithoutWrites(uri, backend);
            AssertInvalidWithoutWrites(providerOwned, backend);
            AssertInvalidWithoutWrites(readOnly, backend);
        }

        [Test]
        public void Factory_RealBclProbeReturnsRootScopedEvidenceAndCleansProbeDirectory()
        {
            PlatformLocation location = WritableLocation(_temporaryRoot);

            FileSystemSaveGameStoreFactoryCreateResult result = FileSystemSaveGameStoreFactory.TryCreate(location);

            Assert.That(result.Success, Is.True, result.Error.ToString());
            Assert.That(result.Factory, Is.Not.Null);
            Assert.That(result.CapabilityEvidence.CreateNewFile, Is.True);
            Assert.That(result.CapabilityEvidence.SameDirectoryMove, Is.True);
            Assert.That(result.CapabilityEvidence.NonOverwritingMove, Is.True);
            Assert.That(result.CapabilityEvidence.DurableFileFlush, Is.True);
            Assert.That(result.CapabilityEvidence.ExclusiveWriterLock, Is.True);
            Assert.That(Directory.GetDirectories(_temporaryRoot, ".mx-capability-*"), Is.Empty);
            RecordLocalVolumeEvidence(_temporaryRoot);
        }

        [Test]
        public void Factory_ProbeFailureSanitizesAbsolutePathFromPublicError()
        {
            var backend = new RecordingBackend(BclFileSystemBackend.Instance)
            {
                CreateDirectoryException = new IOException("failure at " + _temporaryRoot)
            };
            var diagnostics = new RecordingDiagnostics();

            FileSystemSaveGameStoreFactoryCreateResult result = FileSystemSaveGameStoreFactory.TryCreate(
                WritableLocation(_temporaryRoot),
                backend,
                diagnostics);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveGameErrorCode.IoFailure));
            Assert.That(result.Error.Exception, Is.Null);
            Assert.That(result.Error.Path, Is.EqualTo("publish"));
            Assert.That(result.Error.Message, Does.Not.Contain(_temporaryRoot));
            Assert.That(result.Error.ToString(), Does.Not.Contain(_temporaryRoot));
            Assert.That(diagnostics.Exception, Is.SameAs(backend.CreateDirectoryException));
            Assert.That(diagnostics.PhysicalPath, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Factory_RejectsBackendThatOverwritesMoveDestination()
        {
            var backend = new RecordingBackend(BclFileSystemBackend.Instance) { OverwriteOnMove = true };

            FileSystemSaveGameStoreFactoryCreateResult result = FileSystemSaveGameStoreFactory.TryCreate(
                WritableLocation(_temporaryRoot),
                backend,
                new RecordingDiagnostics());

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveGameErrorCode.AtomicCommitUnsupported));
            Assert.That(result.Error.Exception, Is.Null);
            Assert.That(result.CapabilityEvidence.NonOverwritingMove, Is.False);
        }

        [Test]
        public void Factory_RejectsMoveThatCopiesWithoutDeletingSource()
        {
            var backend = new RecordingBackend(BclFileSystemBackend.Instance) { CopyWithoutDeleteOnMove = true };

            FileSystemSaveGameStoreFactoryCreateResult result = FileSystemSaveGameStoreFactory.TryCreate(
                WritableLocation(_temporaryRoot),
                backend,
                new RecordingDiagnostics());

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveGameErrorCode.AtomicCommitUnsupported));
            Assert.That(result.CapabilityEvidence.SameDirectoryMove, Is.False);
        }

        [Test]
        public void Factory_RejectsCreateNewThatOverwritesExistingBytes()
        {
            var backend = new RecordingBackend(BclFileSystemBackend.Instance) { OverwriteOnCreate = true };

            FileSystemSaveGameStoreFactoryCreateResult result = FileSystemSaveGameStoreFactory.TryCreate(
                WritableLocation(_temporaryRoot),
                backend,
                new RecordingDiagnostics());

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveGameErrorCode.AtomicCommitUnsupported));
            Assert.That(result.CapabilityEvidence.CreateNewFile, Is.False);
        }

        [Test]
        public void Factory_ThrowingDiagnosticSinkCannotChangeStructuredFailure()
        {
            var backend = new RecordingBackend(BclFileSystemBackend.Instance)
            {
                CreateDirectoryException = new IOException("physical " + _temporaryRoot)
            };
            FileSystemSaveGameStoreFactoryCreateResult result = default;

            Assert.DoesNotThrow(() =>
                result = FileSystemSaveGameStoreFactory.TryCreate(
                    WritableLocation(_temporaryRoot),
                    backend,
                    new ThrowingDiagnostics()));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveGameErrorCode.IoFailure));
            Assert.That(result.Error.Exception, Is.Null);
            Assert.That(result.Error.ToString(), Does.Not.Contain(_temporaryRoot));
        }

        [Test]
        public void Factory_ProbeCleanupFailureInvalidatesOtherwiseCompleteEvidence()
        {
            var cleanupFailure = new IOException("cleanup at " + _temporaryRoot);
            var backend = new RecordingBackend(BclFileSystemBackend.Instance)
            {
                DeleteDirectoryException = cleanupFailure
            };
            var diagnostics = new RecordingDiagnostics();

            FileSystemSaveGameStoreFactoryCreateResult result = FileSystemSaveGameStoreFactory.TryCreate(
                WritableLocation(_temporaryRoot),
                backend,
                diagnostics);

            Assert.That(result.Success, Is.False);
            Assert.That(result.CapabilityEvidence.CreateNewFile, Is.True);
            Assert.That(result.CapabilityEvidence.SameDirectoryMove, Is.True);
            Assert.That(result.CapabilityEvidence.NonOverwritingMove, Is.True);
            Assert.That(result.CapabilityEvidence.DurableFileFlush, Is.True);
            Assert.That(result.CapabilityEvidence.ExclusiveWriterLock, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(SaveGameErrorCode.IoFailure));
            Assert.That(result.Error.Exception, Is.Null);
            Assert.That(result.Error.Message, Does.Not.Contain(_temporaryRoot));
            Assert.That(diagnostics.Exception, Is.SameAs(cleanupFailure));
        }

        [Test]
        public void Factory_RejectsBackendThatCannotProveExclusiveWriterLockContention()
        {
            var backend = new RecordingBackend(BclFileSystemBackend.Instance)
            {
                AllowsConcurrentLocks = true
            };

            FileSystemSaveGameStoreFactoryCreateResult result = FileSystemSaveGameStoreFactory.TryCreate(
                WritableLocation(_temporaryRoot),
                backend,
                new RecordingDiagnostics());

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveGameErrorCode.AtomicCommitUnsupported));
            Assert.That(result.Error.Path, Is.EqualTo("lock"));
            Assert.That(result.CapabilityEvidence.ExclusiveWriterLock, Is.False);
        }

        [Test]
        public void Factory_MapsExclusiveWriterLockAccessFailureToPermissionDenied()
        {
            var accessFailure = new UnauthorizedAccessException("lock denied at " + _temporaryRoot);
            var backend = new RecordingBackend(BclFileSystemBackend.Instance)
            {
                LockOpenException = accessFailure
            };
            var diagnostics = new RecordingDiagnostics();

            FileSystemSaveGameStoreFactoryCreateResult result = FileSystemSaveGameStoreFactory.TryCreate(
                WritableLocation(_temporaryRoot),
                backend,
                diagnostics);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveGameErrorCode.PermissionDenied));
            Assert.That(result.Error.Exception, Is.Null);
            Assert.That(result.CapabilityEvidence.ExclusiveWriterLock, Is.False);
            Assert.That(diagnostics.Exception, Is.SameAs(accessFailure));
        }

        [Test]
        public void Factory_DisposesStreamReturnedWithInvalidContendedAttempt()
        {
            var invalidStream = new DisposeTrackingStream();
            var backend = new RecordingBackend(BclFileSystemBackend.Instance)
            {
                InvalidContendedStreamAttempt = 2,
                InvalidContendedStream = invalidStream
            };

            FileSystemSaveGameStoreFactoryCreateResult result = FileSystemSaveGameStoreFactory.TryCreate(
                WritableLocation(_temporaryRoot),
                backend,
                new RecordingDiagnostics());

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveGameErrorCode.AtomicCommitUnsupported));
            Assert.That(result.CapabilityEvidence.ExclusiveWriterLock, Is.False);
            Assert.That(invalidStream.IsDisposed, Is.True);
        }

        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        public void Factory_MapsLockEvidenceExceptionToAtomicCommitUnsupported(
            int attempt,
            bool notSupported)
        {
            var backend = new RecordingBackend(BclFileSystemBackend.Instance)
            {
                LockOpenException = notSupported
                    ? (Exception)new NotSupportedException("exclusive lock evidence unavailable")
                    : new IOException("unclassified lock evidence failure"),
                LockOpenExceptionAttempt = attempt
            };

            FileSystemSaveGameStoreFactoryCreateResult result = FileSystemSaveGameStoreFactory.TryCreate(
                WritableLocation(_temporaryRoot),
                backend,
                new RecordingDiagnostics());

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveGameErrorCode.AtomicCommitUnsupported));
            Assert.That(result.Error.Phase, Is.EqualTo(SaveGameOperationPhase.Publication));
            Assert.That(result.Error.Path, Is.EqualTo("lock"));
            Assert.That(result.Error.Exception, Is.Null);
            Assert.That(result.CapabilityEvidence.ExclusiveWriterLock, Is.False);
        }

        [Test]
        public void WindowsRealBcl_ReparseRootAndIntermediateAreRejectedBeforeWrites()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Ignore("Windows junction smoke is only applicable on Windows.");
            }

            Directory.CreateDirectory(_temporaryRoot);
            string rootTarget = Path.Combine(_temporaryRoot, "root-target");
            string rootJunction = Path.Combine(_temporaryRoot, "root-junction");
            CreateJunctionOrIgnore(rootJunction, rootTarget);
            var rootBackend = new RecordingBackend(BclFileSystemBackend.Instance);

            FileSystemSaveGameStoreFactoryCreateResult rootResult = FileSystemSaveGameStoreFactory.TryCreate(
                WritableLocation(rootJunction),
                rootBackend,
                new RecordingDiagnostics());

            Assert.That(rootResult.Success, Is.False);
            Assert.That(rootResult.Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidLocation));
            Assert.That(rootBackend.WriteCount, Is.Zero);

            string ordinaryRoot = Path.Combine(_temporaryRoot, "ordinary-root");
            string profilesTarget = Path.Combine(_temporaryRoot, "profiles-target");
            Directory.CreateDirectory(ordinaryRoot);
            string profilesJunction = Path.Combine(ordinaryRoot, "profiles");
            CreateJunctionOrIgnore(profilesJunction, profilesTarget);
            var componentBackend = new RecordingBackend(BclFileSystemBackend.Instance);

            Assert.That(
                FileSystemSaveGameLayout.TryGetProfileDirectory(
                    ordinaryRoot,
                    new SaveGameProfileId("profile"),
                    componentBackend,
                    out _,
                    out SaveGameError componentError),
                Is.False);
            Assert.That(componentError.Code, Is.EqualTo(SaveGameErrorCode.InvalidLocation));
            Assert.That(componentBackend.WriteCount, Is.Zero);

            string lockRoot = Path.Combine(_temporaryRoot, "lock-root");
            string lockTarget = Path.Combine(_temporaryRoot, "lock-target");
            Directory.CreateDirectory(lockRoot);
            string lockJunction = Path.Combine(lockRoot, ".writer.lock");
            CreateJunctionOrIgnore(lockJunction, lockTarget);
            var lockBackend = new RecordingBackend(BclFileSystemBackend.Instance);

            Assert.That(
                FileSystemWriterLockValidator.TryValidateBeforeOpen(
                    lockRoot,
                    lockJunction,
                    lockBackend,
                    out SaveGameError lockError),
                Is.False);
            Assert.That(lockError.Code, Is.EqualTo(SaveGameErrorCode.InvalidLocation));
            Assert.That(lockBackend.LockOpenCount, Is.Zero);
        }

        [Test]
        public void WriterLock_PreAndPostValidationRejectSyntheticReparsePoints()
        {
            Directory.CreateDirectory(_temporaryRoot);
            string lockPath = Path.Combine(_temporaryRoot, ".writer.lock");
            var preBackend = new RecordingBackend(BclFileSystemBackend.Instance)
            {
                ReparsePath = lockPath
            };

            Assert.That(
                FileSystemWriterLockValidator.TryValidateBeforeOpen(
                    _temporaryRoot,
                    lockPath,
                    preBackend,
                    out SaveGameError preError),
                Is.False);
            Assert.That(preError.Code, Is.EqualTo(SaveGameErrorCode.InvalidLocation));
            Assert.That(preBackend.LockOpenCount, Is.Zero);

            var postBackend = new RecordingBackend(BclFileSystemBackend.Instance)
            {
                ReparseLockAfterOpen = true
            };
            FileSystemExclusiveLockAcquireResult result = FileSystemExclusiveLockAcquirer.WaitAcquire(
                _temporaryRoot,
                lockPath,
                postBackend,
                null,
                new FileSystemPublicationGate(),
                CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(FileSystemExclusiveLockAcquireStatus.Failed));
            Assert.That(result.Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidLocation));
            Assert.That(result.Stream, Is.Null);
            Assert.That(postBackend.LockOpenCount, Is.EqualTo(1));
            using (Stream reopened = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.That(reopened, Is.Not.Null, "Rejected lock stream must be disposed.");
            }
        }

        [Test]
        public void WriterLock_ContentionRetriesUntilAcquiredAndCancellationStopsRetry()
        {
            Directory.CreateDirectory(_temporaryRoot);
            string lockPath = Path.Combine(_temporaryRoot, ".writer.lock");
            var retryBackend = new RecordingBackend(BclFileSystemBackend.Instance)
            {
                ContendedLockAttempts = 2
            };
            FileSystemExclusiveLockAcquireResult acquired = FileSystemExclusiveLockAcquirer.WaitAcquire(
                _temporaryRoot,
                lockPath,
                retryBackend,
                null,
                new FileSystemPublicationGate(),
                CancellationToken.None);

            Assert.That(acquired.Status, Is.EqualTo(FileSystemExclusiveLockAcquireStatus.Acquired));
            Assert.That(retryBackend.LockOpenCount, Is.EqualTo(3));
            acquired.Stream.Dispose();

            var contendedBackend = new RecordingBackend(BclFileSystemBackend.Instance)
            {
                ContendedLockAttempts = int.MaxValue
            };
            var gate = new FileSystemPublicationGate();
            using (var cancellation = new CancellationTokenSource())
            {
                FileSystemExclusiveLockAcquireResult cancelled = default;
                var waiter = new Thread(() => cancelled = FileSystemExclusiveLockAcquirer.WaitAcquire(
                    _temporaryRoot,
                    lockPath,
                    contendedBackend,
                    null,
                    gate,
                    cancellation.Token));
                waiter.Start();
                Assert.That(SpinWait.SpinUntil(() => contendedBackend.LockOpenCount > 0, 5000), Is.True);
                cancellation.Cancel();
                waiter.Join();

                Assert.That(cancelled.Status, Is.EqualTo(FileSystemExclusiveLockAcquireStatus.Cancelled));
                Assert.That(gate.IsCancellationRequested, Is.True);
            }
        }

        private static PlatformLocation WritableLocation(string path)
        {
            PlatformLocationResult result = PlatformLocation.TryCreate(
                PlatformLocationKind.FileSystemPath,
                path,
                true,
                true,
                true);
            Assert.That(result.Success, Is.True, result.Error.ToString());
            return result.Location;
        }

        private static void AssertInvalidWithoutWrites(PlatformLocation location, RecordingBackend backend)
        {
            FileSystemSaveGameStoreFactoryCreateResult result = FileSystemSaveGameStoreFactory.TryCreate(
                location,
                backend,
                new RecordingDiagnostics());
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidLocation));
            Assert.That(result.Error.Exception, Is.Null);
            Assert.That(backend.WriteCount, Is.Zero);
        }

        private void CreateJunctionOrIgnore(string junctionPath, string targetPath)
        {
            Directory.CreateDirectory(targetPath);
            var startInfo = new ProcessStartInfo(
                "cmd.exe",
                "/c mklink /J \"" + junctionPath + "\" \"" + targetPath + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Assert.Ignore("Junction creation is unavailable: " + process.StandardError.ReadToEnd());
                }
            }

            _junctionPaths.Add(junctionPath);
        }

        private static void RemoveJunction(string junctionPath)
        {
            var startInfo = new ProcessStartInfo(
                "cmd.exe",
                "/c rmdir \"" + junctionPath + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
            }
        }

        private static void RecordLocalVolumeEvidence(string path)
        {
            TestContext.WriteLine("OS: " + RuntimeInformation.OSDescription);
            TestContext.WriteLine("Backend: " + typeof(BclFileSystemBackend).FullName);
            DriveType driveType;
            string driveFormat;
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path)));
                driveType = drive.DriveType;
                driveFormat = drive.DriveFormat;
            }
            catch (Exception exception)
            {
                TestContext.WriteLine("DriveEvidenceUnavailable: " + exception.GetType().Name);
                Assert.Ignore("Real-volume evidence is unavailable: " + exception.GetType().Name);
                return;
            }

            TestContext.WriteLine("DriveType: " + driveType);
            TestContext.WriteLine("DriveFormat: " + driveFormat);
            if (driveType != DriveType.Fixed || string.IsNullOrWhiteSpace(driveFormat))
            {
                Assert.Ignore("Real-volume evidence requires a fixed drive with a reported filesystem format.");
            }
        }

        private sealed class RecordingDiagnostics : IFileSystemDiagnosticSink
        {
            public string PhysicalPath { get; private set; }
            public Exception Exception { get; private set; }
            public void Report(string boundary, string physicalPath, Exception exception)
            {
                PhysicalPath = physicalPath;
                Exception = exception;
            }
        }

        private sealed class ThrowingDiagnostics : IFileSystemDiagnosticSink
        {
            public void Report(string boundary, string physicalPath, Exception exception)
            {
                throw new InvalidOperationException("diagnostic sink failure at " + physicalPath);
            }
        }

        private sealed class RecordingBackend : IFileSystemBackend
        {
            private readonly IFileSystemBackend _inner;
            public RecordingBackend(IFileSystemBackend inner) { _inner = inner; }
            public int WriteCount { get; private set; }
            public string ReparsePath { get; set; }
            public Exception CreateDirectoryException { get; set; }
            public Exception DeleteDirectoryException { get; set; }
            public bool OverwriteOnMove { get; set; }
            public bool CopyWithoutDeleteOnMove { get; set; }
            public bool OverwriteOnCreate { get; set; }
            public bool ReparseLockAfterOpen { get; set; }
            public bool AllowsConcurrentLocks { get; set; }
            public Exception LockOpenException { get; set; }
            public int LockOpenExceptionAttempt { get; set; }
            public int InvalidContendedStreamAttempt { get; set; }
            public Stream InvalidContendedStream { get; set; }
            public int ContendedLockAttempts { get; set; }
            public int LockOpenCount { get; private set; }

            public FileSystemEntryStatus GetStatus(string path)
            {
                if (!string.IsNullOrEmpty(ReparsePath)
                    && string.Equals(Path.GetFullPath(path), Path.GetFullPath(ReparsePath), StringComparison.OrdinalIgnoreCase))
                {
                    return new FileSystemEntryStatus(FileSystemEntryKind.ReparsePoint, 0);
                }

                return _inner.GetStatus(path);
            }

            public FileSystemDirectoryEntry[] GetEntries(string directoryPath) =>
                _inner.GetEntries(directoryPath);

            public void CreateDirectory(string path)
            {
                WriteCount++;
                if (CreateDirectoryException != null) throw CreateDirectoryException;
                _inner.CreateDirectory(path);
            }

            public Stream OpenCreateNew(string path)
            {
                WriteCount++;
                if (OverwriteOnCreate && _inner.GetStatus(path).Kind == FileSystemEntryKind.File)
                {
                    _inner.DeleteFile(path);
                }

                return _inner.OpenCreateNew(path);
            }
            public Stream OpenRead(string path) => _inner.OpenRead(path);
            public FileSystemExclusiveLockAttempt TryOpenExclusiveLock(string path)
            {
                LockOpenCount++;
                WriteCount++;
                if (LockOpenException != null
                    && (LockOpenExceptionAttempt == 0 || LockOpenCount == LockOpenExceptionAttempt))
                {
                    throw LockOpenException;
                }
                if (LockOpenCount == InvalidContendedStreamAttempt)
                {
                    return new FileSystemExclusiveLockAttempt(
                        FileSystemExclusiveLockAttemptStatus.Contended,
                        InvalidContendedStream);
                }

                if (AllowsConcurrentLocks)
                {
                    return new FileSystemExclusiveLockAttempt(
                        FileSystemExclusiveLockAttemptStatus.Acquired,
                        new MemoryStream());
                }

                if (ContendedLockAttempts > 0)
                {
                    ContendedLockAttempts--;
                    return new FileSystemExclusiveLockAttempt(
                        FileSystemExclusiveLockAttemptStatus.Contended,
                        null);
                }

                FileSystemExclusiveLockAttempt attempt = _inner.TryOpenExclusiveLock(path);
                if (ReparseLockAfterOpen)
                {
                    ReparsePath = path;
                }

                return attempt;
            }
            public void FlushDurable(Stream stream) => _inner.FlushDurable(stream);

            public void MoveNoOverwrite(string sourcePath, string destinationPath)
            {
                WriteCount++;
                if (CopyWithoutDeleteOnMove)
                {
                    using (Stream source = _inner.OpenRead(sourcePath))
                    using (Stream destination = _inner.OpenCreateNew(destinationPath))
                    {
                        source.CopyTo(destination);
                        _inner.FlushDurable(destination);
                    }

                    return;
                }

                if (OverwriteOnMove)
                {
                    FileSystemEntryStatus destination = _inner.GetStatus(destinationPath);
                    if (destination.Kind == FileSystemEntryKind.File) _inner.DeleteFile(destinationPath);
                }

                _inner.MoveNoOverwrite(sourcePath, destinationPath);
            }

            public void DeleteFile(string path) { WriteCount++; _inner.DeleteFile(path); }
            public void DeleteDirectory(string path, bool recursive)
            {
                WriteCount++;
                if (DeleteDirectoryException != null) throw DeleteDirectoryException;
                _inner.DeleteDirectory(path, recursive);
            }
        }

        private sealed class DisposeTrackingStream : MemoryStream
        {
            public bool IsDisposed { get; private set; }

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
