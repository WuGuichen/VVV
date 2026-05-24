using System.Collections.Generic;
using MxFramework.Runtime;
using MxFramework.Story.Runtime;
using MxFramework.Story.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.StoryUnity
{
    public sealed class StoryPresentationCompletionAdapterTests
    {
        [Test]
        public void EnqueuesCompletePresentationCommand()
        {
            var buffer = new RuntimeCommandBuffer();
            var frameProvider = new StoryUnityManualFrameProvider(new RuntimeFrame(24));
            var gameObject = new GameObject("story-presentation-completion-test");
            try
            {
                StoryPresentationCompletionAdapter adapter = gameObject.AddComponent<StoryPresentationCompletionAdapter>();
                adapter.Bind(buffer, frameProvider);
                adapter.BeatInstanceId = 42;
                adapter.StepId = 77;
                adapter.GraphId = 101;
                adapter.TraceId = "timeline-complete";

                StoryUnityCommandResult result = adapter.CompletePresentation();

                Assert.IsTrue(result.Success);
                IReadOnlyList<RuntimeCommand> commands = buffer.DrainForFrame(new RuntimeFrame(24));
                Assert.AreEqual(1, commands.Count);
                RuntimeCommand command = commands[0];
                Assert.AreEqual(StoryRuntimeCommandIds.CompletePresentation, command.CommandId);
                Assert.AreEqual(StoryRuntimeCommandSources.PresentationAdapter, command.SourceId);
                Assert.AreEqual(101, command.TargetId);
                Assert.AreEqual(42, command.Payload0);
                Assert.AreEqual(77, command.Payload1);
                Assert.AreEqual(0, command.Payload2);
                Assert.AreEqual("timeline-complete", command.TraceId);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
