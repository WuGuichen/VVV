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
                CreateRequest("LightAttack", traceId: "trace.command"));
            CharacterActionResolveResult heavyCommand = resolver.ResolveCommand(
                context,
                CreateRequest("HeavyAttack", traceId: "trace.heavy"));
            CharacterActionResolveResult jumpCommand = resolver.ResolveCommand(
                context,
                CreateRequest("Jump", traceId: "trace.jump"));
            CharacterActionResolveResult ability = resolver.ResolveAbility(
                context,
                CreateRequest(
                    intentId: string.Empty,
                    abilityId: 9001,
                    sourceKind: CharacterActionSourceKind.GameplayAbility,
                    traceId: "trace.ability"));
            CharacterActionResolveResult reaction = resolver.ResolveReaction(
                context,
                CharacterReactionContextBuilder.FromPostureBreak(
                    new PostureBreakEvent(
                        new RuntimeFrame(3),
                        Entity(),
                        PressureBand.Critical,
                        previousValue: 80,
                        currentPressure: 100,
                        maxPressure: 100,
                        delta: 20,
                        traceId: "trace.reaction")).Context);
            CharacterActionResolveResult cancelRejected = resolver.ResolveAbility(
                fixture.CreateContext(
                    new CharacterActionResolverState(
                        hasActiveAction: true,
                        activeActionId: "light_attack",
                        activeActionLocalFrame: 9,
                        activeActionAuthority: CharacterActionTimelineAuthority.CombatAnchored,
                        activeCancelRules: new[]
                        {
                            new CharacterCancelRule(8, 11, fixture.DashStrike.Id, allow: false),
                        },
                        activeCombatTimeline: fixture.LightTimeline)),
                CreateRequest(
                    intentId: string.Empty,
                    abilityId: 9001,
                    sourceKind: CharacterActionSourceKind.GameplayAbility,
                    traceId: "trace.cancel"));

            CharacterActionDiagnostic[] invalidDiagnostics = CharacterActionValidation.ValidateActionConfig(CreateInvalidAction());
            CharacterActionResourceDependency[] dependencies = CharacterActionResourceDependencyCollector.Collect(fixture.LightAttack);
            string formatted = CharacterActionDiagnosticFormatter.Format(
                invalidDiagnostics[0],
                new CharacterActionDiagnosticFormatContext(
                    actionId: "invalid_action",
                    trackKind: CharacterActionTrackKind.Animation,
                    hasTrack: true,
                    frame: 0,
                    suggestedFix: "Assign animation action key."));

            Assert.IsTrue(command.IsSuccess);
            Assert.AreEqual("light_attack", command.Plan.ActionId);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionDurationResolvedFromCombat, command.Diagnostics[0].Code);
            Assert.IsTrue(heavyCommand.IsSuccess);
            Assert.AreEqual("heavy_attack", heavyCommand.Plan.ActionId);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionDurationResolvedFromCombat, heavyCommand.Diagnostics[0].Code);
            Assert.IsTrue(jumpCommand.IsSuccess);
            Assert.AreEqual("basic_jump", jumpCommand.Plan.ActionId);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionDurationResolvedFromConfig, jumpCommand.Diagnostics[0].Code);
            Assert.IsTrue(ability.IsSuccess);
            Assert.AreEqual("dash_strike", ability.Plan.ActionId);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionDurationResolvedFromConfig, ability.Diagnostics[0].Code);
            Assert.IsTrue(reaction.IsSuccess);
            Assert.AreEqual("posture_break_react", reaction.Plan.ActionId);
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
            AssertHasDependency(dependencies, CharacterActionResourceDependencyKind.DebugMarker, "light.start");
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
                    traceId: "trace.death").Context);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("death", result.Plan.ActionId);
            Assert.AreEqual(CharacterActionCategory.Reaction, result.Plan.Category);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.ReactionRuleMatched);
        }

        private static Phase2Fixture CreateFixture()
        {
            CharacterActionConfig light = CreateCombatAction(
                100,
                "light_attack",
                CharacterActionCategory.BasicAttack,
                "combat.light_attack",
                "trace.light_attack",
                "anim.light_attack",
                "sfx.light_attack",
                "vfx.light_slash",
                priority: 10,
                cancelRules: new[] { new CharacterCancelRule(8, 11, targetActionId: 200) });
            CharacterActionConfig heavy = CreateCombatAction(
                101,
                "heavy_attack",
                CharacterActionCategory.BasicAttack,
                "combat.heavy_attack",
                "trace.heavy_attack",
                "anim.heavy_attack",
                "sfx.heavy_attack",
                "vfx.heavy_slam",
                priority: 20);
            CharacterActionConfig dash = CreateCharacterAuthoredAction(
                200,
                "dash_strike",
                CharacterActionCategory.Skill,
                durationFrames: 18,
                animationKey: "anim.dash_strike",
                audioCue: "sfx.dash_strike",
                vfxKey: "vfx.dash_strike",
                tags: new[] { "weapon.sword" });
            CharacterActionConfig jump = CreateCharacterAuthoredAction(
                300,
                "basic_jump",
                CharacterActionCategory.Jump,
                durationFrames: 20,
                animationKey: "anim.basic_jump",
                audioCue: "sfx.jump",
                vfxKey: "vfx.jump");
            CharacterActionConfig lightHit = CreateReactionAction(400, "light_hit_react", 14, "anim.light_hit_react");
            CharacterActionConfig postureBreak = CreateReactionAction(401, "posture_break_react", 18, "anim.posture_break_react");
            CharacterActionConfig death = CreateReactionAction(402, "death", 30, "anim.death");
            CharacterActionConfig[] actions =
            {
                light,
                heavy,
                dash,
                jump,
                lightHit,
                postureBreak,
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
                    new CharacterActionBinding("LightAttack", "light_attack", priority: 10, allowQueue: true),
                    new CharacterActionBinding("HeavyAttack", "heavy_attack", priority: 10),
                    new CharacterActionBinding("Jump", "basic_jump", priority: 5),
                },
                abilityBindings: new[]
                {
                    new CharacterAbilityActionBinding(
                        9001,
                        "dash_strike",
                        requiredTags: new[] { "weapon.sword" }),
                },
                reactionBindings: null,
                movementProfileId: "movement.default",
                reactionProfileId: "pressure-only",
                defaultActionId: "light_hit_react");
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(
                        "posture_break_react",
                        CharacterReactionRuleTrigger.PostureBreak,
                        currentPressureBand: PressureBand.Broken,
                        priority: 10),
                    new CharacterReactionRule(
                        "death",
                        CharacterReactionRuleTrigger.Death,
                        isDeath: true,
                        priority: 100),
                },
                defaultActionId: "light_hit_react");
            return new Phase2Fixture(
                set,
                actions,
                profile,
                light,
                dash,
                CreateTimeline(1001),
                new[]
                {
                    new CharacterActionCombatTimelineBinding("combat.light_attack", CreateTimeline(1001)),
                    new CharacterActionCombatTimelineBinding("combat.heavy_attack", CreateTimeline(1002)),
                });
        }

        private static CharacterActionConfig CreateCombatAction(
            int id,
            string stableId,
            CharacterActionCategory category,
            string combatActionId,
            string traceProfileId,
            string animationKey,
            string audioCue,
            string vfxKey,
            int priority,
            CharacterCancelRule[] cancelRules = null)
        {
            return new CharacterActionConfig(
                id,
                stableId,
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
                    new CombatTrackEvent(4, CharacterActionTrackEventKind.StartHitTrace, traceProfileId: traceProfileId),
                    new CombatTrackEvent(7, CharacterActionTrackEventKind.StopHitTrace, traceProfileId: traceProfileId),
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
            string audioCue,
            string vfxKey,
            string[] tags = null)
        {
            return new CharacterActionConfig(
                id,
                stableId,
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
                    new MotionTrackEvent(0, CharacterActionTrackEventKind.LockMovement, CharacterMovementMode.ControlLocked, stableEventId: stableId + ".motion"),
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
                id,
                stableId,
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
                id: 999,
                stableId: "invalid_action",
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
            string intentId,
            int? abilityId = null,
            CharacterActionSourceKind sourceKind = CharacterActionSourceKind.Command,
            string traceId = "trace.phase2")
        {
            return new CharacterActionIntentRequest(
                Entity(),
                intentId,
                abilityId,
                abilityStableId: abilityId.HasValue ? "ability." + abilityId.Value : string.Empty,
                requestedActionId: string.Empty,
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
