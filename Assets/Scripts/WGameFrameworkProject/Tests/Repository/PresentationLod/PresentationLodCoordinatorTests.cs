using System;
using System.Collections.Generic;
using MxFramework.CharacterCloth;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.PresentationLod
{
    [TestFixture]
    public sealed class PresentationLodCoordinatorTests
    {
        private sealed class Observer : IPresentationLodObserver
        {
            internal int Calls;
            internal readonly Dictionary<(int, int), PresentationLodObservation> Values =
                new Dictionary<(int, int), PresentationLodObservation>();
            public PresentationLodObservation Observe(int subjectId, int cameraId)
            { Calls++; return Values.TryGetValue((subjectId, cameraId), out var value) ? value : default; }
        }
        private static PresentationLodObservation Observation(bool visible = false, bool shadow = false,
            bool known = true, float x = 10) => new PresentationLodObservation(known, visible, shadow, true, x, 20, 1, 100);

        [Test]
        public void UiAndClothReadTheSameVersionedObservation()
        {
            var observer = new Observer(); observer.Values[(1, 10)] = Observation(visible: true);
            var coordinator = new PresentationLodCoordinator(observer);
            var handle = coordinator.Register(1, true, null, 0); coordinator.SetCamera(10, 1);
            coordinator.TryObserve(handle, 10, out var ui);
            coordinator.Tick(0);
            coordinator.TryObserve(handle, 10, out var repeatedUi);
            Assert.That(observer.Calls, Is.EqualTo(1));
            Assert.That(repeatedUi.ScreenX, Is.EqualTo(ui.ScreenX));
            Assert.That(coordinator.Counters.CacheHits, Is.EqualTo(2));
            Assert.That(coordinator.Counters.CachedObservations, Is.EqualTo(1));
        }

        [Test]
        public void SubjectAndCameraChangesInvalidateWithoutFrameBasedPolling()
        {
            var observer = new Observer(); observer.Values[(1, 10)] = Observation(x: 10);
            var coordinator = new PresentationLodCoordinator(observer);
            var handle = coordinator.Register(1, false, null, 0); coordinator.SetCamera(10, 1);
            coordinator.TryObserve(handle, 10, out _);
            coordinator.Tick(100); coordinator.SetCamera(10, 1);
            coordinator.TryObserve(handle, 10, out _);
            Assert.That(observer.Calls, Is.EqualTo(1));
            observer.Values[(1, 10)] = Observation(x: 30); coordinator.Invalidate(handle, 100);
            coordinator.TryObserve(handle, 10, out var moved);
            Assert.That(moved.ScreenX, Is.EqualTo(30));
            coordinator.SetCamera(10, 2); coordinator.TryObserve(handle, 10, out _);
            Assert.That(observer.Calls, Is.EqualTo(3));
        }

        [Test]
        public void CameraIdentityReuseCannotReadAnOldCameraCache()
        {
            var observer = new Observer(); observer.Values[(1, 10)] = Observation(x: 10);
            var coordinator = new PresentationLodCoordinator(observer);
            var handle = coordinator.Register(1, false, null, 0); coordinator.SetCamera(10, 1);
            coordinator.TryObserve(handle, 10, out _); coordinator.RemoveCamera(10);
            Assert.That(coordinator.Counters.CachedObservations, Is.Zero);
            observer.Values[(1, 10)] = Observation(x: 40); coordinator.SetCamera(10, 1);
            coordinator.TryObserve(handle, 10, out var recreated);
            Assert.That(recreated.ScreenX, Is.EqualTo(40));
            Assert.That(observer.Calls, Is.EqualTo(2));
        }

        [Test]
        public void ReleasedHandleCannotTouchAReusedObjectSlot()
        {
            var observer = new Observer(); var coordinator = new PresentationLodCoordinator(observer);
            var old = coordinator.Register(1, true, _ => Assert.Fail("Released consumer called"), 0);
            Assert.That(coordinator.Unregister(old), Is.True);
            var current = coordinator.Register(1, true, null, 0);
            Assert.That(current.Slot, Is.EqualTo(old.Slot));
            Assert.That(current.Generation, Is.Not.EqualTo(old.Generation));
            coordinator.Invalidate(old, 1); coordinator.SetImportance(old, true, 20, 1);
            Assert.That(coordinator.TryObserve(old, 10, out _), Is.False);
            Assert.That(coordinator.Unregister(old), Is.False);
            Assert.That(coordinator.IsRegistered(current), Is.True);
        }

        [Test]
        public void ReleaseFromConsumerCallbackCannotRequeueAReusedSlot()
        {
            var observer = new Observer(); observer.Values[(1, 10)] = Observation();
            observer.Values[(2, 10)] = Observation(visible: true);
            var coordinator = new PresentationLodCoordinator(observer, 0.25, 0);
            PresentationLodHandle original = default, replacement = default;
            original = coordinator.Register(1, true, paused =>
            {
                Assert.That(paused, Is.True); coordinator.Unregister(original);
                replacement = coordinator.Register(2, true, null, 100);
            }, 0);
            coordinator.SetCamera(10, 1); coordinator.Tick(0);
            Assert.That(coordinator.IsRegistered(original), Is.False);
            Assert.That(coordinator.IsRegistered(replacement), Is.True);
            Assert.That(coordinator.Counters.Deadlines, Is.EqualTo(1));
            Assert.That(coordinator.Counters.CachedObservations, Is.Zero);
            Assert.That(coordinator.Tick(1), Is.Zero);
            Assert.That(coordinator.Tick(100), Is.EqualTo(1));
        }

        [Test]
        public void HighestDemandAcrossCamerasKeepsClothRunning()
        {
            var observer = new Observer(); observer.Values[(1, 10)] = Observation();
            observer.Values[(1, 20)] = Observation(visible: true);
            var states = new List<bool>(); var coordinator = new PresentationLodCoordinator(observer, 0.25, 0);
            coordinator.Register(1, true, states.Add, 0); coordinator.SetCamera(10, 1); coordinator.SetCamera(20, 1);
            coordinator.Tick(0);
            Assert.That(states, Is.Empty);
            Assert.That(observer.Calls, Is.EqualTo(2));
        }

        [Test]
        public void OffscreenShadowCasterCannotBePausedByUiVisibility()
        {
            var observer = new Observer(); observer.Values[(1, 10)] = Observation(shadow: true);
            var states = new List<bool>(); var coordinator = new PresentationLodCoordinator(observer, 0.25, 0);
            coordinator.Register(1, true, states.Add, 0); coordinator.SetCamera(10, 1);
            coordinator.Tick(0); coordinator.Tick(10);
            Assert.That(states, Is.Empty);
        }

        [Test]
        public void RegisteredPoseProtectionAvoidsUnnecessaryCameraObservations()
        {
            var observer = new Observer(); var states = new List<bool>();
            var coordinator = new PresentationLodCoordinator(observer, 0.25, 0);
            var handle = coordinator.Register(1, true, states.Add, 0);
            coordinator.SetCamera(10, 1); coordinator.SetPoseProtection(handle, true, 0); coordinator.Tick(0);
            Assert.That(observer.Calls, Is.Zero); Assert.That(states, Is.Empty);
            observer.Values[(1, 10)] = Observation();
            coordinator.SetPoseProtection(handle, false, 1); coordinator.Tick(1);
            Assert.That(states, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void UnknownObservationAndNoCameraNeverAuthorizeReduction()
        {
            var observer = new Observer(); var states = new List<bool>();
            var coordinator = new PresentationLodCoordinator(observer, 0.25, 0);
            coordinator.Register(1, true, states.Add, 0); coordinator.Tick(0);
            coordinator.SetCamera(10, 1); coordinator.Tick(1);
            Assert.That(states, Is.Empty);
        }

        [Test]
        public void PoseBoundInvalidationKeepsHysteresisButUiVisibilityRecoversImmediately()
        {
            var observer = new Observer(); observer.Values[(1, 10)] = Observation();
            var states = new List<bool>(); var coordinator = new PresentationLodCoordinator(observer, 0.25, 0.75);
            var handle = coordinator.Register(1, true, states.Add, 0); coordinator.SetCamera(10, 1);
            coordinator.Tick(0); coordinator.InvalidateObservation(handle); coordinator.Tick(0.25);
            coordinator.InvalidateObservation(handle); coordinator.Tick(0.5);
            coordinator.InvalidateObservation(handle); coordinator.Tick(0.75);
            Assert.That(states, Is.EqualTo(new[] { true }));
            observer.Values[(1, 10)] = Observation(visible: true); coordinator.InvalidateObservation(handle);
            coordinator.TryObserve(handle, 10, out _);
            Assert.That(states, Is.EqualTo(new[] { true, false }));
            Assert.That(coordinator.Tick(0.8, 0), Is.Zero);
        }

        [Test]
        public void DemandedUnknownBoundsRestorePreviouslyPausedConsumer()
        {
            var observer = new Observer(); observer.Values[(1, 10)] = Observation();
            var states = new List<bool>(); var coordinator = new PresentationLodCoordinator(observer, 0.25, 0);
            var handle = coordinator.Register(1, true, states.Add, 0); coordinator.SetCamera(10, 1); coordinator.Tick(0);
            observer.Values.Remove((1, 10)); coordinator.InvalidateObservation(handle);
            coordinator.TryObserve(handle, 10, out _);
            Assert.That(states, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void StableHiddenDelayAndImmediateCameraRecoveryDoNotRepeatSetters()
        {
            var observer = new Observer(); observer.Values[(1, 10)] = Observation();
            var states = new List<bool>(); var coordinator = new PresentationLodCoordinator(observer, 0.25, 0.75);
            coordinator.Register(1, true, states.Add, 0); coordinator.SetCamera(10, 1);
            coordinator.Tick(0); coordinator.Tick(0.25); coordinator.Tick(0.5);
            Assert.That(states, Is.Empty);
            coordinator.Tick(0.75); coordinator.Tick(1);
            Assert.That(states, Is.EqualTo(new[] { true }));
            coordinator.SetCamera(10, 2);
            Assert.That(states, Is.EqualTo(new[] { true, false }));
            Assert.That(coordinator.Counters.ConservativeRestores, Is.EqualTo(1));
        }

        [Test]
        public void FeedbackAndLockPromoteBeforeTheRoutineQuota()
        {
            var observer = new Observer(); observer.Values[(1, 10)] = Observation();
            var states = new List<bool>(); var coordinator = new PresentationLodCoordinator(observer, 0.25, 0);
            var handle = coordinator.Register(1, true, states.Add, 0); coordinator.SetCamera(10, 1); coordinator.Tick(0);
            coordinator.SetImportance(handle, false, 5, 0.1);
            Assert.That(states, Is.EqualTo(new[] { true, false }));
            coordinator.Tick(0.1, 0); coordinator.Tick(1);
            Assert.That(states, Has.Count.EqualTo(2));
            coordinator.SetImportance(handle, true, 0, 6); coordinator.Tick(6);
            Assert.That(states, Has.Count.EqualTo(2));
            coordinator.SetImportance(handle, false, 0, 7); coordinator.Tick(7);
            Assert.That(states, Is.EqualTo(new[] { true, false, true }));
        }

        [Test]
        public void TeleportInvalidationRestoresClothBeforeResampling()
        {
            var observer = new Observer(); observer.Values[(1, 10)] = Observation();
            var states = new List<bool>(); var coordinator = new PresentationLodCoordinator(observer, 0.25, 0.75);
            var handle = coordinator.Register(1, true, states.Add, 0); coordinator.SetCamera(10, 1);
            coordinator.Tick(0); coordinator.Tick(1);
            coordinator.Invalidate(handle, 1.1);
            Assert.That(states, Is.EqualTo(new[] { true, false }));
            coordinator.Tick(1.1); Assert.That(states, Has.Count.EqualTo(2));
        }

        [Test]
        public void IndexedDeadlinesBoundRoutineWorkAndForgetDestroyedSubjects()
        {
            var observer = new Observer(); var coordinator = new PresentationLodCoordinator(observer);
            var handles = new List<PresentationLodHandle>();
            for (int i = 1; i <= 100; i++)
            { observer.Values[(i, 10)] = Observation(visible: true); handles.Add(coordinator.Register(i, true, null, 0)); }
            coordinator.SetCamera(10, 1);
            Assert.That(observer.Calls, Is.Zero);
            Assert.That(coordinator.Tick(0, 7), Is.EqualTo(7)); Assert.That(observer.Calls, Is.EqualTo(7));
            Assert.That(coordinator.Counters.Deadlines, Is.EqualTo(100));
            foreach (var handle in handles) coordinator.Unregister(handle);
            Assert.That(coordinator.Tick(100, 1000), Is.Zero);
            Assert.That(coordinator.Counters.Deadlines, Is.Zero);
            Assert.That(coordinator.Counters.CachedObservations, Is.Zero);
        }

        [Test]
        public void SeparateSessionCachesDoNotPromoteEachOther()
        {
            var observer = new Observer(); observer.Values[(1, 10)] = Observation();
            var gameplayStates = new List<bool>(); var previewStates = new List<bool>();
            var gameplay = new PresentationLodCoordinator(observer, 0.25, 0);
            var preview = new PresentationLodCoordinator(observer, 0.25, 0);
            gameplay.Register(1, true, gameplayStates.Add, 0); gameplay.SetCamera(10, 1); gameplay.Tick(0);
            var previewHandle = preview.Register(1, true, previewStates.Add, 0);
            preview.SetImportance(previewHandle, true, 0, 0); preview.SetCamera(20, 1); preview.Tick(0);
            Assert.That(gameplayStates, Is.EqualTo(new[] { true })); Assert.That(previewStates, Is.Empty);
        }

        [Test]
        public void WarmCacheAndDueTickHaveNoManagedAllocation()
        {
            var observer = new Observer(); observer.Values[(1, 10)] = Observation(visible: true);
            var coordinator = new PresentationLodCoordinator(observer); var handle = coordinator.Register(1, true, null, 0);
            coordinator.SetCamera(10, 1);
            for (int i = 0; i < 100; i++) { coordinator.TryObserve(handle, 10, out _); coordinator.Tick(i); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 100; i < 1100; i++) { coordinator.TryObserve(handle, 10, out _); coordinator.Tick(i); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void ClearInvalidatesHandlesAndRemovesAllDeadlinesAndCameras()
        {
            var observer = new Observer(); var coordinator = new PresentationLodCoordinator(observer);
            var handle = coordinator.Register(1, true, _ => Assert.Fail("Teardown must not revive"), 0);
            coordinator.SetCamera(10, 1); coordinator.Clear();
            Assert.That(coordinator.IsRegistered(handle), Is.False);
            Assert.That(coordinator.Counters.Subjects, Is.Zero); Assert.That(coordinator.Counters.Cameras, Is.Zero);
            Assert.That(coordinator.Tick(100), Is.Zero);
            var recreated = coordinator.Register(1, true, null, 100);
            Assert.That(recreated.Generation, Is.Not.EqualTo(handle.Generation));
        }

        private sealed class Group : ICharacterClothGroup
        {
            public event Action<ICharacterClothGroup> Destroyed;
            internal CharacterClothSystemOverride Applied;
            internal CharacterClothState Local = CharacterClothState.Running;
            internal int Writes, Resets;
            internal CharacterClothState Effective => CharacterClothStateResolver.Resolve(Local, Applied);
            public void SetPresentationOverride(CharacterClothSystemOverride value) { Applied = value; Writes++; }
            public void ResetAll(bool keepPose = false) { Resets++; }
            internal void Destroy() => Destroyed?.Invoke(this);
        }

        [Test]
        public void LodPauseComposesWithMenuPauseAndSessionDisable()
        {
            var group = new Group(); var service = new CharacterClothPresentationService(); service.Register(group);
            service.SetLodPaused(group, true); Assert.That(group.Effective, Is.EqualTo(CharacterClothState.Paused));
            service.SetPaused(true); service.SetLodPaused(group, false);
            Assert.That(group.Effective, Is.EqualTo(CharacterClothState.Paused));
            service.SetLodPaused(group, true); service.SetEnabled(false, keepPose: true);
            service.SetPaused(false); service.SetLodPaused(group, false);
            Assert.That(group.Effective, Is.EqualTo(CharacterClothState.DisabledKeep));
            service.SetEnabled(true); Assert.That(group.Effective, Is.EqualTo(CharacterClothState.Running));
        }

        [Test]
        public void LodRecoveryDoesNotReviveLocalDisableOrResetSimulation()
        {
            var group = new Group { Local = CharacterClothState.DisabledReset };
            var service = new CharacterClothPresentationService(); service.Register(group);
            service.SetLodPaused(group, true); service.SetLodPaused(group, false);
            Assert.That(group.Effective, Is.EqualTo(CharacterClothState.DisabledReset));
            Assert.That(group.Resets, Is.Zero);
        }

        [Test]
        public void RepeatedLodRequestsOnlyWriteActualEffectiveTransitions()
        {
            var group = new Group(); var service = new CharacterClothPresentationService(); service.Register(group);
            for (int i = 0; i < 100; i++) service.SetLodPaused(group, true);
            service.SetPaused(true); service.SetLodPaused(group, false);
            Assert.That(group.Writes, Is.EqualTo(2));
            service.SetPaused(false); Assert.That(group.Writes, Is.EqualTo(3));
        }

        [Test]
        public void DestroyReleaseAndReregistrationForgetLodState()
        {
            var group = new Group(); var service = new CharacterClothPresentationService(); service.Register(group);
            service.SetLodPaused(group, true); group.Destroy();
            int writes = group.Writes;
            Assert.That(service.GroupCount, Is.Zero); Assert.That(service.SetLodPaused(group, false), Is.False);
            service.SetPaused(true); service.ResetAll(); Assert.That(group.Writes, Is.EqualTo(writes));
            Assert.That(group.Resets, Is.Zero);
            service.SetPaused(false); service.Register(group);
            Assert.That(group.Effective, Is.EqualTo(CharacterClothState.Running));
            service.Unregister(group, restoreLocalState: false);
            Assert.That(service.SetLodPaused(group, true), Is.False);
        }

        [Test]
        public void ResetWhileLodPausedPreservesTheComposedPause()
        {
            var group = new Group(); var service = new CharacterClothPresentationService(); service.Register(group);
            service.SetLodPaused(group, true); service.ResetAll();
            Assert.That(group.Resets, Is.EqualTo(1)); Assert.That(group.Effective, Is.EqualTo(CharacterClothState.Paused));
        }

        [Test]
        public void CoordinatorAndClothServiceAreARealJoinedConsumer()
        {
            var observer = new Observer(); observer.Values[(1, 10)] = Observation();
            var group = new Group(); var service = new CharacterClothPresentationService(); service.Register(group);
            var coordinator = new PresentationLodCoordinator(observer, 0.25, 0);
            var handle = coordinator.Register(1, true, paused => service.SetLodPaused(group, paused), 0);
            coordinator.SetCamera(10, 1); coordinator.Tick(0);
            Assert.That(group.Effective, Is.EqualTo(CharacterClothState.Paused));
            service.SetPaused(true); coordinator.SetImportance(handle, true, 0, 0.1);
            Assert.That(group.Effective, Is.EqualTo(CharacterClothState.Paused));
            service.SetPaused(false); Assert.That(group.Effective, Is.EqualTo(CharacterClothState.Running));
        }
    }
}
