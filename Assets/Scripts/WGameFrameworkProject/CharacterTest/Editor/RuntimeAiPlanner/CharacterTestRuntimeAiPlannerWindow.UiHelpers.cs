using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Diagnostics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private static string DescribeActionKind(CharacterTestRuntimeAiPlannerActionKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                    return "输出朝目标移动的 SetControlIntent。";
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                    return "输出相对目标方向的侧移 SetControlIntent。";
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                    return "输出远离目标的 SetControlIntent。";
                case CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack:
                    return "按下 primary attack，不直接写角色输入。";
                case CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot:
                    return "按下指定技能槽，技能槽必须是 1-9。";
                default:
                    return "等待，不提交移动或攻击意图。";
            }
        }

        private static Label CreateSectionTitle(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 5;
            return label;
        }

        private static VisualElement CreateButtonRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 6;
            row.style.minWidth = 0;
            return row;
        }

        private static Button CreateMiniButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.marginRight = 4;
            button.style.marginBottom = 4;
            return button;
        }

        private static void ApplyListRowButtonStyle(Button button, bool fillWidth = true)
        {
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.marginBottom = 3;
            button.style.minWidth = 0;
            button.style.flexShrink = 1;
            button.style.alignSelf = Align.Stretch;
            button.style.overflow = Overflow.Hidden;
            button.style.whiteSpace = WhiteSpace.NoWrap;
            button.style.textOverflow = TextOverflow.Ellipsis;
            if (fillWidth)
                button.style.width = Length.Percent(100);
        }

        private static Button CreatePrimaryButton(string text, Action action)
        {
            Button button = CreateMiniButton(text, action);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.backgroundColor = new Color(0.22f, 0.36f, 0.54f);
            return button;
        }

        private static VisualElement CreateEmptyListAction(
            string message,
            string buttonText,
            Action action)
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;

            Label label = CreateWrappedLabel(message);
            label.style.marginBottom = 8;
            root.Add(label);
            root.Add(CreatePrimaryButton(buttonText, action));
            return root;
        }

        private static TextField CreateDelayedTextField(string label, string value)
        {
            return new TextField(label)
            {
                value = value ?? string.Empty,
                isDelayed = true
            };
        }

        private static Label CreateWrappedLabel(string text)
        {
            var label = new Label(text ?? string.Empty);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 3;
            label.style.minWidth = 0;
            label.style.flexShrink = 1;
            return label;
        }

        private static VisualElement CreateReadOnlyLine(string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 3;
            row.style.minWidth = 0;

            var name = new Label(label);
            name.style.width = 145;
            name.style.flexShrink = 0;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(name);
            Label text = CreateWrappedLabel(value);
            text.style.flexGrow = 1;
            row.Add(text);
            return row;
        }

        private static VisualElement CreateSpacer(float height)
        {
            var spacer = new VisualElement();
            spacer.style.height = height;
            return spacer;
        }

        private static HelpBox CreateNotice(string text)
        {
            var box = new HelpBox(text, HelpBoxMessageType.Info);
            box.style.marginBottom = 8;
            return box;
        }

        private static TextField CreateReportField(string text)
        {
            var field = new TextField
            {
                multiline = true,
                isReadOnly = true,
                value = text ?? string.Empty
            };
            field.style.flexGrow = 1;
            field.style.minHeight = 220;
            field.style.marginBottom = 8;
            return field;
        }

        private static TextField CreateCompactReportField(string text)
        {
            TextField field = CreateReportField(text);
            field.style.flexGrow = 0;
            field.style.minHeight = 90;
            return field;
        }

    }
}
