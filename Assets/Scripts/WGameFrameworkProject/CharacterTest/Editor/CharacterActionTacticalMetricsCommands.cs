using MxFramework.CharacterAction;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterActionTacticalMetricsCommands
    {
        private const string QueryMenu = "MxFramework/CharacterTest/Tactical Metrics/Query Action 301";
        private const string RefreshMenu = "MxFramework/CharacterTest/Tactical Metrics/Remeasure Action 301";

        [MenuItem(QueryMenu)]
        private static void Query() => Run(refresh: false);

        [MenuItem(RefreshMenu)]
        private static void Refresh() => Run(refresh: true);

        [MenuItem(QueryMenu, true)]
        [MenuItem(RefreshMenu, true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating;

        private static void Run(bool refresh)
        {
            if (!CanRun()) return;
            CharacterTestGeneratedPackageSource.InvalidateCache();
            CharacterTestDefaultBakedWeaponTraceSource.InvalidateCachedInstanceForMeasurement();
            CharacterTestDefaultWeaponTraceBindingResolver.InvalidateCachedInstanceForMeasurement();
            var measurement = new CharacterActionSpatialMeasurement(CharacterActionRangeProber.Version, 50, 0, 5000,
                new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
            var store = new CharacterActionTacticalMetricsStore();
            using (var session = new CharacterActionReferenceProbeSession(new CharacterActionId(301), measurement.ReferenceDummy))
            {
                CharacterActionTacticalMetricsLoadResult result = refresh ? store.Refresh(session, measurement)
                    : store.Query(CharacterActionTacticalInput.FromSession(session), measurement);
                Debug.Log("CharacterAction Tactical Metrics: action=301 status=" + result.Status + " issue=" + result.Issue
                    + " prober=" + result.Metrics?.SpatialSummary?.ProberStatus
                    + " optimalMeters=" + result.Metrics?.SpatialSummary?.OptimalDistance
                    + " file=" + store.GetPath(session.Source.Identity.CharacterId, session.Action.ActionId));
            }
        }
    }
}
