using MxFramework.Audio;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestAudioCueCatalog
    {
        /// <summary>Builds the migration-only hardcoded fallback. Generated catalogs are the normal path.</summary>
        /// <remarks>LowFreqAlloc. Used only when the generated catalog is missing or invalid.</remarks>
        public static CharacterTestAudioCueCatalog CreateLegacyFallback()
        {
            var catalog = new CharacterTestAudioCueCatalog();
            catalog.AddBus(new AudioBusDefinition(DefaultBusId, "character_test", "bus:/"));
            catalog.AddEvent(new AudioEventDefinition(
                LightAttackEventId,
                "character_test.light_attack",
                LightAttackEventPath,
                LightAttackEventGuid,
                AudioEventKind.Event,
                DefaultBusId,
                is3D: true,
                isLoop: false,
                maxDistance: 20f));
            catalog.AddEvent(new AudioEventDefinition(
                FireballExplosionEventId,
                "character_test.fireball_explosion",
                FireballExplosionEventPath,
                FireballExplosionEventGuid,
                AudioEventKind.Event,
                DefaultBusId,
                is3D: true,
                isLoop: false,
                maxDistance: 28f));
            AddMixSnapshot(
                catalog,
                AudioProductEventIds.PausedSnapshot,
                "character_test.snapshot.paused",
                "snapshot:/MxPaused",
                "{b6b3f10b-771b-46fc-bf42-ebfadf117690}");
            AddMixSnapshot(
                catalog,
                AudioProductEventIds.MenuSnapshot,
                "character_test.snapshot.menu",
                "snapshot:/MxMenu",
                "{7c6d1b50-4d7c-4193-ba4e-056b01a8e3b0}");
            AddMixSnapshot(
                catalog,
                AudioProductEventIds.CombatSnapshot,
                "character_test.snapshot.combat",
                "snapshot:/MxCombat",
                "{e9667056-9750-4c94-951f-f04d2f5222f1}");
            AddMixSnapshot(
                catalog,
                AudioProductEventIds.DialogueSnapshot,
                "character_test.snapshot.dialogue",
                "snapshot:/MxDialogue",
                "{39b54c5d-3c64-45a0-8e2f-8bbf161f2207}");
            AddControlledEvent(
                catalog,
                AudioProductEventIds.MenuMusic,
                "character_test.music.menu",
                "event:/MxFramework/Product/Music/Menu",
                "{8b03dc7a-f891-46c5-af9d-b5db4b754ed9}",
                isLoop: true);
            AddControlledEvent(
                catalog,
                AudioProductEventIds.ExplorationMusic,
                "character_test.music.exploration",
                "event:/MxFramework/Product/Music/Exploration",
                "{cce3face-9436-4c64-b28e-492da53ec672}",
                isLoop: true);
            AddControlledEvent(
                catalog,
                AudioProductEventIds.CombatMusic,
                "character_test.music.combat",
                "event:/MxFramework/Product/Music/Combat",
                "{fc6d0adb-4d0a-4fe4-ad2a-b170abe19cfa}",
                isLoop: true);
            AddControlledEvent(
                catalog,
                AudioProductEventIds.UiClick,
                "character_test.ui.click",
                "event:/MxFramework/Product/UI/Click",
                "{0a368b45-f85e-4aa9-91ca-6523585ffd35}",
                isLoop: false);
            AddControlledEvent(
                catalog,
                AudioProductEventIds.UiConfirm,
                "character_test.ui.confirm",
                "event:/MxFramework/Product/UI/Confirm",
                "{e079b829-95b1-40f1-9e51-4cb13f0fb5f4}",
                isLoop: false);
            AddControlledEvent(
                catalog,
                AudioProductEventIds.UiBack,
                "character_test.ui.back",
                "event:/MxFramework/Product/UI/Back",
                "{c5a6f56d-ee46-435b-80ef-e831bc797e2a}",
                isLoop: false);
            AddControlledEvent(
                catalog,
                AudioProductEventIds.WindAmbience,
                "character_test.ambience.wind",
                "event:/MxFramework/Product/Ambience/Wind",
                "{e37d5708-9eec-437d-9564-9dc39cf5a668}",
                isLoop: true);
            AddControlledEvent(
                catalog,
                AudioProductEventIds.VoiceReady,
                "character_test.voice.ready",
                "event:/MxFramework/Product/Voice/Ready",
                "{e21277c6-3466-4359-babe-4a14a6359801}",
                isLoop: false);
            catalog.AddCue(
                LightAttackCueKey,
                new AudioCueDefinition(
                    LightAttackCueId,
                    LightAttackCueKey,
                    LightAttackEventId,
                    AudioCuePlayMode.OneShot));
            AddOneShot3DCue(
                catalog,
                LightAttackAirDisplacementCueKey,
                LightAttackAirDisplacementCueId,
                LightAttackAirDisplacementEventId,
                "character_test.air_displacement.light_attack",
                LightAttackAirDisplacementEventPath,
                LightAttackAirDisplacementEventGuid,
                24f);
            catalog.AddCue(
                FireballExplosionCueKey,
                new AudioCueDefinition(
                    FireballExplosionCueId,
                    FireballExplosionCueKey,
                    FireballExplosionEventId,
                    AudioCuePlayMode.OneShot));
            AddOneShot3DCue(
                catalog,
                SoleContactDefaultCueKey,
                SoleContactDefaultCueId,
                SoleContactDefaultEventId,
                "character_test.body_contact.sole.default",
                SoleContactDefaultEventPath,
                PhysicalAudioEventGuid,
                18f);
            AddOneShot3DCue(
                catalog,
                SoleContactRoughCueKey,
                SoleContactRoughCueId,
                SoleContactRoughEventId,
                "character_test.body_contact.sole.rough",
                SoleContactRoughEventPath,
                PhysicalAudioEventGuid,
                18f);
            AddOneShot3DCue(
                catalog,
                SoleContactHardSlickCueKey,
                SoleContactHardSlickCueId,
                SoleContactHardSlickEventId,
                "character_test.body_contact.sole.hard_slick",
                SoleContactHardSlickEventPath,
                PhysicalAudioEventGuid,
                18f);
            AddOneShot3DCue(
                catalog,
                SoleContactElasticCueKey,
                SoleContactElasticCueId,
                SoleContactElasticEventId,
                "character_test.body_contact.sole.elastic",
                SoleContactElasticEventPath,
                PhysicalAudioEventGuid,
                20f);
            AddOneShot3DCue(
                catalog,
                LandingDefaultCueKey,
                LandingDefaultCueId,
                LandingDefaultEventId,
                "character_test.landing.default",
                LandingDefaultEventPath,
                PhysicalAudioEventGuid,
                22f);
            AddOneShot3DCue(
                catalog,
                LandingRoughCueKey,
                LandingRoughCueId,
                LandingRoughEventId,
                "character_test.landing.rough",
                LandingRoughEventPath,
                PhysicalAudioEventGuid,
                22f);
            AddOneShot3DCue(
                catalog,
                LandingHardSlickCueKey,
                LandingHardSlickCueId,
                LandingHardSlickEventId,
                "character_test.landing.hard_slick",
                LandingHardSlickEventPath,
                PhysicalAudioEventGuid,
                22f);
            AddOneShot3DCue(
                catalog,
                LandingElasticCueKey,
                LandingElasticCueId,
                LandingElasticEventId,
                "character_test.landing.elastic",
                LandingElasticEventPath,
                PhysicalAudioEventGuid,
                24f);
            AddOneShot3DCue(
                catalog,
                ObjectCollisionDefaultCueKey,
                ObjectCollisionDefaultCueId,
                ObjectCollisionDefaultEventId,
                "character_test.object_collision.default",
                ObjectCollisionDefaultEventPath,
                PhysicalAudioEventGuid,
                24f);
            AddOneShot3DCue(
                catalog,
                ObjectCollisionRoughCueKey,
                ObjectCollisionRoughCueId,
                ObjectCollisionRoughEventId,
                "character_test.object_collision.rough",
                ObjectCollisionRoughEventPath,
                PhysicalAudioEventGuid,
                24f);
            AddOneShot3DCue(
                catalog,
                ObjectCollisionHardSlickCueKey,
                ObjectCollisionHardSlickCueId,
                ObjectCollisionHardSlickEventId,
                "character_test.object_collision.hard_slick",
                ObjectCollisionHardSlickEventPath,
                PhysicalAudioEventGuid,
                24f);
            AddOneShot3DCue(
                catalog,
                ObjectCollisionElasticCueKey,
                ObjectCollisionElasticCueId,
                ObjectCollisionElasticEventId,
                "character_test.object_collision.elastic",
                ObjectCollisionElasticEventPath,
                PhysicalAudioEventGuid,
                26f);
            AddOneShot3DCue(
                catalog,
                WeaponHitDefaultCueKey,
                WeaponHitDefaultCueId,
                WeaponHitDefaultEventId,
                "character_test.weapon_hit.default",
                WeaponHitDefaultEventPath,
                PhysicalAudioEventGuid,
                24f);
            AddOneShot3DCue(
                catalog,
                WeaponHitBlockedCueKey,
                WeaponHitBlockedCueId,
                WeaponHitBlockedEventId,
                "character_test.weapon_hit.blocked",
                WeaponHitBlockedEventPath,
                PhysicalAudioEventGuid,
                26f);
            AddOneShot3DCue(
                catalog,
                WeaponHitRoughCueKey,
                WeaponHitRoughCueId,
                WeaponHitRoughEventId,
                "character_test.weapon_hit.rough",
                WeaponHitRoughEventPath,
                PhysicalAudioEventGuid,
                24f);
            AddOneShot3DCue(
                catalog,
                WeaponHitHardSlickCueKey,
                WeaponHitHardSlickCueId,
                WeaponHitHardSlickEventId,
                "character_test.weapon_hit.hard_slick",
                WeaponHitHardSlickEventPath,
                PhysicalAudioEventGuid,
                26f);
            AddOneShot3DCue(
                catalog,
                WeaponHitElasticCueKey,
                WeaponHitElasticCueId,
                WeaponHitElasticEventId,
                "character_test.weapon_hit.elastic",
                WeaponHitElasticEventPath,
                PhysicalAudioEventGuid,
                24f);
            return catalog;
        }

        private static void AddMixSnapshot(
            CharacterTestAudioCueCatalog catalog,
            int eventId,
            string name,
            string path,
            string guid)
        {
            catalog.AddEvent(new AudioEventDefinition(
                eventId,
                name,
                path,
                guid,
                AudioEventKind.Snapshot,
                DefaultBusId,
                is3D: false,
                isLoop: true,
                maxDistance: 0f));
        }

        private static void AddControlledEvent(
            CharacterTestAudioCueCatalog catalog,
            int eventId,
            string name,
            string path,
            string guid,
            bool isLoop)
        {
            catalog.AddEvent(new AudioEventDefinition(
                eventId,
                name,
                path,
                guid,
                AudioEventKind.Event,
                DefaultBusId,
                is3D: false,
                isLoop: isLoop,
                maxDistance: 0f));
        }

        /// <summary>Compatibility alias for existing tests and callers.</summary>
        /// <remarks>LowFreqAlloc. Prefer generated catalog loading in composition roots.</remarks>
        public static CharacterTestAudioCueCatalog CreateDefault()
        {
            return CreateLegacyFallback();
        }

        private static void AddOneShot3DCue(
            CharacterTestAudioCueCatalog catalog,
            string cueKey,
            int cueId,
            int eventId,
            string eventName,
            string eventPath,
            string eventGuid,
            float maxDistance)
        {
            catalog.AddEvent(new AudioEventDefinition(
                eventId,
                eventName,
                eventPath,
                eventGuid,
                AudioEventKind.Event,
                DefaultBusId,
                is3D: true,
                isLoop: false,
                maxDistance: maxDistance,
                parameters: PhysicalAudioParameters));
            catalog.AddCue(
                cueKey,
                new AudioCueDefinition(
                    cueId,
                    cueKey,
                    eventId,
                    AudioCuePlayMode.OneShot));
        }

    }
}
