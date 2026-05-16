using System;

namespace MxFramework.Resources
{
    public static class ResourceTypeIds
    {
        public const string GameObject = "GameObject";
        public const string Texture2D = "Texture2D";
        public const string Sprite = "Sprite";
        public const string AudioClip = "AudioClip";
        public const string AnimationClip = "AnimationClip";
        public const string AvatarMask = "AvatarMask";
        public const string TextAsset = "TextAsset";
        public const string Material = "Material";
        public const string PanelSettings = "PanelSettings";
        public const string VisualTreeAsset = "VisualTreeAsset";
        public const string StyleSheet = "StyleSheet";
        public const string Font = "Font";
        public const string String = "String";
        public const string Object = "Object";

        public static string FromType<T>()
        {
            return FromType(typeof(T));
        }

        public static string FromType(Type type)
        {
            if (type == null)
                return Object;

            if (type == typeof(string))
                return String;

            if (type == typeof(object))
                return Object;

            return type.Name;
        }
    }
}
