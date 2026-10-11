using System;
using System.Threading;
using MxFramework.ApplicationStartup.SessionBridge;
using MxFramework.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_INCLUDE_TESTS
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("MxFramework.Tests.PlayMode.Application")]
#endif

namespace MxFramework.ApplicationStartup.SessionBridge.Unity
{
    /// <summary>Creates a fresh Unity-backed candidate scene graph.</summary>
    /// <remarks>AllocByDesign. Construct and invoke on the Unity main thread.</remarks>
    public sealed class ApplicationCandidateUnitySceneGraphFactory
    {
        private readonly int _mainThreadId;
        private readonly SynchronizationContext _mainThreadContext;

        public ApplicationCandidateUnitySceneGraphFactory()
        {
            _mainThreadContext = SynchronizationContext.Current;
            if (_mainThreadContext == null ||
                !string.Equals(_mainThreadContext.GetType().FullName, "UnityEngine.UnitySynchronizationContext", StringComparison.Ordinal))
                throw new InvalidOperationException("Factory construction requires the Unity main-thread synchronization context.");
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public ApplicationCandidateSceneGraph Create(
            ApplicationCandidateUnitySceneManifest manifest,
            IApplicationCandidateUnitySceneArtifactProvider artifactProvider,
            ApplicationControllerPumpStrategy pumpStrategy,
            string runtimeModuleId = "candidate-unity-scene-flow")
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (artifactProvider == null) throw new ArgumentNullException(nameof(artifactProvider));
            if (pumpStrategy != ApplicationControllerPumpStrategy.RuntimeModule &&
                pumpStrategy != ApplicationControllerPumpStrategy.Standalone)
                throw new ArgumentOutOfRangeException(nameof(pumpStrategy));
            EnsureMainThread();

            var adapter = new CandidateUnitySceneAdapter(manifest, artifactProvider, _mainThreadId, _mainThreadContext);
            var driver = new CandidateUnityOwnedSceneDriverProxy(adapter);
            var controller = new SceneFlowController(driver);
            SceneFlowRuntimeModule runtimeModule = pumpStrategy == ApplicationControllerPumpStrategy.RuntimeModule
                ? new SceneFlowRuntimeModule(controller, runtimeModuleId)
                : null;
            return new ApplicationCandidateSceneGraph(controller, runtimeModule, pumpStrategy, adapter);
        }

        private void EnsureMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId ||
                !ReferenceEquals(SynchronizationContext.Current, _mainThreadContext))
                throw new InvalidOperationException("Candidate Unity scene graphs must be created on their factory's Unity main thread.");
        }
    }

    /// <summary>
    /// Keeps SceneFlow driver ownership distinct from the candidate isolation-gate role while both
    /// expose the exact same immutable scene ownership identity.
    /// </summary>
    internal sealed class CandidateUnityOwnedSceneDriverProxy : IApplicationCandidateOwnedSceneDriver
    {
        private readonly CandidateUnitySceneAdapter _adapter;

        internal CandidateUnityOwnedSceneDriverProxy(CandidateUnitySceneAdapter adapter) =>
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));

        public ApplicationCandidateSceneOwnershipIdentity SceneOwnershipIdentity =>
            _adapter.SceneOwnershipIdentity;

        public ISceneFlowOperation LoadScene(SceneFlowRequest request) => _adapter.LoadScene(request);

        public ISceneFlowOperation UnloadScene(string sceneKey) => _adapter.UnloadScene(sceneKey);
    }

    internal sealed class CandidateUnitySceneAdapter : IApplicationCandidateSceneIsolationGate
    {
        private static object s_processMutationOwner;

        private readonly ApplicationCandidateUnitySceneManifest _manifest;
        private readonly IApplicationCandidateUnitySceneArtifactProvider _artifactProvider;
        private readonly int _mainThreadId;
        private readonly SynchronizationContext _mainThreadContext;
        private readonly object _mutationToken = new object();
        private readonly ApplicationCandidateSceneOwnershipIdentity _sceneOwnershipIdentity =
            new ApplicationCandidateSceneOwnershipIdentity();
        private IApplicationCandidateUnitySceneArtifactLease _artifactLease;
        private ApplicationCandidateUnitySceneApprovalProof _approvalProof;
        private bool _artifactReleaseAttempted;
        private Exception _artifactReleaseError;
        private Scene _ownedScene;
        private bool _hasOwnedScene;
        private bool _sceneOwnershipUncertain;
        private bool _artifactReleaseDebt;
        private bool _isIsolated;
        private bool _isInert = true;
        private bool _ownsProcessMutation;
        private bool _permanentMutationDebt;

        internal CandidateUnitySceneAdapter(
            ApplicationCandidateUnitySceneManifest manifest,
            IApplicationCandidateUnitySceneArtifactProvider artifactProvider,
            int mainThreadId,
            SynchronizationContext mainThreadContext)
        {
            _manifest = manifest;
            _artifactProvider = artifactProvider;
            _mainThreadId = mainThreadId;
            _mainThreadContext = mainThreadContext;
        }

        public bool IsIsolated => _isIsolated;
        public bool IsInert => _isInert;
        public ApplicationCandidateSceneOwnershipIdentity SceneOwnershipIdentity => _sceneOwnershipIdentity;

        /// <remarks>NoAlloc and fail-closed.</remarks>
        public bool HasOwnedScene => _hasOwnedScene || _sceneOwnershipUncertain || _artifactReleaseDebt;

        public ApplicationCandidateSceneIsolationResult TryEnterIsolation(
            in ApplicationCandidateSceneRequest request)
        {
            if (!IsMainThread())
                return IsolationFailed("MXCUSC_THREAD", "Candidate scene isolation must run on the Unity main thread.");
            if (_isIsolated)
                return IsolationFailed("MXCUSC_REENTRY", "Candidate scene isolation was already entered.");
            if (!Matches(request))
                return ApplicationCandidateSceneIsolationResult.Failed(
                    ApplicationCandidateSceneIsolationErrorCode.UnsupportedCandidateScene,
                    "Candidate request does not exactly match the approved Unity scene manifest.");

            _isIsolated = true;
            _isInert = true;
            return ApplicationCandidateSceneIsolationResult.Succeeded;
        }

        public ApplicationCandidateSceneIsolationResult MakeInert()
        {
            if (!IsMainThread())
                return IsolationFailed("MXCUSC_THREAD", "Candidate scene cleanup must run on the Unity main thread.", true);

            if (!_hasOwnedScene)
            {
                if (_sceneOwnershipUncertain)
                {
                    _isInert = false;
                    return ApplicationCandidateSceneIsolationResult.Failed(
                        ApplicationCandidateSceneIsolationErrorCode.MakeInertFailed,
                        "Exact candidate Scene ownership is uncertain; inertness cannot be proven.");
                }
                _isInert = true;
                return ApplicationCandidateSceneIsolationResult.Succeeded;
            }

            try
            {
                GameObject[] roots = _ownedScene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                    if (roots[i].activeSelf) roots[i].SetActive(false);
                if (_sceneOwnershipUncertain)
                {
                    _isInert = false;
                    return ApplicationCandidateSceneIsolationResult.Failed(
                        ApplicationCandidateSceneIsolationErrorCode.MakeInertFailed,
                        "A known candidate Scene was made inert, but additional exact Scene ownership remains uncertain.");
                }
                _isInert = true;
                return ApplicationCandidateSceneIsolationResult.Succeeded;
            }
            catch (Exception exception)
            {
                _isInert = false;
                return ApplicationCandidateSceneIsolationResult.Failed(
                    ApplicationCandidateSceneIsolationErrorCode.MakeInertFailed,
                    "Failed to make the exact candidate Unity scene inert.",
                    exception);
            }
        }

        public ISceneFlowOperation LoadScene(SceneFlowRequest request)
        {
            if (!IsMainThread()) return Failed(request.SceneKey, "Candidate Unity scene load must run on the Unity main thread.");
            if (!_isIsolated || !Matches(request)) return Failed(request.SceneKey, "Candidate Unity scene request was not isolated and approved.");
            if (_artifactLease != null || _hasOwnedScene || _sceneOwnershipUncertain || _artifactReleaseDebt)
                return Failed(request.SceneKey, "Candidate Unity scene adapter is not fresh.");
            if (!TryClaimProcessMutation())
                return Failed(request.SceneKey, "Another candidate Unity scene mutation owns the process-wide gate.");

            ulong[] baselineHandles;
            try
            {
                baselineHandles = CaptureBaselineAndPreflight();
            }
            catch (Exception exception)
            {
                ReleaseProcessMutation();
                return Failed(request.SceneKey, "Candidate Unity scene preflight failed.", exception);
            }

            ApplicationCandidateUnityScenePreflightResult preflight;
            try
            {
                preflight = _artifactProvider.Preflight(_manifest);
            }
            catch (Exception exception)
            {
                ReleaseProcessMutation();
                return Failed(request.SceneKey, "Candidate Unity scene artifact provider threw during approval preflight.", exception);
            }
            if (!preflight.Success || !Matches(preflight.Proof))
            {
                ReleaseProcessMutation();
                return Failed(
                    request.SceneKey,
                    !preflight.Success
                        ? "Candidate Unity scene approval preflight failed: " + preflight.ErrorCode + ". " + preflight.Message
                        : "Candidate Unity scene approval proof does not exactly match the immutable manifest.",
                    preflight.Exception);
            }
            _approvalProof = preflight.Proof;

            ApplicationCandidateUnitySceneArtifactAcquireResult acquire;
            try
            {
                acquire = _artifactProvider.Acquire(_manifest, _approvalProof);
            }
            catch (Exception exception)
            {
                _approvalProof = null;
                ReleaseProcessMutation();
                return Failed(request.SceneKey, "Candidate Unity scene artifact provider threw during acquire.", exception);
            }

            IApplicationCandidateUnitySceneArtifactLease acquiredLease = acquire.Lease;
            if (acquiredLease != null) _artifactLease = acquiredLease;
            bool leaseMatches = false;
            Exception validationException = null;
            if (acquire.Success && acquiredLease != null)
            {
                try { leaseMatches = Matches(acquiredLease, _approvalProof); }
                catch (Exception exception) { validationException = exception; }
            }
            if (!acquire.Success || !leaseMatches)
            {
                Exception releaseException = acquiredLease != null ? ReleaseArtifact() : null;
                if (acquiredLease == null || releaseException == null) ReleaseProcessMutationIfSafe();
                return Failed(
                    request.SceneKey,
                    !acquire.Success
                        ? "Candidate Unity scene artifact acquire failed: " + acquire.ErrorCode + ". " + acquire.Message
                        : "Candidate Unity scene artifact lease does not exactly match the immutable manifest.",
                    Combine(validationException ?? acquire.Exception, releaseException));
            }

            var operation = new CandidateUnityLoadOperation(this, baselineHandles);
            operation.Subscribe();
            try
            {
                AsyncOperation unityOperation = SceneManager.LoadSceneAsync(_manifest.ExactScenePath, LoadSceneMode.Additive);
                if (unityOperation == null)
                {
                    operation.FailBeforeStart("Unity SceneManager.LoadSceneAsync returned null.", null);
                    return operation;
                }
                operation.Attach(unityOperation);
                return operation;
            }
            catch (Exception exception)
            {
                operation.FailBeforeStart("Unity SceneManager.LoadSceneAsync threw.", exception);
                return operation;
            }
        }

        public ISceneFlowOperation UnloadScene(string sceneKey)
        {
            if (!IsMainThread()) return Failed(sceneKey, "Candidate Unity scene unload must run on the Unity main thread.");
            if (!string.Equals(sceneKey, _manifest.SceneKey, StringComparison.Ordinal))
                return Failed(sceneKey, "Unload scene key does not match the exact candidate lease.");
            if (!_hasOwnedScene)
            {
                if (_sceneOwnershipUncertain)
                    return Failed(sceneKey, "MXCUSC_8: Exact candidate Scene ownership is uncertain; unload cannot substitute a path/name lookup.");
                Exception releaseException = ReleaseArtifact();
                if (releaseException == null) ReleaseProcessMutationIfSafe();
                return releaseException == null
                    ? CandidateUnityImmediateOperation.Succeeded(sceneKey)
                    : Failed(sceneKey, "Candidate scene artifact release failed.", releaseException);
            }

            bool retainedDebtToken = _ownsProcessMutation;
            var operation = new CandidateUnityUnloadOperation(this, _ownedScene, !retainedDebtToken);
            operation.TryStart(retainedDebtToken);
            return operation;
        }

        internal string SceneKey => _manifest.SceneKey;
        internal string ExactScenePath => _manifest.ExactScenePath;

        internal bool TryCaptureLoadedScene(Scene scene, ulong[] baselineHandles, ref bool ambiguous)
        {
            if (!IsMainThread()) return false;
            if (!string.Equals(scene.path, _manifest.ExactScenePath, StringComparison.Ordinal) ||
                Contains(baselineHandles, scene.handle.GetRawData())) return false;
            if (_hasOwnedScene && _ownedScene.handle.GetRawData() != scene.handle.GetRawData())
            {
                ambiguous = true;
                _sceneOwnershipUncertain = true;
                return false;
            }
            _ownedScene = scene;
            _hasOwnedScene = true;
            return true;
        }

        internal SceneFlowError FinalizeLoadedScene(bool callbackObserved, bool ambiguous)
        {
            if (!IsMainThread()) return Error("Candidate scene load finalization was attempted off the Unity main thread.");
            if (!_hasOwnedScene)
            {
                _sceneOwnershipUncertain = true;
                _permanentMutationDebt = true;
                return Error("Unity completed candidate scene load without yielding the exact new Scene handle through sceneLoaded.");
            }
            if (!callbackObserved || ambiguous)
            {
                _sceneOwnershipUncertain = true;
                _permanentMutationDebt = true;
                return Error("Candidate scene handle capture was missing or ambiguous.");
            }

            try
            {
                GameObject[] roots = _ownedScene.GetRootGameObjects();
                if (roots.Length != _manifest.ExpectedRootCount)
                    return CompleteKnownLoad(Error("Candidate scene root count does not match the immutable manifest."));
                for (int i = 0; i < roots.Length; i++)
                {
                    GameObject root = roots[i];
                    if (root.activeSelf) return CompleteKnownLoad(Error("Every candidate scene root must be serialized inactive."));
                    ApplicationCandidateUnitySceneMarker[] markers = root.GetComponents<ApplicationCandidateUnitySceneMarker>();
                    if (markers.Length != 1 ||
                        markers[0] == null ||
                        !string.Equals(markers[0].MarkerId, _manifest.MarkerId, StringComparison.Ordinal) ||
                        markers[0].Revision != _manifest.Revision)
                        return CompleteKnownLoad(Error("Every candidate scene root must carry the exact approved marker and revision."));
                }
                _isInert = true;
                _sceneOwnershipUncertain = false;
                return CompleteKnownLoad(SceneFlowError.None);
            }
            catch (Exception exception)
            {
                return CompleteKnownLoad(Error("Candidate scene root/marker validation threw.", exception));
            }
        }

        internal SceneFlowError FinalizeUnload(Scene exactScene, bool callbackObserved)
        {
            if (!IsMainThread()) return Error("Candidate scene unload finalization was attempted off the Unity main thread.");
            if (_hasOwnedScene && _ownedScene.handle.GetRawData() != exactScene.handle.GetRawData())
            {
                _permanentMutationDebt = true;
                return Error("Unload terminal did not match the exact candidate Scene handle.");
            }
            if (exactScene.IsValid() && exactScene.isLoaded)
            {
                SceneFlowError loadedError = Error(callbackObserved
                    ? "Unity emitted sceneUnloaded but the exact candidate Scene remains loaded."
                    : "Unity unload operation completed before the exact candidate Scene reached an unloaded terminal state.");
                ReleaseProcessMutationIfSafe();
                return loadedError;
            }

            _ownedScene = default;
            _hasOwnedScene = false;
            _isInert = true;
            if (_sceneOwnershipUncertain)
                return Error("The captured candidate Scene unloaded, but ambiguous additional Scene ownership remains.");
            Exception releaseException = ReleaseArtifact();
            if (releaseException != null)
                return Error("Candidate scene artifact release failed after exact unload terminal.", releaseException);
            ReleaseProcessMutationIfSafe();
            return SceneFlowError.None;
        }

        private SceneFlowError CompleteKnownLoad(SceneFlowError result)
        {
            ReleaseProcessMutationIfSafe();
            return result;
        }

        private ulong[] CaptureBaselineAndPreflight()
        {
            int count = SceneManager.sceneCount;
            var handles = new ulong[count];
            for (int i = 0; i < count; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                handles[i] = scene.handle.GetRawData();
                if (string.Equals(scene.path, _manifest.ExactScenePath, StringComparison.Ordinal))
                    throw new InvalidOperationException("The exact candidate scene path is already loaded.");
            }
            return handles;
        }

        private bool Matches(in ApplicationCandidateSceneRequest request) =>
            string.Equals(request.SceneKey, _manifest.SceneKey, StringComparison.Ordinal) &&
            request.LoadMode == SceneFlowLoadMode.Additive && !request.UnloadPreviousScene &&
            _manifest.IsolationPolicy == ApplicationCandidateUnitySceneIsolationPolicy.SerializedInactiveRootsWithExactMarker;

        private bool Matches(SceneFlowRequest request) =>
            string.Equals(request.SceneKey, _manifest.SceneKey, StringComparison.Ordinal) &&
            request.LoadMode == SceneFlowLoadMode.Additive && !request.UnloadPreviousScene;

        private bool Matches(ApplicationCandidateUnitySceneApprovalProof proof) =>
            proof != null &&
            string.Equals(proof.ArtifactId, _manifest.ArtifactId, StringComparison.Ordinal) &&
            string.Equals(proof.ExactScenePath, _manifest.ExactScenePath, StringComparison.Ordinal) &&
            string.Equals(proof.ContentHash, _manifest.ExpectedContentHash, StringComparison.Ordinal) &&
            proof.Revision == _manifest.Revision && proof.IsolationPolicy == _manifest.IsolationPolicy;

        private bool Matches(
            IApplicationCandidateUnitySceneArtifactLease lease,
            ApplicationCandidateUnitySceneApprovalProof proof) =>
            lease != null && ReferenceEquals(lease.ApprovalProof, proof) &&
            string.Equals(lease.ArtifactId, _manifest.ArtifactId, StringComparison.Ordinal) &&
            string.Equals(lease.ExactScenePath, _manifest.ExactScenePath, StringComparison.Ordinal) &&
            lease.Revision == _manifest.Revision;

        internal bool IsMainThread() => Thread.CurrentThread.ManagedThreadId == _mainThreadId &&
            ReferenceEquals(SynchronizationContext.Current, _mainThreadContext);

        private bool TryClaimProcessMutation()
        {
            if (Interlocked.CompareExchange(ref s_processMutationOwner, _mutationToken, null) != null) return false;
            _ownsProcessMutation = true;
            return true;
        }

        private void ReleaseProcessMutation()
        {
            if (!_ownsProcessMutation) return;
            Interlocked.CompareExchange(ref s_processMutationOwner, null, _mutationToken);
            _ownsProcessMutation = false;
        }

        private Exception ReleaseArtifact()
        {
            IApplicationCandidateUnitySceneArtifactLease lease = _artifactLease;
            if (_artifactReleaseAttempted) return _artifactReleaseError;
            if (lease != null)
            {
                _artifactReleaseAttempted = true;
                try { _artifactProvider.Release(lease); }
                catch (Exception caught)
                {
                    _artifactReleaseError = caught;
                    _artifactReleaseDebt = true;
                    _permanentMutationDebt = true;
                    return caught;
                }
            }
            _artifactLease = null;
            _approvalProof = null;
            _artifactReleaseDebt = false;
            return null;
        }

        private void ReleaseProcessMutationIfSafe()
        {
            if (!_permanentMutationDebt) ReleaseProcessMutation();
        }

#if UNITY_INCLUDE_TESTS
        internal void ReleasePermanentMutationTokenForTests()
        {
            if (!IsMainThread())
                throw new InvalidOperationException(
                    "Test cleanup for a candidate Unity scene mutation token requires the owning Unity main thread.");
            if (!_permanentMutationDebt || !_ownsProcessMutation) return;
            ReleaseProcessMutation();
        }
#endif

        internal void ReleaseUnloadStartMutationIfSafe()
        {
            ReleaseProcessMutationIfSafe();
        }

        private static Exception Combine(Exception primary, Exception cleanup)
        {
            if (primary == null) return cleanup;
            if (cleanup == null || ReferenceEquals(primary, cleanup)) return primary;
            return new AggregateException(primary, cleanup);
        }

        private static bool Contains(ulong[] values, ulong value)
        {
            for (int i = 0; i < values.Length; i++) if (values[i] == value) return true;
            return false;
        }

        private SceneFlowError Error(string message, Exception exception = null) =>
            new SceneFlowError(SceneFlowErrorCode.DriverFailure, _manifest.SceneKey, message, exception);

        private static ISceneFlowOperation Failed(string sceneKey, string message, Exception exception = null) =>
            CandidateUnityImmediateOperation.Failed(sceneKey, new SceneFlowError(
                SceneFlowErrorCode.DriverFailure, sceneKey, message, exception));

        private static ApplicationCandidateSceneIsolationResult IsolationFailed(
            string code,
            string message,
            bool cleanup = false) =>
            ApplicationCandidateSceneIsolationResult.Failed(
                cleanup ? ApplicationCandidateSceneIsolationErrorCode.MakeInertFailed : ApplicationCandidateSceneIsolationErrorCode.IsolationFailed,
                code + ": " + message);

        private sealed class CandidateUnityLoadOperation : ISceneFlowOperation
        {
            private readonly CandidateUnitySceneAdapter _owner;
            private readonly ulong[] _baselineHandles;
            private AsyncOperation _operation;
            private SceneFlowError _error;
            private bool _subscribed;
            private bool _callbackObserved;
            private bool _ambiguous;
            private bool _finalized;
            private SceneFlowError _threadError;

            internal CandidateUnityLoadOperation(CandidateUnitySceneAdapter owner, ulong[] baselineHandles)
            {
                _owner = owner;
                _baselineHandles = baselineHandles;
            }

            public string SceneKey => _owner.SceneKey;
            public bool IsDone
            {
                get
                {
                    if (!EnsureMainThread()) return false;
                    FinalizeIfDone();
                    return _finalized;
                }
            }

            public float Progress
            {
                get
                {
                    if (!EnsureMainThread()) return 0f;
                    return _finalized || _operation == null ? (_finalized ? 1f : 0f) : Mathf.Clamp01(_operation.progress);
                }
            }

            public bool Success
            {
                get
                {
                    if (!EnsureMainThread()) return false;
                    FinalizeIfDone();
                    return _finalized && _error.IsNone;
                }
            }

            public SceneFlowError Error
            {
                get
                {
                    if (!EnsureMainThread()) return _threadError;
                    FinalizeIfDone();
                    return _error;
                }
            }

            internal void Subscribe()
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                _subscribed = true;
            }

            internal void Attach(AsyncOperation operation) => _operation = operation;

            internal void FailBeforeStart(string message, Exception exception)
            {
                Unsubscribe();
                Exception releaseException = _owner.ReleaseArtifact();
                if (releaseException == null) _owner.ReleaseProcessMutationIfSafe();
                _error = _owner.Error(
                    releaseException == null ? message : message + " Artifact release also failed.",
                    Combine(exception, releaseException));
                _finalized = true;
            }

            private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (!EnsureMainThread()) return;
                if (mode != LoadSceneMode.Additive) return;
                if (_owner.TryCaptureLoadedScene(scene, _baselineHandles, ref _ambiguous)) _callbackObserved = true;
            }

            private void FinalizeIfDone()
            {
                if (_finalized || _operation == null || !_operation.isDone) return;
                Unsubscribe();
                _error = _owner.FinalizeLoadedScene(_callbackObserved, _ambiguous);
                if (!_threadError.IsNone && _error.IsNone) _error = _threadError;
                _finalized = true;
            }

            private bool EnsureMainThread()
            {
                if (_owner.IsMainThread()) return true;
                if (_threadError.IsNone)
                    _threadError = _owner.Error("Candidate Unity load operation was observed off the Unity main thread.");
                if (_finalized && _error.IsNone) _error = _threadError;
                return false;
            }

            private void Unsubscribe()
            {
                if (!_subscribed) return;
                SceneManager.sceneLoaded -= OnSceneLoaded;
                _subscribed = false;
            }
        }

        private sealed class CandidateUnityUnloadOperation : ISceneFlowOperation
        {
            private readonly CandidateUnitySceneAdapter _owner;
            private readonly Scene _exactScene;
            private readonly bool _releaseMutationOnSafeStartFailure;
            private AsyncOperation _operation;
            private SceneFlowError _error;
            private bool _subscribed;
            private bool _callbackObserved;
            private bool _finalized;
            private SceneFlowError _threadError;
            private bool _started;
            private bool _hasMutationToken;

            internal CandidateUnityUnloadOperation(
                CandidateUnitySceneAdapter owner,
                Scene exactScene,
                bool releaseMutationOnSafeStartFailure)
            {
                _owner = owner;
                _exactScene = exactScene;
                _releaseMutationOnSafeStartFailure = releaseMutationOnSafeStartFailure;
            }

            public string SceneKey => _owner.SceneKey;
            public bool IsDone
            {
                get
                {
                    if (!EnsureMainThread()) return false;
                    TryStart(false);
                    FinalizeIfDone();
                    return _finalized;
                }
            }

            public float Progress
            {
                get
                {
                    if (!EnsureMainThread()) return 0f;
                    TryStart(false);
                    return _finalized || _operation == null ? (_finalized ? 1f : 0f) : Mathf.Clamp01(_operation.progress);
                }
            }

            public bool Success
            {
                get
                {
                    if (!EnsureMainThread()) return false;
                    TryStart(false);
                    FinalizeIfDone();
                    return _finalized && _error.IsNone;
                }
            }

            public SceneFlowError Error
            {
                get
                {
                    if (!EnsureMainThread()) return _threadError;
                    TryStart(false);
                    FinalizeIfDone();
                    return _error;
                }
            }

            private void Subscribe()
            {
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                _subscribed = true;
            }

            internal void TryStart(bool alreadyOwnsMutationToken)
            {
                if (_started || _finalized || !_owner.IsMainThread()) return;
                if (!_hasMutationToken)
                {
                    if (alreadyOwnsMutationToken)
                    {
                        _hasMutationToken = true;
                    }
                    else
                    {
                        if (!_owner.TryClaimProcessMutation()) return;
                        _hasMutationToken = true;
                    }
                }

                _started = true;
                Subscribe();
                try
                {
                    AsyncOperation unityOperation = SceneManager.UnloadSceneAsync(_exactScene);
                    if (unityOperation == null)
                    {
                        CompleteNullStart();
                        return;
                    }
                    _operation = unityOperation;
                }
                catch (Exception exception)
                {
                    FailStart("Unity SceneManager.UnloadSceneAsync threw for the exact candidate Scene.", exception);
                }
            }

            private void CompleteNullStart()
            {
                Unsubscribe();
                _error = _owner.FinalizeUnload(_exactScene, false);
                _finalized = true;
            }

            private void FailStart(string message, Exception exception)
            {
                Unsubscribe();
                if (_releaseMutationOnSafeStartFailure) _owner.ReleaseUnloadStartMutationIfSafe();
                _error = _owner.Error(message, exception);
                _finalized = true;
            }

            private void OnSceneUnloaded(Scene scene)
            {
                if (!EnsureMainThread()) return;
                if (scene.handle.GetRawData() == _exactScene.handle.GetRawData()) _callbackObserved = true;
            }

            private void FinalizeIfDone()
            {
                if (_finalized || _operation == null || !_operation.isDone) return;
                Unsubscribe();
                _error = _owner.FinalizeUnload(_exactScene, _callbackObserved);
                if (!_threadError.IsNone && _error.IsNone) _error = _threadError;
                _finalized = true;
            }

            private bool EnsureMainThread()
            {
                if (_owner.IsMainThread()) return true;
                if (_threadError.IsNone)
                    _threadError = _owner.Error("Candidate Unity unload operation was observed off the Unity main thread.");
                if (_finalized && _error.IsNone) _error = _threadError;
                return false;
            }

            private void Unsubscribe()
            {
                if (!_subscribed) return;
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
                _subscribed = false;
            }
        }

        private sealed class CandidateUnityImmediateOperation : ISceneFlowOperation
        {
            private CandidateUnityImmediateOperation(string sceneKey, SceneFlowError error)
            {
                SceneKey = sceneKey ?? string.Empty;
                Error = error;
            }

            public string SceneKey { get; }
            public bool IsDone => true;
            public float Progress => 1f;
            public bool Success => Error.IsNone;
            public SceneFlowError Error { get; }

            internal static ISceneFlowOperation Succeeded(string sceneKey) =>
                new CandidateUnityImmediateOperation(sceneKey, SceneFlowError.None);

            internal static ISceneFlowOperation Failed(string sceneKey, SceneFlowError error) =>
                new CandidateUnityImmediateOperation(sceneKey, error);
        }
    }
}
