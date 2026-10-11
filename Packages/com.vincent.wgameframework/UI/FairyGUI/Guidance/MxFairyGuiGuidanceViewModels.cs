namespace MxFramework.UI.FairyGui
{
    /// <summary>Static layout options for the shared guidance surface.</summary>
    public enum MxFairyGuiGuidanceLayoutPreset : byte
    {
        Default = 0,
        TopCornersOccupied = 1
    }

    /// <summary>Resolved presentation data for the one fixed guidance card.</summary>
    /// <remarks>AllocByDesign reference model; one instance is reused by the presenter.</remarks>
    public sealed class MxFairyGuiGuidanceCardViewModel
    {
        public bool Visible { get; set; }
        public string Title { get; set; } = string.Empty;
        public MxUiGuidanceGlyph HeaderGlyph { get; set; } = MxUiGuidanceGlyph.Context;
        public string ActionLabel { get; set; } = string.Empty;
        public string ActionBody { get; set; } = string.Empty;
        public MxUiGuidanceGlyph ActionGlyph { get; set; } = MxUiGuidanceGlyph.Action;
        public MxUiGuidanceTone ActionTone { get; set; } = MxUiGuidanceTone.Info;
        public string FocusLabel { get; set; } = string.Empty;
        public string FocusBody { get; set; } = string.Empty;
        public MxUiGuidanceGlyph FocusGlyph { get; set; } = MxUiGuidanceGlyph.Focus;
        public MxUiGuidanceTone FocusTone { get; set; } = MxUiGuidanceTone.Info;
        public string StatusLabel { get; set; } = string.Empty;
        public string StatusBody { get; set; } = string.Empty;
        public MxUiGuidanceGlyph StatusGlyph { get; set; } = MxUiGuidanceGlyph.Progress;
        public MxUiGuidanceTone StatusTone { get; set; } = MxUiGuidanceTone.Neutral;
        public MxFairyGuiGuidanceLayoutPreset LayoutPreset { get; set; }
    }

    /// <summary>Resolved presentation data for the fixed two-slot guidance notice stack.</summary>
    /// <remarks>AllocByDesign reference model; one instance is reused by the presenter.</remarks>
    public sealed class MxFairyGuiGuidanceNoticeStackViewModel
    {
        public bool Notice1Visible { get; set; }
        public string Notice1Text { get; set; } = string.Empty;
        public MxUiGuidanceGlyph Notice1Glyph { get; set; } = MxUiGuidanceGlyph.Warning;
        public MxUiGuidanceTone Notice1Tone { get; set; } = MxUiGuidanceTone.Warning;
        public long Notice1Sequence { get; set; }
        public long Notice1Revision { get; set; }

        public bool Notice2Visible { get; set; }
        public string Notice2Text { get; set; } = string.Empty;
        public MxUiGuidanceGlyph Notice2Glyph { get; set; } = MxUiGuidanceGlyph.Warning;
        public MxUiGuidanceTone Notice2Tone { get; set; } = MxUiGuidanceTone.Warning;
        public long Notice2Sequence { get; set; }
        public long Notice2Revision { get; set; }
        public MxFairyGuiGuidanceLayoutPreset LayoutPreset { get; set; }
    }
}
