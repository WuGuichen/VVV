using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.UI.Toolkit
{
    public sealed class MxStressBar : VisualElement
    {
        private readonly VisualElement _fill = new VisualElement { name = "stress-bar-fill" };
        private readonly VisualElement _limitLine = new VisualElement { name = "stress-bar-limit-line" };

        public MxStressBar()
        {
            AddToClassList(MxUiThemeTokens.StressBar);
            _fill.AddToClassList(MxUiThemeTokens.StressBarFill);
            _limitLine.AddToClassList(MxUiThemeTokens.StressBarLimitLine);
            Add(_fill);
            Add(_limitLine);
            SetValue(0, 0);
            SetLimitLine(0, 0);
            SetTone(MxUiTone.Neutral);
            SetActive(true);
        }

        public int CurrentValue { get; private set; }

        public int MaxValue { get; private set; }

        public float NormalizedValue { get; private set; }

        public int LimitLineValue { get; private set; }

        public float LimitLineNormalizedValue { get; private set; }

        public MxUiTone Tone { get; private set; }

        public bool IsActive { get; private set; }

        public void SetValue(int current, int max)
        {
            MaxValue = Mathf.Max(0, max);
            CurrentValue = MaxValue > 0 ? Mathf.Clamp(current, 0, MaxValue) : 0;
            NormalizedValue = MaxValue > 0 ? (float)CurrentValue / MaxValue : 0f;
            _fill.style.width = Length.Percent(NormalizedValue * 100f);
        }

        public void SetLimitLine(int limit, int max)
        {
            int safeMax = Mathf.Max(0, max);
            LimitLineValue = safeMax > 0 ? Mathf.Clamp(limit, 0, safeMax) : 0;
            LimitLineNormalizedValue = safeMax > 0 ? (float)LimitLineValue / safeMax : 0f;
            _limitLine.style.left = Length.Percent(LimitLineNormalizedValue * 100f);
            _limitLine.style.display = safeMax > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetTone(MxUiTone tone)
        {
            Tone = tone;
            MxUiThemeTokens.SetStatusTone(_fill, tone);
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            EnableInClassList(MxUiThemeTokens.StressBarActive, active);
            EnableInClassList(MxUiThemeTokens.StressBarInactive, !active);
        }
    }
}
