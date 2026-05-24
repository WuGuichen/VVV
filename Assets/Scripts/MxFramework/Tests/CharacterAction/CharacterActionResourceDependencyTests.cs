using MxFramework.CharacterAction;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionResourceDependencyTests
    {
        [Test]
        public void Collector_ReturnsDependenciesWithTrackFrameAndEventMetadata()
        {
            CharacterActionConfig action = CreateAction(
                combatTrack: new CombatTrackConfig("combat.light", new[]
                {
                    new CombatTrackEvent(4, CharacterActionTrackEventKind.StartCombatAction, stableEventId: "combat.start"),
                    new CombatTrackEvent(5, CharacterActionTrackEventKind.StartHitTrace, traceProfileId: "trace.sword", stableEventId: "trace.start"),
                }),
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(6, CharacterActionTrackEventKind.SendGameplayRequest, requestId: "gameplay.hit", stableEventId: "gameplay.hit"),
                }),
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.CrossFadeAnimation, "anim.light", stableEventId: "anim.start"),
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(4, CharacterActionTrackEventKind.PlayAudioCue, cueId: "sfx.light", stableEventId: "sfx.light"),
                    new PresentationTrackEvent(5, CharacterActionTrackEventKind.SpawnVisualCue, resourceKey: "vfx.slash", stableEventId: "vfx.slash"),
                    new PresentationTrackEvent(6, CharacterActionTrackEventKind.CameraImpulse, stableEventId: "camera.hit"),
                }));

            CharacterActionResourceDependency[] dependencies = CharacterActionResourceDependencyCollector.Collect(action);

            Assert.AreEqual(6, dependencies.Length);
            AssertDependency(
                dependencies[0],
                CharacterActionResourceDependencyKind.CombatAction,
                "combat.light",
                CharacterActionTrackKind.Combat,
                CharacterActionTrackEventKind.StartCombatAction,
                4,
                "combat.start");
            AssertDependency(
                dependencies[1],
                CharacterActionResourceDependencyKind.TraceProfile,
                "trace.sword",
                CharacterActionTrackKind.Combat,
                CharacterActionTrackEventKind.StartHitTrace,
                5,
                "trace.start");
            AssertDependency(
                dependencies[2],
                CharacterActionResourceDependencyKind.GameplayRequest,
                "gameplay.hit",
                CharacterActionTrackKind.Gameplay,
                CharacterActionTrackEventKind.SendGameplayRequest,
                6,
                "gameplay.hit");
            AssertDependency(
                dependencies[3],
                CharacterActionResourceDependencyKind.AnimationAction,
                "anim.light",
                CharacterActionTrackKind.Animation,
                CharacterActionTrackEventKind.CrossFadeAnimation,
                0,
                "anim.start");
            AssertDependency(
                dependencies[4],
                CharacterActionResourceDependencyKind.AudioCue,
                "sfx.light",
                CharacterActionTrackKind.Presentation,
                CharacterActionTrackEventKind.PlayAudioCue,
                4,
                "sfx.light");
            AssertDependency(
                dependencies[5],
                CharacterActionResourceDependencyKind.VfxResource,
                "vfx.slash",
                CharacterActionTrackKind.Presentation,
                CharacterActionTrackEventKind.SpawnVisualCue,
                5,
                "vfx.slash");
        }

        [Test]
        public void Validation_ReusesCollectorForMissingResourceDiagnostics()
        {
            CharacterActionConfig action = CreateAction(
                combatTrack: new CombatTrackConfig(string.Empty, new[]
                {
                    new CombatTrackEvent(4, CharacterActionTrackEventKind.StartCombatAction),
                }),
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(5, CharacterActionTrackEventKind.SendGameplayRequest),
                }),
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.PlayAnimation),
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(5, CharacterActionTrackEventKind.PlayAudioCue),
                    new PresentationTrackEvent(6, CharacterActionTrackEventKind.SpawnVisualCue),
                }));

            CharacterActionDiagnostic[] diagnostics = CharacterActionValidation.ValidateActionConfig(action);

            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.CombatActionMissing);
            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.ResourceCostWithoutResourceId);
            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.AnimationActionMissing);
            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.AudioCueMissing);
            AssertHasDiagnostic(diagnostics, CharacterActionDiagnosticCodes.PresentationResourceMissing);
        }

        [Test]
        public void Collector_ReturnsTrackLevelCombatActionWhenNoStartEventExists()
        {
            CharacterActionConfig action = CreateAction(
                combatTrack: new CombatTrackConfig("combat.light", null));

            CharacterActionResourceDependency[] dependencies = CharacterActionResourceDependencyCollector.Collect(action);

            Assert.AreEqual(1, dependencies.Length);
            AssertDependency(
                dependencies[0],
                CharacterActionResourceDependencyKind.CombatAction,
                "combat.light",
                CharacterActionTrackKind.Combat,
                CharacterActionTrackEventKind.None,
                -1,
                string.Empty);
        }

        [Test]
        public void Formatter_OutputsDeterministicDiagnosticContextFields()
        {
            var diagnostic = CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCodes.AnimationActionMissing,
                "Animation action missing.\nCheck config.");
            var context = new CharacterActionDiagnosticFormatContext(
                actionId: "light_attack",
                phaseKind: CharacterActionPhaseKind.Active,
                trackKind: CharacterActionTrackKind.Animation,
                hasTrack: true,
                frame: 6,
                suggestedFix: "Assign animation action key.");

            string formatted = CharacterActionDiagnosticFormatter.Format(diagnostic, context);

            Assert.AreEqual(
                "code=ACT_ANIMATION_ACTION_MISSING severity=Error action=light_attack phase=Active track=Animation frame=6 message=Animation action missing.\\nCheck config. suggestedFix=Assign animation action key.",
                formatted);
        }

        private static CharacterActionConfig CreateAction(
            CombatTrackConfig combatTrack = null,
            GameplayTrackConfig gameplayTrack = null,
            AnimationTrackConfig animationTrack = null,
            PresentationTrackConfig presentationTrack = null)
        {
            return new CharacterActionConfig(
                id: 100,
                stableId: "light_attack",
                displayName: "Light Attack",
                category: CharacterActionCategory.BasicAttack,
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
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(0, CharacterActionTrackEventKind.LockMovement, CharacterMovementMode.ControlLocked),
                }),
                combatTrack: combatTrack,
                gameplayTrack: gameplayTrack,
                animationTrack: animationTrack,
                presentationTrack: presentationTrack,
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(0, CharacterActionTrackEventKind.EmitDebugMarker, "light.start"),
                }));
        }

        private static void AssertDependency(
            CharacterActionResourceDependency dependency,
            CharacterActionResourceDependencyKind kind,
            string stableId,
            CharacterActionTrackKind trackKind,
            CharacterActionTrackEventKind eventKind,
            int frame,
            string stableEventId)
        {
            Assert.AreEqual(kind, dependency.Kind);
            Assert.AreEqual(stableId, dependency.StableId);
            Assert.AreEqual("light_attack", dependency.ActionId);
            Assert.AreEqual(trackKind, dependency.TrackKind);
            Assert.AreEqual(eventKind, dependency.EventKind);
            Assert.AreEqual(frame, dependency.Frame);
            Assert.AreEqual(stableEventId, dependency.StableEventId);
            Assert.IsFalse(dependency.IsMissing);
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
