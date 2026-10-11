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
                id: 1,
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
                actionId: new CharacterActionId(100),
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
                cancelRules: new[] { new CharacterCancelRule(16, 23) },
                interruptRules: new[] { new CharacterInterruptRule(CharacterActionSourceKind.CohesionBreak, minimumPriority: 100) },
                motionTrack: new MotionTrackConfig(usesRootMotion: false, events: new[]
                {
                    new MotionTrackEvent(0, CharacterActionTrackEventKind.LockMovement, CharacterMovementMode.ControlLocked),
                }),
                combatTrack: new CombatTrackConfig(CharacterTestNumericIds.Field_combat_light_attack, new[]
                {
                    new CombatTrackEvent(6, CharacterActionTrackEventKind.StartCombatAction, CharacterTestNumericIds.Field_combat_light_attack),
                }),
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(6, CharacterActionTrackEventKind.SendGameplayRequest, CharacterTestNumericIds.Field_scatter_light),
                }),
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.CrossFadeAnimation, "anim.light_attack", 0.08f),
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(6, CharacterActionTrackEventKind.PlayAudioCue, CharacterTestNumericIds.Field_sfx_light_attack),
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
                commandBindings: new[] { new CharacterActionBinding(CharacterTestNumericIds.IntentLightAttack, new MxFramework.CharacterAction.CharacterActionId(301), priority: 10, allowQueue: true, queueWindowFrames: 8) },
                abilityBindings: new[] { new CharacterAbilityActionBinding(200, new MxFramework.CharacterAction.CharacterActionId(301), requiredTags: new[] { "sword" }) },
                reactionBindings: new[] { new CharacterReactionBinding("pressure-only", CharacterTestActionIds.Action_LightHitReact) },
                movementProfileId: movement.Id,
                reactionProfileId: "pressure-only",
                defaultActionId: CharacterTestActionIds.Action_idle);

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
            Assert.AreEqual(CharacterTestNumericIds.IntentLightAttack, set.CommandBindings[0].IntentId);
        }

        [Test]
        public void MovementProfile_CachesGravityBasedUpwardMultiplier()
        {
            var profile = new CharacterMovementProfileConfig(
                id: 1,
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
                locomotionBlendId: "test",
                upwardGravityReference: 90f);

            Assert.AreEqual(750, profile.UpwardMotionGravityScaleMilli);
            Assert.AreEqual(1000, CharacterActionVerticalRootMotionGravity.ResolveUpwardScaleMilli(0f));
            Assert.AreEqual(500, CharacterActionVerticalRootMotionGravity.ResolveUpwardScaleMilli(-30f, 30f));
            Assert.AreEqual(10, CharacterActionVerticalRootMotionGravity.ResolveUpwardScaleMilli(-9000f));
        }

        [Test]
        public void TrackEventKind_IsStableEnumNotFreeString()
        {
            var motion = new MotionTrackEvent(0, CharacterActionTrackEventKind.ApplyImpulse, CharacterMovementMode.Airborne, y: 8f);
            var combat = new CombatTrackEvent(3, CharacterActionTrackEventKind.HitTraceWindow, equipmentSlotId: CharacterTestNumericIds.Field_mainHand);
            var gameplay = new GameplayTrackEvent(4, CharacterActionTrackEventKind.CastAbility, abilityStableId: CharacterTestNumericIds.Field_ability_jump);
            var animation = new AnimationTrackEvent(0, CharacterActionTrackEventKind.PlayAnimation, "jump");
            var presentation = new PresentationTrackEvent(2, CharacterActionTrackEventKind.SpawnVisualCue, resourceKey: "vfx.jump");
            var debug = new DebugTrackEvent(0, CharacterActionTrackEventKind.EmitDebugMarker, "jump.start");

            Assert.IsInstanceOf<CharacterActionTrackEventKind>(motion.Kind);
            Assert.IsInstanceOf<CharacterActionTrackEventKind>(combat.Kind);
            Assert.AreEqual("mainHand", combat.EquipmentSlotId);
            Assert.IsInstanceOf<CharacterActionTrackEventKind>(gameplay.Kind);
            Assert.IsInstanceOf<CharacterActionTrackEventKind>(animation.Kind);
            Assert.IsInstanceOf<CharacterActionTrackEventKind>(presentation.Kind);
            Assert.IsInstanceOf<CharacterActionTrackEventKind>(debug.Kind);
            Assert.AreEqual(CharacterActionTrackEventKind.ApplyImpulse, motion.Kind);
            Assert.AreEqual(CharacterTestNumericIds.Field_ability_jump, gameplay.AbilityStableId);
        }

        [Test]
        public void TrackEventKind_MustBelongToTrack()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MotionTrackEvent(0, CharacterActionTrackEventKind.PlayAnimation));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PresentationTrackEvent(0, CharacterActionTrackEventKind.HitTraceWindow));
            Assert.DoesNotThrow(() =>
                new MotionTrackEvent(
                    0,
                    CharacterActionTrackEventKind.SetControlScale,
                    stableEventId: CharacterTestNumericIds.Field_motion_control,
                    movementControlScale: 0.5f,
                    rotationControlScale: 0.25f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CombatTrackEvent(0, CharacterActionTrackEventKind.SetControlScale));
        }

        [Test]
        public void IntentRequest_CanBeConstructedWithStableIntentAndAbilityContext()
        {
            var request = new CharacterActionIntentRequest(
                entity: Entity(),
                intentId: CharacterTestNumericIds.IntentLightAttack,
                abilityId: 200,
                abilityStableId: CharacterTestNumericIds.Field_ability_light,
                requestedActionId: new MxFramework.CharacterAction.CharacterActionId(301),
                sourceKind: CharacterActionSourceKind.LocalInput,
                priority: 10,
                frame: new RuntimeFrame(42),
                traceId: CharacterTestRuntimeTraceIds.Trace403);

            Assert.AreEqual(Entity(), request.Entity);
            Assert.AreEqual(CharacterTestNumericIds.IntentLightAttack, request.IntentId);
            Assert.AreEqual(200, request.AbilityId);
            Assert.AreEqual(CharacterTestNumericIds.Field_ability_light, request.AbilityStableId);
            Assert.AreEqual(CharacterTestActionIds.Action_light_attack, request.RequestedActionId);
            Assert.AreEqual(CharacterActionSourceKind.LocalInput, request.SourceKind);
            Assert.AreEqual(10, request.Priority);
            Assert.AreEqual(new RuntimeFrame(42), request.Frame);
            Assert.AreEqual(CharacterTestRuntimeTraceIds.Trace403, request.TraceId);
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
                    intentId: CharacterTestNumericIds.Intent,
                    abilityId: null,
                    abilityStableId: 0,
                    requestedActionId: MxFramework.CharacterAction.CharacterActionId.None,
                    sourceKind: sourceKinds[i],
                    priority: i,
                    frame: new RuntimeFrame(i),
                    traceId: CharacterTestRuntimeTraceIds.TraceSource);

                Assert.AreEqual(sourceKinds[i], request.SourceKind);
            }

            var requestRule = new CharacterCancelRule(
                startFrame: 0,
                endFrame: 10,
                requestTypeMask: CharacterActionRequestType.BasicAttack | CharacterActionRequestType.Jump);

            Assert.IsTrue(requestRule.Matches(5, CharacterActionCategory.BasicAttack, actionSlotId: 0));
            Assert.IsTrue(requestRule.Matches(5, CharacterActionCategory.Jump, actionSlotId: 0));
            Assert.IsFalse(requestRule.Matches(5, CharacterActionCategory.Reaction, actionSlotId: 0));
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
                        CharacterActionDiagnosticCode.MissingActionBinding,
                        "No binding for intent LightAttack."),
                },
                traceId: CharacterTestRuntimeTraceIds.TraceReject);

            Assert.IsTrue(success.IsSuccess);
            Assert.AreEqual(CharacterActionResolveStatus.Success, success.Status);
            Assert.AreSame(plan, success.Plan);
            Assert.IsTrue(queued.IsQueued);
            Assert.AreEqual(CharacterActionResolveStatus.Queued, queued.Status);
            Assert.IsTrue(rejected.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.MissingActionBinding, rejected.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionBinding, rejected.Diagnostics[0].Code);
            Assert.AreEqual(CharacterTestRuntimeTraceIds.TraceReject, rejected.TraceId);
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
                    CharacterActionDiagnostic.Error(CharacterActionDiagnosticCode.MissingActionBinding, "Binding missing."),
                    CharacterActionDiagnostic.Error(CharacterActionDiagnosticCode.ResourceMissing, "Resource missing."),
                    CharacterActionDiagnostic.Error(CharacterActionDiagnosticCode.ReactionContextIncomplete, "Reaction context incomplete."),
                    CharacterActionDiagnostic.Error(CharacterActionDiagnosticCode.PhaseCombatAnchorMissing, "Phase anchor missing."),
                    CharacterActionDiagnostic.Error(CharacterActionDiagnosticCode.CharacterCombatCancelConflict, "Cancel conflict."),
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
                actionId: config.ActionId,
                category: config.Category,
                priority: config.Priority,
                durationFrames: config.DurationFrames.Value,
                phases: config.Phases,
                tracks: tracks,
                traceId: CharacterTestRuntimeTraceIds.TracePlan);

            Assert.AreEqual(403, plan.PlanId);
            Assert.AreEqual(new CharacterActionId(100), plan.ActionId);
            Assert.AreEqual(CharacterActionCategory.BasicAttack, plan.Category);
            Assert.AreEqual(24, plan.DurationFrames);
            Assert.AreEqual(3, plan.Phases.Length);
            Assert.AreEqual(6, plan.Tracks.Length);
            Assert.AreEqual(CharacterActionTrackKind.Combat, plan.Tracks[1].Kind);
            Assert.AreEqual(CharacterTestNumericIds.Field_combat_light_attack, plan.Tracks[1].ExternalReferenceId);
            Assert.AreEqual(CharacterTestRuntimeTraceIds.TracePlan, plan.TraceId);
        }

        [Test]
        public void PressureOnlyReactionProfile_MapsLightHitCohesionBreakAndDeath()
        {
            var profile = new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(CharacterTestActionIds.Action_Death, CharacterReactionRuleTrigger.Death, isDeath: true),
                    new CharacterReactionRule(CharacterTestActionIds.Action_AirborneRecovery, CharacterReactionRuleTrigger.Any, isAirborne: true, currentPhase: CharacterActionPhaseKind.Airborne),
                    new CharacterReactionRule(CharacterTestActionIds.Action_CommittedFlinch, CharacterReactionRuleTrigger.Any, currentActionCommitted: true, currentActionInterruptible: false),
                    new CharacterReactionRule(CharacterTestActionIds.Action_CohesionBreakReact, CharacterReactionRuleTrigger.CohesionBreak, currentScatterPressureBand: PressureBand.Broken),
                    new CharacterReactionRule(CharacterTestActionIds.Action_LightHitReact, CharacterReactionRuleTrigger.PressureBandChanged, currentScatterPressureBand: PressureBand.Pressed),
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
            CharacterReactionContext cohesionBreak = CharacterReactionContextBuilder.FromCohesionBreak(
                new CohesionBreakEvent(
                    new RuntimeFrame(1),
                    Entity(),
                    PressureBand.Critical,
                    previousValue: 90,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100,
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
                currentScatterPressure: 0,
                maxScatterPressure: 0,
                delta: 0,
                sourceId: 0,
                reason: string.Empty,
                traceId: RuntimeTraceIdentity.None,
                isDeath: false,
                lifecycleState: string.Empty,
                bodyPartId: 0,
                hitZoneId: 0,
                damageTypeId: 0,
                hitDirection: CharacterHitDirection.Unknown,
                reactionGroupId: 0,
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
                currentScatterPressure: 0,
                maxScatterPressure: 0,
                delta: 0,
                sourceId: 0,
                reason: string.Empty,
                traceId: RuntimeTraceIdentity.None,
                isDeath: false,
                lifecycleState: string.Empty,
                bodyPartId: 0,
                hitZoneId: 0,
                damageTypeId: 0,
                hitDirection: CharacterHitDirection.Unknown,
                reactionGroupId: 0,
                currentActionCommitted: true,
                currentActionInterruptible: false);

            Assert.AreEqual(CharacterTestActionIds.Action_LightHitReact, CharacterReactionSelector.Select(profile, lightHit).SelectedActionId);
            Assert.AreEqual(CharacterTestActionIds.Action_CohesionBreakReact, CharacterReactionSelector.Select(profile, cohesionBreak).SelectedActionId);
            Assert.AreEqual(CharacterTestActionIds.Action_Death, CharacterReactionSelector.Select(profile, death).SelectedActionId);
            Assert.AreEqual(CharacterTestActionIds.Action_AirborneRecovery, CharacterReactionSelector.Select(profile, airborne).SelectedActionId);
            Assert.AreEqual(CharacterTestActionIds.Action_CommittedFlinch, CharacterReactionSelector.Select(profile, committed).SelectedActionId);
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
                        CharacterTestActionIds.Action_DirectionalHitReact,
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
                actionId: new MxFramework.CharacterAction.CharacterActionId(301),
                category: CharacterActionCategory.BasicAttack,
                priority: 10,
                durationFrames: durationFrames,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 5),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 6, 10),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 11, durationFrames - 1),
                },
                tracks: new[]
                {
                    new CharacterActionTrackPlan(
                        CharacterActionTrackKind.Debug,
                        new CharacterActionId(100),
                        0,
                        1)
                },
                traceId: CharacterTestRuntimeTraceIds.TracePlan);
        }

        private static CharacterActionConfig CreateActionConfig(
            CharacterActionTimelineAuthority authority,
            int? durationFrames)
        {
            return new CharacterActionConfig(
                actionId: new CharacterActionId(100),
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
                combatTrack: new CombatTrackConfig(CharacterTestNumericIds.Field_combat_light_attack, new[]
                {
                    new CombatTrackEvent(6, CharacterActionTrackEventKind.StartCombatAction, CharacterTestNumericIds.Field_combat_light_attack),
                }));
        }
    }
}
