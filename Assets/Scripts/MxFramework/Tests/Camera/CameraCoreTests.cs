using System.Linq;
using MxFramework.Camera;
using NUnit.Framework;

namespace MxFramework.Tests.Camera
{
    public sealed class CameraCoreTests
    {
        [Test]
        public void Evaluate_SingleTargetFollow_ProducesStableState()
        {
            var service = new MxCameraService(new MxCameraRigId("rig"));
            MxCameraEvaluationResult result = service.Evaluate(CreateContext(Target("actor", 2f, 0f, 4f, primary: true)));

            Assert.AreEqual(MxCameraEvaluationStatus.Success, result.Status);
            Assert.AreEqual("profile", result.ActiveProfileId.Value);
            Assert.AreEqual(1, result.TargetGroupState.ValidTargetCount);
            Assert.AreEqual("actor", result.TargetGroupState.PrimaryTarget.Value);
            Assert.AreEqual(MxCameraStateSource.Normal, result.State.Source);
        }

        [Test]
        public void Evaluate_GroupPerspective_DerivesDistanceFromRadius()
        {
            var service = new MxCameraService(new MxCameraRigId("rig"));
            MxCameraEvaluationResult result = service.Evaluate(CreateContext(
                Target("a", -3f, 0f, 0f, primary: true),
                Target("b", 3f, 0f, 0f, primary: false)));

            Assert.AreEqual(2, result.TargetGroupState.ValidTargetCount);
            Assert.Greater(result.TargetGroupState.Radius, 3f);
            Assert.AreEqual(MxCameraProjectionKind.Perspective, result.State.ProjectionKind);
            Assert.GreaterOrEqual(result.State.FramingUtilization, 0f);
        }

        [Test]
        public void Evaluate_GroupOrthographic_DerivesOrthographicSize()
        {
            var service = new MxCameraService(new MxCameraRigId("rig"));
            MxCameraProfileDefinition profile = Profile();
            profile.Mode = MxCameraMode.GroupFollowOrthographic;
            profile.OrthographicSize = 2f;
            MxCameraEvaluationResult result = service.Evaluate(CreateContext(
                profile,
                Target("a", -5f, 0f, 0f, primary: true),
                Target("b", 5f, 0f, 0f, primary: false)));

            Assert.AreEqual(MxCameraProjectionKind.Orthographic, result.State.ProjectionKind);
            Assert.Greater(result.State.OrthographicSize, 2f);
        }

        [Test]
        public void Evaluate_TargetLost_UsesGraceThenFallback()
        {
            MxCameraProfileDefinition profile = Profile();
            profile.TargetLostGraceFrames = 1;
            var service = new MxCameraService(new MxCameraRigId("rig"));

            service.Evaluate(CreateContext(profile, Target("a", 0f, 0f, 0f, primary: true)));
            MxCameraEvaluationResult grace = service.Evaluate(CreateContext(profile));
            MxCameraEvaluationResult fallback = service.Evaluate(CreateContext(profile));

            Assert.AreEqual(MxCameraStateSource.Grace, grace.State.Source);
            Assert.IsTrue(grace.Diagnostics.Any(d => d.Code == MxCameraDiagnosticCodes.TargetLost));
            Assert.AreEqual(MxCameraEvaluationStatus.FallbackUsed, fallback.Status);
            Assert.AreEqual(MxCameraStateSource.Fallback, fallback.State.Source);
        }

        [Test]
        public void Evaluate_RequestOrdering_ReportsProfileConflict()
        {
            var service = new MxCameraService(new MxCameraRigId("rig"));
            MxCameraProfileDefinition high = Profile("high");
            MxCameraProfileDefinition low = Profile("low");
            var requests = new[]
            {
                new MxCameraRequest(1, 1, 2, "test", MxCameraRequestKind.SetProfile, priority: 10, profileId: high.ProfileId),
                new MxCameraRequest(2, 1, 1, "test", MxCameraRequestKind.SetProfile, priority: 10, profileId: low.ProfileId)
            };

            var context = new MxCameraEvaluationContext(
                1,
                1f / 60f,
                new MxCameraRigId("rig"),
                1920f,
                1080f,
                MxCameraState.Empty,
                new[] { high, low },
                new[] { Target("a", 0f, 0f, 0f, primary: true) },
                requests);
            MxCameraEvaluationResult result = service.Evaluate(context);

            Assert.AreEqual("high", result.ActiveProfileId.Value);
            Assert.IsTrue(result.Diagnostics.Any(d => d.Code == MxCameraDiagnosticCodes.RequestConflict));
        }

        [Test]
        public void ValidateProfile_ReportsStableInvalidProfileCode()
        {
            var validator = new MxCameraProfileValidator();
            MxCameraProfileDefinition profile = Profile();
            profile.Distance = -1f;

            Assert.IsTrue(validator.Validate(profile).Any(d => d.Code == MxCameraDiagnosticCodes.InvalidProfile && d.Field == "Distance"));
        }

        [Test]
        public void Evaluate_Shake_IsClampedByProfile()
        {
            MxCameraProfileDefinition profile = Profile();
            profile.ShakeLimit = 0.5f;
            var service = new MxCameraService(new MxCameraRigId("rig"));
            var request = new MxCameraRequest(3, 1, 0, "test", MxCameraRequestKind.Shake, floatValue: 99f);
            var context = new MxCameraEvaluationContext(
                1,
                1f / 60f,
                new MxCameraRigId("rig"),
                1920f,
                1080f,
                MxCameraState.Empty,
                new[] { profile },
                new[] { Target("a", 0f, 0f, 0f, primary: true) },
                new[] { request });

            MxCameraEvaluationResult result = service.Evaluate(context);

            Assert.LessOrEqual(result.State.ShakeOffset.Magnitude, 0.5001f);
            Assert.Contains(3UL, result.AcceptedRequestIds.ToList());
        }

        private static MxCameraEvaluationContext CreateContext(params MxCameraTargetSnapshot[] targets)
        {
            return CreateContext(Profile(), targets);
        }

        private static MxCameraEvaluationContext CreateContext(MxCameraProfileDefinition profile, params MxCameraTargetSnapshot[] targets)
        {
            return new MxCameraEvaluationContext(
                1,
                1f / 60f,
                new MxCameraRigId("rig"),
                1920f,
                1080f,
                MxCameraState.Empty,
                new[] { profile },
                targets);
        }

        private static MxCameraProfileDefinition Profile(string id = "profile")
        {
            return new MxCameraProfileDefinition
            {
                ProfileId = new MxCameraProfileId(id),
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
                Pitch = 35f,
                Yaw = 0f
            };
        }

        private static MxCameraTargetSnapshot Target(string id, float x, float y, float z, bool primary)
        {
            var position = new MxCameraVector3(x, y, z);
            return new MxCameraTargetSnapshot(
                new MxCameraTargetRef(id),
                position,
                MxCameraVector3.Forward,
                MxCameraVector3.Up,
                MxCameraVector3.Zero,
                position,
                new MxCameraVector3(0.5f, 0.5f, 0.5f),
                1f,
                primary,
                true,
                1);
        }
    }
}
