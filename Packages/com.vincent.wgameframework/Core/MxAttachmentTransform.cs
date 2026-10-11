using System;

namespace MxFramework.Core
{
    /// <summary>Defines how a presentation object is anchored to its subject.</summary>
    public enum MxAttachmentMode
    {
        FollowSubject = 0,
        FollowSocket = 1,
        FixedWorld = 2,
    }

    /// <summary>Defines the coordinate basis used by an attachment transform offset.</summary>
    public enum MxTransformSpace
    {
        Local = 0,
        World = 1,
    }

    /// <summary>Stores position, Euler rotation, and scale offsets without an engine dependency.</summary>
    /// <remarks>NoAlloc value contract.</remarks>
    public readonly struct MxTransformOffset : IEquatable<MxTransformOffset>
    {
        public static readonly MxTransformOffset Identity = new MxTransformOffset(
            scaleX: 1f,
            scaleY: 1f,
            scaleZ: 1f);

        public MxTransformOffset(
            float positionX = 0f,
            float positionY = 0f,
            float positionZ = 0f,
            float rotationEulerX = 0f,
            float rotationEulerY = 0f,
            float rotationEulerZ = 0f,
            float scaleX = 1f,
            float scaleY = 1f,
            float scaleZ = 1f)
        {
            PositionX = positionX;
            PositionY = positionY;
            PositionZ = positionZ;
            RotationEulerX = rotationEulerX;
            RotationEulerY = rotationEulerY;
            RotationEulerZ = rotationEulerZ;
            ScaleX = NormalizeScale(scaleX);
            ScaleY = NormalizeScale(scaleY);
            ScaleZ = NormalizeScale(scaleZ);
        }

        public float PositionX { get; }
        public float PositionY { get; }
        public float PositionZ { get; }
        public float RotationEulerX { get; }
        public float RotationEulerY { get; }
        public float RotationEulerZ { get; }
        public float ScaleX { get; }
        public float ScaleY { get; }
        public float ScaleZ { get; }

        /// <remarks>NoAlloc.</remarks>
        public bool IsScaleValid() => ScaleX > 0f && ScaleY > 0f && ScaleZ > 0f;

        /// <summary>Returns an offset with invalid scale components restored to one.</summary>
        /// <remarks>NoAlloc.</remarks>
        public MxTransformOffset Normalized()
        {
            return IsScaleValid()
                ? this
                : new MxTransformOffset(
                    PositionX,
                    PositionY,
                    PositionZ,
                    RotationEulerX,
                    RotationEulerY,
                    RotationEulerZ,
                    ScaleX,
                    ScaleY,
                    ScaleZ);
        }

        public bool Equals(MxTransformOffset other)
        {
            return PositionX.Equals(other.PositionX)
                && PositionY.Equals(other.PositionY)
                && PositionZ.Equals(other.PositionZ)
                && RotationEulerX.Equals(other.RotationEulerX)
                && RotationEulerY.Equals(other.RotationEulerY)
                && RotationEulerZ.Equals(other.RotationEulerZ)
                && ScaleX.Equals(other.ScaleX)
                && ScaleY.Equals(other.ScaleY)
                && ScaleZ.Equals(other.ScaleZ);
        }

        public override bool Equals(object obj) => obj is MxTransformOffset other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = PositionX.GetHashCode();
                hash = (hash * 397) ^ PositionY.GetHashCode();
                hash = (hash * 397) ^ PositionZ.GetHashCode();
                hash = (hash * 397) ^ RotationEulerX.GetHashCode();
                hash = (hash * 397) ^ RotationEulerY.GetHashCode();
                hash = (hash * 397) ^ RotationEulerZ.GetHashCode();
                hash = (hash * 397) ^ ScaleX.GetHashCode();
                hash = (hash * 397) ^ ScaleY.GetHashCode();
                hash = (hash * 397) ^ ScaleZ.GetHashCode();
                return hash;
            }
        }

        private static float NormalizeScale(float value) => value <= 0f ? 1f : value;
    }

    /// <summary>Combines a reusable attachment policy, socket reference, coordinate basis, and transform offset.</summary>
    /// <remarks>NoAlloc value contract; the socket string is normalized during construction.</remarks>
    public readonly struct MxAttachmentTransform : IEquatable<MxAttachmentTransform>
    {
        public static readonly MxAttachmentTransform Default = new MxAttachmentTransform(
            MxAttachmentMode.FollowSubject,
            string.Empty,
            MxTransformSpace.Local,
            MxTransformOffset.Identity);

        public MxAttachmentTransform(
            MxAttachmentMode mode = MxAttachmentMode.FollowSubject,
            string socketId = "",
            MxTransformSpace offsetSpace = MxTransformSpace.Local,
            MxTransformOffset transform = default)
        {
            if (!Enum.IsDefined(typeof(MxAttachmentMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (!Enum.IsDefined(typeof(MxTransformSpace), offsetSpace))
                throw new ArgumentOutOfRangeException(nameof(offsetSpace));

            Mode = mode;
            SocketId = string.IsNullOrWhiteSpace(socketId) ? string.Empty : socketId.Trim();
            OffsetSpace = offsetSpace;
            Transform = transform.Normalized();
        }

        public MxAttachmentMode Mode { get; }
        public string SocketId { get; }
        public MxTransformSpace OffsetSpace { get; }
        public MxTransformOffset Transform { get; }

        /// <remarks>NoAlloc.</remarks>
        public bool FollowsSubject() => Mode != MxAttachmentMode.FixedWorld;

        /// <remarks>NoAlloc.</remarks>
        public bool UsesSocket() => Mode == MxAttachmentMode.FollowSocket;

        /// <summary>Returns a normalized attachment, including identity scale for default struct values.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static MxAttachmentTransform Normalize(in MxAttachmentTransform value)
        {
            return value.Transform.IsScaleValid()
                ? value
                : new MxAttachmentTransform(value.Mode, value.SocketId, value.OffsetSpace, value.Transform);
        }

        /// <remarks>NoAlloc.</remarks>
        public MxAttachmentTransform WithMode(MxAttachmentMode mode)
        {
            return new MxAttachmentTransform(mode, SocketId, OffsetSpace, Transform);
        }

        /// <remarks>NoAlloc.</remarks>
        public MxAttachmentTransform WithSocketId(string socketId)
        {
            return new MxAttachmentTransform(Mode, socketId, OffsetSpace, Transform);
        }

        /// <remarks>NoAlloc.</remarks>
        public MxAttachmentTransform WithOffsetSpace(MxTransformSpace offsetSpace)
        {
            return new MxAttachmentTransform(Mode, SocketId, offsetSpace, Transform);
        }

        /// <remarks>NoAlloc.</remarks>
        public MxAttachmentTransform WithTransform(MxTransformOffset transform)
        {
            return new MxAttachmentTransform(Mode, SocketId, OffsetSpace, transform);
        }

        public bool Equals(MxAttachmentTransform other)
        {
            return Mode == other.Mode
                && string.Equals(SocketId, other.SocketId, StringComparison.Ordinal)
                && OffsetSpace == other.OffsetSpace
                && Transform.Equals(other.Transform);
        }

        public override bool Equals(object obj) => obj is MxAttachmentTransform other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Mode;
                hash = (hash * 397) ^ (SocketId == null ? 0 : StringComparer.Ordinal.GetHashCode(SocketId));
                hash = (hash * 397) ^ (int)OffsetSpace;
                hash = (hash * 397) ^ Transform.GetHashCode();
                return hash;
            }
        }
    }
}
