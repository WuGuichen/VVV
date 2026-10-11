using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace MxFramework.Authoring
{
    /// <summary>
    /// Describes a low-frequency, explicit schema upgrade for a character resource package and the
    /// global single-weapon library.
    /// AllocByDesign: migration plans allocate diagnostic and step collections.
    /// </summary>
    public sealed class CharacterResourcePackageMigrationPlan
    {
        public string TargetSchemaVersion { get; set; } = CharacterResourcePackageMigrator.CurrentSchemaVersion;
        public bool RequiresMigration { get; set; }
        public bool GlobalWeaponLibraryWriteRequired { get; set; }
        public bool CanMigrate { get; set; } = true;
        public List<CharacterResourcePackageMigrationStep> Steps { get; set; } =
            new List<CharacterResourcePackageMigrationStep>();
        public List<string> BlockingReasons { get; set; } = new List<string>();
    }

    public sealed class CharacterResourcePackageMigrationStep
    {
        public string Document { get; set; } = string.Empty;
        public string FromVersion { get; set; } = string.Empty;
        public string ToVersion { get; set; } = CharacterResourcePackageMigrator.CurrentSchemaVersion;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Upgrades character authoring to the global combined-weapon defaults / character override contract.
    /// AllocByDesign: intended for authoring load/save operations, never runtime ticks.
    /// </summary>
    public static class CharacterResourcePackageMigrator
    {
        public const string CurrentSchemaVersion = "8.0";
        public const string CurrentPackageSchemaVersion = "8.0";
        public const string CurrentCharacterApplicationSchemaVersion = "8.0";
        public const string CurrentWeaponSlotsSchemaVersion = "1.0";
        public const string CurrentUnchangedDocumentSchemaVersion = "1.0";

        /// <summary>AllocByDesign: builds a low-frequency cross-file authoring migration plan.</summary>
        public static CharacterResourcePackageMigrationPlan CreatePlan(CharacterResourcePackage package)
        {
            var plan = new CharacterResourcePackageMigrationPlan();
            if (package == null)
            {
                plan.CanMigrate = false;
                plan.BlockingReasons.Add("Character package is required.");
                return plan;
            }

            AddDocument(plan, "manifest.packageSchemaVersion", package.Manifest?.PackageSchemaVersion, CurrentPackageSchemaVersion);
            AddDocument(plan, "manifest.sourceSchemaVersion", package.Manifest?.SourceSchemaVersion, CurrentPackageSchemaVersion);
            AddDocument(plan, "manifest.authoringSchemaVersion", package.Manifest?.AuthoringSchemaVersion, CurrentPackageSchemaVersion);
            AddDocument(plan, "resource_catalog.schemaVersion", package.ResourceCatalog?.SchemaVersion, CurrentUnchangedDocumentSchemaVersion);
            AddDocument(plan, "geometry.schemaVersion", package.Geometry?.SchemaVersion, CurrentUnchangedDocumentSchemaVersion);
            AddDocument(plan, "geometry/weapon_slots.schemaVersion", package.Geometry?.WeaponSlotsSchemaVersion, CurrentWeaponSlotsSchemaVersion);
            AddDocument(plan, "config/character_application.schemaVersion", package.ApplicationConfig?.SchemaVersion, CurrentCharacterApplicationSchemaVersion);
            AddDocument(plan, "validation/last_report.schemaVersion", package.ValidationReport?.SchemaVersion, CurrentUnchangedDocumentSchemaVersion);

            GlobalWeaponLibrary library = package.GlobalWeaponLibrary;
            if (library == null)
            {
                plan.Steps.Add(new CharacterResourcePackageMigrationStep
                {
                    Document = "WeaponSource/global_weapon_library.json",
                    FromVersion = "missing",
                    ToVersion = GlobalWeaponLibrarySchema.CurrentSchemaVersion,
                    Description = "Create the global library with the immutable weapon.unarmed entry."
                });
                plan.GlobalWeaponLibraryWriteRequired = true;
            }
            else if (string.Equals(library.SchemaVersion, "1.0", StringComparison.Ordinal) ||
                string.Equals(library.SchemaVersion, "2.0", StringComparison.Ordinal) ||
                string.Equals(library.SchemaVersion, "3.0", StringComparison.Ordinal) ||
                string.Equals(library.SchemaVersion, "4.0", StringComparison.Ordinal) ||
                string.Equals(library.SchemaVersion, "5.0", StringComparison.Ordinal))
            {
                plan.Steps.Add(new CharacterResourcePackageMigrationStep
                {
                    Document = "WeaponSource/global_weapon_library.json",
                    FromVersion = library.SchemaVersion,
                    ToVersion = GlobalWeaponLibrarySchema.CurrentSchemaVersion,
                    Description = "Upgrade the global weapon library and promote character-local combinations into public defaults."
                });
                plan.GlobalWeaponLibraryWriteRequired = true;
            }
            else if (!string.Equals(library.SchemaVersion, GlobalWeaponLibrarySchema.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                plan.CanMigrate = false;
                plan.BlockingReasons.Add("GLOBAL_WEAPON_LIBRARY_SCHEMA_UNSUPPORTED: " + library.SchemaVersion);
            }

            CheckPromotionConflicts(plan, package, library);
            if (!string.Equals(package.ApplicationConfig?.SchemaVersion, "6.0", StringComparison.Ordinal) &&
                !string.Equals(package.ApplicationConfig?.SchemaVersion, "7.0", StringComparison.Ordinal) &&
                !string.Equals(package.ApplicationConfig?.SchemaVersion, CurrentCharacterApplicationSchemaVersion, StringComparison.Ordinal))
                CheckWeaponOwnershipPromotionConflicts(plan, package);
            CheckLegacyAbilityRemovalConflicts(plan, library);

            CharacterPackageResourceCatalog catalog = package.ResourceCatalog;
            for (int i = 0; catalog != null && i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry?.Provenance != null)
                    AddDocument(plan,
                        "resource_catalog.entries[" + i + "].provenance.authoringSchemaVersion",
                        entry.Provenance.AuthoringSchemaVersion,
                        CurrentUnchangedDocumentSchemaVersion,
                        treatEmptyAsLegacy: true);
            }

            plan.RequiresMigration = plan.Steps.Count > 0;
            return plan;
        }

        /// <summary>AllocByDesign: applies a previously validated low-frequency cross-file migration.</summary>
        public static CharacterResourcePackageMigrationPlan Apply(CharacterResourcePackage package)
        {
            CharacterResourcePackageMigrationPlan plan = CreatePlan(package);
            if (!plan.CanMigrate || !plan.RequiresMigration)
                return plan;

            GlobalWeaponLibrary library = package.GlobalWeaponLibrary ?? GlobalWeaponLibrarySchema.CreateDefault();
            string applicationVersion = package.ApplicationConfig?.SchemaVersion ?? string.Empty;
            bool requiresLegacyPromotion = !string.Equals(applicationVersion, "6.0", StringComparison.Ordinal) &&
                !string.Equals(applicationVersion, "7.0", StringComparison.Ordinal) &&
                !string.Equals(applicationVersion, CurrentCharacterApplicationSchemaVersion, StringComparison.Ordinal);
            EnsureUnarmed(library);
            if (requiresLegacyPromotion)
            {
                PromoteLegacyDefinitions(package.ApplicationConfig, library);
                MigrateLegacyConfigurations(package.ApplicationConfig);
                PromoteBehaviorConfigurations(package, library);
                PromoteWeaponOwnership(package, library);
                MigrateAbilityConfigurations(library);
            }
            if (!string.Equals(applicationVersion, "7.0", StringComparison.Ordinal) &&
                !string.Equals(applicationVersion, CurrentCharacterApplicationSchemaVersion, StringComparison.Ordinal))
                PromoteCombinedWeaponConfigurations(package, library);

            package.GlobalWeaponLibrary = library;
            library.SchemaVersion = GlobalWeaponLibrarySchema.CurrentSchemaVersion;
            package.Manifest.PackageSchemaVersion = CurrentPackageSchemaVersion;
            package.Manifest.SourceSchemaVersion = CurrentPackageSchemaVersion;
            package.Manifest.AuthoringSchemaVersion = CurrentPackageSchemaVersion;
            package.ResourceCatalog.SchemaVersion = CurrentUnchangedDocumentSchemaVersion;
            package.Geometry.SchemaVersion = CurrentUnchangedDocumentSchemaVersion;
            package.Geometry.WeaponSlotsSchemaVersion = CurrentWeaponSlotsSchemaVersion;
            package.ApplicationConfig.SchemaVersion = CurrentCharacterApplicationSchemaVersion;
            package.ValidationReport.SchemaVersion = CurrentUnchangedDocumentSchemaVersion;

            for (int i = 0; i < package.ResourceCatalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = package.ResourceCatalog.Entries[i];
                if (entry?.Provenance != null)
                    entry.Provenance.AuthoringSchemaVersion = CurrentUnchangedDocumentSchemaVersion;
            }

            return plan;
        }

        private static void PromoteCombinedWeaponConfigurations(
            CharacterResourcePackage package,
            GlobalWeaponLibrary library)
        {
            CharacterApplicationAuthoringSummary application = package?.ApplicationConfig;
            List<CombinedWeaponConfigurationAuthoringSummary> local =
                application?.CombinedWeaponConfigurations;
            if (application == null || local == null || local.Count == 0)
                return;

            library.CombinedWeaponConfigurations ??=
                new List<CombinedWeaponConfigurationAuthoringSummary>();
            application.CombinedWeaponConfigurationBindings ??=
                new List<CharacterCombinedWeaponConfigurationBinding>();
            application.CombinedWeaponConfigurationBindings.Clear();
            string packageId = NormalizeId(package?.Manifest?.PackageId, "character");
            string localDefault = application.DefaultCombinedWeaponConfigurationId;
            string promotedDefault = string.Empty;

            for (int i = 0; i < local.Count; i++)
            {
                CombinedWeaponConfigurationAuthoringSummary source = local[i];
                if (source == null || string.IsNullOrWhiteSpace(source.CombinedWeaponConfigurationId))
                    continue;
                string globalId = packageId + "." +
                    NormalizeId(source.CombinedWeaponConfigurationId, "configuration");
                CombinedWeaponConfigurationAuthoringSummary candidate = DeepClone(source);
                candidate.CombinedWeaponConfigurationId = globalId;
                AddOrReuse(
                    library.CombinedWeaponConfigurations,
                    candidate,
                    item => item.CombinedWeaponConfigurationId);
                application.CombinedWeaponConfigurationBindings.Add(
                    new CharacterCombinedWeaponConfigurationBinding
                    {
                        CombinedWeaponConfigurationId = globalId
                    });
                if (string.Equals(
                    source.CombinedWeaponConfigurationId,
                    localDefault,
                    StringComparison.Ordinal))
                    promotedDefault = globalId;
            }

            application.DefaultCombinedWeaponConfigurationId = !string.IsNullOrWhiteSpace(promotedDefault)
                ? promotedDefault
                : application.CombinedWeaponConfigurationBindings.Count > 0
                    ? application.CombinedWeaponConfigurationBindings[0].CombinedWeaponConfigurationId
                    : string.Empty;
            application.CombinedWeaponConfigurations.Clear();
            application.CombinedWeaponConfigurations = GlobalWeaponConfigurationResolver.ResolveCombinedWeapons(
                application,
                library);
        }

        private static void CheckLegacyAbilityRemovalConflicts(
            CharacterResourcePackageMigrationPlan plan,
            GlobalWeaponLibrary library)
        {
            List<GlobalAbilityConfiguration> configurations = library?.AbilityConfigurations;
            for (int i = 0; configurations != null && i < configurations.Count; i++)
            {
                GlobalAbilityConfiguration configuration = configurations[i];
                if (configuration?.RemoveAbilityIds == null || configuration.RemoveAbilityIds.Count == 0)
                    continue;
                plan.CanMigrate = false;
                plan.BlockingReasons.Add(
                    "GLOBAL_ABILITY_REMOVE_MIGRATION_CONFLICT: " + configuration.AbilityConfigurationId +
                    " contains removeAbilityIds; rebuild the complete semantic ability set explicitly.");
            }
        }

        private static void MigrateAbilityConfigurations(GlobalWeaponLibrary library)
        {
            List<GlobalAbilityConfiguration> configurations = library?.AbilityConfigurations;
            for (int i = 0; configurations != null && i < configurations.Count; i++)
            {
                GlobalAbilityConfiguration configuration = configurations[i];
                if (configuration == null)
                    continue;
                configuration.Abilities ??= new List<GlobalAbilityEntry>();
                List<int> legacyIds = configuration.AbilityIds;
                for (int abilityIndex = 0; legacyIds != null && abilityIndex < legacyIds.Count; abilityIndex++)
                {
                    int abilityId = legacyIds[abilityIndex];
                    if (abilityId <= 0 || configuration.Abilities.Exists(entry => entry != null && entry.AbilityId == abilityId))
                        continue;
                    GlobalAbilitySlotBinding binding = FindLegacyAbilityBinding(configuration.SlotBindings, abilityId);
                    bool hasAction = binding != null && !string.IsNullOrWhiteSpace(binding.ActionRole);
                    bool hasInput = binding != null && (!string.IsNullOrWhiteSpace(binding.SlotId) ||
                        !string.IsNullOrWhiteSpace(binding.InputIntentId));
                    configuration.Abilities.Add(new GlobalAbilityEntry
                    {
                        AbilityId = abilityId,
                        DisplayName = "Ability " + abilityId,
                        Tier = GlobalAbilityTiers.Unspecified,
                        ExecutionMode = hasAction ? GlobalAbilityExecutionModes.ActionOnly : GlobalAbilityExecutionModes.External,
                        TriggerMode = hasInput ? GlobalAbilityTriggerModes.Input : GlobalAbilityTriggerModes.External,
                        SlotId = binding?.SlotId ?? string.Empty,
                        InputIntentId = binding?.InputIntentId ?? string.Empty,
                        ActionBindingMode = hasAction ? GlobalAbilityActionBindingModes.Required : GlobalAbilityActionBindingModes.None,
                        ActionRole = binding?.ActionRole ?? string.Empty
                    });
                }
                configuration.Abilities.Sort((left, right) => (left?.AbilityId ?? 0).CompareTo(right?.AbilityId ?? 0));
                configuration.AbilityIds = null;
                configuration.RemoveAbilityIds = null;
                configuration.SlotBindings = null;
            }
        }

        private static GlobalAbilitySlotBinding FindLegacyAbilityBinding(
            List<GlobalAbilitySlotBinding> bindings,
            int abilityId)
        {
            GlobalAbilitySlotBinding result = null;
            for (int i = 0; bindings != null && i < bindings.Count; i++)
            {
                GlobalAbilitySlotBinding candidate = bindings[i];
                if (candidate == null || candidate.AbilityId != abilityId)
                    continue;
                if (result != null && (!string.Equals(result.SlotId, candidate.SlotId, StringComparison.Ordinal) ||
                    !string.Equals(result.InputIntentId, candidate.InputIntentId, StringComparison.Ordinal) ||
                    !string.Equals(result.ActionRole, candidate.ActionRole, StringComparison.Ordinal)))
                    return null;
                result = candidate;
            }
            return result;
        }

        private static void PromoteWeaponOwnership(CharacterResourcePackage package, GlobalWeaponLibrary library)
        {
            CharacterAuthoringGeometry geometry = package.Geometry ?? new CharacterAuthoringGeometry();
            package.Geometry = geometry;
            var slots = new Dictionary<string, CharacterWeaponSlotProfile>(StringComparer.Ordinal);
            for (int i = 0; i < geometry.WeaponSlots.Count; i++)
            {
                CharacterWeaponSlotProfile slot = geometry.WeaponSlots[i];
                if (slot != null && !string.IsNullOrWhiteSpace(slot.SlotId)) slots[slot.SlotId] = slot;
            }
            var definitions = new Dictionary<string, SingleWeaponConfiguration>(StringComparer.Ordinal);
            for (int i = 0; i < library.SingleWeaponConfigurations.Count; i++)
            {
                SingleWeaponConfiguration definition = library.SingleWeaponConfigurations[i];
                if (definition != null && !string.IsNullOrWhiteSpace(definition.SingleWeaponConfigurationId))
                {
                    definitions[definition.SingleWeaponConfigurationId] = definition;
                    definition.ModelPose ??= CreateModelRootPose();
                    definition.TraceProfiles ??= new List<SingleWeaponTraceProfile>();
                }
            }
            for (int i = 0; i < geometry.WeaponAttachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = geometry.WeaponAttachments[i];
                if (attachment == null || string.IsNullOrWhiteSpace(attachment.SlotId)) continue;
                if (!slots.ContainsKey(attachment.SlotId))
                {
                    var slot = new CharacterWeaponSlotProfile
                    {
                        SlotId = attachment.SlotId,
                        AttachSocketId = attachment.AttachSocketId,
                        SlotPose = ClonePoseForMigration(attachment.LocalGripPose)
                    };
                    geometry.WeaponSlots.Add(slot);
                    slots.Add(slot.SlotId, slot);
                }
                if (!definitions.TryGetValue(attachment.SingleWeaponConfigurationId ?? string.Empty, out SingleWeaponConfiguration definition)) continue;
                for (int traceIndex = 0; traceIndex < geometry.Traces.Count; traceIndex++)
                {
                    WeaponTraceProfile source = geometry.Traces[traceIndex];
                    if (source == null || (!string.IsNullOrWhiteSpace(attachment.TraceId) && !string.Equals(source.TraceId, attachment.TraceId, StringComparison.Ordinal))) continue;
                    if (definition.TraceProfiles.Exists(item => item != null && string.Equals(item.TraceId, source.TraceId, StringComparison.Ordinal))) continue;
                    definition.TraceProfiles.Add(new SingleWeaponTraceProfile
                    {
                        TraceId = source.TraceId,
                        StartLocatorPath = source.StartLocatorPath,
                        EndLocatorPath = source.EndLocatorPath,
                        StartPose = ToWeaponLocalPose(source.StartPose),
                        EndPose = ToWeaponLocalPose(source.EndPose),
                        Radius = source.Radius,
                        ProximalExtension = source.ProximalExtension,
                        ProximalRadius = source.ProximalRadius,
                        SampleRule = source.SampleRule,
                        FixedSampleCount = source.FixedSampleCount,
                        ActionKeys = source.ActionKeys != null ? new List<string>(source.ActionKeys) : new List<string>()
                    });
                }
            }
        }

        private static CharacterAuthoringLocalPose CreateModelRootPose()
        {
            return new CharacterAuthoringLocalPose { ParentKind = CharacterPoseParentKind.ModelRoot };
        }

        private static CharacterAuthoringLocalPose ToWeaponLocalPose(CharacterAuthoringLocalPose source)
        {
            CharacterAuthoringLocalPose pose = ClonePoseForMigration(source);
            pose.ParentKind = CharacterPoseParentKind.ModelRoot;
            pose.ParentPath = string.Empty;
            return pose;
        }

        private static CharacterAuthoringLocalPose ClonePoseForMigration(CharacterAuthoringLocalPose source)
        {
            if (source == null) return new CharacterAuthoringLocalPose();
            return new CharacterAuthoringLocalPose
            {
                ParentKind = source.ParentKind,
                ParentPath = source.ParentPath,
                Position = new CharacterAuthoringVector3 { X = source.Position?.X ?? 0f, Y = source.Position?.Y ?? 0f, Z = source.Position?.Z ?? 0f },
                Rotation = new CharacterAuthoringQuaternion { X = source.Rotation?.X ?? 0f, Y = source.Rotation?.Y ?? 0f, Z = source.Rotation?.Z ?? 0f, W = source.Rotation?.W ?? 1f },
                Scale = new CharacterAuthoringVector3 { X = source.Scale?.X ?? 1f, Y = source.Scale?.Y ?? 1f, Z = source.Scale?.Z ?? 1f },
                EulerHint = new CharacterAuthoringVector3 { X = source.EulerHint?.X ?? 0f, Y = source.EulerHint?.Y ?? 0f, Z = source.EulerHint?.Z ?? 0f }
            };
        }

        private static void PromoteLegacyDefinitions(
            CharacterApplicationAuthoringSummary application,
            GlobalWeaponLibrary library)
        {
            if (application == null || library == null)
                return;
            List<CharacterWeaponDefinitionAuthoringSummary> legacy = application.LegacyWeaponDefinitions ??
                new List<CharacterWeaponDefinitionAuthoringSummary>();
            for (int i = 0; i < legacy.Count; i++)
            {
                CharacterWeaponDefinitionAuthoringSummary source = legacy[i];
                if (source == null || string.IsNullOrWhiteSpace(source.WeaponId) ||
                    FindSingle(library, source.WeaponId) != null)
                    continue;
                library.SingleWeaponConfigurations.Add(new SingleWeaponConfiguration
                {
                    SingleWeaponConfigurationId = source.WeaponId,
                    DisplayName = source.DisplayName,
                    Category = source.Category,
                    Tags = Clone(source.Tags),
                    AllowedSlots = Clone(source.AllowedSlots),
                    OccupiesSlots = Clone(source.OccupiesSlots),
                    WeaponRef = Clone(source.WeaponRef)
                });
            }
            library.SingleWeaponConfigurations.Sort((left, right) => string.CompareOrdinal(
                left?.SingleWeaponConfigurationId,
                right?.SingleWeaponConfigurationId));
        }

        private static void MigrateLegacyConfigurations(CharacterApplicationAuthoringSummary application)
        {
            if (application == null || application.LegacyWeaponConfigurations == null ||
                application.LegacyWeaponConfigurations.Count == 0)
                return;

            application.DefaultCombinedWeaponConfigurationId = application.LegacyDefaultWeaponConfigurationId;
            application.CombinedWeaponConfigurations.Clear();
            List<CharacterWeaponConfigurationAuthoringSummary> legacy = application.LegacyWeaponConfigurations ??
                new List<CharacterWeaponConfigurationAuthoringSummary>();
            for (int i = 0; i < legacy.Count; i++)
            {
                CharacterWeaponConfigurationAuthoringSummary source = legacy[i];
                if (source == null)
                    continue;
                var combined = new CombinedWeaponConfigurationAuthoringSummary
                {
                    CombinedWeaponConfigurationId = source.ConfigurationId,
                    DisplayName = source.DisplayName,
                    AnimationProfileId = source.AnimationProfileId,
                    ActionSetId = source.ActionSetId,
                    AbilityLoadoutId = string.Empty
                };
                combined.Slots.Add(MigrateSlot(source, CharacterWeaponConfigurationValidator.MainHandSlotId));
                combined.Slots.Add(MigrateSlot(source, CharacterWeaponConfigurationValidator.OffHandSlotId));
                application.CombinedWeaponConfigurations.Add(combined);
            }
            application.LegacyDefaultWeaponConfigurationId = string.Empty;
            application.LegacyWeaponDefinitions.Clear();
            application.LegacyWeaponConfigurations.Clear();
        }

        private static void PromoteBehaviorConfigurations(
            CharacterResourcePackage package,
            GlobalWeaponLibrary library)
        {
            CharacterApplicationAuthoringSummary application = package?.ApplicationConfig;
            if (application == null || library == null)
                return;

            EnsureGlobalCollections(library);
            string packageId = NormalizeId(package.Manifest?.PackageId, "character");
            CharacterAbilityLoadoutAuthoringSummary baseAbility = FindAbilityLoadout(application, "base");
            CharacterReactionProfileAuthoringSummary legacyReaction = application.ReactionProfiles != null &&
                application.ReactionProfiles.Count > 0 ? application.ReactionProfiles[0] : null;

            for (int i = 0; i < application.CombinedWeaponConfigurations.Count; i++)
            {
                CombinedWeaponConfigurationAuthoringSummary combined = application.CombinedWeaponConfigurations[i];
                if (combined == null)
                    continue;

                string combinedId = NormalizeId(combined.CombinedWeaponConfigurationId, "configuration");
                if (string.IsNullOrWhiteSpace(combined.AnimationConfigurationId))
                {
                    string sourceProfileId = string.IsNullOrWhiteSpace(combined.AnimationProfileId)
                        ? "default"
                        : combined.AnimationProfileId;
                    string globalId = "animation." + packageId + "." + NormalizeId(sourceProfileId, "default");
                    AddOrReuse(library.AnimationConfigurations, BuildAnimationConfiguration(
                        package.AnimationAuthoring,
                        sourceProfileId,
                        globalId),
                        item => item.AnimationConfigurationId);
                    combined.AnimationConfigurationId = globalId;
                }

                CharacterAbilityLoadoutAuthoringSummary combinedAbility = FindAbilityLoadout(
                    application,
                    combined.AbilityLoadoutId);
                if (string.IsNullOrWhiteSpace(combined.AbilityConfigurationId))
                {
                    string globalId = "ability." + packageId + "." + combinedId;
                    GlobalAbilityConfiguration ability = BuildAbilityConfiguration(
                        globalId,
                        combined.DisplayName,
                        baseAbility,
                        combinedAbility);
                    AddOrReuse(library.AbilityConfigurations, ability, item => item.AbilityConfigurationId);
                    combined.AbilityConfigurationId = globalId;
                }

                if (string.IsNullOrWhiteSpace(combined.ReactionConfigurationId))
                {
                    string sourceReactionId = legacyReaction?.ProfileId;
                    if (string.IsNullOrWhiteSpace(sourceReactionId))
                        sourceReactionId = "default";
                    string globalId = "reaction." + packageId + "." + NormalizeId(sourceReactionId, "default");
                    AddOrReuse(library.ReactionConfigurations, BuildReactionConfiguration(
                        legacyReaction,
                        globalId),
                        item => item.ReactionConfigurationId);
                    combined.ReactionConfigurationId = globalId;
                }

                PromotePrivateActions(combined, baseAbility, combinedAbility, legacyReaction, packageId, combinedId);
                combined.AnimationProfileId = string.Empty;
                combined.AbilityLoadoutId = string.Empty;
                combined.ActionSetId = string.Empty;
            }

            application.AbilityLoadouts.Clear();
            application.AnimationGroups.Clear();
            application.AnimationProfiles.Clear();
            application.ReactionProfiles.Clear();
            library.AnimationConfigurations.Sort((left, right) => string.CompareOrdinal(
                left?.AnimationConfigurationId,
                right?.AnimationConfigurationId));
            library.AbilityConfigurations.Sort((left, right) => string.CompareOrdinal(
                left?.AbilityConfigurationId,
                right?.AbilityConfigurationId));
            library.ReactionConfigurations.Sort((left, right) => string.CompareOrdinal(
                left?.ReactionConfigurationId,
                right?.ReactionConfigurationId));
        }

        private static GlobalAnimationConfiguration BuildAnimationConfiguration(
            AnimationAuthoringPackage source,
            string sourceProfileId,
            string globalId)
        {
            var result = new GlobalAnimationConfiguration
            {
                AnimationConfigurationId = globalId,
                DisplayName = source?.DisplayName ?? sourceProfileId,
                Description = "Migrated from character-local animation authoring.",
                RigType = !string.IsNullOrWhiteSpace(GlobalAnimationRigTypes.Normalize(source?.RigType))
                    ? GlobalAnimationRigTypes.Normalize(source.RigType)
                    : IsHumanoidRigIdentity(source?.SkeletonProfileId, source?.AvatarProfileId)
                        ? GlobalAnimationRigTypes.Humanoid
                        : GlobalAnimationRigTypes.Unspecified,
                SkeletonProfileId = source?.SkeletonProfileId ?? string.Empty,
                AvatarProfileId = source?.AvatarProfileId ?? string.Empty,
                Sets = DeepClone(source?.Sets) ?? new List<AnimationAuthoringSet>(),
                Profiles = DeepClone(source?.Profiles) ?? new List<AnimationAuthoringProfile>()
            };
            if (result.Profiles.Count == 0 && !string.IsNullOrWhiteSpace(sourceProfileId))
            {
                result.Profiles.Add(new AnimationAuthoringProfile
                {
                    ProfileId = sourceProfileId,
                    DisplayName = sourceProfileId
                });
            }
            return result;
        }

        private static bool IsHumanoidRigIdentity(string skeletonProfileId, string avatarProfileId)
        {
            return (!string.IsNullOrWhiteSpace(skeletonProfileId) &&
                    skeletonProfileId.IndexOf("humanoid", StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrWhiteSpace(avatarProfileId) &&
                 avatarProfileId.IndexOf("humanoid", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static GlobalAbilityConfiguration BuildAbilityConfiguration(
            string globalId,
            string displayName,
            CharacterAbilityLoadoutAuthoringSummary baseAbility,
            CharacterAbilityLoadoutAuthoringSummary combinedAbility)
        {
            var result = new GlobalAbilityConfiguration
            {
                AbilityConfigurationId = globalId,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? globalId : displayName,
                Description = "Migrated complete ability set; no character base layer remains."
            };
            var abilityIds = new List<int>();
            var bindings = new List<GlobalAbilitySlotBinding>();
            AppendDistinct(abilityIds, baseAbility?.AbilityIds);
            AppendDistinct(abilityIds, combinedAbility?.AbilityIds);
            AppendAbilityBindings(bindings, baseAbility?.SlotBindings);
            AppendAbilityBindings(bindings, combinedAbility?.SlotBindings);
            abilityIds.Sort();
            for (int i = 0; i < abilityIds.Count; i++)
            {
                GlobalAbilitySlotBinding binding = FindLegacyAbilityBinding(bindings, abilityIds[i]);
                bool hasAction = binding != null && !string.IsNullOrWhiteSpace(binding.ActionRole);
                bool hasInput = binding != null && (!string.IsNullOrWhiteSpace(binding.SlotId) ||
                    !string.IsNullOrWhiteSpace(binding.InputIntentId));
                result.Abilities.Add(new GlobalAbilityEntry
                {
                    AbilityId = abilityIds[i],
                    DisplayName = "Ability " + abilityIds[i],
                    Tier = GlobalAbilityTiers.Unspecified,
                    ExecutionMode = hasAction ? GlobalAbilityExecutionModes.ActionOnly : GlobalAbilityExecutionModes.External,
                    TriggerMode = hasInput ? GlobalAbilityTriggerModes.Input : GlobalAbilityTriggerModes.External,
                    SlotId = binding?.SlotId ?? string.Empty,
                    InputIntentId = binding?.InputIntentId ?? string.Empty,
                    ActionBindingMode = hasAction ? GlobalAbilityActionBindingModes.Required : GlobalAbilityActionBindingModes.None,
                    ActionRole = binding?.ActionRole ?? string.Empty
                });
            }
            return result;
        }

        private static GlobalReactionConfiguration BuildReactionConfiguration(
            CharacterReactionProfileAuthoringSummary source,
            string globalId)
        {
            var result = new GlobalReactionConfiguration
            {
                ReactionConfigurationId = globalId,
                DisplayName = source?.DisplayName ?? globalId,
                Description = source?.Description ?? "Migrated reaction configuration.",
                DefaultSlotId = source?.DefaultSlotId ?? string.Empty
            };
            for (int i = 0; source?.Slots != null && i < source.Slots.Count; i++)
            {
                CharacterReactionSlotAuthoringSummary slot = source.Slots[i];
                if (slot == null)
                    continue;
                result.Slots.Add(new GlobalReactionSlotConfiguration
                {
                    SlotId = slot.SlotId,
                    DisplayName = slot.DisplayName,
                    Trigger = slot.Trigger,
                    PressureBand = slot.PressureBand,
                    ReactionGroupId = slot.ReactionGroupId,
                    MinImpactForce = slot.MinImpactForce,
                    MaxImpactForce = slot.MaxImpactForce,
                    ActionRole = "reaction." + NormalizeId(slot.SlotId, "default"),
                    DurationFrames = slot.DurationFrames,
                    Priority = slot.Priority,
                    ResourceKey = slot.ResourceKey,
                    PreloadPolicy = slot.PreloadPolicy,
                    Required = slot.Required,
                    ResourceSelection = Clone(slot.ResourceSelection) ?? new AuthoringResourceSelectionRef()
                });
            }
            result.Slots.Sort((left, right) => string.CompareOrdinal(left?.SlotId, right?.SlotId));
            return result;
        }

        private static void PromotePrivateActions(
            CombinedWeaponConfigurationAuthoringSummary combined,
            CharacterAbilityLoadoutAuthoringSummary baseAbility,
            CharacterAbilityLoadoutAuthoringSummary legacyAbility,
            CharacterReactionProfileAuthoringSummary legacyReaction,
            string packageId,
            string combinedId)
        {
            CombinedWeaponActionConfiguration actions = combined.ActionConfiguration ??
                new CombinedWeaponActionConfiguration();
            combined.ActionConfiguration = actions;
            if (string.IsNullOrWhiteSpace(actions.ActionSetId))
                actions.ActionSetId = string.IsNullOrWhiteSpace(combined.ActionSetId)
                    ? packageId + "." + combinedId
                    : combined.ActionSetId;

            PromoteAbilityActions(actions, baseAbility, packageId, combinedId);
            PromoteAbilityActions(actions, legacyAbility, packageId, combinedId);

            for (int i = 0; legacyReaction?.Slots != null && i < legacyReaction.Slots.Count; i++)
            {
                CharacterReactionSlotAuthoringSummary slot = legacyReaction.Slots[i];
                if (slot == null)
                    continue;
                string role = "reaction." + NormalizeId(slot.SlotId, "default");
                string actionKey = packageId + "." + combinedId + ".reaction." +
                    NormalizeId(slot.SlotId, "default");
                AddPrivateAction(actions, role, 0, actionKey, slot.ResourceKey, Math.Max(1, slot.DurationFrames));
                actions.ReactionActionBindings.Add(new CombinedWeaponReactionActionBinding
                {
                    ReactionSlotId = slot.SlotId,
                    ActionRole = role,
                    ActionId = 0
                });
            }
        }

        private static void PromoteAbilityActions(
            CombinedWeaponActionConfiguration actions,
            CharacterAbilityLoadoutAuthoringSummary ability,
            string packageId,
            string combinedId)
        {
            for (int i = 0; ability?.SlotBindings != null && i < ability.SlotBindings.Count; i++)
            {
                CharacterAbilitySlotBindingAuthoringSummary slot = ability.SlotBindings[i];
                if (slot == null)
                    continue;
                string role = "ability." + NormalizeId(slot.SlotId, slot.AbilityId.ToString());
                string actionKey = packageId + "." + combinedId + "." + NormalizeId(slot.SlotId, slot.AbilityId.ToString());
                AddPrivateAction(actions, role, 0, actionKey, string.Empty, 1);
                for (int existingIndex = actions.AbilityActionBindings.Count - 1; existingIndex >= 0; existingIndex--)
                    if (actions.AbilityActionBindings[existingIndex]?.AbilityId == slot.AbilityId ||
                        string.Equals(actions.AbilityActionBindings[existingIndex]?.ActionRole, role, StringComparison.Ordinal))
                        actions.AbilityActionBindings.RemoveAt(existingIndex);
                actions.AbilityActionBindings.Add(new CombinedWeaponAbilityActionBinding
                {
                    AbilityId = slot.AbilityId,
                    ActionRole = role,
                    ActionId = 0
                });
            }
        }

        private static void AddPrivateAction(
            CombinedWeaponActionConfiguration actions,
            string role,
            int actionId,
            string actionKey,
            string animationClipId,
            int durationFrames)
        {
            for (int i = 0; i < actions.Actions.Count; i++)
                if (string.Equals(actions.Actions[i]?.ActionRole, role, StringComparison.Ordinal))
                    return;
            actions.Actions.Add(new CombinedWeaponActionEntry
            {
                ActionRole = role,
                ActionId = actionId,
                TimelineId = actionKey + ".timeline",
                AnimationClipId = animationClipId,
                Action = CreateJsonElement(new
                {
                    actionId,
                    displayName = role,
                    category = ResolvePrivateActionCategory(role),
                    timelineAuthority = "CharacterAuthored",
                    tags = new[] { role },
                    priority = 0,
                    durationFrames,
                    requirements = Array.Empty<object>(),
                    phases = new[] { new { kind = "Active", startFrame = 0, endFrame = durationFrames - 1 } },
                    cancelRules = Array.Empty<object>(),
                    interruptRules = Array.Empty<object>()
                }),
                Timeline = CreateJsonElement(new
                {
                    timelineId = actionId + ".timeline",
                    durationFrames,
                    animationClipResourceKey = animationClipId
                })
            });
        }

        private static JsonElement CreateJsonElement<T>(T value)
        {
            return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value));
        }

        private static string ResolvePrivateActionCategory(string role)
        {
            if (role.StartsWith("reaction.", StringComparison.Ordinal))
                return "Reaction";
            return role.IndexOf("dodge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                role.IndexOf("evade", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Dodge"
                : "Skill";
        }

        private static void AppendAbilityBindings(
            List<GlobalAbilitySlotBinding> target,
            List<CharacterAbilitySlotBindingAuthoringSummary> source)
        {
            for (int i = 0; source != null && i < source.Count; i++)
            {
                CharacterAbilitySlotBindingAuthoringSummary slot = source[i];
                if (slot == null)
                    continue;
                for (int existingIndex = target.Count - 1; existingIndex >= 0; existingIndex--)
                    if (string.Equals(target[existingIndex].SlotId, slot.SlotId, StringComparison.Ordinal))
                        target.RemoveAt(existingIndex);
                target.Add(new GlobalAbilitySlotBinding
                {
                    SlotId = slot.SlotId,
                    AbilityId = slot.AbilityId,
                    InputIntentId = slot.InputIntentId,
                    ActionRole = "ability." + NormalizeId(slot.SlotId, slot.AbilityId.ToString())
                });
            }
        }

        private static void AppendDistinct(List<int> target, List<int> source)
        {
            for (int i = 0; source != null && i < source.Count; i++)
                if (!target.Contains(source[i]))
                    target.Add(source[i]);
        }

        private static CharacterAbilityLoadoutAuthoringSummary FindAbilityLoadout(
            CharacterApplicationAuthoringSummary application,
            string id)
        {
            for (int i = 0; application?.AbilityLoadouts != null && i < application.AbilityLoadouts.Count; i++)
            {
                CharacterAbilityLoadoutAuthoringSummary item = application.AbilityLoadouts[i];
                if (item != null && (string.Equals(item.LoadoutId, id, StringComparison.Ordinal) ||
                    string.Equals(item.StableId, id, StringComparison.Ordinal)))
                    return item;
            }
            return null;
        }

        private static void EnsureGlobalCollections(GlobalWeaponLibrary library)
        {
            library.SingleWeaponConfigurations ??= new List<SingleWeaponConfiguration>();
            library.AnimationConfigurations ??= new List<GlobalAnimationConfiguration>();
            library.AbilityConfigurations ??= new List<GlobalAbilityConfiguration>();
            library.ReactionConfigurations ??= new List<GlobalReactionConfiguration>();
        }

        private static void AddOrReuse<T>(List<T> target, T candidate, Func<T, string> idSelector)
        {
            string id = idSelector(candidate);
            for (int i = 0; i < target.Count; i++)
            {
                if (!string.Equals(idSelector(target[i]), id, StringComparison.Ordinal))
                    continue;
                if (!EquivalentJson(target[i], candidate))
                    throw new InvalidOperationException(
                        CharacterAuthoringValidationCodes.WeaponSchemaMigrationGlobalConflict +
                        ": global configuration ID '" + id + "' has different content.");
                return;
            }
            target.Add(candidate);
        }

        private static bool EquivalentJson<T>(T left, T right)
        {
            return string.Equals(JsonSerializer.Serialize(left), JsonSerializer.Serialize(right), StringComparison.Ordinal);
        }

        private static T DeepClone<T>(T source)
        {
            if (source == null)
                return default;
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source));
        }

        private static string NormalizeId(string value, string fallback)
        {
            string source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            var builder = new StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                char character = char.ToLowerInvariant(source[i]);
                builder.Append(char.IsLetterOrDigit(character) || character == '.' || character == '_'
                    ? character
                    : '_');
            }
            return builder.ToString().Trim('_');
        }

        private static CombinedWeaponConfigurationSlotAuthoringSummary MigrateSlot(
            CharacterWeaponConfigurationAuthoringSummary source,
            string slotId)
        {
            List<CharacterWeaponConfigurationSlotAuthoringSummary> slots = source?.Slots;
            for (int i = 0; slots != null && i < slots.Count; i++)
            {
                CharacterWeaponConfigurationSlotAuthoringSummary slot = slots[i];
                if (slot != null && string.Equals(slot.SlotId, slotId, StringComparison.Ordinal))
                {
                    string singleWeaponConfigurationId = string.IsNullOrWhiteSpace(slot.WeaponId)
                        ? GlobalWeaponLibrarySchema.UnarmedConfigurationId
                        : slot.WeaponId;
                    return new CombinedWeaponConfigurationSlotAuthoringSummary
                    {
                        SlotId = slotId,
                        SingleWeaponConfigurationId = singleWeaponConfigurationId,
                    };
                }
            }
            return new CombinedWeaponConfigurationSlotAuthoringSummary
            {
                SlotId = slotId,
                SingleWeaponConfigurationId = GlobalWeaponLibrarySchema.UnarmedConfigurationId
            };
        }

        private static void CheckPromotionConflicts(
            CharacterResourcePackageMigrationPlan plan,
            CharacterResourcePackage package,
            GlobalWeaponLibrary library)
        {
            CharacterApplicationAuthoringSummary application = package?.ApplicationConfig;
            List<CharacterWeaponDefinitionAuthoringSummary> legacy = application?.LegacyWeaponDefinitions;
            for (int i = 0; library != null && legacy != null && i < legacy.Count; i++)
            {
                CharacterWeaponDefinitionAuthoringSummary source = legacy[i];
                SingleWeaponConfiguration existing = FindSingle(library, source?.WeaponId);
                if (existing != null && !Equivalent(existing, source))
                {
                    plan.CanMigrate = false;
                    plan.BlockingReasons.Add(
                        CharacterAuthoringValidationCodes.WeaponSchemaMigrationGlobalConflict +
                        ": single weapon ID '" + source.WeaponId + "' has different global content.");
                }
                else if (existing == null && source != null && !string.IsNullOrWhiteSpace(source.WeaponId))
                {
                    plan.GlobalWeaponLibraryWriteRequired = true;
                }
            }

            if (library != null && string.Equals(library.SchemaVersion, "1.0", StringComparison.Ordinal))
                plan.GlobalWeaponLibraryWriteRequired = true;

            if (library == null || application == null ||
                string.Equals(application.SchemaVersion, CurrentCharacterApplicationSchemaVersion, StringComparison.Ordinal))
                return;

            string packageId = NormalizeId(package.Manifest?.PackageId, "character");
            if (string.Equals(application.SchemaVersion, "6.0", StringComparison.Ordinal))
            {
                for (int i = 0; i < application.CombinedWeaponConfigurations.Count; i++)
                {
                    CombinedWeaponConfigurationAuthoringSummary combined = application.CombinedWeaponConfigurations[i];
                    if (combined == null)
                        continue;
                    string combinedId = NormalizeId(combined.CombinedWeaponConfigurationId, "configuration");
                    CombinedWeaponConfigurationAuthoringSummary publicCandidate = DeepClone(combined);
                    publicCandidate.CombinedWeaponConfigurationId = packageId + "." + combinedId;
                    CheckCandidateConflict(
                        plan,
                        library.CombinedWeaponConfigurations,
                        publicCandidate,
                        item => item.CombinedWeaponConfigurationId);
                }
                return;
            }

            CharacterAbilityLoadoutAuthoringSummary baseAbility = FindAbilityLoadout(application, "base");
            CharacterReactionProfileAuthoringSummary reaction = application.ReactionProfiles != null &&
                application.ReactionProfiles.Count > 0 ? application.ReactionProfiles[0] : null;
            for (int i = 0; i < application.CombinedWeaponConfigurations.Count; i++)
            {
                CombinedWeaponConfigurationAuthoringSummary combined = application.CombinedWeaponConfigurations[i];
                if (combined == null)
                    continue;
                string combinedId = NormalizeId(combined.CombinedWeaponConfigurationId, "configuration");
                string animationSourceId = string.IsNullOrWhiteSpace(combined.AnimationProfileId)
                    ? "default"
                    : combined.AnimationProfileId;
                CheckCandidateConflict(
                    plan,
                    library.AnimationConfigurations,
                    BuildAnimationConfiguration(
                        package.AnimationAuthoring,
                        animationSourceId,
                        "animation." + packageId + "." + NormalizeId(animationSourceId, "default")),
                    item => item.AnimationConfigurationId);
                CheckCandidateConflict(
                    plan,
                    library.AbilityConfigurations,
                    BuildAbilityConfiguration(
                        "ability." + packageId + "." + combinedId,
                        combined.DisplayName,
                        baseAbility,
                        FindAbilityLoadout(application, combined.AbilityLoadoutId)),
                    item => item.AbilityConfigurationId);
                string reactionSourceId = string.IsNullOrWhiteSpace(reaction?.ProfileId)
                    ? "default"
                    : reaction.ProfileId;
                CheckCandidateConflict(
                    plan,
                    library.ReactionConfigurations,
                    BuildReactionConfiguration(
                        reaction,
                        "reaction." + packageId + "." + NormalizeId(reactionSourceId, "default")),
                    item => item.ReactionConfigurationId);

            }
        }

        private static void CheckWeaponOwnershipPromotionConflicts(
            CharacterResourcePackageMigrationPlan plan,
            CharacterResourcePackage package)
        {
            CharacterAuthoringGeometry geometry = package?.Geometry;
            if (geometry == null || geometry.WeaponAttachments == null || geometry.WeaponAttachments.Count == 0)
                return;

            var attachmentsBySlot = new Dictionary<string, WeaponAttachmentProfile>(StringComparer.Ordinal);
            for (int i = 0; i < geometry.WeaponAttachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = geometry.WeaponAttachments[i];
                if (attachment == null || string.IsNullOrWhiteSpace(attachment.SlotId))
                    continue;
                if (!attachmentsBySlot.TryGetValue(attachment.SlotId, out WeaponAttachmentProfile first))
                {
                    attachmentsBySlot.Add(attachment.SlotId, attachment);
                    continue;
                }
                if (!string.Equals(first.AttachSocketId, attachment.AttachSocketId, StringComparison.Ordinal) ||
                    !EquivalentJson(first.LocalGripPose, attachment.LocalGripPose))
                {
                    AddMigrationConflict(plan,
                        CharacterAuthoringValidationCodes.WeaponSlotMigrationConflict +
                        ": slot '" + attachment.SlotId + "' has multiple legacy socket or pose values.");
                }
            }

            var tracesByWeaponAndId = new Dictionary<string, WeaponTraceProfile>(StringComparer.Ordinal);
            for (int attachmentIndex = 0; attachmentIndex < geometry.WeaponAttachments.Count; attachmentIndex++)
            {
                WeaponAttachmentProfile attachment = geometry.WeaponAttachments[attachmentIndex];
                if (attachment == null || string.IsNullOrWhiteSpace(attachment.SingleWeaponConfigurationId))
                    continue;
                for (int traceIndex = 0; traceIndex < geometry.Traces.Count; traceIndex++)
                {
                    WeaponTraceProfile trace = geometry.Traces[traceIndex];
                    if (trace == null || string.IsNullOrWhiteSpace(trace.TraceId) ||
                        (!string.IsNullOrWhiteSpace(attachment.TraceId) &&
                         !string.Equals(attachment.TraceId, trace.TraceId, StringComparison.Ordinal)))
                        continue;
                    string key = attachment.SingleWeaponConfigurationId + "\n" + trace.TraceId;
                    if (!tracesByWeaponAndId.TryGetValue(key, out WeaponTraceProfile first))
                    {
                        tracesByWeaponAndId.Add(key, trace);
                        continue;
                    }
                    if (!EquivalentLegacyTraceContent(first, trace))
                    {
                        AddMigrationConflict(plan,
                            CharacterAuthoringValidationCodes.SingleWeaponTraceMigrationConflict +
                            ": weapon '" + attachment.SingleWeaponConfigurationId + "' trace '" + trace.TraceId +
                            "' has multiple legacy definitions.");
                    }
                }
            }
        }

        private static bool EquivalentLegacyTraceContent(WeaponTraceProfile left, WeaponTraceProfile right)
        {
            return left != null && right != null &&
                string.Equals(left.TraceId, right.TraceId, StringComparison.Ordinal) &&
                string.Equals(left.StartLocatorPath, right.StartLocatorPath, StringComparison.Ordinal) &&
                string.Equals(left.EndLocatorPath, right.EndLocatorPath, StringComparison.Ordinal) &&
                EquivalentJson(ToWeaponLocalPose(left.StartPose), ToWeaponLocalPose(right.StartPose)) &&
                EquivalentJson(ToWeaponLocalPose(left.EndPose), ToWeaponLocalPose(right.EndPose)) &&
                left.Radius.Equals(right.Radius) &&
                left.ProximalExtension.Equals(right.ProximalExtension) &&
                left.ProximalRadius.Equals(right.ProximalRadius) &&
                left.SampleRule == right.SampleRule &&
                left.FixedSampleCount == right.FixedSampleCount &&
                Equal(left.ActionKeys, right.ActionKeys);
        }

        private static void AddMigrationConflict(CharacterResourcePackageMigrationPlan plan, string reason)
        {
            plan.CanMigrate = false;
            if (!plan.BlockingReasons.Contains(reason))
                plan.BlockingReasons.Add(reason);
        }

        private static void CheckCandidateConflict<T>(
            CharacterResourcePackageMigrationPlan plan,
            List<T> existingItems,
            T candidate,
            Func<T, string> idSelector)
        {
            string id = idSelector(candidate);
            for (int i = 0; existingItems != null && i < existingItems.Count; i++)
            {
                T existing = existingItems[i];
                if (!string.Equals(idSelector(existing), id, StringComparison.Ordinal))
                    continue;
                if (!EquivalentJson(existing, candidate))
                {
                    plan.CanMigrate = false;
                    string reason = CharacterAuthoringValidationCodes.WeaponSchemaMigrationGlobalConflict +
                        ": global configuration ID '" + id + "' has different content.";
                    if (!plan.BlockingReasons.Contains(reason))
                        plan.BlockingReasons.Add(reason);
                }
                return;
            }
            plan.GlobalWeaponLibraryWriteRequired = true;
        }

        private static bool Equivalent(
            SingleWeaponConfiguration existing,
            CharacterWeaponDefinitionAuthoringSummary source)
        {
            return existing != null && source != null &&
                string.Equals(existing.SingleWeaponConfigurationId, source.WeaponId, StringComparison.Ordinal) &&
                string.Equals(existing.DisplayName, source.DisplayName, StringComparison.Ordinal) &&
                string.Equals(existing.Category, source.Category, StringComparison.Ordinal) &&
                Equal(existing.Tags, source.Tags) &&
                Equal(existing.AllowedSlots, source.AllowedSlots) &&
                Equal(existing.OccupiesSlots, source.OccupiesSlots) &&
                Equal(existing.WeaponRef, source.WeaponRef);
        }

        private static bool Equal(List<string> left, List<string> right)
        {
            left ??= new List<string>();
            right ??= new List<string>();
            if (left.Count != right.Count)
                return false;
            var leftCopy = new List<string>(left);
            var rightCopy = new List<string>(right);
            leftCopy.Sort(StringComparer.Ordinal);
            rightCopy.Sort(StringComparer.Ordinal);
            for (int i = 0; i < leftCopy.Count; i++)
                if (!string.Equals(leftCopy[i], rightCopy[i], StringComparison.Ordinal))
                    return false;
            return true;
        }

        private static bool Equal(AuthoringResourceSelectionRef left, AuthoringResourceSelectionRef right)
        {
            if (left == null || right == null)
                return left == right;
            return string.Equals(left.ResourceStableId, right.ResourceStableId, StringComparison.Ordinal) &&
                string.Equals(left.SourceProviderId, right.SourceProviderId, StringComparison.Ordinal) &&
                left.BindingKind == right.BindingKind &&
                string.Equals(left.ExpectedKind, right.ExpectedKind, StringComparison.Ordinal) &&
                string.Equals(left.ExpectedUsage, right.ExpectedUsage, StringComparison.Ordinal) &&
                string.Equals(left.ExpectedHash, right.ExpectedHash, StringComparison.Ordinal) &&
                string.Equals(left.UnityGuid, right.UnityGuid, StringComparison.Ordinal) &&
                string.Equals(left.UnityAssetPath, right.UnityAssetPath, StringComparison.Ordinal) &&
                string.Equals(left.RuntimeResourceKey, right.RuntimeResourceKey, StringComparison.Ordinal) &&
                string.Equals(left.ProviderResourceKey, right.ProviderResourceKey, StringComparison.Ordinal) &&
                string.Equals(left.PackageResourceKey, right.PackageResourceKey, StringComparison.Ordinal) &&
                string.Equals(left.AudioCueId, right.AudioCueId, StringComparison.Ordinal) &&
                string.Equals(left.AudioEventDefinitionId, right.AudioEventDefinitionId, StringComparison.Ordinal);
        }

        private static void EnsureUnarmed(GlobalWeaponLibrary library)
        {
            EnsureGlobalCollections(library);
            if (FindSingle(library, GlobalWeaponLibrarySchema.UnarmedConfigurationId) == null)
                library.SingleWeaponConfigurations.Add(GlobalWeaponLibrarySchema.CreateUnarmed());
        }

        private static SingleWeaponConfiguration FindSingle(GlobalWeaponLibrary library, string id)
        {
            List<SingleWeaponConfiguration> items = library?.SingleWeaponConfigurations;
            for (int i = 0; items != null && i < items.Count; i++)
                if (items[i] != null && string.Equals(items[i].SingleWeaponConfigurationId, id, StringComparison.Ordinal))
                    return items[i];
            return null;
        }

        private static List<string> Clone(List<string> source)
        {
            return source != null ? new List<string>(source) : new List<string>();
        }

        private static AuthoringResourceSelectionRef Clone(AuthoringResourceSelectionRef source)
        {
            if (source == null)
                return null;
            return new AuthoringResourceSelectionRef
            {
                ResourceStableId = source.ResourceStableId,
                SourceProviderId = source.SourceProviderId,
                BindingKind = source.BindingKind,
                ExpectedKind = source.ExpectedKind,
                ExpectedUsage = source.ExpectedUsage,
                ExpectedHash = source.ExpectedHash,
                UnityGuid = source.UnityGuid,
                UnityAssetPath = source.UnityAssetPath,
                RuntimeResourceKey = source.RuntimeResourceKey,
                ProviderResourceKey = source.ProviderResourceKey,
                PackageResourceKey = source.PackageResourceKey,
                AudioCueId = source.AudioCueId,
                AudioEventDefinitionId = source.AudioEventDefinitionId
            };
        }

        private static void AddDocument(
            CharacterResourcePackageMigrationPlan plan,
            string document,
            string version,
            string targetVersion,
            bool treatEmptyAsLegacy = false)
        {
            string normalized = (version ?? string.Empty).Trim();
            if (string.Equals(normalized, targetVersion, StringComparison.Ordinal))
                return;
            if (string.IsNullOrEmpty(normalized) && !treatEmptyAsLegacy)
                normalized = "0.0";
            if (!TryParseVersion(normalized, out Version parsed) || !TryParseVersion(targetVersion, out Version current))
            {
                plan.CanMigrate = false;
                plan.BlockingReasons.Add(document + " has an invalid schema version: " + normalized);
                return;
            }
            if (parsed > current)
            {
                plan.CanMigrate = false;
                plan.BlockingReasons.Add(document + " uses unsupported future schema " + normalized +
                    "; target is " + targetVersion + ".");
                return;
            }
            plan.Steps.Add(new CharacterResourcePackageMigrationStep
            {
                Document = document,
                FromVersion = normalized,
                ToVersion = targetVersion,
                Description = string.Equals(targetVersion, "4.0", StringComparison.Ordinal)
                    ? "Upgrade to global reusable behavior configurations and character combined-weapon recipes."
                    : "Keep this structurally unchanged document on its 1.0 contract."
            });
        }

        private static bool TryParseVersion(string value, out Version version)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                version = new Version(0, 0);
                return true;
            }
            string normalized = value.Trim();
            if (normalized.IndexOf('.') < 0)
                normalized += ".0";
            return Version.TryParse(normalized, out version);
        }
    }
}
