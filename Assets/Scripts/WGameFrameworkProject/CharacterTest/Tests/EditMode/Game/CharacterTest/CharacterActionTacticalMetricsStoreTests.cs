using System;
using System.IO;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterActionTacticalMetricsStoreTests
    {
        [Test]
        public void SharedSwordComboSecondSegmentRebuildsWithTwoSeparatedHitWindows()
        {
            int character=CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000009");
            var measurement=CharacterTestTacticalSummaryPreparation.StandardMeasurement();
            using var probe=CharacterActionReferenceProbeSession.ForTestAction(character,measurement.ReferenceDummy,456);
            Assert.IsTrue(probe.TryEvaluate(500,out var preview,out var issue),issue);
            Assert.AreEqual(CharacterTestAttackOpportunityStatus.Valid,preview.Status);
            var rebuilt=_store.Refresh(probe,measurement,requireTacticalValidity:true);
            Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid,rebuilt.Status,rebuilt.Issue);
            Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid,
                _store.Query(CharacterActionTacticalInput.FromSession(probe),measurement).Status);
        }
        private string _directory;
        private CharacterActionTacticalMetricsStore _store;
        private CharacterActionReferenceProbeSession _session;
        private CharacterActionSpatialMeasurement _measurement;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/MxFramework/TacticalStoreTests", Guid.NewGuid().ToString("N")));
            _store = new CharacterActionTacticalMetricsStore(_directory);
            _measurement = Measurement();
            _session = new CharacterActionReferenceProbeSession(new CharacterActionId(301), _measurement.ReferenceDummy);
        }

        [TearDown]
        public void TearDown()
        {
            _session?.Dispose();
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/MxFramework/TacticalStoreTests")) + Path.DirectorySeparatorChar;
            if (_directory != null && _directory.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void MissingRefreshAndNewStoreReloadHaveExplicitLifecycle()
        {
            AssertRejected(Query(), CharacterActionTacticalMetricsLoadStatus.Missing);
            Refresh();
            var loaded = new CharacterActionTacticalMetricsStore(_directory).Query(Inputs(), _measurement);
            Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, loaded.Status, loaded.Issue);
            Assert.IsTrue(loaded.TryGetDefaultSurfaceDistance(out double distance));
            Assert.IsTrue(_session.TryEvaluate((int)Math.Round(distance * 1000), out var actual, out string issue), issue);
            Assert.IsTrue(actual.CanHitFromCurrentPose);
            Assert.AreEqual(0, Directory.GetFiles(_directory, "*.tmp").Length);
            File.Delete(CachePath());
            AssertRejected(Query(), CharacterActionTacticalMetricsLoadStatus.Missing);
        }

        [Test]
        public void CancelledTacticalRefreshPreservesExistingCacheBytes()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(CachePath(), "previous-cache");
            byte[] original = File.ReadAllBytes(CachePath());
            var result = _store.Refresh(_session, _measurement, requireTacticalValidity: true,
                continueMeasurement: (done, total) => false);
            Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Unavailable, result.Status);
            Assert.AreEqual("MeasurementCancelled", result.Issue);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(CachePath()));
            Assert.IsEmpty(Directory.GetFiles(_directory, "*.tmp"));
        }

        [Test]
        public void NoHitTacticalRefreshCannotOverwriteExistingCache()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(CachePath(), "previous-cache");
            byte[] original = File.ReadAllBytes(CachePath());
            var outside = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 50, 20000, 20100, _measurement.ReferenceDummy);
            var result = _store.Refresh(_session, outside, requireTacticalValidity: true);
            Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Unavailable, result.Status);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(CachePath()));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TacticalFallbackKeepsActualActionIdentityAndBorrowedMeasurementSeparate(bool staleOwnFile)
        {
            Refresh();
            using (var other = CharacterActionReferenceProbeSession.ForTestAction(618034977, _measurement.ReferenceDummy, 301))
            {
                var input = CharacterActionTacticalInput.FromSession(other);
                var changedBindings = new ChangedBindings(input.Bindings);
                input = new CharacterActionTacticalInput(input.Action, input.Source, input.Physics, input.Bakes, changedBindings);
                string ownPath = _store.GetPath(other.Source.Identity.CharacterId, other.Action.ActionId);
                byte[] ownBytes = null;
                if (staleOwnFile)
                {
                    var json = JObject.Parse(CharacterActionTacticalSummaryJson.Write(CharacterActionTacticalSummaryBinary.ReadFile(CachePath()))); json["actionContentHash"] = "old-revision";
                    ownBytes = CharacterActionTacticalSummaryBinary.Write(CharacterActionTacticalSummaryJson.Read(json.ToString())); File.WriteAllBytes(ownPath, ownBytes);
                }
                Assert.AreEqual(staleOwnFile ? CharacterActionTacticalMetricsLoadStatus.Stale : CharacterActionTacticalMetricsLoadStatus.Missing,
                    _store.Query(input, _measurement).Status);
                Assert.IsTrue(CharacterActionContentHashUtility.TryCompute(input.Action, input.Source, input.Physics, input.Bakes,
                    input.Bindings, out string actualHash, out string issue), issue);
                LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[Runtime AI Lab\]\[TacticalSummaryFallback\]"));
                var fallback = _store.QueryForTacticalCard(input, _measurement);
                Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, fallback.Status, fallback.Issue);
                Assert.IsTrue(fallback.UsesDefaultSummary);
                Assert.AreEqual(CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, fallback.SummarySourceCharacterId);
                Assert.AreEqual(actualHash, fallback.Metrics.ActionContentHash);
                Assert.IsNull(fallback.Metrics.SpatialSummary, "Borrowed evidence must not masquerade as a matching current-role measurement.");
                Assert.AreEqual(Query().SpatialSummary.ActionContentHash, fallback.SpatialSummary.ActionContentHash);
                Assert.AreNotEqual(actualHash, fallback.SpatialSummary.ActionContentHash);
                Assert.IsTrue(fallback.TryGetDefaultSurfaceDistance(out _));
                Assert.IsTrue(CharacterTestTacticalCardInputs.TryPrepare(other.Runtime.ActionRuntime, other.Source,
                    input.Physics, input.Bakes, changedBindings, _store, _measurement, out var prepared, out issue, input.Action.ActionId.Value), issue);
                Assert.IsTrue(prepared.UsesDefaultSummary);
                Assert.NotNull(prepared.SpatialSummary);
                Assert.IsTrue(CharacterTestTacticalCardCompiler.TryCompile(new CharacterTestTacticalCardDefinition("fallback", CharacterTestTacticalCardApproach.OptimalRange), prepared,
                    out _, out string compilationIssue), compilationIssue);
                _store.QueryForTacticalCard(input, _measurement); // Repeated preparation does not spam warnings.
                LogAssert.NoUnexpectedReceived();
                if (staleOwnFile) CollectionAssert.AreEqual(ownBytes, File.ReadAllBytes(ownPath));
                else Assert.IsFalse(File.Exists(ownPath));
            }
        }

        [Test]
        public void DefaultFallbackNeverFabricatesMissingOrStaleDefaultEvidence()
        {
            using (var other = CharacterActionReferenceProbeSession.ForTestAction(618034977, _measurement.ReferenceDummy, 301))
            {
                var input = CharacterActionTacticalInput.FromSession(other);
                var missing = _store.QueryForTacticalCard(input, _measurement);
                Assert.IsFalse(missing.UsesDefaultSummary); Assert.IsNull(missing.Metrics);
                StringAssert.Contains("DefaultSummaryUnavailable", missing.Issue);
                Refresh();
                var json = JObject.Parse(CharacterActionTacticalSummaryJson.Write(CharacterActionTacticalSummaryBinary.ReadFile(CachePath()))); json["actionContentHash"] = "old-revision";
                File.WriteAllBytes(CachePath(), CharacterActionTacticalSummaryBinary.Write(CharacterActionTacticalSummaryJson.Read(json.ToString())));
                var stale = _store.QueryForTacticalCard(input, _measurement);
                Assert.IsFalse(stale.UsesDefaultSummary); Assert.IsNull(stale.Metrics);
                StringAssert.Contains("Stale", stale.Issue);
            }
        }

        [TestCase("version")]
        [TestCase("step")]
        [TestCase("min")]
        [TestCase("max")]
        [TestCase("dummyVersion")]
        [TestCase("height")]
        [TestCase("radius")]
        [TestCase("x")]
        [TestCase("y")]
        [TestCase("z")]
        public void EveryMeasurementDimensionInvalidates(string dimension)
        {
            Refresh();
            var dummy = new CharacterActionReferenceDummy(dimension == "dummyVersion" ? 2 : 1,
                dimension == "height" ? 1900 : 1800, dimension == "radius" ? 350 : 300,
                dimension == "x" ? 1 : 0, dimension == "y" ? 901 : 900, dimension == "z" ? 1 : 0);
            var changed = new CharacterActionSpatialMeasurement(dimension == "version" ? "future" : CharacterActionRangeProber.Version,
                dimension == "step" ? 100 : 50, dimension == "min" ? 50 : 0, dimension == "max" ? 2050 : 2000, dummy);
            AssertRejected(_store.Query(Inputs(), changed), CharacterActionTacticalMetricsLoadStatus.Stale);
        }

        [TestCase("phase")]
        [TestCase("cancel")]
        [TestCase("animation")]
        [TestCase("rootMotion")]
        [TestCase("hitVolume")]
        public void ActualActionContentChangesAreRejectedByLoader(string dimension)
        {
            Refresh();
            var changed = CharacterActionContentHashTests.LoadAction(action => {
                switch (dimension)
                {
                    case "phase": action["phases"][0]["endFrame"] = 16; break;
                    case "cancel": action["cancelRules"][0]["startFrame"] = 36; break;
                    case "animation": action["animationTrack"]["events"][0]["playbackSpeed"] = 1.1f; break;
                    case "rootMotion": action["motionTrack"]["rootMotion"]["frames"][20]["z"] = 0.25f; break;
                    case "hitVolume": action["combatTrack"]["events"][0]["hitVolume"] = new JObject {
                        ["shapeKind"] = "Capsule", ["radius"] = 0.2f, ["localPointA"] = new JObject { ["y"] = 1 },
                        ["localPointB"] = new JObject { ["y"] = 1, ["z"] = 1 } }; break;
                }
            });
            var input = Inputs();
            AssertRejected(_store.Query(new CharacterActionTacticalInput(
                changed,
                input.Source,
                input.Physics,
                input.Bakes,
                input.Bindings),
                _measurement), CharacterActionTacticalMetricsLoadStatus.Stale);
        }

        [Test]
        public void ActualBakeAndBindingChangesInvalidateWithoutAcceptingCallerHashes()
        {
            Refresh();
            var input = Inputs();
            AssertRejected(_store.Query(new CharacterActionTacticalInput(input.Action, input.Source, input.Physics,
                new ChangedBakes(input.Bakes), input.Bindings), _measurement), CharacterActionTacticalMetricsLoadStatus.Stale);
            AssertRejected(_store.Query(new CharacterActionTacticalInput(input.Action, input.Source, input.Physics,
                input.Bakes, new ChangedBindings(input.Bindings)), _measurement), CharacterActionTacticalMetricsLoadStatus.Stale);
            AssertRejected(_store.Query(new CharacterActionTacticalInput(input.Action, input.Source, input.Physics,
                new ChangedBakes(input.Bakes, missing: true), input.Bindings), _measurement), CharacterActionTacticalMetricsLoadStatus.Unavailable);
        }

        [TestCase("magic")]
        [TestCase("version")]
        [TestCase("checksum")]
        [TestCase("truncated")]
        public void DamagedFilesNeverBecomeValidDefaults(string kind)
        {
            Refresh();
            string path = CachePath();
            byte[] bytes = File.ReadAllBytes(path);
            if (kind == "magic") bytes[0] ^= 1;
            if (kind == "version") bytes[4] = 127;
            if (kind == "checksum") bytes[bytes.Length - 1] ^= 1;
            if (kind == "truncated") Array.Resize(ref bytes, bytes.Length - 5);
            File.WriteAllBytes(path, bytes);
            AssertRejected(Query(), CharacterActionTacticalMetricsLoadStatus.Unavailable);
        }

        [Test]
        public void LegacyJsonRequiresMeasurementAndIsRemovedOnlyAfterSuccess()
        {
            Directory.CreateDirectory(_directory);
            string legacy = Path.ChangeExtension(CachePath(), ".json");
            File.WriteAllText(legacy, "legacy data must never be read");
            string other = Path.Combine(_directory, "source-other-action-301.json");
            File.WriteAllText(other, "unrelated role");
            var missing = Query();
            AssertRejected(missing, CharacterActionTacticalMetricsLoadStatus.Missing);
            Assert.AreEqual("SummaryLegacyFormatRequiresRemeasure", missing.Issue);
            _store.Refresh(_session, _measurement, continueMeasurement: (done, total) => false);
            Assert.IsTrue(File.Exists(legacy));
            Assert.IsFalse(File.Exists(CachePath()));
            Refresh();
            Assert.IsFalse(File.Exists(legacy));
            Assert.IsTrue(File.Exists(other));
            Assert.That(Path.GetExtension(CachePath()), Is.EqualTo(".bin"));
        }

        [Test, Platform("Win")]
        public void LockedBinaryCannotBeReplacedAndOriginalBytesSurvive()
        {
            Refresh();
            byte[] original = File.ReadAllBytes(CachePath());
            using (var locked = new FileStream(CachePath(), FileMode.Open, FileAccess.Read, FileShare.Read))
                AssertRejected(_store.Refresh(_session, _measurement), CharacterActionTacticalMetricsLoadStatus.Unavailable);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(CachePath()));
            Assert.IsEmpty(Directory.GetFiles(_directory, "*.tmp"));
        }

        [Test]
        public void FailedRefreshDoesNotOverwriteOrReturnOldMeasurement()
        {
            Refresh();
            byte[] oldContent = File.ReadAllBytes(CachePath());
            var unsupported = new CharacterActionSpatialMeasurement("unsupported", 50, 0, 2000, _measurement.ReferenceDummy);
            AssertRejected(_store.Refresh(_session, unsupported), CharacterActionTacticalMetricsLoadStatus.Unavailable);
            CollectionAssert.AreEqual(oldContent, File.ReadAllBytes(CachePath()));
            AssertRejected(_store.Query(Inputs(), unsupported), CharacterActionTacticalMetricsLoadStatus.Stale);
            Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, Query().Status);
        }

        [Test]
        public void ValidNoHitReceiptCannotSupplyDefaultDistance()
        {
            var evt = _session.Action.CombatTrack.Events[0];
            _session.Action.CombatTrack.Events[0] = new CombatTrackEvent(evt.Frame, evt.Kind, stableEventId: evt.StableEventId,
                damagePayload: evt.DamagePayload, hitEvent: evt.HitEvent, endFrame: evt.EndFrame,
                hitVolume: CharacterActionHitVolumeConfig.Capsule(0, 10, 0, 0, 10, 1, 0.05f));
            Refresh();
            var result = Query();
            Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, result.Status, result.Issue);
            Assert.AreEqual(CharacterActionProberStatus.NoHit, result.Metrics.SpatialSummary.ProberStatus);
            Assert.IsFalse(result.TryGetDefaultSurfaceDistance(out _));
        }

        private void Refresh()
        {
            var result = _store.Refresh(_session, _measurement);
            Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, result.Status, result.Issue);
        }
        private CharacterActionTacticalInput Inputs() => CharacterActionTacticalInput.FromSession(_session);
        private CharacterActionTacticalMetricsLoadResult Query() => _store.Query(Inputs(), _measurement);
        private string CachePath() => _store.GetPath(_session.Source.Identity.CharacterId, _session.Action.ActionId);
        private static CharacterActionSpatialMeasurement Measurement() => new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version,
            50, 0, 2000, new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
        private static void AssertRejected(CharacterActionTacticalMetricsLoadResult result, CharacterActionTacticalMetricsLoadStatus status)
        {
            Assert.AreEqual(status, result.Status, result.Issue);
            Assert.IsNull(result.Metrics);
            Assert.IsFalse(result.TryGetDefaultSurfaceDistance(out _));
        }

        private sealed class ChangedBakes : ICharacterTestBakedWeaponTraceSource
        {
            private readonly ICharacterTestBakedWeaponTraceSource _inner;
            private readonly bool _missing;
            internal ChangedBakes(ICharacterTestBakedWeaponTraceSource inner, bool missing = false) { _inner = inner; _missing = missing; }
            public bool TryGetFrame(string clip, int profile, int frame, out MxAnimationBakedWeaponTraceFrame value)
            {
                value = null;
                if (_missing || !_inner.TryGetFrame(clip, profile, frame, out var original)) return false;
                value = new MxAnimationBakedWeaponTraceFrame(original.LocalFrame, original.TraceId, original.SocketId,
                    original.RootPrev, original.TipPrev, original.RootNow,
                    new MxAnimationBakedVector3(original.TipNow.X, original.TipNow.Y, original.TipNow.Z + 1));
                return true;
            }
            public bool TryGetSocketFrame(string clip, int socket, int frame, out MxAnimationBakedSocketFrame value) => _inner.TryGetSocketFrame(clip, socket, frame, out value);
            public bool TryResolveSocketTrack(string clip, int socket, out CharacterTestBakedSocketTrack track) => _inner.TryResolveSocketTrack(clip, socket, out track);
        }
        private sealed class ChangedBindings : ICharacterTestWeaponTraceBindingResolver
        {
            private readonly ICharacterTestWeaponTraceBindingResolver _inner;
            internal ChangedBindings(ICharacterTestWeaponTraceBindingResolver inner) { _inner = inner; }
            public bool TryResolve(CharacterTestCharacterEntity entity, int slot, int profile, out CharacterTestWeaponTraceBinding binding)
            {
                if (!_inner.TryResolve(entity, slot, profile, out binding)) return false;
                binding = new CharacterTestWeaponTraceBinding(binding.TraceProfileId, binding.Length, binding.Radius + 0.001f,
                    binding.TraceBinding, binding.EquipmentSlotId, binding.WeaponId, binding.ProximalExtension, binding.ProximalRadius);
                return true;
            }
        }
    }
}
