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

        private readonly Queue<MxCameraDiagnostic> _backendDiagnostics = new Queue<MxCameraDiagnostic>();
        private IMxCameraProfileProvider _profileProvider;
        private MxCameraService _service;
        private MxCameraState _pendingState;
        private bool _hasPendingState;
        private int _lastAppliedUnityFrame = -1;
        private MxCameraResult _lastApplyResult = MxCameraResult.Ok();
        private MxCameraEvaluationResult _lastEvaluationResult;

        public MxCameraRigId RigId => new MxCameraRigId(string.IsNullOrWhiteSpace(_rigId) ? name : _rigId);
        public MxCameraEvaluationResult LastEvaluationResult => _lastEvaluationResult;
        public MxCameraResult LastApplyResult => _lastApplyResult;
        public bool HasAppliedThisUnityFrame => _lastAppliedUnityFrame == Time.frameCount;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void LateUpdate()
        {
            if (_applyInLateUpdate && _hasPendingState)
                ApplyLate(_pendingState);
        }

        public MxCameraResult Initialize(IMxCameraProfileProvider profiles)
        {
            EnsureCamera();
            _profileProvider = profiles ?? ResolveProfileProvider();
            _service = _service ?? new MxCameraService(RigId);
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
            EnsureInitialized();
            IReadOnlyList<MxCameraProfileDefinition> profiles = _profileProvider != null
                ? _profileProvider.Profiles
                : new[] { (_inlineProfile ?? new MxCameraUnityProfileDefinition()).ToRuntimeDefinition() };
            return Evaluate(frame, profiles, targets, requests, targetGroup);
        }

        public MxCameraEvaluationResult Evaluate(
            long frame,
            IEnumerable<MxCameraProfileDefinition> profiles,
            IEnumerable<MxCameraTargetSnapshot> targets,
            IEnumerable<MxCameraRequest> requests = null,
            MxCameraTargetGroup targetGroup = null)
        {
            EnsureInitialized();
            float height = _camera != null ? _camera.pixelHeight : Screen.height;
            float width = _camera != null ? _camera.pixelWidth : Screen.width;
            var context = new MxCameraEvaluationContext(
                frame,
                Time.deltaTime,
                RigId,
                width,
                height,
                _lastEvaluationResult != null ? _lastEvaluationResult.State : MxCameraState.Empty,
                profiles,
                targets,
                requests,
                targetGroup);
            _lastEvaluationResult = _service.Evaluate(context);
            _pendingState = _lastEvaluationResult.State;
            _hasPendingState = true;
            return _lastEvaluationResult;
        }

        public MxCameraResult EvaluateAndApplyLate(
            long frame,
            IEnumerable<MxCameraTargetSnapshot> targets,
            IEnumerable<MxCameraRequest> requests = null,
            MxCameraTargetGroup targetGroup = null)
        {
            MxCameraEvaluationResult result = Evaluate(frame, targets, requests, targetGroup);
            return ApplyLate(result.State);
        }

        public MxCameraResult EvaluateAndApplyLate(
            long frame,
            IEnumerable<MxCameraProfileDefinition> profiles,
            IEnumerable<MxCameraTargetSnapshot> targets,
            IEnumerable<MxCameraRequest> requests = null,
            MxCameraTargetGroup targetGroup = null)
        {
            MxCameraEvaluationResult result = Evaluate(frame, profiles, targets, requests, targetGroup);
            return ApplyLate(result.State);
        }

        public MxCameraResult Apply(in MxCameraState state)
        {
            return ApplyLate(state);
        }

        public MxCameraResult ApplyLate(in MxCameraState state)
        {
            EnsureCamera();
            if (_camera == null)
                return AddBackendDiagnostic(MxCameraDiagnosticCodes.BackendMissingCamera, "Unity Camera is missing.");

            if (_lastAppliedUnityFrame == Time.frameCount)
            {
                _lastApplyResult = MxCameraResult.Ok("Camera state already applied this Unity frame.");
                return _lastApplyResult;
            }

            Vector3 position = MxCameraUnityConversions.ToUnityVector(state.Position);
            Quaternion rotation = Quaternion.Euler(state.Rotation.Pitch, state.Rotation.Yaw, state.Rotation.Roll);
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
            _hasPendingState = false;
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

        private MxCameraResult AddBackendDiagnostic(string code, string message)
        {
            while (_backendDiagnostics.Count >= 32)
                _backendDiagnostics.Dequeue();

            _backendDiagnostics.Enqueue(new MxCameraDiagnostic(code, message));
            _lastApplyResult = MxCameraResult.Failed(code, message);
            return _lastApplyResult;
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
