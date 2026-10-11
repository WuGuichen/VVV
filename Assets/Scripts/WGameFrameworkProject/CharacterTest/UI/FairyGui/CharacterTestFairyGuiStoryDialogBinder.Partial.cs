using System;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiStoryDialogBinder
    {
        partial void OnAfterGeneratedBind(Fgui.GComponent root, CharacterTestFairyGuiStoryDialogViewModel viewModel)
        {
            BindChoices(root, viewModel);
            MxFairyGuiFocusNavigation.Configure(
                root,
                new MxFairyGuiFocusNavigationMetadata(
                    _viewId,
                    CharacterTestFairyGuiStoryDialogIds.Continue,
                    CreateFocusOrder(viewModel)));
            MxFairyGuiFocusNavigation.RequestDefaultFocus(root);
        }

        private static string ResolveContinueLabel(CharacterTestFairyGuiStoryDialogViewModel viewModel)
        {
            return FindCommand(
                viewModel,
                CharacterTestFairyGuiStoryCommandIds.CompletePresentation,
                "Continue").Label;
        }

        private static bool ResolveContinueEnabled(CharacterTestFairyGuiStoryDialogViewModel viewModel)
        {
            return FindCommand(
                viewModel,
                CharacterTestFairyGuiStoryCommandIds.CompletePresentation,
                "Continue").Enabled;
        }

        private static object ResolveContinuePayload(CharacterTestFairyGuiStoryDialogViewModel viewModel)
        {
            return FindCommand(
                viewModel,
                CharacterTestFairyGuiStoryCommandIds.CompletePresentation,
                "Continue").Payload;
        }

        private void BindChoices(Fgui.GComponent root, CharacterTestFairyGuiStoryDialogViewModel viewModel)
        {
            RemoveDynamicChoiceButtons(root);

            Fgui.GButton template = root.GetChild(CharacterTestFairyGuiStoryDialogIds.Choice)?.asButton;
            if (template == null)
                return;

            if (viewModel.Choices == null || viewModel.Choices.Count == 0)
            {
                SetVisible(root, CharacterTestFairyGuiStoryDialogIds.ChoiceText, true);
                LayoutStoryFooter(root, hasChoiceList: false);
                CharacterTestFairyGuiStoryCommandDescriptor descriptor =
                    FindCommand(
                        viewModel,
                        CharacterTestFairyGuiStoryCommandIds.SelectChoice,
                        L("ui.character_test.story.select", "Select"));
                BindChoiceButton(template, CharacterTestFairyGuiStoryDialogIds.Choice, descriptor.Label, descriptor);
                return;
            }

            SetVisible(root, CharacterTestFairyGuiStoryDialogIds.ChoiceText, false);
            LayoutStoryFooter(root, hasChoiceList: true);
            for (int i = 0; i < viewModel.Choices.Count; i++)
            {
                CharacterTestFairyGuiStoryChoiceViewModel choice = viewModel.Choices[i];
                Fgui.GButton button = i == 0 ? template : CreateChoiceButton(root, template, i);
                string name = i == 0
                    ? CharacterTestFairyGuiStoryDialogIds.Choice
                    : CharacterTestFairyGuiStoryDialogIds.ChoiceItemPrefix + i;
                BindChoiceButton(button, name, choice.Text, choice.Command);
                LayoutChoiceButton(button, i, viewModel.Choices.Count);
            }
        }

        private void BindChoiceButton(
            Fgui.GButton button,
            string name,
            string label,
            CharacterTestFairyGuiStoryCommandDescriptor descriptor)
        {
            button.name = name;
            button.text = string.IsNullOrWhiteSpace(label) ? descriptor.Label : label;
            button.enabled = descriptor.Enabled;
            button.visible = true;
            button.onClick.Set(() => EnqueueIfEnabled(descriptor.CommandId, descriptor.Payload, descriptor.Enabled));
        }

        private Fgui.GButton CreateChoiceButton(Fgui.GComponent root, Fgui.GButton template, int index)
        {
            Fgui.GButton button = CreatePackageChoiceButton() ?? new Fgui.GButton();
            button.name = CharacterTestFairyGuiStoryDialogIds.ChoiceItemPrefix + index;
            button.SetSize(template.width > 0f ? template.width : 128f, template.height > 0f ? template.height : 40f);
            root.AddChild(button);
            return button;
        }

        private static Fgui.GButton CreatePackageChoiceButton()
        {
            Fgui.UIPackage package = Fgui.UIPackage.GetByName(CharacterTestFairyGuiStoryDialogIds.PackageId);
            if (package == null)
                return null;

            Fgui.GObject created = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiStoryDialogIds.PackageId,
                CharacterTestFairyGuiStoryDialogIds.ChoiceButtonComponentName);
            return created?.asButton;
        }

        private static void LayoutChoiceButton(Fgui.GButton button, int index, int count)
        {
            const float x = 48f;
            const float y = 228f;
            const float width = 624f;
            const float maxHeight = 32f;
            const float bottom = 336f;
            int safeCount = Math.Max(1, count);
            float gap = safeCount <= 4 ? 6f : Math.Min(2f, 12f / safeCount);
            float available = Math.Max(1f, bottom - y - gap * (safeCount - 1));
            float height = Math.Min(maxHeight, available / safeCount);

            button.SetXY(x, y + (height + gap) * index);
            button.SetSize(width, height);
        }

        private static void LayoutStoryFooter(Fgui.GComponent root, bool hasChoiceList)
        {
            if (!hasChoiceList)
                return;

            SetPosition(root, CharacterTestFairyGuiStoryDialogIds.SignalText, 40f, 338f);
            SetPosition(root, CharacterTestFairyGuiStoryDialogIds.EventLog, 40f, 358f);
            SetPosition(root, CharacterTestFairyGuiStoryDialogIds.Continue, 408f, 374f);
        }

        private static void RemoveDynamicChoiceButtons(Fgui.GComponent root)
        {
            for (int i = root.numChildren - 1; i >= 0; i--)
            {
                Fgui.GObject child = root.GetChildAt(i);
                if (child != null &&
                    child.name != null &&
                    child.name.StartsWith(CharacterTestFairyGuiStoryDialogIds.ChoiceItemPrefix, StringComparison.Ordinal))
                {
                    root.RemoveChildAt(i, true);
                }
            }
        }

        private static string[] CreateFocusOrder(CharacterTestFairyGuiStoryDialogViewModel viewModel)
        {
            int choiceCount = viewModel.Choices != null && viewModel.Choices.Count > 0 ? viewModel.Choices.Count : 1;
            var controls = new string[choiceCount + 1];
            controls[0] = CharacterTestFairyGuiStoryDialogIds.Continue;
            controls[1] = CharacterTestFairyGuiStoryDialogIds.Choice;
            for (int i = 1; i < choiceCount; i++)
                controls[i + 1] = CharacterTestFairyGuiStoryDialogIds.ChoiceItemPrefix + i;

            return controls;
        }

        private void EnqueueIfEnabled(string commandId, object payload, bool enabled)
        {
            if (!enabled)
                return;

            _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, payload));
        }

        private static CharacterTestFairyGuiStoryCommandDescriptor FindCommand(
            CharacterTestFairyGuiStoryDialogViewModel viewModel,
            string commandId,
            string fallbackLabel)
        {
            if (viewModel.Commands != null)
            {
                for (int i = 0; i < viewModel.Commands.Count; i++)
                {
                    CharacterTestFairyGuiStoryCommandDescriptor command = viewModel.Commands[i];
                    if (string.Equals(command.CommandId, commandId, StringComparison.Ordinal))
                        return command;
                }
            }

            return new CharacterTestFairyGuiStoryCommandDescriptor(commandId, fallbackLabel, false, null);
        }

        private static void SetVisible(Fgui.GComponent root, string childName, bool visible)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child != null)
                child.visible = visible;
        }

        private static void SetPosition(Fgui.GComponent root, string childName, float x, float y)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child != null)
                child.SetXY(x, y);
        }
    }
}
