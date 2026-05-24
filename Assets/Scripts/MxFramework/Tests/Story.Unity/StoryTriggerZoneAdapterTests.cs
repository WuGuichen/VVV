using System.Collections.Generic;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.Story.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.StoryUnity
{
    public sealed class StoryTriggerZoneAdapterTests
    {
        [Test]
        public void EnqueuesRaiseTriggerCommand()
        {
            var buffer = new RuntimeCommandBuffer();
            var frameProvider = new StoryUnityManualFrameProvider(new RuntimeFrame(12));
            var gameObject = new GameObject("story-trigger-zone-test");
            try
            {
                StoryTriggerZoneAdapter adapter = gameObject.AddComponent<StoryTriggerZoneAdapter>();
                adapter.Bind(buffer, frameProvider);
                adapter.TriggerId = 301;
                adapter.Param0 = 7;
                adapter.Param1 = 8;
                adapter.TargetId = 99;
                adapter.TraceId = "zone-a";

                StoryUnityCommandResult result = adapter.RaiseTrigger();

                Assert.IsTrue(result.Success);
                Assert.AreEqual(1, buffer.PendingCount);

                IReadOnlyList<RuntimeCommand> commands = buffer.DrainForFrame(new RuntimeFrame(12));
                Assert.AreEqual(1, commands.Count);
                RuntimeCommand command = commands[0];
                Assert.AreEqual(StoryRuntimeCommandIds.RaiseTrigger, command.CommandId);
                Assert.AreEqual(StoryRuntimeCommandSources.UnityAdapter, command.SourceId);
                Assert.AreEqual(99, command.TargetId);
                Assert.AreEqual(301, command.Payload0);
                Assert.AreEqual(7, command.Payload1);
                Assert.AreEqual(8, command.Payload2);
                Assert.AreEqual("zone-a", command.TraceId);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RaiseTriggerDoesNotMutateDirectorBeforeRuntimeDrainsCommand()
        {
            var director = new StoryDirector();
            director.LoadGraph(CreateTriggerGraph());
            var module = new StoryRuntimeModule(director);
            var gameObject = new GameObject("story-trigger-zone-boundary-test");
            try
            {
                StoryTriggerZoneAdapter adapter = gameObject.AddComponent<StoryTriggerZoneAdapter>();
                adapter.Bind(module);
                adapter.TriggerId = 301;

                StoryUnityCommandResult result = adapter.RaiseTrigger();

                Assert.IsTrue(result.Success);
                Assert.AreEqual(1, module.CommandBuffer.PendingCount);
                Assert.AreEqual(0, director.CreateSnapshot().ActiveBeatInstances.Count);

                module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

                Assert.AreEqual(0, module.LastCommandErrors.Count);
                Assert.AreEqual(1, director.CreateSnapshot().ActiveBeatInstances.Count);
            }
            finally
            {
                module.Dispose();
                Object.DestroyImmediate(gameObject);
            }
        }

        private static StoryGraphDefinition CreateTriggerGraph()
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
                        },
                        triggerIds: new[] { 301 })
                });
        }
    }
}
