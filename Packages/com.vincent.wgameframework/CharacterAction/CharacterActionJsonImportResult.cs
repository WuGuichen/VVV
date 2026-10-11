using System;

namespace MxFramework.CharacterAction
{
    public readonly struct CharacterActionJsonDiagnostic : IEquatable<CharacterActionJsonDiagnostic>
    {
        public CharacterActionJsonDiagnostic(
            string sourcePath,
            string jsonPath,
            string code,
            CharacterActionDiagnosticSeverity severity,
            string message)
        {
            SourcePath = sourcePath ?? string.Empty;
            JsonPath = string.IsNullOrWhiteSpace(jsonPath) ? "$" : jsonPath;
            Code = code ?? string.Empty;
            Severity = severity;
            Message = message ?? string.Empty;
        }

        public string SourcePath { get; }
        public string JsonPath { get; }
        public string Code { get; }
        public CharacterActionDiagnosticSeverity Severity { get; }
        public string Message { get; }

        public CharacterActionDiagnostic ToDiagnostic()
        {
            return new CharacterActionDiagnostic(
                CharacterActionDiagnosticCodeText.TryParse(Code, out CharacterActionDiagnosticCode code)
                    ? code
                    : CharacterActionDiagnosticCode.None,
                Severity,
                Message);
        }

        public override string ToString()
        {
            return Severity + " " + Code + " " + SourcePath + " " + JsonPath + " - " + Message;
        }

        public bool Equals(CharacterActionJsonDiagnostic other)
        {
            return string.Equals(SourcePath, other.SourcePath, StringComparison.Ordinal)
                && string.Equals(JsonPath, other.JsonPath, StringComparison.Ordinal)
                && string.Equals(Code, other.Code, StringComparison.Ordinal)
                && Severity == other.Severity
                && string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionJsonDiagnostic other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourcePath != null ? SourcePath.GetHashCode() : 0;
                hash = (hash * 397) ^ (JsonPath != null ? JsonPath.GetHashCode() : 0);
                hash = (hash * 397) ^ (Code != null ? Code.GetHashCode() : 0);
                hash = (hash * 397) ^ (int)Severity;
                hash = (hash * 397) ^ (Message != null ? Message.GetHashCode() : 0);
                return hash;
            }
        }
    }

    public sealed class CharacterActionJsonImportResult
    {
        public CharacterActionJsonImportResult(
            CharacterActionSetConfig[] actionSets,
            CharacterActionConfig[] actions,
            CharacterActionJsonDiagnostic[] diagnostics)
            : this(
                actionSets,
                actions,
                Array.Empty<CharacterReactionProfile>(),
                Array.Empty<CharacterLandingProfile>(),
                diagnostics)
        {
        }

        public CharacterActionJsonImportResult(
            CharacterActionSetConfig[] actionSets,
            CharacterActionConfig[] actions,
            CharacterReactionProfile[] reactionProfiles,
            CharacterActionJsonDiagnostic[] diagnostics)
            : this(actionSets, actions, reactionProfiles, Array.Empty<CharacterLandingProfile>(), diagnostics)
        {
        }

        public CharacterActionJsonImportResult(
            CharacterActionSetConfig[] actionSets,
            CharacterActionConfig[] actions,
            CharacterReactionProfile[] reactionProfiles,
            CharacterLandingProfile[] landingProfiles,
            CharacterActionJsonDiagnostic[] diagnostics)
        {
            ActionSets = actionSets ?? Array.Empty<CharacterActionSetConfig>();
            Actions = actions ?? Array.Empty<CharacterActionConfig>();
            ReactionProfiles = reactionProfiles ?? Array.Empty<CharacterReactionProfile>();
            LandingProfiles = landingProfiles ?? Array.Empty<CharacterLandingProfile>();
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionJsonDiagnostic>();
        }

        public CharacterActionSetConfig[] ActionSets { get; }
        public CharacterActionConfig[] Actions { get; }
        public CharacterReactionProfile[] ReactionProfiles { get; }
        public CharacterLandingProfile[] LandingProfiles { get; }
        public CharacterActionJsonDiagnostic[] Diagnostics { get; }

        public bool Success
        {
            get
            {
                for (int i = 0; i < Diagnostics.Length; i++)
                {
                    if (Diagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error)
                        return false;
                }

                return true;
            }
        }
    }
}
