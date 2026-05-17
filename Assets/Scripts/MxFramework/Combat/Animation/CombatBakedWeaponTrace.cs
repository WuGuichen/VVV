using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Animation
{
    public readonly struct CombatBakedWeaponTraceReferenceFrame
    {
        public CombatBakedWeaponTraceReferenceFrame(
            int traceId,
            int localFrame,
            FixVector3 socketPrev,
            FixVector3 socketNow,
            FixVector3 tipDirectionPrev,
            FixVector3 tipDirectionNow,
            string socketId = "")
        {
            if (traceId < 0)
                throw new ArgumentOutOfRangeException(nameof(traceId), "Trace id cannot be negative.");
            if (localFrame < 0)
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");

            TraceId = traceId;
            LocalFrame = localFrame;
            SocketId = socketId ?? string.Empty;
            SocketPrev = socketPrev;
            SocketNow = socketNow;
            TipDirectionPrev = tipDirectionPrev;
            TipDirectionNow = tipDirectionNow;
        }

        public int TraceId { get; }
        public int LocalFrame { get; }
        public string SocketId { get; }
        public FixVector3 SocketPrev { get; }
        public FixVector3 SocketNow { get; }
        public FixVector3 TipDirectionPrev { get; }
        public FixVector3 TipDirectionNow { get; }
    }

    public readonly struct CombatBakedWeaponRuntimeProfile
    {
        public CombatBakedWeaponRuntimeProfile(
            Fix64 characterScale,
            Fix64 weaponLength,
            Fix64 weaponRadius,
            FixVector3 socketOffset,
            CombatPhysicsLayerMask targetMask)
        {
            if (characterScale <= Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(characterScale), "Character scale must be positive.");
            if (weaponLength < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(weaponLength), "Weapon length cannot be negative.");
            if (weaponRadius < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(weaponRadius), "Weapon radius cannot be negative.");

            CharacterScale = characterScale;
            WeaponLength = weaponLength;
            WeaponRadius = weaponRadius;
            SocketOffset = socketOffset;
            TargetMask = targetMask;
        }

        public Fix64 CharacterScale { get; }
        public Fix64 WeaponLength { get; }
        public Fix64 WeaponRadius { get; }
        public FixVector3 SocketOffset { get; }
        public CombatPhysicsLayerMask TargetMask { get; }
    }

    public static class CombatBakedWeaponTraceAdapter
    {
        public static WeaponTraceFrame BuildFrame(
            CombatBakedWeaponTraceReferenceFrame reference,
            CombatBakedWeaponRuntimeProfile profile)
        {
            FixVector3 rootPrev = Scale(reference.SocketPrev + profile.SocketOffset, profile.CharacterScale);
            FixVector3 rootNow = Scale(reference.SocketNow + profile.SocketOffset, profile.CharacterScale);
            Fix64 scaledLength = profile.WeaponLength * profile.CharacterScale;
            FixVector3 tipPrev = rootPrev + (reference.TipDirectionPrev * scaledLength);
            FixVector3 tipNow = rootNow + (reference.TipDirectionNow * scaledLength);
            Fix64 radius = profile.WeaponRadius * profile.CharacterScale;

            return new WeaponTraceFrame(
                reference.TraceId,
                rootPrev,
                tipPrev,
                rootNow,
                tipNow,
                radius,
                profile.TargetMask);
        }

        public static CombatCapsuleQuery BuildCurrentBladeCapsule(
            CombatBakedWeaponTraceReferenceFrame reference,
            CombatBakedWeaponRuntimeProfile profile,
            CombatEntityId sourceEntityId,
            int actionId,
            int queryId,
            int sourceOrder)
        {
            WeaponTraceFrame frame = BuildFrame(reference, profile);
            return WeaponTraceQueryBuilder.BuildCurrentBladeCapsule(
                frame,
                sourceEntityId,
                actionId,
                queryId,
                sourceOrder);
        }

        private static FixVector3 Scale(FixVector3 value, Fix64 scale)
        {
            return new FixVector3(value.X * scale, value.Y * scale, value.Z * scale);
        }
    }
}
