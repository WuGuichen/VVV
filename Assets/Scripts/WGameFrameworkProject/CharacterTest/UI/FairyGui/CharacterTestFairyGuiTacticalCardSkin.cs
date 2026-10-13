using System;
using System.Collections.Generic;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestFairyGuiTacticalCardSkin : IDisposable
    {
        private static readonly int BattleReadinessId = Shader.PropertyToID("_BattleReadiness");
        private static Fgui.GObject Required(Fgui.GComponent owner,string name) => owner.GetChild(name)
            ?? throw new InvalidOperationException("Card chrome missing: "+name);
        private static void Box(Fgui.GObject item,float x,float y,float width,float height)
        { item.SetXY(x,y); item.SetSize(width,height); }
        private static void SetTextSize(Fgui.GTextField field,int size)
        { var format=field.textFormat; format.size=size; format.bold=true; field.textFormat=format; }
        private readonly Dictionary<Fgui.GComponent, Material> _shells = new();
        private Fgui.NTexture _shellTexture;
        private Material _shellSource;
        private static readonly string[] UtilityControls = { "utilityType", "utilityTrigger", "utilityPrefix", "utilityValue",
            "utilitySuffix", "utilityEffect", "utilityDetails", "utilityLifetime" };
        private readonly Dictionary<string, Texture2D> _illustrations = new();

        internal void Bind(Fgui.GComponent face, CharacterTestCardUiPreviewData data, string qualityCardId = null)
        {
            bool utility = data.UtilityFace != null;
            int quality = ResolveQuality(qualityCardId ?? data.Id);
            // The common authored alpha supplies the utility outline; retain the parent list clip.
            var skinMask = Required(face, "skinMask");
            face.mask = utility ? null : skinMask.displayObject;
            skinMask.visible = !utility;
            _shellSource ??= UnityEngine.Resources.Load<Material>("MxFramework/UI/CardTheme/AuthoredSilver");
            if (_shellSource == null) throw new InvalidOperationException("Preparation card shell material is missing.");
            _shellTexture ??= new Fgui.NTexture(_shellSource.mainTexture) { destroyMethod = Fgui.DestroyMethod.None };
            if (!_shells.TryGetValue(face, out var material))
            {
                _shellSource ??= UnityEngine.Resources.Load<Material>("MxFramework/UI/CardTheme/AuthoredSilver");
                if (_shellSource == null) throw new InvalidOperationException("Preparation card shell material is missing.");
                _shellTexture ??= new Fgui.NTexture(_shellSource.mainTexture) { destroyMethod = Fgui.DestroyMethod.None };
                var background = Required(face, "background").asImage;
                background.texture = _shellTexture;
                material = new Material(_shellSource) { hideFlags = HideFlags.DontSave };
                background.material = material;
                _shells.Add(face, material);
            }
            var surface = _shellSource;
            var image = Required(face, "background").asImage;
            image.texture = _shellTexture;
            image.material = material;
            material.mainTexture = surface.mainTexture;
            material.SetTexture("_RegionTex", surface.GetTexture("_RegionTex"));
            material.SetFloat("_UtilityCard", utility ? 1 : 0);
            material.SetFloat("_RippleStrength", surface.GetFloat("_RippleStrength"));
            material.SetFloat("_Quality", quality);
            material.SetFloat("_FlowTime", Time.unscaledTime);
            material.SetFloat("_PanelOpacity", 1);
            string illustrationName = utility ? data.UtilityFace.Illustration : ResolveStyle(qualityCardId ?? data.Id)?.illustration;
            string illustrationPath = "MxFramework/UI/CardTheme/" + (utility ? "Utility/" : "Icons/") + illustrationName;
            bool paintedIllustration = !string.IsNullOrWhiteSpace(illustrationName);
            if (paintedIllustration)
            {
                if (!_illustrations.TryGetValue(illustrationPath, out var illustration))
                {
                    illustration = UnityEngine.Resources.Load<Texture2D>(illustrationPath);
                    if (illustration == null) throw new InvalidOperationException("Card illustration is missing: " + illustrationName);
                    _illustrations.Add(illustrationPath, illustration);
                }
                material.SetTexture("_CardIllustration", illustration);
                material.SetVector("_CardIllustrationRect", new Vector4(.2765f, .475f, .447f, .32f));
            }
            // Pooled faces can be rebound to another card; reset the flag every bind.
            material.SetFloat("_CardIllustrationEnabled", paintedIllustration ? 1 : 0);
            foreach (string name in new[] { "costPlate", "separatorLeft", "separatorRight", "qualityDiamond", "qualityCircle", "swordIllustration" })
                Required(face, name).visible = false;
            Box(Required(face, "cost"), 24, 26, 58, 56);
            Box(Required(face, "name"), 94, 20, 248, 62);
            var nameField = Required(face, "name").asTextField;
            nameField.autoSize = Fgui.AutoSizeType.Shrink; nameField.singleLine = false; SetTextSize(nameField, 34);
            SetTextSize(Required(face, "cost").asTextField, 42);
            foreach (string name in new[] { "cost", "name", "description" })
            {
                var text = Required(face, name).asTextField; text.color = new Color32(20, 43, 70, 255);
                var format = text.textFormat; format.bold = true; text.textFormat = format;
            }
            Box(Required(face, "description"), 26, 310, 326, 192);
            Box(Required(face, "behaviorHints"), 26, 310, 326, 192);
            foreach (string control in UtilityControls)
                if (face.GetChild(control) is Fgui.GObject node) node.visible = false;
            CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.Bind(face, data.IconKey, 126, 134, 132,
                new Color32(25, 51, 78, 255), !paintedIllustration);
            // The pictogram sits directly over the optical inset; no opaque octagonal badge.
            var icon = face.GetChild("behaviorCardIconVisual")?.asCom;
            if (icon != null)
                foreach (string name in new[] { "badgeBorder", "badgeFill" })
                    if (icon.GetChild(name) is Fgui.GObject badge) badge.visible = false;
        }

        [Serializable] private sealed class QualityStyle { public string cardId; public int quality; public string illustration; }
        [Serializable] private sealed class QualityStyles { public QualityStyle[] cards; }
        private QualityStyles _qualityStyles;
        internal int ResolveQuality(string cardId) => Mathf.Clamp(ResolveStyle(cardId)?.quality ?? 0, 0, 2);

        private QualityStyle ResolveStyle(string cardId)
        {
            if (_qualityStyles == null)
            {
                var config = UnityEngine.Resources.Load<TextAsset>("MxFramework/UI/CardTheme/PreparationQualityStyles");
                _qualityStyles = config == null ? new QualityStyles() : JsonUtility.FromJson<QualityStyles>(config.text);
            }
            if (_qualityStyles.cards != null)
                foreach (var entry in _qualityStyles.cards)
                    if (entry.cardId == cardId) return entry;
            return null;
        }

        internal void Tick()
        {
            foreach (var material in _shells.Values)
                material.SetFloat("_FlowTime", Time.unscaledTime);
        }

        internal void SetBattleReadiness(Fgui.GComponent card, float value)
        {
            if (_shells.TryGetValue(card, out var material))
                material.SetFloat(BattleReadinessId, Mathf.Clamp01(value));
        }

        internal void SetPreviewQuality(Fgui.GComponent card, int quality)
        { if (_shells.TryGetValue(card, out var material)) material.SetFloat("_Quality", Mathf.Clamp(quality, 0, 2)); }

        // Lab-only accessory projection keeps the authored shell and text geometry.
        // Bind resets the original rectangle before each pooled face is projected.
        internal void SetAccessoryGraphicLayout(Fgui.GComponent card)
        { if (_shells.TryGetValue(card, out var material)) material.SetVector("_CardIllustrationRect", new Vector4(.1765f, .475f, .447f, .32f)); }

        public void Dispose()
        {
            foreach (var pair in _shells)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(pair.Value);
                else UnityEngine.Object.DestroyImmediate(pair.Value);
            }
            _shells.Clear(); _shellTexture?.Dispose(); _shellTexture = null; _shellSource = null; _qualityStyles = null;
            _illustrations.Clear();
        }
    }
}
