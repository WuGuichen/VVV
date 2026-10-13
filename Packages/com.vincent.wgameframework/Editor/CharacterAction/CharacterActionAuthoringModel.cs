using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using MxFramework.Combat.Animation;
using MxFramework.Core;
using MxFramework.Editor.Animation;
using MxFramework.Editor.CharacterAction.Timeline;
using Newtonsoft.Json;

namespace MxFramework.Editor.CharacterAction
{
    public sealed class CharacterActionAuthoringModel
    {
        public CharacterActionAuthoringModel()
            : this(null)
        {
        }

        internal CharacterActionAuthoringModel(CharacterActionAuthoringReferenceContext referenceContext)
        {
            ReferenceContext = referenceContext ?? CharacterActionAuthoringReferenceContext.CreateDefault();
        }

        public CharacterActionProfileJsonDto Profile { get; set; } = new CharacterActionProfileJsonDto();
        public CharacterActionSetJsonDto ActionSet { get; set; } = new CharacterActionSetJsonDto();
        public List<CharacterActionJsonDto> Actions { get; } = new List<CharacterActionJsonDto>();
        public List<CharacterReactionProfileJsonDto> ReactionProfiles { get; } = new List<CharacterReactionProfileJsonDto>();
        public List<CharacterLandingProfileJsonDto> LandingProfiles { get; } = new List<CharacterLandingProfileJsonDto>();
        public CharacterActionAuthoringReferenceContext ReferenceContext { get; set; }
        public int SelectedActionIndex { get; set; }

        public CharacterActionJsonDto SelectedAction =>
            Actions.Count == 0 ? null : Actions[Math.Max(0, Math.Min(SelectedActionIndex, Actions.Count - 1))];

        public static CharacterActionAuthoringModel CreateDefault()
        {
            var model = new CharacterActionAuthoringModel();
            model.Profile = new CharacterActionProfileJsonDto
            {
                Id = 1,
                StableId = "codex_flow_test.default",
                DisplayName = "codex flow test Default",
                ActionSetIds = new[] { "codex_flow_test.default" },
                ActionLibraryIds = new[] { "Common" },
            };
            model.ActionSet = new CharacterActionSetJsonDto
            {
                Id = 1,
                StableId = "codex_flow_test.default",
                DisplayName = "codex flow test Default",
                CharacterStableId = string.Empty,
                EquipmentStateStableId = string.Empty,
                MovementProfileId = CharacterContentIdentityAuthoringCatalog.GetOrAllocate(402, "movement.default"),
                ReactionProfileId = "reaction.codex_flow_test.default",
                DefaultActionId = 0,
                CommandBindings = new[]
                {
                    new CharacterActionBindingJsonDto
                    {
                        IntentId = CharacterContentIdentityAuthoringCatalog.GetOrAllocate(404, "Jump"),
                        ActionId = 300,
                        Priority = 5,
                        AllowQueue = false,
                        QueueWindowFrames = 0,
                    },
                    new CharacterActionBindingJsonDto
                    {
                        IntentId = CharacterContentIdentityAuthoringCatalog.GetOrAllocate(404, "LightAttack"),
                        ActionId = 301,
                        Priority = 10,
                        AllowQueue = false,
                        QueueWindowFrames = 0,
                    },
                },
                AbilityBindings = Array.Empty<CharacterAbilityActionBindingJsonDto>(),
                ReactionBindings = Array.Empty<CharacterReactionBindingJsonDto>(),
            };
            model.Actions.Add(CreateJumpAction());
            model.Actions.Add(CreateLightAttackAction());
            model.Actions.Add(CreateCohesionBreakReactionAction());
            model.ReactionProfiles.Add(CreateDefaultReactionProfile());
            model.ReferenceContext = CharacterActionAuthoringReferenceContext.CreateDefault();
            model.SelectedActionIndex = 0;
            return model;
        }

        public void AddAction(CharacterActionJsonDto action)
        {
            if (action == null || action.ActionId <= 0)
                throw new ArgumentException("A catalog-allocated positive CharacterAction ID is required.", nameof(action));
            Actions.Add(NormalizeAction(action));
            SelectedActionIndex = Actions.Count - 1;
        }

        public CharacterActionJsonDto DuplicateSelectedAction(int newActionId)
        {
            CharacterActionJsonDto selected = SelectedAction;
            if (selected == null)
                return null;
            if (newActionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(newActionId));

            CharacterActionJsonDto copy = Clone(selected);
            copy.ActionId = newActionId;
            CharacterActionEventIdentityAuthoring.AssignCopiedEventIds(copy);
            copy.DisplayName = string.IsNullOrWhiteSpace(selected.DisplayName)
                ? "Action #" + copy.ActionId
                : selected.DisplayName + " Copy";
            AddAction(copy);
            return copy;
        }

        public bool DeleteSelectedAction()
        {
            if (Actions.Count == 0)
                return false;

            int index = Math.Max(0, Math.Min(SelectedActionIndex, Actions.Count - 1));
            Actions.RemoveAt(index);
            SelectedActionIndex = Math.Max(0, Math.Min(index, Actions.Count - 1));
            return true;
        }

        public void Normalize()
        {
            Profile = NormalizeProfile(Profile);
            ActionSet = NormalizeActionSet(ActionSet);
            for (int i = 0; i < Actions.Count; i++)
                Actions[i] = NormalizeAction(Actions[i]);
            for (int i = 0; i < ReactionProfiles.Count; i++)
                ReactionProfiles[i] = NormalizeReactionProfile(ReactionProfiles[i]);
            for (int i = LandingProfiles.Count - 1; i >= 0; i--)
            {
                if (LandingProfiles[i] == null)
                    LandingProfiles.RemoveAt(i);
            }
            ReferenceContext = ReferenceContext ?? CharacterActionAuthoringReferenceContext.CreateDefault();
            ReferenceContext.Normalize();
            SelectedActionIndex = Math.Max(0, Math.Min(SelectedActionIndex, Math.Max(0, Actions.Count - 1)));
        }

        public CharacterActionAuthoringModel CloneForEditing()
        {
            var copy = new CharacterActionAuthoringModel
            {
                Profile = Clone(Profile),
                ActionSet = Clone(ActionSet),
                ReferenceContext = Clone(ReferenceContext),
                SelectedActionIndex = SelectedActionIndex,
            };
            for (int i = 0; i < Actions.Count; i++)
                copy.Actions.Add(Clone(Actions[i]));
            for (int i = 0; i < ReactionProfiles.Count; i++)
                copy.ReactionProfiles.Add(Clone(ReactionProfiles[i]));
            for (int i = 0; i < LandingProfiles.Count; i++)
                copy.LandingProfiles.Add(Clone(LandingProfiles[i]));
            copy.Normalize();
            return copy;
        }

        public static CharacterActionJsonDto CreateJumpAction()
        {
            return NormalizeAction(new CharacterActionJsonDto
            {
                ActionId = 300,
                DisplayName = "Basic Jump",
                Category = CharacterActionCategory.Jump.ToString(),
                TimelineAuthority = CharacterActionTimelineAuthority.CharacterAuthored.ToString(),
                Tags = new[] { "movement", "jump" },
                Priority = 10,
                DurationFrames = 8,
                Requirements = Array.Empty<CharacterActionRequirementJsonDto>(),
                Phases = new[]
                {
                    new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Startup.ToString(), StartFrame = 0, EndFrame = 1 },
                    new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Active.ToString(), StartFrame = 2, EndFrame = 4 },
                    new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Recovery.ToString(), StartFrame = 5, EndFrame = 7 },
                },
                MotionTrack = new MotionTrackJsonDto
                {
                    UsesRootMotion = false,
                    Events = new[]
                    {
                        new MotionTrackEventJsonDto
                        {
                            Frame = 0,
                            Kind = CharacterActionTrackEventKind.ApplyImpulse.ToString(),
                            MovementMode = CharacterMovementMode.Airborne.ToString(),
                            Space = CharacterActionMotionVectorSpace.World.ToString(),
                            X = 0f,
                            Y = 8f,
                            Z = 0f,
                            StableEventId = CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401, "basic_jump.impulse"),
                        },
                    },
                },
                AnimationTrack = new AnimationTrackJsonDto
                {
                    Events = new[]
                    {
                        new AnimationTrackEventJsonDto
                        {
                            Frame = 0,
                            Kind = CharacterActionTrackEventKind.CrossFadeAnimation.ToString(),
                            AnimationActionKey = "anim.basic_jump",
                            TransitionSeconds = 0.08f,
                            StableEventId = CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401, "basic_jump.animation"),
                        },
                    },
                },
                CombatTrack = new CombatTrackJsonDto
                {
                    Events = new[]
                    {
                        new CombatTrackEventJsonDto
                        {
                            Frame = 3,
                            EndFrame = 5,
                            Kind = CharacterActionTrackEventKind.HitTraceWindow.ToString(),
                            EquipmentSlotId = CharacterContentIdentityAuthoringCatalog.GetOrAllocate(405, "mainHand"),
                            StableEventId = CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401, "light_attack.hit_window"),
                        HitEvent = CreateDefaultWeaponHitEvent(CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401, "light_attack.hit_window")),
                        },
                    },
                },
            });
        }

        public static CharacterActionJsonDto CreateLightAttackAction()
        {
            return NormalizeAction(new CharacterActionJsonDto
            {
                ActionId = 301,
                DisplayName = "Light Attack",
                Category = CharacterActionCategory.BasicAttack.ToString(),
                TimelineAuthority = CharacterActionTimelineAuthority.CharacterAuthored.ToString(),
                Tags = new[] { "combat", "light_attack" },
                Priority = 20,
                DurationFrames = 10,
                Requirements = Array.Empty<CharacterActionRequirementJsonDto>(),
                Phases = new[]
                {
                    new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Startup.ToString(), StartFrame = 0, EndFrame = 2 },
                    new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Active.ToString(), StartFrame = 3, EndFrame = 5 },
                    new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Recovery.ToString(), StartFrame = 6, EndFrame = 9 },
                },
                MotionTrack = new MotionTrackJsonDto
                {
                    UsesRootMotion = true,
                    RootMotion = CreateDefaultLightAttackRootMotion(),
                    Events = Array.Empty<MotionTrackEventJsonDto>(),
                },
                AnimationTrack = new AnimationTrackJsonDto
                {
                    Events = new[]
                    {
                        new AnimationTrackEventJsonDto
                        {
                            Frame = 0,
                            EndFrame = 9,
                            Kind = CharacterActionTrackEventKind.CrossFadeAnimation.ToString(),
                            AnimationActionKey = "combat.attack.light",
                            AnimationClipResourceKey = "art.character.skeleton.animation.katana_atk1",
                            TransitionSeconds = 0.12f,
                            StableEventId = CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401, "light_attack.animation"),
                        },
                    },
                },
            });
        }

        public static CharacterActionJsonDto CreateCohesionBreakReactionAction()
        {
            return NormalizeAction(new CharacterActionJsonDto
            {
                ActionId = 330,
                DisplayName = "Katana Cohesion Break React",
                Category = CharacterActionCategory.Reaction.ToString(),
                TimelineAuthority = CharacterActionTimelineAuthority.CharacterAuthored.ToString(),
                Tags = new[] { "reaction", "cohesion_break", "katana", "hit_stun" },
                Priority = 100,
                DurationFrames = 18,
                Requirements = Array.Empty<CharacterActionRequirementJsonDto>(),
                Phases = new[]
                {
                    new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Active.ToString(), StartFrame = 0, EndFrame = 17 },
                },
                MotionTrack = new MotionTrackJsonDto
                {
                    UsesRootMotion = false,
                    Events = new[]
                    {
                        new MotionTrackEventJsonDto
                        {
                            Frame = 0,
                            EndFrame = 17,
                            Kind = CharacterActionTrackEventKind.SetControlScale.ToString(),
                            MovementMode = CharacterMovementMode.ControlLocked.ToString(),
                            MovementControlScale = 0f,
                            RotationControlScale = 0f,
                            StableEventId = CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401, "katana_cohesion_break_react.control_window"),
                        },
                    },
                },
                AnimationTrack = new AnimationTrackJsonDto
                {
                    Events = new[]
                    {
                        new AnimationTrackEventJsonDto
                        {
                            Frame = 0,
                            EndFrame = 17,
                            Kind = CharacterActionTrackEventKind.CrossFadeAnimation.ToString(),
                            PlaybackSpeed = 1f,
                            AnimationActionKey = "anim.reaction.cohesion_break",
                            AnimationClipResourceKey = "art.character.skeleton.animation.damage_stun01_start",
                            TransitionSeconds = 0.04f,
                            StableEventId = CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401, "katana_cohesion_break_react.animation"),
                        },
                    },
                },
            });
        }

        public static CharacterReactionProfileJsonDto CreateDefaultReactionProfile()
        {
            return NormalizeReactionProfile(new CharacterReactionProfileJsonDto
            {
                StableId = "reaction.codex_flow_test.default",
                DisplayName = "codex flow test Default Reaction",
                DefaultActionId = 330,
                Rules = new[]
                {
                    new CharacterReactionRuleJsonDto
                    {
                        ActionId = 330,
                        Trigger = CharacterReactionRuleTrigger.CohesionBreak.ToString(),
                        CurrentScatterPressureBand = MxFramework.Gameplay.PressureBand.Broken.ToString(),
                        Priority = 100,
                    },
                    new CharacterReactionRuleJsonDto
                    {
                        ActionId = 330,
                        Trigger = CharacterReactionRuleTrigger.Hit.ToString(),
                        CurrentScatterPressureBand = MxFramework.Gameplay.PressureBand.Broken.ToString(),
                        Priority = 80,
                    },
                },
            });
        }

        public static CharacterActionJsonDto CreateBlankAction(int actionId, string displayName)
        {
            return NormalizeAction(new CharacterActionJsonDto
            {
                ActionId = actionId,
                DisplayName = displayName ?? string.Empty,
                Category = CharacterActionCategory.Movement.ToString(),
                TimelineAuthority = CharacterActionTimelineAuthority.CharacterAuthored.ToString(),
                Tags = Array.Empty<string>(),
                Priority = 0,
                DurationFrames = 1,
                Phases = new[]
                {
                    new CharacterActionPhaseJsonDto { Kind = CharacterActionPhaseKind.Startup.ToString(), StartFrame = 0, EndFrame = 0 },
                },
            });
        }

        public static CharacterActionSetJsonDto NormalizeActionSet(CharacterActionSetJsonDto actionSet)
        {
            actionSet = actionSet ?? new CharacterActionSetJsonDto();
            actionSet.StableId = actionSet.StableId ?? string.Empty;
            actionSet.DisplayName = actionSet.DisplayName ?? string.Empty;
            actionSet.CharacterStableId = actionSet.CharacterStableId ?? string.Empty;
            actionSet.EquipmentStateStableId = actionSet.EquipmentStateStableId ?? string.Empty;
            actionSet.MovementProfileId = actionSet.MovementProfileId;
            actionSet.ReactionProfileId = actionSet.ReactionProfileId ?? string.Empty;
            actionSet.CommandBindings = actionSet.CommandBindings ?? Array.Empty<CharacterActionBindingJsonDto>();
            actionSet.CommandSequences = actionSet.CommandSequences ?? Array.Empty<CharacterActionCommandSequenceJsonDto>();
            actionSet.AbilityBindings = actionSet.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            actionSet.ReactionBindings = actionSet.ReactionBindings ?? Array.Empty<CharacterReactionBindingJsonDto>();
            return actionSet;
        }

        public static CharacterActionProfileJsonDto NormalizeProfile(CharacterActionProfileJsonDto profile)
        {
            profile = profile ?? new CharacterActionProfileJsonDto();
            profile.StableId = profile.StableId ?? string.Empty;
            profile.DisplayName = profile.DisplayName ?? string.Empty;
            profile.ActionSetIds = NormalizeStringArray(profile.ActionSetIds);
            profile.ActionLibraryIds = NormalizeStringArray(profile.ActionLibraryIds);
            return profile;
        }

        public static CharacterReactionProfileJsonDto NormalizeReactionProfile(CharacterReactionProfileJsonDto profile)
        {
            profile = profile ?? new CharacterReactionProfileJsonDto();
            profile.StableId = profile.StableId ?? string.Empty;
            profile.DisplayName = profile.DisplayName ?? string.Empty;
            profile.Rules = profile.Rules ?? Array.Empty<CharacterReactionRuleJsonDto>();
            for (int i = 0; i < profile.Rules.Length; i++)
            {
                CharacterReactionRuleJsonDto rule = profile.Rules[i] ?? new CharacterReactionRuleJsonDto();
                rule.Trigger = string.IsNullOrWhiteSpace(rule.Trigger)
                    ? CharacterReactionRuleTrigger.Any.ToString()
                    : rule.Trigger;
                rule.CurrentScatterPressureBand = rule.CurrentScatterPressureBand ?? string.Empty;
                rule.CurrentPhase = rule.CurrentPhase ?? string.Empty;
                rule.BodyPartId = rule.BodyPartId;
                rule.HitZoneId = rule.HitZoneId;
                rule.DamageTypeId = rule.DamageTypeId;
                rule.HitDirection = rule.HitDirection ?? string.Empty;
                if (rule.ReactionGroupId < 0) throw new ArgumentOutOfRangeException(nameof(rule.ReactionGroupId));
                profile.Rules[i] = rule;
            }

            return profile;
        }

        private static string[] NormalizeStringArray(string[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<string>();

            var normalized = new List<string>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i]?.Trim();
                if (!string.IsNullOrWhiteSpace(value) && !normalized.Contains(value))
                    normalized.Add(value);
            }

            return normalized.ToArray();
        }

        public static CharacterActionJsonDto NormalizeAction(CharacterActionJsonDto action)
        {
            action = action ?? CreateBlankAction(0, string.Empty);
            action.DisplayName = action.DisplayName ?? string.Empty;
            action.Category = string.IsNullOrWhiteSpace(action.Category)
                ? CharacterActionCategory.Movement.ToString()
                : action.Category;
            action.TimelineAuthority = string.IsNullOrWhiteSpace(action.TimelineAuthority)
                ? CharacterActionTimelineAuthority.CharacterAuthored.ToString()
                : action.TimelineAuthority;
            action.Tags = action.Tags ?? Array.Empty<string>();
            action.Requirements = action.Requirements ?? Array.Empty<CharacterActionRequirementJsonDto>();
            action.Phases = NormalizePhases(action.Phases);
            action.CancelRules = action.CancelRules ?? Array.Empty<CharacterCancelRuleJsonDto>();
            for (int i = 0; i < action.CancelRules.Length; i++)
            {
                CharacterCancelRuleJsonDto rule = action.CancelRules[i] ?? new CharacterCancelRuleJsonDto();
                action.CancelRules[i] = rule;
            }
            action.InterruptRules = action.InterruptRules ?? Array.Empty<CharacterInterruptRuleJsonDto>();
            CharacterActionControlScaleAuthoring.Migrate(action);
            if (string.IsNullOrEmpty(action.MotionTrack?.ControlScaleMigrationIssue))
                action.MotionTrack = NormalizeMotionTrack(action.MotionTrack);
            action.CombatTrack = NormalizeCombatTrack(action.CombatTrack);
            action.GameplayTrack = NormalizeGameplayTrack(action.GameplayTrack);
            action.AnimationTrack = NormalizeAnimationTrack(action.AnimationTrack);
            action.PresentationTrack = NormalizePresentationTrack(action.PresentationTrack);
            CharacterActionBladeTrailAuthoringProjection.Synchronize(action);
            action.DebugTrack = action.DebugTrack;
            return action;
        }

        public static CharacterActionPhaseJsonDto[] NormalizePhases(CharacterActionPhaseJsonDto[] phases)
        {
            phases = phases ?? Array.Empty<CharacterActionPhaseJsonDto>();
            for (int i = 0; i < phases.Length; i++)
            {
                CharacterActionPhaseJsonDto phase = phases[i] ?? new CharacterActionPhaseJsonDto();
                phase.Kind = string.IsNullOrWhiteSpace(phase.Kind)
                    ? CharacterActionPhaseKind.Startup.ToString()
                    : phase.Kind;
                phase.CombatPhaseAnchor = string.IsNullOrWhiteSpace(phase.CombatPhaseAnchor)
                    ? CombatActionPhase.None.ToString()
                    : phase.CombatPhaseAnchor;
                phases[i] = phase;
            }

            return phases;
        }

        public static MotionTrackJsonDto NormalizeMotionTrack(MotionTrackJsonDto track)
        {
            if (track == null)
                return null;

            track.RootMotion = NormalizeRootMotion(track.RootMotion);
            if (track.VerticalRootMotion != null)
            {
                track.VerticalRootMotion.SourceAnimationClipResourceKey =
                    track.VerticalRootMotion.SourceAnimationClipResourceKey ?? string.Empty;
                track.VerticalRootMotion.Frames = track.VerticalRootMotion.Frames
                    ?? Array.Empty<VerticalRootMotionFrameJsonDto>();
            }
            track.Events = track.Events ?? Array.Empty<MotionTrackEventJsonDto>();
            for (int i = 0; i < track.Events.Length; i++)
            {
                MotionTrackEventJsonDto evt = track.Events[i] ?? new MotionTrackEventJsonDto();
                evt.Kind = string.IsNullOrWhiteSpace(evt.Kind)
                    ? CharacterActionTrackEventKind.ApplyImpulse.ToString()
                    : evt.Kind;
                evt.MovementMode = string.IsNullOrWhiteSpace(evt.MovementMode)
                    ? CharacterMovementMode.Airborne.ToString()
                    : evt.MovementMode;
                evt.Space = string.IsNullOrWhiteSpace(evt.Space)
                    ? CharacterActionMotionVectorSpace.World.ToString()
                    : evt.Space;
                evt.StableEventId = evt.StableEventId;
                if (evt.MovementControlScale.HasValue)
                    evt.MovementControlScale = CharacterActionControlScaleJsonProjection.ValidateScale(evt.MovementControlScale.Value);
                if (evt.RotationControlScale.HasValue)
                    evt.RotationControlScale = CharacterActionControlScaleJsonProjection.ValidateScale(evt.RotationControlScale.Value);
                track.Events[i] = evt;
            }

            return track;
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }

        private static float Clamp(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return fallback;
            return Math.Max(min, Math.Min(max, value));
        }

        public static RootMotionTrackJsonDto NormalizeRootMotion(RootMotionTrackJsonDto rootMotion)
        {
            if (rootMotion == null)
                return null;

            rootMotion.SourceAnimationEventId = rootMotion.SourceAnimationEventId;
            rootMotion.SourceAnimationClipResourceKey = rootMotion.SourceAnimationClipResourceKey ?? string.Empty;
            rootMotion.Space = string.IsNullOrWhiteSpace(rootMotion.Space)
                ? CharacterActionRootMotionSpace.Local.ToString()
                : rootMotion.Space;
            rootMotion.ObstaclePolicy = string.IsNullOrWhiteSpace(rootMotion.ObstaclePolicy)
                ? CharacterActionRootMotionObstaclePolicy.Default.ToString()
                : rootMotion.ObstaclePolicy;
            if (rootMotion.SampleTickRate <= 0)
                rootMotion.SampleTickRate = CharacterActionAnimationKeyUtility.TimelineFramesPerSecond;
            rootMotion.Frames = rootMotion.Frames ?? Array.Empty<RootMotionFrameJsonDto>();
            for (int i = 0; i < rootMotion.Frames.Length; i++)
            {
                rootMotion.Frames[i] = rootMotion.Frames[i] ?? new RootMotionFrameJsonDto();
                rootMotion.Frames[i].Y = 0f;
            }
            return rootMotion;
        }

        public static RootMotionTrackJsonDto CreateRootMotionDraftFromAnimation(
            CharacterActionJsonDto action,
            int preferredFrame = -1)
        {
            var rootMotion = new RootMotionTrackJsonDto
            {
                Space = CharacterActionRootMotionSpace.Local.ToString(),
                SampleTickRate = CharacterActionAnimationKeyUtility.TimelineFramesPerSecond,
                Frames = Array.Empty<RootMotionFrameJsonDto>(),
            };

            if (TryResolveRootMotionSourceAnimationEvent(action, rootMotion, preferredFrame, out AnimationTrackEventJsonDto animationEvent))
                ApplyRootMotionSource(rootMotion, animationEvent);

            return rootMotion;
        }

        public static void ApplyRootMotionSource(
            RootMotionTrackJsonDto rootMotion,
            AnimationTrackEventJsonDto animationEvent)
        {
            if (rootMotion == null || animationEvent == null)
                return;

            rootMotion.SourceAnimationEventId = animationEvent.StableEventId;
            rootMotion.SourceAnimationClipResourceKey = CharacterActionAnimationKeyUtility.ResolveEffectiveKey(
                animationEvent.AnimationClipResourceKey,
                animationEvent.AnimationActionKey);
        }

        public static RootMotionFrameJsonDto[] MergeRootMotionFrames(
            RootMotionFrameJsonDto[] existingFrames,
            RootMotionFrameJsonDto[] replacementFrames,
            int startFrame,
            int endFrame)
        {
            NormalizeFrameRange(ref startFrame, ref endFrame);
            var byFrame = new SortedDictionary<int, RootMotionFrameJsonDto>();
            existingFrames = existingFrames ?? Array.Empty<RootMotionFrameJsonDto>();
            replacementFrames = replacementFrames ?? Array.Empty<RootMotionFrameJsonDto>();

            for (int i = 0; i < existingFrames.Length; i++)
            {
                RootMotionFrameJsonDto frame = existingFrames[i];
                if (frame == null)
                    continue;

                int clampedFrame = Math.Max(0, frame.Frame);
                if (clampedFrame >= startFrame && clampedFrame <= endFrame)
                    continue;

                RootMotionFrameJsonDto copy = CloneRootMotionFrame(frame);
                byFrame[copy.Frame] = copy;
            }

            for (int i = 0; i < replacementFrames.Length; i++)
            {
                RootMotionFrameJsonDto frame = replacementFrames[i];
                if (frame == null)
                    continue;

                int clampedFrame = Math.Max(0, frame.Frame);
                if (clampedFrame < startFrame || clampedFrame > endFrame)
                    continue;

                byFrame[clampedFrame] = new RootMotionFrameJsonDto
                {
                    Frame = clampedFrame,
                    X = frame.X,
                    Y = 0f,
                    Z = frame.Z,
                };
            }

            return ToRootMotionFrameArray(byFrame);
        }

        public static RootMotionFrameJsonDto[] ScaleRootMotionFrames(
            RootMotionFrameJsonDto[] frames,
            int startFrame,
            int endFrame,
            float scaleX,
            float scaleY,
            float scaleZ)
        {
            NormalizeFrameRange(ref startFrame, ref endFrame);
            frames = frames ?? Array.Empty<RootMotionFrameJsonDto>();
            var byFrame = new SortedDictionary<int, RootMotionFrameJsonDto>();
            for (int i = 0; i < frames.Length; i++)
            {
                RootMotionFrameJsonDto frame = frames[i];
                if (frame == null)
                    continue;

                var copy = CloneRootMotionFrame(frame);
                if (copy.Frame >= startFrame && copy.Frame <= endFrame)
                {
                    copy.X *= scaleX;
                    copy.Y = 0f;
                    copy.Z *= scaleZ;
                }

                byFrame[copy.Frame] = copy;
            }

            return ToRootMotionFrameArray(byFrame);
        }

        public static RootMotionFrameJsonDto[] ZeroRootMotionFrames(
            RootMotionFrameJsonDto[] frames,
            int startFrame,
            int endFrame)
        {
            return ScaleRootMotionFrames(frames, startFrame, endFrame, 0f, 0f, 0f);
        }

        public static bool TryResolveRootMotionSourceAnimationEvent(
            CharacterActionJsonDto action,
            RootMotionTrackJsonDto rootMotion,
            int preferredFrame,
            out AnimationTrackEventJsonDto animationEvent)
        {
            animationEvent = null;
            AnimationTrackEventJsonDto[] events = action?.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEventJsonDto>();
            if (events.Length == 0)
                return false;

            int sourceEventId = rootMotion?.SourceAnimationEventId ?? 0;
            if (sourceEventId > 0)
            {
                for (int i = 0; i < events.Length; i++)
                {
                    AnimationTrackEventJsonDto candidate = events[i];
                    if (candidate == null)
                        continue;
                    if (candidate.StableEventId != sourceEventId)
                        continue;

                    animationEvent = candidate;
                    return true;
                }
            }

            if (preferredFrame >= 0)
            {
                for (int i = 0; i < events.Length; i++)
                {
                    AnimationTrackEventJsonDto candidate = events[i];
                    if (!HasUsableAnimationSource(candidate) || !ContainsFrame(candidate, preferredFrame))
                        continue;

                    animationEvent = candidate;
                    return true;
                }
            }

            for (int i = 0; i < events.Length; i++)
            {
                AnimationTrackEventJsonDto candidate = events[i];
                if (!HasUsableAnimationSource(candidate))
                    continue;

                animationEvent = candidate;
                return true;
            }

            for (int i = 0; i < events.Length; i++)
            {
                if (events[i] == null)
                    continue;

                animationEvent = events[i];
                return true;
            }

            return false;
        }

        private static bool HasUsableAnimationSource(AnimationTrackEventJsonDto animationEvent)
        {
            return !string.IsNullOrWhiteSpace(CharacterActionAnimationKeyUtility.ResolveEffectiveKey(
                animationEvent?.AnimationClipResourceKey,
                animationEvent?.AnimationActionKey));
        }

        private static bool ContainsFrame(AnimationTrackEventJsonDto animationEvent, int frame)
        {
            if (animationEvent == null)
                return false;

            int start = Math.Min(animationEvent.Frame, animationEvent.EndFrame);
            int end = Math.Max(animationEvent.Frame, animationEvent.EndFrame);
            return frame >= start && frame <= end;
        }

        private static void NormalizeFrameRange(ref int startFrame, ref int endFrame)
        {
            if (endFrame < startFrame)
            {
                int temp = startFrame;
                startFrame = endFrame;
                endFrame = temp;
            }

            startFrame = Math.Max(0, startFrame);
            endFrame = Math.Max(startFrame, endFrame);
        }

        private static RootMotionFrameJsonDto CloneRootMotionFrame(RootMotionFrameJsonDto frame)
        {
            if (frame == null)
                return null;

            return new RootMotionFrameJsonDto
            {
                Frame = Math.Max(0, frame.Frame),
                X = frame.X,
                Y = 0f,
                Z = frame.Z,
            };
        }

        private static RootMotionFrameJsonDto[] ToRootMotionFrameArray(SortedDictionary<int, RootMotionFrameJsonDto> frames)
        {
            var result = new RootMotionFrameJsonDto[frames.Count];
            int index = 0;
            foreach (KeyValuePair<int, RootMotionFrameJsonDto> pair in frames)
                result[index++] = pair.Value;
            return result;
        }

        public static CombatTrackJsonDto NormalizeCombatTrack(CombatTrackJsonDto track)
        {
            if (track == null)
                return null;

            track.CombatActionId = track.CombatActionId;
            track.Events = track.Events ?? Array.Empty<CombatTrackEventJsonDto>();
            for (int i = 0; i < track.Events.Length; i++)
            {
                CombatTrackEventJsonDto evt = track.Events[i] ?? new CombatTrackEventJsonDto();
                evt.Kind = string.IsNullOrWhiteSpace(evt.Kind)
                    ? CharacterActionTrackEventKind.StartCombatAction.ToString()
                    : evt.Kind;
                evt.CombatActionId = evt.CombatActionId;
                evt.TraceProfileId = evt.TraceProfileId;
                evt.EquipmentSlotId = evt.EquipmentSlotId;
                NormalizeHitEventFields(evt);
                NormalizeHitVolumeFields(evt);
                evt.StableEventId = evt.StableEventId;
                track.Events[i] = evt;
            }

            return track;
        }

        public static GameplayTrackJsonDto NormalizeGameplayTrack(GameplayTrackJsonDto track)
        {
            if (track == null)
                return null;

            track.Events = track.Events ?? Array.Empty<GameplayTrackEventJsonDto>();
            for (int i = 0; i < track.Events.Length; i++)
            {
                GameplayTrackEventJsonDto evt = track.Events[i] ?? new GameplayTrackEventJsonDto();
                evt.Kind = string.IsNullOrWhiteSpace(evt.Kind)
                    ? CharacterActionTrackEventKind.SendGameplayRequest.ToString()
                    : evt.Kind;
                evt.RequestId = evt.RequestId;
                evt.AbilityStableId = evt.AbilityStableId;
                NormalizeHitEventFields(evt);
                evt.TargetPolicyId = evt.TargetPolicyId;
                evt.StableEventId = evt.StableEventId;
                track.Events[i] = evt;
            }

            return track;
        }

        private static void NormalizeHitEventFields(CombatTrackEventJsonDto evt)
        {
            if (evt == null)
                return;

            if (evt.HitEvent == null)
                return;

            evt.HitEvent.TargetTypes = NormalizeStringArray(evt.HitEvent.TargetTypes);
            evt.HitEvent.EnvironmentHitPolicy = evt.HitEvent.EnvironmentHitPolicy ?? string.Empty;
            evt.HitEvent.StableEventId = evt.HitEvent.StableEventId;
            evt.HitEvent.MaxAcceptedHits = Math.Max(0, evt.HitEvent.MaxAcceptedHits);
            evt.HitEvent.HitSameTargetPolicy = evt.HitEvent.HitSameTargetPolicy ?? string.Empty;
            evt.HitEvent.OnHitBuffs = CharacterActionHitEventAuthoringModel.NormalizeOnHitBuffs(evt.HitEvent.OnHitBuffs);
            if (evt.HitEvent.Damage != null)
            {
                evt.HitEvent.Damage.DamageTypeLabel = CharacterActionDamageIdentityAuthoring.Label(409, evt.HitEvent.Damage.DamageTypeId, evt.HitEvent.Damage.DamageTypeLabel);
                evt.HitEvent.Damage.FormulaLabel = CharacterActionDamageIdentityAuthoring.FormulaLabel(evt.HitEvent.Damage.FormulaId, evt.HitEvent.Damage.FormulaLabel);
                evt.HitEvent.Damage.BaseDamage = Math.Max(0, evt.HitEvent.Damage.BaseDamage);
                evt.HitEvent.Damage.SourceAttributeScales =
                    CharacterActionHitEventAuthoringModel.NormalizeDamageAttributeScales(evt.HitEvent.Damage.SourceAttributeScales);
                if (evt.HitEvent.Damage.SourceAttributeScales.Length > 0
                    && evt.HitEvent.Damage.FormulaId == 0)
                {
                    evt.HitEvent.Damage.FormulaId = 4;
                    evt.HitEvent.Damage.FormulaLabel = CharacterActionDamageAuthoringText.DefaultDamageFormulaId;
                }
                evt.HitEvent.Damage.ImpactPower = Math.Max(0, evt.HitEvent.Damage.ImpactPower);
                evt.HitEvent.Damage.LaunchPower = Math.Max(0, evt.HitEvent.Damage.LaunchPower);
                evt.HitEvent.Damage.Tags = CharacterActionDamageIdentityAuthoring.NormalizeIds(evt.HitEvent.Damage.Tags);
                evt.HitEvent.Damage.TagLabels = CharacterActionDamageIdentityAuthoring.TagLabels(evt.HitEvent.Damage.Tags, evt.HitEvent.Damage.TagLabels);
                evt.HitEvent.Damage.AnalysisCategoryLabel = CharacterActionDamageIdentityAuthoring.Label(411, evt.HitEvent.Damage.AnalysisCategory, evt.HitEvent.Damage.AnalysisCategoryLabel);
            }
        }

        private static void NormalizeHitVolumeFields(CombatTrackEventJsonDto evt)
        {
            if (evt?.HitVolume == null)
                return;

            evt.HitVolume.SourceKind = string.IsNullOrWhiteSpace(evt.HitVolume.SourceKind)
                ? CharacterActionHitVolumeSourceKind.ManualCapsule.ToString()
                : evt.HitVolume.SourceKind;
              if (string.Equals(
                      evt.HitVolume.SourceKind,
                      CharacterActionHitVolumeSourceKind.CasterBody.ToString(),
                      StringComparison.Ordinal))
              {
                  evt.HitVolume.ShapeKind = string.Empty;
                  evt.HitVolume.LocalPointA = null;
                  evt.HitVolume.LocalPointB = null;
                  evt.HitVolume.Radius = 0f;
                  evt.HitVolume.BodyOffset = evt.HitVolume.BodyOffset ?? new CharacterActionHitVolumePointJsonDto();
                  evt.HitVolume.BodyRadiusScale = evt.HitVolume.BodyRadiusScale <= 0f
                      ? 1f
                      : evt.HitVolume.BodyRadiusScale;
                  evt.HitVolume.BodyHeightScale = evt.HitVolume.BodyHeightScale <= 0f
                      ? 1f
                      : evt.HitVolume.BodyHeightScale;
                  return;
              }

              if (string.Equals(
                      evt.HitVolume.SourceKind,
                      CharacterActionHitVolumeSourceKind.BakedSocketCapsule.ToString(),
                      StringComparison.Ordinal))
              {
                  evt.HitVolume.ShapeKind = string.Empty;
                  evt.HitVolume.LocalPointA = null;
                  evt.HitVolume.LocalPointB = null;
                  evt.HitVolume.BodyOffset = null;
                  evt.HitVolume.BodyRadiusScale = 1f;
                  evt.HitVolume.BodyRadiusAdd = 0f;
                  evt.HitVolume.BodyHeightScale = 1f;
                  evt.HitVolume.BodyHeightAdd = 0f;
                  evt.HitVolume.StartSocketId = evt.HitVolume.StartSocketId;
                  evt.HitVolume.EndSocketId = evt.HitVolume.EndSocketId;
                  evt.HitVolume.Radius = Math.Max(0f, evt.HitVolume.Radius);
                  if ((evt.HitVolume.StartSocketId <= 0)
                      || (evt.HitVolume.EndSocketId <= 0)
                      || evt.HitVolume.Radius <= 0f)
                  {
                      evt.HitVolume = null;
                  }
                  return;
              }

              evt.HitVolume.BodyOffset = null;
              evt.HitVolume.BodyRadiusScale = 1f;
              evt.HitVolume.BodyRadiusAdd = 0f;
              evt.HitVolume.BodyHeightScale = 1f;
              evt.HitVolume.BodyHeightAdd = 0f;
              evt.HitVolume.StartSocketId = 0;
              evt.HitVolume.EndSocketId = 0;
              evt.HitVolume.ShapeKind = string.IsNullOrWhiteSpace(evt.HitVolume.ShapeKind)
                  ? CharacterActionHitVolumeShapeKind.Capsule.ToString()
                  : evt.HitVolume.ShapeKind;
            evt.HitVolume.LocalPointA = evt.HitVolume.LocalPointA ?? new CharacterActionHitVolumePointJsonDto();
            evt.HitVolume.LocalPointB = evt.HitVolume.LocalPointB ?? new CharacterActionHitVolumePointJsonDto();
            evt.HitVolume.Radius = Math.Max(0f, evt.HitVolume.Radius);
            if (evt.HitVolume.Radius <= 0f
                && evt.HitVolume.LocalPointA == null
                && evt.HitVolume.LocalPointB == null)
            {
                evt.HitVolume = null;
            }
        }

        private static void NormalizeHitEventFields(GameplayTrackEventJsonDto evt)
        {
            if (evt == null)
                return;

            if (evt.HitEvent == null)
                return;

            evt.HitEvent.TargetTypes = NormalizeStringArray(evt.HitEvent.TargetTypes);
            evt.HitEvent.EnvironmentHitPolicy = evt.HitEvent.EnvironmentHitPolicy ?? string.Empty;
            evt.HitEvent.StableEventId = evt.HitEvent.StableEventId;
            evt.HitEvent.MaxAcceptedHits = Math.Max(0, evt.HitEvent.MaxAcceptedHits);
            evt.HitEvent.HitSameTargetPolicy = evt.HitEvent.HitSameTargetPolicy ?? string.Empty;
            evt.HitEvent.OnHitBuffs = CharacterActionHitEventAuthoringModel.NormalizeOnHitBuffs(evt.HitEvent.OnHitBuffs);
            if (evt.HitEvent.Damage != null)
            {
                evt.HitEvent.Damage.DamageTypeLabel = CharacterActionDamageIdentityAuthoring.Label(409, evt.HitEvent.Damage.DamageTypeId, evt.HitEvent.Damage.DamageTypeLabel);
                evt.HitEvent.Damage.FormulaLabel = CharacterActionDamageIdentityAuthoring.FormulaLabel(evt.HitEvent.Damage.FormulaId, evt.HitEvent.Damage.FormulaLabel);
                evt.HitEvent.Damage.BaseDamage = Math.Max(0, evt.HitEvent.Damage.BaseDamage);
                evt.HitEvent.Damage.SourceAttributeScales =
                    CharacterActionHitEventAuthoringModel.NormalizeDamageAttributeScales(evt.HitEvent.Damage.SourceAttributeScales);
                if (evt.HitEvent.Damage.SourceAttributeScales.Length > 0
                    && evt.HitEvent.Damage.FormulaId == 0)
                {
                    evt.HitEvent.Damage.FormulaId = 4;
                    evt.HitEvent.Damage.FormulaLabel = CharacterActionDamageAuthoringText.DefaultDamageFormulaId;
                }
                evt.HitEvent.Damage.ImpactPower = Math.Max(0, evt.HitEvent.Damage.ImpactPower);
                evt.HitEvent.Damage.LaunchPower = Math.Max(0, evt.HitEvent.Damage.LaunchPower);
                evt.HitEvent.Damage.Tags = CharacterActionDamageIdentityAuthoring.NormalizeIds(evt.HitEvent.Damage.Tags);
                evt.HitEvent.Damage.TagLabels = CharacterActionDamageIdentityAuthoring.TagLabels(evt.HitEvent.Damage.Tags, evt.HitEvent.Damage.TagLabels);
                evt.HitEvent.Damage.AnalysisCategoryLabel = CharacterActionDamageIdentityAuthoring.Label(411, evt.HitEvent.Damage.AnalysisCategory, evt.HitEvent.Damage.AnalysisCategoryLabel);
            }
        }

        private static CharacterActionHitEventJsonDto CreateDefaultWeaponHitEvent(int stableEventId)
        {
            return new CharacterActionHitEventJsonDto
            {
                TargetTypes = new[] { "HostileCharacter" },
                EnvironmentHitPolicy = "None",
                StableEventId = stableEventId,
                MaxAcceptedHits = 1,
                HitSameTargetPolicy = "AllowRepeat",
                Damage = new CharacterActionHitDamageJsonDto
                {
                    DamageTypeId = CharacterContentIdentityAuthoringCatalog.GetExisting(409, CharacterActionDamageAuthoringText.DefaultDamageTypeId),
                    FormulaId = 4,
                    FormulaLabel = CharacterActionDamageAuthoringText.DefaultDamageFormulaId,
                    DamageTypeLabel = CharacterActionDamageAuthoringText.DefaultDamageTypeId,
                    BaseDamage = CharacterActionDamageAuthoringText.BaselineLightAttackBaseDamage,
                    SourceAttributeScales = CharacterActionHitEventAuthoringModel.BuildAttackScaleArray(
                        CharacterActionDamageAuthoringText.BaselineLightAttackAttackScale),
                    ImpactPower = CharacterActionDamageAuthoringText.BaselineLightAttackImpactPower,
                    Tags = CharacterActionDamageIdentityAuthoring.ExistingTags(new[] { "melee", "weapon", "light", "scatter" }),
                    TagLabels = new[] { "melee", "weapon", "light", "scatter" },
                    AnalysisCategory = CharacterActionDamageIdentityAuthoring.ExistingId(411, "BasicAttack"),
                    AnalysisCategoryLabel = "BasicAttack",
                },
            };
        }

        public static AnimationTrackJsonDto NormalizeAnimationTrack(AnimationTrackJsonDto track)
        {
            if (track == null)
                return null;

            track.Events = track.Events ?? Array.Empty<AnimationTrackEventJsonDto>();
            for (int i = 0; i < track.Events.Length; i++)
            {
                AnimationTrackEventJsonDto evt = track.Events[i] ?? new AnimationTrackEventJsonDto();
                evt.Kind = string.IsNullOrWhiteSpace(evt.Kind)
                    ? CharacterActionTrackEventKind.CrossFadeAnimation.ToString()
                    : evt.Kind;
                evt.AnimationActionKey = evt.AnimationActionKey ?? string.Empty;
                evt.AnimationClipResourceKey = evt.AnimationClipResourceKey ?? string.Empty;
                evt.AnimationStartOffsetFrames = Math.Max(0, evt.AnimationStartOffsetFrames);
                evt.AnimationEndOffsetFrames = Math.Max(0, evt.AnimationEndOffsetFrames);
                evt.StableEventId = evt.StableEventId;
                evt.PlaybackSpeed = CharacterActionAnimationTimelineResolver.ClampPlaybackSpeed(
                    evt.PlaybackSpeed);
                if (evt.EndFrame < evt.Frame)
                    evt.EndFrame = evt.Frame;
                track.Events[i] = evt;
            }

            return track;
        }

        public static PresentationTrackJsonDto NormalizePresentationTrack(PresentationTrackJsonDto track)
        {
            if (track == null)
                return null;

            track.Events = track.Events ?? Array.Empty<PresentationTrackEventJsonDto>();
            for (int i = 0; i < track.Events.Length; i++)
            {
                PresentationTrackEventJsonDto evt = track.Events[i] ?? new PresentationTrackEventJsonDto();
                evt.Kind = string.IsNullOrWhiteSpace(evt.Kind)
                    ? CharacterActionTrackEventKind.PlayAudioCue.ToString()
                    : evt.Kind;
                evt.CueId = evt.CueId;
                evt.ResourceKey = evt.ResourceKey ?? string.Empty;
                evt.VfxAttachment = MxAttachmentTransform.Normalize(evt.VfxAttachment);
                evt.VfxPlaybackMode = string.IsNullOrWhiteSpace(evt.VfxPlaybackMode)
                    ? CharacterActionVfxPlaybackMode.PlayToCompletion.ToString()
                    : evt.VfxPlaybackMode;
                evt.VfxPlaybackRate = Clamp(
                    evt.VfxPlaybackRate,
                    CharacterActionVfxEventConfig.MinPlaybackRate,
                    CharacterActionVfxEventConfig.MaxPlaybackRate,
                    1f);
                evt.VfxScaleMultiplier = Clamp(
                    evt.VfxScaleMultiplier,
                    CharacterActionVfxEventConfig.MinScaleMultiplier,
                    CharacterActionVfxEventConfig.MaxScaleMultiplier,
                    1f);
                evt.VfxStartOffsetFrames = Math.Max(0, evt.VfxStartOffsetFrames);
                evt.VfxEndOffsetFrames = Math.Max(0, evt.VfxEndOffsetFrames);
                evt.VfxNaturalEndPolicy = string.IsNullOrWhiteSpace(evt.VfxNaturalEndPolicy)
                    ? CharacterActionVfxStopPolicy.Complete.ToString()
                    : evt.VfxNaturalEndPolicy;
                evt.StableEventId = evt.StableEventId;
                if (evt.EndFrame < evt.Frame)
                    evt.EndFrame = evt.Frame;
                track.Events[i] = evt;
            }

            return track;
        }

        private static T Clone<T>(T value)
        {
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value, CharacterActionAuthoringJsonService.JsonSettings));
        }

        private static RootMotionTrackJsonDto CreateDefaultLightAttackRootMotion()
        {
            return new RootMotionTrackJsonDto
            {
                SourceAnimationEventId = CharacterContentIdentityAuthoringCatalog.GetOrAllocate(401, "light_attack.animation"),
                SourceAnimationClipResourceKey = "art.character.skeleton.animation.katana_atk1",
                Space = CharacterActionRootMotionSpace.Local.ToString(),
                SampleTickRate = CharacterActionAnimationKeyUtility.TimelineFramesPerSecond,
                Frames = new[]
                {
                    new RootMotionFrameJsonDto { Frame = 0, X = 0f, Y = 0f, Z = 0f },
                    new RootMotionFrameJsonDto { Frame = 1, X = -0.00246f, Y = 0f, Z = 0.00096f },
                    new RootMotionFrameJsonDto { Frame = 2, X = -0.00332f, Y = 0f, Z = 0.00008f },
                    new RootMotionFrameJsonDto { Frame = 3, X = -0.00332f, Y = 0f, Z = 0.00008f },
                    new RootMotionFrameJsonDto { Frame = 4, X = -0.00337f, Y = 0f, Z = 0.00005f },
                    new RootMotionFrameJsonDto { Frame = 5, X = -0.00422f, Y = 0f, Z = -0.00050f },
                    new RootMotionFrameJsonDto { Frame = 6, X = -0.00422f, Y = 0f, Z = -0.00050f },
                    new RootMotionFrameJsonDto { Frame = 7, X = -0.00422f, Y = 0f, Z = -0.00047f },
                    new RootMotionFrameJsonDto { Frame = 8, X = -0.00416f, Y = 0f, Z = -0.00017f },
                    new RootMotionFrameJsonDto { Frame = 9, X = -0.00358f, Y = 0f, Z = 0.00004f },
                },
            };
        }
    }

    public sealed class CharacterActionAuthoringReferenceContext :
        ICharacterActionAnimationActionKeyProvider,
        ICharacterActionCombatActionIdProvider,
        ICharacterActionPresentationResourceProvider,
        ICharacterActionNumericPresentationResourceProvider,
        ICharacterActionEquipmentTagProvider,
        ICharacterActionCharacterEquipmentStateProvider
    {
        public List<string> AnimationActionKeys { get; } = new List<string>();
        public List<int> CombatActionIds { get; } = new List<int>();
        public List<string> PresentationResourceKeys { get; } = new List<string>();
        public List<int> AudioCueIds { get; } = new List<int>();
        public List<string> CharacterStableIds { get; } = new List<string>();
        public List<string> EquipmentStateStableIds { get; } = new List<string>();
        public List<int> EquipmentSlotIds { get; } = new List<int>();
        public List<string> EquipmentTags { get; } = new List<string>();
        public static CharacterActionAuthoringReferenceContext CreateDefault()
        {
            var context = new CharacterActionAuthoringReferenceContext();
            context.AddAnimationActionKey("anim.basic_jump");
            context.AddAnimationActionKey("combat.attack.light");
            context.AddAnimationActionKey("art.character.skeleton.animation.katana_atk1");
            context.AddAnimationActionKey("art.character.skeleton.animation.damage_stun01_start");
            context.AddAnimationActionKey("art.character.skeleton.animation.damage_high_f");
            context.AddAnimationActionKey("art.character.skeleton.animation.damage_mid_f");
            context.AddAnimationActionKey("art.character.skeleton.animation.damage_low_l1");
            context.AddAnimationActionKey("art.character.skeleton.animation.damage_strong_l_head");
            context.AddAnimationActionKey("art.character.skeleton.animation.damage_strong_l_chest");
            context.AddAnimationActionKey("art.character.skeleton.animation.damage_strong_l_foot");
            context.AddAnimationActionKey("art.character.skeleton.animation.damage_ko_mid_2");
            context.AddPresentationResourceKey("vfx.light_attack");
            context.AddAudioCueId(AudioCueIdentityAuthoringCatalog.GetExisting("sfx.light_attack"));
            context.AddEquipmentSlotId(CharacterContentIdentityAuthoringCatalog.GetExisting(405, "mainHand"));
            context.AddEquipmentSlotId(CharacterContentIdentityAuthoringCatalog.GetExisting(405, "offHand"));
            context.AddEquipmentTag("weapon.light");
            context.Normalize();
            return context;
        }

        public static CharacterActionAuthoringReferenceContext CreateFromModel(CharacterActionAuthoringModel model)
        {
            CharacterActionAuthoringReferenceContext context = CreateDefault();
            context.MergeFromModel(model);
            context.Normalize();
            return context;
        }

        public CharacterActionValidationOptions CreateValidationOptions()
        {
            Normalize();
            return new CharacterActionValidationOptions(
                CharacterActionMissingAnimationActionKeyPolicy.Error,
                new ICharacterActionCrossModuleValidator[]
                {
                    new CharacterActionAnimationActionKeyValidator(this),
                    new CharacterActionCombatActionIdValidator(this),
                    new CharacterActionPresentationResourceValidator(this),
                    new CharacterActionEquipmentTagRequirementValidator(this),
                    new CharacterActionCharacterEquipmentStateValidator(this),
                });
        }

        public void MergeFromModel(CharacterActionAuthoringModel model)
        {
            if (model == null)
                return;

            CharacterActionSetJsonDto set = model.ActionSet;
            if (set != null)
            {
                AddCharacterStableId(set.CharacterStableId);
                AddEquipmentStateStableId(set.EquipmentStateStableId);
            }

            for (int i = 0; i < model.Actions.Count; i++)
            {
                CharacterActionJsonDto action = model.Actions[i];
                if (action == null)
                    continue;

                AddAnimationReferences(action.AnimationTrack);
                AddCombatReferences(action.CombatTrack);
                AddGameplayReferences(action.GameplayTrack);
                AddPresentationReferences(action.PresentationTrack);
                AddRequirementReferences(action.Requirements);
                AddAbilityTagReferences(set != null ? set.AbilityBindings : null);
            }
        }

        public void MergeFromReferenceContext(CharacterActionAuthoringReferenceContext context)
        {
            if (context == null)
                return;

            AddMany(AnimationActionKeys, context.AnimationActionKeys.ToArray());
            for (int i = 0; i < context.CombatActionIds.Count; i++) AddCombatActionId(context.CombatActionIds[i]);
            AddMany(PresentationResourceKeys, context.PresentationResourceKeys.ToArray());
            foreach (int id in context.AudioCueIds) AddAudioCueId(id);
            AddMany(CharacterStableIds, context.CharacterStableIds.ToArray());
            AddMany(EquipmentStateStableIds, context.EquipmentStateStableIds.ToArray());
            foreach (int id in context.EquipmentSlotIds) AddEquipmentSlotId(id);
            AddMany(EquipmentTags, context.EquipmentTags.ToArray());
        }

        public void MergeFromAnimationClipRegistry(MxAnimationClipRegistryAsset asset)
        {
            if (asset == null)
                return;

            MxAnimationClipRegistryBindingEntry[] bindings = asset.Bindings;
            for (int i = 0; i < bindings.Length; i++)
                AddAnimationActionKey(bindings[i].ResolveActionKey());
        }

        public void MergeFromCombatActionSet(CombatActionSetConfig combatActionSet)
        {
            if (combatActionSet == null)
                return;

            CombatActionEntry[] actions = combatActionSet.Actions;
            for (int i = 0; i < actions.Length; i++)
            {
                CombatActionEntry action = actions[i];
                AddCombatActionId(action.CombatActionId.Value);
                AddAnimationActionKey(action.AnimationActionKey);
            }
        }

        public void MergeFromPresentationProfile(CharacterPresentationProfileConfig presentationProfile)
        {
            if (presentationProfile == null)
                return;

            CharacterResourceKeyEntry[] resources = presentationProfile.ResourceKeys;
            for (int i = 0; i < resources.Length; i++)
                AddPresentationResourceKey(resources[i].Id);
        }

        public void AddAnimationActionKey(string value)
        {
            AddUnique(AnimationActionKeys, value);
        }

        public void AddCombatActionId(int value)
        {
            if (value > 0 && !CombatActionIds.Contains(value)) CombatActionIds.Add(value);
        }

        public void AddPresentationResourceKey(string value)
        {
            AddUnique(PresentationResourceKeys, value);
        }

        public void AddAudioCueId(int value)
        {
            if (value > 0 && !AudioCueIds.Contains(value)) AudioCueIds.Add(value);
        }

        public void AddCharacterStableId(string value)
        {
            AddUnique(CharacterStableIds, value);
        }

        public void AddEquipmentStateStableId(string value)
        {
            AddUnique(EquipmentStateStableIds, value);
        }

        public void AddEquipmentSlotId(int value)
        {
            if (value > 0 && !EquipmentSlotIds.Contains(value)) EquipmentSlotIds.Add(value);
        }

        public void AddEquipmentTag(string value)
        {
            AddUnique(EquipmentTags, value);
        }

        public bool ContainsAnimationActionKey(
            string actionSetStableId,
            CharacterActionId actionId,
            string animationActionKey)
        {
            return Contains(AnimationActionKeys, animationActionKey);
        }

        public bool ContainsCombatActionId(
            string actionSetStableId,
            CharacterActionId actionId,
            int combatActionId)
        {
            return CombatActionIds.Contains(combatActionId);
        }

        public bool ContainsPresentationResource(
            string actionSetStableId,
            CharacterActionId actionId,
            CharacterActionResourceDependencyKind dependencyKind,
            string stableId)
        {
            if (dependencyKind == CharacterActionResourceDependencyKind.AudioCue) return false;

            return Contains(PresentationResourceKeys, stableId);
        }

        public bool ContainsEquipmentTag(string actionSetStableId, string equipmentTag)
        {
            return Contains(EquipmentTags, equipmentTag);
        }

        public bool ContainsCharacterStableId(string characterStableId)
        {
            return Contains(CharacterStableIds, characterStableId);
        }

        public bool ContainsEquipmentStateStableId(string characterStableId, string equipmentStateStableId)
        {
            return Contains(EquipmentStateStableIds, equipmentStateStableId);
        }

        public bool ContainsEquipmentSlotId(int equipmentSlotId)
        {
            return EquipmentSlotIds.Contains(equipmentSlotId);
        }

        public bool ContainsNumericPresentationResource(MxFramework.CharacterAction.CharacterActionId actionId,
            CharacterActionResourceDependencyKind dependencyKind, int id) =>
            dependencyKind == CharacterActionResourceDependencyKind.AudioCue && AudioCueIds.Contains(id);

        public void Normalize()
        {
            NormalizeList(AnimationActionKeys);
            CombatActionIds.Sort();
            NormalizeList(PresentationResourceKeys);
            AudioCueIds.Sort();
            NormalizeList(CharacterStableIds);
            NormalizeList(EquipmentStateStableIds);
            EquipmentSlotIds.Sort();
            NormalizeList(EquipmentTags);
        }

        private void AddAnimationReferences(AnimationTrackJsonDto track)
        {
            if (track?.Events == null)
                return;

            for (int i = 0; i < track.Events.Length; i++)
            {
                AnimationTrackEventJsonDto evt = track.Events[i];
                if (evt == null)
                    continue;
                AddAnimationActionKey(CharacterActionAnimationKeyUtility.ResolveEffectiveKey(
                    evt.AnimationClipResourceKey,
                    evt.AnimationActionKey));
            }
        }

        private void AddCombatReferences(CombatTrackJsonDto track)
        {
            if (track == null)
                return;

            AddCombatActionId(track.CombatActionId);
            CombatTrackEventJsonDto[] events = track.Events ?? Array.Empty<CombatTrackEventJsonDto>();
            for (int i = 0; i < events.Length; i++)
            {
                AddCombatActionId(events[i]?.CombatActionId ?? 0);
                AddEquipmentSlotId(events[i]?.EquipmentSlotId ?? 0);
            }
        }

        private void AddGameplayReferences(GameplayTrackJsonDto track)
        {
        }

        private void AddPresentationReferences(PresentationTrackJsonDto track)
        {
            if (track?.Events == null)
                return;

            for (int i = 0; i < track.Events.Length; i++)
            {
                PresentationTrackEventJsonDto evt = track.Events[i];
                if (evt == null)
                    continue;
                AddAudioCueId(evt.CueId);
                AddPresentationResourceKey(evt.ResourceKey);
            }
        }

        private void AddRequirementReferences(CharacterActionRequirementJsonDto[] requirements)
        {
            requirements = requirements ?? Array.Empty<CharacterActionRequirementJsonDto>();
            for (int i = 0; i < requirements.Length; i++)
            {
                CharacterActionRequirementJsonDto requirement = requirements[i];
                if (requirement == null)
                    continue;
                if (string.Equals(requirement.Kind, CharacterActionRequirementKind.EquipmentTagRequired.ToString(), StringComparison.Ordinal)
                    || string.Equals(requirement.Kind, CharacterActionRequirementKind.EquipmentTagForbidden.ToString(), StringComparison.Ordinal))
                {
                    AddEquipmentTag(requirement.StableId);
                }
            }
        }

        private void AddAbilityTagReferences(CharacterAbilityActionBindingJsonDto[] abilityBindings)
        {
            abilityBindings = abilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            for (int i = 0; i < abilityBindings.Length; i++)
            {
                CharacterAbilityActionBindingJsonDto binding = abilityBindings[i];
                if (binding == null)
                    continue;
                AddMany(EquipmentTags, binding.RequiredTags);
                AddMany(EquipmentTags, binding.ForbiddenTags);
            }
        }

        private static void AddMany(List<string> list, string[] values)
        {
            values = values ?? Array.Empty<string>();
            for (int i = 0; i < values.Length; i++)
                AddUnique(list, values[i]);
        }

        private static bool Contains(List<string> list, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return list.Contains(value.Trim());
        }

        private static void AddUnique(List<string> list, string value)
        {
            if (list == null || string.IsNullOrWhiteSpace(value))
                return;

            value = value.Trim();
            if (!list.Contains(value))
                list.Add(value);
        }

        private static void NormalizeList(List<string> list)
        {
            if (list == null)
                return;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                string value = list[i]?.Trim() ?? string.Empty;
                if (value.Length == 0 || !seen.Add(value))
                    list.RemoveAt(i);
                else
                    list[i] = value;
            }

            list.Sort(StringComparer.Ordinal);
        }
    }

}
