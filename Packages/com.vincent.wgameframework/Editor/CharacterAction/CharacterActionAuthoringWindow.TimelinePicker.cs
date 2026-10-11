using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private const int TimelinePickerRecentLimit = 10;
        private const string TimelinePickerEditorPrefsPrefix =
            "MxFramework.CharacterActionAuthoringWindow.TimelinePicker.";
        private const string TimelinePickerModeCharacterAction = "CharacterAction";
        private const string TimelinePickerModeAbilityEffectTimeline = "AbilityEffectTimeline";

        private string BuildCharacterActionTimelinePickerLabel()
        {
            CharacterActionJsonDto action = _model?.SelectedAction;
            string id = CharacterActionTimelinePickerActionId(action);
            return "\u52a8\u4f5c: " + (string.IsNullOrWhiteSpace(id) ? "\uff08\u65e0\uff09" : id);
        }

        private string CurrentCharacterActionTimelinePickerItemId()
        {
            return CharacterActionTimelinePickerActionId(_model?.SelectedAction);
        }

        private IReadOnlyList<CharacterActionTimelinePickerItem> BuildCharacterActionTimelinePickerItems()
        {
            var items = new List<CharacterActionTimelinePickerItem>();
            List<CharacterActionJsonDto> actions = _model?.Actions;
            if (actions == null)
                return items;

            string scope = CharacterActionTimelinePickerScope();
            HashSet<string> favorites = LoadTimelinePickerSet(
                TimelinePickerModeCharacterAction,
                scope,
                "favorites");
            HashSet<string> recent = LoadTimelinePickerSet(
                TimelinePickerModeCharacterAction,
                scope,
                "recent");

            for (int i = 0; i < actions.Count; i++)
            {
                CharacterActionJsonDto action = actions[i];
                string id = CharacterActionTimelinePickerActionId(action);
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                string title = id;
                if (!string.IsNullOrWhiteSpace(action?.DisplayName)
                    && !string.Equals(action.DisplayName, id, StringComparison.Ordinal))
                {
                    title += " - " + action.DisplayName;
                }

                string subtitle = "#" + (action?.ActionId ?? 0).ToString(CultureInfo.InvariantCulture)
                    + " | " + (string.IsNullOrWhiteSpace(action?.Category) ? "Uncategorized" : action.Category)
                    + " | " + CharacterActionTimelinePickerDurationText(action);
                if (action?.Tags != null && action.Tags.Length > 0)
                    subtitle += " | " + string.Join(", ", action.Tags);

                string search = id + " " + (action == null ? string.Empty : action.ActionId.ToString(CultureInfo.InvariantCulture))
                    + " " + action?.DisplayName
                    + " " + action?.Category
                    + " " + string.Join(" ", action?.Tags ?? Array.Empty<string>());
                items.Add(new CharacterActionTimelinePickerItem(
                    id,
                    title,
                    subtitle,
                    search,
                    favorites.Contains(id),
                    recent.Contains(id),
                    _actionLibraryDirty));
            }

            return items;
        }

        private void SelectCharacterActionTimelinePickerItem(string itemId)
        {
            if (_model?.Actions == null || string.IsNullOrWhiteSpace(itemId))
                return;

            for (int i = 0; i < _model.Actions.Count; i++)
            {
                if (!string.Equals(CharacterActionTimelinePickerActionId(_model.Actions[i]), itemId, StringComparison.Ordinal))
                    continue;

                if (_model.SelectedActionIndex == i)
                    return;

                SelectActionAndRefresh(_model.Actions[i].ActionId, i);
                return;
            }
        }

        private void NavigateToCharacterActionTimelineItem(string itemId)
        {
            if (_model?.Actions == null || string.IsNullOrWhiteSpace(itemId))
                return;

            for (int i = 0; i < _model.Actions.Count; i++)
            {
                if (!string.Equals(CharacterActionTimelinePickerActionId(_model.Actions[i]), itemId, StringComparison.Ordinal))
                    continue;

                _model.SelectedActionIndex = i;
                RecordCurrentActionRecentActivity();
                _pendingTimelineSelectedItemId = string.Empty;
                SaveSessionState();
                SetTab(AuthoringTab.ActionSet);
                return;
            }
        }

        private string BuildAbilityEffectTimelinePickerLabel()
        {
            return "\u65f6\u95f4\u8f74: " + (string.IsNullOrWhiteSpace(_selectedAbilityEffectTimelineId)
                ? "\uff08\u65e0\uff09"
                : _selectedAbilityEffectTimelineId);
        }

        private IReadOnlyList<CharacterActionTimelinePickerItem> BuildAbilityEffectTimelinePickerItems()
        {
            var items = new List<CharacterActionTimelinePickerItem>();
            if (_abilityEffectTimelines == null)
                return items;

            string scope = AbilityEffectTimelinePickerScope();
            HashSet<string> favorites = LoadTimelinePickerSet(
                TimelinePickerModeAbilityEffectTimeline,
                scope,
                "favorites");
            HashSet<string> recent = LoadTimelinePickerSet(
                TimelinePickerModeAbilityEffectTimeline,
                scope,
                "recent");

            for (int i = 0; i < _abilityEffectTimelines.Count; i++)
            {
                AbilityEffectTimelineDraft timeline = _abilityEffectTimelines[i];
                string id = timeline?.TimelineId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                int eventCount = timeline.Events?.Count ?? 0;
                string subtitle = "Duration " + Math.Max(1, timeline.DurationFrames).ToString(CultureInfo.InvariantCulture)
                    + "f | " + eventCount.ToString(CultureInfo.InvariantCulture)
                    + (eventCount == 1 ? " event" : " events");
                string search = BuildAbilityEffectTimelinePickerSearchText(timeline);
                items.Add(new CharacterActionTimelinePickerItem(
                    id,
                    id,
                    subtitle,
                    search,
                    favorites.Contains(id),
                    recent.Contains(id),
                    _abilityEffectTimelineDirty));
            }

            return items;
        }

        private void SelectAbilityEffectTimelinePickerItem(string timelineId)
        {
            if (_abilityEffectTimelines == null || string.IsNullOrWhiteSpace(timelineId))
                return;

            for (int i = 0; i < _abilityEffectTimelines.Count; i++)
            {
                if (!string.Equals(_abilityEffectTimelines[i]?.TimelineId, timelineId, StringComparison.Ordinal))
                    continue;

                if (string.Equals(_selectedAbilityEffectTimelineId, timelineId, StringComparison.Ordinal))
                    return;

                _selectedAbilityEffectTimelineId = timelineId;
                _isRestoringAbilityTimelineSelection = true;
                try
                {
                    _timelineView?.SelectItem(string.Empty);
                }
                finally
                {
                    _isRestoringAbilityTimelineSelection = false;
                }

                _pendingTimelineSelectedItemId = string.Empty;
                SaveSessionState();
                RefreshUi(UiRefreshScope.Content);
                return;
            }
        }

        private void BuildAbilityEffectTimelinePickerFooter(VisualElement footer)
        {
            if (footer == null)
                return;

            footer.Add(new Button(AddAbilityEffectTimeline) { text = "New Timeline" });
            var delete = new Button(DeleteSelectedAbilityEffectTimeline) { text = "Delete" };
            delete.SetEnabled(GetSelectedAbilityEffectTimeline() != null);
            footer.Add(delete);
        }

        private void SetCharacterActionTimelinePickerFavorite(string itemId, bool favorite)
        {
            SetTimelinePickerFavorite(
                TimelinePickerModeCharacterAction,
                CharacterActionTimelinePickerScope(),
                itemId,
                favorite);
        }

        private void RecordCharacterActionTimelinePickerRecent(string itemId)
        {
            RecordTimelinePickerRecent(
                TimelinePickerModeCharacterAction,
                CharacterActionTimelinePickerScope(),
                itemId);
        }

        private void SetAbilityEffectTimelinePickerFavorite(string itemId, bool favorite)
        {
            SetTimelinePickerFavorite(
                TimelinePickerModeAbilityEffectTimeline,
                AbilityEffectTimelinePickerScope(),
                itemId,
                favorite);
        }

        private void RecordAbilityEffectTimelinePickerRecent(string itemId)
        {
            RecordTimelinePickerRecent(
                TimelinePickerModeAbilityEffectTimeline,
                AbilityEffectTimelinePickerScope(),
                itemId);
        }

        private static string CharacterActionTimelinePickerActionId(CharacterActionJsonDto action)
        {
            return action == null || action.ActionId <= 0
                ? string.Empty
                : action.ActionId.ToString(CultureInfo.InvariantCulture);
        }

        private static string CharacterActionTimelinePickerDurationText(CharacterActionJsonDto action)
        {
            return action?.DurationFrames.HasValue == true
                ? action.DurationFrames.Value.ToString(CultureInfo.InvariantCulture) + "f"
                : "default duration";
        }

        private string CharacterActionTimelinePickerScope()
        {
            return string.IsNullOrWhiteSpace(_actionLibrarySourceDirectory)
                ? "__unsaved_character_action_library__"
                : _actionLibrarySourceDirectory;
        }

        private string AbilityEffectTimelinePickerScope()
        {
            return string.IsNullOrWhiteSpace(_abilityAuthoringJsonPath)
                ? "__unsaved_ability_authoring_json__"
                : _abilityAuthoringJsonPath;
        }

        private static string BuildAbilityEffectTimelinePickerSearchText(AbilityEffectTimelineDraft timeline)
        {
            var builder = new StringBuilder();
            builder.Append(timeline?.TimelineId);
            List<AbilityEffectTimelineEventDraft> events = timeline?.Events;
            if (events == null)
                return builder.ToString();

            for (int i = 0; i < events.Count; i++)
            {
                AbilityEffectTimelineEventDraft evt = events[i];
                if (evt == null)
                    continue;

                builder.Append(' ')
                    .Append(evt.EventId)
                    .Append(' ')
                    .Append(evt.TrackKind)
                    .Append(' ')
                    .Append(evt.EventKind)
                    .Append(' ')
                    .Append(evt.TargetTimelineId)
                    .Append(' ')
                    .Append(evt.RequestId)
                    .Append(' ')
                    .Append(evt.PresentationProfileId);
            }

            return builder.ToString();
        }

        private HashSet<string> LoadTimelinePickerSet(string mode, string source, string suffix)
        {
            return new HashSet<string>(
                LoadTimelinePickerList(mode, source, suffix),
                StringComparer.Ordinal);
        }

        private List<string> LoadTimelinePickerList(string mode, string source, string suffix)
        {
            string json = EditorPrefs.GetString(TimelinePickerPrefsKey(mode, source, suffix), string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return new List<string>();

            try
            {
                return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        private void SaveTimelinePickerList(string mode, string source, string suffix, List<string> ids)
        {
            ids ??= new List<string>();
            EditorPrefs.SetString(
                TimelinePickerPrefsKey(mode, source, suffix),
                JsonConvert.SerializeObject(ids));
        }

        private void SetTimelinePickerFavorite(string mode, string source, string itemId, bool favorite)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            List<string> ids = LoadTimelinePickerList(mode, source, "favorites");
            ids.RemoveAll(id => string.Equals(id, itemId, StringComparison.Ordinal));
            if (favorite)
                ids.Add(itemId);
            SaveTimelinePickerList(mode, source, "favorites", ids);
        }

        private void RecordTimelinePickerRecent(string mode, string source, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            List<string> ids = LoadTimelinePickerList(mode, source, "recent");
            ids.RemoveAll(id => string.Equals(id, itemId, StringComparison.Ordinal));
            ids.Insert(0, itemId);
            while (ids.Count > TimelinePickerRecentLimit)
                ids.RemoveAt(ids.Count - 1);
            SaveTimelinePickerList(mode, source, "recent", ids);
        }

        private static string TimelinePickerPrefsKey(string mode, string source, string suffix)
        {
            string scope = Application.dataPath + "|" + (mode ?? string.Empty) + "|" + (source ?? string.Empty);
            return TimelinePickerEditorPrefsPrefix + StableHash(scope) + "." + suffix;
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                value ??= string.Empty;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash.ToString("x8", CultureInfo.InvariantCulture);
            }
        }
    }
}
