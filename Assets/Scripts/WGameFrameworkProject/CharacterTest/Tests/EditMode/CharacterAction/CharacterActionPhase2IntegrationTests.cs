using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionPhase2IntegrationTests
    {
        [Test]
        public void Phase2Fixture_CommandAbilityReactionAndValidationMatrix_IsStable()
        {
            Phase2Fixture fixture = CreateFixture();
            var resolver = new CharacterActionResolver();
            CharacterActionResolverContext context = fixture.CreateContext();

            CharacterActionResolveResult command = resolver.ResolveCommand(
                context,
                CreateRequest(CharacterTestNumericIds.IntentLightAttack, traceId: CharacterTestRuntimeTraceIds.TraceCommand));
            CharacterActionResolveResult heavyCommand = resolver.ResolveCommand(
                context,
                CreateRequest(CharacterTestNumericIds.IntentHeavyAttack, traceId: CharacterTestRuntimeTraceIds.TraceHeavy));
            CharacterActionResolveResult jumpCommand = resolver.ResolveCommand(
                context,
                CreateRequest(CharacterTestNumericIds.IntentJump, traceId: CharacterTestRuntimeTraceIds.TraceJump));
            CharacterActionResolveResult ability = resolver.ResolveAbility(
                context,
                CreateRequest(
                    intentId: 0,
                    abilityId: 9001,
                    sourceKind: CharacterActionSourceKind.GameplayAbility,
                    traceId: CharacterTestRuntimeTraceIds.TraceAbility));
            CharacterActionResolveResult reaction = resolver.ResolveReaction(
                context,
                CharacterReactionContextBuilder.FromCohesionBreak(
                    new CohesionBreakEvent(
                        new RuntimeFrame(3),
                        Entity(),
                        PressureBand.Critical,
                        previousValue: 80,
                        currentScatterPressure: 100,
                        maxScatterPressure: 100,
                        delta: 20,
                        traceId: CharacterTestRuntimeTraceIds.TraceReaction)).Context);
            CharacterActionResolveResult cancelRejected = resolver.ResolveAbility(
                fixture.CreateContext(
                    new CharacterActionResolverState(
                        hasActiveAction: true,
                        activeActionId: new MxFramework.CharacterAction.CharacterActionId(301),
                        activeActionLocalFrame: 9,
                        activeActionAuthority: CharacterActionTimelineAuthority.CombatAnchored,
                        activeCancelRules: new[]
                        {
                            new CharacterCancelRule(8, 11, CharacterActionRequestType.BasicAttack),
                        },
                        activeCombatTimeline: fixture.LightTimeline)),
                CreateRequest(
                    intentId: 0,
                    abilityId: 9001,
                    sourceKind: CharacterActionSourceKind.GameplayAbility,
                    traceId: CharacterTestRuntimeTraceIds.TraceCancel));

            CharacterActionDiagnostic[] invalidDiagnostics = CharacterActionValidation.ValidateActionConfig(CreateInvalidAction());
            CharacterActionResourceDependency[] dependencies = CharacterActionResourceDependencyCollector.Collect(fixture.LightAttack);
            string formatted = CharacterActionDiagnosticFormatter.Format(
                invalidDiagnostics[0],
                new CharacterActionDiagnosticFormatContext(
                    actionId: CharacterTestActionIds.Action_invalid_action,
                    trackKind: CharacterActionTrackKind.Animation,
                    hasTrack: true,
                    frame: 0,
                    suggestedFix: "Assign animation action key."));

            Assert.IsTrue(command.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_light_attack, command.Plan.ActionId);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionDurationResolvedFromCombat, command.Diagnostics[0].Code);
            Assert.IsTrue(heavyCommand.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_heavy_attack, heavyCommand.Plan.ActionId);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionDurationResolvedFromCombat, heavyCommand.Diagnostics[0].Code);
            Assert.IsTrue(jumpCommand.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_basic_jump, jumpCommand.Plan.ActionId);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionDurationResolvedFromConfig, jumpCommand.Diagnostics[0].Code);
            Assert.IsTrue(ability.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_dash_strike, ability.Plan.ActionId);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionDurationResolvedFromConfig, ability.Diagnostics[0].Code);
            Assert.IsTrue(reaction.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_cohesion_break_react, reaction.Plan.ActionId);
            AssertHasDiagnostic(reaction.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleMatched);
            Assert.IsTrue(cancelRejected.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.CancelConflict, cancelRejected.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCancelRejected, cancelRejected.Diagnostics[0].Code);
            AssertHasDiagnostic(invalidDiagnostics, CharacterActionDiagnosticCodes.AnimationActionMissing);
            AssertHasDependency(dependencies, CharacterActionResourceDependencyKind.CombatAction, "combat.light_attack");
            AssertHasDependency(dependencies, CharacterActionResourceDependencyKind.TraceProfile, "trace.light_attack");
            AssertHasDependency(dependencies, CharacterActionResourceDependencyKind.AnimationAction, "anim.light_attack");
            AssertHasDependency(dependencies, CharacterActionResourceDependencyKind.AudioCue, "sfx.light_attack");
            AssertHasDependency(dependencies, CharacterActionResourceDependencyKind.VfxResource, "vfx.light_slash");
            AssertHasDependency(dependencies, CharacterActionResourceDependencyKind.DebugMarker, "light_attack.start");
            Assert.AreEqual(
                "code=ACT_ANIMATION_ACTION_MISSING severity=Error action=invalid_action phase=- track=Animation frame=0 message=Animation track event requires an animation action key. suggestedFix=Assign animation action key.",
                formatted);
        }

        [Test]
        public void Phase2Fixture_DeathPressureOnlyReaction_ResolvesDeathPlan()
        {
            Phase2Fixture fixture = CreateFixture();
            var resolver = new CharacterActionResolver();
            CharacterActionResolveResult result = resolver.ResolveReaction(
                fixture.CreateContext(),
                CharacterReactionContextBuilder.FromDeath(
                    new RuntimeFrame(7),
                    Entity(),
                    reason: "hp_zero",
                    traceId: CharacterTestRuntimeTraceIds.TraceDeath).Context);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(CharacterTestActionIds.Action_Death, result.Plan.ActionId);
            Assert.AreEqual(CharacterActionCategory.Reaction, result.Plan.Category);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleMatched);
        }

        private static Phase2Fixture CreateFixture()
        {
            CharacterActionConfig light = CreateCombatAction(
                100,
                "light_attack",
                CharacterActionCategory.BasicAttack,
                CharacterTestNumericIds.Field_combat_light_attack,
                CharacterTestNumericIds.Field_trace_light,
                "anim.light_attack",
                CharacterTestNumericIds.Field_sfx_light_attack,
                "vfx.light_slash",
                priority: 10,
                cancelRules: new[] { new CharacterCancelRule(8, 11) });
            CharacterActionConfig heavy = CreateCombatAction(
                101,
                "heavy_attack",
                CharacterActionCategory.BasicAttack,
                CharacterTestNumericIds.Field_combat_heavy_attack,
                760102,
                "anim.heavy_attack",
                760103,
                "vfx.heavy_slam",
                priority: 20);
            CharacterActionConfig dash = CreateCharacterAuthoredAction(
                200,
                "dash_strike",
                CharacterActionCategory.Skill,
                durationFrames: 18,
                animationKey: "anim.dash_strike",
                 audioCue: CharacterTestNumericIds.Field_audio_dash_strike,
                vfxKey: "vfx.dash_strike",
                tags: new[] { "weapon.sword" });
            CharacterActionConfig jump = CreateCharacterAuthoredAction(
                300,
                "basic_jump",
                CharacterActionCategory.Jump,
                durationFrames: 20,
                animationKey: "anim.basic_jump",
                 audioCue: CharacterTestNumericIds.Field_audio_jump,
                vfxKey: "vfx.jump");
            CharacterActionConfig lightHit = CreateReactionAction(400, "light_hit_react", 14, "anim.light_hit_react");
            CharacterActionConfig cohesionBreak = CreateReactionAction(401, "cohesion_break_react", 18, "anim.cohesion_break_react");
            CharacterActionConfig death = CreateReactionAction(402, "death", 30, "anim.death");
            CharacterActionConfig[] actions =
            {
                light,
                heavy,
                dash,
                jump,
                lightHit,
                cohesionBreak,
                death,
            };
            var set = new CharacterActionSetConfig(
                id: 1,
                stableId: "phase2.action_set",
                displayName: "Phase 2 Action Set",
                characterStableId: "phase2.character",
                equipmentStateStableId: "phase2.sword",
                commandBindings: new[]
                {
                    new CharacterActionBinding(CharacterTestNumericIds.IntentLightAttack, new MxFramework.CharacterAction.CharacterActionId(301), priority: 10, allowQueue: true),
                    new CharacterActionBinding(CharacterTestNumericIds.IntentHeavyAttack, CharacterTestActionIds.Action_heavy_attack, priority: 10),
                    new CharacterActionBinding(CharacterTestNumericIds.IntentJump, new MxFramework.CharacterAction.CharacterActionId(300), priority: 5),
                },
                abilityBindings: new[]
                {
                    new CharacterAbilityActionBinding(
                        9001,
                        CharacterTestActionIds.Action_dash_strike,
                        requiredTags: new[] { "weapon.sword" }),
                },
                reactionBindings: null,
                movementProfileId: CharacterTestNumericIds.Field_movement_default,
                reactionProfileId: "pressure-only",
                defaultActionId: CharacterTestActionIds.Action_light_hit_react);
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_cohesion_break_react,
                        CharacterReactionRuleTrigger.CohesionBreak,
                        currentScatterPressureBand: PressureBand.Broken,
                        priority: 10),
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_Death,
                        CharacterReactionRuleTrigger.Death,
                        isDeath: true,
                        priority: 100),
                },
                defaultActionId: CharacterTestActionIds.Action_light_hit_react);
            return new Phase2Fixture(
                set,
                actions,
                profile,
                light,
                dash,
                CreateTimeline(1001),
                new[]
                {
                    new CharacterActionCombatTimelineBinding(CharacterTestNumericIds.Field_combat_light_attack, CreateTimeline(1001)),
                    new CharacterActionCombatTimelineBinding(CharacterTestNumericIds.Field_combat_heavy_attack, CreateTimeline(1002)),
                });
        }

        private static CharacterActionConfig CreateCombatAction(
            int id,
            string stableId,
            CharacterActionCategory category,
            int combatActionId,
            int traceProfileId,
            string animationKey,
            int audioCue,
            string vfxKey,
            int priority,
            CharacterCancelRule[] cancelRules = null)
        {
            return new CharacterActionConfig(
                new CharacterActionId(id),
                stableId,
                category,
                CharacterActionTimelineAuthority.CombatAnchored,
                tags: new[] { "weapon.sword" },
                priority: priority,
                durationFrames: null,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 3, CombatActionPhase.Startup),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 4, 7, CombatActionPhase.Active),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 8, 11, CombatActionPhase.Recovery),
                },
                cancelRules: cancelRules,
                interruptRules: null,
                combatTrack: new CombatTrackConfig(combatActionId, new[]
                {
                    new CombatTrackEvent(4, CharacterActionTrackEventKind.StartCombatAction, combatActionId),
                    new CombatTrackEvent(4, CharacterActionTrackEventKind.HitTraceWindow, traceProfileId: traceProfileId),
                    new CombatTrackEvent(7, CharacterActionTrackEventKind.HitTraceWindow, traceProfileId: traceProfileId),
                }),
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.CrossFadeAnimation, animationKey),
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(4, CharacterActionTrackEventKind.PlayAudioCue, audioCue),
                    new PresentationTrackEvent(5, CharacterActionTrackEventKind.SpawnVisualCue, resourceKey: vfxKey),
                }),
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(0, CharacterActionTrackEventKind.EmitDebugMarker, stableId + ".start"),
                }));
        }

        private static CharacterActionConfig CreateCharacterAuthoredAction(
            int id,
            string stableId,
            CharacterActionCategory category,
            int durationFrames,
            string animationKey,
            int audioCue,
            string vfxKey,
            string[] tags = null)
        {
            return new CharacterActionConfig(
                new CharacterActionId(id),
                stableId,
                category,
                CharacterActionTimelineAuthority.CharacterAuthored,
                tags,
                priority: 10,
                durationFrames: durationFrames,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 3),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 4, durationFrames - 5),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, durationFrames - 4, durationFrames - 1),
                },
                cancelRules: null,
                interruptRules: null,
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(0, CharacterActionTrackEventKind.LockMovement, CharacterMovementMode.ControlLocked, stableEventId: CharacterTestNumericIds.Field_motion_control),
                }),
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.CrossFadeAnimation, animationKey),
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(2, CharacterActionTrackEventKind.PlayAudioCue, audioCue),
                    new PresentationTrackEvent(3, CharacterActionTrackEventKind.SpawnVisualCue, resourceKey: vfxKey),
                }),
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(0, CharacterActionTrackEventKind.EmitDebugMarker, stableId + ".start"),
                }));
        }

        private static CharacterActionConfig CreateReactionAction(int id, string stableId, int durationFrames, string animationKey)
        {
            return new CharacterActionConfig(
                new CharacterActionId(id),
                stableId,
                CharacterActionCategory.Reaction,
                CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 100,
                durationFrames: durationFrames,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 2),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 3, durationFrames - 4),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, durationFrames - 3, durationFrames - 1),
                },
                cancelRules: null,
                interruptRules: null,
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.CrossFadeAnimation, animationKey),
                }));
        }

        private static CharacterActionConfig CreateInvalidAction()
        {
            return new CharacterActionConfig(
                actionId: new CharacterActionId(999),
                displayName: "Invalid Action",
                category: CharacterActionCategory.Skill,
                timelineAuthority: CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 1,
                durationFrames: 8,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 1),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 2, 5),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 6, 7),
                },
                cancelRules: null,
                interruptRules: null,
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.PlayAnimation),
                }));
        }

        private static CharacterActionIntentRequest CreateRequest(
            int intentId,
            int? abilityId = null,
            CharacterActionSourceKind sourceKind = CharacterActionSourceKind.Command,
            RuntimeTraceIdentity traceId = default)
        {
            return new CharacterActionIntentRequest(
                Entity(),
                intentId,
                abilityId,
                abilityStableId: abilityId.HasValue ? CharacterTestBulkIds.Id790009 : 0,
                requestedActionId: MxFramework.CharacterAction.CharacterActionId.None,
                sourceKind: sourceKind,
                priority: 10,
                frame: new RuntimeFrame(5),
                traceId: traceId);
        }

        private static CombatActionTimeline CreateTimeline(int actionId)
        {
            return new CombatActionTimeline(
                actionId,
                totalFrames: 12,
                startup: new CombatFrameRange(0, 3),
                active: new CombatFrameRange(4, 7),
                recovery: new CombatFrameRange(8, 11),
                windows: null,
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

        private static void AssertHasDependency(
            CharacterActionResourceDependency[] dependencies,
            CharacterActionResourceDependencyKind kind,
            string stableId)
        {
            for (int i = 0; i < dependencies.Length; i++)
            {
                if (dependencies[i].Kind == kind && dependencies[i].StableId == stableId)
                    return;
            }

            Assert.Fail("Expected dependency " + kind + " " + stableId + ".");
        }

        private sealed class Phase2Fixture
        {
            public Phase2Fixture(
                CharacterActionSetConfig actionSet,
                CharacterActionConfig[] actions,
                CharacterReactionProfile reactionProfile,
                CharacterActionConfig lightAttack,
                CharacterActionConfig dashStrike,
                CombatActionTimeline lightTimeline,
                CharacterActionCombatTimelineBinding[] combatBindings)
            {
                ActionSet = actionSet;
                Actions = actions;
                ReactionProfile = reactionProfile;
                LightAttack = lightAttack;
                DashStrike = dashStrike;
                LightTimeline = lightTimeline;
                CombatBindings = combatBindings;
            }

            public CharacterActionSetConfig ActionSet { get; }
            public CharacterActionConfig[] Actions { get; }
            public CharacterReactionProfile ReactionProfile { get; }
            public CharacterActionConfig LightAttack { get; }
            public CharacterActionConfig DashStrike { get; }
            public CombatActionTimeline LightTimeline { get; }
            public CharacterActionCombatTimelineBinding[] CombatBindings { get; }

            public CharacterActionResolverContext CreateContext(CharacterActionResolverState state = default)
            {
                return new CharacterActionResolverContext(
                    ActionSet,
                    Actions,
                    reactionProfiles: new[] { ReactionProfile },
                    state: state,
                    durationPolicy: new CharacterActionDurationPolicy(24),
                    combatTimelineBindings: CombatBindings,
                    contextTags: new[] { "weapon.sword" });
            }
        }
    }
}
