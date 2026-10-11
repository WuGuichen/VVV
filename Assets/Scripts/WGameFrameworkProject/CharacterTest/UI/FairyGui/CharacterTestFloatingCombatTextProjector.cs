using System;
using System.Collections.Generic;
using MxFramework.Camera.URP;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFloatingCombatTextProjector
    {
        private const float DefaultOffscreenMargin = 96f;
        private const float DefaultEdgePadding = 18f;
        private UnityCamera _cachedMainCamera;

        public UnityCamera Camera { get; set; }

        public bool TryProject(Vector3 worldPosition, out Vector2 stagePosition)
        {
            return TryProject(
                Camera != null ? Camera : CharacterTestMainCameraCache.Resolve(ref _cachedMainCamera),
                worldPosition,
                out stagePosition);
        }

        public static bool TryProject(
            UnityCamera camera,
            Vector3 worldPosition,
            out Vector2 stagePosition,
            float edgePadding = DefaultEdgePadding,
            float offscreenMargin = DefaultOffscreenMargin)
        {
            stagePosition = default;
            if (camera == null)
                return false;

            Vector3 screen = camera.WorldToScreenPoint(worldPosition);
            if (screen.z <= 0f)
                return false;

            float width = ResolveScreenWidth(camera);
            float height = ResolveScreenHeight(camera);
            if (screen.x < -offscreenMargin
                || screen.x > width + offscreenMargin
                || screen.y < -offscreenMargin
                || screen.y > height + offscreenMargin)
            {
                return false;
            }

            float safePadding = edgePadding < 0f ? 0f : edgePadding;
            screen.x = Mathf.Clamp(screen.x, safePadding, Math.Max(safePadding, width - safePadding));
            screen.y = Mathf.Clamp(screen.y, safePadding, Math.Max(safePadding, height - safePadding));

            var globalPoint = new Vector2(screen.x, height - screen.y);
            if (!Application.isPlaying)
            {
                stagePosition = globalPoint;
                return true;
            }

            try
            {
                Fgui.GRoot root = Fgui.GRoot.inst;
                stagePosition = root != null ? root.GlobalToLocal(globalPoint) : globalPoint;
                return true;
            }
            catch (InvalidOperationException)
            {
                stagePosition = globalPoint;
                return true;
            }
        }

        private static float ResolveScreenWidth(UnityCamera camera)
        {
            if (Screen.width > 0)
                return Screen.width;
            return camera.pixelWidth > 0 ? camera.pixelWidth : 1920f;
        }

        private static float ResolveScreenHeight(UnityCamera camera)
        {
            if (Screen.height > 0)
                return Screen.height;
            return camera.pixelHeight > 0 ? camera.pixelHeight : 1080f;
        }
    }

}
