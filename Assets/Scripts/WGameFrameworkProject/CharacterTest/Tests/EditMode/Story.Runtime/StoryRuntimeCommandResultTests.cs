using System;
using System.Reflection;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.Tests.Story;
using NUnit.Framework;

namespace MxFramework.Tests.Story.Runtime
{
    public sealed class StoryRuntimeCommandResultTests
    {
        [Test]
        public void PublicContract_FreezesNumericStatusAndFlatResultShape()
        {
            Assert.AreEqual(0, (int)StoryRuntimeCommandResultStatus.None);
            Assert.AreEqual(1, (int)StoryRuntimeCommandResultStatus.Succeeded);
            Assert.AreEqual(2, (int)StoryRuntimeCommandResultStatus.Rejected);

            FieldInfo[] fields = typeof(StoryRuntimeCommandResult).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                Type type = fields[i].FieldType;
                Assert.AreNotEqual(typeof(string), type, fields[i].Name);
                Assert.AreNotEqual(typeof(object), type, fields[i].Name);
                Assert.IsFalse(typeof(Delegate).IsAssignableFrom(type), fields[i].Name);
                Assert.IsFalse(
                    type.Namespace != null &&
                    type.Namespace.Contains("Gameplay.Triggers"),
                    fields[i].Name);
            }
        }

        [Test]
        public void EnvelopedRaiseTrigger_PublishesExactSuccessOnce()
        {
            var director = new StoryDirector();
            director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph());
            var sink = new CaptureSink();
            var module = new StoryRuntimeModule(
                director,
                null,
                null,
                null,
                sink);
            RuntimeCommand command = EnvelopedRaiseTrigger(
                RuntimeFrame.Zero,
                StoryTestGraphs.TriggerId,
                param0: int.MinValue,
                param1: int.MaxValue,
                targetId: 77);
            Assert.IsTrue(module.CommandBuffer.Enqueue(command).Success);

            module.Tick(new RuntimeTickContext(0L, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(1, sink.Count);
            StoryRuntimeCommandResult result = sink.Last;
            Assert.AreEqual(RuntimeFrame.Zero, result.Frame);
            Assert.AreEqual(StoryRuntimeCommandSources.GameplayBridge, result.SourceId);
            Assert.AreEqual(StoryRuntimeCommandIds.RaiseTrigger, result.CommandId);
            Assert.AreEqual(77, result.TargetId);
            Assert.AreEqual(StoryTestGraphs.TriggerId, result.CommandPayload0);
            Assert.AreEqual(int.MinValue, result.CommandPayload1);
            Assert.AreEqual(int.MaxValue, result.CommandPayload2);
            Assert.AreEqual(0L, result.CommandSequence);
            Assert.IsTrue(result.TraceIdIsEmpty);
            Assert.AreEqual(command.Envelope, result.Envelope);
            Assert.AreEqual(StoryRuntimeCommandResultStatus.Succeeded, result.Status);
            Assert.AreEqual(StoryDirectorResultCode.Success, result.DirectorResultCode);
            Assert.AreEqual(StoryTestGraphs.GraphId, result.ResultPayload0);
            Assert.AreEqual(StoryTestGraphs.EntryBeatId, result.ResultPayload1);
            Assert.Greater(result.ResultPayload2, 0L);
            Assert.AreEqual(1, director.CreateSnapshot().ActiveBeatInstances.Count);
        }

        [Test]
        public void EnvelopedRaiseTrigger_PublishesExactSemanticRejectionOnce()
        {
            var sink = new CaptureSink();
            var module = new StoryRuntimeModule(
                new StoryDirector(),
                null,
                null,
                null,
                sink);
            RuntimeCommand command = EnvelopedRaiseTrigger(
                RuntimeFrame.Zero,
                StoryTestGraphs.TriggerId,
                targetId: 88);
            Assert.IsTrue(module.CommandBuffer.Enqueue(command).Success);

            module.Tick(new RuntimeTickContext(0L, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(1, sink.Count);
            Assert.AreEqual(1, module.LastCommandErrors.Count);
            Assert.AreEqual(StoryRuntimeCommandResultStatus.Rejected, sink.Last.Status);
            Assert.AreEqual(StoryDirectorResultCode.TriggerNotFound, sink.Last.DirectorResultCode);
            Assert.AreEqual(StoryTestGraphs.TriggerId, sink.Last.ResultPayload0);
            Assert.AreEqual(0L, sink.Last.ResultPayload1);
            Assert.AreEqual(0L, sink.Last.ResultPayload2);
        }

        [Test]
        public void DefaultEnvelopeOtherCommandsAndValidationReject_PublishNothing()
        {
            var director = new StoryDirector();
            director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph());
            var sink = new CaptureSink();
            var module = new StoryRuntimeModule(
                director,
                null,
                null,
                null,
                sink);

            Assert.IsTrue(module.CommandBuffer.Enqueue(
                StoryRuntimeCommandFactory.RaiseTrigger(
                    RuntimeFrame.Zero,
                    StoryRuntimeCommandSources.GameplayBridge,
                    StoryTestGraphs.TriggerId)).Success);
            Assert.IsTrue(module.CommandBuffer.Enqueue(new RuntimeCommand(
                RuntimeFrame.Zero,
                StoryRuntimeCommandSources.TestDriver,
                StoryRuntimeCommandIds.RequestEnterBeat,
                0,
                StoryTestGraphs.GraphId,
                StoryTestGraphs.EntryBeatId,
                0,
                RuntimeTraceIdentity.None,
                0L,
                Envelope())).Success);
            Assert.IsTrue(module.CommandBuffer.Enqueue(new RuntimeCommand(
                RuntimeFrame.Zero,
                StoryRuntimeCommandSources.GameplayBridge,
                StoryRuntimeCommandIds.RaiseTrigger,
                1,
                0,
                0,
                0,
                RuntimeTraceIdentity.None,
                0L,
                Envelope())).Success);

            module.Tick(new RuntimeTickContext(0L, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(0, sink.Count);
            Assert.AreEqual(1, module.LastCommandErrors.Count);
        }

        [Test]
        public void DefaultAndCustomValidatorContractsHaveDistinctInternalIdentity()
        {
            var director = new StoryDirector();
            var defaultValidator = new StoryRuntimeCommandValidator(director);
            var customValidator = new StoryRuntimeCommandValidator(
                director,
                StoryRuntimeCommandRegistry.CreateDefault());

            Assert.AreSame(
                StoryRuntimeCommandValidator.DefaultCommandContractIdentity,
                defaultValidator.CommandContractIdentity);
            Assert.IsNull(customValidator.CommandContractIdentity);
        }

        private static RuntimeCommand EnvelopedRaiseTrigger(
            RuntimeFrame frame,
            int triggerId,
            int param0 = 0,
            int param1 = 0,
            int targetId = 1)
        {
            return new RuntimeCommand(
                frame,
                StoryRuntimeCommandSources.GameplayBridge,
                StoryRuntimeCommandIds.RaiseTrigger,
                targetId,
                triggerId,
                param0,
                param1,
                RuntimeTraceIdentity.None,
                0L,
                Envelope());
        }

        private static RuntimeCommandEnvelope Envelope()
        {
            return new RuntimeCommandEnvelope(
                1002105,
                1,
                RuntimeCommandProvenanceKind.Derived,
                1L,
                0L,
                StoryRuntimeCommandIds.RaiseTrigger,
                1L,
                0L,
                1L,
                0L,
                0L);
        }

        private sealed class CaptureSink : IStoryRuntimeCommandResultSink
        {
            public int Count { get; private set; }
            public StoryRuntimeCommandResult Last { get; private set; }

            public void Publish(in StoryRuntimeCommandResult result)
            {
                Count++;
                Last = result;
            }
        }
    }
}
