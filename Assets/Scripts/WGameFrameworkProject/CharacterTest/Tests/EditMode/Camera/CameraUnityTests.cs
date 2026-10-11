using MxFramework.Camera;
using MxFramework.Camera.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Tests.Camera
{
    public sealed class CameraUnityTests
    {
        [Test]
        public void UnityRig_ApplyLate_AppliesTransformAndPerspective()
        {
            GameObject go = new GameObject("camera-test");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                MxCameraUnityRig rig = go.AddComponent<MxCameraUnityRig>();
                var state = new MxCameraState(
                    new MxCameraRigId("rig"),
                    new MxCameraProfileId("profile"),
                    new MxCameraVector3(1f, 2f, 3f),
                    MxCameraVector3.Forward,
                    MxCameraVector3.Up,
                    new MxCameraEulerRotation(10f, 20f, 0f),
                    MxCameraProjectionKind.Perspective,
                    55f,
                    4f,
                    MxCameraVector3.Zero,
                    MxCameraVector3.Zero,
                    0f,
                    0f,
                    MxCameraStateSource.Normal);

                MxCameraResult result = rig.ApplyLate(state);

                Assert.IsTrue(result.Success);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), camera.transform.position);
                Assert.IsFalse(camera.orthographic);
                Assert.AreEqual(55f, camera.fieldOfView, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UnityRig_EvaluateAndApplyLate_UsesRuntimeProfilesAndTargets()
        {
            GameObject go = new GameObject("camera-test");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                MxCameraUnityRig rig = go.AddComponent<MxCameraUnityRig>();
                var resolver = new RecordingObstructionResolver();
                rig.SetObstruction(MxCameraUnityObstructionMode.FrameworkPhysicsAabb, resolver: resolver);
                MxCameraProfileDefinition[] profiles = { CreateProfile() };
                MxCameraTargetSnapshot[] targets =
                {
                    CreateTarget("actor", 2f, 0f, 4f, primary: true)
                };

                MxCameraResult result = rig.EvaluateAndApplyLate(1, 0.25f, profiles, targets);

                Assert.IsTrue(result.Success, result.Message);
                Assert.NotNull(rig.LastEvaluationResult);
                Assert.AreEqual(MxCameraEvaluationStatus.Success, rig.LastEvaluationResult.Status);
                Assert.AreEqual("profile", rig.LastEvaluationResult.ActiveProfileId.Value);
                Assert.AreEqual(1, rig.LastEvaluationResult.TargetGroupState.ValidTargetCount);
                Assert.AreEqual(0.25f, resolver.LastDeltaTime, 0.0001f);
                Assert.AreEqual(rig.LastEvaluationResult.State.Position.X, camera.transform.position.x, 0.001f);
                Assert.IsFalse(camera.orthographic);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UnityRig_ApplyLate_AppliesOrthographicSizeAtMostOncePerFrame()
        {
            GameObject go = new GameObject("camera-test");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                MxCameraUnityRig rig = go.AddComponent<MxCameraUnityRig>();
                var first = new MxCameraState(
                    new MxCameraRigId("rig"),
                    new MxCameraProfileId("profile"),
                    new MxCameraVector3(1f, 2f, 3f),
                    MxCameraVector3.Forward,
                    MxCameraVector3.Up,
                    new MxCameraEulerRotation(0f, 0f, 0f),
                    MxCameraProjectionKind.Orthographic,
                    60f,
                    7f,
                    MxCameraVector3.Zero,
                    MxCameraVector3.Zero,
                    0f,
                    0f,
                    MxCameraStateSource.Normal);
                var second = new MxCameraState(
                    new MxCameraRigId("rig"),
                    new MxCameraProfileId("profile"),
                    new MxCameraVector3(9f, 9f, 9f),
                    MxCameraVector3.Forward,
                    MxCameraVector3.Up,
                    new MxCameraEulerRotation(0f, 0f, 0f),
                    MxCameraProjectionKind.Orthographic,
                    60f,
                    9f,
                    MxCameraVector3.Zero,
                    MxCameraVector3.Zero,
                    0f,
                    0f,
                    MxCameraStateSource.Normal);

                rig.ApplyLate(first);
                rig.ApplyLate(second);

                Assert.IsTrue(camera.orthographic);
                Assert.AreEqual(7f, camera.orthographicSize, 0.001f);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), camera.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UnityRig_Dispose_AllowsRebuiltSessionStateToApplyInSameFrame()
        {
            GameObject go = new GameObject("camera-test");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                MxCameraUnityRig rig = go.AddComponent<MxCameraUnityRig>();
                var previousSession = new MxCameraState(
                    new MxCameraRigId("rig"),
                    new MxCameraProfileId("profile"),
                    new MxCameraVector3(1f, 2f, 3f),
                    MxCameraVector3.Forward,
                    MxCameraVector3.Up,
                    new MxCameraEulerRotation(0f, 0f, 0f),
                    MxCameraProjectionKind.Perspective,
                    60f,
                    4f,
                    MxCameraVector3.Zero,
                    MxCameraVector3.Zero,
                    0f,
                    0f,
                    MxCameraStateSource.Normal);
                var rebuiltSession = new MxCameraState(
                    new MxCameraRigId("rig"),
                    new MxCameraProfileId("profile"),
                    new MxCameraVector3(9f, 8f, 7f),
                    MxCameraVector3.Forward,
                    MxCameraVector3.Up,
                    new MxCameraEulerRotation(10f, 20f, 0f),
                    MxCameraProjectionKind.Perspective,
                    50f,
                    4f,
                    MxCameraVector3.Zero,
                    MxCameraVector3.Zero,
                    0f,
                    0f,
                    MxCameraStateSource.Normal);

                rig.ApplyLate(previousSession);
                rig.Dispose();
                MxCameraResult result = rig.ApplyLate(rebuiltSession);

                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(new Vector3(9f, 8f, 7f), camera.transform.position);
                Assert.AreEqual(50f, camera.fieldOfView, 0.001f);
                Assert.That(camera.transform.rotation.eulerAngles.x, Is.EqualTo(10f).Within(0.001f));
                Assert.That(camera.transform.rotation.eulerAngles.y, Is.EqualTo(20f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UnityRig_ApplyLate_NoneObstructionKeepsOriginalPosition()
        {
            GameObject go = new GameObject("camera-test");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                MxCameraUnityRig rig = go.AddComponent<MxCameraUnityRig>();
                rig.SetObstruction(
                    MxCameraUnityObstructionMode.None,
                    resolver: new FixedObstructionResolver(new Vector3(9f, 9f, 9f)));
                var state = new MxCameraState(
                    new MxCameraRigId("rig"),
                    new MxCameraProfileId("profile"),
                    new MxCameraVector3(1f, 2f, 3f),
                    MxCameraVector3.Forward,
                    MxCameraVector3.Up,
                    new MxCameraEulerRotation(0f, 0f, 0f),
                    MxCameraProjectionKind.Perspective,
                    60f,
                    5f,
                    MxCameraVector3.Zero,
                    MxCameraVector3.Zero,
                    0f,
                    0f,
                    MxCameraStateSource.Normal);

                MxCameraResult result = rig.ApplyLate(state);

                Assert.IsTrue(result.Success);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), camera.transform.position);
                Assert.IsFalse(rig.LastObstructionResult.IsObstructed);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UnityRig_ApplyLate_ResolverOverridesPositionOnly()
        {
            GameObject go = new GameObject("camera-test");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                MxCameraUnityRig rig = go.AddComponent<MxCameraUnityRig>();
                rig.SetObstruction(
                    MxCameraUnityObstructionMode.FrameworkPhysicsAabb,
                    resolver: new FixedObstructionResolver(new Vector3(4f, 5f, 6f)));
                var state = new MxCameraState(
                    new MxCameraRigId("rig"),
                    new MxCameraProfileId("profile"),
                    new MxCameraVector3(1f, 2f, 3f),
                    MxCameraVector3.Forward,
                    MxCameraVector3.Up,
                    new MxCameraEulerRotation(10f, 20f, 0f),
                    MxCameraProjectionKind.Perspective,
                    55f,
                    4f,
                    MxCameraVector3.Zero,
                    MxCameraVector3.Zero,
                    0f,
                    0f,
                    MxCameraStateSource.Normal);

                MxCameraResult result = rig.ApplyLate(state);

                Assert.IsTrue(result.Success);
                Assert.AreEqual(new Vector3(4f, 5f, 6f), camera.transform.position);
                Assert.AreEqual(55f, camera.fieldOfView, 0.001f);
                Assert.That(camera.transform.rotation.eulerAngles.x, Is.EqualTo(10f).Within(0.001f));
                Assert.That(camera.transform.rotation.eulerAngles.y, Is.EqualTo(20f).Within(0.001f));
                Assert.IsTrue(rig.LastObstructionResult.IsObstructed);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UnityRig_ApplyLate_DefaultShakeModeKeepsLegacyCoreOffset()
        {
            GameObject go = new GameObject("camera-test");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                MxCameraUnityRig rig = go.AddComponent<MxCameraUnityRig>();
                var state = CreateShakeState(
                    position: new MxCameraVector3(1.5f, 2f, 3f),
                    shakeOffset: new MxCameraVector3(0.5f, 0f, 0f));

                MxCameraResult result = rig.ApplyLate(state);

                Assert.IsTrue(result.Success);
                Assert.AreEqual(MxCameraUnityShakeMode.LegacyCoreOffset, rig.ShakeMode);
                Assert.AreEqual(new Vector3(1.5f, 2f, 3f), camera.transform.position);
                Assert.IsTrue(rig.LastShakeResult.IsActive);
                Assert.AreEqual("CAM_SHAKE_CORE_OFFSET", rig.LastShakeResult.Code);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UnityRig_ApplyLate_NoneShakeRemovesCoreOffset()
        {
            GameObject go = new GameObject("camera-test");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                MxCameraUnityRig rig = go.AddComponent<MxCameraUnityRig>();
                rig.SetShake(MxCameraUnityShakeMode.None);
                var state = CreateShakeState(
                    position: new MxCameraVector3(1.5f, 2f, 3f),
                    shakeOffset: new MxCameraVector3(0.5f, 0f, 0f));

                MxCameraResult result = rig.ApplyLate(state);

                Assert.IsTrue(result.Success);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), camera.transform.position);
                Assert.IsFalse(rig.LastShakeResult.IsActive);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UnityRig_ApplyLate_ProceduralShakeAppliesPositionAndRotationOnly()
        {
            GameObject go = new GameObject("camera-test");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                MxCameraUnityRig rig = go.AddComponent<MxCameraUnityRig>();
                rig.SetShake(
                    MxCameraUnityShakeMode.Procedural,
                    new MxCameraUnityShakeSettings
                    {
                        positionAmplitude = 1f,
                        rotationAmplitude = 10f,
                        frequency = 18f,
                        recoverySharpness = 10f,
                        maxPositionOffset = 1f,
                        maxRotationDegrees = 10f,
                        directionalInfluence = 0f,
                        seed = 42
                    });
                var state = CreateShakeState(
                    position: new MxCameraVector3(1.5f, 2f, 3f),
                    shakeOffset: new MxCameraVector3(0.5f, 0f, 0f));

                MxCameraResult result = rig.ApplyLate(state);

                Assert.IsTrue(result.Success);
                Assert.IsTrue(rig.LastShakeResult.IsActive);
                Assert.Greater(rig.LastShakeResult.PositionOffset.magnitude, 0f);
                Assert.Greater(rig.LastShakeResult.RotationEulerOffset.magnitude, 0f);
                Assert.AreEqual(55f, camera.fieldOfView, 0.001f);
                Assert.AreEqual(rig.LastShakeResult.Position, camera.transform.position);
                Assert.AreNotEqual(Quaternion.Euler(10f, 20f, 0f), camera.transform.rotation);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UnityRig_ApplyLate_ProceduralShakeRunsAfterObstructionBasePosition()
        {
            GameObject go = new GameObject("camera-test");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                MxCameraUnityRig rig = go.AddComponent<MxCameraUnityRig>();
                var resolver = new RecordingObstructionResolver();
                rig.SetObstruction(MxCameraUnityObstructionMode.FrameworkPhysicsAabb, resolver: resolver);
                rig.SetShake(
                    MxCameraUnityShakeMode.Procedural,
                    new MxCameraUnityShakeSettings
                    {
                        positionAmplitude = 1f,
                        rotationAmplitude = 0f,
                        maxPositionOffset = 1f,
                        directionalInfluence = 0f,
                        seed = 7
                    });
                var state = CreateShakeState(
                    position: new MxCameraVector3(1.5f, 2f, 3f),
                    shakeOffset: new MxCameraVector3(0.5f, 0f, 0f));

                MxCameraResult result = rig.ApplyLate(state);

                Assert.IsTrue(result.Success);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), resolver.LastDesiredPosition);
                Assert.AreEqual(rig.LastShakeResult.Position, camera.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UnityRig_ApplyLate_UsesProvidedDeltaTimeForPresentationContexts()
        {
            GameObject go = new GameObject("camera-test");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                MxCameraUnityRig rig = go.AddComponent<MxCameraUnityRig>();
                var resolver = new RecordingObstructionResolver();
                var presenter = new RecordingShakePresenter();
                rig.SetObstruction(MxCameraUnityObstructionMode.FrameworkPhysicsAabb, resolver: resolver);
                rig.SetShake(MxCameraUnityShakeMode.Procedural, presenter: presenter);
                var state = CreateShakeState(
                    position: new MxCameraVector3(1.5f, 2f, 3f),
                    shakeOffset: new MxCameraVector3(0.5f, 0f, 0f));

                MxCameraResult result = rig.ApplyLate(state, 0.125f);

                Assert.IsTrue(result.Success);
                Assert.AreEqual(0.125f, resolver.LastDeltaTime, 0.0001f);
                Assert.AreEqual(0.125f, presenter.LastDeltaTime, 0.0001f);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), camera.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static MxCameraState CreateShakeState(
            MxCameraVector3 position,
            MxCameraVector3 shakeOffset)
        {
            return new MxCameraState(
                new MxCameraRigId("rig"),
                new MxCameraProfileId("profile"),
                position,
                MxCameraVector3.Forward,
                MxCameraVector3.Up,
                new MxCameraEulerRotation(10f, 20f, 0f),
                MxCameraProjectionKind.Perspective,
                55f,
                4f,
                MxCameraVector3.Zero,
                shakeOffset,
                0f,
                0f,
                MxCameraStateSource.Normal);
        }

        private static MxCameraProfileDefinition CreateProfile()
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
                Pitch = 35f,
                Yaw = 0f
            };
        }

        private static MxCameraTargetSnapshot CreateTarget(
            string id,
            float x,
            float y,
            float z,
            bool primary)
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

        private sealed class FixedObstructionResolver : IMxCameraUnityObstructionResolver
        {
            private readonly Vector3 _position;

            public FixedObstructionResolver(Vector3 position)
            {
                _position = position;
            }

            public MxCameraUnityObstructionResult Resolve(in MxCameraUnityObstructionContext context)
            {
                return MxCameraUnityObstructionResult.Obstructed(
                    _position,
                    3f,
                    2f,
                    2f,
                    Vector3.up);
            }
        }

        private sealed class RecordingObstructionResolver : IMxCameraUnityObstructionResolver
        {
            public Vector3 LastDesiredPosition { get; private set; }
            public float LastDeltaTime { get; private set; }

            public MxCameraUnityObstructionResult Resolve(in MxCameraUnityObstructionContext context)
            {
                LastDesiredPosition = context.DesiredPosition;
                LastDeltaTime = context.DeltaTime;
                return MxCameraUnityObstructionResult.Unobstructed(context.DesiredPosition);
            }
        }

        private sealed class RecordingShakePresenter : IMxCameraUnityShakePresenter
        {
            public float LastDeltaTime { get; private set; }

            public void Reset()
            {
                LastDeltaTime = 0f;
            }

            public MxCameraUnityShakeResult Resolve(in MxCameraUnityShakeContext context)
            {
                LastDeltaTime = context.DeltaTime;
                return MxCameraUnityShakeResult.Inactive(
                    context.BasePosition,
                    context.BaseRotation,
                    context.CoreShakeOffset);
            }
        }
    }
}
