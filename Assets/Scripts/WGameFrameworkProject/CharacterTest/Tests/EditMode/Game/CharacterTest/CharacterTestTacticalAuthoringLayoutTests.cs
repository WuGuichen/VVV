using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalAuthoringLayoutTests
    {
        [TestCase(54f, 3.3f)]
        [TestCase(54f, 1f)]
        [TestCase(4f, 3.3f)]
        [TestCase(4f, 1f)]
        public void PreviewFitKeepsBothSubjectsInsideTheActualAspect(float span, float aspect)
        {
            Vector3 a = new Vector3(-span * 0.5f, 0, 0), b = new Vector3(span * 0.5f, 0, 0);
            Vector3 pivot = Vector3.up * 0.8f;
            Vector3 direction = new Vector3(0, 0.6f, -0.8f);
            Quaternion rotation = Quaternion.LookRotation(-direction, Vector3.up);
            float distance = CharacterTestRuntimeAiLabBehaviorCardPreviewHost.CalculateFramingDistance(a, b, pivot, rotation, 42, aspect);
            float vertical = Mathf.Tan(21 * Mathf.Deg2Rad), horizontal = vertical * aspect;
            foreach (var point in new[] { a, b, a + Vector3.up * 3.5f, b + Vector3.up * 3.5f })
            {
                Vector3 local = Quaternion.Inverse(rotation) * (point - pivot) + Vector3.forward * distance;
                Assert.Greater(local.z, 0);
                Assert.Less(Mathf.Abs(local.x / (local.z * horizontal)), 1f);
                Assert.Less(Mathf.Abs(local.y / (local.z * vertical)), 1f);
            }
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2560f, 1440f)]
        [TestCase(1280f, 720f)]
        public void SequentialWorkspaceKeepsInspectorFullHeightAndPreviewBelowEvents(float width, float height)
        {
            var state = new CharacterTestRuntimeAiLabTacticalAuthoringLayoutState { Sequential = true };
            Assert.IsTrue(state.TryGet(width, height, out var layout));
            Assert.Less(layout.Overview.xMax, layout.Timeline.xMin);
            Assert.Less(layout.Preview.xMax, layout.Inspector.xMin);
            Assert.AreEqual(layout.Timeline.yMax, layout.Divider.yMin);
            Assert.AreEqual(layout.Divider.yMax, layout.Preview.yMin);
            Assert.AreEqual(layout.Preview.yMax, layout.Inspector.yMax);
            Assert.AreEqual(layout.Overview.yMin, layout.Inspector.yMin);
            Assert.GreaterOrEqual(layout.Timeline.height, 360f);
            Assert.GreaterOrEqual(layout.Preview.height, 180f);
            Assert.Greater(layout.Preview.width, 0f);
        }

        [Test]
        public void SequentialDividerPreferenceSurvivesExpandWithoutChangingLegacyPreference()
        {
            var state = new CharacterTestRuntimeAiLabTacticalAuthoringLayoutState { Sequential = true };
            Assert.IsTrue(state.SetDividerPosition(new Vector2(700, 720), 1920, 1080));
            Assert.IsTrue(state.TryGet(1920, 1080, out var before));
            float legacyRatio = state.SplitRatio;
            state.SetExpanded(true);
            Assert.IsFalse(state.SetDividerPosition(new Vector2(700, 400), 1920, 1080));
            state.SetExpanded(false);
            Assert.IsTrue(state.TryGet(1920, 1080, out var after));
            Assert.AreEqual(before.Preview, after.Preview);
            Assert.AreEqual(before.Timeline, after.Timeline);
            Assert.AreEqual(legacyRatio, state.SplitRatio);
        }

        [TestCase(1920f, 1080f)]
        [TestCase(1280f, 720f)]
        public void FourRegionsAreDisjointAndPreviewTimelineShareTheirDivider(float width, float height)
        {
            var state = new CharacterTestRuntimeAiLabTacticalAuthoringLayoutState();
            Assert.IsTrue(state.TryGet(width, height, out var layout));
            Assert.Less(layout.Overview.xMax, layout.Preview.xMin);
            Assert.Less(layout.Preview.xMax, layout.Inspector.xMin);
            Assert.AreEqual(layout.Preview.x, layout.Timeline.x);
            Assert.AreEqual(layout.Preview.width, layout.Timeline.width);
            Assert.AreEqual(layout.Preview.yMax, layout.Divider.yMin);
            Assert.AreEqual(layout.Divider.yMax, layout.Timeline.yMin);
            Assert.AreEqual(layout.Timeline.yMax, layout.Inspector.yMax);
            foreach (var rect in new[] { layout.Overview, layout.Preview, layout.Timeline, layout.Inspector })
            {
                Assert.GreaterOrEqual(rect.xMin, 0);
                Assert.LessOrEqual(rect.xMax, width);
                Assert.GreaterOrEqual(rect.yMin, 0);
                Assert.LessOrEqual(rect.yMax, height);
                Assert.GreaterOrEqual(rect.height, 180);
            }
        }

        [Test]
        public void ExpandIgnoresHiddenDividerCommandsAndRestoresTheUsersRatio()
        {
            var state = new CharacterTestRuntimeAiLabTacticalAuthoringLayoutState();
            Assert.IsTrue(state.SetDividerPosition(new Vector2(600, 700), 1920, 1080));
            Assert.IsTrue(state.TryGet(1920, 1080, out var before));
            float ratio = state.SplitRatio;
            state.SetExpanded(true);
            Assert.IsTrue(state.TryGet(1920, 1080, out var expanded));
            Assert.Greater(expanded.Preview.width, before.Preview.width);
            Assert.AreEqual(0, expanded.Timeline.width);
            Assert.IsFalse(state.SetDividerPosition(new Vector2(600, 100), 1920, 1080));
            Assert.AreEqual(ratio, state.SplitRatio);
            state.SetExpanded(false);
            Assert.IsTrue(state.TryGet(1920, 1080, out var restored));
            Assert.AreEqual(before.Preview, restored.Preview);
            Assert.AreEqual(before.Timeline, restored.Timeline);
        }

        [Test]
        public void DividerClampsBothPanesAndUnsupportedSizesFailExplicitly()
        {
            var state = new CharacterTestRuntimeAiLabTacticalAuthoringLayoutState();
            Assert.IsTrue(state.SetDividerPosition(new Vector2(500, -100), 1280, 720));
            Assert.IsTrue(state.TryGet(1280, 720, out var low));
            Assert.AreEqual(180, low.Preview.height, 0.01f);
            Assert.IsTrue(state.SetDividerPosition(new Vector2(500, 10000), 1280, 720));
            Assert.IsTrue(state.TryGet(1280, 720, out var high));
            Assert.AreEqual(180, high.Timeline.height, 0.01f);
            Assert.IsFalse(state.TryGet(1000, 600, out _));
            Assert.IsFalse(state.TryGet(float.NaN, 1080, out _));
        }
    }
}
