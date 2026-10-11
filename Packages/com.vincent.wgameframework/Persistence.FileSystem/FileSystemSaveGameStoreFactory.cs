using System;
using MxFramework.Platform;

namespace MxFramework.Persistence.FileSystem
{
    /// <summary>Contains either a preflighted filesystem provider factory or a structured failure.</summary>
    /// <remarks>NoAlloc when copied or inspected. Factory creation and capability probing are LowFreqAlloc.</remarks>
    public readonly struct FileSystemSaveGameStoreFactoryCreateResult
    {
        internal FileSystemSaveGameStoreFactoryCreateResult(
            FileSystemSaveGameStoreFactory factory,
            FileSystemCapabilityEvidence capabilityEvidence,
            SaveGameError error)
        {
            Factory = factory;
            CapabilityEvidence = capabilityEvidence;
            Error = error;
        }

        /// <summary>
        /// Gets the authoritative creation outcome. Individual evidence fields are diagnostic facts and must not be
        /// used as a substitute for this complete result invariant.
        /// </summary>
        public bool Success => Factory != null && Error.IsNone && CapabilityEvidence.HasRequiredPublicationPrimitives;
        public FileSystemSaveGameStoreFactory Factory { get; }
        public FileSystemCapabilityEvidence CapabilityEvidence { get; }
        public SaveGameError Error { get; }
    }

    /// <summary>
    /// Owns one validated filesystem root and its immutable capability evidence.
    /// </summary>
    /// <remarks>
    /// Create through <see cref="TryCreate(PlatformLocation)"/>. Creation is LowFreqAlloc and performs bounded filesystem
    /// writes in a one-use probe directory. Created stores share this factory's root-scoped worker capacity and backend.
    /// A slot-local writer lock coordinates same-slot publication across store instances,
    /// while operations targeting different slots may proceed concurrently.
    /// </remarks>
    public sealed class FileSystemSaveGameStoreFactory
    {
        private readonly string _rootPath;
        private readonly IFileSystemBackend _backend;
        private readonly IFileSystemDiagnosticSink _diagnostics;
        private readonly FileSystemWorkerCapacity _capacity;

        private FileSystemSaveGameStoreFactory(
            string rootPath,
            FileSystemCapabilityEvidence capabilityEvidence,
            IFileSystemBackend backend,
            IFileSystemDiagnosticSink diagnostics)
        {
            _rootPath = rootPath;
            CapabilityEvidence = capabilityEvidence;
            _backend = backend;
            _diagnostics = diagnostics;
            _capacity = new FileSystemWorkerCapacity(SaveGameLimits.MaximumConcurrentOperations);
        }

        public FileSystemCapabilityEvidence CapabilityEvidence { get; }

        /// <summary>Creates an independent filesystem generation store for this preflighted root.</summary>
        /// <remarks>LowFreqAlloc. All stores from this factory share capacity for at most eight active operations.</remarks>
        public ISaveGameStore CreateStore()
        {
            return CreateStore(null);
        }

        internal FileSystemSaveGameStore CreateStore(IFileSystemSaveGameStoreTestHook testHook)
        {
            return new FileSystemSaveGameStore(
                _rootPath,
                _backend,
                _diagnostics,
                _capacity,
                testHook);
        }

        /// <summary>Validates a writable filesystem location and probes required publication primitives.</summary>
        /// <remarks>LowFreqAlloc. Invalid locations fail before any filesystem write.</remarks>
        public static FileSystemSaveGameStoreFactoryCreateResult TryCreate(PlatformLocation location)
        {
            return TryCreate(location, BclFileSystemBackend.Instance, NullFileSystemDiagnosticSink.Instance);
        }

        internal static FileSystemSaveGameStoreFactoryCreateResult TryCreate(
            PlatformLocation location,
            IFileSystemBackend backend,
            IFileSystemDiagnosticSink diagnostics)
        {
            if (backend == null)
            {
                return Failure(SaveGameErrorCode.InvalidLocation, "location", "Filesystem backend is unavailable.");
            }

            if (diagnostics == null)
            {
                diagnostics = NullFileSystemDiagnosticSink.Instance;
            }

            if (location.Kind != PlatformLocationKind.FileSystemPath
                || !location.IsWritable
                || string.IsNullOrEmpty(location.Value))
            {
                return Failure(SaveGameErrorCode.InvalidLocation, "location", "A writable filesystem location is required.");
            }

            string rootPath;
            SaveGameError layoutError;
            if (!FileSystemSaveGameLayout.TryNormalizeRoot(location.Value, backend, out rootPath, out layoutError))
            {
                return new FileSystemSaveGameStoreFactoryCreateResult(null, default, layoutError);
            }

            FileSystemCapabilityEvidence evidence;
            SaveGameError probeError;
            if (!FileSystemCapabilityProbe.TryProbe(rootPath, backend, diagnostics, out evidence, out probeError))
            {
                return new FileSystemSaveGameStoreFactoryCreateResult(null, evidence, probeError);
            }

            var factory = new FileSystemSaveGameStoreFactory(rootPath, evidence, backend, diagnostics);
            return new FileSystemSaveGameStoreFactoryCreateResult(factory, evidence, SaveGameError.None);
        }

        internal string RootPath => _rootPath;
        internal IFileSystemBackend Backend => _backend;
        internal IFileSystemDiagnosticSink Diagnostics => _diagnostics;

        private static FileSystemSaveGameStoreFactoryCreateResult Failure(
            SaveGameErrorCode code,
            string path,
            string message)
        {
            return new FileSystemSaveGameStoreFactoryCreateResult(
                null,
                default,
                new SaveGameError(code, SaveGameOperationPhase.Validation, path, message));
        }
    }
}
