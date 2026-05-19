using System;
using System.Collections.Generic;

namespace MxFramework.CharacterApplication
{
    internal sealed class CharacterDiagnosticBuilder
    {
        private readonly List<CharacterDiagnostic> _items = new List<CharacterDiagnostic>();

        public int Count => _items.Count;

        public void Add(CharacterDiagnostic diagnostic)
        {
            if (diagnostic.Code == CharacterDiagnosticCode.None && diagnostic.Severity == CharacterDiagnosticSeverity.Info)
                return;

            _items.Add(diagnostic);
        }

        public void Add(
            CharacterDiagnosticSeverity severity,
            CharacterDiagnosticCode code,
            string sourceTable,
            int sourceId,
            string sourceStableId,
            string field,
            string message)
        {
            Add(new CharacterDiagnostic(severity, code, sourceTable, sourceId, sourceStableId, field, message));
        }

        public void AddRange(CharacterDiagnostic[] diagnostics)
        {
            if (diagnostics == null)
                return;

            for (int i = 0; i < diagnostics.Length; i++)
            {
                Add(diagnostics[i]);
            }
        }

        public CharacterDiagnostic[] ToArray()
        {
            return _items.Count == 0 ? Array.Empty<CharacterDiagnostic>() : _items.ToArray();
        }
    }

    internal static class CharacterResolverUtility
    {
        public const string AnimationProfileResourceTypeId = "character.animation.profile";
        public const string TraceProfileResourceTypeId = "character.trace.profile";

        public static bool IsNullOrEmpty(string value)
        {
            return string.IsNullOrEmpty(value);
        }

        public static bool EqualsOrdinal(string a, string b)
        {
            return string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);
        }

        public static bool ContainsString(string[] values, string value)
        {
            if (values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (EqualsOrdinal(values[i], value))
                    return true;
            }

            return false;
        }

        public static bool ContainsEquipmentStateId(EquipmentStateId[] values, EquipmentStateId value)
        {
            if (values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].Equals(value))
                    return true;
            }

            return false;
        }

        public static bool ContainsAbilityId(CharacterAbilityId[] values, CharacterAbilityId value)
        {
            if (values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].Equals(value))
                    return true;
            }

            return false;
        }

        public static bool TrySplitSlotToken(string value, out string slotId, out string token)
        {
            slotId = string.Empty;
            token = string.Empty;

            if (string.IsNullOrEmpty(value))
                return false;

            int separatorIndex = value.IndexOf(':');
            if (separatorIndex < 0)
                separatorIndex = value.IndexOf('=');

            if (separatorIndex <= 0 || separatorIndex >= value.Length - 1)
                return false;

            slotId = value.Substring(0, separatorIndex);
            token = value.Substring(separatorIndex + 1);
            return !string.IsNullOrEmpty(slotId) && !string.IsNullOrEmpty(token);
        }

        public static string DiagnosticSummary(CharacterDiagnostic[] diagnostics)
        {
            if (diagnostics == null || diagnostics.Length == 0)
                return "ok";

            int errors = 0;
            int warnings = 0;
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Severity == CharacterDiagnosticSeverity.Error)
                    errors++;
                else if (diagnostics[i].Severity == CharacterDiagnosticSeverity.Warning)
                    warnings++;
            }

            return "errors=" + errors + ", warnings=" + warnings;
        }

        public static T[] ToArrayOrEmpty<T>(List<T> list)
        {
            return list == null || list.Count == 0 ? Array.Empty<T>() : list.ToArray();
        }
    }
}
