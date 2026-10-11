using System;
using System.Linq;
using System.Text.RegularExpressions;
namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterTestLevelDiagnosticLocation
    {
        internal static int ItemIndex(string message, string collection)
        {
            var match = Regex.Match(message ?? string.Empty, Regex.Escape(collection) + @"\[(\d+)\]", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, out int index) ? index : -1;
        }
        internal static int ActorIndex(string message)
        {
            var match = Regex.Match(message ?? string.Empty, @"characters\[(\d+)\]");
            return match.Success && int.TryParse(match.Groups[1].Value, out int index) ? index : -1;
        }
        internal static string ActorId(string message, CharacterTestSceneCharacterAuthoringDocument document)
        {
            if (document?.Characters == null) return null;
            int index = ActorIndex(message);
            if (index >= 0 && index < document.Characters.Count) return document.Characters[index]?.StableId;
            return document.Characters.Where(c => !string.IsNullOrEmpty(c?.StableId))
                .OrderByDescending(c => c.StableId.Length)
                .FirstOrDefault(c => (message ?? string.Empty).Contains("/" + c.StableId + "/", StringComparison.Ordinal))?.StableId;
        }
    }
}
