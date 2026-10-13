using System;
using System.Linq;
using MxFramework.Tests.Items;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Actual Stage/ACK path. Native execution belongs exclusively to the unified coordinator.</summary>
    [Category("AccessorySecondBatchNative")][Parallelizable(ParallelScope.None)]
    public sealed class TacticalAccessorySecondBatchNativeTests
    {
        private float _scale;
        [SetUp] public void Setup() => _scale = UnityEngine.Time.timeScale;
        [TearDown] public void Teardown() => UnityEngine.Time.timeScale = _scale;
        private static JointInteractionPausedSessionNativeTests.Fixture Fixture(int? executionBudgetTicks = null)
            => new JointInteractionPausedSessionNativeTests.Fixture(executionBudgetTicks: executionBudgetTicks,
                transformPlayerDefinition: source => source.Kind != TacticalCardKind.Action ? source :
                new TacticalActionCardDefinition(source.Id, source.Name, source.Description, source.BaseMomentumCost,
                    source.PublishedCardId, source.PublishedRevision, source.PublishedContentHash,
                    source.DeckEntryInstanceId == 0 ? 90 : source.DeckEntryInstanceId, source.IconKey));
        private static TacticalCardInstance Attach(JointInteractionPausedSessionNativeTests.Fixture f)
        {
            var card = f.Mixed.Stage.Deck.Hand.Single(c => c.Definition.Kind == TacticalCardKind.Action); var d = card.Definition;
            var snapshot = TacticalFixedAssemblyManifestTests.Snapshot();
            snapshot.Quality = CharacterTacticalCardQuality.Rainbow; snapshot.AccessorySlotCount = 3;
            snapshot.Assembly.MainCard.CardId = d.PublishedCardId; snapshot.Assembly.MainCard.Revision = d.PublishedRevision;
            snapshot.Assembly.MainCard.ContentHash = d.PublishedContentHash;
            snapshot.Accessories = TacticalAccessorySecondBatchPublication.Create(null).Configuration.Accessories;
            snapshot.Assembly.Accessories = snapshot.Accessories.Select((a, i) => new CharacterTacticalAccessorySlot {
                SlotIndex = i, AccessoryItemInstanceId = 81 + i, Reference = a.Reference }).ToList();
            Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(snapshot, d, 11, 7, 3,
                f.Deployment.Participants.Single(p => p.IsPlayerControlled).Bindings[d.DeckEntryInstanceId], out var assembly, out var issue), issue);
            d.AttachFixedAssembly(assembly); return card;
        }


        [Test] public void ActualJointSubmissionProducesSecondBatchReceiptsAndPreservesOneHp()
        {
            using var f = Fixture(); var card = Attach(f); var stage = f.Mixed.Stage;
            var world = f.Slice.CharacterRuntime.World; var target = f.Mixed.CurrentTarget;
            world.SetCurrentAttribute(target.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 1);
            ulong turn = stage.TurnIndex;
            f.Apply(TacticalStageCommandKind.SelectAction, card);
            f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            for (int i = 0; i < 1600 && (stage.TurnIndex == turn || stage.Phase != TacticalStagePhase.Planning); i++) f.Tick();
            Assert.AreEqual(TacticalStageHealth.Running, stage.Health, stage.ExecutionIssue);
            Assert.AreEqual(TacticalStagePhase.Planning, stage.Phase, stage.ExecutionIssue);
            Assert.Greater(stage.TurnIndex, turn); Assert.AreEqual(0, stage.Ledger.Reserved);
            Assert.AreEqual(1, world.GetCurrentAttribute(target.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp));
            var rows = world.DoubleEdgeState.CaptureProjection();
            Assert.IsTrue(rows.Any(row => row[0] == 3), "Actual action ACK, never injected lineage.");
            Assert.IsTrue(rows.Any(row => row[0] == 8), "Actual classified hit receipts are required.");
            Assert.AreEqual(1, rows.Single(row => row[0] == 6)[10], "A real first contact consumes the one punch.");
            Assert.IsTrue(rows.Any(row => row[0] == 9), "One ordinary interrupt attempt survives zero HP debit.");
        }

        [Test] public void ActualSettledSecondBatchTrustedRestoreRetainsClosedRoundsAndRejectsForgedWinner()
        {
            using var f = Fixture(); var card = Attach(f); var stage = f.Mixed.Stage; ulong turn = stage.TurnIndex;
            f.Apply(TacticalStageCommandKind.SelectAction, card); f.Apply(TacticalStageCommandKind.ConfirmExecution, card);
            for (int i = 0; i < 1600 && (stage.TurnIndex == turn || stage.Phase != TacticalStagePhase.Planning); i++) f.Tick();
            Assert.AreEqual(TacticalStagePhase.Planning, stage.Phase, stage.ExecutionIssue);
            Assert.IsTrue(f.Slice.CharacterRuntime.World.DoubleEdgeState.CaptureProjection().Any(row => row[0] == 8));
            f.Freeze(); string saved = f.Owner.Capture();
            var before = f.Slice.CharacterRuntime.World.DoubleEdgeState.CaptureProjection();
            Assert.IsTrue(f.Owner.TryRestore(saved, out var issue), issue);
            CollectionAssert.AreEqual(before, f.Slice.CharacterRuntime.World.DoubleEdgeState.CaptureProjection());
            var document = Newtonsoft.Json.Linq.JObject.Parse(saved);
            var network = Newtonsoft.Json.Linq.JObject.Parse((string)document["Network"]);
            var rows = (Newtonsoft.Json.Linq.JArray)(network["DoubleEdgeStage"] ?? network["doubleEdgeStage"]);
            var leaf = rows.Single(row => (long)row[0] == 6); leaf[10] = 0;
            document["Network"] = network.ToString(Newtonsoft.Json.Formatting.None);
            var current = f.Slice;
            Assert.IsFalse(f.Owner.TryRestore(document.ToString(), out issue));
            Assert.AreSame(current, f.Slice); Assert.IsTrue(issue.Contains("double-edge"), issue);
        }
    }
}
