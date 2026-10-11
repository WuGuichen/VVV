using System;
using MxFramework.Demo.Story;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Demo.FairyGui
{
    public sealed class StoryRuntimeFairyGuiDialogBinder :
        IMxFairyGuiViewBinder<StoryRuntimeVerticalSliceFairyGuiViewModel>
    {
        private readonly IMxUiCommandSink _commandSink;
        private readonly IMxUiTextProvider _textProvider;
        private readonly MxUiViewId _viewId;

        public StoryRuntimeFairyGuiDialogBinder(
            IMxUiCommandSink commandSink,
            IMxUiTextProvider textProvider = null)
            : this(commandSink, textProvider, StoryRuntimeFairyGuiDialogIds.ViewId)
        {
        }

        public StoryRuntimeFairyGuiDialogBinder(
            IMxUiCommandSink commandSink,
            IMxUiTextProvider textProvider,
            MxUiViewId viewId)
        {
            _commandSink = commandSink;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;
            _viewId = viewId;
        }

        public void Bind(IMxFairyGuiComponentHandle component, StoryRuntimeVerticalSliceFairyGuiViewModel viewModel)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            var handle = component as MxFairyGuiComponentHandle;
            if (handle == null)
                throw new ArgumentException("Story FairyGUI dialog binder requires a concrete FairyGUI component handle.", nameof(component));

            viewModel = viewModel ?? new StoryRuntimeVerticalSliceFairyGuiViewModel();
            Fgui.GComponent root = handle.Component;

            SetText(root, StoryRuntimeFairyGuiDialogIds.Title, Resolve(viewModel.TitleText, viewModel.Title));
            SetText(root, StoryRuntimeFairyGuiDialogIds.Phase, viewModel.Phase);
            SetText(root, StoryRuntimeFairyGuiDialogIds.DialogueText, Resolve(viewModel.DialogueLocalizedText, viewModel.DialogueText));
            SetText(root, StoryRuntimeFairyGuiDialogIds.ChoiceText, Resolve(viewModel.ChoiceLocalizedText, viewModel.ChoiceText));
            SetText(root, StoryRuntimeFairyGuiDialogIds.SignalText, viewModel.SignalText);
            SetText(root, StoryRuntimeFairyGuiDialogIds.EventLog, viewModel.EventLogText);
            BindButton(root, StoryRuntimeFairyGuiDialogIds.Continue, StoryRuntimeVerticalSliceUiCommandIds.CompletePresentation, "Continue", viewModel);
            BindChoices(root, viewModel);
            MxFairyGuiFocusNavigation.Configure(
                root,
                new MxFairyGuiFocusNavigationMetadata(
                    _viewId,
                    StoryRuntimeFairyGuiDialogIds.Continue,
                    CreateFocusOrder(viewModel)));
            MxFairyGuiFocusNavigation.RequestDefaultFocus(root);
        }

        private void BindButton(
            Fgui.GComponent root,
            string childName,
            string commandId,
            string fallbackLabel,
            StoryRuntimeVerticalSliceFairyGuiViewModel viewModel)
        {
            Fgui.GButton button = root.GetChild(childName)?.asButton;
            if (button == null)
                return;

            StoryRuntimeVerticalSliceUiCommandDescriptor descriptor = FindCommand(viewModel, commandId, fallbackLabel);
            button.text = descriptor.Label;
            button.enabled = descriptor.Enabled;
            button.onClick.Set(() => EnqueueIfEnabled(commandId, descriptor.Payload, descriptor.Enabled));
        }

        private void BindChoices(
            Fgui.GComponent root,
            StoryRuntimeVerticalSliceFairyGuiViewModel viewModel)
        {
            RemoveDynamicChoiceButtons(root);

            Fgui.GButton template = root.GetChild(StoryRuntimeFairyGuiDialogIds.Choice)?.asButton;
            if (template == null)
                return;

            if (viewModel.Choices == null || viewModel.Choices.Count == 0)
            {
                SetVisible(root, StoryRuntimeFairyGuiDialogIds.ChoiceText, true);
                LayoutStoryFooter(root, hasChoiceList: false);
                StoryRuntimeVerticalSliceUiCommandDescriptor descriptor =
                    FindCommand(viewModel, StoryRuntimeVerticalSliceUiCommandIds.SelectChoice, "Select");
                BindChoiceButton(
                    template,
                    StoryRuntimeFairyGuiDialogIds.Choice,
                    Resolve(viewModel.ChoiceLocalizedText, descriptor.Label),
                    descriptor);
                return;
            }

            SetVisible(root, StoryRuntimeFairyGuiDialogIds.ChoiceText, false);
            LayoutStoryFooter(root, hasChoiceList: true);
            for (int i = 0; i < viewModel.Choices.Count; i++)
            {
                StoryRuntimeVerticalSliceChoiceViewModel choice = viewModel.Choices[i];
                Fgui.GButton button = i == 0 ? template : CreateChoiceButton(root, template, i);
                string name = i == 0 ? StoryRuntimeFairyGuiDialogIds.Choice : StoryRuntimeFairyGuiDialogIds.ChoiceItemPrefix + i;
                BindChoiceButton(button, name, Resolve(choice.LocalizedText, choice.Text), choice.Command);
                LayoutChoiceButton(button, i, viewModel.Choices.Count);
            }
        }

        private void BindChoiceButton(
            Fgui.GButton button,
            string name,
            string label,
            StoryRuntimeVerticalSliceUiCommandDescriptor descriptor)
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
            button.name = StoryRuntimeFairyGuiDialogIds.ChoiceItemPrefix + index;
            button.SetSize(template.width > 0f ? template.width : 128f, template.height > 0f ? template.height : 40f);
            root.AddChild(button);
            return button;
        }

        private static Fgui.GButton CreatePackageChoiceButton()
        {
            Fgui.UIPackage package = Fgui.UIPackage.GetByName(StoryRuntimeFairyGuiDialogIds.PackageId);
            if (package == null)
                return null;

            Fgui.GObject created = Fgui.UIPackage.CreateObject(StoryRuntimeFairyGuiDialogIds.PackageId, StoryRuntimeFairyGuiDialogIds.ChoiceButtonComponentName);
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

            SetPosition(root, StoryRuntimeFairyGuiDialogIds.SignalText, 40f, 338f);
            SetPosition(root, StoryRuntimeFairyGuiDialogIds.EventLog, 40f, 358f);
            SetPosition(root, StoryRuntimeFairyGuiDialogIds.Continue, 408f, 374f);
        }

        private static void RemoveDynamicChoiceButtons(Fgui.GComponent root)
        {
            for (int i = root.numChildren - 1; i >= 0; i--)
            {
                Fgui.GObject child = root.GetChildAt(i);
                if (child != null &&
                    child.name != null &&
                    child.name.StartsWith(StoryRuntimeFairyGuiDialogIds.ChoiceItemPrefix, StringComparison.Ordinal))
                {
                    root.RemoveChildAt(i, true);
                }
            }
        }

        private static string[] CreateFocusOrder(StoryRuntimeVerticalSliceFairyGuiViewModel viewModel)
        {
            int choiceCount = viewModel.Choices != null && viewModel.Choices.Count > 0 ? viewModel.Choices.Count : 1;
            var controls = new string[choiceCount + 1];
            controls[0] = StoryRuntimeFairyGuiDialogIds.Continue;
            controls[1] = StoryRuntimeFairyGuiDialogIds.Choice;
            for (int i = 1; i < choiceCount; i++)
                controls[i + 1] = StoryRuntimeFairyGuiDialogIds.ChoiceItemPrefix + i;

            return controls;
        }

        private void EnqueueIfEnabled(string commandId, object payload, bool enabled)
        {
            if (!enabled)
                return;

            _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, payload));
        }

        private string Resolve(MxUiLocalizedTextRequest request, string fallback)
        {
            if (request.IsValid && _textProvider.TryGetText(request, out string text))
                return text;

            if (request.IsValid && !string.IsNullOrEmpty(request.FallbackText))
                return request.FallbackText;

            return fallback ?? string.Empty;
        }

        private static StoryRuntimeVerticalSliceUiCommandDescriptor FindCommand(
            StoryRuntimeVerticalSliceFairyGuiViewModel viewModel,
            string commandId,
            string fallbackLabel)
        {
            if (viewModel.Commands != null)
            {
                for (int i = 0; i < viewModel.Commands.Count; i++)
                {
                    StoryRuntimeVerticalSliceUiCommandDescriptor command = viewModel.Commands[i];
                    if (string.Equals(command.CommandId, commandId, StringComparison.Ordinal))
                        return command;
                }
            }

            return new StoryRuntimeVerticalSliceUiCommandDescriptor(commandId, fallbackLabel, false, null);
        }

        private static void SetText(Fgui.GComponent root, string childName, string value)
        {
            Fgui.GTextField text = root.GetChild(childName)?.asTextField;
            if (text != null)
                text.text = value ?? string.Empty;
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
