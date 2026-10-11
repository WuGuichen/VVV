using MxFramework.Editor.Authoring;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Editor.CharacterAction.Timeline.Preview;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionTimelinePlaybackControllerTests
    {
        [Test]
        public void Seek_ClampsToDurationMinusOne()
        {
            var controller = new CharacterActionTimelinePlaybackController();
            controller.SetDuration(12);
            controller.Seek(99);
            Assert.AreEqual(11, controller.CurrentFrame);
            controller.Dispose();
        }

        [Test]
        public void SetDuration_ClampsCurrentFrame()
        {
            var controller = new CharacterActionTimelinePlaybackController();
            controller.SetDuration(24);
            controller.Seek(20);
            controller.SetDuration(10);
            Assert.AreEqual(9, controller.CurrentFrame);
            controller.Dispose();
        }

        [Test]
        public void AdvanceForTests_AdvancesOneFrameAtSixtyFps()
        {
            var controller = new CharacterActionTimelinePlaybackController();
            controller.SetDuration(24);
            controller.Seek(0);
            controller.AdvanceForTests(1f / CharacterActionTimelineLayout.EditorTimelineFps + 0.001f);
            Assert.AreEqual(1, controller.CurrentFrame);
            controller.Dispose();
        }

        [Test]
        public void AdvanceForTests_LoopsAtEnd()
        {
            var controller = new CharacterActionTimelinePlaybackController();
            controller.SetDuration(3);
            controller.Seek(2);
            controller.AdvanceForTests(1f / CharacterActionTimelineLayout.EditorTimelineFps + 0.001f);
            Assert.AreEqual(0, controller.CurrentFrame);
            controller.Dispose();
        }

        [Test]
        public void AdvanceForTests_WhenLoopDisabledStopsAtEnd()
        {
            var controller = new CharacterActionTimelinePlaybackController
            {
                LoopPlayback = false,
            };
            controller.SetDuration(3);
            controller.Seek(2);
            controller.Play();

            controller.AdvanceForTests(1f / CharacterActionTimelineLayout.EditorTimelineFps + 0.001f);

            Assert.AreEqual(2, controller.CurrentFrame);
            Assert.IsFalse(controller.IsPlaying);
            controller.Dispose();
        }

        [Test]
        public void PlaybackSpeed_NonFiniteValueFallsBackToNormalSpeed()
        {
            var controller = new CharacterActionTimelinePlaybackController
            {
                PlaybackSpeed = float.PositiveInfinity,
            };

            Assert.AreEqual(1f, controller.PlaybackSpeed);

            controller.PlaybackSpeed = float.NaN;
            Assert.AreEqual(1f, controller.PlaybackSpeed);
            controller.Dispose();
        }

        [Test]
        public void AdvanceForTests_HugeEditorDelayHasBoundedCatchUp()
        {
            var controller = new CharacterActionTimelinePlaybackController();
            controller.SetDuration(10000);

            controller.AdvanceForTests(10000f);

            Assert.AreEqual(
                AuthoringTimelinePlaybackController.MaxAdvanceStepsPerUpdate,
                controller.CurrentFrame);
            controller.Dispose();
        }

        [Test]
        public void AdvanceForTests_NonFiniteDeltaIsIgnored()
        {
            var controller = new CharacterActionTimelinePlaybackController();
            controller.SetDuration(100);

            controller.AdvanceForTests(float.PositiveInfinity);
            controller.AdvanceForTests(float.NaN);

            Assert.AreEqual(0, controller.CurrentFrame);
            controller.Dispose();
        }
    }
}
