using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Settings;
using MxFramework.Settings.Input;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestSettingsPage
    {
        Graphics = 0,
        Audio = 1,
        Controls = 2
    }

    public enum CharacterTestSettingsInputDevicePage
    {
        KeyboardMouse = 0,
        Gamepad = 1
    }

    public enum CharacterTestSettingsInputRowKind
    {
        Section = 0,
        Binding = 1
    }

    public sealed class CharacterTestFairyGuiSettingsPanelEditPayload
    {
        public string RenderScaleText { get; set; } = string.Empty;
        public string ShadowDistanceText { get; set; } = string.Empty;
        public string AudioVolumeText { get; set; } = string.Empty;
    }

    public sealed class CharacterTestFairyGuiSettingsPanelInputPayload
    {
        public int RowIndex { get; set; }
        public MxInputBindingTarget Target { get; set; }
    }

    public sealed class CharacterTestFairyGuiSettingsInputRowViewModel
    {
        public int RowIndex { get; set; }
        public CharacterTestSettingsInputRowKind Kind { get; set; } =
            CharacterTestSettingsInputRowKind.Binding;
        public string Label { get; set; } = string.Empty;
        public string PrimaryValue { get; set; } = string.Empty;
        public string SecondaryValue { get; set; } = string.Empty;
        public MxInputBindingTarget PrimaryTarget { get; set; }
        public MxInputBindingTarget SecondaryTarget { get; set; }
        public bool PrimaryEnabled { get; set; }
        public bool SecondaryEnabled { get; set; }
        public bool PrimaryListening { get; set; }
        public bool SecondaryListening { get; set; }

        public bool IsSection => Kind == CharacterTestSettingsInputRowKind.Section;

        // Compatibility aliases for callers that still treat a row as one binding.
        public string Value
        {
            get => PrimaryValue;
            set => PrimaryValue = value;
        }

        public MxInputBindingTarget Target
        {
            get => PrimaryTarget;
            set => PrimaryTarget = value;
        }

        public bool Enabled
        {
            get => PrimaryEnabled;
            set => PrimaryEnabled = value;
        }
    }

    public sealed class CharacterTestFairyGuiSettingsPanelViewModel
    {
        private CharacterTestFairyGuiSettingsInputRowViewModel[] _inputRows =
            Array.Empty<CharacterTestFairyGuiSettingsInputRowViewModel>();

        public string Title { get; set; } = "Settings";
        public string Subtitle { get; set; } = string.Empty;
        public string PageTitle { get; set; } = string.Empty;
        public string PageDescription { get; set; } = string.Empty;
        public string InputInstruction { get; set; } = string.Empty;
        public string ActionColumnTitle { get; set; } = string.Empty;
        public string PrimaryColumnTitle { get; set; } = string.Empty;
        public string SecondaryColumnTitle { get; set; } = string.Empty;
        public string StatusText { get; set; } = "Ready";
        public string DirtyText { get; set; } = "Saved";
        public bool Dirty { get; set; }
        public bool IsRebinding { get; set; }
        public CharacterTestSettingsPage SelectedPage { get; set; } =
            CharacterTestSettingsPage.Controls;
        public CharacterTestSettingsInputDevicePage SelectedInputDevice { get; set; } =
            CharacterTestSettingsInputDevicePage.KeyboardMouse;
        public MxGraphicsQualityPreset QualityPreset { get; set; } = MxGraphicsQualityPreset.Custom;
        public string QualityText { get; set; } = "Custom";
        public int VSyncCount { get; set; } = -1;
        public string VSyncText { get; set; } = "Current";
        public string RenderScaleText { get; set; } = "Current";
        public string AntiAliasingText { get; set; } = "Current";
        public string ShadowDistanceText { get; set; } = "Current";
        public string DirectionalShadowQualityText { get; set; } = "Current";
        public string ShadowCascadeCountText { get; set; } = "Current";
        public string LocalShadowResolutionText { get; set; } = "Current";
        public string HdrModeText { get; set; } = "Off";
        public string HdrStatusText { get; set; } = string.Empty;
        public string HdrPaperWhiteText { get; set; } = "Automatic";
        public string HdrPeakText { get; set; } = "Automatic";
        public bool CanToggleHdr { get; set; }
        public bool CanCalibrateHdr { get; set; }
        public string ResolutionText { get; set; } = "Current";
        public string WindowModeText { get; set; } = "Current";
        public string LocaleId { get; set; } = MxLocalizationSettings.DefaultLocaleId;
        public string LocaleText { get; set; } = "Chinese (Simplified)";
        public int AudioBusId { get; set; }
        public string AudioCategoryText { get; set; } = "Master";
        public float AudioVolume { get; set; } = 1f;
        public string AudioVolumeText { get; set; } = "1";
        public bool AudioMuted { get; set; }
        public string AudioMutedText { get; set; } = "On";
        public bool CanEdit { get; set; } = true;

        public IReadOnlyList<CharacterTestFairyGuiSettingsInputRowViewModel> InputRows => _inputRows;

        public void SetInputRows(IReadOnlyList<CharacterTestFairyGuiSettingsInputRowViewModel> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                _inputRows = Array.Empty<CharacterTestFairyGuiSettingsInputRowViewModel>();
                return;
            }

            _inputRows = new CharacterTestFairyGuiSettingsInputRowViewModel[rows.Count];
            for (int i = 0; i < rows.Count; i++)
                _inputRows[i] = rows[i];
        }
    }

    internal static class CharacterTestFairyGuiSettingsPanelViewModelSignature
    {
        public static string Create(CharacterTestFairyGuiSettingsPanelViewModel viewModel)
        {
            if (viewModel == null)
                return string.Empty;

            var builder = new StringBuilder(512);
            builder.Append(viewModel.Dirty ? '1' : '0').Append('|')
                .Append(viewModel.IsRebinding ? '1' : '0').Append('|')
                .Append((int)viewModel.SelectedPage).Append('|')
                .Append((int)viewModel.SelectedInputDevice).Append('|')
                .Append(viewModel.QualityPreset).Append('|')
                .Append(viewModel.VSyncCount).Append('|')
                .Append(viewModel.AudioMuted ? '1' : '0').Append('|')
                .Append(viewModel.AudioBusId).Append('|');
            AppendToken(builder, viewModel.StatusText);
            AppendToken(builder, viewModel.Title);
            AppendToken(builder, viewModel.Subtitle);
            AppendToken(builder, viewModel.PageTitle);
            AppendToken(builder, viewModel.PageDescription);
            AppendToken(builder, viewModel.InputInstruction);
            AppendToken(builder, viewModel.DirtyText);
            AppendToken(builder, viewModel.RenderScaleText);
            AppendToken(builder, viewModel.AntiAliasingText);
            AppendToken(builder, viewModel.ShadowDistanceText);
            AppendToken(builder, viewModel.DirectionalShadowQualityText);
            AppendToken(builder, viewModel.ShadowCascadeCountText);
            AppendToken(builder, viewModel.LocalShadowResolutionText);
            AppendToken(builder, viewModel.HdrModeText);
            AppendToken(builder, viewModel.HdrStatusText);
            AppendToken(builder, viewModel.HdrPaperWhiteText);
            AppendToken(builder, viewModel.HdrPeakText);
            builder.Append(viewModel.CanToggleHdr).Append('|').Append(viewModel.CanCalibrateHdr).Append('|');
            AppendToken(builder, viewModel.ResolutionText);
            AppendToken(builder, viewModel.WindowModeText);
            AppendToken(builder, viewModel.LocaleId);
            AppendToken(builder, viewModel.LocaleText);
            AppendToken(builder, viewModel.AudioCategoryText);
            AppendToken(builder, viewModel.AudioVolumeText);
            for (int i = 0; i < viewModel.InputRows.Count; i++)
            {
                CharacterTestFairyGuiSettingsInputRowViewModel row = viewModel.InputRows[i];
                builder.Append((int)row.Kind).Append('|');
                AppendToken(builder, row.Label);
                AppendToken(builder, row.PrimaryValue);
                AppendToken(builder, row.SecondaryValue);
                builder.Append(row.PrimaryEnabled ? '1' : '0').Append('|')
                    .Append(row.SecondaryEnabled ? '1' : '0').Append('|')
                    .Append(row.PrimaryListening ? '1' : '0').Append('|')
                    .Append(row.SecondaryListening ? '1' : '0').Append('|');
            }

            return builder.ToString();
        }

        private static void AppendToken(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append('|');
        }
    }
}
