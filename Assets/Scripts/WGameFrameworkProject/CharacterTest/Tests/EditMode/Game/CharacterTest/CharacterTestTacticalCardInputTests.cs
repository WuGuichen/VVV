using System;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalCardInputTests
    {
        [Test]
        public void PublishedActionOnlyCardDoesNotRequireDistanceMeasurement()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateBlank("动作依赖验证");
            source.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "attack",
                Start = CharacterTestTacticalAuthoringTemplates.Absolute(0), Operation = new CharacterTestTacticalAuthoringOperation {
                    Kind = "CharacterAction", SlotId = "primary" } });
            var published = CharacterTestTacticalPublishedSource.Read(new Newtonsoft.Json.Linq.JObject {
                ["schema"] = CharacterTestTacticalPublishedSource.Schema,
                ["legacy"] = Newtonsoft.Json.Linq.JObject.Parse(CharacterTestTacticalAuthoringJson.Write(source)) });
            Assert.AreEqual(CharacterTestTacticalInputRequirements.PrimaryAction, published.InputRequirements);
        }

        [Test]
        public void PublishedApproachCardRetainsRequiredDistanceMeasurement()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateSingleAttack();
            var published = CharacterTestTacticalPublishedSource.Read(new Newtonsoft.Json.Linq.JObject {
                ["schema"] = CharacterTestTacticalPublishedSource.Schema,
                ["legacy"] = Newtonsoft.Json.Linq.JObject.Parse(CharacterTestTacticalAuthoringJson.Write(source)) });
            Assert.AreEqual(CharacterTestTacticalInputRequirements.PrimaryAction | CharacterTestTacticalInputRequirements.SpatialSummary,
                published.InputRequirements);
        }

        [Test]
        public void OpenerUsesFormalPrioritySelectionAndNeverContinuesASequence()
        {
            var first = CharacterActionContentHashTests.LoadAction();
            var second = new CharacterActionConfig(new CharacterActionId(324), "Second opener fixture", first.Category,
                first.TimelineAuthority, first.Tags, first.Priority, first.DurationFrames, first.Requirements,
                first.Phases, first.CancelRules, first.InterruptRules, first.MotionTrack, first.CombatTrack,
                first.GameplayTrack, first.AnimationTrack);
            var sequences = new[]
            {
                new CharacterActionCommandSequence(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D404_Id59, new[] { first.ActionId, second.ActionId }, priority: 2),
                new CharacterActionCommandSequence(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D404_Id59, new[] { second.ActionId, first.ActionId }, priority: 3)
            };
            Assert.IsTrue(CharacterTestTacticalCardInputs.TryResolveOpener(Set(sequences), new[] { first, second },
                out var selected, out string issue), issue);
            Assert.AreEqual(second.ActionId, selected.ActionId);
            sequences[1] = new CharacterActionCommandSequence(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D404_Id59, new[] { second.ActionId }, priority: 2);
            Assert.IsTrue(CharacterTestTacticalCardInputs.TryResolveOpener(Set(sequences), new[] { first, second },
                out selected, out issue), issue);
            Assert.AreEqual(first.ActionId, selected.ActionId, "Equal priority retains the first sequence, not its continuation.");
        }

        [Test]
        public void ExplicitActionIdSelectsEquippedActionDirectly()
        {
            var action = CharacterActionContentHashTests.LoadAction();
            // Set has no command sequences, so TryResolveOpener would fail, but explicit actionId resolves directly
            var set = Set(null);
            Assert.IsFalse(CharacterTestTacticalCardInputs.TryResolveOpener(set, new[] { action }, out _, out string openerIssue));
            StringAssert.Contains("MissingLightAttackSequence", openerIssue);

            Assert.IsTrue(CharacterTestTacticalCardInputs.TryResolveSelectedAction(set, new[] { action },
                action.ActionId.Value, out var selected, out string issue), issue);
            Assert.IsNotNull(selected);
            Assert.AreEqual(action.ActionId, selected.ActionId);
        }

        [Test]
        public void ExplicitActionIdRejectsMissingAction()
        {
            var action = CharacterActionContentHashTests.LoadAction();
            var set = Set(null);
            Assert.IsFalse(CharacterTestTacticalCardInputs.TryResolveSelectedAction(set, new[] { action },
                999999, out var selected, out string issue));
            Assert.IsNull(selected);
            StringAssert.Contains("ActionSlot:SelectedTestActionUnavailable", issue);
        }

        [Test]
        public void ExplicitTacticalActionDoesNotRequireBasicBehaviourMapping()
        {
            var action = CharacterActionContentHashTests.LoadAction();
            Assert.IsTrue(CharacterTestTacticalCardInputs.TryResolveSelectedAction(null, new[] { action },
                action.ActionId.Value, out var resolved, out string issue), issue);
            Assert.AreEqual(action.ActionId, resolved.ActionId);
            Assert.IsFalse(CharacterTestTacticalCardInputs.TryResolveSelectedAction(Set(null), new[] { action },
                0, out _, out issue));
            Assert.AreEqual("Action:ExplicitSelectionRequired", issue);
        }

        [Test]
        public void MissingSequenceCannotFallBackToBindingOrDefaultAction()
        {
            var action = CharacterActionContentHashTests.LoadAction();
            var binding = new[] { new CharacterActionBinding(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D404_Id59, action.ActionId) };
            Assert.IsFalse(CharacterTestTacticalCardInputs.TryResolveOpener(Set(null, binding), new[] { action }, out var value, out string issue));
            Assert.IsNull(value);
            StringAssert.Contains("MissingLightAttackSequence", issue);
            Assert.IsFalse(CharacterTestTacticalCardInputs.TryResolveOpener(
                Set(new[] { new CharacterActionCommandSequence(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D404_Id59, new[] { action.ActionId }) }, binding),
                new[] { action }, out value, out issue));
            Assert.IsNull(value);
            StringAssert.Contains("OpenerRejected", issue);
        }

        [TestCase("gap")]
        [TestCase("missingStartup")]
        [TestCase("multipleActive")]
        [TestCase("duration")]
        [TestCase("noDirectHit")]
        public void UnsupportedActionShapeFailsBeforeSummaryUse(string change)
        {
            var original = CharacterActionContentHashTests.LoadAction();
            var phases = original.Phases;
            if (change == "gap") phases = new[] {
                new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 16),
                new CharacterActionPhase(CharacterActionPhaseKind.Active, 18, 34) };
            if (change == "missingStartup") phases = new[] { new CharacterActionPhase(CharacterActionPhaseKind.Active, 0, 34) };
            if (change == "multipleActive") phases = new[] {
                new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 17),
                new CharacterActionPhase(CharacterActionPhaseKind.Active, 18, 22),
                new CharacterActionPhase(CharacterActionPhaseKind.Active, 23, 34) };
            var changed = new CharacterActionConfig(original.ActionId, original.DisplayName, original.Category,
                original.TimelineAuthority, original.Tags, original.Priority, change == "duration" ? (int?)null : original.DurationFrames,
                original.Requirements, phases, original.CancelRules, original.InterruptRules,
                original.MotionTrack, change == "noDirectHit" ? CombatTrackConfig.Empty : original.CombatTrack);
            Assert.IsFalse(CharacterTestTacticalCardInputs.TryValidateAction(changed, out string issue), change);
            StringAssert.StartsWith("Action:", issue);
        }

        [TestCase("noHit")]
        [TestCase("openBounded")]
        [TestCase("missingOptimal")]
        [TestCase("offsetSector")]
        public void UnsupportedSummaryShapesCannotSupplyTheFirstCardContract(string shape)
        {
            // Pure validation fixtures, never persisted or passed to TryPrepare/Compiler as measurement evidence.
            var measurement = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 50, 0, 5000,
                new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
            var status = CharacterActionProberStatus.Valid;
            var intervals = new[] { new CharacterActionSpatialInterval(100, 900) };
            int? optimal = 300;
            bool upperOpen = false;
            var sector = CharacterActionTacticalSector.Fallback;
            switch (shape)
            {
                case "noHit":
                    status = CharacterActionProberStatus.NoHit;
                    intervals = Array.Empty<CharacterActionSpatialInterval>();
                    optimal = null;
                    break;
                case "openBounded":
                    status = CharacterActionProberStatus.OpenBounded;
                    intervals = new[] { new CharacterActionSpatialInterval(100, 5000) };
                    upperOpen = true;
                    break;
                case "missingOptimal":
                    optimal = null;
                    break;
                case "offsetSector":
                    sector = new CharacterActionTacticalSector(15000, 30000, CharacterActionSectorSource.Authored);
                    break;
            }
            var summary = new CharacterActionSpatialSummary(new CharacterActionId(301), "validation-fixture.action",
                "validation-fixture.dummy", measurement, status, intervals, optimal, false, upperOpen, sector);
            Assert.IsFalse(CharacterTestTacticalCardInputs.TryValidateSummary(summary, out string issue), shape);
            StringAssert.StartsWith("ActionSummary:", issue);
        }

        [Test]
        public void MultiBandSelectsOnlyFirstRunWithoutChangingFullEvidence()
        {
            var summary = RangeFixture(new[] { new CharacterActionSpatialInterval(0, 750),
                new CharacterActionSpatialInterval(1100, 2250) }, 1125);
            string original = CharacterActionTacticalSummaryJson.Write(summary);
            Assert.IsTrue(CharacterTestTacticalCardInputs.TrySelectRange(summary, out var range, out string issue), issue);
            Assert.AreSame(summary, range.Evidence);
            Assert.AreEqual(0d, range.MinDistance);
            Assert.AreEqual(0.75d, range.MaxDistance);
            Assert.AreEqual(350, range.PreferredMillimeters, "Choose a measured grid hit inside the first run, not the distant global optimum.");
            Assert.IsTrue(range.IgnoresFurtherIntervals);
            Assert.AreEqual(CharacterActionProberStatus.Discontinuous, summary.ProberStatus);
            Assert.AreEqual(original, CharacterActionTacticalSummaryJson.Write(summary));
        }

        [Test]
        public void SingleRunRetainsIndependentlyVerifiedPreferredPoint()
        {
            var summary = RangeFixture(new[] { new CharacterActionSpatialInterval(100, 750) }, 425);
            Assert.IsTrue(CharacterTestTacticalCardInputs.TrySelectRange(summary, out var range, out string issue), issue);
            Assert.AreEqual(425, range.PreferredMillimeters);
            Assert.IsFalse(range.IgnoresFurtherIntervals);
        }

        [Test]
        public void VerifiedPointInGapIsRejectedRatherThanClampedIntoFirstRun()
        {
            var summary = RangeFixture(new[] { new CharacterActionSpatialInterval(0, 750),
                new CharacterActionSpatialInterval(1100, 2250) }, 900);
            Assert.IsFalse(CharacterTestTacticalCardInputs.TrySelectRange(summary, out _, out string issue));
            Assert.AreEqual("ActionSummary:OptimalOutsideRange", issue);
        }

        [Test]
        public void OldMergedMeasurementVersionIsRejected()
        {
            var summary = RangeFixture(new[] { new CharacterActionSpatialInterval(0, 750) }, 350,
                "character-test-reference-prober-v4-hurtbox-surface");
            Assert.IsFalse(CharacterTestTacticalCardInputs.TrySelectRange(summary, out _, out string issue));
            Assert.AreEqual("ActionSummary:HurtboxSurfaceMeasurementRequired", issue);
        }

        [Test]
        public void MissingRangeEvidenceIsRejected()
        {
            Assert.IsFalse(CharacterTestTacticalCardInputs.TrySelectRange(null, out var range, out string issue));
            Assert.IsNull(range);
            Assert.AreEqual("ActionSummary:ClosedVerifiedFirstRangeRequired", issue);
        }

        [Test]
        public void SingleSampleFirstRunIsKeptWithoutTakingDistantRun()
        {
            var summary = RangeFixture(new[] { new CharacterActionSpatialInterval(100, 100),
                new CharacterActionSpatialInterval(600, 900) }, 750);
            Assert.IsTrue(CharacterTestTacticalCardInputs.TrySelectRange(summary, out var range, out string issue), issue);
            Assert.AreEqual(0.1d, range.MinDistance);
            Assert.AreEqual(0.1d, range.MaxDistance);
            Assert.AreEqual(100, range.PreferredMillimeters);
        }

        private static CharacterActionSpatialSummary RangeFixture(CharacterActionSpatialInterval[] intervals, int optimal, string version = null)
            => new CharacterActionSpatialSummary(new CharacterActionId(455), "selector-fixture.action", "selector-fixture.dummy",
                new CharacterActionSpatialMeasurement(version ?? CharacterActionRangeProber.Version, 50, 0, 5000,
                    new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0)),
                intervals.Length > 1 ? CharacterActionProberStatus.Discontinuous : CharacterActionProberStatus.Valid,
                intervals, optimal, false, false, CharacterActionTacticalSector.Fallback);

        [Test]
        public void RealStorePreparationRequiresCurrentMeasurementAndIgnoresUnrelatedSequenceTail()
        {
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/MxFramework/TacticalCardInputTests"));
            string directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
            var measurement = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 50, 0, 5000,
                new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
            using (var session = CharacterActionReferenceProbeSession.ForCurrentOpener(CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, measurement.ReferenceDummy))
            {
                try
                {
                    var store = new CharacterActionTacticalMetricsStore(directory);
                    Assert.IsFalse(Prepare(session, store, measurement, out var inputs, out string issue));
                    Assert.IsNull(inputs);
                    StringAssert.Contains("Missing", issue);
                    var refreshed = store.Refresh(session, measurement);
                    Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, refreshed.Status, refreshed.Issue);
                    Assert.IsTrue(Prepare(session, store, measurement, out inputs, out issue), issue);
                    Assert.AreEqual(refreshed.SpatialSummary.Intervals[0].FirstHitMillimeters / 1000d, inputs.SelectedRange.MinDistance);
                    Assert.AreEqual(refreshed.SpatialSummary.Intervals[0].LastHitMillimeters / 1000d, inputs.SelectedRange.MaxDistance);
                    Assert.AreEqual(CharacterActionTacticalSummaryJson.Write(refreshed.SpatialSummary), CharacterActionTacticalSummaryJson.Write(inputs.SpatialSummary));
                    Assert.AreEqual(session.Action.ActionId, inputs.Action.ActionId);
                    Assert.AreEqual(Array.Find(session.Action.Phases, p => p.Kind == CharacterActionPhaseKind.Startup).EndFrame, inputs.StartupEndFrame);
                    Assert.AreEqual(session.Action.DurationFrames.Value, inputs.DurationFrames);
                    string fingerprint = inputs.DependencyFingerprint;
                    Assert.IsTrue(session.Runtime.ActionRuntime.TryGetEntityActionSet(session.Source.Id, out var set));
                    CharacterActionCommandSequence sequence = Array.Find(set.CommandSequences, s => s.IntentId == CharacterTestCharacterActionConfigSource.LightAttackIntentId);
                    Assert.IsNotNull(sequence);
                    Assert.Greater(sequence.ActionIds.Length, 1);
                    var old = sequence.ActionIds[1];
                    try
                    {
                        sequence.ActionIds[1] = new CharacterActionId(999999);
                        Assert.IsTrue(Prepare(session, store, measurement, out inputs, out issue), issue);
                        Assert.AreEqual(fingerprint, inputs.DependencyFingerprint, "Unselected combo tails are not first-card dependencies.");
                    }
                    finally { sequence.ActionIds[1] = old; }
                    var equipment = session.Source.Equipment;
                    try
                    {
                        session.Source.SetEquipment(new CharacterTestCharacterEquipmentComponent(false, "tactical-test.missing"));
                        Assert.IsFalse(Prepare(session, store, measurement, out inputs, out issue));
                        Assert.IsNull(inputs);
                        StringAssert.Contains("WeaponBindingUnavailable", issue);
                    }
                    finally { session.Source.SetEquipment(equipment); }
                    var changedMeasurement = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 100, 0, 5000, measurement.ReferenceDummy);
                    Assert.IsFalse(Prepare(session, store, changedMeasurement, out inputs, out issue));
                    StringAssert.Contains("Stale", issue);
                    Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, store.Refresh(session, changedMeasurement).Status);
                    Assert.IsTrue(Prepare(session, store, changedMeasurement, out inputs, out issue), issue);
                    Assert.AreNotEqual(fingerprint, inputs.DependencyFingerprint);
                }
                finally
                {
                    if (Path.GetFullPath(directory).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        && Directory.Exists(directory)) Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void T05_MissingOrStaleMetricsCacheStrictlyRejectsExecution()
        {
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/MxFramework/TacticalCardInputTests"));
            string directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
            var dummy = new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0);
            var measurement = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 50, 0, 2000, dummy);

            using (var session = new CharacterActionReferenceProbeSession(new CharacterActionId(301), dummy))
            {
                try
                {
                    var store = new CharacterActionTacticalMetricsStore(directory);
                    var input = CharacterActionTacticalInput.FromSession(session);

                    // 1. Missing summary returns Missing status and rejects distance query
                    var missingResult = store.Query(input, measurement);
                    Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Missing, missingResult.Status);
                    Assert.IsNull(missingResult.Metrics);
                    Assert.IsFalse(missingResult.TryGetDefaultSurfaceDistance(out _));

                    // 2. Measure and refresh makes it valid
                    var refreshed = store.Refresh(session, measurement);
                    Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, refreshed.Status);
                    var validResult = store.Query(input, measurement);
                    Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Valid, validResult.Status);
                    Assert.IsNotNull(validResult.Metrics);
                    Assert.AreEqual(new CharacterActionId(301), validResult.Metrics.ActionId);
                    Assert.IsTrue(validResult.TryGetDefaultSurfaceDistance(out _));

                    // 3. Stale measurement (prober parameters changed) returns Stale status and rejects
                    var modifiedMeasurement = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 100, 0, 2000, dummy);
                    var staleResult = store.Query(input, modifiedMeasurement);
                    Assert.AreEqual(CharacterActionTacticalMetricsLoadStatus.Stale, staleResult.Status);
                    Assert.IsNull(staleResult.Metrics);
                    Assert.IsFalse(staleResult.TryGetDefaultSurfaceDistance(out _));
                }
                finally
                {
                    if (Path.GetFullPath(directory).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        && Directory.Exists(directory)) Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void T09_SkillActionCategoryAllowedAndValidated()
        {
            var original = CharacterActionContentHashTests.LoadAction();
            var skillAction = new CharacterActionConfig(new CharacterActionId(1201), "Skill Strike", CharacterActionCategory.Skill,
                original.TimelineAuthority, original.Tags, original.Priority, original.DurationFrames,
                original.Requirements, original.Phases, original.CancelRules, original.InterruptRules,
                original.MotionTrack, original.CombatTrack);

            Assert.IsTrue(CharacterTestTacticalCardInputs.TryValidateAction(skillAction, out string issue), issue);
        }

        [Test]
        public void T10_DodgeActionCategoryAllowedWithoutHitTrace()
        {
            var original = CharacterActionContentHashTests.LoadAction();
            var dodgePhases = new[]
            {
                new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 5),
                new CharacterActionPhase(CharacterActionPhaseKind.Active, 6, 25),
                new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 26, 35)
            };
            var dodgeAction = new CharacterActionConfig(new CharacterActionId(950003), "Dodge Roll", CharacterActionCategory.Dodge,
                original.TimelineAuthority, original.Tags, original.Priority, 36,
                original.Requirements, dodgePhases, original.CancelRules, original.InterruptRules,
                original.MotionTrack, CombatTrackConfig.Empty);

            Assert.IsTrue(CharacterTestTacticalCardInputs.TryValidateAction(dodgeAction, out string issue), issue);
        }

        [Test]
        public void T11_ComboDerivativeRejectedAsOpener()
        {
            var action1 = CharacterActionContentHashTests.LoadAction();
            var action2 = new CharacterActionConfig(new CharacterActionId(302), "Combo Hit 2", CharacterActionCategory.BasicAttack,
                action1.TimelineAuthority, action1.Tags, action1.Priority, action1.DurationFrames,
                action1.Requirements, action1.Phases, action1.CancelRules, action1.InterruptRules,
                action1.MotionTrack, action1.CombatTrack);

            var sequence = new CharacterActionCommandSequence(
                CharacterTestCharacterActionConfigSource.LightAttackIntentNumericId,
                new[] { new CharacterActionId(301), new CharacterActionId(302) });

            var set = new CharacterActionSetConfig(1, "test.tactical.set", "Tactical test", "test.character", "test.equipment",
                null, null, null, 0, string.Empty, CharacterActionId.None,
                defaultActionSlotMask: (int)CharacterCancelInputMask.AttackPrimary,
                commandSequences: new[] { sequence });

            // Action 301 is the sequence opener -> Allowed
            Assert.IsTrue(CharacterTestTacticalCardInputs.IsActionEligibleForTacticalOpener(set, 301));
            Assert.IsTrue(CharacterTestTacticalCardInputs.TryResolveSelectedAction(set, new[] { action1, action2 },
                301, out var selected1, out string issue1), issue1);
            Assert.AreEqual(301, selected1.ActionId.Value);

            // Basic-behaviour sequence position must not restrict an explicit tactical action.
            Assert.IsFalse(CharacterTestTacticalCardInputs.IsActionEligibleForTacticalOpener(set, 302));
            Assert.IsTrue(CharacterTestTacticalCardInputs.TryResolveSelectedAction(set, new[] { action1, action2 },
                302, out var selected2, out string issue2), issue2);
            Assert.AreEqual(302, selected2.ActionId.Value);
        }

        [Test]
        public void T11_MixedIdentityOpenerAndDerivativeOrderInvariance()
        {
            // Minimal counterexample from RFC review:
            // Sequence A: 301 -> 302 -> 303 (where 303 is derivative)
            // Sequence B: 303 -> 304 (where 303 is opener)
            var seqA = new CharacterActionCommandSequence(1001,
                new[] { new CharacterActionId(301), new CharacterActionId(302), new CharacterActionId(303) });
            var seqB = new CharacterActionCommandSequence(1002,
                new[] { new CharacterActionId(303), new CharacterActionId(304) });

            // 1. Order [A, B]: seqA first, then seqB
            var setAB = new CharacterActionSetConfig(1, "set.AB", "Set AB", "char", "equip",
                null, null, null, 0, string.Empty, CharacterActionId.None,
                defaultActionSlotMask: 1, commandSequences: new[] { seqA, seqB });

            // 2. Order [B, A]: seqB first, then seqA (reversed)
            var setBA = new CharacterActionSetConfig(2, "set.BA", "Set BA", "char", "equip",
                null, null, null, 0, string.Empty, CharacterActionId.None,
                defaultActionSlotMask: 1, commandSequences: new[] { seqB, seqA });

            // Mixed-identity Action 303 is opener in seqB and derivative in seqA.
            // Eligibility MUST NOT depend on array ordering!
            Assert.IsTrue(CharacterTestTacticalCardInputs.IsActionEligibleForTacticalOpener(setAB, 303),
                "Action 303 is opener of sequence B, must be allowed in [A, B]");
            Assert.IsTrue(CharacterTestTacticalCardInputs.IsActionEligibleForTacticalOpener(setBA, 303),
                "Action 303 is opener of sequence B, must be allowed in [B, A]");

            // Pure derivatives:
            // 302 is only derivative of seqA -> rejected in both
            Assert.IsFalse(CharacterTestTacticalCardInputs.IsActionEligibleForTacticalOpener(setAB, 302));
            Assert.IsFalse(CharacterTestTacticalCardInputs.IsActionEligibleForTacticalOpener(setBA, 302));

            // 304 is only derivative of seqB -> rejected in both
            Assert.IsFalse(CharacterTestTacticalCardInputs.IsActionEligibleForTacticalOpener(setAB, 304));
            Assert.IsFalse(CharacterTestTacticalCardInputs.IsActionEligibleForTacticalOpener(setBA, 304));

            // Standalone binding override:
            // If pure derivative 302 is explicitly bound in CommandBindings, it acquires standalone eligibility!
            var binding302 = new CharacterActionBinding(2001, new CharacterActionId(302));
            var setWithBinding = new CharacterActionSetConfig(3, "set.bound", "Set Bound", "char", "equip",
                new[] { binding302 }, null, null, 0, string.Empty, CharacterActionId.None,
                defaultActionSlotMask: 1, commandSequences: new[] { seqA, seqB });

            Assert.IsTrue(CharacterTestTacticalCardInputs.IsActionEligibleForTacticalOpener(setWithBinding, 302),
                "Action 302 has standalone CommandBinding, so it must be allowed as opener.");
        }

        internal static bool Prepare(CharacterActionReferenceProbeSession session, CharacterActionTacticalMetricsStore store,
            CharacterActionSpatialMeasurement measurement, out CharacterTestTacticalCardInputs inputs, out string issue)
            => CharacterTestTacticalCardInputs.TryPrepare(session.Runtime.ActionRuntime, session.Source, session.Runtime.PhysicsRuntime,
                session.Runtime.CombatTraceSystem.BakedTraceSource, session.Runtime.CombatTraceSystem.WeaponTraceBindingResolver,
                store, measurement, out inputs, out issue, session.Action.ActionId.Value);

        private static CharacterActionSetConfig Set(CharacterActionCommandSequence[] sequences, CharacterActionBinding[] bindings = null)
            => new CharacterActionSetConfig(1, "test.tactical.set", "Tactical test", "test.character", "test.equipment",
                bindings, null, null, 0, string.Empty, new CharacterActionId(301),
                defaultActionSlotMask: (int)CharacterCancelInputMask.AttackPrimary, commandSequences: sequences);
    }
}
