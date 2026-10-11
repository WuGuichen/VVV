using MxFramework.DebugUI;
using MxFramework.Diagnostics;
using MxFramework.Story.Editor;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace MxFramework.Tests.StoryEditor
{
    public sealed class StoryEditorDebugWindowTests
    {
        [Test]
        public void BuildsReadonlyUiToolkitTree()
        {
            var host = new VisualElement();
            var dashboard = new DebugUiDashboardViewModel(
                1,
                new[]
                {
                    new DebugUiSourceViewModel(
                        "StoryRuntime",
                        FrameworkDebugMode.Runtime,
                        DebugUiSourceStatus.Available,
                        new[] { new DebugUiSectionViewModel("摘要", "activeBeats=1") })
                },
                null);

            StoryEditorDebugWindowView.BuildReadonlyTree(host, dashboard, "source: StoryRuntime");

            VisualElement root = host.Q<VisualElement>(StoryEditorDebugWindowView.RootName);
            Assert.IsNotNull(root);
            Assert.AreEqual("Story 运行时调试", host.Q<Label>(StoryEditorDebugWindowView.TitleName).text);
            var contentLabels = host.Q<VisualElement>(StoryEditorDebugWindowView.ContentName).Query<Label>().ToList();
            Assert.That(contentLabels.Exists(label => label.text.Contains("StoryRuntime")));

            TextField report = host.Q<TextField>(StoryEditorDebugWindowView.ReportName);
            Assert.IsNotNull(report);
            Assert.IsTrue(report.isReadOnly);
            Assert.That(report.value, Does.Contain("StoryRuntime"));
        }

        [Test]
        public void EmptyDashboardShowsNoRegisteredTargetMessage()
        {
            var host = new VisualElement();

            StoryEditorDebugWindowView.BuildReadonlyTree(host, DebugUiDashboardViewModel.Empty, "empty");

            var labels = host.Q<VisualElement>(StoryEditorDebugWindowView.ContentName).Query<Label>().ToList();
            Assert.That(labels.Exists(label => label.text.Contains("状态")));
            Assert.That(labels.Exists(label => label.text.Contains("没有已注册的 Story Runtime 调试源")));
        }
    }
}
