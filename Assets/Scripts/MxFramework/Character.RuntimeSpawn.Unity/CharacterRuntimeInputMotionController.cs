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

        [SerializeField] private InputService _inputService;
        [SerializeField] private bool _enableInputMotion = true;
        [SerializeField] private float _stepRate = 60f;
        [SerializeField] private int _maxStepsPerUpdate = 4;
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private bool _rotateToMoveDirection = true;

        private CharacterRuntimeControllerBinding _binding;
        private IInputProvider _inputProvider;
        private InputCharacterCommandSource _commandSource;
        private CharacterMotionResolver _motionResolver;
        private CombatMotionState _motionState;
        private float _accumulator;
        private long _frame;
        private bool _initialized;
        private CharacterMotionResult _lastMotionResult;

        public bool IsInitialized => _initialized;
        public bool EnableInputMotion
        {
            get => _enableInputMotion;
            set => _enableInputMotion = value;
        }

        public CharacterMotionResult LastMotionResult => _lastMotionResult;
        public long CurrentFrame => _frame;

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

            float stepRate = Mathf.Max(1f, _stepRate);
            float fixedDelta = 1f / stepRate;
            _accumulator += Mathf.Max(0f, Time.deltaTime);

            int maxSteps = Mathf.Max(1, _maxStepsPerUpdate);
            int steps = 0;
            while (_accumulator + FixedStepEpsilon >= fixedDelta && steps < maxSteps)
            {
                StepFrame();
                _accumulator -= fixedDelta;
                steps++;
            }

            if (steps == maxSteps && _accumulator >= fixedDelta)
                _accumulator = 0f;
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

            _motionResolver = new CharacterMotionResolver(new CombatKinematicMotor(CreateMotionConfig()));
            _commandSource = new InputCharacterCommandSource(_inputProvider, new InputCharacterCommandSourceOptions
            {
                SourceId = 1,
                MoveSpeedScale = Fix64.One,
                TracePrefix = "characterstudio-runtime-input"
            });
            _motionState = new CombatMotionState(
                CombatFrame.Zero,
                ToFixVector3(transform.position),
                FixVector3.Zero,
                grounded: true,
                FixVector3.Zero,
                CombatMotionCollisionFlags.Grounded);
            _frame = 0L;
            _accumulator = 0f;
            _initialized = true;
            return true;
        }

        public bool StepFrame()
        {
            if (!EnsureInitialized())
                return false;

            long frameValue = _inputProvider == null
                ? _frame
                : System.Math.Max(_frame, _inputProvider.Commands.CurrentFrame);
            var runtimeFrame = new RuntimeFrame(frameValue);
            if (!_commandSource.TryGetCommand(runtimeFrame, _binding.EntityRef, out CharacterCommand command))
            {
                _frame = frameValue + 1L;
                return false;
            }

            _motionState = _motionState.WithFrame(new CombatFrame(checked((int)frameValue)));
            _lastMotionResult = _motionResolver.Step(command, _binding.StateMachine, _motionState);
            _motionState = _lastMotionResult.StepResult.State;
            ApplyMotion(command);
            _frame = frameValue + 1L;
            return true;
        }

        private void ApplyMotion(CharacterCommand command)
        {
            transform.position = ToVector3(_motionState.Position);

            if (!_rotateToMoveDirection)
                return;

            FixVector3 move = command.GetWorldMoveDirection();
            if (move.X.IsZero && move.Z.IsZero)
                return;

            var forward = new Vector3(ToFloat(move.X), 0f, ToFloat(move.Z));
            if (forward.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private CombatMotionConfig CreateMotionConfig()
        {
            return new CombatMotionConfig(
                CombatStepConfig.Default,
                characterHalfExtents: new FixVector3(Fix64.FromRatio(4, 10), Fix64.FromRatio(9, 10), Fix64.FromRatio(4, 10)),
                moveSpeed: ToFix64(Mathf.Max(0f, _moveSpeed)),
                gravityPerSecond: Fix64.Zero,
                jumpSpeed: Fix64.Zero,
                maxFallSpeed: Fix64.Zero,
                skinWidth: Fix64.FromRatio(1, 100),
                groundMinNormalY: Fix64.Half,
                ceilingMinNormalY: Fix64.Half,
                collisionLayerMask: CombatPhysicsLayerMask.All.Value,
                maxSlideIterations: 3);
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
