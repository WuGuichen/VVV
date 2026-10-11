using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiMainMenuBinder
    {
        private static readonly string[] HomepageCursorNames =
        {
            "wgame.home.default", "wgame.home.pointer", "wgame.home.unavailable"
        };
        private static readonly string[] HomepageCursorResources = { "Default", "Pointer", "Unavailable" };
        private static readonly Vector2[] HomepageCursorHotspots =
        {
            new Vector2(4f, 4f), new Vector2(16f, 4f), new Vector2(4f, 4f)
        };
        private Fgui.Stage _homepageCursorStage;
        private string _previousHomepageCursor;
        private bool _homepageCursorOwned;

        internal void UpdateHomepageCursor()
        {
            if (!Application.isPlaying || _homepage == null || _homepage.isDisposed || !_homepage.onStage)
                return;
            Fgui.Stage stage = Fgui.Stage.inst;
            if (_homepageCursorStage != stage)
            {
                for (int i = 0; i < HomepageCursorNames.Length; i++)
                {
                    Texture2D texture = UnityEngine.Resources.Load<Texture2D>("MxFramework/UI/Cursors/" + HomepageCursorResources[i]);
                    if (texture == null) return;
                    stage.RegisterCursor(HomepageCursorNames[i], texture, HomepageCursorHotspots[i]);
                }
                _homepageCursorStage = stage;
            }

            // Other windows (for example Settings) retain their own cursor policy.
            Fgui.DisplayObject target = stage.touchTarget;
            var homeContainer = _homepage.displayObject as Fgui.Container;
            if (target != null && target != homeContainer && !homeContainer.IsAncestorOf(target))
            {
                ReleaseHomepageCursor();
                return;
            }
            if (!_homepageCursorOwned)
            {
                _previousHomepageCursor = stage.cursor;
                _homepageCursorOwned = true;
            }
            string desired = ResolveHomepageCursor(stage.touchPosition);
            if (stage.cursor != desired) stage.cursor = desired;
        }

        internal string ResolveHomepageCursor(Vector2 stagePosition)
        {
            bool modal = _homepageChoice != HomepageChoice.None;
            // Cursor shape describes the available action; button press feedback stays on the button.
            foreach (HomepageFeedback feedback in _homepageFeedback)
            {
                Fgui.GButton button = feedback.Button;
                if (button.isDisposed || !button.visible || !button.onStage) continue;
                bool inConfirmation = button.parent == _homepageConfirmation;
                if (modal != inConfirmation) continue;
                Vector2 point = button.GlobalToLocal(stagePosition);
                if (point.x >= 0f && point.y >= 0f && point.x < button.width && point.y < button.height)
                    return HomepageCursorNames[button.enabled ? 1 : 2];
            }
            return HomepageCursorNames[0];
        }

        internal void ReleaseHomepageCursor()
        {
            if (!_homepageCursorOwned) return;
            if (_homepageCursorStage != null && !_homepageCursorStage.isDisposed)
            {
                string active = _homepageCursorStage.cursor;
                for (int i = 0; i < HomepageCursorNames.Length; i++)
                {
                    if (active != HomepageCursorNames[i]) continue;
                    _homepageCursorStage.cursor = _previousHomepageCursor;
                    // The pointer can be over the world, with no FairyGUI touch target to refresh it.
                    if (_previousHomepageCursor == null) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    break;
                }
            }
            _homepageCursorOwned = false;
            _previousHomepageCursor = null;
        }
    }
}
