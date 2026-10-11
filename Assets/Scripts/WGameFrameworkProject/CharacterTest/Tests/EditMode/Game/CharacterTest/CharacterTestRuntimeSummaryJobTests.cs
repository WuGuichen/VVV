using System;
using System.IO;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeSummaryJobTests
    {
        private string _directory;
        private CharacterActionTacticalMetricsStore _store;
        private CharacterActionSpatialMeasurement _measurement;
        private int Character => CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId;
        private static CharacterTestActionBrowserEntry Attack => new() { ActionId = 301, Name = "测试攻击", Category = CharacterActionCategory.BasicAttack };

        [SetUp] public void SetUp()
        {
            _directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/RuntimeSummaryTests", Guid.NewGuid().ToString("N")));
            _store = new CharacterActionTacticalMetricsStore(_directory);
            _measurement = CharacterTestTacticalSummaryPreparation.StandardMeasurement();
        }
        [TearDown] public void TearDown()
        {
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/RuntimeSummaryTests")) + Path.DirectorySeparatorChar;
            if (_directory.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test] public void FrameBudgetedMeasurementMatchesSynchronousSummaryAndLeavesOtherWorldUntouched()
        {
            using var unrelated = new CharacterActionReferenceProbeSession(new CharacterActionId(301), _measurement.ReferenceDummy);
            Assert.IsTrue(unrelated.TryEvaluate(1000, out _, out string issue), issue);
            unrelated.Runtime.TryGetLastCompletedRuntimeFrame(out var before);
            int targetPosition = unrelated.Target.Transform.ZMillimeters;
            var reference = CharacterActionRangeProber.MeasureSession(unrelated, _measurement);
            unrelated.Runtime.TryGetLastCompletedRuntimeFrame(out before);
            targetPosition = unrelated.Target.Transform.ZMillimeters;
            using var job = new CharacterTestRuntimeSummaryJob(Character, new CharacterTestCharacterEquipmentComponent(true, ""),
                _measurement, new[] { Attack }, () => true, _store);
            job.Tick();
            Assert.IsTrue(job.Running, "First frame should only initialize the isolated session.");
            Assert.IsFalse(File.Exists(_store.GetPath(Character, new CharacterActionId(301))));
            for (int frame = 0; frame < 2000 && job.Running; frame++) job.Tick();
            Assert.IsFalse(job.Running); Assert.AreEqual(1, job.CommittedCount, job.Details);
            CollectionAssert.AreEqual(CharacterActionTacticalSummaryBinary.Write(reference.Summary),
                File.ReadAllBytes(_store.GetPath(Character, new CharacterActionId(301))));
            unrelated.Runtime.TryGetLastCompletedRuntimeFrame(out var after);
            Assert.AreEqual(before, after); Assert.AreEqual(targetPosition, unrelated.Target.Transform.ZMillimeters);
        }

        [TestCase(false)] [TestCase(true)]
        public void CancellationAndContextChangesPreserveExistingBinary(bool changeContext)
        {
            string path = _store.GetPath(Character, new CharacterActionId(301));
            Directory.CreateDirectory(_directory); byte[] original = { 7, 4, 1, 9 }; File.WriteAllBytes(path, original);
            bool current = true;
            using var job = new CharacterTestRuntimeSummaryJob(Character, new CharacterTestCharacterEquipmentComponent(true, ""),
                _measurement, new[] { Attack }, () => current, _store);
            job.Tick(); job.Tick();
            Assert.IsTrue(job.Running);
            if (changeContext) { current = false; job.Tick(); } else job.Cancel();
            Assert.IsFalse(job.Running); Assert.AreEqual(0, job.CommittedCount);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
            Assert.AreEqual(0, Directory.GetFiles(_directory, "*.tmp").Length);
        }

        [Test] public void UnsupportedAndMissingActionsHaveExplicitResultsWithoutNewCache()
        {
            var actions = new[] {
                new CharacterTestActionBrowserEntry { ActionId = 1, Name = "技能", Category = CharacterActionCategory.Skill },
                new CharacterTestActionBrowserEntry { ActionId = 2, Name = "闪避", Category = CharacterActionCategory.Dodge },
                new CharacterTestActionBrowserEntry { ActionId = int.MaxValue, Name = "缺失动作", Category = CharacterActionCategory.BasicAttack } };
            using var job = new CharacterTestRuntimeSummaryJob(Character, new CharacterTestCharacterEquipmentComponent(true, ""),
                _measurement, actions, () => true, _store);
            for (int frame = 0; frame < 20 && job.Running; frame++) job.Tick();
            Assert.IsFalse(job.Running); Assert.AreEqual(0, job.CommittedCount);
            CollectionAssert.AreEqual(new[] { "跳过", "跳过", "失败" }, job.Results.Select(r => r.Status));
            StringAssert.Contains("只支持攻击", job.Results[0].Reason);
            StringAssert.Contains("不需要空间摘要", job.Results[1].Reason);
            Assert.IsFalse(Directory.Exists(_directory) && Directory.GetFiles(_directory, "*.bin").Length > 0);
        }

        [TestCase("ActionSummary:Missing:SummaryMissing")]
        [TestCase("ActionSummary:Unavailable:DefaultSummaryUnavailable:Stale:MeasurementInputsChanged")]
        public void CardPresentationAutomaticallyMeasuresAndDeduplicatesExpiredInputs(string diagnostic)
        {
            var role = CharacterTestTacticalPreparationCatalog.Role(Character);
            using var preparation = new CharacterTestCardSummaryPreparation(_store);
            string path = _store.GetPath(Character, new CharacterActionId(301));
            long summaries = CharacterTestTacticalPreparationCatalog.Revision;
            StringAssert.Contains("自动测量", preparation.Prepare(role, 301, diagnostic));
            long requested = preparation.Revision;
            preparation.Prepare(role, 301, diagnostic);
            Assert.AreEqual(requested, preparation.Revision, "Two cards sharing this action must share one request.");
            Assert.IsFalse(File.Exists(path), "Presentation requests must not synchronously sweep the range.");
            for (int frame = 0; frame < 3000 && preparation.Revision == requested; frame++) preparation.Tick();
            var result = _store.Query(role.Input(301), _measurement);
            Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, result.Status, result.Issue);
            Assert.Greater(CharacterTestTacticalPreparationCatalog.Revision, summaries, "Visible card projections need invalidation.");
            long completed = preparation.Revision;
            preparation.Prepare(role, 301, diagnostic); preparation.Tick();
            Assert.AreEqual(completed, preparation.Revision, "The same inputs must not start another measurement.");
        }

        [Test]
        public void CardPresentationDoesNotMeasureBrokenBakesAndCancelsOnClose()
        {
            var role = CharacterTestTacticalPreparationCatalog.Role(Character);
            using var preparation = new CharacterTestCardSummaryPreparation(_store);
            preparation.Prepare(role, 301, "ActionSummary:Unavailable:WeaponBakeFrameUnavailable:24");
            Assert.AreEqual(0, preparation.Revision);
            preparation.Tick();
            Assert.IsFalse(File.Exists(_store.GetPath(Character, new CharacterActionId(301))));
            preparation.Prepare(role, 301, "ActionSummary:Missing:SummaryMissing");
            preparation.Tick(); preparation.Dispose(); preparation.Tick();
            Assert.IsFalse(File.Exists(_store.GetPath(Character, new CharacterActionId(301))), "Closing cancels unfinished cache writes.");
        }

        [TestCase("ActionSummary:Stale:ProberVersionChanged",true)]
        [TestCase("ActionSummary:Unavailable:DefaultSummaryUnavailable:Stale:SourceGeometryChanged",true)]
        [TestCase("ActionSummary:Missing:SummaryMissing",true)]
        [TestCase("ActionSummary:Unavailable:SummaryLegacyFormatRequiresRemeasure",true)]
        [TestCase("ActionSummary:Unavailable:WeaponBakeFrameUnavailable:24",false)]
        [TestCase("ActionSummary:Stale:MeasurementInputsChanged;SocketBakeFrameUnavailable:24",false)]
        [TestCase("Action:ExplicitSelectionRequired",false)]
        public void PreparationAndAdmissionUseTheSameExpiredSummaryPolicy(string diagnostic,bool expected)
        {
            Assert.AreEqual(expected,CharacterTestTacticalSummaryPreparation.NeedsRebuild(diagnostic));
            Assert.AreEqual(expected,CharacterTestCardSummaryPreparation.NeedsMeasurement(diagnostic));
        }

        [TestCase(false)] [TestCase(true)]
        public void PreviouslyCompletedCardSummaryIsRebuiltWhenItsCacheExpiresAgain(bool missing)
        {
            var role=CharacterTestTacticalPreparationCatalog.Role(Character);
            using var preparation=new CharacterTestCardSummaryPreparation(_store);
            const string diagnostic="ActionSummary:Stale:InputsUpdated";
            preparation.Prepare(role,301,diagnostic);
            for(int frame=0;frame<3000 && preparation.Revision<2;frame++)preparation.Tick();
            string path=_store.GetPath(Character,new CharacterActionId(301));Assert.IsTrue(File.Exists(path));
            long completed=preparation.Revision;
            if(missing)File.Delete(path);
            else
            {
                var json=Newtonsoft.Json.Linq.JObject.Parse(CharacterActionTacticalSummaryJson.Write(CharacterActionTacticalSummaryBinary.ReadFile(path)));
                json["actionContentHash"]=new string('0',64);
                File.WriteAllBytes(path,CharacterActionTacticalSummaryBinary.Write(CharacterActionTacticalSummaryJson.Read(json.ToString())));
            }
            Assert.AreNotEqual(CharacterActionTacticalMetricsLoadStatus.Valid,_store.Query(role.Input(301),_measurement).Status);
            Assert.That(preparation.Prepare(role,301,diagnostic),Does.Contain("自动测量"));
            Assert.Greater(preparation.Revision,completed);
            long queued=preparation.Revision;
            preparation.Prepare(role,301,diagnostic);Assert.AreEqual(queued,preparation.Revision,"Cards share the same replacement request.");
            for(int frame=0;frame<3000 && preparation.Revision==queued;frame++)preparation.Tick();
            Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid,_store.Query(role.Input(301),_measurement).Status);
            long rebuilt=preparation.Revision;preparation.Prepare(role,301,diagnostic);preparation.Tick();
            Assert.AreEqual(rebuilt,preparation.Revision,"A valid summary must not be rebuilt repeatedly.");
        }

        [Test]
        public void EquipmentChangeCancelsOldPreparationAndQueuesCurrentInputs()
        {
            var role=CharacterTestTacticalPreparationCatalog.Role(Character);var original=role.Source.Equipment;
            using var preparation=new CharacterTestCardSummaryPreparation(_store);
            string path=_store.GetPath(Character,new CharacterActionId(301));
            try
            {
                preparation.Prepare(role,301,"ActionSummary:Missing:SummaryMissing");preparation.Tick();
                role.Source.SetEquipment(new CharacterTestCharacterEquipmentComponent(original.UseDefaultLoadout,
                    original.EquipmentStateStableId,original.Version+1));
                preparation.Tick();Assert.IsFalse(File.Exists(path),"Obsolete equipment must not commit a summary.");
                long cancelled=preparation.Revision;
                preparation.Prepare(role,301,"ActionSummary:Missing:SummaryMissing");Assert.Greater(preparation.Revision,cancelled);
                long queued=preparation.Revision;
                for(int frame=0;frame<3000 && preparation.Revision==queued;frame++)preparation.Tick();
                Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid,_store.Query(role.Input(301),_measurement).Status);
            }
            finally{role.Source.SetEquipment(original);}
        }
    }
}
