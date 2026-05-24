using MxFramework.CharacterControl;
using MxFramework.CharacterControl.Input;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Input;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterRuntimeControllerBinding))]
    [AddComponentMenu("MxFramework/Character/Runtime Input Motion Controller")]
    public sealed class CharacterRuntimeInputMotionController : MonoBehaviour
    {
        private const float FixedStepEpsilon = 0.0001f;
        private static readonly CombatEntityId PreviewGroundEntityId = new CombatEntityId(900001);
        private static readonly CombatBodyId PreviewGroundBodyId = new CombatBodyId(900001);
        private static readonly CombatColliderId PreviewGroundColliderId = new CombatColliderId(1);

        [SerializeField] private InputService _inputService;
        [SerializeField] private bool _enableInputMotion = true;
        [SerializeField] private float _stepRate = 60f;
        [SerializeField] private int _maxStepsPerUpdate = 4;
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private float _moveSpeedScale = 1f;
        [SerializeField] private bool _enableGravity = true;
        [SerializeField] private bool _enablePreviewGround = true;
        [SerializeField] private float _gravityPerSecond = -30f;
        [SerializeField] private float _jumpSpeed = 10f;
        [SerializeField] private float _maxFallSpeed = -50f;
        [SerializeField] private Vector3 _characterHalfExtents = new Vector3(0.4f, 0.9f, 0.4f);
        [SerializeField] private float _previewGroundY;
        [SerializeField] private Vector3 _previewGroundHalfExtents = new Vector3(24f, 1f, 24f);
        [SerializeField] private int _previewGroundLayer = 1;
        [SerializeField] private bool _rotateToMoveDirection = true;
        [SerializeField] private bool _interpolateVisualMotion = true;

        private CharacterRuntimeControllerBinding _binding;
        private IInputProvider _inputProvider;
        private FakeInputProvider _motionInputProvider;
        private InputCharacterCommandSource _commandSource;
        private InputCharacterCommandSourceOptions _commandSourceOptions;
        private CharacterMotionResolver _motionResolver;
        private CombatPhysicsWorld _physicsWorld;
        private CombatMotionState _motionState;
        private float _accumulator;
        private long _frame;
        private bool _initialized;
        private CharacterMotionResult _lastMotionResult;
        private Vector2 _lastLocalMove;
        private float _lastSpeed01;
        private float _lastPlanarSpeed;
        private bool _lastSourceJumpPressed;
        private Vector3 _previousRootPosition;
        private Vector3 _currentRootPosition;
        private Quaternion _previousRootRotation = Quaternion.identity;
        private Quaternion _currentRootRotation = Quaternion.identity;
        private bool _hasVisualMotionState;
        private bool _deferVisualMotionInterpolation;

        public bool IsInitialized => _initialized;
        public bool EnableInputMotion
        {
            get => _enableInputMotion;
            set => _enableInputMotion = value;
        }

        public bool RotateToMoveDirection
        {
            get => _rotateToMoveDirection;
            set => _rotateToMoveDirection = value;
        }

        public float MoveSpeedScale
        {
            get => _moveSpeedScale;
            set
            {
                _moveSpeedScale = Mathf.Max(0f, value);
                if (_commandSourceOptions != null)
                    _commandSourceOptions.MoveSpeedScale = ToFix64(_moveSpeedScale);
            }
        }

        public CharacterMotionResult LastMotionResult => _lastMotionResult;
        public long CurrentFrame => _frame;
        public Vector2 LastLocalMove => _lastLocalMove;
        public float LastSpeed01 => _lastSpeed01;
        public float LastPlanarSpeed => _lastPlanarSpeed;
        public bool UsesPhysicsWorld => _physicsWorld != null;

        private void Awake()
        {
            _binding = GetComponent<CharacterRuntimeControllerBinding>();
        }

        private void Start()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            if (!_enableInputMotion)
                return;

            EnsureInitialized();
            if (!_initialized)
                return;

            SyncBufferedInput();

            float stepRate = Mathf.Max(1f, _stepRate);
            float fixedDelta = 1f / stepRate;
            _accumulator += Mathf.Max(0f, Time.deltaTime);

            int maxSteps = Mathf.Max(1, _maxStepsPerUpdate);
            int steps = 0;
            _deferVisualMotionInterpolation = _interpolateVisualMotion;
            try
            {
                while (_accumulator + FixedStepEpsilon >= fixedDelta && steps < maxSteps)
                {
                    StepFrame();
                    _accumulator -= fixedDelta;
                    steps++;
                }
            }
            finally
            {
                _deferVisualMotionInterpolation = false;
            }

            if (steps == maxSteps && _accumulator >= fixedDelta)
                _accumulator = 0f;

            ApplyVisualMotionInterpolation(fixedDelta);
        }

        public void ConfigureInputProvider(IInputProvider inputProvider)
        {
            _inputProvider = inputProvider;
            _commandSource = null;
            _initialized = false;
        }

        public bool EnsureInitialized()
        {
            if (_initialized)
                return true;

            if (_binding == null)
                _binding = GetComponent<CharacterRuntimeControllerBinding>();
            if (_binding == null || _binding.Initialize() == null)
                return false;

            _inputProvider = _inputProvider ?? InputProviderResolver.ResolveOrCreateDefault(this, _inputService);
            if (_inputProvider == null)
                return false;

            _motionInputProvider = new FakeInputProvider();
            CombatMotionConfig motionConfig = CreateMotionConfig();
            _motionResolver = new CharacterMotionResolver(new CombatKinematicMotor(motionConfig));
            _physicsWorld = _enableGravity || _enablePreviewGround ? new CombatPhysicsWorld() : null;
            _commandSourceOptions = new InputCharacterCommandSourceOptions
            {
                SourceId = 1,
                MoveSpeedScale = ToFix64(Mathf.Max(0f, _moveSpeedScale)),
                TracePrefix = "characterstudio-runtime-input"
            };
            _commandSource = new InputCharacterCommandSource(_motionInputProvider, _commandSourceOptions);
            FixVector3 motionPosition = ToFixVector3(RootToMotionCenter(transform.position));
            _motionState = new CombatMotionState(
                CombatFrame.Zero,
                motionPosition,
                FixVector3.Zero,
                grounded: _enablePreviewGround && transform.position.y <= _previewGroundY + 0.01f,
                FixVector3.Zero,
                _enablePreviewGround ? CombatMotionCollisionFlags.Grounded : CombatMotionCollisionFlags.None);
            EnsurePhysicsWorldBody();
            _frame = 0L;
            _accumulator = 0f;
            _lastLocalMove = Vector2.zero;
            _lastSpeed01 = 0f;
            _lastPlanarSpeed = 0f;
            _lastSourceJumpPressed = false;
            ResetVisualMotionState(transform.position, transform.rotation);
            SyncBufferedInput();
            _initialized = true;
            return true;
        }

        public bool StepFrame()
        {
            if (!EnsureInitialized())
                return false;

            SyncBufferedInput();

            long frameValue = _motionInputProvider == null
                ? _frame
                : System.Math.Max(_frame, _motionInputProvider.Commands.CurrentFrame);
            var runtimeFrame = new RuntimeFrame(frameValue);
            if (!_commandSource.TryGetCommand(runtimeFrame, _binding.EntityRef, out CharacterCommand command))
            {
                _frame = frameValue + 1L;
                return false;
            }

            EnsurePhysicsWorldBody();
            _motionState = _motionState.WithFrame(new CombatFrame(checked((int)frameValue)));
            _lastMotionResult = _motionResolver.Step(command, _binding.StateMachine, _motionState, _physicsWorld);
            _motionState = _lastMotionResult.StepResult.State;
            ApplyMotion(command);
            _frame = frameValue + 1L;
            return true;
        }

        public void WarpRootPosition(Vector3 rootPosition)
        {
            transform.position = rootPosition;
            ResetVisualMotionState(rootPosition, transform.rotation);
            if (!EnsureInitialized())
                return;

            _motionState = new CombatMotionState(
                _motionState.Frame,
                ToFixVector3(RootToMotionCenter(rootPosition)),
                _motionState.Velocity,
                _motionState.Grounded,
                _motionState.LastCollisionNormal,
                _motionState.CollisionFlags);
            EnsurePhysicsWorldBody();
        }

        private void SyncBufferedInput()
        {
            if (_inputProvider == null || _motionInputProvider == null)
                return;

            InputSnapshot source = _inputProvider.Snapshot;
            _motionInputProvider.SetContext(_inputProvider.IsContextEnabled(InputContext.Gameplay)
                ? InputContext.Gameplay
                : _inputProvider.CurrentContext);
            _motionInputProvider.SetSnapshot(CreateContinuousMotionSnapshot(source));

            if (source.JumpPressed && !_lastSourceJumpPressed && _inputProvider.IsContextEnabled(InputContext.Gameplay))
            {
                long commandFrame = System.Math.Max(_frame, _motionInputProvider.Commands.CurrentFrame);
                _motionInputProvider.Commands.TryEnqueue(new InputCommand(
                    commandFrame,
                    sourceId: 1,
                    InputIntent.Jump,
                    InputCommandPhase.Pressed,
                    traceId: "characterstudio-runtime-input:Jump"),
                    out _);
            }

            _lastSourceJumpPressed = source.JumpPressed;
        }

        private static InputSnapshot CreateContinuousMotionSnapshot(InputSnapshot source)
        {
            return new InputSnapshot(
                source.Move,
                source.Look,
                source.Navigate,
                source.Point,
                source.Scroll,
                source.Throttle,
                jumpPressed: false,
                jumpHeld: source.JumpHeld,
                jumpReleased: false,
                attackPrimaryPressed: false,
                attackPrimaryHeld: source.AttackPrimaryHeld,
                attackSecondaryPressed: false,
                interactPressed: false,
                dodgePressed: false,
                sprintHeld: source.SprintHeld,
                submitPressed: false,
                cancelPressed: false,
                pausePressed: false,
                debugTogglePressed: false,
                clickPressed: false,
                clickHeld: source.ClickHeld,
                clickReleased: false,
                rightClickPressed: false,
                rightClickHeld: source.RightClickHeld,
                rightClickReleased: false,
                restartPressed: false,
                debugPrimaryPressed: false,
                debugSecondaryPressed: false,
                debugCyclePressed: false,
                debugStepPressed: false,
                toggleHudPressed: false,
                audioPrimaryPressed: false,
                audioSecondaryPressed: false);
        }

        private void ApplyMotion(CharacterCommand command)
        {
            Vector3 rootPosition = MotionCenterToRoot(ToVector3(_motionState.Position));
            Quaternion rootRotation = transform.rotation;

            if (_rotateToMoveDirection)
            {
                FixVector3 move = command.GetWorldMoveDirection();
                if (!move.X.IsZero || !move.Z.IsZero)
                {
                    var forward = new Vector3(ToFloat(move.X), 0f, ToFloat(move.Z));
                    if (forward.sqrMagnitude > 0.0001f)
                        rootRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                }
            }

            SetVisualMotionTarget(rootPosition, rootRotation);
            UpdateBlendPreview(command);
        }

        private void ResetVisualMotionState(Vector3 rootPosition, Quaternion rootRotation)
        {
            _previousRootPosition = rootPosition;
            _currentRootPosition = rootPosition;
            _previousRootRotation = rootRotation;
            _currentRootRotation = rootRotation;
            _hasVisualMotionState = true;
        }

        private void SetVisualMotionTarget(Vector3 rootPosition, Quaternion rootRotation)
        {
            if (!_hasVisualMotionState)
                ResetVisualMotionState(transform.position, transform.rotation);

            _previousRootPosition = _currentRootPosition;
            _previousRootRotation = _currentRootRotation;
            _currentRootPosition = rootPosition;
            _currentRootRotation = rootRotation;
            if (!_interpolateVisualMotion || !_deferVisualMotionInterpolation)
            {
                transform.SetPositionAndRotation(rootPosition, rootRotation);
            }
        }

        private void ApplyVisualMotionInterpolation(float fixedDelta)
        {
            if (!_interpolateVisualMotion || !_hasVisualMotionState)
                return;

            float alpha = fixedDelta <= FixedStepEpsilon
                ? 1f
                : Mathf.Clamp01(_accumulator / fixedDelta);
            transform.SetPositionAndRotation(
                Vector3.Lerp(_previousRootPosition, _currentRootPosition, alpha),
                Quaternion.Slerp(_previousRootRotation, _currentRootRotation, alpha));
        }

        private void EnsurePhysicsWorldBody()
        {
            if (_physicsWorld == null)
                return;

            CharacterControlEntityRef entity = _binding.EntityRef;
            if (entity.HasCombatBody)
                _physicsWorld.UpsertBody(new CombatPhysicsBody(entity.CombatEntityId, entity.CombatBodyId, _motionState.Position));

            if (!_enablePreviewGround)
                return;

            int groundLayer = Mathf.Clamp(_previewGroundLayer, 0, 31);
            Vector3 halfExtents = new Vector3(
                Mathf.Max(0.01f, _previewGroundHalfExtents.x),
                Mathf.Max(0.01f, _previewGroundHalfExtents.y),
                Mathf.Max(0.01f, _previewGroundHalfExtents.z));
            _physicsWorld.UpsertBody(new CombatPhysicsBody(
                PreviewGroundEntityId,
                PreviewGroundBodyId,
                new FixVector3(Fix64.Zero, ToFix64(_previewGroundY), Fix64.Zero)));
            _physicsWorld.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                PreviewGroundBodyId,
                PreviewGroundColliderId,
                groundLayer,
                new FixVector3(ToFix64(-halfExtents.x), ToFix64(-halfExtents.y), ToFix64(-halfExtents.z)),
                new FixVector3(ToFix64(halfExtents.x), Fix64.Zero, ToFix64(halfExtents.z))));
        }

        private void UpdateBlendPreview(CharacterCommand command)
        {
            FixVector3 move = command.GetWorldMoveDirection();
            Vector3 worldMove = new Vector3(ToFloat(move.X), 0f, ToFloat(move.Z));
            float moveScale = Mathf.Max(0f, ToFloat(command.MoveSpeedScale));
            float speed = worldMove.magnitude * moveScale;
            _lastLocalMove = speed <= 0.0001f
                ? Vector2.zero
                : new Vector2(ToFloat(command.MoveDirection.X), ToFloat(command.MoveDirection.Z)) * moveScale;
            _lastSpeed01 = speed;
            FixVector3 velocity = _lastMotionResult.Velocity;
            float velocityX = ToFloat(velocity.X);
            float velocityZ = ToFloat(velocity.Z);
            _lastPlanarSpeed = Mathf.Sqrt((velocityX * velocityX) + (velocityZ * velocityZ));
        }

        private CombatMotionConfig CreateMotionConfig()
        {
            FixVector3 halfExtents = new FixVector3(
                ToFix64(Mathf.Max(0.01f, _characterHalfExtents.x)),
                ToFix64(Mathf.Max(0.01f, _characterHalfExtents.y)),
                ToFix64(Mathf.Max(0.01f, _characterHalfExtents.z)));
            int collisionMask = _enablePreviewGround
                ? CombatPhysicsLayerMask.FromLayer(Mathf.Clamp(_previewGroundLayer, 0, 31)).Value
                : CombatPhysicsLayerMask.All.Value;
            return new CombatMotionConfig(
                CombatStepConfig.Default,
                characterHalfExtents: halfExtents,
                moveSpeed: ToFix64(Mathf.Max(0f, _moveSpeed)),
                gravityPerSecond: _enableGravity ? ToFix64(_gravityPerSecond) : Fix64.Zero,
                jumpSpeed: _enableGravity ? ToFix64(Mathf.Max(0f, _jumpSpeed)) : Fix64.Zero,
                maxFallSpeed: _enableGravity ? ToFix64(Mathf.Min(0f, _maxFallSpeed)) : Fix64.Zero,
                skinWidth: Fix64.FromRatio(1, 100),
                groundMinNormalY: Fix64.Half,
                ceilingMinNormalY: Fix64.Half,
                collisionLayerMask: collisionMask,
                maxSlideIterations: 3);
        }

        private Vector3 RootToMotionCenter(Vector3 rootPosition)
        {
            return rootPosition + new Vector3(0f, Mathf.Max(0.01f, _characterHalfExtents.y), 0f);
        }

        private Vector3 MotionCenterToRoot(Vector3 centerPosition)
        {
            return centerPosition - new Vector3(0f, Mathf.Max(0.01f, _characterHalfExtents.y), 0f);
        }

        private static FixVector3 ToFixVector3(Vector3 value)
        {
            return new FixVector3(ToFix64(value.x), ToFix64(value.y), ToFix64(value.z));
        }

        private static Vector3 ToVector3(FixVector3 value)
        {
            return new Vector3(ToFloat(value.X), ToFloat(value.Y), ToFloat(value.Z));
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)(value * Fix64.Scale));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)value.RawValue / Fix64.Scale;
        }
    }
}
