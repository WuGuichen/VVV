using System.Collections.Generic;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private readonly List<Fgui.GTextField> _wholeTimelineHeaders = new List<Fgui.GTextField>();
        private string _wholeLocateKey;
        private void LocateWholeCardSelection()
        {
            var session = _recipeSource?.CompositionEditor;
            if (_recipeSource?.TacticalPreview?.WholeCard == null || session == null || _recipeSource.InspectCompiledExecution) { _wholeLocateKey = null; return; }
            string key = session.CardId + "/" + session.SelectedNodeId;
            if (key == _wholeLocateKey) return;
            _wholeLocateKey = key;
            float offset = 0;
            for (int i = 0; i < _recipeProjection.TimelineCount; i++)
            {
                var timeline = _recipeProjection.GetTimeline(i);
                for (int j = 0; j < timeline.TrackCount; j++)
                    if (timeline.GetTrack(j).Event?.SourceHighlighted == true)
                    {
                        _recipeVerticalOffset = offset; _recipeSource.VerticalScrollOffset = offset; return;
                    }
                offset += RecipeTimelineVisualHeight(timeline);
            }
        }
        private void DrawWholeTimelineHeader(int index, string title, float y, float width)
        {
            if (_wholeTimelineHeaders.Count > 0 && _wholeTimelineHeaders[0].isDisposed) _wholeTimelineHeaders.Clear();
            while (_wholeTimelineHeaders.Count <= index)
            {
                var label = NewRecipeText("wholeTimelineHeader" + _wholeTimelineHeaders.Count, "", 13,
                    new Color32(153, 193, 211, 255), Fgui.AlignType.Left, false);
                _recipeViewport.AddChild(label); _wholeTimelineHeaders.Add(label);
            }
            var header = _wholeTimelineHeaders[index];
            header.SetXY(RecipeContentInset + 4, y); header.SetSize(width - 8, 22);
            header.text = title;
            header.visible = y + 24 > 0 && y < _recipeViewport.height;
        }
    }
}
