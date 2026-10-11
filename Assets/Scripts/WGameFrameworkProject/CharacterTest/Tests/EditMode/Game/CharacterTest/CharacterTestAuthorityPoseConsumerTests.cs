using System;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestAuthorityPoseConsumerTests
    {
        private const int ColliderId = CharacterTestAuthoredContentIds.D400_Id128;
        private const int ContactPointId = 9101;
        private const int PresentationSocketId = 9102;
        private const string ContactPointLabel = "contact.test.limb";
        private const int ChannelId = CharacterTestAuthorityPoseTerrainTests.FirstTipChannelId;
        private const string Dragon14GeneratedPackageRoot =
            "Assets/MxFrameworkGenerated/CharacterPackages/000003";
        private const string Dragon14FormalPrefabPath =
            Dragon14GeneratedPackageRoot + "/prefabs/000003_character_preview.prefab";

        [Test]
        public void AttachmentDelta_ValidNoHitRetainedSample_ConsumesPositionAndBasis()
        {
            ConsumerFixture fixture = CreateFixture();
            Assert.IsTrue(Publish(
                fixture,
                runtimeFrame: 11,
                valid: true,
                CharacterTestAuthorityPoseSampleSourceMask.Base
                    | CharacterTestAuthorityPoseSampleSourceMask.TerrainIk,
                CharacterTestAuthorityPoseFailure.NoHit));

            Assert.IsTrue(TryResolve(fixture, 11, out CharacterTestAuthorityPoseAttachmentDelta delta));
            Assert.AreEqual(11L, delta.RuntimeFrame);
            Assert.AreEqual(fixture.Entity.ResolvedAuthorityPoseFrame.Revision, delta.Revision);

            FixVector3 point = delta.TransformPoint(V(1, 1, 1));
            AssertVector(V(3, 3, 4), point);
            AssertVector(V(1, 0, 0), delta.TransformAxis(V(0, 0, 1)));
        }

        [Test]
        public void AttachmentDelta_SecondaryOnlySample_RemainsAResolvedConsumerSource()
        {
            ConsumerFixture fixture = CreateFixture();
            Assert.IsTrue(Publish(
                fixture,
                runtimeFrame: 12,
                valid: true,
                CharacterTestAuthorityPoseSampleSourceMask.Base
                    | CharacterTestAuthorityPoseSampleSourceMask.Secondary,
                CharacterTestAuthorityPoseFailure.Success));

            Assert.IsTrue(TryResolve(fixture, 12, out CharacterTestAuthorityPoseAttachmentDelta delta));
            AssertVector(V(2, 3, 4), delta.TransformPoint(V(1, 1, 0)));
        }

        [Test]
        public void AttachmentDelta_InvalidOrWrongFrame_FailsToBakedBase()
        {
            ConsumerFixture fixture = CreateFixture();
            Assert.IsTrue(Publish(
                fixture,
                runtimeFrame: 20,
                valid: false,
                CharacterTestAuthorityPoseSampleSourceMask.Base,
                CharacterTestAuthorityPoseFailure.SourceStale));

            Assert.IsFalse(TryResolve(fixture, 20, out _));

            Assert.IsTrue(Publish(
                fixture,
                runtimeFrame: 21,
                valid: true,
                CharacterTestAuthorityPoseSampleSourceMask.Base,
                CharacterTestAuthorityPoseFailure.Success));
            Assert.IsFalse(TryResolve(fixture, 20, out _), "A previous RuntimeFrame must never be reused.");
        }

        [Test]
        public void AttachmentDelta_WarmedSameFrameConsumer_AllocatesZeroBytes()
        {
            ConsumerFixture fixture = CreateFixture();
            CharacterBodyPartContactPointConfig contactConfig = CreateContactPointConfig(ContactPointLabel);
            FixVector3 contactBase = V(1, 1, 1);
            for (int i = 0; i < 16; i++)
            {
                Assert.IsTrue(Publish(
                    fixture,
                    100 + i,
                    valid: true,
                    CharacterTestAuthorityPoseSampleSourceMask.Base
                        | CharacterTestAuthorityPoseSampleSourceMask.TerrainIk,
                    CharacterTestAuthorityPoseFailure.Success));
                Assert.IsTrue(TryResolve(fixture, 100 + i, out _));
                Assert.IsTrue(CharacterTestPhysicalAudioSystem.TryApplyAuthorityPoseContactPoint(
                    fixture.Entity,
                    fixture.Rig,
                    fixture.Catalog,
                    contactConfig,
                    CharacterTestAuthorityPoseTerrainTests.ClipKey,
                    localFrame: 0,
                    loop: true,
                    new RuntimeFrame(100 + i),
                    contactBase,
                    out _));
            }

            bool resolved = true;
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 256; i++)
            {
                long frame = 1000 + i;
                resolved &= Publish(
                    fixture,
                    frame,
                    valid: true,
                    CharacterTestAuthorityPoseSampleSourceMask.Base
                        | CharacterTestAuthorityPoseSampleSourceMask.TerrainIk,
                    CharacterTestAuthorityPoseFailure.Success);
                resolved &= TryResolve(fixture, frame, out CharacterTestAuthorityPoseAttachmentDelta delta)
                    && delta.TransformPoint(V(1, 1, 1)) == V(3, 3, 4);
                resolved &= CharacterTestPhysicalAudioSystem.TryApplyAuthorityPoseContactPoint(
                        fixture.Entity,
                        fixture.Rig,
                        fixture.Catalog,
                        contactConfig,
                        CharacterTestAuthorityPoseTerrainTests.ClipKey,
                        localFrame: 0,
                        loop: true,
                        new RuntimeFrame(frame),
                        contactBase,
                        out FixVector3 contactResolved)
                    && contactResolved == V(3, 3, 4);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(resolved);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void InjectedPackageSocketSource_DoesNotBorrowDefaultCatalogForAuthorityDelta()
        {
            ConsumerFixture fixture = CreateFixture();
            Assert.IsFalse(CharacterTestCharacterCombatTraceSystem.TryGetAuthorityPoseSocketInputs(
                new InjectedPackageSocketSource(),
                fixture.Entity,
                out CharacterAuthorityPoseRigRuntimeBinding rig,
                out CharacterSocketBakeCatalog catalog));
            Assert.IsNull(rig);
            Assert.IsNull(catalog);
        }

        [Test]
        public void View_UsesSameFrameResolvedPose_WithoutScaleOrLegacyDoubleApply_AndRestoresOnFallback()
        {
            ConsumerFixture fixture = CreateFixture();
            var instance = new GameObject("authority-pose-view");
            instance.AddComponent<Animator>();
            Transform authoredBone = CreateBonePath(instance.transform, "root/limb0/tip");
            Transform presentationOnly = CreateBonePath(instance.transform, "presentation_only");
            instance.transform.SetPositionAndRotation(
                new Vector3(10f, 5f, -3f),
                Quaternion.Euler(0f, 90f, 0f));
            instance.transform.localScale = new Vector3(2f, 3f, 4f);
            authoredBone.localPosition = new Vector3(0.25f, 0.5f, 0.75f);
            authoredBone.localRotation = Quaternion.Euler(8f, 12f, 16f);
            Vector3 baseLocalPosition = authoredBone.localPosition;
            Quaternion baseLocalRotation = authoredBone.localRotation;
            var boneSync = instance.AddComponent<CharacterBoneRuntimeSync>();
            var presentationBinding = new CharacterBoneRuntimeSyncBinding();
            presentationBinding.Configure("socket:presentation-test", string.Empty,
                presentationOnly, instance.transform, Vector3.zero, Quaternion.identity, Vector3.one,
                socketId: PresentationSocketId);
            boneSync.Configure(new[] { presentationBinding }, useExternalRuntimeTick: true);
            CharacterAuthorityPoseStateComponent legacy = CreateLegacyViewPoseState();
            var view = new CharacterTestRuntimeCharacterView(
                fixture.Entity.Id,
                instance,
                null,
                null,
                null,
                boneSync: boneSync,
                proceduralBoneRig: null,
                authorityPoseRig: fixture.Rig);

            try
            {
                view.SyncAnimation(
                    CharacterTestCharacterAnimationComponent.IdleGrounded,
                    new RuntimeFrame(40),
                    presentationPaused: true);
                Assert.IsTrue(Publish(
                    fixture,
                    runtimeFrame: 40,
                    valid: true,
                    CharacterTestAuthorityPoseSampleSourceMask.Base
                        | CharacterTestAuthorityPoseSampleSourceMask.Secondary,
                    CharacterTestAuthorityPoseFailure.Success));
                view.SyncAuthorityPose(
                    legacy,
                    fixture.Entity.TerrainIkState,
                    fixture.Entity.ResolvedAuthorityPoseFrame,
                    new RuntimeFrame(40));
                view.TickPresentation(0f, new RuntimeFrame(40));

                Vector3 expectedLocalPosition = new Vector3(2f, 3f, 4f);
                Vector3 expectedWorldPosition = instance.transform.position
                    + instance.transform.rotation * expectedLocalPosition;
                Quaternion expectedWorldRotation = instance.transform.rotation
                    * Quaternion.LookRotation(Vector3.right, Vector3.up);
                Assert.That(
                    Vector3.Distance(expectedWorldPosition, authoredBone.position),
                    Is.LessThan(0.0001f),
                    "ModelRoot scale and the legacy same-channel offset must not be applied.");
                Assert.That(
                    Quaternion.Angle(expectedWorldRotation, authoredBone.rotation),
                    Is.LessThan(0.001f));
                Assert.That(
                    Vector3.Distance(
                        instance.transform.position + instance.transform.rotation * Vector3.right,
                        presentationOnly.position),
                    Is.LessThan(0.0001f),
                    "Unbound PresentationOnly secondary motion must remain active.");

                view.TickPresentation(0f, new RuntimeFrame(41));
                Assert.That(
                    Vector3.Distance(baseLocalPosition, authoredBone.localPosition),
                    Is.LessThan(0.0001f),
                    "A stale frame must restore the paused animation/base pose.");
                Assert.That(
                    Quaternion.Angle(baseLocalRotation, authoredBone.localRotation),
                    Is.LessThan(0.001f));

                fixture.Entity.TerrainIkState.BeginResolvedFrame(42);
                view.SyncAuthorityPose(
                    legacy,
                    fixture.Entity.TerrainIkState,
                    fixture.Entity.ResolvedAuthorityPoseFrame,
                    new RuntimeFrame(42));
                view.TickPresentation(0f, new RuntimeFrame(42));
                Assert.That(
                    Vector3.Distance(baseLocalPosition, authoredBone.localPosition),
                    Is.LessThan(0.0001f),
                    "A missing authored sample must retain the base pose.");

                Assert.IsTrue(Publish(
                    fixture,
                    runtimeFrame: 43,
                    valid: false,
                    CharacterTestAuthorityPoseSampleSourceMask.Base,
                    CharacterTestAuthorityPoseFailure.SourceStale));
                view.SyncAuthorityPose(
                    legacy,
                    fixture.Entity.TerrainIkState,
                    fixture.Entity.ResolvedAuthorityPoseFrame,
                    new RuntimeFrame(43));
                view.TickPresentation(0f, new RuntimeFrame(43));
                Assert.That(
                    Vector3.Distance(baseLocalPosition, authoredBone.localPosition),
                    Is.LessThan(0.0001f),
                    "An invalid authored sample must retain the base pose.");
                Assert.That(
                    Quaternion.Angle(baseLocalRotation, authoredBone.localRotation),
                    Is.LessThan(0.001f));
            }
            finally
            {
                view.Release();
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Dragon14FormalPrefab_ResolvesAllTwelveAuthoredBonesFromAnimatorRoot_AndAppliesSameFramePose()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dragon14FormalPrefabPath);
            Assert.IsNotNull(prefab, Dragon14FormalPrefabPath);
            CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadPackage(
                Dragon14GeneratedPackageRoot);
            Assert.IsNotNull(package);
            CharacterAuthorityPoseRigRuntimeBinding rig = package.Geometry.AuthorityPoseRig;
            Assert.IsNotNull(rig);
            Assert.AreEqual(12, rig.Channels.Length, "The formal Dragon14 authority rig must keep 12 channels.");

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            CharacterTestRuntimeCharacterView view = null;
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>(includeInactive: true);
                Assert.IsNotNull(animator);
                Assert.AreEqual("000003_basemesh2", animator.transform.name);
                Assert.IsNull(
                    instance.transform.Find(rig.Channels[0].BonePath),
                    "The fixture must retain the production hierarchy that exposed the old Instance-root lookup bug.");

                CharacterTestAuthorityPoseTerrainTests.TerrainFixture terrain =
                    CharacterTestAuthorityPoseTerrainTests.ConfigureFixture(
                        new CharacterTestCharacterRuntimeWorld(),
                        actionRuntime: null,
                        rig);
                const long runtimeFrame = 70L;
                terrain.Entity.TerrainIkState.BeginResolvedFrame(runtimeFrame);
                for (int i = 0; i < rig.Channels.Length; i++)
                {
                    CharacterAuthorityPoseChannelRuntimeBinding channel = rig.Channels[i];
                    Assert.IsNotNull(
                        animator.transform.Find(channel.BonePath),
                        "Formal authored path must resolve exactly from the Animator root: " + channel.BonePath);
                    Assert.IsTrue(terrain.Entity.ResolvedAuthorityPoseFrame.TryAdd(
                        new CharacterTestResolvedAuthorityPoseSample(
                            channel.Ordinal,
                            channel.ChannelId,
                            position: V(i + 1, 2, 3),
                            basis: new CharacterTestResolvedAuthorityPoseBasis(
                                right: V(1, 0, 0),
                                up: V(0, 1, 0),
                                forward: V(0, 0, 1)),
                            valid: true,
                            CharacterTestAuthorityPoseSampleSourceMask.Base
                                | CharacterTestAuthorityPoseSampleSourceMask.TerrainIk,
                            CharacterTestAuthorityPoseFailure.Success)));
                }

                view = new CharacterTestRuntimeCharacterView(
                    terrain.Entity.Id,
                    instance,
                    null,
                    null,
                    null,
                    boneSync: null,
                    proceduralBoneRig: null,
                    authorityPoseRig: rig);
                view.SyncAuthorityPose(
                    authorityPose: null,
                    terrain.Entity.TerrainIkState,
                    terrain.Entity.ResolvedAuthorityPoseFrame,
                    new RuntimeFrame(runtimeFrame));
                view.TickPresentation(0f, new RuntimeFrame(runtimeFrame));

                int applied = 0;
                for (int i = 0; i < rig.Channels.Length; i++)
                {
                    Transform bone = animator.transform.Find(rig.Channels[i].BonePath);
                    Vector3 localPosition = new Vector3(i + 1, 2f, 3f);
                    Vector3 expectedWorldPosition = instance.transform.position
                        + instance.transform.rotation * localPosition;
                    if (Vector3.Distance(expectedWorldPosition, bone.position) < 0.0001f)
                        applied++;
                }

                Assert.AreEqual(
                    12,
                    applied,
                    "All 12 formal Dragon14 channels must consume the same published RuntimeFrame.");
            }
            finally
            {
                if (view != null)
                    view.Release();
                else if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ContactPointAdapter_ExplicitStableIdUsesSameFrameDelta_AndOtherwiseFailsToBase()
        {
            ConsumerFixture fixture = CreateFixture();
            CharacterBodyPartContactPointConfig config = CreateContactPointConfig(ContactPointLabel);
            FixVector3 baseOffset = V(1, 1, 1);
            Assert.IsTrue(Publish(
                fixture,
                runtimeFrame: 50,
                valid: true,
                CharacterTestAuthorityPoseSampleSourceMask.Base
                    | CharacterTestAuthorityPoseSampleSourceMask.TerrainIk,
                CharacterTestAuthorityPoseFailure.Success));

            Assert.IsTrue(CharacterTestPhysicalAudioSystem.TryApplyAuthorityPoseContactPoint(
                fixture.Entity,
                fixture.Rig,
                fixture.Catalog,
                config,
                CharacterTestAuthorityPoseTerrainTests.ClipKey,
                localFrame: 0,
                loop: true,
                new RuntimeFrame(50),
                baseOffset,
                out FixVector3 resolved));
            AssertVector(V(3, 3, 4), resolved);

            CharacterBodyPartContactPointConfig undeclared =
                CreateContactPointConfig("contact.test.undeclared");
            Assert.IsFalse(CharacterTestPhysicalAudioSystem.TryApplyAuthorityPoseContactPoint(
                fixture.Entity,
                fixture.Rig,
                fixture.Catalog,
                undeclared,
                CharacterTestAuthorityPoseTerrainTests.ClipKey,
                localFrame: 0,
                loop: true,
                new RuntimeFrame(50),
                baseOffset,
                out FixVector3 undeclaredResult));
            AssertVector(baseOffset, undeclaredResult);

            Assert.IsFalse(CharacterTestPhysicalAudioSystem.TryApplyAuthorityPoseContactPoint(
                fixture.Entity,
                fixture.Rig,
                fixture.Catalog,
                config,
                CharacterTestAuthorityPoseTerrainTests.ClipKey,
                localFrame: 0,
                loop: true,
                new RuntimeFrame(49),
                baseOffset,
                out FixVector3 staleResult));
            AssertVector(baseOffset, staleResult);
        }

        [Test]
        public void Dragon14_PresentationContactPointsHaveNoAuthorityAttachment_AndAdapterStaysClosed()
        {
            CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadPackage(
                "Assets/MxFrameworkGenerated/CharacterPackages/000003");
            Assert.IsNotNull(package);
            Assert.AreEqual(3, package.Configs.BodyPartContactPoints.Length);
            for (int i = 0; i < package.Configs.BodyPartContactPoints.Length; i++)
            {
                CharacterBodyPartContactPointConfig config = package.Configs.BodyPartContactPoints[i];
                Assert.AreEqual("PresentationAudio", config.Usage);
                Assert.AreEqual("PresentationOnly", config.Authority);
            }
            CharacterAuthorityPoseRigRuntimeBinding rig = package.Geometry.AuthorityPoseRig;
            int contactAttachmentCount = 0;
            for (int i = 0; i < rig.Attachments.Length; i++)
            {
                if (rig.Attachments[i].Kind == CharacterAuthorityPoseRigAttachmentKind.ContactPoint)
                    contactAttachmentCount++;
            }
            Assert.AreEqual(0, contactAttachmentCount);

            ConsumerFixture fixture = CreateFixture();
            FixVector3 baseOffset = V(1, 1, 1);
            Assert.IsFalse(CharacterTestPhysicalAudioSystem.TryApplyAuthorityPoseContactPoint(
                fixture.Entity,
                rig,
                fixture.Catalog,
                CreateContactPointConfig(ContactPointLabel),
                CharacterTestAuthorityPoseTerrainTests.ClipKey,
                localFrame: 0,
                loop: true,
                new RuntimeFrame(60),
                baseOffset,
                out FixVector3 resolved));
            AssertVector(baseOffset, resolved);
        }

        private static ConsumerFixture CreateFixture()
        {
            CharacterAuthorityPoseRigRuntimeBinding source =
                CharacterTestAuthorityPoseTerrainTests.CreateRig(limbCount: 1);
            var rig = new CharacterAuthorityPoseRigRuntimeBinding(
                source.Format,
                source.SchemaVersion,
                source.Revision,
                source.ContentHash,
                source.CoordinateSpace,
                source.DistanceUnit,
                source.NormalQuantizationScale,
                source.Channels,
                source.Limbs,
                new[]
                {
                    new CharacterAuthorityPoseAttachmentRuntimeBinding(
                        0,
                        CharacterAuthorityPoseRigAttachmentKind.Collider,
                        ColliderId,
                        ChannelId, channelLabel: "channel.0.tip"),
                    new CharacterAuthorityPoseAttachmentRuntimeBinding(
                        1,
                        CharacterAuthorityPoseRigAttachmentKind.ContactPoint,
                        ContactPointId,
                        ChannelId, channelLabel: "channel.0.tip")
                });
            CharacterTestAuthorityPoseTerrainTests.TerrainFixture terrain =
                CharacterTestAuthorityPoseTerrainTests.ConfigureFixture(
                    new CharacterTestCharacterRuntimeWorld(),
                    actionRuntime: null,
                    rig,
                    tipXOffsetMillimeters: 1000);
            return new ConsumerFixture(terrain.Entity, rig, terrain.Catalog);
        }

        private static bool Publish(
            ConsumerFixture fixture,
            long runtimeFrame,
            bool valid,
            CharacterTestAuthorityPoseSampleSourceMask sourceMask,
            CharacterTestAuthorityPoseFailure failure)
        {
            CharacterTestTerrainIkStateComponent state = fixture.Entity.TerrainIkState;
            state.BeginResolvedFrame(runtimeFrame);
            return state.ResolvedFrame.TryAdd(new CharacterTestResolvedAuthorityPoseSample(
                ordinal: 2,
                ChannelId,
                position: V(2, 3, 4),
                basis: new CharacterTestResolvedAuthorityPoseBasis(
                    right: V(0, 0, -1),
                    up: V(0, 1, 0),
                    forward: V(1, 0, 0)),
                valid,
                sourceMask,
                failure));
        }

        private static bool TryResolve(
            ConsumerFixture fixture,
            long runtimeFrame,
            out CharacterTestAuthorityPoseAttachmentDelta delta)
        {
            return CharacterTestAuthorityPoseConsumerMath.TryResolveAttachmentDelta(
                fixture.Entity,
                fixture.Rig,
                fixture.Catalog,
                CharacterAuthorityPoseRigAttachmentKind.Collider,
                ColliderId,
                CharacterTestAuthorityPoseTerrainTests.ClipKey,
                localFrame: 0,
                loop: true,
                new RuntimeFrame(runtimeFrame),
                out delta);
        }

        private static FixVector3 V(int x, int y, int z)
        {
            return new FixVector3(Fix64.FromInt(x), Fix64.FromInt(y), Fix64.FromInt(z));
        }

        private static void AssertVector(FixVector3 expected, FixVector3 actual)
        {
            Assert.AreEqual(expected.X.RawValue, actual.X.RawValue, "x");
            Assert.AreEqual(expected.Y.RawValue, actual.Y.RawValue, "y");
            Assert.AreEqual(expected.Z.RawValue, actual.Z.RawValue, "z");
        }

        private static CharacterAuthorityPoseStateComponent CreateLegacyViewPoseState()
        {
            var state = new CharacterAuthorityPoseStateComponent(
                new CharacterAuthorityPoseRigDefinition(new[]
                {
                    new CharacterAuthoritySecondaryBinding(
                        CharacterTestAuthoredContentIds.D400_Id10,
                        CharacterTestAuthoredContentIds.D400_Id11,
                        ChannelId,
                        0,
                        Fix64.Zero,
                        Fix64.One,
                        Fix64.FromInt(2),
                        Fix64.One),
                    new CharacterAuthoritySecondaryBinding(
                        CharacterTestAuthoredContentIds.D400_Id12,
                        CharacterTestAuthoredContentIds.D400_Id13,
                        CharacterTestAuthoredContentIds.D400_Id14,
                        PresentationSocketId,
                        Fix64.Zero,
                        Fix64.One,
                        Fix64.FromInt(2),
                        Fix64.One)
                }));
            state.RestoreSnapshot(new CharacterAuthorityPoseSnapshot
            {
                SecondaryBindings = new[]
                {
                    new CharacterAuthoritySecondarySnapshot
                    {
                        BindingId = CharacterTestAuthoredContentIds.D400_Id10,
                        RegionId = CharacterTestAuthoredContentIds.D400_Id11,
                        ChannelId = ChannelId,
                        SocketId = 0,
                        OffsetXRaw = Fix64.One.RawValue,
                        Active = true
                    },
                    new CharacterAuthoritySecondarySnapshot
                    {
                        BindingId = CharacterTestAuthoredContentIds.D400_Id12,
                        RegionId = CharacterTestAuthoredContentIds.D400_Id13,
                        ChannelId = CharacterTestAuthoredContentIds.D400_Id14,
                        SocketId = PresentationSocketId,
                        OffsetXRaw = Fix64.One.RawValue,
                        Active = true
                    }
                }
            });
            return state;
        }

        private static Transform CreateBonePath(Transform root, string path)
        {
            Transform current = root;
            string[] segments = path.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                Transform child = current.Find(segments[i]);
                if (child == null)
                {
                    var childObject = new GameObject(segments[i]);
                    child = childObject.transform;
                    child.SetParent(current, worldPositionStays: false);
                }
                current = child;
            }
            return current;
        }

        private static CharacterBodyPartContactPointConfig CreateContactPointConfig(string stableId)
        {
            return new CharacterBodyPartContactPointConfig(
                new CharacterBodyPartContactPointConfigId(9101),
                stableId,
                "partset.test",
                CharacterTestAuthoredContentIds.D400_Id11,
                contactPointId: stableId == ContactPointLabel ? ContactPointId : ContactPointId + 1,
                bodyPartId: 1,
                localOffsetXMillimeters: 0,
                localOffsetYMillimeters: 0,
                localOffsetZMillimeters: 0,
                radiusMillimeters: 80,
                layerMaskProfileId: "default",
                activationMode: CharacterBodyPartContactActivationMode.Always,
                motionMode: CharacterBodyPartContactMotionMode.BakedSocket,
                motionSocketId: ChannelId,
                usage: "PresentationAudio",
                authority: "PresentationOnly",
                sourceOrder: 0);
        }

        private sealed class ConsumerFixture
        {
            internal ConsumerFixture(
                CharacterTestCharacterEntity entity,
                CharacterAuthorityPoseRigRuntimeBinding rig,
                CharacterSocketBakeCatalog catalog)
            {
                Entity = entity;
                Rig = rig;
                Catalog = catalog;
            }

            internal CharacterTestCharacterEntity Entity { get; }
            internal CharacterAuthorityPoseRigRuntimeBinding Rig { get; }
            internal CharacterSocketBakeCatalog Catalog { get; }
        }

        private sealed class InjectedPackageSocketSource :
            ICharacterTestBakedWeaponTraceSource,
            ICharacterTestPackageBakedSocketSource
        {
            public bool TryGetFrame(
                string clipKey,
                int traceProfileId,
                int localFrame,
                out MxAnimationBakedWeaponTraceFrame frame)
            {
                frame = null;
                return false;
            }

            public bool TryGetSocketFrame(
                string clipKey,
                int socketId,
                int localFrame,
                out MxAnimationBakedSocketFrame frame)
            {
                frame = null;
                return false;
            }

            public bool TryResolveSocketTrack(
                string clipKey,
                int socketId,
                out CharacterTestBakedSocketTrack track)
            {
                track = null;
                return false;
            }

            public bool TryGetSocketFrame(
                string generatedPackageRoot,
                string clipKey,
                int socketId,
                int localFrame,
                out MxAnimationBakedSocketFrame frame)
            {
                frame = null;
                return false;
            }
        }
    }
}
