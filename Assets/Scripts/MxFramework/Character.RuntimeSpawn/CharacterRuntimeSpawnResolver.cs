using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Resources;

namespace MxFramework.CharacterRuntimeSpawn
{
    public static class CharacterRuntimeSpawnResolver
    {
        public static CharacterRuntimeSpawnResult Resolve(CharacterImportedPackage package, CharacterSpawnRequest request)
        {
            var issues = new List<CharacterRuntimeSpawnIssue>();
            if (package == null)
            {
                issues.AddIssue(
                    CharacterRuntimeSpawnIssueSeverity.Error,
                    CharacterRuntimeSpawnIssueCodes.MissingImportedPackage,
                    string.Empty,
                    nameof(package),
                    "Imported character package is required.");
                return new CharacterRuntimeSpawnResult(CharacterRuntimeSpawnStatus.MissingImportedPackage, null, default, issues.ToArray());
            }

            CharacterUnityImportRuntimeReport report = package.ImportReport;
            if (report == null || !report.CanWriteToUnityProject)
            {
                issues.AddIssue(
                    CharacterRuntimeSpawnIssueSeverity.Error,
                    CharacterRuntimeSpawnIssueCodes.ImportBlocked,
                    report == null ? string.Empty : report.ReportPath,
                    nameof(CharacterUnityImportRuntimeReport.CanWriteToUnityProject),
                    "Imported package has no writable Unity import report. Runtime must not consume ImportBlocked packages.");
                return new CharacterRuntimeSpawnResult(CharacterRuntimeSpawnStatus.ImportBlocked, null, default, issues.ToArray());
            }

            if (!report.CanSpawnAfterImport)
            {
                issues.AddIssue(
                    CharacterRuntimeSpawnIssueSeverity.Error,
                    CharacterRuntimeSpawnIssueCodes.SpawnBlocked,
                    report.ReportPath,
                    nameof(CharacterUnityImportRuntimeReport.CanSpawnAfterImport),
                    "Imported package is marked SpawnBlocked by the authoring compiler gate.");
                return new CharacterRuntimeSpawnResult(CharacterRuntimeSpawnStatus.SpawnBlocked, null, default, issues.ToArray());
            }

            SpawnProfileConfig spawnProfile = ResolveSpawnProfile(package.Configs, request);
            if (spawnProfile == null)
            {
                issues.AddIssue(
                    CharacterRuntimeSpawnIssueSeverity.Error,
                    CharacterRuntimeSpawnIssueCodes.MissingConfig,
                    "config/character_config_patch.json",
                    SpawnProfileConfig.TableName,
                    "Imported package does not contain a spawn profile.");
                return new CharacterRuntimeSpawnResult(CharacterRuntimeSpawnStatus.ResolveFailed, null, default, issues.ToArray());
            }

            CharacterSpawnRequest effectiveRequest = request.SpawnProfileId.IsValid
                ? request
                : new CharacterSpawnRequest(spawnProfile.SpawnProfileId);
            CharacterSpawnPlan spawnPlan = SpawnPlanResolver.Resolve(spawnProfile, effectiveRequest);
            AddSpawnPlanDiagnostics(spawnPlan, issues);
            if (HasCharacterDiagnosticErrors(spawnPlan.Diagnostics))
                return new CharacterRuntimeSpawnResult(CharacterRuntimeSpawnStatus.ResolveFailed, null, default, issues.ToArray());

            CharacterConfig character = package.Configs.FindCharacter(spawnPlan.CharacterId);
            if (character == null)
            {
                issues.AddIssue(
                    CharacterRuntimeSpawnIssueSeverity.Error,
                    CharacterRuntimeSpawnIssueCodes.MissingConfig,
                    "config/character_config_patch.json",
                    CharacterConfig.TableName,
                    "Spawn plan references a missing CharacterConfig: " + spawnPlan.CharacterId.Value + ".");
                return new CharacterRuntimeSpawnResult(CharacterRuntimeSpawnStatus.ResolveFailed, null, default, issues.ToArray());
            }

            CharacterPackageResolveRequest resolveRequest = CreatePackageResolveRequest(package.Configs, character, spawnPlan.LoadoutId, issues);
            if (HasRuntimeErrors(issues))
                return new CharacterRuntimeSpawnResult(CharacterRuntimeSpawnStatus.ResolveFailed, null, default, issues.ToArray());

            CharacterPackageResolveResult resolved = CharacterPackageResolver.Resolve(resolveRequest);
            AddCharacterDiagnostics(resolved.ValidationReport.Issues, issues);
            if (resolved.ValidationReport.HasErrors)
            {
                issues.AddIssue(
                    CharacterRuntimeSpawnIssueSeverity.Error,
                    CharacterRuntimeSpawnIssueCodes.ResolverFailed,
                    "config/character_config_patch.json",
                    nameof(CharacterPackageResolver),
                    "CharacterPackageResolver returned errors; Runtime binding was not created.");
                return new CharacterRuntimeSpawnResult(CharacterRuntimeSpawnStatus.ResolveFailed, null, resolved, issues.ToArray());
            }

            CharacterRuntimeBinding binding = CreateBinding(package, spawnPlan, resolved, issues);
            CharacterRuntimeSpawnStatus status = HasRuntimeErrors(issues)
                ? CharacterRuntimeSpawnStatus.ResolveFailed
                : CharacterRuntimeSpawnStatus.Success;

            return new CharacterRuntimeSpawnResult(status, status == CharacterRuntimeSpawnStatus.Success ? binding : null, resolved, issues.ToArray());
        }

        private static SpawnProfileConfig ResolveSpawnProfile(CharacterImportedConfigSet configs, CharacterSpawnRequest request)
        {
            if (configs == null)
                return null;
            if (request.SpawnProfileId.IsValid)
                return configs.FindSpawnProfile(request.SpawnProfileId);

            return configs.GetDefaultSpawnProfile();
        }

        private static CharacterPackageResolveRequest CreatePackageResolveRequest(
            CharacterImportedConfigSet configs,
            CharacterConfig character,
            EquipmentLoadoutId loadoutId,
            List<CharacterRuntimeSpawnIssue> issues)
        {
            CharacterAttributeProfileConfig attributes = configs.FindAttributeProfile(character.AttributeProfileId);
            CharacterBodyProfileConfig body = configs.FindBodyProfile(character.BodyProfileId);
            EquipmentSchemaConfig schema = configs.FindEquipmentSchema(character.EquipmentSchemaId);
            EquipmentLoadoutConfig loadout = configs.FindLoadout(loadoutId.IsValid ? loadoutId : character.DefaultLoadoutId);
            CharacterPresentationProfileConfig presentation = configs.FindPresentationProfile(character.PresentationProfileId);

            AddMissingConfigIssue(attributes == null, CharacterAttributeProfileConfig.TableName, character.AttributeProfileId.Value, issues);
            AddMissingConfigIssue(body == null, CharacterBodyProfileConfig.TableName, character.BodyProfileId.Value, issues);
            AddMissingConfigIssue(schema == null, EquipmentSchemaConfig.TableName, character.EquipmentSchemaId.Value, issues);
            AddMissingConfigIssue(loadout == null, EquipmentLoadoutConfig.TableName, loadoutId.Value, issues);
            AddMissingConfigIssue(presentation == null, CharacterPresentationProfileConfig.TableName, character.PresentationProfileId.Value, issues);

            return new CharacterPackageResolveRequest(
                character,
                attributes,
                body,
                configs.BodyParts,
                schema,
                loadout,
                configs.EquipmentStates,
                configs.Weapons,
                configs.AbilityLoadouts,
                configs.CombatActionSets,
                presentation);
        }

        private static void AddMissingConfigIssue(bool missing, string table, int id, List<CharacterRuntimeSpawnIssue> issues)
        {
            if (!missing)
                return;

            issues.AddIssue(
                CharacterRuntimeSpawnIssueSeverity.Error,
                CharacterRuntimeSpawnIssueCodes.MissingConfig,
                "config/character_config_patch.json",
                table,
                "Imported package is missing " + table + " id " + id + ".");
        }

        private static CharacterRuntimeBinding CreateBinding(
            CharacterImportedPackage package,
            CharacterSpawnPlan spawnPlan,
            CharacterPackageResolveResult resolved,
            List<CharacterRuntimeSpawnIssue> issues)
        {
            int stableId = spawnPlan.CharacterId.Value;
            var gameplayEntityId = new GameplayEntityId(stableId, 1);
            var combatEntityId = new CombatEntityId(stableId);
            var combatBodyId = new CombatBodyId(stableId);
            CharacterControlEntityRef entityRef = CharacterControlEntityRef.FromGameplayAndCombat(
                gameplayEntityId,
                combatEntityId,
                combatBodyId,
                stableId);

            var gameplayPlan = new CharacterGameplayRegistrationPlan(
                gameplayEntityId,
                spawnPlan.CharacterId,
                spawnPlan.TeamId,
                willCreateEntity: false,
                diagnostics: new[]
                {
                    "First slice creates a registration plan only; GameplayComponentWorld instantiation is a later slice."
                });
            issues.AddIssue(
                CharacterRuntimeSpawnIssueSeverity.Info,
                CharacterRuntimeSpawnIssueCodes.DeferredBinding,
                string.Empty,
                nameof(CharacterGameplayRegistrationPlan),
                "Gameplay entity creation is deferred; this slice emits a registration plan.");

            CharacterCombatBodyBindingPlan combatPlan = CreateCombatBodyPlan(package, combatEntityId, combatBodyId, issues);
            CharacterWeaponAttachmentBindingPlan weaponPlan = new CharacterWeaponAttachmentBindingPlan(
                package.Geometry.WeaponAttachments,
                package.Geometry.WeaponTraces);
            CharacterResourcePreloadBindingPlan resourcePlan = CreateResourcePreloadPlan(package, resolved.ResolvedProfile, issues);
            string debugSummary = CreateDebugSummary(package, spawnPlan, resolved, entityRef, combatPlan, resourcePlan);

            return new CharacterRuntimeBinding(
                entityRef,
                spawnPlan,
                resolved.ResolvedProfile,
                package.ImportReport.SourcePackageHash,
                package.ImportReport.GeneratedConfigHash,
                package.ImportReport.GeometryBindingHash,
                package.ImportReport.ResourceMappingHash,
                gameplayPlan,
                combatPlan,
                weaponPlan,
                resourcePlan,
                debugSummary);
        }

        private static CharacterCombatBodyBindingPlan CreateCombatBodyPlan(
            CharacterImportedPackage package,
            CombatEntityId entityId,
            CombatBodyId bodyId,
            List<CharacterRuntimeSpawnIssue> issues)
        {
            CharacterBodyColliderRuntimeBinding[] source = package.Geometry.BodyColliders;
            var colliders = new CharacterCombatColliderBinding[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                CharacterBodyColliderRuntimeBinding collider = source[i];
                BodyPartHitZoneResolveResult hitZone = BodyPartHitZoneResolver.Resolve(
                    package.Configs.FindBodyProfile(package.Configs.FindCharacter(new CharacterConfigId(entityId.Value))?.BodyProfileId ?? default),
                    package.Configs.BodyParts,
                    collider.HitZoneId);

                AddCharacterDiagnostics(hitZone.Diagnostics, issues);
                colliders[i] = new CharacterCombatColliderBinding(
                    new CombatColliderId((bodyId.Value * 100) + i + 1),
                    collider.ColliderId,
                    string.IsNullOrEmpty(hitZone.PartId) ? collider.PartId : hitZone.PartId,
                    collider.HitZoneId,
                    collider.Shape,
                    collider.Priority,
                    collider.IsWeakPoint);
            }

            return new CharacterCombatBodyBindingPlan(entityId, bodyId, colliders);
        }

        private static CharacterResourcePreloadBindingPlan CreateResourcePreloadPlan(
            CharacterImportedPackage package,
            CharacterResolvedProfile profile,
            List<CharacterRuntimeSpawnIssue> issues)
        {
            var required = new List<ResourceKey>();
            var resolved = new List<CharacterResolvedResourceBinding>();
            var missing = new List<string>();

            for (int i = 0; i < profile.RequiredResources.Length; i++)
            {
                CharacterResourceKeyEntry entry = profile.RequiredResources[i];
                var requestedKey = new ResourceKey(entry.Id, entry.TypeId, entry.Variant, entry.PackageId);
                required.Add(requestedKey);

                CharacterImportedResourceMappingEntry mapping = package.ResourceMapping.FindByPackageResourceKey(entry.Id);
                ResourceCatalogEntry catalogEntry = FindCatalogEntry(package.UnityResourceCatalog, entry.Id);
                if (mapping == null && catalogEntry == null)
                {
                    if (string.IsNullOrEmpty(entry.PackageId))
                    {
                        resolved.Add(new CharacterResolvedResourceBinding(
                            requestedKey,
                            requestedKey,
                            "characterVirtual",
                            string.Empty,
                            string.Empty));
                        continue;
                    }

                    missing.Add(entry.Id);
                    issues.AddIssue(
                        CharacterRuntimeSpawnIssueSeverity.Error,
                        CharacterRuntimeSpawnIssueCodes.MissingResourceMapping,
                        "config/resource_catalog_mapping.json",
                        entry.Id,
                        "Required resource has no project resource mapping.");
                    continue;
                }

                string projectId = mapping == null || string.IsNullOrEmpty(mapping.ProjectResourceKey)
                    ? catalogEntry.Id
                    : mapping.ProjectResourceKey;
                string projectType = catalogEntry == null ? entry.TypeId : catalogEntry.TypeId;
                string projectVariant = catalogEntry == null ? entry.Variant : catalogEntry.Variant;
                string projectPackage = catalogEntry == null ? entry.PackageId : catalogEntry.PackageId;
                string provider = catalogEntry == null ? string.Empty : catalogEntry.ProviderId;
                string address = catalogEntry == null ? mapping.ImportTargetPath : catalogEntry.Address;
                string assetPath = catalogEntry != null && catalogEntry.ProviderData.TryGetValue("assetPath", out string value)
                    ? value
                    : mapping?.ImportTargetPath ?? string.Empty;

                resolved.Add(new CharacterResolvedResourceBinding(
                    requestedKey,
                    new ResourceKey(projectId, projectType, projectVariant, projectPackage),
                    provider,
                    address,
                    assetPath));
            }

            return new CharacterResourcePreloadBindingPlan(required.ToArray(), resolved.ToArray(), missing.ToArray());
        }

        private static ResourceCatalogEntry FindCatalogEntry(ResourceCatalog catalog, string id)
        {
            if (catalog == null || string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (string.Equals(entry.Id, id, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static string CreateDebugSummary(
            CharacterImportedPackage package,
            CharacterSpawnPlan spawnPlan,
            CharacterPackageResolveResult resolved,
            CharacterControlEntityRef entityRef,
            CharacterCombatBodyBindingPlan combatPlan,
            CharacterResourcePreloadBindingPlan resourcePlan)
        {
            return "character=" + resolved.ResolvedProfile.DebugContext.CharacterStableId
                + " loadout=" + resolved.ResolvedProfile.DebugContext.LoadoutStableId
                + " equipmentState=" + resolved.ResolvedProfile.DebugContext.ActiveEquipmentStateStableId
                + " animation=" + resolved.ResolvedProfile.AnimationProfileId
                + " entityRef=" + entityRef
                + " team=" + spawnPlan.TeamId
                + " colliders=" + combatPlan.Colliders.Length
                + " resources=" + resourcePlan.ResolvedResources.Length + "/" + resourcePlan.RequiredResources.Length
                + " sourcePackageHash=" + package.ImportReport.SourcePackageHash
                + " resourceMappingHash=" + package.ImportReport.ResourceMappingHash;
        }

        private static void AddSpawnPlanDiagnostics(CharacterSpawnPlan plan, List<CharacterRuntimeSpawnIssue> issues)
        {
            AddCharacterDiagnostics(plan.Diagnostics, issues);
        }

        private static void AddCharacterDiagnostics(CharacterDiagnostic[] diagnostics, List<CharacterRuntimeSpawnIssue> issues)
        {
            if (diagnostics == null)
                return;

            for (int i = 0; i < diagnostics.Length; i++)
            {
                CharacterDiagnostic diagnostic = diagnostics[i];
                if (diagnostic.Code == CharacterDiagnosticCode.None)
                    continue;

                issues.AddIssue(
                    ConvertSeverity(diagnostic.Severity),
                    diagnostic.StableCode,
                    diagnostic.SourceTable,
                    diagnostic.Field,
                    diagnostic.Message);
            }
        }

        private static CharacterRuntimeSpawnIssueSeverity ConvertSeverity(CharacterDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case CharacterDiagnosticSeverity.Error:
                    return CharacterRuntimeSpawnIssueSeverity.Error;
                case CharacterDiagnosticSeverity.Warning:
                    return CharacterRuntimeSpawnIssueSeverity.Warning;
                default:
                    return CharacterRuntimeSpawnIssueSeverity.Info;
            }
        }

        private static bool HasCharacterDiagnosticErrors(CharacterDiagnostic[] diagnostics)
        {
            if (diagnostics == null)
                return false;

            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Severity == CharacterDiagnosticSeverity.Error)
                    return true;
            }

            return false;
        }

        private static bool HasRuntimeErrors(List<CharacterRuntimeSpawnIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].IsError)
                    return true;
            }

            return false;
        }
    }
}
