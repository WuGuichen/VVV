using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionResolverTests
    {
        [Test]
        public void CommandBoundBasicAttack_ReturnsSuccessAndPlan()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            CharacterActionResolverContext context = CreateContext(actions: new[] { action });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: "LightAttack"));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(CharacterActionRejectReason.None, result.RejectReason);
            Assert.IsNotNull(result.Plan);
            Assert.AreEqual("light_attack", result.Plan.ActionId);
            Assert.AreEqual(CharacterActionCategory.BasicAttack, result.Plan.Category);
            Assert.AreEqual(24, result.Plan.DurationFrames);
            Assert.AreEqual(6, result.Plan.Tracks.Length);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionDurationResolvedFromConfig, result.Diagnostics[0].Code);
        }

        [Test]
        public void AbilityRequest_ResolvesThroughAbilityBinding()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateAction(200, "dash_strike", CharacterActionCategory.Skill);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new CharacterActionBinding[0],
                abilityBindings: new[] { new CharacterAbilityActionBinding(9001, "dash_strike") });
            CharacterActionResolverContext context = CreateContext(set, new[] { action });

            CharacterActionResolveResult result = resolver.ResolveAbility(
                context,
                CreateRequest(
                    intentId: string.Empty,
                    abilityId: 9001,
                    sourceKind: CharacterActionSourceKind.GameplayAbility));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("dash_strike", result.Plan.ActionId);
            Assert.AreEqual(CharacterActionCategory.Skill, result.Plan.Category);
        }

        [Test]
        public void CommandBindings_ResolveDeterministicCandidateOrder()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig light = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack, priority: 10);
            CharacterActionConfig heavy = CreateAction(200, "heavy_attack", CharacterActionCategory.BasicAttack, priority: 20);
            CharacterActionConfig jump = CreateAction(300, "basic_jump", CharacterActionCategory.Jump, priority: 15);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new[]
                {
                    new CharacterActionBinding("Attack", "light_attack", priority: 5),
                    new CharacterActionBinding("Attack", "basic_jump", priority: 8),
                    new CharacterActionBinding("Attack", "heavy_attack", priority: 8),
                });
            CharacterActionResolverContext context = CreateContext(set, new[] { light, heavy, jump });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: "Attack"));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("heavy_attack", result.Plan.ActionId);
            Assert.AreEqual(20, result.Plan.Priority);
        }

        [Test]
        public void CommandBindings_HighestPriorityMissingActionRejectsInsteadOfFallingBack()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig light = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new[]
                {
                    new CharacterActionBinding("Attack", "missing_heavy_attack", priority: 20),
                    new CharacterActionBinding("Attack", "light_attack", priority: 5),
                });
            CharacterActionResolverContext context = CreateContext(set, new[] { light });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: "Attack"));

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.MissingActionConfig, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionConfig, result.Diagnostics[0].Code);
        }

        [Test]
        public void CommandBindings_UseStableIdTieBreakerAfterSourceOrder()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig light = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack, priority: 10);
            CharacterActionConfig heavy = CreateAction(200, "heavy_attack", CharacterActionCategory.BasicAttack, priority: 10);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new[]
                {
                    new CharacterActionBinding("Attack", "light_attack", priority: 5),
                    new CharacterActionBinding("Attack", "heavy_attack", priority: 5),
                });
            CharacterActionResolverContext context = CreateContext(set, new[] { light, heavy });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: "Attack"));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("light_attack", result.Plan.ActionId);
        }

        [Test]
        public void BasicJumpCommandBinding_ReturnsJumpPlan()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig jump = CreateAction(300, "basic_jump", CharacterActionCategory.Jump, priority: 15);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new[] { new CharacterActionBinding("Jump", "basic_jump", priority: 3) });
            CharacterActionResolverContext context = CreateContext(set, new[] { jump });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: "Jump"));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("basic_jump", result.Plan.ActionId);
            Assert.AreEqual(CharacterActionCategory.Jump, result.Plan.Category);
        }

        [Test]
        public void AbilityBindingRequiredTags_AcceptsMatchingActionOrContextTags()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateAction(
                200,
                "dash_strike",
                CharacterActionCategory.Skill,
                tags: new[] { "weapon.sword" });
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new CharacterActionBinding[0],
                abilityBindings: new[]
                {
                    new CharacterAbilityActionBinding(
                        9001,
                        "dash_strike",
                        requiredTags: new[] { "weapon.sword", "stance.ready" }),
                });
            CharacterActionResolverContext context = CreateContext(
                set,
                new[] { action },
                contextTags: new[] { "stance.ready" });

            CharacterActionResolveResult result = resolver.ResolveAbility(
                context,
                CreateRequest(
                    intentId: string.Empty,
                    abilityId: 9001,
                    sourceKind: CharacterActionSourceKind.GameplayAbility));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("dash_strike", result.Plan.ActionId);
        }

        [Test]
        public void AbilityBindingRequiredTags_RejectsMissingTagWithStableDiagnostic()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateAction(200, "dash_strike", CharacterActionCategory.Skill);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new CharacterActionBinding[0],
                abilityBindings: new[]
                {
                    new CharacterAbilityActionBinding(
                        9001,
                        "dash_strike",
                        requiredTags: new[] { "weapon.sword" }),
                });
            CharacterActionResolverContext context = CreateContext(set, new[] { action });

            CharacterActionResolveResult result = resolver.ResolveAbility(
                context,
                CreateRequest(
                    intentId: string.Empty,
                    abilityId: 9001,
                    sourceKind: CharacterActionSourceKind.GameplayAbility));

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.EquipmentStateMismatch, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.AbilityRequiredTagMissing, result.Diagnostics[0].Code);
        }

        [Test]
        public void AbilityBindingForbiddenTags_RejectsMatchedTagWithStableDiagnostic()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateAction(
                200,
                "dash_strike",
                CharacterActionCategory.Skill,
                tags: new[] { "status.silenced" });
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new CharacterActionBinding[0],
                abilityBindings: new[]
                {
                    new CharacterAbilityActionBinding(
                        9001,
                        "dash_strike",
                        forbiddenTags: new[] { "status.silenced" }),
                });
            CharacterActionResolverContext context = CreateContext(set, new[] { action });

            CharacterActionResolveResult result = resolver.ResolveAbility(
                context,
                CreateRequest(
                    intentId: string.Empty,
                    abilityId: 9001,
                    sourceKind: CharacterActionSourceKind.GameplayAbility));

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.EquipmentStateMismatch, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.AbilityForbiddenTagMatched, result.Diagnostics[0].Code);
        }

        [Test]
        public void PressureOnlyReactionContext_ResolvesToReactionAction()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig reaction = CreateAction(300, "posture_break_react", CharacterActionCategory.Reaction);
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(
                        "posture_break_react",
                        CharacterReactionRuleTrigger.PostureBreak,
                        currentPressureBand: PressureBand.Broken),
                });
            CharacterActionResolverContext context = CreateContext(
                actions: new[] { reaction },
                reactionProfiles: new[] { profile });
            CharacterReactionContext reactionContext = CharacterReactionContextBuilder.FromPostureBreak(
                new PostureBreakEvent(
                    new RuntimeFrame(3),
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 80,
                    currentPressure: 100,
                    maxPressure: 100,
                    delta: 20)).Context;

            CharacterActionResolveResult result = resolver.ResolveReaction(context, reactionContext);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("posture_break_react", result.Plan.ActionId);
            Assert.AreEqual(CharacterActionCategory.Reaction, result.Plan.Category);
        }

        [Test]
        public void PressureOnlyReactionProfile_TargetingNonReactionAction_IsRejectedByResolver()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig basicAttack = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(
                        "light_attack",
                        CharacterReactionRuleTrigger.PostureBreak,
                        currentPressureBand: PressureBand.Broken),
                });
            CharacterActionResolverContext context = CreateContext(
                actions: new[] { basicAttack },
                reactionProfiles: new[] { profile });
            CharacterReactionContext reactionContext = CharacterReactionContextBuilder.FromPostureBreak(
                new PostureBreakEvent(
                    new RuntimeFrame(3),
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 80,
                    currentPressure: 100,
                    maxPressure: 100,
                    delta: 20)).Context;

            CharacterActionResolveResult result = resolver.ResolveReaction(context, reactionContext);

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.InvalidTarget, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ReactionRuleNoTarget, result.Diagnostics[0].Code);
        }

        [Test]
        public void MissingActionSetBindingAndConfig_RejectWithStableDiagnostics()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionResolveResult missingSet = resolver.ResolveCommand(
                new CharacterActionResolverContext(null, null),
                CreateRequest(intentId: "LightAttack", traceId: "missing-set"));
            CharacterActionResolveResult missingBinding = resolver.ResolveCommand(
                CreateContext(CreateActionSet(commandBindings: new CharacterActionBinding[0]), new[] { CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack) }),
                CreateRequest(intentId: "HeavyAttack", traceId: "missing-binding"));
            CharacterActionResolveResult missingConfig = resolver.ResolveCommand(
                CreateContext(CreateActionSet(), new CharacterActionConfig[0]),
                CreateRequest(intentId: "LightAttack", traceId: "missing-config"));

            Assert.IsTrue(missingSet.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.MissingActionSet, missingSet.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionSet, missingSet.Diagnostics[0].Code);
            Assert.AreEqual(CharacterActionRejectReason.MissingActionBinding, missingBinding.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionBinding, missingBinding.Diagnostics[0].Code);
            Assert.AreEqual(CharacterActionRejectReason.MissingActionConfig, missingConfig.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionConfig, missingConfig.Diagnostics[0].Code);
        }

        [Test]
        public void InvalidCombatAnchoredPhaseAnchor_RejectsWithStableDiagnostic()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateCombatAnchoredAction(
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 3),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 4, 6, CombatActionPhase.Active),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 7, 10, CombatActionPhase.Recovery),
                });
            CharacterActionResolverContext context = CreateContext(
                actions: new[] { action },
                combatTimelines: new[] { CreateCombatTimeline(1001, cancelTargetActionId: 2002) });

            CharacterActionResolveResult result = resolver.ResolveCommand(context, CreateRequest(intentId: "LightAttack"));

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.PhaseAnchorInvalid, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.PhaseCombatAnchorMissing, result.Diagnostics[0].Code);
        }

        [Test]
        public void MissingAnimationDependency_RejectsWithAnimationReason()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateActionWithTrackDependencyIssue(
                "missing_animation",
                new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.PlayAnimation),
                }),
                default);
            CharacterActionResolverContext context = CreateContext(actions: new[] { action });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: "LightAttack"));

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.AnimationActionMissing, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.AnimationActionMissing, result.Diagnostics[0].Code);
        }

        [Test]
        public void MissingPresentationDependency_RejectsWithResourceReason()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateActionWithTrackDependencyIssue(
                "missing_visual",
                default,
                new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(6, CharacterActionTrackEventKind.SpawnVisualCue),
                }));
            CharacterActionResolverContext context = CreateContext(actions: new[] { action });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: "LightAttack"));

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.ResourceMissing, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.PresentationResourceMissing, result.Diagnostics[0].Code);
        }

        [Test]
        public void CombatAnchoredAction_CanResolveTimelineThroughStableCombatActionIdBinding()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateCombatAnchoredAction(
                combatActionId: "combat.light_attack",
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 3, CombatActionPhase.Startup),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 4, 6, CombatActionPhase.Active),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 7, 10, CombatActionPhase.Recovery),
                });
            CombatActionTimeline timeline = CreateCombatTimeline(1001, cancelTargetActionId: 2002);
            CharacterActionResolverContext context = new CharacterActionResolverContext(
                CreateActionSet(),
                new[] { action },
                combatTimelineBindings: new[]
                {
                    new CharacterActionCombatTimelineBinding("combat.light_attack", timeline),
                });

            CharacterActionResolveResult result = resolver.ResolveCommand(context, CreateRequest(intentId: "LightAttack"));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(11, result.Plan.DurationFrames);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionDurationResolvedFromCombat, result.Diagnostics[0].Code);
        }

        [Test]
        public void CharacterAndCombatCancelRejections_RemainDistinct()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateAction(2002, "dodge", CharacterActionCategory.Dodge);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new[] { new CharacterActionBinding("Dodge", "dodge") });
            CharacterActionResolverContext characterRejectContext = CreateContext(
                set,
                new[] { action },
                state: new CharacterActionResolverState(
                    hasActiveAction: true,
                    activeActionId: "light_attack",
                    activeActionLocalFrame: 5,
                    activeActionAuthority: CharacterActionTimelineAuthority.CombatAnchored,
                    activeCancelRules: new[] { new CharacterCancelRule(0, 10, targetActionId: 2002, allow: false) },
                    activeCombatTimeline: CreateCombatTimeline(1001, cancelTargetActionId: 2002)));
            CharacterActionResolverContext combatRejectContext = CreateContext(
                set,
                new[] { action },
                state: new CharacterActionResolverState(
                    hasActiveAction: true,
                    activeActionId: "light_attack",
                    activeActionLocalFrame: 5,
                    activeActionAuthority: CharacterActionTimelineAuthority.CombatAnchored,
                    activeCancelRules: new[] { new CharacterCancelRule(0, 10, targetActionId: 2002) },
                    activeCombatTimeline: CreateCombatTimeline(1001, cancelTargetActionId: 3003)));

            CharacterActionResolveResult characterReject = resolver.ResolveCommand(
                characterRejectContext,
                CreateRequest(intentId: "Dodge"));
            CharacterActionResolveResult combatReject = resolver.ResolveCommand(
                combatRejectContext,
                CreateRequest(intentId: "Dodge"));

            Assert.IsTrue(characterReject.IsRejected);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCancelRejected, characterReject.Diagnostics[0].Code);
            Assert.IsTrue(combatReject.IsRejected);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CombatCancelRejected, combatReject.Diagnostics[0].Code);
        }

        [Test]
        public void QueueAllowedBinding_ReturnsQueuedWhenActiveActionBlocksImmediateStart()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new[] { new CharacterActionBinding("LightAttack", "light_attack", allowQueue: true, queueWindowFrames: 6) });
            CharacterActionResolverContext context = CreateContext(
                set,
                new[] { action },
                state: new CharacterActionResolverState(
                    hasActiveAction: true,
                    activeActionBlocksImmediateStart: true,
                    activeActionId: "recovery_lock"));

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: "LightAttack"));

            Assert.IsTrue(result.IsQueued);
            Assert.IsNotNull(result.Plan);
            Assert.AreEqual("light_attack", result.Plan.ActionId);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionQueued, result.Diagnostics[result.Diagnostics.Length - 1].Code);
        }

        [Test]
        public void QueueDisabledBinding_ReturnsLowerPriorityRejectedWhenActiveActionBlocksImmediateStart()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new[] { new CharacterActionBinding("LightAttack", "light_attack", allowQueue: false) });
            CharacterActionResolverContext context = CreateContext(
                set,
                new[] { action },
                state: new CharacterActionResolverState(
                    hasActiveAction: true,
                    activeActionBlocksImmediateStart: true,
                    activeActionId: "recovery_lock"));

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: "LightAttack"));

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.LowerPriorityRejected, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionLowerPriorityRejected, result.Diagnostics[0].Code);
        }

        [Test]
        public void ValidationHelpers_ReportBindingReactionAndCancelDiagnostics()
        {
            CharacterActionConfig reaction = CreateAction(300, "hit_react", CharacterActionCategory.Reaction);
            CharacterActionDiagnostic[] setDiagnostics = CharacterActionValidation.ValidateActionSet(
                CreateActionSet(
                    commandBindings: new[] { new CharacterActionBinding("Missing", "missing_action") }),
                new[] { reaction },
                new[] { new CharacterReactionProfile("pressure-only", null, "hit_react") });
            CharacterActionDiagnostic[] reactionDiagnostics = CharacterActionValidation.ValidatePressureOnlyReactionProfile(
                new CharacterReactionProfile(
                    "pressure-only",
                    new[] { new CharacterReactionRule("not_reaction", CharacterReactionRuleTrigger.Any) }),
                new[] { CreateAction(100, "not_reaction", CharacterActionCategory.BasicAttack) });
            CharacterActionDiagnostic[] cancelDiagnostics = CharacterActionValidation.ValidateCancelConflict(
                CharacterActionTimelineAuthority.CombatAnchored,
                new[] { new CharacterCancelRule(0, 10, targetActionId: 2002) },
                CreateCombatTimeline(1001, cancelTargetActionId: 3003),
                localFrame: 5,
                targetActionId: 2002,
                sourceKind: CharacterActionSourceKind.Command);

            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionConfig, setDiagnostics[0].Code);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ReactionRuleNoTarget, reactionDiagnostics[0].Code);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CombatCancelRejected, cancelDiagnostics[0].Code);
        }

        [Test]
        public void ValidationHelpers_ReportMissingTrackDependenciesWithStableDiagnostics()
        {
            CharacterActionConfig action = new CharacterActionConfig(
                id: 400,
                stableId: "broken_tracks",
                displayName: "Broken Tracks",
                category: CharacterActionCategory.Skill,
                timelineAuthority: CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 10,
                durationFrames: 12,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 3),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 4, 7),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 8, 11),
                },
                cancelRules: null,
                interruptRules: null,
                combatTrack: new CombatTrackConfig(string.Empty, new[]
                {
                    new CombatTrackEvent(4, CharacterActionTrackEventKind.StartCombatAction),
                }),
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(4, CharacterActionTrackEventKind.SendGameplayRequest),
                }),
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.PlayAnimation),
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(4, CharacterActionTrackEventKind.PlayAudioCue),
                    new PresentationTrackEvent(5, CharacterActionTrackEventKind.SpawnVisualCue),
                }));

            CharacterActionDiagnostic[] diagnostics = CharacterActionValidation.ValidateActionConfig(action);

            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.CombatActionMissing);
            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.ResourceCostWithoutResourceId);
            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.AnimationActionMissing);
            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.AudioCueMissing);
            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.PresentationResourceMissing);
        }

        private static CharacterActionResolverContext CreateContext(
            CharacterActionConfig[] actions,
            CharacterReactionProfile[] reactionProfiles = null,
            CombatActionTimeline[] combatTimelines = null)
        {
            return CreateContext(CreateActionSet(), actions, reactionProfiles, combatTimelines);
        }

        private static CharacterActionResolverContext CreateContext(
            CharacterActionSetConfig set,
            CharacterActionConfig[] actions,
            CharacterReactionProfile[] reactionProfiles = null,
            CombatActionTimeline[] combatTimelines = null,
            CharacterActionResolverState state = default,
            string[] contextTags = null)
        {
            return new CharacterActionResolverContext(
                set,
                actions,
                reactionProfiles,
                combatTimelines,
                state,
                new CharacterActionDurationPolicy(24),
                contextTags: contextTags);
        }

        private static CharacterActionSetConfig CreateActionSet(
            CharacterActionBinding[] commandBindings = null,
            CharacterAbilityActionBinding[] abilityBindings = null)
        {
            return new CharacterActionSetConfig(
                id: 1,
                stableId: "test.action_set",
                displayName: "Test Action Set",
                characterStableId: "test.character",
                equipmentStateStableId: "test.equipment",
                commandBindings: commandBindings ?? new[] { new CharacterActionBinding("LightAttack", "light_attack", allowQueue: true, queueWindowFrames: 8) },
                abilityBindings: abilityBindings ?? new[] { new CharacterAbilityActionBinding(9001, "dash_strike") },
                reactionBindings: null,
                movementProfileId: "movement.default",
                reactionProfileId: "pressure-only",
                defaultActionId: "idle");
        }

        private static CharacterActionIntentRequest CreateRequest(
            string intentId,
            int? abilityId = null,
            CharacterActionSourceKind sourceKind = CharacterActionSourceKind.Command,
            int priority = 10,
            string traceId = "trace-resolver")
        {
            return new CharacterActionIntentRequest(
                Entity(),
                intentId,
                abilityId,
                abilityStableId: abilityId.HasValue ? "ability." + abilityId.Value : string.Empty,
                requestedActionId: string.Empty,
                sourceKind: sourceKind,
                priority: priority,
                frame: new RuntimeFrame(7),
                traceId: traceId);
        }

        private static CharacterActionConfig CreateAction(
            int id,
            string stableId,
            CharacterActionCategory category,
            string[] tags = null,
            int priority = 10)
        {
            return new CharacterActionConfig(
                id,
                stableId,
                stableId,
                category,
                CharacterActionTimelineAuthority.CharacterAuthored,
                tags: tags,
                priority: priority,
                durationFrames: 24,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 5),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 6, 10),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 11, 23),
                },
                cancelRules: null,
                interruptRules: null,
                combatTrack: new CombatTrackConfig("1001", new[]
                {
                    new CombatTrackEvent(6, CharacterActionTrackEventKind.StartCombatAction, "1001"),
                }),
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(0, CharacterActionTrackEventKind.EmitDebugMarker, stableId + ".start"),
                }));
        }

        private static CharacterActionConfig CreateCombatAnchoredAction(CharacterActionPhase[] phases)
        {
            return CreateCombatAnchoredAction("1001", phases);
        }

        private static CharacterActionConfig CreateCombatAnchoredAction(string combatActionId, CharacterActionPhase[] phases)
        {
            return new CharacterActionConfig(
                id: 100,
                stableId: "light_attack",
                displayName: "Light Attack",
                category: CharacterActionCategory.BasicAttack,
                timelineAuthority: CharacterActionTimelineAuthority.CombatAnchored,
                tags: null,
                priority: 10,
                durationFrames: null,
                requirements: null,
                phases: phases,
                cancelRules: null,
                interruptRules: null,
                combatTrack: new CombatTrackConfig(combatActionId, null));
        }

        private static CharacterActionConfig CreateActionWithTrackDependencyIssue(
            string stableId,
            AnimationTrackConfig animationTrack,
            PresentationTrackConfig presentationTrack)
        {
            return new CharacterActionConfig(
                id: 100,
                stableId: "light_attack",
                displayName: stableId,
                category: CharacterActionCategory.BasicAttack,
                timelineAuthority: CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 10,
                durationFrames: 24,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 5),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 6, 10),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 11, 23),
                },
                cancelRules: null,
                interruptRules: null,
                combatTrack: new CombatTrackConfig("1001", null),
                animationTrack: animationTrack,
                presentationTrack: presentationTrack);
        }

        private static CombatActionTimeline CreateCombatTimeline(int actionId, int cancelTargetActionId)
        {
            return new CombatActionTimeline(
                actionId,
                totalFrames: 11,
                startup: new CombatFrameRange(0, 3),
                active: new CombatFrameRange(4, 6),
                recovery: new CombatFrameRange(7, 10),
                windows: new[]
                {
                    new CombatActionWindow(
                        CombatActionWindowKind.Cancel,
                        new CombatFrameRange(4, 6),
                        cancelTargetActionId),
                },
                events: null);
        }

        private static GameplayEntityId Entity()
        {
            return new GameplayEntityId(1, 1);
        }

        private static void AssertHasDiagnostic(CharacterActionDiagnostic[] diagnostics, string code)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Code == code)
                    return;
            }

            Assert.Fail("Expected diagnostic code " + code + ".");
        }
    }
}
