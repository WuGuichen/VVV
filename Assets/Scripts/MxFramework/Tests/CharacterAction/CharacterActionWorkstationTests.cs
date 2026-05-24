using MxFramework.CharacterAction;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionWorkstationTests
    {
        [Test]
        public void Snapshot_BuildsTimelineRowsAndDeterministicExport()
        {
            CharacterActionConfig action = CreateAction();

            CharacterActionWorkstationSnapshot first = CharacterActionWorkstation.BuildSnapshot(action);
            CharacterActionWorkstationSnapshot second = CharacterActionWorkstation.BuildSnapshot(action);

            Assert.AreEqual("light_attack", first.ActionId);
            Assert.AreEqual(12, first.DurationFrames);
            Assert.IsTrue(first.DurationResolved);
            Assert.AreEqual(9, first.TimelineRows.Length);
            Assert.AreEqual(CharacterActionWorkstationRowKind.Phase, first.TimelineRows[0].Kind);
            Assert.AreEqual(CharacterActionWorkstationRowKind.Cancel, first.TimelineRows[7].Kind);
            Assert.AreEqual(CharacterActionWorkstationRowKind.Interrupt, first.TimelineRows[8].Kind);
            Assert.AreEqual(3, first.TimelineRows[0].Entries.Length);
            Assert.AreEqual(0, first.TimelineRows[0].Entries[0].StartFrame);
            Assert.AreEqual(CharacterActionPhaseKind.Startup, first.TimelineRows[0].Entries[0].PhaseKind);
            Assert.AreEqual(8, first.TimelineRows[7].Entries[0].StartFrame);
            Assert.AreEqual(10, first.TimelineRows[7].Entries[0].EndFrame);
            Assert.AreEqual(CharacterActionSourceKind.PostureBreak, first.TimelineRows[8].Entries[0].SourceKind);
            Assert.AreEqual(first.ExportText(), second.ExportText());
            StringAssert.Contains("row kind=Animation label=Animation entries=1", first.ExportText());
            StringAssert.Contains("entry row=Animation start=0 end=0 phase=None event=CrossFadeAnimation", first.ExportText());
            StringAssert.Contains("entry row=Combat start=5 end=5 phase=None event=StartHitTrace", first.ExportText());
        }

        [Test]
        public void Snapshot_AggregatesResourceDependenciesAndDiagnostics()
        {
            CharacterActionConfig action = CreateAction(
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.PlayAnimation, stableEventId: "anim.missing"),
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(4, CharacterActionTrackEventKind.PlayAudioCue, stableEventId: "sfx.missing"),
                    new PresentationTrackEvent(5, CharacterActionTrackEventKind.SpawnVisualCue, stableEventId: "vfx.missing"),
                }),
                additionalDebug: false);

            CharacterActionWorkstationSnapshot snapshot = CharacterActionWorkstation.BuildSnapshot(action);

            AssertHasDependency(snapshot.Dependencies, CharacterActionResourceDependencyKind.AnimationAction, true);
            AssertHasDependency(snapshot.Dependencies, CharacterActionResourceDependencyKind.AudioCue, true);
            AssertHasDependency(snapshot.Dependencies, CharacterActionResourceDependencyKind.VfxResource, true);
            AssertHasDiagnostic(snapshot.Diagnostics, CharacterActionDiagnosticCodes.AnimationActionMissing);
            AssertHasDiagnostic(snapshot.Diagnostics, CharacterActionDiagnosticCodes.AudioCueMissing);
            AssertHasDiagnostic(snapshot.Diagnostics, CharacterActionDiagnosticCodes.PresentationResourceMissing);
            AssertHasFormattedDiagnostic(snapshot.FormattedDiagnostics, "action=light_attack");
            StringAssert.Contains("diagnostic code=ACT_ANIMATION_ACTION_MISSING", snapshot.ExportText());
        }

        [Test]
        public void Snapshot_IncludesRunnerPreviewEventsDeterministically()
        {
            CharacterActionConfig action = CreateAction();
            var runner = new CharacterActionRunner();
            CharacterActionRunnerOperationResult start = runner.Start(
                CharacterActionResolveResult.Success(CreatePlan(action)),
                CharacterActionRunnerActionDefinition.FromConfig(action));
            CharacterActionRunnerOperationResult tick = runner.Tick();

            var runnerEvents = new CharacterActionRunnerEvent[start.Events.Length + tick.Events.Length];
            start.Events.CopyTo(runnerEvents, 0);
            tick.Events.CopyTo(runnerEvents, start.Events.Length);

            CharacterActionWorkstationSnapshot first = CharacterActionWorkstation.BuildSnapshot(
                new CharacterActionWorkstationBuildRequest(action, runnerEvents: runnerEvents));
            CharacterActionWorkstationSnapshot second = CharacterActionWorkstation.BuildSnapshot(
                new CharacterActionWorkstationBuildRequest(action, runnerEvents: runnerEvents));

            Assert.Greater(first.PreviewEvents.Length, 0);
            Assert.AreEqual(0, first.PreviewEvents[0].Sequence);
            Assert.AreEqual(first.ExportText(), second.ExportText());
            StringAssert.Contains("preview sequence=0 kind=ActionStarted", first.ExportText());
            StringAssert.Contains("trackEvent=EmitDebugMarker", first.ExportText());
        }

        [Test]
        public void Snapshot_UnsupportedEditingCapabilityProducesStableDiagnostic()
        {
            CharacterActionConfig action = CreateAction();

            CharacterActionWorkstationSnapshot snapshot = CharacterActionWorkstation.BuildSnapshot(
                new CharacterActionWorkstationBuildRequest(
                    action,
                    requestedCapabilities: new[]
                    {
                        CharacterActionWorkstationCapability.LightEditing,
                        CharacterActionWorkstationCapability.UnityAssetEditing,
                    }));

            AssertHasDiagnostic(snapshot.Diagnostics, CharacterActionDiagnosticCodes.WorkstationEditingUnsupported);
            Assert.IsFalse(snapshot.HasErrors);
            StringAssert.Contains("ACT_WORKSTATION_EDITING_UNSUPPORTED", snapshot.ExportText());
        }

        private static CharacterActionConfig CreateAction(
            AnimationTrackConfig animationTrack = null,
            PresentationTrackConfig presentationTrack = null,
            bool additionalDebug = true)
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
                cancelRules: new[]
                {
                    new CharacterCancelRule(8, 10, targetActionId: 200, sourceKind: CharacterActionSourceKind.Command),
                },
                interruptRules: new[]
                {
                    new CharacterInterruptRule(CharacterActionSourceKind.PostureBreak, minimumPriority: 50, targetActionId: 400),
                },
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        2,
                        CharacterActionTrackEventKind.LockMovement,
                        CharacterMovementMode.ControlLocked,
                        stableEventId: "motion.lock"),
                }),
                combatTrack: new CombatTrackConfig("combat.light", new[]
                {
                    new CombatTrackEvent(4, CharacterActionTrackEventKind.StartCombatAction, stableEventId: "combat.start"),
                    new CombatTrackEvent(5, CharacterActionTrackEventKind.StartHitTrace, traceProfileId: "trace.sword", stableEventId: "trace.start"),
                }),
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(6, CharacterActionTrackEventKind.SendGameplayRequest, requestId: "gameplay.hit", stableEventId: "gameplay.hit"),
                }),
                animationTrack: animationTrack ?? new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.CrossFadeAnimation, "anim.light", 0.08f, "anim.start"),
                }),
                presentationTrack: presentationTrack ?? new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(4, CharacterActionTrackEventKind.PlayAudioCue, cueId: "sfx.light", stableEventId: "sfx.light"),
                    new PresentationTrackEvent(5, CharacterActionTrackEventKind.SpawnVisualCue, resourceKey: "vfx.slash", stableEventId: "vfx.slash"),
                }),
                debugTrack: additionalDebug
                    ? new DebugTrackConfig(new[]
                    {
                        new DebugTrackEvent(0, CharacterActionTrackEventKind.EmitDebugMarker, "light.start", "debug.start"),
                    })
                    : DebugTrackConfig.Empty);
        }

        private static CharacterActionPlan CreatePlan(CharacterActionConfig action)
        {
            return new CharacterActionPlan(
                planId: action.Id,
                actionId: action.StableId,
                category: action.Category,
                priority: action.Priority,
                durationFrames: action.DurationFrames.GetValueOrDefault(),
                phases: action.Phases,
                tracks: CharacterActionTrackPlan.FromConfig(action),
                traceId: "trace." + action.StableId);
        }

        private static void AssertHasDependency(
            CharacterActionResourceDependency[] dependencies,
            CharacterActionResourceDependencyKind kind,
            bool isMissing)
        {
            for (int i = 0; i < dependencies.Length; i++)
            {
                if (dependencies[i].Kind == kind && dependencies[i].IsMissing == isMissing)
                    return;
            }

            Assert.Fail("Expected dependency kind " + kind + " with missing=" + isMissing + ".");
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

        private static void AssertHasFormattedDiagnostic(string[] diagnostics, string expectedPart)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Contains(expectedPart))
                    return;
            }

            Assert.Fail("Expected formatted diagnostic containing '" + expectedPart + "'.");
        }
    }
}
