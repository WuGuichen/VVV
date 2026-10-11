using System;
using System.Threading;
using MxFramework.Platform.Unity;
using UnityEngine;

namespace MxFramework.ApplicationStartup.Unity
{
    /// <summary>Persistent, composition-owned Unity shell for one Application lifetime.</summary>
    [DisallowMultipleComponent]
    public sealed class ApplicationUnityBootRoot : MonoBehaviour
    {
        private static int s_processOwner;

        [SerializeField] private MonoBehaviour _compositionFactoryAdapter;
        [SerializeField] private UnityApplicationLifecycleProxy _lifecycleProxy;
        [SerializeField] private MonoBehaviour _startupSurfaceAdapter;

        private ApplicationUnityBootDriver _driver;
        private bool _ownsProcessGate;
        private bool _initialized;
        private bool _controlledShutdown;
        private bool _irreversibleClaim;
        private bool _factoryInvoked;
        private bool _driving;
        private ApplicationUnityBootErrorCode _initializationError;
        private string _initializationStableErrorCode = string.Empty;

        public bool IsInitialized => _initialized;
        public ApplicationUnityBootErrorCode InitializationError => _initializationError;
        public string InitializationStableErrorCode => _initializationStableErrorCode;
        public ApplicationUnityBootDriver Driver => _driver;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetProcessGate()
        {
            Volatile.Write(ref s_processOwner, 0);
        }

        private void Awake()
        {
            TryInitialize(_compositionFactoryAdapter, _lifecycleProxy, _startupSurfaceAdapter);
        }

        internal bool TryInitialize(
            MonoBehaviour compositionFactoryAdapter,
            UnityApplicationLifecycleProxy lifecycleProxy,
            MonoBehaviour startupSurfaceAdapter)
        {
            if (_initialized || _ownsProcessGate) return false;
            if (!(compositionFactoryAdapter is IApplicationUnityBootCompositionFactory factory) ||
                lifecycleProxy == null || lifecycleProxy.IsBound ||
                (startupSurfaceAdapter != null && !(startupSurfaceAdapter is IApplicationUnityStartupSurface)))
            {
                _initializationError = ApplicationUnityBootErrorCode.InvalidShell;
                return false;
            }

            // The process gate precedes every outer-factory callback and ownership acquisition.
            if (Interlocked.CompareExchange(ref s_processOwner, 1, 0) != 0)
            {
                _initializationError = ApplicationUnityBootErrorCode.OwnershipAlreadyClaimed;
                return false;
            }
            _ownsProcessGate = true;

            ApplicationUnityBootCompositionCreateResult result;
            _factoryInvoked = true;
            try { result = factory.TryCreate(); }
            catch
            {
                _initializationError = ApplicationUnityBootErrorCode.FactoryException;
                return false;
            }
            if (!result.Success)
            {
                _initializationError = ApplicationUnityBootErrorCode.FactoryRejected;
                _initializationStableErrorCode = result.StableErrorCode;
                return false;
            }

            IApplicationUnityStartupSurface surface = startupSurfaceAdapter as IApplicationUnityStartupSurface;
            ApplicationUnityBootErrorCode createError = ApplicationUnityBootDriver.TryCreate(result.Composition, surface, out _driver);
            if (createError != ApplicationUnityBootErrorCode.None)
            {
                _initializationError = createError;
                return false;
            }
            _irreversibleClaim = true;

            try { lifecycleProxy.Bind(result.Composition.LifecycleReporter); }
            catch
            {
                // The frame driver cannot be unclaimed. Retain the process gate so another root
                // cannot represent this contaminated application lifetime as clean.
                _initializationError = ApplicationUnityBootErrorCode.ProxyBindFailed;
                _driver.RecordUnexpectedDestroy();
                return false;
            }

            _compositionFactoryAdapter = compositionFactoryAdapter;
            _lifecycleProxy = lifecycleProxy;
            _startupSurfaceAdapter = startupSurfaceAdapter;
            // EditMode composition tests intentionally initialize the shell without entering the
            // player loop. Persistence is a PlayMode concern and Unity rejects this call in EditMode.
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            _initialized = true;
            _driving = true;
            _initializationError = ApplicationUnityBootErrorCode.None;
            _initializationStableErrorCode = string.Empty;
            _driver.StartInitialRun();
            return true;
        }

        /// <summary>Starts drainable shutdown and stops future lifecycle facts from this root.</summary>
        public void BeginControlledShutdown()
        {
            if (!_initialized || _controlledShutdown) return;
            _controlledShutdown = true;
            _lifecycleProxy.Unbind();
            _driver.RequestShutdown();
        }

        private void Update()
        {
            if (!_initialized || !_driving) return;
            ApplicationUnityDriverUpdateStatus status = _driver.Update(Time.unscaledDeltaTime);
            if (_controlledShutdown &&
                (status == ApplicationUnityDriverUpdateStatus.ShutdownComplete ||
                 status == ApplicationUnityDriverUpdateStatus.ShutdownDebt))
            {
                // Both terminal shutdown outcomes make the old shell permanently inert. Debt keeps
                // the process gate claimed, while clean completion may transfer ownership.
                _driving = false;
                if (status == ApplicationUnityDriverUpdateStatus.ShutdownComplete)
                {
                    ReleaseCompletedGate();
                }
            }
        }

        private void FixedUpdate()
        {
            if (_initialized && _driving) _driver.FixedUpdate(Time.fixedUnscaledDeltaTime);
        }

        private void OnApplicationQuit()
        {
            // Non-blocking best effort. The proxy independently reports the host quitting fact.
            if (_initialized && _driving) _driver.RequestShutdown();
        }

        private void OnDestroy()
        {
            if (!_ownsProcessGate) return;
            if (_lifecycleProxy != null) _lifecycleProxy.Unbind();
            if (_initialized && _driver.RootState == ApplicationUnityBootRootState.ShutdownComplete)
            {
                ReleaseCompletedGate();
                return;
            }
            if (_driver != null) _driver.RecordUnexpectedDestroy();
            // Fail closed: an unexpected destroy or a poisoned irreversible claim retains the gate
            // until the next SubsystemRegistration reset.
            if (!_factoryInvoked && !_irreversibleClaim && !_initialized) ReleaseCleanGate();
        }

        private void ReleaseCleanGate()
        {
            if (!_ownsProcessGate || _factoryInvoked || _irreversibleClaim) return;
            _ownsProcessGate = false;
            Interlocked.CompareExchange(ref s_processOwner, 0, 1);
        }

        private void ReleaseCompletedGate()
        {
            if (!_ownsProcessGate) return;
            _ownsProcessGate = false;
            Interlocked.CompareExchange(ref s_processOwner, 0, 1);
        }

        internal static void ResetProcessGateForTests() => ResetProcessGate();
    }
}
