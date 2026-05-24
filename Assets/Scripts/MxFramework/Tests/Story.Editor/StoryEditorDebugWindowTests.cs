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
            Assert.That(host.Q<VisualElement>(StoryEditorDebugWindowView.ContentName).Q<Label>().text, Does.Contain("StoryRuntime"));

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

            var labels = new List<Label>();
            host.Q<VisualElement>(StoryEditorDebugWindowView.ContentName).Query<Label>().ForEach(labels.Add);
            Assert.That(labels[0].text, Does.Contain("状态"));
            Assert.That(labels[1].text, Does.Contain("没有已注册的 Story Runtime 调试源"));
        }
    }
}
