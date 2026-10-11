using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using UnityEngine;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    public readonly struct CharacterProceduralBodyRegionBinding
    {
        public CharacterProceduralBodyRegionBinding(
            int regionId,
            int[] hitZoneIds,
            Transform anchor,
            Transform[] affectedBones,
            float strengthScale = 1f,
            float durationScale = 1f,
            float stiffness = 42f,
            float damping = 11f,
            float maxAngleDegrees = 24f,
            float impulseScale = 18f,
            int maxAutoDepth = 3)
        {
            RegionId = CharacterProceduralBodyRegionIds.NormalizeBindingRegion(regionId);
            HitZoneIds = hitZoneIds ?? Array.Empty<int>();
            Anchor = anchor;
            AffectedBones = affectedBones ?? Array.Empty<Transform>();
            StrengthScale = strengthScale > 0f ? strengthScale : 1f;
            DurationScale = durationScale > 0f ? durationScale : 1f;
            Stiffness = Mathf.Max(0f, stiffness);
            Damping = Mathf.Max(0f, damping);
            MaxAngleDegrees = Mathf.Max(0f, maxAngleDegrees);
            ImpulseScale = Mathf.Max(0f, impulseScale);
            MaxAutoDepth = Math.Max(0, maxAutoDepth);
        }

        public int RegionId { get; }
        public int[] HitZoneIds { get; }
        public Transform Anchor { get; }
        public Transform[] AffectedBones { get; }
        public float StrengthScale { get; }
        public float DurationScale { get; }
        public float Stiffness { get; }
        public float Damping { get; }
        public float MaxAngleDegrees { get; }
        public float ImpulseScale { get; }
        public int MaxAutoDepth { get; }
        public bool IsValid => Anchor != null && RegionId > 0;
    }

    public readonly struct CharacterProceduralBodyRegionInstallResult
    {
        public CharacterProceduralBodyRegionInstallResult(
            int requestedRegionCount,
            int installedRegionCount,
            string lastIssue)
        {
            RequestedRegionCount = Math.Max(0, requestedRegionCount);
            InstalledRegionCount = Math.Max(0, installedRegionCount);
            LastIssue = lastIssue ?? string.Empty;
        }

        public int RequestedRegionCount { get; }
        public int InstalledRegionCount { get; }
        public string LastIssue { get; }
        public bool InstalledAny => InstalledRegionCount > 0;
    }

    public static class CharacterProceduralBodyRegionInstaller
    {
        private const int DefaultMaxAutoDepth = 3;

        public static CharacterProceduralBodyRegionInstallResult InstallFromImportedPackage(
            GameObject instance,
            CharacterImportedPackage package)
        {
            if (instance == null)
                return new CharacterProceduralBodyRegionInstallResult(0, 0, "character instance is not available");
            if (package == null)
                return new CharacterProceduralBodyRegionInstallResult(0, 0, "character imported package is not available");

            CharacterBodyProfileConfig bodyProfile = ResolveDefaultBodyProfile(package);
            CharacterBodyPartConfig[] bodyParts = ResolveBodyParts(package, bodyProfile);
            if (bodyParts.Length == 0)
                return new CharacterProceduralBodyRegionInstallResult(0, 0, "character package has no body parts");

            int requested = 0;
            int installed = 0;
            string lastIssue = string.Empty;
            Transform firstAnchor = null;
            Transform torsoAnchor = null;
            for (int i = 0; i < bodyParts.Length; i++)
            {
                CharacterBodyPartConfig part = bodyParts[i];
                if (part == null || part.PartId <= 0)
                    continue;

                requested++;
                CharacterProceduralBodyRegionBinding binding =
                    CreateBinding(instance, package, part, out string issue);
                if (!binding.IsValid)
                {
                    lastIssue = issue;
                    continue;
                }

                InstallChain(binding);
                installed++;
                if (firstAnchor == null)
                    firstAnchor = binding.Anchor;
                if (torsoAnchor == null
                    && part.PartId == CharacterAuthorityPoseRegionIds.Torso)
                {
                    torsoAnchor = binding.Anchor;
                }
            }

            Transform fallbackAnchor = torsoAnchor != null ? torsoAnchor : firstAnchor;
            if (fallbackAnchor != null)
            {
                InstallChain(new CharacterProceduralBodyRegionBinding(
                    CharacterProceduralBodyRegionIds.Default,
                    Array.Empty<int>(),
                    fallbackAnchor,
                    CollectAffectedBones(fallbackAnchor, DefaultMaxAutoDepth),
                    strengthScale: 1f,
                    durationScale: 1f,
                    stiffness: 42f,
                    damping: 11f,
                    maxAngleDegrees: 18f,
                    impulseScale: 12f,
                    maxAutoDepth: DefaultMaxAutoDepth));
                installed++;
            }

            return new CharacterProceduralBodyRegionInstallResult(requested, installed, lastIssue);
        }

        private static CharacterProceduralBodyRegionBinding CreateBinding(
            GameObject instance,
            CharacterImportedPackage package,
            CharacterBodyPartConfig part,
            out string issue)
        {
            issue = string.Empty;
            Transform anchor = ResolvePartAnchor(instance.transform, package, part);
            if (anchor == null)
            {
                issue = "no transform found for body region " + part.PartId;
                return default;
            }

            float strengthScale = part.ImpulseScale > 0f ? part.ImpulseScale : 1f;
            return new CharacterProceduralBodyRegionBinding(
                part.PartId,
                ResolveHitZones(package, part.PartId, part.HitZoneId),
                anchor,
                CollectAffectedBones(anchor, DefaultMaxAutoDepth),
                strengthScale,
                durationScale: 1f,
                stiffness: 42f,
                damping: 11f,
                ResolveMaxAngle(part),
                ResolveChainImpulseScale(part),
                DefaultMaxAutoDepth);
        }

        private static void InstallChain(CharacterProceduralBodyRegionBinding binding)
        {
            CharacterSecondaryBoneChain chain = binding.Anchor.gameObject.AddComponent<CharacterSecondaryBoneChain>();
            chain.ConfigureRuntimeChain(binding.Anchor, binding.RegionId, binding.AffectedBones);
            chain.ConfigureRuntimeTuning(
                binding.Stiffness,
                binding.Damping,
                binding.MaxAngleDegrees,
                binding.ImpulseScale,
                binding.MaxAutoDepth);
            chain.CaptureBindPose();
        }

        private static CharacterBodyProfileConfig ResolveDefaultBodyProfile(CharacterImportedPackage package)
        {
            CharacterImportedConfigSet configs = package?.Configs;
            CharacterConfig character = configs?.Characters != null && configs.Characters.Length > 0
                ? configs.Characters[0]
                : null;
            return character == null
                ? (configs?.BodyProfiles != null && configs.BodyProfiles.Length > 0 ? configs.BodyProfiles[0] : null)
                : configs?.FindBodyProfile(character.BodyProfileId);
        }

        private static CharacterBodyPartConfig[] ResolveBodyParts(
            CharacterImportedPackage package,
            CharacterBodyProfileConfig bodyProfile)
        {
            CharacterBodyPartConfig[] source = package?.Configs?.BodyParts ?? Array.Empty<CharacterBodyPartConfig>();
            if (bodyProfile == null || string.IsNullOrEmpty(bodyProfile.PartSetId))
                return source;

            var filtered = new List<CharacterBodyPartConfig>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                CharacterBodyPartConfig part = source[i];
                if (part != null
                    && string.Equals(part.PartSetId, bodyProfile.PartSetId, StringComparison.Ordinal))
                {
                    filtered.Add(part);
                }
            }

            return filtered.ToArray();
        }

        private static Transform ResolvePartAnchor(
            Transform root,
            CharacterImportedPackage package,
            CharacterBodyPartConfig part)
        {
            if (root == null || part == null)
                return null;

            Transform anchor = ResolveLocator(root, part.LocatorId);
            if (anchor != null)
                return anchor;

            CharacterBodyColliderRuntimeBinding collider = FindColliderForPart(package, part.PartId);
            if (collider != null)
            {
                anchor = ResolveLocator(root, collider.LocalPose.ParentPath);
                if (anchor != null)
                    return anchor;
            }


            return null;
        }

        private static Transform ResolveLocator(Transform root, string locatorIdOrPath)
        {
            if (root == null || string.IsNullOrWhiteSpace(locatorIdOrPath))
                return null;

            string normalized = NormalizeLocatorToken(locatorIdOrPath);
            Transform found = FindDescendantByExactOrSuffix(root, locatorIdOrPath);
            if (found != null)
                return found;

            found = FindDescendantByExactOrSuffix(root, normalized);
            if (found != null)
                return found;

            string[] tokens = normalized.Split(new[] { '.', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = tokens.Length - 1; i >= 0; i--)
            {
                found = FindDescendantByExactOrSuffix(root, tokens[i]);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static CharacterBodyColliderRuntimeBinding FindColliderForPart(
            CharacterImportedPackage package,
            int partId)
        {
            CharacterBodyColliderRuntimeBinding[] colliders =
                package?.Geometry?.BodyColliders ?? Array.Empty<CharacterBodyColliderRuntimeBinding>();
            CharacterBodyColliderRuntimeBinding best = null;
            for (int i = 0; i < colliders.Length; i++)
            {
                CharacterBodyColliderRuntimeBinding collider = colliders[i];
                if (collider == null
                    || collider.PartId != partId)
                {
                    continue;
                }

                if (best == null || collider.Priority > best.Priority)
                    best = collider;
            }

            return best;
        }

        private static int[] ResolveHitZones(
            CharacterImportedPackage package,
            int partId,
            int defaultHitZone)
        {
            var zones = new List<int>();
            AddDistinct(zones, defaultHitZone);
            CharacterBodyColliderRuntimeBinding[] colliders =
                package?.Geometry?.BodyColliders ?? Array.Empty<CharacterBodyColliderRuntimeBinding>();
            for (int i = 0; i < colliders.Length; i++)
            {
                CharacterBodyColliderRuntimeBinding collider = colliders[i];
                if (collider != null
                    && collider.PartId == partId)
                {
                    AddDistinct(zones, collider.HitZoneId);
                }
            }

            CharacterHitZoneRuntimeBinding[] hitZones =
                package?.Geometry?.HitZoneBindings ?? Array.Empty<CharacterHitZoneRuntimeBinding>();
            for (int i = 0; i < hitZones.Length; i++)
            {
                CharacterHitZoneRuntimeBinding hitZone = hitZones[i];
                if (hitZone != null
                    && hitZone.PartId == partId)
                {
                    AddDistinct(zones, hitZone.HitZoneId);
                }
            }

            return zones.ToArray();
        }

        private static Transform[] CollectAffectedBones(Transform anchor, int maxDepth)
        {
            if (anchor == null)
                return Array.Empty<Transform>();

            int capacity = Math.Max(1, maxDepth + 1);
            Transform[] buffer = new Transform[capacity];
            int count = CollectPrimaryChain(anchor, buffer, 0, 0, Math.Max(0, maxDepth));
            if (count == 0)
                return new[] { anchor };

            var result = new Transform[count];
            Array.Copy(buffer, result, count);
            return result;
        }

        private static int CollectPrimaryChain(
            Transform node,
            Transform[] buffer,
            int count,
            int depth,
            int maxDepth)
        {
            if (node == null || count >= buffer.Length || depth > maxDepth)
                return count;

            buffer[count++] = node;
            if (node.childCount == 0 || depth == maxDepth)
                return count;

            return CollectPrimaryChain(node.GetChild(0), buffer, count, depth + 1, maxDepth);
        }

        private static float ResolveMaxAngle(CharacterBodyPartConfig part)
        {
            switch (part.PartKind)
            {
                case CharacterBodyPartKind.Head:
                    return 18f;
                case CharacterBodyPartKind.Hand:
                case CharacterBodyPartKind.Foot:
                case CharacterBodyPartKind.Wing:
                case CharacterBodyPartKind.Tail:
                    return 28f;
                case CharacterBodyPartKind.Leg:
                case CharacterBodyPartKind.Arm:
                    return 22f;
                default:
                    return 20f;
            }
        }

        private static float ResolveChainImpulseScale(CharacterBodyPartConfig part)
        {
            switch (part.PartKind)
            {
                case CharacterBodyPartKind.Head:
                    return 14f;
                case CharacterBodyPartKind.Hand:
                case CharacterBodyPartKind.Foot:
                case CharacterBodyPartKind.Wing:
                case CharacterBodyPartKind.Tail:
                    return 18f;
                case CharacterBodyPartKind.Leg:
                case CharacterBodyPartKind.Arm:
                    return 15f;
                default:
                    return 12f;
            }
        }

        private static Transform FindDescendantByExactOrSuffix(Transform root, string pathOrName)
        {
            if (root == null || string.IsNullOrWhiteSpace(pathOrName))
                return null;

            string normalized = pathOrName.Replace('\\', '/').Trim();
            string[] segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            Transform[] transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (string.Equals(candidate.name, normalized, StringComparison.OrdinalIgnoreCase)
                    || PathEndsWith(candidate, segments))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool PathEndsWith(Transform transform, string[] segments)
        {
            if (transform == null || segments == null || segments.Length == 0)
                return false;

            int index = segments.Length - 1;
            Transform current = transform;
            while (current != null && index >= 0)
            {
                if (!string.Equals(current.name, segments[index], StringComparison.OrdinalIgnoreCase))
                    return false;

                current = current.parent;
                index--;
            }

            return index < 0;
        }

        private static string NormalizeLocatorToken(string value)
        {
            string normalized = value.Trim();
            if (normalized.StartsWith("bone.", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("bone.".Length);
            if (normalized.StartsWith("loc.", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("loc.".Length);
            if (normalized.StartsWith("locator.", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("locator.".Length);

            return normalized;
        }

        private static void AddDistinct(List<int> values, int value)
        {
            if (values == null || value <= 0)
                return;

            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return;
            }

            values.Add(value);
        }
    }
}
