using System;
using MxFramework.CharacterAction;
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
                previousPressureValue: 0,
                currentPressureValue: 0,
                pressureDelta: 0,
                sourceId: 0,
                isAirborne: true,
                currentCharacterPhase: CharacterActionPhaseKind.Airborne);
            var committed = new CharacterReactionContext(
                CharacterReactionContextSourceKind.PressureBandChanged,
                CharacterReactionContextCompleteness.PressureOnly,
                new RuntimeFrame(4),
                Entity(),
                PressureBand.Stable,
                PressureBand.Stable,
                previousPressureValue: 0,
                currentPressureValue: 0,
                pressureDelta: 0,
                sourceId: 0,
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
    }
}
