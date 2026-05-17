using System.Collections.Generic;
using System.Linq;
using MxFramework.Animation;
using MxFramework.Editor.Animation;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.Animation
{
    public sealed class MxAnimationBakeTests
    {
        [Test]
        public void BakeArtifactHash_IsStableAcrossFrameOrder()
        {
            MxAnimationBakeProfile profile = CreateProfile();
            var first = new MxAnimationBakeArtifact(
                profile,
                new[] { Trace(localFrame: 2), Trace(localFrame: 1) },
                new[] { Root(localFrame: 1), Root(localFrame: 0) },
                new[] { Marker(localFrame: 4, sourceOrder: 1), Marker(localFrame: 3, sourceOrder: 0) },
                socketFrames: new[] { Socket(localFrame: 2), Socket(localFrame: 1) });
            var second = new MxAnimationBakeArtifact(
                profile,
                new[] { Trace(localFrame: 1), Trace(localFrame: 2) },
                new[] { Root(localFrame: 0), Root(localFrame: 1) },
                new[] { Marker(localFrame: 3, sourceOrder: 0), Marker(localFrame: 4, sourceOrder: 1) },
                socketFrames: new[] { Socket(localFrame: 1), Socket(localFrame: 2) });

            Assert.AreEqual(first.ArtifactHash, second.ArtifactHash);
            Assert.IsFalse(MxAnimationBakeArtifactValidator.Validate(first).HasErrors);
        }

        [Test]
        public void BakeArtifactHash_IncludesSocketTrajectoryAndEventAlignment()
        {
            MxAnimationBakeProfile profile = CreateProfile();
            var socketA = new MxAnimationBakeArtifact(
                profile,
                new[] { Trace(localFrame: 1) },
                new[] { Root(localFrame: 1) },
                new[] { Marker(localFrame: 3, sourceOrder: 0, presentationFrame: 3) },
                socketFrames: new[] { Socket(localFrame: 1, x: 10) });
            var socketB = new MxAnimationBakeArtifact(
                profile,
                new[] { Trace(localFrame: 1) },
                new[] { Root(localFrame: 1) },
                new[] { Marker(localFrame: 3, sourceOrder: 0, presentationFrame: 3) },
                socketFrames: new[] { Socket(localFrame: 1, x: 11) });
            var eventB = new MxAnimationBakeArtifact(
                profile,
                new[] { Trace(localFrame: 1) },
                new[] { Root(localFrame: 1) },
                new[] { Marker(localFrame: 3, sourceOrder: 0, presentationFrame: 4) },
                socketFrames: new[] { Socket(localFrame: 1, x: 10) });

            Assert.AreNotEqual(socketA.ArtifactHash, socketB.ArtifactHash);
            Assert.AreNotEqual(socketA.ArtifactHash, eventB.ArtifactHash);
        }

        [Test]
        public void BakePublicConstructors_PreserveLegacySignatures()
        {
            Assert.NotNull(typeof(MxAnimationBakedEventMarker).GetConstructor(new[]
            {
                typeof(int),
                typeof(string),
                typeof(MxAnimationBakeEventKind),
                typeof(ResourceKey),
                typeof(int)
            }));
            Assert.NotNull(typeof(MxAnimationBakeArtifact).GetConstructor(new[]
            {
                typeof(MxAnimationBakeProfile),
                typeof(IEnumerable<MxAnimationBakedWeaponTraceFrame>),
                typeof(IEnumerable<MxAnimationBakedRootMotionFrame>),
                typeof(IEnumerable<MxAnimationBakedEventMarker>),
                typeof(string)
            }));
        }

        [Test]
        public void BakeArtifactValidator_ReportsSourceProfileAndArtifactMismatch()
        {
            MxAnimationBakeProfile profile = CreateProfile();
            var artifact = new MxAnimationBakeArtifact(profile, new[] { Trace(localFrame: 1) });
            var expectation = new MxAnimationBakeExpectation(
                sourceClipHash: "sha256:other-source",
                profileHash: "sha256:other-profile",
                skeletonProfileHash: "sha256:other-skeleton",
                artifactHash: "sha256:other-artifact");

            MxAnimationBakeValidationReport report = MxAnimationBakeArtifactValidator.Validate(artifact, expectation);

            Assert.IsTrue(report.HasErrors);
            Assert.That(report.Issues.Select(i => i.Code), Contains.Item("BakeSourceClipHashMismatch"));
            Assert.That(report.Issues.Select(i => i.Code), Contains.Item("BakeProfileHashExpectedMismatch"));
            Assert.That(report.Issues.Select(i => i.Code), Contains.Item("BakeSkeletonProfileHashMismatch"));
            Assert.That(report.Issues.Select(i => i.Code), Contains.Item("BakeArtifactHashExpectedMismatch"));
            Assert.IsTrue(report.Issues.All(i => i.Location.SourceClipKey.Equals(profile.SourceClipKey)));
            Assert.IsTrue(report.Issues.All(i => i.Location.ProfileId == profile.ProfileId));
            Assert.IsTrue(report.Issues.All(i => i.Location.SkeletonProfileId == profile.SkeletonProfileId));
            Assert.IsTrue(report.Issues.All(i => i.Location.ArtifactHash == artifact.ArtifactHash));
        }

        [Test]
        public void BakeArtifactValidator_ReportsDuplicateSocketFrameWithLocation()
        {
            MxAnimationBakeProfile profile = CreateProfile();
            var artifact = new MxAnimationBakeArtifact(
                profile,
                socketFrames: new[] { Socket(localFrame: 1), Socket(localFrame: 1) });

            MxAnimationBakeValidationReport report = MxAnimationBakeArtifactValidator.Validate(artifact);

            Assert.IsTrue(report.HasErrors);
            MxAnimationBakeIssue issue = report.Issues.Single(i => i.Code == "BakeDuplicateSocketFrame");
            Assert.AreEqual(profile.SourceClipKey, issue.Location.SourceClipKey);
            Assert.AreEqual(profile.ProfileId, issue.Location.ProfileId);
            Assert.AreEqual(profile.SkeletonProfileId, issue.Location.SkeletonProfileId);
            Assert.AreEqual(artifact.ArtifactHash, issue.Location.ArtifactHash);
        }

        [Test]
        public void BakeArtifactValidator_ReportsCompatibilityExpectationMismatch()
        {
            MxAnimationBakeProfile profile = CreateProfile();
            var artifact = new MxAnimationBakeArtifact(profile, new[] { Trace(localFrame: 1) });
            var expectation = new MxAnimationBakeExpectation(
                compatibilityExpectation: new MxAnimationCompatibilityExpectation(
                    skeletonProfileId: profile.SkeletonProfileId,
                    skeletonProfileHash: "sha256:expected-skeleton"));

            MxAnimationBakeValidationReport report = MxAnimationBakeArtifactValidator.Validate(artifact, expectation);

            Assert.IsTrue(report.HasErrors);
            Assert.That(report.Issues.Select(i => i.Code), Contains.Item(MxAnimationCompatibilityIssueCodes.BakeArtifactSkeletonProfileHashMismatch));
        }

        [Test]
        public void BakeQuantizer_UsesConfiguredRoundingPolicy()
        {
            Assert.AreEqual(1250, MxAnimationBakeQuantizer.Quantize(1.25d, 1000, MxAnimationBakeRoundingPolicy.RoundNearest));
            Assert.AreEqual(12, MxAnimationBakeQuantizer.Quantize(1.29d, 10, MxAnimationBakeRoundingPolicy.Floor));
            Assert.AreEqual(13, MxAnimationBakeQuantizer.Quantize(1.21d, 10, MxAnimationBakeRoundingPolicy.Ceiling));
        }

        [Test]
        public void EditorBakeTool_SamplesClipCurvesEventsAndProducesStableArtifact()
        {
            AnimationClip clip = CreateClip();

            MxAnimationBakeEditorResult first = MxAnimationBakeEditorTool.BakeClip(clip);
            MxAnimationBakeEditorResult second = MxAnimationBakeEditorTool.BakeClip(clip);

            Assert.IsTrue(first.Success, first.ReportText);
            Assert.AreEqual(first.Artifact.ArtifactHash, second.Artifact.ArtifactHash);
            Assert.Greater(first.Artifact.WeaponTraceFrames.Count, 0);
            Assert.Greater(first.Artifact.SocketFrames.Count, first.Artifact.RootMotionFrames.Count);
            Assert.AreEqual(2000, first.Artifact.RootMotionFrames[first.Artifact.RootMotionFrames.Count - 1].RootPosition.X);
            Assert.AreEqual(3000, first.Artifact.SocketFrames.Last(frame => frame.SocketId == "weapon").Position.X);
            Assert.AreEqual(3000, first.Artifact.WeaponTraceFrames[first.Artifact.WeaponTraceFrames.Count - 1].RootNow.X);
            Assert.AreEqual(4000, first.Artifact.WeaponTraceFrames[first.Artifact.WeaponTraceFrames.Count - 1].TipNow.X);
            Assert.AreEqual(MxAnimationBakeEventKind.Footstep, first.Artifact.EventMarkers[0].Kind);
            Assert.AreEqual(first.Artifact.EventMarkers[0].LocalFrame, first.Artifact.EventMarkers[0].PresentationFrame);
            Assert.AreEqual(-1, first.Artifact.EventMarkers[0].CombatFrame);
            Assert.That(first.ReportText, Does.Contain("sourceClipHash: sha256:"));
            Assert.That(first.ReportText, Does.Contain("profileId: mxanimation.bake.bake_test"));
            Assert.That(first.ReportText, Does.Contain("skeletonProfileId: skeleton"));
            Assert.That(first.ReportText, Does.Contain("importSettingsFingerprint:"));
            Assert.That(first.ReportText, Does.Contain("socketTrajectoryFrames:"));
            Assert.That(first.ReportText, Does.Contain("eventAlignment:"));
        }

        [Test]
        public void EditorCompatibilityExtractor_ValidatesSkeletonClipAndAvatarMaskWithoutPlayMode()
        {
            GameObject root = CreateSkeletonRoot();
            try
            {
                AnimationClip clip = new AnimationClip { name = "Compatibility Test", frameRate = 30f };
                SetCurve(clip, "Hips/Spine", "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
                var avatarMask = new AvatarMask { transformCount = 2 };
                avatarMask.SetTransformPath(0, "Hips/Spine");
                avatarMask.SetTransformActive(0, true);
                avatarMask.SetTransformPath(1, "Hips/Leg");
                avatarMask.SetTransformActive(1, false);
                ResourceKey clipKey = new ResourceKey("demo.animation.compatibility", ResourceTypeIds.AnimationClip);
                ResourceKey maskKey = new ResourceKey("demo.animation.mask.upper_body", ResourceTypeIds.AvatarMask);

                MxAnimationSkeletonCompatibilityProfile skeleton =
                    MxAnimationCompatibilityEditorExtractor.CreateSkeletonProfile(
                        root,
                        "humanoid",
                        new[] { "Hips/Spine/WeaponSocket" });
                MxAnimationCompatibilityProfile profile = MxAnimationCompatibilityEditorExtractor.CreateProfile(
                    skeleton,
                    new[] { MxAnimationCompatibilityEditorExtractor.CreateClipProfile(clip, clipKey, skeleton) },
                    new[] { MxAnimationCompatibilityEditorExtractor.CreateAvatarMaskProfile(avatarMask, maskKey, skeleton) });
                var expectation = new MxAnimationCompatibilityExpectation(
                    skeleton.ProfileId,
                    skeleton.ProfileHash,
                    new[] { "Hips/Spine" },
                    new[] { "Hips/Spine/WeaponSocket" },
                    new[] { new MxAnimationClipCompatibilityExpectation(clipKey, new[] { "Hips/Spine" }) },
                    new[] { new MxAnimationAvatarMaskCompatibilityExpectation(maskKey, new[] { "Hips/Spine" }) });

                MxAnimationCompatibilityValidationReport report =
                    MxAnimationCompatibilityEditorExtractor.Validate(profile, expectation);

                Assert.IsFalse(report.HasErrors, Describe(report));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EditorCompatibilityExtractor_DoesNotStampMissingSocketOrWrongSkeleton()
        {
            GameObject root = CreateSkeletonRoot();
            try
            {
                AnimationClip clip = new AnimationClip { name = "Wrong Skeleton Test", frameRate = 30f };
                SetCurve(clip, "Hips/MissingArm", "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
                ResourceKey clipKey = new ResourceKey("demo.animation.wrong_skeleton", ResourceTypeIds.AnimationClip);
                MxAnimationSkeletonCompatibilityProfile skeleton =
                    MxAnimationCompatibilityEditorExtractor.CreateSkeletonProfile(
                        root,
                        "humanoid",
                        new[] { "Hips/Spine/MissingSocket" });
                MxAnimationCompatibilityProfile profile = MxAnimationCompatibilityEditorExtractor.CreateProfile(
                    skeleton,
                    new[] { MxAnimationCompatibilityEditorExtractor.CreateClipProfile(clip, clipKey, skeleton) });
                var expectation = new MxAnimationCompatibilityExpectation(
                    skeleton.ProfileId,
                    skeleton.ProfileHash,
                    requiredSocketPaths: new[] { "Hips/Spine/MissingSocket" },
                    clipExpectations: new[]
                    {
                        new MxAnimationClipCompatibilityExpectation(clipKey, new[] { "Hips/MissingArm" })
                    });

                MxAnimationCompatibilityValidationReport report =
                    MxAnimationCompatibilityEditorExtractor.Validate(profile, expectation);

                Assert.IsTrue(report.HasErrors);
                Assert.That(report.Issues.Select(i => i.Code), Contains.Item(MxAnimationCompatibilityIssueCodes.SocketPathMissing));
                Assert.That(report.Issues.Select(i => i.Code), Contains.Item(MxAnimationCompatibilityIssueCodes.ClipSkeletonProfileHashMismatch));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EditorBakeTool_SourceClipHashChangesWhenEventsChange()
        {
            AnimationClip firstClip = CreateClip();
            AnimationClip secondClip = CreateClip();
            SetEvents(
                secondClip,
                new[]
                {
                    new AnimationEvent { time = 0.5f, functionName = "hit_right" }
                });

            MxAnimationBakeEditorResult first = MxAnimationBakeEditorTool.BakeClip(firstClip);
            MxAnimationBakeEditorResult second = MxAnimationBakeEditorTool.BakeClip(secondClip);

            Assert.AreNotEqual(first.Artifact.Profile.SourceClipHash, second.Artifact.Profile.SourceClipHash);
        }

        private static void SetEvents(AnimationClip clip, AnimationEvent[] events)
        {
            AnimationUtility.SetAnimationEvents(clip, events);
        }

        private static MxAnimationBakeProfile CreateProfile()
        {
            return new MxAnimationBakeProfile(
                "profile.test",
                new ResourceKey("demo.animation.attack", ResourceTypeIds.AnimationClip),
                "sha256:source",
                "skeleton.test",
                "sha256:skeleton",
                sampleTickRate: 30,
                quantizationScale: 1000,
                MxAnimationBakeCoordinateSpace.Local,
                MxAnimationBakeRoundingPolicy.RoundNearest,
                "import:test");
        }

        private static MxAnimationBakedWeaponTraceFrame Trace(int localFrame)
        {
            return new MxAnimationBakedWeaponTraceFrame(
                localFrame,
                traceId: 7,
                socketId: "weapon",
                new MxAnimationBakedVector3(localFrame, 0, 0),
                new MxAnimationBakedVector3(localFrame, 0, 1000),
                new MxAnimationBakedVector3(localFrame + 1, 0, 0),
                new MxAnimationBakedVector3(localFrame + 1, 0, 1000));
        }

        private static MxAnimationBakedRootMotionFrame Root(int localFrame)
        {
            return new MxAnimationBakedRootMotionFrame(
                localFrame,
                new MxAnimationBakedVector3(localFrame, 0, 0),
                new MxAnimationBakedVector3(1, 0, 0));
        }

        private static MxAnimationBakedSocketFrame Socket(int localFrame, long x = 0)
        {
            return new MxAnimationBakedSocketFrame(
                localFrame,
                "weapon",
                "Hips/Spine/WeaponSocket",
                new MxAnimationBakedVector3(x, localFrame, 0),
                new MxAnimationBakedVector3(1, 0, 0));
        }

        private static MxAnimationBakedEventMarker Marker(int localFrame, int sourceOrder, int presentationFrame = -1)
        {
            return new MxAnimationBakedEventMarker(
                localFrame,
                "event:" + sourceOrder,
                MxAnimationBakeEventKind.Marker,
                sourceOrder: sourceOrder,
                presentationFrame: presentationFrame);
        }

        private static GameObject CreateSkeletonRoot()
        {
            var root = new GameObject("Root");
            var hips = new GameObject("Hips");
            hips.transform.SetParent(root.transform);
            var spine = new GameObject("Spine");
            spine.transform.SetParent(hips.transform);
            var weaponSocket = new GameObject("WeaponSocket");
            weaponSocket.transform.SetParent(spine.transform);
            return root;
        }

        private static AnimationClip CreateClip()
        {
            var clip = new AnimationClip { name = "Bake Test", frameRate = 30f };
            SetCurve(clip, string.Empty, "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 2f));
            SetCurve(clip, string.Empty, "m_LocalPosition.y", AnimationCurve.Linear(0f, 0f, 1f, 0f));
            SetCurve(clip, string.Empty, "m_LocalPosition.z", AnimationCurve.Linear(0f, 0f, 1f, 0f));
            SetCurve(clip, "WeaponSocket", "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 3f));
            SetCurve(clip, "WeaponSocket", "m_LocalPosition.y", AnimationCurve.Linear(0f, 0f, 1f, 0f));
            SetCurve(clip, "WeaponSocket", "m_LocalPosition.z", AnimationCurve.Linear(0f, 0f, 1f, 0f));
            SetCurve(clip, "WeaponTip", "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 4f));
            SetCurve(clip, "WeaponTip", "m_LocalPosition.y", AnimationCurve.Linear(0f, 0f, 1f, 0f));
            SetCurve(clip, "WeaponTip", "m_LocalPosition.z", AnimationCurve.Linear(0f, 1f, 1f, 1f));
            SetEvents(
                clip,
                new[]
                {
                    new AnimationEvent { time = 0.5f, functionName = "foot_left" }
                });
            return clip;
        }

        private static void SetCurve(AnimationClip clip, string path, string propertyName, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), propertyName),
                curve);
        }

        private static string Describe(MxAnimationCompatibilityValidationReport report)
        {
            return string.Join("\n", report.Issues.Select(issue =>
                issue.Code + " " + issue.Field + " " + issue.Key + " expected=" + issue.Expected + " actual=" + issue.Actual + " " + issue.Message));
        }
    }
}
