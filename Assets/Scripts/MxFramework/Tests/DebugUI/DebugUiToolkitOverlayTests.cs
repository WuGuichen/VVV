using MxFramework.DebugUI;
using MxFramework.DebugUI.Toolkit;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace MxFramework.Tests.DebugUI
{
    public sealed class DebugUiToolkitOverlayTests
    {
        [Test]
        public void Bind_HiddenDisablesRootDisplay()
        {
            var host = new VisualElement();
            var binder = new DebugUiOverlayViewModelBinder();
            binder.Build(host);

            binder.Bind(DebugUiDashboardViewModel.Empty, DebugUiVisibility.Hidden, refreshPaused: false);

            Assert.AreEqual(DisplayStyle.None, binder.Root.style.display.value);
        }

        [Test]
        public void Bind_CollapsedShowsCollapsedSummary()
        {
            var host = new VisualElement();
            var binder = new DebugUiOverlayViewModelBinder();
            binder.Build(host);
            var model = new DebugUiDashboardViewModel(
                1,
                new[] { new DebugUiSourceViewModel("RuntimeHost", MxFramework.Diagnostics.FrameworkDebugMode.Runtime, DebugUiSourceStatus.Available, new[] { new DebugUiSectionViewModel("Summary", "ok") }) },
                null);

            binder.Bind(model, DebugUiVisibility.Collapsed, refreshPaused: true);

            VisualElement collapsed = host.Q<VisualElement>(DebugUiToolkitThemeTokens.CollapsedName);
            Assert.AreEqual(DisplayStyle.Flex, collapsed.style.display.value);
            Assert.That(collapsed.Q<Label>().text, Does.Contain("sources=1"));
            Assert.That(collapsed.Q<Label>().text, Does.Contain("paused"));
        }

        [Test]
        public void Bind_ExpandedRendersSourceSection()
        {
            var host = new VisualElement();
            var binder = new DebugUiOverlayViewModelBinder();
            binder.Build(host);
            var model = new DebugUiDashboardViewModel(
                2,
                new[] { new DebugUiSourceViewModel("Gameplay", MxFramework.Diagnostics.FrameworkDebugMode.Runtime, DebugUiSourceStatus.Available, new[] { new DebugUiSectionViewModel("Summary", "entities: 2") }) },
                null);

            binder.Bind(model, DebugUiVisibility.Expanded, refreshPaused: false);

            VisualElement expanded = host.Q<VisualElement>(DebugUiToolkitThemeTokens.ExpandedName);
            Assert.AreEqual(DisplayStyle.Flex, expanded.style.display.value);
            Assert.IsNotNull(host.Q<ScrollView>(DebugUiToolkitThemeTokens.ContentName));
            Assert.That(host.Q<Label>().text, Does.Contain("Debug UI"));
        }
    }
}
