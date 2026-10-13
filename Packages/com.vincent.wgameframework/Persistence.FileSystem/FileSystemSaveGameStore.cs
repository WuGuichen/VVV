using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace MxFramework.Persistence.FileSystem
{
    internal enum FileSystemSaveGameStoreHookPoint
    {
        BeforeWriterLock = 0,
        BeforePublication = 1,
        AfterPublicationGate = 2,
        AfterPublication = 3,
        BeforeStagingCreate = 4,
        AfterStagingCreate = 5,
        BeforeFlush = 6,
        AfterFlush = 7,
        AfterStagingClosed = 8,
        BeforeVerify = 9,
        AfterVerify = 10
    }

    internal readonly struct FileSystemSaveGameStoreHookContext
    {
        public FileSystemSaveGameStoreHookContext(
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameGeneration generation,
            string stagingPath,
            string finalPath)
        {
            Profile = profile;
            Slot = slot;
            Generation = generation;
            StagingPath = stagingPath ?? string.Empty;
            FinalPath = finalPath ?? string.Empty;
        }

        public SaveGameProfileId Profile { get; }
        public SaveGameSlotId Slot { get; }
        public SaveGameGeneration Generation { get; }
        public string StagingPath { get; }
        public string FinalPath { get; }
    }

    internal interface IFileSystemSaveGameStoreTestHook
    {
        void Reach(FileSystemSaveGameStoreHookPoint point, FileSystemSaveGameStoreHookContext context);
    }

    /// <summary>Filesystem-backed SaveGame generation store using immutable publication.</summary>
    /// <remarks>
    /// Commit and Delete are AllocByDesign low-frequency operations. Operation property access, Poll and terminal
    /// RequestCancel are NoAlloc after initialization.
    /// </remarks>
    internal sealed class FileSystemSaveGameStore : ISaveGameStore
    {
        private readonly struct ListSlotCandidate
        {
            public ListSlotCandidate(SaveGameSlotId slot, string path)
            {
                Slot = slot;
                Path = path;
            }

            public SaveGameSlotId Slot { get; }
            public string Path { get; }
        }

        private readonly string _rootPath;
        private readonly IFileSystemBackend _backend;
        private readonly IFileSystemDiagnosticSink _diagnostics;
        private readonly FileSystemWorkerCapacity _capacity;
        private readonly FileSystemSlotWriterLeaseCoordinator _slotLeases;
        private readonly IFileSystemSaveGameStoreTestHook _testHook;
        private long _operationSequence;

        public FileSystemSaveGameStore(
            string rootPath,
            IFileSystemBackend backend,
            IFileSystemDiagnosticSink diagnostics,
            FileSystemWorkerCapacity capacity,
            IFileSystemSaveGameStoreTestHook testHook)
        {
            _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _diagnostics = diagnostics ?? NullFileSystemDiagnosticSink.Instance;
            _capacity = capacity ?? throw new ArgumentNullException(nameof(capacity));
            _slotLeases = new FileSystemSlotWriterLeaseCoordinator();
            _testHook = testHook;
        }

        public ISaveGameOperation<SaveGameCommitResult> Commit(
            SaveGameCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            SaveGameOperationId operationId = NextOperationId();
            return FileSystemSaveGameOperation<SaveGameCommitResult>.Start(
                operationId,
                _capacity,
                context => CommitWorker(request, context),
                cancellationToken);
        }

        public ISaveGameOperation<SaveGameDeleteResult> Delete(
            SaveGameDeleteRequest request,
            CancellationToken cancellationToken = default)
        {
            SaveGameOperationId operationId = NextOperationId();
            return FileSystemSaveGameOperation<SaveGameDeleteResult>.Start(
                operationId,
                _capacity,
                context => DeleteWorker(request, context),
                cancellationToken);
        }

        public ISaveGameOperation<SaveGameReadResult> Read(
            SaveGameReadRequest request,
            CancellationToken cancellationToken = default)
        {
            return FileSystemSaveGameOperation<SaveGameReadResult>.Start(
                NextOperationId(),
                _capacity,
                context => ReadWorker(request, context),
                cancellationToken);
        }

        public ISaveGameOperation<SaveGameListResult> List(
            SaveGameListRequest request,
            CancellationToken cancellationToken = default)
        {
            return FileSystemSaveGameOperation<SaveGameListResult>.Start(
                NextOperationId(),
                _capacity,
                context => ListWorker(request, context),
                cancellationToken);
        }

        internal FileSystemWorkerCapacity Capacity => _capacity;

        private FileSystemOperationCompletion<SaveGameCommitResult> CommitWorker(
            SaveGameCommitRequest request,
            FileSystemOperationWorkerContext context)
        {
            if (context.IsCancellationRequested)
            {
                return FileSystemOperationCompletion<SaveGameCommitResult>.Cancelled();
            }

            SaveGameError error = ValidateIdentity(
                request == null ? default : request.Profile,
                request == null ? default : request.Slot);
            if (!error.IsNone)
            {
                return FileSystemOperationCompletion<SaveGameCommitResult>.Failure(error);
            }

            if (request.Envelope == null)
            {
                return Failure<SaveGameCommitResult>(
                    SaveGameErrorCode.InvalidEnvelope,
                    SaveGameOperationPhase.Validation,
                    "envelope",
                    "Commit envelope is null.");
            }

            SaveGameEnvelopeResult decoded = SaveGameEnvelopeCodec.Decode(request.Envelope);
            if (!decoded.IsSuccess)
            {
                return FileSystemOperationCompletion<SaveGameCommitResult>.Failure(Sanitize(decoded.Error));
            }

            SaveGameEnvelope envelope = decoded.Envelope;
            if (envelope.RecordKind != SaveGameRecordKind.Save
                || envelope.Profile != request.Profile
                || envelope.Slot != request.Slot)
            {
                return Failure<SaveGameCommitResult>(
                    SaveGameErrorCode.EnvelopePayloadMismatch,
                    SaveGameOperationPhase.Validation,
                    "envelope",
                    "Commit envelope kind or identity does not match the request.");
            }

            if (!request.ExpectedHead.TryNext(out SaveGameGeneration next))
            {
                return Failure<SaveGameCommitResult>(
                    SaveGameErrorCode.GenerationExhausted,
                    SaveGameOperationPhase.Validation,
                    "expectedHead",
                    "Generation cannot be incremented.");
            }

            if (envelope.Generation != next)
            {
                return Failure<SaveGameCommitResult>(
                    SaveGameErrorCode.GenerationConflict,
                    SaveGameOperationPhase.Validation,
                    "generation",
                    "Envelope generation is not expected head plus one.");
            }

            byte[] bytes = decoded.Encoded.ToArray();
            return ExecuteWrite(
                request.Profile,
                request.Slot,
                request.ExpectedHead,
                next,
                bytes,
                context,
                warning => new SaveGameCommitResult(next, warning));
        }

        private FileSystemOperationCompletion<SaveGameDeleteResult> DeleteWorker(
            SaveGameDeleteRequest request,
            FileSystemOperationWorkerContext context)
        {
            if (context.IsCancellationRequested)
            {
                return FileSystemOperationCompletion<SaveGameDeleteResult>.Cancelled();
            }

            SaveGameError error = ValidateIdentity(
                request == null ? default : request.Profile,
                request == null ? default : request.Slot);
            if (!error.IsNone)
            {
                return FileSystemOperationCompletion<SaveGameDeleteResult>.Failure(error);
            }

            if (request.Build == null)
            {
                return Failure<SaveGameDeleteResult>(
                    SaveGameErrorCode.InvalidEnvelope,
                    SaveGameOperationPhase.Validation,
                    "build",
                    "Tombstone build descriptor is null.");
            }

            if (!request.ExpectedHead.TryNext(out SaveGameGeneration next))
            {
                return Failure<SaveGameDeleteResult>(
                    SaveGameErrorCode.GenerationExhausted,
                    SaveGameOperationPhase.Validation,
                    "expectedHead",
                    "Generation cannot be incremented.");
            }

            if (!SaveGameEnvelope.TryCreateTombstone(
                    request.Profile,
                    request.Slot,
                    next,
                    request.DeletedAtUtc,
                    request.Build,
                    out SaveGameEnvelope tombstone,
                    out error))
            {
                return FileSystemOperationCompletion<SaveGameDeleteResult>.Failure(Sanitize(error));
            }

            SaveGameEnvelopeResult encoded = SaveGameEnvelopeCodec.Encode(tombstone);
            if (!encoded.IsSuccess)
            {
                return FileSystemOperationCompletion<SaveGameDeleteResult>.Failure(Sanitize(encoded.Error));
            }

            return ExecuteWrite(
                request.Profile,
                request.Slot,
                request.ExpectedHead,
                next,
                encoded.Encoded.ToArray(),
                context,
                warning => new SaveGameDeleteResult(next, warning));
        }

        private FileSystemOperationCompletion<SaveGameReadResult> ReadWorker(
            SaveGameReadRequest request,
            FileSystemOperationWorkerContext context)
        {
            if (context.IsCancellationRequested)
            {
                return FileSystemOperationCompletion<SaveGameReadResult>.Cancelled();
            }

            SaveGameError validation = ValidateIdentity(
                request == null ? default : request.Profile,
                request == null ? default : request.Slot);
            if (!validation.IsNone)
            {
                return FileSystemOperationCompletion<SaveGameReadResult>.Failure(validation);
            }

            if (!request.Selection.IsValid
                || (request.RecoveryPolicy != SaveGameRecoveryPolicy.PrimaryOnly
                    && request.RecoveryPolicy != SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)
                || (request.Selection.Kind == SaveGameReadSelectionKind.ExactGeneration
                    && request.RecoveryPolicy != SaveGameRecoveryPolicy.PrimaryOnly))
            {
                return Failure<SaveGameReadResult>(
                    SaveGameErrorCode.InvalidSelection,
                    SaveGameOperationPhase.Validation,
                    "selection",
                    "Read selection or recovery policy is invalid.");
            }

            string slotPath = string.Empty;
            try
            {
                if (!FileSystemSaveGameLayout.TryGetSlotDirectory(
                        _rootPath,
                        request.Profile,
                        request.Slot,
                        _backend,
                        out slotPath,
                        out SaveGameError layoutError))
                {
                    return FileSystemOperationCompletion<SaveGameReadResult>.Failure(Sanitize(layoutError));
                }

                FileSystemEntryStatus slotStatus = _backend.GetStatus(slotPath);
                if (slotStatus.Kind == FileSystemEntryKind.Missing)
                {
                    return Failure<SaveGameReadResult>(
                        SaveGameErrorCode.SlotNotFound,
                        SaveGameOperationPhase.Read,
                        "slot",
                        "Slot does not exist.");
                }

                if (slotStatus.Kind != FileSystemEntryKind.Directory)
                {
                    return Failure<SaveGameReadResult>(
                        SaveGameErrorCode.InvalidLocation,
                        SaveGameOperationPhase.Read,
                        "slot",
                        "Slot entry is not a directory.");
                }

                if (!FileSystemStoreRecords.TryCollectGenerationsDescending(
                        _backend.GetEntries(slotPath),
                        SaveGameOperationPhase.Read,
                        out SaveGameGeneration[] generations,
                        out SaveGameError generationError))
                {
                    return FileSystemOperationCompletion<SaveGameReadResult>.Failure(Sanitize(generationError));
                }

                if (generations.Length == 0)
                {
                    return Failure<SaveGameReadResult>(
                        SaveGameErrorCode.SlotNotFound,
                        SaveGameOperationPhase.Read,
                        "slot",
                        "Slot has no published generation.");
                }

                SaveGameGeneration original = generations[0];
                SaveGameGeneration selected = request.Selection.Kind == SaveGameReadSelectionKind.Current
                    ? original
                    : request.Selection.Generation;
                FileSystemRecordReadResult primary = ReadGeneration(
                    slotPath,
                    request.Profile,
                    request.Slot,
                    selected,
                    SaveGameOperationPhase.Read,
                    "read");
                if (primary.Status == FileSystemRecordReadStatus.Success)
                {
                    return CompleteRead(primary, original, selected, SaveGameRecoverySource.Primary, request.IncludeDeleted);
                }

                if (request.Selection.Kind == SaveGameReadSelectionKind.ExactGeneration
                    || request.RecoveryPolicy != SaveGameRecoveryPolicy.PrimaryThenLastKnownGood
                    || primary.Status != FileSystemRecordReadStatus.RecoverableFailure)
                {
                    return FileSystemOperationCompletion<SaveGameReadResult>.Failure(primary.Error);
                }

                for (int index = 1; index < generations.Length; index++)
                {
                    if (context.IsCancellationRequested)
                    {
                        return FileSystemOperationCompletion<SaveGameReadResult>.Cancelled();
                    }

                    FileSystemRecordReadResult candidate = ReadGeneration(
                        slotPath,
                        request.Profile,
                        request.Slot,
                        generations[index],
                        SaveGameOperationPhase.Read,
                        "read");
                    if (candidate.Status == FileSystemRecordReadStatus.Success)
                    {
                        if (candidate.Envelope.RecordKind == SaveGameRecordKind.Tombstone)
                        {
                            return FileSystemOperationCompletion<SaveGameReadResult>.Failure(primary.Error);
                        }

                        return CompleteRead(
                            candidate,
                            original,
                            generations[index],
                            SaveGameRecoverySource.LastKnownGood,
                            request.IncludeDeleted);
                    }

                    if (candidate.Status == FileSystemRecordReadStatus.TerminalFailure)
                    {
                        return FileSystemOperationCompletion<SaveGameReadResult>.Failure(candidate.Error);
                    }
                }

                return FileSystemOperationCompletion<SaveGameReadResult>.Failure(primary.Error);
            }
            catch (Exception exception)
            {
                FileSystemDiagnosticReporter.ReportBestEffort(
                    _diagnostics,
                    "read",
                    slotPath,
                    exception);
                return FileSystemOperationCompletion<SaveGameReadResult>.Failure(
                    MapReadIo(exception, SaveGameOperationPhase.Read, "read"));
            }
        }

        private FileSystemOperationCompletion<SaveGameListResult> ListWorker(
            SaveGameListRequest request,
            FileSystemOperationWorkerContext context)
        {
            if (context.IsCancellationRequested)
            {
                return FileSystemOperationCompletion<SaveGameListResult>.Cancelled();
            }

            if (request == null || !request.Profile.IsValid)
            {
                return Failure<SaveGameListResult>(
                    SaveGameErrorCode.InvalidProfile,
                    SaveGameOperationPhase.Validation,
                    "profile",
                    "Profile id is invalid.");
            }

            string diagnosticPath = string.Empty;
            try
            {
                if (!FileSystemSaveGameLayout.TryGetProfileDirectory(
                        _rootPath,
                        request.Profile,
                        _backend,
                        out string profilePath,
                        out SaveGameError layoutError))
                {
                    return FileSystemOperationCompletion<SaveGameListResult>.Failure(Sanitize(layoutError));
                }

                diagnosticPath = profilePath;
                if (_backend.GetStatus(profilePath).Kind == FileSystemEntryKind.Missing)
                {
                    return FileSystemOperationCompletion<SaveGameListResult>.Success(
                        new SaveGameListResult(Array.Empty<SaveGameSlotSummary>()));
                }

                string slotsPath = Path.Combine(profilePath, "slots");
                if (!FileSystemSaveGameLayout.TryValidateContainedPath(
                        _rootPath,
                        slotsPath,
                        _backend,
                        out layoutError))
                {
                    return FileSystemOperationCompletion<SaveGameListResult>.Failure(Sanitize(layoutError));
                }

                diagnosticPath = slotsPath;
                FileSystemEntryStatus slotsStatus = _backend.GetStatus(slotsPath);
                if (slotsStatus.Kind == FileSystemEntryKind.Missing)
                {
                    return FileSystemOperationCompletion<SaveGameListResult>.Success(
                        new SaveGameListResult(Array.Empty<SaveGameSlotSummary>()));
                }

                if (slotsStatus.Kind != FileSystemEntryKind.Directory)
                {
                    return Failure<SaveGameListResult>(
                        SaveGameErrorCode.InvalidLocation,
                        SaveGameOperationPhase.Listing,
                        "slots",
                        "Profile slot namespace is not a directory.");
                }

                FileSystemDirectoryEntry[] entries = _backend.GetEntries(slotsPath);
                var candidates = new List<ListSlotCandidate>(entries.Length);
                for (int index = 0; index < entries.Length; index++)
                {
                    string name = entries[index].Name;
                    bool reserved = name != null
                        && name.StartsWith("s-", StringComparison.OrdinalIgnoreCase);
                    if (!reserved) continue;
                    if (entries[index].Kind != FileSystemEntryKind.Directory
                        || !FileSystemSaveGameLayout.TryDecodeSlotSegment(
                            name,
                            out SaveGameSlotId slot))
                    {
                        return Failure<SaveGameListResult>(
                            SaveGameErrorCode.InvalidSlot,
                            SaveGameOperationPhase.Listing,
                            "slot",
                            "Reserved slot directory name is malformed.");
                    }

                    candidates.Add(new ListSlotCandidate(slot, Path.Combine(slotsPath, name)));
                }

                if (candidates.Count > SaveGameLimits.MaximumListedSlots)
                {
                    return Failure<SaveGameListResult>(
                        SaveGameErrorCode.LimitExceeded,
                        SaveGameOperationPhase.Listing,
                        "slots",
                        "Slot listing exceeds the v1 limit.");
                }

                candidates.Sort((left, right) => left.Slot.CompareTo(right.Slot));
                var summaries = new List<SaveGameSlotSummary>(candidates.Count);
                for (int index = 0; index < candidates.Count; index++)
                {
                    if (context.IsCancellationRequested)
                    {
                        return FileSystemOperationCompletion<SaveGameListResult>.Cancelled();
                    }

                    ListSlotCandidate candidate = candidates[index];
                    if (!FileSystemStoreRecords.TryCollectGenerationsDescending(
                            _backend.GetEntries(candidate.Path),
                            SaveGameOperationPhase.Listing,
                            out SaveGameGeneration[] generations,
                            out SaveGameError generationError))
                    {
                        return FileSystemOperationCompletion<SaveGameListResult>.Failure(Sanitize(generationError));
                    }

                    if (generations.Length == 0) continue;
                    FileSystemRecordReadResult head = ReadGeneration(
                        candidate.Path,
                        request.Profile,
                        candidate.Slot,
                        generations[0],
                        SaveGameOperationPhase.Listing,
                        "list");
                    if (head.Status != FileSystemRecordReadStatus.Success)
                    {
                        return FileSystemOperationCompletion<SaveGameListResult>.Failure(head.Error);
                    }

                    if (request.IncludeDeleted
                        || head.Envelope.RecordKind != SaveGameRecordKind.Tombstone)
                    {
                        summaries.Add(new SaveGameSlotSummary(
                            candidate.Slot,
                            generations[0],
                            head.Envelope.UpdatedAtUtc,
                            head.Envelope.RecordKind));
                    }
                }

                return FileSystemOperationCompletion<SaveGameListResult>.Success(
                    new SaveGameListResult(summaries.ToArray()));
            }
            catch (Exception exception)
            {
                FileSystemDiagnosticReporter.ReportBestEffort(
                    _diagnostics,
                    "list",
                    diagnosticPath,
                    exception);
                return FileSystemOperationCompletion<SaveGameListResult>.Failure(
                    MapReadIo(exception, SaveGameOperationPhase.Listing, "list"));
            }
        }

        private FileSystemOperationCompletion<TResult> ExecuteWrite<TResult>(
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameGeneration expectedHead,
            SaveGameGeneration next,
            byte[] bytes,
            FileSystemOperationWorkerContext context,
            Func<SaveGameWarning, TResult> resultFactory)
        {
            string slotPath = string.Empty;
            string stagingPath = string.Empty;
            string finalPath = string.Empty;
            string writerLockPath = string.Empty;
            string diagnosticPath = string.Empty;
            string errorPath = "location";
            FileSystemSlotWriterLease slotLease = null;
            Stream writerLock = null;
            bool published = false;
            TResult successResult = resultFactory(SaveGameWarning.None);

            try
            {
                if (!FileSystemSaveGameLayout.TryGetSlotDirectory(
                        _rootPath,
                        profile,
                        slot,
                        _backend,
                        out slotPath,
                        out SaveGameError layoutError))
                {
                    return FileSystemOperationCompletion<TResult>.Failure(Sanitize(layoutError));
                }

                diagnosticPath = slotPath;
                if (!_slotLeases.WaitAcquire(
                        slotPath,
                        context.PublicationGate,
                        CancellationToken.None,
                        out slotLease))
                {
                    return context.IsCancellationRequested
                        ? FileSystemOperationCompletion<TResult>.Cancelled()
                        : Failure<TResult>(
                            SaveGameErrorCode.InvalidLocation,
                            SaveGameOperationPhase.Validation,
                            "location",
                            "Slot path is invalid.");
                }

                if (context.IsCancellationRequested)
                {
                    return FileSystemOperationCompletion<TResult>.Cancelled();
                }

                errorPath = "staging";
                if (!TryEnsureSlotDirectory(slotPath, out SaveGameError directoryError))
                {
                    return FileSystemOperationCompletion<TResult>.Failure(directoryError);
                }

                writerLockPath = Path.Combine(slotPath, ".writer.lock");
                diagnosticPath = writerLockPath;
                ReachHook(
                    FileSystemSaveGameStoreHookPoint.BeforeWriterLock,
                    profile,
                    slot,
                    next,
                    string.Empty,
                    string.Empty);

                FileSystemExclusiveLockAcquireResult lockResult = FileSystemExclusiveLockAcquirer.WaitAcquire(
                    _rootPath,
                    writerLockPath,
                    _backend,
                    _diagnostics,
                    context.PublicationGate,
                    CancellationToken.None);
                if (lockResult.Status == FileSystemExclusiveLockAcquireStatus.Cancelled)
                {
                    return FileSystemOperationCompletion<TResult>.Cancelled();
                }

                if (lockResult.Status != FileSystemExclusiveLockAcquireStatus.Acquired)
                {
                    return FileSystemOperationCompletion<TResult>.Failure(Sanitize(lockResult.Error));
                }

                writerLock = lockResult.Stream;
                if (context.IsCancellationRequested)
                {
                    return FileSystemOperationCompletion<TResult>.Cancelled();
                }

                if (!FileSystemGenerationLayout.TryFindHighest(
                        _backend.GetEntries(slotPath),
                        SaveGameOperationPhase.Publication,
                        out SaveGameGeneration actualHead,
                        out SaveGameError generationError))
                {
                    return FileSystemOperationCompletion<TResult>.Failure(Sanitize(generationError));
                }

                if (actualHead != expectedHead)
                {
                    return Conflict<TResult>();
                }

                finalPath = Path.Combine(slotPath, FileSystemGenerationLayout.Format(next));
                stagingPath = Path.Combine(slotPath, ".staging-" + Guid.NewGuid().ToString("N"));
                diagnosticPath = stagingPath;
                if (bytes == null || bytes.Length == 0 || bytes.Length > SaveGameLimits.MaximumEnvelopeBytes)
                {
                    return Failure<TResult>(
                        SaveGameErrorCode.LimitExceeded,
                        SaveGameOperationPhase.Validation,
                        "staging",
                        "Encoded envelope exceeds the filesystem write bound.");
                }

                ReachHook(
                    FileSystemSaveGameStoreHookPoint.BeforeStagingCreate,
                    profile,
                    slot,
                    next,
                    stagingPath,
                    finalPath);
                using (Stream staging = _backend.OpenCreateNew(stagingPath))
                {
                    ReachHook(
                        FileSystemSaveGameStoreHookPoint.AfterStagingCreate,
                        profile,
                        slot,
                        next,
                        stagingPath,
                        finalPath);
                    staging.Write(bytes, 0, bytes.Length);
                    errorPath = "flush";
                    ReachHook(
                        FileSystemSaveGameStoreHookPoint.BeforeFlush,
                        profile,
                        slot,
                        next,
                        stagingPath,
                        finalPath);
                    _backend.FlushDurable(staging);
                    ReachHook(
                        FileSystemSaveGameStoreHookPoint.AfterFlush,
                        profile,
                        slot,
                        next,
                        stagingPath,
                        finalPath);
                }

                ReachHook(
                    FileSystemSaveGameStoreHookPoint.AfterStagingClosed,
                    profile,
                    slot,
                    next,
                    stagingPath,
                    finalPath);

                context.ReportProgress(500);
                if (context.IsCancellationRequested)
                {
                    return FileSystemOperationCompletion<TResult>.Cancelled();
                }

                errorPath = "verify";
                ReachHook(
                    FileSystemSaveGameStoreHookPoint.BeforeVerify,
                    profile,
                    slot,
                    next,
                    stagingPath,
                    finalPath);
                if (!FileBytesEqual(stagingPath, bytes))
                {
                    return Failure<TResult>(
                        SaveGameErrorCode.IntegrityFailed,
                        SaveGameOperationPhase.Integrity,
                        "verify",
                        "Staging bytes did not verify exactly.");
                }

                ReachHook(
                    FileSystemSaveGameStoreHookPoint.AfterVerify,
                    profile,
                    slot,
                    next,
                    stagingPath,
                    finalPath);

                if (context.IsCancellationRequested)
                {
                    return FileSystemOperationCompletion<TResult>.Cancelled();
                }

                ReachHook(
                    FileSystemSaveGameStoreHookPoint.BeforePublication,
                    profile,
                    slot,
                    next,
                    stagingPath,
                    finalPath);
                if (!context.TryEnterPublishing())
                {
                    return FileSystemOperationCompletion<TResult>.Cancelled();
                }

                ReachHook(
                    FileSystemSaveGameStoreHookPoint.AfterPublicationGate,
                    profile,
                    slot,
                    next,
                    stagingPath,
                    finalPath);

                errorPath = "publish";
                diagnosticPath = finalPath;
                try
                {
                    _backend.MoveNoOverwrite(stagingPath, finalPath);
                    published = true;
                }
                catch (Exception moveException)
                {
                    FileSystemDiagnosticReporter.ReportBestEffort(
                        _diagnostics,
                        "publish-move",
                        finalPath,
                        moveException);
                    FileSystemOperationCompletion<TResult> reconciled = ReconcileMove(
                        finalPath,
                        bytes,
                        moveException,
                        successResult,
                        out published);
                    if (!published) return reconciled;
                }

                ReachAfterPublicationBestEffort(profile, slot, next, stagingPath, finalPath);
                SaveGameWarning retentionWarning = ApplyRetention(
                    slotPath,
                    profile,
                    slot,
                    next);
                successResult = resultFactory(retentionWarning);
                return FileSystemOperationCompletion<TResult>.Success(successResult);
            }
            catch (Exception exception)
            {
                FileSystemDiagnosticReporter.ReportBestEffort(
                    _diagnostics,
                    "write",
                    diagnosticPath,
                    exception);
                if (published)
                {
                    return FileSystemOperationCompletion<TResult>.Success(successResult);
                }

                return FileSystemOperationCompletion<TResult>.Failure(MapIo(exception, errorPath));
            }
            finally
            {
                if (!published && !string.IsNullOrEmpty(stagingPath))
                {
                    DeleteStagingBestEffort(stagingPath);
                }

                if (writerLock != null)
                {
                    try
                    {
                        writerLock.Dispose();
                    }
                    catch (Exception exception)
                    {
                        FileSystemDiagnosticReporter.ReportBestEffort(
                            _diagnostics,
                            "lock-dispose",
                            writerLockPath,
                            exception);
                    }
                }
                if (slotLease != null) slotLease.Dispose();
            }
        }

        private FileSystemOperationCompletion<TResult> ReconcileMove<TResult>(
            string finalPath,
            byte[] expectedBytes,
            Exception moveException,
            TResult successResult,
            out bool published)
        {
            published = false;
            FileSystemEntryStatus status = _backend.GetStatus(finalPath);
            if (status.Kind == FileSystemEntryKind.Missing)
            {
                return FileSystemOperationCompletion<TResult>.Failure(MapIo(moveException, "publish"));
            }

            if (FileBytesEqual(finalPath, expectedBytes))
            {
                published = true;
                return FileSystemOperationCompletion<TResult>.Success(successResult);
            }

            return Conflict<TResult>();
        }

        private bool FileBytesEqual(string path, byte[] expected)
        {
            FileSystemEntryStatus status = _backend.GetStatus(path);
            if (status.Kind != FileSystemEntryKind.File || status.Length != expected.Length)
            {
                return false;
            }

            using (Stream stream = _backend.OpenRead(path))
            {
                var buffer = new byte[8192];
                int offset = 0;
                while (offset < expected.Length)
                {
                    int requested = Math.Min(buffer.Length, expected.Length - offset);
                    int read = stream.Read(buffer, 0, requested);
                    if (read <= 0) return false;
                    for (int index = 0; index < read; index++)
                    {
                        if (buffer[index] != expected[offset + index]) return false;
                    }

                    offset += read;
                }

                return stream.ReadByte() == -1;
            }
        }

        private bool TryReadBounded(string path, out byte[] bytes)
        {
            bytes = null;
            FileSystemEntryStatus status = _backend.GetStatus(path);
            if (status.Kind != FileSystemEntryKind.File
                || status.Length <= 0
                || status.Length > SaveGameLimits.MaximumEnvelopeBytes)
            {
                return false;
            }

            bytes = new byte[(int)status.Length];
            using (Stream stream = _backend.OpenRead(path))
            {
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0)
                    {
                        bytes = null;
                        return false;
                    }

                    offset += read;
                }

                if (stream.ReadByte() != -1)
                {
                    bytes = null;
                    return false;
                }
            }

            return true;
        }

        private bool TryEnsureSlotDirectory(string slotPath, out SaveGameError error)
        {
            error = SaveGameError.None;
            string slotsPath = Path.GetDirectoryName(slotPath);
            string profilePath = Path.GetDirectoryName(slotsPath);
            string profilesPath = Path.GetDirectoryName(profilePath);
            string[] paths = { profilesPath, profilePath, slotsPath, slotPath };
            for (int index = 0; index < paths.Length; index++)
            {
                if (string.IsNullOrEmpty(paths[index])
                    || !FileSystemSaveGameLayout.TryValidateContainedPath(
                        _rootPath,
                        paths[index],
                        _backend,
                        out error))
                {
                    return false;
                }

                FileSystemEntryStatus status = _backend.GetStatus(paths[index]);
                if (status.Kind == FileSystemEntryKind.Missing)
                {
                    _backend.CreateDirectory(paths[index]);
                }
                else if (status.Kind != FileSystemEntryKind.Directory)
                {
                    error = new SaveGameError(
                        SaveGameErrorCode.InvalidLocation,
                        SaveGameOperationPhase.Validation,
                        "location",
                        "Filesystem store directory is not a directory.");
                    return false;
                }

                if (!FileSystemSaveGameLayout.TryValidateContainedPath(
                        _rootPath,
                        paths[index],
                        _backend,
                        out error))
                {
                    return false;
                }
            }

            error = SaveGameError.None;
            return true;
        }

        private void DeleteStagingBestEffort(string stagingPath)
        {
            try
            {
                if (_backend.GetStatus(stagingPath).Kind == FileSystemEntryKind.File)
                {
                    _backend.DeleteFile(stagingPath);
                }
            }
            catch (Exception exception)
            {
                FileSystemDiagnosticReporter.ReportBestEffort(
                    _diagnostics,
                    "staging-cleanup",
                    stagingPath,
                    exception);
            }
        }

        private void ReachHook(
            FileSystemSaveGameStoreHookPoint point,
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameGeneration generation,
            string stagingPath,
            string finalPath)
        {
            if (_testHook == null) return;
            _testHook.Reach(
                point,
                new FileSystemSaveGameStoreHookContext(
                    profile,
                    slot,
                    generation,
                    stagingPath,
                    finalPath));
        }

        private void ReachAfterPublicationBestEffort(
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameGeneration generation,
            string stagingPath,
            string finalPath)
        {
            try
            {
                ReachHook(
                    FileSystemSaveGameStoreHookPoint.AfterPublication,
                    profile,
                    slot,
                    generation,
                    stagingPath,
                    finalPath);
            }
            catch (Exception exception)
            {
                FileSystemDiagnosticReporter.ReportBestEffort(
                    _diagnostics,
                    "after-publication",
                    finalPath,
                    exception);
            }
        }

        private FileSystemRecordReadResult ReadGeneration(
            string slotPath,
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameGeneration generation,
            SaveGameOperationPhase phase,
            string logicalPath)
        {
            return FileSystemStoreRecords.Read(
                Path.Combine(slotPath, FileSystemGenerationLayout.Format(generation)),
                profile,
                slot,
                generation,
                _backend,
                _diagnostics,
                phase,
                logicalPath);
        }

        private static FileSystemOperationCompletion<SaveGameReadResult> CompleteRead(
            FileSystemRecordReadResult record,
            SaveGameGeneration original,
            SaveGameGeneration selected,
            SaveGameRecoverySource source,
            bool includeDeleted)
        {
            if (record.Envelope.RecordKind == SaveGameRecordKind.Tombstone && !includeDeleted)
            {
                return Failure<SaveGameReadResult>(
                    SaveGameErrorCode.SlotDeleted,
                    SaveGameOperationPhase.Read,
                    "slot",
                    "Selected generation is a tombstone.");
            }

            return FileSystemOperationCompletion<SaveGameReadResult>.Success(
                new SaveGameReadResult(record.Encoded, original, selected, source));
        }

        private SaveGameWarning ApplyRetention(
            string slotPath,
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameGeneration head)
        {
            bool failed = false;
            try
            {
                if (!FileSystemStoreRecords.TryCollectGenerationsDescending(
                        _backend.GetEntries(slotPath),
                        SaveGameOperationPhase.Retention,
                        out SaveGameGeneration[] generations,
                        out SaveGameError generationError))
                {
                    FileSystemDiagnosticReporter.ReportBestEffort(
                        _diagnostics,
                        "retention-enumeration",
                        slotPath,
                        new IOException(generationError.Message));
                    return SaveGameWarning.RetentionCleanupFailed;
                }

                SaveGameGeneration previous = SaveGameGeneration.None;
                for (int index = 0; index < generations.Length; index++)
                {
                    SaveGameGeneration generation = generations[index];
                    if (generation == head) continue;
                    FileSystemRecordReadResult record = ReadGeneration(
                        slotPath,
                        profile,
                        slot,
                        generation,
                        SaveGameOperationPhase.Retention,
                        "retention");
                    if (record.Status == FileSystemRecordReadStatus.Success)
                    {
                        previous = generation;
                        break;
                    }

                    if (record.Error.Code == SaveGameErrorCode.IoFailure
                        || record.Error.Code == SaveGameErrorCode.QuotaExceeded
                        || record.Error.Code == SaveGameErrorCode.PermissionDenied)
                    {
                        return SaveGameWarning.RetentionCleanupFailed;
                    }
                }

                for (int index = 0; index < generations.Length; index++)
                {
                    SaveGameGeneration generation = generations[index];
                    if (generation == head || generation == previous) continue;
                    string path = Path.Combine(slotPath, FileSystemGenerationLayout.Format(generation));
                    try
                    {
                        _backend.DeleteFile(path);
                    }
                    catch (Exception exception)
                    {
                        failed = true;
                        FileSystemDiagnosticReporter.ReportBestEffort(
                            _diagnostics,
                            "retention-delete",
                            path,
                            exception);
                    }
                }
            }
            catch (Exception exception)
            {
                FileSystemDiagnosticReporter.ReportBestEffort(
                    _diagnostics,
                    "retention",
                    slotPath,
                    exception);
                failed = true;
            }

            return failed
                ? SaveGameWarning.RetentionCleanupFailed
                : SaveGameWarning.None;
        }

        private SaveGameOperationId NextOperationId()
        {
            long value = Interlocked.Increment(ref _operationSequence);
            if (value <= 0)
            {
                Interlocked.Exchange(ref _operationSequence, 1);
                value = 1;
            }

            return new SaveGameOperationId("fs-" + value.ToString(CultureInfo.InvariantCulture));
        }

        private static SaveGameError ValidateIdentity(SaveGameProfileId profile, SaveGameSlotId slot)
        {
            if (!profile.IsValid)
            {
                return new SaveGameError(
                    SaveGameErrorCode.InvalidProfile,
                    SaveGameOperationPhase.Validation,
                    "profile",
                    "Profile id is invalid.");
            }

            if (!slot.IsValid)
            {
                return new SaveGameError(
                    SaveGameErrorCode.InvalidSlot,
                    SaveGameOperationPhase.Validation,
                    "slot",
                    "Slot id is invalid.");
            }

            return SaveGameError.None;
        }

        private static SaveGameError Sanitize(SaveGameError error)
        {
            if (error.IsNone) return error;
            return new SaveGameError(error.Code, error.Phase, error.Path, error.Message);
        }

        private SaveGameError MapIo(Exception exception, string path)
        {
            return new SaveGameError(
                FileSystemIoErrorMapper.GetCode(_backend, exception),
                SaveGameOperationPhase.Publication,
                string.IsNullOrEmpty(path) ? "publish" : path,
                "Filesystem write failed.");
        }

        private SaveGameError MapReadIo(
            Exception exception,
            SaveGameOperationPhase phase,
            string path)
        {
            return new SaveGameError(
                FileSystemIoErrorMapper.GetCode(_backend, exception),
                phase,
                path,
                "Filesystem read failed.");
        }

        private static FileSystemOperationCompletion<TResult> Conflict<TResult>()
        {
            return Failure<TResult>(
                SaveGameErrorCode.GenerationConflict,
                SaveGameOperationPhase.Publication,
                "generation",
                "Expected generation does not match the authoritative head.");
        }

        private static FileSystemOperationCompletion<TResult> Failure<TResult>(
            SaveGameErrorCode code,
            SaveGameOperationPhase phase,
            string path,
            string message)
        {
            return FileSystemOperationCompletion<TResult>.Failure(
                new SaveGameError(code, phase, path, message));
        }

    }
}
