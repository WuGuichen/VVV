namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal sealed class CharacterActionTimelineState
    {
        public float PixelsPerFrame { get; set; } = CharacterActionTimelineLayout.DefaultPixelsPerFrame;
        public bool SnapToFrame { get; set; } = true;
        public bool ShowMarkerTooltipText { get; set; }
        public string SelectedItemId { get; set; } = string.Empty;
        public int PlayheadFrame { get; set; }
        public float SavedPixelsPerFrame { get; set; } = CharacterActionTimelineLayout.DefaultPixelsPerFrame;
        public float SavedScrollOffsetX { get; set; }

        public void SaveViewState(float scrollOffsetX)
        {
            SavedPixelsPerFrame = PixelsPerFrame;
            SavedScrollOffsetX = scrollOffsetX;
        }

        public void RestoreViewState()
        {
            PixelsPerFrame = SavedPixelsPerFrame;
        }
    }
}
