using System.Globalization;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestGenesisBoundaryFormsSample
    {
        public CharacterTestGenesisBoundaryFormsSample(
            CharacterTestGenesisBoundaryKind boundaryKind,
            CombatSurfaceMaterialKind boundaryMaterialKind,
            string boundaryMaterialStableId,
            int tickCount,
            int tickRate,
            bool edgeStartFullySupported,
            bool startGrounded,
            bool finalGrounded,
            int groundedFrames,
            int airborneFrames,
            int maxConsecutiveAirborneFrames,
            int firstAirborneFrame,
            CombatMotionCollisionFlags observedFlags,
            CombatMotionCollisionFlags finalFlags,
            float startPositionX,
            float startPositionY,
            float startPositionZ,
            float finalPositionX,
            float finalPositionY,
            float finalPositionZ,
            float finalVelocityX,
            float finalVelocityY,
            float finalVelocityZ,
            float contactDisplacementX,
            float contactDisplacementY,
            float contactDisplacementZ,
            float lastAppliedDeltaX,
            float lastAppliedDeltaY,
            float lastAppliedDeltaZ,
            float lastContactNormalX,
            float lastContactNormalY,
            float lastContactNormalZ,
            float boundaryNormalX,
            float boundaryNormalY,
            float boundaryNormalZ,
            float minPositionY,
            float maxPositionY)
        {
            BoundaryKind = boundaryKind;
            BoundaryMaterialKind = boundaryMaterialKind;
            BoundaryMaterialStableId = boundaryMaterialStableId ?? string.Empty;
            TickCount = tickCount < 0 ? 0 : tickCount;
            TickRate = tickRate < 0 ? 0 : tickRate;
            EdgeStartFullySupported = edgeStartFullySupported;
            StartGrounded = startGrounded;
            FinalGrounded = finalGrounded;
            GroundedFrames = groundedFrames < 0 ? 0 : groundedFrames;
            AirborneFrames = airborneFrames < 0 ? 0 : airborneFrames;
            MaxConsecutiveAirborneFrames = maxConsecutiveAirborneFrames < 0 ? 0 : maxConsecutiveAirborneFrames;
            FirstAirborneFrame = firstAirborneFrame;
            ObservedFlags = observedFlags;
            FinalFlags = finalFlags;
            StartPositionX = startPositionX;
            StartPositionY = startPositionY;
            StartPositionZ = startPositionZ;
            FinalPositionX = finalPositionX;
            FinalPositionY = finalPositionY;
            FinalPositionZ = finalPositionZ;
            FinalVelocityX = finalVelocityX;
            FinalVelocityY = finalVelocityY;
            FinalVelocityZ = finalVelocityZ;
            ContactDisplacementX = contactDisplacementX;
            ContactDisplacementY = contactDisplacementY;
            ContactDisplacementZ = contactDisplacementZ;
            LastAppliedDeltaX = lastAppliedDeltaX;
            LastAppliedDeltaY = lastAppliedDeltaY;
            LastAppliedDeltaZ = lastAppliedDeltaZ;
            LastContactNormalX = lastContactNormalX;
            LastContactNormalY = lastContactNormalY;
            LastContactNormalZ = lastContactNormalZ;
            BoundaryNormalX = boundaryNormalX;
            BoundaryNormalY = boundaryNormalY;
            BoundaryNormalZ = boundaryNormalZ;
            MinPositionY = minPositionY;
            MaxPositionY = maxPositionY;
        }

        public CharacterTestGenesisBoundaryKind BoundaryKind { get; }
        public CombatSurfaceMaterialKind BoundaryMaterialKind { get; }
        public string BoundaryMaterialStableId { get; }
        public int TickCount { get; }
        public int TickRate { get; }
        public bool EdgeStartFullySupported { get; }
        public bool StartGrounded { get; }
        public bool FinalGrounded { get; }
        public int GroundedFrames { get; }
        public int AirborneFrames { get; }
        public int MaxConsecutiveAirborneFrames { get; }
        public int FirstAirborneFrame { get; }
        public CombatMotionCollisionFlags ObservedFlags { get; }
        public CombatMotionCollisionFlags FinalFlags { get; }
        public float StartPositionX { get; }
        public float StartPositionY { get; }
        public float StartPositionZ { get; }
        public float FinalPositionX { get; }
        public float FinalPositionY { get; }
        public float FinalPositionZ { get; }
        public float FinalVelocityX { get; }
        public float FinalVelocityY { get; }
        public float FinalVelocityZ { get; }
        public float ContactDisplacementX { get; }
        public float ContactDisplacementY { get; }
        public float ContactDisplacementZ { get; }
        public float LastAppliedDeltaX { get; }
        public float LastAppliedDeltaY { get; }
        public float LastAppliedDeltaZ { get; }
        public float LastContactNormalX { get; }
        public float LastContactNormalY { get; }
        public float LastContactNormalZ { get; }
        public float BoundaryNormalX { get; }
        public float BoundaryNormalY { get; }
        public float BoundaryNormalZ { get; }
        public float MinPositionY { get; }
        public float MaxPositionY { get; }

        public bool ObservedGrounded => HasFlag(ObservedFlags, CombatMotionCollisionFlags.Grounded);
        public bool ObservedWall => HasFlag(ObservedFlags, CombatMotionCollisionFlags.Wall);
        public bool ObservedBlockedX => HasFlag(ObservedFlags, CombatMotionCollisionFlags.BlockedX);
        public bool ObservedBlockedZ => HasFlag(ObservedFlags, CombatMotionCollisionFlags.BlockedZ);
        public bool ObservedStepUp => HasFlag(ObservedFlags, CombatMotionCollisionFlags.StepUp);

        public string ToSummaryLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "boundary={0} material={1}/{2} ticks={3}@{4}Hz startGrounded={5} finalGrounded={6} groundedFrames={7} airborneFrames={8} maxAirborne={9} firstAirborne={10} flags={11} finalFlags={12} contactDisplacement=({13:0.###},{14:0.###},{15:0.###}) finalPosition=({16:0.###},{17:0.###},{18:0.###}) finalVelocity=({19:0.###},{20:0.###},{21:0.###}) boundaryNormal=({22:0.###},{23:0.###},{24:0.###})",
                BoundaryKind,
                BoundaryMaterialKind,
                BoundaryMaterialStableId,
                TickCount,
                TickRate,
                StartGrounded,
                FinalGrounded,
                GroundedFrames,
                AirborneFrames,
                MaxConsecutiveAirborneFrames,
                FirstAirborneFrame,
                ObservedFlags,
                FinalFlags,
                ContactDisplacementX,
                ContactDisplacementY,
                ContactDisplacementZ,
                FinalPositionX,
                FinalPositionY,
                FinalPositionZ,
                FinalVelocityX,
                FinalVelocityY,
                FinalVelocityZ,
                BoundaryNormalX,
                BoundaryNormalY,
                BoundaryNormalZ);
        }

        private static bool HasFlag(CombatMotionCollisionFlags value, CombatMotionCollisionFlags flag)
        {
            return (value & flag) == flag;
        }
    }
}
