using System;
using MxFramework.Core.Math;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestAbilityEffectTimelineSource
    {
        public const string DefaultGeneratedCatalogPath =
            "Assets/MxFrameworkGenerated/Abilities/ability_effect_timelines.json";
        public const int FireballExplosionTimelineId = CharacterTestAuthoredContentIds.D401_Id1;
        public const string FireballExplosionTimelineLabel = "character_test.fireball_explosion.afterblast";

        public static AbilityEffectTimelineRegistry CreateRegistry()
        {
            AbilityEffectTimelineRegistry registry = LoadGeneratedOrEmpty(DefaultGeneratedCatalogPath);
            return registry.Count > 0 ? registry : CreateEmbeddedFallback();
        }

        public static AbilityEffectTimelineRegistry LoadGeneratedOrEmpty(string path)
        {
            return CharacterTestRuntimeCatalogBinaryFallback.LoadBinaryThenJsonOrEmpty(
                path,
                AbilityEffectTimelineBinaryImporter.CatalogKind,
                AbilityEffectTimelineBinaryImporter.ImportFile,
                AbilityEffectTimelineJsonImporter.ImportFile,
                registry => registry?.Count ?? 0,
                () => new AbilityEffectTimelineRegistry());
        }

        public static AbilityEffectTimelineRegistry CreateEmbeddedFallback()
        {
            var registry = new AbilityEffectTimelineRegistry(new[]
            {
                new AbilityEffectTimelineDefinition(
                    FireballExplosionTimelineId,
                    121,
                    new[]
                    {
                        new AbilityEffectTimelineEvent(
                            CharacterTestAuthoredContentIds.D401_Id2,
                            AbilityEffectTimelineTrackKind.Presentation,
                            AbilityEffectTimelineEventKind.SpawnVfx,
                            frame: 0,
                            effectId: CharacterTestVfxIds.FireballExplosionImpactDefinitionId,
                            eventLabel: "fireball_explosion.initial_vfx"),
                        new AbilityEffectTimelineEvent(
                            CharacterTestAuthoredContentIds.D401_Id3,
                            AbilityEffectTimelineTrackKind.Combat,
                            AbilityEffectTimelineEventKind.AreaPulse,
                            frame: 120,
                            shapeKind: AbilityEffectTimelineShapeKind.Sphere,
                            radius: Fix64.FromRatio(5, 2),
                            layerMask: 256,
                            requestId: CharacterTestAuthoredContentIds.D401_Id5,
                            eventLabel: "fireball_explosion.repeat_pulse"),
                        new AbilityEffectTimelineEvent(
                            CharacterTestAuthoredContentIds.D401_Id4,
                            AbilityEffectTimelineTrackKind.Presentation,
                            AbilityEffectTimelineEventKind.SpawnVfx,
                            frame: 120,
                            effectId: CharacterTestVfxIds.FireballExplosionImpactDefinitionId,
                            eventLabel: "fireball_explosion.repeat_vfx")
                    },
                    FireballExplosionTimelineLabel)
            });
            CharacterTestRuntimeCatalogLoadDiagnostics.RecordEmbedded(
                AbilityEffectTimelineBinaryImporter.CatalogKind,
                registry.Count,
                "Generated ability effect timeline catalog was empty or missing.");
            return registry;
        }
    }
}
