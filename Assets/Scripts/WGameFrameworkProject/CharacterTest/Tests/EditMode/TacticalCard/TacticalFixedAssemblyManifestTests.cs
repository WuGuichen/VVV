using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Isolated fixture numbers only. These tests do not publish content or deploy formal defaults.</summary>
    public sealed class TacticalFixedAssemblyManifestTests
    {
        private static TacticalAccessoryExecutionDefinition Execution(TacticalAccessoryCombatKind kind)
            => new TacticalAccessoryExecutionDefinition { Version = 1, Kind = kind,
                Lifetime = TacticalAccessoryLifetimeKind.UntilBattleEnd, DurationTurns = 0,
                ConfirmationMomentumCost = 1, MomentumAmount = kind == TacticalAccessoryCombatKind.PushAway ? 0 : 2,
                ReferenceSpeed = kind == TacticalAccessoryCombatKind.PushAway ? 4000 : 0,
                ReferenceMass = kind == TacticalAccessoryCombatKind.PushAway ? 80000 : 0,
                MinimumMassScale = kind == TacticalAccessoryCombatKind.PushAway ? 100 : 0,
                MaximumMassScale = kind == TacticalAccessoryCombatKind.PushAway ? 4000 : 0,
                MaximumSpeed = kind == TacticalAccessoryCombatKind.PushAway ? 12000 : 0 };

        internal static CharacterTacticalAssemblySnapshot Snapshot()
        {
            var borrow = new CharacterTacticalAccessoryDefinition { ItemDefinitionId = 101,
                Reference = new CharacterTacticalAccessoryReference { AccessoryId = "fixture.borrow", Revision = 1, ContentHash = "borrow-exact" },
                Quality = CharacterTacticalCardQuality.Silver, TriggerContractId = "fixture.borrow.trigger",
                FailureContractId = "fixture.borrow.failure", LifetimeContractId = "fixture.until-end",
                Execution = Execution(TacticalAccessoryCombatKind.BorrowMomentum) };
            var push = new CharacterTacticalAccessoryDefinition { ItemDefinitionId = 102,
                Reference = new CharacterTacticalAccessoryReference { AccessoryId = "fixture.push", Revision = 1, ContentHash = "push-exact" },
                Quality = CharacterTacticalCardQuality.Silver, TriggerContractId = "fixture.push.trigger",
                FailureContractId = "fixture.push.failure", LifetimeContractId = "fixture.until-end",
                Execution = Execution(TacticalAccessoryCombatKind.PushAway) };
            return new CharacterTacticalAssemblySnapshot { Mode = CharacterTacticalAssemblyMode.FixedPreassembled,
                Quality = CharacterTacticalCardQuality.Gold, AccessorySlotCount = 2, Accessories = new[] { push, borrow },
                Assembly = new CharacterTacticalCardAssembly { Schema = CharacterTacticalCardAssembly.CurrentSchema,
                    ConfigurationRevision = 8, ConfigurationHash = "fixture-config-exact",
                    MainCard = new CharacterTacticalCardReference { InstanceId = 40, CardId = "fixture.main", Revision = 2, ContentHash = "main-exact" },
                    Accessories = new List<CharacterTacticalAccessorySlot> {
                        new CharacterTacticalAccessorySlot { SlotIndex = 0, AccessoryItemInstanceId = 82, Reference = push.Reference },
                        new CharacterTacticalAccessorySlot { SlotIndex = 1, AccessoryItemInstanceId = 81, Reference = borrow.Reference } } } };
        }
        internal static TacticalCardDefinition Main()
            => new TacticalActionCardDefinition("fixture.main", "Fixture only", "Unpublished test contract", 3,
                "fixture.main", 2, "main-exact", 90);
        private static bool Freeze(CharacterTacticalAssemblySnapshot snapshot, out TacticalFixedAssemblyManifest manifest)
            => TacticalFixedAssemblyManifest.TryCreate(snapshot, Main(), 11, 7, 3,
                new Dictionary<string, int> { { "main.action", 17 } }, out manifest, out _);

        [Test] public void ManifestTakesPrivateValueCopiesOfWritableSnapshotAndBindingInputs()
        {
            var snapshot = Snapshot(); var bindings = new Dictionary<string, int> { { "main.action", 17 } };
            Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(snapshot, Main(), 11, 7, 3, bindings, out var frozen, out _));
            string hash = frozen.Fingerprint;
            snapshot.Assembly.MainCard.CardId = "changed"; snapshot.Assembly.ConfigurationHash = "changed";
            snapshot.Assembly.Accessories[1].AccessoryItemInstanceId = 999;
            snapshot.Accessories[1].Execution.MomentumAmount = 99; snapshot.Accessories[1].TriggerContractId = "changed";
            snapshot.Accessories[1].Reference.Revision = 99; bindings["main.action"] = 999;
            Assert.AreEqual("fixture.main", frozen.CardId); Assert.AreEqual("fixture-config-exact", frozen.ConfigurationHash);
            Assert.AreEqual(81, frozen.Accessories[0].ItemInstance); Assert.AreEqual(2, frozen.Accessories[0].MomentumAmount);
            Assert.AreEqual(1, frozen.Accessories[0].Revision); Assert.AreEqual("fixture.borrow.trigger", frozen.Accessories[0].TriggerContract);
            Assert.AreEqual(17, frozen.Bindings["main.action"]); Assert.AreEqual(hash, frozen.Fingerprint);
            Assert.AreEqual(40, frozen.HeldCardInstance); Assert.AreEqual(90, frozen.BattleEntryInstance);
        }

        [Test] public void SwappingUiSlotsCannotChangeFixedExecutionOrder()
        {
            var a = Snapshot(); var b = Snapshot();
            b.Assembly.Accessories[0].SlotIndex = 1; b.Assembly.Accessories[1].SlotIndex = 0;
            Array.Reverse(b.Accessories); b.Assembly.Accessories.Reverse();
            Assert.IsTrue(Freeze(a, out var fa)); Assert.IsTrue(Freeze(b, out var fb));
            Assert.AreEqual(TacticalAccessoryCombatKind.BorrowMomentum, fa.Accessories[0].Kind);
            Assert.AreEqual(fa.Accessories[0].AccessoryId, fb.Accessories[0].AccessoryId);
            Assert.AreEqual(fa.Accessories[1].AccessoryId, fb.Accessories[1].AccessoryId);
            Assert.AreEqual(2, fa.AccessoryConfirmationCost); Assert.AreEqual(3, fa.MainConfirmationCost);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void MissingOrInvalidExplicitExecutionParameterCannotFreeze(int mutation)
        {
            var snapshot = Snapshot(); var execution = snapshot.Accessories[1].Execution;
            switch (mutation) {
                case 0: execution.ConfirmationMomentumCost = null; break;
                case 1: execution.DurationTurns = null; break;
                case 2: execution.Version = 0; break;
                case 3: execution.Lifetime = TacticalAccessoryLifetimeKind.Unspecified; break;
                case 4: execution.MaximumSpeed = null; break;
                case 5: execution.MomentumAmount = 0; break; }
            Assert.IsFalse(Freeze(snapshot, out var result)); Assert.IsNull(result);
        }

        [Test] public void LifetimeIsExplicitAndTurnOverflowRejectsWithoutInventedExpiry()
        {
            var snapshot = Snapshot(); Assert.IsTrue(Freeze(snapshot, out var untilEnd));
            Assert.IsTrue(untilEnd.Accessories[0].TryGetLastTurn(2, out var end)); Assert.AreEqual(ulong.MaxValue, end);
            snapshot.Accessories[1].Execution.Lifetime = TacticalAccessoryLifetimeKind.LimitedTurns;
            snapshot.Accessories[1].Execution.DurationTurns = 2; Assert.IsTrue(Freeze(snapshot, out var limited));
            Assert.IsTrue(limited.Accessories[0].TryGetLastTurn(2, out var last)); Assert.AreEqual(3, last);
            Assert.IsFalse(limited.Accessories[0].TryGetLastTurn(ulong.MaxValue, out _));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void FrozenFingerprintIncludesFeeLifetimeMechanismAndExactContract(int mutation)
        {
            var snapshot = Snapshot(); Assert.IsTrue(Freeze(snapshot, out var before));
            switch (mutation) {
                case 0: snapshot.Accessories[1].Execution.ConfirmationMomentumCost = 2; break;
                case 1: snapshot.Accessories[1].Execution.Lifetime = TacticalAccessoryLifetimeKind.LimitedTurns;
                    snapshot.Accessories[1].Execution.DurationTurns = 2; break;
                case 2: snapshot.Accessories[1].Execution.MomentumAmount = 3; break;
                case 3: snapshot.Accessories[1].FailureContractId = "fixture.changed.failure"; break; }
            Assert.IsTrue(Freeze(snapshot, out var after)); Assert.AreNotEqual(before.Fingerprint, after.Fingerprint);
        }

        [Test] public void V2ConfigurationDigestCoversExplicitExecutionWhileV1StaysDataOnly()
        {
            var snapshot = Snapshot(); var config = new CharacterTacticalAssemblyConfiguration {
                Schema = CharacterTacticalAssemblyConfiguration.ExecutableSchema, Revision = 8,
                MainCards = new[] { new CharacterTacticalMainCardDefinition { Card = snapshot.Assembly.MainCard, Quality = snapshot.Quality } },
                Accessories = snapshot.Accessories };
            string first = CharacterTacticalAssemblyRules.ConfigurationHash(config);
            config.Accessories[1].Execution.MomentumAmount = 3;
            Assert.AreNotEqual(first, CharacterTacticalAssemblyRules.ConfigurationHash(config));
            string stable = CharacterTacticalAssemblyRules.ConfigurationHash(config); Array.Reverse(config.Accessories);
            Assert.AreEqual(stable, CharacterTacticalAssemblyRules.ConfigurationHash(config));
            config.Schema = CharacterTacticalAssemblyConfiguration.CurrentSchema;
            foreach (var accessory in config.Accessories) accessory.Execution = null;
            string v1 = CharacterTacticalAssemblyRules.ConfigurationHash(config);
            var reopened = JsonConvert.DeserializeObject<CharacterTacticalAssemblyConfiguration>(JsonConvert.SerializeObject(config));
            Assert.AreEqual(v1, CharacterTacticalAssemblyRules.ConfigurationHash(reopened));
        }
    }
}
