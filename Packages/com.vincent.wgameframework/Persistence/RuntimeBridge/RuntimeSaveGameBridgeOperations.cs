using System;
using System.Collections.Generic;
using System.Threading;
using MxFramework.Runtime;

namespace MxFramework.Persistence.RuntimeBridge
{
    internal static class BridgeOperation<TResult>
    {
        public static IRuntimeSaveGameOperation<TResult> Failed(RuntimeSaveGameBridgeErrorDetail error) =>
            new CompletedBridgeOperation<TResult>(SaveGameOperationStatus.Failed, default, error);
        public static IRuntimeSaveGameOperation<TResult> Cancelled() =>
            new CompletedBridgeOperation<TResult>(SaveGameOperationStatus.Cancelled, default, null);
    }

    internal static class RuntimeSaveGameOperationIds
    {
        private static long _next;
        public static SaveGameOperationId Next() =>
            new SaveGameOperationId("runtime-bridge-" + Interlocked.Increment(ref _next).ToString(
                System.Globalization.CultureInfo.InvariantCulture));
    }

    internal sealed class CompletedBridgeOperation<TResult> : IRuntimeSaveGameOperation<TResult>
    {
        private readonly TResult _result;
        public CompletedBridgeOperation(SaveGameOperationStatus status, TResult result, RuntimeSaveGameBridgeErrorDetail error)
        {
            OperationId = RuntimeSaveGameOperationIds.Next();
            Status = status;
            _result = result;
            ErrorDetail = error;
        }
        public SaveGameOperationId OperationId { get; }
        public SaveGameOperationStatus Status { get; }
        public float Progress => Status == SaveGameOperationStatus.Succeeded ? 1f : 0f;
        public bool IsDone => true;
        public bool CanCancel => false;
        public RuntimeSaveGameBridgeErrorDetail ErrorDetail { get; }
        public bool HasResult => Status == SaveGameOperationStatus.Succeeded;
        public TResult Result => _result;
        public bool TryGetResult(out TResult result) { result = _result; return HasResult; }
        public void Poll() { }
        public void RequestCancel() { }
    }

    internal abstract class ProviderBridgeOperation<TProviderResult, TResult> : IRuntimeSaveGameOperation<TResult>
    {
        private ISaveGameOperation<TProviderResult> _provider;
        private SaveGameOperationId _operationId;
        private SaveGameOperationStatus _status;
        private float _progress;
        private RuntimeSaveGameBridgeErrorDetail _error;
        private TResult _result;
        private bool _hasResult;
        private bool _released;
        private bool _observedProvider;
        private SaveGameOperationId _providerOperationId;
        private SaveGameOperationStatus _providerStatus;

        protected ProviderBridgeOperation(ISaveGameOperation<TProviderResult> provider)
        {
            _operationId = RuntimeSaveGameOperationIds.Next();
            _provider = provider;
        }

        protected void Initialize()
        {
            if (_provider == null)
            {
                Fail(InvalidProvider("provider", "Store returned a null operation."));
                return;
            }
            Poll();
        }

        public SaveGameOperationId OperationId => _operationId;
        public SaveGameOperationStatus Status => _status;
        public float Progress => _progress;
        public bool IsDone => IsTerminal(_status);
        public bool CanCancel
        {
            get
            {
                if (IsDone || _provider == null) return false;
                try { return _provider.CanCancel; }
                catch (Exception exception) { Fail(ProviderException("canCancel", exception)); return false; }
            }
        }
        public RuntimeSaveGameBridgeErrorDetail ErrorDetail => _error;
        public bool HasResult => _hasResult;
        public TResult Result => _result;
        public bool TryGetResult(out TResult result) { result = _result; return _hasResult; }

        public void Poll()
        {
            if (IsDone || _provider == null) return;
            try
            {
                _provider.Poll();
                SaveGameOperationStatus status = _provider.Status;
                bool terminal = IsTerminal(status);
                bool reportedDone = _provider.IsDone;
                bool canCancel = _provider.CanCancel;
                bool hasResult = _provider.HasResult;
                bool tryHasResult = _provider.TryGetResult(out TProviderResult providerResult);
                SaveGameError providerError = _provider.Error;
                float progress = _provider.Progress;
                SaveGameOperationId providerOperationId = _provider.OperationId;
                if (!IsKnownOperationStatus(status) || terminal != reportedDone ||
                    terminal && canCancel || float.IsNaN(progress) || float.IsInfinity(progress) ||
                    progress < 0f || progress > 1f || progress < _progress || hasResult != tryHasResult ||
                    !providerOperationId.IsValid || _observedProvider && providerOperationId != _providerOperationId ||
                    _observedProvider && _providerStatus == SaveGameOperationStatus.Running &&
                        status == SaveGameOperationStatus.Pending)
                {
                    Fail(InvalidProvider("state", "Store operation exposed contradictory status metadata."));
                    return;
                }
                _providerOperationId = providerOperationId;
                _providerStatus = status;
                _observedProvider = true;
                if (!IsValidProviderError(providerError))
                {
                    Fail(InvalidProvider("error", "Store operation exposed an invalid error tuple."));
                    return;
                }
                if (!terminal && (hasResult || !providerError.IsNone))
                {
                    Fail(InvalidProvider("running", "A non-terminal store operation exposed a result or error."));
                    return;
                }

                _progress = progress;
                if (!terminal)
                {
                    _status = status;
                    return;
                }

                if (status == SaveGameOperationStatus.Succeeded)
                {
                    if (!hasResult || !providerError.IsNone || progress != 1f ||
                        !EqualityComparer<TProviderResult>.Default.Equals(providerResult, _provider.Result))
                    {
                        Fail(InvalidProvider("result", "Succeeded store operation did not expose exactly one result."));
                        return;
                    }
                    RuntimeSaveGameBridgeResult<TResult> translated = TranslateSuccess(providerResult);
                    if (!translated.Success)
                    {
                        Fail(translated.ErrorDetail ?? InvalidProvider("translation", "Bridge translation failed without detail."));
                        return;
                    }
                    _result = translated.Value;
                    _hasResult = true;
                    _status = SaveGameOperationStatus.Succeeded;
                    _progress = 1f;
                    ReleaseProvider();
                    return;
                }

                if (hasResult || status == SaveGameOperationStatus.Failed && providerError.IsNone ||
                    status == SaveGameOperationStatus.TimedOut && providerError.Code != SaveGameErrorCode.Timeout ||
                    status == SaveGameOperationStatus.Cancelled && !providerError.IsNone)
                {
                    Fail(InvalidProvider("terminal", "Store operation exposed contradictory terminal outcome."));
                    return;
                }

                _status = status;
                _error = status == SaveGameOperationStatus.Cancelled
                    ? null
                    : RuntimeSaveGameService.PersistenceFailure(ProviderPhase, providerError);
                ReleaseProvider();
            }
            catch (Exception exception)
            {
                Fail(ProviderException("poll", exception));
            }
        }

        public void RequestCancel()
        {
            if (IsDone || _provider == null) return;
            try { _provider.RequestCancel(); }
            catch (Exception exception) { Fail(ProviderException("cancel", exception)); }
        }

        protected abstract RuntimeSaveGameBridgePhase ProviderPhase { get; }
        protected abstract RuntimeSaveGameBridgeResult<TResult> TranslateSuccess(TProviderResult result);
        protected virtual void OnReleased() { }

        private void Fail(RuntimeSaveGameBridgeErrorDetail error)
        {
            if (IsDone) return;
            _status = SaveGameOperationStatus.Failed;
            _progress = 0f;
            _error = error;
            _hasResult = false;
            _result = default;
            ReleaseProvider();
        }

        private void ReleaseProvider()
        {
            _provider = null;
            if (_released) return;
            _released = true;
            OnReleased();
        }

        private RuntimeSaveGameBridgeErrorDetail ProviderException(string path, Exception exception) =>
            RuntimeSaveGameService.PersistenceFailure(ProviderPhase,
                new SaveGameError(SaveGameErrorCode.StoreUnavailable,
                    ProviderPhase == RuntimeSaveGameBridgePhase.StoreRead ? SaveGameOperationPhase.Read : SaveGameOperationPhase.Publication,
                    path, "Store operation threw an exception.", exception));

        private static RuntimeSaveGameBridgeErrorDetail InvalidProvider(string path, string message) =>
            new RuntimeSaveGameBridgeErrorDetail(RuntimeSaveGameBridgeErrorCode.InvalidProviderOperation,
                RuntimeSaveGameBridgePhase.None,
                new SaveGameError(SaveGameErrorCode.StoreUnavailable, SaveGameOperationPhase.Validation, path, message),
                null, null, RuntimeSaveGameTargetValidationError.None);

        private static bool IsTerminal(SaveGameOperationStatus status) =>
            status == SaveGameOperationStatus.Succeeded || status == SaveGameOperationStatus.Failed ||
            status == SaveGameOperationStatus.Cancelled || status == SaveGameOperationStatus.TimedOut;

        private static bool IsValidProviderError(SaveGameError error)
        {
            if (!IsKnownErrorCode(error.Code) || !IsKnownOperationPhase(error.Phase)) return false;
            if (error.IsNone)
                return error.Phase == SaveGameOperationPhase.None && string.IsNullOrEmpty(error.Path) &&
                       string.IsNullOrEmpty(error.Message) && error.Exception == null;
            return error.Phase != SaveGameOperationPhase.None;
        }

        private static bool IsKnownOperationStatus(SaveGameOperationStatus status) =>
            status >= SaveGameOperationStatus.Pending && status <= SaveGameOperationStatus.TimedOut;

        private static bool IsKnownOperationPhase(SaveGameOperationPhase phase) =>
            phase >= SaveGameOperationPhase.None && phase <= SaveGameOperationPhase.Retention;

        private static bool IsKnownErrorCode(SaveGameErrorCode code)
        {
            switch (code)
            {
                case SaveGameErrorCode.None:
                case SaveGameErrorCode.InvalidProfile:
                case SaveGameErrorCode.InvalidSlot:
                case SaveGameErrorCode.InvalidOperationId:
                case SaveGameErrorCode.InvalidSelection:
                case SaveGameErrorCode.Busy:
                case SaveGameErrorCode.GenerationConflict:
                case SaveGameErrorCode.GenerationExhausted:
                case SaveGameErrorCode.SlotNotFound:
                case SaveGameErrorCode.SlotDeleted:
                case SaveGameErrorCode.GenerationNotFound:
                case SaveGameErrorCode.LimitExceeded:
                case SaveGameErrorCode.RuntimeCheckpointRejected:
                case SaveGameErrorCode.InvalidTimestamp:
                case SaveGameErrorCode.SerializeFailed:
                case SaveGameErrorCode.RuntimeSerializationFailed:
                case SaveGameErrorCode.StoreUnavailable:
                case SaveGameErrorCode.FaultInjected:
                case SaveGameErrorCode.InvalidLocation:
                case SaveGameErrorCode.AtomicCommitUnsupported:
                case SaveGameErrorCode.PermissionDenied:
                case SaveGameErrorCode.QuotaExceeded:
                case SaveGameErrorCode.IoFailure:
                case SaveGameErrorCode.InvalidEnvelope:
                case SaveGameErrorCode.IntegrityFailed:
                case SaveGameErrorCode.UnsupportedEnvelopeVersion:
                case SaveGameErrorCode.UnsupportedEnvelopeFeature:
                case SaveGameErrorCode.UnsupportedTransform:
                case SaveGameErrorCode.EnvelopePayloadMismatch:
                case SaveGameErrorCode.RuntimeCompatibilityFailed:
                case SaveGameErrorCode.CandidateRestoreFailed:
                case SaveGameErrorCode.Timeout:
                    return true;
                default:
                    return false;
            }
        }
    }

    internal sealed class CommitBridgeOperation : ProviderBridgeOperation<SaveGameCommitResult, SaveGameCommitResult>
    {
        private readonly RuntimeSaveGameWritePlan _plan;
        private readonly object _ownerToken;
        private readonly SaveGameGeneration _expectedGeneration;

        public CommitBridgeOperation(ISaveGameOperation<SaveGameCommitResult> provider,
            RuntimeSaveGameWritePlan plan, object ownerToken) : base(provider)
        {
            _plan = plan;
            _ownerToken = ownerToken;
            plan.Request.ExpectedHead.TryNext(out _expectedGeneration);
            Initialize();
        }

        protected override RuntimeSaveGameBridgePhase ProviderPhase => RuntimeSaveGameBridgePhase.StoreCommit;

        protected override RuntimeSaveGameBridgeResult<SaveGameCommitResult> TranslateSuccess(SaveGameCommitResult result)
        {
            if (result.Generation != _expectedGeneration || !result.Generation.IsPublished)
            {
                return RuntimeSaveGameBridgeResult<SaveGameCommitResult>.Failed(
                    new RuntimeSaveGameBridgeErrorDetail(RuntimeSaveGameBridgeErrorCode.InvalidProvenance,
                        RuntimeSaveGameBridgePhase.StoreCommit,
                        RuntimeSaveGameService.ContractFailure(SaveGameErrorCode.RuntimeSerializationFailed,
                            SaveGameOperationPhase.Validation, "commit", "Store result provenance was invalid."),
                        null, null,
                        RuntimeSaveGameTargetValidationError.None));
            }
            SaveGameWarning warning = result.Warning;
            if (!Enum.IsDefined(typeof(SaveGameWarningCode), warning.Code) ||
                warning.IsNone && (warning.Phase != SaveGameOperationPhase.None || warning.Path.Length != 0) ||
                !warning.IsNone && !warning.Equals(SaveGameWarning.RetentionCleanupFailed))
            {
                return RuntimeSaveGameBridgeResult<SaveGameCommitResult>.Failed(
                    new RuntimeSaveGameBridgeErrorDetail(RuntimeSaveGameBridgeErrorCode.InvalidProviderOperation,
                        RuntimeSaveGameBridgePhase.StoreCommit,
                        RuntimeSaveGameService.ContractFailure(SaveGameErrorCode.RuntimeSerializationFailed,
                            SaveGameOperationPhase.Validation, "commit.warning", "Store warning tuple was invalid."),
                        null, null,
                        RuntimeSaveGameTargetValidationError.None));
            }
            return RuntimeSaveGameBridgeResult<SaveGameCommitResult>.Succeeded(result);
        }

        protected override void OnReleased()
        {
            if (_plan != null) _plan.TryReleaseTransferred(_ownerToken);
        }
    }

    internal sealed class PrepareLoadBridgeOperation : ProviderBridgeOperation<SaveGameReadResult, PreparedRuntimeSaveGame>
    {
        private readonly RuntimeSaveGameLoadRequest _request;
        private readonly object _ownerToken;
        private readonly RuntimeSaveStateRestoreProfile _templateProfile;
        private readonly IRuntimeSaveGameCompatibilityChecker _compatibilityChecker;

        public PrepareLoadBridgeOperation(ISaveGameOperation<SaveGameReadResult> provider,
            RuntimeSaveGameLoadRequest request, object ownerToken,
            RuntimeSaveStateRestoreProfile templateProfile,
            IRuntimeSaveGameCompatibilityChecker compatibilityChecker) : base(provider)
        {
            _request = request;
            _ownerToken = ownerToken;
            _templateProfile = templateProfile;
            _compatibilityChecker = compatibilityChecker;
            Initialize();
        }

        protected override RuntimeSaveGameBridgePhase ProviderPhase => RuntimeSaveGameBridgePhase.StoreRead;

        protected override RuntimeSaveGameBridgeResult<PreparedRuntimeSaveGame> TranslateSuccess(SaveGameReadResult read)
        {
            if (read == null || read.Envelope == null || !read.OriginalGeneration.IsPublished ||
                !read.SelectedGeneration.IsPublished || read.SelectedGeneration.CompareTo(read.OriginalGeneration) > 0 ||
                read.RecoverySource == SaveGameRecoverySource.None ||
                !Enum.IsDefined(typeof(SaveGameRecoverySource), read.RecoverySource))
            {
                return ProvenanceFailure("read");
            }

            SaveGameEnvelopeResult decoded = SaveGameEnvelopeCodec.Decode(read.Envelope);
            if (!decoded.IsSuccess || decoded.Envelope == null)
                return RuntimeSaveGameBridgeResult<PreparedRuntimeSaveGame>.Failed(
                    RuntimeSaveGameService.PersistenceFailure(RuntimeSaveGameBridgePhase.EnvelopeValidation, decoded.Error));

            SaveGameEnvelope envelope = decoded.Envelope;
            if (envelope.RecordKind != SaveGameRecordKind.Save ||
                envelope.Profile != _request.Profile || envelope.Slot != _request.Slot ||
                envelope.Generation != read.SelectedGeneration ||
                !string.Equals(envelope.CodecId, SaveGameEnvelope.RuntimeSaveStateJsonCodecId, StringComparison.Ordinal) ||
                _request.Selection.Kind == SaveGameReadSelectionKind.ExactGeneration &&
                    (read.SelectedGeneration != _request.Selection.Generation ||
                     read.OriginalGeneration.CompareTo(read.SelectedGeneration) < 0 ||
                     read.RecoverySource != SaveGameRecoverySource.Primary) ||
                _request.Selection.Kind == SaveGameReadSelectionKind.Current &&
                    (read.RecoverySource == SaveGameRecoverySource.Primary &&
                         read.OriginalGeneration != read.SelectedGeneration ||
                     read.RecoverySource == SaveGameRecoverySource.LastKnownGood &&
                         (_request.RecoveryPolicy != SaveGameRecoveryPolicy.PrimaryThenLastKnownGood ||
                          read.SelectedGeneration.CompareTo(read.OriginalGeneration) >= 0)))
            {
                return ProvenanceFailure("envelope");
            }

            RuntimeSaveGameCompatibilityResult compatibility;
            try { compatibility = _compatibilityChecker.Check(envelope.Build, _templateProfile); }
            catch (Exception exception)
            {
                return RuntimeSaveGameBridgeResult<PreparedRuntimeSaveGame>.Failed(
                    RuntimeSaveGameService.RuntimeFailure(RuntimeSaveGameBridgeErrorCode.CompatibilityFailure,
                        RuntimeSaveGameBridgePhase.Compatibility,
                        new RuntimeSaveStateError(RuntimeSaveStateErrorCode.InvalidDocument, "$.build",
                            "Compatibility checker threw an exception.", envelope.RuntimeSchemaVersion,
                            _templateProfile.Descriptor.SchemaVersion, exception)));
            }
            bool compatibilityShapeValid = Enum.IsDefined(typeof(RuntimeSaveGameCompatibilityStatus), compatibility.Status) &&
                (compatibility.IsCompatible && compatibility.ErrorCode == RuntimeSaveGameCompatibilityErrorCode.None && compatibility.Error == null ||
                 !compatibility.IsCompatible && compatibility.ErrorCode != RuntimeSaveGameCompatibilityErrorCode.None);
            if (!compatibilityShapeValid || !compatibility.IsCompatible)
            {
                return RuntimeSaveGameBridgeResult<PreparedRuntimeSaveGame>.Failed(
                    RuntimeSaveGameService.RuntimeFailure(RuntimeSaveGameBridgeErrorCode.CompatibilityFailure,
                        RuntimeSaveGameBridgePhase.Compatibility,
                        compatibility.Error ?? new RuntimeSaveStateError(RuntimeSaveStateErrorCode.RootHeaderMismatch,
                            "$.build", "Persisted build compatibility was unknown or rejected.",
                            envelope.RuntimeSchemaVersion, _templateProfile.Descriptor.SchemaVersion)));
            }

            byte[] payload = envelope.CopyPayload();
            RuntimeSaveStateResult<RuntimeSaveState> parsed = RuntimeSaveStateJson.LoadFromUtf8(payload,
                SaveGameLimits.MaximumPayloadBytes, 16, envelope.RuntimeSchemaVersion, null);
            if (!parsed.Success || parsed.Value == null)
                return RuntimeSaveGameBridgeResult<PreparedRuntimeSaveGame>.Failed(
                    RuntimeSaveGameService.RuntimeFailure(RuntimeSaveGameBridgeErrorCode.InvalidRuntimePayload,
                        RuntimeSaveGameBridgePhase.RuntimePreparation, parsed.Error));

            RuntimeSaveState state = parsed.Value;
            if (state.SchemaVersion != envelope.RuntimeSchemaVersion || state.Frame != envelope.RuntimeFrame ||
                !string.Equals(state.FrameworkVersion, envelope.Build.FrameworkVersion, StringComparison.Ordinal) ||
                !string.Equals(state.ConfigVersion, envelope.Build.ConfigVersion, StringComparison.Ordinal) ||
                !string.Equals(state.ResourceCatalogVersion, envelope.Build.ResourceCatalogVersion, StringComparison.Ordinal))
            {
                return ProvenanceFailure("payload");
            }

            RuntimeSaveStateRestoreProfile sibling = _templateProfile.CreateSibling();
            RuntimeSaveStateResult<RuntimeSaveStateRestorePlan> prepared = sibling.Prepare(state);
            if (!prepared.Success || prepared.Value == null)
                return RuntimeSaveGameBridgeResult<PreparedRuntimeSaveGame>.Failed(
                    RuntimeSaveGameService.RuntimeFailure(RuntimeSaveGameBridgeErrorCode.RuntimeFailure,
                        RuntimeSaveGameBridgePhase.RuntimePreparation, prepared.Error));

            long completedFrame = prepared.Value.State.Frame;
            if (completedFrame == long.MaxValue)
                return RuntimeSaveGameBridgeResult<PreparedRuntimeSaveGame>.Failed(
                    new RuntimeSaveGameBridgeErrorDetail(RuntimeSaveGameBridgeErrorCode.FrameOverflow,
                        RuntimeSaveGameBridgePhase.RuntimePreparation,
                        RuntimeSaveGameService.ContractFailure(SaveGameErrorCode.CandidateRestoreFailed,
                            SaveGameOperationPhase.Validation, "runtime.frame", "Restored frame cannot advance."),
                        new RuntimeSaveStateError(RuntimeSaveStateErrorCode.InvalidDocument, "$.frame",
                            "The restored completed frame cannot advance to F+1.", state.SchemaVersion,
                            sibling.Descriptor.SchemaVersion), null, RuntimeSaveGameTargetValidationError.None));

            return RuntimeSaveGameBridgeResult<PreparedRuntimeSaveGame>.Succeeded(new PreparedRuntimeSaveGame(
                _ownerToken, prepared.Value, _request.Profile, _request.Slot,
                read.OriginalGeneration, read.SelectedGeneration, read.RecoverySource,
                envelope.CreatedAtUtc,
                completedFrame, completedFrame + 1L));
        }

        private static RuntimeSaveGameBridgeResult<PreparedRuntimeSaveGame> ProvenanceFailure(string path) =>
            RuntimeSaveGameBridgeResult<PreparedRuntimeSaveGame>.Failed(
                new RuntimeSaveGameBridgeErrorDetail(RuntimeSaveGameBridgeErrorCode.InvalidProvenance,
                    RuntimeSaveGameBridgePhase.EnvelopeValidation,
                    new SaveGameError(SaveGameErrorCode.EnvelopePayloadMismatch,
                        SaveGameOperationPhase.Validation, path, "Read/envelope/runtime provenance did not match."),
                    null, null, RuntimeSaveGameTargetValidationError.None));
    }
}
