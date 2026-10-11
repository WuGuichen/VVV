using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.Story.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.StoryUnity
{
    public sealed class StoryRuntimeEventPresentationRouterTests
    {
        [Test]
        public void DispatchesPresentationEvents()
        {
            var gameObject = new GameObject("story-event-router-test");
            try
            {
                StoryRuntimeEventPresentationRouter router = gameObject.AddComponent<StoryRuntimeEventPresentationRouter>();
                int routedCount = 0;
                StoryRuntimeEvent routedEvent = default;
                router.OnPresentationEvent.AddListener(evt =>
                {
                    routedCount++;
                    routedEvent = evt;
                });
                StoryRuntimeEventRoute stepRoute = router.AddRoute(StoryEventKind.StepStarted);
                int routeSpecificCount = 0;
                stepRoute.Event.AddListener(_ => routeSpecificCount++);

                var evt = new StoryRuntimeEvent(
                    RuntimeFrame.Zero,
                    StoryEventKind.StepStarted,
                    graphId: 101,
                    beatId: 201,
                    beatInstanceId: 1,
                    stepId: 401);

                StoryRuntimeEventRouteResult result = router.Route(evt);

                Assert.IsTrue(result.Supported);
                Assert.IsTrue(result.Routed);
                Assert.AreEqual(1, routedCount);
                Assert.AreEqual(1, routeSpecificCount);
                Assert.AreEqual(401, routedEvent.StepId);
                Assert.AreEqual(1, router.RecentEvents.Count);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void IgnoresUnsupportedEvents()
        {
            var gameObject = new GameObject("story-event-router-ignore-test");
            try
            {
                StoryRuntimeEventPresentationRouter router = gameObject.AddComponent<StoryRuntimeEventPresentationRouter>();
                int routedCount = 0;
                router.OnPresentationEvent.AddListener(_ => routedCount++);

                StoryRuntimeEventRouteResult result = router.Route(new StoryRuntimeEvent(
                    RuntimeFrame.Zero,
                    StoryEventKind.FactChanged,
                    graphId: 101));

                Assert.IsFalse(result.Supported);
                Assert.IsFalse(result.Routed);
                Assert.AreEqual(0, routedCount);
                Assert.AreEqual(0, router.RecentEvents.Count);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DrainAndRouteConsumesRuntimeEventQueue()
        {
            var queue = new RuntimeEventQueue<StoryRuntimeEvent>();
            queue.Enqueue(RuntimeFrame.Zero, new StoryRuntimeEvent(RuntimeFrame.Zero, StoryEventKind.BeatEntered, graphId: 101, beatId: 201));
            var gameObject = new GameObject("story-event-router-drain-test");
            try
            {
                StoryRuntimeEventPresentationRouter router = gameObject.AddComponent<StoryRuntimeEventPresentationRouter>();
                int routedCount = 0;
                router.OnPresentationEvent.AddListener(_ => routedCount++);

                int drained = router.DrainAndRoute(queue, RuntimeFrame.Zero);

                Assert.AreEqual(1, drained);
                Assert.AreEqual(1, routedCount);
                Assert.AreEqual(0, queue.PendingCount);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
