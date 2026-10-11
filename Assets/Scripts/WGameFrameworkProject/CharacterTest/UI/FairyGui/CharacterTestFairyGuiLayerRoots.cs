using System;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestFairyGuiLayerRoots
    {
        private static readonly MxUiLayer[] OrderedLayers =
        {
            MxUiLayer.Background,
            MxUiLayer.Hud,
            MxUiLayer.Panel,
            MxUiLayer.Popup,
            MxUiLayer.Modal,
            MxUiLayer.Toast,
            MxUiLayer.Debug
        };

        private static readonly MxFairyGuiLayerHost SharedHost = new MxFairyGuiLayerHost();

        public const string HudRootName = "MxFairyGuiLayer_Hud";
        public const string ToastRootName = "MxFairyGuiLayer_Toast";

        public static MxFairyGuiLayerHost SharedLayerHost => SharedHost;

        public static void EnsureLayerRoots(MxFairyGuiLayerHost layerHost = null)
        {
            // Player UI has its own clock even while the tactical world eases to a stop.
            Fgui.GTweener.defaultIgnoreEngineTimeScale = true;
            PruneInvalidLayerRoots();
            (layerHost ?? SharedHost).EnsureLayerRoots();
            NormalizeRootOrder(layerHost);
        }

        public static void ReleaseSharedLayerRoots()
        {
            SharedHost.Dispose();
        }

        public static bool TryGetLayerRoot(MxUiLayer layer, out Fgui.GComponent root)
        {
            EnsureLayerRoots();
            return SharedHost.TryGetLayerRoot(layer, out root);
        }

        public static void NormalizeRootOrder(MxFairyGuiLayerHost layerHost = null)
        {
            if (!TryGetGRoot(out Fgui.GRoot gRoot))
                return;

            PruneInvalidLayerRoots(gRoot);
            MxFairyGuiLayerHost host = layerHost ?? SharedHost;
            int layerIndex = 0;
            for (int i = 0; i < OrderedLayers.Length; i++)
            {
                Fgui.GObject root = null;
                if (!host.TryGetLayerRoot(OrderedLayers[i], out Fgui.GComponent layerRoot)
                    || layerRoot == null
                    || layerRoot.parent != gRoot)
                {
                    TryGetLiveLayerRoot(gRoot, OrderedLayers[i], out root);
                }
                else
                {
                    root = layerRoot;
                }

                if (root == null)
                    continue;

                int currentIndex = gRoot.GetChildIndex(root);
                if (currentIndex != layerIndex)
                    gRoot.SetChildIndex(root, layerIndex);

                layerIndex++;
            }
        }

        public static int GetLayerRootIndex(MxUiLayer layer)
        {
            if (!SharedHost.TryGetLayerRoot(layer, out Fgui.GComponent root) || root == null)
                return -1;

            Fgui.GRoot gRoot = root.parent as Fgui.GRoot;
            return gRoot != null ? gRoot.GetChildIndex(root) : -1;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            ReleaseSharedLayerRoots();
            PruneInvalidLayerRoots();
        }

        private static void PruneInvalidLayerRoots()
        {
            if (TryGetGRoot(out Fgui.GRoot gRoot))
                PruneInvalidLayerRoots(gRoot);
        }

        private static void PruneInvalidLayerRoots(Fgui.GRoot gRoot)
        {
            if (gRoot == null)
                return;

            for (int i = gRoot.numChildren - 1; i >= 0; i--)
            {
                Fgui.GObject child = gRoot.GetChildAt(i);
                if (child == null
                    || !IsKnownLayerRootName(child.name)
                    || HasLiveDisplayHierarchy(child))
                {
                    continue;
                }

                RemoveFromParentIfSafe(child);
            }
        }

        private static bool TryGetLiveLayerRoot(
            Fgui.GRoot gRoot,
            MxUiLayer layer,
            out Fgui.GObject root)
        {
            root = null;
            if (gRoot == null)
                return false;

            string rootName = GetLayerRootName(layer);
            for (int i = 0; i < gRoot.numChildren; i++)
            {
                Fgui.GObject child = gRoot.GetChildAt(i);
                if (child == null
                    || !string.Equals(child.name, rootName, StringComparison.Ordinal)
                    || !HasLiveDisplayHierarchy(child))
                {
                    continue;
                }

                root = child;
                return true;
            }

            return false;
        }

        private static void RemoveFromParentIfSafe(Fgui.GObject value)
        {
            if (value == null || value.parent == null)
                return;

            if (!HasLiveDisplayHierarchy(value) || !HasLiveDisplayHierarchy(value.parent))
                return;

            value.RemoveFromParent();
        }

        private static bool IsKnownLayerRootName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            for (int i = 0; i < OrderedLayers.Length; i++)
            {
                if (string.Equals(name, GetLayerRootName(OrderedLayers[i]), StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool HasLiveDisplayObject(Fgui.GObject value)
        {
            if (value == null)
                return false;

            Fgui.DisplayObject displayObject = value.displayObject;
            return displayObject != null && !displayObject.isDisposed && displayObject.gameObject != null;
        }

        private static bool HasLiveDisplayHierarchy(Fgui.GObject value)
        {
            if (!HasLiveDisplayObject(value))
                return false;

            Fgui.DisplayObject displayObject = value.displayObject;
            Fgui.Container parent = displayObject != null ? displayObject.parent : null;
            return parent == null || (!parent.isDisposed && parent.gameObject != null);
        }

        private static bool HasLiveGRoot(Fgui.GRoot gRoot)
        {
            if (!HasLiveDisplayHierarchy(gRoot))
                return false;

            Fgui.DisplayObject displayObject = gRoot.displayObject;
            Fgui.Container parent = displayObject != null ? displayObject.parent : null;
            return parent != null && !parent.isDisposed && parent.gameObject != null;
        }

        private static bool TryGetGRoot(out Fgui.GRoot gRoot)
        {
            if (!Application.isPlaying)
            {
                gRoot = null;
                return false;
            }

            try
            {
                gRoot = Fgui.GRoot.inst;
                if (!HasLiveGRoot(gRoot))
                {
                    gRoot = null;
                    return false;
                }

                return true;
            }
            catch (InvalidOperationException) when (!UnityEngine.Application.isPlaying)
            {
                gRoot = null;
                return false;
            }
            catch (NullReferenceException)
            {
                gRoot = null;
                return false;
            }
        }

        private static string GetLayerRootName(MxUiLayer layer)
        {
            switch (layer)
            {
                case MxUiLayer.Background:
                    return "MxFairyGuiLayer_Background";
                case MxUiLayer.Hud:
                    return HudRootName;
                case MxUiLayer.Panel:
                    return "MxFairyGuiLayer_Panel";
                case MxUiLayer.Popup:
                    return "MxFairyGuiLayer_Popup";
                case MxUiLayer.Modal:
                    return "MxFairyGuiLayer_Modal";
                case MxUiLayer.Toast:
                    return ToastRootName;
                case MxUiLayer.Debug:
                    return "MxFairyGuiLayer_Debug";
                default:
                    return "MxFairyGuiLayer_" + layer;
            }
        }
    }
}
