using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Audio.Physical;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestWeaponHitAudioRuleBinding
    {
        public CharacterTestWeaponHitAudioRuleBinding(int eventId, bool is3D)
        {
            EventId = eventId;
            Is3D = is3D;
        }

        public int EventId { get; }
        public bool Is3D { get; }
        public bool IsValid => EventId > 0;
    }

    internal readonly struct CharacterTestWeaponHitAudioRule
    {
        public CharacterTestWeaponHitAudioRule(
            HitResolveKind resultKind,
            CombatSurfaceMaterialKind surfaceMaterialKind,
            string cueKey,
            int cooldownFrames,
            int priority,
            int minimumIntensity = 0,
            int weaponId = 0,
            int equipmentSlotId = 0,
            int damageTypeId = 0,
            int hitZoneId = 0,
            int bodyPartId = 0)
        {
            ResultKind = resultKind;
            SurfaceMaterialKind = surfaceMaterialKind;
            CueKey = cueKey ?? string.Empty;
            CooldownFrames = cooldownFrames < 0 ? 0 : cooldownFrames;
            Priority = priority < 0 ? 0 : priority;
            MinimumIntensity = minimumIntensity < 0 ? 0 : minimumIntensity;
            WeaponId = weaponId;
            EquipmentSlotId = equipmentSlotId;
            DamageTypeId = damageTypeId;
            HitZoneId = hitZoneId;
            BodyPartId = bodyPartId;
        }

        public HitResolveKind ResultKind { get; }
        public CombatSurfaceMaterialKind SurfaceMaterialKind { get; }
        public string CueKey { get; }
        public int CooldownFrames { get; }
        public int Priority { get; }
        public int MinimumIntensity { get; }
        public int WeaponId { get; }
        public int EquipmentSlotId { get; }
        public int DamageTypeId { get; }
        public int HitZoneId { get; }
        public int BodyPartId { get; }

        public bool Matches(in CharacterTestWeaponHitAudioEvent evt, out int score)
        {
            score = int.MinValue;
            if (string.IsNullOrEmpty(CueKey))
                return false;
            if (MinimumIntensity > evt.Intensity)
                return false;

            bool resultExact = ResultKind == evt.ResultKind;
            bool resultFallback = (int)ResultKind == 0;
            if (!resultExact && !resultFallback)
                return false;

            bool materialExact = SurfaceMaterialKind == evt.SurfaceMaterial.Kind;
            bool materialFallback = SurfaceMaterialKind == CombatSurfaceMaterialKind.Default;
            if (!materialExact && !materialFallback)
                return false;

            bool weaponExact = MatchesIdentity(WeaponId, evt.WeaponId, out bool weaponFallback);
            if (!weaponExact && !weaponFallback)
                return false;

            bool equipmentExact = MatchesIdentity(EquipmentSlotId, evt.EquipmentSlotId, out bool equipmentFallback);
            if (!equipmentExact && !equipmentFallback)
                return false;

            bool damageExact = MatchesIdentity(DamageTypeId, evt.DamageTypeId, out bool damageFallback);
            if (!damageExact && !damageFallback)
                return false;

            bool hitZoneExact = MatchesIdentity(HitZoneId, evt.HitZoneId, out bool hitZoneFallback);
            if (!hitZoneExact && !hitZoneFallback)
                return false;

            bool bodyPartExact = MatchesIdentity(BodyPartId, evt.BodyPartId, out bool bodyPartFallback);
            if (!bodyPartExact && !bodyPartFallback)
                return false;

            score = Priority + MinimumIntensity;
            if (resultExact)
                score += 10000000;
            if (materialExact)
                score += 1000000;
            if (weaponExact)
                score += 100000;
            if (equipmentExact)
                score += 50000;
            if (damageExact)
                score += 30000;
            if (hitZoneExact)
                score += 20000;
            if (bodyPartExact)
                score += 10000;
            return true;
        }

        private static bool MatchesIdentity(int ruleValue, int eventValue, out bool fallback)
        {
            fallback = ruleValue == 0;
            if (fallback)
                return false;

            return ruleValue == eventValue;
        }
    }

    public readonly struct CharacterTestWeaponHitAudioSummary
    {
        public static readonly CharacterTestWeaponHitAudioSummary Empty =
            new CharacterTestWeaponHitAudioSummary(
                RuntimeFrame.Zero,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                string.Empty,
                0,
                0);

        public CharacterTestWeaponHitAudioSummary(
            RuntimeFrame frame,
            int processedCount,
            int playedCount,
            int damagePlayedCount,
            int blockedPlayedCount,
            int ignoredResultCount,
            int missingRuleSkipCount,
            int missingAudioServiceSkipCount,
            int missingCueSkipCount,
            int cooldownSkipCount,
            int playbackFailedSkipCount,
            string lastCueKey,
            int lastEventId,
            int lastEmitterId,
            int cooldownCapacitySkipCount = 0)
        {
            Frame = frame;
            ProcessedCount = processedCount < 0 ? 0 : processedCount;
            PlayedCount = playedCount < 0 ? 0 : playedCount;
            DamagePlayedCount = damagePlayedCount < 0 ? 0 : damagePlayedCount;
            BlockedPlayedCount = blockedPlayedCount < 0 ? 0 : blockedPlayedCount;
            IgnoredResultCount = ignoredResultCount < 0 ? 0 : ignoredResultCount;
            MissingRuleSkipCount = missingRuleSkipCount < 0 ? 0 : missingRuleSkipCount;
            MissingAudioServiceSkipCount = missingAudioServiceSkipCount < 0 ? 0 : missingAudioServiceSkipCount;
            MissingCueSkipCount = missingCueSkipCount < 0 ? 0 : missingCueSkipCount;
            CooldownSkipCount = cooldownSkipCount < 0 ? 0 : cooldownSkipCount;
            CooldownCapacitySkipCount = cooldownCapacitySkipCount < 0 ? 0 : cooldownCapacitySkipCount;
            PlaybackFailedSkipCount = playbackFailedSkipCount < 0 ? 0 : playbackFailedSkipCount;
            LastCueKey = lastCueKey ?? string.Empty;
            LastEventId = lastEventId < 0 ? 0 : lastEventId;
            LastEmitterId = lastEmitterId < 0 ? 0 : lastEmitterId;
        }

        public RuntimeFrame Frame { get; }
        public int ProcessedCount { get; }
        public int PlayedCount { get; }
        public int DamagePlayedCount { get; }
        public int BlockedPlayedCount { get; }
        public int IgnoredResultCount { get; }
        public int MissingRuleSkipCount { get; }
        public int MissingAudioServiceSkipCount { get; }
        public int MissingCueSkipCount { get; }
        public int CooldownSkipCount { get; }
        public int CooldownCapacitySkipCount { get; }
        public int PlaybackFailedSkipCount { get; }
        public string LastCueKey { get; }
        public int LastEventId { get; }
        public int LastEmitterId { get; }
    }}
