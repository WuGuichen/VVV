using System;
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
                CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(CharacterActionRejectReason.None, result.RejectReason);
            Assert.IsNotNull(result.Plan);
            Assert.AreEqual(CharacterTestActionIds.Action_light_attack, result.Plan.ActionId);
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
                abilityBindings: new[] { new CharacterAbilityActionBinding(9001, CharacterTestActionIds.Action_dash_strike) });
            CharacterActionResolverContext context = CreateContext(set, new[] { action });

            CharacterActionResolveResult result = resolver.ResolveAbility(
                context,
                CreateRequest(
                    intentId: 0,
                    abilityId: 9001,
                    sourceKind: CharacterActionSourceKind.GameplayAbility));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_dash_strike, result.Plan.ActionId);
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
                    new CharacterActionBinding(CharacterTestNumericIds.IntentAttack, CharacterTestActionIds.Action_light_attack, priority: 5),
                    new CharacterActionBinding(CharacterTestNumericIds.IntentAttack, CharacterTestActionIds.Action_basic_jump, priority: 8),
                    new CharacterActionBinding(CharacterTestNumericIds.IntentAttack, CharacterTestActionIds.Action_heavy_attack, priority: 8),
                });
            CharacterActionResolverContext context = CreateContext(set, new[] { light, heavy, jump });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: CharacterTestNumericIds.IntentAttack));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_heavy_attack, result.Plan.ActionId);
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
                    new CharacterActionBinding(CharacterTestNumericIds.IntentAttack, CharacterTestActionIds.Action_missing_heavy_attack, priority: 20),
                    new CharacterActionBinding(CharacterTestNumericIds.IntentAttack, CharacterTestActionIds.Action_light_attack, priority: 5),
                });
            CharacterActionResolverContext context = CreateContext(set, new[] { light });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: CharacterTestNumericIds.IntentAttack));

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.MissingActionConfig, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionConfig, result.Diagnostics[0].Code);
        }

        [Test]
        public void CommandBindings_UseNumericActionIdTieBreakerAfterSourceOrder()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig light = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack, priority: 10);
            CharacterActionConfig heavy = CreateAction(200, "heavy_attack", CharacterActionCategory.BasicAttack, priority: 10);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new[]
                {
                    new CharacterActionBinding(CharacterTestNumericIds.IntentAttack, CharacterTestActionIds.Action_light_attack, priority: 5),
                    new CharacterActionBinding(CharacterTestNumericIds.IntentAttack, CharacterTestActionIds.Action_heavy_attack, priority: 5),
                });
            CharacterActionResolverContext context = CreateContext(set, new[] { light, heavy });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: CharacterTestNumericIds.IntentAttack));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_light_attack, result.Plan.ActionId);
        }

        [Test]
        public void CommandSequence_AdvancesFromActiveActionAndWrapsAfterLast()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig first = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            CharacterActionConfig second = CreateAction(101, "light_attack_2", CharacterActionCategory.BasicAttack);
            CharacterActionConfig third = CreateAction(102, "light_attack_3", CharacterActionCategory.BasicAttack);
            var sequence = new CharacterActionCommandSequence(
                CharacterTestNumericIds.IntentLightAttack,
                new[] { first.ActionId, second.ActionId, third.ActionId },
                wrapAfterLast: true,
                priority: 10);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: Array.Empty<CharacterActionBinding>(),
                commandSequences: new[] { sequence });

            CharacterActionResolveResult initial = resolver.ResolveCommand(
                CreateContext(set, new[] { first, second, third }),
                CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack));
            CharacterActionResolveResult secondStep = resolver.ResolveCommand(
                CreateContext(
                    set,
                    new[] { first, second, third },
                    state: ActiveState(first.ActionId)),
                CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack));
            CharacterActionResolveResult thirdStep = resolver.ResolveCommand(
                CreateContext(
                    set,
                    new[] { first, second, third },
                    state: ActiveState(second.ActionId)),
                CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack));
            CharacterActionResolveResult wrapped = resolver.ResolveCommand(
                CreateContext(
                    set,
                    new[] { first, second, third },
                    state: ActiveState(third.ActionId)),
                CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack));

            Assert.AreEqual(first.ActionId, initial.Plan.ActionId);
            Assert.AreEqual(second.ActionId, secondStep.Plan.ActionId);
            Assert.AreEqual(third.ActionId, thirdStep.Plan.ActionId);
            Assert.AreEqual(first.ActionId, wrapped.Plan.ActionId);
        }

        [Test]
        public void CommandSequence_SingleActionWrapsToSameAction()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateAction(
                100,
                "light_attack",
                CharacterActionCategory.BasicAttack);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: Array.Empty<CharacterActionBinding>(),
                commandSequences: new[]
                {
                    new CharacterActionCommandSequence(
                        CharacterTestNumericIds.IntentLightAttack,
                        new[] { action.ActionId },
                        wrapAfterLast: true),
                });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                CreateContext(
                    set,
                    new[] { action },
                    state: ActiveState(action.ActionId)),
                CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(action.ActionId, result.Plan.ActionId);
        }

        [Test]
        public void CommandSequence_WithoutWrapRejectsAfterLastAction()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig first = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            CharacterActionConfig second = CreateAction(101, "light_attack_2", CharacterActionCategory.BasicAttack);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: Array.Empty<CharacterActionBinding>(),
                commandSequences: new[]
                {
                    new CharacterActionCommandSequence(
                        CharacterTestNumericIds.IntentLightAttack,
                        new[] { first.ActionId, second.ActionId }),
                });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                CreateContext(
                    set,
                    new[] { first, second },
                    state: ActiveState(second.ActionId)),
                CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack));

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CommandSequenceExhausted, result.Diagnostics[0].Code);
        }

        [Test]
        public void ActionSetValidation_RejectsIntentSharedByBindingAndSequence()
        {
            CharacterActionConfig action = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new[] { new CharacterActionBinding(CharacterTestNumericIds.IntentLightAttack, action.ActionId) },
                commandSequences: new[]
                {
                    new CharacterActionCommandSequence(CharacterTestNumericIds.IntentLightAttack, new[] { action.ActionId }),
                });

            CharacterActionDiagnostic[] diagnostics = CharacterActionValidation.ValidateActionSet(set, new[] { action });

            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.CommandIntentConflict);
        }

        [Test]
        public void BasicJumpCommandBinding_ReturnsJumpPlan()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig jump = CreateAction(300, "basic_jump", CharacterActionCategory.Jump, priority: 15);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new[] { new CharacterActionBinding(CharacterTestNumericIds.IntentJump, CharacterTestActionIds.Action_basic_jump, priority: 3) });
            CharacterActionResolverContext context = CreateContext(set, new[] { jump });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: CharacterTestNumericIds.IntentJump));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_basic_jump, result.Plan.ActionId);
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
                        CharacterTestActionIds.Action_dash_strike,
                        requiredTags: new[] { "weapon.sword", "stance.ready" }),
                });
            CharacterActionResolverContext context = CreateContext(
                set,
                new[] { action },
                contextTags: new[] { "stance.ready" });

            CharacterActionResolveResult result = resolver.ResolveAbility(
                context,
                CreateRequest(
                    intentId: 0,
                    abilityId: 9001,
                    sourceKind: CharacterActionSourceKind.GameplayAbility));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_dash_strike, result.Plan.ActionId);
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
                        CharacterTestActionIds.Action_dash_strike,
                        requiredTags: new[] { "weapon.sword" }),
                });
            CharacterActionResolverContext context = CreateContext(set, new[] { action });

            CharacterActionResolveResult result = resolver.ResolveAbility(
                context,
                CreateRequest(
                    intentId: 0,
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
                        CharacterTestActionIds.Action_dash_strike,
                        forbiddenTags: new[] { "status.silenced" }),
                });
            CharacterActionResolverContext context = CreateContext(set, new[] { action });

            CharacterActionResolveResult result = resolver.ResolveAbility(
                context,
                CreateRequest(
                    intentId: 0,
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
            CharacterActionConfig reaction = CreateAction(300, "cohesion_break_react", CharacterActionCategory.Reaction);
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_cohesion_break_react,
                        CharacterReactionRuleTrigger.CohesionBreak,
                        currentScatterPressureBand: PressureBand.Broken),
                });
            CharacterActionResolverContext context = CreateContext(
                actions: new[] { reaction },
                reactionProfiles: new[] { profile });
            CharacterReactionContext reactionContext = CharacterReactionContextBuilder.FromCohesionBreak(
                new CohesionBreakEvent(
                    new RuntimeFrame(3),
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 80,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100,
                    delta: 20)).Context;

            CharacterActionResolveResult result = resolver.ResolveReaction(context, reactionContext);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_cohesion_break_react, result.Plan.ActionId);
            Assert.AreEqual(CharacterActionCategory.Reaction, result.Plan.Category);
        }

        [Test]
        public void FullHitReactionContext_ResolvesHitAwareReactionAction()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig torsoReact = CreateAction(300, "torso_back_slash_react", CharacterActionCategory.Reaction);
            CharacterActionConfig fallback = CreateAction(301, "fallback_hit_react", CharacterActionCategory.Reaction);
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_torso_back_slash_react,
                        CharacterReactionRuleTrigger.Hit,
                        bodyPartId: CharacterTestNumericIds.BodyPartTorso,
                        hitZoneId: CharacterTestNumericIds.HitZoneRib,
                        damageTypeId: CharacterTestNumericIds.DamageSlash,
                        hitDirection: CharacterHitDirection.Back,
                        minImpactForce: 40,
                        reactionGroupId: CharacterTestNumericIds.ReactionCore),
                },
                defaultActionId: CharacterTestActionIds.Action_fallback_hit_react);
            CharacterReactionContext context = CharacterReactionContextBuilder.FromHitSource(
                new CharacterReactionHitSource(
                    new RuntimeFrame(12),
                    Entity(),
                    bodyPartId: CharacterTestNumericIds.BodyPartTorso,
                    hitZoneId: CharacterTestNumericIds.HitZoneRib,
                    damageTypeId: CharacterTestNumericIds.DamageSlash,
                    hitDirection: CharacterHitDirection.Back,
                    impactForce: 64,
                    reactionGroupId: CharacterTestNumericIds.ReactionCore,
                    traceId: CharacterTestRuntimeTraceIds.TraceFullHit)).Context;
            CharacterActionResolverContext resolverContext = CreateContext(
                actions: new[] { torsoReact, fallback },
                reactionProfiles: new[] { profile });

            CharacterActionResolveResult result = resolver.ResolveReaction(resolverContext, context);
            CharacterActionDiagnostic[] validation = CharacterActionValidation.ValidateReactionProfile(
                profile,
                new[] { torsoReact, fallback },
                CharacterReactionContextCompleteness.Full);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_torso_back_slash_react, result.Plan.ActionId);
            Assert.AreEqual(CharacterActionCategory.Reaction, result.Plan.Category);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleMatched);
            Assert.AreEqual(0, validation.Length);
        }

        [Test]
        public void MixedReactionProfile_PressureOnlyCohesionBreakSkipsIrrelevantFullHitRule()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig hitReact = CreateAction(300, "torso_hit_react", CharacterActionCategory.Reaction);
            CharacterActionConfig scatterPressureReact = CreateAction(301, "cohesion_break_react", CharacterActionCategory.Reaction);
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_torso_hit_react,
                        CharacterReactionRuleTrigger.Hit,
                        bodyPartId: CharacterTestNumericIds.BodyPartTorso,
                        hitZoneId: CharacterTestNumericIds.HitZoneRib,
                        damageTypeId: CharacterTestNumericIds.DamageSlash,
                        hitDirection: CharacterHitDirection.Front,
                        minImpactForce: 40,
                        reactionGroupId: CharacterTestNumericIds.ReactionCore),
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_cohesion_break_react,
                        CharacterReactionRuleTrigger.CohesionBreak,
                        currentScatterPressureBand: PressureBand.Broken),
                });
            CharacterActionResolverContext context = CreateContext(
                actions: new[] { hitReact, scatterPressureReact },
                reactionProfiles: new[] { profile });
            CharacterReactionContext reactionContext = CharacterReactionContextBuilder.FromCohesionBreak(
                new CohesionBreakEvent(
                    new RuntimeFrame(3),
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 80,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100,
                    delta: 20)).Context;

            CharacterActionResolveResult result = resolver.ResolveReaction(context, reactionContext);
            CharacterActionDiagnostic[] authoringDiagnostics =
                CharacterActionValidation.ValidatePressureOnlyReactionProfile(profile, new[] { hitReact, scatterPressureReact });

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_cohesion_break_react, result.Plan.ActionId);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleSkipped);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleMatched);
            AssertHasDiagnostic(authoringDiagnostics, CharacterActionDiagnosticCodes.ReactionRuleRequiresHitContext);
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
                        CharacterTestActionIds.Action_light_attack,
                        CharacterReactionRuleTrigger.CohesionBreak,
                        currentScatterPressureBand: PressureBand.Broken),
                });
            CharacterActionResolverContext context = CreateContext(
                actions: new[] { basicAttack },
                reactionProfiles: new[] { profile });
            CharacterReactionContext reactionContext = CharacterReactionContextBuilder.FromCohesionBreak(
                new CohesionBreakEvent(
                    new RuntimeFrame(3),
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 80,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100,
                    delta: 20)).Context;

            CharacterActionResolveResult result = resolver.ResolveReaction(context, reactionContext);

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.InvalidTarget, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ReactionRuleNoTarget, result.Diagnostics[0].Code);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleMatched);
            Assert.AreNotEqual(CharacterActionDiagnosticCodes.ReactionRuleMatched, result.Diagnostics[0].Code);
        }

        [Test]
        public void ReactionRuntime_ResolvesAndStartsReactionAction()
        {
            CharacterActionConfig reaction = CreateAction(300, "cohesion_break_react", CharacterActionCategory.Reaction);
            CharacterReactionProfile profile = CreateCohesionBreakProfile(CharacterTestActionIds.Action_cohesion_break_react);
            CharacterActionResolverContext context = CreateContext(
                actions: new[] { reaction },
                reactionProfiles: new[] { profile });
            var gateway = new CharacterActionExecutionGateway();
            var planCache = new CharacterActionRuntimePlanCache(context);
            var buffer = new CharacterActionReactionStartBuffer();

            CharacterActionReactionStartResult result = CharacterActionReactionRuntime.TryStart(
                context,
                CreateCohesionBreakContext(),
                gateway,
                planCache,
                buffer);

            Assert.IsTrue(result.Started, result.DiagnosticMessage);
            Assert.IsTrue(result.HasExecution);
            Assert.AreEqual(CharacterActionExecutionStatus.Started, result.Execution.Status);
            Assert.AreEqual(CharacterTestActionIds.Action_cohesion_break_react, gateway.ActiveInstance.Plan.ActionId);
            AssertHasRunnerEvent(result.Execution.Events, CharacterActionRunnerEventKind.ActionStarted);
        }

        [Test]
        public void ReactionRuntime_BufferedStartDoesNotAllocateAfterWarmup()
        {
            CharacterActionConfig reaction = CreateAction(
                300,
                "cohesion_break_react",
                CharacterActionCategory.Reaction,
                priority: 100,
                interruptRules: new[]
                {
                    new CharacterInterruptRule(
                        CharacterActionSourceKind.CohesionBreak,
                        minimumPriority: 50,
                        targetActionId: CharacterTestActionIds.Action_cohesion_break_react),
                });
            CharacterReactionProfile profile = CreateCohesionBreakProfile(CharacterTestActionIds.Action_cohesion_break_react);
            CharacterActionResolverContext context = CreateContext(
                actions: new[] { reaction },
                reactionProfiles: new[] { profile });
            var gateway = new CharacterActionExecutionGateway();
            var planCache = new CharacterActionRuntimePlanCache(context);
            var buffer = new CharacterActionReactionStartBuffer(20);
            CharacterReactionContext reactionContext = CreateCohesionBreakContext();

            for (int i = 0; i < 8; i++)
            {
                CharacterActionReactionStartResult warm = CharacterActionReactionRuntime.TryStart(
                    context,
                    reactionContext,
                    gateway,
                    planCache,
                    buffer);
                Assert.IsTrue(warm.Started, warm.DiagnosticCode);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            CharacterActionReactionStartResult result = default;
            bool allStarted = true;
            for (int i = 0; i < 16; i++)
            {
                result = CharacterActionReactionRuntime.TryStart(
                    context,
                    reactionContext,
                    gateway,
                    planCache,
                    buffer);
                allStarted &= result.Started;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(allStarted, result.DiagnosticCode);
            Assert.AreEqual(0L, allocated);
            Assert.IsTrue(result.HasExecution);
            Assert.AreEqual(CharacterActionExecutionStatus.Interrupted, result.Execution.Status);
            AssertHasRunnerEvent(result.Execution.Events, CharacterActionRunnerEventKind.ActionInterrupted);
            AssertHasRunnerEvent(result.Execution.Events, CharacterActionRunnerEventKind.ActionStarted);
            Assert.AreEqual(CharacterTestActionIds.Action_cohesion_break_react, gateway.ActiveInstance.Plan.ActionId);
        }

        [Test]
        public void ReactionSelector_NoAllocSelectionMatchesDiagnosticSelector()
        {
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_fallback_specific,
                        CharacterReactionRuleTrigger.CohesionBreak,
                        currentScatterPressureBand: PressureBand.Cracked),
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_cohesion_break_react,
                        CharacterReactionRuleTrigger.CohesionBreak,
                        currentScatterPressureBand: PressureBand.Broken,
                        priority: 10),
                },
                defaultActionId: CharacterTestActionIds.Action_default_react);
            CharacterReactionContext context = CreateCohesionBreakContext();

            CharacterReactionSelectionResult diagnostic = CharacterReactionSelector.Select(profile, context);
            CharacterReactionSelectionFastResult noAlloc = CharacterReactionSelector.SelectNoAlloc(profile, context);
            CharacterReactionSelectionResult fallbackDiagnostic = CharacterReactionSelector.Select(
                new CharacterReactionProfile("pressure-only", Array.Empty<CharacterReactionRule>(), CharacterTestActionIds.Action_default_react),
                context);
            CharacterReactionSelectionFastResult fallbackNoAlloc = CharacterReactionSelector.SelectNoAlloc(
                new CharacterReactionProfile("pressure-only", Array.Empty<CharacterReactionRule>(), CharacterTestActionIds.Action_default_react),
                context);

            Assert.AreEqual(diagnostic.Accepted, noAlloc.Accepted);
            Assert.AreEqual(diagnostic.SelectedActionId, noAlloc.SelectedActionId);
            Assert.IsFalse(noAlloc.FallbackUsed);
            Assert.AreEqual(fallbackDiagnostic.Accepted, fallbackNoAlloc.Accepted);
            Assert.AreEqual(fallbackDiagnostic.SelectedActionId, fallbackNoAlloc.SelectedActionId);
            Assert.IsTrue(fallbackNoAlloc.FallbackUsed);
        }

        [Test]
        public void ReactionRuntime_RejectsFallbackWhenDisabled()
        {
            CharacterActionConfig fallback = CreateAction(301, "fallback_hit_react", CharacterActionCategory.Reaction);
            var profile = new CharacterReactionProfile(
                "pressure-only",
                rules: null,
                defaultActionId: CharacterTestActionIds.Action_fallback_hit_react);
            CharacterActionResolverContext context = CreateContext(
                actions: new[] { fallback },
                reactionProfiles: new[] { profile });
            var gateway = new CharacterActionExecutionGateway();

            CharacterActionReactionStartResult result = CharacterActionReactionRuntime.TryStart(
                context,
                CreateCohesionBreakContext(),
                gateway,
                new CharacterActionRuntimePlanCache(context),
                new CharacterActionReactionStartBuffer(),
                allowFallback: false);

            Assert.IsFalse(result.Started);
            Assert.IsTrue(result.FallbackRejected);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ReactionFallbackUsed, result.DiagnosticCode);
            Assert.IsFalse(gateway.HasActiveInstance);
        }

        [Test]
        public void ReactionRuntime_OrdinaryReactionDoesNotBypassInterruptRules()
        {
            CharacterActionConfig active = CreateAction(200, "active_reaction", CharacterActionCategory.Reaction);
            CharacterActionConfig next = CreateAction(300, "cohesion_break_react", CharacterActionCategory.Reaction);
            CharacterReactionProfile profile = CreateCohesionBreakProfile(CharacterTestActionIds.Action_cohesion_break_react);
            CharacterActionResolverContext context = CreateContext(
                actions: new[] { active, next },
                reactionProfiles: new[] { profile });
            var gateway = new CharacterActionExecutionGateway();
            var eventBuffer = new CharacterActionRunnerEventBuffer(16);
            CharacterActionExecutionRequest startRequest = CharacterActionExecutionRequest.StartIntent(
                CharacterActionResolveResult.Success(CreatePlan(active)),
                CharacterActionRunnerActionDefinition.FromConfig(active),
                CharacterActionSourceKind.Command,
                active.Priority,
                CharacterTestRuntimeTraceIds.ReactionActive);
            Assert.IsTrue(gateway.Execute(in startRequest, eventBuffer).Accepted);
            long activeInstanceId = gateway.ActiveInstance.InstanceId;

            CharacterActionReactionStartResult result = CharacterActionReactionRuntime.TryStart(
                context,
                CreateCohesionBreakContext(),
                gateway,
                new CharacterActionRuntimePlanCache(context),
                new CharacterActionReactionStartBuffer());

            Assert.IsFalse(result.Started);
            Assert.IsTrue(result.HasExecution);
            Assert.AreEqual(CharacterActionExecutionStatus.Rejected, result.Execution.Status);
            AssertHasRunnerEvent(result.Execution.Events, CharacterActionRunnerEventKind.InterruptRejected);
            Assert.AreEqual(CharacterTestActionIds.Action_active_reaction, gateway.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(activeInstanceId, gateway.ActiveInstance.InstanceId);
            Assert.AreEqual(CharacterActionInstanceState.Running, gateway.ActiveInstance.State);
        }

        [Test]
        public void ReactionRuntime_DeathTerminatesActiveBeforeStartingResolvedDeathAction()
        {
            CharacterActionConfig active = CreateAction(200, "active_action", CharacterActionCategory.BasicAttack);
            CharacterActionConfig death = CreateAction(400, "death_react", CharacterActionCategory.Reaction, priority: 100);
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_death_react,
                        CharacterReactionRuleTrigger.Death,
                        isDeath: true),
                });
            CharacterActionResolverContext context = CreateContext(
                actions: new[] { active, death },
                reactionProfiles: new[] { profile });
            var gateway = new CharacterActionExecutionGateway();
            var eventBuffer = new CharacterActionRunnerEventBuffer(16);
            CharacterActionExecutionRequest startRequest = CharacterActionExecutionRequest.StartIntent(
                CharacterActionResolveResult.Success(CreatePlan(active)),
                CharacterActionRunnerActionDefinition.FromConfig(active),
                CharacterActionSourceKind.Command,
                active.Priority,
                CharacterTestRuntimeTraceIds.ReactionActive);
            Assert.IsTrue(gateway.Execute(in startRequest, eventBuffer).Accepted);
            CharacterReactionContext deathContext = CharacterReactionContextBuilder.FromDeath(
                new RuntimeFrame(9),
                Entity(),
                "death",
                CharacterTestRuntimeTraceIds.ReactionDeath).Context;

            CharacterActionReactionStartResult result = CharacterActionReactionRuntime.TryStart(
                context,
                deathContext,
                gateway,
                new CharacterActionRuntimePlanCache(context),
                new CharacterActionReactionStartBuffer());

            Assert.IsTrue(result.Started, result.DiagnosticMessage);
            Assert.IsTrue(result.HasExecution);
            Assert.AreEqual(CharacterActionExecutionStatus.LifecycleReplaced, result.Execution.Status);
            AssertHasRunnerEvent(result.Execution.Events, CharacterActionRunnerEventKind.ActionCancelled);
            AssertHasRunnerEvent(result.Execution.Events, CharacterActionRunnerEventKind.ActionStarted);
            Assert.AreEqual(CharacterTestActionIds.Action_death_react, gateway.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(2L, gateway.ActiveInstance.InstanceId);
        }

        [Test]
        public void ReactionRuntime_ReturnsStableDiagnosticsForMissingTargets()
        {
            CharacterActionConfig reaction = CreateAction(300, "cohesion_break_react", CharacterActionCategory.Reaction);
            CharacterReactionProfile profile = CreateCohesionBreakProfile(CharacterTestActionIds.Action_cohesion_break_react);
            CharacterActionResolverContext context = CreateContext(
                actions: new[] { reaction },
                reactionProfiles: new[] { profile });

            CharacterActionResolverContext missingActionContext = CreateContext(
                actions: Array.Empty<CharacterActionConfig>(),
                reactionProfiles: new[] { profile });
            CharacterActionReactionStartResult missingAction = CharacterActionReactionRuntime.TryStart(
                missingActionContext,
                CreateCohesionBreakContext(),
                new CharacterActionExecutionGateway(),
                new CharacterActionRuntimePlanCache(missingActionContext),
                new CharacterActionReactionStartBuffer());
            CharacterActionResolverContext missingProfileContext = CreateContext(
                CreateActionSet(),
                new[] { reaction },
                reactionProfiles: Array.Empty<CharacterReactionProfile>());
            CharacterActionReactionStartResult missingProfile = CharacterActionReactionRuntime.TryStart(
                missingProfileContext,
                CreateCohesionBreakContext(),
                new CharacterActionExecutionGateway(),
                new CharacterActionRuntimePlanCache(missingProfileContext),
                new CharacterActionReactionStartBuffer());
            CharacterActionConfig basic = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            var nonReactionProfile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_light_attack,
                        CharacterReactionRuleTrigger.CohesionBreak,
                        currentScatterPressureBand: PressureBand.Broken),
                });
            CharacterActionResolverContext nonReactionContext = CreateContext(
                actions: new[] { basic },
                reactionProfiles: new[] { nonReactionProfile });
            CharacterActionReactionStartResult nonReaction = CharacterActionReactionRuntime.TryStart(
                nonReactionContext,
                CreateCohesionBreakContext(),
                new CharacterActionExecutionGateway(),
                new CharacterActionRuntimePlanCache(nonReactionContext),
                new CharacterActionReactionStartBuffer());

            Assert.IsFalse(missingAction.Started);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionConfig, missingAction.DiagnosticCode);
            Assert.IsFalse(missingProfile.Started);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingReactionProfile, missingProfile.DiagnosticCode);
            Assert.IsFalse(nonReaction.Started);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionConfig, nonReaction.DiagnosticCode);
        }

        [Test]
        public void MissingActionSetBindingAndConfig_RejectWithStableDiagnostics()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionResolveResult missingSet = resolver.ResolveCommand(
                new CharacterActionResolverContext(null, null),
                CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack, traceId: CharacterTestRuntimeTraceIds.MissingSet));
            CharacterActionResolveResult missingBinding = resolver.ResolveCommand(
                CreateContext(CreateActionSet(commandBindings: new CharacterActionBinding[0]), new[] { CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack) }),
                CreateRequest(intentId: CharacterTestNumericIds.IntentHeavyAttack, traceId: CharacterTestRuntimeTraceIds.MissingBinding));
            CharacterActionResolveResult missingConfig = resolver.ResolveCommand(
                CreateContext(CreateActionSet(), new CharacterActionConfig[0]),
                CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack, traceId: CharacterTestRuntimeTraceIds.MissingConfig));

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

            CharacterActionResolveResult result = resolver.ResolveCommand(context, CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack));

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
            CharacterActionResolverContext context = CreateContext(
                CreateActionSet(commandBindings: new[]
                {
                    new CharacterActionBinding(CharacterTestNumericIds.IntentLightAttack, action.ActionId),
                }),
                new[] { action });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack));

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
            CharacterActionResolverContext context = CreateContext(
                CreateActionSet(commandBindings: new[]
                {
                    new CharacterActionBinding(CharacterTestNumericIds.IntentLightAttack, action.ActionId),
                }),
                new[] { action });

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack));

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.ResourceMissing, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.PresentationResourceMissing, result.Diagnostics[0].Code);
        }

        [Test]
        public void CombatAnchoredAction_CanResolveTimelineThroughStableCombatActionIdBinding()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateCombatAnchoredAction(
                combatActionId: CharacterTestNumericIds.Field_combat_light_attack,
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
                    new CharacterActionCombatTimelineBinding(CharacterTestNumericIds.Field_combat_light_attack, timeline),
                });

            CharacterActionResolveResult result = resolver.ResolveCommand(context, CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack));

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
                commandBindings: new[] { new CharacterActionBinding(CharacterTestNumericIds.IntentDodge, CharacterTestActionIds.Action_dodge) });
            CharacterActionResolverContext characterRejectContext = CreateContext(
                set,
                new[] { action },
                state: new CharacterActionResolverState(
                    hasActiveAction: true,
                    activeActionId: new MxFramework.CharacterAction.CharacterActionId(301),
                    activeActionLocalFrame: 5,
                    activeActionAuthority: CharacterActionTimelineAuthority.CombatAnchored,
                    activeCancelRules: new[] { new CharacterCancelRule(0, 10, CharacterActionRequestType.Skill) },
                    activeCombatTimeline: CreateCombatTimeline(1001, cancelTargetActionId: 2002)));
            CharacterActionResolverContext combatRejectContext = CreateContext(
                set,
                new[] { action },
                state: new CharacterActionResolverState(
                    hasActiveAction: true,
                    activeActionId: new MxFramework.CharacterAction.CharacterActionId(301),
                    activeActionLocalFrame: 5,
                    activeActionAuthority: CharacterActionTimelineAuthority.CombatAnchored,
                    activeCancelRules: new[] { new CharacterCancelRule(0, 10) },
                    activeCombatTimeline: CreateCombatTimeline(1001, cancelTargetActionId: 3003)));

            CharacterActionResolveResult characterReject = resolver.ResolveCommand(
                characterRejectContext,
                CreateRequest(intentId: CharacterTestNumericIds.IntentDodge));
            CharacterActionResolveResult combatReject = resolver.ResolveCommand(
                combatRejectContext,
                CreateRequest(intentId: CharacterTestNumericIds.IntentDodge));

            Assert.IsTrue(characterReject.IsRejected);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCancelRejected, characterReject.Diagnostics[0].Code);
            Assert.IsTrue(combatReject.IsRejected);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CombatCancelRejected, combatReject.Diagnostics[0].Code);
        }

        [Test]
        public void ActionSlotRequest_UsesDefaultActionSlotMaskWithoutActiveAction()
        {
            const int attackPrimary = 1;
            const int dodge = 4;
            var resolver = new CharacterActionResolver();
            CharacterActionConfig attack = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new CharacterActionBinding[0],
                defaultActionSlotMask: attackPrimary);
            CharacterActionResolverContext context = CreateContext(set, new[] { attack });

            CharacterActionResolveResult allowed = resolver.ResolveActionSlot(
                context,
                CreateRequest(
                    intentId: 0,
                    requestedActionId: CharacterTestActionIds.Action_light_attack,
                    actionSlotId: attackPrimary));
            CharacterActionResolveResult blocked = resolver.ResolveActionSlot(
                context,
                CreateRequest(
                    intentId: 0,
                    requestedActionId: CharacterTestActionIds.Action_light_attack,
                    actionSlotId: dodge));

            Assert.IsTrue(allowed.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_light_attack, allowed.Plan.ActionId);
            Assert.IsTrue(blocked.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.ActionSlotBlocked, blocked.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionSlotBlocked, blocked.Diagnostics[0].Code);
        }

        [Test]
        public void ActionSlotRequest_EmptySlotRejectsWithoutResolvingAction()
        {
            const int dodge = 4;
            var resolver = new CharacterActionResolver();
            CharacterActionConfig attack = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new CharacterActionBinding[0],
                defaultActionSlotMask: dodge);
            CharacterActionResolverContext context = CreateContext(set, new[] { attack });

            CharacterActionResolveResult result = resolver.ResolveActionSlot(
                context,
                CreateRequest(
                    intentId: 0,
                    requestedActionId: CharacterActionId.None,
                    actionSlotId: dodge));

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.ActionSlotEmpty, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionSlotEmpty, result.Diagnostics[0].Code);
        }

        [Test]
        public void ActionSlotRequest_ActiveWindowsUseActionSlotMask()
        {
            const int attackPrimary = 1;
            const int dodge = 4;
            var resolver = new CharacterActionResolver();
            CharacterActionConfig followUp = CreateAction(101, "light_attack_2", CharacterActionCategory.BasicAttack);
            CharacterActionConfig dodgeAction = CreateAction(200, "dodge", CharacterActionCategory.Dodge);
            CharacterActionSetConfig set = CreateActionSet(commandBindings: new CharacterActionBinding[0]);
            CharacterActionResolverContext frame10 = CreateContext(
                set,
                new[] { followUp, dodgeAction },
                state: new CharacterActionResolverState(
                    hasActiveAction: true,
                    activeActionId: new MxFramework.CharacterAction.CharacterActionId(301),
                    activeActionLocalFrame: 10,
                    activeCancelRules: new[]
                    {
                        new CharacterCancelRule(6, 75, cancelInputMask: (CharacterCancelInputMask)dodge),
                        new CharacterCancelRule(18, 34, cancelInputMask: (CharacterCancelInputMask)attackPrimary),
                    }));
            CharacterActionResolverContext frame18 = CreateContext(
                set,
                new[] { followUp, dodgeAction },
                state: new CharacterActionResolverState(
                    hasActiveAction: true,
                    activeActionId: new MxFramework.CharacterAction.CharacterActionId(301),
                    activeActionLocalFrame: 18,
                    activeCancelRules: new[]
                    {
                        new CharacterCancelRule(6, 75, cancelInputMask: (CharacterCancelInputMask)dodge),
                        new CharacterCancelRule(18, 34, cancelInputMask: (CharacterCancelInputMask)attackPrimary),
                    }));

            CharacterActionResolveResult attackEarly = resolver.ResolveActionSlot(
                frame10,
                CreateRequest(
                    intentId: 0,
                    requestedActionId: CharacterTestActionIds.Action_light_attack_2,
                    actionSlotId: attackPrimary));
            CharacterActionResolveResult dodgeEarly = resolver.ResolveActionSlot(
                frame10,
                CreateRequest(
                    intentId: 0,
                    requestedActionId: CharacterTestActionIds.Action_dodge,
                    actionSlotId: dodge));
            CharacterActionResolveResult attackWindow = resolver.ResolveActionSlot(
                frame18,
                CreateRequest(
                    intentId: 0,
                    requestedActionId: CharacterTestActionIds.Action_light_attack_2,
                    actionSlotId: attackPrimary));

            Assert.IsTrue(attackEarly.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.ActionSlotBlocked, attackEarly.RejectReason);
            Assert.IsTrue(dodgeEarly.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_dodge, dodgeEarly.Plan.ActionId);
            Assert.IsTrue(attackWindow.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_light_attack_2, attackWindow.Plan.ActionId);
        }

        [Test]
        public void QueueAllowedBinding_ReturnsQueuedWhenActiveActionBlocksImmediateStart()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new[] { new CharacterActionBinding(CharacterTestNumericIds.IntentLightAttack, CharacterTestActionIds.Action_light_attack, allowQueue: true, queueWindowFrames: 6) });
            CharacterActionResolverContext context = CreateContext(
                set,
                new[] { action },
                state: new CharacterActionResolverState(
                    hasActiveAction: true,
                    activeActionBlocksImmediateStart: true,
                    activeActionId: CharacterTestActionIds.Action_recovery_lock));

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack));

            Assert.IsTrue(result.IsQueued);
            Assert.IsNotNull(result.Plan);
            Assert.AreEqual(CharacterTestActionIds.Action_light_attack, result.Plan.ActionId);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionQueued, result.Diagnostics[result.Diagnostics.Length - 1].Code);
        }

        [Test]
        public void QueueAllowedCommandSequence_ReturnsQueuedWhenActiveActionBlocksImmediateStart()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: Array.Empty<CharacterActionBinding>(),
                commandSequences: new[]
                {
                    new CharacterActionCommandSequence(
                        CharacterTestNumericIds.IntentLightAttack,
                        new[] { action.ActionId },
                        allowQueue: true,
                        queueWindowFrames: 6),
                });
            CharacterActionResolverContext context = CreateContext(
                set,
                new[] { action },
                state: new CharacterActionResolverState(
                    hasActiveAction: true,
                    activeActionBlocksImmediateStart: true,
                    activeActionId: CharacterTestActionIds.Action_recovery_lock));

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack));

            Assert.IsTrue(result.IsQueued);
            Assert.IsNotNull(result.Plan);
            Assert.AreEqual(action.ActionId, result.Plan.ActionId);
            Assert.AreEqual(
                CharacterActionDiagnosticCodes.ActionQueued,
                result.Diagnostics[result.Diagnostics.Length - 1].Code);
        }

        [Test]
        public void QueueDisabledBinding_ReturnsLowerPriorityRejectedWhenActiveActionBlocksImmediateStart()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            CharacterActionSetConfig set = CreateActionSet(
                commandBindings: new[] { new CharacterActionBinding(CharacterTestNumericIds.IntentLightAttack, CharacterTestActionIds.Action_light_attack, allowQueue: false) });
            CharacterActionResolverContext context = CreateContext(
                set,
                new[] { action },
                state: new CharacterActionResolverState(
                    hasActiveAction: true,
                    activeActionBlocksImmediateStart: true,
                    activeActionId: CharacterTestActionIds.Action_recovery_lock));

            CharacterActionResolveResult result = resolver.ResolveCommand(
                context,
                CreateRequest(intentId: CharacterTestNumericIds.IntentLightAttack));

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.LowerPriorityRejected, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionLowerPriorityRejected, result.Diagnostics[0].Code);
        }

        [Test]
        public void RuntimePlanCache_MatchesDirectActionSlotResolveAndDoesNotAllocateAfterWarmup()
        {
            var resolver = new CharacterActionResolver();
            CharacterActionConfig action = CreateAction(100, "light_attack", CharacterActionCategory.BasicAttack);
            CharacterActionResolverContext context = CreateContext(
                CreateActionSet(defaultActionSlotMask: 1),
                new[] { action });
            CharacterActionResolveResult legacy = resolver.ResolveActionSlot(
                context,
                CreateRequest(
                    intentId: 0,
                    requestedActionId: CharacterTestActionIds.Action_light_attack,
                    actionSlotId: 1,
                    traceId: CharacterTestRuntimeTraceIds.TraceCache));
            var cache = new CharacterActionRuntimePlanCache(context);

            Assert.IsTrue(cache.TryGetOrCreate(CharacterTestActionIds.Action_light_attack, CharacterTestRuntimeTraceIds.TraceCache, out CharacterActionRuntimePlanEntry cached));
            Assert.IsTrue(legacy.IsSuccess);
            Assert.IsTrue(cached.IsSuccess);
            Assert.AreEqual(legacy.Plan.ActionId, cached.Plan.ActionId);
            Assert.AreEqual(legacy.Plan.Category, cached.Plan.Category);
            Assert.AreEqual(legacy.Plan.Priority, cached.Plan.Priority);
            Assert.AreEqual(legacy.Plan.DurationFrames, cached.Plan.DurationFrames);
            Assert.AreEqual(legacy.Plan.TraceId, cached.Plan.TraceId);
            Assert.AreEqual(legacy.Plan.Tracks.Length, cached.Plan.Tracks.Length);
            Assert.AreEqual(legacy.Diagnostics.Length, cached.Diagnostics.Length);
            for (int i = 0; i < legacy.Plan.Tracks.Length; i++)
                Assert.AreEqual(legacy.Plan.Tracks[i], cached.Plan.Tracks[i]);
            for (int i = 0; i < legacy.Diagnostics.Length; i++)
                Assert.AreEqual(legacy.Diagnostics[i].Code, cached.Diagnostics[i].Code);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool resolved = true;
            for (int i = 0; i < 128; i++)
            {
                resolved &= cache.TryGetOrCreate(
                    CharacterTestActionIds.Action_light_attack,
                    CharacterTestRuntimeTraceIds.TraceCache,
                    out CharacterActionRuntimePlanEntry warm);
                resolved &= ReferenceEquals(cached, warm);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(resolved);
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void ValidationHelpers_ReportBindingReactionAndCancelDiagnostics()
        {
            CharacterActionConfig reaction = CreateAction(300, "hit_react", CharacterActionCategory.Reaction);
            CharacterActionDiagnostic[] setDiagnostics = CharacterActionValidation.ValidateActionSet(
                CreateActionSet(
                    commandBindings: new[] { new CharacterActionBinding(CharacterTestNumericIds.IntentMissing, CharacterTestActionIds.Action_missing_action) }),
                new[] { reaction },
                new[] { new CharacterReactionProfile("pressure-only", null, CharacterTestActionIds.Action_hit_react) });
            CharacterActionDiagnostic[] reactionDiagnostics = CharacterActionValidation.ValidatePressureOnlyReactionProfile(
                new CharacterReactionProfile(
                    "pressure-only",
                    new[] { new CharacterReactionRule(CharacterTestActionIds.Action_not_reaction, CharacterReactionRuleTrigger.Any) }),
                new[] { CreateAction(100, "not_reaction", CharacterActionCategory.BasicAttack) });
            CharacterActionDiagnostic[] cancelDiagnostics = CharacterActionValidation.ValidateCancelConflict(
                CharacterActionTimelineAuthority.CombatAnchored,
                new[] { new CharacterCancelRule(0, 10) },
                CreateCombatTimeline(1001, cancelTargetActionId: 3003),
                localFrame: 5,
                targetActionId: new CharacterActionId(2002),
                requestCategory: CharacterActionCategory.Dodge);

            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionConfig, setDiagnostics[0].Code);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ReactionRuleNoTarget, reactionDiagnostics[0].Code);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CombatCancelRejected, cancelDiagnostics[0].Code);
        }

        [Test]
        public void ValidationHelpers_ReportMissingTrackDependenciesWithStableDiagnostics()
        {
            CharacterActionConfig action = new CharacterActionConfig(
                actionId: new CharacterActionId(400),
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
                combatTrack: new CombatTrackConfig(0, new[]
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

        [Test]
        public void ValidationHelpers_RejectCancelWindowInsideNonCancelablePhase()
        {
            CharacterActionConfig action = CreateTimelineValidationAction(
                cancelRules: new[]
                {
                    new CharacterCancelRule(0, 2),
                });

            CharacterActionDiagnostic[] diagnostics = CharacterActionValidation.ValidateActionConfig(action);

            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.InvalidCancelWindow);
        }

        [Test]
        public void ValidationHelpers_AllowCancelWindowAcrossContinuousCancelablePhases()
        {
            CharacterActionConfig action = CreateTimelineValidationAction(
                cancelRules: new[]
                {
                    new CharacterCancelRule(6, 11),
                });

            CharacterActionDiagnostic[] diagnostics = CharacterActionValidation.ValidateActionConfig(action);

            AssertNoDiagnostic(diagnostics, CharacterActionDiagnosticCodes.InvalidCancelWindow);
        }

        [Test]
        public void ValidationHelpers_RejectCancelWindowAcrossStartupIntoActive()
        {
            CharacterActionConfig action = CreateTimelineValidationAction(
                cancelRules: new[]
                {
                    new CharacterCancelRule(2, 5),
                });

            CharacterActionDiagnostic[] diagnostics = CharacterActionValidation.ValidateActionConfig(action);

            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.InvalidCancelWindow);
        }

        [Test]
        public void ValidationHelpers_RejectCancelWindowAcrossPhaseGap()
        {
            CharacterActionConfig action = CreateTimelineValidationAction(
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 3),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 4, 6),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 8, 11),
                },
                cancelRules: new[]
                {
                    new CharacterCancelRule(5, 9),
                });

            CharacterActionDiagnostic[] diagnostics = CharacterActionValidation.ValidateActionConfig(action);

            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.InvalidCancelWindow);
        }

        [Test]
        public void ValidationHelpers_RejectCancelWindowOutsideDuration()
        {
            CharacterActionConfig action = CreateTimelineValidationAction(
                cancelRules: new[]
                {
                    new CharacterCancelRule(8, 12),
                });

            CharacterActionDiagnostic[] diagnostics = CharacterActionValidation.ValidateActionConfig(action);

            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.InvalidCancelWindow);
        }

        [Test]
        public void ValidationHelpers_RejectCombatTraceOutsideDurationOrActivePhase()
        {
            CharacterActionConfig action = CreateTimelineValidationAction(
                combatTrack: new CombatTrackConfig(1001, new[]
                {
                    new CombatTrackEvent(2, CharacterActionTrackEventKind.HitTraceWindow, traceProfileId: CharacterTestNumericIds.Field_trace_light),
                    new CombatTrackEvent(12, CharacterActionTrackEventKind.HitTraceWindow, traceProfileId: CharacterTestNumericIds.Field_trace_light),
                }));

            CharacterActionDiagnostic[] diagnostics = CharacterActionValidation.ValidateActionConfig(action);

            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.CombatTraceOutsideActivePhase);
            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.TrackEventOutsideDuration);
        }

        [Test]
        public void ValidationHelpers_IgnoreLegacyCancelTargetAndRejectMissingInterruptTarget()
        {
            CharacterActionConfig action = CreateTimelineValidationAction(
                cancelRules: new[]
                {
                    new CharacterCancelRule(4, 6),
                },
                interruptRules: new[]
                {
                    new CharacterInterruptRule(CharacterActionSourceKind.Command, targetActionId: new CharacterActionId(3003)),
                });

            CharacterActionDiagnostic[] diagnostics = CharacterActionValidation.ValidateActionSet(
                CreateActionSet(),
                new[] { action });

            AssertNoDiagnostic(diagnostics, CharacterActionDiagnosticCodes.CancelTargetMissing);
            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.InterruptTargetMissing);
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
            CharacterAbilityActionBinding[] abilityBindings = null,
            int defaultActionSlotMask = 0,
            CharacterActionCommandSequence[] commandSequences = null)
        {
            return new CharacterActionSetConfig(
                id: 1,
                stableId: "test.action_set",
                displayName: "Test Action Set",
                characterStableId: "test.character",
                equipmentStateStableId: "test.equipment",
                commandBindings: commandBindings ?? new[] { new CharacterActionBinding(CharacterTestNumericIds.IntentLightAttack, CharacterTestActionIds.Action_light_attack, allowQueue: true, queueWindowFrames: 8) },
                abilityBindings: abilityBindings ?? new[] { new CharacterAbilityActionBinding(9001, CharacterTestActionIds.Action_dash_strike) },
                reactionBindings: null,
                movementProfileId: CharacterTestNumericIds.Field_movement_default,
                reactionProfileId: "pressure-only",
                defaultActionId: CharacterTestActionIds.Action_idle,
                defaultActionSlotMask: defaultActionSlotMask,
                commandSequences: commandSequences);
        }

        private static CharacterActionResolverState ActiveState(CharacterActionId actionId)
        {
            return new CharacterActionResolverState(
                hasActiveAction: true,
                activeActionId: actionId,
                activeActionLocalFrame: 5,
                activeActionAuthority: CharacterActionTimelineAuthority.CharacterAuthored,
                activeCancelRules: new[] { new CharacterCancelRule(0, 23) });
        }

        private static CharacterActionIntentRequest CreateRequest(
            int intentId,
            int? abilityId = null,
            CharacterActionSourceKind sourceKind = CharacterActionSourceKind.Command,
            int priority = 10,
            RuntimeTraceIdentity traceId = default,
            CharacterActionId requestedActionId = default,
            int actionSlotId = 0)
        {
            return new CharacterActionIntentRequest(
                Entity(),
                intentId,
                abilityId,
                abilityStableId: abilityId.HasValue ? CharacterTestBulkIds.Id790009 : 0,
                requestedActionId: requestedActionId,
                sourceKind: sourceKind,
                priority: priority,
                frame: new RuntimeFrame(7),
                traceId: traceId,
                actionSlotId: actionSlotId);
        }

        private static CharacterActionConfig CreateAction(
            int id,
            string stableId,
            CharacterActionCategory category,
            string[] tags = null,
            int priority = 10,
            CharacterInterruptRule[] interruptRules = null)
        {
            return new CharacterActionConfig(
                new CharacterActionId(id),
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
                interruptRules: interruptRules,
                combatTrack: new CombatTrackConfig(1001, new[]
                {
                    new CombatTrackEvent(6, CharacterActionTrackEventKind.StartCombatAction, 1001),
                }),
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(0, CharacterActionTrackEventKind.EmitDebugMarker, stableId + ".start"),
                }));
        }

        private static CharacterActionConfig CreateCombatAnchoredAction(CharacterActionPhase[] phases)
        {
            return CreateCombatAnchoredAction(1001, phases);
        }

        private static CharacterActionConfig CreateCombatAnchoredAction(int combatActionId, CharacterActionPhase[] phases)
        {
            return new CharacterActionConfig(
                actionId: CharacterTestActionIds.Action_light_attack,
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
                actionId: new CharacterActionId(740001),
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
                combatTrack: new CombatTrackConfig(1001, null),
                animationTrack: animationTrack,
                presentationTrack: presentationTrack);
        }

        private static CharacterActionConfig CreateTimelineValidationAction(
            CharacterCancelRule[] cancelRules = null,
            CharacterInterruptRule[] interruptRules = null,
            CombatTrackConfig combatTrack = null,
            CharacterActionPhase[] phases = null,
            int durationFrames = 12)
        {
            return new CharacterActionConfig(
                actionId: new CharacterActionId(100),
                displayName: "Light Attack",
                category: CharacterActionCategory.BasicAttack,
                timelineAuthority: CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 10,
                durationFrames: durationFrames,
                requirements: null,
                phases: phases ?? new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 3),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 4, 7),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 8, 11),
                },
                cancelRules: cancelRules,
                interruptRules: interruptRules,
                combatTrack: combatTrack ?? new CombatTrackConfig(1001, null));
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

        private static CharacterReactionProfile CreateCohesionBreakProfile(CharacterActionId actionId)
        {
            return new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(
                        actionId,
                        CharacterReactionRuleTrigger.CohesionBreak,
                        currentScatterPressureBand: PressureBand.Broken),
                });
        }

        private static CharacterReactionContext CreateCohesionBreakContext()
        {
            return CharacterReactionContextBuilder.FromCohesionBreak(
                new CohesionBreakEvent(
                    new RuntimeFrame(3),
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 80,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100,
                    delta: 20)).Context;
        }

        private static CharacterActionPlan CreatePlan(CharacterActionConfig action)
        {
            return new CharacterActionPlan(
                action.ActionId.Value,
                action.ActionId,
                action.Category,
                action.Priority,
                action.DurationFrames.GetValueOrDefault(),
                action.Phases,
                CharacterActionTrackPlan.FromConfig(action),
                CharacterTestBulkIds.Id795001);
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

        private static void AssertHasRunnerEvent(CharacterActionRunnerEvent[] events, CharacterActionRunnerEventKind kind)
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Kind == kind)
                    return;
            }

            Assert.Fail("Expected runner event " + kind + ".");
        }

        private static void AssertHasRunnerEvent(CharacterActionRunnerEventBuffer events, CharacterActionRunnerEventKind kind)
        {
            int count = events == null ? 0 : events.Count;
            for (int i = 0; i < count; i++)
            {
                if (events[i].Kind == kind)
                    return;
            }

            Assert.Fail("Expected runner event " + kind + ".");
        }

        private static void AssertNoDiagnostic(CharacterActionDiagnostic[] diagnostics, string code)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Code == code)
                    Assert.Fail("Did not expect diagnostic code " + code + ".");
            }
        }
    }
}
