using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline.Adapters;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal sealed partial class CharacterActionTimelineView
    {
        private const float ControlScaleRowHeight = 22f;
        private const float ControlScaleItemHeight = 16f;

        private void BuildControlScaleBand(VisualElement row, bool rotation)
        {
            row.Q<VisualElement>("control-scale-values")?.RemoveFromHierarchy();
            var band = new VisualElement { name = "control-scale-values", pickingMode = PickingMode.Ignore };
            band.style.position = Position.Absolute;
            band.style.left = 0;
            band.style.top = 0;
            band.style.width = row.style.width;
            band.style.height = ControlScaleRowHeight;
            var keys = new List<ControlScaleKeyJsonDto>(CharacterActionControlScaleAuthoring.Keys(_snapshot.Action, rotation));
            keys.RemoveAll(key => key == null);
            keys.Sort((left, right) => left.Frame.CompareTo(right.Frame));
            int start = 0;
            float value = 1f;
            for (int i = 0; i <= keys.Count; i++)
            {
                int end = i < keys.Count ? Math.Min(_snapshot.ActionDurationFrames, keys[i].Frame) : _snapshot.ActionDurationFrames;
                if (end > start)
                {
                    var segment = new Label(value.ToString("0.###", CultureInfo.InvariantCulture)) { pickingMode = PickingMode.Ignore };
                    segment.AddToClassList("ca-tl-control-segment");
                    segment.style.position = Position.Absolute;
                    segment.style.left = FrameStartX(start);
                    segment.style.width = Math.Max(1, (end - start) * _state.PixelsPerFrame);
                    segment.style.top = (ControlScaleRowHeight - ControlScaleItemHeight) * 0.5f;
                    segment.style.height = ControlScaleItemHeight;
                    segment.style.unityTextAlign = TextAnchor.MiddleLeft;
                    segment.style.paddingLeft = Math.Max(14f, _state.PixelsPerFrame * 0.5f + 8f);
                    segment.style.color = new Color(0.92f, 0.96f, 0.96f);
                    segment.style.backgroundColor = rotation ? new Color(0.28f, 0.43f, 0.62f, 0.7f) : new Color(0.23f, 0.5f, 0.39f, 0.7f);
                    segment.style.borderRightWidth = 1;
                    segment.style.borderRightColor = new Color(0.7f, 0.83f, 0.87f, 0.7f);
                    segment.style.overflow = Overflow.Hidden;
                    band.Add(segment);
                }
                if (i < keys.Count) { start = Math.Max(0, keys[i].Frame); value = keys[i].Value; }
            }
            row.Insert(0, band);
        }
    }
}
