using System.Collections.Generic;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private readonly Dictionary<RecipeTrackSlot, Fgui.GGraph> _timingStartHandles = new();
        private void BindTimingStartHandle(RecipeTrackSlot slot, BehaviorRecipeEventViewModel ev, float x)
        {
            bool visible = _recipeSource.TacticalPreview?.TimingPreview != null && ev.CanResize && _canEdit;
            if (!_timingStartHandles.TryGetValue(slot, out var handle))
            {
                if (!visible) return;
                handle = NewRecipeGraph("timingStartHandle", true); slot.Content.AddChild(handle); _timingStartHandles.Add(slot, handle);
                handle.onTouchBegin.Set(context => { BeginRecipeGesture(slot, true, context); if (_tacticalGesture != null) _tacticalGesture.StartResize = true; });
                handle.onTouchMove.Set(UpdateRecipeGesture); handle.onTouchEnd.Set(EndRecipeGesture);
            }
            handle.visible = visible; if (!visible) return;
            handle.SetXY(x - 3, RecipeEventY); handle.DrawRect(7, RecipeEventHeight, 0, Color.clear, new Color32(194, 225, 233, 180));
            handle.tooltips = "拖动开始时间；保留关联目标";
        }
    }
}
