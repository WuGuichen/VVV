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
                case ResourceTypeIds.AnimationClip:
                    return typeof(AnimationClip);
                case ResourceTypeIds.Material:
                    return typeof(Material);
                case ResourceTypeIds.PanelSettings:
                    return typeof(PanelSettings);
                case ResourceTypeIds.VisualTreeAsset:
                    return typeof(VisualTreeAsset);
                case ResourceTypeIds.StyleSheet:
                    return typeof(StyleSheet);
                case ResourceTypeIds.Font:
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
