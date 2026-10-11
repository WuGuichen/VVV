using System;
using MxFramework.CharacterAction;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionValidationRegistryTests
    {
        [Test]
        public void Registry_ProvidesDeterministicLookupAndDuplicateDiagnostics()
        {
            var actionA1 = CreateAction(id: 1, stableId: "attack.a");
            var actionA2 = CreateAction(id: 2, stableId: "attack.a");
            var actionB1 = CreateAction(id: 1, stableId: "attack.b");
            var setA1 = CreateActionSet(id: 10, stableId: "set.a");
            var setA2 = CreateActionSet(id: 11, stableId: "set.a");
            var setB1 = CreateActionSet(id: 10, stableId: "set.b");

            var registry = new CharacterActionConfigRegistry(
                new[] { setB1, setA2, setA1 },
                new[] { actionB1, actionA2, actionA1 });

            Assert.IsTrue(registry.TryGetAction(CharacterTestActionIds.Action_attack_a, out CharacterActionConfig action));
            Assert.AreEqual(1, action.ActionId.Value);
            Assert.IsTrue(registry.TryGetActionSet("set.a", out CharacterActionSetConfig actionSet));
            Assert.AreEqual(10, actionSet.Id);
            AssertHasDiagnostic(registry.Diagnostics, CharacterActionDiagnosticCodes.ConfigDuplicateId);
            AssertHasDiagnostic(registry.Diagnostics, CharacterActionDiagnosticCodes.ConfigDuplicateStableId);
        }

        [Test]
        public void Report_FindsMissingCommandAbilityAndReactionActionReferences()
        {
            var set = CreateActionSet(
                commandBindings: new[] { new CharacterActionBinding(CharacterTestNumericIds.IntentJump, CharacterTestActionIds.Action_missing_command) },
                abilityBindings: new[] { new CharacterAbilityActionBinding(20, CharacterTestActionIds.Action_missing_ability) },
                reactionBindings: new[] { new CharacterReactionBinding("reaction.default", CharacterTestActionIds.Action_missing_reaction) });
            var registry = new CharacterActionConfigRegistry(new[] { set }, new[] { CreateAction() });

            CharacterActionValidationReport report = CharacterActionValidationReport.Validate(registry);

            Assert.IsFalse(report.Success);
            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.MissingActionConfig, "commandBindings[0].actionId");
            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.MissingActionConfig, "abilityBindings[0].actionId");
            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.MissingActionConfig, "reactionBindings[0].defaultActionId");
        }

        [Test]
        public void Report_FindsMissingReactionProfileAndReactionProfileActionReferences()
        {
            var missingProfileSet = CreateActionSet(reactionProfileId: "reaction.missing");
            var brokenProfile = new CharacterReactionProfile(
                "reaction.broken",
                new[]
                {
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_missing_reaction_action,
                        CharacterReactionRuleTrigger.CohesionBreak),
                },
                defaultActionId: CharacterTestActionIds.Action_missing_default_action);
            var brokenProfileSet = CreateActionSet(
                stableId: "set.profile",
                reactionProfileId: "reaction.broken");
            var registry = new CharacterActionConfigRegistry(
                new[] { missingProfileSet, brokenProfileSet },
                new[] { CreateAction() },
                reactionProfiles: new[] { brokenProfile });

            CharacterActionValidationReport report = CharacterActionValidationReport.Validate(registry);

            Assert.IsFalse(report.Success);
            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.MissingReactionProfile, ".reactionProfileId");
            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.MissingActionConfig, "reactionProfiles[reaction.broken]");
        }

        [Test]
        public void Report_FindsPhaseRangeAndTrackEventOutsideDuration()
        {
            var action = CreateAction(
                durationFrames: 5,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 5),
                },
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(5, CharacterActionTrackEventKind.LockMovement, stableEventId: CharacterTestNumericIds.Field_late_motion),
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(
                        0,
                        CharacterActionTrackEventKind.SpawnVisualCue,
                        resourceKey: "vfx.charge",
                        stableEventId: CharacterTestNumericIds.Field_late_presentation,
                        endFrame: 5),
                }));
            var registry = new CharacterActionConfigRegistry(Array.Empty<CharacterActionSetConfig>(), new[] { action });

            CharacterActionValidationReport report = CharacterActionValidationReport.Validate(registry);

            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.PhaseRangeOutsideDuration);
            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.TrackEventOutsideDuration, "late.motion");
            AssertNoDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.TrackEventOutsideDuration, "late.presentation");
        }

        [Test]
        public void Report_StillRejectsNonVfxPresentationTailOutsideActionDuration()
        {
            CharacterActionConfig action = CreateAction(
                durationFrames: 5,
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(
                        0,
                        CharacterActionTrackEventKind.PlayAudioCue,
                        cueId: CharacterTestNumericIds.Field_sfx_late,
                        stableEventId: CharacterTestNumericIds.Field_late_audio,
                        endFrame: 5),
                }));
            var registry = new CharacterActionConfigRegistry(
                Array.Empty<CharacterActionSetConfig>(),
                new[] { action });

            CharacterActionValidationReport report = CharacterActionValidationReport.Validate(registry);

            AssertHasDiagnostic(
                report.Diagnostics,
                CharacterActionDiagnosticCodes.TrackEventOutsideDuration,
                "late.audio");
        }

        [Test]
        public void Report_AppliesMissingAnimationActionKeyPolicy()
        {
            var action = CreateAction(
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.PlayAnimation, stableEventId: CharacterTestNumericIds.Field_anim_missing),
                }));
            var registry = new CharacterActionConfigRegistry(Array.Empty<CharacterActionSetConfig>(), new[] { action });
            var options = new CharacterActionValidationOptions(CharacterActionMissingAnimationActionKeyPolicy.Warning);

            CharacterActionValidationReport report = CharacterActionValidationReport.Validate(registry, options);

            CharacterActionValidationDiagnostic diagnostic = FindDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.AnimationActionMissing);
            Assert.AreEqual(CharacterActionDiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.AreEqual("actions[light_attack].animationTrack.events[anim.missing]", diagnostic.StablePath);
        }

        [Test]
        public void Report_FindsMissingCombatAnchoredCombatActionId()
        {
            var action = CreateAction(
                authority: CharacterActionTimelineAuthority.CombatAnchored,
                combatTrack: CombatTrackConfig.Empty);
            var registry = new CharacterActionConfigRegistry(Array.Empty<CharacterActionSetConfig>(), new[] { action });

            CharacterActionValidationReport report = CharacterActionValidationReport.Validate(registry);

            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.CombatActionMissing, ".combatTrack.combatActionId");
        }

        [Test]
        public void Report_UsesFakeCrossModuleValidatorsForExternalReferences()
        {
            var action = CreateAction(
                requirements: new[]
                {
                    new CharacterActionRequirement(CharacterActionRequirementKind.EquipmentTagRequired, "tag.missing"),
                },
                combatTrack: new CombatTrackConfig(CharacterTestNumericIds.Field_combat_missing, null),
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.CrossFadeAnimation, "anim.missing", stableEventId: CharacterTestNumericIds.Field_anim_missing),
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(0, CharacterActionTrackEventKind.PlayAudioCue, cueId: CharacterTestNumericIds.Field_sfx_missing, stableEventId: CharacterTestNumericIds.Field_sfx_missing),
                    new PresentationTrackEvent(1, CharacterActionTrackEventKind.SpawnVisualCue, resourceKey: "vfx.missing", stableEventId: CharacterTestNumericIds.Field_vfx_missing),
                }));
            var set = CreateActionSet(characterStableId: "character.missing", equipmentStateStableId: "equipment.missing");
            var options = new CharacterActionValidationOptions(
                crossModuleValidators: new ICharacterActionCrossModuleValidator[]
                {
                    new CharacterActionAnimationActionKeyValidator(new MissingExternalProvider()),
                    new CharacterActionCombatActionIdValidator(new MissingExternalProvider()),
                    new CharacterActionPresentationResourceValidator(new MissingExternalProvider()),
                    new CharacterActionEquipmentTagRequirementValidator(new MissingExternalProvider()),
                    new CharacterActionCharacterEquipmentStateValidator(new MissingExternalProvider()),
                });
            var registry = new CharacterActionConfigRegistry(new[] { set }, new[] { action });

            CharacterActionValidationReport report = CharacterActionValidationReport.Validate(registry, options);

            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.AnimationActionMissing, "anim.missing");
            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.CombatActionMissing, "combatTrack.combatActionId");
            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.AudioCueMissing, "sfx.missing");
            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.PresentationResourceMissing, "vfx.missing");
            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.EquipmentTagMissing, "requirements[0].stableId");
            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.CharacterEquipmentStateMissing, "characterStableId");
            AssertHasDiagnostic(report.Diagnostics, CharacterActionDiagnosticCodes.CharacterEquipmentStateMissing, "equipmentStateStableId");
        }

        [Test]
        public void Report_SortsDiagnosticsDeterministically()
        {
            var registry = new CharacterActionConfigRegistry(
                new[] { CreateActionSet(commandBindings: new[] { new CharacterActionBinding(CharacterTestNumericIds.IntentJump, CharacterTestActionIds.Action_missing) }) },
                new[] { CreateAction(animationTrack: new AnimationTrackConfig(new[] { new AnimationTrackEvent(0, CharacterActionTrackEventKind.PlayAnimation, stableEventId: CharacterTestNumericIds.Field_missing_anim) })) });

            CharacterActionValidationReport report = CharacterActionValidationReport.Validate(registry);

            for (int i = 1; i < report.Diagnostics.Length; i++)
            {
                Assert.LessOrEqual(
                    CharacterActionValidationDiagnostic.Compare(report.Diagnostics[i - 1], report.Diagnostics[i]),
                    0);
            }
        }

        private static CharacterActionSetConfig CreateActionSet(
            int id = 1,
            string stableId = "set.default",
            string characterStableId = "",
            string equipmentStateStableId = "",
            CharacterActionBinding[] commandBindings = null,
            CharacterAbilityActionBinding[] abilityBindings = null,
            CharacterReactionBinding[] reactionBindings = null,
            string reactionProfileId = "")
        {
            return new CharacterActionSetConfig(
                id,
                stableId,
                stableId,
                characterStableId,
                equipmentStateStableId,
                commandBindings ?? new[] { new CharacterActionBinding(CharacterTestNumericIds.IntentLightAttack, new MxFramework.CharacterAction.CharacterActionId(301)) },
                abilityBindings,
                reactionBindings,
                movementProfileId: 0,
                reactionProfileId: reactionProfileId,
                defaultActionId: MxFramework.CharacterAction.CharacterActionId.None);
        }

        private static CharacterActionConfig CreateAction(
            int id = 100,
            string stableId = "light_attack",
            CharacterActionTimelineAuthority authority = CharacterActionTimelineAuthority.CharacterAuthored,
            int? durationFrames = 12,
            CharacterActionRequirement[] requirements = null,
            CharacterActionPhase[] phases = null,
            MotionTrackConfig motionTrack = null,
            CombatTrackConfig combatTrack = null,
            AnimationTrackConfig animationTrack = null,
            PresentationTrackConfig presentationTrack = null)
        {
            return new CharacterActionConfig(
                new CharacterActionId(id),
                stableId,
                CharacterActionCategory.BasicAttack,
                authority,
                tags: null,
                priority: 10,
                durationFrames: durationFrames,
                requirements: requirements,
                phases: phases ?? new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 3),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 4, 7),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 8, 11),
                },
                cancelRules: null,
                interruptRules: null,
                motionTrack: motionTrack,
                combatTrack: combatTrack,
                gameplayTrack: null,
                animationTrack: animationTrack,
                presentationTrack: presentationTrack,
                debugTrack: null);
        }

        private static void AssertHasDiagnostic(CharacterActionValidationDiagnostic[] diagnostics, string code)
        {
            FindDiagnostic(diagnostics, code);
        }

        private static void AssertHasDiagnostic(CharacterActionValidationDiagnostic[] diagnostics, string code, string stablePathContains)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Code == code
                    && diagnostics[i].StablePath.IndexOf(stablePathContains, StringComparison.Ordinal) >= 0)
                {
                    return;
                }
            }

            Assert.Fail("Expected diagnostic " + code + " with path containing " + stablePathContains + ".");
        }

        private static void AssertNoDiagnostic(
            CharacterActionValidationDiagnostic[] diagnostics,
            string code,
            string stablePathContains)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Code == code
                    && diagnostics[i].StablePath.IndexOf(stablePathContains, StringComparison.Ordinal) >= 0)
                {
                    Assert.Fail("Unexpected diagnostic " + code + " with path containing " + stablePathContains + ".");
                }
            }
        }

        private static CharacterActionValidationDiagnostic FindDiagnostic(
            CharacterActionValidationDiagnostic[] diagnostics,
            string code)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Code == code)
                    return diagnostics[i];
            }

            Assert.Fail("Expected diagnostic " + code + ".");
            return default;
        }

        private sealed class MissingExternalProvider :
            ICharacterActionAnimationActionKeyProvider,
            ICharacterActionCombatActionIdProvider,
            ICharacterActionPresentationResourceProvider,
            ICharacterActionEquipmentTagProvider,
            ICharacterActionCharacterEquipmentStateProvider
        {
            public bool ContainsAnimationActionKey(
                string actionSetStableId,
                CharacterActionId actionId,
                string animationActionKey)
            {
                return false;
            }

            public bool ContainsCombatActionId(
                string actionSetStableId,
                CharacterActionId actionId,
                int combatActionId)
            {
                return false;
            }

            public bool ContainsPresentationResource(
                string actionSetStableId,
                CharacterActionId actionId,
                CharacterActionResourceDependencyKind dependencyKind,
                string stableId)
            {
                return false;
            }

            public bool ContainsEquipmentTag(string actionSetStableId, string equipmentTag)
            {
                return false;
            }

            public bool ContainsCharacterStableId(string characterStableId)
            {
                return false;
            }

            public bool ContainsEquipmentStateStableId(string characterStableId, string equipmentStateStableId)
            {
                return false;
            }
        }
    }
}
