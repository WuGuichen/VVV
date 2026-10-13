using UnityEngine;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal enum TimelineItemKind
    {
        Clip,
        Marker,
    }

    internal enum TimelineTrackKind
    {
        Phases,
        TransitionWindow,
        Motion,
        Animation,
        Combat,
        Gameplay,
        Presentation,
        MovementControl,
        RotationControl,
    }

    internal interface ITimelineItemAdapter
    {
        string Id { get; }
        TimelineTrackKind TrackKind { get; }
        TimelineItemKind ItemKind { get; }
        int StartFrame { get; }
        int EndFrame { get; }
        string Title { get; }
        string FullTitle { get; }
        Color Color { get; }
        bool CanEdit { get; }
        bool TryGetCurrentIndex(out int index);
        void SetRange(int startFrame, int endFrame);
        void Remove();
    }

    internal interface IVfxTimelineClipAdapter
    {
        bool AllowsTailBeyondAction { get; }
        void SetVfxRange(int startFrame, int endFrame, bool trimSource);
    }

    internal interface IAnimationTimelineClipAdapter
    {
        void SetAnimationRange(
            int startFrame,
            int endFrame,
            bool trimSource,
            int actionDurationFrames,
            string characterStableId);
    }
}
