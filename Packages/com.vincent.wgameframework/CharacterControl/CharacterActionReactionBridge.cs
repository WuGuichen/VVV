using System;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterControl
{
    public readonly struct CharacterReactionHitMetadata
    {
        public CharacterReactionHitMetadata(
            GameplayEntityId targetEntityId,
            int reactionGroupId,
            CharacterHitDirection hitDirection = CharacterHitDirection.Unknown,
            bool isAirborne = false,
            CharacterActionId currentActionId = default,
            int? impactForce = null,
            int bodyPartId = 0,
            int hitZoneId = 0,
            int damageTypeId = 0,
            int sourceId = 0,
            string reason = "",
            RuntimeTraceIdentity traceId = default,
            PressureBand previousPressureBand = PressureBand.Stable,
            PressureBand currentScatterPressureBand = PressureBand.Stable,
            int previousPressure = 0,
            int currentScatterPressure = 0,
            int maxScatterPressure = 0,
            int delta = 0)
        {
            TargetEntityId = targetEntityId;
            ReactionGroupId = reactionGroupId;
            HitDirection = hitDirection;
            IsAirborne = isAirborne;
            CurrentActionId = currentActionId;
            ImpactForce = impactForce;
            BodyPartId = bodyPartId;
            HitZoneId = hitZoneId;
            DamageTypeId = damageTypeId;
            SourceId = sourceId;
            Reason = reason ?? string.Empty;
            TraceId = traceId;
            PreviousPressureBand = previousPressureBand;
            CurrentScatterPressureBand = currentScatterPressureBand;
            PreviousPressure = previousPressure;
            CurrentScatterPressure = currentScatterPressure;
            MaxScatterPressure = maxScatterPressure;
            Delta = delta;
        }

        public GameplayEntityId TargetEntityId { get; }

        public int ReactionGroupId { get; }

        public CharacterHitDirection HitDirection { get; }

        public bool IsAirborne { get; }

        public CharacterActionId CurrentActionId { get; }

        public int? ImpactForce { get; }

        public int BodyPartId { get; }

        public int HitZoneId { get; }

        public int DamageTypeId { get; }

        public int SourceId { get; }

        public string Reason { get; }

        public RuntimeTraceIdentity TraceId { get; }

        public PressureBand PreviousPressureBand { get; }

        public PressureBand CurrentScatterPressureBand { get; }

        public int PreviousPressure { get; }

        public int CurrentScatterPressure { get; }

        public int MaxScatterPressure { get; }

        public int Delta { get; }
    }

    public sealed class CharacterActionReactionBridge
    {
        private readonly CharacterActionController _controller;
        private readonly bool _allowFallback;

        public CharacterActionReactionBridge(
            CharacterActionController controller,
            bool allowFallback = true)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _allowFallback = allowFallback;
        }

        /// <remarks>LowFreqAlloc on reaction event frames; event subscription and ordinary hit filtering stay in the composition root.</remarks>
        public CharacterActionResult Apply(CohesionBreakEvent evt)
        {
            return Submit(CharacterReactionContextBuilder.FromCohesionBreak(evt));
        }

        /// <remarks>LowFreqAlloc on reaction event frames; event subscription stays in the composition root.</remarks>
        public CharacterActionResult Apply(GuardBreakEvent evt)
        {
            return Submit(CharacterReactionContextBuilder.FromGuardBreak(evt));
        }

        /// <remarks>LowFreqAlloc on reaction event frames; event subscription stays in the composition root.</remarks>
        public CharacterActionResult Apply(ArmorBreakEvent evt)
        {
            return Submit(CharacterReactionContextBuilder.FromArmorBreak(evt));
        }

        /// <remarks>LowFreqAlloc on reaction event frames; callers decide whether a band change should start an action-level reaction.</remarks>
        public CharacterActionResult Apply(PressureBandChangedEvent evt)
        {
            return Submit(CharacterReactionContextBuilder.FromPressureBandChanged(evt));
        }

        /// <remarks>LowFreqAlloc on reaction event frames; death fallback policy is controlled by the supplied start options.</remarks>
        public CharacterActionResult ApplyDeath(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            string reason = "",
            RuntimeTraceIdentity traceId = default)
        {
            return Submit(CharacterReactionContextBuilder.FromDeath(frame, entityId, reason, traceId));
        }

        /// <remarks>LowFreqAlloc on explicit hit-reaction event frames; ordinary damage does not call this method automatically.</remarks>
        public CharacterActionResult ApplyHit(
            DamageResolvedEvent evt,
            CharacterReactionHitMetadata metadata)
        {
            if (!evt.HitResult.IsAcceptedDamage || evt.FinalDamage <= 0)
            {
                return CharacterActionResult.Rejected(
                    CharacterActionRejectedReason.InvalidRequest,
                    "Hit reaction requires an accepted positive damage event.");
            }

            if (!metadata.TargetEntityId.IsValid)
            {
                return CharacterActionResult.Rejected(
                    CharacterActionRejectedReason.InvalidRequest,
                    "Hit reaction metadata must provide a target gameplay entity.");
            }

            DamageProfileUsage usage = evt.ProfileResult.Usage;
            if ((metadata.BodyPartId <= 0 && usage.BodyPartId > 0)
                || (metadata.HitZoneId <= 0 && usage.HitZoneId > 0)
                || (metadata.DamageTypeId <= 0 && evt.ProfileResult.DamageTypeId > 0))
            {
                return CharacterActionResult.Rejected(
                    CharacterActionRejectedReason.InvalidRequest,
                    "Hit reaction requires numeric bodyPartId, hitZoneId, and damageTypeId metadata.");
            }
            int? impactForce = metadata.ImpactForce.HasValue
                ? metadata.ImpactForce
                : ResolveImpactForce(evt.ProfileResult);
            var source = new CharacterReactionHitSource(
                new RuntimeFrame(evt.HitResult.Frame.Value),
                metadata.TargetEntityId,
                metadata.BodyPartId > 0 ? metadata.BodyPartId : 0,
                metadata.HitZoneId > 0 ? metadata.HitZoneId : 0,
                metadata.DamageTypeId > 0 ? metadata.DamageTypeId : 0,
                metadata.HitDirection,
                impactForce,
                metadata.ReactionGroupId,
                metadata.SourceId > 0 ? metadata.SourceId : usage.SourceEntityId,
                string.IsNullOrEmpty(metadata.Reason) ? "hit" : metadata.Reason,
                metadata.TraceId,
                metadata.PreviousPressureBand,
                metadata.CurrentScatterPressureBand,
                metadata.PreviousPressure,
                metadata.CurrentScatterPressure,
                metadata.MaxScatterPressure,
                metadata.Delta,
                isDeath: false,
                lifecycleState: string.Empty,
                metadata.IsAirborne,
                metadata.CurrentActionId,
                traceNumericId: evt.HitResult.TraceId);
            return Submit(CharacterReactionContextBuilder.FromHitSource(source));
        }

        private CharacterActionResult Submit(CharacterReactionContextBuildResult build)
        {
            if (!build.Success)
            {
                return CharacterActionResult.Rejected(
                    CharacterActionRejectedReason.InvalidRequest,
                    ResolveDiagnosticMessage(build.Diagnostics, "Reaction context could not be built."));
            }

            return _controller.SubmitReaction(build.Context, _allowFallback);
        }

        private static int? ResolveImpactForce(DamageProfileResolveResult profileResult)
        {
            if (profileResult.ImpactPower > 0)
                return profileResult.ImpactPower;
            if (profileResult.ImpactForce > 0)
                return profileResult.ImpactForce;
            return null;
        }

        private static string ResolveDiagnosticMessage(CharacterActionDiagnostic[] diagnostics, string fallback)
        {
            diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (!string.IsNullOrEmpty(diagnostics[i].Message))
                    return diagnostics[i].Message;
                if (!string.IsNullOrEmpty(diagnostics[i].Code))
                    return diagnostics[i].Code;
            }

            return fallback ?? string.Empty;
        }
    }
}
