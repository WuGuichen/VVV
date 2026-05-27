using System;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Demo.FairyGui
{
    public sealed class RuntimeAbilitySliceFairyGuiHudBinder : IMxFairyGuiViewBinder<RuntimeAbilitySliceHudViewModel>
    {
        private readonly IMxUiCommandSink _commandSink;
        private readonly IMxUiTextProvider _textProvider;
        private readonly MxUiViewId _viewId;

        public RuntimeAbilitySliceFairyGuiHudBinder(
            IMxUiCommandSink commandSink,
            IMxUiTextProvider textProvider = null)
            : this(commandSink, textProvider, RuntimeAbilitySliceFairyGuiHudIds.ViewId)
        {
        }

        public RuntimeAbilitySliceFairyGuiHudBinder(IMxUiCommandSink commandSink, MxUiViewId viewId)
            : this(commandSink, null, viewId)
        {
        }

        public RuntimeAbilitySliceFairyGuiHudBinder(
            IMxUiCommandSink commandSink,
            IMxUiTextProvider textProvider,
            MxUiViewId viewId)
        {
            _commandSink = commandSink;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;
            _viewId = viewId;
        }

        public void Bind(IMxFairyGuiComponentHandle component, RuntimeAbilitySliceHudViewModel viewModel)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            var handle = component as MxFairyGuiComponentHandle;
            if (handle == null)
                throw new ArgumentException("RuntimeAbilitySlice FairyGUI HUD binder requires a concrete FairyGUI component handle.", nameof(component));

            viewModel = viewModel ?? new RuntimeAbilitySliceHudViewModel();
            Fgui.GComponent root = handle.Component;

            SetText(root, RuntimeAbilitySliceFairyGuiHudIds.Title, Resolve("ui.runtimehud.title", viewModel.Title, "Runtime HUD"));
            SetText(root, RuntimeAbilitySliceFairyGuiHudIds.Mode, Resolve("ui.runtimehud.mode", viewModel.ModeName, "Mode"));
            SetText(root, RuntimeAbilitySliceFairyGuiHudIds.PlayerName, NonEmpty(viewModel.Player.DisplayName, "Player"));
            SetText(root, RuntimeAbilitySliceFairyGuiHudIds.PlayerHp, FormatHp(viewModel.Player));
            SetText(root, RuntimeAbilitySliceFairyGuiHudIds.EnemyName, NonEmpty(viewModel.Enemy.DisplayName, "Enemy"));
            SetText(root, RuntimeAbilitySliceFairyGuiHudIds.EnemyHp, FormatHp(viewModel.Enemy));
            SetText(root, RuntimeAbilitySliceFairyGuiHudIds.RecentAction, viewModel.Feedback.RecentActionText);
            BindButton(root, RuntimeAbilitySliceFairyGuiHudIds.Strike, RuntimeAbilitySliceHudCommandIds.Strike, "Strike", viewModel);
            BindButton(root, RuntimeAbilitySliceFairyGuiHudIds.Reset, RuntimeAbilitySliceHudCommandIds.Reset, "Reset", viewModel);
            MxFairyGuiFocusNavigation.Configure(
                root,
                new MxFairyGuiFocusNavigationMetadata(
                    _viewId,
                    RuntimeAbilitySliceFairyGuiHudIds.Strike,
                    new[] { RuntimeAbilitySliceFairyGuiHudIds.Strike, RuntimeAbilitySliceFairyGuiHudIds.Reset }));
            MxFairyGuiFocusNavigation.RequestDefaultFocus(root);
        }

        private void BindButton(
            Fgui.GComponent root,
            string childName,
            string commandId,
            string fallbackLabel,
            RuntimeAbilitySliceHudViewModel viewModel)
        {
            Fgui.GButton button = root.GetChild(childName)?.asButton;
            if (button == null)
                return;

            RuntimeAbilitySliceHudCommandDescriptor descriptor = FindCommand(viewModel, commandId, fallbackLabel);
            button.text = descriptor.Label;
            button.enabled = descriptor.Enabled;
            button.onClick.Set(() => Enqueue(commandId));
        }

        private void Enqueue(string commandId)
        {
            _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, null));
        }

        private string Resolve(string key, string fallback, string defaultFallback)
        {
            string fallbackText = string.IsNullOrEmpty(fallback) ? defaultFallback : fallback;
            var request = new MxUiLocalizedTextRequest(new MxUiTextKey(key), fallbackText);
            if (_textProvider.TryGetText(request, out string text))
                return text;

            return fallbackText ?? string.Empty;
        }

        private static RuntimeAbilitySliceHudCommandDescriptor FindCommand(
            RuntimeAbilitySliceHudViewModel viewModel,
            string commandId,
            string fallbackLabel)
        {
            if (viewModel.Commands != null)
            {
                for (int i = 0; i < viewModel.Commands.Count; i++)
                {
                    RuntimeAbilitySliceHudCommandDescriptor command = viewModel.Commands[i];
                    if (string.Equals(command.CommandId, commandId, StringComparison.Ordinal))
                        return command;
                }
            }

            return new RuntimeAbilitySliceHudCommandDescriptor(commandId, fallbackLabel, true);
        }

        private static void SetText(Fgui.GComponent root, string childName, string value)
        {
            Fgui.GTextField text = root.GetChild(childName)?.asTextField;
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private static string FormatHp(RuntimeAbilitySliceHudEntityViewModel entity)
        {
            if (entity == null)
                return string.Empty;

            return "HP " + entity.Hp + "/" + entity.MaxHp
                + "  ATK " + entity.Attack
                + "  DEF " + entity.Defense
                + "  Buffs " + NonEmpty(entity.BuffSummary, "none");
        }

        private static string NonEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
