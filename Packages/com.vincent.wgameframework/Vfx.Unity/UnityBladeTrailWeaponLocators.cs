using System;
using UnityEngine;

namespace MxFramework.Vfx.Unity
{
    /// <summary>Low-frequency locator lookup. Uses published trace paths, never name-based guessed geometry.</summary>
    public static class UnityBladeTrailWeaponLocators
    {
        public static bool TryResolve(Transform weapon, string rootPath, string tipPath, out Transform root, out Transform tip)
        {
            root = Find(weapon, rootPath); tip = Find(weapon, tipPath);
            return root != null && tip != null && root != tip;
        }
        private static Transform Find(Transform weapon, string locatorPath)
        {
            if (weapon == null || string.IsNullOrEmpty(locatorPath)) return null;
            Transform result = weapon.Find(locatorPath);
            // "weapon/" is the existing published weapon-local namespace, not a scene-name heuristic.
            if (result == null && locatorPath.StartsWith("weapon/", StringComparison.Ordinal))
                result = weapon.Find(locatorPath.Substring(7));
            return result;
        }
    }
}
