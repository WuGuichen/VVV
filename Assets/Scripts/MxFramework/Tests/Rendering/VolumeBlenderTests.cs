using System.IO;
using System.Linq;
using MxFramework.Diagnostics;
using MxFramework.Rendering;
using NUnit.Framework;

namespace MxFramework.Tests.Rendering
{
    public class VolumeBlenderTests
    {
        [Test]
        public void VolumeBlender_RequestId_IsStableUntilReleaseOrExpiry()
        {
            var blender = new VolumeBlender();
            MxVolumeRequestId requestId = blender.Request(Request("profile-a", 10, Timing(0f, 0f, 1f)));

            Assert.IsTrue(requestId.IsValid);
            Assert.IsTrue(blender.TryGetRequest(requestId, out MxVolumeRequestSnapshot beforeRelease));
            Assert.AreEqual(requestId, beforeRelease.RequestId);

            blender.CaptureBlendState(Context(0.25f));
            Assert.IsTrue(blender.Release(requestId));
            Assert.IsTrue(blender.TryGetRequest(requestId, out MxVolumeRequestSnapshot released));
            Assert.AreEqual(requestId, released.RequestId);
            Assert.AreEqual(MxVolumeRequestPhase.Released, released.Phase);

            blender.CaptureBlendState(Context(2f));

            Assert.IsFalse(blender.TryGetRequest(requestId, out _));
            MxVolumeDiagnosticsSnapshot diagnostics = blender.CaptureDiagnostics();
            Assert.AreEqual(1, diagnostics.ExpiredRequests.Count);
            Assert.AreEqual(requestId, diagnostics.ExpiredRequests[0].RequestId);
            Assert.AreEqual(MxVolumeRequestCleanupReason.Released, diagnostics.ExpiredRequests[0].CleanupReason);
        }

        [Test]
        public void VolumeBlender_RequestLookup_ReturnsCurrentWeight()
        {
            var blender = new VolumeBlender();
            MxVolumeRequestId requestId = blender.Request(Request("profile-a", 1, Timing(2f, 0f, 1f)));

            blender.CaptureBlendState(Context(1f));

            Assert.IsTrue(blender.TryGetRequest(requestId, out MxVolumeRequestSnapshot snapshot));
            Assert.AreEqual(MxVolumeRequestPhase.BlendIn, snapshot.Phase);
            Assert.AreEqual(0.5f, snapshot.Weight, 0.0001f);
        }

        [Test]
        public void VolumeBlender_Priority_UsesStableTieBreakerForEqualPriority()
        {
            var blender = new VolumeBlender();
            MxVolumeRequestId first = blender.Request(Request("profile-first", 5, Timing(0f, 0f, 1f)));
            MxVolumeRequestId second = blender.Request(Request("profile-second", 5, Timing(0f, 0f, 1f)));
            MxVolumeRequestId high = blender.Request(Request("profile-high", 10, Timing(0f, 0f, 1f)));

            MxVolumeBlendStateSnapshot state = blender.CaptureBlendState(Context(0f));

            Assert.AreEqual(high, state.AppliedProfiles[0].SourceRequestId);
            Assert.AreEqual(2, state.SuppressedRequests.Count);
            Assert.AreEqual(first, state.SuppressedRequests[0].RequestId);
            Assert.AreEqual(second, state.SuppressedRequests[1].RequestId);

            blender.Release(high);
            blender.CaptureBlendState(Context(2f));

            state = blender.CaptureBlendState(Context(2.1f));
            Assert.AreEqual(first, state.AppliedProfiles[0].SourceRequestId);
            Assert.AreEqual(second, state.SuppressedRequests[0].RequestId);
        }

        [Test]
        public void VolumeBlender_Lifetime_BlendInHoldBlendOutAndZeroDurations()
        {
            var blender = new VolumeBlender();
            blender.Request(Request("timed", 1, Timing(2f, 3f, 4f)));

            AssertRequest(blender.CaptureBlendState(Context(1f)).ActiveRequests[0], MxVolumeRequestPhase.BlendIn, 0.5f);
            AssertRequest(blender.CaptureBlendState(Context(2.5f)).ActiveRequests[0], MxVolumeRequestPhase.Hold, 1f);
            AssertRequest(blender.CaptureBlendState(Context(6f)).ActiveRequests[0], MxVolumeRequestPhase.BlendOut, 0.75f);
            Assert.AreEqual(0, blender.CaptureBlendState(Context(9f)).AppliedProfiles.Count);

            var zero = new VolumeBlender();
            MxVolumeRequestId zeroId = zero.Request(Request("zero", 1, Timing(0f, 0f, 0f)));
            AssertRequest(zero.CaptureBlendState(Context(0f)).ActiveRequests[0], MxVolumeRequestPhase.Hold, 1f);
            Assert.IsTrue(zero.Release(zeroId));
            Assert.AreEqual(0, zero.CaptureBlendState(Context(0f)).AppliedProfiles.Count);
            Assert.AreEqual(MxVolumeRequestCleanupReason.Released, zero.CaptureDiagnostics().ExpiredRequests[0].CleanupReason);
        }

        [Test]
        public void VolumeBlender_Release_IsIdempotentAndStartsBlendOut()
        {
            var blender = new VolumeBlender();
            MxVolumeRequestId requestId = blender.Request(Request("profile-a", 1, Timing(2f, 0f, 2f)));

            blender.CaptureBlendState(Context(1f));

            Assert.IsTrue(blender.Release(requestId));
            Assert.IsFalse(blender.Release(requestId));

            MxVolumeRequestSnapshot released = blender.CaptureBlendState(Context(2f)).ActiveRequests[0];
            Assert.AreEqual(MxVolumeRequestPhase.Released, released.Phase);
            Assert.AreEqual(0.25f, released.Weight, 0.0001f);
        }

        [Test]
        public void VolumeBlender_Scope_GlobalAppliesToAllCameraKinds()
        {
            var blender = new VolumeBlender();
            MxVolumeRequestId global = blender.Request(Request("global", 1, Timing(0f, 0f, 1f), MxVolumeRequestScope.Global()));

            Assert.AreEqual(global, blender.CaptureBlendState(Context(0f, MxCameraRenderKind.Game)).AppliedProfiles[0].SourceRequestId);
            Assert.AreEqual(global, blender.CaptureBlendState(Context(0f, MxCameraRenderKind.SceneView)).AppliedProfiles[0].SourceRequestId);
            Assert.AreEqual(global, blender.CaptureBlendState(Context(0f, MxCameraRenderKind.Preview)).AppliedProfiles[0].SourceRequestId);
        }

        [Test]
        public void VolumeBlender_Scope_CameraKindAndExplicitCameraAreIsolated()
        {
            var blender = new VolumeBlender();
            var tokenA = new MxRenderingCameraToken(11);
            var tokenB = new MxRenderingCameraToken(12);
            MxVolumeRequestId game = blender.Request(Request("game", 5, Timing(0f, 0f, 1f), MxVolumeRequestScope.ForCameraKind(MxCameraRenderKind.Game)));
            MxVolumeRequestId scene = blender.Request(Request("scene", 10, Timing(0f, 0f, 1f), MxVolumeRequestScope.ForCameraKind(MxCameraRenderKind.SceneView)));
            MxVolumeRequestId explicitA = blender.Request(Request("explicit-a", 20, Timing(0f, 0f, 1f), MxVolumeRequestScope.ForExplicitCamera(tokenA)));

            Assert.AreEqual(game, blender.CaptureBlendState(Context(0f, MxCameraRenderKind.Game, tokenB)).AppliedProfiles[0].SourceRequestId);
            Assert.AreEqual(scene, blender.CaptureBlendState(Context(0f, MxCameraRenderKind.SceneView, tokenB)).AppliedProfiles[0].SourceRequestId);
            Assert.AreEqual(explicitA, blender.CaptureBlendState(Context(0f, MxCameraRenderKind.Game, tokenA)).AppliedProfiles[0].SourceRequestId);
            Assert.AreEqual(0, blender.CaptureBlendState(Context(0f, MxCameraRenderKind.Preview, tokenB)).AppliedProfiles.Count);
        }

        [Test]
        public void VolumeBlender_Diagnostics_ReportsActiveExpiredSuppressedWeightsAndAppliedState()
        {
            var blender = new VolumeBlender();
            MxVolumeRequestId lower = blender.Request(Request("lower", 1, Timing(0f, 0f, 1f), debugName: "lower-debug"));
            MxVolumeRequestId higher = blender.Request(Request("higher", 5, Timing(0f, 1f, 0.5f), debugName: "higher-debug"));

            MxVolumeBlendStateSnapshot state = blender.CaptureBlendState(Context(0.25f));

            Assert.AreEqual(higher, state.AppliedProfiles[0].SourceRequestId);
            Assert.AreEqual(lower, state.SuppressedRequests[0].RequestId);
            Assert.AreEqual(1f, state.SuppressedRequests[0].Weight, 0.0001f);

            blender.CaptureBlendState(Context(2f));
            MxVolumeDiagnosticsSnapshot diagnostics = blender.CaptureDiagnostics();

            Assert.AreEqual(1, diagnostics.ActiveRequests.Count);
            Assert.AreEqual(lower, diagnostics.ActiveRequests[0].RequestId);
            Assert.AreEqual(1, diagnostics.ExpiredRequests.Count);
            Assert.AreEqual(higher, diagnostics.ExpiredRequests[0].RequestId);
            Assert.AreEqual(MxVolumeRequestCleanupReason.AutoExpired, diagnostics.ExpiredRequests[0].CleanupReason);
            Assert.GreaterOrEqual(diagnostics.RecentBlendStates.Count, 2);
            Assert.AreEqual(1, diagnostics.RecentBlendStates[0].SuppressedRequests.Count);
            Assert.AreEqual(higher, diagnostics.RecentBlendStates[0].AppliedProfiles[0].SourceRequestId);
        }

        [Test]
        public void VolumeBlenderDebugSource_ExposesDiagnosticsSection()
        {
            var blender = new VolumeBlender();
            blender.Request(Request("profile-a", 1, Timing(0f, 0f, 1f), debugName: "debug-a"));
            blender.CaptureBlendState(Context(0f));
            var source = new VolumeBlenderDebugSource(blender);

            FrameworkDebugSnapshot snapshot = source.CreateSnapshot();

            Assert.AreEqual("Rendering", snapshot.SourceName);
            Assert.AreEqual(FrameworkDebugMode.Runtime, snapshot.Mode);
            Assert.AreEqual(RenderingDebugSectionNames.VolumeBlender, snapshot.Sections[0].Title);
            StringAssert.Contains("activeRequests: 1", snapshot.Sections[0].Body);
            StringAssert.Contains("applied id=", snapshot.Sections[0].Body);
            StringAssert.Contains("debugName=debug-a", snapshot.Sections[0].Body);
        }

        [Test]
        public void VolumeBlender_Dependencies_DoNotReferenceForbiddenModulesOrLegacyPostProcessing()
        {
            string asmdef = File.ReadAllText("Assets/Scripts/MxFramework/Rendering/MxFramework.Rendering.asmdef");

            Assert.IsFalse(asmdef.Contains("MxFramework.Camera"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Gameplay"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Combat"));
            Assert.IsFalse(asmdef.Contains("MxFramework.Runtime"));
            Assert.IsFalse(asmdef.Contains("MxFramework.DebugUI"));
            Assert.IsFalse(asmdef.Contains("PostProcessing"));

            string[] renderingSources = Directory.GetFiles("Assets/Scripts/MxFramework/Rendering", "*.cs", SearchOption.AllDirectories);
            Assert.IsFalse(renderingSources.Any(path => File.ReadAllText(path).Contains("UnityEngine.Rendering.PostProcessing")), "Legacy Post Processing Stack v2 must not be referenced.");
            CollectionAssert.AreEquivalent(
                new[] { "Assets/Scripts/MxFramework/Rendering/MxRenderingPipelineFeature.cs" },
                renderingSources.Where(path => File.ReadAllText(path).Contains("ScriptableRendererFeature")).ToArray());
        }

        private static MxVolumeRequestDescriptor Request(
            string key,
            int priority,
            MxVolumeBlendTiming timing,
            MxVolumeRequestScope scope = default,
            string debugName = null)
        {
            return new MxVolumeRequestDescriptor(new MxVolumeProfileReference(key), scope, priority, timing, debugName);
        }

        private static MxVolumeBlendTiming Timing(float blendIn, float hold, float blendOut)
        {
            return new MxVolumeBlendTiming(blendIn, hold, blendOut);
        }

        private static MxVolumeEvaluationContext Context(
            float presentationTimeSeconds,
            MxCameraRenderKind cameraKind = MxCameraRenderKind.Game,
            MxRenderingCameraToken cameraToken = default)
        {
            return new MxVolumeEvaluationContext(cameraKind, cameraToken, presentationTimeSeconds);
        }

        private static void AssertRequest(MxVolumeRequestSnapshot snapshot, MxVolumeRequestPhase phase, float weight)
        {
            Assert.AreEqual(phase, snapshot.Phase);
            Assert.AreEqual(weight, snapshot.Weight, 0.0001f);
        }
    }
}
