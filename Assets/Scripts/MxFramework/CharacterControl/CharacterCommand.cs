using System;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.CharacterControl
{
    [Flags]
    public enum CharacterActionButtons
    {
        None = 0,
        Primary = 1 << 0,
        Secondary = 1 << 1,
        Skill1 = 1 << 2,
        Skill2 = 1 << 3,
        Interact = 1 << 4,
        Dodge = 1 << 5,
        Cancel = 1 << 6
    }

    public readonly struct CharacterFacingBasis : IEquatable<CharacterFacingBasis>
    {
        private static readonly FixVector3 WorldRight = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
        private static readonly FixVector3 WorldForward = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

        public static readonly CharacterFacingBasis Identity =
            new CharacterFacingBasis(WorldRight, WorldForward, WorldForward);

        public CharacterFacingBasis(FixVector3 right, FixVector3 forward, FixVector3 facing)
        {
            Right = NormalizeHorizontalOrFallback(right, WorldRight);
            Forward = NormalizeHorizontalOrFallback(forward, WorldForward);
            Facing = NormalizeHorizontalOrFallback(facing, Forward);
        }

        public FixVector3 Right { get; }

        public FixVector3 Forward { get; }

        public FixVector3 Facing { get; }

        public static CharacterFacingBasis FromForward(FixVector3 forward)
        {
            FixVector3 normalizedForward = NormalizeHorizontalOrFallback(forward, WorldForward);
            var right = new FixVector3(normalizedForward.Z, Fix64.Zero, -normalizedForward.X);
            return new CharacterFacingBasis(right, normalizedForward, normalizedForward);
        }

        public FixVector3 ToWorldDirection(FixVector3 localMove)
        {
            FixVector3 local = ClampHorizontal(localMove);
            FixVector3 world = (Right * local.X) + (Forward * local.Z);
            return ClampHorizontal(world);
        }

        public bool Equals(CharacterFacingBasis other)
        {
            return Right.Equals(other.Right)
                && Forward.Equals(other.Forward)
                && Facing.Equals(other.Facing);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterFacingBasis other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Right.GetHashCode();
                hash = (hash * 397) ^ Forward.GetHashCode();
                hash = (hash * 397) ^ Facing.GetHashCode();
                return hash;
            }
        }

        internal static FixVector3 ClampHorizontal(FixVector3 value)
        {
            var horizontal = new FixVector3(value.X, Fix64.Zero, value.Z);
            Fix64 lengthSquared = horizontal.LengthSquared();
            if (lengthSquared <= Fix64.One)
            {
                return horizontal;
            }

            return horizontal / lengthSquared.Sqrt();
        }

        private static FixVector3 NormalizeHorizontalOrFallback(FixVector3 value, FixVector3 fallback)
        {
            FixVector3 horizontal = ClampHorizontal(value);
            if (horizontal.IsZero)
            {
                return fallback;
            }

            return horizontal.TryNormalize(out FixVector3 normalized) ? normalized : fallback;
        }
    }

    public readonly struct CharacterCommand : IEquatable<CharacterCommand>
    {
        public CharacterCommand(
            RuntimeFrame frame,
            int sourceId,
            CharacterControlEntityRef entity,
            FixVector3 moveDirection,
            CharacterFacingBasis facingBasis,
            bool jumpPressed,
            bool sprintHeld,
            CharacterActionButtons actionButtons,
            CharacterActionRequest actionRequest,
            Fix64 moveSpeedScale,
            string traceId = "")
        {
            if (moveSpeedScale < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(moveSpeedScale), "Character command move speed scale cannot be negative.");

            Frame = frame;
            SourceId = sourceId;
            Entity = entity;
            MoveDirection = CharacterFacingBasis.ClampHorizontal(moveDirection);
            FacingBasis = facingBasis;
            JumpPressed = jumpPressed;
            SprintHeld = sprintHeld;
            ActionButtons = actionButtons;
            ActionRequest = actionRequest;
            MoveSpeedScale = moveSpeedScale;
            TraceId = traceId ?? string.Empty;
        }

        public CharacterCommand(
            RuntimeFrame frame,
            int sourceId,
            CharacterControlEntityRef entity,
            FixVector3 moveDirection,
            CharacterFacingBasis facingBasis,
            bool jumpPressed,
            bool sprintHeld,
            CharacterActionButtons actionButtons,
            CharacterActionRequest actionRequest,
            string traceId = "")
            : this(
                frame,
                sourceId,
                entity,
                moveDirection,
                facingBasis,
                jumpPressed,
                sprintHeld,
                actionButtons,
                actionRequest,
                Fix64.One,
                traceId)
        {
        }

        public RuntimeFrame Frame { get; }

        public int SourceId { get; }

        public CharacterControlEntityRef Entity { get; }

        public FixVector3 MoveDirection { get; }

        public CharacterFacingBasis FacingBasis { get; }

        public bool JumpPressed { get; }

        public bool SprintHeld { get; }

        public CharacterActionButtons ActionButtons { get; }

        public CharacterActionRequest ActionRequest { get; }

        public Fix64 MoveSpeedScale { get; }

        public string TraceId { get; }

        public bool HasActionRequest => ActionRequest.Kind != CharacterActionKind.None;

        public FixVector3 GetWorldMoveDirection()
        {
            return FacingBasis.ToWorldDirection(MoveDirection);
        }

        public bool Equals(CharacterCommand other)
        {
            return Frame == other.Frame
                && SourceId == other.SourceId
                && Entity.Equals(other.Entity)
                && MoveDirection.Equals(other.MoveDirection)
                && FacingBasis.Equals(other.FacingBasis)
                && JumpPressed == other.JumpPressed
                && SprintHeld == other.SprintHeld
                && ActionButtons == other.ActionButtons
                && ActionRequest.Equals(other.ActionRequest)
                && MoveSpeedScale.Equals(other.MoveSpeedScale)
                && string.Equals(TraceId, other.TraceId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterCommand other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ Entity.GetHashCode();
                hash = (hash * 397) ^ MoveDirection.GetHashCode();
                hash = (hash * 397) ^ FacingBasis.GetHashCode();
                hash = (hash * 397) ^ (JumpPressed ? 1 : 0);
                hash = (hash * 397) ^ (SprintHeld ? 1 : 0);
                hash = (hash * 397) ^ (int)ActionButtons;
                hash = (hash * 397) ^ ActionRequest.GetHashCode();
                hash = (hash * 397) ^ MoveSpeedScale.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(TraceId ?? string.Empty);
                return hash;
            }
        }
    }

    public interface ICharacterCommandSource
    {
        bool TryGetCommand(RuntimeFrame frame, CharacterControlEntityRef entity, out CharacterCommand command);
    }
}
