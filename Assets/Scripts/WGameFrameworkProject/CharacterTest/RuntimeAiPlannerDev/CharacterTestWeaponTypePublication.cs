using System;
using System.IO;
using System.Linq;
using MxFramework.CharacterApplication;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Read-only preflight; no generated file is touched until every explicit type binding validates.</summary>
    internal static class CharacterTestWeaponTypePublication
    {
        internal const string PlayerPath = "MxFramework/CharacterTest/weapon_type_catalog.json";
        internal const string GeneratedPath = "Assets/MxFrameworkGenerated/CharacterActions/weapon_type_catalog.json";

        internal static JObject Build(string root, CharacterTestActionBrowserData actions)
        {
            if (actions == null || actions.Issues.Length != 0) throw new ArgumentException("WeaponTypeActionCatalogInvalid");
            var bytes = File.ReadAllBytes(Path.Combine(root, CharacterTestActionBrowserCatalog.WeaponLibraryPath));
            var source = JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
            var definitions = source["weaponTypes"]?.ToObject<WeaponTypeDefinition[]>();
            var catalog = new WeaponTypeCatalog(definitions);
            foreach (var item in (source["singleWeaponConfigurations"] as JArray ?? new JArray()).OfType<JObject>())
            {
                string id = (string)item["singleWeaponConfigurationId"], type = (string)item["weaponTypeId"] ?? "";
                if (id == "weapon.unarmed")
                {
                    if (type.Length != 0) throw new ArgumentException("UnarmedWeaponTypeInvalid:" + id);
                    continue;
                }
                if (!catalog.ValidateWeapon(type, (string)item["category"], out string issue))
                    throw new ArgumentException(issue + ":configuration=" + id);
            }
            // The allocation catalog carries identity/export metadata; it cannot define another mapping.
            string allocationPath = Path.Combine(root, "Authoring/WeaponPackages/weapon_id_catalog.json");
            var allocation = JObject.Parse(File.ReadAllText(allocationPath));
            foreach (var item in (allocation["entries"] as JArray ?? new JArray()).OfType<JObject>())
            {
                if ((string)item["status"] != "active") continue;
                string type = (string)item["weaponTypeId"] ?? "";
                if ((bool?)item["isVirtual"] == true)
                {
                    if (type.Length != 0) throw new ArgumentException("VirtualWeaponTypeInvalid:" + (string)item["id"]);
                    continue;
                }
                if (!catalog.ValidateWeapon(type, (string)item["category"], out string issue))
                    throw new ArgumentException(issue + ":weaponId=" + (string)item["id"]);
                string configurationId = (string)item["singleWeaponConfigurationId"] ?? "weapon." + (string)item["id"] + ".main";
                var single = (source["singleWeaponConfigurations"] as JArray ?? new JArray()).OfType<JObject>()
                    .SingleOrDefault(s => (string)s["singleWeaponConfigurationId"] == configurationId);
                if (single == null || (string)single["weaponTypeId"] != type || (string)single["category"] != (string)item["category"])
                    throw new ArgumentException("WeaponIdentityTypeProjectionMismatch:" + (string)item["id"]);
            }
            foreach (string path in CharacterTestActionBrowserCatalog.SourceFiles(root))
            {
                var document = JObject.Parse(File.ReadAllText(path));
                foreach (var action in (document["actions"] as JArray ?? new JArray()).OfType<JObject>())
                    foreach (var requirement in (action["requirements"] as JArray ?? new JArray()).OfType<JObject>())
                    {
                        if ((string)requirement["kind"] == "WeaponTypeIndependent")
                        {
                            if (((string)requirement["stableId"] ?? "").Length != 0 || (int?)requirement["value"] != 0 || (int?)requirement["referenceId"] != 0)
                                throw new ArgumentException("WeaponTypeIndependentShapeInvalid:action=" + (int?)action["actionId"]);
                            if ((action["requirements"] as JArray ?? new JArray()).OfType<JObject>().Any(r => (string)r["kind"] == "WeaponTypeRequired"))
                                throw new ArgumentException("WeaponTypeRequirementConflict:action=" + (int?)action["actionId"]);
                            continue;
                        }
                        if ((string)requirement["kind"] != "WeaponTypeRequired") continue;
                        if ((int?)requirement["referenceId"] != 0
                            || !catalog.ValidateRequirement((string)requirement["stableId"], (int?)requirement["value"] ?? 0, out string issue))
                            throw new ArgumentException("WeaponTypeRequirementInvalid:action=" + (int?)action["actionId"]);
                    }
            }
            return new JObject {
                ["schema"] = "mx.weapon-types.v1",
                ["sourcePath"] = CharacterTestActionBrowserCatalog.WeaponLibraryPath,
                ["sourceHash"] = CharacterTestActionBrowserCatalog.Hash(bytes),
                ["actionCatalogFingerprint"] = actions.Fingerprint,
                ["weaponTypes"] = source["weaponTypes"].DeepClone(),
                ["unclassifiedBasicAttackPolicy"] = "reject",
                ["weaponIndependentActionIds"] = new JArray(actions.Actions.Where(a => a.WeaponTypeIndependent).Select(a => a.ActionId)),
                ["unreviewedBasicAttackIds"] = new JArray(actions.Actions.Where(a => a.Category == MxFramework.CharacterAction.CharacterActionCategory.BasicAttack
                    && a.RequiredWeaponTypes.Length == 0 && !a.WeaponTypeIndependent).Select(a => a.ActionId))
            };
        }
    }
}
