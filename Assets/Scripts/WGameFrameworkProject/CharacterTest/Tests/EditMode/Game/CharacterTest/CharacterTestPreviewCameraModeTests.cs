using System.Collections.Generic;
using MxFramework.Camera;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestPreviewCameraModeTests
    {
        [Test]
        public void MultiTargetDeduplicatesAndKeepsTheSelectedAgentPrimary()
        {
            var subjects = new List<CharacterTestPreviewCameraSubject>();
            var frame = Frame(10, Agent(1, 2, -8f, 0f), Agent(2, 3, 0f, 12f));
            Assert.IsTrue(CharacterTestRuntimeAiPlannerDevSceneController.CollectPreviewCameraSubjects(frame, 1, true, subjects));
            Assert.AreEqual(3, subjects.Count);
            Assert.AreEqual(2, subjects[0].EntityId);
            Assert.IsTrue(subjects[0].Primary);
            Assert.AreEqual(3, subjects[1].EntityId);
            Assert.IsFalse(subjects[1].Primary);
            Assert.AreEqual(1, subjects[2].EntityId);
        }

        [Test]
        public void SwitchingBackToAnOlderFrameUsesItsOwnPositionsAndClearsStaleSubjects()
        {
            var subjects = new List<CharacterTestPreviewCameraSubject>();
            var old = Frame(10, Agent(1, 2, -8f, 0f));
            var latest = Frame(20, Agent(1, 2, 100f, 120f), Agent(3, 4, 200f, 220f));
            CharacterTestRuntimeAiPlannerDevSceneController.CollectPreviewCameraSubjects(latest, 0, true, subjects);
            Assert.AreEqual(4, subjects.Count);
            CharacterTestRuntimeAiPlannerDevSceneController.CollectPreviewCameraSubjects(old, 0, true, subjects);
            Assert.AreEqual(2, subjects.Count);
            Assert.AreEqual(-8f, subjects[0].Center.x);
            Assert.AreEqual(100f, latest.Agents[0].ScenePreviewSourceCenterX);
        }

        [TestCase(16f / 9f)]
        [TestCase(1f)]
        public void FrameworkGroupFramingContainsThreeSeparatedSubjects(float aspect)
        {
            var subjects = new List<CharacterTestPreviewCameraSubject> {
                new CharacterTestPreviewCameraSubject(1, new Vector3(-20, 0, 0), true),
                new CharacterTestPreviewCameraSubject(2, new Vector3(20, 0, 0), false),
                new CharacterTestPreviewCameraSubject(3, new Vector3(0, 5, 10), false) };
            var profile = Profile();
            CharacterTestRuntimeAiLabBehaviorCardPreviewHost.ConfigureAutomaticProfile(profile,
                CharacterTestPreviewCameraMode.MultiTarget, subjects, aspect);
            var state = Evaluate(profile, subjects, aspect);
            var forward = new Vector3(state.ViewForward.X, state.ViewForward.Y, state.ViewForward.Z);
            var rotation = Quaternion.Inverse(Quaternion.LookRotation(forward));
            var position = new Vector3(state.Position.X, state.Position.Y, state.Position.Z);
            float tan = Mathf.Tan(state.FieldOfView * Mathf.Deg2Rad * 0.5f);
            foreach (var subject in subjects)
            {
                var local = rotation * (subject.Center - position);
                Assert.Greater(local.z, 0);
                Assert.Less(Mathf.Abs(local.x / (local.z * tan * aspect)), 1f);
                Assert.Less(Mathf.Abs(local.y / (local.z * tan)), 1f);
            }
        }

        [Test]
        public void ThirdPersonUsesPrimaryFollowAndExistingLockedComposition()
        {
            var subjects = new List<CharacterTestPreviewCameraSubject> {
                new CharacterTestPreviewCameraSubject(1, Vector3.zero, true),
                new CharacterTestPreviewCameraSubject(2, new Vector3(8, 0, 0), false) };
            var profile = Profile();
            CharacterTestRuntimeAiLabBehaviorCardPreviewHost.ConfigureAutomaticProfile(profile,
                CharacterTestPreviewCameraMode.ThirdPerson, subjects, 16f / 9f);
            Assert.AreEqual(MxCameraMode.Follow, profile.Mode);
            Assert.AreEqual(90f, profile.Yaw, 0.01f);
            Assert.Greater(profile.Distance, 0);
            Assert.AreEqual(profile.MinDistance, profile.MaxDistance);
            var state = Evaluate(profile, subjects, 16f / 9f);
            var forward = new Vector3(state.ViewForward.X, state.ViewForward.Y, state.ViewForward.Z);
            var inverse = Quaternion.Inverse(Quaternion.LookRotation(forward));
            var camera = new Vector3(state.Position.X, state.Position.Y, state.Position.Z);
            float tan = Mathf.Tan(state.FieldOfView * Mathf.Deg2Rad * 0.5f);
            foreach (var subject in subjects)
                foreach (float y in new[] { -2f, 2f })
                {
                    var local = inverse * (subject.Center + Vector3.up * y - camera);
                    Assert.Greater(local.z, 0f);
                    Assert.Less(Mathf.Abs(local.y / (local.z * tan)), 1f);
                }
            CharacterTestRuntimeAiLabBehaviorCardPreviewHost.ConfigureAutomaticProfile(profile,
                CharacterTestPreviewCameraMode.MultiTarget, subjects, 16f / 9f);
            Assert.AreEqual(MxCameraMode.GroupFollowPerspective, profile.Mode);
            Assert.AreEqual(MxCameraVector3.Zero, profile.WorldOffset);
        }

        [Test]
        public void CameraModesCycleWithoutNeedingGameplayOrChangingAWorld()
        {
            using var host = new CharacterTestRuntimeAiLabBehaviorCardPreviewHost(null);
            Assert.AreEqual(CharacterTestPreviewCameraMode.MultiTarget, host.CameraMode);
            host.CycleCameraMode(); Assert.AreEqual(CharacterTestPreviewCameraMode.Free, host.CameraMode);
            host.CycleCameraMode(); Assert.AreEqual(CharacterTestPreviewCameraMode.ThirdPerson, host.CameraMode);
            host.CycleCameraMode(); Assert.AreEqual(CharacterTestPreviewCameraMode.MultiTarget, host.CameraMode);
        }

        private static MxCameraProfileDefinition Profile() => new MxCameraProfileDefinition {
            ProfileId = new MxCameraProfileId("preview.test"), FieldOfView = 45, MaxTargetRadius = 1000 };

        private static MxCameraState Evaluate(MxCameraProfileDefinition profile,
            IReadOnlyList<CharacterTestPreviewCameraSubject> subjects, float aspect)
        {
            var targets = new List<MxCameraTargetSnapshot>();
            foreach (var subject in subjects)
            {
                if (profile.Mode == MxCameraMode.Follow && !subject.Primary) continue;
                var p = new MxCameraVector3(subject.Center.x, subject.Center.y, subject.Center.z);
                targets.Add(new MxCameraTargetSnapshot(new MxCameraTargetRef(subject.EntityId.ToString()), p,
                    MxCameraVector3.Forward, MxCameraVector3.Up, MxCameraVector3.Zero, p,
                    new MxCameraVector3(0.6f, 1.1f, 0.6f), 1, subject.Primary, true, 10));
            }
            var id = new MxCameraRigId("preview.test");
            return new MxCameraService(id).Evaluate(new MxCameraEvaluationContext(10, 0, id,
                aspect * 1000, 1000, null, new[] { profile }, targets)).State;
        }

        private static CharacterTestRuntimeAiPlannerDevFrameSnapshot Frame(int frame,
            params CharacterTestRuntimeAiPlannerAgentSnapshot[] agents) => new CharacterTestRuntimeAiPlannerDevFrameSnapshot(
                frame, 0, new CharacterTestRuntimeAiPlannerSnapshot(true, "test", agents, ""));

        private static CharacterTestRuntimeAiPlannerAgentSnapshot Agent(int id, int target, float x, float targetX) =>
            new CharacterTestRuntimeAiPlannerAgentSnapshot(id, target, "", true, false, 0, true, 0, 0, 0, "",
                default, "", "", "", 0, 0, hasScenePreviewGeometry: true,
                scenePreviewSourceCenterX: x, scenePreviewTargetCenterX: targetX);
    }
}
