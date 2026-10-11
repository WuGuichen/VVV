using System;
using System.Collections;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Input;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterProceduralBoneRigTests
    {
        private const string CharacterTestScenePath = "Assets/Scenes/CharacterTestScene.unity";

        [Test]
        public void SecondaryChain_TickDampsImpulseWithoutNaN()
        {
            var root = new GameObject("character");
            try
            {
                Transform chainRoot = CreateChild(root.transform, "hair.0", Vector3.zero);
                Transform chainMid = CreateChild(chainRoot, "hair.1", Vector3.up * 0.25f);
                Transform chainTip = CreateChild(chainMid, "hair.2", Vector3.up * 0.25f);
                CharacterSecondaryBoneChain chain = chainRoot.gameObject.AddComponent<CharacterSecondaryBoneChain>();
                ConfigureSecondaryChain(chain, chainRoot, chainRoot, chainMid, chainTip);
                chain.CaptureBindPose();

                chain.ApplyImpulse(new CharacterProceduralBoneImpulseRequest(
                    Vector3.zero,
                    new Vector3(1f, 0.25f, 0.5f),
                    4f,
                    0.35f,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_3,
                    "test-secondary"));
                chain.Tick(1f / 60f);
                float firstAngle = Quaternion.Angle(Quaternion.identity, chainRoot.localRotation);

                for (int i = 0; i < 180; i++)
                    chain.Tick(1f / 60f);

                float finalAngle = Quaternion.Angle(Quaternion.identity, chainRoot.localRotation);
                Assert.That(firstAngle, Is.GreaterThan(0.001f));
                Assert.That(finalAngle, Is.LessThan(firstAngle));
                AssertFinite(chainRoot.localRotation);
                AssertFinite(chainMid.localRotation);
                AssertFinite(chainTip.localRotation);
                Assert.AreEqual(string.Empty, chain.LastIssue);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TwoBoneIk_ClampsUnreachableTarget()
        {
            var rootObject = new GameObject("upperArm");
            var targetObject = new GameObject("target");
            var poleObject = new GameObject("pole");
            try
            {
                Transform root = rootObject.transform;
                Transform mid = CreateChild(root, "forearm", Vector3.right);
                Transform tip = CreateChild(mid, "hand", Vector3.right);
                targetObject.transform.position = new Vector3(10f, 0f, 0f);
                poleObject.transform.position = Vector3.up;

                CharacterTwoBoneIkTarget ik = rootObject.AddComponent<CharacterTwoBoneIkTarget>();
                ConfigureIk(ik, root, mid, tip, targetObject.transform, poleObject.transform);

                ik.Tick();

                Assert.IsTrue(ik.LastTargetClamped);
                Assert.AreEqual(string.Empty, ik.LastIssue);
                Assert.That(Vector3.Distance(root.position, tip.position), Is.LessThanOrEqualTo(1.991f));
                Assert.That(tip.position.x, Is.GreaterThan(1.9f));
                AssertFinite(mid.position);
                AssertFinite(tip.position);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(poleObject);
            }
        }

        [Test]
        public void RagdollModeSwitch_DoesNotModifyRootAuthorityTransform()
        {
            var root = new GameObject("character");
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(3f, 0.5f, -2f), Quaternion.Euler(0f, 35f, 0f));
                Transform bone = CreateChild(root.transform, "spine", Vector3.up);
                Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
                BoxCollider collider = bone.gameObject.AddComponent<BoxCollider>();
                CharacterViewOnlyRagdollRig ragdoll = root.AddComponent<CharacterViewOnlyRagdollRig>();
                Vector3 rootPosition = root.transform.position;
                Quaternion rootRotation = root.transform.rotation;

                ragdoll.RefreshBodies();
                ragdoll.SetMode(CharacterViewOnlyRagdollMode.DynamicPresentation);

                Assert.IsFalse(body.isKinematic);
                Assert.IsTrue(collider.enabled);
                Assert.AreEqual(rootPosition, root.transform.position);
                Assert.AreEqual(rootRotation, root.transform.rotation);

                ragdoll.Tick(1f);

                Assert.AreEqual(CharacterViewOnlyRagdollMode.KinematicFollow, ragdoll.Mode);
                Assert.IsTrue(body.isKinematic);
                Assert.IsFalse(collider.enabled);
                Assert.AreEqual(rootPosition, root.transform.position);
                Assert.AreEqual(rootRotation, root.transform.rotation);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProceduralBoneRig_ForwardsImpulseAndReportsSnapshot()
        {
            var root = new GameObject("character");
            try
            {
                Transform chainRoot = CreateChild(root.transform, "strap.0", Vector3.zero);
                Transform chainTip = CreateChild(chainRoot, "strap.1", Vector3.up * 0.2f);
                CharacterSecondaryBoneChain chain = chainRoot.gameObject.AddComponent<CharacterSecondaryBoneChain>();
                ConfigureSecondaryChain(chain, chainRoot, chainRoot, chainTip);
                Transform ragdollBone = CreateChild(root.transform, "spine", Vector3.up);
                ragdollBone.gameObject.AddComponent<Rigidbody>();
                CharacterViewOnlyRagdollRig ragdoll = root.AddComponent<CharacterViewOnlyRagdollRig>();
                CharacterProceduralBoneRig rig = root.AddComponent<CharacterProceduralBoneRig>();

                ragdoll.RefreshBodies();
                rig.RefreshChildren();
                Assert.IsFalse(rig.HasActivePresentationWork);
                rig.ApplyImpulse(new CharacterProceduralBoneImpulseRequest(
                    Vector3.zero,
                    Vector3.right,
                    2f,
                    0.25f,
                    0,
                    "test-rig"));
                rig.Tick(1f / 60f);

                CharacterProceduralBoneRigSnapshot snapshot = rig.CreateSnapshot();
                Assert.IsTrue(rig.HasActivePresentationWork);
                Assert.AreEqual(1, snapshot.ActiveChainCount);
                Assert.AreEqual(0, snapshot.ActiveIkTargetCount);
                Assert.That(snapshot.ActiveImpulseCount, Is.GreaterThanOrEqualTo(1));
                Assert.AreEqual(CharacterViewOnlyRagdollMode.DynamicPresentation, snapshot.RagdollState);
                Assert.IsFalse(snapshot.HasIssue, snapshot.LastIssue);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProceduralBoneRig_PlaybackStateRestoresSecondaryAndRagdollPose()
        {
            var root = new GameObject("character.rewind");
            try
            {
                Transform chainRoot = CreateChild(root.transform, "strap.0", Vector3.zero);
                Transform chainTip = CreateChild(chainRoot, "strap.1", Vector3.up * 0.2f);
                CharacterSecondaryBoneChain chain = chainRoot.gameObject.AddComponent<CharacterSecondaryBoneChain>();
                ConfigureSecondaryChain(chain, chainRoot, chainRoot, chainTip);
                Transform ragdollBone = CreateChild(root.transform, "spine", Vector3.up);
                Rigidbody body = ragdollBone.gameObject.AddComponent<Rigidbody>();
                ragdollBone.gameObject.AddComponent<BoxCollider>();
                CharacterViewOnlyRagdollRig ragdoll = root.AddComponent<CharacterViewOnlyRagdollRig>();
                CharacterProceduralBoneRig rig = root.AddComponent<CharacterProceduralBoneRig>();

                ragdoll.RefreshBodies();
                rig.RefreshChildren();
                rig.ApplyImpulse(new CharacterProceduralBoneImpulseRequest(
                    Vector3.zero,
                    Vector3.right,
                    2f,
                    0.25f,
                    0,
                    "test-rig-rewind"));
                rig.Tick(1f / 60f);
                CharacterProceduralBonePlaybackState expected = rig.CapturePlaybackState();
                Quaternion expectedChainRotation = chainRoot.localRotation;
                Vector3 expectedBodyPosition = body.position;

                rig.Tick(0.1f);
                body.position += Vector3.right;
                Assert.IsTrue(rig.RestorePlaybackState(expected, out string diagnostic), diagnostic);
                CharacterProceduralBonePlaybackState actual = rig.CapturePlaybackState();

                Assert.AreEqual(expected.IkSolveSecondsRemaining, actual.IkSolveSecondsRemaining, 0.0001f);
                Assert.AreEqual(expected.SecondaryChains.Count, actual.SecondaryChains.Count);
                Assert.AreEqual(expected.SecondaryChains[0].OffsetDegrees, actual.SecondaryChains[0].OffsetDegrees);
                Assert.That(Quaternion.Angle(expectedChainRotation, chainRoot.localRotation), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(expectedBodyPosition, body.position), Is.LessThan(0.0001f));
                Assert.AreEqual(expected.Ragdoll.Mode, actual.Ragdoll.Mode);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProceduralBoneRig_SkipsPresentationTickWhileIdle()
        {
            var root = new GameObject("character");
            try
            {
                Transform chainRoot = CreateChild(root.transform, "strap.0", Vector3.zero);
                Transform chainTip = CreateChild(chainRoot, "strap.1", Vector3.up * 0.2f);
                CharacterSecondaryBoneChain chain = chainRoot.gameObject.AddComponent<CharacterSecondaryBoneChain>();
                ConfigureSecondaryChain(chain, chainRoot, chainRoot, chainTip);
                CharacterProceduralBoneRig rig = root.AddComponent<CharacterProceduralBoneRig>();

                chain.CaptureBindPose();
                rig.RefreshChildren();
                Quaternion idleRotation = chainRoot.localRotation;

                Assert.IsFalse(chain.HasActivePresentationWork);
                Assert.IsFalse(rig.HasActivePresentationWork);

                rig.Tick(1f / 60f);

                Assert.AreEqual(idleRotation, chainRoot.localRotation);
                Assert.IsFalse(rig.HasActivePresentationWork);

                rig.ApplyImpulse(new CharacterProceduralBoneImpulseRequest(
                    Vector3.zero,
                    Vector3.right,
                    2f,
                    0.15f,
                    0,
                    "test-idle-gate"));

                Assert.IsTrue(rig.HasActivePresentationWork);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProceduralBoneRig_AuthorityControlledBonesSkipFloatIkAndSecondaryOnly()
        {
            var root = new GameObject("character.authority-pose-suppression");
            try
            {
                Transform controlledSecondary = CreateChild(
                    root.transform,
                    "controlled.secondary",
                    new Vector3(-1f, 3f, 0f));
                CharacterSecondaryBoneChain controlledChain =
                    controlledSecondary.gameObject.AddComponent<CharacterSecondaryBoneChain>();
                ConfigureSecondaryChain(controlledChain, controlledSecondary, controlledSecondary);

                Transform presentationSecondary = CreateChild(
                    root.transform,
                    "presentation.secondary",
                    new Vector3(1f, 3f, 0f));
                CharacterSecondaryBoneChain presentationChain =
                    presentationSecondary.gameObject.AddComponent<CharacterSecondaryBoneChain>();
                ConfigureSecondaryChain(presentationChain, presentationSecondary, presentationSecondary);

                Transform controlledRoot = CreateChild(
                    root.transform,
                    "controlled.ik.root",
                    new Vector3(-3f, 0f, 0f));
                Transform controlledMid = CreateChild(controlledRoot, "mid", Vector3.right);
                Transform controlledTip = CreateChild(controlledMid, "tip", Vector3.right);
                Transform controlledTarget = CreateChild(
                    root.transform,
                    "controlled.ik.target",
                    new Vector3(-2f, 1f, 0f));
                Transform controlledPole = CreateChild(
                    root.transform,
                    "controlled.ik.pole",
                    new Vector3(-3f, 0f, 1f));
                CharacterTwoBoneIkTarget controlledIk =
                    controlledRoot.gameObject.AddComponent<CharacterTwoBoneIkTarget>();
                ConfigureIk(
                    controlledIk,
                    controlledRoot,
                    controlledMid,
                    controlledTip,
                    controlledTarget,
                    controlledPole);

                Transform presentationRoot = CreateChild(
                    root.transform,
                    "presentation.ik.root",
                    new Vector3(3f, 0f, 0f));
                Transform presentationMid = CreateChild(presentationRoot, "mid", Vector3.right);
                Transform presentationTip = CreateChild(presentationMid, "tip", Vector3.right);
                Transform presentationTarget = CreateChild(
                    root.transform,
                    "presentation.ik.target",
                    new Vector3(4f, 1f, 0f));
                Transform presentationPole = CreateChild(
                    root.transform,
                    "presentation.ik.pole",
                    new Vector3(3f, 0f, 1f));
                CharacterTwoBoneIkTarget presentationIk =
                    presentationRoot.gameObject.AddComponent<CharacterTwoBoneIkTarget>();
                ConfigureIk(
                    presentationIk,
                    presentationRoot,
                    presentationMid,
                    presentationTip,
                    presentationTarget,
                    presentationPole);

                CharacterProceduralBoneRig rig = root.AddComponent<CharacterProceduralBoneRig>();
                rig.RefreshChildren();
                rig.ConfigureAuthorityPoseControlledBones(
                    new[]
                    {
                        controlledSecondary,
                        controlledRoot,
                        controlledMid,
                        controlledTip
                    },
                    count: 4);
                Vector3 controlledTipBefore = controlledTip.position;
                Vector3 presentationTipBefore = presentationTip.position;

                rig.ApplySecondaryBoneImpulse(new CharacterProceduralBoneImpulseRequest(
                    Vector3.zero,
                    Vector3.right,
                    3f,
                    0.2f,
                    CharacterProceduralBodyRegionIds.BroadcastAll,
                    "test-authority-pose-suppression"));
                rig.RequestIkSolve(0.1f);
                rig.Tick(1f / 60f);

                Assert.That(
                    Quaternion.Angle(Quaternion.identity, controlledSecondary.localRotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(Quaternion.identity, presentationSecondary.localRotation),
                    Is.GreaterThan(0.001f));
                Assert.That(
                    Vector3.Distance(controlledTipBefore, controlledTip.position),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Vector3.Distance(presentationTipBefore, presentationTip.position),
                    Is.GreaterThan(0.001f));

                CharacterProceduralBoneRigSnapshot snapshot = rig.CreateSnapshot();
                Assert.AreEqual(1, snapshot.ActiveChainCount);
                Assert.AreEqual(1, snapshot.ActiveIkTargetCount);
                Assert.AreEqual(0, controlledChain.ActiveImpulseCount);
                Assert.AreEqual(1, presentationChain.ActiveImpulseCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProceduralBoneRig_SecondaryOnlyImpulseDoesNotTriggerRagdoll()
        {
            var root = new GameObject("character");
            try
            {
                Transform chainRoot = CreateChild(root.transform, "strap.0", Vector3.zero);
                Transform chainTip = CreateChild(chainRoot, "strap.1", Vector3.up * 0.2f);
                CharacterSecondaryBoneChain chain = chainRoot.gameObject.AddComponent<CharacterSecondaryBoneChain>();
                ConfigureSecondaryChain(chain, chainRoot, chainRoot, chainTip);
                Transform ragdollBone = CreateChild(root.transform, "spine", Vector3.up);
                Rigidbody body = ragdollBone.gameObject.AddComponent<Rigidbody>();
                ragdollBone.gameObject.AddComponent<BoxCollider>();
                CharacterViewOnlyRagdollRig ragdoll = root.AddComponent<CharacterViewOnlyRagdollRig>();
                CharacterProceduralBoneRig rig = root.AddComponent<CharacterProceduralBoneRig>();

                ragdoll.RefreshBodies();
                rig.RefreshChildren();
                rig.ApplySecondaryBoneImpulse(new CharacterProceduralBoneImpulseRequest(
                    Vector3.zero,
                    Vector3.right,
                    3f,
                    0.2f,
                    0,
                    "test-secondary-only"));

                CharacterProceduralBoneRigSnapshot snapshot = rig.CreateSnapshot();
                Assert.IsTrue(rig.HasActivePresentationWork);
                Assert.That(snapshot.ActiveImpulseCount, Is.GreaterThanOrEqualTo(1));
                Assert.AreEqual(CharacterViewOnlyRagdollMode.KinematicFollow, snapshot.RagdollState);
                Assert.IsTrue(body.isKinematic);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SecondaryChain_DefaultRegionDoesNotMatchUnknownButBroadcastMatchesAll()
        {
            var root = new GameObject("character");
            try
            {
                Transform headRoot = CreateChild(root.transform, "head", Vector3.up);
                Transform fallbackRoot = CreateChild(root.transform, "torso", Vector3.zero);
                CharacterSecondaryBoneChain head = headRoot.gameObject.AddComponent<CharacterSecondaryBoneChain>();
                CharacterSecondaryBoneChain fallback =
                    fallbackRoot.gameObject.AddComponent<CharacterSecondaryBoneChain>();
                head.ConfigureRuntimeChain(headRoot, MxFramework.Tests.CharacterCoreFixtureIds.Id_3, headRoot);
                fallback.ConfigureRuntimeChain(
                    fallbackRoot,
                    CharacterProceduralBodyRegionIds.Default,
                    fallbackRoot);
                CharacterProceduralBoneRig rig = root.AddComponent<CharacterProceduralBoneRig>();
                rig.RefreshChildren();

                rig.ApplySecondaryBoneImpulse(new CharacterProceduralBoneImpulseRequest(
                    Vector3.zero,
                    Vector3.right,
                    3f,
                    0.2f,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_9,
                    "test-unknown-region"));

                Assert.AreEqual(0, head.ActiveImpulseCount);
                Assert.AreEqual(0, fallback.ActiveImpulseCount);
                Assert.That(rig.CreateSnapshot().LastRegionFallbackIssue, Does.Contain("tail"));

                rig.ApplySecondaryBoneImpulse(new CharacterProceduralBoneImpulseRequest(
                    Vector3.zero,
                    Vector3.right,
                    3f,
                    0.2f,
                    CharacterProceduralBodyRegionIds.Default,
                    "test-default-region"));

                Assert.AreEqual(0, head.ActiveImpulseCount);
                Assert.AreEqual(1, fallback.ActiveImpulseCount);

                rig.ApplySecondaryBoneImpulse(new CharacterProceduralBoneImpulseRequest(
                    Vector3.zero,
                    Vector3.right,
                    3f,
                    0.2f,
                    CharacterProceduralBodyRegionIds.BroadcastAll,
                    "test-broadcast-region"));

                Assert.AreEqual(1, head.ActiveImpulseCount);
                Assert.AreEqual(1, fallback.ActiveImpulseCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProceduralBodyRegionInstaller_InstallsCodexFlowTestGeneratedRegionsFromLocators()
        {
            var root = new GameObject("codex-flow-test");
            try
            {
                CreateChild(root.transform, "Spine", Vector3.up);
                CreateChild(root.transform, "Head", Vector3.up * 1.8f);
                CreateChild(root.transform, "RightHand", new Vector3(0.5f, 1.1f, 0f));
                CreateChild(root.transform, "LeftHand", new Vector3(-0.5f, 1.1f, 0f));
                CreateChild(root.transform, "RightLeg", new Vector3(0.25f, 0.5f, 0f));
                CreateChild(root.transform, "LeftLeg", new Vector3(-0.25f, 0.5f, 0f));

                CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadDefaultPackage();
                CharacterProceduralBodyRegionInstallResult result =
                    CharacterProceduralBodyRegionInstaller.InstallFromImportedPackage(root, package);
                CharacterProceduralBoneRig rig = root.AddComponent<CharacterProceduralBoneRig>();
                rig.RefreshChildren();

                Assert.IsTrue(result.InstalledAny, result.LastIssue);
                Assert.That(result.InstalledRegionCount, Is.GreaterThanOrEqualTo(3));
                Assert.That(rig.CreateSnapshot().KnownRegionCount, Is.GreaterThanOrEqualTo(3));

                rig.ApplySecondaryBoneImpulse(new CharacterProceduralBoneImpulseRequest(
                    Vector3.zero,
                    Vector3.right,
                    3f,
                    0.2f,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_3,
                    "test-codex-flow-region"));
                CharacterProceduralBoneRigSnapshot snapshot = rig.CreateSnapshot();
                Assert.AreEqual("head", snapshot.LastResolvedRegionId);
                Assert.That(snapshot.ActiveImpulseCount, Is.GreaterThanOrEqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProceduralBodyRegionInstaller_SupportsNonHumanoidLocatorParts()
        {
            var root = new GameObject("slime");
            try
            {
                CreateChild(root.transform, "core", Vector3.up * 0.6f);
                CreateChild(root.transform, "shell", Vector3.up * 0.75f);
                CreateChild(root.transform, "front_face", new Vector3(0f, 0.65f, 0.45f));

                CharacterImportedPackage package = CreateSyntheticBodyPartPackage(
                    CharacterBodyKind.Slime,
                    new[]
                    {
                        CreateBodyPart(1, "creature", MxFramework.Tests.CharacterCoreFixtureIds.Id_2, CharacterBodyPartKind.Core, "core", MxFramework.Tests.CharacterCoreFixtureIds.Id_300000165, "core"),
                        CreateBodyPart(2, "creature", MxFramework.Tests.CharacterCoreFixtureIds.Id_8, CharacterBodyPartKind.Custom, "shell", MxFramework.Tests.CharacterCoreFixtureIds.Id_300000166, "shell"),
                        CreateBodyPart(3, "creature", MxFramework.Tests.CharacterCoreFixtureIds.Id_300000106, CharacterBodyPartKind.Custom, "front_face", MxFramework.Tests.CharacterCoreFixtureIds.Id_300000167, "front_face")
                    });
                CharacterProceduralBodyRegionInstallResult result =
                    CharacterProceduralBodyRegionInstaller.InstallFromImportedPackage(root, package);
                CharacterProceduralBoneRig rig = root.AddComponent<CharacterProceduralBoneRig>();
                rig.RefreshChildren();

                Assert.IsTrue(result.InstalledAny, result.LastIssue);
                Assert.That(rig.CreateSnapshot().KnownRegionCount, Is.GreaterThanOrEqualTo(3));
                rig.ApplySecondaryBoneImpulse(new CharacterProceduralBoneImpulseRequest(
                    Vector3.zero,
                    Vector3.forward,
                    3f,
                    0.2f,
                    MxFramework.Tests.CharacterCoreFixtureIds.Id_300000106,
                    "test-nonhuman-region"));

                CharacterProceduralBoneRigSnapshot snapshot = rig.CreateSnapshot();
                Assert.AreEqual("front_face", snapshot.LastResolvedRegionId);
                Assert.That(snapshot.ActiveImpulseCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProceduralHitFeedbackMapper_PositiveDamageCreatesClampedImpulse()
        {
            CharacterTestCharacterRuntimeWorld world = CreateFeedbackWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target);
            attacker.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0f, 0f));
            target.SetTransform(new CharacterTestCharacterTransformComponent(2f, 0f, 0f, 90f));
            DamageResolvedEvent evt = CreateDamageResolvedEvent(
                attacker,
                target,
                HitResolveKind.Damage,
                finalDamage: 14,
                impactPower: 12,
                impactForce: 0,
                hitPointWorld: ToFixVector3(1f, 1f, 0f),
                hitNormalWorld: ToFixVector3(-1f, 0f, 0f),
                hitZoneId: MxFramework.Tests.CharacterCoreFixtureIds.Id_55,
                bodyPartId: MxFramework.Tests.CharacterCoreFixtureIds.Id_10);

            Assert.IsTrue(CharacterTestProceduralHitFeedbackMapper.TryCreateImpulse(
                evt,
                world.Entities,
                out CharacterTestProceduralHitFeedbackImpulse impulse));

            Assert.AreEqual(target.Id.Value, impulse.TargetEntityId);
            Assert.AreEqual(MxFramework.Tests.CharacterCoreFixtureIds.Id_10, impulse.Request.RegionId);
            Assert.AreEqual(Fix64.FromRatio(252, 100).RawValue, impulse.Request.Strength.RawValue);
            Assert.AreEqual(9, impulse.Request.DurationFrames);
            Assert.That(
                impulse.Request.Direction.Dot(ToFixVector3(1f, 0f, 0f)).RawValue,
                Is.GreaterThan(Fix64.FromRatio(99, 100).RawValue));
            Assert.AreEqual(target.Id.Value, impulse.Request.CorrelationId.SourceId);
        }

        [Test]
        public void ProceduralHitFeedbackMapper_HitZoneResolvesCanonicalBodyRegionFromPackage()
        {
            CharacterTestCharacterRuntimeWorld world = CreateFeedbackWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target);
            DamageResolvedEvent evt = CreateDamageResolvedEvent(
                attacker,
                target,
                HitResolveKind.Damage,
                finalDamage: 14,
                impactPower: 12,
                impactForce: 0,
                hitPointWorld: ToFixVector3(1f, 1.6f, 0f),
                hitNormalWorld: ToFixVector3(-1f, 0f, 0f),
                hitZoneId: MxFramework.Tests.CharacterCoreFixtureIds.Id_56,
                bodyPartId: 0);

            Assert.IsTrue(CharacterTestProceduralHitFeedbackMapper.TryCreateImpulse(
                evt,
                world.Entities,
                CharacterTestGeneratedPackageSource.LoadPackageForEntity,
                out CharacterTestProceduralHitFeedbackImpulse impulse));

            Assert.AreEqual(MxFramework.Tests.CharacterCoreFixtureIds.Id_3, impulse.Request.RegionId);
            Assert.AreEqual("hitZone", impulse.Request.MatchSource);
            Assert.AreEqual(string.Empty, impulse.Request.FallbackIssue);
        }

        [Test]
        public void ProceduralHitFeedbackMapper_UnknownHitZoneFallsBackToDefaultRegion()
        {
            CharacterTestCharacterRuntimeWorld world = CreateFeedbackWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target);
            DamageResolvedEvent evt = CreateDamageResolvedEvent(
                attacker,
                target,
                HitResolveKind.Damage,
                finalDamage: 14,
                impactPower: 12,
                impactForce: 0,
                hitPointWorld: ToFixVector3(1f, 1f, 0f),
                hitNormalWorld: ToFixVector3(-1f, 0f, 0f),
                hitZoneId: MxFramework.Tests.CharacterCoreFixtureIds.Id_300000182,
                bodyPartId: 0);

            Assert.IsTrue(CharacterTestProceduralHitFeedbackMapper.TryCreateImpulse(
                evt,
                world.Entities,
                CharacterTestGeneratedPackageSource.LoadPackageForEntity,
                out CharacterTestProceduralHitFeedbackImpulse impulse));

            Assert.AreEqual(CharacterAuthorityPoseRegionIds.Default, impulse.Request.RegionId);
            Assert.AreEqual("default", impulse.Request.MatchSource);
            Assert.That(impulse.Request.FallbackIssue, Does.Contain("hit.unknown"));
        }

        [Test]
        public void ProceduralHitFeedbackMapper_FiltersZeroDamageAndBlocked()
        {
            CharacterTestCharacterRuntimeWorld world = CreateFeedbackWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target);

            DamageResolvedEvent zeroDamage = CreateDamageResolvedEvent(
                attacker,
                target,
                HitResolveKind.Damage,
                finalDamage: 0,
                impactPower: 12,
                impactForce: 0,
                hitPointWorld: FixVector3.Zero,
                hitNormalWorld: FixVector3.Zero,
                hitZoneId: MxFramework.Tests.CharacterCoreFixtureIds.Id_55,
                bodyPartId: MxFramework.Tests.CharacterCoreFixtureIds.Id_10);
            DamageResolvedEvent blocked = CreateDamageResolvedEvent(
                attacker,
                target,
                HitResolveKind.Blocked,
                finalDamage: 9,
                impactPower: 12,
                impactForce: 0,
                hitPointWorld: FixVector3.Zero,
                hitNormalWorld: FixVector3.Zero,
                hitZoneId: MxFramework.Tests.CharacterCoreFixtureIds.Id_55,
                bodyPartId: MxFramework.Tests.CharacterCoreFixtureIds.Id_10);

            Assert.IsFalse(CharacterTestProceduralHitFeedbackMapper.TryCreateImpulse(
                zeroDamage,
                world.Entities,
                out _));
            Assert.IsFalse(CharacterTestProceduralHitFeedbackMapper.TryCreateImpulse(
                blocked,
                world.Entities,
                out _));
        }

        [Test]
        public void ProceduralHitFeedbackMapper_RecordsStrongestImpulsePerTarget()
        {
            var impulses = new Dictionary<int, CharacterTestProceduralHitFeedbackImpulse>();
            CharacterAuthorityPoseImpulseRequest weakRequest =
                new CharacterAuthorityPoseImpulseRequest(MxFramework.Tests.CharacterCoreFixtureIds.Id_10, ToFixVector3(1f, 0f, 0f), Fix64.FromRatio(12, 10), 6);
            CharacterAuthorityPoseImpulseRequest strongRequest =
                new CharacterAuthorityPoseImpulseRequest(MxFramework.Tests.CharacterCoreFixtureIds.Id_10, ToFixVector3(-1f, 0f, 0f), Fix64.FromInt(4), 12);

            Assert.IsTrue(CharacterTestProceduralHitFeedbackMapper.TryRecordBestImpulse(
                impulses,
                new CharacterTestProceduralHitFeedbackImpulse(2, weakRequest, 4)));
            Assert.IsTrue(CharacterTestProceduralHitFeedbackMapper.TryRecordBestImpulse(
                impulses,
                new CharacterTestProceduralHitFeedbackImpulse(2, strongRequest, 20)));
            Assert.IsFalse(CharacterTestProceduralHitFeedbackMapper.TryRecordBestImpulse(
                impulses,
                new CharacterTestProceduralHitFeedbackImpulse(2, weakRequest, 3)));

            Assert.AreEqual(1, impulses.Count);
            Assert.AreEqual(20, impulses[2].ImpactScore);
            Assert.That(
                impulses[2].Request.Direction.Dot(ToFixVector3(-1f, 0f, 0f)).RawValue,
                Is.GreaterThan(Fix64.FromRatio(99, 100).RawValue));
        }

        [Test]
        public void GameSlice_DisabledProceduralHitFeedbackDoesNotRegisterRuntimeToggle()
        {
            using (var slice = new GameSlice(
                       createCharacterViews: true,
                       enableLocalInputCommands: false,
                       proceduralHitFeedbackOptions: new CharacterTestProceduralHitFeedbackOptions(enabled: false)))
            {
                Assert.IsFalse(slice.ProceduralHitFeedbackEnabled);
                Assert.IsFalse(slice.SetProceduralHitFeedbackEnabled(true));
            }
        }

        [Test]
        public void GameSlice_ProceduralHitFeedbackRuntimeToggleControlsRegisteredModule()
        {
            using (var slice = new GameSlice(
                       createCharacterViews: true,
                       enableLocalInputCommands: false))
            {
                Assert.IsTrue(slice.ProceduralHitFeedbackEnabled);
                Assert.IsTrue(slice.SetProceduralHitFeedbackEnabled(false));
                Assert.IsFalse(slice.ProceduralHitFeedbackEnabled);
                Assert.IsTrue(slice.SetProceduralHitFeedbackEnabled(true));
                Assert.IsTrue(slice.ProceduralHitFeedbackEnabled);
            }
        }

        [Test]
        public void ProceduralEffectPreviewHotkeys_MapF5ThroughF8()
        {
            AssertPreviewHotkey(
                CreatePreviewSnapshot(f5: true),
                CharacterTestProceduralEffectPreviewId.LightSecondary,
                "F5");
            AssertPreviewHotkey(
                CreatePreviewSnapshot(f6: true),
                CharacterTestProceduralEffectPreviewId.HeavySecondary,
                "F6");
            AssertPreviewHotkey(
                CreatePreviewSnapshot(f7: true),
                CharacterTestProceduralEffectPreviewId.FullRigPulse,
                "F7");
            AssertPreviewHotkey(
                CreatePreviewSnapshot(f8: true),
                CharacterTestProceduralEffectPreviewId.SideSecondary,
                "F8");

            Assert.IsFalse(CharacterTestProceduralEffectPreviewHotkeys.TryResolve(
                InputSnapshot.Empty,
                out _,
                out _));
        }

        [Test]
        public void ProceduralEffectPreviewRequests_CreateStableFiniteImpulses()
        {
            CharacterTestCharacterRuntimeWorld world = CreateFeedbackWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity target);
            attacker.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0f, 0f));
            target.SetTransform(new CharacterTestCharacterTransformComponent(2f, 0f, 0f, 0f));
            CharacterTestProceduralEffectPreviewId[] effectIds =
            {
                CharacterTestProceduralEffectPreviewId.LightSecondary,
                CharacterTestProceduralEffectPreviewId.HeavySecondary,
                CharacterTestProceduralEffectPreviewId.FullRigPulse,
                CharacterTestProceduralEffectPreviewId.SideSecondary
            };

            for (int i = 0; i < effectIds.Length; i++)
            {
                CharacterTestProceduralEffectPreviewId effectId = effectIds[i];
                Assert.IsTrue(CharacterTestProceduralEffectPreviewRequests.TryCreateRequest(
                    effectId,
                    attacker,
                    target,
                    "F" + (i + 5).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    out CharacterProceduralBoneImpulseRequest request,
                    out bool useFullRig));

                Assert.IsTrue(request.HasEffect);
                AssertFinite(request.SourcePosition);
                AssertFinite(request.Direction);
                Assert.That(request.Strength, Is.GreaterThan(0f));
                Assert.That(request.DurationSeconds, Is.GreaterThan(0f));
                Assert.AreEqual(
                    effectId == CharacterTestProceduralEffectPreviewId.FullRigPulse,
                    useFullRig);
            }
        }

        [Test]
        public void GameSlice_ProceduralEffectPreviewFallsBackToLocalPlayerWithoutLockedTarget()
        {
            using (var slice = new GameSlice(
                       createCharacterViews: false,
                       enableLocalInputCommands: false))
            {
                CharacterTestCharacterEntity localPlayer = null;
                for (int i = 0; i < 8; i++)
                {
                    slice.Tick(1d / 60d);
                    localPlayer = FindLocalPlayer(slice.CharacterEntities);
                    if (localPlayer != null)
                        break;
                }

                Assert.IsNotNull(localPlayer);

                CharacterTestProceduralEffectPreviewResult result =
                    slice.TryApplyLockedTargetProceduralEffectPreview(
                        CharacterTestProceduralEffectPreviewId.LightSecondary,
                        "F5");

                Assert.IsTrue(result.Accepted);
                Assert.AreEqual(CharacterTestProceduralEffectPreviewId.LightSecondary, result.EffectId);
                Assert.AreEqual(localPlayer.Id.Value, result.TargetEntityId);
                Assert.That(result.Message, Does.Contain("self"));
                Assert.That(slice.PendingRuntimeCommandCount, Is.GreaterThan(0));
                Assert.AreEqual(result.Message, slice.LastProceduralEffectPreviewResult.Message);
            }
        }

        [UnityTest]
        public IEnumerator CharacterTestScene_PlayModeDebugImpulseIsViewOnly()
        {
            EditorSceneManager.OpenScene(CharacterTestScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            bool managerFound = manager != null;
            bool menuClosed = false;
            bool localPlayerFound = false;
            bool rigFound = false;
            bool rigIdleBeforeTrigger = false;
            bool probeChainInstalled = false;
            bool triggerAccepted = false;
            bool snapshotAvailable = false;
            bool snapshotSawImpulse = false;
            bool authorityUnchanged = false;
            bool hashUnchanged = false;

            if (manager != null)
            {
                CharacterTestMainMenuCommandResult closeResult = manager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
                menuClosed = closeResult.Success && !manager.RuntimeContext.IsPaused;

                CharacterTestCharacterEntity localPlayer = null;
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    localPlayer = FindLocalPlayer(manager.RuntimeContext.Slice?.CharacterEntities);
                    if (localPlayer != null)
                        break;
                }

                localPlayerFound = localPlayer != null;
                CharacterProceduralBoneRig rig = localPlayer != null
                    ? FindRigForEntity(localPlayer.Id)
                    : null;
                rigFound = rig != null;

                if (localPlayer != null && rig != null)
                {
                    InstallProbeChain(rig.transform);
                    rig.RefreshChildren();
                    rigIdleBeforeTrigger = !rig.HasActivePresentationWork;
                    probeChainInstalled = rig.CreateSnapshot().ActiveChainCount > 0;

                    CharacterTestCharacterTransformComponent beforeTransform = localPlayer.Transform;
                    int beforeHealth = localPlayer.CombatState.Health;
                    int beforeScatterPressure = localPlayer.CombatState.ScatterPressure;
                    int beforeLastDamage = localPlayer.CombatState.LastDamage;
                    long beforeLastHitFrame = localPlayer.CombatState.LastHitFrame;
                    RuntimeFrame hashFrame = manager.RuntimeContext.Slice.CurrentFrame;
                    var participant = new CharacterTestNetworkSnapshotParticipant(
                        manager.RuntimeContext.Slice.CharacterRuntime,
                        manager.RuntimeContext.Slice.RuntimeCommands);
                    long beforeHash = participant.ComputeStateHash(hashFrame);

                    triggerAccepted = manager.RuntimeContext.Slice.CharacterRuntime.ViewBridge.TryApplyProceduralBoneImpulse(
                        localPlayer.Id,
                        new CharacterProceduralBoneImpulseRequest(
                            rig.transform.position,
                            Vector3.right,
                            3f,
                            0.25f,
                            0,
                            "playmode.procedural-bone-test"));
                    snapshotAvailable = manager.RuntimeContext.Slice.CharacterRuntime.ViewBridge.TryGetProceduralBoneRigSnapshot(
                        localPlayer.Id,
                        out CharacterProceduralBoneRigSnapshot snapshot);
                    snapshotSawImpulse = snapshot.ActiveChainCount > 0
                        && snapshot.ActiveImpulseCount > 0
                        && !snapshot.HasIssue;

                    long afterHash = participant.ComputeStateHash(hashFrame);
                    authorityUnchanged = SameTransform(beforeTransform, localPlayer.Transform)
                        && beforeHealth == localPlayer.CombatState.Health
                        && beforeScatterPressure == localPlayer.CombatState.ScatterPressure
                        && beforeLastDamage == localPlayer.CombatState.LastDamage
                        && beforeLastHitFrame == localPlayer.CombatState.LastHitFrame;
                    hashUnchanged = beforeHash == afterHash;

                    for (int i = 0; i < 5; i++)
                        yield return null;
                }
            }

            yield return new ExitPlayMode();

            Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
            Assert.IsTrue(menuClosed, "CharacterTest main menu should close before the procedural bone smoke runs.");
            Assert.IsTrue(localPlayerFound, "CharacterTest scene did not spawn the local player.");
            Assert.IsTrue(rigFound, "CharacterTest runtime view did not add a procedural bone rig.");
            Assert.IsTrue(rigIdleBeforeTrigger, "Procedural bone rig should be idle before a debug trigger.");
            Assert.IsTrue(probeChainInstalled, "Procedural probe chain was not discovered by the runtime rig.");
            Assert.IsTrue(triggerAccepted, "View bridge rejected the procedural bone debug impulse.");
            Assert.IsTrue(snapshotAvailable, "View bridge did not return a procedural bone rig snapshot.");
            Assert.IsTrue(snapshotSawImpulse, "Procedural bone rig snapshot did not report the active debug impulse.");
            Assert.IsTrue(authorityUnchanged, "Procedural debug impulse changed CharacterTest authority state.");
            Assert.IsTrue(hashUnchanged, "Procedural debug impulse changed CharacterTest runtime hash.");
        }

        [UnityTest]
        public IEnumerator CharacterTestScene_PlayModeLockedTargetProceduralEffectPreviewUsesAuthorityPose()
        {
            EditorSceneManager.OpenScene(CharacterTestScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            bool managerFound = manager != null;
            bool menuClosed = false;
            bool localPlayerFound = false;
            bool selfFallbackPreviewAccepted = false;
            bool selfFallbackTargetedLocalPlayer = false;
            bool lockCommandAccepted = false;
            bool targetLocked = false;
            bool lockedTargetFound = false;
            bool allPreviewAccepted = false;
            bool snapshotSawImpulse = false;
            bool previewResolvedRegions = false;
            bool authorityUnchanged = false;
            bool hashChanged = false;

            if (manager != null)
            {
                CharacterTestMainMenuCommandResult closeResult = manager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
                menuClosed = closeResult.Success && !manager.RuntimeContext.IsPaused;

                CharacterTestCharacterEntity localPlayer = null;
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                    localPlayer = FindLocalPlayer(manager.RuntimeContext.Slice?.CharacterEntities);
                    if (localPlayer != null)
                        break;
                }

                localPlayerFound = localPlayer != null;
                if (localPlayer != null)
                {
                    CharacterTestProceduralEffectPreviewResult selfPreview =
                        manager.RuntimeContext.Slice.TryApplyLockedTargetProceduralEffectPreview(
                            CharacterTestProceduralEffectPreviewId.LightSecondary,
                            "F5");
                    selfFallbackPreviewAccepted = selfPreview.Accepted;
                    selfFallbackTargetedLocalPlayer =
                        selfPreview.TargetEntityId == localPlayer.Id.Value
                        && selfPreview.Message.Contains("self");

                    RuntimeCommandValidationResult lockResult =
                        manager.RuntimeContext.Slice.EnqueueToggleTargetLock(localPlayer.Id.Value);
                    lockCommandAccepted = lockResult.Success;
                    for (int i = 0; i < 20; i++)
                    {
                        yield return null;
                        if (manager.RuntimeContext.Slice.TargetLockEnabled && manager.RuntimeContext.Slice.LockedTargetEntityId > 0)
                            break;
                    }

                    targetLocked = manager.RuntimeContext.Slice.TargetLockEnabled
                        && manager.RuntimeContext.Slice.LockedTargetEntityId > 0;
                }

                CharacterTestCharacterEntity lockedTarget = targetLocked
                    ? FindEntity(manager.RuntimeContext.Slice.CharacterEntities, manager.RuntimeContext.Slice.LockedTargetEntityId)
                    : null;
                lockedTargetFound = lockedTarget != null;

                if (lockedTarget != null)
                {
                    CharacterTestCharacterTransformComponent beforeTransform = lockedTarget.Transform;
                    int beforeHealth = lockedTarget.CombatState.Health;
                    int beforeScatterPressure = lockedTarget.CombatState.ScatterPressure;
                    int beforeLastDamage = lockedTarget.CombatState.LastDamage;
                    long beforeLastHitFrame = lockedTarget.CombatState.LastHitFrame;
                    RuntimeFrame hashFrame = manager.RuntimeContext.Slice.CurrentFrame;
                    var participant = new CharacterTestNetworkSnapshotParticipant(
                        manager.RuntimeContext.Slice.CharacterRuntime,
                        manager.RuntimeContext.Slice.RuntimeCommands);
                    long beforeHash = participant.ComputeStateHash(hashFrame);

                    CharacterTestProceduralEffectPreviewResult f5 =
                        manager.RuntimeContext.Slice.TryApplyLockedTargetProceduralEffectPreview(
                            CharacterTestProceduralEffectPreviewId.LightSecondary,
                            "F5");
                    CharacterTestProceduralEffectPreviewResult f6 =
                        manager.RuntimeContext.Slice.TryApplyLockedTargetProceduralEffectPreview(
                            CharacterTestProceduralEffectPreviewId.HeavySecondary,
                            "F6");
                    CharacterTestProceduralEffectPreviewResult f7 =
                        manager.RuntimeContext.Slice.TryApplyLockedTargetProceduralEffectPreview(
                            CharacterTestProceduralEffectPreviewId.FullRigPulse,
                            "F7");
                    CharacterTestProceduralEffectPreviewResult f8 =
                        manager.RuntimeContext.Slice.TryApplyLockedTargetProceduralEffectPreview(
                            CharacterTestProceduralEffectPreviewId.SideSecondary,
                            "F8");
                    allPreviewAccepted = f5.Accepted && f6.Accepted && f7.Accepted && f8.Accepted;
                    for (int i = 0; i < 2; i++)
                        manager.RuntimeContext.Slice.Tick(0d);

                    CharacterAuthorityPoseSnapshot authorityPoseSnapshot =
                        lockedTarget.AuthorityPose.CreateSnapshot();
                    snapshotSawImpulse = authorityPoseSnapshot.ActiveSecondaryCount > 0;
                    previewResolvedRegions = authorityPoseSnapshot.KnownRegionCount >= 2
                        && authorityPoseSnapshot.LastResolvedRegionId == MxFramework.Tests.CharacterCoreFixtureIds.Id_6
                        && authorityPoseSnapshot.LastMatchSource == CharacterAuthorityPoseMatchSource.PreviewCommand;

                    long afterHash = participant.ComputeStateHash(hashFrame);
                    authorityUnchanged = SameTransform(beforeTransform, lockedTarget.Transform)
                        && beforeHealth == lockedTarget.CombatState.Health
                        && beforeScatterPressure == lockedTarget.CombatState.ScatterPressure
                        && beforeLastDamage == lockedTarget.CombatState.LastDamage
                        && beforeLastHitFrame == lockedTarget.CombatState.LastHitFrame;
                    hashChanged = beforeHash != afterHash;

                    for (int i = 0; i < 5; i++)
                        yield return null;
                }
            }

            yield return new ExitPlayMode();

            Assert.IsTrue(managerFound, "CharacterTestScene did not create a GameManager in Play Mode.");
            Assert.IsTrue(menuClosed, "CharacterTest main menu should close before the procedural preview smoke runs.");
            Assert.IsTrue(localPlayerFound, "CharacterTest scene did not spawn the local player.");
            Assert.IsTrue(selfFallbackPreviewAccepted, "No-lock procedural preview did not apply to the local player.");
            Assert.IsTrue(selfFallbackTargetedLocalPlayer, "No-lock procedural preview did not target the local player.");
            Assert.IsTrue(lockCommandAccepted, "Target lock command was rejected.");
            Assert.IsTrue(targetLocked, "CharacterTest did not lock a target for procedural preview.");
            Assert.IsTrue(lockedTargetFound, "Locked target entity was not found.");
            Assert.IsTrue(allPreviewAccepted, "One or more locked-target procedural previews were rejected.");
            Assert.IsTrue(snapshotSawImpulse, "Locked-target procedural previews did not report active impulses.");
            Assert.IsTrue(previewResolvedRegions, "Locked-target procedural previews did not report configured body regions.");
            Assert.IsTrue(authorityUnchanged, "Procedural preview changed CharacterTest root/combat authority state.");
            Assert.IsTrue(hashChanged, "Procedural preview authority pose did not enter CharacterTest runtime hash.");
        }

        private static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }

        private static CharacterTestCharacterRuntimeWorld CreateFeedbackWorld(
            out CharacterTestCharacterEntity attacker,
            out CharacterTestCharacterEntity target)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(registry.TryGet(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                out CharacterTestCharacterDefinition definition));
            var world = new CharacterTestCharacterRuntimeWorld();
            attacker = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 2f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            return world;
        }

        private static DamageResolvedEvent CreateDamageResolvedEvent(
            CharacterTestCharacterEntity attacker,
            CharacterTestCharacterEntity target,
            HitResolveKind kind,
            int finalDamage,
            int impactPower,
            int impactForce,
            FixVector3 hitPointWorld,
            FixVector3 hitNormalWorld,
            int hitZoneId,
            int bodyPartId)
        {
            var usage = new DamageProfileUsage(
                DamageProfileSourceKind.WeaponTrace,
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                actionInstanceId: 1,
                stableEventId: MxFramework.Tests.CharacterCoreFixtureIds.Id_300000122,
                sourceEntityId: attacker.Id.Value,
                targetEntityId: target.Id.Value,
                hitZoneId: hitZoneId,
                bodyPartId: bodyPartId,
                frame: 12,
                localFrame: 3);
            var profile = new DamageProfileResolveResult(
                success: true,
                reason: string.Empty,
                damageProfileId: 0,
                damageTypeId: MxFramework.Tests.CharacterCoreFixtureIds.Id_1,
                formulaId: DamageFormulaIds.Flat,
                incomingDamage: finalDamage,
                staggerFrames: 1,
                impactForce: impactForce,
                knockback: FixVector3.Zero,
                impactPower: impactPower,
                guardPressure: 0,
                presentationProfileId: 0,
                usage: usage);
            var hit = new HitResolveResult(
                ToCombatEntityId(attacker),
                ToCombatEntityId(target),
                actionId: 1,
                actionInstanceId: 1,
                traceId: 7,
                frame: new CombatFrame(12),
                kind: kind,
                damage: finalDamage,
                staggerFrames: 1,
                knockback: FixVector3.Zero,
                hitPointWorld: hitPointWorld,
                hitNormalWorld: hitNormalWorld);
            return new DamageResolvedEvent(
                new DamageRequest(0, usage),
                profile,
                hit,
                finalDamage);
        }

        private static CombatEntityId ToCombatEntityId(CharacterTestCharacterEntity entity)
        {
            return new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value);
        }

        private static FixVector3 ToFixVector3(float x, float y, float z)
        {
            return new FixVector3(ToFix64(x), ToFix64(y), ToFix64(z));
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)(value * Fix64.Scale));
        }

        private static CharacterTestCharacterEntity FindLocalPlayer(
            IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            if (entities == null)
                return null;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entity;
            }

            return null;
        }

        private static CharacterTestCharacterEntity FindEntity(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int entityId)
        {
            if (entities == null || entityId <= 0)
                return null;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null && entity.Id.Value == entityId)
                    return entity;
            }

            return null;
        }

        private static InputSnapshot CreatePreviewSnapshot(
            bool f5 = false,
            bool f6 = false,
            bool f7 = false,
            bool f8 = false)
        {
            return new InputSnapshot(
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                0f,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: false,
                attackPrimaryHeld: false,
                attackSecondaryPressed: false,
                interactPressed: false,
                dodgePressed: false,
                sprintHeld: false,
                submitPressed: false,
                cancelPressed: false,
                pausePressed: false,
                debugTogglePressed: false,
                debugPrimaryPressed: f5,
                debugSecondaryPressed: f6,
                audioPrimaryPressed: f7,
                audioSecondaryPressed: f8);
        }

        private static void AssertPreviewHotkey(
            InputSnapshot snapshot,
            CharacterTestProceduralEffectPreviewId expectedEffectId,
            string expectedTriggerName)
        {
            Assert.IsTrue(CharacterTestProceduralEffectPreviewHotkeys.TryResolve(
                snapshot,
                out CharacterTestProceduralEffectPreviewId effectId,
                out string triggerName));
            Assert.AreEqual(expectedEffectId, effectId);
            Assert.AreEqual(expectedTriggerName, triggerName);
        }

        private static CharacterProceduralBoneRig FindRigForEntity(CharacterTestCharacterEntityId entityId)
        {
            CharacterProceduralBoneRig[] rigs = Object.FindObjectsByType<CharacterProceduralBoneRig>(UnityEngine.FindObjectsInactive.Exclude);
            string suffix = "_Entity" + entityId.Value;
            for (int i = 0; i < rigs.Length; i++)
            {
                CharacterProceduralBoneRig rig = rigs[i];
                if (rig != null && rig.gameObject.name.EndsWith(suffix, System.StringComparison.Ordinal))
                    return rig;
            }

            return null;
        }

        private static void InstallProbeChain(Transform parent)
        {
            Transform root = CreateChild(parent, "ProceduralBoneProbe.0", Vector3.up * 1.5f);
            CreateChild(root, "ProceduralBoneProbe.1", Vector3.up * 0.2f);
            root.gameObject.AddComponent<CharacterSecondaryBoneChain>().CaptureBindPose();
        }

        private static bool SameTransform(
            CharacterTestCharacterTransformComponent left,
            CharacterTestCharacterTransformComponent right)
        {
            return Mathf.Approximately(left.X, right.X)
                && Mathf.Approximately(left.Y, right.Y)
                && Mathf.Approximately(left.Z, right.Z)
                && Mathf.Approximately(left.YawDegrees, right.YawDegrees);
        }

        private static CharacterImportedPackage CreateSyntheticBodyPartPackage(
            CharacterBodyKind bodyKind,
            CharacterBodyPartConfig[] bodyParts)
        {
            var bodyProfile = new CharacterBodyProfileConfig(
                new CharacterBodyProfileId(1),
                "body.synthetic",
                bodyKind,
                "creature",
                string.Empty,
                string.Empty,
                Array.Empty<CharacterSocketEntry>(),
                Array.Empty<CharacterHitZoneBindingEntry>());
            var configs = new CharacterImportedConfigSet(
                Array.Empty<CharacterConfig>(),
                Array.Empty<CharacterAttributeProfileConfig>(),
                new[] { bodyProfile },
                bodyParts,
                Array.Empty<CharacterBodyPartContactPointConfig>(),
                Array.Empty<EquipmentSchemaConfig>(),
                Array.Empty<EquipmentLoadoutConfig>(),
                Array.Empty<EquipmentStateConfig>(),
                Array.Empty<WeaponConfig>(),
                Array.Empty<AbilityLoadoutConfig>(),
                Array.Empty<CombatActionSetConfig>(),
                Array.Empty<CharacterPresentationProfileConfig>(),
                Array.Empty<SpawnProfileConfig>());
            return new CharacterImportedPackage(
                "synthetic",
                "synthetic",
                configs,
                CharacterImportedGeometryBinding.Empty,
                CharacterImportedResourceMapping.Empty,
                null,
                null);
        }

        private static CharacterBodyPartConfig CreateBodyPart(
            int id,
            string partSetId,
            int partId,
            CharacterBodyPartKind partKind,
            string locatorId,
            int hitZoneId,
            string partLabel)
        {
            return new CharacterBodyPartConfig(
                new CharacterBodyPartConfigId(id),
                "part." + partLabel,
                partSetId,
                partId,
                0,
                partKind,
                locatorId,
                hitZoneId,
                0,
                1f,
                1f,
                1f,
                1f,
                isCritical: false);
        }

        private static void ConfigureSecondaryChain(
            CharacterSecondaryBoneChain chain,
            Transform chainRoot,
            params Transform[] affectedBones)
        {
            var serialized = new SerializedObject(chain);
            serialized.FindProperty("_chainRoot").objectReferenceValue = chainRoot;
            serialized.FindProperty("_bodyRegionId").stringValue = "head";
            SerializedProperty bones = serialized.FindProperty("_affectedBones");
            bones.arraySize = affectedBones.Length;
            for (int i = 0; i < affectedBones.Length; i++)
                bones.GetArrayElementAtIndex(i).objectReferenceValue = affectedBones[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureIk(
            CharacterTwoBoneIkTarget ik,
            Transform root,
            Transform mid,
            Transform tip,
            Transform target,
            Transform pole)
        {
            var serialized = new SerializedObject(ik);
            serialized.FindProperty("_root").objectReferenceValue = root;
            serialized.FindProperty("_mid").objectReferenceValue = mid;
            serialized.FindProperty("_tip").objectReferenceValue = tip;
            serialized.FindProperty("_target").objectReferenceValue = target;
            serialized.FindProperty("_pole").objectReferenceValue = pole;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertFinite(Vector3 value)
        {
            Assert.IsFalse(float.IsNaN(value.x) || float.IsInfinity(value.x));
            Assert.IsFalse(float.IsNaN(value.y) || float.IsInfinity(value.y));
            Assert.IsFalse(float.IsNaN(value.z) || float.IsInfinity(value.z));
        }

        private static void AssertFinite(Quaternion value)
        {
            Assert.IsFalse(float.IsNaN(value.x) || float.IsInfinity(value.x));
            Assert.IsFalse(float.IsNaN(value.y) || float.IsInfinity(value.y));
            Assert.IsFalse(float.IsNaN(value.z) || float.IsInfinity(value.z));
            Assert.IsFalse(float.IsNaN(value.w) || float.IsInfinity(value.w));
        }
    }
}
