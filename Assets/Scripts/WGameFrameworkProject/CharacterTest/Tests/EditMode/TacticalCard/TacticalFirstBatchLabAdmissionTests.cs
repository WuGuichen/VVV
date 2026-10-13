using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed class TacticalFirstBatchLabAdmissionTests
    {
        [TestCase(TacticalAccessoryCombatKind.DuelInvitation, 1)]
        [TestCase(TacticalAccessoryCombatKind.ShatteredFantasy, 2)]
        [TestCase(TacticalAccessoryCombatKind.BlackHolePocket, 2)]
        public void LabUsesExactReviewedDefinitionAndActualFee(TacticalAccessoryCombatKind kind, int fee)
        {
            var actual = CharacterTestRuntimeAiLabFixedAssembly.Definition(kind);
            Assert.NotNull(actual); Assert.AreEqual(fee, actual.Execution.ConfirmationMomentumCost);
            foreach (var expected in TacticalDoubleEdgePublication.Create(null).Configuration.Accessories)
                if (expected.Execution.Kind == kind) Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(expected), JToken.FromObject(actual)));
        }
        [TestCase(TacticalAccessoryCombatKind.DuelInvitation, 1)]
        [TestCase(TacticalAccessoryCombatKind.ShatteredFantasy, 2)]
        [TestCase(TacticalAccessoryCombatKind.BlackHolePocket, 2)]
        public void ExplicitLabCopyAttachesToTheRealMainManifest(TacticalAccessoryCombatKind kind, int fee)
        {
            var selection = new CharacterTestRuntimeAiLabCardTrialSelection("fixture.main", "action:17") {
                FixedAssembly = true, FixedQuality = CharacterTacticalCardQuality.Silver };
            selection.AccessorySlots[0] = kind;
            Assert.IsNull(CharacterTestRuntimeAiLabFixedAssembly.Validate(selection));
            var main = TacticalFixedAssemblyManifestTests.Main();
            CharacterTestRuntimeAiLabFixedAssembly.Attach(selection, main, 11, new Dictionary<string, int> { { "main.action", 17 } });
            Assert.IsTrue(main.FixedAssembly.HasDoubleEdge); Assert.AreEqual(fee, main.FixedAssembly.AccessoryConfirmationCost);
            Assert.AreEqual(kind, main.FixedAssembly.Accessories[0].Kind);
            Assert.AreEqual(fee, CharacterTestRuntimeAiLabFixedAssembly.ConfirmationCost(selection));
        }
        [Test] public void LabPatchStillRejectsUnregisteredKinds()
        {
            Assert.IsNull(CharacterTestRuntimeAiLabFixedAssembly.Definition((TacticalAccessoryCombatKind)700));
            var selection = new CharacterTestRuntimeAiLabCardTrialSelection("fixture.main", "action:17") {
                FixedAssembly = true, FixedQuality = CharacterTacticalCardQuality.Silver };
            selection.AccessorySlots[0] = (TacticalAccessoryCombatKind)700;
            Assert.IsNotNull(CharacterTestRuntimeAiLabFixedAssembly.Validate(selection));
        }
    }
}
