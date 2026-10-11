#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry
    {
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry(
            string workspaceId,
            string sourceMainWorkspaceId = null,
            int sourceMainRevision = 0,
            DateTime? copiedUtc = null,
            string previousWorkspaceId = null)
        {
            WorkspaceId = NormalizeGuid(workspaceId, required: true);
            SourceMainWorkspaceId = NormalizeGuid(sourceMainWorkspaceId, required: false);
            SourceMainRevision = Math.Max(0, sourceMainRevision);
            CopiedUtc = copiedUtc;
            PreviousWorkspaceId = NormalizeGuid(previousWorkspaceId, required: false);
        }

        internal string WorkspaceId { get; }
        internal string SourceMainWorkspaceId { get; }
        internal int SourceMainRevision { get; }
        internal DateTime? CopiedUtc { get; }
        internal string PreviousWorkspaceId { get; }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry Clone()
        {
            return new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry(
                WorkspaceId,
                SourceMainWorkspaceId,
                SourceMainRevision,
                CopiedUtc,
                PreviousWorkspaceId);
        }

        private static string NormalizeGuid(string value, bool required)
        {
            if (Guid.TryParse(value, out Guid parsed))
                return parsed.ToString("D", CultureInfo.InvariantCulture);
            if (!required && string.IsNullOrWhiteSpace(value))
                return string.Empty;
            throw new ArgumentException("Workspace role identity must be a GUID.", nameof(value));
        }
    }

    internal sealed class CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState
    {
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry Main { get; set; }
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry ExperimentA { get; set; }
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry ExperimentB { get; set; }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry Get(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                ? Main
                : role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA
                    ? ExperimentA
                    : role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB
                        ? ExperimentB
                        : null;
        }

        internal void Set(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry entry)
        {
            if (role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main) Main = entry;
            else if (role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA) ExperimentA = entry;
            else if (role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB) ExperimentB = entry;
            else throw new ArgumentOutOfRangeException(nameof(role));
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState Clone()
        {
            return new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState
            {
                Main = Main?.Clone(),
                ExperimentA = ExperimentA?.Clone(),
                ExperimentB = ExperimentB?.Clone()
            };
        }

        internal IReadOnlyCollection<string> WorkspaceIds()
        {
            var ids = new List<string>(3);
            if (Main != null) ids.Add(Main.WorkspaceId);
            if (ExperimentA != null) ids.Add(ExperimentA.WorkspaceId);
            if (ExperimentB != null) ids.Add(ExperimentB.WorkspaceId);
            return ids;
        }

        internal bool TryValidateIdentities(out string message)
        {
            if (Main == null)
            {
                message = "The role registry has no Main mapping.";
                return false;
            }
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!ids.Add(Main.WorkspaceId)
                || (ExperimentA != null && !ids.Add(ExperimentA.WorkspaceId))
                || (ExperimentB != null && !ids.Add(ExperimentB.WorkspaceId)))
            {
                message = "Main, ExperimentA, and ExperimentB must use distinct workspace IDs.";
                return false;
            }
            if ((ExperimentA != null && string.Equals(
                    ExperimentA.WorkspaceId,
                    ExperimentA.SourceMainWorkspaceId,
                    StringComparison.OrdinalIgnoreCase))
                || (ExperimentB != null && string.Equals(
                    ExperimentB.WorkspaceId,
                    ExperimentB.SourceMainWorkspaceId,
                    StringComparison.OrdinalIgnoreCase)))
            {
                message = "An experiment workspace cannot be its own Main source.";
                return false;
            }
            if (Main.PreviousWorkspaceId.Length > 0
                && string.Equals(
                    Main.WorkspaceId,
                    Main.PreviousWorkspaceId,
                    StringComparison.OrdinalIgnoreCase))
            {
                message = "Main cannot reference itself as its previous workspace.";
                return false;
            }
            message = string.Empty;
            return true;
        }
    }

    internal enum CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryLoadStatus
    {
        Missing = 0,
        Loaded = 1,
        Invalid = 2
    }

    internal sealed class CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistry
    {
        private const string Schema = "mx.character-test.runtime-ai-lab.authoring-workspace-roles.v1";
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private readonly Func<string, bool> _shouldFailWrite;

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistry(
            string registryPath,
            string activeRolePath,
            Func<string, bool> shouldFailWrite = null)
        {
            RegistryPath = Path.GetFullPath(registryPath);
            ActiveRolePath = Path.GetFullPath(activeRolePath);
            _shouldFailWrite = shouldFailWrite;
        }

        internal string RegistryPath { get; }
        internal string ActiveRolePath { get; }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryLoadStatus Load(
            out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState state,
            out string message)
        {
            state = null;
            if (!File.Exists(RegistryPath))
            {
                message = "The authoring workspace role registry does not exist.";
                return CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryLoadStatus.Missing;
            }

            try
            {
                RegistryDto dto = JsonConvert.DeserializeObject<RegistryDto>(File.ReadAllText(RegistryPath));
                if (dto == null || !string.Equals(dto.Schema, Schema, StringComparison.Ordinal))
                    throw new InvalidDataException("The role registry schema is unsupported.");
                state = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState
                {
                    Main = Parse(dto.Main, experiment: false),
                    ExperimentA = Parse(dto.ExperimentA, experiment: true),
                    ExperimentB = Parse(dto.ExperimentB, experiment: true)
                };
                if (!state.TryValidateIdentities(out string validationMessage))
                    throw new InvalidDataException(validationMessage);
                message = "Authoring workspace role registry loaded.";
                return CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryLoadStatus.Loaded;
            }
            catch (Exception exception)
            {
                state = null;
                message = "The authoring workspace role registry is invalid: " + exception.Message;
                return CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryLoadStatus.Invalid;
            }
        }

        internal bool Save(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState state,
            out string message)
        {
            if (state == null)
            {
                message = "The role registry state is unavailable.";
                return false;
            }
            if (!state.TryValidateIdentities(out message))
            {
                return false;
            }

            try
            {
                ValidateExperiment(state.ExperimentA);
                ValidateExperiment(state.ExperimentB);
                var dto = new RegistryDto
                {
                    Schema = Schema,
                    Main = Write(state.Main),
                    ExperimentA = Write(state.ExperimentA),
                    ExperimentB = Write(state.ExperimentB)
                };
                return AtomicWrite(
                    RegistryPath,
                    JsonConvert.SerializeObject(dto, Formatting.Indented),
                    out message);
            }
            catch (Exception exception)
            {
                message = "The authoring workspace role registry could not be saved: " + exception.Message;
                return false;
            }
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRole ReadActiveRole(out bool rewrite)
        {
            rewrite = false;
            try
            {
                if (File.Exists(ActiveRolePath))
                {
                    string value = File.ReadAllText(ActiveRolePath).Trim();
                    if (Enum.TryParse(value, out CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
                        && CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsValid(role))
                    {
                        return role;
                    }
                }
            }
            catch
            {
                // Machine-local selection is disposable; a valid Main remains the safe fallback.
            }
            rewrite = true;
            return CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main;
        }

        internal bool WriteActiveRole(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            out string message)
        {
            if (!CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsValid(role))
            {
                message = "The requested authoring workspace role is invalid.";
                return false;
            }
            return AtomicWrite(ActiveRolePath, role.ToString(), out message);
        }

        private static CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry Parse(
            EntryDto dto,
            bool experiment)
        {
            if (dto == null)
                return null;
            DateTime? copiedUtc = string.IsNullOrWhiteSpace(dto.CopiedUtc)
                ? (DateTime?)null
                : DateTime.Parse(dto.CopiedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var entry = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry(
                dto.WorkspaceId,
                dto.SourceMainWorkspaceId,
                dto.SourceMainRevision,
                copiedUtc,
                dto.PreviousWorkspaceId);
            if (experiment)
                ValidateExperiment(entry);
            return entry;
        }

        private static EntryDto Write(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry entry)
        {
            return entry == null
                ? null
                : new EntryDto
                {
                    WorkspaceId = entry.WorkspaceId,
                    SourceMainWorkspaceId = EmptyToNull(entry.SourceMainWorkspaceId),
                    SourceMainRevision = entry.SourceMainRevision,
                    CopiedUtc = entry.CopiedUtc?.ToString("O", CultureInfo.InvariantCulture),
                    PreviousWorkspaceId = EmptyToNull(entry.PreviousWorkspaceId)
                };
        }

        private static void ValidateExperiment(CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry entry)
        {
            if (entry == null)
                return;
            if (entry.SourceMainWorkspaceId.Length == 0
                || entry.SourceMainRevision <= 0
                || !entry.CopiedUtc.HasValue)
            {
                throw new InvalidDataException("An experiment mapping requires a Main revision checkpoint.");
            }
        }

        private bool AtomicWrite(string path, string content, out string message)
        {
            if (_shouldFailWrite?.Invoke(Path.GetFullPath(path)) == true)
            {
                message = "Injected authoring workspace role state write failure.";
                return false;
            }
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temporary, content ?? string.Empty, Utf8);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                message = "Authoring workspace role state saved.";
                return true;
            }
            catch (Exception exception)
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { }
                message = "Authoring workspace role state save failed: " + exception.Message;
                return false;
            }
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        [Serializable]
        private sealed class RegistryDto
        {
            [JsonProperty("schema", Order = 0)] public string Schema;
            [JsonProperty("main", Order = 1)] public EntryDto Main;
            [JsonProperty("experimentA", Order = 2)] public EntryDto ExperimentA;
            [JsonProperty("experimentB", Order = 3)] public EntryDto ExperimentB;
        }

        [Serializable]
        private sealed class EntryDto
        {
            [JsonProperty("workspaceId", Order = 0)] public string WorkspaceId;
            [JsonProperty("sourceMainWorkspaceId", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
            public string SourceMainWorkspaceId;
            [JsonProperty("sourceMainRevision", Order = 2, DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int SourceMainRevision;
            [JsonProperty("copiedUtc", Order = 3, NullValueHandling = NullValueHandling.Ignore)] public string CopiedUtc;
            [JsonProperty("previousWorkspaceId", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
            public string PreviousWorkspaceId;
        }
    }
}
#endif
