using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Configuration/manifest preparation only; no production combat consumers or publication.</summary>
    public sealed class TacticalAccessorySecondBatchConfigurationTests
    {
        private static CharacterTacticalAssemblySnapshot Snapshot(params CharacterTacticalAccessoryDefinition[] rows)
        {
            var snapshot = TacticalFixedAssemblyManifestTests.Snapshot();
            snapshot.Quality = CharacterTacticalCardQuality.Rainbow; snapshot.AccessorySlotCount = 3;
            snapshot.Accessories = rows; snapshot.Assembly.Accessories.Clear();
            for (int i = 0; i < rows.Length; i++) snapshot.Assembly.Accessories.Add(new CharacterTacticalAccessorySlot {
                SlotIndex = i, AccessoryItemInstanceId = 81 + i, Reference = rows[i].Reference });
            return snapshot;
        }
        private static bool Freeze(CharacterTacticalAssemblySnapshot snapshot, out TacticalFixedAssemblyManifest manifest, out string issue)
            => TacticalFixedAssemblyManifest.TryCreate(snapshot, TacticalFixedAssemblyManifestTests.Main(), 11, 7, 3,
                new Dictionary<string, int> { { "main.action", 17 } }, out manifest, out issue);
        private static void Validate(CharacterTacticalAssemblyPublication p)
            => CharacterTacticalAssemblyRules.ValidateConfiguration(p.Configuration, new CharacterTacticalContentCatalog(),
                p.BuildItemDefinitions(TacticalDoubleEdgePublication.LegacyRegistry()), new Dictionary<int, string>());

        [Test] public void ThreeExplicitCandidatesPassTheActualConfigurationValidatorAndJsonRoundTrip()
        {
            var p = TacticalAccessorySecondBatchPublication.Create(null); Validate(p);
            CollectionAssert.AreEqual(new[] { 3004, 3008, 3018 }, p.Configuration.Accessories.Select(a => a.ItemDefinitionId));
            CollectionAssert.AreEqual(new[] { 1, 1, 2 }, p.Configuration.Accessories.Select(a => a.Execution.ConfirmationMomentumCost.Value));
            Assert.IsTrue(p.Configuration.Accessories.All(a => a.Quality == CharacterTacticalCardQuality.Silver && a.Execution.Version == 3));
            Validate(JsonConvert.DeserializeObject<CharacterTacticalAssemblyPublication>(JsonConvert.SerializeObject(p)));
            Assert.IsTrue(Freeze(Snapshot(p.Configuration.Accessories), out var manifest, out var issue), issue);
            Assert.IsTrue(manifest.HasSecondBatch); Assert.IsFalse(manifest.HasDoubleEdge);
            Assert.IsTrue(manifest.SecondBatchParameters.NoTurningBack && manifest.SecondBatchParameters.NonLethalOath && manifest.SecondBatchParameters.OnePunch);
            Assert.AreEqual(1500, manifest.SecondBatchParameters.RearScaleMilli); Assert.AreEqual(2000, manifest.SecondBatchParameters.OnePunchScaleMilli);
            Assert.AreEqual(4, manifest.AccessoryConfirmationCost);
        }

        [Test] public void CombinedCatalogPreservesFirstBatchExactRowsAndItems()
        {
            var first = TacticalDoubleEdgePublication.Create(null); string frozen = JsonConvert.SerializeObject(first);
            var combined = TacticalAccessorySecondBatchPublication.CreateWithFirstBatch(null); Validate(combined);
            Assert.AreEqual(6, combined.Configuration.Accessories.Length); Assert.AreEqual(6, combined.Items.Length);
            Assert.AreEqual(2, combined.Configuration.Revision);
            for (int i = 0; i < 3; i++) {
                Assert.AreEqual(JsonConvert.SerializeObject(first.Configuration.Accessories[i]), JsonConvert.SerializeObject(combined.Configuration.Accessories[i]));
                Assert.AreEqual(JsonConvert.SerializeObject(first.Items[i]), JsonConvert.SerializeObject(combined.Items[i]));
            }
            Assert.AreEqual(frozen, JsonConvert.SerializeObject(TacticalDoubleEdgePublication.Create(null)));
            Assert.IsFalse(frozen.Contains("SecondBatch"), "Absent v3 data must not change v1/v2 serialized identities.");
        }

        [TestCase(0)][TestCase(2)]
        public void ManifestCopiesAndFingerprintsSecondBatchValues(int index)
        {
            var row = TacticalAccessorySecondBatchPublication.Create(null).Configuration.Accessories[index]; var snapshot = Snapshot(row);
            Assert.IsTrue(Freeze(snapshot, out var before, out _)); string fingerprint = before.Fingerprint;
            if (index == 0) row.Execution.SecondBatch.RearScaleMilli += 100; else row.Execution.SecondBatch.OnePunchScaleMilli += 100;
            Assert.AreEqual(index == 0 ? 1500 : 2000, index == 0 ? before.SecondBatchParameters.RearScaleMilli : before.SecondBatchParameters.OnePunchScaleMilli);
            Assert.IsTrue(Freeze(snapshot, out var after, out _)); Assert.AreNotEqual(fingerprint, after.Fingerprint);
            Assert.AreEqual(fingerprint, before.Fingerprint);
        }

        [TestCase("v1-with-second")][TestCase("v2-with-second")][TestCase("v3-with-double")][TestCase("v3-missing-second")]
        [TestCase("missing-rear")][TestCase("missing-punch")][TestCase("wrong-duration")][TestCase("wrong-lifetime")]
        [TestCase("missing-fee")][TestCase("rear-not-bonus")][TestCase("mixed-scale")]
        public void MalformedPayloadsCannotFreezeOrValidate(string mutation)
        {
            var p = TacticalAccessorySecondBatchPublication.Create(null); var row = p.Configuration.Accessories[0]; var e = row.Execution;
            switch (mutation) {
                case "v1-with-second": e.Version = 1; break;
                case "v2-with-second": e.Version = 2; break;
                case "v3-with-double": e.DoubleEdge = new TacticalDoubleEdgeExecutionDefinition(); break;
                case "v3-missing-second": e.SecondBatch = null; break;
                case "missing-rear": e.SecondBatch.RearScaleMilli = null; break;
                case "missing-punch": e.SecondBatch.OnePunchScaleMilli = null; break;
                case "wrong-duration": e.DurationTurns = 2; break;
                case "wrong-lifetime": e.Lifetime = TacticalAccessoryLifetimeKind.UntilBattleEnd; e.DurationTurns = 0; break;
                case "missing-fee": e.ConfirmationMomentumCost = null; break;
                case "rear-not-bonus": e.SecondBatch.RearScaleMilli = 1000; break;
                case "mixed-scale": e.SecondBatch.OnePunchScaleMilli = 2000; break;
                default: Assert.Fail("Unexpected fixture mutation"); break;
            }
            Assert.IsFalse(e.IsValid); Assert.IsFalse(Freeze(Snapshot(row), out _, out _));
            p.Configuration.ContentHash = CharacterTacticalAssemblyRules.ConfigurationHash(p.Configuration);
            Assert.Throws<JsonSerializationException>(() => Validate(p));
        }

        [Test] public void DuplicateMechanismUnderDifferentPublicationIdentityIsRejected()
        {
            var first = TacticalAccessorySecondBatchPublication.Create(null).Configuration.Accessories[0];
            var second = JsonConvert.DeserializeObject<CharacterTacticalAccessoryDefinition>(JsonConvert.SerializeObject(first));
            second.Reference.AccessoryId = "fixture.alias-rear"; second.ItemDefinitionId = 9999;
            Assert.IsFalse(Freeze(Snapshot(first, second), out _, out var issue));
            Assert.AreEqual("fixed-assembly:DuplicateSecondBatchKindUnsupported", issue);
        }

        [Test] public void StrictAuthoringPayloadRejectsUnknownFields()
            => Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<TacticalAccessorySecondBatchExecutionDefinition>(
                "{\"RearScaleMilli\":1500,\"OnePunchScaleMilli\":0,\"SkipCausalValidation\":true}"));

        [Test] public void ExistingV2PayloadCannotCarrySecondBatchEvenWhenOtherwiseValid()
        {
            var e = TacticalDoubleEdgePublication.Create(null).Configuration.Accessories[0].Execution;
            Assert.IsTrue(e.IsValid);
            e.SecondBatch = new TacticalAccessorySecondBatchExecutionDefinition { RearScaleMilli = 1500, OnePunchScaleMilli = 0 };
            Assert.IsFalse(e.IsValid);
        }
    }
}
