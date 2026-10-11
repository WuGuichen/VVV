using System;
using System.IO;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestActionRuntimePreparationTests
    {
        [Test]
        public void AllDiscoveredCharacters_PrepareAnimationBindingsBeforeSessionCreation()
        {
            var runtime = CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(runtime.IsReady);
            Assert.DoesNotThrow(() => runtime.PrepareAnimationDefinitions(new CharacterTestCharacterDefinitionRegistry()));
        }

        [Test]
        public void NormalRuntimeLoad_PreservesTunedEventOrdersAndRemainsReady()
        {
            var runtime = CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(runtime.ImportResult.Success,
                string.Join("\n", Array.ConvertAll(runtime.ImportResult.Diagnostics, diagnostic => diagnostic.ToString())));
            Assert.IsTrue(runtime.IsReady);
            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.AreaConfinementActionId, out var action));
            var events = action.MotionTrack.Events;
            Assert.AreEqual(3, events.Length);
            foreach (var evt in events)
            {
                Assert.Greater(evt.StableEventId, 0);
                Assert.Greater(evt.StableEventOrder, 0);
                Assert.AreEqual(CharacterTestAuthoredContentIds.GetEventOrder(evt.StableEventId), evt.StableEventOrder);
            }
        }

        [Test]
        public void CurrentOpener_CanRebuildSummaryThroughProductionActionGateway()
        {
            string directory = Path.Combine(Path.GetTempPath(), "mx-action-preparation-" + Guid.NewGuid().ToString("N"));
            try
            {
                var measurement = CharacterTestTacticalSummaryPreparation.StandardMeasurement();
                using var probe = CharacterActionReferenceProbeSession.ForCurrentOpener(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, measurement.ReferenceDummy);
                Assert.IsTrue(CharacterTestTacticalSummaryPreparation.TryDescribe(
                    probe.Runtime, probe.Source, measurement, out var request, out string issue), issue);
                var store = new CharacterActionTacticalMetricsStore(directory);
                Assert.IsTrue(CharacterTestTacticalSummaryPreparation.TryRebuild(request, store, out var result, out issue), issue);
                Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, result.Status);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void SharedCatalog_CreatesRuntimesWithIdenticalStaticCatalogAndIsolatedExecutionState()
        {
            var catalog = CharacterTestCharacterActionConfigSource.LoadCatalog();
            Assert.IsNotNull(catalog);
            Assert.IsTrue(catalog.IsReady);

            var serverRuntime = new CharacterTestCharacterActionRuntime(catalog);
            var clientRuntime = new CharacterTestCharacterActionRuntime(catalog);

            Assert.AreSame(serverRuntime.Catalog, clientRuntime.Catalog);
            Assert.AreSame(serverRuntime.Registry, clientRuntime.Registry);
            Assert.AreSame(serverRuntime.ImportResult, clientRuntime.ImportResult);
            Assert.AreNotSame(serverRuntime, clientRuntime);

            Assert.AreEqual(0, serverRuntime.ActiveExecutionCount);
            Assert.AreEqual(0, clientRuntime.ActiveExecutionCount);

            var entityId = new CharacterTestCharacterEntityId(1);
            serverRuntime.AllocateExecutionGatewayForTesting(entityId);

            Assert.AreEqual(1, serverRuntime.ActiveExecutionCount);
            Assert.AreEqual(0, clientRuntime.ActiveExecutionCount);
        }
    }
}
