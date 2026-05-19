using System;
using System.Collections.Generic;

namespace MxFramework.CharacterApplication
{
    public readonly struct ResourceDependencyResolveResult
    {
        public ResourceDependencyResolveResult(CharacterResourceDependencyReport report)
        {
            Report = report ?? CharacterResourceDependencyReport.Empty;
        }

        public CharacterResourceDependencyReport Report { get; }
        public CharacterResourceKeyEntry[] RequiredResources => Report.RequiredResources;
        public string[] PreloadGroupIds => Report.PreloadGroupIds;
        public CharacterDiagnostic[] Diagnostics => Report.Diagnostics;
    }

    public static class ResourceDependencyResolver
    {
        public static ResourceDependencyResolveResult Resolve(
            CharacterPresentationProfileConfig presentationProfile,
            IReadOnlyList<EquippedWeaponSlot> equippedWeapons,
            EquipmentStateConfig activeEquipmentState,
            CombatActionSetConfig actionSet)
        {
            var diagnostics = new CharacterDiagnosticBuilder();
            var resources = new List<CharacterResourceKeyEntry>();
            var resourceKeys = new HashSet<string>(StringComparer.Ordinal);
            var preloadGroups = new List<string>();
            var preloadGroupKeys = new HashSet<string>(StringComparer.Ordinal);

            if (presentationProfile == null)
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.MissingPresentationProfile,
                    CharacterPresentationProfileConfig.TableName,
                    0,
                    string.Empty,
                    nameof(presentationProfile),
                    "Presentation profile is required.");
            }
            else
            {
                AddResourceEntries(
                    presentationProfile.ResourceKeys,
                    CharacterPresentationProfileConfig.TableName,
                    presentationProfile.Id,
                    presentationProfile.StableId,
                    nameof(CharacterPresentationProfileConfig.ResourceKeys),
                    resources,
                    resourceKeys,
                    preloadGroups,
                    preloadGroupKeys,
                    diagnostics);

                AddSyntheticResource(
                    presentationProfile.DefaultAnimationProfileId,
                    CharacterResolverUtility.AnimationProfileResourceTypeId,
                    CharacterResourceUsageKind.AnimationProfile,
                    string.Empty,
                    CharacterPresentationProfileConfig.TableName,
                    presentationProfile.Id,
                    presentationProfile.StableId,
                    nameof(CharacterPresentationProfileConfig.DefaultAnimationProfileId),
                    resources,
                    resourceKeys,
                    preloadGroups,
                    preloadGroupKeys,
                    diagnostics);
            }

            if (activeEquipmentState != null)
            {
                AddSyntheticResource(
                    activeEquipmentState.AnimationProfileId,
                    CharacterResolverUtility.AnimationProfileResourceTypeId,
                    CharacterResourceUsageKind.AnimationProfile,
                    string.Empty,
                    EquipmentStateConfig.TableName,
                    activeEquipmentState.Id,
                    activeEquipmentState.StableId,
                    nameof(EquipmentStateConfig.AnimationProfileId),
                    resources,
                    resourceKeys,
                    preloadGroups,
                    preloadGroupKeys,
                    diagnostics);
            }

            if (equippedWeapons != null)
            {
                var emittedWeaponIds = new HashSet<int>();
                for (int i = 0; i < equippedWeapons.Count; i++)
                {
                    WeaponConfig weapon = equippedWeapons[i].Weapon;
                    if (weapon == null || !emittedWeaponIds.Add(weapon.Id))
                        continue;

                    AddResourceEntries(
                        weapon.ResourceKeys,
                        WeaponConfig.TableName,
                        weapon.Id,
                        weapon.StableId,
                        nameof(WeaponConfig.ResourceKeys),
                        resources,
                        resourceKeys,
                        preloadGroups,
                        preloadGroupKeys,
                        diagnostics);

                    if (weapon.TraceBindings == null)
                        continue;

                    for (int j = 0; j < weapon.TraceBindings.Length; j++)
                    {
                        CharacterTraceBindingEntry trace = weapon.TraceBindings[j];
                        AddSyntheticResource(
                            trace.TraceProfileId,
                            CharacterResolverUtility.TraceProfileResourceTypeId,
                            CharacterResourceUsageKind.Custom,
                            string.Empty,
                            WeaponConfig.TableName,
                            weapon.Id,
                            weapon.StableId,
                            "TraceBindings[" + j + "].TraceProfileId",
                            resources,
                            resourceKeys,
                            preloadGroups,
                            preloadGroupKeys,
                            diagnostics);
                    }
                }
            }

            if (actionSet != null && actionSet.Actions != null)
            {
                for (int i = 0; i < actionSet.Actions.Length; i++)
                {
                    CombatActionEntry action = actionSet.Actions[i];
                    if (string.IsNullOrEmpty(action.TraceProfileIdOverride))
                        continue;

                    AddSyntheticResource(
                        action.TraceProfileIdOverride,
                        CharacterResolverUtility.TraceProfileResourceTypeId,
                        CharacterResourceUsageKind.Custom,
                        string.Empty,
                        CombatActionSetConfig.TableName,
                        actionSet.Id,
                        actionSet.StableId,
                        "Actions[" + i + "].TraceProfileIdOverride",
                        resources,
                        resourceKeys,
                        preloadGroups,
                        preloadGroupKeys,
                        diagnostics);
                }
            }

            return new ResourceDependencyResolveResult(new CharacterResourceDependencyReport(resources.ToArray(), preloadGroups.ToArray(), diagnostics.ToArray()));
        }

        private static void AddResourceEntries(
            CharacterResourceKeyEntry[] entries,
            string sourceTable,
            int sourceId,
            string sourceStableId,
            string fieldPrefix,
            List<CharacterResourceKeyEntry> resources,
            HashSet<string> resourceKeys,
            List<string> preloadGroups,
            HashSet<string> preloadGroupKeys,
            CharacterDiagnosticBuilder diagnostics)
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                AddResourceEntry(
                    entries[i],
                    sourceTable,
                    sourceId,
                    sourceStableId,
                    fieldPrefix + "[" + i + "]",
                    resources,
                    resourceKeys,
                    preloadGroups,
                    preloadGroupKeys,
                    diagnostics);
            }
        }

        private static void AddSyntheticResource(
            string id,
            string typeId,
            CharacterResourceUsageKind usageKind,
            string preloadGroupId,
            string sourceTable,
            int sourceId,
            string sourceStableId,
            string field,
            List<CharacterResourceKeyEntry> resources,
            HashSet<string> resourceKeys,
            List<string> preloadGroups,
            HashSet<string> preloadGroupKeys,
            CharacterDiagnosticBuilder diagnostics)
        {
            var entry = new CharacterResourceKeyEntry(id, typeId, usageKind, preloadGroupId: preloadGroupId);
            AddResourceEntry(entry, sourceTable, sourceId, sourceStableId, field, resources, resourceKeys, preloadGroups, preloadGroupKeys, diagnostics);
        }

        private static void AddResourceEntry(
            CharacterResourceKeyEntry entry,
            string sourceTable,
            int sourceId,
            string sourceStableId,
            string field,
            List<CharacterResourceKeyEntry> resources,
            HashSet<string> resourceKeys,
            List<string> preloadGroups,
            HashSet<string> preloadGroupKeys,
            CharacterDiagnosticBuilder diagnostics)
        {
            if (string.IsNullOrEmpty(entry.Id) || string.IsNullOrEmpty(entry.TypeId))
            {
                diagnostics.Add(
                    CharacterDiagnosticSeverity.Error,
                    CharacterDiagnosticCode.MissingResourceKey,
                    sourceTable,
                    sourceId,
                    sourceStableId,
                    field,
                    "Resource dependency has an empty id or type id.");
                return;
            }

            string resourceKey = entry.Id + "|" + entry.TypeId + "|" + entry.Variant + "|" + entry.PackageId;
            if (resourceKeys.Add(resourceKey))
                resources.Add(entry);

            if (!string.IsNullOrEmpty(entry.PreloadGroupId) && preloadGroupKeys.Add(entry.PreloadGroupId))
                preloadGroups.Add(entry.PreloadGroupId);
        }
    }
}
