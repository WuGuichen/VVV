using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal sealed class CharacterActionTimelinePickerItem
    {
        public CharacterActionTimelinePickerItem(
            string id,
            string title,
            string subtitle,
            string searchText,
            bool isFavorite,
            bool isRecent,
            bool isDirty = false)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            SearchText = searchText ?? string.Empty;
            IsFavorite = isFavorite;
            IsRecent = isRecent;
            IsDirty = isDirty;
        }

        public string Id { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string SearchText { get; }
        public bool IsFavorite { get; }
        public bool IsRecent { get; }
        public bool IsDirty { get; }
    }

    internal sealed class CharacterActionTimelineHost
    {
        public Action SaveRequested { get; set; }
        public Func<bool> CanSaveProvider { get; set; }
        public Func<string> DirtyStateProvider { get; set; }
        public Func<string> CurrentItemLabelProvider { get; set; }
        public Func<string> CurrentItemIdProvider { get; set; }
        public Func<IReadOnlyList<CharacterActionTimelinePickerItem>> PickerItemsProvider { get; set; }
        public Action<string> PickerItemSelected { get; set; }
        public Action<string, bool> PickerFavoriteChanged { get; set; }
        public Action<string> PickerRecentRecorded { get; set; }
        public Action<string> NavigateToItemProvider { get; set; }
        public Action<VisualElement> PickerFooterBuilder { get; set; }
        public Action<DropdownMenu, int> AddMenuProvider { get; set; }
        public Action<DropdownMenu> MoreMenuProvider { get; set; }
        public Action<Action, bool> CommitFieldChange { get; set; }
        public Action<Action> CommitStructuralChange { get; set; }
        public Action<string> SelectionChanged { get; set; }
        public Action RequestTracksRefresh { get; set; }
        public Action<TimelineTrackKind, int> AddEventAtTrack { get; set; }
        public Action<int> DurationFramesChanged { get; set; }
        public Action BeginGesture { get; set; }
        public Action EndGesture { get; set; }
        public Func<string> CharacterStableIdProvider { get; set; }
        public int DefaultDurationFrames { get; set; } = 8;

        public string CharacterStableId => CharacterStableIdProvider?.Invoke() ?? string.Empty;

        public void CommitField(Action mutate, bool recordUndo = true)
        {
            CommitFieldChange?.Invoke(mutate, recordUndo);
        }

        public void CommitStructural(Action mutate)
        {
            CommitStructuralChange?.Invoke(mutate);
        }
    }
}
