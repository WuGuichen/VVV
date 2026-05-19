using System;
using System.Collections.Generic;

namespace MxFramework.CharacterApplication
{
    public readonly struct CharacterPackageResolveRequest
    {
        public CharacterPackageResolveRequest(
            CharacterConfig character,
            CharacterAttributeProfileConfig attributeProfile,
            CharacterBodyProfileConfig bodyProfile,
            CharacterBodyPartConfig[] bodyParts,
            EquipmentSchemaConfig equipmentSchema,
            EquipmentLoadoutConfig loadout,
            EquipmentStateConfig[] equipmentStates,
            WeaponConfig[] weapons,
            AbilityLoadoutConfig[] abilityLoadouts,
            CombatActionSetConfig[] combatActionSets,
            CharacterPresentationProfileConfig presentationProfile,
            AbilityLoadoutConfig runtimeTemporaryGrantLoadout = null,
            CharacterAbilityId[] runtimeDisabledAbilityIds = null,
            CharacterAbilityId[] knownAbilityIds = null)
        {
            Character = character;
            AttributeProfile = attributeProfile;
            BodyProfile = bodyProfile;
            BodyParts = bodyParts ?? Array.Empty<CharacterBodyPartConfig>();
            EquipmentSchema = equipmentSchema;
            Loadout = loadout;
            EquipmentStates = equipmentStates ?? Array.Empty<EquipmentStateConfig>();
            Weapons = weapons ?? Array.Empty<WeaponConfig>();
            AbilityLoadouts = abilityLoadouts ?? Array.Empty<AbilityLoadoutConfig>();
            CombatActionSets = combatActionSets ?? Array.Empty<CombatActionSetConfig>();
            PresentationProfile = presentationProfile;
            RuntimeTemporaryGrantLoadout = runtimeTemporaryGrantLoadout;
            RuntimeDisabledAbilityIds = runtimeDisabledAbilityIds ?? Array.Empty<CharacterAbilityId>();
            KnownAbilityIds = knownAbilityIds ?? Array.Empty<CharacterAbilityId>();
        }

        public CharacterConfig Character { get; }
        public CharacterAttributeProfileConfig AttributeProfile { get; }
        public CharacterBodyProfileConfig BodyProfile { get; }
        public CharacterBodyPartConfig[] BodyParts { get; }
        public EquipmentSchemaConfig EquipmentSchema { get; }
        public EquipmentLoadoutConfig Loadout { get; }
        public EquipmentStateConfig[] EquipmentStates { get; }
        public WeaponConfig[] Weapons { get; }
        public AbilityLoadoutConfig[] AbilityLoadouts { get; }
        public CombatActionSetConfig[] CombatActionSets { get; }
        public CharacterPresentationProfileConfig PresentationProfile { get; }
        public AbilityLoadoutConfig RuntimeTemporaryGrantLoadout { get; }
        public CharacterAbilityId[] RuntimeDisabledAbilityIds { get; }
        public CharacterAbilityId[] KnownAbilityIds { get; }
    }

    public readonly struct CharacterPackageResolveResult
    {
        public CharacterPackageResolveResult(
            CharacterResolvedProfile resolvedProfile,
            CharacterValidationReport validationReport,
            CharacterResourceDependencyReport resourceDependencyReport)
        {
            ResolvedProfile = resolvedProfile;
            ValidationReport = validationReport ?? CharacterValidationReport.Empty;
            ResourceDependencyReport = resourceDependencyReport ?? CharacterResourceDependencyReport.Empty;
        }

        public CharacterResolvedProfile ResolvedProfile { get; }
        public CharacterValidationReport ValidationReport { get; }
        public CharacterResourceDependencyReport ResourceDependencyReport { get; }
    }

    public static class CharacterPackageResolver
    {
        public static CharacterPackageResolveResult Resolve(CharacterPackageResolveRequest request)
        {
            var diagnostics = new CharacterDiagnosticBuilder();
            ValidateRequiredConfigs(request, diagnostics);
            ValidateBodyPartSet(request.BodyProfile, request.BodyParts, diagnostics);

            EquipmentStateResolveResult equipmentResult = EquipmentStateResolver.Resolve(
                request.EquipmentSchema,
                request.Loadout,
                request.Weapons,
                request.EquipmentStates);
            diagnostics.AddRange(equipmentResult.Diagnostics);

            EquipmentStateConfig activeState = equipmentResult.ActiveState;
            CombatActionSetConfig actionSet = activeState == null
                ? null
                : FindCombatActionSet(request.CombatActionSets, activeState.CombatActionSetId);

            if (activeState != null && actionSet == null)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.MissingCombatActionSet,
                    EquipmentStateConfig.TableName,
                    activeState.Id,
                    activeState.StableId,
                    nameof(EquipmentStateConfig.CombatActionSetId),
                    "Active equipment state references a missing combat action set.");
            }

            AbilityGrantResolveResult abilityResult = AbilityGrantResolver.Resolve(new AbilityGrantResolveRequest(
                request.Character,
                equipmentResult.EquippedWeapons,
                activeState,
                request.AbilityLoadouts,
                request.RuntimeTemporaryGrantLoadout,
                request.RuntimeDisabledAbilityIds,
                request.KnownAbilityIds));
            diagnostics.AddRange(abilityResult.Diagnostics);

            CombatActionBindingResolveResult actionResult = CombatActionBindingResolver.Resolve(actionSet);
            diagnostics.AddRange(actionResult.Diagnostics);

            ResourceDependencyResolveResult resourceResult = ResourceDependencyResolver.Resolve(
                request.PresentationProfile,
                equipmentResult.EquippedWeapons,
                activeState,
                actionSet);
            diagnostics.AddRange(resourceResult.Diagnostics);

            CharacterDiagnostic[] issues = diagnostics.ToArray();
            var validationReport = new CharacterValidationReport(issues);
            CharacterResourceDependencyReport resourceReport = resourceResult.Report;
            CharacterDebugContext debugContext = CreateDebugContext(request, activeState, actionSet, issues);

            var resolvedProfile = new CharacterResolvedProfile(
                request.Character == null ? default : request.Character.CharacterId,
                request.BodyProfile == null ? default : request.BodyProfile.BodyProfileId,
                request.AttributeProfile == null ? default : request.AttributeProfile.AttributeProfileId,
                request.EquipmentSchema == null ? default : request.EquipmentSchema.EquipmentSchemaId,
                request.Loadout == null ? default : request.Loadout.LoadoutId,
                equipmentResult.ActiveStateId,
                abilityResult.AppliedLoadoutIds,
                abilityResult.EffectiveAbilityIds,
                abilityResult.EffectiveSlotBindings,
                abilityResult.GrantHandles,
                actionSet == null ? default : actionSet.ActionSetId,
                activeState != null && !string.IsNullOrEmpty(activeState.AnimationProfileId)
                    ? activeState.AnimationProfileId
                    : request.PresentationProfile == null ? string.Empty : request.PresentationProfile.DefaultAnimationProfileId,
                resourceReport.RequiredResources,
                validationReport,
                resourceReport,
                debugContext);

            return new CharacterPackageResolveResult(resolvedProfile, validationReport, resourceReport);
        }

        private static void ValidateRequiredConfigs(CharacterPackageResolveRequest request, CharacterDiagnosticBuilder diagnostics)
        {
            if (request.Character == null)
            {
                diagnostics.Add(CharacterDiagnosticSeverity.Error, CharacterDiagnosticCode.MissingCharacterConfig, CharacterConfig.TableName, 0, string.Empty, nameof(request.Character), "Character config is required.");
                return;
            }

            if (request.AttributeProfile == null || !request.AttributeProfile.AttributeProfileId.Equals(request.Character.AttributeProfileId))
            {
                diagnostics.Add(CharacterDiagnosticSeverity.Error, CharacterDiagnosticCode.MissingAttributeProfile, CharacterConfig.TableName, request.Character.Id, request.Character.StableId, nameof(CharacterConfig.AttributeProfileId), "Character attribute profile is missing or does not match CharacterConfig.");
            }

            if (request.BodyProfile == null || !request.BodyProfile.BodyProfileId.Equals(request.Character.BodyProfileId))
            {
                diagnostics.Add(CharacterDiagnosticSeverity.Error, CharacterDiagnosticCode.MissingBodyProfile, CharacterConfig.TableName, request.Character.Id, request.Character.StableId, nameof(CharacterConfig.BodyProfileId), "Character body profile is missing or does not match CharacterConfig.");
            }

            if (request.EquipmentSchema == null || !request.EquipmentSchema.EquipmentSchemaId.Equals(request.Character.EquipmentSchemaId))
            {
                diagnostics.Add(CharacterDiagnosticSeverity.Error, CharacterDiagnosticCode.MissingEquipmentSchema, CharacterConfig.TableName, request.Character.Id, request.Character.StableId, nameof(CharacterConfig.EquipmentSchemaId), "Character equipment schema is missing or does not match CharacterConfig.");
            }

            if (request.Loadout == null)
            {
                diagnostics.Add(CharacterDiagnosticSeverity.Error, CharacterDiagnosticCode.MissingDefaultLoadout, CharacterConfig.TableName, request.Character.Id, request.Character.StableId, nameof(CharacterConfig.DefaultLoadoutId), "Character loadout is missing.");
            }
            else if (!request.Loadout.LoadoutId.Equals(request.Character.DefaultLoadoutId) && request.Loadout.EquipmentSchemaId.Equals(request.Character.EquipmentSchemaId))
            {
                diagnostics.Add(CharacterDiagnosticSeverity.Info, CharacterDiagnosticCode.None, EquipmentLoadoutConfig.TableName, request.Loadout.Id, request.Loadout.StableId, nameof(EquipmentLoadoutConfig.LoadoutId), "Resolver is using a non-default loadout override.");
            }

            if (request.PresentationProfile == null || !request.PresentationProfile.PresentationProfileId.Equals(request.Character.PresentationProfileId))
            {
                diagnostics.Add(CharacterDiagnosticSeverity.Error, CharacterDiagnosticCode.MissingPresentationProfile, CharacterConfig.TableName, request.Character.Id, request.Character.StableId, nameof(CharacterConfig.PresentationProfileId), "Character presentation profile is missing or does not match CharacterConfig.");
            }
        }

        private static void ValidateBodyPartSet(
            CharacterBodyProfileConfig bodyProfile,
            CharacterBodyPartConfig[] bodyParts,
            CharacterDiagnosticBuilder diagnostics)
        {
            if (bodyProfile == null)
                return;

            var partIds = new HashSet<string>(StringComparer.Ordinal);
            var duplicateIds = new HashSet<string>(StringComparer.Ordinal);
            int partCount = 0;
            for (int i = 0; i < bodyParts.Length; i++)
            {
                CharacterBodyPartConfig part = bodyParts[i];
                if (part == null || !CharacterResolverUtility.EqualsOrdinal(part.PartSetId, bodyProfile.PartSetId))
                    continue;

                partCount++;
                if (!partIds.Add(part.PartId))
                    duplicateIds.Add(part.PartId);
            }

            if (partCount == 0)
            {
                diagnostics.Add(CharacterDiagnosticSeverity.Error, CharacterDiagnosticCode.InvalidBodyPartSet, CharacterBodyProfileConfig.TableName, bodyProfile.Id, bodyProfile.StableId, nameof(CharacterBodyProfileConfig.PartSetId), "Body profile part set has no body parts.");
                return;
            }

            if (duplicateIds.Count > 0)
            {
                diagnostics.Add(CharacterDiagnosticSeverity.Error, CharacterDiagnosticCode.InvalidBodyPartSet, CharacterBodyProfileConfig.TableName, bodyProfile.Id, bodyProfile.StableId, nameof(CharacterBodyPartConfig.PartId), "Body profile part set contains duplicate part ids.");
            }

            for (int i = 0; i < bodyParts.Length; i++)
            {
                CharacterBodyPartConfig part = bodyParts[i];
                if (part == null || !CharacterResolverUtility.EqualsOrdinal(part.PartSetId, bodyProfile.PartSetId))
                    continue;

                if (string.IsNullOrEmpty(part.ParentPartId))
                    continue;

                if (!partIds.Contains(part.ParentPartId))
                {
                    diagnostics.Add(
                        CharacterDiagnosticSeverity.Error,
                        CharacterDiagnosticCode.BodyPartParentMissing,
                        CharacterBodyPartConfig.TableName,
                        part.Id,
                        part.StableId,
                        nameof(CharacterBodyPartConfig.ParentPartId),
                        "Body part parent is missing from the same part set.");
                }
            }
        }

        private static CombatActionSetConfig FindCombatActionSet(CombatActionSetConfig[] actionSets, CombatActionSetId id)
        {
            if (actionSets == null || !id.IsValid)
                return null;

            for (int i = 0; i < actionSets.Length; i++)
            {
                CombatActionSetConfig actionSet = actionSets[i];
                if (actionSet != null && actionSet.ActionSetId.Equals(id))
                    return actionSet;
            }

            return null;
        }

        private static CharacterDebugContext CreateDebugContext(
            CharacterPackageResolveRequest request,
            EquipmentStateConfig activeState,
            CombatActionSetConfig actionSet,
            CharacterDiagnostic[] diagnostics)
        {
            return new CharacterDebugContext(
                request.Character == null ? string.Empty : request.Character.StableId,
                request.Loadout == null ? string.Empty : request.Loadout.StableId,
                activeState == null ? string.Empty : activeState.StableId,
                actionSet == null ? string.Empty : actionSet.StableId,
                activeState != null && !string.IsNullOrEmpty(activeState.AnimationProfileId)
                    ? activeState.AnimationProfileId
                    : request.PresentationProfile == null ? string.Empty : request.PresentationProfile.DefaultAnimationProfileId,
                CharacterResolverUtility.DiagnosticSummary(diagnostics));
        }
    }
}
