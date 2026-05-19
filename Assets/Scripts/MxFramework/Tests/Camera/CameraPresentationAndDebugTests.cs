using System.Linq;
using MxFramework.Animation;
using MxFramework.Camera;
using MxFramework.Camera.Animation;
using MxFramework.DebugUI.Adapters;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Camera
{
    public sealed class CameraPresentationAndDebugTests
    {
        [Test]
        public void PresentationSink_ReusesAnimationDedupeAndEnqueuesShakeOnce()
        {
            var service = new MxCameraService(new MxCameraRigId("rig"));
            var resolver = new MxCameraDictionaryPresentationEventPayloadResolver();
            var key = new ResourceKey("camera.shake", "camera");
            resolver.Register(key, new MxCameraPresentationEventPayload(MxCameraPresentationEffectKind.Shake, 1f, priority: 5));
            var cameraSink = new MxCameraPresentationEventSink(service, resolver);
            var dispatchSink = new MxAnimationPresentationEventDispatchSink(cameraSink);
            var evt = new MxAnimationPresentationEvent("shake", MxAnimationEventTimeDomain.CombatFrame, 1f, "Camera", key);
            var dispatch = new MxAnimationPresentationEventDispatch("actor", "action", "binding", 1, 1, 1, 0, evt);

            dispatchSink.TryDispatch(dispatch, payloadResolved: true, out _);
            dispatchSink.TryDispatch(dispatch, payloadResolved: true, out MxAnimationPresentationEventDispatchDiagnostic duplicate);
            MxCameraEvaluationResult result = service.Evaluate(new MxCameraEvaluationContext(
                1,
                1f / 60f,
                new MxCameraRigId("rig"),
                1920f,
                1080f,
                MxCameraState.Empty,
                new[] { Profile() },
                new[] { Target() }));

            Assert.AreEqual(MxAnimationPresentationEventDispatchStatus.DuplicateDropped, duplicate.Status);
            Assert.AreEqual(1, result.AcceptedRequestIds.Count);
            Assert.Greater(result.State.ShakeOffset.Magnitude, 0f);
        }

        [Test]
        public void PresentationSink_MissingPayloadReportsStableDiagnostic()
        {
            var cameraSink = new MxCameraPresentationEventSink(new MxCameraService(new MxCameraRigId("rig")), null);
            var evt = new MxAnimationPresentationEvent("shake", MxAnimationEventTimeDomain.CombatFrame, 1f, "Camera", new ResourceKey("missing", "camera"));
            var dispatch = new MxAnimationPresentationEventDispatch("actor", "action", "binding", 1, 1, 1, 0, evt);

            cameraSink.Dispatch(dispatch);

            Assert.IsTrue(cameraSink.RecentDiagnostics.Any(d => d.Code == MxCameraDiagnosticCodes.EventPayloadMissing));
        }

        [Test]
        public void CameraDebugSource_ExportsSnapshotSections()
        {
            var snapshot = new MxCameraDebugSnapshot(
                true,
                new MxCameraRigId("rig"),
                "UnityCamera",
                new MxCameraProfileId("profile"),
                MxCameraMode.GroupFollowPerspective,
                new MxCameraTargetGroupState(default, MxCameraVector3.Zero, MxCameraVector3.Zero, MxCameraVector3.Zero, 1f, new MxCameraTargetRef("target"), 1, true),
                MxCameraState.Empty,
                new[] { new MxCameraDiagnostic(MxCameraDiagnosticCodes.GroupBoundsExceeded, "too wide") },
                2);

            var source = new CameraDebugSource(() => snapshot);
            var debug = source.CreateSnapshot();

            Assert.IsTrue(source.IsAvailable);
            Assert.AreEqual("Camera", debug.SourceName);
            Assert.IsTrue(debug.Sections.Any(s => s.Title == "Diagnostics" && s.Body.Contains(MxCameraDiagnosticCodes.GroupBoundsExceeded)));
        }

        private static MxCameraProfileDefinition Profile()
        {
            return new MxCameraProfileDefinition
            {
                ProfileId = new MxCameraProfileId("profile"),
                Mode = MxCameraMode.GroupFollowPerspective,
                Distance = 8f,
                MinDistance = 2f,
                MaxDistance = 20f,
                FieldOfView = 60f,
                MinFieldOfView = 30f,
                MaxFieldOfView = 90f,
                OrthographicSize = 5f,
                MinOrthographicSize = 1f,
                MaxOrthographicSize = 20f,
                TargetPadding = 0.5f,
                TargetLostGraceFrames = 2,
                MaxTargetRadius = 64f,
                ShakeLimit = 1f,
                Pitch = 35f
            };
        }

        private static MxCameraTargetSnapshot Target()
        {
            return new MxCameraTargetSnapshot(
                new MxCameraTargetRef("target"),
                MxCameraVector3.Zero,
                MxCameraVector3.Forward,
                MxCameraVector3.Up,
                MxCameraVector3.Zero,
                MxCameraVector3.Zero,
                new MxCameraVector3(0.5f, 0.5f, 0.5f),
                1f,
                true,
                true,
                1);
        }
    }
}
