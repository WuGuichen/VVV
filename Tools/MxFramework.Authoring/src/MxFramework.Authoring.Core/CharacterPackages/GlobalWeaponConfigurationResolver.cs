using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public enum GlobalAnimationCompatibilityKind
    {
        Incompatible = 0,
        Native = 1,
        HumanoidRetargeting = 2
    }

    public readonly struct GlobalAnimationCompatibilityResult
    {
        public GlobalAnimationCompatibilityResult(
            GlobalAnimationCompatibilityKind kind,
            string message)
        {
            Kind = kind;
            Message = message ?? string.Empty;
        }

        public GlobalAnimationCompatibilityKind Kind { get; }
        public string Message { get; }
        public bool IsCompatible => Kind != GlobalAnimationCompatibilityKind.Incompatible;
        public bool RequiresRetargeting => Kind == GlobalAnimationCompatibilityKind.HumanoidRetargeting;
    }

    /// <summary>
    /// Resolves project-global weapon configuration components for one character package.
    /// AllocByDesign: used only by authoring validation and compilation.
    /// </summary>
    public static class GlobalWeaponConfigurationResolver
    {
        public static SingleWeaponConfiguration FindSingle(GlobalWeaponLibrary library, string id)
        {
            List<SingleWeaponConfiguration> items = library?.SingleWeaponConfigurations;
            for (int i = 0; items != null && i < items.Count; i++)
                if (items[i] != null && string.Equals(items[i].SingleWeaponConfigurationId, id, StringComparison.Ordinal))
                    return items[i];
            return null;
        }

        public static GlobalAnimationConfiguration FindAnimation(GlobalWeaponLibrary library, string id)
        {
            List<GlobalAnimationConfiguration> items = library?.AnimationConfigurations;
            for (int i = 0; items != null && i < items.Count; i++)
                if (items[i] != null && string.Equals(items[i].AnimationConfigurationId, id, StringComparison.Ordinal))
                    return items[i];
            return null;
        }

        public static GlobalAbilityConfiguration FindAbility(GlobalWeaponLibrary library, string id)
        {
            List<GlobalAbilityConfiguration> items = library?.AbilityConfigurations;
            for (int i = 0; items != null && i < items.Count; i++)
                if (items[i] != null && string.Equals(items[i].AbilityConfigurationId, id, StringComparison.Ordinal))
                    return items[i];
            return null;
        }

        public static GlobalReactionConfiguration FindReaction(GlobalWeaponLibrary library, string id)
        {
            List<GlobalReactionConfiguration> items = library?.ReactionConfigurations;
            for (int i = 0; items != null && i < items.Count; i++)
                if (items[i] != null && string.Equals(items[i].ReactionConfigurationId, id, StringComparison.Ordinal))
                    return items[i];
            return null;
        }

        public static CombinedWeaponConfigurationAuthoringSummary FindCombinedWeapon(
            GlobalWeaponLibrary library,
            string id)
        {
            List<CombinedWeaponConfigurationAuthoringSummary> items = library?.CombinedWeaponConfigurations;
            for (int i = 0; items != null && i < items.Count; i++)
                if (items[i] != null && string.Equals(
                    items[i].CombinedWeaponConfigurationId,
                    id,
                    StringComparison.Ordinal))
                    return items[i];
            return null;
        }

        /// <summary>
        /// Returns the character-scoped runtime identity for a public combined weapon configuration.
        /// Configurations owned by the current character keep their historical local identity, while
        /// shared configurations owned elsewhere retain their full public identity to avoid collisions.
        /// </summary>
        public static string GetCharacterScopedCombinationIdentity(string packageId, string configurationId)
        {
            string normalizedPackageId = CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId);
            string normalizedConfigurationId = CharacterPackageResourceKeyGenerator.NormalizeSegment(configurationId);
            string ownedPrefix = normalizedPackageId + ".";
            return normalizedConfigurationId.StartsWith(ownedPrefix, StringComparison.Ordinal)
                ? normalizedConfigurationId.Substring(ownedPrefix.Length)
                : normalizedConfigurationId;
        }

        /// <summary>
        /// Resolves character selections against project-global defaults. Empty override fields inherit
        /// independently; slots always come from the public configuration.
        /// AllocByDesign: authoring load/validation/compile only.
        /// </summary>
        public static List<CombinedWeaponConfigurationAuthoringSummary> ResolveCombinedWeapons(
            CharacterApplicationAuthoringSummary application,
            GlobalWeaponLibrary library)
        {
            var result = new List<CombinedWeaponConfigurationAuthoringSummary>();
            List<CharacterCombinedWeaponConfigurationBinding> bindings =
                application?.CombinedWeaponConfigurationBindings;
            if (bindings == null || bindings.Count == 0)
            {
                if (application?.CombinedWeaponConfigurations != null)
                    result.AddRange(application.CombinedWeaponConfigurations);
                return result;
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                CharacterCombinedWeaponConfigurationBinding binding = bindings[i];
                CombinedWeaponConfigurationAuthoringSummary defaults = FindCombinedWeapon(
                    library,
                    binding?.CombinedWeaponConfigurationId);
                if (defaults == null)
                    continue;
                CharacterCombinedWeaponConfigurationOverrides overrides = binding?.Overrides;
                result.Add(new CombinedWeaponConfigurationAuthoringSummary
                {
                    CombinedWeaponConfigurationId = defaults.CombinedWeaponConfigurationId,
                    DisplayName = defaults.DisplayName,
                    Slots = defaults.Slots ?? new List<CombinedWeaponConfigurationSlotAuthoringSummary>(),
                    AnimationConfigurationId = FirstOverride(
                        overrides?.AnimationConfigurationId,
                        defaults.AnimationConfigurationId),
                    AbilityConfigurationId = FirstOverride(
                        overrides?.AbilityConfigurationId,
                        defaults.AbilityConfigurationId),
                    ReactionConfigurationId = FirstOverride(
                        overrides?.ReactionConfigurationId,
                        defaults.ReactionConfigurationId),
                    ActionConfiguration = overrides?.ActionConfiguration ?? defaults.ActionConfiguration ??
                        new CombinedWeaponActionConfiguration()
                });
            }
            return result;
        }

        public static void HydrateCombinedWeapons(CharacterResourcePackage package)
        {
            if (package?.ApplicationConfig == null)
                return;
            package.ApplicationConfig.CombinedWeaponConfigurations = ResolveCombinedWeapons(
                package.ApplicationConfig,
                package.GlobalWeaponLibrary);
        }

        private static string FirstOverride(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }

        /// <summary>
        /// Resolves whether a reusable animation configuration can drive the character target rig.
        /// Different Humanoid skeleton/avatar identities are compatible only when the animation
        /// configuration explicitly opts every authored set and profile into retargeting.
        /// AllocByDesign: authoring validation only.
        /// </summary>
        public static GlobalAnimationCompatibilityResult ResolveAnimationCompatibility(
            CharacterResourcePackage characterPackage,
            GlobalAnimationConfiguration configuration)
        {
            if (configuration == null)
            {
                return new GlobalAnimationCompatibilityResult(
                    GlobalAnimationCompatibilityKind.Incompatible,
                    "The animation configuration does not exist.");
            }

            AnimationAuthoringPackage target = characterPackage?.AnimationAuthoring;
            string targetRigType = ResolveCharacterRigType(characterPackage);
            string sourceRigType = ResolveAnimationConfigurationRigType(configuration);
            if (string.IsNullOrWhiteSpace(targetRigType))
            {
                return new GlobalAnimationCompatibilityResult(
                    GlobalAnimationCompatibilityKind.Incompatible,
                    characterPackage?.Geometry?.BodyProfile?.BodyKind == CharacterAuthoringBodyKind.Skeletal
                        ? "The Skeletal target character must explicitly declare rigType; Generic is never inferred."
                        : "The target character rig type is unspecified.");
            }

            if (string.IsNullOrWhiteSpace(sourceRigType))
            {
                return new GlobalAnimationCompatibilityResult(
                    GlobalAnimationCompatibilityKind.Incompatible,
                    "The animation configuration rig type is unspecified; only legacy Humanoid identities may be inferred.");
            }

            if (!string.Equals(targetRigType, sourceRigType, StringComparison.Ordinal))
            {
                return new GlobalAnimationCompatibilityResult(
                    GlobalAnimationCompatibilityKind.Incompatible,
                    "The source and target rig types do not match.");
            }

            if (HasNativeRigIdentity(target, configuration))
            {
                return new GlobalAnimationCompatibilityResult(
                    GlobalAnimationCompatibilityKind.Native,
                    "The animation configuration uses the character's native skeleton and Avatar identities.");
            }

            if (string.Equals(targetRigType, GlobalAnimationRigTypes.Generic, StringComparison.Ordinal))
            {
                return new GlobalAnimationCompatibilityResult(
                    GlobalAnimationCompatibilityKind.Incompatible,
                    "Generic animation reuse requires the exact same skeleton and Avatar identities; cross-skeleton retargeting is not supported.");
            }

            if (!IsHumanoidCharacterTarget(characterPackage))
            {
                return new GlobalAnimationCompatibilityResult(
                    GlobalAnimationCompatibilityKind.Incompatible,
                    "The target character is not declared as a Humanoid rig.");
            }

            if (!IsHumanoidAnimationConfiguration(configuration))
            {
                return new GlobalAnimationCompatibilityResult(
                    GlobalAnimationCompatibilityKind.Incompatible,
                    "The animation configuration is not declared as a Humanoid animation source.");
            }

            if (!AllowsRetargeting(configuration))
            {
                return new GlobalAnimationCompatibilityResult(
                    GlobalAnimationCompatibilityKind.Incompatible,
                    "Cross-skeleton Humanoid reuse requires allowRetargeting=true on every authored animation set and profile.");
            }

            return new GlobalAnimationCompatibilityResult(
                GlobalAnimationCompatibilityKind.HumanoidRetargeting,
                "The source and target are Humanoid rigs; Unity will retarget the clips through the target character Avatar.");
        }

        /// <summary>
        /// Returns the selected global animation configurations when the character references them;
        /// package-local animation authoring remains only as the target-rig declaration and legacy fallback.
        /// AllocByDesign: authoring compile only.
        /// </summary>
        public static AnimationAuthoringPackage BuildEffectiveAnimationPackage(
            CharacterResourcePackage characterPackage)
        {
            if (!HasAnimationConfigurationReferences(characterPackage))
                return characterPackage?.AnimationAuthoring ?? new AnimationAuthoringPackage();
            return BuildAnimationPackage(characterPackage);
        }

        /// <summary>AllocByDesign: merges the exact animation configurations referenced by a character.</summary>
        public static AnimationAuthoringPackage BuildAnimationPackage(CharacterResourcePackage characterPackage)
        {
            var result = new AnimationAuthoringPackage
            {
                SchemaVersion = "1.0",
                PackageId = "animation." + (characterPackage?.Manifest?.PackageId ?? "character"),
                StableId = "charpkg." + (characterPackage?.Manifest?.PackageId ?? "character") + ".animation",
                DisplayName = (characterPackage?.Manifest?.DisplayName ?? "Character") + " Animation",
                RigType = ResolveCharacterRigType(characterPackage),
                SkeletonProfileId = characterPackage?.AnimationAuthoring?.SkeletonProfileId ?? string.Empty,
                AvatarProfileId = characterPackage?.AnimationAuthoring?.AvatarProfileId ?? string.Empty,
                Metadata = new Dictionary<string, string>
                {
                    ["authority"] = "global-animation-configuration"
                }
            };
            var configurationIds = new HashSet<string>(StringComparer.Ordinal);
            var setIds = new HashSet<string>(StringComparer.Ordinal);
            var profileIds = new HashSet<string>(StringComparer.Ordinal);
            List<CombinedWeaponConfigurationAuthoringSummary> combinations =
                characterPackage?.ApplicationConfig?.CombinedWeaponConfigurations;
            for (int i = 0; combinations != null && i < combinations.Count; i++)
            {
                string id = combinations[i]?.AnimationConfigurationId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id) || !configurationIds.Add(id))
                    continue;
                GlobalAnimationConfiguration configuration = FindAnimation(characterPackage.GlobalWeaponLibrary, id);
                if (configuration == null)
                    continue;
                if (string.IsNullOrWhiteSpace(result.SkeletonProfileId))
                    result.SkeletonProfileId = configuration.SkeletonProfileId;
                if (string.IsNullOrWhiteSpace(result.AvatarProfileId))
                    result.AvatarProfileId = configuration.AvatarProfileId;
                if (string.IsNullOrWhiteSpace(result.RigType))
                    result.RigType = ResolveAnimationConfigurationRigType(configuration);
                for (int setIndex = 0; configuration.Sets != null && setIndex < configuration.Sets.Count; setIndex++)
                {
                    AnimationAuthoringSet set = configuration.Sets[setIndex];
                    if (set != null && setIds.Add(set.SetId ?? string.Empty))
                        result.Sets.Add(set);
                }
                for (int profileIndex = 0; configuration.Profiles != null && profileIndex < configuration.Profiles.Count; profileIndex++)
                {
                    AnimationAuthoringProfile profile = configuration.Profiles[profileIndex];
                    if (profile != null && profileIds.Add(profile.ProfileId ?? string.Empty))
                        result.Profiles.Add(profile);
                }
            }
            result.Sets.Sort((left, right) => string.CompareOrdinal(left?.SetId, right?.SetId));
            result.Profiles.Sort((left, right) => string.CompareOrdinal(left?.ProfileId, right?.ProfileId));
            return result;
        }

        private static bool HasAnimationConfigurationReferences(CharacterResourcePackage characterPackage)
        {
            List<CombinedWeaponConfigurationAuthoringSummary> combinations =
                characterPackage?.ApplicationConfig?.CombinedWeaponConfigurations;
            for (int i = 0; combinations != null && i < combinations.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(combinations[i]?.AnimationConfigurationId))
                    return true;
            }
            return false;
        }

        private static bool HasNativeRigIdentity(
            AnimationAuthoringPackage target,
            GlobalAnimationConfiguration source)
        {
            if (target == null || source == null || string.IsNullOrWhiteSpace(source.SkeletonProfileId))
                return false;
            if (!string.Equals(target.SkeletonProfileId, source.SkeletonProfileId, StringComparison.Ordinal))
                return false;
            return string.IsNullOrWhiteSpace(source.AvatarProfileId) ||
                string.Equals(target.AvatarProfileId, source.AvatarProfileId, StringComparison.Ordinal);
        }

        public static string ResolveCharacterRigType(CharacterResourcePackage characterPackage)
        {
            string declaredRigType = characterPackage?.AnimationAuthoring?.RigType;
            string explicitRigType = GlobalAnimationRigTypes.Normalize(declaredRigType);
            if (!string.IsNullOrWhiteSpace(explicitRigType))
                return explicitRigType;
            if (!string.IsNullOrWhiteSpace(declaredRigType))
                return GlobalAnimationRigTypes.Unspecified;
            return HasLegacyHumanoidCharacterIdentity(characterPackage)
                ? GlobalAnimationRigTypes.Humanoid
                : GlobalAnimationRigTypes.Unspecified;
        }

        public static string ResolveAnimationConfigurationRigType(GlobalAnimationConfiguration configuration)
        {
            string declaredRigType = configuration?.RigType;
            string explicitRigType = GlobalAnimationRigTypes.Normalize(declaredRigType);
            if (!string.IsNullOrWhiteSpace(explicitRigType))
                return explicitRigType;
            if (!string.IsNullOrWhiteSpace(declaredRigType))
                return GlobalAnimationRigTypes.Unspecified;
            return HasLegacyHumanoidAnimationIdentity(configuration)
                ? GlobalAnimationRigTypes.Humanoid
                : GlobalAnimationRigTypes.Unspecified;
        }

        private static bool IsHumanoidCharacterTarget(CharacterResourcePackage characterPackage)
        {
            return string.Equals(
                ResolveCharacterRigType(characterPackage),
                GlobalAnimationRigTypes.Humanoid,
                StringComparison.Ordinal);
        }

        private static bool HasLegacyHumanoidCharacterIdentity(CharacterResourcePackage characterPackage)
        {
            CharacterBodyGeometryProfile body = characterPackage?.Geometry?.BodyProfile;
            if (body != null && body.BodyKind == CharacterAuthoringBodyKind.Primitive)
                return false;

            List<CharacterPackageResourceEntry> entries = characterPackage?.ResourceCatalog?.Entries;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = entries[i];
                if (!string.Equals(entry?.Usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.Ordinal))
                    continue;
                for (int tagIndex = 0; entry.Tags != null && tagIndex < entry.Tags.Count; tagIndex++)
                {
                    if (string.Equals(entry.Tags[tagIndex], "humanoid", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            AnimationAuthoringPackage target = characterPackage?.AnimationAuthoring;
            if (ContainsHumanoid(target?.SkeletonProfileId) || ContainsHumanoid(target?.AvatarProfileId))
                return true;
            for (int i = 0; target?.Sets != null && i < target.Sets.Count; i++)
            {
                if (HasHumanoidMetadata(target.Sets[i]?.Compatibility?.Metadata))
                    return true;
            }
            for (int i = 0; target?.Profiles != null && i < target.Profiles.Count; i++)
            {
                if (HasHumanoidMetadata(target.Profiles[i]?.Compatibility?.Metadata))
                    return true;
            }
            return false;
        }

        private static bool IsHumanoidAnimationConfiguration(GlobalAnimationConfiguration configuration)
        {
            return string.Equals(
                ResolveAnimationConfigurationRigType(configuration),
                GlobalAnimationRigTypes.Humanoid,
                StringComparison.Ordinal);
        }

        private static bool HasLegacyHumanoidAnimationIdentity(GlobalAnimationConfiguration configuration)
        {
            if (ContainsHumanoid(configuration?.SkeletonProfileId) || ContainsHumanoid(configuration?.AvatarProfileId))
                return true;

            for (int i = 0; configuration?.Sets != null && i < configuration.Sets.Count; i++)
            {
                if (HasHumanoidMetadata(configuration.Sets[i]?.Compatibility?.Metadata))
                    return true;
            }
            for (int i = 0; configuration?.Profiles != null && i < configuration.Profiles.Count; i++)
            {
                if (HasHumanoidMetadata(configuration.Profiles[i]?.Compatibility?.Metadata))
                    return true;
            }
            return false;
        }

        private static bool AllowsRetargeting(GlobalAnimationConfiguration configuration)
        {
            bool declared = false;
            for (int i = 0; configuration?.Sets != null && i < configuration.Sets.Count; i++)
            {
                AnimationCompatibilityExpectationAuthoring compatibility = configuration.Sets[i]?.Compatibility;
                if (compatibility == null || !compatibility.AllowRetargeting)
                    return false;
                declared = true;
            }
            for (int i = 0; configuration?.Profiles != null && i < configuration.Profiles.Count; i++)
            {
                AnimationCompatibilityExpectationAuthoring compatibility = configuration.Profiles[i]?.Compatibility;
                if (compatibility == null || !compatibility.AllowRetargeting)
                    return false;
                declared = true;
            }
            return declared;
        }

        private static bool HasHumanoidMetadata(Dictionary<string, string> metadata)
        {
            return metadata != null && metadata.TryGetValue("rigType", out string rigType) &&
                string.Equals(rigType, GlobalAnimationRigTypes.Humanoid, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsHumanoid(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.IndexOf("humanoid", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
