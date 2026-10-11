using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Projects live UI interaction semantics onto the shared game cursor.</summary>
    [DefaultExecutionOrder(10000)]
    internal sealed class CharacterTestFairyGuiCursor : MonoBehaviour
    {
        internal const string Default = "wgame.ui.default";
        internal const string Pointer = "wgame.ui.pointer";
        internal const string Grab = "wgame.ui.grab";
        internal const string Grabbing = "wgame.ui.grabbing";
        internal const string Unavailable = "wgame.ui.unavailable";
        private static readonly string[] Names = { Default, Pointer, Grab, Grabbing, Unavailable };
        private static readonly string[] Resources = { "Default", "Pointer", "Grab", "Grabbing", "Unavailable" };
        private static readonly Vector2[] Hotspots =
        {
            new Vector2(4, 4), new Vector2(16, 4), new Vector2(24, 24),
            new Vector2(24, 24), new Vector2(4, 4)
        };
        private static CharacterTestFairyGuiCursor _active;
        private static TacticalCard.TacticalBattlePreparationController _sceneController;
        private Fgui.Stage _stage;
        private CharacterTestFairyGuiClickFeedback _clickFeedback;
        private object _placementOwner;
        private string _placementCursor;

        internal static void BindScene(TacticalCard.TacticalBattlePreparationController controller)
            => _sceneController = controller;

        internal static void UnbindScene(TacticalCard.TacticalBattlePreparationController controller)
        {
            if (_sceneController == controller) _sceneController = null;
        }

        internal static void SetPlacement(object owner, bool dragging, bool valid)
        {
            if (_active == null) return;
            _active._placementOwner = owner;
            _active._placementCursor = !valid ? Unavailable : dragging ? Grabbing : Pointer;
        }

        internal static void ClearPlacement(object owner)
        {
            if (_active == null || !ReferenceEquals(_active._placementOwner, owner)) return;
            _active._placementOwner = null;
            _active._placementCursor = null;
        }

        private void OnEnable() => _active = this;

        private void OnDisable()
        {
            _clickFeedback?.Dispose();
            _clickFeedback = null;
            _placementOwner = null;
            _placementCursor = null;
            if (_active != this) return;
            _active = null;
            if (_stage != null && !_stage.isDisposed)
            {
                for (int i = 0; i < Names.Length; i++)
                    if (_stage.cursor == Names[i]) { _stage.cursor = null; break; }
            }
            _stage = null;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            Fgui.Stage stage = Fgui.Stage.inst;
            if (_stage != stage && !RegisterStage(stage)) return;

            string desired;
            if (_placementOwner != null) desired = _placementCursor;
            else if (Fgui.GObject.draggingObject != null) desired = Grabbing;
            else
            {
                // Non-touch hit testing also sees disabled controls; normal hit testing skips them.
                Fgui.DisplayObject visual = stage.HitTest(stage.touchPosition, false);
                desired = Resolve(visual, true) == Unavailable ? Unavailable : Resolve(stage.touchTarget, false);
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (_sceneController != null && mouse != null)
                {
                    bool overUi = stage.touchTarget != null && stage.touchTarget != stage;
                    string sceneCursor = _sceneController.ResolveSceneCursor(mouse.position.ReadValue(), overUi);
                    if (sceneCursor != null) desired = sceneCursor;
                }
            }
            if (stage.cursor != desired || stage.activeCursor != desired) stage.cursor = desired;
        }

        /// <remarks>LowFreqAlloc: textures are registered only when the Stage instance changes.</remarks>
        private bool RegisterStage(Fgui.Stage stage)
        {
            for (int i = 0; i < Names.Length; i++)
            {
                Texture2D texture = UnityEngine.Resources.Load<Texture2D>("MxFramework/UI/Cursors/" + Resources[i]);
                if (texture == null) return false;
                stage.RegisterCursor(Names[i], texture, Hotspots[i]);
            }
            _stage = stage;
            _clickFeedback?.Dispose();
            _clickFeedback = new CharacterTestFairyGuiClickFeedback(stage);
            return true;
        }

        internal static string Resolve(Fgui.DisplayObject hit, bool disabledOnly)
        {
            string result = Default;
            for (Fgui.DisplayObject current = hit; current != null; current = current.parent)
            {
                Fgui.GObject owner = current.gOwner;
                if (owner == null) continue;
                bool actionable = owner is Fgui.GButton || owner.draggable || owner.hasEventListeners("onClick");
                if (owner.grayed && actionable) return Unavailable;
                if (disabledOnly || result != Default) continue;
                if (!string.IsNullOrEmpty(owner.cursor)) result = owner.cursor;
                else if (owner.draggable && owner.touchable) result = Grab;
                else if (actionable && owner.touchable) result = Pointer;
            }
            return result;
        }
    }
}
