using System;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestInteractionPresentationRules
    {
        public const int VfxSourceDomain = 91001;

        public static bool IsFireballExplosion(CharacterTestInteractionEvent interaction)
        {
            if (interaction.Kind != CharacterTestInteractionKind.PhysicalField
                || interaction.PhysicalField.Shape != CharacterTestInteractionFieldShape.Sphere
                || interaction.PhysicalField.ForceMode != CharacterTestInteractionForceMode.RadialImpulse)
            {
                return false;
            }

            return interaction.StableId == CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId
                || interaction.StableId == CharacterTestCharacterActionConfigSource.FireballExplosionTimelineRepeatStableEventId;
        }

    }
}


