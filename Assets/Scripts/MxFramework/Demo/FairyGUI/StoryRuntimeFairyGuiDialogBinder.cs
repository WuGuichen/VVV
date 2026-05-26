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
            BindButton(root, StoryRuntimeFairyGuiDialogIds.Choice, StoryRuntimeVerticalSliceUiCommandIds.SelectChoice, "Select", viewModel);
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
            button.onClick.Set(() => Enqueue(commandId, descriptor.Payload));
        }

        private void Enqueue(string commandId, object payload)
        {
            _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, payload));
        }

        private string Resolve(MxUiLocalizedTextRequest request, string fallback)
        {
            if (request.IsValid && _textProvider.TryGetText(request, out string text))
                return text;

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
    }
}
