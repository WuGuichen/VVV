using System;
using System.Collections.Generic;
using System.Linq;
namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterTestLevelBatchEdits
    {
        internal static void Apply(CharacterTestSceneCharacterAuthoringDocument document, IEnumerable<string> ids,
            int? teamId, bool setGroup, string group)
        {
            if (document?.Characters == null) throw new ArgumentException("角色文档不可用。");
            var keys = ids?.Distinct(StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
            if (keys.Length == 0) throw new ArgumentException("请先选择角色。");
            var actors = keys.Select(id => {
                var matches = document.Characters.Where(c => c != null && c.StableId == id).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("角色身份缺失或重复：" + id);
                return matches[0];
            }).ToArray();
            // Validate all identities before changing the first actor.
            foreach (var actor in actors)
            {
                if (teamId.HasValue) actor.TeamId = teamId.Value;
                if (setGroup) actor.Group = group ?? string.Empty;
            }
        }
    }
}
