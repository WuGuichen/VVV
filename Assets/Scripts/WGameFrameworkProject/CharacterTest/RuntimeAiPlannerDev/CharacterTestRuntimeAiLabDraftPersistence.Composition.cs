using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabDraftPersistence
    {
        private static JArray WriteTacticalDefinitions(string stagingDirectory, CharacterTestRuntimeAiLabDraftWorkspace workspace)
        {
            var references = new JArray();
            if (!workspace.HasCompositionData) return references;
            Directory.CreateDirectory(Path.Combine(stagingDirectory, "tactical-shared"));
            foreach (var definition in workspace.CloneTacticalDefinitions())
            {
                string json = CharacterTestTacticalCompositionJson.WriteShared(definition);
                string relative = "tactical-shared/" + Hash(definition.DefinitionId) + ".json";
                File.WriteAllText(Path.Combine(stagingDirectory, relative.Replace('/', Path.DirectorySeparatorChar)), json, System.Text.Encoding.UTF8);
                references.Add(WriteReference(definition.DefinitionId, definition.Revision, relative, Hash(json)));
            }
            return references;
        }

        private static void ReadTacticalDefinitions(string directory, JToken source, int workspaceVersion, CharacterTestRuntimeAiLabDraftWorkspace workspace)
        {
            if (source == null && workspaceVersion != 4) return;
            if (!(source is JArray references)) throw new InvalidDataException("Shared tactical references must be an array.");
            foreach (JObject reference in references)
            {
                string id = (string)reference["id"];
                if (reference["id"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace(id) || id.Length > 128
                    || !string.Equals((string)reference["path"], "tactical-shared/" + Hash(id) + ".json", StringComparison.Ordinal))
                    throw new InvalidDataException("Shared tactical path must match its encoded identity.");
                var definition = CharacterTestTacticalCompositionJson.ReadShared(ReadAndValidateReference(directory, reference));
                if (definition.DefinitionId != (string)reference["id"] || definition.Revision != (int?)reference["revision"])
                    throw new InvalidDataException("Shared tactical identity/revision mismatch.");
                workspace.AddLoadedTacticalDefinition(definition);
            }
        }
    }
}
