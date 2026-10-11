using System.Collections.Generic;
using System;
using MxFramework.Camera;
using MxFramework.Camera.Unity;
using MxFramework.Core.Math;
using MxFramework.Input;
using MxFramework.Runtime;
using Unity.Profiling;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Game/Character Test/Third Person Camera Rig Bridge")]
    public sealed class CharacterTestThirdPersonCameraRigBridge : MonoBehaviour, ICharacterTestCharacterMovementBasisProvider
    {
        private static readonly ProfilerMarker SyncRuntimeCameraMarker =
            new ProfilerMarker("CharacterTest.Camera.SyncRuntimeState");
        private static readonly ProfilerMarker ResolveTargetMarker =
            new ProfilerMarker("CharacterTest.Camera.ResolveTarget");
        private static readonly ProfilerMarker MovementBasisMarker =
            new ProfilerMarker("CharacterTest.Camera.MovementBasis");
        private static readonly ProfilerMarker TargetSnapshotsMarker =
            new ProfilerMarker("CharacterTest.Camera.TargetSnapshots");
        private static readonly ProfilerMarker ProfileMarker =
            new ProfilerMarker("CharacterTest.Camera.Profile");
        private static readonly ProfilerMarker EvaluateMarker =
            new ProfilerMarker("CharacterTest.Camera.Evaluate");

        private const string LogCategory = "CharacterTest.Camera";
        private const string LocalPlayerTargetRef = "character-test.local-player";
        private const string AutoTargetRefPrefix = "character-test.entity.";
        private const string CameraCursorOwner = "CharacterTest.Camera";
        private const string DebugCursorOwner = "CharacterTest.Debug";
        private const string LegacyUiCursorOwner = "CharacterTest.UI.Legacy";
        private const float TargetLockViewSwitchAnchorHeightMeters = 1.15f;
        private const float TargetLockViewSwitchEpsilon = 0.0001f;
        private const float RuntimeAiLabAutoMinDistance = 8f;
        private const float RuntimeAiLabAutoMaxDistance = 90f;
        private const float RuntimeAiLabAutoTargetPadding = 2.5f;
        private const float RuntimeAiLabAutoHorizontalDistanceRatio = 0.21f;
        private const float RuntimeAiLabAutoVerticalDistanceRatio = -0.345f;
        private static readonly string[] NoDiagnosticTags = new string[0];

        [Header("Mode")]
        [SerializeField] private CharacterTestCameraMode _cameraMode = CharacterTestCameraMode.ThirdPersonControlled;
        [SerializeField] private UnityCamera _camera;

        [Header("Presentation")]
        [SerializeField] private bool _useExternalPresentationTick = true;

        [Header("Third Person")]
        [SerializeField] private string _profileId = "character-test.third-person";
        [SerializeField] private float _distance = 2f;
        [SerializeField] private float _minDistance = 1.5f;
        [SerializeField] private float _maxDistance = 10f;
        [SerializeField] private float _fieldOfView = 60f;
        [SerializeField] private float _pitch = 28f;
        [SerializeField] private float _yawOffset;
        [SerializeField] private Vector3 _focusOffset = new Vector3(0f, 1.45f, 0f);
        [SerializeField] private Vector3 _boundsExtents = new Vector3(0.55f, 1f, 0.55f);
        [SerializeField] private float _targetPadding = 0.35f;
        [SerializeField] private bool _followTargetYaw;
        [SerializeField] private bool _fallbackToFirstCharacter = true;

        [Header("Vertical Follow")]
        [SerializeField] private bool _enableVerticalDeadZone = true;
        [SerializeField] private float _verticalDeadZoneUp = 0.9f;
        [SerializeField] private float _verticalDeadZoneDown = 0.35f;
        [SerializeField] private float _verticalGroundedReturnSharpness = 16f;

        [Header("Third Person Lock View")]
        [SerializeField][Range(0.55f, 0.85f)] private float _lockedTargetScreenY = 0.66f;
        [SerializeField] private float _cameraTransitionSharpness = 12f;

        [Header("Mouse View")]
        [SerializeField] private bool _captureCursorOnBind = true;
        [SerializeField] private float _mouseYawSensitivity = 0.12f;
        [SerializeField] private float _mousePitchSensitivity = 0.1f;
        [SerializeField] private float _minPitch = 5f;
        [SerializeField] private float _maxPitch = 65f;

        [Header("Multi Target Auto")]
        [SerializeField] private float _autoYaw = 0f;
        [SerializeField] private float _autoPitch = 35f;
        [SerializeField] private float _autoTargetPadding = 1f;
        [SerializeField] private float _autoMaxTargetRadius = 64f;
        private bool _tacticalBattleAutoFraming;
        private const float TacticalBattlePitch = 26f;
        private const float TacticalBattleFieldOfView = 60f;
        // Keep the group obstruction sweep above the floor after grounded actors reach Y=0.
        private const float TacticalBattleFocusOffsetY = 1.25f;

        [Header("Target Lock")]
        [SerializeField] private bool _enableTargetLock = true;
        [SerializeField] private float _targetLockMaxDistance = 24f;
        [SerializeField] private float _targetLockViewSwitchThresholdDegrees = 12f;
        [SerializeField] private float _targetLockViewSwitchInputDeadzoneDegrees = 0.08f;
        [SerializeField] private float _targetLockViewSwitchCooldownSeconds = 0.3f;

        [Header("Camera Obstruction")]
        [SerializeField] private MxCameraUnityObstructionMode _obstructionMode =
            MxCameraUnityObstructionMode.FrameworkPhysicsAabb;
        [SerializeField] private float _obstructionRadius = 0.22f;
        [SerializeField] private float _obstructionPadding = 0.12f;
        [SerializeField] private float _obstructionMinDistance = 0.75f;
        [SerializeField] private float _obstructionFocusClearance = 0.25f;
        [SerializeField] private float _obstructionRecoverySharpness = 12f;

        [Header("Camera Shake")]
        [SerializeField] private MxCameraUnityShakeMode _shakeMode = MxCameraUnityShakeMode.Procedural;
        [SerializeField] private float _shakePositionAmplitude = 1f;
        [SerializeField] private float _shakeRotationAmplitude = 3.5f;
        [SerializeField] private float _shakeFrequency = 22f;
        [SerializeField] private float _shakeRecoverySharpness = 14f;
        [SerializeField] private float _shakeMaxPositionOffset = 0.45f;
        [SerializeField] private float _shakeMaxRotationDegrees = 4f;
        [SerializeField][Range(0f, 1f)] private float _shakeDirectionalInfluence = 0.35f;
        [SerializeField] private int _shakeSeed = 1337;

        private readonly List<MxCameraTargetSnapshot> _targets = new List<MxCameraTargetSnapshot>(4);
        private readonly MxCameraProfileDefinition[] _profiles = { new MxCameraProfileDefinition() };

        private CharacterTestSliceServices _services;
        private IInputProvider _inputProvider;
        private CharacterTestArenaSessionController _arenaSessionController;
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private MxCameraUnityRig _rig;
        private long _cameraFrame;
        private float _controlledYaw;
        private float _controlledPitch;
        private CharacterTestCharacterMovementBasis _currentMovementBasis =
            CharacterTestCharacterMovementBasis.Identity;
        private readonly CharacterTestCameraProfileSmoother _profileSmoother =
            new CharacterTestCameraProfileSmoother();
        private readonly HashSet<int> _unresolvedCameraAnchorEntities = new HashSet<int>();
        private string _activeGameplayCameraProfileId = string.Empty;
        private IMxCursorController _cursorController;
        private System.IDisposable _gameplayCursorScope;
        private System.IDisposable _debugCursorScope;
        private bool _ownsCursorController;
        private bool _missingCameraLogged;
        private bool _missingInputLogged;
        private bool _missingTargetLogged;
        private bool _applyFailureLogged;
        private bool _hasPrimaryCameraTargetY;
        private int _primaryCameraTargetEntityId;
        private float _primaryCameraTargetY;
        private bool _runtimeAiLabAutoFraming;
        private float _presentationDeltaTime;
        private CharacterTestFrameworkPhysicsCameraObstructionResolver _frameworkPhysicsObstructionResolver;
        private bool _hasObstructionFocusOverride;
        private Vector3 _obstructionFocusOverride;
        private Vector2 _targetLockViewSwitchAccumulator;
        private float _targetLockViewSwitchCooldownRemaining;

        public CharacterTestCameraMode CameraMode
        {
            get => _cameraMode;
            set
            {
                _cameraMode = value;
                if (_cameraMode != CharacterTestCameraMode.ThirdPersonControlled)
                    ResetCameraObstructionFocusOverride();

                if (_services != null)
                {
                    RuntimeCommandValidationResult result = _services.EnqueueSetCameraMode(
                        value,
                        CharacterTestRuntimeCommandSources.System);
                    if (!result.Success)
                        _logger.Warning(LogCategory, "Camera mode command rejected: " + result.Error.Message);
                }

                ApplyCursorPolicy();
            }
        }

        /// <summary>
        /// Selects the Runtime AI Lab safe-area composition for multi-target auto framing.
        /// </summary>
        /// <remarks>NoAlloc. Call during low-frequency session composition before presentation ticks.</remarks>
        internal void ConfigureRuntimeAiLabAutoFraming(bool enabled)
        {
            _runtimeAiLabAutoFraming = enabled;
        }

        internal void ConfigureTacticalBattleAutoFraming(bool enabled)
        {
            _tacticalBattleAutoFraming = enabled;
        }

        public CharacterTestCharacterMovementBasis CurrentMovementBasis => _currentMovementBasis;
        public bool CursorCaptured => _cursorController != null && _cursorController.IsCaptured;
        public MxCameraUnityRig Rig => _rig;
        public MxCameraUnityObstructionMode ObstructionMode
        {
            get => _obstructionMode;
            set
            {
                _obstructionMode = value;
                ConfigureCameraObstruction();
            }
        }

        public MxCameraUnityObstructionResult LastObstructionResult =>
            _rig != null ? _rig.LastObstructionResult : default;

        public MxCameraUnityShakeMode ShakeMode
        {
            get => _shakeMode;
            set
            {
                _shakeMode = value;
                ConfigureCameraShake();
            }
        }

        public MxCameraUnityShakeResult LastShakeResult =>
            _rig != null ? _rig.LastShakeResult : default;

        public bool UseExternalPresentationTick
        {
            get => _useExternalPresentationTick;
            set => _useExternalPresentationTick = value;
        }

        public MxCameraState CurrentState
        {
            get
            {
                if (_rig == null)
                    return MxCameraState.Empty;
                if (_rig.LastEvaluationResult != null)
                    return _rig.LastEvaluationResult.State;
                return _rig.HasLastNoAllocEvaluation
                    ? _rig.LastNoAllocEvaluation.State.ToReference()
                    : MxCameraState.Empty;
            }
        }

        private MxCameraStateValue CurrentStateValue
        {
            get
            {
                if (_rig == null)
                    return MxCameraStateValue.Empty;
                if (_rig.HasLastNoAllocEvaluation)
                    return _rig.LastNoAllocEvaluation.State;
                return _rig.LastEvaluationResult != null
                    ? MxCameraStateValue.From(_rig.LastEvaluationResult.State)
                    : MxCameraStateValue.Empty;
            }
        }

        public void Bind(
            GameSlice slice,
            IRuntimeLogger logger,
            UnityCamera camera = null,
            IMxCursorController cursorController = null)
        {
            Bind(slice?.Services, logger, camera, cursorController);
        }

        internal void Bind(
            CharacterTestSliceServices services,
            IRuntimeLogger logger,
            UnityCamera camera = null,
            IMxCursorController cursorController = null)
        {
            ReleaseCursorScopes();
            ReleaseOwnedCursorController();
            _services = services;
            _logger = logger ?? NullRuntimeLogger.Instance;
            if (camera != null)
                _camera = camera;

            _cursorController = cursorController ?? new MxCursorController();
            _ownsCursorController = cursorController == null;

            _cameraFrame = 0L;
            _controlledYaw = _yawOffset;
            _controlledPitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            _currentMovementBasis = CharacterTestCharacterMovementBasis.Identity;
            _profileSmoother.Reset();
            _unresolvedCameraAnchorEntities.Clear();
            _activeGameplayCameraProfileId = string.Empty;
            _frameworkPhysicsObstructionResolver?.Reset();
            ResetCameraObstructionFocusOverride();
            ResetVerticalDeadZone();
            ResetTargetLockViewSwitchInput();
            _missingCameraLogged = false;
            _missingInputLogged = false;
            _missingTargetLogged = false;
            _applyFailureLogged = false;
            _inputProvider = InputProviderResolver.ResolveOrCreateDefault(this);
            _arenaSessionController = GetComponent<CharacterTestArenaSessionController>();
            enabled = _services != null;
            _services?.SetTargetLockMaxDistance(_targetLockMaxDistance);
            SyncRuntimeCameraState();
            ResolveCameraRig();
            ApplyCursorPolicy();
        }

        public void PrepareForRuntimeTick()
        {
            if (_services == null)
                return;

            SyncRuntimeCameraState();
            CharacterTestCharacterEntity primaryTarget = ResolvePrimaryTarget();
            UpdateCursorAndViewInput(primaryTarget);
            UpdateMovementBasis(primaryTarget);
        }

        public void RequestCursorReleasedByDebugUi()
        {
            EnsureDebugCursorScope();
        }

        public void RequestCursorCapturedByDebugUi()
        {
            ReleaseDebugCursorScope();
            ApplyCursorPolicy();
        }

        public System.IDisposable PushCursorReleasedByUi()
        {
            EnsureCursorController();
            return _cursorController.Push(MxCursorRequest.Released(LegacyUiCursorOwner));
        }

        public void Clear()
        {
            ReleaseCursorScopes();
            ReleaseOwnedCursorController();
            _services = null;
            _arenaSessionController = null;
            _targets.Clear();
            if (_rig != null)
            {
                _rig.SetShake(MxCameraUnityShakeMode.None);
                _rig.SetObstruction(MxCameraUnityObstructionMode.None);
                _rig.Dispose();
            }

            _rig = null;
            _currentMovementBasis = CharacterTestCharacterMovementBasis.Identity;
            _profileSmoother.Reset();
            ResetCameraObstructionFocusOverride();
            ResetVerticalDeadZone();
            ResetTargetLockViewSwitchInput();
            enabled = false;
        }

        private void LateUpdate()
        {
            if (_useExternalPresentationTick)
                return;

            TickPresentation(Time.deltaTime, new RuntimeFrame(Time.frameCount));
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                _cursorController?.Refresh();
        }

        public void TickPresentation(float deltaTime, RuntimeFrame frame = default)
        {
            if (_services == null || !ResolveCameraRig())
                return;

            _presentationDeltaTime = Mathf.Max(0f, deltaTime);
            using (SyncRuntimeCameraMarker.Auto())
            {
                SyncRuntimeCameraState();
            }

            _cameraFrame++;
            CharacterTestCharacterEntity primaryTarget;
            using (ResolveTargetMarker.Auto())
            {
                primaryTarget = ResolvePrimaryTarget();
            }

            using (MovementBasisMarker.Auto())
            {
                UpdateMovementBasis(primaryTarget);
            }

            using (TargetSnapshotsMarker.Auto())
            {
                BuildTargetSnapshots(primaryTarget);
            }

            using (ProfileMarker.Auto())
            {
                UpdateProfile(primaryTarget, _profiles[0]);
            }

            MxCameraResult result;
            using (EvaluateMarker.Auto())
            {
                result = _rig.EvaluateAndApplyLateNoAlloc(_cameraFrame, _presentationDeltaTime, _profiles, _targets);
            }

            if (!result.Success && !_applyFailureLogged)
            {
                _applyFailureLogged = true;
                _logger.Warning(LogCategory, "Camera apply failed. mode=" + _cameraMode + " code=" + result.Code + " message=" + result.Message);
            }
        }

        private void ApplyCursorPolicy()
        {
            if (_services == null
                || IsAutoFramingMode(_cameraMode)
                || !_captureCursorOnBind)
            {
                ReleaseGameplayCursorScope();
                return;
            }

            EnsureGameplayCursorScope();
        }

        private void UpdateCursorAndViewInput(CharacterTestCharacterEntity target)
        {
            InputSnapshot snapshot = ReadInputSnapshot();
            if (snapshot.CameraModeNextPressed)
                SelectNextCameraMode();
            TickTargetLockViewSwitchCooldown();
            if (IsAutoFramingMode(_cameraMode))
            {
                ApplyCursorPolicy();
                if (snapshot.TargetLockPressed)
                    ToggleTargetLock(target);

                return;
            }

            if (snapshot.DebugTogglePressed)
            {
                if (CursorCaptured)
                    RequestCursorReleasedByDebugUi();
                else
                    RequestCursorCapturedByDebugUi();
            }
            else if (snapshot.TargetLockPressed)
            {
                if (CursorCaptured)
                    ToggleTargetLock(target);
            }

            if (!CursorCaptured || target == null)
            {
                ResetTargetLockViewSwitchInput();
                return;
            }

            CharacterTestCharacterEntity lockedTarget = ResolveLockedTarget(target);
            if (lockedTarget != null)
            {
                HandleTargetLockViewSwitchInput(target, lockedTarget, snapshot.Look);
                return;
            }

            ResetTargetLockViewSwitchInput();

            Vector2 mouseDelta = snapshot.Look;
            _controlledYaw += mouseDelta.x * _mouseYawSensitivity;
            _controlledPitch = Mathf.Clamp(
                _controlledPitch - mouseDelta.y * _mousePitchSensitivity,
                Mathf.Min(_minPitch, _maxPitch),
                Mathf.Max(_minPitch, _maxPitch));
        }

        private void SelectNextCameraMode()
        {
            if (_arenaSessionController != null)
            {
                _arenaSessionController.SelectNextCameraMode();
                return;
            }

            if (_services == null)
                return;

            CharacterTestCameraMode next = _cameraMode == CharacterTestCameraMode.ThirdPersonControlled
                ? CharacterTestCameraMode.MultiTargetAuto
                : CharacterTestCameraMode.ThirdPersonControlled;
            RuntimeCommandValidationResult result = _services.EnqueueSetCameraMode(
                next,
                CharacterTestRuntimeCommandSources.CameraInput);
            if (!result.Success)
                _logger.Warning(LogCategory, "Camera mode shortcut rejected: " + result.Error.Message);
        }

        private void UpdateMovementBasis(CharacterTestCharacterEntity primaryTarget)
        {
            Vector2 forward = ResolveCameraForward();
            Vector2 right = new Vector2(forward.y, -forward.x);
            CharacterTestCharacterEntity lockedTarget = ResolveLockedTarget(primaryTarget);
            _currentMovementBasis = new CharacterTestCharacterMovementBasis(
                forward,
                right,
                lockedTarget != null,
                lockedTarget != null
                    ? new Vector2(lockedTarget.Transform.X, lockedTarget.Transform.Z)
                    : Vector2.zero);
        }

        private bool ResolveCameraRig()
        {
            if (_camera == null)
                _camera = UnityCamera.main;

            if (_camera == null)
            {
                if (!_missingCameraLogged)
                {
                    _missingCameraLogged = true;
                    _logger.Warning(LogCategory, "Camera bridge is enabled but no Unity Camera is available.");
                }

                return false;
            }

            if (_rig == null)
            {
                _rig = MxCameraUnityRig.EnsureFor(_camera);
                ConfigureCameraShake();
                ConfigureCameraObstruction();
            }

            return _rig != null;
        }

        private void ConfigureCameraShake()
        {
            if (_rig == null)
                return;

            _rig.SetShake(
                _shakeMode,
                new MxCameraUnityShakeSettings
                {
                    positionAmplitude = Mathf.Max(0f, _shakePositionAmplitude),
                    rotationAmplitude = Mathf.Max(0f, _shakeRotationAmplitude),
                    frequency = Mathf.Max(0.001f, _shakeFrequency),
                    recoverySharpness = Mathf.Max(0f, _shakeRecoverySharpness),
                    maxPositionOffset = Mathf.Max(0f, _shakeMaxPositionOffset),
                    maxRotationDegrees = Mathf.Max(0f, _shakeMaxRotationDegrees),
                    directionalInfluence = Mathf.Clamp01(_shakeDirectionalInfluence),
                    seed = _shakeSeed
                });
        }

        private void ConfigureCameraObstruction()
        {
            ConfigureCameraObstruction(null);
        }

        private void ConfigureCameraObstruction(CharacterTestGameplayCameraSettings? gameplaySettings)
        {
            if (_rig == null)
                return;

            IMxCameraUnityObstructionResolver resolver = null;
            if (_obstructionMode == MxCameraUnityObstructionMode.FrameworkPhysicsAabb)
            {
                _frameworkPhysicsObstructionResolver =
                    _frameworkPhysicsObstructionResolver
                    ?? new CharacterTestFrameworkPhysicsCameraObstructionResolver(
                        () => _services != null ? _services.PhysicsRuntime : null,
                        ResolveCameraObstructionFocusOverride);
                resolver = _frameworkPhysicsObstructionResolver;
            }

            _rig.SetObstruction(
                _obstructionMode,
                new MxCameraUnityObstructionSettings
                {
                    radius = Mathf.Max(0.001f, gameplaySettings?.ObstructionRadius ?? _obstructionRadius),
                    padding = Mathf.Max(0f, gameplaySettings?.ObstructionPadding ?? _obstructionPadding),
                    minDistance = Mathf.Max(0f, gameplaySettings?.ObstructionMinDistance ?? _obstructionMinDistance),
                    focusClearance = Mathf.Max(0f, gameplaySettings?.ObstructionFocusClearance ?? _obstructionFocusClearance),
                    recoverySharpness = Mathf.Max(0f, _obstructionRecoverySharpness)
                },
                resolver);
        }

        private CharacterTestCharacterEntity ResolvePrimaryTarget()
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _services.CharacterEntities;
            CharacterTestCharacterEntity first = null;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (first == null)
                    first = entity;

                if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entity;
            }

            return _fallbackToFirstCharacter ? first : null;
        }

        private void BuildTargetSnapshots(CharacterTestCharacterEntity primaryTarget)
        {
            _targets.Clear();
            if (IsAutoFramingMode(_cameraMode))
            {
                CharacterTestCharacterEntity lockedTarget = ResolveLockedTarget(primaryTarget);
                if (primaryTarget != null && lockedTarget != null)
                {
                    AddTargetSnapshot(primaryTarget, LocalPlayerTargetRef, true);
                    AddTargetSnapshot(lockedTarget, AutoTargetRefPrefix + lockedTarget.Id.Value, false);
                }
                else
                {
                    IReadOnlyList<CharacterTestCharacterEntity> entities = _services.CharacterEntities;
                    for (int i = 0; i < entities.Count; i++)
                        AddTargetSnapshot(entities[i], AutoTargetRefPrefix + i, entities[i] == primaryTarget);
                }
            }
            else if (primaryTarget != null)
            {
                AddTargetSnapshot(primaryTarget, LocalPlayerTargetRef, true);
            }

            if (_targets.Count == 0)
            {
                ResetVerticalDeadZone();
                if (ShouldWarnMissingTarget() && !_missingTargetLogged)
                {
                    _missingTargetLogged = true;
                    _logger.Warning(LogCategory, "Camera has no character target; fallback camera state will be used.");
                }
            }
        }

        private bool ShouldWarnMissingTarget()
        {
            return _services != null
                && _services.CharacterEntities != null
                && _services.CharacterEntities.Count > 0;
        }

        private void AddTargetSnapshot(CharacterTestCharacterEntity target, string targetRef, bool isPrimary)
        {
            if (!TryResolveCameraTargetPresentation(target, isPrimary, out CharacterTestCameraTargetPresentation presentation))
                return;
            _targets.Add(new MxCameraTargetSnapshot(
                new MxCameraTargetRef(targetRef),
                MxCameraUnityConversions.ToCameraVector(presentation.Focus),
                MxCameraUnityConversions.ToCameraVector(presentation.Forward),
                MxCameraVector3.Up,
                MxCameraVector3.Zero,
                MxCameraUnityConversions.ToCameraVector(presentation.BoundsCenter),
                MxCameraUnityConversions.ToCameraVector(presentation.BoundsExtents),
                1f,
                isPrimary,
                true,
                _cameraFrame));
        }

        private bool TryResolveCameraTargetPresentation(
            CharacterTestCharacterEntity target,
            bool isPrimary,
            out CharacterTestCameraTargetPresentation presentation)
        {
            presentation = default;
            if (target == null)
                return false;

            CharacterTestGameplayCameraSettings settings = ResolveGameplayCameraSettings(target);
            Quaternion yawRotation = Quaternion.Euler(0f, target.Transform.YawDegrees, 0f);
            Vector3 root = new Vector3(target.Transform.X, target.Transform.Y, target.Transform.Z);
            Vector3 focusBase = root;
            Vector3 forward = yawRotation * Vector3.forward;
            if (settings.ExplicitProfile)
            {
                CharacterTestCharacterUnityViewBridge viewBridge = _services?.CharacterRuntime?.ViewBridge;
                if (viewBridge == null
                    || !viewBridge.TryResolveAbstractBoneAnchor(
                        target.Id.Value,
                        settings.FocusAbstractBoneId,
                        out focusBase,
                        out forward))
                {
                    if (_unresolvedCameraAnchorEntities.Add(target.Id.Value))
                    {
                        _logger.Error(
                            LogCategory,
                            "CHARACTER_CAMERA_ANCHOR_UNRESOLVED packageRoot=" + target.Identity.GeneratedPackageRoot
                            + " entityId=" + target.Id.Value
                            + " characterId=" + target.Identity.CharacterId
                            + " profile=" + settings.ProfileId
                            + " abstractBoneId=" + settings.FocusAbstractBoneId
                            + " action=stop-camera-target-update");
                    }
                    return false;
                }
                _unresolvedCameraAnchorEntities.Remove(target.Id.Value);
            }

            focusBase = ResolveCameraTargetPosition(target, isPrimary, focusBase);
            Vector3 boundsCenter = root + yawRotation * settings.FramingBoundsCenter;
            Vector3 boundsExtents = settings.ExplicitProfile
                ? RotateExtentsToWorld(settings.FramingBoundsExtents, yawRotation)
                : SanitizeExtents(settings.FramingBoundsExtents);
            presentation = new CharacterTestCameraTargetPresentation(
                target,
                focusBase,
                forward,
                root,
                boundsCenter,
                boundsExtents,
                settings);
            return true;
        }

        private CharacterTestGameplayCameraSettings ResolveGameplayCameraSettings(
            CharacterTestCharacterEntity target)
        {
            if (target != null
                && _services?.CharacterRuntime != null
                && _services.CharacterRuntime.TryGetCharacterDefinition(
                    target.Identity.CharacterId,
                    out CharacterTestCharacterDefinition definition)
                && definition.GameplayCameraProfile != null)
                return CharacterTestGameplayCameraSettings.FromProfile(definition.GameplayCameraProfile);

            return new CharacterTestGameplayCameraSettings(
                false,
                "character-test.legacy",
                string.Empty,
                _focusOffset,
                Vector3.zero,
                _boundsExtents,
                _distance,
                _minDistance,
                _maxDistance,
                _fieldOfView,
                _pitch,
                _yawOffset,
                _targetPadding,
                _minDistance,
                _maxDistance,
                _lockedTargetScreenY,
                _targetPadding,
                _obstructionRadius,
                _obstructionPadding,
                _obstructionMinDistance,
                _obstructionFocusClearance);
        }

        private static Vector3 RotateExtentsToWorld(Vector3 extents, Quaternion rotation)
        {
            Vector3 safe = SanitizeExtents(extents);
            Matrix4x4 matrix = Matrix4x4.Rotate(rotation);
            return new Vector3(
                Mathf.Abs(matrix.m00) * safe.x + Mathf.Abs(matrix.m01) * safe.y + Mathf.Abs(matrix.m02) * safe.z,
                Mathf.Abs(matrix.m10) * safe.x + Mathf.Abs(matrix.m11) * safe.y + Mathf.Abs(matrix.m12) * safe.z,
                Mathf.Abs(matrix.m20) * safe.x + Mathf.Abs(matrix.m21) * safe.y + Mathf.Abs(matrix.m22) * safe.z);
        }

        private Vector3 ResolveCameraTargetPosition(
            CharacterTestCharacterEntity target,
            bool isPrimary,
            Vector3 position)
        {
            if (!ShouldApplyVerticalDeadZone(target, isPrimary))
            {
                if (isPrimary && target != null)
                    ResetVerticalDeadZone(target.Id.Value, position.y);

                return position;
            }

            float heightOffset = position.y - target.Transform.Y;
            position.y = ResolveVerticalDeadZoneY(target) + heightOffset;
            return position;
        }

        private bool ShouldApplyVerticalDeadZone(CharacterTestCharacterEntity target, bool isPrimary)
        {
            if (!_enableVerticalDeadZone || !isPrimary || target == null)
                return false;

            if (_cameraMode != CharacterTestCameraMode.ThirdPersonControlled)
                return false;

            if (ResolveLockedTarget(target) != null)
                return false;

            return _verticalDeadZoneUp > 0f || _verticalDeadZoneDown > 0f;
        }

        private float ResolveVerticalDeadZoneY(CharacterTestCharacterEntity target)
        {
            float targetY = target.Transform.Y;
            if (!_hasPrimaryCameraTargetY || _primaryCameraTargetEntityId != target.Id.Value)
            {
                ResetVerticalDeadZone(target.Id.Value, targetY);
                return _primaryCameraTargetY;
            }

            float up = Mathf.Max(0f, _verticalDeadZoneUp);
            float down = Mathf.Max(0f, _verticalDeadZoneDown);
            float nextY = _primaryCameraTargetY;
            if (targetY > nextY + up)
                nextY = targetY - up;
            else if (targetY < nextY - down)
                nextY = targetY + down;
            else if (target.IsGrounded)
                nextY = SmoothVerticalTargetBackToGround(nextY, targetY);

            _primaryCameraTargetY = nextY;
            return _primaryCameraTargetY;
        }

        private float SmoothVerticalTargetBackToGround(float currentY, float targetY)
        {
            float sharpness = Mathf.Max(0f, _verticalGroundedReturnSharpness);
            if (sharpness <= 0f)
                return targetY;

            float t = 1f - Mathf.Exp(-sharpness * _presentationDeltaTime);
            float nextY = Mathf.Lerp(currentY, targetY, t);
            return Mathf.Abs(nextY - targetY) <= 0.001f ? targetY : nextY;
        }

        private void ResetVerticalDeadZone(int entityId, float targetY)
        {
            _hasPrimaryCameraTargetY = true;
            _primaryCameraTargetEntityId = entityId;
            _primaryCameraTargetY = targetY;
        }

        private void ResetVerticalDeadZone()
        {
            _hasPrimaryCameraTargetY = false;
            _primaryCameraTargetEntityId = 0;
            _primaryCameraTargetY = 0f;
        }

        private void UpdateProfile(CharacterTestCharacterEntity target, MxCameraProfileDefinition profile)
        {
            CharacterTestGameplayCameraSettings settings = ResolveGameplayCameraSettings(target);
            CharacterTestCameraTargetPresentation primaryPresentation = default;
            if (target != null && !TryResolveCameraTargetPresentation(target, true, out primaryPresentation))
                return;
            ApplyGameplayCameraSettings(settings);

            bool autoFramingMode = IsAutoFramingMode(_cameraMode);
            bool runtimeAiLabAutoFraming = autoFramingMode && _runtimeAiLabAutoFraming;
            CharacterTestCharacterEntity lockedTarget = !autoFramingMode ? ResolveLockedTarget(target) : null;
            CharacterTestCameraTargetPresentation lockedPresentation = default;
            bool lockedThirdPerson = lockedTarget != null
                && TryResolveCameraTargetPresentation(lockedTarget, false, out lockedPresentation);
            CharacterTestLockedThirdPersonCameraComposition lockedComposition = lockedThirdPerson
                ? CharacterTestLockedThirdPersonCameraComposer.Compose(
                    primaryPresentation,
                    lockedPresentation,
                    CreateLockedThirdPersonCompositionSettings(settings))
                : default;
            float desiredDistance = lockedThirdPerson
                ? lockedComposition.Distance
                : runtimeAiLabAutoFraming
                    ? RuntimeAiLabAutoMinDistance
                    : settings.Distance;
            float minDistance = lockedThirdPerson
                    ? Mathf.Max(0.01f, desiredDistance)
                    : runtimeAiLabAutoFraming
                        ? RuntimeAiLabAutoMinDistance
                    : Mathf.Max(0.01f, Mathf.Min(settings.MinDistance, desiredDistance));
            float maxDistance = lockedThirdPerson
                    ? minDistance
                    : runtimeAiLabAutoFraming
                        ? RuntimeAiLabAutoMaxDistance
                    : Mathf.Max(minDistance, settings.MaxDistance);
            float distance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);
            float yaw = autoFramingMode
                    ? _autoYaw
                    : lockedThirdPerson
                        ? lockedComposition.Yaw
                        : ResolveThirdPersonYaw(target);
            float pitch = autoFramingMode
                    ? _tacticalBattleAutoFraming ? TacticalBattlePitch : _autoPitch
                    : lockedThirdPerson
                        ? lockedComposition.Pitch
                        : _controlledPitch;
            Vector3 worldOffset = lockedThirdPerson
                ? lockedComposition.WorldOffset
                : settings.ExplicitProfile && target != null
                    ? ResolvePhysicalObstructionFocus(primaryPresentation) - primaryPresentation.BoundsCenter
                    : settings.FocusOffset;
            if (autoFramingMode && _tacticalBattleAutoFraming && !runtimeAiLabAutoFraming)
                worldOffset.y = TacticalBattleFocusOffsetY;
            if (lockedThirdPerson)
                SetCameraObstructionFocusOverride(ResolvePhysicalObstructionFocus(primaryPresentation));
            else
                ResetCameraObstructionFocusOverride();

            if (lockedThirdPerson)
                yaw = AlignAngleToReference(yaw, _controlledYaw);

            if (autoFramingMode || lockedThirdPerson)
            {
                _profileSmoother.Smooth(
                    _cameraTransitionSharpness,
                    _presentationDeltaTime,
                    ref yaw,
                    ref pitch,
                    ref distance,
                    ref worldOffset);
            }
            else
                _profileSmoother.Snap(yaw, pitch, distance, worldOffset);

            if (lockedThirdPerson)
            {
                SyncControlledViewToActiveProfile(yaw, pitch);
                minDistance = Mathf.Max(0.01f, distance);
                maxDistance = minDistance;
            }

            profile.ProfileId = new MxCameraProfileId(ResolveProfileId(_cameraMode));
            profile.Mode = autoFramingMode
                    ? MxCameraMode.GroupFollowPerspective
                    : MxCameraMode.Follow;
            profile.Priority = 0;
            float runtimeAiLabFramingDistance = runtimeAiLabAutoFraming
                ? ResolveRuntimeAiLabAutoFramingDistance(
                    Mathf.Clamp(settings.FieldOfView, 20f, 80f),
                    minDistance,
                    maxDistance)
                : 0f;
            profile.LocalOffset = runtimeAiLabAutoFraming
                ? new MxCameraVector3(
                    runtimeAiLabFramingDistance * RuntimeAiLabAutoHorizontalDistanceRatio,
                    runtimeAiLabFramingDistance * RuntimeAiLabAutoVerticalDistanceRatio,
                    0f)
                : MxCameraVector3.Zero;
            profile.WorldOffset = MxCameraUnityConversions.ToCameraVector(
                worldOffset);
            profile.Distance = distance;
            profile.MinDistance = minDistance;
            profile.MaxDistance = maxDistance;
            profile.FieldOfView = _tacticalBattleAutoFraming
                ? TacticalBattleFieldOfView
                : Mathf.Clamp(settings.FieldOfView, 20f, 80f);
            profile.MinFieldOfView = 20f;
            profile.MaxFieldOfView = 80f;
            profile.OrthographicSize = 6f;
            profile.MinOrthographicSize = 2f;
            profile.MaxOrthographicSize = 24f;
            profile.PositionSmoothing = 0f;
            profile.RotationSmoothing = 0f;
            profile.ZoomSmoothing = 0f;
            profile.TargetPadding = Mathf.Max(
                0f,
                runtimeAiLabAutoFraming
                    ? RuntimeAiLabAutoTargetPadding
                    : autoFramingMode
                        ? _autoTargetPadding
                        : lockedThirdPerson
                            ? settings.LockedTargetPadding
                            : settings.TargetPadding);
            profile.TargetLostGraceFrames = 8;
            profile.BoundsPolicy = MxCameraBoundsPolicy.NoClamp;
            profile.MaxTargetRadius = Mathf.Max(
                1f,
                autoFramingMode
                        ? _autoMaxTargetRadius
                        : Mathf.Max(8f, settings.FramingBoundsExtents.magnitude * 2f));
            profile.ShakeLimit = 0.35f;
            profile.Pitch = Mathf.Clamp(pitch, -85f, 85f);
            profile.Yaw = yaw;
            profile.FallbackPosition = new MxCameraVector3(0f, worldOffset.y + distance * 0.45f, -distance);
            profile.FallbackFocus = MxCameraUnityConversions.ToCameraVector(worldOffset);
            profile.DiagnosticTags = NoDiagnosticTags;
        }

        private void ApplyGameplayCameraSettings(CharacterTestGameplayCameraSettings settings)
        {
            string profileId = settings.ExplicitProfile ? settings.ProfileId : "character-test.legacy";
            if (string.Equals(_activeGameplayCameraProfileId, profileId, StringComparison.Ordinal))
                return;

            _activeGameplayCameraProfileId = profileId;
            _controlledYaw = settings.YawOffset;
            _controlledPitch = Mathf.Clamp(settings.Pitch, -80f, 80f);
            ConfigureCameraObstruction(settings);
        }

        private float ResolveRuntimeAiLabAutoFramingDistance(
            float fieldOfView,
            float minDistance,
            float maxDistance)
        {
            if (_targets.Count == 0)
                return minDistance;

            float totalWeight = 0f;
            MxCameraVector3 center = MxCameraVector3.Zero;
            for (int i = 0; i < _targets.Count; i++)
            {
                MxCameraTargetSnapshot target = _targets[i];
                float weight = target.Weight > 0f ? target.Weight : 1f;
                totalWeight += weight;
                center += target.BoundsCenter * weight;
            }
            center /= Mathf.Max(0.0001f, totalWeight);

            float radius = 0f;
            for (int i = 0; i < _targets.Count; i++)
            {
                MxCameraTargetSnapshot target = _targets[i];
                radius = Mathf.Max(
                    radius,
                    MxCameraVector3.Distance(
                        center,
                        target.BoundsCenter + target.BoundsExtents));
            }

            float halfFovRadians = Mathf.Clamp(fieldOfView, 1f, 179f)
                * Mathf.Deg2Rad * 0.5f;
            float requiredDistance = radius / Mathf.Max(0.01f, Mathf.Tan(halfFovRadians));
            return Mathf.Clamp(requiredDistance, minDistance, maxDistance);
        }

        private float ResolveThirdPersonYaw(CharacterTestCharacterEntity target)
        {
            CharacterTestCharacterEntity lockedTarget = ResolveLockedTarget(target);
            if (target != null && lockedTarget != null)
            {
                Vector2 from = new Vector2(target.Transform.X, target.Transform.Z);
                Vector2 to = new Vector2(lockedTarget.Transform.X, lockedTarget.Transform.Z);
                Vector2 direction = to - from;
                if (direction.sqrMagnitude > 0.0001f)
                    return Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            }

            if (_followTargetYaw && target != null && _services != null && _services.TargetLockEnabled)
                return target.Transform.YawDegrees + _controlledYaw;

            return _controlledYaw;
        }

        private string ResolveProfileId(CharacterTestCameraMode mode)
        {
            if (!string.IsNullOrWhiteSpace(_profileId))
            {
                return mode == CharacterTestCameraMode.MultiTargetAuto ? _profileId + ".auto" : _profileId;
            }

            return mode == CharacterTestCameraMode.MultiTargetAuto
                ? "character-test.multi-target-auto"
                : "character-test.third-person";
        }

        private CharacterTestLockedThirdPersonCameraCompositionSettings CreateLockedThirdPersonCompositionSettings(
            CharacterTestGameplayCameraSettings settings)
        {
            return new CharacterTestLockedThirdPersonCameraCompositionSettings(
                settings.Distance,
                settings.LockedMinDistance,
                settings.LockedMaxDistance,
                settings.FieldOfView,
                ResolveCameraAspectRatio(),
                settings.LockedTargetScreenY,
                settings.LockedTargetPadding,
                _controlledYaw,
                settings.Pitch);
        }

        private float ResolveCameraAspectRatio()
        {
            if (_camera != null && _camera.aspect > 0.1f)
                return _camera.aspect;

            return Screen.height > 0
                ? Mathf.Max(0.1f, (float)Screen.width / Screen.height)
                : 16f / 9f;
        }

        private void SyncControlledViewToActiveProfile(float yaw, float pitch)
        {
            _controlledYaw = yaw;
            _controlledPitch = Mathf.Clamp(
                pitch,
                Mathf.Min(_minPitch, _maxPitch),
                Mathf.Max(_minPitch, _maxPitch));
        }

        private Vector3? ResolveCameraObstructionFocusOverride()
        {
            return _hasObstructionFocusOverride
                ? _obstructionFocusOverride
                : (Vector3?)null;
        }

        private static Vector3 ResolvePhysicalObstructionFocus(
            in CharacterTestCameraTargetPresentation presentation)
        {
            if (!presentation.Settings.ExplicitProfile)
                return presentation.RootPosition + presentation.Settings.FocusOffset;

            float yaw = presentation.Entity != null
                ? presentation.Entity.Transform.YawDegrees
                : 0f;
            return presentation.Focus
                + Quaternion.Euler(0f, yaw, 0f) * presentation.Settings.FocusOffset;
        }

        private void SetCameraObstructionFocusOverride(Vector3 focus)
        {
            if (!_hasObstructionFocusOverride)
                _frameworkPhysicsObstructionResolver?.Reset();
            _obstructionFocusOverride = focus;
            _hasObstructionFocusOverride = true;
        }

        private void ResetCameraObstructionFocusOverride()
        {
            bool hadOverride = _hasObstructionFocusOverride;
            _hasObstructionFocusOverride = false;
            _obstructionFocusOverride = Vector3.zero;
            if (hadOverride)
                _frameworkPhysicsObstructionResolver?.Reset();
        }

        private static float AlignAngleToReference(float angle, float reference)
        {
            return reference + Mathf.DeltaAngle(reference, angle);
        }

        private void EnsureCursorController()
        {
            if (_cursorController != null)
                return;

            _cursorController = new MxCursorController();
            _ownsCursorController = true;
        }

        private void EnsureGameplayCursorScope()
        {
            if (_gameplayCursorScope != null)
                return;

            EnsureCursorController();
            _gameplayCursorScope = _cursorController.Push(MxCursorRequest.Captured(CameraCursorOwner));
        }

        private void ReleaseGameplayCursorScope()
        {
            System.IDisposable scope = _gameplayCursorScope;
            _gameplayCursorScope = null;
            scope?.Dispose();
        }

        private void EnsureDebugCursorScope()
        {
            if (_debugCursorScope != null)
                return;

            EnsureCursorController();
            _debugCursorScope = _cursorController.Push(MxCursorRequest.Released(DebugCursorOwner));
        }

        private void ReleaseDebugCursorScope()
        {
            System.IDisposable scope = _debugCursorScope;
            _debugCursorScope = null;
            scope?.Dispose();
        }

        private void ReleaseCursorScopes()
        {
            ReleaseDebugCursorScope();
            ReleaseGameplayCursorScope();
        }

        private void ReleaseOwnedCursorController()
        {
            if (_ownsCursorController)
            {
                (_cursorController as System.IDisposable)?.Dispose();
            }

            _cursorController = null;
            _ownsCursorController = false;
        }

        private void ToggleTargetLock(CharacterTestCharacterEntity primaryTarget)
        {
            if (!_enableTargetLock || primaryTarget == null || _services == null)
                return;

            RuntimeCommandValidationResult result = _services.EnqueueToggleTargetLock(primaryTarget.Id.Value);
            if (!result.Success)
                _logger.Warning(LogCategory, "Target lock command rejected: " + result.Error.Message);
        }

        private void HandleTargetLockViewSwitchInput(
            CharacterTestCharacterEntity primaryTarget,
            CharacterTestCharacterEntity lockedTarget,
            Vector2 look)
        {
            if (!_enableTargetLock || primaryTarget == null || lockedTarget == null || _services == null)
            {
                ResetTargetLockViewSwitchInput();
                return;
            }

            if (_targetLockViewSwitchCooldownRemaining > 0f)
            {
                _targetLockViewSwitchAccumulator = Vector2.zero;
                return;
            }

            Vector2 contribution = new Vector2(
                look.x * _mouseYawSensitivity,
                look.y * _mousePitchSensitivity);
            float contributionSqrMagnitude = contribution.sqrMagnitude;
            float inputDeadzone = Mathf.Max(TargetLockViewSwitchEpsilon, _targetLockViewSwitchInputDeadzoneDegrees);
            if (contributionSqrMagnitude <= inputDeadzone * inputDeadzone)
            {
                _targetLockViewSwitchAccumulator = Vector2.zero;
                return;
            }

            _targetLockViewSwitchAccumulator += contribution;
            float threshold = Mathf.Max(0.001f, _targetLockViewSwitchThresholdDegrees);
            if (_targetLockViewSwitchAccumulator.sqrMagnitude < threshold * threshold)
                return;

            Vector2 direction = _targetLockViewSwitchAccumulator;
            float magnitude = Mathf.Sqrt(direction.sqrMagnitude);
            if (magnitude > TargetLockViewSwitchEpsilon)
                direction /= magnitude;

            CharacterTestCharacterEntity target = FindTargetLockViewSwitchTarget(
                primaryTarget,
                lockedTarget,
                direction);
            if (target == null)
            {
                _targetLockViewSwitchAccumulator = Vector2.zero;
                _targetLockViewSwitchCooldownRemaining = Mathf.Max(0f, _targetLockViewSwitchCooldownSeconds);
                return;
            }

            EnqueueTargetLockViewSwitch(primaryTarget, target.Id.Value, direction);
            _targetLockViewSwitchAccumulator = Vector2.zero;
            _targetLockViewSwitchCooldownRemaining = Mathf.Max(0f, _targetLockViewSwitchCooldownSeconds);
        }

        private CharacterTestCharacterEntity FindTargetLockViewSwitchTarget(
            CharacterTestCharacterEntity primaryTarget,
            CharacterTestCharacterEntity lockedTarget,
            Vector2 direction)
        {
            if (primaryTarget == null || lockedTarget == null || _services == null)
                return null;

            UnityCamera camera = ResolveTargetLockViewSwitchCamera();
            if (camera == null || direction.sqrMagnitude <= TargetLockViewSwitchEpsilon)
                return null;

            Vector3 lockedViewport3 = camera.WorldToViewportPoint(ResolveTargetLockViewSwitchAnchor(lockedTarget));
            if (lockedViewport3.z <= 0f)
                return null;

            Vector2 lockedViewport = new Vector2(lockedViewport3.x, lockedViewport3.y);
            IReadOnlyList<CharacterTestCharacterEntity> entities = _services.CharacterEntities;
            CharacterTestCharacterEntity best = null;
            float bestDirectionDot = float.NegativeInfinity;
            float bestViewportSqrDistance = float.MaxValue;
            float bestWorldSqrDistance = float.MaxValue;
            int bestEntityId = int.MaxValue;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity candidate = entities[i];
                if (!IsTargetLockViewSwitchCandidate(primaryTarget, lockedTarget, candidate))
                    continue;

                Vector3 candidateViewport3 = camera.WorldToViewportPoint(ResolveTargetLockViewSwitchAnchor(candidate));
                if (candidateViewport3.z <= 0f)
                    continue;

                Vector2 viewportDelta = new Vector2(
                    candidateViewport3.x - lockedViewport.x,
                    candidateViewport3.y - lockedViewport.y);
                float viewportSqrDistance = viewportDelta.sqrMagnitude;
                if (viewportSqrDistance <= TargetLockViewSwitchEpsilon)
                    continue;

                float viewportDistance = Mathf.Sqrt(viewportSqrDistance);
                float directionDot = Vector2.Dot(direction, viewportDelta) / viewportDistance;
                if (directionDot <= 0f)
                    continue;

                float worldSqrDistance = ResolveTargetLockViewSwitchSqrDistance(primaryTarget, candidate);
                int candidateEntityId = candidate.Id.Value;
                bool select = best == null
                    || directionDot > bestDirectionDot + TargetLockViewSwitchEpsilon
                    || (Mathf.Abs(directionDot - bestDirectionDot) <= TargetLockViewSwitchEpsilon
                        && (viewportSqrDistance < bestViewportSqrDistance - TargetLockViewSwitchEpsilon
                            || (Mathf.Abs(viewportSqrDistance - bestViewportSqrDistance) <= TargetLockViewSwitchEpsilon
                                && (worldSqrDistance < bestWorldSqrDistance - TargetLockViewSwitchEpsilon
                                    || (Mathf.Abs(worldSqrDistance - bestWorldSqrDistance) <= TargetLockViewSwitchEpsilon
                                        && candidateEntityId < bestEntityId)))));
                if (!select)
                    continue;

                best = candidate;
                bestDirectionDot = directionDot;
                bestViewportSqrDistance = viewportSqrDistance;
                bestWorldSqrDistance = worldSqrDistance;
                bestEntityId = candidateEntityId;
            }

            return best;
        }

        private UnityCamera ResolveTargetLockViewSwitchCamera()
        {
            if (_camera == null)
                _camera = UnityCamera.main;
            return _camera;
        }

        private Vector3 ResolveTargetLockViewSwitchAnchor(CharacterTestCharacterEntity entity)
        {
            if (entity != null
                && _services != null
                && _services.TargetLockEnabled
                && _services.LockedTargetEntityId == entity.Id.Value
                && _services.TryGetLockedCombatTargetSample(
                    out CharacterTestLockedHurtboxDistanceSample lockedSample)
                && lockedSample.HasDistance)
            {
                return new Vector3(
                    (float)(lockedSample.AimPoint.X.RawValue / (double)Fix64.Scale),
                    (float)(lockedSample.AimPoint.Y.RawValue / (double)Fix64.Scale),
                    (float)(lockedSample.AimPoint.Z.RawValue / (double)Fix64.Scale));
            }
            return new Vector3(
                entity.Transform.X,
                entity.Transform.Y + TargetLockViewSwitchAnchorHeightMeters,
                entity.Transform.Z);
        }

        private bool IsTargetLockViewSwitchCandidate(
            CharacterTestCharacterEntity primaryTarget,
            CharacterTestCharacterEntity lockedTarget,
            CharacterTestCharacterEntity candidate)
        {
            if (primaryTarget == null || lockedTarget == null || candidate == null)
                return false;

            int candidateId = candidate.Id.Value;
            if (candidateId == primaryTarget.Id.Value || candidateId == lockedTarget.Id.Value)
                return false;
            return IsTargetLockCandidate(primaryTarget, candidate);
        }

        private float ResolveTargetLockViewSwitchSqrDistance(
            CharacterTestCharacterEntity primaryTarget,
            CharacterTestCharacterEntity target)
        {
            CharacterTestPerceptionFrameCache perception =
                _services?.Modules?.CharacterRuntime?.PerceptionFrameCache;
            if (perception != null
                && perception.TryGetCommittedPlanarDistanceSquaredMeters(
                    primaryTarget,
                    target,
                    out float committedSqrDistance))
            {
                return committedSqrDistance;
            }

            float dx = target.Transform.X - primaryTarget.Transform.X;
            float dz = target.Transform.Z - primaryTarget.Transform.Z;
            return dx * dx + dz * dz;
        }

        private void EnqueueTargetLockViewSwitch(
            CharacterTestCharacterEntity primaryTarget,
            int requestedTargetEntityId,
            Vector2 direction)
        {
            RuntimeCommandValidationResult result = _services.EnqueueRuntimeCommand(
                CharacterTestRuntimeCommandFactory.ToggleTargetLockViewSwitch(
                    _services.CurrentFrame,
                    CharacterTestRuntimeCommandSources.CameraInput,
                    primaryTarget.Id.Value,
                    requestedTargetEntityId,
                    direction,
                    new RuntimeTraceIdentity(90865, 0, _services.CurrentFrame.Value)));
            if (!result.Success)
                _logger.Warning(LogCategory, "Target lock view switch command rejected: " + result.Error.Message);
        }

        private void TickTargetLockViewSwitchCooldown()
        {
            if (_targetLockViewSwitchCooldownRemaining <= 0f)
                return;

            float deltaTime = _presentationDeltaTime > 0f
                ? _presentationDeltaTime
                : 1f / CharacterTestDeterministicUnits.DefaultTickRate;
            _targetLockViewSwitchCooldownRemaining = Mathf.Max(
                0f,
                _targetLockViewSwitchCooldownRemaining - deltaTime);
        }

        private void ResetTargetLockViewSwitchInput()
        {
            _targetLockViewSwitchAccumulator = Vector2.zero;
            _targetLockViewSwitchCooldownRemaining = 0f;
        }

        private CharacterTestCharacterEntity ResolveLockedTarget(CharacterTestCharacterEntity primaryTarget)
        {
            if (_services == null || !_services.TargetLockEnabled || _services.LockedTargetEntityId <= 0 || primaryTarget == null)
                return null;

            IReadOnlyList<CharacterTestCharacterEntity> entities = _services.CharacterEntities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Id.Value == _services.LockedTargetEntityId && IsTargetLockCandidate(primaryTarget, entity))
                    return entity;
            }

            return null;
        }

        private bool IsTargetLockCandidate(
            CharacterTestCharacterEntity primaryTarget,
            CharacterTestCharacterEntity candidate)
        {
            if (primaryTarget == null || candidate == null)
                return false;
            if (candidate.Id.Value == primaryTarget.Id.Value)
                return false;
            if (candidate.Team.TeamId == primaryTarget.Team.TeamId)
                return false;
            if (!candidate.BattleState.CountsAsAlive || !candidate.BattleState.CanReceiveHpDamage)
                return false;

            float maxDistance = Mathf.Max(0f, _targetLockMaxDistance);
            return ResolveTargetLockViewSwitchSqrDistance(primaryTarget, candidate) <= maxDistance * maxDistance;
        }

        private CharacterTestCharacterEntity FindNearestTarget(CharacterTestCharacterEntity primaryTarget)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _services.CharacterEntities;
            CharacterTestCharacterEntity nearest = null;
            float nearestSqrDistance = Mathf.Max(0f, _targetLockMaxDistance) * Mathf.Max(0f, _targetLockMaxDistance);

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == primaryTarget)
                    continue;

                float sqrDistance = ResolveTargetLockViewSwitchSqrDistance(primaryTarget, entity);
                if (sqrDistance <= nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = entity;
                }
            }

            return nearest;
        }

        private Vector2 ResolveCameraForward()
        {
            MxCameraStateValue state = CurrentStateValue;
            Vector2 forward = new Vector2(state.ViewForward.X, state.ViewForward.Z);

            if (forward.sqrMagnitude > 0.0001f)
                return forward.normalized;

            float yawRadians = ResolveThirdPersonYaw(null) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(yawRadians), Mathf.Cos(yawRadians)).normalized;
        }

        private InputSnapshot ReadInputSnapshot()
        {
            if (_inputProvider == null)
            {
                if (!_missingInputLogged)
                {
                    _missingInputLogged = true;
                    _logger.Warning(LogCategory, "Input provider is not available for camera control.");
                }

                return InputSnapshot.Empty;
            }
            bool gameplayEnabled = _inputProvider.IsContextEnabled(InputContext.Gameplay);
            InputSnapshot snapshot = _inputProvider.Snapshot;

            return gameplayEnabled ? snapshot : InputSnapshot.Empty;
        }

        private void SyncRuntimeCameraState()
        {
            if (_services == null)
                return;

            CharacterTestCameraMode runtimeMode = _services.CameraMode;
            if (_cameraMode == runtimeMode)
                return;

            _cameraMode = runtimeMode;
            if (_cameraMode != CharacterTestCameraMode.ThirdPersonControlled)
                ResetCameraObstructionFocusOverride();

            ApplyCursorPolicy();
        }

        private static bool IsAutoFramingMode(CharacterTestCameraMode mode)
        {
            return mode == CharacterTestCameraMode.MultiTargetAuto;
        }

        private static Vector3 SanitizeExtents(Vector3 extents)
        {
            return new Vector3(
                Mathf.Max(0.01f, extents.x),
                Mathf.Max(0.01f, extents.y),
                Mathf.Max(0.01f, extents.z));
        }
    }
}
