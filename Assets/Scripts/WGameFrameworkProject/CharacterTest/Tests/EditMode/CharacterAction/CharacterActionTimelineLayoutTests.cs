using System;
using System.Linq;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Editor.CharacterAction.Timeline.Adapters;
using MxFramework.Editor.CharacterAction.Timeline.Preview;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionTimelineLayoutTests
    {
        [Test]
        public void XToFrame_SnapsToNearestInteger()
        {
            int frame = CharacterActionTimelineLayout.XToFrame(35f, 10f, snapToFrame: true);
            Assert.AreEqual(4, frame);
        }

        [Test]
        public void ClampFrame_ClampsToDuration()
        {
            int frame = CharacterActionTimelineLayout.ClampFrame(99, 12);
            Assert.AreEqual(11, frame);
        }

        [Test]
        public void Snapshot_FromJumpAction_IncludesPhaseAndMotionEvent()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            CharacterActionJsonDto jump = CharacterActionAuthoringModel.CreateJumpAction();
            model.AddAction(jump);
            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(jump, 8);

            Assert.AreEqual(8, snapshot.DurationFrames);
            Assert.Greater(snapshot.ItemsByTrack[TimelineTrackKind.Phases].Count, 0);
            Assert.Greater(snapshot.ItemsByTrack[TimelineTrackKind.Motion].Count, 0);
            Assert.Greater(snapshot.ItemsByTrack[TimelineTrackKind.Animation].Count, 0);
        }

        [Test]
        public void Snapshot_TransitionWindows_RenderOnDedicatedTrack()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateLightAttackAction();
            action.CancelRules = new[]
            {
                new CharacterCancelRuleJsonDto
                {
                    StartFrame = 6,
                    EndFrame = 8,
                    RequestTypeMask = (int)CharacterActionRequestType.Dodge,
                    ActionSlotMask = (int)CharacterCancelInputMask.Dodge,
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            TimelineTrackDescriptor track = snapshot.Tracks.FirstOrDefault(item => item.Kind == TimelineTrackKind.TransitionWindow);
            ITimelineItemAdapter adapter = snapshot.ItemsByTrack[TimelineTrackKind.TransitionWindow][0];

            Assert.NotNull(track);
            StringAssert.Contains("切换窗口", track.Title);
            StringAssert.Contains("Transition Window", track.Title);
            Assert.AreEqual(TimelineItemKind.Clip, adapter.ItemKind);
            Assert.AreEqual(6, adapter.StartFrame);
            Assert.AreEqual(8, adapter.EndFrame);
            StringAssert.Contains("Dodge", adapter.FullTitle);
            StringAssert.DoesNotContain("CancelRule", adapter.FullTitle);
        }

        [Test]
        public void TransitionWindow_SetRange_ClampsToSwitchablePhasesAndPreservesFields()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateLightAttackAction();
            action.DurationFrames = 12;
            action.Phases = new[]
            {
                new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Startup.ToString(), StartFrame = 0, EndFrame = 2 },
                new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Active.ToString(), StartFrame = 3, EndFrame = 5 },
                new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Recovery.ToString(), StartFrame = 6, EndFrame = 8 },
                new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Landing.ToString(), StartFrame = 9, EndFrame = 11 },
            };
            action.CancelRules = new[]
            {
                new CharacterCancelRuleJsonDto
                {
                    StartFrame = 4,
                    EndFrame = 5,
                    RequestTypeMask = (int)CharacterActionRequestType.Dodge,
                    ActionSlotMask = (int)CharacterCancelInputMask.Dodge,
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            ITimelineItemAdapter adapter = snapshot.ItemsByTrack[TimelineTrackKind.TransitionWindow][0];

            adapter.SetRange(0, 11);

            Assert.AreEqual(3, action.CancelRules[0].StartFrame);
            Assert.AreEqual(11, action.CancelRules[0].EndFrame);
            Assert.AreEqual((int)CharacterActionRequestType.Dodge, action.CancelRules[0].RequestTypeMask);
            Assert.AreEqual((int)CharacterCancelInputMask.Dodge, action.CancelRules[0].ActionSlotMask);
        }

        [Test]
        public void TransitionWindow_SetRange_SnapsOutsideRangeToNearestSwitchablePhase()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateLightAttackAction();
            action.DurationFrames = 12;
            action.Phases = new[]
            {
                new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Startup.ToString(), StartFrame = 0, EndFrame = 2 },
                new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Active.ToString(), StartFrame = 3, EndFrame = 5 },
                new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Landing.ToString(), StartFrame = 6, EndFrame = 11 },
            };
            action.CancelRules = new[]
            {
                new CharacterCancelRuleJsonDto
                {
                    StartFrame = 3,
                    EndFrame = 4,
                    RequestTypeMask = (int)CharacterActionRequestType.BasicAttack,
                    ActionSlotMask = (int)CharacterCancelInputMask.AttackPrimary,
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            ITimelineItemAdapter adapter = snapshot.ItemsByTrack[TimelineTrackKind.TransitionWindow][0];

            adapter.SetRange(0, 1);

            Assert.AreEqual(3, action.CancelRules[0].StartFrame);
            Assert.AreEqual(4, action.CancelRules[0].EndFrame);
        }

        [Test]
        public void TimelineView_AddTransitionWindow_DefaultsToAnyRequestTypeAndSlot()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateLightAttackAction();
            action.CancelRules = Array.Empty<CharacterCancelRuleJsonDto>();
            var view = new CharacterActionTimelineView();
            view.SetHost(new CharacterActionTimelineHost
            {
                CommitStructuralChange = mutate => mutate?.Invoke(),
            });
            view.Bind(action);

            view.AddCharacterActionEventAtTrack(TimelineTrackKind.TransitionWindow, 6);

            Assert.AreEqual(1, action.CancelRules.Length);
            Assert.AreEqual((int)CharacterActionRequestType.Any, action.CancelRules[0].RequestTypeMask);
            Assert.AreEqual((int)CharacterCancelInputMask.Any, action.CancelRules[0].ActionSlotMask);
            Assert.AreEqual(6, action.CancelRules[0].StartFrame);
            Assert.AreEqual(8, action.CancelRules[0].EndFrame);
        }

        [Test]
        public void Selection_ParsePhaseId()
        {
            bool ok = CharacterActionTimelineSelection.TryParse("phase:1", out CharacterActionTimelineSelection selection);
            Assert.IsTrue(ok);
            Assert.AreEqual(TimelineTrackKind.Phases, selection.TrackKind);
            Assert.AreEqual(1, selection.Index);
        }

        [Test]
        public void TimelineIdentity_AllTrackKinds_RoundTripPrefix()
        {
            foreach (TimelineTrackKind kind in Enum.GetValues(typeof(TimelineTrackKind)))
            {
                string itemId = CharacterActionTimelineIdentity.ItemId(
                    kind,
                    new object(),
                    kind.ToString().ToLowerInvariant() + ".event");

                Assert.IsTrue(CharacterActionTimelineIdentity.TryParseTrackKind(itemId, out TimelineTrackKind parsed), itemId);
                Assert.AreEqual(kind, parsed, itemId);
                Assert.IsTrue(CharacterActionTimelineSelection.TryParse(itemId, out CharacterActionTimelineSelection selection), itemId);
                Assert.AreEqual(kind, selection.TrackKind, itemId);
            }
        }

        [Test]
        public void Selection_ParseGameplayTimelineItemId()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.GameplayTrack = new GameplayTrackJsonDto
            {
                Events = new[]
                {
                    new GameplayTrackEventJsonDto
                    {
                        Frame = 6,
                        Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                        RequestId = CharacterTestBulkIds38.Id790018,
                        StableEventId = CharacterTestBulkIds38.Id790060,
                    },
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            ITimelineItemAdapter adapter = snapshot.ItemsByTrack[TimelineTrackKind.Gameplay][0];

            Assert.IsTrue(CharacterActionTimelineSelection.TryParse(adapter.Id, out CharacterActionTimelineSelection selection), adapter.Id);
            Assert.AreEqual(TimelineTrackKind.Gameplay, selection.TrackKind);
            Assert.AreSame(adapter, snapshot.FindItem(adapter.Id));
        }

        [Test]
        public void ComputeSpanFrames_RespectsPlaybackSpeed()
        {
            int span = CharacterActionTimelineLayout.ComputeSpanFrames(60, 2f);
            Assert.AreEqual(30, span);
        }

        [Test]
        public void VfxTimelineResolver_ComputesTrimmedRetimedSpan()
        {
            int playable = CharacterActionVfxTimelineResolver.ComputePlayableSourceFrames(
                sourceFrames: 60,
                startOffsetFrames: 10,
                endOffsetFrames: 20);

            Assert.AreEqual(30, playable);
            Assert.AreEqual(
                15,
                CharacterActionVfxTimelineResolver.ComputeTimelineSpanFrames(playable, playbackRate: 2f));
        }

        [Test]
        public void VfxTimelineResolver_UsesCeilingAndClampsPlaybackRate()
        {
            Assert.AreEqual(16f, CharacterActionVfxEventConfig.MaxPlaybackRate);
            Assert.AreEqual(
                4,
                CharacterActionVfxTimelineResolver.ComputeTimelineSpanFrames(
                    playableSourceFrames: 7,
                    playbackRate: 2f));
            Assert.AreEqual(
                CharacterActionVfxEventConfig.MaxPlaybackRate,
                CharacterActionVfxTimelineResolver.ComputePlaybackRate(
                    playableSourceFrames: 60,
                    timelineSpanFrames: 1));
            Assert.AreEqual(
                CharacterActionVfxEventConfig.MinPlaybackRate,
                CharacterActionVfxTimelineResolver.ComputePlaybackRate(
                    playableSourceFrames: 1,
                    timelineSpanFrames: 9999));
        }

        [Test]
        public void VfxTimingValidation_UnknownLifetimeRejectsOnlyPlayToCompletion()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            model.Actions.Clear();
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.PresentationTrack = new PresentationTrackJsonDto
            {
                Events = new[]
                {
                    new PresentationTrackEventJsonDto
                    {
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = "vfx.missing:GameObject@mxframework.samples",
                        VfxPlaybackMode = CharacterActionVfxPlaybackMode.PlayToCompletion.ToString(),
                    },
                    new PresentationTrackEventJsonDto
                    {
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = "vfx.missing:GameObject@mxframework.samples",
                        VfxPlaybackMode = CharacterActionVfxPlaybackMode.StopAtEventEnd.ToString(),
                    },
                },
            };
            model.Actions.Add(action);
            var provider = new CharacterActionVfxTimingValidationProvider();

            CharacterActionValidationDiagnostic[] diagnostics = provider.Validate(
                new CharacterActionAuthoringLocalValidationContext(model, string.Empty, string.Empty));

            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual(
                CharacterActionVfxTimingValidationProvider.MissingFiniteLifetimeCode,
                diagnostics[0].Code);
        }

        [Test]
        public void VfxTimingValidation_RejectsTrimsThatConsumeEntireSource()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            model.Actions.Clear();
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.PresentationTrack = new PresentationTrackJsonDto
            {
                Events = new[]
                {
                    new PresentationTrackEventJsonDto
                    {
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = "vfx.magic.fireball.charge:GameObject@mxframework.samples",
                        VfxStartOffsetFrames = 6,
                        VfxEndOffsetFrames = 6,
                    },
                },
            };
            model.Actions.Add(action);
            var provider = new CharacterActionVfxTimingValidationProvider();

            CharacterActionValidationDiagnostic[] diagnostics = provider.Validate(
                new CharacterActionAuthoringLocalValidationContext(model, string.Empty, string.Empty));

            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual(
                CharacterActionVfxTimingValidationProvider.InvalidSourceTrimCode,
                diagnostics[0].Code);
        }

        [Test]
        public void Snapshot_FiniteVfxSource_DerivesSpanAndAllowsTailBeyondAction()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 6;
            action.PresentationTrack = new PresentationTrackJsonDto
            {
                Events = new[]
                {
                    new PresentationTrackEventJsonDto
                    {
                        Frame = 4,
                        EndFrame = 4,
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = "vfx.magic.fireball.charge:GameObject@mxframework.samples",
                        VfxPlaybackMode = CharacterActionVfxPlaybackMode.PlayToCompletion.ToString(),
                        VfxPlaybackRate = 2f,
                        VfxStartOffsetFrames = 2,
                        VfxEndOffsetFrames = 2,
                        StableEventId = CharacterTestBulkIds38.Id790061,
                    },
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 6);
            ITimelineItemAdapter adapter = snapshot.ItemsByTrack[TimelineTrackKind.Presentation][0];

            Assert.AreEqual(6, snapshot.ActionDurationFrames);
            Assert.Greater(snapshot.DurationFrames, snapshot.ActionDurationFrames);
            Assert.AreEqual(4, adapter.StartFrame);
            Assert.AreEqual(7, adapter.EndFrame);
        }

        [Test]
        public void AnimationClipAdapter_ResizeRetimesAndAltResizeTrims()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 200;
            action.AnimationTrack = new AnimationTrackJsonDto
            {
                Events = new[]
                {
                    new AnimationTrackEventJsonDto
                    {
                        Frame = 2,
                        Kind = CharacterActionTrackEventKind.PlayAnimation.ToString(),
                        AnimationClipResourceKey = "art.character.skeleton.animation.standing_jump",
                        PlaybackSpeed = 1f,
                        StableEventId = CharacterTestBulkIds38.Id790062,
                    },
                },
            };
            AnimationTrackEventJsonDto evt = action.AnimationTrack.Events[0];
            Assert.IsTrue(CharacterActionAnimationTimelineResolver.ApplyAutoSpan(
                evt,
                action,
                24,
                "char.iron_vanguard"));
            Assert.IsTrue(CharacterActionAnimationTimelineResolver.TryResolveSourceClipFrames(
                evt,
                "char.iron_vanguard",
                out int sourceFrames));
            int targetSpan = Math.Max(1, Math.Min(100, sourceFrames / 2));
            CharacterActionTimelineSnapshot snapshot =
                CharacterActionTimelineSnapshot.FromAction(action, 200);
            var adapter = (IAnimationTimelineClipAdapter)
                snapshot.ItemsByTrack[TimelineTrackKind.Animation][0];

            adapter.SetAnimationRange(
                startFrame: 2,
                endFrame: 2 + targetSpan - 1,
                trimSource: false,
                actionDurationFrames: 200,
                characterStableId: "char.iron_vanguard");

            Assert.AreEqual(
                CharacterActionAnimationTimelineResolver.ComputePlaybackSpeed(
                    sourceFrames,
                    targetSpan),
                evt.PlaybackSpeed,
                0.0001f);
            float retimedSpeed = evt.PlaybackSpeed;
            int trimmedEnd = Math.Max(evt.Frame, evt.EndFrame - 5);

            adapter.SetAnimationRange(
                startFrame: evt.Frame,
                endFrame: trimmedEnd,
                trimSource: true,
                actionDurationFrames: 200,
                characterStableId: "char.iron_vanguard");

            Assert.AreEqual(retimedSpeed, evt.PlaybackSpeed, 0.0001f);
            Assert.Greater(evt.AnimationEndOffsetFrames, 0);
        }

        [Test]
        public void VfxClipAdapter_ResizeRetimesAndAltResizeTrims()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 20;
            action.PresentationTrack = new PresentationTrackJsonDto
            {
                Events = new[]
                {
                    new PresentationTrackEventJsonDto
                    {
                        Frame = 0,
                        EndFrame = 0,
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = "vfx.magic.fireball.charge:GameObject@mxframework.samples",
                        VfxPlaybackMode = CharacterActionVfxPlaybackMode.PlayToCompletion.ToString(),
                        VfxPlaybackRate = 1f,
                        StableEventId = CharacterTestBulkIds38.Id790063,
                    },
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 20);
            var adapter = (IVfxTimelineClipAdapter)snapshot.ItemsByTrack[TimelineTrackKind.Presentation][0];

            adapter.SetVfxRange(0, 5, trimSource: false);
            Assert.AreEqual(2f, action.PresentationTrack.Events[0].VfxPlaybackRate, 0.0001f);
            Assert.AreEqual(5, action.PresentationTrack.Events[0].EndFrame);

            adapter.SetVfxRange(2, 5, trimSource: true);
            Assert.AreEqual(4, action.PresentationTrack.Events[0].VfxStartOffsetFrames);
            Assert.AreEqual(2, action.PresentationTrack.Events[0].Frame);
            Assert.AreEqual(5, action.PresentationTrack.Events[0].EndFrame);
        }

        [Test]
        public void VfxClipAdapter_LoopResizePreservesPlaybackRate()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 20;
            action.PresentationTrack = new PresentationTrackJsonDto
            {
                Events = new[]
                {
                    new PresentationTrackEventJsonDto
                    {
                        Frame = 0,
                        EndFrame = 5,
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = "vfx.magic.fireball.charge:GameObject@mxframework.samples",
                        VfxPlaybackMode = CharacterActionVfxPlaybackMode.LoopUntilEventEnd.ToString(),
                        VfxPlaybackRate = 1.5f,
                        StableEventId = CharacterTestBulkIds38.Id790064,
                    },
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 20);
            var adapter = (IVfxTimelineClipAdapter)snapshot.ItemsByTrack[TimelineTrackKind.Presentation][0];

            adapter.SetVfxRange(0, 12, trimSource: false);

            Assert.AreEqual(1.5f, action.PresentationTrack.Events[0].VfxPlaybackRate, 0.0001f);
            Assert.AreEqual(12, action.PresentationTrack.Events[0].EndFrame);
        }

        [Test]
        public void TimelineItemContextAction_FitsActionDurationToEventEnd()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 6;
            action.PresentationTrack = new PresentationTrackJsonDto
            {
                Events = new[]
                {
                    new PresentationTrackEventJsonDto
                    {
                        Frame = 4,
                        EndFrame = 11,
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = "vfx.unknown:GameObject@mxframework.samples",
                        VfxPlaybackMode = CharacterActionVfxPlaybackMode.StopAtEventEnd.ToString(),
                        StableEventId = CharacterTestBulkIds38.Id790065,
                    },
                },
            };
            int commitCount = 0;
            var view = new CharacterActionTimelineView();
            view.SetHost(new CharacterActionTimelineHost
            {
                CommitFieldChange = (mutate, _) =>
                {
                    commitCount++;
                    mutate?.Invoke();
                },
            });
            view.Bind(action);
            commitCount = 0;
            ITimelineItemAdapter adapter =
                view.Snapshot.ItemsByTrack[TimelineTrackKind.Presentation][0];

            view.FitActionDurationToItem(adapter);

            Assert.AreEqual(12, action.DurationFrames);
            Assert.AreEqual(12, view.Snapshot.ActionDurationFrames);
            Assert.AreEqual(1, commitCount);
        }

        [Test]
        public void TimelineItemContextAction_CanShrinkActionToEventEnd()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 20;
            action.GameplayTrack = new GameplayTrackJsonDto
            {
                Events = new[]
                {
                    new GameplayTrackEventJsonDto
                    {
                        Frame = 7,
                        Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                        RequestId = CharacterTestBulkIds38.Id790066,
                        StableEventId = CharacterTestBulkIds38.Id790067,
                    },
                },
            };
            var view = new CharacterActionTimelineView();
            view.SetHost(new CharacterActionTimelineHost
            {
                CommitFieldChange = (mutate, _) => mutate?.Invoke(),
            });
            view.Bind(action);
            ITimelineItemAdapter adapter =
                view.Snapshot.ItemsByTrack[TimelineTrackKind.Gameplay][0];

            view.FitActionDurationToItem(adapter);

            Assert.AreEqual(8, action.DurationFrames);
            Assert.AreEqual(8, view.Snapshot.ActionDurationFrames);
        }

        [Test]
        public void ComputeEndFrame_ClampsToDuration()
        {
            int end = CharacterActionTimelineLayout.ComputeEndFrame(0, 99, 24);
            Assert.AreEqual(23, end);
        }

        [Test]
        public void TransitionFramesFromSeconds_UsesEditorFps()
        {
            int frames = CharacterActionTimelineLayout.TransitionFramesFromSeconds(0.5f);
            Assert.AreEqual(30, frames);
        }

        [Test]
        public void Snapshot_AnimationEvents_UseClipAdapters()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.AnimationTrack.Events[0].EndFrame = 5;
            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            ITimelineItemAdapter adapter = snapshot.ItemsByTrack[TimelineTrackKind.Animation][0];
            Assert.AreEqual(TimelineItemKind.Clip, adapter.ItemKind);
            Assert.AreEqual(0, adapter.StartFrame);
            Assert.AreEqual(5, adapter.EndFrame);
        }

        [Test]
        public void Snapshot_PresentationVisualCueWithEndFrame_UsesClipAdapter()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 12;
            action.PresentationTrack = new PresentationTrackJsonDto
            {
                Events = new[]
                {
                    new PresentationTrackEventJsonDto
                    {
                        Frame = 0,
                        EndFrame = 5,
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = "vfx.magic.fireball.charge",
                        StableEventId = CharacterTestBulkIds38.Id790068,
                    },
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            ITimelineItemAdapter adapter = snapshot.ItemsByTrack[TimelineTrackKind.Presentation][0];

            Assert.AreEqual(TimelineItemKind.Clip, adapter.ItemKind);
            Assert.AreEqual(0, adapter.StartFrame);
            Assert.AreEqual(5, adapter.EndFrame);
            StringAssert.Contains("vfx.magic.fireball.charge", adapter.FullTitle);

            adapter.SetRange(1, 4);

            Assert.AreEqual(1, action.PresentationTrack.Events[0].Frame);
            Assert.AreEqual(4, action.PresentationTrack.Events[0].EndFrame);
        }

        [Test]
        public void Snapshot_PresentationAudioCue_RemainsMarkerEvenWithEndFrame()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 12;
            action.PresentationTrack = new PresentationTrackJsonDto
            {
                Events = new[]
                {
                    new PresentationTrackEventJsonDto
                    {
                        Frame = 2,
                        EndFrame = 5,
                        Kind = CharacterActionTrackEventKind.PlayAudioCue.ToString(),
                        CueId = CharacterTestBulkIds38.Id790069,
                        StableEventId = CharacterTestBulkIds38.Id790070,
                    },
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            ITimelineItemAdapter adapter = snapshot.ItemsByTrack[TimelineTrackKind.Presentation][0];

            Assert.AreEqual(TimelineItemKind.Marker, adapter.ItemKind);
            Assert.AreEqual(2, adapter.StartFrame);
            Assert.AreEqual(2, adapter.EndFrame);

            adapter.SetRange(4, 8);

            Assert.AreEqual(4, action.PresentationTrack.Events[0].Frame);
            Assert.AreEqual(4, action.PresentationTrack.Events[0].EndFrame);
            Assert.AreEqual(4, adapter.StartFrame);
            Assert.AreEqual(4, adapter.EndFrame);
        }

        [Test]
        public void Snapshot_GameplayProjectileRequest_AppearsAsTimelineMarker()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 12;
            action.GameplayTrack = new GameplayTrackJsonDto
            {
                Events = new[]
                {
                    new GameplayTrackEventJsonDto
                    {
                        Frame = 6,
                        Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                        RequestId = CharacterTestBulkIds38.Id790018,
                        StableEventId = CharacterTestBulkIds38.Id790060,
                    },
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            ITimelineItemAdapter adapter = snapshot.ItemsByTrack[TimelineTrackKind.Gameplay][0];

            Assert.AreEqual(TimelineItemKind.Marker, adapter.ItemKind);
            Assert.AreEqual(6, adapter.StartFrame);
            Assert.AreEqual(6, adapter.EndFrame);
            StringAssert.Contains("发射投射物", adapter.FullTitle);
            StringAssert.Contains("character_test.fireball.projectile", adapter.FullTitle);
            StringAssert.DoesNotContain("F6", adapter.FullTitle);
            StringAssert.DoesNotContain("Frame", adapter.FullTitle);
        }

        [Test]
        public void Snapshot_GameplayProjectileRequest_DoesNotInheritPresentationRangeAtSameIndex()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 12;
            action.PresentationTrack = new PresentationTrackJsonDto
            {
                Events = new[]
                {
                    new PresentationTrackEventJsonDto
                    {
                        Frame = 0,
                        EndFrame = 5,
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = "vfx.magic.fireball.charge",
                        StableEventId = CharacterTestBulkIds38.Id790071,
                    },
                },
            };
            action.GameplayTrack = new GameplayTrackJsonDto
            {
                Events = new[]
                {
                    new GameplayTrackEventJsonDto
                    {
                        Frame = 6,
                        Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                        RequestId = CharacterTestBulkIds38.Id790018,
                        StableEventId = CharacterTestBulkIds38.Id790060,
                    },
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            ITimelineItemAdapter presentation = snapshot.ItemsByTrack[TimelineTrackKind.Presentation][0];
            ITimelineItemAdapter gameplay = snapshot.ItemsByTrack[TimelineTrackKind.Gameplay][0];

            Assert.AreEqual(TimelineItemKind.Clip, presentation.ItemKind);
            Assert.AreEqual(0, presentation.StartFrame);
            Assert.AreEqual(5, presentation.EndFrame);
            Assert.AreEqual(TimelineItemKind.Marker, gameplay.ItemKind);
            Assert.AreEqual(6, gameplay.StartFrame);
            Assert.AreEqual(6, gameplay.EndFrame);

            gameplay.SetRange(7, 10);

            Assert.AreEqual(7, action.GameplayTrack.Events[0].Frame);
            Assert.AreEqual(7, gameplay.StartFrame);
            Assert.AreEqual(7, gameplay.EndFrame);
        }

        [Test]
        public void TimelineView_GameplayProjectileRequest_BuildsMarkerWithoutResizeHandles()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 12;
            action.PresentationTrack = new PresentationTrackJsonDto
            {
                Events = new[]
                {
                    new PresentationTrackEventJsonDto
                    {
                        Frame = 0,
                        EndFrame = 5,
                        Kind = CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                        ResourceKey = "vfx.magic.fireball.charge",
                        StableEventId = CharacterTestBulkIds38.Id790071,
                    },
                },
            };
            action.GameplayTrack = new GameplayTrackJsonDto
            {
                Events = new[]
                {
                    new GameplayTrackEventJsonDto
                    {
                        Frame = 6,
                        Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                        RequestId = CharacterTestBulkIds38.Id790018,
                        StableEventId = CharacterTestBulkIds38.Id790060,
                    },
                },
            };
            var view = new CharacterActionTimelineView();

            view.Bind(action);

            VisualElement marker = null;
            view.Query<VisualElement>(className: "ca-tl-marker").ForEach(
                element =>
                {
                    if (marker == null
                        && !string.IsNullOrEmpty(element.tooltip)
                        && element.tooltip.Contains("character_test.fireball.projectile"))
                    {
                        marker = element;
                    }
                });

            Assert.NotNull(marker);
            Assert.IsNull(marker.Q(className: "ca-tl-clip-handle"));
            Assert.IsNull(marker.Q(className: "ca-tl-clip-handle-left"));
            Assert.IsNull(marker.Q(className: "ca-tl-clip-handle-right"));
        }

        [Test]
        public void TimelineView_Toolbar_OmitsLegacySpecialButtons()
        {
            var view = new CharacterActionTimelineView();

            view.Bind(CharacterActionAuthoringModel.CreateJumpAction());

            Assert.NotNull(view.Q<Button>("timeline-save-button"));
            Assert.NotNull(view.Q<ToolbarMenu>("timeline-context-selector"));
            Assert.NotNull(view.Q<ToolbarMenu>("timeline-add-menu"));
            Assert.NotNull(view.Q<ToolbarMenu>("timeline-more-menu"));
            Assert.NotNull(view.Q(className: "ca-tl-context-bar"));
            Assert.IsNull(view.Q(className: "ca-tl-transport-bar"));
            Assert.NotNull(view.Q(className: "ca-tl-body-scroll"));
            Assert.IsNull(FindButtonByText(view, "Add RootMotion"));
            Assert.IsNull(FindButtonByText(view, "Set Control Scale"));
            Assert.IsNull(FindButtonByText(view, "Hit Window"));
        }

        [Test]
        public void TimelineView_ViewportControl_PrecedesScrollableTracksAndDoesNotShrink()
        {
            var view = new CharacterActionTimelineView();

            view.Bind(CharacterActionAuthoringModel.CreateJumpAction());

            VisualElement viewport = view.Q(className: "ca-tl-viewport");
            VisualElement bodyScroll = view.Q(className: "ca-tl-body-scroll");
            Assert.NotNull(viewport);
            Assert.NotNull(bodyScroll);
            Assert.Less(view.IndexOf(viewport), view.IndexOf(bodyScroll));
            Assert.AreEqual(0f, viewport.resolvedStyle.flexShrink, 0.01f);
        }

        [Test]
        public void TimelineView_PlayheadDragRemainsAvailableWhenPlaybackBindsAfterCanvas()
        {
            var view = new CharacterActionTimelineView();
            var controller = new CharacterActionTimelinePlaybackController();
            try
            {
                view.Bind(CharacterActionAuthoringModel.CreateJumpAction());

                Assert.IsTrue(
                    view.HasPlayheadDragManipulatorForTests(),
                    "The visible playhead must remain draggable before playback is assigned.");
                VisualElement playhead = view.Q(className: "ca-tl-playhead");
                Assert.NotNull(playhead);
                float initialLeft = playhead.style.left.value.value;

                view.SetPlaybackController(controller);
                controller.SetDuration(view.Snapshot.DurationFrames);
                controller.Seek(3);

                Assert.AreEqual(3, controller.CurrentFrame);
                Assert.AreEqual(3, view.PlaybackController.CurrentFrame);
                Assert.Greater(
                    playhead.style.left.value.value,
                    initialLeft,
                    "Seeking through the late-bound controller must redraw the playhead.");
            }
            finally
            {
                view.SetPlaybackController(null);
                controller.Dispose();
            }
        }

        [Test]
        public void TimelineView_ContextSelector_ReflectsHostLabel()
        {
            var view = new CharacterActionTimelineView();
            view.SetHost(new CharacterActionTimelineHost
            {
                CurrentItemLabelProvider = () => "\u52a8\u4f5c: jump",
                CurrentItemIdProvider = () => "jump",
                PickerItemsProvider = () => Array.Empty<CharacterActionTimelinePickerItem>(),
                PickerItemSelected = _ => { },
            });

            view.Bind(CharacterActionAuthoringModel.CreateJumpAction());

            ToolbarMenu selector = view.Q<ToolbarMenu>("timeline-context-selector");
            Assert.NotNull(selector);
            Assert.AreEqual("\u52a8\u4f5c: jump", selector.text);
        }

        [Test]
        public void TimelineView_ReadOnlyBanner_LivesAboveBodyNotInsideCanvas()
        {
            var action = CharacterActionAuthoringModel.CreateJumpAction();
            action.TimelineAuthority = CharacterActionTimelineAuthority.CombatAnchored.ToString();
            action.DurationFrames = 24;

            var view = new CharacterActionTimelineView();
            view.Bind(action);

            VisualElement bannerHost = view.Q(name: "timeline-readonly-banner-host");
            Assert.NotNull(bannerHost);
            Assert.IsNotNull(bannerHost.Q<HelpBox>());
            Assert.IsNull(view.Q(className: "ca-tl-canvas-root")?.Q<HelpBox>());
        }

        [Test]
        public void TimelineView_TrackRows_ApplyAlternatingStripeClass()
        {
            var view = new CharacterActionTimelineView();
            view.Bind(CharacterActionAuthoringModel.CreateJumpAction());

            int evenRows = view.Query(className: "ca-tl-row-even").ToList().Count;
            Assert.Greater(evenRows, 0);
        }

        [Test]
        public void TimelineView_ItemPicker_SearchSelectsAndRecordsRecent()
        {
            string selected = string.Empty;
            string recent = string.Empty;
            var view = new CharacterActionTimelineView();
            view.SetHost(new CharacterActionTimelineHost
            {
                CurrentItemLabelProvider = () => "\u52a8\u4f5c: jump",
                CurrentItemIdProvider = () => "jump",
                PickerItemsProvider = () => new[]
                {
                    new CharacterActionTimelinePickerItem(
                        "jump",
                        "jump - Basic Jump",
                        "Movement | movement, jump",
                        "jump basic movement",
                        false,
                        true),
                    new CharacterActionTimelinePickerItem(
                        "fireball",
                        "fireball - Fireball Explosion",
                        "Combat | fire",
                        "fireball explosion projectile",
                        false,
                        false),
                },
                PickerItemSelected = id => selected = id,
                PickerRecentRecorded = id => recent = id,
            });

            view.Bind(CharacterActionAuthoringModel.CreateJumpAction());

            ToolbarMenu selector = view.Q<ToolbarMenu>("timeline-context-selector");
            Assert.NotNull(selector);
            Assert.AreEqual("\u52a8\u4f5c: jump", selector.text);

            view.OpenItemPickerForTests("fireball");

            CollectionAssert.AreEqual(new[] { "fireball" }, view.VisiblePickerItemIdsForTests());

            view.SelectPickerItemForTests("fireball");

            Assert.AreEqual("fireball", selected);
            Assert.AreEqual("fireball", recent);
        }

        [Test]
        public void TimelineView_ItemPicker_CanCloseFromCloseButtonAndHelper()
        {
            var view = new CharacterActionTimelineView();
            view.SetHost(new CharacterActionTimelineHost
            {
                CurrentItemLabelProvider = () => "\u52a8\u4f5c: jump",
                CurrentItemIdProvider = () => "jump",
                PickerItemsProvider = () => new[]
                {
                    new CharacterActionTimelinePickerItem(
                        "jump",
                        "jump - Basic Jump",
                        "Movement | movement, jump",
                        "jump basic movement",
                        false,
                        true),
                },
                PickerItemSelected = _ => { },
            });
            view.Bind(CharacterActionAuthoringModel.CreateJumpAction());

            view.OpenItemPickerForTests("jump");
            Assert.IsTrue(view.IsItemPickerOpenForTests());

            Button closeButton = view.Q<Button>("timeline-current-item-picker-close");
            Assert.NotNull(closeButton);
            PressButton(closeButton);
            Assert.IsFalse(view.IsItemPickerOpenForTests());

            view.OpenItemPickerForTests();
            view.CloseItemPickerForTests();
            Assert.IsFalse(view.IsItemPickerOpenForTests());
        }

        [Test]
        public void TimelineView_ItemPicker_TogglesFavoriteThroughHost()
        {
            bool favorite = false;
            string toggledId = string.Empty;
            var view = new CharacterActionTimelineView();
            view.SetHost(new CharacterActionTimelineHost
            {
                CurrentItemLabelProvider = () => "\u65f6\u95f4\u8f74: afterblast",
                CurrentItemIdProvider = () => "afterblast",
                PickerItemsProvider = () => new[]
                {
                    new CharacterActionTimelinePickerItem(
                        "afterblast",
                        "afterblast",
                        "121f | 2 events",
                        "afterblast fireball second explosion",
                        favorite,
                        false),
                },
                PickerItemSelected = _ => { },
                PickerFavoriteChanged = (id, value) =>
                {
                    toggledId = id;
                    favorite = value;
                },
            });

            view.BindAbilityEffectTimeline(new AbilityEffectTimelineDraft
            {
                TimelineId = "afterblast",
                DurationFrames = 121,
            });
            view.OpenItemPickerForTests();
            view.TogglePickerFavoriteForTests("afterblast");

            Assert.AreEqual("afterblast", toggledId);
            Assert.IsTrue(favorite);
        }

        [Test]
        public void TimelineView_AbilityPicker_UsesHostProvidedTimelineItemsOnly()
        {
            var view = new CharacterActionTimelineView();
            view.SetHost(new CharacterActionTimelineHost
            {
                CurrentItemLabelProvider = () => "\u65f6\u95f4\u8f74: character_test.fireball.afterblast",
                CurrentItemIdProvider = () => "character_test.fireball.afterblast",
                PickerItemsProvider = () => new[]
                {
                    new CharacterActionTimelinePickerItem(
                        "character_test.fireball.afterblast",
                        "character_test.fireball.afterblast",
                        "121f | 2 events",
                        "character_test.fireball.afterblast afterblast.vfx AreaPulse",
                        false,
                        false),
                },
                PickerItemSelected = _ => { },
            });

            view.BindAbilityEffectTimeline(new AbilityEffectTimelineDraft
            {
                TimelineId = "character_test.fireball.afterblast",
                DurationFrames = 121,
            });

            view.OpenItemPickerForTests("AreaPulse");

            CollectionAssert.AreEqual(
                new[] { "character_test.fireball.afterblast" },
                view.VisiblePickerItemIdsForTests());
            CollectionAssert.DoesNotContain(view.VisiblePickerItemIdsForTests(), "jump");
        }

        [Test]
        public void TimelineView_AbilityAddMenu_UsesHostProvidedEventTypes()
        {
            var view = new CharacterActionTimelineView();
            var timeline = new AbilityEffectTimelineDraft
            {
                TimelineId = "test.timeline",
                DurationFrames = 121,
            };
            view.SetHost(new CharacterActionTimelineHost
            {
                SaveRequested = () => { },
                CanSaveProvider = () => true,
                DirtyStateProvider = () => "Modified",
                AddMenuProvider = (menu, frame) =>
                {
                    menu.AppendAction("Combat/AreaPulse", _ => { }, DropdownMenuAction.AlwaysEnabled);
                    menu.AppendAction("Gameplay/StartEffectTimeline", _ => { }, DropdownMenuAction.AlwaysEnabled);
                    menu.AppendAction("Presentation/SpawnVfx", _ => { }, DropdownMenuAction.AlwaysEnabled);
                },
            });

            view.BindAbilityEffectTimeline(timeline);

            ToolbarMenu addMenu = view.Q<ToolbarMenu>("timeline-add-menu");
            string[] actionNames = addMenu.menu.MenuItems()
                .OfType<DropdownMenuAction>()
                .Select(item => item.name)
                .ToArray();
            CollectionAssert.Contains(actionNames, "Combat/AreaPulse");
            CollectionAssert.Contains(actionNames, "Gameplay/StartEffectTimeline");
            CollectionAssert.Contains(actionNames, "Presentation/SpawnVfx");
            CollectionAssert.DoesNotContain(actionNames, "Character Action/Motion");
            CollectionAssert.DoesNotContain(actionNames, "Character Action/Animation");
            CollectionAssert.DoesNotContain(actionNames, "Add RootMotion");
        }

        [Test]
        public void Snapshot_CombatHitTraceWindow_UsesSingleWindowClip()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.CombatTrack = new CombatTrackJsonDto
            {
                Events = new[]
                {
                    new CombatTrackEventJsonDto
                    {
                        Frame = 3,
                        EndFrame = 6,
                        Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                        EquipmentSlotId = CharacterTestBulkIds38.Id790007,
                        StableEventId = CharacterTestBulkIds38.Id790055,
                    },
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            ITimelineItemAdapter adapter = snapshot.ItemsByTrack[TimelineTrackKind.Combat][0];

            Assert.AreEqual(1, snapshot.ItemsByTrack[TimelineTrackKind.Combat].Count);
            Assert.IsInstanceOf<HitTraceWindowTimelineAdapter>(adapter);
            Assert.AreEqual(TimelineItemKind.Clip, adapter.ItemKind);
            Assert.AreEqual(3, adapter.StartFrame);
            Assert.AreEqual(6, adapter.EndFrame);
            StringAssert.Contains("武器轨迹判定", adapter.Title);
            StringAssert.Contains("装备槽位=mainHand", adapter.FullTitle);
        }

        [Test]
        public void HitTraceWindow_SetRange_UpdatesSingleEvent()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.CombatTrack = new CombatTrackJsonDto
            {
                Events = new[]
                {
                    new CombatTrackEventJsonDto
                    {
                        Frame = 3,
                        EndFrame = 6,
                        Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                        EquipmentSlotId = CharacterTestBulkIds38.Id790007,
                        StableEventId = CharacterTestBulkIds38.Id790055,
                    },
                },
            };
            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            ITimelineItemAdapter adapter = snapshot.ItemsByTrack[TimelineTrackKind.Combat][0];

            adapter.SetRange(4, 7);

            Assert.AreEqual(4, action.CombatTrack.Events[0].Frame);
            Assert.AreEqual(7, action.CombatTrack.Events[0].EndFrame);
            Assert.AreEqual(CharacterActionTrackEventKind.HitTraceWindow.ToString(), action.CombatTrack.Events[0].Kind);
            Assert.AreEqual("mainHand", action.CombatTrack.Events[0].EquipmentSlotId);
            Assert.AreEqual(1, action.CombatTrack.Events.Length);
        }

        [Test]
        public void Snapshot_CombatHitTraceProfile_UsesWindowClip()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.CombatTrack = new CombatTrackJsonDto
            {
                Events = new[]
                {
                    new CombatTrackEventJsonDto
                    {
                        Frame = 3,
                        EndFrame = 6,
                        Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                        TraceProfileId = CharacterTestBulkIds38.Id790056,
                        StableEventId = CharacterTestBulkIds38.Id790055,
                    },
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);

            Assert.AreEqual(1, snapshot.ItemsByTrack[TimelineTrackKind.Combat].Count);
            Assert.IsInstanceOf<HitTraceWindowTimelineAdapter>(snapshot.ItemsByTrack[TimelineTrackKind.Combat][0]);
        }

        [Test]
        public void Snapshot_CombatHitTraceInvalidRange_ReportsWarning()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.CombatTrack = new CombatTrackJsonDto
            {
                Events = new[]
                {
                    new CombatTrackEventJsonDto
                    {
                        Frame = 6,
                        EndFrame = 3,
                        Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                        EquipmentSlotId = CharacterTestBulkIds38.Id790007,
                        StableEventId = CharacterTestBulkIds38.Id790055,
                    },
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);

            Assert.AreEqual(1, snapshot.ItemsByTrack[TimelineTrackKind.Combat].Count);
            Assert.AreEqual(TimelineItemKind.Clip, snapshot.ItemsByTrack[TimelineTrackKind.Combat][0].ItemKind);
            Assert.AreEqual(1, HitTraceWindowTimelineAdapter.BuildWindowWarnings(action.CombatTrack.Events).Length);
        }

        [Test]
        public void Snapshot_RootMotionWithoutSamples_UsesSourceAnimationRange()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 16;
            action.AnimationTrack.Events = new[]
            {
                new AnimationTrackEventJsonDto
                {
                    Frame = 4,
                    EndFrame = 11,
                    Kind = CharacterActionTrackEventKind.CrossFadeAnimation.ToString(),
                    AnimationClipResourceKey = "clip.root_motion",
                    StableEventId = CharacterTestBulkIds38.Id790057,
                },
            };
            action.MotionTrack.UsesRootMotion = true;
            action.MotionTrack.RootMotion = new RootMotionTrackJsonDto
            {
                SourceAnimationEventId = CharacterTestBulkIds38.Id790057,
                SourceAnimationClipResourceKey = "clip.root_motion",
                Space = CharacterActionRootMotionSpace.Local.ToString(),
                SampleTickRate = 60,
                Frames = Array.Empty<RootMotionFrameJsonDto>(),
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            ITimelineItemAdapter adapter = snapshot.FindItem("motion:rootMotion");

            Assert.NotNull(adapter);
            Assert.AreEqual(TimelineItemKind.Clip, adapter.ItemKind);
            Assert.AreEqual(4, adapter.StartFrame);
            Assert.AreEqual(11, adapter.EndFrame);
        }

        [Test]
        public void SampleRootMotionY_DisplaysAsEditableMotionRange()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.DurationFrames = 112;
            action.MotionTrack.Events = new[]
            {
                new MotionTrackEventJsonDto
                {
                    Frame = 0,
                    EndFrame = 111,
                    Kind = CharacterActionTrackEventKind.SampleRootMotionY.ToString(),
                    StableEventId = CharacterTestBulkIds38.Id790058,
                },
            };

            var adapter = new TrackEventTimelineAdapter(
                action, TimelineTrackKind.Motion, 0, UnityEngine.Color.white, canEdit: true);

            Assert.AreEqual(TimelineItemKind.Clip, adapter.ItemKind);
            Assert.AreEqual(0, adapter.StartFrame);
            Assert.AreEqual(111, adapter.EndFrame);
            adapter.SetRange(5, 80);
            Assert.AreEqual(5, action.MotionTrack.Events[0].Frame);
            Assert.AreEqual(80, action.MotionTrack.Events[0].EndFrame);
            action.MotionTrack.Events[0].Kind = CharacterActionTrackEventKind.ApplyImpulse.ToString();
            Assert.AreEqual(TimelineItemKind.Marker, adapter.ItemKind);
            Assert.AreEqual(5, adapter.EndFrame);
        }

        [Test]
        public void LaneLayout_SameFrameMarkers_UseDifferentLanes()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.MotionTrack.Events = new[]
            {
                new MotionTrackEventJsonDto
                {
                    Frame = 3,
                    Kind = CharacterActionTrackEventKind.ApplyImpulse.ToString(),
                    MovementMode = CharacterMovementMode.Airborne.ToString(),
                    StableEventId = CharacterTestBulkIds38.Id790058,
                },
                new MotionTrackEventJsonDto
                {
                    Frame = 3,
                    Kind = CharacterActionTrackEventKind.SetMovementMode.ToString(),
                    MovementMode = CharacterMovementMode.Airborne.ToString(),
                    StableEventId = CharacterTestBulkIds38.Id790059,
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            CharacterActionTimelineTrackLayout layout = CharacterActionTimelineLaneLayout.Build(
                TimelineTrackKind.Motion,
                snapshot.ItemsByTrack[TimelineTrackKind.Motion],
                CharacterActionTimelineLayout.DefaultPixelsPerFrame);

            Assert.AreEqual(2, layout.LaneCount);
            Assert.IsTrue(layout.TryGetPlacement(snapshot.ItemsByTrack[TimelineTrackKind.Motion][0].Id, out CharacterActionTimelineItemPlacement first));
            Assert.IsTrue(layout.TryGetPlacement(snapshot.ItemsByTrack[TimelineTrackKind.Motion][1].Id, out CharacterActionTimelineItemPlacement second));
            Assert.AreNotEqual(first.Lane, second.Lane);
        }

        [Test]
        public void LaneLayout_NonOverlappingMarkers_ReusesLane()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();
            action.MotionTrack.Events = new[]
            {
                new MotionTrackEventJsonDto
                {
                    Frame = 0,
                    Kind = CharacterActionTrackEventKind.ApplyImpulse.ToString(),
                    MovementMode = CharacterMovementMode.Airborne.ToString(),
                    StableEventId = CharacterTestBulkIds38.Id790058,
                },
                new MotionTrackEventJsonDto
                {
                    Frame = 5,
                    Kind = CharacterActionTrackEventKind.SetMovementMode.ToString(),
                    MovementMode = CharacterMovementMode.Airborne.ToString(),
                    StableEventId = CharacterTestBulkIds38.Id790059,
                },
            };

            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);
            CharacterActionTimelineTrackLayout layout = CharacterActionTimelineLaneLayout.Build(
                TimelineTrackKind.Motion,
                snapshot.ItemsByTrack[TimelineTrackKind.Motion],
                CharacterActionTimelineLayout.DefaultPixelsPerFrame);

            Assert.AreEqual(1, layout.LaneCount);
        }

        [Test]
        public void PhaseTimelineUtility_NormalizesPhasesToCoverDurationWithoutOverlap()
        {
            var action = new CharacterActionJsonDto
            {
                DurationFrames = 8,
                Phases = new[]
                {
                    new CharacterActionPhaseJsonDto
                    {
                        Kind = CharacterActionPhaseKind.Startup.ToString(),
                        StartFrame = 0,
                        EndFrame = 4,
                    },
                    new CharacterActionPhaseJsonDto
                    {
                        Kind = CharacterActionPhaseKind.Active.ToString(),
                        StartFrame = 3,
                        EndFrame = 5,
                    },
                    new CharacterActionPhaseJsonDto
                    {
                        Kind = CharacterActionPhaseKind.Recovery.ToString(),
                        StartFrame = 7,
                        EndFrame = 7,
                    },
                },
            };

            CharacterActionPhaseTimelineUtility.NormalizePhases(action, 8);

            Assert.AreEqual(3, action.Phases.Length);
            Assert.AreEqual(0, action.Phases[0].StartFrame);
            Assert.AreEqual(2, action.Phases[0].EndFrame);
            Assert.AreEqual(3, action.Phases[1].StartFrame);
            Assert.AreEqual(6, action.Phases[1].EndFrame);
            Assert.AreEqual(7, action.Phases[2].StartFrame);
            Assert.AreEqual(7, action.Phases[2].EndFrame);
        }

        [Test]
        public void PhaseTimelineUtility_InsertPhase_SplitsAndKeepsFullCoverage()
        {
            CharacterActionJsonDto action = CharacterActionAuthoringModel.CreateJumpAction();

            CharacterActionPhaseTimelineUtility.InsertPhaseAt(
                action,
                3,
                action.DurationFrames.Value,
                CharacterActionPhaseKind.Active.ToString());

            Assert.AreEqual(4, action.Phases.Length);
            Assert.AreEqual(0, action.Phases[0].StartFrame);
            Assert.AreEqual(1, action.Phases[0].EndFrame);
            Assert.AreEqual(2, action.Phases[1].StartFrame);
            Assert.AreEqual(2, action.Phases[1].EndFrame);
            Assert.AreEqual(3, action.Phases[2].StartFrame);
            Assert.AreEqual(4, action.Phases[2].EndFrame);
            Assert.AreEqual(5, action.Phases[3].StartFrame);
            Assert.AreEqual(7, action.Phases[3].EndFrame);
        }

        [Test]
        public void LaneLayout_PhaseClips_AlwaysUseSingleLane()
        {
            var action = new CharacterActionJsonDto
            {
                DurationFrames = 8,
                Phases = new[]
                {
                    new CharacterActionPhaseJsonDto { StartFrame = 0, EndFrame = 5 },
                    new CharacterActionPhaseJsonDto { StartFrame = 2, EndFrame = 7 },
                },
            };
            CharacterActionTimelineSnapshot snapshot = CharacterActionTimelineSnapshot.FromAction(action, 8);

            CharacterActionTimelineTrackLayout layout = CharacterActionTimelineLaneLayout.Build(
                TimelineTrackKind.Phases,
                snapshot.ItemsByTrack[TimelineTrackKind.Phases],
                CharacterActionTimelineLayout.DefaultPixelsPerFrame);

            Assert.AreEqual(1, layout.LaneCount);
            Assert.IsTrue(layout.TryGetPlacement(snapshot.ItemsByTrack[TimelineTrackKind.Phases][0].Id, out CharacterActionTimelineItemPlacement first));
            Assert.IsTrue(layout.TryGetPlacement(snapshot.ItemsByTrack[TimelineTrackKind.Phases][1].Id, out CharacterActionTimelineItemPlacement second));
            Assert.AreEqual(0, first.Lane);
            Assert.AreEqual(0, second.Lane);
        }

        private static Button FindButtonByText(VisualElement root, string text)
        {
            Button result = null;
            root.Query<Button>().ForEach(button =>
            {
                if (result == null && string.Equals(button.text, text, StringComparison.Ordinal))
                    result = button;
            });
            return result;
        }

        private static void PressButton(Button button)
        {
            Assert.NotNull(button);
            object clickable = typeof(Button)
                .GetProperty("clickable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(button);
            Assert.NotNull(clickable);

            MethodInfo[] methods = clickable.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo invoke = null;
            for (int i = 0; i < methods.Length; i++)
            {
                if (!string.Equals(methods[i].Name, "Invoke", StringComparison.Ordinal))
                    continue;
                ParameterInfo[] parameters = methods[i].GetParameters();
                if (parameters.Length == 0
                    || (parameters.Length == 1 && typeof(EventBase).IsAssignableFrom(parameters[0].ParameterType)))
                {
                    invoke = methods[i];
                    break;
                }
            }

            Assert.NotNull(invoke);
            if (invoke.GetParameters().Length == 0)
            {
                invoke.Invoke(clickable, Array.Empty<object>());
                return;
            }

            using (ClickEvent click = ClickEvent.GetPooled())
                invoke.Invoke(clickable, new object[] { click });
        }
    }
}
