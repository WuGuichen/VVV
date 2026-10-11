using System;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Reads the formal type projection and the entity's exact current equipment state.</summary>
    internal static class CharacterTestWeaponTypeAdmission
    {
        internal const string PlayerRelativePath = "MxFramework/CharacterTest/weapon_type_catalog.json";
        private static WeaponTypeCatalog _catalog;
        private static long _stamp;
        private static string _path;
        internal static void Invalidate() { _catalog = null; _stamp = 0; _path = null; }
        internal static bool TryLoad(out WeaponTypeCatalog catalog, out string issue)
        {
            catalog = _catalog; issue = string.Empty;
            if (catalog != null) return true;
            string path = Path.Combine(Application.streamingAssetsPath, PlayerRelativePath);
            try
            {
                if (!File.Exists(path)) { issue = "WeaponTypeProjectionMissing"; return false; }
                long stamp = File.GetLastWriteTimeUtc(path).Ticks;
                if (_catalog == null || _stamp != stamp || _path != path)
                {
                    var json = JObject.Parse(File.ReadAllText(path));
                    if ((string)json["schema"] != "mx.weapon-types.v1") { issue = "WeaponTypeProjectionSchemaInvalid"; return false; }
                    var definitions = json["weaponTypes"]?.ToObject<WeaponTypeDefinition[]>();
                    var validated = new WeaponTypeCatalog(definitions);
                    _catalog = validated; _stamp = stamp; _path = path;
                }
                catalog = _catalog; return true;
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is Newtonsoft.Json.JsonException)
            { issue = "WeaponTypeProjectionInvalid:" + error.Message; return false; }
        }

        internal static bool Evaluate(CharacterTestCharacterRuntimeModule runtime, CharacterTestCharacterEntity entity,
            CharacterActionConfig action, out int requirementIndex, out string issue)
        {
            requirementIndex = -1; issue = string.Empty;
            if (action == null) { issue = "WeaponTypeActionMissing"; return false; }
            bool requiresType = false, independent = false;
            for (int i = 0; i < action.Requirements.Length; i++)
            {
                var requirement = action.Requirements[i];
                if (requirement?.Kind == CharacterActionRequirementKind.WeaponTypeRequired) requiresType = true;
                if (requirement?.Kind != CharacterActionRequirementKind.WeaponTypeIndependent) continue;
                if (!requirement.HasValidReferenceShape())
                { requirementIndex = i; issue = "WeaponTypeIndependentShapeInvalid"; return false; }
                independent = true;
            }
            if (requiresType && independent) { issue = "WeaponTypeRequirementConflict"; return false; }
            if (action.Category == CharacterActionCategory.BasicAttack && !requiresType && !independent)
            { issue = "WeaponTypeClassificationPending:action=" + action.ActionId.Value; return false; }
            for (int i = 0; i < action.Requirements.Length; i++)
            {
                var r = action.Requirements[i];
                if (r?.Kind != CharacterActionRequirementKind.WeaponTypeRequired) continue;
                requirementIndex = i;
                if (!r.HasValidReferenceShape()) { issue = "RequiredWeaponTypeShapeInvalid"; return false; }
                // The live trace/VFX owner only binds equipment while this source is enabled.
                if (entity != null && !entity.Equipment.UseDefaultLoadout)
                { issue = "WeaponTypeEquipmentSourceUnavailable"; return false; }
                if (!TryLoad(out var types, out issue)) return false;
                if (!types.ValidateRequirement(r.StableId, r.Value, out issue)) return false;
                if (entity == null || runtime == null)
                { issue = "WeaponTypeCharacterPackageMissing"; return false; }
                MxFramework.CharacterRuntimeSpawn.CharacterImportedPackage package;
                try
                {
                    package = string.IsNullOrWhiteSpace(entity.Identity.GeneratedPackageRoot)
                        ? CharacterTestGeneratedPackageSource.LoadDefaultCharacterPackage()
                        : CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity);
                }
                catch (Exception error) when (error is IOException || error is ArgumentException || error is Newtonsoft.Json.JsonException)
                { issue = "WeaponTypeCharacterPackageInvalid:" + error.Message; return false; }
                var configs = package?.Configs;
                if (configs == null) { issue = "WeaponTypeCharacterPackageMissing"; return false; }
                MxFramework.CharacterApplication.EquipmentLoadoutConfig loadout = null;
                string stateId = entity.Equipment.EquipmentStateStableId;
                for (int s = 0; s < configs.EquipmentStates.Length; s++)
                {
                    var state = configs.EquipmentStates[s];
                    if (state == null || state.StableId != stateId) continue;
                    for (int j = 0; j < configs.Loadouts.Length; j++)
                        if (configs.Loadouts[j] != null && configs.Loadouts[j].EquipmentStateId.Equals(state.StateId))
                        {
                            if (loadout != null) { issue = "WeaponTypeLoadoutAmbiguous"; return false; }
                            loadout = configs.Loadouts[j];
                        }
                }
                if (loadout == null && entity.Equipment.UseDefaultLoadout && string.IsNullOrEmpty(stateId)
                    && configs.Characters.Length > 0)
                    loadout = configs.FindLoadout(configs.Characters[0].DefaultLoadoutId);
                if (loadout == null) { issue = "WeaponTypeEquipmentStateMissing"; return false; }
                WeaponConfig equipped = null;
                for (int s = 0; s < loadout.Slots.Length; s++)
                    if (loadout.Slots[s].SlotId == r.Value)
                        for (int j = 0; j < configs.Weapons.Length; j++)
                            if (configs.Weapons[j] != null && configs.Weapons[j].WeaponId.Equals(loadout.Slots[s].WeaponId)) equipped = configs.Weapons[j];
                if (equipped == null) { issue = "RequiredWeaponSlotEmpty:" + r.Value; return false; }
                if (!types.Matches(r.StableId, r.Value, equipped.WeaponTypeId, equipped.Category.ToString(), out issue)) return false;
            }
            requirementIndex = -1; return true;
        }
    }
}
