using System;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionAnimationTimelineResolverTests
    {
        private const string CharacterStableId = "char.iron_vanguard";
        private const string StandingJumpClipResourceKey = "art.character.skeleton.animation.standing_jump";
        private const string StandingJumpClipPath =
            "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_jump.anim";

        [Test]
        public void ApplyAutoSpan_LeavesManualWindowWhenClipUnknown()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            var evt = new AnimationTrackEventJsonDto
            {
                Frame = 2,
                EndFrame = 9,
                PlaybackSpeed = 1f,
                AnimationActionKey = "anim.unknown.key",
            };

            bool resolved = CharacterActionAnimationTimelineResolver.ApplyAutoSpan(evt, action, 24);

            Assert.IsFalse(resolved);
            Assert.AreEqual(9, evt.EndFrame);
        }

        [Test]
        public void ApplyAutoSpan_UsesGeneratedClipResourceKeyFrames()
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(StandingJumpClipPath);
            Assert.NotNull(clip, "Missing generated source clip fixture: " + StandingJumpClipPath);
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 200;
            var evt = new AnimationTrackEventJsonDto
            {
                Frame = 2,
                PlaybackSpeed = 2f,
                AnimationClipResourceKey = StandingJumpClipResourceKey,
                AnimationStartOffsetFrames = 6,
                AnimationEndOffsetFrames = 4,
            };

            CharacterActionAnimationTimelineResolver.ApplyAutoSpan(evt, action, 24, CharacterStableId);

            int sourceFrames = Mathf.CeilToInt(clip.length * CharacterActionTimelineLayout.EditorTimelineFps);
            int expectedSpan = CharacterActionAnimationTimelineResolver.ComputeTimelineSpanFrames(
                sourceFrames - 10,
                2f);
            Assert.AreEqual(evt.Frame + expectedSpan - 1, evt.EndFrame);
        }

        [Test]
        public void ApplyRange_NormalResizeRetimesAnimation()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 200;
            var evt = new AnimationTrackEventJsonDto
            {
                Frame = 2,
                PlaybackSpeed = 1f,
                AnimationClipResourceKey = StandingJumpClipResourceKey,
                AnimationStartOffsetFrames = 6,
                AnimationEndOffsetFrames = 4,
            };
            Assert.IsTrue(CharacterActionAnimationTimelineResolver.TryResolveSourceClipFrames(
                evt,
                CharacterStableId,
                out int sourceFrames));
            int playableFrames = sourceFrames - 10;

            CharacterActionAnimationTimelineResolver.ApplyRange(
                evt,
                requestedStartFrame: 2,
                requestedEndFrame: 21,
                trimSource: false,
                actionDurationFrames: 200,
                characterStableId: CharacterStableId);

            float expectedSpeed = CharacterActionAnimationTimelineResolver.ComputePlaybackSpeed(
                playableFrames,
                20);
            int expectedSpan = CharacterActionAnimationTimelineResolver.ComputeTimelineSpanFrames(
                playableFrames,
                expectedSpeed);
            Assert.AreEqual(expectedSpeed, evt.PlaybackSpeed, 0.0001f);
            Assert.AreEqual(2, evt.Frame);
            Assert.AreEqual(2 + expectedSpan - 1, evt.EndFrame);
            Assert.AreEqual(6, evt.AnimationStartOffsetFrames);
            Assert.AreEqual(4, evt.AnimationEndOffsetFrames);
        }

        [Test]
        public void ApplyRange_AltResizeTrimsAndMovePreservesTiming()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 200;
            var evt = new AnimationTrackEventJsonDto
            {
                Frame = 2,
                PlaybackSpeed = 1f,
                AnimationClipResourceKey = StandingJumpClipResourceKey,
            };
            Assert.IsTrue(CharacterActionAnimationTimelineResolver.ApplyAutoSpan(
                evt,
                action,
                24,
                CharacterStableId));
            int trimmedEnd = Math.Max(evt.Frame, evt.EndFrame - 5);

            CharacterActionAnimationTimelineResolver.ApplyRange(
                evt,
                evt.Frame,
                trimmedEnd,
                trimSource: true,
                actionDurationFrames: 200,
                characterStableId: CharacterStableId);

            Assert.AreEqual(1f, evt.PlaybackSpeed, 0.0001f);
            Assert.Greater(evt.AnimationEndOffsetFrames, 0);
            int span = evt.EndFrame - evt.Frame;
            int startOffset = evt.AnimationStartOffsetFrames;
            int endOffset = evt.AnimationEndOffsetFrames;

            CharacterActionAnimationTimelineResolver.ApplyRange(
                evt,
                evt.Frame + 3,
                evt.EndFrame + 3,
                trimSource: false,
                actionDurationFrames: 200,
                characterStableId: CharacterStableId);

            Assert.AreEqual(span, evt.EndFrame - evt.Frame);
            Assert.AreEqual(startOffset, evt.AnimationStartOffsetFrames);
            Assert.AreEqual(endOffset, evt.AnimationEndOffsetFrames);
            Assert.AreEqual(1f, evt.PlaybackSpeed, 0.0001f);
        }

        [Test]
        public void TimingMath_UsesCeilingAndClampsPlaybackSpeed()
        {
            Assert.AreEqual(16f, CharacterActionAnimationTimelineResolver.MaxPlaybackSpeed);
            Assert.AreEqual(
                4,
                CharacterActionAnimationTimelineResolver.ComputeTimelineSpanFrames(
                    playableSourceFrames: 7,
                    playbackSpeed: 2f));
            Assert.AreEqual(
                CharacterActionAnimationTimelineResolver.MaxPlaybackSpeed,
                CharacterActionAnimationTimelineResolver.ComputePlaybackSpeed(
                    playableSourceFrames: 60,
                    timelineSpanFrames: 1));
            Assert.AreEqual(
                CharacterActionAnimationTimelineResolver.MinPlaybackSpeed,
                CharacterActionAnimationTimelineResolver.ComputePlaybackSpeed(
                    playableSourceFrames: 1,
                    timelineSpanFrames: 9999));
        }

        [Test]
        public void FitEndToActionDuration_SetsLastFrame()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 20;
            var evt = new AnimationTrackEventJsonDto { Frame = 3, EndFrame = 3 };

            CharacterActionAnimationTimelineResolver.FitEndToActionDuration(evt, action, 8);

            Assert.AreEqual(19, evt.EndFrame);
        }
    }
}
