using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction
{
    internal sealed class CharacterActionRecentEditEntry
    {
        [JsonProperty("libraryId")]
        public string LibraryId { get; set; } = string.Empty;

        [JsonProperty("actionId")]
        public int ActionId { get; set; }
    }

    internal sealed class CharacterActionRecentEditHistory
    {
        internal const int Capacity = 10;
        private const string PreferenceKeyPrefix =
            "MxFramework.CharacterAction.Workbench.RecentEdits.";

        private readonly string _preferenceKey;
        private readonly List<CharacterActionRecentEditEntry> _entries =
            new List<CharacterActionRecentEditEntry>(Capacity);

        internal CharacterActionRecentEditHistory(string preferenceKey)
        {
            _preferenceKey = preferenceKey ?? string.Empty;
            Load();
        }

        internal IReadOnlyList<CharacterActionRecentEditEntry> Entries => _entries;

        internal static CharacterActionRecentEditHistory ForProject(string dataPath)
        {
            return new CharacterActionRecentEditHistory(PreferenceKey(dataPath));
        }

        internal static string PreferenceKey(string dataPath)
        {
            string normalized = (dataPath ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/')
                .ToLowerInvariant();
            return PreferenceKeyPrefix + Hash128.Compute(normalized);
        }

        internal int IndexOf(string libraryId, int actionId)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (Matches(_entries[i], libraryId, actionId))
                    return i;
            }

            return -1;
        }

        internal void Touch(
            string libraryId,
            int actionId,
            string previousLibraryId = null,
            int previousActionId = 0)
        {
            if (!IsValid(libraryId, actionId))
                return;

            RemoveMatches(libraryId, actionId);
            if (IsValid(previousLibraryId, previousActionId)
                && (!string.Equals(previousLibraryId, libraryId, StringComparison.Ordinal)
                    || previousActionId != actionId))
            {
                RemoveMatches(previousLibraryId, previousActionId);
            }

            _entries.Insert(0, new CharacterActionRecentEditEntry
            {
                LibraryId = libraryId,
                ActionId = actionId,
            });
            if (_entries.Count > Capacity)
                _entries.RemoveRange(Capacity, _entries.Count - Capacity);
            Save();
        }

        internal void Remove(string libraryId, int actionId)
        {
            if (RemoveMatches(libraryId, actionId))
                Save();
        }

        internal void Prune(Predicate<CharacterActionRecentEditEntry> keep)
        {
            if (keep == null)
                return;

            bool changed = false;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (keep(_entries[i]))
                    continue;
                _entries.RemoveAt(i);
                changed = true;
            }

            if (changed)
                Save();
        }

        private void Load()
        {
            if (string.IsNullOrEmpty(_preferenceKey))
                return;

            string json = EditorPrefs.GetString(_preferenceKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                CharacterActionRecentEditEntry[] restored =
                    JsonConvert.DeserializeObject<CharacterActionRecentEditEntry[]>(json)
                    ?? Array.Empty<CharacterActionRecentEditEntry>();
                for (int i = 0; i < restored.Length && _entries.Count < Capacity; i++)
                {
                    CharacterActionRecentEditEntry entry = restored[i];
                    if (entry == null
                        || !IsValid(entry.LibraryId, entry.ActionId)
                        || IndexOf(entry.LibraryId, entry.ActionId) >= 0)
                    {
                        continue;
                    }

                    _entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                _entries.Clear();
            }
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(_preferenceKey))
                return;

            EditorPrefs.SetString(
                _preferenceKey,
                JsonConvert.SerializeObject(_entries, Formatting.None));
        }

        private bool RemoveMatches(string libraryId, int actionId)
        {
            bool changed = false;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (!Matches(_entries[i], libraryId, actionId))
                    continue;
                _entries.RemoveAt(i);
                changed = true;
            }

            return changed;
        }

        private static bool Matches(
            CharacterActionRecentEditEntry entry,
            string libraryId,
            int actionId)
        {
            return entry != null
                && string.Equals(entry.LibraryId, libraryId, StringComparison.Ordinal)
                && entry.ActionId == actionId;
        }

        private static bool IsValid(string libraryId, int actionId)
        {
            return !string.IsNullOrWhiteSpace(libraryId)
                && actionId > 0;
        }
    }
}
