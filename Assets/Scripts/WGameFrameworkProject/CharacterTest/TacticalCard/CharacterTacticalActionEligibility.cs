using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed class CharacterTacticalActionClassRule
    {
        public string CharacterStableId;
        public string[] WeaponTypes = Array.Empty<string>();
        public string[] ActionTypes = Array.Empty<string>();
    }

    internal sealed class CharacterTacticalActionEligibility
    {
        internal const string RelativePath = "MxFramework/CharacterTest/tactical_action_eligibility.json";
        public string Schema = "mx.character-test.tactical-action-eligibility.v1";
        public CharacterTacticalActionClassRule[] Characters = Array.Empty<CharacterTacticalActionClassRule>();
        private static CharacterTacticalActionEligibility _cached;
        private static long _timestamp;

        internal static CharacterTacticalActionEligibility Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, RelativePath);
            long stamp = File.GetLastWriteTimeUtc(path).Ticks;
            if (_cached == null || stamp != _timestamp)
            {
                var value = JsonConvert.DeserializeObject<CharacterTacticalActionEligibility>(File.ReadAllText(path));
                if (value?.Schema != "mx.character-test.tactical-action-eligibility.v1" || value.Characters == null
                    || value.Characters.Any(r => r == null || string.IsNullOrWhiteSpace(r.CharacterStableId) || r.WeaponTypes == null || r.ActionTypes == null)
                    || value.Characters.Select(r => r.CharacterStableId).Distinct(StringComparer.Ordinal).Count() != value.Characters.Length)
                    throw new JsonSerializationException("角色动作分类范围配置无效。");
                _cached = value; _timestamp = stamp;
            }
            return _cached;
        }

        internal bool Allows(string character, CharacterTestActionBrowserEntry action)
        {
            var rule = Characters.FirstOrDefault(r => r.CharacterStableId == character);
            if (rule == null || action == null) return false;
            return (rule.ActionTypes.Contains("*") || rule.ActionTypes.Contains(action.Category.ToString()))
                && (rule.WeaponTypes.Contains("*") || (action.WeaponTypes.Length == 0
                    ? rule.WeaponTypes.Contains("unclassified") : action.WeaponTypes.Any(rule.WeaponTypes.Contains)));
        }

        internal void Project(CharacterTacticalContentCatalog catalog, CharacterTestActionBrowserData browser)
        {
            if (browser.Issues.Length > 0) throw new InvalidOperationException("动作目录无效：" + browser.Issues[0]);
            var validWeapons = browser.WeaponTypes.Select(w => w.Id).Concat(new[] { "*", "unclassified" }).ToArray();
            var validTypes = Enum.GetNames(typeof(MxFramework.CharacterAction.CharacterActionCategory)).Concat(new[] { "*" }).ToArray();
            if (Characters.Any(r => r.WeaponTypes.Any(w => !validWeapons.Contains(w)) || r.ActionTypes.Any(t => !validTypes.Contains(t))))
                throw new JsonSerializationException("角色动作范围引用了未知分类。");
            var actions = catalog.Actions.ToList();
            foreach (var entry in browser.Actions)
            {
                string id = CharacterTestRuntimeAiPlannerDevSceneController.AuthoringActionKey(entry.ActionId);
                if (actions.All(a => a.Id != id)) actions.Add(new CharacterTacticalActionDefinition {
                    Id = id, ActionId = entry.ActionId, ActionLibraryId = entry.LibraryId,
                    DisplayName = entry.Name, Source = entry.SourcePath });
            }
            foreach (var action in actions)
            {
                var entry = browser.Actions.FirstOrDefault(a => a.ActionId == action.ActionId);
                action.CharacterStableIds = Characters.Where(r => Allows(r.CharacterStableId, entry)).Select(r => r.CharacterStableId).ToArray();
            }
            catalog.Actions = actions.ToArray();
            // Acquisition candidates are a projection of classification rules, never player ownership.
            var pools = catalog.Acquisition.ToList();
            foreach (var rule in Characters)
            {
                var pool = pools.FirstOrDefault(p => p.CharacterStableId == rule.CharacterStableId);
                if (pool == null) { pool = new CharacterTacticalAcquisitionPool { CharacterStableId = rule.CharacterStableId }; pools.Add(pool); }
                pool.ActionIds = browser.Actions.Where(a => Allows(rule.CharacterStableId, a))
                    .Select(a => CharacterTestRuntimeAiPlannerDevSceneController.AuthoringActionKey(a.ActionId)).ToArray();
            }
            catalog.Acquisition = pools.ToArray();
        }
    }
}
