using System;
using MxFramework.Combat.Animation;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterAction
{
    public readonly struct CharacterActionIntentRequest : IEquatable<CharacterActionIntentRequest>
    {
        public CharacterActionIntentRequest(
            GameplayEntityId entity,
            string intentId,
            int? abilityId,
            string abilityStableId,
            string requestedActionId,
            CharacterActionSourceKind sourceKind,
            int priority,
            RuntimeFrame frame,
            string traceId)
        {
            if (!Enum.IsDefined(typeof(CharacterActionSourceKind), sourceKind))
                throw new ArgumentOutOfRangeException(nameof(sourceKind), "Action source kind is not defined.");

            Entity = entity;
            IntentId = intentId ?? string.Empty;
            AbilityId = abilityId;
            AbilityStableId = abilityStableId ?? string.Empty;
            RequestedActionId = requestedActionId ?? string.Empty;
            SourceKind = sourceKind;
            Priority = priority;
            Frame = frame;
            TraceId = traceId ?? string.Empty;
        }

        public GameplayEntityId Entity { get; }
        public string IntentId { get; }
        public int? AbilityId { get; }
        public string AbilityStableId { get; }
        public string RequestedActionId { get; }
        public CharacterActionSourceKind SourceKind { get; }
        public int Priority { get; }
        public RuntimeFrame Frame { get; }
        public string TraceId { get; }

        public bool Equals(CharacterActionIntentRequest other)
        {
            return Entity.Equals(other.Entity)
                && string.Equals(IntentId, other.IntentId, StringComparison.Ordinal)
                && AbilityId == other.AbilityId
                && string.Equals(AbilityStableId, other.AbilityStableId, StringComparison.Ordinal)
                && string.Equals(RequestedActionId, other.RequestedActionId, StringComparison.Ordinal)
                && SourceKind == other.SourceKind
                && Priority == other.Priority
                && Frame.Equals(other.Frame)
                && string.Equals(TraceId, other.TraceId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionIntentRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Entity.GetHashCode();
                hash = (hash * 397) ^ (IntentId == null ? 0 : IntentId.GetHashCode());
                hash = (hash * 397) ^ AbilityId.GetHashCode();
                hash = (hash * 397) ^ (AbilityStableId == null ? 0 : AbilityStableId.GetHashCode());
                hash = (hash * 397) ^ (RequestedActionId == null ? 0 : RequestedActionId.GetHashCode());
                hash = (hash * 397) ^ (int)SourceKind;
                hash = (hash * 397) ^ Priority;
                hash = (hash * 397) ^ Frame.GetHashCode();
                hash = (hash * 397) ^ (TraceId == null ? 0 : TraceId.GetHashCode());
                return hash;
            }
        }
    }

    public enum CharacterActionResolveStatus
    {
        Success = 0,
        Queued = 1,
        Rejected = 2,
    }

    public enum CharacterActionRejectReason
    {
        None = 0,
        MissingActionSet = 1,
        MissingActionBinding = 2,
        MissingAbilityBinding = 3,
        MissingActionConfig = 4,
        StateDisabled = 10,
        Dead = 11,
        ActionCommitted = 12,
        CooldownActive = 20,
        InsufficientResource = 21,
        PressureReactionLocked = 30,
        ReactionContextIncomplete = 31,
        InvalidTarget = 40,
        EquipmentStateMismatch = 41,
        LowerPriorityRejected = 50,
        CombatActionMissing = 60,
        AnimationActionMissing = 61,
        ResourceMissing = 62,
        PhaseAnchorInvalid = 70,
        CancelConflict = 80,
    }

    public sealed class CharacterActionResolveResult
    {
        private CharacterActionResolveResult(
            CharacterActionResolveStatus status,
            CharacterActionRejectReason rejectReason,
            CharacterActionPlan plan,
            CharacterActionDiagnostic[] diagnostics,
            string traceId)
        {
            if (!Enum.IsDefined(typeof(CharacterActionResolveStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status), "Resolve status is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionRejectReason), rejectReason))
                throw new ArgumentOutOfRangeException(nameof(rejectReason), "Reject reason is not defined.");
            if (status != CharacterActionResolveStatus.Rejected && plan == null)
                throw new ArgumentNullException(nameof(plan), "Successful or queued resolve results require a plan.");
            if (status == CharacterActionResolveStatus.Rejected && rejectReason == CharacterActionRejectReason.None)
                throw new ArgumentOutOfRangeException(nameof(rejectReason), "Rejected resolve results require a stable reject reason.");

            Status = status;
            RejectReason = rejectReason;
            Plan = plan;
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
            TraceId = traceId ?? plan?.TraceId ?? string.Empty;
        }

        public CharacterActionResolveStatus Status { get; }
        public CharacterActionRejectReason RejectReason { get; }
        public CharacterActionPlan Plan { get; }
        public CharacterActionDiagnostic[] Diagnostics { get; }
        public string TraceId { get; }
        public bool IsSuccess => Status == CharacterActionResolveStatus.Success;
        public bool IsQueued => Status == CharacterActionResolveStatus.Queued;
        public bool IsRejected => Status == CharacterActionResolveStatus.Rejected;

        public static CharacterActionResolveResult Success(CharacterActionPlan plan, CharacterActionDiagnostic[] diagnostics = null)
        {
            return new CharacterActionResolveResult(CharacterActionResolveStatus.Success, CharacterActionRejectReason.None, plan, diagnostics, plan?.TraceId);
        }

        public static CharacterActionResolveResult Queued(CharacterActionPlan plan, CharacterActionDiagnostic[] diagnostics = null)
        {
            return new CharacterActionResolveResult(CharacterActionResolveStatus.Queued, CharacterActionRejectReason.None, plan, diagnostics, plan?.TraceId);
        }

        public static CharacterActionResolveResult Rejected(
            CharacterActionRejectReason rejectReason,
            CharacterActionDiagnostic[] diagnostics = null,
            string traceId = "")
        {
            return new CharacterActionResolveResult(CharacterActionResolveStatus.Rejected, rejectReason, null, diagnostics, traceId);
        }
    }

    public sealed class CharacterActionPlan
    {
        public CharacterActionPlan(
            long planId,
            string actionId,
            CharacterActionCategory category,
            int priority,
            int durationFrames,
            CharacterActionPhase[] phases,
            CharacterActionTrackPlan[] tracks,
            string traceId)
        {
            if (planId < 0L)
                throw new ArgumentOutOfRangeException(nameof(planId), "Plan id cannot be negative.");
            if (!Enum.IsDefined(typeof(CharacterActionCategory), category))
                throw new ArgumentOutOfRangeException(nameof(category), "Action category is not defined.");
            if (durationFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(durationFrames), "Duration frames cannot be negative.");

            PlanId = planId;
            ActionId = actionId ?? string.Empty;
            Category = category;
            Priority = priority;
            DurationFrames = durationFrames;
            Phases = phases ?? Array.Empty<CharacterActionPhase>();
            Tracks = tracks ?? Array.Empty<CharacterActionTrackPlan>();
            TraceId = traceId ?? string.Empty;
        }

        public long PlanId { get; }
        public string ActionId { get; }
        public CharacterActionCategory Category { get; }
        public int Priority { get; }
        public int DurationFrames { get; }
        public CharacterActionPhase[] Phases { get; }
        public CharacterActionTrackPlan[] Tracks { get; }
        public string TraceId { get; }
    }

    public readonly struct CharacterActionTrackPlan : IEquatable<CharacterActionTrackPlan>
    {
        public CharacterActionTrackPlan(CharacterActionTrackKind kind, string configReferenceId, int eventCount)
        {
            if (!Enum.IsDefined(typeof(CharacterActionTrackKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), "Track kind is not defined.");
            if (eventCount < 0)
                throw new ArgumentOutOfRangeException(nameof(eventCount), "Track event count cannot be negative.");

            Kind = kind;
            ConfigReferenceId = configReferenceId ?? string.Empty;
            EventCount = eventCount;
        }

        public CharacterActionTrackKind Kind { get; }
        public string ConfigReferenceId { get; }
        public int EventCount { get; }

        public bool Equals(CharacterActionTrackPlan other)
        {
            return Kind == other.Kind
                && string.Equals(ConfigReferenceId, other.ConfigReferenceId, StringComparison.Ordinal)
                && EventCount == other.EventCount;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionTrackPlan other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ (ConfigReferenceId == null ? 0 : ConfigReferenceId.GetHashCode());
                hash = (hash * 397) ^ EventCount;
                return hash;
            }
        }

        public static CharacterActionTrackPlan[] FromConfig(CharacterActionConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            return new[]
            {
                new CharacterActionTrackPlan(CharacterActionTrackKind.Motion, config.StableId, config.MotionTrack.Events.Length),
                new CharacterActionTrackPlan(CharacterActionTrackKind.Combat, config.CombatTrack.CombatActionId, config.CombatTrack.Events.Length),
                new CharacterActionTrackPlan(CharacterActionTrackKind.Gameplay, config.StableId, config.GameplayTrack.Events.Length),
                new CharacterActionTrackPlan(CharacterActionTrackKind.Animation, config.StableId, config.AnimationTrack.Events.Length),
                new CharacterActionTrackPlan(CharacterActionTrackKind.Presentation, config.StableId, config.PresentationTrack.Events.Length),
                new CharacterActionTrackPlan(CharacterActionTrackKind.Debug, config.StableId, config.DebugTrack.Events.Length),
            };
        }
    }

    public readonly struct CharacterActionDurationPolicy
    {
        public CharacterActionDurationPolicy(int fallbackDurationFrames)
        {
            if (fallbackDurationFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(fallbackDurationFrames), "Fallback duration frames cannot be negative.");

            HasFallbackDuration = true;
            FallbackDurationFrames = fallbackDurationFrames;
        }

        public bool HasFallbackDuration { get; }
        public int FallbackDurationFrames { get; }
    }

    public enum CharacterActionPlanDurationSource
    {
        None = 0,
        CharacterActionConfig = 1,
        CombatActionTimeline = 2,
        FallbackPolicy = 3,
    }

    public readonly struct CharacterActionPlanDurationResult
    {
        public CharacterActionPlanDurationResult(
            bool resolved,
            int durationFrames,
            CharacterActionPlanDurationSource source,
            CharacterActionDiagnostic[] diagnostics)
        {
            if (durationFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(durationFrames), "Duration frames cannot be negative.");
            if (!Enum.IsDefined(typeof(CharacterActionPlanDurationSource), source))
                throw new ArgumentOutOfRangeException(nameof(source), "Duration source is not defined.");

            Resolved = resolved;
            DurationFrames = durationFrames;
            Source = source;
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
        }

        public bool Resolved { get; }
        public int DurationFrames { get; }
        public CharacterActionPlanDurationSource Source { get; }
        public CharacterActionDiagnostic[] Diagnostics { get; }
    }

    public static class CharacterActionPlanDurationResolver
    {
        public static CharacterActionPlanDurationResult Resolve(
            CharacterActionConfig config,
            CombatActionTimeline combatTimeline = null,
            CharacterActionDurationPolicy policy = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (config.TimelineAuthority == CharacterActionTimelineAuthority.CombatAnchored)
            {
                if (combatTimeline == null)
                {
                    return new CharacterActionPlanDurationResult(
                        false,
                        0,
                        CharacterActionPlanDurationSource.None,
                        new[]
                        {
                            CharacterActionDiagnostic.Error(
                                CharacterActionDiagnosticCodes.CombatActionMissing,
                                "CombatAnchored action duration requires a CombatActionTimeline."),
                        });
                }

                return new CharacterActionPlanDurationResult(
                    true,
                    combatTimeline.TotalFrames,
                    CharacterActionPlanDurationSource.CombatActionTimeline,
                    new[]
                    {
                        CharacterActionDiagnostic.Info(
                            CharacterActionDiagnosticCodes.ActionDurationResolvedFromCombat,
                            "Action duration resolved from CombatActionTimeline.TotalFrames."),
                    });
            }

            if (config.DurationFrames.HasValue)
            {
                return new CharacterActionPlanDurationResult(
                    true,
                    config.DurationFrames.Value,
                    CharacterActionPlanDurationSource.CharacterActionConfig,
                    new[]
                    {
                        CharacterActionDiagnostic.Info(
                            CharacterActionDiagnosticCodes.ActionDurationResolvedFromConfig,
                            "Action duration resolved from CharacterActionConfig.DurationFrames."),
                    });
            }

            if (policy.HasFallbackDuration)
            {
                return new CharacterActionPlanDurationResult(
                    true,
                    policy.FallbackDurationFrames,
                    CharacterActionPlanDurationSource.FallbackPolicy,
                    new[]
                    {
                        CharacterActionDiagnostic.Warning(
                            CharacterActionDiagnosticCodes.ActionDurationFallbackUsed,
                            "Action duration resolved from explicit fallback policy."),
                    });
            }

            return new CharacterActionPlanDurationResult(
                false,
                0,
                CharacterActionPlanDurationSource.None,
                new[]
                {
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.ActionDurationMissing,
                        "CharacterAuthored action duration requires DurationFrames or an explicit fallback policy."),
                });
        }
    }
}
