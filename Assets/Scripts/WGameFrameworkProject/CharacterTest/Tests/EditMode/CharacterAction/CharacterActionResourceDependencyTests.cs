using MxFramework.CharacterAction;
using MxFramework.Combat.Damage;
using MxFramework.Editor.CharacterAction;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionResourceDependencyTests
    {
        [Test]
        public void Collector_ReturnsDependenciesWithTrackFrameAndEventMetadata()
        {
            CharacterActionConfig action = CreateAction(
                combatTrack: new CombatTrackConfig(CharacterTestNumericIds.Field_combat_light, new[]
                {
                    new CombatTrackEvent(4, CharacterActionTrackEventKind.StartCombatAction, stableEventId: CharacterTestNumericIds.Field_combat_start),
                    new CombatTrackEvent(5, CharacterActionTrackEventKind.HitTraceWindow, traceProfileId: CharacterTestNumericIds.Field_trace_sword, stableEventId: CharacterTestNumericIds.Field_trace_start),
                }),
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(6, CharacterActionTrackEventKind.SendGameplayRequest, requestId: CharacterTestNumericIds.Field_gameplay_hit, stableEventId: CharacterTestNumericIds.Field_gameplay_hit),
                }),
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.CrossFadeAnimation, "anim.light", stableEventId: CharacterTestNumericIds.Field_anim_start),
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(4, CharacterActionTrackEventKind.PlayAudioCue, cueId: CharacterTestNumericIds.Field_sfx_light, stableEventId: CharacterTestNumericIds.Field_sfx_light),
                    new PresentationTrackEvent(5, CharacterActionTrackEventKind.SpawnVisualCue, resourceKey: "vfx.slash", stableEventId: CharacterTestNumericIds.Field_vfx_slash),
                    new PresentationTrackEvent(6, CharacterActionTrackEventKind.CameraImpulse, stableEventId: CharacterTestNumericIds.Field_camera_hit),
                }));

            CharacterActionResourceDependency[] dependencies = CharacterActionResourceDependencyCollector.Collect(action);

            Assert.AreEqual(8, dependencies.Length);
            AssertDependency(
                dependencies[0],
                CharacterActionResourceDependencyKind.MotionEvent,
                "motion.lock",
                CharacterActionTrackKind.Motion,
                CharacterActionTrackEventKind.LockMovement,
                0,
                 CharacterTestNumericIds.Field_motion_lock);
            AssertDependency(
                dependencies[1],
                CharacterActionResourceDependencyKind.CombatAction,
                "combat.light",
                CharacterActionTrackKind.Combat,
                CharacterActionTrackEventKind.StartCombatAction,
                4,
                 CharacterTestNumericIds.Field_combat_start);
            AssertDependency(
                dependencies[2],
                CharacterActionResourceDependencyKind.TraceProfile,
                "trace.sword",
                CharacterActionTrackKind.Combat,
                CharacterActionTrackEventKind.HitTraceWindow,
                5,
                 CharacterTestNumericIds.Field_trace_start);
            AssertDependency(
                dependencies[3],
                CharacterActionResourceDependencyKind.GameplayRequest,
                "gameplay.hit",
                CharacterActionTrackKind.Gameplay,
                CharacterActionTrackEventKind.SendGameplayRequest,
                6,
                 CharacterTestNumericIds.Field_gameplay_hit);
            AssertDependency(
                dependencies[4],
                CharacterActionResourceDependencyKind.AnimationAction,
                "anim.light",
                CharacterActionTrackKind.Animation,
                CharacterActionTrackEventKind.CrossFadeAnimation,
                0,
                 CharacterTestNumericIds.Field_anim_start);
            AssertDependency(
                dependencies[5],
                CharacterActionResourceDependencyKind.AudioCue,
                "sfx.light",
                CharacterActionTrackKind.Presentation,
                CharacterActionTrackEventKind.PlayAudioCue,
                4,
                 CharacterTestNumericIds.Field_sfx_light);
            AssertDependency(
                dependencies[6],
                CharacterActionResourceDependencyKind.VfxResource,
                "vfx.slash",
                CharacterActionTrackKind.Presentation,
                CharacterActionTrackEventKind.SpawnVisualCue,
                5,
                 CharacterTestNumericIds.Field_vfx_slash);
            AssertDependency(
                dependencies[7],
                CharacterActionResourceDependencyKind.DebugMarker,
                "light.start",
                CharacterActionTrackKind.Debug,
                CharacterActionTrackEventKind.EmitDebugMarker,
                0,
                 CharacterTestNumericIds.Field_debug_start);
        }

        [Test]
        public void Collector_MarksStartCombatActionMissingWhenNoEventOrTrackIdExists()
        {
            CharacterActionConfig action = CreateAction(
                combatTrack: new CombatTrackConfig(0, new[]
                {
                    new CombatTrackEvent(4, CharacterActionTrackEventKind.StartCombatAction, stableEventId: CharacterTestNumericIds.Field_combat_start),
                }),
                animationTrack: AnimationTrackConfig.Empty,
                presentationTrack: PresentationTrackConfig.Empty,
                debugTrack: DebugTrackConfig.Empty);

            CharacterActionResourceDependency[] dependencies = CharacterActionResourceDependencyCollector.Collect(action);

            Assert.AreEqual(2, dependencies.Length);
            CharacterActionResourceDependency dependency = dependencies[1];
            Assert.AreEqual(CharacterActionResourceDependencyKind.CombatAction, dependency.Kind);
            Assert.AreEqual(string.Empty, dependency.StableId);
            Assert.AreEqual(CharacterTestActionIds.Action_light_attack, dependency.ActionId);
            Assert.AreEqual(CharacterActionTrackKind.Combat, dependency.TrackKind);
            Assert.AreEqual(CharacterActionTrackEventKind.StartCombatAction, dependency.EventKind);
            Assert.AreEqual(4, dependency.Frame);
            Assert.AreEqual(CharacterTestNumericIds.Field_combat_start, dependency.StableEventId);
            Assert.IsTrue(dependency.IsMissing);
        }

        [Test]
        public void Validation_ReusesCollectorForMissingResourceDiagnostics()
        {
            CharacterActionConfig action = CreateAction(
                combatTrack: new CombatTrackConfig(0, new[]
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
                motionTrack: MotionTrackConfig.Empty,
                combatTrack: new CombatTrackConfig(CharacterTestNumericIds.Field_combat_light, null),
                debugTrack: DebugTrackConfig.Empty);

            CharacterActionResourceDependency[] dependencies = CharacterActionResourceDependencyCollector.Collect(action);

            Assert.AreEqual(1, dependencies.Length);
            AssertDependency(
                dependencies[0],
                CharacterActionResourceDependencyKind.CombatAction,
                "combat.light",
                CharacterActionTrackKind.Combat,
                CharacterActionTrackEventKind.None,
                -1,
                 0);
        }

        [Test]
        public void Collector_DoesNotEmitResourceDependenciesForInlineDamagePayloads()
        {
            CharacterActionConfig action = CreateAction(
                combatTrack: new CombatTrackConfig(CharacterTestNumericIds.Field_combat_light, new[]
                {
                    new CombatTrackEvent(
                        5,
                        CharacterActionTrackEventKind.HitTraceWindow,
                        equipmentSlotId: CharacterTestNumericIds.Field_mainHand,
                        stableEventId: CharacterTestNumericIds.Field_trace_start,
                        damagePayload: CreateLightDamagePayload()),
                }),
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(
                        8,
                        CharacterActionTrackEventKind.ApplyGameplayEffect,
                        stableEventId: CharacterTestNumericIds.Field_shockwave_damage,
                        targetPolicyId: CharacterTestNumericIds.Field_target_locked_area_2m,
                        damagePayload: CreateAreaDamagePayload()),
                }),
                animationTrack: AnimationTrackConfig.Empty,
                presentationTrack: PresentationTrackConfig.Empty,
                debugTrack: DebugTrackConfig.Empty);

            CharacterActionResourceDependency[] dependencies =
                CharacterActionResourceDependencyCollector.Collect(action);

            Assert.AreEqual(0, dependencies.Length);
        }

        [Test]
        public void DamageUsageIndex_BuildsWeaponTraceAndScriptedRows()
        {
            CharacterActionConfig action = CreateAction(
                combatTrack: new CombatTrackConfig(CharacterTestNumericIds.Field_combat_light, new[]
                {
                    new CombatTrackEvent(
                        5,
                        CharacterActionTrackEventKind.HitTraceWindow,
                        equipmentSlotId: CharacterTestNumericIds.Field_mainHand,
                        stableEventId: CharacterTestNumericIds.Field_trace_start,
                        damagePayload: CreateLightDamagePayload()),
                }),
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(
                        8,
                        CharacterActionTrackEventKind.ApplyGameplayEffect,
                        stableEventId: CharacterTestNumericIds.Field_shockwave_damage,
                        targetPolicyId: CharacterTestNumericIds.Field_target_locked_area_2m,
                        damagePayload: CreateAreaDamagePayload()),
                }),
                animationTrack: AnimationTrackConfig.Empty,
                presentationTrack: PresentationTrackConfig.Empty,
                debugTrack: DebugTrackConfig.Empty);

            CharacterActionDamageUsageIndexRow[] rows =
                CharacterActionDamageUsageIndexBuilder.Build(action);

            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual(CharacterTestNumericIds.DamageSlash, rows[0].DamagePayload.DamageTypeId);
            Assert.AreEqual(34, rows[0].DamagePayload.ImpactPower);
            Assert.AreEqual(CharacterActionDamageSourceKind.WeaponTrace, rows[0].SourceKind);
            Assert.AreEqual(CharacterActionAuthoringIds.Resolve("mainHand", 405, allocate: false), rows[0].EquipmentSlotId);
            Assert.AreEqual(CharacterTestNumericIds.DamageForce, rows[1].DamagePayload.DamageTypeId);
            Assert.AreEqual(12, rows[1].DamagePayload.ImpactPower);
            Assert.AreEqual(CharacterActionDamageSourceKind.Scripted, rows[1].SourceKind);
            Assert.AreEqual(CharacterActionAuthoringIds.Resolve("target.locked.area_2m", 406, allocate: false), rows[1].TargetPolicyId);
        }

        [Test]
        public void Formatter_OutputsDeterministicDiagnosticContextFields()
        {
            var diagnostic = CharacterActionDiagnostic.Error(
                CharacterActionDiagnosticCode.AnimationActionMissing,
                "Animation action missing.\nCheck config.");
            var context = new CharacterActionDiagnosticFormatContext(
                actionId: new MxFramework.CharacterAction.CharacterActionId(301),
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
            MotionTrackConfig motionTrack = null,
            CombatTrackConfig combatTrack = null,
            GameplayTrackConfig gameplayTrack = null,
            AnimationTrackConfig animationTrack = null,
            PresentationTrackConfig presentationTrack = null,
            DebugTrackConfig debugTrack = null)
        {
            return new CharacterActionConfig(
                actionId: new CharacterActionId(100),
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
                motionTrack: motionTrack ?? new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        0,
                        CharacterActionTrackEventKind.LockMovement,
                        CharacterMovementMode.ControlLocked,
                        stableEventId: CharacterTestNumericIds.Field_motion_lock),
                }),
                combatTrack: combatTrack,
                gameplayTrack: gameplayTrack,
                animationTrack: animationTrack,
                presentationTrack: presentationTrack,
                debugTrack: debugTrack ?? new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(0, CharacterActionTrackEventKind.EmitDebugMarker, "light.start", CharacterTestNumericIds.Field_debug_start),
                }));
        }

        private static CharacterActionDamagePayload CreateLightDamagePayload()
        {
            return new CharacterActionDamagePayload(
                CharacterTestBulkIds.Id790011,
                DamageFormulaIds.CharacterTestSoftDefenseV1,
                6,
                new[] { new DamageAttributeScale(2, 0.65f) },
                34,
                CharacterActionDamageIdentityAuthoring.ExistingTags(new[] { "melee", "weapon", "scatter" }),
                CharacterTestBulkIds.Id790012);
        }

        private static CharacterActionDamagePayload CreateAreaDamagePayload()
        {
            return new CharacterActionDamagePayload(
                CharacterTestBulkIds.Id790013,
                DamageFormulaIds.Flat,
                6,
                new DamageAttributeScale[0],
                12,
                CharacterActionDamageIdentityAuthoring.ExistingTags(new[] { "area", "shockwave", "scatter" }),
                CharacterTestBulkIds.Id790014);
        }

        private static void AssertDependency(
            CharacterActionResourceDependency dependency,
            CharacterActionResourceDependencyKind kind,
            string stableId,
            CharacterActionTrackKind trackKind,
            CharacterActionTrackEventKind eventKind,
            int frame,
            int stableEventId)
        {
            Assert.AreEqual(kind, dependency.Kind);
            Assert.AreEqual(stableId, dependency.StableId);
            Assert.AreEqual(CharacterTestActionIds.Action_light_attack, dependency.ActionId);
            Assert.AreEqual(trackKind, dependency.TrackKind);
            Assert.AreEqual(eventKind, dependency.EventKind);
            Assert.AreEqual(frame, dependency.Frame);
            Assert.AreEqual(stableEventId, dependency.StableEventId);
            Assert.IsFalse(dependency.IsMissing);
        }

        private static CharacterActionResourceDependency FindDependency(
            CharacterActionResourceDependency[] dependencies,
            CharacterActionResourceDependencyKind kind,
            string stableId)
        {
            for (int i = 0; i < dependencies.Length; i++)
            {
                CharacterActionResourceDependency dependency = dependencies[i];
                if (dependency.Kind == kind && dependency.StableId == stableId)
                    return dependency;
            }

            Assert.Fail("Expected dependency " + kind + " " + stableId + ".");
            return default;
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
