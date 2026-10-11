using System;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Core.Math;
using MxFramework.Combat.Projectiles;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline.Preview;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionPreviewRuntimeSessionTests
    {
        private const string CharacterStableId = "char.codex_flow_test";
        private const string CodexFlowTestCharacterStableId = "char.codex_flow_test";
        private const string EmptyEmptyState = "equip_state.codex_flow_test.empty_empty";
        private const string SwordEmptyState = "equip_state.codex_flow_test.sword_empty";
        private const string StandingJumpClipResourceKey = "art.character.skeleton.animation.standing_jump";
        private const string KatanaAtk1ClipResourceKey = "art.character.skeleton.animation.katana_atk1";
        private const string StandingJumpClipPath =
            "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_jump.anim";
        private const string CharacterPrefabPath =
            "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test/prefabs/codex_flow_test_character_preview.prefab";
        private const string CodexFlowTestCharacterPrefabPath =
            "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test/prefabs/codex_flow_test_character_preview.prefab";

        [TearDown]
        public void TearDown()
        {
            CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();
        }

        [Test]
        public void CleanupStalePreviewObjects_DestroysPreviewNamedObjectsOnly()
        {
            var previewRoot = new GameObject("[CharacterAction Preview] stale");
            var previewVfx = new GameObject("[角色动作预览 特效] stale");
            var ordinary = new GameObject("ordinary scene object");

            try
            {
                int destroyed = CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();

                Assert.GreaterOrEqual(destroyed, 2);
                Assert.IsTrue(previewRoot == null);
                Assert.IsTrue(previewVfx == null);
                Assert.IsFalse(ordinary == null);
            }
            finally
            {
                if (ordinary != null)
                    UnityEngine.Object.DestroyImmediate(ordinary);
            }
        }

        [Test]
        public void Dispose_DestroysOwnedPreviewInstance()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);

            var session = new CharacterActionPreviewRuntimeSession();
            GameObject preview = null;
            try
            {
                session.PreviewFrame(model, 0);
                preview = session.PreviewInstance;
            }
            finally
            {
                session.Dispose();
            }

            Assert.IsTrue(preview == null);
        }

        [Test]
        public void PreviewFrame_CodexFlowTestSwordEmpty_LoadsRuntimePrefabAndBinderWeapon()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                CharacterDefaultEquipmentRuntimeBinder binder =
                    session.PreviewInstance.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
                Assert.NotNull(binder);
                Assert.AreEqual(1, binder.SpawnedWeapons.Count);

                CharacterRuntimeLocomotionBlendController locomotion =
                    session.PreviewInstance.GetComponent<CharacterRuntimeLocomotionBlendController>();
                Assert.NotNull(locomotion);
                Assert.IsTrue(locomotion.HasAnimationBackend);
                Assert.AreEqual(KatanaAtk1ClipResourceKey, locomotion.LastActionKey);
                Assert.AreEqual(1, session.LastFrameAnimationRequestCount);
                Assert.NotNull(session.LastDebugSnapshot);
                Assert.AreEqual(CharacterTestActionIds.Action_light_attack, session.LastDebugSnapshot.ActionId);
                Assert.AreEqual(0, session.LastDebugSnapshot.LocalFrame);
            }
        }

        [Test]
        public void PreviewFrame_CodexFlowTestSwordEmpty_LoadsSaneSizedAnimatedPreview()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Bounds bodyBounds = CalculateBodyRendererBounds(session.PreviewInstance);
                Assert.Greater(bodyBounds.size.y, 1.2f, "codex_flow_test body should not be scaled down to a marker.");
                Assert.Less(bodyBounds.size.y, 2.4f, "codex_flow_test body should stay near humanoid world scale.");

                CharacterRuntimeLocomotionBlendController locomotion =
                    session.PreviewInstance.GetComponent<CharacterRuntimeLocomotionBlendController>();
                Assert.NotNull(locomotion);
                Assert.IsTrue(locomotion.HasAnimationBackend, session.DiagnosticsText);
                Assert.IsTrue(locomotion.LastActionAnimationResult.Success, session.DiagnosticsText);
                Assert.AreEqual(KatanaAtk1ClipResourceKey, locomotion.LastActionKey);
                Assert.AreEqual(1, session.LastFrameAnimationRequestCount, session.DiagnosticsText);

                CharacterDefaultEquipmentRuntimeBinder binder =
                    session.PreviewInstance.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
                Assert.NotNull(binder);
                Assert.AreEqual(1, binder.DefaultWeapons.Count, session.DiagnosticsText);
                Assert.AreEqual("weapon.common_weapon_1.model", binder.DefaultWeapons[0].ResourceId);
                Assert.AreEqual(1, binder.SpawnedWeapons.Count, session.DiagnosticsText);

                Transform rightHand = FindChildByName(session.PreviewInstance.transform, "RightHand");
                Assert.NotNull(rightHand, "codex_flow_test preview should expose RightHand for animation sampling.");
                Quaternion frame0Rotation = rightHand.rotation;

                session.PreviewFrame(model, 8);

                MxAnimationLayerDiagnostic baseLayer = FindBaseLayer(locomotion.CreateAnimationSnapshot());
                string currentClip = baseLayer != null ? baseLayer.CurrentClipKey.Id : "<none>";
                Assert.That(
                    Quaternion.Angle(frame0Rotation, rightHand.rotation),
                    Is.GreaterThan(0.5f),
                    "codex_flow_test preview should apply the project animation clip to the skeleton. currentClip=" + currentClip);
            }
        }

        [Test]
        public void PreviewFrame_CodexFlowTestSwordEmpty_LoadsWeaponLocatorPosePreview()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.AreEqual(0, session.ActiveHitTraceCount);
                Assert.AreEqual(1, session.WeaponLocatorPoseSnapshots.Count);
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewWeaponLocatorPoseSnapshot locator =
                    session.WeaponLocatorPoseSnapshots[0];
                Assert.AreEqual(CharacterTestNumericIds.Assert_trace_iron_sword_blade, locator.TraceProfileId);
                Assert.AreEqual(780001, locator.WeaponId);
                Assert.AreEqual(0, locator.EquipSlot);
                Assert.AreEqual("weapon/blade_root", locator.StartLocatorPath);
                Assert.AreEqual("weapon/blade_tip", locator.EndLocatorPath);
                Assert.AreEqual("ModelRoot", locator.StartParentKind);
                Assert.AreEqual(string.Empty, locator.StartParentPath);
                Assert.AreEqual("ModelRoot", locator.EndParentKind);
                Assert.AreEqual(string.Empty, locator.EndParentPath);
                Assert.AreEqual(0f, locator.StartLocalPosition.x, 0.0001f);
                Assert.AreEqual(0f, locator.StartLocalPosition.y, 0.0001f);
                Assert.AreEqual(0f, locator.StartLocalPosition.z, 0.0001f);
                Assert.AreEqual(0f, locator.EndLocalPosition.x, 0.0001f);
                Assert.AreEqual(1.071494f, locator.EndLocalPosition.y, 0.0001f);
                Assert.AreEqual(0.000843f, locator.EndLocalPosition.z, 0.0001f);
                Assert.AreEqual(0.055f, locator.Radius, 0.0001f);
                Assert.AreEqual("CapsuleSweep", locator.SampleRule);
            }
        }

        [Test]
        public void PreviewFrame_CodexFlowTestSwordEmpty_ResolvesLocatorPoseFromSpawnedWeaponModel()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.AreEqual(1, session.WeaponLocatorPoseSnapshots.Count);
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewWeaponLocatorPoseSnapshot locator =
                    session.WeaponLocatorPoseSnapshots[0];
                Assert.IsTrue(
                    session.TryResolveWeaponLocatorPose(
                        locator,
                        out CharacterActionPreviewRuntimeSession.CharacterActionPreviewWeaponLocatorWorldPose pose),
                    session.DiagnosticsText);

                CharacterDefaultEquipmentRuntimeBinder binder =
                    session.PreviewInstance.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
                Assert.NotNull(binder);
                Assert.GreaterOrEqual(binder.SpawnedWeapons.Count, 1);
                Transform swordTransform = null;
                for (int i = 0; i < binder.SpawnedWeapons.Count; i++)
                {
                    GameObject weapon = binder.SpawnedWeapons[i];
                    if (weapon != null && weapon.name.Contains("mainHand_sword"))
                    {
                        swordTransform = weapon.transform;
                        break;
                    }
                }

                if (swordTransform == null && binder.SpawnedWeapons.Count > 0)
                    swordTransform = binder.SpawnedWeapons[0].transform;
                Assert.NotNull(swordTransform);
                Transform modelTransform = swordTransform.Find("Model") ?? swordTransform;
                Assert.NotNull(modelTransform);

                Transform rootLocator = FindChildByPathOrName(swordTransform, locator.StartLocatorPath);
                Transform tipLocator = FindChildByPathOrName(swordTransform, locator.EndLocatorPath);
                Transform startParent = ResolveLocatorParent(
                    session.PreviewInstance.transform,
                    locator.StartParentKind,
                    locator.StartParentPath);
                Transform endParent = ResolveLocatorParent(
                    session.PreviewInstance.transform,
                    locator.EndParentKind,
                    locator.EndParentPath);
                bool usesWeaponLocator = rootLocator != null && tipLocator != null;
                bool usesConfiguredParent = !usesWeaponLocator && startParent != null && endParent != null;
                Vector3 expectedRoot = usesWeaponLocator
                    ? rootLocator.position
                    : usesConfiguredParent
                        ? startParent.TransformPoint(locator.StartLocalPosition)
                        : modelTransform.position + (modelTransform.rotation * locator.StartLocalPosition);
                Vector3 expectedTip = usesWeaponLocator
                    ? tipLocator.position
                    : usesConfiguredParent
                        ? endParent.TransformPoint(locator.EndLocalPosition)
                        : modelTransform.position + (modelTransform.rotation * locator.EndLocalPosition);
                Assert.AreEqual(expectedRoot.x, pose.Root.x, 0.0001f);
                Assert.AreEqual(expectedRoot.y, pose.Root.y, 0.0001f);
                Assert.AreEqual(expectedRoot.z, pose.Root.z, 0.0001f);
                Assert.AreEqual(expectedTip.x, pose.Tip.x, 0.0001f);
                Assert.AreEqual(expectedTip.y, pose.Tip.y, 0.0001f);
                Assert.AreEqual(expectedTip.z, pose.Tip.z, 0.0001f);
                Assert.AreEqual(!usesConfiguredParent, pose.IsUsingSpawnedWeapon);
                Assert.AreEqual(
                    usesWeaponLocator ? "weaponLocator" : usesConfiguredParent ? "configuredParent" : "weaponModelFallback",
                    pose.Source);
            }
        }

        [Test]
        public void PreviewFrame_CodexFlowTestEmptyEmpty_DoesNotFallbackWeaponTraceToCharacterRoot()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(EmptyEmptyState);
            ConfigureWeaponTracePreview(model.SelectedAction);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                CharacterDefaultEquipmentRuntimeBinder binder =
                    session.PreviewInstance.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
                Assert.NotNull(binder);
                Assert.AreEqual(0, binder.SpawnedWeapons.Count, session.DiagnosticsText);
                Assert.AreEqual(1, session.WeaponLocatorPoseSnapshots.Count, session.DiagnosticsText);
                Assert.IsFalse(
                    session.TryResolveWeaponLocatorPose(
                        session.WeaponLocatorPoseSnapshots[0],
                        out CharacterActionPreviewRuntimeSession.CharacterActionPreviewWeaponLocatorWorldPose _),
                    session.DiagnosticsText);
                Assert.AreEqual(0, session.DrawnWeaponTraceCount, session.DiagnosticsText);
                StringAssert.Contains("could not be resolved from the spawned weapon", session.DiagnosticsText);
            }
        }

        [Test]
        public void PreviewFrame_CodexFlowTestSwordEmpty_NormalizesTraceToSpawnedWeaponRoot()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.AreEqual(1, session.WeaponLocatorPoseSnapshots.Count, session.DiagnosticsText);
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewWeaponLocatorPoseSnapshot locator =
                    session.WeaponLocatorPoseSnapshots[0];
                Assert.AreEqual(CharacterTestNumericIds.Assert_trace_iron_sword_blade, locator.TraceProfileId);
                Assert.AreEqual(0, locator.EquipSlot);
                Assert.IsTrue(
                    session.TryResolveWeaponLocatorPose(
                        locator,
                        out CharacterActionPreviewRuntimeSession.CharacterActionPreviewWeaponLocatorWorldPose pose),
                    session.DiagnosticsText);

                CharacterDefaultEquipmentRuntimeBinder binder =
                    session.PreviewInstance.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
                Assert.NotNull(binder);
                Assert.GreaterOrEqual(binder.SpawnedWeapons.Count, 1);
                Transform weapon = binder.SpawnedWeapons[0].transform;
                Transform rootLocator = FindChildByPathOrName(weapon, locator.StartLocatorPath);
                Transform tipLocator = FindChildByPathOrName(weapon, locator.EndLocatorPath);
                Assert.NotNull(rootLocator);
                Assert.NotNull(tipLocator);

                Vector3 expectedRoot = rootLocator.position;
                Vector3 expectedTip = tipLocator.position;
                AssertVector(expectedRoot, pose.Root, 0.0001f);
                AssertVector(expectedTip, pose.Tip, 0.0001f);
                Assert.IsTrue(pose.IsUsingSpawnedWeapon);
                Assert.AreEqual("weaponLocator", pose.Source);
            }
        }

        [Test]
        public void PreviewFrame_LightAttackRootMotion_MovesPreviewInstance()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 1);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Vector3 position = session.PreviewInstance.transform.position;
                Assert.Greater(new Vector2(position.x, position.z).magnitude, 0.0001f);
            }
        }

        [Test]
        public void PreviewFrame_HitVolumeWindow_DrawsActionCapsuleInsteadOfWeaponTrace()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            ConfigureHitVolumePreview(model.SelectedAction);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.AreEqual(1, session.ActiveHitWindowCount, session.DiagnosticsText);
                Assert.AreEqual(0, session.DrawnWeaponTraceCount, session.DiagnosticsText);
                Assert.AreEqual(1, session.DrawnHitVolumeCount, session.DiagnosticsText);
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewHitVolumeSnapshot snapshot =
                    session.HitVolumeSnapshots[0];
                Assert.AreEqual(CharacterTestNumericIds.Assert_preview_hit_volume_start, snapshot.StableEventId);
                Assert.AreEqual(CharacterActionHitVolumeShapeKind.Capsule, snapshot.ShapeKind);
                Assert.AreEqual(CharacterActionHitVolumeSourceKind.ManualCapsule, snapshot.SourceKind);
                Assert.AreEqual(0f, snapshot.PointA.x, 0.0001f);
                Assert.AreEqual(0.8f, snapshot.PointA.y, 0.0001f);
                Assert.AreEqual(0.6f, snapshot.PointA.z, 0.0001f);
                Assert.AreEqual(0f, snapshot.PointB.x, 0.0001f);
                Assert.AreEqual(0.8f, snapshot.PointB.y, 0.0001f);
                Assert.AreEqual(1.8f, snapshot.PointB.z, 0.0001f);
                Assert.AreEqual(0.65f, snapshot.Radius, 0.0001f);

                session.PreviewFrame(model, 1);

                Assert.AreEqual(0, session.ActiveHitWindowCount, session.DiagnosticsText);
                Assert.AreEqual(1, session.DrawnHitVolumeCount, session.DiagnosticsText);

                session.PreviewFrame(model, 2);

                Assert.AreEqual(0, session.ActiveHitWindowCount, session.DiagnosticsText);
                Assert.AreEqual(0, session.DrawnHitVolumeCount, session.DiagnosticsText);
                Assert.AreEqual(0, session.DrawnWeaponTraceCount, session.DiagnosticsText);
            }
        }

        [Test]
        public void PreviewFrame_CasterBodyHitVolume_DrawsBodyCapsuleInsteadOfWeaponTrace()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            ConfigureCasterBodyHitVolumePreview(model.SelectedAction);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.AreEqual(1, session.ActiveHitWindowCount, session.DiagnosticsText);
                Assert.AreEqual(0, session.DrawnWeaponTraceCount, session.DiagnosticsText);
                Assert.AreEqual(1, session.DrawnHitVolumeCount, session.DiagnosticsText);
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewHitVolumeSnapshot snapshot =
                    session.HitVolumeSnapshots[0];
                Assert.AreEqual(CharacterTestNumericIds.Assert_preview_caster_body_start, snapshot.StableEventId);
                Assert.AreEqual(CharacterActionHitVolumeShapeKind.Capsule, snapshot.ShapeKind);
                Assert.AreEqual(CharacterActionHitVolumeSourceKind.CasterBody, snapshot.SourceKind);
                Assert.AreEqual(0.34f, snapshot.Radius, 0.0001f);
                Assert.AreEqual(0f, snapshot.PointA.x, 0.0001f);
                Assert.AreEqual(0.34f, snapshot.PointA.y, 0.0001f);
                Assert.AreEqual(0f, snapshot.PointA.z, 0.0001f);
                Assert.AreEqual(0f, snapshot.PointB.x, 0.0001f);
                Assert.AreEqual(1.52f, snapshot.PointB.y, 0.0001f);
                Assert.AreEqual(0f, snapshot.PointB.z, 0.0001f);

                session.PreviewFrame(model, 1);

                Assert.AreEqual(0, session.ActiveHitWindowCount, session.DiagnosticsText);
                Assert.AreEqual(1, session.DrawnHitVolumeCount, session.DiagnosticsText);
            }
        }

        [Test]
        public void PreviewFrame_CasterBodyHitVolume_AppliesBodyModifiers()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            ConfigureCasterBodyHitVolumePreview(model.SelectedAction);
            model.SelectedAction.CombatTrack.Events[0].HitVolume.BodyOffset = new CharacterActionHitVolumePointJsonDto
            {
                Y = 0.1f,
                Z = 0.2f,
            };
            model.SelectedAction.CombatTrack.Events[0].HitVolume.BodyRadiusScale = 1.25f;
            model.SelectedAction.CombatTrack.Events[0].HitVolume.BodyRadiusAdd = 0.05f;
            model.SelectedAction.CombatTrack.Events[0].HitVolume.BodyHeightScale = 1f;
            model.SelectedAction.CombatTrack.Events[0].HitVolume.BodyHeightAdd = 0.2f;

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                Assert.AreEqual(1, session.DrawnHitVolumeCount, session.DiagnosticsText);
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewHitVolumeSnapshot snapshot =
                    session.HitVolumeSnapshots[0];
                Assert.AreEqual(CharacterActionHitVolumeSourceKind.CasterBody, snapshot.SourceKind);
                Assert.AreEqual(0.475f, snapshot.Radius, 0.0001f);
                Assert.AreEqual(0f, snapshot.PointA.x, 0.0001f);
                Assert.AreEqual(0.475f, snapshot.PointA.y, 0.0001f);
                Assert.AreEqual(0.2f, snapshot.PointA.z, 0.0001f);
                Assert.AreEqual(0f, snapshot.PointB.x, 0.0001f);
                Assert.AreEqual(1.585f, snapshot.PointB.y, 0.0001f);
                Assert.AreEqual(0.2f, snapshot.PointB.z, 0.0001f);
            }
        }

        [Test]
        public void PreviewFrame_WeaponTraceWindow_StillDrawsWeaponTrace()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            ConfigureWeaponTracePreview(model.SelectedAction);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.AreEqual(1, session.ActiveHitWindowCount, session.DiagnosticsText);
                Assert.AreEqual(1, session.DrawnWeaponTraceCount, session.DiagnosticsText);
                Assert.AreEqual(0, session.DrawnHitVolumeCount, session.DiagnosticsText);
                Assert.AreEqual(CharacterTestNumericIds.Assert_trace_iron_sword_blade, session.WeaponTraceSnapshots[0].TraceProfileId);
                Assert.AreEqual(1, session.WeaponLocatorPoseSnapshots.Count, session.DiagnosticsText);
                Assert.IsTrue(
                    session.TryResolveWeaponLocatorPose(
                        session.WeaponLocatorPoseSnapshots[0],
                        out CharacterActionPreviewRuntimeSession.CharacterActionPreviewWeaponLocatorWorldPose pose),
                    session.DiagnosticsText);

                var traceFrame = session.WeaponTraceSnapshots[0].TraceFrame;
                AssertVector(pose.Root, ToVector3(traceFrame.RootNow), 0.0001f);
                AssertVector(pose.Tip, ToVector3(traceFrame.TipNow), 0.0001f);
            }
        }

        [Test]
        public void PreviewFrame_BasicJump_DispatchesMotionWithoutIntegratingAtFrameZero()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            model.SelectedActionIndex = 0;

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.AreEqual(1, session.LastFrameMotionRequestCount);
                Assert.AreEqual(1, session.LastFrameAnimationRequestCount);
                Assert.AreEqual(CharacterMovementMode.Airborne, session.PreviewMovementMode);
                Assert.AreEqual(0f, session.PreviewInstance.transform.position.y, 0.0001f);
                Assert.IsTrue(session.PreviewGrounded);
            }
        }

        [Test]
        public void PreviewFrame_BasicJump_IntegratesFrameZeroImpulseWhenAdvancingToFrameOne()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            model.SelectedActionIndex = 0;

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 1);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.Greater(session.PreviewInstance.transform.position.y, 0f);
                Assert.IsFalse(session.PreviewGrounded);
            }
        }

        [Test]
        public void PreviewFrame_DirectAnimationClipResourceKey_CrossFadesClip()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            model.SelectedAction.AnimationTrack.Events[0].AnimationActionKey = string.Empty;
            model.SelectedAction.AnimationTrack.Events[0].AnimationClipResourceKey = StandingJumpClipResourceKey;

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                CharacterRuntimeLocomotionBlendController locomotion =
                    session.PreviewInstance.GetComponent<CharacterRuntimeLocomotionBlendController>();
                Assert.NotNull(locomotion);
                Assert.IsTrue(locomotion.LastActionAnimationResult.Success, session.DiagnosticsText);
                Assert.AreEqual(StandingJumpClipResourceKey, locomotion.LastActionKey);
            }
        }

        [Test]
        public void PreviewFrame_DirectAnimationClipResourceKey_KeepsActionClipDuringEventSpan()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            AnimationTrackEventJsonDto evt = model.SelectedAction.AnimationTrack.Events[0];
            evt.AnimationActionKey = string.Empty;
            evt.AnimationClipResourceKey = StandingJumpClipResourceKey;
            evt.EndFrame = 30;

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 1);

                CharacterRuntimeLocomotionBlendController locomotion =
                    session.PreviewInstance.GetComponent<CharacterRuntimeLocomotionBlendController>();
                Assert.NotNull(locomotion);
                Assert.IsTrue(locomotion.IsLocomotionBlendSuppressed, session.DiagnosticsText);
                MxAnimationLayerDiagnostic baseLayer = FindBaseLayer(locomotion.CreateAnimationSnapshot());
                Assert.NotNull(baseLayer, session.DiagnosticsText);
                Assert.AreEqual(StandingJumpClipResourceKey, baseLayer.CurrentClipKey.Id);
            }
        }

        [Test]
        public void PreviewFrame_DelayedAnimationEvent_CrossFadesOnlyAtEventFrame()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            AnimationTrackEventJsonDto evt = model.SelectedAction.AnimationTrack.Events[0];
            evt.Frame = 2;
            evt.EndFrame = 30;
            evt.AnimationActionKey = string.Empty;
            evt.AnimationClipResourceKey = StandingJumpClipResourceKey;

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 1);
                Assert.AreEqual(0, session.LastFrameAnimationRequestCount, session.DiagnosticsText);

                session.PreviewFrame(model, 2);
                Assert.AreEqual(1, session.LastFrameAnimationRequestCount, session.DiagnosticsText);
                CharacterRuntimeLocomotionBlendController locomotion =
                    session.PreviewInstance.GetComponent<CharacterRuntimeLocomotionBlendController>();
                Assert.NotNull(locomotion);
                Assert.AreEqual(StandingJumpClipResourceKey, locomotion.LastActionKey);
            }
        }

        [Test]
        public void PreviewFrame_ScrubBeforeDelayedAnimationEvent_ClearsPreviousActionClip()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            AnimationTrackEventJsonDto evt = model.SelectedAction.AnimationTrack.Events[0];
            evt.Frame = 2;
            evt.EndFrame = 30;
            evt.AnimationActionKey = string.Empty;
            evt.AnimationClipResourceKey = StandingJumpClipResourceKey;

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 2);
                CharacterRuntimeLocomotionBlendController locomotion =
                    session.PreviewInstance.GetComponent<CharacterRuntimeLocomotionBlendController>();
                Assert.NotNull(locomotion);
                Assert.AreEqual(StandingJumpClipResourceKey, locomotion.LastActionKey);

                session.PreviewFrame(model, 1);

                Assert.AreEqual(0, session.LastFrameAnimationRequestCount, session.DiagnosticsText);
                Assert.AreEqual(string.Empty, locomotion.LastActionKey);
                Assert.IsFalse(locomotion.IsLocomotionBlendSuppressed);
            }
        }

        [Test]
        public void PreviewBindingDiscovery_DroppedAnimationClip_ResolvesGeneratedResourceKey()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(StandingJumpClipPath);
            Assert.NotNull(clip, "Missing generated source clip fixture: " + StandingJumpClipPath);

            bool resolved = CharacterActionPreviewBindingDiscovery.TryFindAnimationClipChoice(
                clip,
                CharacterStableId,
                out CharacterActionAnimationClipChoice choice);

            Assert.IsTrue(resolved);
            Assert.AreEqual(StandingJumpClipResourceKey, choice.ResourceKey);
        }

        [Test]
        public void PreviewBindingDiscovery_ListsGeneratedCharacterPackages()
        {
            CharacterActionPreviewBindingChoice[] choices = CharacterActionPreviewBindingDiscovery.DiscoverCharacters();

            AssertContainsBindingChoice(choices, "char.codex_flow_test");
            AssertContainsBindingChoice(choices, "char.codex_flow_test");
            AssertContainsBindingChoice(choices, "char.codex_flow_test");
        }

        [Test]
        public void PreviewFrame_DispatchesNonAnimationTrackRequests()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            CharacterActionJsonDto action = model.SelectedAction;
            action.CombatTrack = new CombatTrackJsonDto
            {
                CombatActionId = CharacterTestBulkIds38.Id790042,
                Events = new[]
                {
                    new CombatTrackEventJsonDto
                    {
                        Frame = 0,
                        Kind = CharacterActionTrackEventKind.StartCombatAction.ToString(),
                        CombatActionId = CharacterTestBulkIds38.Id790042,
                        StableEventId = CharacterTestBulkIds38.Id790043,
                    },
                },
            };
            action.GameplayTrack = new GameplayTrackJsonDto
            {
                Events = new[]
                {
                    new GameplayTrackEventJsonDto
                    {
                        Frame = 0,
                        Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                        RequestId = CharacterTestBulkIds38.Id790044,
                        StableEventId = CharacterTestBulkIds38.Id790045,
                    },
                },
            };
            action.PresentationTrack = new PresentationTrackJsonDto
            {
                Events = new[]
                {
                    new PresentationTrackEventJsonDto
                    {
                        Frame = 0,
                        Kind = CharacterActionTrackEventKind.PlayAudioCue.ToString(),
                        CueId = CharacterTestBulkIds38.Id790046,
                        StableEventId = CharacterTestBulkIds38.Id790047,
                    },
                    new PresentationTrackEventJsonDto
                    {
                        Frame = 0,
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = "vfx.light_attack",
                        StableEventId = CharacterTestBulkIds38.Id790048,
                    },
                },
            };
            action.DebugTrack = new DebugTrackJsonDto
            {
                Events = new[]
                {
                    new DebugTrackEventJsonDto
                    {
                        Frame = 0,
                        Kind = CharacterActionTrackEventKind.EmitDebugMarker.ToString(),
                        MarkerId = "debug.preview.marker",
                        StableEventId = CharacterTestBulkIds38.Id790049,
                    },
                },
            };

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.AreEqual(1, session.LastFrameCombatRequestCount, session.DiagnosticsText);
                Assert.AreEqual(1, session.LastFrameGameplayRequestCount, session.DiagnosticsText);
                Assert.AreEqual(1, session.LastFrameAudioCueRequestCount, session.DiagnosticsText);
                Assert.AreEqual(1, session.LastFrameVfxRequestCount, session.DiagnosticsText);
                Assert.AreEqual(1, session.LastFrameDebugDispatchCount, session.DiagnosticsText);
            }
        }

        [Test]
        public void PreviewFrame_SpawnVisualCue_CreatesFollowingVfxPreviewForFrameRange()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            CharacterActionJsonDto action = model.SelectedAction;
            ExtendSelectedActionForPreview(action);
            action.PresentationTrack = new PresentationTrackJsonDto
            {
                Events = new[]
                {
                    new PresentationTrackEventJsonDto
                    {
                        Frame = 0,
                        EndFrame = 5,
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = "vfx.magic.fireball.charge",
                        VfxPlaybackMode = CharacterActionVfxPlaybackMode.StopAtEventEnd.ToString(),
                        VfxPlaybackRate = 0.5f,
                        VfxScaleMultiplier = 1.5f,
                        StableEventId = CharacterTestBulkIds38.Id790050,
                    },
                },
            };

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.AreEqual(1, session.ActiveVfxPreviewCount, session.DiagnosticsText);
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewVfxSnapshot snapshot =
                    session.VfxPreviewSnapshots[0];
                Vector3 expected = session.PreviewInstance.transform.position
                    + session.PreviewInstance.transform.forward * 0.65f
                    + Vector3.up * 1.15f;
                Assert.AreEqual(0, snapshot.StartFrame);
                Assert.AreEqual(5, snapshot.EndFrame);
                Assert.AreEqual(expected.x, snapshot.Position.x, 0.0001f);
                Assert.AreEqual(expected.y, snapshot.Position.y, 0.0001f);
                Assert.AreEqual(expected.z, snapshot.Position.z, 0.0001f);
                Assert.IsTrue(snapshot.HasPrefabView, session.DiagnosticsText);

                UnityEngine.Object previousSelection = Selection.activeObject;
                var selectionSentinel = new GameObject("AttachmentLocateSelectionSentinel");
                try
                {
                    Selection.activeObject = selectionSentinel;
                    Assert.IsTrue(session.LocateAttachmentSocket(string.Empty));
                    Assert.AreSame(
                        selectionSentinel,
                        Selection.activeObject,
                        "定位临时预览挂点不得把即将销毁的 Preview 对象放入 Inspector。");
                }
                finally
                {
                    Selection.activeObject = previousSelection;
                    UnityEngine.Object.DestroyImmediate(selectionSentinel);
                }

                session.PreviewFrame(model, 6);

                Assert.AreEqual(0, session.ActiveVfxPreviewCount, session.DiagnosticsText);
            }
        }

        [Test]
        public void PreviewFrame_ProjectileGameplayRequest_CreatesLightweightFlightPreview()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            CharacterActionJsonDto action = model.SelectedAction;
            ConfigureFireballProjectilePreview(action);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 6);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.AreEqual(1, session.ActiveProjectilePreviewCount, session.DiagnosticsText);
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewProjectileSnapshot atSpawn =
                    session.ProjectilePreviewSnapshots[0];
                Assert.AreEqual(CharacterTestNumericIds.Assert_character_test_fireball_projectile, atSpawn.RequestId);
                Assert.AreEqual(91001, atSpawn.DefinitionId);
                Assert.AreEqual(6, atSpawn.StartFrame);
                Assert.AreEqual(atSpawn.Origin.x, atSpawn.Position.x, 0.0001f);
                Assert.AreEqual(atSpawn.Origin.y, atSpawn.Position.y, 0.0001f);
                Assert.AreEqual(atSpawn.Origin.z, atSpawn.Position.z, 0.0001f);
                Assert.AreEqual(0.35f, atSpawn.SpeedPerFrame, 0.0001f);
                Assert.AreEqual(0.25f, atSpawn.Radius, 0.0001f);
                StringAssert.Contains("mainHand", atSpawn.OriginLabel);
                Assert.IsTrue(atSpawn.HasPrefabView, session.DiagnosticsText);

                session.PreviewFrame(model, 7);

                CharacterActionPreviewRuntimeSession.CharacterActionPreviewProjectileSnapshot moved =
                    session.ProjectilePreviewSnapshots[0];
                Vector3 expected = moved.Origin + moved.Direction * 0.35f;
                Assert.AreEqual(expected.x, moved.Position.x, 0.0001f);
                Assert.AreEqual(expected.y, moved.Position.y, 0.0001f);
                Assert.AreEqual(expected.z, moved.Position.z, 0.0001f);
            }
        }

        [Test]
        public void PreviewFrame_ArcProjectileGameplayRequest_SamplesCurve()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            ConfigureProjectilePreview(model.SelectedAction, 23);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 51);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.AreEqual(1, session.ActiveProjectilePreviewCount, session.DiagnosticsText);
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewProjectileSnapshot snapshot =
                    session.ProjectilePreviewSnapshots[0];
                Assert.AreEqual(CharacterTestNumericIds.Assert_character_test_fireball_lob_projectile, snapshot.RequestId);
                Assert.AreEqual(91004, snapshot.DefinitionId);
                Assert.AreEqual(CombatProjectileMotionKind.ArcToTarget, snapshot.MotionKind);
                Assert.AreEqual(4f, snapshot.ArcHeight, 0.0001f);

                Vector3 linearMid = Vector3.LerpUnclamped(snapshot.Origin, snapshot.TargetPoint, 0.5f);
                Assert.AreEqual(linearMid.x, snapshot.Position.x, 0.0001f);
                Assert.AreEqual(linearMid.y + 4f, snapshot.Position.y, 0.0001f);
                Assert.AreEqual(linearMid.z, snapshot.Position.z, 0.0001f);
            }
        }

        [Test]
        public void PreviewFrame_SpeedProfileProjectileGameplayRequest_SamplesEaseInProgress()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            ConfigureProjectilePreview(model.SelectedAction, 25);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 51);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.AreEqual(1, session.ActiveProjectilePreviewCount, session.DiagnosticsText);
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewProjectileSnapshot snapshot =
                    session.ProjectilePreviewSnapshots[0];
                Assert.AreEqual(CharacterTestNumericIds.Assert_character_test_fireball_surge_projectile, snapshot.RequestId);
                Assert.AreEqual(91005, snapshot.DefinitionId);
                Assert.AreEqual(CombatProjectileSpeedProfileKind.EaseInQuadratic, snapshot.SpeedProfileKind);
                float traveled = Vector3.Dot(snapshot.Position - snapshot.Origin, snapshot.Direction);
                Assert.AreEqual(7.875f, traveled, 0.001f);
            }
        }

        [Test]
        public void PreviewFrame_ReturnAndSwayProjectileGameplayRequests_PreserveMotionConfig()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            ConfigureProjectilePreview(model.SelectedAction, 27);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 51);

                Assert.AreEqual(1, session.ActiveProjectilePreviewCount, session.DiagnosticsText);
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewProjectileSnapshot snapshot =
                    session.ProjectilePreviewSnapshots[0];
                Assert.AreEqual(CharacterTestNumericIds.Assert_character_test_fireball_return_projectile, snapshot.RequestId);
                Assert.AreEqual(91006, snapshot.DefinitionId);
                Assert.AreEqual(CombatProjectileMotionKind.BoomerangReturn, snapshot.MotionKind);
            }

            ConfigureProjectilePreview(model.SelectedAction, 29);
            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 17);

                Assert.AreEqual(1, session.ActiveProjectilePreviewCount, session.DiagnosticsText);
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewProjectileSnapshot snapshot =
                    session.ProjectilePreviewSnapshots[0];
                Assert.AreEqual(CharacterTestNumericIds.Assert_character_test_fireball_sway_projectile, snapshot.RequestId);
                Assert.AreEqual(91007, snapshot.DefinitionId);
                Assert.AreEqual(CombatProjectileMotionKind.SwayForward, snapshot.MotionKind);
                Assert.AreEqual(0.75f, snapshot.LateralAmplitude, 0.0001f);
                Assert.AreEqual(2, snapshot.LateralCycles);
            }
        }

        [Test]
        public void PreviewFrame_HomingProjectileGameplayRequest_PreservesMotionConfig()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            ConfigureProjectilePreview(model.SelectedAction, 31);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 17);

                Assert.AreEqual(1, session.ActiveProjectilePreviewCount, session.DiagnosticsText);
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewProjectileSnapshot snapshot =
                    session.ProjectilePreviewSnapshots[0];
                Assert.AreEqual(CharacterTestNumericIds.Assert_character_test_fireball_homing_projectile, snapshot.RequestId);
                Assert.AreEqual(91008, snapshot.DefinitionId);
                Assert.AreEqual(CombatProjectileMotionKind.HomingToTarget, snapshot.MotionKind);
                Assert.AreEqual(0.15f, snapshot.HomingTurnRatePerFrame, 0.0001f);
            }
        }

        [Test]
        public void PreviewFrame_BezierProjectileGameplayRequest_PreservesMotionConfig()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            ConfigureProjectilePreview(model.SelectedAction, 33);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 17);

                Assert.AreEqual(1, session.ActiveProjectilePreviewCount, session.DiagnosticsText);
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewProjectileSnapshot snapshot =
                    session.ProjectilePreviewSnapshots[0];
                Assert.AreEqual(CharacterTestNumericIds.Assert_character_test_fireball_bezier_projectile, snapshot.RequestId);
                Assert.AreEqual(91009, snapshot.DefinitionId);
                Assert.AreEqual(CombatProjectileMotionKind.BezierToTarget, snapshot.MotionKind);
                Assert.AreEqual(new Vector3(0f, 3f, 6f), snapshot.BezierControlOffsetA);
                Assert.AreEqual(new Vector3(0f, 2f, -4f), snapshot.BezierControlOffsetB);
                Assert.Greater(snapshot.Position.y, snapshot.Origin.y);
            }
        }

        [Test]
        public void PreviewFrame_MissingVfxResourceOrProjectileDefinition_ProducesDiagnosticsWithoutThrowing()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            CharacterActionJsonDto action = model.SelectedAction;
            ExtendSelectedActionForPreview(action);
            action.PresentationTrack = new PresentationTrackJsonDto
            {
                Events = new[]
                {
                    new PresentationTrackEventJsonDto
                    {
                        Frame = 0,
                        EndFrame = 1,
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = "vfx.preview.missing",
                        StableEventId = CharacterTestBulkIds38.Id790051,
                    },
                },
            };
            action.GameplayTrack = new GameplayTrackJsonDto
            {
                Events = new[]
                {
                    new GameplayTrackEventJsonDto
                    {
                        Frame = 0,
                        Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                        RequestId = CharacterTestBulkIds38.Id790052,
                        StableEventId = CharacterTestBulkIds38.Id790053,
                    },
                },
            };

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                Assert.DoesNotThrow(() => session.PreviewFrame(model, 0));

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.AreEqual(1, session.ActiveVfxPreviewCount, session.DiagnosticsText);
                Assert.IsFalse(session.VfxPreviewSnapshots[0].HasPrefabView);
                Assert.AreEqual(0, session.ActiveProjectilePreviewCount);
                StringAssert.Contains("Prefab 加载失败", session.DiagnosticsText);
                StringAssert.Contains("投射物请求找不到对应定义", session.DiagnosticsText);
            }
        }

        [Test]
        public void PreviewFrame_ProjectileScrubReplayMatchesSequentialPlaybackPosition()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);
            ConfigureFireballProjectilePreview(model.SelectedAction);

            using (var replayed = new CharacterActionPreviewRuntimeSession())
            using (var sequential = new CharacterActionPreviewRuntimeSession())
            {
                replayed.PreviewFrame(model, 7);
                sequential.PreviewFrame(model, 6);
                sequential.PreviewFrame(model, 7);

                CharacterActionPreviewRuntimeSession.CharacterActionPreviewProjectileSnapshot replayedSnapshot =
                    replayed.ProjectilePreviewSnapshots[0];
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewProjectileSnapshot sequentialSnapshot =
                    sequential.ProjectilePreviewSnapshots[0];
                Assert.AreEqual(sequentialSnapshot.Position.x, replayedSnapshot.Position.x, 0.0001f);
                Assert.AreEqual(sequentialSnapshot.Position.y, replayedSnapshot.Position.y, 0.0001f);
                Assert.AreEqual(sequentialSnapshot.Position.z, replayedSnapshot.Position.z, 0.0001f);
                Assert.AreEqual(sequentialSnapshot.RequestId, replayedSnapshot.RequestId);
                Assert.AreEqual(sequentialSnapshot.DefinitionId, replayedSnapshot.DefinitionId);
            }
        }

        [Test]
        public void PreviewFrame_CodexFlowTestSwordEmpty_UsesSelectedEquipmentStateLoadout()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);

            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                session.PreviewFrame(model, 0);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                CharacterDefaultEquipmentRuntimeBinder binder =
                    session.PreviewInstance.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
                Assert.NotNull(binder);
                Assert.AreEqual(1, binder.DefaultWeapons.Count);
                Assert.AreEqual(780001, binder.DefaultWeapons[0].WeaponId);
                Assert.AreEqual(1, binder.DefaultWeapons[0].EquipSlot);
                Assert.AreEqual("weapon.common_weapon_1.model", binder.DefaultWeapons[0].ResourceId);
                Assert.AreEqual(1, binder.SpawnedWeapons.Count);
                Assert.NotNull(binder.SpawnedWeapons[0]);
            }
        }

        [Test]
        public void PreviewFrame_ScrubReplayMatchesSequentialPlaybackSnapshot()
        {
            AssertGeneratedCodexFlowTestPackageExists();
            CharacterActionAuthoringModel model = CreateCodexFlowTestModel(SwordEmptyState);

            using (var replayed = new CharacterActionPreviewRuntimeSession())
            using (var sequential = new CharacterActionPreviewRuntimeSession())
            {
                replayed.PreviewFrame(model, 3);
                for (int frame = 0; frame <= 3; frame++)
                    sequential.PreviewFrame(model, frame);

                Assert.NotNull(replayed.LastDebugSnapshot, replayed.DiagnosticsText);
                Assert.NotNull(sequential.LastDebugSnapshot, sequential.DiagnosticsText);
                Assert.AreEqual(sequential.LastDebugSnapshot.ActionId, replayed.LastDebugSnapshot.ActionId);
                Assert.AreEqual(sequential.LastDebugSnapshot.LocalFrame, replayed.LastDebugSnapshot.LocalFrame);
                Assert.AreEqual(sequential.LastDebugSnapshot.CurrentPhase, replayed.LastDebugSnapshot.CurrentPhase);
                CollectionAssert.AreEqual(
                    sequential.LastDebugSnapshot.FiredEventsThisFrame,
                    replayed.LastDebugSnapshot.FiredEventsThisFrame);
            }
        }

        private static CharacterActionAuthoringModel CreateCodexFlowTestModel(string equipmentStateStableId)
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            model.Profile.StableId = "codex_flow_test.default";
            model.Profile.DisplayName = "codex flow test Default";
            model.Profile.ActionSetIds = new[] { "codex_flow_test.default" };
            model.Profile.ActionLibraryIds = new[] { "Common" };
            model.ActionSet.StableId = "codex_flow_test.default";
            model.ActionSet.DisplayName = "codex flow test Default";
            model.ActionSet.CharacterStableId = CodexFlowTestCharacterStableId;
            model.ActionSet.EquipmentStateStableId = equipmentStateStableId;
            model.SelectedActionIndex = 1;
            model.ReferenceContext.AddCharacterStableId(CodexFlowTestCharacterStableId);
            model.ReferenceContext.AddEquipmentStateStableId(equipmentStateStableId);
            model.ReferenceContext.Normalize();
            return model;
        }

        private static void ConfigureFireballProjectilePreview(CharacterActionJsonDto action)
        {
            ConfigureProjectilePreview(action, 17);
        }

        private static void ConfigureHitVolumePreview(CharacterActionJsonDto action)
        {
            ExtendSelectedActionForPreview(action);
            action.CombatTrack = new CombatTrackJsonDto
            {
                CombatActionId = CharacterTestBulkIds38.Id790036,
                Events = new[]
                {
                    new CombatTrackEventJsonDto
                    {
                        Frame = 0,
                        EndFrame = 1,
                        Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                        StableEventId = CharacterTestBulkIds38.Id790037,
                        HitEvent = new CharacterActionHitEventJsonDto
                        {
                            Damage = new CharacterActionHitDamageJsonDto
                            {
                                DamageTypeId = CharacterTestBulkIds38.Id790006,
                                BaseDamage = 1,
                                ImpactPower = 1,
                            },
                        },
                        HitVolume = new CharacterActionHitVolumeJsonDto
                        {
                            ShapeKind = CharacterActionHitVolumeShapeKind.Capsule.ToString(),
                            LocalPointA = new CharacterActionHitVolumePointJsonDto
                            {
                                Y = 0.8f,
                                Z = 0.6f,
                            },
                            LocalPointB = new CharacterActionHitVolumePointJsonDto
                            {
                                Y = 0.8f,
                                Z = 1.8f,
                            },
                            Radius = 0.65f,
                        },
                    },
                },
            };
        }

        private static void ConfigureCasterBodyHitVolumePreview(CharacterActionJsonDto action)
        {
            ExtendSelectedActionForPreview(action);
            action.CombatTrack = new CombatTrackJsonDto
            {
                CombatActionId = CharacterTestBulkIds38.Id790038,
                Events = new[]
                {
                    new CombatTrackEventJsonDto
                    {
                        Frame = 0,
                        EndFrame = 1,
                        Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                        StableEventId = CharacterTestBulkIds38.Id790039,
                        HitEvent = new CharacterActionHitEventJsonDto
                        {
                            Damage = new CharacterActionHitDamageJsonDto
                            {
                                DamageTypeId = CharacterTestBulkIds38.Id790006,
                                BaseDamage = 1,
                                ImpactPower = 1,
                            },
                        },
                        HitVolume = new CharacterActionHitVolumeJsonDto
                        {
                            SourceKind = CharacterActionHitVolumeSourceKind.CasterBody.ToString(),
                        },
                    },
                },
            };
        }

        private static void ConfigureWeaponTracePreview(CharacterActionJsonDto action)
        {
            ExtendSelectedActionForPreview(action);
            int combatActionId = action.CombatTrack?.CombatActionId ?? CharacterTestNumericIds.Field_combat_light_attack;
            action.CombatTrack = new CombatTrackJsonDto
            {
                CombatActionId = combatActionId,
                Events = new[]
                {
                    new CombatTrackEventJsonDto
                    {
                        Frame = 0,
                        EndFrame = 1,
                        Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                        EquipmentSlotId = CharacterTestBulkIds38.Id790007,
                        StableEventId = CharacterTestBulkIds38.Id790040,
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
        }

        private static void ConfigureProjectilePreview(CharacterActionJsonDto action, int requestId)
        {
            ExtendSelectedActionForPreview(action);
            action.GameplayTrack = new GameplayTrackJsonDto
            {
                Events = new[]
                {
                    new GameplayTrackEventJsonDto
                    {
                        Frame = 6,
                        Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                        RequestId = requestId,
                        StableEventId = CharacterTestBulkIds38.Id790041,
                    },
                },
            };
        }

        private static void ExtendSelectedActionForPreview(CharacterActionJsonDto action)
        {
            Assert.NotNull(action);
            action.DurationFrames = 100;
            action.Phases = new[]
            {
                new CharacterActionPhaseJsonDto
                {
                    Kind = CharacterActionPhaseKind.Startup.ToString(),
                    StartFrame = 0,
                    EndFrame = 5,
                },
                new CharacterActionPhaseJsonDto
                {
                    Kind = CharacterActionPhaseKind.Active.ToString(),
                    StartFrame = 6,
                    EndFrame = 6,
                },
                new CharacterActionPhaseJsonDto
                {
                    Kind = CharacterActionPhaseKind.Recovery.ToString(),
                    StartFrame = 7,
                    EndFrame = 99,
                },
            };
            if (action.AnimationTrack?.Events != null)
            {
                for (int i = 0; i < action.AnimationTrack.Events.Length; i++)
                    action.AnimationTrack.Events[i].EndFrame = Math.Max(action.AnimationTrack.Events[i].EndFrame, 99);
            }
        }

        private static void AssertGeneratedCodexFlowTestPackageExists()
        {
            Assert.NotNull(
                AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(CodexFlowTestCharacterPrefabPath),
                "Generated codex_flow_test runtime prefab is required for this integration test.");
        }

        private static Bounds CalculateBodyRendererBounds(GameObject preview)
        {
            Assert.NotNull(preview);
            Transform bodyModel = FindChildByName(preview.transform, "BodyModel");
            Assert.NotNull(bodyModel, "Preview prefab should contain BodyModel.");

            Renderer[] renderers = bodyModel.GetComponentsInChildren<Renderer>(includeInactive: true);
            Assert.Greater(renderers.Length, 0, "BodyModel should contain at least one renderer.");
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void AssertContainsBindingChoice(
            CharacterActionPreviewBindingChoice[] choices,
            string value)
        {
            for (int i = 0; i < choices.Length; i++)
            {
                if (string.Equals(choices[i].Value, value, StringComparison.Ordinal))
                    return;
            }

            Assert.Fail("Missing preview binding choice: " + value);
        }

        private static void AssertVector(Vector3 expected, Vector3 actual, float tolerance)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance);
            Assert.AreEqual(expected.y, actual.y, tolerance);
            Assert.AreEqual(expected.z, actual.z, tolerance);
        }

        private static Vector3 ToVector3(FixVector3 value)
        {
            return new Vector3(ToFloat(value.X), ToFloat(value.Y), ToFloat(value.Z));
        }

        private static float ToFloat(Fix64 value)
        {
            return value.RawValue / (float)Fix64.Scale;
        }

        private static Transform FindChildByPathOrName(Transform root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path))
                return null;

            string normalized = path.Replace('\\', '/').Trim('/');
            Transform byPath = root.Find(normalized);
            if (byPath != null)
                return byPath;

            int slash = normalized.LastIndexOf('/');
            string name = slash >= 0 ? normalized.Substring(slash + 1) : normalized;
            return FindChildByName(root, name);
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
                return null;
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindChildByName(root.GetChild(i), name);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static Transform ResolveLocatorParent(Transform previewRoot, string parentKind, string parentPath)
        {
            if (previewRoot == null)
                return null;

            if (string.Equals(parentKind, "Socket", StringComparison.OrdinalIgnoreCase))
            {
                Transform socket = FindChildByName(previewRoot, "Socket_" + parentPath)
                    ?? FindChildByName(previewRoot, parentPath);
                if (socket != null)
                    return socket;
            }

            if (!string.IsNullOrWhiteSpace(parentPath))
            {
                Transform byPath = previewRoot.Find(parentPath);
                if (byPath != null)
                    return byPath;

                Transform byName = FindChildByName(previewRoot, parentPath);
                if (byName != null)
                    return byName;
            }

            return previewRoot;
        }

        private static MxAnimationLayerDiagnostic FindBaseLayer(MxAnimationDiagnosticSnapshot snapshot)
        {
            Assert.NotNull(snapshot);
            for (int i = 0; i < snapshot.LayerStates.Count; i++)
            {
                MxAnimationLayerDiagnostic layer = snapshot.LayerStates[i];
                if (layer.LayerId == MxAnimationLayerId.Base)
                    return layer;
            }

            return null;
        }
    }
}
