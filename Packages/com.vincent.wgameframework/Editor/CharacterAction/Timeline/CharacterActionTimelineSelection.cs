namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal readonly struct CharacterActionTimelineSelection
    {
        public CharacterActionTimelineSelection(string itemId, TimelineTrackKind trackKind, int index)
        {
            ItemId = itemId ?? string.Empty;
            TrackKind = trackKind;
            Index = index;
        }

        public string ItemId { get; }
        public TimelineTrackKind TrackKind { get; }
        public int Index { get; }
        public bool IsValid => !string.IsNullOrEmpty(ItemId);

        public static CharacterActionTimelineSelection Empty => new CharacterActionTimelineSelection(string.Empty, TimelineTrackKind.Phases, -1);

        public static bool TryParse(string itemId, out CharacterActionTimelineSelection selection)
        {
            selection = Empty;
            if (!CharacterActionTimelineIdentity.TryParseTrackKind(itemId, out TimelineTrackKind trackKind))
                return false;

            int colon = itemId.IndexOf(':');
            int index = -1;
            if (colon >= 0 && colon < itemId.Length - 1)
                int.TryParse(itemId.Substring(colon + 1), out index);
            selection = new CharacterActionTimelineSelection(itemId, trackKind, index);
            return true;
        }
    }
}
