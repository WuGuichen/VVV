using System.Globalization;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestSettingsVolumeDraft
    {
        internal CharacterTestSettingsVolumeDraft(int busId, float value) { BusId = busId; Value = value; }
        internal int BusId { get; }
        internal float Value { get; }
    }

    public sealed partial class CharacterTestFairyGuiSettingsPanelBinder
    {
        private Fgui.GComponent _volumeSlider;
        private Fgui.GTextField _volumePayloadField;
        private int _volumeBusId;
        private float _volumeValue;
        private bool _volumeDragging;
        private bool _restoreVolumeFocus;
        private bool _volumeEditable;
        private const float VolumeStart = 12f;
        private const float VolumeEnd = 282f;

        private void BindVolumeSlider(Fgui.GComponent root, CharacterTestFairyGuiSettingsPanelViewModel model)
        {
            Fgui.GComponent slider = root.GetChild("audioVolumeSlider")?.asCom;
            if (slider == null) return; // Older or minimal test surfaces retain their input-field contract.
            if (_volumeSlider != slider)
            {
                _volumeSlider = slider;
                slider.opaque = true;
                slider.onTouchBegin.Add(context =>
                {
                    if (!_volumeEditable || context.inputEvent.button != 0) return;
                    _volumeDragging = true;
                    context.CaptureTouch();
                    SetVolumeFromPointer(context.inputEvent.position);
                });
                slider.onTouchMove.Add(context =>
                {
                    if (_volumeDragging) SetVolumeFromPointer(context.inputEvent.position);
                });
                slider.onTouchEnd.Add(() => _volumeDragging = false);
                slider.onRemovedFromStage.Add(() => { _volumeDragging = false; _restoreVolumeFocus = false; });
                slider.onKeyDown.Add(OnVolumeKeyDown);
                for (int i = 0; i < slider.numChildren; i++) slider.GetChildAt(i).touchable = false;
            }
            if (_volumeBusId != model.AudioBusId) { _volumeDragging = false; _restoreVolumeFocus = false; }
            _volumeBusId = model.AudioBusId;
            _volumeEditable = model.CanEdit && model.SelectedPage == CharacterTestSettingsPage.Audio;
            slider.visible = model.SelectedPage == CharacterTestSettingsPage.Audio;
            slider.touchable = _volumeEditable;
            slider.focusable = _volumeEditable;
            slider.tabStop = _volumeEditable;
            slider.alpha = model.CanEdit ? 1f : 0.45f;
            _volumePayloadField = root.GetChild(CharacterTestFairyGuiSettingsPanelIds.AudioVolumeInput)?.asTextField;
            if (_volumePayloadField != null) { _volumePayloadField.visible = false; _volumePayloadField.tabStop = false; }
            if (root.GetChild("audioVolumeInputBg") is Fgui.GObject background) background.visible = false;
            _volumeValue = Mathf.Clamp01(model.AudioVolume);
            DrawVolumeSlider();
            if (_restoreVolumeFocus && _volumeEditable) slider.RequestFocus();
            _restoreVolumeFocus = false;
        }

        private void SetVolumeFromPointer(Vector2 stagePosition)
        {
            float x = _volumeSlider.GlobalToLocal(stagePosition).x;
            SetVolumeDraft((x - VolumeStart) / (VolumeEnd - VolumeStart));
        }

        private void SetVolumeDraft(float value)
        {
            if (!_volumeEditable) return;
            float next = Mathf.Round(Mathf.Clamp01(value) * 100f) / 100f;
            if (Mathf.Approximately(next, _volumeValue)) return;
            _volumeValue = next;
            if (_volumePayloadField != null) _volumePayloadField.text = next.ToString("0.##", CultureInfo.InvariantCulture);
            DrawVolumeSlider();
            _commandSink?.Enqueue(new MxUiCommand(_viewId,
                CharacterTestFairyGuiSettingsPanelCommandSink.VolumeDraftCommand,
                new CharacterTestSettingsVolumeDraft(_volumeBusId, next)));
        }

        private void OnVolumeKeyDown(Fgui.EventContext context)
        {
            Fgui.InputEvent input = context.data as Fgui.InputEvent ?? context.inputEvent;
            if (input == null || !_volumeEditable) return;
            float step = (input.modifiers & EventModifiers.Shift) != 0 ? 0.1f : 0.01f;
            float value;
            switch (input.keyCode)
            {
                case KeyCode.LeftArrow: case KeyCode.DownArrow: value = _volumeValue - step; break;
                case KeyCode.RightArrow: case KeyCode.UpArrow: value = _volumeValue + step; break;
                case KeyCode.Home: value = 0f; break;
                case KeyCode.End: value = 1f; break;
                default: _restoreVolumeFocus = false; return;
            }
            _restoreVolumeFocus = true;
            SetVolumeDraft(value);
            context.StopPropagation();
            context.PreventDefault();
        }

        private void DrawVolumeSlider()
        {
            if (_volumeSlider == null || _volumeSlider.isDisposed) return;
            float span = VolumeEnd - VolumeStart;
            _volumeSlider.GetChild("track")?.asGraph?.DrawRect(span, 8f, 0, Color.clear, new Color32(154, 182, 197, 255));
            Fgui.GGraph fill = _volumeSlider.GetChild("fill")?.asGraph;
            if (fill != null) { fill.visible = _volumeValue > 0f; fill.DrawRect(span * _volumeValue, 8f, 0, Color.clear, new Color32(64, 150, 150, 255)); }
            Fgui.GObject thumb = _volumeSlider.GetChild("thumb");
            if (thumb != null) thumb.x = VolumeStart + span * _volumeValue - thumb.width * 0.5f;
            if (_volumeSlider.GetChild("value")?.asTextField is Fgui.GTextField label)
                label.text = Mathf.RoundToInt(_volumeValue * 100f).ToString(CultureInfo.InvariantCulture) + "%";
            _volumeSlider.tooltips = "音量 " + Mathf.RoundToInt(_volumeValue * 100f) + "% · 方向键微调，Shift 加速";
        }
    }
}
