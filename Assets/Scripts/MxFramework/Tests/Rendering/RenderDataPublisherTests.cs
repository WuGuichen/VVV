using MxFramework.Diagnostics;
using MxFramework.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Rendering
{
    public class RenderDataPublisherTests
    {
        [Test]
        public void Publisher_TracksCurrentRecentAndTotalCountsByEventKind()
        {
            var registry = new MxRenderSubjectRegistry();
            MxRenderSubjectId subject = registry.Register(MxRenderSubjectRole.Primary);
            var publisher = new RenderDataPublisher(registry, recentCapacity: 8);

            Assert.IsTrue(publisher.Publish(new RenderDataEvent(subject, RenderDataEventKind.Impact, Vector3.one, Vector3.up, 2f)));
            Assert.IsTrue(publisher.Publish(new RenderDataEvent(subject, RenderDataEventKind.SurfaceContact, Vector3.right, Vector3.up)));
            Assert.IsTrue(publisher.Publish(new RenderDataEvent(subject, RenderDataEventKind.FieldImpulse, Vector3.zero, Vector3.forward, 3f)));
            Assert.IsTrue(publisher.Publish(new RenderDataEvent(subject, RenderDataEventKind.Movement, Vector3.forward, Vector3.forward)));
            Assert.IsTrue(publisher.Publish(new RenderDataEvent(subject, RenderDataEventKind.Lifecycle, Vector3.zero, Vector3.zero)));

            RenderDataPublisherSnapshot snapshot = publisher.CaptureSnapshot();

            Assert.AreEqual(5, snapshot.CurrentFrameEventCount);
            Assert.AreEqual(5, snapshot.RecentEventCount);
            Assert.AreEqual(5, snapshot.TotalEventCount);
            Assert.AreEqual(1, snapshot.CurrentFrameCount(RenderDataEventKind.Impact));
            Assert.AreEqual(1, snapshot.CurrentFrameCount(RenderDataEventKind.SurfaceContact));
            Assert.AreEqual(1, snapshot.CurrentFrameCount(RenderDataEventKind.FieldImpulse));
            Assert.AreEqual(1, snapshot.CurrentFrameCount(RenderDataEventKind.Movement));
            Assert.AreEqual(1, snapshot.CurrentFrameCount(RenderDataEventKind.Lifecycle));
            Assert.AreEqual(1, snapshot.RecentCount(RenderDataEventKind.Impact));
            Assert.AreEqual(1, snapshot.TotalCount(RenderDataEventKind.Lifecycle));
        }

        [Test]
        public void Publisher_BeginFrameClearsPerFrameCountsAndKeepsRecentAndTotalCounts()
        {
            var registry = new MxRenderSubjectRegistry();
            MxRenderSubjectId subject = registry.Register(MxRenderSubjectRole.Focus);
            var publisher = new RenderDataPublisher(registry, recentCapacity: 8);

            publisher.Publish(new RenderDataEvent(subject, RenderDataEventKind.Impact, default, default));
            publisher.BeginFrame();

            RenderDataPublisherSnapshot snapshot = publisher.CaptureSnapshot();

            Assert.AreEqual(0, snapshot.CurrentFrameEventCount);
            Assert.AreEqual(1, snapshot.RecentEventCount);
            Assert.AreEqual(1, snapshot.TotalEventCount);
            Assert.AreEqual(0, snapshot.CurrentFrameCount(RenderDataEventKind.Impact));
            Assert.AreEqual(1, snapshot.RecentCount(RenderDataEventKind.Impact));
            Assert.AreEqual(1, snapshot.TotalCount(RenderDataEventKind.Impact));
        }

        [Test]
        public void Publisher_RecentCapacityEvictsOldestCounts()
        {
            var registry = new MxRenderSubjectRegistry();
            MxRenderSubjectId subject = registry.Register(MxRenderSubjectRole.Tracked);
            var publisher = new RenderDataPublisher(registry, recentCapacity: 2);

            publisher.Publish(new RenderDataEvent(subject, RenderDataEventKind.Impact, default, default));
            publisher.Publish(new RenderDataEvent(subject, RenderDataEventKind.Movement, default, default));
            publisher.Publish(new RenderDataEvent(subject, RenderDataEventKind.Movement, default, default));

            RenderDataPublisherSnapshot snapshot = publisher.CaptureSnapshot();

            Assert.AreEqual(3, snapshot.CurrentFrameEventCount);
            Assert.AreEqual(2, snapshot.RecentEventCount);
            Assert.AreEqual(0, snapshot.RecentCount(RenderDataEventKind.Impact));
            Assert.AreEqual(2, snapshot.RecentCount(RenderDataEventKind.Movement));
            Assert.AreEqual(1, snapshot.TotalCount(RenderDataEventKind.Impact));
            Assert.AreEqual(2, snapshot.TotalCount(RenderDataEventKind.Movement));
        }

        [Test]
        public void Publisher_RejectsInvalidUnknownOrReleasedSubjects()
        {
            var registry = new MxRenderSubjectRegistry();
            MxRenderSubjectId subject = registry.Register(MxRenderSubjectRole.Primary);
            var publisher = new RenderDataPublisher(registry);

            Assert.IsFalse(publisher.Publish(new RenderDataEvent(default, RenderDataEventKind.Impact, default, default)));
            Assert.IsFalse(publisher.Publish(new RenderDataEvent(subject, (RenderDataEventKind)99, default, default)));

            registry.Release(subject);

            Assert.IsFalse(publisher.Publish(new RenderDataEvent(subject, RenderDataEventKind.Impact, default, default)));
            Assert.AreEqual(0, publisher.CaptureSnapshot().TotalEventCount);
        }

        [Test]
        public void Publisher_LifecycleReleaseClearsSubjectStateAndAllowsFreshRegistration()
        {
            var registry = new MxRenderSubjectRegistry();
            RenderSubjectMap<int> map = registry.CreateMap<int>();
            var publisher = new RenderDataPublisher(registry);
            MxRenderSubjectId first = map.GetOrCreate(7, MxRenderSubjectRole.LocalControlled);

            publisher.Publish(new RenderDataEvent(first, RenderDataEventKind.Lifecycle, default, default));
            publisher.Publish(new RenderDataEvent(first, RenderDataEventKind.Movement, default, default));

            Assert.IsTrue(map.Release(7));

            RenderDataPublisherSnapshot cleared = publisher.CaptureSnapshot();
            Assert.IsFalse(map.TryResolve(7, out var _));
            Assert.AreEqual(0, cleared.CurrentFrameEventCount);
            Assert.AreEqual(0, cleared.RecentEventCount);
            Assert.AreEqual(2, cleared.TotalEventCount);

            MxRenderSubjectId second = map.GetOrCreate(8, MxRenderSubjectRole.LocalControlled);
            Assert.AreNotEqual(first, second);
            Assert.IsTrue(publisher.Publish(new RenderDataEvent(second, RenderDataEventKind.Movement, default, default)));
        }

        [Test]
        public void PublisherDebugSource_ExposesPublisherCountsSection()
        {
            var registry = new MxRenderSubjectRegistry();
            MxRenderSubjectId subject = registry.Register(MxRenderSubjectRole.Primary);
            var publisher = new RenderDataPublisher(registry);
            publisher.Publish(new RenderDataEvent(subject, RenderDataEventKind.Impact, default, default));
            var source = new RenderDataPublisherDebugSource(publisher);

            FrameworkDebugSnapshot snapshot = source.CreateSnapshot();

            Assert.AreEqual("Rendering", snapshot.SourceName);
            Assert.AreEqual(FrameworkDebugMode.Runtime, snapshot.Mode);
            Assert.AreEqual(RenderingDebugSectionNames.PublisherCounts, snapshot.Sections[0].Title);
            StringAssert.Contains("currentFrameEvents: 1", snapshot.Sections[0].Body);
            StringAssert.Contains("Impact: current=1 recent=1 total=1", snapshot.Sections[0].Body);
        }
    }
}
