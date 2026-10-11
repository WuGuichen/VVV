using System;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline.Preview;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionPreviewNoAllocTests
    {
        private const string CharacterPrefabPath =
            "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test/prefabs/codex_flow_test_character_preview.prefab";
        private const string CharacterStableId = "char.codex_flow_test";
        private const string EquipmentStateStableId = "equip_state.codex_flow_test.sword_empty";
        private const string ValidTraceProfileId = "trace.iron_sword.blade";

        [TearDown]
        public void TearDown()
        {
            CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();
        }

        [Test]
        public void PreviewFrame_UnchangedRevisionReusesHierarchyLocatorAndSceneGuiBuffers()
        {
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath));
            CharacterActionAuthoringModel model = CreatePreviewModel();

            using (var renderScene = new CharacterActionPreviewRenderScene())
            using (var session = new CharacterActionPreviewRuntimeSession(renderScene))
            {
                Vector3[] lineBuffer = session.SceneLinePointBufferForTests;
                Vector3[] projectilePathBuffer = session.ProjectilePathPointBufferForTests;

                session.PreviewFrame(model, 0, revisionToken: 17L);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.NotNull(session.CachedPreviewAnimator);
                Assert.NotNull(session.CachedDefaultEquipmentBinder);
                Assert.AreEqual(1, session.PreviewHierarchyCacheBuildCount);
                Assert.AreEqual(1, session.WeaponLocatorCacheBuildCount);
                Assert.AreEqual(2, lineBuffer.Length);
                Assert.AreEqual(17, projectilePathBuffer.Length);

                var locator = new CharacterActionPreviewRuntimeSession.CharacterActionPreviewWeaponLocatorPoseSnapshot(
                    CharacterTestBulkIds.Id790010,
                    0,
                    0,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    Vector3.zero,
                    Quaternion.identity,
                    string.Empty,
                    string.Empty,
                    Vector3.up,
                    Quaternion.identity,
                    0.05f,
                    "NoAlloc");
                session.SetWeaponLocatorPoseSnapshotForTests(locator);
                Assert.AreEqual(1, session.CachedWeaponLocatorCount);

                Animator animator = session.CachedPreviewAnimator;
                CharacterDefaultEquipmentRuntimeBinder binder = session.CachedDefaultEquipmentBinder;
                int hierarchyBuilds = session.PreviewHierarchyCacheBuildCount;
                int locatorBuilds = session.WeaponLocatorCacheBuildCount;
                for (int i = 0; i < 8; i++)
                {
                    Assert.IsTrue(session.TryResolveWeaponLocatorPose(locator, out _), session.DiagnosticsText);
                }

                session.PreviewFrame(model, 1, revisionToken: 17L);
                session.PreviewFrame(model, 2, revisionToken: 17L);

                Assert.AreSame(animator, session.CachedPreviewAnimator);
                Assert.AreSame(binder, session.CachedDefaultEquipmentBinder);
                Assert.AreEqual(hierarchyBuilds, session.PreviewHierarchyCacheBuildCount);
                Assert.AreEqual(locatorBuilds, session.WeaponLocatorCacheBuildCount);
                Assert.AreSame(lineBuffer, session.SceneLinePointBufferForTests);
                Assert.AreSame(projectilePathBuffer, session.ProjectilePathPointBufferForTests);
            }
        }

        [Test]
        public void PreviewFrame_ReplayingSameEventFrameReusesDispatchTraceText()
        {
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath));
            CharacterActionAuthoringModel model = CreatePreviewModel();

            using (var renderScene = new CharacterActionPreviewRenderScene())
            using (var session = new CharacterActionPreviewRuntimeSession(renderScene))
            {
                session.PreviewFrame(model, 0, revisionToken: 23L);
                int firstBuildCount = session.DispatchTraceTextBuildCount;
                Assert.Greater(firstBuildCount, 0, session.DiagnosticsText);

                for (int i = 0; i < 5; i++)
                    session.PreviewFrame(model, 0, revisionToken: 23L);

                Assert.AreEqual(firstBuildCount, session.DispatchTraceTextBuildCount,
                    "Repeated scrub of the same event frame should reuse revision-scoped trace text.");

                session.PreviewFrame(model, 0, revisionToken: 24L);
                Assert.Greater(session.DispatchTraceTextBuildCount, firstBuildCount,
                    "A new document revision must rebuild diagnostics from the new action context.");
            }
        }

        [TestCase("clip")]
        [TestCase("profile")]
        [TestCase("locator")]
        [TestCase("baked")]
        public void PreviewFrame_MissingHitTraceResourceCachesOneRevisionDiagnostic(string missingResource)
        {
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath));
            CharacterActionAuthoringModel model = CreatePreviewModel();
            ConfigureMissingHitTraceResource(model.SelectedAction, missingResource);

            using (var renderScene = new CharacterActionPreviewRenderScene())
            using (var session = new CharacterActionPreviewRuntimeSession(renderScene))
            {
                const long revisionToken = 31L;
                session.PreviewFrame(model, 0, revisionToken);
                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);

                if (string.Equals(missingResource, "locator", StringComparison.Ordinal))
                {
                    session.SetWeaponLocatorPoseSnapshotForTests(
                        new CharacterActionPreviewRuntimeSession.CharacterActionPreviewWeaponLocatorPoseSnapshot(
                            CharacterTestBulkIds38.Id790040,
                            0,
                            CharacterTestBulkIds.Id790006,
                            "Missing/BladeRoot",
                            "Missing/BladeTip",
                            string.Empty,
                            string.Empty,
                            Vector3.zero,
                            Quaternion.identity,
                            string.Empty,
                            string.Empty,
                            Vector3.up,
                            Quaternion.identity,
                            0.05f,
                            "NoAllocMissingLocator"));
                }
                else if (string.Equals(missingResource, "baked", StringComparison.Ordinal))
                {
                    session.SetWeaponLocatorPoseSnapshotForTests(default);
                    session.ClearPreviewTraceBakeCatalogForTests();
                }

                session.PreviewFrame(model, 1, revisionToken);
                Assert.AreEqual(1, session.CachedHitTraceDiagnosticCount, session.DiagnosticsText);
                Assert.AreEqual(1, session.HitTraceDiagnosticLineBuildCount, session.DiagnosticsText);
                Assert.AreEqual(1, session.HitTraceDiagnosticLogCount, session.DiagnosticsText);
                StringAssert.Contains("CharacterActionPreview.HitTrace", session.DiagnosticsText);

                int buildCount = session.HitTraceDiagnosticLineBuildCount;
                int logCount = session.HitTraceDiagnosticLogCount;
                for (int frame = 2; frame <= 4; frame++)
                    session.PreviewFrame(model, frame, revisionToken);
                for (int i = 0; i < 5; i++)
                    session.PreviewFrame(model, 4, revisionToken);

                Assert.AreEqual(1, session.CachedHitTraceDiagnosticCount,
                    "A structural missing-resource diagnostic should be cached once per action revision.");
                Assert.AreEqual(buildCount, session.HitTraceDiagnosticLineBuildCount,
                    "60fps sampling must not rebuild the same missing-resource warning string.");
                Assert.AreEqual(logCount, session.HitTraceDiagnosticLogCount,
                    "Sequential playback and repeated scrub must not log the same warning again.");
                StringAssert.Contains("CharacterActionPreview.HitTrace", session.DiagnosticsText,
                    "The cached warning must remain visible even when it is not logged again.");
            }
        }

        private static CharacterActionAuthoringModel CreatePreviewModel()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            model.Profile.StableId = "codex_flow_test.default";
            model.Profile.DisplayName = "codex flow test Default";
            model.Profile.ActionSetIds = new[] { "codex_flow_test.default" };
            model.Profile.ActionLibraryIds = new[] { "Common" };
            model.ActionSet.StableId = "codex_flow_test.default";
            model.ActionSet.DisplayName = "codex flow test Default";
            model.ActionSet.CharacterStableId = CharacterStableId;
            model.ActionSet.EquipmentStateStableId = EquipmentStateStableId;
            model.SelectedActionIndex = 1;
            model.ReferenceContext.AddCharacterStableId(CharacterStableId);
            model.ReferenceContext.AddEquipmentStateStableId(EquipmentStateStableId);
            model.ReferenceContext.Normalize();
            return model;
        }

        private static void ConfigureMissingHitTraceResource(
            CharacterActionJsonDto action,
            string missingResource)
        {
            Assert.NotNull(action);
            action.DurationFrames = 8;
            int traceProfileId = string.Equals(missingResource, "profile", StringComparison.Ordinal)
                ? 0
                : CharacterTestBulkIds38.Id790040;
            action.CombatTrack = new CombatTrackJsonDto
            {
                CombatActionId = CharacterTestBulkIds38.Id790033,
                Events = new[]
                {
                    new CombatTrackEventJsonDto
                    {
                        Frame = 1,
                        EndFrame = 7,
                        Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                        EquipmentSlotId = CharacterTestBulkIds38.Id790007,
                        TraceProfileId = traceProfileId,
                        StableEventId = CharacterTestBulkIds38.Id790034,
                        HitEvent = new CharacterActionHitEventJsonDto
                        {
                            Damage = new CharacterActionHitDamageJsonDto
                            {
                                DamageTypeId = CharacterTestBulkIds38.Id790006,
                                BaseDamage = 1,
                                ImpactPower = 1,
                            },
                        },
                    },
                },
            };

            if (string.Equals(missingResource, "clip", StringComparison.Ordinal))
            {
                action.AnimationTrack = new AnimationTrackJsonDto
                {
                    Events = Array.Empty<AnimationTrackEventJsonDto>(),
                };
            }
            else if (string.Equals(missingResource, "baked", StringComparison.Ordinal))
            {
                action.AnimationTrack = new AnimationTrackJsonDto
                {
                    Events = new[]
                    {
                        new AnimationTrackEventJsonDto
                        {
                            Frame = 0,
                            EndFrame = 7,
                            Kind = CharacterActionTrackEventKind.CrossFadeAnimation.ToString(),
                            AnimationActionKey = "preview.noalloc.missing_baked_trace_clip",
                            StableEventId = CharacterTestBulkIds38.Id790035,
                        },
                    },
                };
            }
            else if (action.AnimationTrack?.Events != null)
            {
                for (int i = 0; i < action.AnimationTrack.Events.Length; i++)
                    action.AnimationTrack.Events[i].EndFrame = 7;
            }
        }
    }
}
