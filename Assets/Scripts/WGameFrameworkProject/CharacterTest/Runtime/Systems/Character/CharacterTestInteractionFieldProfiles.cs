using MxFramework.Core.Math;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestInteractionFieldProfiles
    {
        public const int FireballExplosionImpulseSpeedPerImpactPowerMillimetersPerSecond = 250;
        public const int FireballExplosionMaxSpeedMillimetersPerSecond = 6000;
        public const int DefaultReferenceMassGrams = CharacterTestCharacterPhysicsComponent.DefaultMassGrams;
        public const int DefaultMinimumMassScaleMilli = 250;
        public const int DefaultMaximumMassScaleMilli = 2000;
        public const int TornadoAttractionAccelerationMillimetersPerSecondSquared = 36000;
        public const int TornadoAttractionAccelerationPerImpactPowerMillimetersPerSecondSquared = 3600;
        public const int TornadoAttractionMinimumDistanceScaleMilli = 250;
        public const int TornadoAttractionTerminalSpeedMillimetersPerSecond = 6500;
        public const int SwordWaveDirectionalImpulseSpeedPerImpactPowerMillimetersPerSecond = 220;
        public const int SwordWaveDirectionalImpulseMaxSpeedMillimetersPerSecond = 4500;

        public static readonly Fix64 TornadoAttractionRadius = Fix64.FromRatio(9, 2);
        public static readonly Fix64 TornadoAttractionCapsuleBottomOffset = -Fix64.One;
        public static readonly Fix64 TornadoAttractionCapsuleTopOffset = Fix64.FromRatio(5, 2);
        public static readonly Fix64 SwordWaveDirectionalImpulseRadius =
            CharacterTestFireballProjectileAdapter.SwordWaveCollisionHalfWidth
            + CharacterTestFireballProjectileAdapter.SwordWaveRadius;
        public static readonly Fix64 SwordWaveDirectionalImpulseCapsuleHalfLength =
            CharacterTestFireballProjectileAdapter.SwordWaveSpeedPerFrame / Fix64.FromInt(2);
        public static readonly CharacterTestInteractionPhysicalField TornadoAttractionPhysicalField =
            CharacterTestInteractionPhysicalField.AttractionCapsule(
                CharacterTestInteractionTargetFilter.DynamicCharacters
                    | CharacterTestInteractionTargetFilter.DynamicMovableObjects
                    | CharacterTestInteractionTargetFilter.RequireHostile
                    | CharacterTestInteractionTargetFilter.ExcludeSource,
                TornadoAttractionCapsuleBottomOffset,
                TornadoAttractionCapsuleTopOffset,
                TornadoAttractionAccelerationMillimetersPerSecondSquared,
                TornadoAttractionMinimumDistanceScaleMilli,
                TornadoAttractionTerminalSpeedMillimetersPerSecond,
                DefaultReferenceMassGrams,
                DefaultMinimumMassScaleMilli,
                DefaultMaximumMassScaleMilli,
                useMassScale: true);

        public static int ResolveFireballExplosionImpulseSpeed(int impactPower)
        {
            return CharacterTestDeterministicMath.Clamp(
                impactPower * FireballExplosionImpulseSpeedPerImpactPowerMillimetersPerSecond,
                0,
                FireballExplosionMaxSpeedMillimetersPerSecond);
        }

        public static CharacterTestInteractionPhysicalField FireballExplosionRadialImpulse(int impactPower)
        {
            return CharacterTestInteractionPhysicalField.RadialImpulseSphere(
                CharacterTestInteractionTargetFilter.DynamicCharacters
                    | CharacterTestInteractionTargetFilter.DynamicMovableObjects
                    | CharacterTestInteractionTargetFilter.ExcludeSource,
                ResolveFireballExplosionImpulseSpeed(impactPower),
                linearFalloffMinimumScaleMilli: 0,
                FireballExplosionMaxSpeedMillimetersPerSecond,
                DefaultReferenceMassGrams,
                DefaultMinimumMassScaleMilli,
                DefaultMaximumMassScaleMilli,
                useMassScale: true,
                usePushScale: true,
                useMaterialPushScale: true);
        }

        public static CharacterTestInteractionPhysicalField MeteorBarrageRadialImpulse(int impactPower)
        {
            return FireballExplosionRadialImpulse(impactPower);
        }

        public static int ResolveTornadoAttractionAcceleration(int impactPower)
        {
            return CharacterTestDeterministicMath.Clamp(
                impactPower * TornadoAttractionAccelerationPerImpactPowerMillimetersPerSecondSquared,
                0,
                TornadoAttractionAccelerationMillimetersPerSecondSquared);
        }

        public static CharacterTestInteractionPhysicalField TornadoAttractionCapsule(int impactPower)
        {
            return CharacterTestInteractionPhysicalField.AttractionCapsule(
                CharacterTestInteractionTargetFilter.DynamicCharacters
                    | CharacterTestInteractionTargetFilter.DynamicMovableObjects
                    | CharacterTestInteractionTargetFilter.RequireHostile
                    | CharacterTestInteractionTargetFilter.ExcludeSource,
                TornadoAttractionCapsuleBottomOffset,
                TornadoAttractionCapsuleTopOffset,
                ResolveTornadoAttractionAcceleration(impactPower),
                TornadoAttractionMinimumDistanceScaleMilli,
                TornadoAttractionTerminalSpeedMillimetersPerSecond,
                DefaultReferenceMassGrams,
                DefaultMinimumMassScaleMilli,
                DefaultMaximumMassScaleMilli,
                useMassScale: true);
        }

        /// <summary>Creates the Sword Wave movable-object directional impulse field.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static CharacterTestInteractionPhysicalField SwordWaveDirectionalImpulse(int impactPower)
        {
            int speed = CharacterTestDeterministicMath.Clamp(
                impactPower * SwordWaveDirectionalImpulseSpeedPerImpactPowerMillimetersPerSecond,
                0,
                SwordWaveDirectionalImpulseMaxSpeedMillimetersPerSecond);
            return CharacterTestInteractionPhysicalField.DirectionalImpulseOrientedCapsule(
                CharacterTestInteractionTargetFilter.DynamicMovableObjects,
                -SwordWaveDirectionalImpulseCapsuleHalfLength,
                SwordWaveDirectionalImpulseCapsuleHalfLength,
                speed,
                SwordWaveDirectionalImpulseMaxSpeedMillimetersPerSecond,
                DefaultReferenceMassGrams,
                DefaultMinimumMassScaleMilli,
                DefaultMaximumMassScaleMilli,
                useMassScale: true,
                useMaterialPushScale: true);
        }

        public static CharacterTestInteractionPhysicalField TornadoAttractionCapsule()
        {
            return TornadoAttractionPhysicalField;
        }
    }
}
