using MxFramework.Diagnostics;
using MxFramework.Rendering;
using NUnit.Framework;
using UnityEngine;
using System;
using System.Linq;

namespace MxFramework.Tests.Rendering
{
    public class RenderDataPublisherTests
    {
        [Test]
        public void PublisherInterface_ExposesDocumentedSemanticApi()
        {
            string[] methodNames = typeof(IRenderDataPublisher)
                .GetMethods()
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    nameof(IRenderDataPublisher.PublishFieldImpulse),
                    nameof(IRenderDataPublisher.PublishImpact),
                    nameof(IRenderDataPublisher.PublishSubjectLifecycle),
                    nameof(IRenderDataPublisher.PublishSubjectMovement),
                    nameof(IRenderDataPublisher.PublishSurfaceContact)
                },
                methodNames);
        }

        [Test]
        public void Publisher_TracksCurrentRecentAndTotalCountsByEventKind()
        {
            var registry = new MxRenderSubjectRegistry();
            MxRenderSubjectId subject = registry.Register(MxRenderSubjectRole.Primary);
            var publisher = new RenderDataPublisher(registry, recentCapacity: 8);

            publisher.PublishImpact(subject, new MxRenderImpactEvent(Vector3.one, Color.red, 2f, 0.25f));
            publisher.PublishSurfaceContact(subject, new MxRenderSurfaceContactEvent(Vector3.right, 1.5f, 0.75f));
            publisher.PublishFieldImpulse(subject, new MxRenderFieldImpulseEvent(Vector3.zero, 3f, 4f, 9));
            publisher.PublishSubjectMovement(subject, Vector3.forward);
            publisher.PublishSubjectLifecycle(subject, MxSubjectLifecycleKind.Enabled);

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
            Assert.AreEqual(Color.red, snapshot.CurrentFrameEvents[0].Tint);
            Assert.AreEqual(1.5f, snapshot.CurrentFrameEvents[1].Radius);
            Assert.AreEqual(9, snapshot.CurrentFrameEvents[2].ChannelId);
            Assert.AreEqual(Vector3.forward, snapshot.CurrentFrameEvents[3].Direction);
            Assert.AreEqual(MxSubjectLifecycleKind.Enabled, snapshot.CurrentFrameEvents[4].Lifecycle);
        }

        [Test]
        public void Publisher_BeginFrameClearsPerFrameCountsAndKeepsRecentAndTotalCounts()
        {
            var registry = new MxRenderSubjectRegistry();
            MxRenderSubjectId subject = registry.Register(MxRenderSubjectRole.Focus);
            var publisher = new RenderDataPublisher(registry, recentCapacity: 8);

            publisher.PublishImpact(subject, new MxRenderImpactEvent(default, Color.white, 1f, 0.1f));
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

            publisher.PublishImpact(subject, new MxRenderImpactEvent(default, Color.white, 1f, 0.1f));
            publisher.PublishSubjectMovement(subject, Vector3.right);
            publisher.PublishSubjectMovement(subject, Vector3.left);

            RenderDataPublisherSnapshot snapshot = publisher.CaptureSnapshot();

            Assert.AreEqual(3, snapshot.CurrentFrameEventCount);
            Assert.AreEqual(2, snapshot.RecentEventCount);
            Assert.AreEqual(0, snapshot.RecentCount(RenderDataEventKind.Impact));
            Assert.AreEqual(2, snapshot.RecentCount(RenderDataEventKind.Movement));
            Assert.AreEqual(1, snapshot.TotalCount(RenderDataEventKind.Impact));
            Assert.AreEqual(2, snapshot.TotalCount(RenderDataEventKind.Movement));
        }

        [Test]
        public void Publisher_RequiresRegistryAndRejectsInvalidUnknownOrReleasedSubjects()
        {
            var registry = new MxRenderSubjectRegistry();
            MxRenderSubjectId subject = registry.Register(MxRenderSubjectRole.Primary);
            var publisher = new RenderDataPublisher(registry);

            Assert.Throws<ArgumentNullException>(() => new RenderDataPublisher(null));

            publisher.PublishImpact(default, new MxRenderImpactEvent(default, Color.white, 1f, 0.1f));
            publisher.PublishSubjectLifecycle(subject, (MxSubjectLifecycleKind)99);

            registry.Release(subject);

            publisher.PublishImpact(subject, new MxRenderImpactEvent(default, Color.white, 1f, 0.1f));
            publisher.PublishImpact(new MxRenderSubjectId(123), new MxRenderImpactEvent(default, Color.white, 1f, 0.1f));
            Assert.AreEqual(0, publisher.CaptureSnapshot().TotalEventCount);
        }

        [Test]
        public void Publisher_LifecycleReleaseClearsSubjectStateAndAllowsFreshRegistration()
        {
            var registry = new MxRenderSubjectRegistry();
            RenderSubjectMap<int> map = registry.CreateMap<int>();
            var publisher = new RenderDataPublisher(registry);
            MxRenderSubjectId first = map.GetOrCreate(7, MxRenderSubjectRole.LocalControlled);

            publisher.PublishSubjectLifecycle(first, MxSubjectLifecycleKind.Spawned);
            publisher.PublishSubjectMovement(first, Vector3.forward);

            Assert.IsTrue(map.Release(7));

            RenderDataPublisherSnapshot cleared = publisher.CaptureSnapshot();
            Assert.IsFalse(map.TryResolve(7, out var _));
            Assert.AreEqual(0, cleared.CurrentFrameEventCount);
            Assert.AreEqual(0, cleared.RecentEventCount);
            Assert.AreEqual(2, cleared.TotalEventCount);

            MxRenderSubjectId second = map.GetOrCreate(8, MxRenderSubjectRole.LocalControlled);
            Assert.AreNotEqual(first, second);
            publisher.PublishSubjectMovement(second, Vector3.right);
            Assert.AreEqual(1, publisher.CaptureSnapshot().CurrentFrameCount(RenderDataEventKind.Movement));
        }

        [Test]
        public void PublisherDebugSource_ExposesPublisherCountsSection()
        {
            var registry = new MxRenderSubjectRegistry();
            MxRenderSubjectId subject = registry.Register(MxRenderSubjectRole.Primary);
            var publisher = new RenderDataPublisher(registry);
            publisher.PublishImpact(subject, new MxRenderImpactEvent(default, Color.white, 1f, 0.1f));
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
