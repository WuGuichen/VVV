using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Resources.Unity
{
    public static class UnityResourceTypeResolver
    {
        public static Type Resolve(string typeId)
        {
            switch (typeId)
            {
                case ResourceTypeIds.GameObject:
                    return typeof(GameObject);
                case ResourceTypeIds.Texture2D:
                    return typeof(Texture2D);
                case ResourceTypeIds.Sprite:
                    return typeof(Sprite);
                case ResourceTypeIds.AudioClip:
                    return typeof(AudioClip);
                case "Material":
                    return typeof(Material);
                case "PanelSettings":
                    return typeof(PanelSettings);
                case "VisualTreeAsset":
                    return typeof(VisualTreeAsset);
                case "StyleSheet":
                    return typeof(StyleSheet);
                case "Font":
                    return typeof(Font);
                case ResourceTypeIds.TextAsset:
                case ResourceTypeIds.String:
                    return typeof(TextAsset);
                case ResourceTypeIds.Object:
                case "":
                case null:
                    return typeof(UnityEngine.Object);
                default:
                    return typeof(UnityEngine.Object);
            }
        }
    }
}
