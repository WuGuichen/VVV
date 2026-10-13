using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MxFramework.CharacterAction;
using Newtonsoft.Json;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterActionTacticalMetricsLoadStatus { Missing = 0, Valid = 1, Stale = 2, Unavailable = 3 }

    /// <summary>Current loaded measurement inputs; no caller-provided hash is accepted.</summary>
    internal sealed class CharacterActionTacticalInput
    {
        internal CharacterActionTacticalInput(CharacterActionConfig action, CharacterTestCharacterEntity source,
            CharacterTestCharacterPhysicsRuntime physics, ICharacterTestBakedWeaponTraceSource bakes, ICharacterTestWeaponTraceBindingResolver bindings, CharacterActionProjectileReferenceCatalog combat = null)
        { Action = action; Source = source; Physics = physics; Bakes = bakes; Bindings = bindings; Combat = combat; }
        internal CharacterActionProjectileReferenceCatalog Combat { get; }
        internal CharacterActionConfig Action { get; }
        internal CharacterTestCharacterEntity Source { get; }
        internal CharacterTestCharacterPhysicsRuntime Physics { get; }
        internal ICharacterTestBakedWeaponTraceSource Bakes { get; }
        internal ICharacterTestWeaponTraceBindingResolver Bindings { get; }
        internal static CharacterActionTacticalInput FromSession(CharacterActionReferenceProbeSession session) =>
            new CharacterActionTacticalInput(session.Action, session.Source, session.Runtime.PhysicsRuntime,
                session.Runtime.CombatTraceSystem.BakedTraceSource, session.Runtime.CombatTraceSystem.WeaponTraceBindingResolver, session.Runtime.CombatTraceSystem.ReferenceProjectiles);
    }

    internal readonly struct CharacterActionTacticalMetricsLoadResult
    {
        internal CharacterActionTacticalMetricsLoadResult(CharacterActionTacticalMetricsLoadStatus status, string issue,
            CharacterActionTacticalMetrics metrics = null, CharacterActionSpatialSummary borrowedSummary = null,
            int summarySourceCharacterId = 0)
        {
            Status = status; Issue = issue ?? string.Empty; Metrics = metrics;
            BorrowedSummary = borrowedSummary; SummarySourceCharacterId = summarySourceCharacterId;
        }
        internal CharacterActionTacticalMetricsLoadStatus Status { get; }
        internal string Issue { get; }
        internal CharacterActionTacticalMetrics Metrics { get; }
        private CharacterActionSpatialSummary BorrowedSummary { get; }
        internal CharacterActionSpatialSummary SpatialSummary => BorrowedSummary ?? Metrics?.SpatialSummary;
        internal int SummarySourceCharacterId { get; }
        internal bool UsesDefaultSummary => BorrowedSummary != null;

        internal bool TryGetDefaultSurfaceDistance(out double distanceMeters)
        {
            distanceMeters = 0;
            double? measured = Status == CharacterActionTacticalMetricsLoadStatus.Valid
                && SpatialSummary?.ProberVersion == CharacterActionRangeProber.Version ? SpatialSummary.OptimalDistance : null;
            if (!measured.HasValue) return false;
            distanceMeters = measured.Value;
            return true;
        }
    }

    /// <summary>Single low-frequency load/query/remeasure boundary for derived nominal summaries.</summary>
    /// <remarks>LowFreqAlloc. File IO, hashing and probing are forbidden on a Tick or guard path.</remarks>
    internal sealed class CharacterActionTacticalMetricsStore
    {
        private readonly string _directory;
        private static readonly HashSet<string> ReportedFallbacks = new HashSet<string>(StringComparer.Ordinal);
        internal CharacterActionTacticalMetricsStore(string directory = null)
        {
            _directory = Path.GetFullPath(directory ?? Path.Combine(Application.dataPath, "../Library/MxFramework/CharacterActionTacticalMetrics"));
        }

        internal string GetPath(int sourceCharacterId, CharacterActionId actionId)
        {
            if (sourceCharacterId <= 0 || !actionId.IsValid) throw new ArgumentOutOfRangeException(nameof(actionId));
            return Path.Combine(_directory, "source-" + sourceCharacterId.ToString(CultureInfo.InvariantCulture)
                + "-action-" + actionId.Value.ToString(CultureInfo.InvariantCulture) + ".bin");
        }

        internal CharacterActionTacticalMetricsLoadResult Query(CharacterActionTacticalInput input, CharacterActionSpatialMeasurement measurement)
        {
            if (input?.Action == null || input.Source == null || measurement == null)
                return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, "CurrentInputsMissing");
            try
            {
                string path = GetPath(input.Source.Identity.CharacterId, input.Action.ActionId);
                if (!File.Exists(path)) return Result(CharacterActionTacticalMetricsLoadStatus.Missing,
                    File.Exists(Path.ChangeExtension(path, ".json")) ? "SummaryLegacyFormatRequiresRemeasure" : "SummaryMissing");
                CharacterActionSpatialSummary saved = CharacterActionTacticalSummaryBinary.ReadFile(path);
                if (saved.ProberStatus == CharacterActionProberStatus.Unavailable || saved.ProberStatus == CharacterActionProberStatus.Unsupported)
                    return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, "SummaryHasNoCompletedMeasurement");
                if (!CharacterActionContentHashUtility.TryCompute(input.Action, input.Source, input.Physics, input.Bakes, input.Bindings,
                    out string currentHash, out string issue, input.Combat, requireProjectileCatalog: true)) return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, issue);
                string dummyHash = CharacterActionContentHashUtility.ComputeReferenceDummyHash(measurement.ReferenceDummy);
                if (saved.ActionId != input.Action.ActionId || saved.ActionContentHash != currentHash
                    || saved.ReferenceDummyHash != dummyHash
                    || saved.ReferenceDummyHash != CharacterActionContentHashUtility.ComputeReferenceDummyHash(saved.Measurement.ReferenceDummy)
                    || saved.ProberVersion != measurement.ProberVersion || measurement.ProberVersion != CharacterActionRangeProber.Version
                    || saved.Measurement.StepMillimeters != measurement.StepMillimeters
                    || saved.Measurement.SearchMinMillimeters != measurement.SearchMinMillimeters
                    || saved.Measurement.SearchMaxMillimeters != measurement.SearchMaxMillimeters)
                    return Result(CharacterActionTacticalMetricsLoadStatus.Stale, "MeasurementInputsChanged");
                return new CharacterActionTacticalMetricsLoadResult(CharacterActionTacticalMetricsLoadStatus.Valid, string.Empty,
                    new CharacterActionTacticalMetrics(input.Action, currentHash, saved));
            }
            catch (InvalidDataException error) { return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, error.Message); }
            catch (IOException error) { return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, error.Message); }
            catch (UnauthorizedAccessException error) { return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, error.Message); }
            catch (JsonException error) { return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, error.Message); }
            catch (ArgumentException error) { return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, error.Message); }
            catch (FormatException error) { return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, error.Message); }
            catch (OverflowException error) { return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, error.Message); }
        }

        /// <summary>Gameplay preparation may borrow a default-role measurement; explicit measurement queries stay strict.</summary>
        internal CharacterActionTacticalMetricsLoadResult QueryForTacticalCard(
            CharacterActionTacticalInput input, CharacterActionSpatialMeasurement measurement)
        {
            var own = Query(input, measurement);
            if (own.Status != CharacterActionTacticalMetricsLoadStatus.Missing
                && own.Status != CharacterActionTacticalMetricsLoadStatus.Stale) return own;
            int defaultCharacter = CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId;
            if (input.Source.Identity.CharacterId == defaultCharacter) return own;
            if (!CharacterActionContentHashUtility.TryCompute(input.Action, input.Source, input.Physics, input.Bakes, input.Bindings,
                    out string currentHash, out string inputIssue, input.Combat, requireProjectileCatalog: true))
                return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, inputIssue);
            try
            {
                {
                    var reference = CharacterTestTacticalPreparationCatalog.FallbackInput(defaultCharacter, input.Action.ActionId.Value);
                    var fallback = Query(reference, measurement);
                    if (fallback.Status != CharacterActionTacticalMetricsLoadStatus.Valid
                        || !CharacterTestTacticalCardInputs.TryValidateSummary(fallback.SpatialSummary, out _))
                        return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable,
                            "DefaultSummaryUnavailable:" + fallback.Status + ":" + fallback.Issue + ";Current:" + own.Issue);
                    string issue = "DefaultCharacterSummary:" + defaultCharacter + ":" + own.Status + ":" + own.Issue;
                    string warningKey = GetPath(input.Source.Identity.CharacterId, input.Action.ActionId) + "|" + currentHash
                        + "|" + fallback.SpatialSummary.ActionContentHash + "|" + CharacterActionTacticalSummaryJson.Write(fallback.SpatialSummary);
                    if (ReportedFallbacks.Count >= 256) ReportedFallbacks.Clear();
                    if (ReportedFallbacks.Add(warningKey))
                        Debug.LogWarning("[Runtime AI Lab][TacticalSummaryFallback] 角色 " + input.Source.Identity.CharacterId
                            + " 的动作 " + input.Action.ActionId.Value + " 测量摘要" + (own.Status == CharacterActionTacticalMetricsLoadStatus.Missing ? "缺失" : "已失效")
                            + "，使用默认角色 " + defaultCharacter + " 的同动作测量数据。当前动作与资源规则保持自身配置。 reason=" + own.Issue);
                    // Keep the current action fingerprint and timing separate from borrowed spatial evidence.
                    // Never rewrite the default receipt's hash or save it as this role's actual measurement.
                    return new CharacterActionTacticalMetricsLoadResult(CharacterActionTacticalMetricsLoadStatus.Valid, issue,
                        new CharacterActionTacticalMetrics(input.Action, currentHash), fallback.SpatialSummary, defaultCharacter);
                }
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException
                || error is IOException || error is UnauthorizedAccessException)
            { return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, "DefaultSummaryUnavailable:" + error.Message); }
        }

        internal CharacterActionTacticalMetricsLoadResult Refresh(CharacterActionReferenceProbeSession session, CharacterActionSpatialMeasurement measurement,
            bool requireTacticalValidity = false, Func<int, int, bool> continueMeasurement = null)
        {
            try { return CommitMeasurement(session, measurement, CharacterActionRangeProber.MeasureSession(session, measurement, continueMeasurement), requireTacticalValidity, continueMeasurement); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is InvalidOperationException)
            { return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, error.Message); }
        }

        // The same atomic transaction is used after synchronous and frame-budgeted measurements.
        internal CharacterActionTacticalMetricsLoadResult CommitMeasurement(CharacterActionReferenceProbeSession session,
            CharacterActionSpatialMeasurement measurement, CharacterActionRangeProbeResult measured,
            bool requireTacticalValidity = false, Func<int, int, bool> continueMeasurement = null)
        {
            if (session == null || measurement == null || measured == null) return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, "CurrentInputsMissing");
            string temporary = null;
            string rollback = null, committedPath = null;
            bool committed = false, accepted = false;
            try
            {
                if (measured.Summary == null) return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, measured.Issue);
                if (requireTacticalValidity && !CharacterTestTacticalCardInputs.TryValidateSummary(measured.Summary, out string summaryIssue))
                    return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, measured.Status + ":" + summaryIssue);
                if (continueMeasurement?.Invoke(1, 1) == false)
                    return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, "MeasurementCancelled");
                string path = GetPath(session.Source.Identity.CharacterId, session.Action.ActionId);
                Directory.CreateDirectory(_directory);
                temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllBytes(temporary, CharacterActionTacticalSummaryBinary.Write(measured.Summary));
                CharacterActionTacticalSummaryBinary.ReadFile(temporary);
                if (continueMeasurement?.Invoke(1, 1) == false)
                    return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, "MeasurementCancelled");
                if (File.Exists(path))
                {
                    rollback = path + "." + Guid.NewGuid().ToString("N") + ".rollback";
                    File.Replace(temporary, path, rollback);
                }
                else File.Move(temporary, path);
                committed = true; committedPath = path;
                var result = Query(CharacterActionTacticalInput.FromSession(session), measurement);
                accepted = result.Status == CharacterActionTacticalMetricsLoadStatus.Valid;
                if (accepted)
                {
                    CharacterTestTacticalPreparationCatalog.SummaryChanged();
                    // No migration or broad cleanup: only remove the receipt successfully replaced by this measurement.
                    DeleteResidual(Path.ChangeExtension(path, ".json"));
                }
                return result;
            }
            catch (IOException error) { return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, error.Message); }
            catch (UnauthorizedAccessException error) { return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, error.Message); }
            catch (ArgumentException error) { return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, error.Message); }
            catch (InvalidOperationException error) { return Result(CharacterActionTacticalMetricsLoadStatus.Unavailable, error.Message); }
            finally
            {
                if (committed && !accepted)
                {
                    if (rollback != null && File.Exists(rollback)) File.Replace(rollback, committedPath, null);
                    else if (File.Exists(committedPath)) File.Delete(committedPath);
                }
                if (accepted) DeleteResidual(rollback);
                DeleteResidual(temporary);
            }
        }

        private static void DeleteResidual(string path)
        {
            if (path == null) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { Debug.LogWarning("[TacticalSummary] 缓存清理失败，文件已保留：" + path + "；" + error.Message); }
        }

        private static CharacterActionTacticalMetricsLoadResult Result(CharacterActionTacticalMetricsLoadStatus status, string issue) =>
            new CharacterActionTacticalMetricsLoadResult(status, issue);
    }
}
