using System;
using System.IO;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestActionUseAuthorityTests
    {
        private const string OwnerId = "task04.fixture.action-use";
        private const int ControlledAbilityId = 93001;
        // Stable author-assigned identities for the two test-only behavior entries.
        private const int FirstCardEntryId = 93011;
        private static CharacterTestCharacterActionRuntime RestrictedRuntime()
        {
            var original = CharacterTestCharacterActionConfigSource.Load(CharacterTestCharacterActionConfigSourceOptions.CreateDefault());
            Assert.IsTrue(original.Registry.TryGetAction(new CharacterActionId(301), out var action));
            var restricted = new CharacterActionConfig(action.ActionId, action.DisplayName, action.Category, action.TimelineAuthority,
                action.Tags, action.Priority, action.DurationFrames, new[] {
                    new CharacterActionRequirement(CharacterActionRequirementKind.Grounded),
                    new CharacterActionRequirement(CharacterActionRequirementKind.ResourceAvailable,
                        value: 20, referenceId: CharacterTestCharacterAttributeIds.Hp),
                    new CharacterActionRequirement(CharacterActionRequirementKind.CooldownReady,
                        referenceId: ControlledAbilityId) }.Concat(action.Requirements.Where(requirement => requirement != null
                        && (requirement.Kind == CharacterActionRequirementKind.WeaponTypeRequired
                            || requirement.Kind == CharacterActionRequirementKind.WeaponTypeIndependent))).ToArray(),
                action.Phases, action.CancelRules, action.InterruptRules, action.MotionTrack, action.CombatTrack,
                action.GameplayTrack, action.AnimationTrack, action.PresentationTrack, action.DebugTrack, action.CohesionSupport);
            var registry = new CharacterActionConfigRegistry(original.Registry.ActionSets,
                original.Registry.Actions.Select(a => a.ActionId.Value == 301 ? restricted : a).ToArray(),
                original.Registry.ActionSetsSourcePath, original.Registry.ActionsSourcePath,
                original.Registry.ReactionProfiles, original.Registry.ReactionProfilesSourcePath,
                original.Registry.LandingProfiles, original.Registry.LandingProfilesSourcePath);
            return new CharacterTestCharacterActionRuntime(original.ImportResult, registry, original.ValidationReport, original.ActionSet,
                original.ConfigSourceKind, "Task04 controlled requirements; original action tracks", original.ActionSetsSourcePath,
                original.ActionsSourcePath, original.ConfigSourceDiagnostics.ToArray());
        }

        private static CharacterTestRuntimeAiLabCompiledLogicProgram Program(CharacterTestTacticalCardRuntimeFixture fixture, string name, bool holdGate = false)
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateBlank(name);
            source.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "delayed-attack",
                Start = CharacterTestTacticalAuthoringTemplates.Absolute(5), Operation = new CharacterTestTacticalAuthoringOperation { Kind = "CharacterAction", SlotId = "primary" } });
            if (holdGate) source.Timelines[0].Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = "explicit-wait",
                Start = CharacterTestTacticalAuthoringTemplates.Absolute(5), TimingKind = "Instant",
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "WaitForCondition" },
                ConditionRoot = CharacterTestTacticalDslTrialTemplates.LowHealth(0.01f) });
            var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(workspace.TryImportTacticalSource(1, 0, workspace.CurrentRevision, source, out string id, out string issue), issue);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(workspace.Cards[id].CloneTacticalSource(), fixture.Inputs, out var compiled, out var errors), string.Join(";", errors));
            var program = compiled.Program.WithTacticalSource(CharacterTestTacticalFrozenSource.CaptureLegacy(workspace, workspace.Cards[id], fixture.Inputs, compiled));
            return program.WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromTacticalCard(workspace.Cards[id], program));
        }

        private static CharacterTestCharacterActionRuntime ScopedAbilityRuntime(bool ambiguous = false)
        {
            CharacterTestCharacterActionRuntime original = RestrictedRuntime();
            var bindings = new[] { new CharacterAbilityActionBinding(ControlledAbilityId, new CharacterActionId(301)) };
            if (ambiguous)
                bindings = bindings.Concat(new[] { new CharacterAbilityActionBinding(ControlledAbilityId, new CharacterActionId(302)) }).ToArray();
            CharacterActionSetConfig[] sets = original.Registry.ActionSets.Select(set => new CharacterActionSetConfig(
                set.Id, set.StableId, set.DisplayName, set.CharacterStableId, set.EquipmentStateStableId,
                set.CommandBindings, bindings, set.ReactionBindings, set.MovementProfileId, set.ReactionProfileId,
                set.DefaultActionId, set.DefaultActionSlotMask, set.LandingProfileId, set.CommandSequences)).ToArray();
            var registry = new CharacterActionConfigRegistry(sets, original.Registry.Actions,
                original.Registry.ActionSetsSourcePath, original.Registry.ActionsSourcePath,
                original.Registry.ReactionProfiles, original.Registry.ReactionProfilesSourcePath,
                original.Registry.LandingProfiles, original.Registry.LandingProfilesSourcePath);
            return new CharacterTestCharacterActionRuntime(original.ImportResult, registry, original.ValidationReport,
                original.ActionSet == null ? null : sets.First(set => set.Id == original.ActionSet.Id), original.ConfigSourceKind,
                "Scoped Ability fixture", original.ActionSetsSourcePath, original.ActionsSourcePath,
                original.ConfigSourceDiagnostics.ToArray());
        }

        [Test]
        public void ScopedAbilityActionKeepsSharedCooldownAndCostOwner()
        {
            var definition = new AbilityDefinitionConfig(ControlledAbilityId, OwnerId, "Scoped usage", null,
                new AbilityCooldownConfig(50, 0), new[] { new AbilityCostConfig(CharacterTestCharacterAttributeIds.Hp, 10) },
                0, new AbilityActionBindingConfig(new CharacterActionId(302), MigrationFixtureIds.Id0030), default, 0, 0);
            var definitions = new AbilityDefinitionRegistry();
            Assert.IsTrue(definitions.TryRegister(definition, out string issue), issue);
            var abilities = new AbilityRuntimeModule(definitions);
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture(actionId: 301,
                actionRuntime: ScopedAbilityRuntime(), abilityRuntime: abilities))
            {
                var authority = fixture.Runtime.ActionUseAuthority;
                var frame = new RuntimeFrame(fixture.NextFrame);
                Assert.AreEqual(301, fixture.Runtime.ActionRuntime.ResolveAbilityActionId(fixture.Source.Id, definition).Value);
                Assert.IsTrue(authority.Evaluate(fixture.Source, 301, frame, fixture.Facts, definition).Available);
                int hp = fixture.Runtime.World.GetCurrentAttribute(fixture.Source, CharacterTestCharacterAttributeIds.Hp);
                var commit = authority.TryCommit(fixture.Source,
                    fixture.Runtime.ActionRuntime.Registry.Actions.Single(action => action.ActionId.Value == 301),
                    frame, fixture.Facts, definition);
                Assert.IsTrue(commit.Committed, commit.Reason);
                Assert.AreEqual(hp - 10, fixture.Runtime.World.GetCurrentAttribute(fixture.Source, CharacterTestCharacterAttributeIds.Hp));
                Assert.AreEqual(50, abilities.GetRemainingCooldownFrames(fixture.Source.Id.Value, definition, frame));
                Assert.AreEqual(CharacterTestActionUseStatus.AbilityUnavailable,
                    authority.Evaluate(fixture.Source, 301, frame, fixture.Facts, definition).Status);
                Assert.AreEqual(CharacterTestActionUseStatus.AbilityOwnerMismatch,
                    authority.Evaluate(fixture.Source, 302, frame, fixture.Facts, definition).Status);
                Assert.AreEqual(302, definition.ActionBinding.ActionId.Value);
            }
        }

        [Test]
        public void AmbiguousScopedAbilityDoesNotCommitCooldownOrCosts()
        {
            var definition = new AbilityDefinitionConfig(ControlledAbilityId, OwnerId, "Ambiguous usage", null,
                new AbilityCooldownConfig(50, 0), new[] { new AbilityCostConfig(CharacterTestCharacterAttributeIds.Hp, 10) },
                0, new AbilityActionBindingConfig(new CharacterActionId(302), MigrationFixtureIds.Id0030), default, 0, 0);
            var definitions = new AbilityDefinitionRegistry();
            Assert.IsTrue(definitions.TryRegister(definition, out string issue), issue);
            var abilities = new AbilityRuntimeModule(definitions);
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture(actionId: 301,
                actionRuntime: ScopedAbilityRuntime(ambiguous: true), abilityRuntime: abilities))
            {
                var frame = new RuntimeFrame(fixture.NextFrame);
                int hp = fixture.Runtime.World.GetCurrentAttribute(fixture.Source, CharacterTestCharacterAttributeIds.Hp);
                Assert.IsFalse(fixture.Runtime.ActionRuntime.ResolveAbilityActionId(fixture.Source.Id, definition).IsValid);
                Assert.AreEqual(CharacterTestActionUseStatus.AbilityOwnerMismatch,
                    fixture.Runtime.ActionUseAuthority.Evaluate(fixture.Source, 301, frame, fixture.Facts, definition).Status);
                Assert.AreEqual(hp, fixture.Runtime.World.GetCurrentAttribute(fixture.Source, CharacterTestCharacterAttributeIds.Hp));
                Assert.AreEqual(0, abilities.GetRemainingCooldownFrames(fixture.Source.Id.Value, definition, frame));
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ActualActionCommitRechecksCurrentRequirementsAndNeverPublishesAnUnpaidAction(bool depleteAfterSelection, bool holdGate)
        {
            var definition = new AbilityDefinitionConfig(ControlledAbilityId, OwnerId, "Controlled opener usage", null,
                new AbilityCooldownConfig(50, 0), new[] { new AbilityCostConfig(CharacterTestCharacterAttributeIds.Hp, 10) },
                0, new AbilityActionBindingConfig(new CharacterActionId(301), global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0030), default, 0, 0);
            var definitions = new AbilityDefinitionRegistry(); Assert.IsTrue(definitions.TryRegister(definition, out string issue), issue);
            var abilities = new AbilityRuntimeModule(definitions);
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture(actionId: 301, actionRuntime: RestrictedRuntime(), abilityRuntime: abilities))
            {
                var authority = fixture.Runtime.ActionUseAuthority;
                // The probe omits level composition, which normally registers this adapter.
                GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(fixture.Runtime.World.GameplayComponentWorld.Schemas);
                fixture.PlaceTarget(4000);
                var now = new RuntimeFrame(fixture.NextFrame);
                string abilityBefore = JsonConvert.SerializeObject(abilities.CreateStateSnapshot());
                int hpBefore = fixture.Runtime.World.GetCurrentAttribute(fixture.Source, CharacterTestCharacterAttributeIds.Hp);
                var qualification = authority.Evaluate(fixture.Source, 301, now, fixture.Facts);
                Assert.IsTrue(qualification.Available, qualification.Status.ToString());
                Assert.AreEqual(abilityBefore, JsonConvert.SerializeObject(abilities.CreateStateSnapshot()));
                Assert.AreEqual(hpBefore, fixture.Runtime.World.GetCurrentAttribute(fixture.Source, CharacterTestCharacterAttributeIds.Hp));
                Assert.AreEqual(0, authority.CommitCount);

                var programs = new[] { Program(fixture, "A", holdGate), Program(fixture, "B", holdGate) };
                var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("shared-action-use");
                for (int i = 0; i < 2; i++) profile.Entries.Add(new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle, true, i)
                    { EntryId = "card-" + i, NumericId = FirstCardEntryId + i });
                var overlay = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay(profile, programs, fixture.Runtime.ActionRuntime, abilities,
                    12345, entities: fixture.Runtime.Entities, navigationService: CharacterTestDirectAiNavigationService.Instance, characterRuntime: fixture.Runtime);
                void Tick()
                {
                    fixture.RefreshFacts(); var frame = new RuntimeFrame(fixture.NextFrame);
                    abilities.Tick(new RuntimeTickContext(frame.Value, 1d / 60d, frame.Value / 60d, RuntimeTickStage.PreSimulation));
                    Assert.IsTrue(overlay.TryTick(fixture.Source, fixture.Facts, frame, out var intent));
                    if (intent.Kind != CharacterTestRuntimeAiLabRuntimeIntentKind.None)
                    {
                        var result = CharacterTestBehaviorExecutionCommandWriter.Submit(frame, fixture.Source.Id.Value, intent, fixture.Commands.Enqueue);
                        overlay.RecordCommandResult(fixture.Source.Id.Value, result); Assert.IsTrue(result.Success);
                    }
                    fixture.AdvanceWorldOnly();
                }
                Tick();
                long queriesAfterSelection = authority.QueryCount;
                Assert.AreEqual(3, queriesAfterSelection, "One standalone read plus the two actual candidate reads.");
                if (depleteAfterSelection) fixture.Runtime.World.SetCurrentAttribute(fixture.Source.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 5);
                for (int i = 0; i < 12 && authority.CommitCount == 0; i++) Tick();
                Assert.AreEqual(1, authority.CommitCount);
                Assert.AreEqual(queriesAfterSelection + 1, authority.QueryCount, "Current requirements are checked once at actual commit.");
                Assert.AreEqual(1, authority.CommitQueryCount);
                long queriesAtCommit = authority.QueryCount;
                bool active = fixture.Runtime.ActionRuntime.TryGetActiveActionPresentationState(fixture.Source.Id, out long instance, out _, out _, out _);
                Assert.AreEqual(!depleteAfterSelection, active);
                if (!depleteAfterSelection) Assert.Greater(instance, 0);
                var commit = authority.LastCommit;
                Assert.IsTrue(commit.Configured); Assert.AreEqual(!depleteAfterSelection, commit.Committed);
                Assert.AreEqual(depleteAfterSelection ? 5 : hpBefore - 10,
                    fixture.Runtime.World.GetCurrentAttribute(fixture.Source, CharacterTestCharacterAttributeIds.Hp));
                now = new RuntimeFrame(fixture.NextFrame);
                long remaining = abilities.GetRemainingCooldownFrames(fixture.Source.Id.Value, definition, now);
                Assert.AreEqual(depleteAfterSelection, remaining == 0);
                if (depleteAfterSelection)
                {
                    StringAssert.Contains("CommitFailed:ResourceUnavailable", commit.Reason);
                    var failed = overlay.CreateStateSnapshot(0).Agents.Single().ExecutionState;
                    Assert.IsTrue(failed.Results.Contains((int)CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.CommitFailed));
                    Assert.IsTrue(failed.CommitFailureDetails.Any(r => r.Status == CharacterTestActionUseStatus.ResourceUnavailable));
                    var snapshot = overlay.CreateStateSnapshot(0);
                    string snapshotJson = JsonConvert.SerializeObject(snapshot);
                    var restoredSnapshot = JsonConvert.DeserializeObject<CharacterTestRuntimeAiLabLogicSlotStateSnapshot>(snapshotJson);
                    Assert.IsTrue(overlay.TryApplyStateSnapshot(restoredSnapshot, out string restoreIssue), restoreIssue);
                    Assert.AreEqual(snapshotJson, JsonConvert.SerializeObject(overlay.CreateStateSnapshot(0)));
                    for (int i = 0; i < 8; i++) Tick();
                    Assert.AreEqual(1, authority.CommitCount, "The failed event entry is consumed, not retried while the cursor remains in the card.");
                    if (holdGate) Assert.AreEqual(5, overlay.CreateStateSnapshot(0).Agents.Single().ExecutionState.LocalFrame);
                }
                else
                {
                    var other = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge(fixture.Source.Id.Value, "other-card", programs[1],
                        fixture.Runtime.Entities, CharacterTestDirectAiNavigationService.Instance, fixture.Runtime.PerceptionFrameCache, fixture.Runtime,
                        bindingNumericId: FirstCardEntryId + 1);
                    other.SetFrame(fixture.Source, fixture.Facts, now, fixture.Runtime.ActionRuntime, abilities);
                    Assert.IsFalse(other.MeetsSystemActionRequirements(), "Another card referencing the same action sees the same cooldown owner.");
                    var abilitySave = new AbilityRuntimeSaveStateProvider(abilities); var savedAbility = abilitySave.CaptureSaveState();
                    var attributeSave = new GameplayComponentWorldSaveStateProvider(fixture.Runtime.World.GameplayComponentWorld); var savedAttributes = attributeSave.CaptureSaveState();
                    Assert.IsTrue(savedAbility.Success); Assert.IsTrue(savedAttributes.Success);
                    abilities.Tick(new RuntimeTickContext(now.Value + 100, 1d / 60d, 0, RuntimeTickStage.PreSimulation));
                    fixture.Runtime.World.SetCurrentAttribute(fixture.Source.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 1);
                    Assert.IsTrue(abilitySave.RestoreSaveState(savedAbility.Value).Success); Assert.IsTrue(attributeSave.RestoreSaveState(savedAttributes.Value).Success);
                    Assert.AreEqual(remaining, abilities.GetRemainingCooldownFrames(fixture.Source.Id.Value, definition, now));
                    Assert.AreEqual(hpBefore - 10, fixture.Runtime.World.GetCurrentAttribute(fixture.Source, CharacterTestCharacterAttributeIds.Hp));
                }
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/AtomicActionUse")); Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, holdGate ? "failed-entry-held-gate.json" : depleteAfterSelection ? "late-resource-rejected.json" : "shared-action-owner.json"), JsonConvert.SerializeObject(new {
                    actionId = 301, abilityId = definition.AbilityId, actionInstance = instance, queriesAfterSelection, queriesAtCommit,
                    commit.Configured, commit.Committed, commit.Reason, remainingCooldown = remaining,
                    hp = fixture.Runtime.World.GetCurrentAttribute(fixture.Source, CharacterTestCharacterAttributeIds.Hp), owner = "AbilityRuntimeModule + formal gameplay attributes" }, Formatting.Indented));
            }
        }
    }
}
