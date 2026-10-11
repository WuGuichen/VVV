using System.Collections.Generic;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Camera.Unity
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityCamera))]
    [AddComponentMenu("MxFramework/Camera/Camera Unity Rig")]
    public sealed class MxCameraUnityRig : MonoBehaviour, IMxCameraBackend
    {
        [SerializeField] private UnityCamera _camera;
        [SerializeField] private string _rigId = "main";
        [SerializeField] private MxCameraUnityProfileDefinition _inlineProfile = new MxCameraUnityProfileDefinition();
        [SerializeField] private MonoBehaviour _profileProviderBehaviour;
        [SerializeField] private bool _applyInLateUpdate;
        [SerializeField] private MxCameraUnityShakeMode _shakeMode = MxCameraUnityShakeMode.LegacyCoreOffset;
        [SerializeField] private MxCameraUnityShakeSettings _shakeSettings = new MxCameraUnityShakeSettings();
        [SerializeField] private MonoBehaviour _shakePresenterBehaviour;
        [SerializeField] private MxCameraUnityObstructionMode _obstructionMode;
        [SerializeField] private MxCameraUnityObstructionSettings _obstructionSettings = new MxCameraUnityObstructionSettings();
        [SerializeField] private MonoBehaviour _obstructionResolverBehaviour;

        private readonly Queue<MxCameraDiagnostic> _backendDiagnostics = new Queue<MxCameraDiagnostic>();
        private IMxCameraProfileProvider _profileProvider;
        private readonly MxCameraUnityProceduralShakePresenter _defaultShakePresenter =
            new MxCameraUnityProceduralShakePresenter();
        private IMxCameraUnityShakePresenter _shakePresenter;
        private IMxCameraUnityObstructionResolver _obstructionResolver;
        private MxCameraService _service;
        private MxCameraState _pendingState;
        private bool _hasPendingState;
        private int _lastAppliedUnityFrame = -1;
        private MxCameraResult _lastApplyResult = MxCameraResult.Ok();
        private MxCameraEvaluationResult _lastEvaluationResult;
        private MxCameraEvaluationFrame _lastNoAllocEvaluation;
        private MxCameraStateValue _lastNoAllocState = MxCameraStateValue.Empty;
        private bool _hasLastNoAllocState;
        private MxCameraUnityShakeResult _lastShakeResult;
        private MxCameraUnityObstructionResult _lastObstructionResult;

        public MxCameraRigId RigId => new MxCameraRigId(string.IsNullOrWhiteSpace(_rigId) ? name : _rigId);
        public MxCameraEvaluationResult LastEvaluationResult => _lastEvaluationResult;
        public MxCameraEvaluationFrame LastNoAllocEvaluation => _lastNoAllocEvaluation;
        public bool HasLastNoAllocEvaluation => _hasLastNoAllocState;
        public MxCameraResult LastApplyResult => _lastApplyResult;
        public MxCameraUnityShakeMode ShakeMode => _shakeMode;
        public MxCameraUnityShakeResult LastShakeResult => _lastShakeResult;
        public MxCameraUnityObstructionMode ObstructionMode => _obstructionMode;
        public MxCameraUnityObstructionResult LastObstructionResult => _lastObstructionResult;
        public bool HasAppliedThisUnityFrame => _lastAppliedUnityFrame == Time.frameCount;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void LateUpdate()
        {
            if (_applyInLateUpdate && _hasPendingState)
                ApplyLate(_pendingState, Time.deltaTime);
        }

        public MxCameraResult Initialize(IMxCameraProfileProvider profiles)
        {
            EnsureCamera();
            _profileProvider = profiles ?? ResolveProfileProvider();
            _service = _service ?? new MxCameraService(RigId);
            _shakePresenter = _shakePresenter ?? ResolveShakePresenter();
            _obstructionResolver = _obstructionResolver ?? ResolveObstructionResolver();
            if (_camera == null)
                return AddBackendDiagnostic(MxCameraDiagnosticCodes.BackendMissingCamera, "Unity Camera is missing.");

            if (_profileProvider == null)
                return AddBackendDiagnostic(MxCameraDiagnosticCodes.BackendMissingProfileProvider, "Camera profile provider is missing.");

            return MxCameraResult.Ok();
        }

        public MxCameraEvaluationResult Evaluate(
            long frame,
            IEnumerable<MxCameraTargetSnapshot> targets,
            IEnumerable<MxCameraRequest> requests = null,
            MxCameraTargetGroup targetGroup = null)
        {
            return Evaluate(frame, CurrentUnityDeltaTime(), targets, requests, targetGroup);
        }

        public MxCameraEvaluationResult Evaluate(
            long frame,
            float deltaTime,
            IEnumerable<MxCameraTargetSnapshot> targets,
            IEnumerable<MxCameraRequest> requests = null,
            MxCameraTargetGroup targetGroup = null)
        {
            EnsureInitialized();
            IReadOnlyList<MxCameraProfileDefinition> profiles = _profileProvider != null
                ? _profileProvider.Profiles
                : new[] { (_inlineProfile ?? new MxCameraUnityProfileDefinition()).ToRuntimeDefinition() };
            return Evaluate(frame, deltaTime, profiles, targets, requests, targetGroup);
        }

        public MxCameraEvaluationResult Evaluate(
            long frame,
            IEnumerable<MxCameraProfileDefinition> profiles,
            IEnumerable<MxCameraTargetSnapshot> targets,
            IEnumerable<MxCameraRequest> requests = null,
            MxCameraTargetGroup targetGroup = null)
        {
            return Evaluate(frame, CurrentUnityDeltaTime(), profiles, targets, requests, targetGroup);
        }

        public MxCameraEvaluationResult Evaluate(
            long frame,
            float deltaTime,
            IEnumerable<MxCameraProfileDefinition> profiles,
            IEnumerable<MxCameraTargetSnapshot> targets,
            IEnumerable<MxCameraRequest> requests = null,
            MxCameraTargetGroup targetGroup = null)
        {
            EnsureInitialized();
            float safeDeltaTime = NormalizeDeltaTime(deltaTime);
            float height = _camera != null ? _camera.pixelHeight : Screen.height;
            float width = _camera != null ? _camera.pixelWidth : Screen.width;
            MxCameraState previousState =
                _lastEvaluationResult != null
                    ? _lastEvaluationResult.State
                    : _hasLastNoAllocState
                        ? _lastNoAllocState.ToReference()
                        : MxCameraState.Empty;
            MxCameraEvaluationContext context = CreateEvaluationContext(
                frame,
                safeDeltaTime,
                width,
                height,
                previousState,
                profiles,
                targets,
                requests,
                targetGroup);
            _lastEvaluationResult = _service.Evaluate(context);
            _hasLastNoAllocState = false;
            _pendingState = _lastEvaluationResult.State;
            _hasPendingState = true;
            return _lastEvaluationResult;
        }

        private MxCameraEvaluationContext CreateEvaluationContext(
            long frame,
            float deltaTime,
            float width,
            float height,
            MxCameraState previousState,
            IEnumerable<MxCameraProfileDefinition> profiles,
            IEnumerable<MxCameraTargetSnapshot> targets,
            IEnumerable<MxCameraRequest> requests,
            MxCameraTargetGroup targetGroup)
        {
            IReadOnlyList<MxCameraProfileDefinition> profileList =
                profiles as IReadOnlyList<MxCameraProfileDefinition>;
            IReadOnlyList<MxCameraTargetSnapshot> targetList =
                targets as IReadOnlyList<MxCameraTargetSnapshot>;
            IReadOnlyList<MxCameraRequest> requestList =
                requests as IReadOnlyList<MxCameraRequest>;
            if (profileList != null && targetList != null && (requests == null || requestList != null))
            {
                return MxCameraEvaluationContext.Borrow(
                    frame,
                    deltaTime,
                    RigId,
                    width,
                    height,
                    previousState,
                    profileList,
                    targetList,
                    requestList,
                    targetGroup);
            }

            return new MxCameraEvaluationContext(
                frame,
                deltaTime,
                RigId,
                width,
                height,
                previousState,
                profiles,
                targets,
                requests,
                targetGroup);
        }

        public MxCameraResult EvaluateAndApplyLate(
            long frame,
            IEnumerable<MxCameraTargetSnapshot> targets,
            IEnumerable<MxCameraRequest> requests = null,
            MxCameraTargetGroup targetGroup = null)
        {
            return EvaluateAndApplyLate(frame, CurrentUnityDeltaTime(), targets, requests, targetGroup);
        }

        public MxCameraResult EvaluateAndApplyLate(
            long frame,
            float deltaTime,
            IEnumerable<MxCameraTargetSnapshot> targets,
            IEnumerable<MxCameraRequest> requests = null,
            MxCameraTargetGroup targetGroup = null)
        {
            float safeDeltaTime = NormalizeDeltaTime(deltaTime);
            MxCameraEvaluationResult result = Evaluate(frame, safeDeltaTime, targets, requests, targetGroup);
            return ApplyLate(result.State, safeDeltaTime);
        }

        public MxCameraResult EvaluateAndApplyLate(
            long frame,
            IEnumerable<MxCameraProfileDefinition> profiles,
            IEnumerable<MxCameraTargetSnapshot> targets,
            IEnumerable<MxCameraRequest> requests = null,
            MxCameraTargetGroup targetGroup = null)
        {
            return EvaluateAndApplyLate(frame, CurrentUnityDeltaTime(), profiles, targets, requests, targetGroup);
        }

        public MxCameraResult EvaluateAndApplyLate(
            long frame,
            float deltaTime,
            IEnumerable<MxCameraProfileDefinition> profiles,
            IEnumerable<MxCameraTargetSnapshot> targets,
            IEnumerable<MxCameraRequest> requests = null,
            MxCameraTargetGroup targetGroup = null)
        {
            float safeDeltaTime = NormalizeDeltaTime(deltaTime);
            MxCameraEvaluationResult result = Evaluate(frame, safeDeltaTime, profiles, targets, requests, targetGroup);
            return ApplyLate(result.State, safeDeltaTime);
        }

        /// <summary>
        /// NoAlloc after list capacity warmup when the camera service can evaluate the stable path.
        /// Falls back to the allocation-preserving diagnostics path for requests, invalid data, or fallback states.
        /// </summary>
        public MxCameraResult EvaluateAndApplyLateNoAlloc(
            long frame,
            float deltaTime,
            IReadOnlyList<MxCameraProfileDefinition> profiles,
            IReadOnlyList<MxCameraTargetSnapshot> targets)
        {
            EnsureInitialized();
            float safeDeltaTime = NormalizeDeltaTime(deltaTime);
            float height = _camera != null ? _camera.pixelHeight : Screen.height;
            float width = _camera != null ? _camera.pixelWidth : Screen.width;
            MxCameraProfileId previousProfileId =
                _hasLastNoAllocState
                    ? _lastNoAllocState.ProfileId
                    : _lastEvaluationResult != null
                        ? _lastEvaluationResult.State.ProfileId
                        : default;

            if (_service != null
                && _service.TryEvaluateNoAlloc(
                    frame,
                    safeDeltaTime,
                    RigId,
                    width,
                    height,
                    previousProfileId,
                    profiles,
                    targets,
                    out MxCameraEvaluationFrame evaluation))
            {
                _lastNoAllocEvaluation = evaluation;
                _lastNoAllocState = evaluation.State;
                _hasLastNoAllocState = true;
                _lastEvaluationResult = null;
                _hasPendingState = false;
                return ApplyLate(evaluation.State, safeDeltaTime);
            }

            return EvaluateAndApplyLate(frame, safeDeltaTime, profiles, targets);
        }

        public MxCameraResult Apply(in MxCameraState state)
        {
            return ApplyLate(state);
        }

        public MxCameraResult ApplyLate(in MxCameraState state)
        {
            return ApplyLate(state, CurrentUnityDeltaTime());
        }

        public MxCameraResult ApplyLate(in MxCameraState state, float deltaTime)
        {
            EnsureCamera();
            if (_camera == null)
                return AddBackendDiagnostic(MxCameraDiagnosticCodes.BackendMissingCamera, "Unity Camera is missing.");

            if (_lastAppliedUnityFrame == Time.frameCount)
            {
                _lastApplyResult = MxCameraResult.Ok("Camera state already applied this Unity frame.");
                return _lastApplyResult;
            }

            Vector3 corePosition = MxCameraUnityConversions.ToUnityVector(state.Position);
            Vector3 coreShakeOffset = MxCameraUnityConversions.ToUnityVector(state.ShakeOffset);
            Vector3 position = ShouldUseCoreShakeOffset()
                ? corePosition
                : corePosition - coreShakeOffset;
            Quaternion rotation = Quaternion.Euler(state.Rotation.Pitch, state.Rotation.Yaw, state.Rotation.Roll);
            float safeDeltaTime = NormalizeDeltaTime(deltaTime);
            position = ResolveObstruction(state, position, rotation, safeDeltaTime);
            MxCameraUnityShakeResult shakeResult = ResolveShake(state, position, rotation, coreShakeOffset, safeDeltaTime);
            position = shakeResult.Position;
            rotation = shakeResult.Rotation;
            _camera.transform.SetPositionAndRotation(position, rotation);
            _camera.orthographic = state.ProjectionKind == MxCameraProjectionKind.Orthographic;
            if (_camera.orthographic)
                _camera.orthographicSize = Mathf.Max(0.01f, state.OrthographicSize);
            else
                _camera.fieldOfView = Mathf.Clamp(state.FieldOfView, 1f, 179f);

            _lastAppliedUnityFrame = Time.frameCount;
            _hasPendingState = false;
            _lastApplyResult = MxCameraResult.Ok();
            return _lastApplyResult;
        }

        /// <summary>
        /// NoAlloc after initialization for value-state camera application when obstruction and shake
        /// resolvers support the value-state path.
        /// </summary>
        public MxCameraResult ApplyLate(in MxCameraStateValue state, float deltaTime)
        {
            EnsureCamera();
            if (_camera == null)
                return AddBackendDiagnostic(MxCameraDiagnosticCodes.BackendMissingCamera, "Unity Camera is missing.");

            if (_lastAppliedUnityFrame == Time.frameCount)
            {
                _lastApplyResult = MxCameraResult.Ok("Camera state already applied this Unity frame.");
                return _lastApplyResult;
            }

            Vector3 corePosition = MxCameraUnityConversions.ToUnityVector(state.Position);
            Vector3 coreShakeOffset = MxCameraUnityConversions.ToUnityVector(state.ShakeOffset);
            Vector3 position = ShouldUseCoreShakeOffset()
                ? corePosition
                : corePosition - coreShakeOffset;
            Quaternion rotation = Quaternion.Euler(state.Rotation.Pitch, state.Rotation.Yaw, state.Rotation.Roll);
            float safeDeltaTime = NormalizeDeltaTime(deltaTime);
            position = ResolveObstruction(state, position, rotation, safeDeltaTime);
            MxCameraUnityShakeResult shakeResult = ResolveShake(state, position, rotation, coreShakeOffset, safeDeltaTime);
            position = shakeResult.Position;
            rotation = shakeResult.Rotation;
            _camera.transform.SetPositionAndRotation(position, rotation);
            _camera.orthographic = state.ProjectionKind == MxCameraProjectionKind.Orthographic;
            if (_camera.orthographic)
                _camera.orthographicSize = Mathf.Max(0.01f, state.OrthographicSize);
            else
                _camera.fieldOfView = Mathf.Clamp(state.FieldOfView, 1f, 179f);

            _lastAppliedUnityFrame = Time.frameCount;
            _hasPendingState = false;
            _lastApplyResult = MxCameraResult.Ok();
            return _lastApplyResult;
        }

        public MxCameraDebugSnapshot CaptureSnapshot()
        {
            if (_service == null)
            {
                return new MxCameraDebugSnapshot(
                    false,
                    RigId,
                    "UnityCamera",
                    default,
                    MxCameraMode.Follow,
                    MxCameraTargetGroupState.Empty,
                    MxCameraState.Empty,
                    _backendDiagnostics.ToArray(),
                    0);
            }

            MxCameraDebugSnapshot core = _service.CaptureSnapshot();
            var diagnostics = new List<MxCameraDiagnostic>(core.RecentDiagnostics);
            diagnostics.AddRange(_backendDiagnostics.ToArray());
            return new MxCameraDebugSnapshot(
                _camera != null,
                RigId,
                "UnityCamera",
                core.ActiveProfileId,
                core.Mode,
                core.TargetGroupState,
                core.State,
                diagnostics,
                core.ShakeRequestCount);
        }

        public void Dispose()
        {
            // A Unity Camera keeps this component across CharacterTest session rebuilds. Treat
            // disposal as the end of an evaluation continuity segment so the first camera state
            // of the next session can be applied immediately, even when both sessions are
            // rebuilt during the same Unity frame.
            _hasPendingState = false;
            _pendingState = null;
            _lastAppliedUnityFrame = -1;
            _lastEvaluationResult = null;
            _lastNoAllocEvaluation = default;
            _lastNoAllocState = MxCameraStateValue.Empty;
            _hasLastNoAllocState = false;
            _service = null;
        }

        public void SetShake(
            MxCameraUnityShakeMode mode,
            MxCameraUnityShakeSettings settings = null,
            IMxCameraUnityShakePresenter presenter = null)
        {
            _shakeMode = mode;
            if (settings != null)
                _shakeSettings = settings.Clone();

            if (presenter != null)
                SetShakePresenter(presenter);
            else if (mode == MxCameraUnityShakeMode.None)
                SetShakePresenter(null);

            if (_shakePresenter != null)
                _shakePresenter.Reset();
            else
                _defaultShakePresenter.Reset();
        }

        public void SetShakePresenter(IMxCameraUnityShakePresenter presenter)
        {
            _shakePresenter = presenter;
            _shakePresenterBehaviour = presenter as MonoBehaviour;
        }

        public void SetObstruction(
            MxCameraUnityObstructionMode mode,
            MxCameraUnityObstructionSettings settings = null,
            IMxCameraUnityObstructionResolver resolver = null)
        {
            _obstructionMode = mode;
            if (settings != null)
                _obstructionSettings = settings.Clone();

            if (resolver != null)
                SetObstructionResolver(resolver);
            else if (mode == MxCameraUnityObstructionMode.None)
                SetObstructionResolver(null);
        }

        public void SetObstructionResolver(IMxCameraUnityObstructionResolver resolver)
        {
            _obstructionResolver = resolver;
            _obstructionResolverBehaviour = resolver as MonoBehaviour;
        }

        public static MxCameraUnityRig EnsureFor(UnityCamera camera)
        {
            if (camera == null)
                return null;

            MxCameraUnityRig rig = camera.GetComponent<MxCameraUnityRig>();
            if (rig == null)
                rig = camera.gameObject.AddComponent<MxCameraUnityRig>();
            rig._camera = camera;
            rig.EnsureInitialized();
            return rig;
        }

        private void EnsureInitialized()
        {
            EnsureCamera();
            _service = _service ?? new MxCameraService(RigId);
            _profileProvider = _profileProvider ?? ResolveProfileProvider();
            _shakePresenter = _shakePresenter ?? ResolveShakePresenter();
            _obstructionResolver = _obstructionResolver ?? ResolveObstructionResolver();
        }

        private void EnsureCamera()
        {
            if (_camera == null)
                _camera = GetComponent<UnityCamera>();
        }

        private IMxCameraProfileProvider ResolveProfileProvider()
        {
            if (_profileProviderBehaviour is IMxCameraProfileProvider provider)
                return provider;

            var componentProvider = GetComponent<IMxCameraProfileProvider>();
            if (componentProvider != null)
                return componentProvider;

            return new InlineProfileProvider((_inlineProfile ?? new MxCameraUnityProfileDefinition()).ToRuntimeDefinition());
        }

        private IMxCameraUnityShakePresenter ResolveShakePresenter()
        {
            if (_shakePresenterBehaviour is IMxCameraUnityShakePresenter presenter)
                return presenter;

            return GetComponent<IMxCameraUnityShakePresenter>();
        }

        private IMxCameraUnityObstructionResolver ResolveObstructionResolver()
        {
            if (_obstructionResolverBehaviour is IMxCameraUnityObstructionResolver provider)
                return provider;

            return GetComponent<IMxCameraUnityObstructionResolver>();
        }

        private bool ShouldUseCoreShakeOffset()
        {
            return _shakeMode == MxCameraUnityShakeMode.LegacyCoreOffset;
        }

        private MxCameraUnityShakeResult ResolveShake(
            in MxCameraState state,
            Vector3 basePosition,
            Quaternion baseRotation,
            Vector3 coreShakeOffset,
            float deltaTime)
        {
            _lastShakeResult = MxCameraUnityShakeResult.Inactive(
                basePosition,
                baseRotation,
                coreShakeOffset);

            if (_shakeMode == MxCameraUnityShakeMode.None)
                return _lastShakeResult;

            if (_shakeMode == MxCameraUnityShakeMode.LegacyCoreOffset)
            {
                _lastShakeResult = coreShakeOffset.sqrMagnitude > 0.0000001f
                    ? MxCameraUnityShakeResult.Applied(
                        basePosition,
                        baseRotation,
                        coreShakeOffset.magnitude,
                        coreShakeOffset,
                        Vector3.zero,
                        coreShakeOffset,
                        "CAM_SHAKE_CORE_OFFSET",
                        "Camera core shake offset was applied directly.")
                    : MxCameraUnityShakeResult.Inactive(basePosition, baseRotation, coreShakeOffset);
                return _lastShakeResult;
            }

            IMxCameraUnityShakePresenter presenter =
                _shakePresenter ?? ResolveShakePresenter();
            if (presenter == null)
                presenter = _defaultShakePresenter;

            _shakePresenter = presenter;
            var context = new MxCameraUnityShakeContext(
                _shakeMode,
                state,
                basePosition,
                baseRotation,
                coreShakeOffset,
                deltaTime,
                _shakeSettings);
            MxCameraUnityShakeResult result = presenter.Resolve(in context);
            if (!result.HasResult)
                return _lastShakeResult;

            _lastShakeResult = result;
            return result;
        }

        private MxCameraUnityShakeResult ResolveShake(
            in MxCameraStateValue state,
            Vector3 basePosition,
            Quaternion baseRotation,
            Vector3 coreShakeOffset,
            float deltaTime)
        {
            _lastShakeResult = MxCameraUnityShakeResult.Inactive(
                basePosition,
                baseRotation,
                coreShakeOffset);

            if (_shakeMode == MxCameraUnityShakeMode.None)
                return _lastShakeResult;

            if (_shakeMode == MxCameraUnityShakeMode.LegacyCoreOffset)
            {
                _lastShakeResult = coreShakeOffset.sqrMagnitude > 0.0000001f
                    ? MxCameraUnityShakeResult.Applied(
                        basePosition,
                        baseRotation,
                        coreShakeOffset.magnitude,
                        coreShakeOffset,
                        Vector3.zero,
                        coreShakeOffset,
                        "CAM_SHAKE_CORE_OFFSET",
                        "Camera core shake offset was applied directly.")
                    : MxCameraUnityShakeResult.Inactive(basePosition, baseRotation, coreShakeOffset);
                return _lastShakeResult;
            }

            IMxCameraUnityShakePresenter presenter =
                _shakePresenter ?? ResolveShakePresenter();
            if (presenter == null)
                presenter = _defaultShakePresenter;

            _shakePresenter = presenter;
            if (!ReferenceEquals(presenter, _defaultShakePresenter))
                return ResolveShake(state.ToReference(), basePosition, baseRotation, coreShakeOffset, deltaTime);

            var context = new MxCameraUnityShakeContext(
                _shakeMode,
                null,
                basePosition,
                baseRotation,
                coreShakeOffset,
                deltaTime,
                _shakeSettings);
            MxCameraUnityShakeResult result = presenter.Resolve(in context);
            if (!result.HasResult)
                return _lastShakeResult;

            _lastShakeResult = result;
            return result;
        }

        private Vector3 ResolveObstruction(
            in MxCameraState state,
            Vector3 desiredPosition,
            Quaternion desiredRotation,
            float deltaTime)
        {
            _lastObstructionResult = MxCameraUnityObstructionResult.Unobstructed(desiredPosition);
            if (_obstructionMode == MxCameraUnityObstructionMode.None)
                return desiredPosition;

            IMxCameraUnityObstructionResolver resolver =
                _obstructionResolver ?? ResolveObstructionResolver();
            if (resolver == null)
                return desiredPosition;

            _obstructionResolver = resolver;
            var context = new MxCameraUnityObstructionContext(
                _obstructionMode,
                state,
                desiredPosition,
                desiredRotation,
                deltaTime,
                _obstructionSettings);
            MxCameraUnityObstructionResult result = resolver.Resolve(in context);
            if (!result.HasResult)
                return desiredPosition;

            _lastObstructionResult = result;
            return result.Position;
        }

        private Vector3 ResolveObstruction(
            in MxCameraStateValue state,
            Vector3 desiredPosition,
            Quaternion desiredRotation,
            float deltaTime)
        {
            _lastObstructionResult = MxCameraUnityObstructionResult.Unobstructed(desiredPosition);
            if (_obstructionMode == MxCameraUnityObstructionMode.None)
                return desiredPosition;

            IMxCameraUnityObstructionResolver resolver =
                _obstructionResolver ?? ResolveObstructionResolver();
            if (resolver == null)
                return desiredPosition;

            _obstructionResolver = resolver;
            if (!(resolver is IMxCameraUnityObstructionResolverNoAlloc noAllocResolver))
                return ResolveObstruction(state.ToReference(), desiredPosition, desiredRotation, deltaTime);

            var context = new MxCameraUnityObstructionContextValue(
                _obstructionMode,
                state,
                desiredPosition,
                desiredRotation,
                deltaTime,
                _obstructionSettings);
            MxCameraUnityObstructionResult result = noAllocResolver.Resolve(in context);
            if (!result.HasResult)
                return desiredPosition;

            _lastObstructionResult = result;
            return result.Position;
        }

        private MxCameraResult AddBackendDiagnostic(string code, string message)
        {
            while (_backendDiagnostics.Count >= 32)
                _backendDiagnostics.Dequeue();

            _backendDiagnostics.Enqueue(new MxCameraDiagnostic(code, message));
            _lastApplyResult = MxCameraResult.Failed(code, message);
            return _lastApplyResult;
        }

        private static float NormalizeDeltaTime(float deltaTime)
        {
            return deltaTime > 0f ? deltaTime : 0f;
        }

        // UnityTimeBoundary: compatibility overloads without explicit deltaTime read Unity's frame delta here.
        private static float CurrentUnityDeltaTime()
        {
            return Time.deltaTime;
        }

        private sealed class InlineProfileProvider : IMxCameraProfileProvider
        {
            private readonly MxCameraProfileDefinition[] _profiles;

            public InlineProfileProvider(MxCameraProfileDefinition profile)
            {
                _profiles = new[] { profile };
            }

            public IReadOnlyList<MxCameraProfileDefinition> Profiles => _profiles;
        }
    }

    public static class MxCameraUnityConversions
    {
        public static MxCameraVector3 ToCameraVector(Vector3 value)
        {
            return new MxCameraVector3(value.x, value.y, value.z);
        }

        public static Vector3 ToUnityVector(MxCameraVector3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }
    }
}
