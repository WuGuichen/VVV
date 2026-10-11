using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Runtime.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Combat.Animation
{
    public class CombatAnimationUnityModuleTests
    {
        [Test]
        public void Initialize_RoutesActionRunnerEventsByEntityId()
        {
            CombatActionRunner runner = CreateRunner();
            var context = new TestCombatAnimationContext(runner);
            var module = new CombatAnimationUnityModule(context);
            var entity = new CombatEntityId(7);
            var otherEntity = new CombatEntityId(8);
            var driver = new RecordingAnimatorDriver();
            var otherDriver = new RecordingAnimatorDriver();

            module.RegisterDriver(entity, driver);
            module.RegisterDriver(otherEntity, otherDriver);
            module.Initialize();

            runner.StartAction(entity, 1001, CombatFrame.Zero);
            runner.TickActions(new CombatFrame(1));
            runner.TickActions(new CombatFrame(2));
            runner.TickActions(new CombatFrame(3));
            runner.TryCancel(entity, 3003, new CombatFrame(3));
            runner.ForceStartAction(entity, 2002, new CombatFrame(4));
            runner.ForceCancel(entity);

            Assert.AreEqual(2, driver.Started.Count);
            Assert.AreEqual(1, driver.PhaseChanged.Count);
            Assert.AreEqual(1, driver.CancelRejected.Count);
            Assert.AreEqual(2, driver.Canceled.Count);
            Assert.AreEqual(0, driver.Finished.Count);
            Assert.AreEqual(0, otherDriver.TotalEvents);
        }

        [Test]
        public void Shutdown_UnsubscribesFromActionRunnerEvents()
        {
            CombatActionRunner runner = CreateRunner();
            var context = new TestCombatAnimationContext(runner);
            var module = new CombatAnimationUnityModule(context);
            var entity = new CombatEntityId(9);
            var driver = new RecordingAnimatorDriver();

            module.RegisterDriver(entity, driver);
            module.Initialize();
            module.Shutdown();

            runner.StartAction(entity, 1001, CombatFrame.Zero);

            Assert.AreEqual(0, driver.TotalEvents);
        }

        [Test]
        public void CombatAnimatorMapping_FindsSerializedActionMapping()
        {
            CombatAnimatorMapping mapping = ScriptableObject.CreateInstance<CombatAnimatorMapping>();
            CombatAnimatorMapping restored = ScriptableObject.CreateInstance<CombatAnimatorMapping>();

            try
            {
                mapping.ActionMappings.Add(new ActionAnimMapping
                {
                    ActionId = 1001,
                    AnimatorStateName = "Attack",
                    CrossFadeDuration = 0.25f,
                });

                string json = JsonUtility.ToJson(mapping);
                JsonUtility.FromJsonOverwrite(json, restored);

                Assert.IsTrue(restored.TryGetMapping(1001, out ActionAnimMapping actionMapping));
                Assert.AreEqual("Attack", actionMapping.AnimatorStateName);
                Assert.AreEqual(0.25f, actionMapping.CrossFadeDuration);
                Assert.IsFalse(restored.TryGetMapping(2002, out _));
            }
            finally
            {
                Object.DestroyImmediate(mapping);
                Object.DestroyImmediate(restored);
            }
        }

        [Test]
        public void CombatTransformDriver_TickInterpolatesTowardPoseSource()
        {
            var gameObject = new GameObject("combat-transform-driver-test");

            try
            {
                var entity = new CombatEntityId(12);
                CombatTransformDriver driver = gameObject.AddComponent<CombatTransformDriver>();
                driver.EntityId = entity;
                driver.InterpolationSpeed = 5f;
                driver.SetPoseSource(new StaticPoseSource(entity, new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f)));

                driver.Tick(0.1f);

                Assert.AreEqual(5f, gameObject.transform.position.x, 0.0001f);
                Assert.AreEqual(0f, gameObject.transform.position.y, 0.0001f);
                Assert.AreEqual(0f, gameObject.transform.position.z, 0.0001f);
                Assert.Greater(gameObject.transform.rotation.eulerAngles.y, 0f);

                driver.InterpolationSpeed = 0f;
                driver.Tick(0.1f);

                Assert.AreEqual(10f, gameObject.transform.position.x, 0.0001f);
                Assert.AreEqual(90f, gameObject.transform.rotation.eulerAngles.y, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CombatAnimatorDriver_UsesMappingWithoutAnimatorSideEffects()
        {
            var gameObject = new GameObject("combat-animator-driver-test");
            CombatAnimatorMapping mapping = ScriptableObject.CreateInstance<CombatAnimatorMapping>();

            try
            {
                CombatAnimatorDriver driver = gameObject.AddComponent<CombatAnimatorDriver>();
                driver.EntityId = new CombatEntityId(14);
                driver.Mapping = mapping;
                mapping.ActionMappings.Add(new ActionAnimMapping
                {
                    ActionId = 1001,
                    AnimatorStateName = "Attack",
                    CrossFadeDuration = 0.15f,
                });

                driver.OnActionStarted(new ActionStartedEvent(driver.EntityId, 1001, 1, CombatFrame.Zero));

                Assert.IsTrue(driver.TryGetActionMapping(1001, out ActionAnimMapping actionMapping));
                Assert.AreEqual("Attack", actionMapping.AnimatorStateName);
            }
            finally
            {
                Object.DestroyImmediate(mapping);
                Object.DestroyImmediate(gameObject);
            }
        }

        private static CombatActionRunner CreateRunner()
        {
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(1001, new CombatActionTimeline(
                1001,
                5,
                new CombatFrameRange(0, 1),
                new CombatFrameRange(2, 3),
                new CombatFrameRange(4, 4),
                new[]
                {
                    new CombatActionWindow(CombatActionWindowKind.Cancel, new CombatFrameRange(3, 3), targetActionId: 2002),
                },
                null));
            registry.RegisterTimeline(2002, new CombatActionTimeline(
                2002,
                4,
                new CombatFrameRange(0, 1),
                new CombatFrameRange(2, 2),
                new CombatFrameRange(3, 3),
                null,
                null));
            return new CombatActionRunner(registry);
        }

        private sealed class RecordingAnimatorDriver : ICombatAnimatorDriver
        {
            public readonly List<ActionStartedEvent> Started = new List<ActionStartedEvent>();
            public readonly List<ActionPhaseChangedEvent> PhaseChanged = new List<ActionPhaseChangedEvent>();
            public readonly List<ActionFinishedEvent> Finished = new List<ActionFinishedEvent>();
            public readonly List<ActionCanceledEvent> Canceled = new List<ActionCanceledEvent>();
            public readonly List<ActionCancelRejectedEvent> CancelRejected = new List<ActionCancelRejectedEvent>();

            public int TotalEvents => Started.Count + PhaseChanged.Count + Finished.Count + Canceled.Count + CancelRejected.Count;

            public void OnActionStarted(ActionStartedEvent evt)
            {
                Started.Add(evt);
            }

            public void OnActionPhaseChanged(ActionPhaseChangedEvent evt)
            {
                PhaseChanged.Add(evt);
            }

            public void OnActionFinished(ActionFinishedEvent evt)
            {
                Finished.Add(evt);
            }

            public void OnActionCanceled(ActionCanceledEvent evt)
            {
                Canceled.Add(evt);
            }

            public void OnActionCancelRejected(ActionCancelRejectedEvent evt)
            {
                CancelRejected.Add(evt);
            }
        }

        private sealed class StaticPoseSource : ICombatEntityPoseSource
        {
            private readonly CombatEntityId _entityId;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;

            public StaticPoseSource(CombatEntityId entityId, Vector3 position, Quaternion rotation)
            {
                _entityId = entityId;
                _position = position;
                _rotation = rotation;
            }

            public bool TryGetPose(CombatEntityId entityId, out Vector3 position, out Quaternion rotation)
            {
                if (entityId.Equals(_entityId))
                {
                    position = _position;
                    rotation = _rotation;
                    return true;
                }

                position = default;
                rotation = default;
                return false;
            }
        }

        private sealed class TestCombatAnimationContext : ICombatAnimationContext
        {
            private readonly List<MxFramework.Combat.Hit.HitCandidate> _hitCandidates =
                new List<MxFramework.Combat.Hit.HitCandidate>();

            public TestCombatAnimationContext(CombatActionRunner runner)
            {
                ActionRunner = runner;
            }

            public CombatActionRunner ActionRunner { get; private set; }

            public IReadOnlyList<MxFramework.Combat.Hit.HitCandidate> LastFrameHitCandidates => _hitCandidates;

            public CombatAnimationSnapshot? LastSnapshot { get; private set; }

            public void SetActionRunner(CombatActionRunner runner)
            {
                ActionRunner = runner;
            }

            public void SetLastFrameHitCandidates(List<MxFramework.Combat.Hit.HitCandidate> candidates)
            {
                _hitCandidates.Clear();
                if (candidates != null)
                {
                    _hitCandidates.AddRange(candidates);
                }
            }

            public void SetLastSnapshot(CombatAnimationSnapshot snapshot)
            {
                LastSnapshot = snapshot;
            }
        }
    }
}
