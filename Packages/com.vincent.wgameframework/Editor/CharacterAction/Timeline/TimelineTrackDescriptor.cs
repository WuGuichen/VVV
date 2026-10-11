using UnityEngine;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal sealed class TimelineTrackDescriptor
    {
        public TimelineTrackDescriptor(
            TimelineTrackKind kind,
            string title,
            Color color,
            bool isEnabled)
        {
            Kind = kind;
            Title = title;
            Color = color;
            IsEnabled = isEnabled;
        }

        public TimelineTrackKind Kind { get; }
        public string Title { get; }
        public Color Color { get; }
        public bool IsEnabled { get; }
    }
}
