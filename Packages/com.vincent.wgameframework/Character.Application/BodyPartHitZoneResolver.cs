using System;
using System.Collections.Generic;

namespace MxFramework.CharacterApplication
{
    public enum BodyPartHitZoneResolveStatus
    {
        Success = 0,
        UnknownHitZone = 1,
        UnmappedHitZone = 2,
        AmbiguousHitZone = 3,
        MissingBodyPart = 4
    }

    public readonly struct BodyPartHitZoneResolveResult
    {
        public BodyPartHitZoneResolveResult(
            BodyPartHitZoneResolveStatus status,
            int hitZoneId,
            int partId,
            CharacterBodyPartConfigId bodyPartConfigId,
            int reactionGroupId,
            float damageMultiplier,
            float impulseScale,
            float staggerScale,
            float impactPowerScale,
            bool isWeakPoint,
            CharacterDiagnostic[] diagnostics)
        {
            Status = status;
            HitZoneId = hitZoneId;
            PartId = partId;
            BodyPartConfigId = bodyPartConfigId;
            ReactionGroupId = reactionGroupId;
            DamageMultiplier = damageMultiplier;
            ImpulseScale = impulseScale;
            StaggerScale = staggerScale;
            ImpactPowerScale = impactPowerScale;
            IsWeakPoint = isWeakPoint;
            Diagnostics = diagnostics ?? Array.Empty<CharacterDiagnostic>();
        }

        public BodyPartHitZoneResolveStatus Status { get; }
        public bool IsSuccess => Status == BodyPartHitZoneResolveStatus.Success;
        public int HitZoneId { get; }
        public int PartId { get; }
        public CharacterBodyPartConfigId BodyPartConfigId { get; }
        public int ReactionGroupId { get; }
        public float DamageMultiplier { get; }
        public float ImpulseScale { get; }
        public float StaggerScale { get; }
        public float ImpactPowerScale { get; }
        public bool IsWeakPoint { get; }
        public CharacterDiagnostic[] Diagnostics { get; }
    }

    public static class BodyPartHitZoneResolver
    {
        public static BodyPartHitZoneResolveResult Resolve(
            CharacterBodyProfileConfig bodyProfile,
            IReadOnlyList<CharacterBodyPartConfig> bodyParts,
            int hitZoneId)
        {
            var diagnostics = new CharacterDiagnosticBuilder();
            if (bodyProfile == null)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.MissingBodyProfile,
                    CharacterBodyProfileConfig.TableName,
                    0,
                    string.Empty,
                    nameof(bodyProfile),
                    "Body profile is required.");
                return Failure(BodyPartHitZoneResolveStatus.UnknownHitZone, hitZoneId, diagnostics);
            }

            if ((hitZoneId <= 0))
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.UnknownHitZone,
                    CharacterBodyProfileConfig.TableName,
                    bodyProfile.Id,
                    bodyProfile.StableId,
                    nameof(hitZoneId),
                    "Hit zone id is empty.");
                return Failure(BodyPartHitZoneResolveStatus.UnknownHitZone, hitZoneId, diagnostics);
            }

            List<CharacterHitZoneBindingEntry> explicitBindings = FindExplicitBindings(bodyProfile, hitZoneId);
            if (explicitBindings.Count > 0)
                return ResolveExplicitBinding(bodyProfile, bodyParts, hitZoneId, explicitBindings, diagnostics);

            return ResolveBodyPartFallback(bodyProfile, bodyParts, hitZoneId, diagnostics);
        }

        private static BodyPartHitZoneResolveResult ResolveExplicitBinding(
            CharacterBodyProfileConfig bodyProfile,
            IReadOnlyList<CharacterBodyPartConfig> bodyParts,
            int hitZoneId,
            List<CharacterHitZoneBindingEntry> bindings,
            CharacterDiagnosticBuilder diagnostics)
        {
            CharacterHitZoneBindingEntry selected = bindings[0];
            int topPriority = selected.Priority;
            int topCount = 0;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].Priority > topPriority)
                {
                    selected = bindings[i];
                    topPriority = bindings[i].Priority;
                    topCount = 1;
                }
                else if (bindings[i].Priority == topPriority)
                {
                    topCount++;
                }
            }

            if (topCount > 1)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.AmbiguousHitZone,
                    CharacterBodyProfileConfig.TableName,
                    bodyProfile.Id,
                    bodyProfile.StableId,
                    nameof(CharacterBodyProfileConfig.HitZoneBindings),
                    "Hit zone has multiple explicit bindings with the same top priority.");
                return Failure(BodyPartHitZoneResolveStatus.AmbiguousHitZone, hitZoneId, diagnostics);
            }

            CharacterBodyPartConfig part = FindPartByPartId(bodyParts, bodyProfile.PartSetId, selected.PartId);
            if (part == null)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.MissingBodyPart,
                    CharacterBodyProfileConfig.TableName,
                    bodyProfile.Id,
                    bodyProfile.StableId,
                    nameof(CharacterHitZoneBindingEntry.PartId),
                    "Explicit hit zone binding points to a missing body part: " + selected.PartId + ".");
                return Failure(BodyPartHitZoneResolveStatus.MissingBodyPart, hitZoneId, diagnostics);
            }

            return Success(hitZoneId, part, selected.IsWeakPoint, selected.DamageMultiplierOverride, selected.ImpactPowerScaleOverride, diagnostics);
        }

        private static BodyPartHitZoneResolveResult ResolveBodyPartFallback(
            CharacterBodyProfileConfig bodyProfile,
            IReadOnlyList<CharacterBodyPartConfig> bodyParts,
            int hitZoneId,
            CharacterDiagnosticBuilder diagnostics)
        {
            CharacterBodyPartConfig selected = null;
            int matchCount = 0;
            if (bodyParts != null)
            {
                for (int i = 0; i < bodyParts.Count; i++)
                {
                    CharacterBodyPartConfig part = bodyParts[i];
                    if (part == null)
                        continue;

                    if (!CharacterResolverUtility.EqualsOrdinal(part.PartSetId, bodyProfile.PartSetId))
                        continue;

                    if (!(part.HitZoneId == hitZoneId))
                        continue;

                    selected = part;
                    matchCount++;
                }
            }

            if (matchCount == 0)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.UnmappedHitZone,
                    CharacterBodyProfileConfig.TableName,
                    bodyProfile.Id,
                    bodyProfile.StableId,
                    nameof(CharacterBodyPartConfig.HitZoneId),
                    "Hit zone is not mapped to a body part: " + hitZoneId + ".");
                return Failure(BodyPartHitZoneResolveStatus.UnmappedHitZone, hitZoneId, diagnostics);
            }

            if (matchCount > 1)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.AmbiguousHitZone,
                    CharacterBodyProfileConfig.TableName,
                    bodyProfile.Id,
                    bodyProfile.StableId,
                    nameof(CharacterBodyPartConfig.HitZoneId),
                    "Hit zone maps to multiple body parts without an explicit priority binding.");
                return Failure(BodyPartHitZoneResolveStatus.AmbiguousHitZone, hitZoneId, diagnostics);
            }

            return Success(hitZoneId, selected, selected.IsCritical, 0f, 0f, diagnostics);
        }

        private static List<CharacterHitZoneBindingEntry> FindExplicitBindings(CharacterBodyProfileConfig bodyProfile, int hitZoneId)
        {
            var matches = new List<CharacterHitZoneBindingEntry>();
            if (bodyProfile.HitZoneBindings == null)
                return matches;

            for (int i = 0; i < bodyProfile.HitZoneBindings.Length; i++)
            {
                CharacterHitZoneBindingEntry binding = bodyProfile.HitZoneBindings[i];
                if ((binding.HitZoneId == hitZoneId))
                    matches.Add(binding);
            }

            return matches;
        }

        private static CharacterBodyPartConfig FindPartByPartId(
            IReadOnlyList<CharacterBodyPartConfig> bodyParts,
            string partSetId,
            int partId)
        {
            if (bodyParts == null)
                return null;

            for (int i = 0; i < bodyParts.Count; i++)
            {
                CharacterBodyPartConfig part = bodyParts[i];
                if (part == null)
                    continue;

                if (CharacterResolverUtility.EqualsOrdinal(part.PartSetId, partSetId)
                    && (part.PartId == partId))
                    return part;
            }

            return null;
        }

        private static BodyPartHitZoneResolveResult Success(
            int hitZoneId,
            CharacterBodyPartConfig part,
            bool isWeakPoint,
            float damageMultiplierOverride,
            float impactPowerScaleOverride,
            CharacterDiagnosticBuilder diagnostics)
        {
            float damageMultiplier = damageMultiplierOverride > 0f ? damageMultiplierOverride : part.DamageMultiplier;
            float impactPowerScale = impactPowerScaleOverride > 0f ? impactPowerScaleOverride : part.ImpactPowerScale;
            return new BodyPartHitZoneResolveResult(
                BodyPartHitZoneResolveStatus.Success,
                hitZoneId,
                part.PartId,
                part.BodyPartConfigId,
                part.ReactionGroupId,
                damageMultiplier,
                part.ImpulseScale,
                part.StaggerScale,
                impactPowerScale,
                isWeakPoint,
                diagnostics.ToArray());
        }

        private static BodyPartHitZoneResolveResult Failure(
            BodyPartHitZoneResolveStatus status,
            int hitZoneId,
            CharacterDiagnosticBuilder diagnostics)
        {
            return new BodyPartHitZoneResolveResult(
                status,
                hitZoneId,
                0,
                default,
                0,
                0f,
                0f,
                0f,
                0f,
                false,
                diagnostics.ToArray());
        }
    }
}
