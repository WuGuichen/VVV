using System;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Project-owned font assets and aliases shared by published FairyGUI screens.</summary>
    internal static class CharacterTestFairyGuiFonts
    {
        internal const string Body = "MxUI Sans SC";
        internal const string Control = Body;
        internal const string Heading = "MxUI Serif SC";
        internal static string CurrentLocale { get; private set; } = "zh-CN";

        // These three published names are stable typography roles. Locale changes
        // replace their native faces in place so existing fields keep their handles.
        private sealed class LocaleFont : Fgui.DynamicFont
        {
            internal LocaleFont(string alias, Font asset) : base(alias, asset) { }
            internal void Use(Font asset)
            {
                if (nativeFont == asset) return;
                nativeFont = asset;
                version++;
                textRebuildFlag = true;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Initialize()
        {
            SetLocale("zh-CN");
            Fgui.UIConfig.defaultFont = Body;
        }

        internal static void SetLocale(string locale)
        {
            string region = RegionForLocale(locale);
            // Resolve all required assets before changing any visible role.
            Font body = Load("MxUiSans" + region);
            Font heading = Load("MxUiSerif" + region);
            Register(Body, body);
            Register(Heading, heading);
            CurrentLocale = string.IsNullOrWhiteSpace(locale) ? "zh-CN" : locale.Trim();
        }

        internal static string RegionForLocale(string locale)
        {
            string code = (locale ?? string.Empty).Trim().Replace('_', '-').ToLowerInvariant();
            if (code == "tchinese" || code == "zh-tw" || code == "zh-hk" || code.StartsWith("zh-hant")) return "TW";
            if (code == "japanese" || code == "ja" || code.StartsWith("ja-")) return "JP";
            if (code == "koreana" || code == "korean" || code == "ko" || code.StartsWith("ko-")) return "KR";
            // Shared Latin/Cyrillic glyphs; all regions use the same small Western
            // fallback files for extended letters and combining characters.
            return "SC";
        }

        private static Font Load(string resource)
        {
            Font asset = UnityEngine.Resources.Load<Font>("Fonts/" + resource);
            if (asset == null) throw new InvalidOperationException("Required UI font is missing: " + resource);
            return asset;
        }

        // LowFreqAlloc: initialization and explicit locale changes, never per-frame.
        private static void Register(string alias, Font asset)
        {
            if (Fgui.FontManager.sFontFactory.TryGetValue(alias, out var current))
            {
                if (current is LocaleFont localized) { localized.Use(asset); return; }
                Fgui.FontManager.UnregisterFont(current);
                current.Dispose();
            }
            Fgui.FontManager.RegisterFont(new LocaleFont(alias, asset));
        }
    }
}
