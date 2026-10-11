using System;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalCardBindingTests
    {
        private string _directory;
        private CharacterActionReferenceProbeSession _session;
        private CharacterActionTacticalMetricsStore _store;
        private CharacterActionSpatialMeasurement _measurement;
        private CharacterTestTacticalCardRuntimeInputs _inputs;
        private CharacterTestTacticalCardBinding _binding;
        private CharacterTestTacticalCardDefinition _source;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "../Library/MxFramework/TacticalCardBindingTests", Guid.NewGuid().ToString("N")));
            _measurement = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 50, 0, 5000,
                new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
            _session = CharacterActionReferenceProbeSession.ForCurrentOpener(CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, _measurement.ReferenceDummy);
            _store = new CharacterActionTacticalMetricsStore(_directory);
            var measured = _store.Refresh(_session, _measurement);
            Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, measured.Status, measured.Issue);
            _inputs = new CharacterTestTacticalCardRuntimeInputs(_session.Runtime, _session.Source.Id.Value, _store, _measurement);
            _binding = new CharacterTestTacticalCardBinding(_inputs);
            _source = new CharacterTestTacticalCardDefinition("Approach and attack", CharacterTestTacticalCardApproach.OptimalRange);
        }

        [TearDown]
        public void TearDown()
        {
            _binding?.Release();
            _session?.Dispose();
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "../Library/MxFramework/TacticalCardBindingTests")) + Path.DirectorySeparatorChar;
            if (_directory != null && _directory.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void FailedEffectiveEquipmentCannotStartRetainedProgramAndDoesNotRollBackEquipment()
        {
            Assert.IsTrue(_binding.TryCompile(_source), _binding.LastIssue);
            var original = _binding.RetainedCompilation;
            Assert.IsTrue(_binding.TryAcquire(_source, out var acquired), _binding.LastIssue);
            Assert.AreSame(original, acquired);
            Assert.IsTrue(_binding.IsExecuting);
            Assert.IsFalse(_binding.TryCompile(_source));
            Assert.AreEqual("binding:ExecutingRecompileForbidden", _binding.LastIssue);
            Assert.AreSame(original, _binding.RetainedCompilation);
            Assert.IsFalse(_binding.TryAcquire(_source, out acquired));
            Assert.IsNull(acquired);
            _binding.Release();
            var savedA = _binding.CaptureState();
            var equipmentA = _session.Source.Equipment;
            _session.Source.SetEquipment(new CharacterTestCharacterEquipmentComponent(false, "tactical-test.invalid-B"));
            Assert.IsFalse(_binding.TryCompile(_source));
            Assert.AreSame(original, _binding.RetainedCompilation, "Failed B compilation preserves only A's stored artifact.");
            Assert.AreEqual("tactical-test.invalid-B", _session.Source.Equipment.EquipmentStateStableId);
            Assert.IsFalse(_binding.TryAcquire(_source, out acquired));
            Assert.IsNull(acquired, "The host cannot receive old action/movement/turning data from a rejected acquisition.");
            Assert.IsFalse(_binding.IsExecuting);
            Assert.IsFalse(_binding.TryValidateRestore(_source, savedA), "A saved valid binding cannot bypass current B.");
            Assert.AreEqual("tactical-test.invalid-B", _session.Source.Equipment.EquipmentStateStableId);
            _session.Source.SetEquipment(equipmentA);
            Assert.IsTrue(_binding.TryValidateRestore(_source, savedA), _binding.LastIssue);
            Assert.IsTrue(_binding.TryAcquire(_source, out acquired), _binding.LastIssue);
            Assert.AreSame(original, acquired, "A is available again only after its actual dependencies match.");
            _binding.Release();
        }

        [Test]
        public void TacticalSourceChangeInvalidatesBindingButDisplayRenameDoesNot()
        {
            Assert.IsTrue(_binding.TryCompile(_source), _binding.LastIssue);
            var original = _binding.RetainedCompilation;
            var renamed = new CharacterTestTacticalCardDefinition("New display name", _source.Approach);
            Assert.IsTrue(_binding.TryAcquire(renamed, out var acquired), _binding.LastIssue);
            Assert.AreSame(original, acquired);
            _binding.Release();
            var stationary = new CharacterTestTacticalCardDefinition(_source.Name, CharacterTestTacticalCardApproach.None);
            Assert.IsFalse(_binding.TryAcquire(stationary, out acquired));
            Assert.IsNull(acquired);
            Assert.AreEqual("binding:SourceChanged", _binding.LastIssue);
            Assert.IsFalse(_binding.IsExecuting);
            Assert.AreSame(original, _binding.RetainedCompilation);
            Assert.IsTrue(_binding.TryCompile(stationary), _binding.LastIssue);
            Assert.AreNotSame(original, _binding.RetainedCompilation);
            Assert.IsTrue(_binding.TryAcquire(stationary, out acquired), _binding.LastIssue);
            Assert.AreEqual(stationary.SemanticFingerprint, acquired.SourceFingerprint);
            Assert.AreEqual(original.DependencyFingerprint, acquired.DependencyFingerprint,
                "Only the tactical choice changed; the measured action dependencies are identical.");
            _binding.Release();
        }

        [Test]
        public void RestoreRejectsEverySavedIdentityMismatchAndRechecksCurrentSummary()
        {
            Assert.IsTrue(_binding.TryCompile(_source), _binding.LastIssue);
            var valid = _binding.CaptureState();
            valid = Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterTestTacticalCardBindingState>(
                Newtonsoft.Json.JsonConvert.SerializeObject(valid));
            Assert.IsTrue(_binding.TryValidateRestore(_source, valid), _binding.LastIssue);
            var invalid = new[]
            {
                new CharacterTestTacticalCardBindingState(valid.Version + 1, valid.ProgramIdentity, valid.SourceFingerprint, valid.DependencyFingerprint, false),
                new CharacterTestTacticalCardBindingState(valid.Version, valid.ProgramIdentity ^ 1L, valid.SourceFingerprint, valid.DependencyFingerprint, false),
                new CharacterTestTacticalCardBindingState(valid.Version, valid.ProgramIdentity, MxFramework.Runtime.RuntimeHashDigest.ComputeContentDigest("changed-source"), valid.DependencyFingerprint, false),
                new CharacterTestTacticalCardBindingState(valid.Version, valid.ProgramIdentity, valid.SourceFingerprint, MxFramework.Runtime.RuntimeHashDigest.ComputeContentDigest("changed-dependency"), false)
            };
            foreach (var state in invalid)
            {
                Assert.IsFalse(_binding.TryValidateRestore(_source, state));
                Assert.IsFalse(_binding.IsExecuting);
            }
            Assert.IsTrue(_binding.TryAcquire(_source, out _), _binding.LastIssue);
            var runningState = _binding.CaptureState();
            Assert.IsTrue(runningState.Executing);
            Assert.IsFalse(_binding.TryValidateRestore(_source, runningState));
            Assert.AreEqual("binding:StopBeforeRestore", _binding.LastIssue);
            _binding.Release();
            string path = _store.GetPath(_session.Source.Identity.CharacterId, _session.Action.ActionId);
            File.Delete(path);
            Assert.IsFalse(_binding.TryValidateRestore(_source, runningState), "An executing snapshot cannot vouch for a missing current summary.");
            Assert.IsFalse(_binding.TryAcquire(_source, out var acquired));
            Assert.IsNull(acquired);
            Assert.IsFalse(_binding.IsExecuting);
            StringAssert.Contains("Missing", _binding.LastIssue);
            Assert.IsNotNull(_binding.RetainedCompilation);
        }

        [Test]
        public void RuntimeInputsCannotReuseEntityReferenceAfterWorldRemoval()
        {
            Assert.IsTrue(_binding.TryCompile(_source), _binding.LastIssue);
            Assert.IsTrue(_session.Runtime.World.Remove(_session.Source.Id));
            Assert.IsFalse(_inputs.TryPrepare(out var inputs, out string issue));
            Assert.IsNull(inputs);
            Assert.AreEqual("binding:SourceEntityUnavailable", issue);
            Assert.IsFalse(_binding.TryAcquire(_source, out var acquired));
            Assert.IsNull(acquired);
        }
    }
}
