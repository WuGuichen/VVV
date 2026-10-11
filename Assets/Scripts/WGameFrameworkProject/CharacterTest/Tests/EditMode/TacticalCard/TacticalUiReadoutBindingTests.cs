using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class TacticalUiReadoutBindingTests
    {
        [TestCase(0, 100, false, 1f, true)]
        [TestCase(25, 100, false, 0.75f, true)]
        [TestCase(25, 200, false, 0.875f, true)]
        [TestCase(150, 100, false, 0f, true)]
        [TestCase(0, 100, true, 0f, true)]
        [TestCase(25, 200, true, 0f, true)]
        [TestCase(0, 0, false, 0f, false)]
        [TestCase(0, -1, true, 0f, false)]
        [TestCase(-1, 100, false, 0f, false)]
        public void RemainingBearingContract(int scatter, int limit, bool broken, float expected, bool available)
        {
            var reading = TacticalCohesionReadout.FromScatterPressure(scatter, limit, broken);
            Assert.AreEqual(available, reading.IsAvailable);
            Assert.AreEqual(broken, reading.IsBroken);
            Assert.AreEqual(expected, reading.Remaining01, 0.00001f);
        }

        [Test]
        public void DetachedProductionHeadpadUsesRemainingCapacityAndAuthoritativeBroken()
        {
            using var pad = CharacterTestFairyGuiHeadPad.CreateDetachedForTests(out var root);
            void Sync(int scatter, int limit, PressureBand band) => pad.Sync(1, "Target", 2,
                CharacterTestCharacterControlOwner.RemotePlayer, CharacterTestHeadPadAllegiance.Enemy,
                new CharacterTestCharacterCombatStateComponent(100, 100, 0, 0, -1, 0, 0,
                    scatter, limit, band, lastCohesionBreakFrame: 12, brokenRecoveryDelayFrames: 270),
                true, CharacterTestHeadPadDisplayMode.CombatReadout, null, Vector3.zero, 282);
            var fill = root.GetChild("scatterPressureFill");
            var background = root.GetChild("scatterPressureBack");
            Sync(0, 100, PressureBand.Stable);
            Assert.IsTrue(fill.visible);
            Assert.AreEqual(background.width, fill.width, 0.001f);
            Sync(25, 100, PressureBand.Stable);
            Assert.AreEqual(Mathf.Round(background.width * 0.75f), fill.width, 0.001f);
            Sync(25, 200, PressureBand.Broken);
            Assert.IsFalse(fill.visible, "A larger dynamic limit or elapsed recovery timer must not release Broken.");
            Assert.IsFalse(root.GetChild("cohesionRecoveryFill").visible);
            Assert.IsFalse(root.GetChild("cohesionLimit").visible);
            Sync(0, 200, PressureBand.Stable);
            Assert.IsTrue(fill.visible);
            Assert.AreEqual(background.width, fill.width, 0.001f);
        }

        [Test]
        public void ExistingUitkResourceClonesWithThreeBoundSlots()
        {
            var asset = UnityEngine.Resources.Load<VisualTreeAsset>("TacticalCard/TacticalCardHud");
            Assert.NotNull(asset);
            var tree = asset.CloneTree();
            for (int i = 0; i < 3; i++) Assert.NotNull(tree.Q<Button>("hand" + i));
            Assert.NotNull(tree.Q<Label>("reservedValue"));
            Assert.NotNull(tree.Q<Label>("spendableValue"));
        }

        [Test]
        public void ProductionHudReadsBoundStageAndCommandOwnedReservation()
        {
            float originalScale = Time.timeScale;
            var go = new GameObject("TacticalUiReadoutBindingTest");
            go.SetActive(false);
            try
            {
                var deck = new TacticalDeckManager();
                var ledger = new MomentumLedger(7);
                var time = new TacticalTimeCoordinator(new UnityTacticalTimeAdapter(), new SettleDetector(new SettleDetectorConfig(requiredStableTicks: 3)));
                var port = new UnusedExecutionPort();
                var driver = new TacticalSingleCardDriver(port, time, deck, ledger);
                var card = new TacticalActionCardDefinition("card", "Name", "Description", 10, "card", 3, "9223372036854775807", ulong.MaxValue - 1, "ui.exact");
                var stage = new TacticalStageFlowCoordinator(1, time, deck, ledger, driver, new[] { card }, 17,
                    _ => true, new TacticalStoryStepBinding(11, 21, 31), initialMomentum: 37);
                var pump = new TacticalCommandPump(stage);
                var hud = go.AddComponent<TacticalCardHud>();
                hud.Bind(stage, pump);
                Assert.AreEqual(37, hud.MotionReadout.Initial);
                Assert.AreEqual(MomentumLedger.Maximum, hud.MotionReadout.Capacity);
                var token = new TacticalStoryStepToken(1, 11, 1, 21, 31);
                stage.OnStoryStepStarted(in token);
                stage.OnSpawnBound(1, 0, new GameplayEntityId(1, 1), 0);
                stage.OnSpawnBound(1, 1, new GameplayEntityId(2, 1), 0);
                for (long frame = 1; frame <= 3; frame++)
                {
                    var sample = new SettleSample(frame, false, true, true, false, false, false, true, true, true, false, 0, 0, noActionStarted: true);
                    stage.OnCompletedSimulationTick(frame, in sample, in sample);
                }
                Assert.AreEqual(TacticalStagePhase.Planning, stage.Phase);
                Assert.AreEqual(37, hud.CurrentMomentum);
                var display = hud.GetCardDisplayData(0);
                Assert.AreEqual("ui.exact", display.IconKey);
                Assert.AreEqual(ulong.MaxValue - 1, display.DeckEntryInstanceId);
                Assert.AreEqual("9223372036854775807", display.PublishedContentHash);
                Assert.AreEqual(TacticalCardQualityPresentation.Unspecified, display.Quality);
                Assert.IsTrue(hud.TrySelectHandSlot(0));
                Assert.AreEqual(0, ledger.Reserved, "Submitting the UI command must not reserve before owner consumption.");
                Assert.AreEqual(1, pump.Drain());
                Assert.AreEqual(37, hud.CurrentMomentum);
                Assert.AreEqual(10, hud.ReservedMomentum);
                Assert.AreEqual(27, hud.SpendableMomentum);
                Assert.IsTrue(hud.TryCancelPending());
                Assert.AreEqual(1, pump.Drain());
                Assert.AreEqual(37, ledger.Current);
                Assert.AreEqual(0, hud.ReservedMomentum);
                Assert.AreEqual(0, port.Calls);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); Time.timeScale = originalScale; }
        }

        [Test]
        public async Task PublishedIconSurvivesCatalogLoadoutAndBothDeploymentRoutes()
        {
            var published = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            Assert.IsTrue(published.Success, published.Diagnostic);
            var authored = published.CardCatalog.Cards.First(value => value.TacticalSource != null);
            var definitions = CharacterTestPlayableCharacterCatalog.Discover().ToArray();
            var hero = definitions.First(value => value.TacticalDefaults != null);
            var opponent = definitions.First(value => value.CharacterId != hero.CharacterId);
            string stable = hero.TacticalDefaults.CharacterStableId;
            var catalog = new CharacterTacticalContentCatalog { InitialMotion = 37, Actions = new[] {
                new CharacterTacticalActionDefinition { Id = "test.action", ActionId = 301, ActionLibraryId = "Common", CharacterStableIds = new[] { stable } } } };
            catalog.SetPublishedCards(new[] { authored });
            var metadata = catalog.Cards.Single();
            Assert.AreEqual(authored.Metadata.IconKey, metadata.IconKey);
            var defaults = new CharacterTacticalDefaults { CharacterStableId = stable,
                ActionIds = new List<string> { "test.action" }, Cards = new List<CharacterTacticalCardReference> {
                    new CharacterTacticalCardReference { CardId = authored.StableCardId, Revision = authored.Revision,
                        ContentHash = authored.ContentHash.ToString(CultureInfo.InvariantCulture) } } };
            if (metadata.HasActionSlot) defaults.Bindings.Add(new CharacterTacticalSlotBinding {
                CardId = authored.StableCardId, SlotPath = metadata.ActionSlotPath, TacticalActionId = "test.action" });
            var player = await CharacterTacticalPlayerService.OpenAsync(catalog, new[] { defaults }, new MemoryStore());
            var preparation = await TacticalBattlePreparationService.OpenAsync(player, new MemoryStore(), Array.Empty<CharacterTestCharacterDefinition>());
            var opening = typeof(CharacterTacticalServices).GetField("Opening", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var publishedProperty = opening.GetType().GetProperty("Published", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var previous = publishedProperty.GetValue(opening);
            try
            {
                publishedProperty.SetValue(opening, new CharacterTacticalServiceSet(player, preparation));
                var owned = player.Read(stable);
                var pinned = owned.Cards.Single();
                void AssertCard(TacticalCardDefinition definition)
                {
                    var display = TacticalCardDisplayData.FromDefinition(definition);
                    Assert.AreEqual(authored.Metadata.IconKey, display.IconKey);
                    Assert.AreEqual(pinned.InstanceId, display.DeckEntryInstanceId);
                    Assert.AreEqual(pinned.Revision, display.PublishedRevision);
                    Assert.AreEqual(pinned.ContentHash, display.PublishedContentHash);
                    Assert.AreEqual(TacticalCardQualityPresentation.Unspecified, display.Quality);
                }
                AssertCard(TacticalDeckCatalog.GetLoadout(hero).Cards.Single());
                Assert.IsTrue(TacticalDeployment.TryCreate(hero.CharacterId, opponent.CharacterId, out var legacy, out string issue), issue);
                AssertCard(legacy.Cards.Single());
                var setup = new TacticalBattleSetup { PlayerControlledEntityId = "player", Entities = new List<TacticalBattleEntitySetup> {
                    new TacticalBattleEntitySetup { EntityId = "player", TeamId = 1, CharacterId = hero.CharacterId, CharacterStableId = stable, Loadout = owned },
                    new TacticalBattleEntitySetup { EntityId = "opponent", TeamId = 2, CharacterId = opponent.CharacterId, Loadout = new CharacterTacticalDefaults() } } };
                Assert.IsTrue(TacticalDeployment.TryCreate(setup, out var battlefield, out issue), issue);
                AssertCard(battlefield.Cards.Single());
                Assert.AreEqual(37, battlefield.InitialMotion);
                metadata.ContentHash = "stale-hash";
                Assert.IsNull(catalog.ExactCard(pinned));
                AssertCard(TacticalDeckCatalog.GetLoadout(hero).Cards.Single());
                player.Catalog.Cards.Single().ContentHash = "stale-hash";
                Assert.IsNull(player.Catalog.ExactCard(pinned));
                Assert.IsFalse(TacticalCardDisplayData.FromDefinition(TacticalDeckCatalog.GetLoadout(hero).Cards.Single()).HasAuthoredIcon);
                Assert.IsFalse(TacticalDeployment.TryCreate(setup, out _, out _));
                Assert.IsFalse(TacticalDeployment.TryCreate(hero.CharacterId, opponent.CharacterId, out _, out _));
            }
            finally { publishedProperty.SetValue(opening, previous); }
        }

        private sealed class MemoryStore : ICharacterTacticalStateStore
        {
            private string _json;
            public Task<string> LoadAsync() => Task.FromResult(_json);
            public Task SaveAsync(string value) { _json = value; return Task.CompletedTask; }
            public Task ResetAsync(string value) => SaveAsync(value);
        }
        private sealed class UnusedExecutionPort : ITacticalExecutionPort
        {
            internal int Calls;
            public bool TryPrepare(TacticalCardInstance card, out string issue) { Calls++; issue = "not executing"; return false; }
            public bool TryAdmit(TacticalCardInstance card, out ulong executionId, out string issue) { Calls++; executionId = 0; issue = "not executing"; return false; }
            public ExecutionObservation Observe(ulong executionId) { Calls++; return default; }
            public bool TryAbortPreAction(ulong executionId, string reason) { Calls++; return false; }
        }
    }
}
