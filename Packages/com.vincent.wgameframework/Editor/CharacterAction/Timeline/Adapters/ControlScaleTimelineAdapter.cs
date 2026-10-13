using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction.Timeline.Adapters
{
    internal sealed class ControlScaleTimelineAdapter : ITimelineItemAdapter
    {
        private readonly CharacterActionJsonDto _action;
        private readonly ControlScaleKeyJsonDto _key;
        public ControlScaleTimelineAdapter(CharacterActionJsonDto action, ControlScaleKeyJsonDto key, bool rotation, bool canEdit)
        { _action = action; _key = key; Rotation = rotation; CanEdit = canEdit; }
        public bool Rotation { get; }
        public ControlScaleKeyJsonDto Key => _key;
        public string Id => CharacterActionTimelineIdentity.TrackPrefix(TrackKind) + ":" +
            (_key.StableEventId > 0 ? _key.StableEventId.ToString(CultureInfo.InvariantCulture) : _key.StableEventLabel);
        public TimelineTrackKind TrackKind => Rotation ? TimelineTrackKind.RotationControl : TimelineTrackKind.MovementControl;
        public TimelineItemKind ItemKind => TimelineItemKind.Marker;
        public int StartFrame => _key.Frame;
        public int EndFrame => _key.Frame;
        public string Title => _key.Value.ToString("0.###", CultureInfo.InvariantCulture);
        public string FullTitle => (Rotation ? "旋转" : "移动") + "控制倍率：" + Title + "，从第 " + StartFrame + " 帧生效";
        public Color Color => StartFrame < 0 || StartFrame >= _action.DurationFrames
            ? new Color(0.9f, 0.35f, 0.3f)
            : Rotation ? new Color(0.45f, 0.68f, 0.93f) : new Color(0.35f, 0.78f, 0.6f);
        public bool CanEdit { get; }
        public bool TryGetCurrentIndex(out int index)
        { index = Array.IndexOf(CharacterActionControlScaleAuthoring.Keys(_action, Rotation), _key); return index >= 0; }
        public void SetRange(int startFrame, int endFrame)
        {
            foreach (var other in CharacterActionControlScaleAuthoring.Keys(_action, Rotation))
                if (!ReferenceEquals(other, _key) && other.Frame == startFrame) return;
            _key.Frame = startFrame;
        }
        public void Remove()
        {
            var keys = new List<ControlScaleKeyJsonDto>(CharacterActionControlScaleAuthoring.Keys(_action, Rotation));
            keys.Remove(_key);
            CharacterActionControlScaleAuthoring.SetKeys(_action, Rotation, keys.ToArray());
        }
    }
}
