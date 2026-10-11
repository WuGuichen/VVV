using System;
using UnityEngine;
using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalAuthoringLayoutBinder
    {
        private bool _libraryOpen;
        private string _libraryCard;

        private void ArrangeLibrary(Fgui.GComponent root, CharacterTestRuntimeAiLabTacticalAuthoringLayout layout)
        {
            if (_libraryCard != _compositionSession.CardId) { _libraryOpen = false; _libraryCard = _compositionSession.CardId; }
            var button = root.GetChild("btnWorkbenchEdit")?.asButton;
            var list = root.GetChild("workbenchEntryList")?.asList;
            if (button == null || list == null) return;
            button.visible = true; button.enabled = true; button.title = "卡库";
            float buttonWidth = _compositionSession.Controller?.TacticalCardTestMode == true ? 112 : 64;
            Geometry(button, new Rect(layout.Overview.xMax - buttonWidth - 12, layout.Overview.y + 16, buttonWidth, 34));
            HideOwned(button.GetChild("icon"));
            if (button.GetTextField() is Fgui.GTextField caption)
            {
                caption.SetXY(4, 0); caption.SetSize(buttonWidth - 8, 34);
                caption.align = Fgui.AlignType.Center; caption.verticalAlign = Fgui.VertAlignType.Middle;
                caption.textFormat.size = 14;
            }
            button.sortingOrder = 65;
            root.GetChild("inputProjectCardDisplayName")?.SetSize(layout.Overview.width - buttonWidth - 42, 42);
            button.onClick.Set(() => { _libraryOpen = !_libraryOpen; _rebind?.Invoke(); });
            list.visible = _libraryOpen;
            if (_libraryOpen)
            {
                list.sortingOrder = 75;
                Geometry(list, new Rect(layout.Overview.x + 8, layout.Overview.y + 64,
                    layout.Overview.width - 16, Mathf.Min(520, layout.Overview.height - 140)));
            }
            string[] actions = { "btnCreateBehaviorCard", CharacterTestTacticalPreviewControlContract.Template };
            for (int i = 0; i < actions.Length; i++)
            {
                var action = root.GetChild(actions[i]);
                if (action == null) continue;
                action.visible = _libraryOpen;
                if (_libraryOpen)
                {
                    action.sortingOrder = 75;
                    Geometry(action, new Rect(layout.Overview.x + 8 + i * (layout.Overview.width - 20) / 2,
                        list.y + list.height + 8, (layout.Overview.width - 24) / 2, 34));
                }
            }
        }
    }
}
