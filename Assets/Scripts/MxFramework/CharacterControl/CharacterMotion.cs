using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.CharacterControl
{
    public readonly struct CharacterMotionSettings : IEquatable<CharacterMotionSettings>
    {
        public static readonly CharacterMotionSettings Default = new CharacterMotionSettings(
            baseMoveSpeedScale: Fix64.One,
            sprintMoveSpeedScale: Fix64.FromRatio(3, 2),
            actionMoveSpeedScale: Fix64.Half,
            reactionMoveSpeedScale: Fix64.Zero,
            disabledMoveSpeedScale: Fix64.Zero,
            tractionMoveSpeedScale: Fix64.One);

        public CharacterMotionSettings(
            Fix64 baseMoveSpeedScale,
            Fix64 sprintMoveSpeedScale,
            Fix64 actionMoveSpeedScale,
            Fix64 reactionMoveSpeedScale,
            Fix64 disabledMoveSpeedScale,
            Fix64 tractionMoveSpeedScale)
        {
            EnsureNonNegative(baseMoveSpeedScale, nameof(baseMoveSpeedScale));
            EnsureNonNegative(sprintMoveSpeedScale, nameof(sprintMoveSpeedScale));
            EnsureNonNegative(actionMoveSpeedScale, nameof(actionMoveSpeedScale));
            EnsureNonNegative(reactionMoveSpeedScale, nameof(reactionMoveSpeedScale));
            EnsureNonNegative(disabledMoveSpeedScale, nameof(disabledMoveSpeedScale));
            EnsureNonNegative(tractionMoveSpeedScale, nameof(tractionMoveSpeedScale));

            BaseMoveSpeedScale = baseMoveSpeedScale;
            SprintMoveSpeedScale = sprintMoveSpeedScale;
            ActionMoveSpeedScale = actionMoveSpeedScale;
            ReactionMoveSpeedScale = reactionMoveSpeedScale;
            DisabledMoveSpeedScale = disabledMoveSpeedScale;
            TractionMoveSpeedScale = tractionMoveSpeedScale;
        }

        public Fix64 BaseMoveSpeedScale { get; }

        public Fix64 SprintMoveSpeedScale { get; }

        public Fix64 ActionMoveSpeedScale { get; }

        public Fix64 ReactionMoveSpeedScale { get; }

        public Fix64 DisabledMoveSpeedScale { get; }

        public Fix64 TractionMoveSpeedScale { get; }

        public bool Equals(CharacterMotionSettings other)
        {
            return BaseMoveSpeedScale.Equals(other.BaseMoveSpeedScale)
                && SprintMoveSpeedScale.Equals(other.SprintMoveSpeedScale)
                && ActionMoveSpeedScale.Equals(other.ActionMoveSpeedScale)
                && ReactionMoveSpeedScale.Equals(other.ReactionMoveSpeedScale)
                && DisabledMoveSpeedScale.Equals(other.DisabledMoveSpeedScale)
                && TractionMoveSpeedScale.Equals(other.TractionMoveSpeedScale);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterMotionSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BaseMoveSpeedScale.GetHashCode();
                hash = (hash * 397) ^ SprintMoveSpeedScale.GetHashCode();
                hash = (hash * 397) ^ ActionMoveSpeedScale.GetHashCode();
                hash = (hash * 397) ^ ReactionMoveSpeedScale.GetHashCode();
                hash = (hash * 397) ^ DisabledMoveSpeedScale.GetHashCode();
                hash = (hash * 397) ^ TractionMoveSpeedScale.GetHashCode();
                return hash;
            }
        }

        private static void EnsureNonNegative(Fix64 value, string name)
        {
            if (value < Fix64.Zero)
                throw new ArgumentOutOfRangeException(name, "Character motion scale cannot be negative.");
        }
    }

    public readonly struct CharacterMotionResult
    {
        public CharacterMotionResult(
            CharacterCommand command,
            CharacterControlState controlState,
            CharacterControlLockMask lockMask,
            CombatMotionInput motionInput,
            CombatMotionStepResult stepResult,
            bool worldSynced,
            int worldRevision)
        {
            Command = command;
            ControlState = controlState;
            LockMask = lockMask;
            MotionInput = motionInput;
            StepResult = stepResult;
            WorldSynced = worldSynced;
            WorldRevision = worldRevision;
        }

        public CharacterCommand Command { get; }

        public CharacterControlState ControlState { get; }

        public CharacterControlLockMask LockMask { get; }

        public CombatMotionInput MotionInput { get; }

        public CombatMotionStepResult StepResult { get; }

        public bool WorldSynced { get; }

        public int WorldRevision { get; }

        public FixVector3 Position => StepResult.State.Position;

        public FixVector3 Velocity => StepResult.State.Velocity;

        public bool Grounded => StepResult.State.Grounded;

        public CombatMotionCollisionFlags CollisionFlags => StepResult.CollisionFlags;

        public FixVector3 DesiredDelta => StepResult.DesiredDelta;

        public FixVector3 AppliedDelta => StepResult.AppliedDelta;

        public bool JumpStarted => StepResult.JumpStarted;
    }

    public sealed class CharacterMotionResolver
    {
        private readonly CombatKinematicMotor _motor;
        private readonly CharacterMotionSettings _settings;

        public CharacterMotionResolver(CombatKinematicMotor motor)
            : this(motor, CharacterMotionSettings.Default)
        {
        }

        public CharacterMotionResolver(CombatKinematicMotor motor, CharacterMotionSettings settings)
        {
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));
            _settings = settings.Equals(default(CharacterMotionSettings)) ? CharacterMotionSettings.Default : settings;
        }

        public CharacterMotionSettings Settings => _settings;

        public CharacterMotionResult Step(
            CharacterCommand command,
            CharacterControlStateMachine controlState,
            CombatMotionState motionState,
            CombatPhysicsWorld physicsWorld = null)
        {
            if (controlState == null)
                throw new ArgumentNullException(nameof(controlState));

            controlState.RecordCommandFrame(command.Frame);

            CombatMotionInput input = CreateMotionInput(command, controlState);
            CharacterControlEntityRef entity = command.Entity.IsValid ? command.Entity : controlState.Entity;
            CombatMotionStepResult step = physicsWorld != null && entity.HasCombatBody
                ? _motor.Step(physicsWorld, entity.CombatBodyId, motionState, input)
                : _motor.Step(motionState, input);
            bool worldSynced = physicsWorld != null && entity.HasCombatBody && physicsWorld.TryGetBody(entity.CombatBodyId, out CombatPhysicsBody body) && body.Position == step.State.Position;
            int worldRevision = physicsWorld == null ? 0 : physicsWorld.Revision;
            return new CharacterMotionResult(
                command,
                controlState.CurrentState,
                controlState.ControlLockMask,
                input,
                step,
                worldSynced,
                worldRevision);
        }

        public CombatMotionInput CreateMotionInput(CharacterCommand command, CharacterControlStateMachine controlState)
        {
            if (controlState == null)
                throw new ArgumentNullException(nameof(controlState));

            bool moveLocked = controlState.IsLocked(CharacterControlLockMask.Move);
            bool jumpLocked = controlState.IsLocked(CharacterControlLockMask.Jump);
            Fix64 stateScale = GetStateScale(controlState.CurrentState);
            FixVector3 moveDirection = moveLocked ? FixVector3.Zero : command.GetWorldMoveDirection();
            bool jumpPressed = !jumpLocked && command.JumpPressed && controlState.CurrentState == CharacterControlState.Locomotion;

            Fix64 scale = command.MoveSpeedScale * _settings.BaseMoveSpeedScale * stateScale * _settings.TractionMoveSpeedScale;
            if (command.SprintHeld && controlState.CurrentState == CharacterControlState.Locomotion)
            {
                scale *= _settings.SprintMoveSpeedScale;
            }

            if (scale.IsZero)
            {
                moveDirection = FixVector3.Zero;
            }

            return new CombatMotionInput(moveDirection, jumpPressed, scale);
        }

        private Fix64 GetStateScale(CharacterControlState state)
        {
            switch (state)
            {
                case CharacterControlState.Action:
                    return _settings.ActionMoveSpeedScale;
                case CharacterControlState.Reaction:
                    return _settings.ReactionMoveSpeedScale;
                case CharacterControlState.Disabled:
                    return _settings.DisabledMoveSpeedScale;
                default:
                    return Fix64.One;
            }
        }
    }
}
