using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.Resources;

namespace MxFramework.CharacterRuntimeSpawn
{
    public static class CharacterRuntimeAnimationProfileResolver
    {
        public const string LocomotionSlotId = "locomotion";

        /// <summary>LowFreqAlloc: resolves the package's sole authoritative locomotion profile slot.</summary>
        public static CharacterRuntimeAnimationProfileResolveResult Resolve(
            CharacterImportedPackage package,
            string animationProfileId)
        {
            CharacterCompiledAnimationArtifacts artifacts = package?.CompiledAnimationArtifacts;
            CharacterAnimationSetDefinition document = artifacts?.AnimationSetDefinition;
            if (document == null || !artifacts.HasRuntimeAnimationContracts)
            {
                return Failed(
                    CharacterRuntimeSpawnIssueCodes.MissingCompiledAnimationArtifact,
                    nameof(CharacterCompiledAnimationArtifacts),
                    "Compiled animation definition, mapping, and clip registry are required.");
            }

            if (string.IsNullOrWhiteSpace(animationProfileId))
            {
                return Failed(
                    CharacterRuntimeSpawnIssueCodes.MissingAnimationProfile,
                    nameof(animationProfileId),
                    "The resolved Character Application profile does not select an animation profile.");
            }

            if (!TryFindExactlyOneProfile(document.Profiles, animationProfileId, out CharacterAnimationProfile profile, out int profileCount))
            {
                return Failed(
                    profileCount == 0
                        ? CharacterRuntimeSpawnIssueCodes.MissingAnimationProfile
                        : CharacterRuntimeSpawnIssueCodes.AmbiguousAnimationProfile,
                    "profiles/profileId",
                    profileCount == 0
                        ? "Animation profile was not found: " + animationProfileId + "."
                        : "Animation profile id is duplicated: " + animationProfileId + ".");
            }

            if (!TryFindExactlyOneLocomotionSlot(profile.Slots, out CharacterAnimationProfileSlot slot, out int slotCount))
            {
                return Failed(
                    slotCount == 0
                        ? CharacterRuntimeSpawnIssueCodes.MissingLocomotionSlot
                        : CharacterRuntimeSpawnIssueCodes.InvalidLocomotionSlot,
                    "profiles/" + animationProfileId + "/slots/" + LocomotionSlotId,
                    slotCount == 0
                        ? "Animation profile must contain the exact locomotion slot."
                        : "Animation profile contains more than one locomotion slot.");
            }

            if (!slot.Required
                || string.IsNullOrWhiteSpace(slot.SetId)
                || string.IsNullOrWhiteSpace(slot.GroupId)
                || string.IsNullOrWhiteSpace(slot.DefaultBlendId))
            {
                return Failed(
                    CharacterRuntimeSpawnIssueCodes.InvalidLocomotionSlot,
                    "profiles/" + animationProfileId + "/slots/" + LocomotionSlotId,
                    "The locomotion slot must be required and declare setId, groupId, and defaultBlendId.");
            }

            if (!TryFindExactlyOneSet(document.Sets, slot.SetId, out CharacterAnimationSet set))
            {
                return Failed(
                    CharacterRuntimeSpawnIssueCodes.MissingLocomotionSet,
                    "profiles/" + animationProfileId + "/slots/" + LocomotionSlotId + "/setId",
                    "Locomotion slot references a missing or duplicated animation set: " + slot.SetId + ".");
            }

            if (!TryFindExactlyOneGroup(set.Groups, slot.GroupId, out CharacterAnimationGroup group))
            {
                return Failed(
                    CharacterRuntimeSpawnIssueCodes.MissingLocomotionGroup,
                    "profiles/" + animationProfileId + "/slots/" + LocomotionSlotId + "/groupId",
                    "Locomotion slot references a missing or duplicated group in set " + slot.SetId + ": " + slot.GroupId + ".");
            }

            if (!TryFindExactlyOneBlend(group.Blends, slot.DefaultBlendId, out CharacterAnimationBlendReference blend, out int blendCount))
            {
                return Failed(
                    blendCount == 0
                        ? CharacterRuntimeSpawnIssueCodes.MissingLocomotionBlend
                        : CharacterRuntimeSpawnIssueCodes.AmbiguousLocomotionBlend,
                    "profiles/" + animationProfileId + "/slots/" + LocomotionSlotId + "/defaultBlendId",
                    blendCount == 0
                        ? "Locomotion blend is not declared by the selected group: " + slot.DefaultBlendId + "."
                        : "Locomotion blend id is duplicated or declared by multiple blend kinds: " + slot.DefaultBlendId + ".");
            }

            if (!ValidateRuntimeBlend(artifacts.RuntimeMappingProvider, slot, blend, out string runtimeBlendIssue))
            {
                return Failed(
                    CharacterRuntimeSpawnIssueCodes.AmbiguousLocomotionBlend,
                    "profiles/" + animationProfileId + "/slots/" + LocomotionSlotId + "/defaultBlendId",
                    runtimeBlendIssue);
            }

            if (!TryResolveResourcePackageId(
                    package.RuntimeResourceCatalog,
                    group,
                    blend,
                    out string resourcePackageId,
                    out string clipIssueCode,
                    out string clipIssue))
            {
                return Failed(
                    clipIssueCode,
                    "profiles/" + animationProfileId + "/slots/" + LocomotionSlotId,
                    clipIssue);
            }

            return new CharacterRuntimeAnimationProfileResolveResult(
                new CharacterRuntimeAnimationProfileBinding(
                    profile.ProfileId,
                    slot.SlotId,
                    slot.SetId,
                    slot.GroupId,
                    slot.DefaultBlendId,
                    blend.BlendKind,
                    resourcePackageId),
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static bool ValidateRuntimeBlend(
            IMxAnimationMappingProvider provider,
            CharacterAnimationProfileSlot slot,
            CharacterAnimationBlendReference selected,
            out string issue)
        {
            issue = string.Empty;
            if (provider == null || !provider.TryFindDefinition(slot.SetId, out MxAnimationSetDefinition definition))
            {
                issue = "Compiled runtime animation set is missing: " + slot.SetId + ".";
                return false;
            }

            int blend1DCount = CountBlend1D(definition, slot.DefaultBlendId);
            int blend2DCount = CountBlend2D(definition, slot.DefaultBlendId);
            bool valid = selected.BlendKind == CharacterAnimationBlendKind.Blend1D
                ? blend1DCount == 1 && blend2DCount == 0
                : selected.BlendKind == CharacterAnimationBlendKind.Blend2D
                    && blend2DCount == 1 && blend1DCount == 0;
            if (!valid)
            {
                issue = "Compiled locomotion blend kind is missing or ambiguous. blendId="
                    + slot.DefaultBlendId + " blend1D=" + blend1DCount + " blend2D=" + blend2DCount + ".";
            }
            return valid;
        }

        private static bool TryResolveResourcePackageId(
            ResourceCatalog catalog,
            CharacterAnimationGroup group,
            CharacterAnimationBlendReference blend,
            out string resourcePackageId,
            out string issueCode,
            out string issue)
        {
            resourcePackageId = string.Empty;
            issueCode = CharacterRuntimeSpawnIssueCodes.MissingAnimationResource;
            issue = string.Empty;
            if (catalog == null)
            {
                issue = "Runtime resource catalog is required for locomotion animation clips.";
                return false;
            }
            if (blend.ClipIds == null || blend.ClipIds.Length == 0)
            {
                issueCode = CharacterRuntimeSpawnIssueCodes.MissingLocomotionClip;
                issue = "Locomotion blend contains no clip points: " + blend.BlendId + ".";
                return false;
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            if (!TryValidateClipResource(
                    catalog,
                    group,
                    blend.DefaultClipId,
                    ref resourcePackageId,
                    visited,
                    out issueCode,
                    out issue))
            {
                return false;
            }
            for (int i = 0; i < blend.ClipIds.Length; i++)
            {
                string clipId = blend.ClipIds[i] ?? string.Empty;
                if (!TryValidateClipResource(catalog, group, clipId, ref resourcePackageId, visited, out issueCode, out issue))
                    return false;
            }

            return !string.IsNullOrWhiteSpace(resourcePackageId);
        }

        private static bool TryValidateClipResource(
            ResourceCatalog catalog,
            CharacterAnimationGroup group,
            string clipId,
            ref string resourcePackageId,
            HashSet<string> visited,
            out string issueCode,
            out string issue)
        {
            issueCode = CharacterRuntimeSpawnIssueCodes.MissingAnimationResource;
            issue = string.Empty;
            if (string.IsNullOrWhiteSpace(clipId))
            {
                issueCode = CharacterRuntimeSpawnIssueCodes.MissingLocomotionClip;
                issue = "Locomotion blend contains an empty default or point clip reference.";
                return false;
            }
            if (!visited.Add(clipId))
                return true;
            if (!TryFindExactlyOneClip(group.Clips, clipId, out CharacterAnimationClipReference clip)
                || string.IsNullOrWhiteSpace(clip.RuntimeResourceKey))
            {
                issueCode = CharacterRuntimeSpawnIssueCodes.MissingLocomotionClip;
                issue = "Locomotion blend references a missing, duplicated, or unbound group clip: " + clipId + ".";
                return false;
            }

            if (!TryFindExactlyOneCatalogClip(catalog, clip.RuntimeResourceKey, out ResourceCatalogEntry entry))
            {
                issue = "Runtime resource catalog does not contain exactly one AnimationClip entry: "
                    + clip.RuntimeResourceKey + ".";
                return false;
            }

            string candidatePackageId = string.IsNullOrWhiteSpace(entry.PackageId)
                ? catalog.PackageId
                : entry.PackageId;
            if (string.IsNullOrWhiteSpace(candidatePackageId))
            {
                issue = "Animation clip resource has no package id: " + clip.RuntimeResourceKey + ".";
                return false;
            }
            if (!string.IsNullOrEmpty(resourcePackageId)
                && !string.Equals(resourcePackageId, candidatePackageId, StringComparison.Ordinal))
            {
                issue = "Locomotion blend clips span multiple resource packages.";
                return false;
            }
            resourcePackageId = candidatePackageId;
            return true;
        }

        private static bool TryFindExactlyOneProfile(
            CharacterAnimationProfile[] profiles,
            string profileId,
            out CharacterAnimationProfile result,
            out int count)
        {
            result = null;
            count = 0;
            for (int i = 0; profiles != null && i < profiles.Length; i++)
            {
                CharacterAnimationProfile candidate = profiles[i];
                if (candidate == null || !string.Equals(candidate.ProfileId, profileId, StringComparison.Ordinal))
                    continue;
                result = candidate;
                count++;
            }
            return count == 1;
        }

        private static bool TryFindExactlyOneLocomotionSlot(
            CharacterAnimationProfileSlot[] slots,
            out CharacterAnimationProfileSlot result,
            out int count)
        {
            result = null;
            count = 0;
            for (int i = 0; slots != null && i < slots.Length; i++)
            {
                CharacterAnimationProfileSlot candidate = slots[i];
                if (candidate == null || !string.Equals(candidate.SlotId, LocomotionSlotId, StringComparison.Ordinal))
                    continue;
                result = candidate;
                count++;
            }
            return count == 1;
        }

        private static bool TryFindExactlyOneSet(CharacterAnimationSet[] sets, string setId, out CharacterAnimationSet result)
        {
            result = null;
            int count = 0;
            for (int i = 0; sets != null && i < sets.Length; i++)
            {
                CharacterAnimationSet candidate = sets[i];
                if (candidate == null || !string.Equals(candidate.SetId, setId, StringComparison.Ordinal))
                    continue;
                result = candidate;
                count++;
            }
            return count == 1;
        }

        private static bool TryFindExactlyOneGroup(CharacterAnimationGroup[] groups, string groupId, out CharacterAnimationGroup result)
        {
            result = null;
            int count = 0;
            for (int i = 0; groups != null && i < groups.Length; i++)
            {
                CharacterAnimationGroup candidate = groups[i];
                if (candidate == null || !string.Equals(candidate.GroupId, groupId, StringComparison.Ordinal))
                    continue;
                result = candidate;
                count++;
            }
            return count == 1;
        }

        private static bool TryFindExactlyOneBlend(
            CharacterAnimationBlendReference[] blends,
            string blendId,
            out CharacterAnimationBlendReference result,
            out int count)
        {
            result = null;
            count = 0;
            for (int i = 0; blends != null && i < blends.Length; i++)
            {
                CharacterAnimationBlendReference candidate = blends[i];
                if (candidate == null || !string.Equals(candidate.BlendId, blendId, StringComparison.Ordinal))
                    continue;
                result = candidate;
                count++;
            }
            return count == 1;
        }

        private static bool TryFindExactlyOneClip(
            CharacterAnimationClipReference[] clips,
            string clipId,
            out CharacterAnimationClipReference result)
        {
            result = null;
            int count = 0;
            for (int i = 0; clips != null && i < clips.Length; i++)
            {
                CharacterAnimationClipReference candidate = clips[i];
                if (candidate == null || !string.Equals(candidate.ClipId, clipId, StringComparison.Ordinal))
                    continue;
                result = candidate;
                count++;
            }
            return count == 1;
        }

        private static bool TryFindExactlyOneCatalogClip(
            ResourceCatalog catalog,
            string resourceId,
            out ResourceCatalogEntry result)
        {
            result = null;
            int count = 0;
            for (int i = 0; catalog?.Entries != null && i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry candidate = catalog.Entries[i];
                if (candidate == null
                    || !string.Equals(candidate.Id, resourceId, StringComparison.Ordinal)
                    || !string.Equals(candidate.TypeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal))
                {
                    continue;
                }
                result = candidate;
                count++;
            }
            return count == 1;
        }

        private static int CountBlend1D(MxAnimationSetDefinition definition, string blendId)
        {
            int count = 0;
            for (int i = 0; definition?.Blend1DDefinitions != null && i < definition.Blend1DDefinitions.Count; i++)
            {
                if (string.Equals(definition.Blend1DDefinitions[i]?.BlendId, blendId, StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        private static int CountBlend2D(MxAnimationSetDefinition definition, string blendId)
        {
            int count = 0;
            for (int i = 0; definition?.Blend2DDefinitions != null && i < definition.Blend2DDefinitions.Count; i++)
            {
                if (string.Equals(definition.Blend2DDefinitions[i]?.BlendId, blendId, StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        private static CharacterRuntimeAnimationProfileResolveResult Failed(
            string issueCode,
            string field,
            string message)
        {
            return new CharacterRuntimeAnimationProfileResolveResult(default, issueCode, field, message);
        }
    }
}
