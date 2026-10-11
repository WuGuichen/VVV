using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MxFramework.Authoring
{
    /// <summary>
    /// Editable CharacterAction source library. Runtime/Unity projections are generated from this
    /// document and never become authoring authority.
    /// AllocByDesign: authoring/compiler only.
    /// </summary>
    public sealed class CharacterActionSourceLibrary
    {
        public const string CurrentSchemaVersion = "0.3.0";

        public string SchemaVersion { get; set; } = CurrentSchemaVersion;
        public List<CharacterActionAnimationSource> AnimationSources { get; set; } =
            new List<CharacterActionAnimationSource>();
        public List<JsonElement> Actions { get; set; } = new List<JsonElement>();

        public bool TryGetAction(int actionId, out JsonElement action)
        {
            for (int i = 0; Actions != null && i < Actions.Count; i++)
            {
                JsonElement candidate = Actions[i];
                if (candidate.ValueKind == JsonValueKind.Object &&
                    candidate.TryGetProperty("actionId", out JsonElement value) &&
                    value.ValueKind == JsonValueKind.Number &&
                    value.TryGetInt32(out int candidateId) &&
                    candidateId == actionId)
                {
                    action = candidate;
                    return true;
                }
            }

            action = default;
            return false;
        }

        public static int GetActionId(JsonElement action)
        {
            return action.ValueKind == JsonValueKind.Object &&
                action.TryGetProperty("actionId", out JsonElement value) &&
                value.TryGetInt32(out int actionId)
                ? actionId
                : 0;
        }
    }

    /// <summary>
    /// Declares the source rig for animation resources referenced by CharacterActions.
    /// The longest matching resource-key prefix wins, which lets copied Common actions retain
    /// their original Humanoid source while character-native actions use the character rig.
    /// AllocByDesign: authoring/compiler metadata only.
    /// </summary>
    public sealed class CharacterActionAnimationSource
    {
        public string ResourceKeyPrefix { get; set; } = string.Empty;
        public string RigType { get; set; } = GlobalAnimationRigTypes.Unspecified;
        public string SkeletonProfileId { get; set; } = string.Empty;
        public string AvatarProfileId { get; set; } = string.Empty;
        public bool AllowRetargeting { get; set; }
    }

    /// <summary>
    /// Resolves every animation clip reference in one CharacterAction against the effective
    /// Profile libraries and the target character rig.
    /// AllocByDesign: low-frequency authoring validation and CharacterStudio catalog only.
    /// </summary>
    public static class CharacterActionAnimationCompatibilityResolver
    {
        public static GlobalAnimationCompatibilityResult Resolve(
            CharacterResourcePackage characterPackage,
            JsonElement action,
            params CharacterActionSourceLibrary[] libraries)
        {
            var resourceKeys = new HashSet<string>(StringComparer.Ordinal);
            CollectAnimationResourceKeys(action, resourceKeys);
            if (resourceKeys.Count == 0)
            {
                return new GlobalAnimationCompatibilityResult(
                    GlobalAnimationCompatibilityKind.Native,
                    "The action does not reference an animation clip resource.");
            }

            GlobalAnimationCompatibilityKind effectiveKind = GlobalAnimationCompatibilityKind.Native;
            foreach (string resourceKey in resourceKeys)
            {
                CharacterActionAnimationSource source = FindSource(resourceKey, libraries, out bool ambiguous);
                if (ambiguous)
                {
                    return new GlobalAnimationCompatibilityResult(
                        GlobalAnimationCompatibilityKind.Incompatible,
                        "Animation resource '" + resourceKey +
                        "' matches conflicting source-rig declarations.");
                }
                if (source == null)
                {
                    return new GlobalAnimationCompatibilityResult(
                        GlobalAnimationCompatibilityKind.Incompatible,
                        "Animation resource '" + resourceKey +
                        "' has no source-rig declaration in the effective Action Libraries.");
                }

                GlobalAnimationCompatibilityResult compatibility =
                    GlobalWeaponConfigurationResolver.ResolveAnimationCompatibility(
                        characterPackage,
                        CreateCompatibilityConfiguration(source));
                if (!compatibility.IsCompatible)
                {
                    return new GlobalAnimationCompatibilityResult(
                        GlobalAnimationCompatibilityKind.Incompatible,
                        "Animation resource '" + resourceKey + "' is incompatible: " +
                        compatibility.Message);
                }
                if (compatibility.RequiresRetargeting)
                    effectiveKind = GlobalAnimationCompatibilityKind.HumanoidRetargeting;
            }

            return effectiveKind == GlobalAnimationCompatibilityKind.HumanoidRetargeting
                ? new GlobalAnimationCompatibilityResult(
                    effectiveKind,
                    "The action uses Humanoid animation resources retargeted through the target character Avatar.")
                : new GlobalAnimationCompatibilityResult(
                    effectiveKind,
                    "The action animation resources use the character's native skeleton and Avatar identities.");
        }

        private static CharacterActionAnimationSource FindSource(
            string resourceKey,
            CharacterActionSourceLibrary[] libraries,
            out bool ambiguous)
        {
            CharacterActionAnimationSource result = null;
            int longestPrefix = -1;
            ambiguous = false;
            for (int libraryIndex = 0; libraries != null && libraryIndex < libraries.Length; libraryIndex++)
            {
                List<CharacterActionAnimationSource> sources = libraries[libraryIndex]?.AnimationSources;
                for (int sourceIndex = 0; sources != null && sourceIndex < sources.Count; sourceIndex++)
                {
                    CharacterActionAnimationSource candidate = sources[sourceIndex];
                    string prefix = candidate?.ResourceKeyPrefix ?? string.Empty;
                    if (prefix.Length == 0 || !resourceKey.StartsWith(prefix, StringComparison.Ordinal))
                        continue;
                    if (prefix.Length > longestPrefix)
                    {
                        result = candidate;
                        longestPrefix = prefix.Length;
                        ambiguous = false;
                    }
                    else if (prefix.Length == longestPrefix && !SameSource(result, candidate))
                    {
                        ambiguous = true;
                    }
                }
            }
            return result;
        }

        private static bool SameSource(
            CharacterActionAnimationSource left,
            CharacterActionAnimationSource right)
        {
            return left != null && right != null
                && string.Equals(left.ResourceKeyPrefix, right.ResourceKeyPrefix, StringComparison.Ordinal)
                && string.Equals(left.RigType, right.RigType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.SkeletonProfileId, right.SkeletonProfileId, StringComparison.Ordinal)
                && string.Equals(left.AvatarProfileId, right.AvatarProfileId, StringComparison.Ordinal)
                && left.AllowRetargeting == right.AllowRetargeting;
        }

        private static GlobalAnimationConfiguration CreateCompatibilityConfiguration(
            CharacterActionAnimationSource source)
        {
            return new GlobalAnimationConfiguration
            {
                AnimationConfigurationId = "character-action-source:" + source.ResourceKeyPrefix,
                RigType = source.RigType ?? string.Empty,
                SkeletonProfileId = source.SkeletonProfileId ?? string.Empty,
                AvatarProfileId = source.AvatarProfileId ?? string.Empty,
                Sets =
                {
                    new AnimationAuthoringSet
                    {
                        SetId = "character-action-source",
                        Compatibility = new AnimationCompatibilityExpectationAuthoring
                        {
                            AllowRetargeting = source.AllowRetargeting,
                        },
                    },
                },
            };
        }

        private static void CollectAnimationResourceKeys(
            JsonElement action,
            HashSet<string> destination)
        {
            if (action.ValueKind != JsonValueKind.Object)
                return;
            if (action.TryGetProperty("animationTrack", out JsonElement animationTrack)
                && animationTrack.ValueKind == JsonValueKind.Object
                && animationTrack.TryGetProperty("events", out JsonElement events)
                && events.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in events.EnumerateArray())
                    AddStringProperty(item, "animationClipResourceKey", destination);
            }
            if (action.TryGetProperty("motionTrack", out JsonElement motionTrack)
                && motionTrack.ValueKind == JsonValueKind.Object)
            {
                if (motionTrack.TryGetProperty("rootMotion", out JsonElement rootMotion))
                    AddStringProperty(rootMotion, "sourceAnimationClipResourceKey", destination);
                if (motionTrack.TryGetProperty("verticalRootMotion", out JsonElement verticalRootMotion))
                    AddStringProperty(verticalRootMotion, "sourceAnimationClipResourceKey", destination);
            }
        }

        private static void AddStringProperty(
            JsonElement source,
            string propertyName,
            HashSet<string> destination)
        {
            if (source.ValueKind != JsonValueKind.Object
                || !source.TryGetProperty(propertyName, out JsonElement value)
                || value.ValueKind != JsonValueKind.String)
            {
                return;
            }
            string resourceKey = value.GetString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(resourceKey))
                destination.Add(resourceKey);
        }
    }
}
