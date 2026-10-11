using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Projectiles;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    internal enum CharacterActionGameplayRequestPickerItemKind
    {
        CurrentUnknown,
        Projectile,
        Gameplay,
    }

    internal sealed class CharacterActionGameplayRequestPickerItem
    {
        public CharacterActionGameplayRequestPickerItem(
            string requestId,
            string title,
            string subtitle,
            string searchText,
            string section,
            CharacterActionGameplayRequestPickerItemKind kind,
            bool isFavorite,
            bool isRecent)
        {
            RequestId = requestId ?? string.Empty;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            SearchText = searchText ?? string.Empty;
            Section = section ?? string.Empty;
            Kind = kind;
            IsFavorite = isFavorite;
            IsRecent = isRecent;
        }

        public string RequestId { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string SearchText { get; }
        public string Section { get; }
        public CharacterActionGameplayRequestPickerItemKind Kind { get; }
        public bool IsFavorite { get; set; }
        public bool IsRecent { get; set; }
    }

    internal sealed class CharacterActionGameplayRequestPickerSection
    {
        public CharacterActionGameplayRequestPickerSection(
            string title,
            List<CharacterActionGameplayRequestPickerItem> items)
        {
            Title = title ?? string.Empty;
            Items = items ?? new List<CharacterActionGameplayRequestPickerItem>();
        }

        public string Title { get; }
        public List<CharacterActionGameplayRequestPickerItem> Items { get; }
    }

    internal static class CharacterActionGameplayRequestPickerCatalog
    {
        private const int RecentLimit = 10;
        private const string EditorPrefsPrefix =
            "MxFramework.CharacterActionAuthoringWindow.GameplayRequestPicker.";

        public static List<CharacterActionGameplayRequestPickerItem> BuildItems(
            string currentValue,
            CharacterActionProjectileAuthoringStore projectileStore,
            CharacterActionAuthoringModel model,
            ISet<string> favorites,
            ISet<string> recent)
        {
            currentValue = currentValue ?? string.Empty;
            var result = new List<CharacterActionGameplayRequestPickerItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            favorites = favorites ?? new HashSet<string>(StringComparer.Ordinal);
            recent = recent ?? new HashSet<string>(StringComparer.Ordinal);

            IReadOnlyList<CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord> records =
                projectileStore?.Records
                ?? Array.Empty<CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord>();
            for (int i = 0; i < records.Count; i++)
            {
                CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record = records[i];
                if (!CharacterActionProjectileRequestEligibility.IsCompiledReferenceable(record)
                    || !seen.Add(record.RequestId))
                    continue;
                result.Add(CreateProjectileItem(record, favorites, recent));
            }

            List<CharacterActionJsonDto> actions = model?.Actions;
            int actionCount = actions?.Count ?? 0;
            for (int i = 0; i < actionCount; i++)
            {
                GameplayTrackEventJsonDto[] events =
                    actions[i]?.GameplayTrack?.Events ?? Array.Empty<GameplayTrackEventJsonDto>();
                for (int j = 0; j < events.Length; j++)
                {
                    string requestId = CharacterActionAuthoringIds.Label(events[j]?.RequestId ?? 0, 408);
                    if (string.IsNullOrWhiteSpace(requestId) || !seen.Add(requestId))
                        continue;
                    result.Add(CreateGameplayItem(requestId, favorites, recent));
                }
            }

            if (!string.IsNullOrWhiteSpace(currentValue) && !seen.Contains(currentValue))
                result.Insert(0, CreateCurrentUnknownItem(currentValue, favorites, recent));

            result.Sort(CompareItems);
            return result;
        }

        public static List<CharacterActionGameplayRequestPickerSection> BuildSections(
            IReadOnlyList<CharacterActionGameplayRequestPickerItem> items,
            string query)
        {
            var sections = new List<CharacterActionGameplayRequestPickerSection>();
            items = items ?? Array.Empty<CharacterActionGameplayRequestPickerItem>();
            query = query ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(query))
            {
                var matches = new List<CharacterActionGameplayRequestPickerItem>();
                for (int i = 0; i < items.Count; i++)
                {
                    if (Matches(items[i], query))
                        matches.Add(items[i]);
                }

                matches.Sort((left, right) =>
                {
                    int score = MatchScore(right, query).CompareTo(MatchScore(left, query));
                    return score != 0 ? score : CompareItems(left, right);
                });
                AddSection(sections, "Search Results", matches);
                return sections;
            }

            var shown = new HashSet<string>(StringComparer.Ordinal);
            AddSection(
                sections,
                "Current / Unknown",
                Filter(items, item => item.Kind == CharacterActionGameplayRequestPickerItemKind.CurrentUnknown, shown));
            AddSection(sections, "Favorites", Filter(items, item => item.IsFavorite, shown));
            AddSection(sections, "Recent", Filter(items, item => item.IsRecent, shown));

            var sectionNames = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < items.Count; i++)
            {
                CharacterActionGameplayRequestPickerItem item = items[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.Section))
                    sectionNames.Add(item.Section);
            }

            foreach (string sectionName in sectionNames)
            {
                AddSection(
                    sections,
                    sectionName,
                    Filter(
                        items,
                        item => string.Equals(item.Section, sectionName, StringComparison.Ordinal),
                        shown));
            }

            return sections;
        }

        public static HashSet<string> LoadSet(string scope, string setName)
        {
            string raw = EditorPrefs.GetString(BuildPrefsKey(scope, setName), string.Empty);
            var values = new HashSet<string>(StringComparer.Ordinal);
            string[] parts = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string value = parts[i].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value);
            }

            return values;
        }

        public static void SetFavorite(string scope, string requestId, bool favorite)
        {
            var values = LoadSet(scope, "favorites");
            if (favorite)
                values.Add(requestId ?? string.Empty);
            else
                values.Remove(requestId ?? string.Empty);
            SaveSet(scope, "favorites", values, RecentLimit);
        }

        public static void RecordRecent(string scope, string requestId)
        {
            requestId = requestId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(requestId))
                return;

            var ordered = new List<string> { requestId };
            var seen = new HashSet<string>(StringComparer.Ordinal) { requestId };
            List<string> existing = LoadOrderedList(scope, "recent");
            for (int i = 0; i < existing.Count; i++)
            {
                string value = existing[i];
                if (seen.Add(value))
                    ordered.Add(value);
            }

            SaveSet(scope, "recent", ordered, RecentLimit);
        }

        private static List<string> LoadOrderedList(string scope, string setName)
        {
            string raw = EditorPrefs.GetString(BuildPrefsKey(scope, setName), string.Empty);
            var values = new List<string>();
            string[] parts = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string value = parts[i].Trim();
                if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value))
                    values.Add(value);
            }

            return values;
        }

        private static void SaveSet(string scope, string setName, IEnumerable<string> values, int limit)
        {
            var ordered = new List<string>();
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value) || ordered.Contains(value))
                    continue;
                ordered.Add(value);
                if (ordered.Count >= limit)
                    break;
            }

            EditorPrefs.SetString(BuildPrefsKey(scope, setName), string.Join("\n", ordered));
        }

        private static string BuildPrefsKey(string scope, string setName)
        {
            return EditorPrefsPrefix + SanitizePrefsScope(scope) + "." + (setName ?? string.Empty);
        }

        private static string SanitizePrefsScope(string scope)
        {
            scope = scope ?? string.Empty;
            var chars = new char[scope.Length];
            int count = 0;
            for (int i = 0; i < scope.Length; i++)
            {
                char c = scope[i];
                chars[count++] = char.IsLetterOrDigit(c) ? c : '_';
            }

            return count == 0 ? "default" : new string(chars, 0, count);
        }

        private static CharacterActionGameplayRequestPickerItem CreateProjectileItem(
            CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record,
            ISet<string> favorites,
            ISet<string> recent)
        {
            CharacterActionProjectileInspectorViewModel viewModel =
                CharacterActionProjectileInspectorViewModel.Create(record);
            string group = ResolveProjectileGroup(record);
            string subtitle = record.StableId
                + " | " + viewModel.TypeLabel
                + " | " + viewModel.MotionLabel
                + " | " + viewModel.RangeLabel
                + " | " + CharacterActionDamageAuthoringText.FormatDamageTypeLabel(record.DamageTypeId);
            string search = record.StableId
                + " " + record.DisplayName
                + " " + viewModel.ChoiceLabel
                + " " + record.Tags
                + " " + record.AnalysisCategory
                + " " + group;
            return new CharacterActionGameplayRequestPickerItem(
                record.StableId,
                string.IsNullOrWhiteSpace(record.DisplayName) ? FormatRequestTitle(record.StableId) : record.DisplayName,
                subtitle,
                search,
                "Projectiles / " + group,
                CharacterActionGameplayRequestPickerItemKind.Projectile,
                favorites.Contains(record.StableId),
                recent.Contains(record.StableId));
        }

        private static CharacterActionGameplayRequestPickerItem CreateGameplayItem(
            string requestId,
            ISet<string> favorites,
            ISet<string> recent)
        {
            if (TryCreateKnownGameplayItem(requestId, favorites, recent, out CharacterActionGameplayRequestPickerItem known))
                return known;

            return new CharacterActionGameplayRequestPickerItem(
                requestId,
                FormatRequestTitle(requestId),
                requestId,
                requestId + " " + FormatRequestTitle(requestId),
                "Gameplay Requests",
                CharacterActionGameplayRequestPickerItemKind.Gameplay,
                favorites.Contains(requestId),
                recent.Contains(requestId));
        }

        private static bool TryCreateKnownGameplayItem(
            string requestId,
            ISet<string> favorites,
            ISet<string> recent,
            out CharacterActionGameplayRequestPickerItem item)
        {
            requestId = requestId ?? string.Empty;
            if (string.Equals(requestId, "character_test.arc_mine.deploy", StringComparison.Ordinal))
            {
                string subtitle =
                    "投掷地雷 | 抛物线飞行体命中后转下落 | 落地布雷 | 入圈后 60f 爆炸 | 触发 r1.25m | 爆炸 r2.25m";
                item = new CharacterActionGameplayRequestPickerItem(
                    requestId,
                    "投掷地雷 / Arc Mine",
                    subtitle,
                    requestId + " arc mine throw projectile carrier drop fuse trap deploy trigger blast damage 投掷 地雷 引信 爆炸",
                    "玩法请求 / 陷阱",
                    CharacterActionGameplayRequestPickerItemKind.Gameplay,
                    favorites.Contains(requestId),
                    recent.Contains(requestId));
                return true;
            }

            item = null;
            return false;
        }

        private static CharacterActionGameplayRequestPickerItem CreateCurrentUnknownItem(
            string requestId,
            ISet<string> favorites,
            ISet<string> recent)
        {
            return new CharacterActionGameplayRequestPickerItem(
                requestId,
                FormatRequestTitle(requestId),
                "Current value; not found in the projectile catalog or action library.",
                requestId,
                "Current / Unknown",
                CharacterActionGameplayRequestPickerItemKind.CurrentUnknown,
                favorites.Contains(requestId),
                recent.Contains(requestId));
        }

        private static string ResolveProjectileGroup(
            CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record)
        {
            if (ContainsProjectileFamily(record, "meteor"))
                return "Meteor";

            if (!string.IsNullOrWhiteSpace(record?.DamageTypeId))
                return CharacterActionDamageAuthoringText.FormatDamageTypeLabel(record.DamageTypeId);

            string[] tags = (record?.Tags ?? string.Empty)
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tags.Length; i++)
            {
                string tag = tags[i].Trim();
                if (string.IsNullOrWhiteSpace(tag) || IsGenericProjectileTag(tag))
                    continue;
                return ToTitleCase(tag.Replace('_', ' '));
            }

            return "Other";
        }

        private static bool ContainsProjectileFamily(
            CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record,
            string token)
        {
            if (record == null || string.IsNullOrWhiteSpace(token))
                return false;

            return ContainsToken(record.StableId, token)
                || ContainsToken(record.DisplayName, token)
                || ContainsToken(record.Tags, token)
                || ContainsToken(record.AnalysisCategory, token);
        }

        private static bool ContainsToken(string value, string token)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsGenericProjectileTag(string tag)
        {
            return string.Equals(tag, "projectile", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tag, "area", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tag, "magic", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tag, "poise", StringComparison.OrdinalIgnoreCase);
        }

        private static List<CharacterActionGameplayRequestPickerItem> Filter(
            IReadOnlyList<CharacterActionGameplayRequestPickerItem> items,
            Func<CharacterActionGameplayRequestPickerItem, bool> predicate,
            HashSet<string> shown)
        {
            var result = new List<CharacterActionGameplayRequestPickerItem>();
            for (int i = 0; i < items.Count; i++)
            {
                CharacterActionGameplayRequestPickerItem item = items[i];
                if (item == null || shown.Contains(item.RequestId) || !predicate(item))
                    continue;
                shown.Add(item.RequestId);
                result.Add(item);
            }

            result.Sort(CompareItems);
            return result;
        }

        private static void AddSection(
            List<CharacterActionGameplayRequestPickerSection> sections,
            string title,
            List<CharacterActionGameplayRequestPickerItem> items)
        {
            if (items == null || items.Count == 0)
                return;
            sections.Add(new CharacterActionGameplayRequestPickerSection(title, items));
        }

        private static bool Matches(CharacterActionGameplayRequestPickerItem item, string query)
        {
            if (item == null)
                return false;
            string haystack = (item.RequestId + " " + item.Title + " " + item.Subtitle + " " + item.SearchText)
                .ToLowerInvariant();
            string[] tokens = query.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (haystack.IndexOf(tokens[i], StringComparison.Ordinal) < 0)
                    return false;
            }

            return true;
        }

        private static int MatchScore(CharacterActionGameplayRequestPickerItem item, string query)
        {
            if (item == null || string.IsNullOrWhiteSpace(query))
                return 0;
            string normalized = query.Trim().ToLowerInvariant();
            string id = item.RequestId.ToLowerInvariant();
            string title = item.Title.ToLowerInvariant();
            if (string.Equals(id, normalized, StringComparison.Ordinal))
                return 100;
            if (string.Equals(title, normalized, StringComparison.Ordinal))
                return 90;
            if (id.StartsWith(normalized, StringComparison.Ordinal))
                return 80;
            if (title.StartsWith(normalized, StringComparison.Ordinal))
                return 70;
            if (id.IndexOf(normalized, StringComparison.Ordinal) >= 0)
                return 60;
            if (title.IndexOf(normalized, StringComparison.Ordinal) >= 0)
                return 50;
            return 10;
        }

        private static int CompareItems(
            CharacterActionGameplayRequestPickerItem left,
            CharacterActionGameplayRequestPickerItem right)
        {
            int section = string.Compare(left?.Section, right?.Section, StringComparison.OrdinalIgnoreCase);
            if (section != 0)
                return section;
            return string.Compare(left?.Title, right?.Title, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatRequestTitle(string requestId)
        {
            requestId = requestId ?? string.Empty;
            if (requestId.StartsWith("character_test.", StringComparison.Ordinal))
                requestId = requestId.Substring("character_test.".Length);
            requestId = requestId.Replace(".projectile", string.Empty);
            requestId = requestId.Replace('.', ' ').Replace('_', ' ');
            return ToTitleCase(requestId);
        }

        private static string ToTitleCase(string value)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length == 0)
                return string.Empty;
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
        }
    }

    internal sealed class CharacterActionGameplayRequestPickerWindow : EditorWindow
    {
        private readonly List<CharacterActionGameplayRequestPickerItem> _items =
            new List<CharacterActionGameplayRequestPickerItem>();
        private readonly List<CharacterActionGameplayRequestPickerItem> _visibleItems =
            new List<CharacterActionGameplayRequestPickerItem>();
        private string _scope = string.Empty;
        private string _query = string.Empty;
        private Action<string> _selected;
        private ToolbarSearchField _search;
        private ScrollView _scroll;

        public static void Show(
            Rect activatorRect,
            string scope,
            IEnumerable<CharacterActionGameplayRequestPickerItem> items,
            Action<string> selected)
        {
            CharacterActionGameplayRequestPickerWindow window =
                CreateInstance<CharacterActionGameplayRequestPickerWindow>();
            window.titleContent = new GUIContent("Select Request ID");
            window.Initialize(scope, items, selected);
            window.ShowAsDropDown(activatorRect, new Vector2(560f, 520f));
        }

        private void Initialize(
            string scope,
            IEnumerable<CharacterActionGameplayRequestPickerItem> items,
            Action<string> selected)
        {
            _scope = scope ?? string.Empty;
            _selected = selected;
            _items.Clear();
            if (items != null)
                _items.AddRange(items);
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;

            var header = new Label("Select Gameplay Request ID");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 6f;
            rootVisualElement.Add(header);

            _search = new ToolbarSearchField();
            _search.RegisterValueChangedCallback(evt =>
            {
                _query = evt.newValue ?? string.Empty;
                RebuildList();
            });
            rootVisualElement.Add(_search);

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1f;
            _scroll.style.marginTop = 6f;
            rootVisualElement.Add(_scroll);

            RebuildList();
            _search.schedule.Execute(() => _search.Focus());
        }

        private void RebuildList()
        {
            if (_scroll == null)
                return;
            _scroll.Clear();
            _visibleItems.Clear();

            List<CharacterActionGameplayRequestPickerSection> sections =
                CharacterActionGameplayRequestPickerCatalog.BuildSections(_items, _query);
            if (sections.Count == 0)
            {
                _scroll.Add(new HelpBox("No matching request ID.", HelpBoxMessageType.Info));
                return;
            }

            for (int i = 0; i < sections.Count; i++)
            {
                CharacterActionGameplayRequestPickerSection section = sections[i];
                var sectionLabel = new Label(section.Title);
                sectionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                sectionLabel.style.marginTop = 8f;
                sectionLabel.style.marginBottom = 3f;
                _scroll.Add(sectionLabel);

                for (int j = 0; j < section.Items.Count; j++)
                    AddRow(section.Items[j]);
            }
        }

        private void AddRow(CharacterActionGameplayRequestPickerItem item)
        {
            if (item == null)
                return;
            _visibleItems.Add(item);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4f;
            row.style.paddingRight = 4f;
            row.style.paddingTop = 3f;
            row.style.paddingBottom = 3f;
            row.style.marginBottom = 2f;
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = new Color(1f, 1f, 1f, 0.08f);
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;
                Select(item);
                evt.StopPropagation();
            });

            var favorite = new Button(() => ToggleFavorite(item))
            {
                text = item.IsFavorite ? "*" : "+",
                tooltip = item.IsFavorite ? "Remove favorite" : "Add favorite",
            };
            favorite.style.width = 24f;
            favorite.style.marginRight = 4f;
            row.Add(favorite);

            var text = new VisualElement();
            text.style.flexGrow = 1f;
            var title = new Label(item.Title);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            text.Add(title);
            var subtitle = new Label(item.Subtitle);
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            subtitle.style.opacity = 0.78f;
            text.Add(subtitle);
            row.Add(text);

            _scroll.Add(row);
        }

        private void ToggleFavorite(CharacterActionGameplayRequestPickerItem item)
        {
            item.IsFavorite = !item.IsFavorite;
            CharacterActionGameplayRequestPickerCatalog.SetFavorite(_scope, item.RequestId, item.IsFavorite);
            RebuildList();
        }

        private void Select(CharacterActionGameplayRequestPickerItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.RequestId))
                return;
            CharacterActionGameplayRequestPickerCatalog.RecordRecent(_scope, item.RequestId);
            _selected?.Invoke(item.RequestId);
            Close();
        }
    }

}
