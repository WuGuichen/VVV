using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;

namespace MxFramework.Editor.CharacterAction
{
    internal enum CharacterActionAuthoringProblemOrigin
    {
        Import = 0,
        LocalValidation = 1,
        CrossModuleValidation = 2,
        ReferenceDiscovery = 3,
    }

    internal enum CharacterActionAuthoringProblemWorkspace
    {
        Source = 0,
        ActionComposition = 1,
        Mappings = 2,
    }

    internal enum CharacterActionAuthoringProblemTargetKind
    {
        Source = 0,
        ActionSet = 1,
        CommandBinding = 2,
        AbilityBinding = 3,
        LegacyReactionBinding = 4,
        ReactionProfile = 5,
        Action = 6,
        TimelineItem = 7,
    }

    internal readonly struct CharacterActionAuthoringProblemLocation
    {
        public CharacterActionAuthoringProblemLocation(
            CharacterActionAuthoringProblemWorkspace workspace,
            CharacterActionAuthoringProblemTargetKind targetKind,
            string sourcePath,
            string objectKey,
            string section,
            string itemKey,
            int itemIndex,
            string fieldPath)
        {
            Workspace = workspace;
            TargetKind = targetKind;
            SourcePath = sourcePath ?? string.Empty;
            ObjectKey = objectKey ?? string.Empty;
            Section = section ?? string.Empty;
            ItemKey = itemKey ?? string.Empty;
            ItemIndex = itemIndex;
            FieldPath = fieldPath ?? string.Empty;
        }

        public CharacterActionAuthoringProblemWorkspace Workspace { get; }
        public CharacterActionAuthoringProblemTargetKind TargetKind { get; }
        public string SourcePath { get; }
        public string ObjectKey { get; }
        public string Section { get; }
        public string ItemKey { get; }
        public int ItemIndex { get; }
        public string FieldPath { get; }

        public bool HasStructuredTarget => TargetKind != CharacterActionAuthoringProblemTargetKind.Source;

        public static CharacterActionAuthoringProblemLocation ForSource(string sourcePath)
        {
            return new CharacterActionAuthoringProblemLocation(
                CharacterActionAuthoringProblemWorkspace.Source,
                CharacterActionAuthoringProblemTargetKind.Source,
                sourcePath,
                string.Empty,
                string.Empty,
                string.Empty,
                -1,
                string.Empty);
        }
    }

    internal sealed class CharacterActionAuthoringProblem
    {
        public CharacterActionAuthoringProblem(
            CharacterActionAuthoringProblemOrigin origin,
            CharacterActionDiagnosticSeverity severity,
            string code,
            string sourcePath,
            string objectPath,
            string message)
        {
            Origin = origin;
            Severity = severity;
            Code = code ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            ObjectPath = string.IsNullOrWhiteSpace(objectPath) ? "$" : objectPath;
            Message = message ?? string.Empty;
            Location = CharacterActionAuthoringProblemLocationParser.Parse(SourcePath, ObjectPath);
        }

        public CharacterActionAuthoringProblemOrigin Origin { get; }
        public CharacterActionDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string SourcePath { get; }
        public string ObjectPath { get; }
        public string Message { get; }
        public CharacterActionAuthoringProblemLocation Location { get; }
    }

    internal static class CharacterActionAuthoringProblemLocationParser
    {
        private static readonly string[] TimelineCollections =
        {
            "phases",
            "cancelRules",
            "interruptRules",
        };

        public static CharacterActionAuthoringProblemLocation Parse(string sourcePath, string objectPath)
        {
            string normalized = NormalizeObjectPath(objectPath);
            if (TryParseRoot(normalized, "actionSets", out string actionSetKey, out string actionSetPath))
                return ParseActionSet(sourcePath, actionSetKey, actionSetPath);

            if (TryParseRoot(normalized, "actions", out string actionKey, out string actionPath))
                return ParseAction(sourcePath, actionKey, actionPath);

            if (TryParseRoot(normalized, "reactionProfiles", out string reactionProfileKey, out string reactionPath))
            {
                return new CharacterActionAuthoringProblemLocation(
                    CharacterActionAuthoringProblemWorkspace.Mappings,
                    CharacterActionAuthoringProblemTargetKind.ReactionProfile,
                    sourcePath,
                    reactionProfileKey,
                    "reactionProfiles",
                    string.Empty,
                    -1,
                    reactionPath);
            }

            return CharacterActionAuthoringProblemLocation.ForSource(sourcePath);
        }

        private static CharacterActionAuthoringProblemLocation ParseActionSet(
            string sourcePath,
            string actionSetKey,
            string remainder)
        {
            if (TryParseCollection(remainder, "commandBindings", out string itemKey, out int itemIndex, out string fieldPath))
            {
                return CreateMappingLocation(
                    sourcePath,
                    actionSetKey,
                    CharacterActionAuthoringProblemTargetKind.CommandBinding,
                    "commandBindings",
                    itemKey,
                    itemIndex,
                    fieldPath);
            }

            if (TryParseCollection(remainder, "abilityBindings", out itemKey, out itemIndex, out fieldPath))
            {
                return CreateMappingLocation(
                    sourcePath,
                    actionSetKey,
                    CharacterActionAuthoringProblemTargetKind.AbilityBinding,
                    "abilityBindings",
                    itemKey,
                    itemIndex,
                    fieldPath);
            }

            if (TryParseCollection(remainder, "reactionBindings", out itemKey, out itemIndex, out fieldPath))
            {
                return CreateMappingLocation(
                    sourcePath,
                    actionSetKey,
                    CharacterActionAuthoringProblemTargetKind.LegacyReactionBinding,
                    "reactionBindings",
                    itemKey,
                    itemIndex,
                    fieldPath);
            }

            return new CharacterActionAuthoringProblemLocation(
                CharacterActionAuthoringProblemWorkspace.Mappings,
                CharacterActionAuthoringProblemTargetKind.ActionSet,
                sourcePath,
                actionSetKey,
                "actionSet",
                string.Empty,
                -1,
                remainder);
        }

        private static CharacterActionAuthoringProblemLocation ParseAction(
            string sourcePath,
            string actionKey,
            string remainder)
        {
            for (int i = 0; i < TimelineCollections.Length; i++)
            {
                string collection = TimelineCollections[i];
                if (!TryParseCollection(remainder, collection, out string itemKey, out int itemIndex, out string fieldPath))
                    continue;

                return new CharacterActionAuthoringProblemLocation(
                    CharacterActionAuthoringProblemWorkspace.ActionComposition,
                    CharacterActionAuthoringProblemTargetKind.TimelineItem,
                    sourcePath,
                    actionKey,
                    collection,
                    itemKey,
                    itemIndex,
                    fieldPath);
            }

            if (TryParseTrackEvent(remainder, out string section, out string eventKey, out int eventIndex, out string eventFieldPath))
            {
                return new CharacterActionAuthoringProblemLocation(
                    CharacterActionAuthoringProblemWorkspace.ActionComposition,
                    CharacterActionAuthoringProblemTargetKind.TimelineItem,
                    sourcePath,
                    actionKey,
                    section,
                    eventKey,
                    eventIndex,
                    eventFieldPath);
            }

            return new CharacterActionAuthoringProblemLocation(
                CharacterActionAuthoringProblemWorkspace.ActionComposition,
                CharacterActionAuthoringProblemTargetKind.Action,
                sourcePath,
                actionKey,
                "action",
                string.Empty,
                -1,
                remainder);
        }

        private static CharacterActionAuthoringProblemLocation CreateMappingLocation(
            string sourcePath,
            string actionSetKey,
            CharacterActionAuthoringProblemTargetKind targetKind,
            string section,
            string itemKey,
            int itemIndex,
            string fieldPath)
        {
            return new CharacterActionAuthoringProblemLocation(
                CharacterActionAuthoringProblemWorkspace.Mappings,
                targetKind,
                sourcePath,
                actionSetKey,
                section,
                itemKey,
                itemIndex,
                fieldPath);
        }

        private static bool TryParseTrackEvent(
            string path,
            out string section,
            out string itemKey,
            out int itemIndex,
            out string fieldPath)
        {
            section = string.Empty;
            itemKey = string.Empty;
            itemIndex = -1;
            fieldPath = string.Empty;

            const string marker = ".events[";
            int markerIndex = path.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex <= 0)
                return false;

            int itemStart = markerIndex + marker.Length;
            int itemEnd = path.IndexOf(']', itemStart);
            if (itemEnd < itemStart)
                return false;

            string track = path.Substring(0, markerIndex);
            if (!track.EndsWith("Track", StringComparison.Ordinal))
                return false;

            section = track + ".events";
            itemKey = path.Substring(itemStart, itemEnd - itemStart);
            if (!int.TryParse(itemKey, NumberStyles.None, CultureInfo.InvariantCulture, out itemIndex))
                itemIndex = -1;
            fieldPath = TrimLeadingDot(path.Substring(itemEnd + 1));
            return true;
        }

        private static bool TryParseCollection(
            string path,
            string collection,
            out string itemKey,
            out int itemIndex,
            out string fieldPath)
        {
            itemKey = string.Empty;
            itemIndex = -1;
            fieldPath = string.Empty;
            string prefix = collection + "[";
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            int itemStart = prefix.Length;
            int itemEnd = path.IndexOf(']', itemStart);
            if (itemEnd < itemStart)
                return false;

            itemKey = path.Substring(itemStart, itemEnd - itemStart);
            if (!int.TryParse(itemKey, NumberStyles.None, CultureInfo.InvariantCulture, out itemIndex))
                itemIndex = -1;
            fieldPath = TrimLeadingDot(path.Substring(itemEnd + 1));
            return true;
        }

        private static bool TryParseRoot(string path, string rootName, out string objectKey, out string remainder)
        {
            objectKey = string.Empty;
            remainder = string.Empty;
            string prefix = rootName + "[";
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            int keyStart = prefix.Length;
            int keyEnd = path.IndexOf(']', keyStart);
            if (keyEnd < keyStart)
                return false;

            objectKey = path.Substring(keyStart, keyEnd - keyStart);
            remainder = TrimLeadingDot(path.Substring(keyEnd + 1));
            return true;
        }

        private static string NormalizeObjectPath(string objectPath)
        {
            string path = string.IsNullOrWhiteSpace(objectPath) ? string.Empty : objectPath.Trim();
            if (path.StartsWith("$", StringComparison.Ordinal))
                path = path.Substring(1);
            return TrimLeadingDot(path);
        }

        private static string TrimLeadingDot(string value)
        {
            return !string.IsNullOrEmpty(value) && value[0] == '.' ? value.Substring(1) : value;
        }
    }

    [Flags]
    internal enum CharacterActionAuthoringProblemSeverityFilter
    {
        None = 0,
        Info = 1 << 0,
        Warning = 1 << 1,
        Error = 1 << 2,
        All = Info | Warning | Error,
    }

    internal enum CharacterActionAuthoringProblemScope
    {
        All = 0,
        CurrentAction = 1,
        CurrentSource = 2,
        ReferenceDataSource = 3,
    }

    internal readonly struct CharacterActionAuthoringProblemFilter
    {
        public static readonly CharacterActionAuthoringProblemFilter Default =
            new CharacterActionAuthoringProblemFilter(
                CharacterActionAuthoringProblemScope.All,
                CharacterActionAuthoringProblemSeverityFilter.All);

        public CharacterActionAuthoringProblemFilter(
            CharacterActionAuthoringProblemScope scope = CharacterActionAuthoringProblemScope.All,
            CharacterActionAuthoringProblemSeverityFilter severities = CharacterActionAuthoringProblemSeverityFilter.All,
            string query = "",
            string currentActionId = "",
            string currentSourcePath = "")
        {
            Scope = scope;
            Severities = severities;
            Query = query ?? string.Empty;
            CurrentActionId = currentActionId ?? string.Empty;
            CurrentSourcePath = currentSourcePath ?? string.Empty;
        }

        public CharacterActionAuthoringProblemScope Scope { get; }
        public CharacterActionAuthoringProblemSeverityFilter Severities { get; }
        public string Query { get; }
        public string CurrentActionId { get; }
        public string CurrentSourcePath { get; }
    }

    internal sealed class CharacterActionAuthoringProblemsProjection
    {
        public CharacterActionAuthoringProblemsProjection(
            CharacterActionAuthoringProblem[] items,
            int totalErrorCount,
            int totalWarningCount,
            int totalInfoCount)
        {
            Items = items ?? Array.Empty<CharacterActionAuthoringProblem>();
            TotalErrorCount = totalErrorCount;
            TotalWarningCount = totalWarningCount;
            TotalInfoCount = totalInfoCount;
        }

        public CharacterActionAuthoringProblem[] Items { get; }
        public int TotalErrorCount { get; }
        public int TotalWarningCount { get; }
        public int TotalInfoCount { get; }
    }

    internal sealed class CharacterActionAuthoringProblemsModel
    {
        private readonly CharacterActionAuthoringProblem[] _items;

        private CharacterActionAuthoringProblemsModel(CharacterActionAuthoringProblem[] items)
        {
            _items = items ?? Array.Empty<CharacterActionAuthoringProblem>();
            for (int i = 0; i < _items.Length; i++)
            {
                switch (_items[i].Severity)
                {
                    case CharacterActionDiagnosticSeverity.Error:
                        ErrorCount++;
                        break;
                    case CharacterActionDiagnosticSeverity.Warning:
                        WarningCount++;
                        break;
                    default:
                        InfoCount++;
                        break;
                }
            }
        }

        public IReadOnlyList<CharacterActionAuthoringProblem> Items => _items;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public int InfoCount { get; private set; }
        public bool HasErrors => ErrorCount > 0;

        public static CharacterActionAuthoringProblemsModel Create(
            CharacterActionAuthoringExportResult export,
            CharacterActionReferenceDiscoveryResult referenceDiscovery)
        {
            return Create(
                export?.ImportResult?.Diagnostics,
                export?.LocalValidationReport?.Diagnostics,
                export?.ValidationReport?.Diagnostics,
                referenceDiscovery?.Diagnostics);
        }

        public static CharacterActionAuthoringProblemsModel Create(
            CharacterActionJsonDiagnostic[] importDiagnostics,
            CharacterActionValidationDiagnostic[] localDiagnostics,
            CharacterActionValidationDiagnostic[] validationDiagnostics,
            CharacterActionReferenceDiscoveryDiagnostic[] referenceDiagnostics)
        {
            var items = new List<CharacterActionAuthoringProblem>();
            var identities = new HashSet<ProblemIdentity>();

            AddImportDiagnostics(items, identities, importDiagnostics);
            AddValidationDiagnostics(
                items,
                identities,
                localDiagnostics,
                CharacterActionAuthoringProblemOrigin.LocalValidation);
            AddValidationDiagnostics(
                items,
                identities,
                validationDiagnostics,
                CharacterActionAuthoringProblemOrigin.CrossModuleValidation);
            AddReferenceDiagnostics(items, identities, referenceDiagnostics);

            CharacterActionAuthoringProblem[] result = items.ToArray();
            Array.Sort(result, CompareProblems);
            return new CharacterActionAuthoringProblemsModel(result);
        }

        public CharacterActionAuthoringProblemsProjection Project(CharacterActionAuthoringProblemFilter filter)
        {
            var result = new List<CharacterActionAuthoringProblem>(_items.Length);
            string[] queryTokens = SplitQuery(filter.Query);
            for (int i = 0; i < _items.Length; i++)
            {
                CharacterActionAuthoringProblem problem = _items[i];
                if (!MatchesSeverity(problem.Severity, filter.Severities)
                    || !MatchesScope(problem, filter)
                    || !MatchesQuery(problem, queryTokens))
                {
                    continue;
                }

                result.Add(problem);
            }

            return new CharacterActionAuthoringProblemsProjection(
                result.ToArray(),
                ErrorCount,
                WarningCount,
                InfoCount);
        }

        public CharacterActionAuthoringProblemsProjection Project()
        {
            return Project(CharacterActionAuthoringProblemFilter.Default);
        }

        private static void AddImportDiagnostics(
            List<CharacterActionAuthoringProblem> items,
            HashSet<ProblemIdentity> identities,
            CharacterActionJsonDiagnostic[] diagnostics)
        {
            diagnostics = diagnostics ?? Array.Empty<CharacterActionJsonDiagnostic>();
            for (int i = 0; i < diagnostics.Length; i++)
            {
                CharacterActionJsonDiagnostic diagnostic = diagnostics[i];
                AddProblem(
                    items,
                    identities,
                    new CharacterActionAuthoringProblem(
                        CharacterActionAuthoringProblemOrigin.Import,
                        diagnostic.Severity,
                        diagnostic.Code,
                        diagnostic.SourcePath,
                        diagnostic.JsonPath,
                        diagnostic.Message));
            }
        }

        private static void AddValidationDiagnostics(
            List<CharacterActionAuthoringProblem> items,
            HashSet<ProblemIdentity> identities,
            CharacterActionValidationDiagnostic[] diagnostics,
            CharacterActionAuthoringProblemOrigin origin)
        {
            diagnostics = diagnostics ?? Array.Empty<CharacterActionValidationDiagnostic>();
            for (int i = 0; i < diagnostics.Length; i++)
            {
                CharacterActionValidationDiagnostic diagnostic = diagnostics[i];
                AddProblem(
                    items,
                    identities,
                    new CharacterActionAuthoringProblem(
                        origin,
                        diagnostic.Severity,
                        diagnostic.Code,
                        diagnostic.SourcePath,
                        diagnostic.StablePath,
                        diagnostic.Message));
            }
        }

        private static void AddReferenceDiagnostics(
            List<CharacterActionAuthoringProblem> items,
            HashSet<ProblemIdentity> identities,
            CharacterActionReferenceDiscoveryDiagnostic[] diagnostics)
        {
            diagnostics = diagnostics ?? Array.Empty<CharacterActionReferenceDiscoveryDiagnostic>();
            for (int i = 0; i < diagnostics.Length; i++)
            {
                CharacterActionReferenceDiscoveryDiagnostic diagnostic = diagnostics[i];
                AddProblem(
                    items,
                    identities,
                    new CharacterActionAuthoringProblem(
                        CharacterActionAuthoringProblemOrigin.ReferenceDiscovery,
                        ToActionSeverity(diagnostic.Severity),
                        diagnostic.Code,
                        diagnostic.SourceName,
                        "$",
                        diagnostic.Message));
            }
        }

        private static void AddProblem(
            List<CharacterActionAuthoringProblem> items,
            HashSet<ProblemIdentity> identities,
            CharacterActionAuthoringProblem problem)
        {
            var identity = new ProblemIdentity(problem);
            if (identities.Add(identity))
                items.Add(problem);
        }

        private static CharacterActionDiagnosticSeverity ToActionSeverity(
            CharacterActionReferenceDiscoverySeverity severity)
        {
            switch (severity)
            {
                case CharacterActionReferenceDiscoverySeverity.Error:
                    return CharacterActionDiagnosticSeverity.Error;
                case CharacterActionReferenceDiscoverySeverity.Warning:
                    return CharacterActionDiagnosticSeverity.Warning;
                default:
                    return CharacterActionDiagnosticSeverity.Info;
            }
        }

        private static int CompareProblems(CharacterActionAuthoringProblem left, CharacterActionAuthoringProblem right)
        {
            int compare = right.Severity.CompareTo(left.Severity);
            if (compare != 0)
                return compare;
            compare = left.Origin.CompareTo(right.Origin);
            if (compare != 0)
                return compare;
            compare = string.CompareOrdinal(left.SourcePath, right.SourcePath);
            if (compare != 0)
                return compare;
            compare = string.CompareOrdinal(left.ObjectPath, right.ObjectPath);
            if (compare != 0)
                return compare;
            compare = string.CompareOrdinal(left.Code, right.Code);
            if (compare != 0)
                return compare;
            return string.CompareOrdinal(left.Message, right.Message);
        }

        private static bool MatchesSeverity(
            CharacterActionDiagnosticSeverity severity,
            CharacterActionAuthoringProblemSeverityFilter filter)
        {
            CharacterActionAuthoringProblemSeverityFilter flag;
            switch (severity)
            {
                case CharacterActionDiagnosticSeverity.Error:
                    flag = CharacterActionAuthoringProblemSeverityFilter.Error;
                    break;
                case CharacterActionDiagnosticSeverity.Warning:
                    flag = CharacterActionAuthoringProblemSeverityFilter.Warning;
                    break;
                default:
                    flag = CharacterActionAuthoringProblemSeverityFilter.Info;
                    break;
            }

            return (filter & flag) != 0;
        }

        private static bool MatchesScope(
            CharacterActionAuthoringProblem problem,
            CharacterActionAuthoringProblemFilter filter)
        {
            switch (filter.Scope)
            {
                case CharacterActionAuthoringProblemScope.CurrentAction:
                    return problem.Location.Workspace == CharacterActionAuthoringProblemWorkspace.ActionComposition
                        && string.Equals(problem.Location.ObjectKey, filter.CurrentActionId, StringComparison.Ordinal);
                case CharacterActionAuthoringProblemScope.CurrentSource:
                    return string.Equals(problem.SourcePath, filter.CurrentSourcePath, StringComparison.Ordinal);
                case CharacterActionAuthoringProblemScope.ReferenceDataSource:
                    return problem.Origin == CharacterActionAuthoringProblemOrigin.ReferenceDiscovery;
                default:
                    return true;
            }
        }

        private static bool MatchesQuery(CharacterActionAuthoringProblem problem, string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!Contains(problem.Code, token)
                    && !Contains(problem.Message, token)
                    && !Contains(problem.SourcePath, token)
                    && !Contains(problem.ObjectPath, token))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Contains(string value, string token)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string[] SplitQuery(string query)
        {
            return string.IsNullOrWhiteSpace(query)
                ? Array.Empty<string>()
                : query.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        private readonly struct ProblemIdentity : IEquatable<ProblemIdentity>
        {
            private readonly CharacterActionDiagnosticSeverity _severity;
            private readonly string _code;
            private readonly string _sourcePath;
            private readonly string _objectPath;
            private readonly string _message;

            public ProblemIdentity(CharacterActionAuthoringProblem problem)
            {
                _severity = problem.Severity;
                _code = problem.Code;
                _sourcePath = problem.SourcePath;
                _objectPath = problem.ObjectPath;
                _message = problem.Message;
            }

            public bool Equals(ProblemIdentity other)
            {
                return _severity == other._severity
                    && string.Equals(_code, other._code, StringComparison.Ordinal)
                    && string.Equals(_sourcePath, other._sourcePath, StringComparison.Ordinal)
                    && string.Equals(_objectPath, other._objectPath, StringComparison.Ordinal)
                    && string.Equals(_message, other._message, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is ProblemIdentity other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)_severity;
                    hash = (hash * 397) ^ (_code == null ? 0 : _code.GetHashCode());
                    hash = (hash * 397) ^ (_sourcePath == null ? 0 : _sourcePath.GetHashCode());
                    hash = (hash * 397) ^ (_objectPath == null ? 0 : _objectPath.GetHashCode());
                    hash = (hash * 397) ^ (_message == null ? 0 : _message.GetHashCode());
                    return hash;
                }
            }
        }
    }
}
