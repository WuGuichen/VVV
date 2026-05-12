using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.UI.Toolkit
{
    public enum MxUiTone
    {
        Neutral,
        Positive,
        Warning,
        Danger
    }

    public static class MxUiThemeTokens
    {
        public const string Panel = "mx-panel";
        public const string PanelSection = "mx-panel-section";
        public const string CommandButton = "mx-command-button";
        public const string CommandEnabled = "mx-command-button--enabled";
        public const string CommandHot = "mx-command-button--hot";
        public const string CommandMuted = "mx-command-button--muted";
        public const string StatusBadge = "mx-status-badge";
        public const string StatusNeutral = "mx-status-badge--neutral";
        public const string StatusPositive = "mx-status-badge--positive";
        public const string StatusWarning = "mx-status-badge--warning";
        public const string StatusDanger = "mx-status-badge--danger";
        public const string StatBar = "mx-stat-bar";
        public const string StatBarFill = "mx-stat-bar__fill";
        public const string EventLog = "mx-event-log";
        public const string EventLogRow = "mx-event-log__row";
        public const string EventLogEmpty = "mx-event-log__empty";
        public const string PanelTabs = "mx-panel-tabs";
        public const string PanelTab = "mx-panel-tabs__tab";
        public const string PanelTabActive = "mx-panel-tabs__tab--active";
        public const string DiagnosticTabs = "mx-diagnostic-tabs";

        public static MxUiTone ParseTone(string tone)
        {
            if (string.Equals(tone, "positive", StringComparison.OrdinalIgnoreCase))
                return MxUiTone.Positive;
            if (string.Equals(tone, "warning", StringComparison.OrdinalIgnoreCase))
                return MxUiTone.Warning;
            if (string.Equals(tone, "danger", StringComparison.OrdinalIgnoreCase))
                return MxUiTone.Danger;

            return MxUiTone.Neutral;
        }

        public static string GetStatusToneClass(MxUiTone tone)
        {
            switch (tone)
            {
                case MxUiTone.Positive:
                    return StatusPositive;
                case MxUiTone.Warning:
                    return StatusWarning;
                case MxUiTone.Danger:
                    return StatusDanger;
                default:
                    return StatusNeutral;
            }
        }

        public static void SetStatusTone(VisualElement element, MxUiTone tone)
        {
            if (element == null)
                return;

            element.EnableInClassList(StatusPositive, tone == MxUiTone.Positive);
            element.EnableInClassList(StatusWarning, tone == MxUiTone.Warning);
            element.EnableInClassList(StatusDanger, tone == MxUiTone.Danger);
            element.EnableInClassList(StatusNeutral, tone == MxUiTone.Neutral);
        }
    }

    public sealed class MxStatusBadge : Label
    {
        public MxStatusBadge()
            : this(null, MxUiTone.Neutral)
        {
        }

        public MxStatusBadge(string text, MxUiTone tone)
            : base(string.IsNullOrEmpty(text) ? "-" : text)
        {
            AddToClassList(MxUiThemeTokens.StatusBadge);
            SetTone(tone);
        }

        public MxUiTone Tone { get; private set; }

        public void Set(string value, MxUiTone tone)
        {
            text = string.IsNullOrEmpty(value) ? "-" : value;
            SetTone(tone);
        }

        public void SetTone(MxUiTone tone)
        {
            Tone = tone;
            MxUiThemeTokens.SetStatusTone(this, tone);
        }
    }

    public sealed class MxCommandButton : Button
    {
        public MxCommandButton()
            : this(null, null)
        {
        }

        public MxCommandButton(Action clicked, string label)
            : base(clicked)
        {
            text = string.IsNullOrEmpty(label) ? "-" : label;
            AddToClassList(MxUiThemeTokens.CommandButton);
        }

        public bool IsHot { get; private set; }

        public void SetState(bool enabled, bool hot, string tooltipText)
        {
            IsHot = hot;
            EnableInClassList(MxUiThemeTokens.CommandEnabled, enabled);
            EnableInClassList(MxUiThemeTokens.CommandHot, hot);
            EnableInClassList(MxUiThemeTokens.CommandMuted, !hot);
            tooltip = string.IsNullOrEmpty(tooltipText) ? null : tooltipText;
        }
    }

    public sealed class MxStatBar : VisualElement
    {
        private readonly VisualElement _fill = new VisualElement();

        public MxStatBar()
        {
            AddToClassList(MxUiThemeTokens.StatBar);
            _fill.AddToClassList(MxUiThemeTokens.StatBarFill);
            Add(_fill);
        }

        public float NormalizedValue { get; private set; }

        public void SetValue(float current, float maximum, MxUiTone tone)
        {
            NormalizedValue = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            _fill.style.width = Length.Percent(NormalizedValue * 100f);
            MxUiThemeTokens.SetStatusTone(_fill, tone);
        }
    }

    public sealed class MxEventLog : VisualElement
    {
        public MxEventLog()
        {
            AddToClassList(MxUiThemeTokens.EventLog);
        }

        public void SetItems(IReadOnlyList<string> items, string emptyText, bool newestFirst = true)
        {
            Clear();
            if (items == null || items.Count == 0)
            {
                Add(CreateRow(string.IsNullOrEmpty(emptyText) ? "-" : emptyText, true));
                return;
            }

            if (newestFirst)
            {
                for (int i = items.Count - 1; i >= 0; i--)
                    Add(CreateRow(items[i], false));
                return;
            }

            for (int i = 0; i < items.Count; i++)
                Add(CreateRow(items[i], false));
        }

        private static Label CreateRow(string value, bool empty)
        {
            var row = new Label(string.IsNullOrEmpty(value) ? "-" : value);
            row.AddToClassList(empty ? MxUiThemeTokens.EventLogEmpty : MxUiThemeTokens.EventLogRow);
            return row;
        }
    }

    public sealed class MxPanelTabs : VisualElement
    {
        private readonly List<Button> _tabs = new List<Button>();

        public MxPanelTabs()
        {
            AddToClassList(MxUiThemeTokens.PanelTabs);
        }

        public event Action<int> TabSelected;

        public int ActiveIndex { get; private set; }

        public void SetTabs(IReadOnlyList<string> labels, int activeIndex = 0)
        {
            Clear();
            _tabs.Clear();

            if (labels == null)
                return;

            for (int i = 0; i < labels.Count; i++)
            {
                int index = i;
                var button = new Button(() => Select(index)) { text = string.IsNullOrEmpty(labels[i]) ? "-" : labels[i] };
                button.AddToClassList(MxUiThemeTokens.PanelTab);
                _tabs.Add(button);
                Add(button);
            }

            Select(Mathf.Clamp(activeIndex, 0, Math.Max(0, _tabs.Count - 1)), false);
        }

        public void Select(int index)
        {
            Select(index, true);
        }

        private void Select(int index, bool notify)
        {
            if (_tabs.Count == 0)
            {
                ActiveIndex = 0;
                return;
            }

            ActiveIndex = Mathf.Clamp(index, 0, _tabs.Count - 1);
            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].EnableInClassList(MxUiThemeTokens.PanelTabActive, i == ActiveIndex);

            if (notify)
                TabSelected?.Invoke(ActiveIndex);
        }
    }
}
