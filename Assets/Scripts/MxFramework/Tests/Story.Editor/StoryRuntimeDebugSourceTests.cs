using System.Linq;
using MxFramework.Diagnostics;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Editor;
using MxFramework.Story.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.StoryEditor
{
    public sealed class StoryRuntimeDebugSourceTests
    {
        [Test]
        public void BuildsSnapshotFromStoryRuntime()
        {
            var director = new StoryDirector();
            var module = new StoryRuntimeModule(director);
            director.LoadGraph(CreateGraph());
            director.Blackboard.Set(new StoryFactKey(101, 701), StoryValue.FromInt32(9));
            director.TryEnterBeat(101, 201, default);
            module.Events.Enqueue(RuntimeFrame.Zero, new StoryRuntimeEvent(RuntimeFrame.Zero, StoryEventKind.StepStarted, graphId: 101, beatId: 201, beatInstanceId: 1, stepId: 401));
            var source = new StoryRuntimeDebugSource(module);

            FrameworkDebugSnapshot snapshot = source.CreateSnapshot();

            Assert.AreEqual("StoryRuntime", snapshot.SourceName);
            Assert.AreEqual(FrameworkDebugMode.Runtime, snapshot.Mode);
            Assert.That(GetBody(snapshot, "摘要"), Does.Contain("activeBeats=1"));
            Assert.That(GetBody(snapshot, "Graphs"), Does.Contain("graph=101"));
            Assert.That(GetBody(snapshot, "Beats"), Does.Contain("beat=201"));
            Assert.That(GetBody(snapshot, "Blackboard"), Does.Contain("101:701=Int32(9)"));
            Assert.That(GetBody(snapshot, "事件队列"), Does.Contain("pending=4"));
        }

        [Test]
        public void UnavailableSourceReturnsReadonlyStatus()
        {
            var source = new StoryRuntimeDebugSource((StoryRuntimeModule)null);

            FrameworkDebugSnapshot snapshot = source.CreateSnapshot();

            Assert.IsFalse(source.IsAvailable);
            Assert.That(GetBody(snapshot, "状态"), Does.Contain("unavailable"));
        }

        private static string GetBody(FrameworkDebugSnapshot snapshot, string title)
        {
            return snapshot.Sections.First(section => section.Title == title).Body;
        }

        private static StoryGraphDefinition CreateGraph()
        {
            return new StoryGraphDefinition(
                101,
                1,
                201,
                new[]
                {
                    new StoryBeatDefinition(
                        201,
                        new[]
                        {
                            new StoryStepDefinition(
                                401,
                                StoryStepKind.Presentation,
                                waitPolicy: StoryPresentationWaitPolicy.WaitForCommand)
                        })
                });
        }
    }
}
