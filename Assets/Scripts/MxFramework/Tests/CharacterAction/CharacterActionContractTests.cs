using System;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionContractTests
    {
        [Test]
        public void MvpContractTypes_CanBeInstantiatedWithoutUnityObjects()
        {
            var movement = new CharacterMovementProfileConfig(
                stableId: "humanoid.default",
                defaultMode: CharacterMovementMode.Walk,
                walkSpeed: 2.5f,
                runSpeed: 5f,
                acceleration: 20f,
                deceleration: 24f,
                turnSpeed: 540f,
                groundFriction: 8f,
                airControl: 0.35f,
                gravity: -30f,
                jumpImpulse: 8f,
                slopeLimitDegrees: 45f,
                locomotionBlendId: "humanoid.locomotion");
            var action = new CharacterActionConfig(
                id: 100,
                stableId: "light_attack",
                displayName: "Light Attack",
                category: CharacterActionCategory.BasicAttack,
                timelineAuthority: CharacterActionTimelineAuthority.CharacterAuthored,
                tags: new[] { "attack", "melee" },
                priority: 10,
                durationFrames: 24,
                requirements: new[] { new CharacterActionRequirement(CharacterActionRequirementKind.Grounded) },
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 5),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 6, 10),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 11, 23),
                },
                cancelRules: new[] { new CharacterCancelRule(16, 23, sourceKind: CharacterActionSourceKind.Command) },
                interruptRules: new[] { new CharacterInterruptRule(CharacterActionSourceKind.PostureBreak, minimumPriority: 100) },
                motionTrack: new MotionTrackConfig(usesRootMotion: false, events: new[]
                {
                    new MotionTrackEvent(0, CharacterActionTrackEventKind.LockMovement, CharacterMovementMode.ControlLocked),
                }),
                combatTrack: new CombatTrackConfig("combat.light_attack", new[]
                {
                    new CombatTrackEvent(6, CharacterActionTrackEventKind.StartCombatAction, "combat.light_attack"),
                }),
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(6, CharacterActionTrackEventKind.SendGameplayRequest, "posture.light"),
                }),
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.CrossFadeAnimation, "anim.light_attack", 0.08f),
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(6, CharacterActionTrackEventKind.PlayAudioCue, "sfx.light_attack"),
                }),
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(0, CharacterActionTrackEventKind.EmitDebugMarker, "light_attack.start"),
                }));
            var set = new CharacterActionSetConfig(
                id: 1,
                stableId: "hero.base",
                displayName: "Hero Base",
                characterStableId: "hero",
                equipmentStateStableId: "sword",
                commandBindings: new[] { new CharacterActionBinding("LightAttack", "light_attack", priority: 10, allowQueue: true, queueWindowFrames: 8) },
                abilityBindings: new[] { new CharacterAbilityActionBinding(200, "light_attack", requiredTags: new[] { "sword" }) },
                reactionBindings: new[] { new CharacterReactionBinding("pressure-only", "LightHitReact") },
                movementProfileId: movement.StableId,
                reactionProfileId: "pressure-only",
                defaultActionId: "idle");

            Assert.AreEqual("humanoid.default", movement.StableId);
            Assert.AreEqual(CharacterActionCategory.BasicAttack, action.Category);
            Assert.AreEqual(CharacterActionTimelineAuthority.CharacterAuthored, action.TimelineAuthority);
            Assert.IsFalse(action.MotionTrack.UsesRootMotion);
            Assert.AreEqual(1, action.MotionTrack.Events.Length);
            Assert.AreEqual(1, action.CombatTrack.Events.Length);
            Assert.AreEqual(1, action.GameplayTrack.Events.Length);
            Assert.AreEqual(1, action.AnimationTrack.Events.Length);
            Assert.AreEqual(1, action.PresentationTrack.Events.Length);
            Assert.AreEqual(1, action.DebugTrack.Events.Length);
            Assert.AreEqual("LightAttack", set.CommandBindings[0].IntentId);
        }

        [Test]
        public void TrackEventKind_IsStableEnumNotFreeString()
        {
            var motion = new MotionTrackEvent(0, CharacterActionTrackEventKind.ApplyImpulse, CharacterMovementMode.Airborne, y: 8f);
            var combat = new CombatTrackEvent(3, CharacterActionTrackEventKind.StartHitTrace, traceProfileId: "sword.short");
            var gameplay = new GameplayTrackEvent(4, CharacterActionTrackEventKind.CastAbility, abilityStableId: "ability.jump");
            var animation = new AnimationTrackEvent(0, CharacterActionTrackEventKind.PlayAnimation, "jump");
            var presentation = new PresentationTrackEvent(2, CharacterActionTrackEventKind.SpawnVisualCue, resourceKey: "vfx.jump");
            var debug = new DebugTrackEvent(0, CharacterActionTrackEventKind.EmitDebugMarker, "jump.start");

            Assert.IsInstanceOf<CharacterActionTrackEventKind>(motion.Kind);
            Assert.IsInstanceOf<CharacterActionTrackEventKind>(combat.Kind);
            Assert.IsInstanceOf<CharacterActionTrackEventKind>(gameplay.Kind);
            Assert.IsInstanceOf<CharacterActionTrackEventKind>(animation.Kind);
            Assert.IsInstanceOf<CharacterActionTrackEventKind>(presentation.Kind);
            Assert.IsInstanceOf<CharacterActionTrackEventKind>(debug.Kind);
            Assert.AreEqual(CharacterActionTrackEventKind.ApplyImpulse, motion.Kind);
            Assert.AreEqual("ability.jump", gameplay.AbilityStableId);
        }

        [Test]
        public void TrackEventKind_MustBelongToTrack()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MotionTrackEvent(0, CharacterActionTrackEventKind.PlayAnimation));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PresentationTrackEvent(0, CharacterActionTrackEventKind.StartHitTrace));
        }

        [Test]
        public void IntentRequest_CanBeConstructedWithStableIntentAndAbilityContext()
        {
            var request = new CharacterActionIntentRequest(
                entity: Entity(),
                intentId: "LightAttack",
                abilityId: 200,
                abilityStableId: "ability.light",
                requestedActionId: "light_attack",
                sourceKind: CharacterActionSourceKind.LocalInput,
                priority: 10,
                frame: new RuntimeFrame(42),
                traceId: "trace-403");

            Assert.AreEqual(Entity(), request.Entity);
            Assert.AreEqual("LightAttack", request.IntentId);
            Assert.AreEqual(200, request.AbilityId);
            Assert.AreEqual("ability.light", request.AbilityStableId);
            Assert.AreEqual("light_attack", request.RequestedActionId);
            Assert.AreEqual(CharacterActionSourceKind.LocalInput, request.SourceKind);
            Assert.AreEqual(10, request.Priority);
            Assert.AreEqual(new RuntimeFrame(42), request.Frame);
            Assert.AreEqual("trace-403", request.TraceId);
        }

        [Test]
        public void IntentRequest_SourceKindsCoverCommandAiReplayDebugInterventionAndReaction()
        {
            var sourceKinds = new[]
            {
                CharacterActionSourceKind.LocalInput,
                CharacterActionSourceKind.RuntimeAiPlanner,
                CharacterActionSourceKind.RuntimeAI,
                CharacterActionSourceKind.Replay,
                CharacterActionSourceKind.Scripted,
                CharacterActionSourceKind.Debug,
                CharacterActionSourceKind.PlayerIntervention,
                CharacterActionSourceKind.Reaction,
            };

            for (int i = 0; i < sourceKinds.Length; i++)
            {
                var request = new CharacterActionIntentRequest(
                    entity: Entity(),
                    intentId: "Intent",
                    abilityId: null,
                    abilityStableId: string.Empty,
                    requestedActionId: string.Empty,
                    sourceKind: sourceKinds[i],
                    priority: i,
                    frame: new RuntimeFrame(i),
                    traceId: "trace-source");

                Assert.AreEqual(sourceKinds[i], request.SourceKind);
            }

            var commandRule = new CharacterCancelRule(
                startFrame: 0,
                endFrame: 10,
                sourceKind: CharacterActionSourceKind.Command);

            Assert.IsTrue(commandRule.Matches(5, targetActionId: 0, CharacterActionSourceKind.LocalInput));
            Assert.IsTrue(commandRule.Matches(5, targetActionId: 0, CharacterActionSourceKind.RuntimeAiPlanner));
            Assert.IsTrue(commandRule.Matches(5, targetActionId: 0, CharacterActionSourceKind.RuntimeAI));
            Assert.IsTrue(commandRule.Matches(5, targetActionId: 0, CharacterActionSourceKind.Replay));
            Assert.IsTrue(commandRule.Matches(5, targetActionId: 0, CharacterActionSourceKind.Scripted));
            Assert.IsTrue(commandRule.Matches(5, targetActionId: 0, CharacterActionSourceKind.Debug));
            Assert.IsTrue(commandRule.Matches(5, targetActionId: 0, CharacterActionSourceKind.PlayerIntervention));
            Assert.IsFalse(commandRule.Matches(5, targetActionId: 0, CharacterActionSourceKind.Reaction));
        }

        [Test]
        public void ResolveResult_ExposesSuccessQueuedAndRejectedStates()
        {
            CharacterActionPlan plan = CreatePlan(durationFrames: 24);

            CharacterActionResolveResult success = CharacterActionResolveResult.Success(plan);
            CharacterActionResolveResult queued = CharacterActionResolveResult.Queued(plan);
            CharacterActionResolveResult rejected = CharacterActionResolveResult.Rejected(
                CharacterActionRejectReason.MissingActionBinding,
                new[]
                {
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.MissingActionBinding,
                        "No binding for intent LightAttack."),
                },
                traceId: "trace-reject");

            Assert.IsTrue(success.IsSuccess);
            Assert.AreEqual(CharacterActionResolveStatus.Success, success.Status);
            Assert.AreSame(plan, success.Plan);
            Assert.IsTrue(queued.IsQueued);
            Assert.AreEqual(CharacterActionResolveStatus.Queued, queued.Status);
            Assert.IsTrue(rejected.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.MissingActionBinding, rejected.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionBinding, rejected.Diagnostics[0].Code);
            Assert.AreEqual("trace-reject", rejected.TraceId);
        }

        [Test]
        public void CharacterAuthoredDuration_UsesConfigDurationOrExplicitFallbackPolicy()
        {
            CharacterActionConfig explicitDuration = CreateActionConfig(
                CharacterActionTimelineAuthority.CharacterAuthored,
                durationFrames: 18);
            CharacterActionConfig fallbackDuration = CreateActionConfig(
                CharacterActionTimelineAuthority.CharacterAuthored,
                durationFrames: null);

            CharacterActionPlanDurationResult fromConfig = CharacterActionPlanDurationResolver.Resolve(explicitDuration);
            CharacterActionPlanDurationResult fromFallback = CharacterActionPlanDurationResolver.Resolve(
                fallbackDuration,
                policy: new CharacterActionDurationPolicy(12));
            CharacterActionPlanDurationResult unresolved = CharacterActionPlanDurationResolver.Resolve(fallbackDuration);

            Assert.IsTrue(fromConfig.Resolved);
            Assert.AreEqual(18, fromConfig.DurationFrames);
            Assert.AreEqual(CharacterActionPlanDurationSource.CharacterActionConfig, fromConfig.Source);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionDurationResolvedFromConfig, fromConfig.Diagnostics[0].Code);
            Assert.IsTrue(fromFallback.Resolved);
            Assert.AreEqual(12, fromFallback.DurationFrames);
            Assert.AreEqual(CharacterActionPlanDurationSource.FallbackPolicy, fromFallback.Source);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionDurationFallbackUsed, fromFallback.Diagnostics[0].Code);
            Assert.IsFalse(unresolved.Resolved);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionDurationMissing, unresolved.Diagnostics[0].Code);
        }

        [Test]
        public void CombatAnchoredDuration_UsesCombatTimelineTotalFrames()
        {
            CharacterActionConfig config = CreateActionConfig(
                CharacterActionTimelineAuthority.CombatAnchored,
                durationFrames: 99);
            CombatActionTimeline timeline = new CombatActionTimeline(
                actionId: 1001,
                totalFrames: 31,
                startup: new CombatFrameRange(0, 5),
                active: new CombatFrameRange(6, 12),
                recovery: new CombatFrameRange(13, 30),
                windows: null,
                events: null);

            CharacterActionPlanDurationResult result = CharacterActionPlanDurationResolver.Resolve(config, timeline);

            Assert.IsTrue(result.Resolved);
            Assert.AreEqual(31, result.DurationFrames);
            Assert.AreEqual(CharacterActionPlanDurationSource.CombatActionTimeline, result.Source);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionDurationResolvedFromCombat, result.Diagnostics[0].Code);
        }

        [Test]
        public void ResolveDiagnostics_CanCarryRejectBindingResourcePhaseAndCancelCodes()
        {
            CharacterActionResolveResult result = CharacterActionResolveResult.Rejected(
                CharacterActionRejectReason.ResourceMissing,
                new[]
                {
                    CharacterActionDiagnostic.Error(CharacterActionDiagnosticCodes.MissingActionBinding, "Binding missing."),
                    CharacterActionDiagnostic.Error(CharacterActionDiagnosticCodes.ResourceMissing, "Resource missing."),
                    CharacterActionDiagnostic.Error(CharacterActionDiagnosticCodes.ReactionContextIncomplete, "Reaction context incomplete."),
                    CharacterActionDiagnostic.Error(CharacterActionDiagnosticCodes.PhaseCombatAnchorMissing, "Phase anchor missing."),
                    CharacterActionDiagnostic.Error(CharacterActionDiagnosticCodes.CharacterCombatCancelConflict, "Cancel conflict."),
                });

            Assert.AreEqual(CharacterActionRejectReason.ResourceMissing, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionBinding, result.Diagnostics[0].Code);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ResourceMissing, result.Diagnostics[1].Code);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ReactionContextIncomplete, result.Diagnostics[2].Code);
            Assert.AreEqual(CharacterActionDiagnosticCodes.PhaseCombatAnchorMissing, result.Diagnostics[3].Code);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCombatCancelConflict, result.Diagnostics[4].Code);
        }

        [Test]
        public void Plan_CarriesResolvedDurationPhasesTrackSummariesAndTrace()
        {
            CharacterActionConfig config = CreateActionConfig(
                CharacterActionTimelineAuthority.CharacterAuthored,
                durationFrames: 24);
            CharacterActionTrackPlan[] tracks = CharacterActionTrackPlan.FromConfig(config);
            CharacterActionPlan plan = new CharacterActionPlan(
                planId: 403,
                actionId: config.StableId,
                category: config.Category,
                priority: config.Priority,
                durationFrames: config.DurationFrames.Value,
                phases: config.Phases,
                tracks: tracks,
                traceId: "trace-plan");

            Assert.AreEqual(403, plan.PlanId);
            Assert.AreEqual("light_attack", plan.ActionId);
            Assert.AreEqual(CharacterActionCategory.BasicAttack, plan.Category);
            Assert.AreEqual(24, plan.DurationFrames);
            Assert.AreEqual(3, plan.Phases.Length);
            Assert.AreEqual(6, plan.Tracks.Length);
            Assert.AreEqual(CharacterActionTrackKind.Combat, plan.Tracks[1].Kind);
            Assert.AreEqual("combat.light_attack", plan.Tracks[1].ConfigReferenceId);
            Assert.AreEqual("trace-plan", plan.TraceId);
        }

        [Test]
        public void PressureOnlyReactionProfile_MapsLightHitPostureBreakAndDeath()
        {
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule("Death", CharacterReactionRuleTrigger.Death, isDeath: true),
                    new CharacterReactionRule("AirborneRecovery", CharacterReactionRuleTrigger.Any, isAirborne: true, currentPhase: CharacterActionPhaseKind.Airborne),
                    new CharacterReactionRule("CommittedFlinch", CharacterReactionRuleTrigger.Any, currentActionCommitted: true, currentActionInterruptible: false),
                    new CharacterReactionRule("PostureBreakReact", CharacterReactionRuleTrigger.PostureBreak, currentPressureBand: PressureBand.Broken),
                    new CharacterReactionRule("LightHitReact", CharacterReactionRuleTrigger.PressureBandChanged, currentPressureBand: PressureBand.Pressed),
                });
            CharacterReactionContext lightHit = CharacterReactionContextBuilder.FromPressureBandChanged(
                new PressureBandChangedEvent(
                    RuntimeFrame.Zero,
                    Entity(),
                    PressureBand.Stable,
                    PressureBand.Pressed,
                    previousValue: 0,
                    newValue: 25,
                    delta: 25,
                    sourceId: 8)).Context;
            CharacterReactionContext postureBreak = CharacterReactionContextBuilder.FromPostureBreak(
                new PostureBreakEvent(
                    new RuntimeFrame(1),
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 90,
                    currentPressure: 100,
                    maxPressure: 100,
                    delta: 10)).Context;
            CharacterReactionContext death = CharacterReactionContextBuilder.FromDeath(new RuntimeFrame(2), Entity()).Context;
            var airborne = new CharacterReactionContext(
                CharacterReactionContextSourceKind.PressureBandChanged,
                CharacterReactionContextCompleteness.PressureOnly,
                new RuntimeFrame(3),
                Entity(),
                PressureBand.Stable,
                PressureBand.Stable,
                previousPressure: 0,
                currentPressure: 0,
                maxPressure: 0,
                delta: 0,
                sourceId: 0,
                reason: string.Empty,
                traceId: string.Empty,
                isDeath: false,
                lifecycleState: string.Empty,
                bodyPartId: string.Empty,
                hitZoneId: string.Empty,
                damageTypeId: string.Empty,
                hitDirection: CharacterHitDirection.Unknown,
                reactionGroupId: string.Empty,
                isAirborne: true,
                currentCharacterPhase: CharacterActionPhaseKind.Airborne);
            var committed = new CharacterReactionContext(
                CharacterReactionContextSourceKind.PressureBandChanged,
                CharacterReactionContextCompleteness.PressureOnly,
                new RuntimeFrame(4),
                Entity(),
                PressureBand.Stable,
                PressureBand.Stable,
                previousPressure: 0,
                currentPressure: 0,
                maxPressure: 0,
                delta: 0,
                sourceId: 0,
                reason: string.Empty,
                traceId: string.Empty,
                isDeath: false,
                lifecycleState: string.Empty,
                bodyPartId: string.Empty,
                hitZoneId: string.Empty,
                damageTypeId: string.Empty,
                hitDirection: CharacterHitDirection.Unknown,
                reactionGroupId: string.Empty,
                currentActionCommitted: true,
                currentActionInterruptible: false);

            Assert.AreEqual("LightHitReact", CharacterReactionSelector.Select(profile, lightHit).SelectedActionId);
            Assert.AreEqual("PostureBreakReact", CharacterReactionSelector.Select(profile, postureBreak).SelectedActionId);
            Assert.AreEqual("Death", CharacterReactionSelector.Select(profile, death).SelectedActionId);
            Assert.AreEqual("AirborneRecovery", CharacterReactionSelector.Select(profile, airborne).SelectedActionId);
            Assert.AreEqual("CommittedFlinch", CharacterReactionSelector.Select(profile, committed).SelectedActionId);
            Assert.AreEqual(0, CharacterReactionRuleValidator.ValidatePressureOnlyProfile(profile).Length);
        }

        [Test]
        public void PressureOnlyProfileValidator_BlocksHitAndBodyPartDimensionsWithStableDiagnostic()
        {
            var profile = new CharacterReactionProfile(
                "invalid-pressure-only",
                new[]
                {
                    new CharacterReactionRule(
                        "DirectionalHitReact",
                        CharacterReactionRuleTrigger.PressureBandChanged,
                        requiresBodyPart: true,
                        requiresHitZone: true,
                        requiresDamageType: true,
                        requiresHitDirection: true,
                        requiresReactionGroup: true),
                });

            CharacterActionDiagnostic[] diagnostics = CharacterReactionRuleValidator.ValidatePressureOnlyProfile(profile);

            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ReactionRuleRequiresHitContext, diagnostics[0].Code);
        }

        private static GameplayEntityId Entity()
        {
            return new GameplayEntityId(1, 1);
        }

        private static CharacterActionPlan CreatePlan(int durationFrames)
        {
            return new CharacterActionPlan(
                planId: 1,
                actionId: "light_attack",
                category: CharacterActionCategory.BasicAttack,
                priority: 10,
                durationFrames: durationFrames,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 5),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 6, 10),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 11, durationFrames - 1),
                },
                tracks: new[] { new CharacterActionTrackPlan(CharacterActionTrackKind.Debug, "light_attack", 1) },
                traceId: "trace-plan");
        }

        private static CharacterActionConfig CreateActionConfig(
            CharacterActionTimelineAuthority authority,
            int? durationFrames)
        {
            return new CharacterActionConfig(
                id: 100,
                stableId: "light_attack",
                displayName: "Light Attack",
                category: CharacterActionCategory.BasicAttack,
                timelineAuthority: authority,
                tags: new[] { "attack" },
                priority: 10,
                durationFrames: durationFrames,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 5),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 6, 10),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 11, 23),
                },
                cancelRules: null,
                interruptRules: null,
                combatTrack: new CombatTrackConfig("combat.light_attack", new[]
                {
                    new CombatTrackEvent(6, CharacterActionTrackEventKind.StartCombatAction, "combat.light_attack"),
                }));
        }
    }
}
