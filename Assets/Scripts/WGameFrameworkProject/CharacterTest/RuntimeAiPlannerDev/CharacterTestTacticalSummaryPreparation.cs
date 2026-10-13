using System;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Stable preparation request captured from actual loaded providers, not UI text.</summary>
    internal sealed class CharacterTestTacticalSummaryRequest
    {
        internal CharacterTestTacticalSummaryRequest(int characterId, int actionId, string actionSet,
            CharacterTestCharacterEquipmentComponent equipment, string contentHash, CharacterActionSpatialMeasurement measurement, bool testActionOverride = false)
        { CharacterId = characterId; ActionId = actionId; ActionSetId = actionSet; Equipment = equipment; ContentHash = contentHash; Measurement = measurement; TestActionOverride = testActionOverride; }
        internal bool TestActionOverride { get; }
        internal int CharacterId { get; }
        internal int ActionId { get; }
        internal string ActionSetId { get; }
        internal CharacterTestCharacterEquipmentComponent Equipment { get; }
        internal string ContentHash { get; }
        internal CharacterActionSpatialMeasurement Measurement { get; }
    }

    /// <summary>Explicit nominal-summary repair using the existing probe and Store transaction.</summary>
    /// <remarks>LowFreqAlloc. UI invokes this at a preparation boundary; it never changes an active world or binding.</remarks>
    internal static class CharacterTestTacticalSummaryPreparation
    {
        // Missing/expired derived data can be measured again. Missing source bakes cannot be repaired by measuring.
        internal static bool NeedsRebuild(string diagnostic) => diagnostic != null
            && diagnostic.StartsWith("ActionSummary:", StringComparison.Ordinal)
            && diagnostic.IndexOf("WeaponBakeFrameUnavailable", StringComparison.Ordinal) < 0
            && diagnostic.IndexOf("SocketBakeFrameUnavailable", StringComparison.Ordinal) < 0
            && (diagnostic.IndexOf(":Stale:", StringComparison.Ordinal) >= 0
                || diagnostic.IndexOf(":Missing:", StringComparison.Ordinal) >= 0
                || diagnostic.IndexOf("MeasurementInputsChanged", StringComparison.Ordinal) >= 0
                || diagnostic.IndexOf("SummaryMissing", StringComparison.Ordinal) >= 0
                || diagnostic.IndexOf("SummaryLegacyFormatRequiresRemeasure", StringComparison.Ordinal) >= 0);

        internal static CharacterActionSpatialMeasurement StandardMeasurement()
            => new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 50, 0, 5000,
                new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
        internal static CharacterActionSpatialMeasurement DualLoadoutFixtureMeasurement()
            => new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 100, 0, 10000,
                new CharacterActionReferenceDummy(2, 5000, 2200, 0, 2500, -4000));

        internal static bool TryDescribe(CharacterTestCharacterRuntimeModule runtime, CharacterTestCharacterEntity source,
            CharacterActionSpatialMeasurement measurement, out CharacterTestTacticalSummaryRequest request, out string issue, int testActionId = 0)
        {
            request = null;
            issue = string.Empty;
            if (runtime == null || source == null || measurement == null)
            { issue = "SummaryPreparation:CurrentInputsMissing"; return false; }
            runtime.ActionRuntime.TryGetEntityActionSet(source.Id, out var actionSet);
            if (!CharacterTestTacticalCardInputs.TryResolveSelectedAction(actionSet, runtime.ActionRuntime.Registry.Actions, testActionId, out var action, out issue)
                || !CharacterTestTacticalCardInputs.TryValidateAction(action, out issue)) return false;
            if (!CharacterActionContentHashUtility.TryCompute(action, source, runtime.PhysicsRuntime,
                runtime.CombatTraceSystem.BakedTraceSource, runtime.CombatTraceSystem.WeaponTraceBindingResolver, out string hash, out issue, runtime.CombatTraceSystem.ReferenceProjectiles)) return false;
            request = new CharacterTestTacticalSummaryRequest(source.Identity.CharacterId, action.ActionId.Value,
                actionSet?.StableId ?? string.Empty, source.Equipment, hash, measurement, testActionId > 0);
            return true;
        }

        internal static bool TryRebuild(CharacterTestTacticalSummaryRequest request, CharacterActionTacticalMetricsStore store,
            out CharacterActionTacticalMetricsLoadResult result, out string issue)
        {
            result = default;
            issue = string.Empty;
            if (request == null || store == null) { issue = "SummaryPreparation:RequestMissing"; return false; }
            try
            {
                using (var probe = CharacterActionReferenceProbeSession.ForTestAction(request.CharacterId, request.Measurement.ReferenceDummy,
                    request.TestActionOverride ? request.ActionId : 0, request.Equipment))
                {
                    if (!TryDescribe(probe.Runtime, probe.Source, request.Measurement, out var actual, out issue,
                        request.TestActionOverride ? request.ActionId : 0)) return false;
                    if (actual.ActionId != request.ActionId || actual.ActionSetId != request.ActionSetId || actual.ContentHash != request.ContentHash)
                    { issue = "SummaryPreparation:LoadedProviderMismatch; prepare a new request for the next run."; return false; }
                    result = store.Refresh(probe, request.Measurement, requireTacticalValidity: true);
                    if (result.Status != CharacterActionTacticalMetricsLoadStatus.Valid)
                    { issue = "SummaryPreparation:" + result.Status + ":" + result.Issue; return false; }
                    // Refresh already queries again. Keep the explicit current-input check visible in this boundary.
                    result = store.Query(CharacterActionTacticalInput.FromSession(probe), request.Measurement);
                    if (result.Status != CharacterActionTacticalMetricsLoadStatus.Valid)
                    { issue = "SummaryPreparation:" + result.Status + ":" + result.Issue; return false; }
                    if (!CharacterTestTacticalCardInputs.TryValidateSummary(result.Metrics.SpatialSummary, out issue)) return false;
                    return true;
                }
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException
                || error is System.IO.IOException || error is UnauthorizedAccessException)
            { issue = "SummaryPreparation:" + error.Message; return false; }
        }
    }
}
