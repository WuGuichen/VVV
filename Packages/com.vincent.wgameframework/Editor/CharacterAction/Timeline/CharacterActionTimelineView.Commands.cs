using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline.Adapters;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline
{
    internal sealed partial class CharacterActionTimelineView
    {
        private void PopulateDefaultAddMenu(DropdownMenu menu, int frame)
        {
            AppendDefaultAddAction(menu, "Character Action/Phase", TimelineTrackKind.Phases, frame);
            AppendDefaultAddAction(menu, "Character Action/Transition Window", TimelineTrackKind.TransitionWindow, frame);
            AppendDefaultAddAction(menu, "Character Action/Motion", TimelineTrackKind.Motion, frame);
            AppendDefaultAddAction(menu, "移动控制倍率标记", TimelineTrackKind.MovementControl, frame);
            AppendDefaultAddAction(menu, "旋转控制倍率标记", TimelineTrackKind.RotationControl, frame);
            AppendDefaultAddAction(menu, "Character Action/Animation", TimelineTrackKind.Animation, frame);
            AppendDefaultAddAction(menu, "Character Action/Combat", TimelineTrackKind.Combat, frame);
            AppendDefaultAddAction(menu, "Character Action/Gameplay", TimelineTrackKind.Gameplay, frame);
            AppendDefaultAddAction(menu, "Character Action/Presentation", TimelineTrackKind.Presentation, frame);
        }

        private void AppendDefaultAddAction(DropdownMenu menu, string label, TimelineTrackKind kind, int frame)
        {
            menu.AppendAction(
                label,
                _ => AddEventAtTrack(kind, frame),
                _ => _snapshot != null && !_snapshot.ReadOnly
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
        }

        private int CurrentTimelineFrame()
        {
            int duration = _snapshot?.ActionDurationFrames ?? Math.Max(1, _host?.DefaultDurationFrames ?? 8);
            int frame = _playbackController?.CurrentFrame ?? _state.PlayheadFrame;
            return CharacterActionTimelineLayout.ClampFrame(frame, duration);
        }

        private void OnRowDoubleClick(TimelineTrackKind kind, MouseDownEvent evt)
        {
            if (_snapshot == null || _snapshot.ReadOnly)
                return;

            _focusedTrack = kind;
            int frame = CharacterActionTimelineLayout.ClampFrame(
                PixelToFrame(CanvasWorldToLocalX(evt.mousePosition.x)),
                _snapshot.ActionDurationFrames);
            AddEventAtTrack(kind, frame);
        }

        private void AddPhase()
        {
            if (_snapshot?.Action == null || _snapshot.ReadOnly)
                return;

            CharacterActionJsonDto action = _snapshot.Action;
            _host?.CommitStructural(() =>
            {
                CharacterActionPhaseTimelineUtility.InsertPhaseAt(
                    action,
                    _state.PlayheadFrame,
                    _snapshot.ActionDurationFrames,
                    CharacterActionPhaseKind.Startup.ToString());
            });
        }

        private void AddEventToFocusedTrack()
        {
            if (_snapshot == null)
                return;
            AddEventAtTrack(_focusedTrack, _state.PlayheadFrame);
        }

        private void AddRootMotionAtPlayhead()
        {
            if (_snapshot == null)
                return;

            AddRootMotionAtFrame(_state.PlayheadFrame);
        }

        private void AddControlScaleAtPlayhead()
        {
            if (_snapshot == null)
                return;

            AddControlScaleAtFrame(_state.PlayheadFrame, reset: false);
        }

        private void AddHitTraceWindowAtPlayhead()
        {
            if (_snapshot == null)
                return;

            AddHitTraceWindowAtFrame(_state.PlayheadFrame);
        }

        private void AddTransitionWindowAtPlayhead()
        {
            if (_snapshot == null)
                return;

            AddTransitionWindowAtFrame(_state.PlayheadFrame);
        }

        private void AddRootMotionAtFrame(int frame)
        {
            if (_snapshot?.Action == null || _snapshot.ReadOnly)
                return;

            CharacterActionJsonDto action = _snapshot.Action;
            frame = CharacterActionTimelineLayout.ClampFrame(frame, _snapshot.ActionDurationFrames);

            _host?.CommitStructural(() =>
            {
                action.MotionTrack = action.MotionTrack ?? new MotionTrackJsonDto();
                action.MotionTrack.UsesRootMotion = true;
                action.MotionTrack.RootMotion = action.MotionTrack.RootMotion
                    ?? CharacterActionAuthoringModel.CreateRootMotionDraftFromAnimation(action, frame);
            });
            SelectItem(RootMotionTimelineAdapter.RootMotionItemId);
        }

        private void AddControlScaleAtFrame(int frame, bool reset)
        {
            if (_snapshot?.Action == null || _snapshot.ReadOnly)
                return;

            CharacterActionJsonDto action = _snapshot.Action;
            frame = CharacterActionTimelineLayout.ClampFrame(frame, _snapshot.ActionDurationFrames);

            _host?.CommitStructural(() =>
            {
                action.MotionTrack = action.MotionTrack ?? new MotionTrackJsonDto();
                var events = new List<MotionTrackEventJsonDto>(action.MotionTrack.Events ?? Array.Empty<MotionTrackEventJsonDto>());
                events.Add(new MotionTrackEventJsonDto
                {
                    Frame = frame,
                    EndFrame = Math.Max(frame, Math.Min(frame + 3, Math.Max(0, _snapshot.ActionDurationFrames - 1))),
                    Kind = CharacterActionTrackEventKind.SetControlScale.ToString(),
                    MovementControlScale = reset ? 1f : 0f,
                    RotationControlScale = reset ? 1f : 0f,
                    StableEventId = CharacterActionAuthoringIds.Resolve(
                        CharacterActionTimelineIdentity.MakeStableEventId(action, "control_scale"), 401),
                });
                action.MotionTrack.Events = events.ToArray();
                CharacterActionEventIdentityAuthoring.AssignMissingEventOrders(action);
            });
        }

        private void AddHitTraceWindowAtFrame(int frame)
        {
            if (_snapshot?.Action == null || _snapshot.ReadOnly)
                return;

            CharacterActionJsonDto action = _snapshot.Action;
            frame = CharacterActionTimelineLayout.ClampFrame(frame, _snapshot.ActionDurationFrames);

            _host?.CommitStructural(() =>
            {
                action.CombatTrack = action.CombatTrack ?? new CombatTrackJsonDto();
                var combat = new List<CombatTrackEventJsonDto>(action.CombatTrack.Events ?? Array.Empty<CombatTrackEventJsonDto>());
                int endFrame = Math.Max(frame, Math.Min(frame + 3, Math.Max(0, _snapshot.ActionDurationFrames - 1)));
                int equipmentSlotId = ResolveDefaultHitTraceEquipmentSlotId(action);
                int stableEventId = CharacterActionAuthoringIds.Resolve(
                    CharacterActionTimelineIdentity.MakeStableEventId(action, "hit_window"), 401);
                var window = new CombatTrackEventJsonDto
                {
                    Frame = frame,
                    EndFrame = endFrame,
                    Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                    EquipmentSlotId = equipmentSlotId,
                    StableEventId = stableEventId,
                    HitEvent = new CharacterActionHitEventJsonDto
                    {
                        TargetTypes = new[] { "HostileCharacter" },
                        EnvironmentHitPolicy = "None",
                        StableEventId = stableEventId,
                        MaxAcceptedHits = 1,
                        HitSameTargetPolicy = "AllowRepeat",
                        Damage = new CharacterActionHitDamageJsonDto
                        {
                            DamageTypeId = CharacterActionAuthoringIds.Resolve(CharacterActionDamageAuthoringText.DefaultDamageTypeId, 409),
                            FormulaId = CharacterActionAuthoringIds.Resolve(CharacterActionDamageAuthoringText.DefaultDamageFormulaId, 410),
                            DamageTypeLabel = CharacterActionDamageAuthoringText.DefaultDamageTypeId,
                            FormulaLabel = CharacterActionDamageAuthoringText.DefaultDamageFormulaId,
                            BaseDamage = CharacterActionDamageAuthoringText.BaselineLightAttackBaseDamage,
                            SourceAttributeScales = CharacterActionHitEventAuthoringModel.BuildAttackScaleArray(
                                CharacterActionDamageAuthoringText.BaselineLightAttackAttackScale),
                            ImpactPower = CharacterActionDamageAuthoringText.BaselineLightAttackImpactPower,
                            Tags = CharacterActionAuthoringIds.ResolveMany(new[] { "melee", "weapon", "light", "scatter" }, 410),
                            TagLabels = new[] { "melee", "weapon", "light", "scatter" },
                            AnalysisCategory = CharacterActionAuthoringIds.Resolve("BasicAttack", 411),
                            AnalysisCategoryLabel = "BasicAttack",
                        },
                    },
                };
                CharacterActionBladeTrailAuthoringProjection.Enable(action, window);
                window.StableEventOrder = CharacterContentIdentityAuthoringCatalog.ReadEventOrders()[stableEventId];
                combat.Add(window);
                action.CombatTrack.Events = combat.ToArray();
            });
        }

        private void AddTransitionWindowAtFrame(int frame)
        {
            if (_snapshot?.Action == null || _snapshot.ReadOnly)
                return;

            CharacterActionJsonDto action = _snapshot.Action;
            frame = CharacterActionTimelineLayout.ClampFrame(frame, _snapshot.ActionDurationFrames);

            _host?.CommitStructural(() =>
            {
                var rules = new List<CharacterCancelRuleJsonDto>(action.CancelRules ?? Array.Empty<CharacterCancelRuleJsonDto>());
                rules.Add(TransitionWindowTimelineAdapter.CreateDefaultRule(
                    action,
                    frame,
                    _snapshot.ActionDurationFrames));
                action.CancelRules = rules.ToArray();
            });
        }

        private static int ResolveDefaultHitTraceEquipmentSlotId(CharacterActionJsonDto action)
        {
            CombatTrackEventJsonDto[] events = action?.CombatTrack?.Events ?? Array.Empty<CombatTrackEventJsonDto>();
            for (int i = 0; i < events.Length; i++)
            {
                int slot = events[i]?.EquipmentSlotId ?? 0;
                if (slot > 0)
                    return slot;
            }

            return CharacterActionAuthoringIds.Resolve("mainHand", 405);
        }

        private void AddEventAtTrack(TimelineTrackKind kind, int frame)
        {
            if (_snapshot == null || _snapshot.ReadOnly)
                return;

            if (_host?.AddEventAtTrack != null)
            {
                frame = CharacterActionTimelineLayout.ClampFrame(frame, _snapshot.ActionDurationFrames);
                _host.AddEventAtTrack(kind, frame);
                return;
            }

            AddCharacterActionEventAtTrack(kind, frame);
        }

        public void AddCharacterActionEventAtTrack(TimelineTrackKind kind, int frame)
        {
            if (_snapshot?.Action == null || _snapshot.ReadOnly)
                return;

            CharacterActionJsonDto action = _snapshot.Action;
            frame = CharacterActionTimelineLayout.ClampFrame(frame, _snapshot.ActionDurationFrames);

            if (kind == TimelineTrackKind.MovementControl || kind == TimelineTrackKind.RotationControl)
            {
                bool rotation = kind == TimelineTrackKind.RotationControl;
                ControlScaleKeyJsonDto key = null;
                foreach (var existing in CharacterActionControlScaleAuthoring.Keys(action, rotation))
                    if (existing.Frame == frame) key = existing;
                if (key == null)
                    _host?.CommitStructural(() => key = CharacterActionControlScaleAuthoring.Insert(action, rotation, frame));
                if (key != null) SelectItem(new ControlScaleTimelineAdapter(action, key, rotation, true).Id);
                return;
            }

            _host?.CommitStructural(() =>
            {
                switch (kind)
                {
                    case TimelineTrackKind.Phases:
                    {
                        CharacterActionPhaseTimelineUtility.InsertPhaseAt(
                            action,
                            frame,
                            _snapshot.ActionDurationFrames,
                            CharacterActionPhaseKind.Active.ToString());
                        break;
                    }
                    case TimelineTrackKind.TransitionWindow:
                    {
                        var rules = new List<CharacterCancelRuleJsonDto>(action.CancelRules ?? Array.Empty<CharacterCancelRuleJsonDto>());
                        rules.Add(TransitionWindowTimelineAdapter.CreateDefaultRule(
                            action,
                            frame,
                            _snapshot.ActionDurationFrames));
                        action.CancelRules = rules.ToArray();
                        break;
                    }
                    case TimelineTrackKind.Motion:
                        action.MotionTrack = action.MotionTrack ?? new MotionTrackJsonDto();
                        var motion = new List<MotionTrackEventJsonDto>(action.MotionTrack.Events ?? Array.Empty<MotionTrackEventJsonDto>());
                        motion.Add(new MotionTrackEventJsonDto
                        {
                            Frame = frame,
                            Kind = CharacterActionTrackEventKind.ApplyImpulse.ToString(),
                            MovementMode = CharacterMovementMode.Airborne.ToString(),
                            Space = CharacterActionMotionVectorSpace.World.ToString(),
                            Y = 8f,
                            StableEventId = CharacterActionAuthoringIds.Resolve(
                                CharacterActionTimelineIdentity.MakeStableEventId(action, "impulse"), 401),
                        });
                        action.MotionTrack.Events = motion.ToArray();
                        break;
                    case TimelineTrackKind.Animation:
                    {
                        action.AnimationTrack = action.AnimationTrack ?? new AnimationTrackJsonDto();
                        var anim = new List<AnimationTrackEventJsonDto>(action.AnimationTrack.Events ?? Array.Empty<AnimationTrackEventJsonDto>());
                        var newAnim = new AnimationTrackEventJsonDto
                        {
                            Frame = frame,
                            Kind = CharacterActionTrackEventKind.CrossFadeAnimation.ToString(),
                            AnimationActionKey = "anim." + action.ActionId,
                            TransitionSeconds = 0.08f,
                            PlaybackSpeed = 1f,
                            StableEventId = CharacterActionAuthoringIds.Resolve(
                                CharacterActionTimelineIdentity.MakeStableEventId(action, "animation"), 401),
                        };
                        CharacterActionAnimationTimelineResolver.ApplyAutoSpan(
                            newAnim,
                            action,
                            _host?.DefaultDurationFrames ?? 8,
                            _host?.CharacterStableId);
                        anim.Add(newAnim);
                        action.AnimationTrack.Events = anim.ToArray();
                        break;
                    }
                    case TimelineTrackKind.Combat:
                        action.CombatTrack = action.CombatTrack ?? new CombatTrackJsonDto();
                        var combat = new List<CombatTrackEventJsonDto>(action.CombatTrack.Events ?? Array.Empty<CombatTrackEventJsonDto>());
                        combat.Add(new CombatTrackEventJsonDto
                        {
                            Frame = frame,
                            Kind = CharacterActionTrackEventKind.StartCombatAction.ToString(),
                            CombatActionId = CharacterActionAuthoringIds.Resolve("combat." + action.ActionId, 412),
                            StableEventId = CharacterActionAuthoringIds.Resolve(
                                CharacterActionTimelineIdentity.MakeStableEventId(action, "combat"), 401),
                        });
                        action.CombatTrack.Events = combat.ToArray();
                        break;
                    case TimelineTrackKind.Gameplay:
                        action.GameplayTrack = action.GameplayTrack ?? new GameplayTrackJsonDto();
                        var gameplay = new List<GameplayTrackEventJsonDto>(action.GameplayTrack.Events ?? Array.Empty<GameplayTrackEventJsonDto>());
                        gameplay.Add(new GameplayTrackEventJsonDto
                        {
                            Frame = frame,
                            Kind = CharacterActionTrackEventKind.SendGameplayRequest.ToString(),
                            RequestId = CharacterActionAuthoringIds.Resolve("gameplay." + action.ActionId, 408),
                            StableEventId = CharacterActionAuthoringIds.Resolve(
                                CharacterActionTimelineIdentity.MakeStableEventId(action, "gameplay"), 401),
                        });
                        action.GameplayTrack.Events = gameplay.ToArray();
                        break;
                    case TimelineTrackKind.Presentation:
                        action.PresentationTrack = action.PresentationTrack ?? new PresentationTrackJsonDto();
                        var pres = new List<PresentationTrackEventJsonDto>(action.PresentationTrack.Events ?? Array.Empty<PresentationTrackEventJsonDto>());
                        pres.Add(new PresentationTrackEventJsonDto
                        {
                            Frame = frame,
                            Kind = CharacterActionTrackEventKind.PlayAudioCue.ToString(),
                            CueId = CharacterActionAuthoringIds.Resolve("sfx." + action.ActionId, 401),
                            StableEventId = CharacterActionAuthoringIds.Resolve(
                                CharacterActionTimelineIdentity.MakeStableEventId(action, "sfx"), 401),
                        });
                        action.PresentationTrack.Events = pres.ToArray();
                        break;
                }
                CharacterActionEventIdentityAuthoring.AssignMissingEventOrders(action);
            });
        }
    }
}
