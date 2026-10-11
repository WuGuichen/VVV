using System;
using System.Collections.Generic;

namespace MxFramework.Editor.CharacterAction
{
    internal enum CharacterActionAuthoringSearchCategory
    {
        Action = 0,
        Intent = 1,
        Ability = 2,
        ReactionProfile = 3,
        Character = 4,
        EquipmentState = 5,
        EquipmentSlot = 6,
        Tag = 7,
        AnimationActionKey = 8,
        CombatActionId = 9,
        PresentationResourceKey = 10,
        AudioCueId = 11,
    }

    internal readonly struct CharacterActionAuthoringSearchCandidate
    {
        public CharacterActionAuthoringSearchCandidate(
            CharacterActionAuthoringSearchCategory category,
            string value,
            string displayName = "",
            string secondaryText = "",
            string sourceName = "",
            bool isKnown = true,
            bool isCurrent = false)
        {
            Category = category;
            Value = value ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Value : displayName;
            SecondaryText = secondaryText ?? string.Empty;
            SourceName = sourceName ?? string.Empty;
            IsKnown = isKnown;
            IsCurrent = isCurrent;
        }

        public CharacterActionAuthoringSearchCategory Category { get; }
        public string Value { get; }
        public string DisplayName { get; }
        public string SecondaryText { get; }
        public string SourceName { get; }
        public bool IsKnown { get; }
        public bool IsCurrent { get; }

        public CharacterActionAuthoringSearchCandidate AsCurrent()
        {
            return new CharacterActionAuthoringSearchCandidate(
                Category,
                Value,
                DisplayName,
                SecondaryText,
                SourceName,
                IsKnown,
                isCurrent: true);
        }
    }

    internal sealed class CharacterActionAuthoringSearchProjection
    {
        public CharacterActionAuthoringSearchProjection(
            CharacterActionAuthoringSearchCandidate[] candidates,
            bool hasUnknownCurrentValue)
        {
            Candidates = candidates ?? Array.Empty<CharacterActionAuthoringSearchCandidate>();
            HasUnknownCurrentValue = hasUnknownCurrentValue;
        }

        public CharacterActionAuthoringSearchCandidate[] Candidates { get; }
        public bool HasUnknownCurrentValue { get; }
    }

    internal sealed class CharacterActionAuthoringSearchCatalog
    {
        private readonly CharacterActionAuthoringSearchCandidate[] _candidates;

        public CharacterActionAuthoringSearchCatalog(
            IEnumerable<CharacterActionAuthoringSearchCandidate> candidates)
        {
            var unique = new Dictionary<CandidateKey, CharacterActionAuthoringSearchCandidate>();
            if (candidates != null)
            {
                foreach (CharacterActionAuthoringSearchCandidate candidate in candidates)
                {
                    if (string.IsNullOrWhiteSpace(candidate.Value))
                        continue;

                    var key = new CandidateKey(candidate.Category, candidate.Value);
                    if (!unique.ContainsKey(key))
                        unique.Add(key, candidate);
                }
            }

            _candidates = new CharacterActionAuthoringSearchCandidate[unique.Count];
            int index = 0;
            foreach (KeyValuePair<CandidateKey, CharacterActionAuthoringSearchCandidate> pair in unique)
                _candidates[index++] = pair.Value;
            Array.Sort(_candidates, CompareCandidates);
        }

        public IReadOnlyList<CharacterActionAuthoringSearchCandidate> Candidates => _candidates;

        public static CharacterActionAuthoringSearchCatalog CreateFromReferenceDiscovery(
            CharacterActionReferenceDiscoveryResult discovery,
            IEnumerable<CharacterActionAuthoringSearchCandidate> additionalCandidates = null)
        {
            var candidates = new List<CharacterActionAuthoringSearchCandidate>();
            if (additionalCandidates != null)
                candidates.AddRange(additionalCandidates);

            CharacterActionReferenceDiscoveryEntry[] entries =
                discovery?.Entries ?? Array.Empty<CharacterActionReferenceDiscoveryEntry>();
            for (int i = 0; i < entries.Length; i++)
            {
                CharacterActionReferenceDiscoveryEntry entry = entries[i];
                if (!TryMapCategory(entry.Category, out CharacterActionAuthoringSearchCategory category))
                    continue;

                candidates.Add(new CharacterActionAuthoringSearchCandidate(
                    category,
                    entry.Value,
                    entry.Value,
                    entry.SourceKind.ToString(),
                    entry.SourceName));
            }

            return new CharacterActionAuthoringSearchCatalog(candidates);
        }

        public CharacterActionAuthoringSearchProjection Project(
            CharacterActionAuthoringSearchCategory category,
            string query,
            string currentValue)
        {
            string normalizedCurrent = currentValue ?? string.Empty;
            string[] queryTokens = SplitQuery(query);
            int currentIndex = Find(category, normalizedCurrent);
            bool hasUnknownCurrent = normalizedCurrent.Length > 0 && currentIndex < 0;
            var result = new List<CharacterActionAuthoringSearchCandidate>();

            if (hasUnknownCurrent)
            {
                result.Add(new CharacterActionAuthoringSearchCandidate(
                    category,
                    normalizedCurrent,
                    normalizedCurrent,
                    "未知引用",
                    string.Empty,
                    isKnown: false,
                    isCurrent: true));
            }
            else if (currentIndex >= 0)
            {
                result.Add(_candidates[currentIndex].AsCurrent());
            }

            for (int i = 0; i < _candidates.Length; i++)
            {
                CharacterActionAuthoringSearchCandidate candidate = _candidates[i];
                if (candidate.Category != category
                    || i == currentIndex
                    || !MatchesQuery(candidate, queryTokens))
                {
                    continue;
                }

                result.Add(candidate);
            }

            return new CharacterActionAuthoringSearchProjection(result.ToArray(), hasUnknownCurrent);
        }

        private int Find(CharacterActionAuthoringSearchCategory category, string value)
        {
            if (string.IsNullOrEmpty(value))
                return -1;

            for (int i = 0; i < _candidates.Length; i++)
            {
                CharacterActionAuthoringSearchCandidate candidate = _candidates[i];
                if (candidate.Category == category
                    && string.Equals(candidate.Value, value, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryMapCategory(
            CharacterActionReferenceDiscoveryCategory source,
            out CharacterActionAuthoringSearchCategory target)
        {
            switch (source)
            {
                case CharacterActionReferenceDiscoveryCategory.AnimationActionKey:
                    target = CharacterActionAuthoringSearchCategory.AnimationActionKey;
                    return true;
                case CharacterActionReferenceDiscoveryCategory.CombatActionId:
                    target = CharacterActionAuthoringSearchCategory.CombatActionId;
                    return true;
                case CharacterActionReferenceDiscoveryCategory.PresentationResourceKey:
                    target = CharacterActionAuthoringSearchCategory.PresentationResourceKey;
                    return true;
                case CharacterActionReferenceDiscoveryCategory.AudioCueId:
                    target = CharacterActionAuthoringSearchCategory.AudioCueId;
                    return true;
                case CharacterActionReferenceDiscoveryCategory.CharacterStableId:
                    target = CharacterActionAuthoringSearchCategory.Character;
                    return true;
                case CharacterActionReferenceDiscoveryCategory.EquipmentStateStableId:
                    target = CharacterActionAuthoringSearchCategory.EquipmentState;
                    return true;
                case CharacterActionReferenceDiscoveryCategory.EquipmentSlotId:
                    target = CharacterActionAuthoringSearchCategory.EquipmentSlot;
                    return true;
                case CharacterActionReferenceDiscoveryCategory.EquipmentTag:
                    target = CharacterActionAuthoringSearchCategory.Tag;
                    return true;
                default:
                    target = default;
                    return false;
            }
        }

        private static int CompareCandidates(
            CharacterActionAuthoringSearchCandidate left,
            CharacterActionAuthoringSearchCandidate right)
        {
            int compare = left.Category.CompareTo(right.Category);
            if (compare != 0)
                return compare;
            compare = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (compare != 0)
                return compare;
            compare = string.CompareOrdinal(left.Value, right.Value);
            if (compare != 0)
                return compare;
            return string.CompareOrdinal(left.SourceName, right.SourceName);
        }

        private static bool MatchesQuery(
            CharacterActionAuthoringSearchCandidate candidate,
            string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!Contains(candidate.Value, token)
                    && !Contains(candidate.DisplayName, token)
                    && !Contains(candidate.SecondaryText, token)
                    && !Contains(candidate.SourceName, token))
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

        private readonly struct CandidateKey : IEquatable<CandidateKey>
        {
            private readonly CharacterActionAuthoringSearchCategory _category;
            private readonly string _value;

            public CandidateKey(CharacterActionAuthoringSearchCategory category, string value)
            {
                _category = category;
                _value = value;
            }

            public bool Equals(CandidateKey other)
            {
                return _category == other._category
                    && string.Equals(_value, other._value, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is CandidateKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)_category * 397) ^ (_value == null ? 0 : _value.GetHashCode());
                }
            }
        }
    }
}
