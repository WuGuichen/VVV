using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalCardLoadoutRuntimeTests
    {
        [Test]
        public void SameSourceRunsTwoRealLoadoutsThroughTheSharedHostAndFormalCombat()
        {
            var measurement = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 100, 0, 10000,
                new CharacterActionReferenceDummy(2, 5000, 2200, 0, 2500, -4000));
            var rows = new JArray();
            string sourceFingerprint = null;
            int[] actions = new int[2];
            int[] characters = { CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                CharacterTestPlayableCharacterCatalog.ComputeCharacterId("000003") };
            for (int loadout = 0; loadout < actions.Length; loadout++)
            {
                using (var fixture = new CharacterTestTacticalCardRuntimeFixture(actionId: actions[loadout],
                    sourceCharacterId: characters[loadout], measurement: measurement))
                {
                    actions[loadout] = fixture.Inputs.Action.ActionId.Value;
                    if (loadout > 0) Assert.AreNotEqual(actions[0], actions[loadout]);
                    if (sourceFingerprint == null) sourceFingerprint = fixture.Definition.SemanticFingerprint;
                    else Assert.AreEqual(sourceFingerprint, fixture.Definition.SemanticFingerprint);
                    // The fixture already places the real target at the formally verified surface optimum.
                    // A surface distance cannot be used directly as the target's root coordinate.
                    fixture.AdvanceWorldOnly();
                    int health = fixture.Target.CombatState.Health;
                    Assert.IsTrue(fixture.Start(), fixture.Card.LastIssue);
                    bool played = false;
                    bool hit = false;
                    for (int i = 0; i < fixture.Inputs.DurationFrames + 40 && fixture.Card.IsExecuting; i++)
                    {
                        fixture.Step();
                        var sample = fixture.Samples[fixture.Samples.Count - 1];
                        played |= sample.ActionId == actions[loadout] && sample.ActionInstance > 0;
                        hit |= sample.HitCount > 0 || sample.TargetHealth < health;
                    }
                    Assert.IsTrue(played, "No formal action instance for loadout " + actions[loadout] + ": " + fixture.Card.LastIssue);
                    Assert.IsTrue(hit, "No formal combat effect for loadout " + actions[loadout]);
                    Assert.IsFalse(fixture.Card.IsExecuting, "The card did not finish its complete action duration.");
                    Assert.IsFalse(fixture.Source.Input.HasActiveMove);
                    Assert.AreNotEqual(2, fixture.Source.Input.BehaviorControl.FacingMode);
                    rows.Add(new JObject
                    {
                        ["actionId"] = actions[loadout], ["sourceFingerprint"] = sourceFingerprint,
                        ["executionIdentity"] = fixture.Card.Compilation.Program.Execution.Identity,
                        ["formalActionObserved"] = played, ["formalHitObserved"] = hit,
                        ["samples"] = JArray.FromObject(fixture.Samples)
                    });
                }
            }
            string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/Task03ExecutionEvidence"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "two-loadouts-runtime.json"), new JObject
            {
                ["passed"] = true, ["referenceKind"] = "explicit-giant-offset-test-capsule-v2",
                ["evidenceKind"] = "shared-host-formal-command-motion-action-combat", ["loadouts"] = rows
            }.ToString(Formatting.Indented));
        }
    }
}
