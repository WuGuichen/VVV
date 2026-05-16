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
                new[] { Marker(localFrame: 4, sourceOrder: 1), Marker(localFrame: 3, sourceOrder: 0) });
            var second = new MxAnimationBakeArtifact(
                profile,
                new[] { Trace(localFrame: 1), Trace(localFrame: 2) },
                new[] { Root(localFrame: 0), Root(localFrame: 1) },
                new[] { Marker(localFrame: 3, sourceOrder: 0), Marker(localFrame: 4, sourceOrder: 1) });

            Assert.AreEqual(first.ArtifactHash, second.ArtifactHash);
            Assert.IsFalse(MxAnimationBakeArtifactValidator.Validate(first).HasErrors);
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
            Assert.AreEqual(2000, first.Artifact.RootMotionFrames[first.Artifact.RootMotionFrames.Count - 1].RootPosition.X);
            Assert.AreEqual(MxAnimationBakeEventKind.Footstep, first.Artifact.EventMarkers[0].Kind);
            Assert.That(first.ReportText, Does.Contain("sourceClipHash: sha256:"));
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

        private static MxAnimationBakedEventMarker Marker(int localFrame, int sourceOrder)
        {
            return new MxAnimationBakedEventMarker(localFrame, "event:" + sourceOrder, MxAnimationBakeEventKind.Marker, sourceOrder: sourceOrder);
        }

        private static AnimationClip CreateClip()
        {
            var clip = new AnimationClip { name = "Bake Test", frameRate = 30f };
            SetCurve(clip, string.Empty, "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 2f));
            SetCurve(clip, string.Empty, "m_LocalPosition.y", AnimationCurve.Linear(0f, 0f, 1f, 0f));
            SetCurve(clip, string.Empty, "m_LocalPosition.z", AnimationCurve.Linear(0f, 0f, 1f, 0f));
            SetCurve(clip, "WeaponTip", "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 2f));
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
    }
}
