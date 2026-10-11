using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private static Label CreateCompactBadge(string text, Color color, float maxWidth)
        {
            Label label = CreateSingleLineLabel(text, 10, new Color(0.86f, 0.90f, 0.94f));
            label.style.marginRight = 4;
            label.style.paddingLeft = 5;
            label.style.paddingRight = 5;
            label.style.paddingTop = 0;
            label.style.paddingBottom = 0;
            label.style.backgroundColor = color;
            label.style.maxWidth = maxWidth;
            label.style.height = 20;
            label.style.minHeight = 20;
            label.style.minWidth = 0;
            label.style.flexShrink = 1;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.tooltip = text;
            return label;
        }

        private static Label CreateSingleLineLabel(string text, int fontSize, Color color)
        {
            Label label = CreateWrappedLabel(text);
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            return label;
        }

        private static Label CreateMiniBadge(string text, Color color)
        {
            Label label = CreateSingleLineLabel(text, 10, new Color(0.86f, 0.90f, 0.94f));
            label.style.marginRight = 4;
            label.style.marginBottom = 2;
            label.style.paddingLeft = 5;
            label.style.paddingRight = 5;
            label.style.paddingTop = 0;
            label.style.paddingBottom = 0;
            label.style.backgroundColor = color;
            label.style.minHeight = 18;
            label.style.minWidth = 0;
            label.style.flexShrink = 1;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.tooltip = text;
            return label;
        }

        private static VisualElement CreateBehaviorCardSurface(Color background, Color border)
        {
            var surface = new VisualElement();
            surface.style.paddingLeft = 10;
            surface.style.paddingRight = 10;
            surface.style.paddingTop = 10;
            surface.style.paddingBottom = 10;
            surface.style.marginBottom = 8;
            surface.style.backgroundColor = background;
            surface.style.borderTopWidth = 1;
            surface.style.borderBottomWidth = 1;
            surface.style.borderLeftWidth = 1;
            surface.style.borderRightWidth = 1;
            surface.style.borderTopColor = border;
            surface.style.borderBottomColor = border;
            surface.style.borderLeftColor = border;
            surface.style.borderRightColor = border;
            return surface;
        }
    }
}
