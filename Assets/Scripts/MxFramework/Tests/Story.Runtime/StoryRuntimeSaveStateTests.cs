using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.Tests.Story;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace MxFramework.Tests.Story.Runtime
{
    public class StoryRuntimeSaveStateTests
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include
        };

        [Test]
        public void JsonRoundtripRestoresHash()
        {
            var source = new StoryDirector();
            source.LoadGraph(StoryTestGraphs.MinimalChoiceGraph(waitForPresentation: true));
            source.TryEnterBeat(StoryTestGraphs.GraphId, StoryTestGraphs.EntryBeatId, default);
            StoryBeatInstanceSnapshot waiting = source.CreateSnapshot().ActiveBeatInstances[0];
            source.CompletePresentation(waiting.BeatInstanceId, StoryTestGraphs.PresentationStepId);
            int choiceBeatInstanceId = source.CreateSnapshot().ActiveBeatInstances[0].BeatInstanceId;

            long before = Hash(source);
            RuntimeSaveStateResult<RuntimeSaveState> captured = new StoryRuntimeSaveStateProvider(source, () => 9L).CaptureSaveState();
            string json = RuntimeSaveStateJson.SaveToJson(captured.Value);
            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(json);
            var restored = new StoryDirector();
            RuntimeSaveStateResult<bool> restore = new StoryRuntimeSaveStateProvider(restored).RestoreSaveState(loaded.Value);

            Assert.IsTrue(captured.Success);
            Assert.IsTrue(loaded.Success);
            Assert.IsTrue(restore.Success);
            Assert.AreEqual(before, Hash(restored));

            StoryChoiceResult result = restored.TryResolveChoice(choiceBeatInstanceId, StoryTestGraphs.FirstChoiceId);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void RestoreInvalidPayloadsReturnsErrorAndDoesNotMutate()
        {
            foreach (InvalidRestoreCase restoreCase in InvalidRestoreCases())
            {
                StoryDirector target = ProgressedDirector();
                long before = Hash(target);
                var provider = new StoryRuntimeSaveStateProvider(target);
                RuntimeSaveStateResult<bool> result = default;

                Assert.DoesNotThrow(
                    () => result = provider.RestoreSaveState(SaveStateFor(restoreCase.State)),
                    restoreCase.Name);

                Assert.IsFalse(result.Success, restoreCase.Name);
                Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code, restoreCase.Name);
                Assert.AreEqual(before, Hash(target), restoreCase.Name);
            }
        }

        private static long Hash(StoryDirector director)
        {
            return RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new StoryRuntimeHashContributor(director) });
        }

        private static StoryDirector ProgressedDirector()
        {
            var director = new StoryDirector();
            director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph(waitForPresentation: true));
            director.TryEnterBeat(StoryTestGraphs.GraphId, StoryTestGraphs.EntryBeatId, default);
            StoryBeatInstanceSnapshot waiting = director.CreateSnapshot().ActiveBeatInstances[0];
            director.CompletePresentation(waiting.BeatInstanceId, StoryTestGraphs.PresentationStepId);
            return director;
        }

        private static RuntimeSaveState SaveStateFor(StoryDirectorSaveState state)
        {
            string payload = JsonConvert.SerializeObject(state, JsonSettings);
            return new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                "story-runtime-s1",
                string.Empty,
                string.Empty,
                0L,
                null,
                null,
                new[]
                {
                    new RuntimeModuleSaveState(
                        StoryRuntimeSaveStateProvider.ModuleId,
                        StoryRuntimeSaveStateProvider.SchemaVersion,
                        new RuntimeCustomState(
                            StoryRuntimeSaveStateProvider.CustomStateTypeId,
                            StoryRuntimeSaveStateProvider.SchemaVersion,
                            payload))
                },
                null);
        }

        private static IEnumerable<InvalidRestoreCase> InvalidRestoreCases()
        {
            yield return new InvalidRestoreCase(
                "invalid fact key",
                new StoryDirectorSaveState(
                    StoryDirectorSaveState.CurrentSchemaVersion,
                    1,
                    new[] { LoadedGraph() },
                    null,
                    new[] { new StoryFactEntry(default, StoryValue.FromInt32(1)) }));

            yield return new InvalidRestoreCase(
                "duplicate beat instance id",
                new StoryDirectorSaveState(
                    StoryDirectorSaveState.CurrentSchemaVersion,
                    2,
                    new[] { ActiveGraph() },
                    new[] { WaitingBeat(1), WaitingBeat(1) },
                    null));

            yield return new InvalidRestoreCase(
                "invalid step cursor",
                new StoryDirectorSaveState(
                    StoryDirectorSaveState.CurrentSchemaVersion,
                    2,
                    new[] { ActiveGraph() },
                    new[] { WaitingBeat(1, currentStepIndex: 1) },
                    null));

            yield return new InvalidRestoreCase(
                "missing graph reference",
                new StoryDirectorSaveState(
                    StoryDirectorSaveState.CurrentSchemaVersion,
                    2,
                    new[] { LoadedGraph() },
                    new[] { WaitingBeat(1, graphId: StoryTestGraphs.GraphId + 999) },
                    null));

            yield return new InvalidRestoreCase(
                "missing beat reference",
                new StoryDirectorSaveState(
                    StoryDirectorSaveState.CurrentSchemaVersion,
                    2,
                    new[] { ActiveGraph() },
                    new[] { WaitingBeat(1, beatId: StoryTestGraphs.EntryBeatId + 999) },
                    null));

            yield return new InvalidRestoreCase(
                "next beat instance id reuse",
                new StoryDirectorSaveState(
                    StoryDirectorSaveState.CurrentSchemaVersion,
                    1,
                    new[] { ActiveGraph() },
                    new[] { WaitingBeat(1) },
                    null));
        }

        private static StoryGraphSaveState LoadedGraph()
        {
            return new StoryGraphSaveState(
                StoryTestGraphs.MinimalChoiceGraph(waitForPresentation: true),
                StoryGraphRuntimeStatus.Loaded);
        }

        private static StoryGraphSaveState ActiveGraph()
        {
            return new StoryGraphSaveState(
                StoryTestGraphs.MinimalChoiceGraph(waitForPresentation: true),
                StoryGraphRuntimeStatus.Active);
        }

        private static StoryBeatInstanceSaveState WaitingBeat(
            int beatInstanceId,
            int graphId = StoryTestGraphs.GraphId,
            int beatId = StoryTestGraphs.EntryBeatId,
            int currentStepIndex = 2)
        {
            return new StoryBeatInstanceSaveState(
                graphId,
                beatId,
                beatInstanceId,
                currentStepIndex,
                StoryTestGraphs.PresentationStepId,
                StoryPresentationWaitPolicy.WaitForCommand,
                0);
        }

        private readonly struct InvalidRestoreCase
        {
            public InvalidRestoreCase(string name, StoryDirectorSaveState state)
            {
                Name = name;
                State = state;
            }

            public string Name { get; }
            public StoryDirectorSaveState State { get; }
        }
    }
}
