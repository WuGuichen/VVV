using System.Collections.Generic;
using System.IO;
using MxFramework.Rendering;
using NUnit.Framework;

namespace MxFramework.Tests.Rendering
{
    public class RenderSubjectIdentityTests
    {
        [Test]
        public void SubjectMap_GetOrCreateAndResolve_ReturnsStableSubject()
        {
            var registry = new MxRenderSubjectRegistry();
            IRenderSubjectMap<int> map = registry.CreateMap<int>();

            MxRenderSubjectId first = map.GetOrCreate(10, MxRenderSubjectRole.Primary);
            MxRenderSubjectId second = map.GetOrCreate(10, MxRenderSubjectRole.Focus);

            Assert.AreEqual(first, second);
            Assert.IsTrue(first.IsValid);
            Assert.IsTrue(map.TryResolve(10, out MxRenderSubjectId resolved));
            Assert.AreEqual(first, resolved);
            Assert.IsTrue(registry.TryResolve(first, out MxRenderSubjectRegistration registration));
            Assert.AreEqual(MxRenderSubjectRole.Primary, registration.Role);
        }

        [Test]
        public void SubjectMap_Release_ClearsSourceMappingAndRejectsStaleSubject()
        {
            var registry = new MxRenderSubjectRegistry();
            IRenderSubjectMap<string> map = registry.CreateMap<string>();
            MxRenderSubjectId subject = map.GetOrCreate("source-a", MxRenderSubjectRole.Tracked);

            Assert.IsTrue(map.Release("source-a"));

            Assert.IsFalse(map.TryResolve("source-a", out var _));
            Assert.IsFalse(registry.TryResolve(subject, out var _));
            Assert.AreEqual(0, registry.ActiveCount);
        }

        [Test]
        public void Registry_ReleasedSlotCanReuseIndexWithNewGeneration()
        {
            var registry = new MxRenderSubjectRegistry();
            IRenderSubjectMap<int> map = registry.CreateMap<int>();
            MxRenderSubjectId first = map.GetOrCreate(1, MxRenderSubjectRole.Primary);

            Assert.IsTrue(map.Release(1));
            MxRenderSubjectId second = map.GetOrCreate(2, MxRenderSubjectRole.LocalControlled);

            Assert.AreNotEqual(first, second);
            Assert.AreEqual(first.Value & ((1 << 20) - 1), second.Value & ((1 << 20) - 1));
            Assert.IsFalse(registry.TryResolve(first, out var _));
            Assert.IsTrue(registry.TryResolve(second, out MxRenderSubjectRegistration registration));
            Assert.AreEqual(MxRenderSubjectRole.LocalControlled, registration.Role);
        }

        [Test]
        public void Registry_ReusesSlotOnlyAfterPublisherReleaseCallbackClearsEvents()
        {
            var registry = new MxRenderSubjectRegistry();
            MxRenderSubjectId first = registry.Register(MxRenderSubjectRole.Primary);
            var publisher = new RenderDataPublisher(registry, recentCapacity: 4);

            publisher.PublishSubjectMovement(first, default);
            Assert.IsTrue(registry.Release(first));
            MxRenderSubjectId second = registry.Register(MxRenderSubjectRole.Focus);

            Assert.AreEqual(first.Value & ((1 << 20) - 1), second.Value & ((1 << 20) - 1));
            Assert.AreNotEqual(first, second);
            Assert.IsFalse(registry.TryResolve(first, out var _));
            Assert.AreEqual(0, publisher.CaptureSnapshot().RecentEventCount);
        }

        [Test]
        public void Registry_GuardsConfiguredSlotCapacityBeforeEncoding()
        {
            var registry = new MxRenderSubjectRegistry(maxSubjectSlots: 1);

            MxRenderSubjectId first = registry.Register(MxRenderSubjectRole.Primary);

            Assert.IsTrue(first.IsValid);
            Assert.Throws<System.InvalidOperationException>(() => registry.Register(MxRenderSubjectRole.Focus));
        }

        [Test]
        public void Registry_ClearAfterRelease_RestoresSlotWithNewGeneration()
        {
            var registry = new MxRenderSubjectRegistry(maxSubjectSlots: 1);
            MxRenderSubjectId first = registry.Register(MxRenderSubjectRole.Primary);

            Assert.IsTrue(registry.Release(first));
            registry.Clear();

            Assert.AreEqual(0, registry.ActiveCount);
            MxRenderSubjectId second = registry.Register(MxRenderSubjectRole.Focus);
            Assert.AreEqual(SlotIndex(first), SlotIndex(second));
            Assert.AreEqual(Generation(first) + 2, Generation(second));
            Assert.IsFalse(registry.TryResolve(first, out var _));
            Assert.IsFalse(registry.Release(first));
            Assert.IsTrue(registry.TryResolve(second, out MxRenderSubjectRegistration registration));
            Assert.AreEqual(MxRenderSubjectRole.Focus, registration.Role);
            Assert.AreEqual(1, registry.ActiveCount);
            Assert.Throws<System.InvalidOperationException>(() => registry.Register(MxRenderSubjectRole.Tracked));
        }

        [Test]
        public void Registry_RepeatedClearWithoutActiveSubjects_PreservesCapacityAndNotifications()
        {
            var registry = new MxRenderSubjectRegistry(maxSubjectSlots: 1);
            MxRenderSubjectId first = registry.Register(MxRenderSubjectRole.Primary);
            var released = new List<MxRenderSubjectId>();
            registry.SubjectReleased += subject => released.Add(subject);

            registry.Clear();
            registry.Clear();
            registry.Clear();

            CollectionAssert.AreEqual(new[] { first }, released);
            Assert.AreEqual(0, registry.ActiveCount);
            MxRenderSubjectId second = registry.Register(MxRenderSubjectRole.Focus);
            Assert.AreEqual(SlotIndex(first), SlotIndex(second));
            Assert.AreEqual(Generation(first) + 3, Generation(second));
            Assert.IsFalse(registry.TryResolve(first, out var _));
            Assert.IsFalse(registry.Release(first));
            Assert.IsTrue(registry.TryResolve(second, out var _));
            Assert.AreEqual(1, registry.ActiveCount);
            Assert.Throws<System.InvalidOperationException>(() => registry.Register(MxRenderSubjectRole.Tracked));
        }

        [Test]
        public void Registry_ClearWithActiveAndReleasedSlots_RestoresBothMapsAndCapacity()
        {
            var registry = new MxRenderSubjectRegistry(maxSubjectSlots: 2);
            var firstMap = registry.CreateMap<int>();
            var secondMap = registry.CreateMap<string>();
            MxRenderSubjectId first = firstMap.GetOrCreate(1, MxRenderSubjectRole.Primary);
            MxRenderSubjectId other = secondMap.GetOrCreate("b", MxRenderSubjectRole.Focus);
            var released = new List<MxRenderSubjectId>();
            registry.SubjectReleased += subject => released.Add(subject);

            Assert.IsTrue(firstMap.Release(1));
            registry.Clear();

            CollectionAssert.AreEqual(new[] { first, other }, released);
            Assert.AreEqual(0, firstMap.Count);
            Assert.AreEqual(0, secondMap.Count);
            Assert.AreEqual(0, registry.ActiveCount);
            Assert.IsFalse(firstMap.TryResolve(1, out var _));
            Assert.IsFalse(secondMap.TryResolve("b", out var _));
            MxRenderSubjectId second = firstMap.GetOrCreate(2, MxRenderSubjectRole.Tracked);
            MxRenderSubjectId nextOther = secondMap.GetOrCreate("b2", MxRenderSubjectRole.Focus);

            CollectionAssert.AreEquivalent(
                new[] { SlotIndex(first), SlotIndex(other) },
                new[] { SlotIndex(second), SlotIndex(nextOther) });
            Assert.AreNotEqual(SlotIndex(second), SlotIndex(nextOther));
            Assert.IsFalse(registry.TryResolve(first, out var _));
            Assert.IsFalse(registry.TryResolve(other, out var _));
            Assert.IsFalse(registry.Release(first));
            Assert.IsFalse(registry.Release(other));
            Assert.IsTrue(registry.TryResolve(second, out MxRenderSubjectRegistration secondRegistration));
            Assert.AreEqual(MxRenderSubjectRole.Tracked, secondRegistration.Role);
            Assert.IsTrue(registry.TryResolve(nextOther, out MxRenderSubjectRegistration otherRegistration));
            Assert.AreEqual(MxRenderSubjectRole.Focus, otherRegistration.Role);
            Assert.AreEqual(2, registry.ActiveCount);
            Assert.Throws<System.InvalidOperationException>(() => firstMap.GetOrCreate(3, MxRenderSubjectRole.Primary));
            Assert.AreEqual(1, firstMap.Count);
            Assert.AreEqual(1, secondMap.Count);
            Assert.IsFalse(firstMap.TryResolve(3, out var _));
        }

        [Test]
        public void Registry_Clear_NotifiesBeforeGenerationAdvanceAndCleansPublisherReferences()
        {
            var registry = new MxRenderSubjectRegistry(maxSubjectSlots: 2);
            var map = registry.CreateMap<int>();
            MxRenderSubjectId first = map.GetOrCreate(1, MxRenderSubjectRole.Primary);
            MxRenderSubjectId other = map.GetOrCreate(2, MxRenderSubjectRole.Focus);
            var publisher = new RenderDataPublisher(registry, recentCapacity: 4);
            publisher.PublishSubjectMovement(first, default);
            publisher.PublishSubjectMovement(other, default);
            Assert.IsTrue(registry.AddReference(first));
            Assert.IsTrue(registry.AddReference(other));
            var released = new List<MxRenderSubjectId>();
            bool invalidDuringNotifications = true;
            bool oldReferencesReleased = true;
            bool publisherCleanedBeforeObserver = true;
            registry.SubjectReleased += subject =>
            {
                released.Add(subject);
                invalidDuringNotifications &= registry.ActiveCount == 0
                    && !registry.TryResolve(first, out var _) && !registry.TryResolve(other, out var _);
                oldReferencesReleased &= registry.ReleaseReference(subject);
                RenderDataPublisherSnapshot snapshot = publisher.CaptureSnapshot();
                for (int i = 0; i < snapshot.CurrentFrameEvents.Count; i++)
                    publisherCleanedBeforeObserver &= snapshot.CurrentFrameEvents[i].Subject != subject;
                for (int i = 0; i < snapshot.RecentEvents.Count; i++)
                    publisherCleanedBeforeObserver &= snapshot.RecentEvents[i].Subject != subject;
            };

            registry.Clear();

            CollectionAssert.AreEqual(new[] { first, other }, released);
            Assert.IsTrue(invalidDuringNotifications);
            Assert.IsTrue(oldReferencesReleased, "Cleanup must run while the old generation still matches.");
            Assert.IsTrue(publisherCleanedBeforeObserver);
            Assert.AreEqual(0, map.Count);
            Assert.AreEqual(0, publisher.CaptureSnapshot().CurrentFrameEventCount);
            Assert.AreEqual(0, publisher.CaptureSnapshot().RecentEventCount);
            MxRenderSubjectId second = map.GetOrCreate(3, MxRenderSubjectRole.Tracked);
            MxRenderSubjectId nextOther = map.GetOrCreate(4, MxRenderSubjectRole.Focus);
            CollectionAssert.AreEquivalent(
                new[] { SlotIndex(first), SlotIndex(other) },
                new[] { SlotIndex(second), SlotIndex(nextOther) });
            Assert.IsFalse(registry.TryResolve(first, out var _));
            Assert.IsFalse(registry.TryResolve(other, out var _));
            Assert.IsTrue(registry.TryResolve(second, out var _));
            Assert.IsTrue(registry.TryResolve(nextOther, out var _));
            Assert.AreEqual(2, registry.ActiveCount);
        }

        [Test]
        public void Registry_Release_NotifiesBeforeAdvancingGeneration()
        {
            var registry = new MxRenderSubjectRegistry(maxSubjectSlots: 1);
            var map = registry.CreateMap<int>();
            MxRenderSubjectId first = map.GetOrCreate(1, MxRenderSubjectRole.Primary);
            Assert.IsTrue(registry.AddReference(first));
            bool invalidDuringNotification = false;
            bool oldReferenceReleased = false;
            int notificationCount = 0;
            registry.SubjectReleased += subject =>
            {
                notificationCount++;
                invalidDuringNotification = subject == first && registry.ActiveCount == 0
                    && map.Count == 0 && !registry.TryResolve(first, out var _);
                oldReferenceReleased = registry.ReleaseReference(subject);
            };

            Assert.IsTrue(map.Release(1));

            Assert.AreEqual(1, notificationCount);
            Assert.IsTrue(invalidDuringNotification);
            Assert.IsTrue(oldReferenceReleased);
            MxRenderSubjectId second = map.GetOrCreate(2, MxRenderSubjectRole.Focus);
            Assert.AreEqual(SlotIndex(first), SlotIndex(second));
            Assert.AreEqual(Generation(first) + 1, Generation(second));
            Assert.IsFalse(registry.TryResolve(first, out var _));
            Assert.IsFalse(registry.Release(first));
            Assert.IsTrue(registry.TryResolve(second, out var _));
        }

        [Test]
        public void Registry_Clear_PreservesOutstandingReferenceGate()
        {
            var registry = new MxRenderSubjectRegistry(maxSubjectSlots: 2);
            MxRenderSubjectId blocked = registry.Register(MxRenderSubjectRole.Primary);
            MxRenderSubjectId free = registry.Register(MxRenderSubjectRole.Focus);
            Assert.IsTrue(registry.AddReference(blocked));
            Assert.IsTrue(registry.Release(free));

            registry.Clear();
            registry.Clear();

            MxRenderSubjectId current = registry.Register(MxRenderSubjectRole.Tracked);
            Assert.AreEqual(SlotIndex(free), SlotIndex(current));
            Assert.IsFalse(registry.TryResolve(blocked, out var _));
            Assert.IsTrue(registry.TryResolve(current, out var _));
            Assert.AreEqual(1, registry.ActiveCount);
            Assert.Throws<System.InvalidOperationException>(() => registry.Register(MxRenderSubjectRole.Focus));
        }

        [Test]
        public void PublicRenderingApi_DoesNotExposeForbiddenBusinessWords()
        {
            string renderingApi = string.Join(
                "\n",
                File.ReadAllText("Packages/com.vincent.wgameframework/Rendering/RenderSubjectIdentity.cs"),
                File.ReadAllText("Packages/com.vincent.wgameframework/Rendering/RenderDataPublisher.cs"),
                File.ReadAllText("Packages/com.vincent.wgameframework/Rendering/RenderingDiagnostics.cs"));

            AssertForbidden(renderingApi, "Player");
            AssertForbidden(renderingApi, "Enemy");
            AssertForbidden(renderingApi, "Boss");
            AssertForbidden(renderingApi, "Hero");
            AssertForbidden(renderingApi, "Monster");
            AssertForbidden(renderingApi, "Skill");
            AssertForbidden(renderingApi, "Element");
            AssertForbidden(renderingApi, "LocalPlayer");
        }

        private static int SlotIndex(MxRenderSubjectId subject)
        {
            return subject.Value & ((1 << 20) - 1);
        }

        private static int Generation(MxRenderSubjectId subject)
        {
            return subject.Value >> 20;
        }

        private static void AssertForbidden(string text, string forbidden)
        {
            Assert.IsFalse(text.Contains(forbidden), forbidden);
        }
    }
}
