using MxFramework.Runtime;
using System;

namespace MxFramework.CharacterAction
{
    /// <summary>Caller-owned storage for reaction execution events.</summary>
    /// <remarks>
    /// Allocates during construction and capacity growth. Repeated reaction requests are NoAlloc after the selected
    /// action/trace pair and event capacity have been warmed.
    /// </remarks>
    public sealed class CharacterActionReactionStartBuffer
    {
        public CharacterActionReactionStartBuffer(int eventCapacity = 20)
        {
            Events = new CharacterActionRunnerEventBuffer(Math.Max(0, eventCapacity));
        }

        internal CharacterActionRunnerEventBuffer Events { get; }

        /// <remarks>NoAlloc when the requested capacity does not grow.</remarks>
        public void EnsureCapacity(int eventCapacity)
        {
            Events.EnsureCapacity(Math.Max(0, eventCapacity));
        }
    }

    /// <summary>Result of the single warmed CharacterAction reaction path.</summary>
    /// <remarks>NoAlloc value type. Execution events live in the caller-owned reaction buffer.</remarks>
    public readonly struct CharacterActionReactionStartResult
    {
        internal CharacterActionReactionStartResult(
            CharacterActionResolveResult resolveResult,
            CharacterActionExecutionResult execution,
            bool hasExecution,
            bool started,
            bool fallbackRejected,
            bool fallbackUsed,
            CharacterActionId selectedActionId,
            CharacterActionDiagnosticCode diagnosticCode,
            string diagnosticMessage)
        {
            ResolveResult = resolveResult;
            Execution = execution;
            HasExecution = hasExecution;
            Started = started;
            FallbackRejected = fallbackRejected;
            FallbackUsed = fallbackUsed;
            SelectedActionId = selectedActionId;
            DiagnosticCodeId = diagnosticCode;
            DiagnosticMessage = diagnosticMessage ?? string.Empty;
        }

        public CharacterActionResolveResult ResolveResult { get; }
        public CharacterActionExecutionResult Execution { get; }
        public bool HasExecution { get; }
        public bool Started { get; }
        public bool FallbackRejected { get; }
        public bool FallbackUsed { get; }
        public CharacterActionId SelectedActionId { get; }
        public CharacterActionDiagnosticCode DiagnosticCodeId { get; }
        public string DiagnosticCode => CharacterActionDiagnosticCodeText.Get(DiagnosticCodeId);
        public string DiagnosticMessage { get; }
    }

    /// <summary>Resolves and executes reactions through the sole CharacterAction authority.</summary>
    public static class CharacterActionReactionRuntime
    {
        /// <summary>Resolves and starts a reaction using cached plans and caller-owned storage.</summary>
        /// <remarks>
        /// NoAlloc after the reaction profile, selected action/trace pair, and event buffer have been warmed.
        /// Normal reactions obey interrupt rules. Death and lifecycle reactions use atomic lifecycle replacement.
        /// </remarks>
        public static CharacterActionReactionStartResult TryStart(
            CharacterActionResolverContext resolverContext,
            CharacterReactionContext reactionContext,
            CharacterActionExecutionGateway gateway,
            CharacterActionRuntimePlanCache planCache,
            CharacterActionReactionStartBuffer buffer,
            bool allowFallback = true)
        {
            if (resolverContext == null)
                throw new ArgumentNullException(nameof(resolverContext));
            if (gateway == null)
                throw new ArgumentNullException(nameof(gateway));
            if (planCache == null)
                throw new ArgumentNullException(nameof(planCache));
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            RuntimeTraceIdentity traceId = reactionContext.TraceId;
            if (resolverContext.ActionSet == null)
            {
                return Rejected(
                    null,
                    false,
                    false,
                    CharacterActionId.None,
                    CharacterActionDiagnosticCode.MissingActionSet,
                    string.Empty);
            }

            CharacterReactionProfile profile = FindReactionProfile(
                resolverContext,
                resolverContext.ActionSet.ReactionProfileId);
            if (profile == null)
            {
                return Rejected(
                    null,
                    false,
                    false,
                    CharacterActionId.None,
                    CharacterActionDiagnosticCode.MissingReactionProfile,
                    string.Empty);
            }

            CharacterReactionSelectionFastResult selection =
                CharacterReactionSelector.SelectNoAlloc(profile, reactionContext);
            if (!selection.Accepted)
            {
                return Rejected(
                    null,
                    false,
                    false,
                    selection.SelectedActionId,
                    selection.RejectCodeId == CharacterActionDiagnosticCode.None
                        ? CharacterActionDiagnosticCode.ReactionContextIncomplete
                        : selection.RejectCodeId,
                    string.Empty);
            }

            if (!allowFallback && selection.FallbackUsed)
            {
                return Rejected(
                    null,
                    true,
                    true,
                    selection.SelectedActionId,
                    CharacterActionDiagnosticCode.ReactionFallbackUsed,
                    string.Empty);
            }

            if (!planCache.TryGetOrCreate(selection.SelectedActionId, traceId, out CharacterActionRuntimePlanEntry entry)
                || entry == null)
            {
                return Rejected(
                    null,
                    false,
                    selection.FallbackUsed,
                    selection.SelectedActionId,
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    string.Empty);
            }

            CharacterActionDiagnostic diagnostic = FirstDiagnostic(entry.Diagnostics);
            if (!entry.IsSuccess)
            {
                return Rejected(
                    entry.ResolveResult,
                    false,
                    selection.FallbackUsed,
                    selection.SelectedActionId,
                    diagnostic.CodeId != CharacterActionDiagnosticCode.None
                        ? diagnostic.CodeId
                        : CharacterActionDiagnosticCodeText.FromRejectReason(entry.RejectReason),
                    diagnostic.Message);
            }

            if (entry.Action.Category != CharacterActionCategory.Reaction)
            {
                return Rejected(
                    entry.ResolveResult,
                    false,
                    selection.FallbackUsed,
                    selection.SelectedActionId,
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    string.Empty);
            }

            int eventCapacity = entry.Definition == null || entry.Definition.TrackEvents == null
                ? 12
                : Math.Max(12, entry.Definition.TrackEvents.Length + 8);
            buffer.EnsureCapacity(eventCapacity);

            CharacterActionExecutionRequest request = IsLifecycleReplacement(reactionContext.SourceKind)
                ? CharacterActionExecutionRequest.ReplaceForLifecycle(
                    entry.ResolveResult,
                    entry.Definition,
                    ResolveLifecycleReason(reactionContext),
                    traceId)
                : CharacterActionExecutionRequest.React(
                    entry.ResolveResult,
                    entry.Definition,
                    MapReactionSource(reactionContext.SourceKind),
                    entry.Plan.Priority,
                    traceId);
            CharacterActionExecutionResult execution = gateway.Execute(in request, buffer.Events);
            diagnostic = FirstDiagnostic(execution.Diagnostics);
            return new CharacterActionReactionStartResult(
                entry.ResolveResult,
                execution,
                hasExecution: true,
                started: execution.Accepted,
                fallbackRejected: false,
                fallbackUsed: selection.FallbackUsed,
                selectedActionId: selection.SelectedActionId,
                diagnosticCode: diagnostic.CodeId,
                diagnosticMessage: diagnostic.Message);
        }

        public static bool UsesReactionFallback(CharacterActionResolveResult resolve)
        {
            if (resolve == null || resolve.Diagnostics == null)
                return false;

            CharacterActionDiagnostic[] diagnostics = resolve.Diagnostics;
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].CodeId == CharacterActionDiagnosticCode.ReactionFallbackUsed)
                {
                    return true;
                }
            }

            return false;
        }

        private static CharacterActionReactionStartResult Rejected(
            CharacterActionResolveResult resolveResult,
            bool fallbackRejected,
            bool fallbackUsed,
            CharacterActionId selectedActionId,
            CharacterActionDiagnosticCode diagnosticCode,
            string diagnosticMessage)
        {
            return new CharacterActionReactionStartResult(
                resolveResult,
                default,
                hasExecution: false,
                started: false,
                fallbackRejected: fallbackRejected,
                fallbackUsed: fallbackUsed,
                selectedActionId: selectedActionId,
                diagnosticCode: diagnosticCode,
                diagnosticMessage: diagnosticMessage);
        }

        private static CharacterActionSourceKind MapReactionSource(CharacterReactionContextSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case CharacterReactionContextSourceKind.CohesionBreak:
                    return CharacterActionSourceKind.CohesionBreak;
                case CharacterReactionContextSourceKind.GuardBreak:
                    return CharacterActionSourceKind.GuardBreak;
                case CharacterReactionContextSourceKind.ArmorBreak:
                    return CharacterActionSourceKind.ArmorBreak;
                case CharacterReactionContextSourceKind.Hit:
                    return CharacterActionSourceKind.Hit;
                case CharacterReactionContextSourceKind.Death:
                case CharacterReactionContextSourceKind.Lifecycle:
                    return CharacterActionSourceKind.Death;
                default:
                    return CharacterActionSourceKind.Reaction;
            }
        }

        private static bool IsLifecycleReplacement(CharacterReactionContextSourceKind sourceKind)
        {
            return sourceKind == CharacterReactionContextSourceKind.Death
                || sourceKind == CharacterReactionContextSourceKind.Lifecycle;
        }

        private static CharacterActionLifecycleReason ResolveLifecycleReason(CharacterReactionContext context)
        {
            if (context.IsDeath || context.SourceKind == CharacterReactionContextSourceKind.Death)
                return CharacterActionLifecycleReason.Death;

            return CharacterActionLifecycleReason.ReviveReset;
        }

        private static CharacterActionDiagnostic FirstDiagnostic(CharacterActionDiagnostic[] diagnostics)
        {
            return diagnostics == null || diagnostics.Length == 0 ? default : diagnostics[0];
        }

        private static CharacterReactionProfile FindReactionProfile(
            CharacterActionResolverContext context,
            string profileId)
        {
            CharacterReactionProfile[] profiles = context.ReactionProfiles ?? Array.Empty<CharacterReactionProfile>();
            for (int i = 0; i < profiles.Length; i++)
            {
                CharacterReactionProfile profile = profiles[i];
                if (profile != null && string.Equals(profile.StableId, profileId, StringComparison.Ordinal))
                    return profile;
            }

            return null;
        }
    }
}
