using MxFramework.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;

namespace MxFramework.Combat.GameplayBridge
{
    public readonly struct CombatOnHitBuffApplicationSummary : IEquatable<CombatOnHitBuffApplicationSummary>
    {
        public CombatOnHitBuffApplicationSummary(
            int resultCount,
            int acceptedDamageCount,
            int requestedBuffCount,
            int skippedResultCount,
            int skippedMissingGameplayTargetCount)
        {
            ResultCount = resultCount;
            AcceptedDamageCount = acceptedDamageCount;
            RequestedBuffCount = requestedBuffCount;
            SkippedResultCount = skippedResultCount;
            SkippedMissingGameplayTargetCount = skippedMissingGameplayTargetCount;
        }

        public int ResultCount { get; }
        public int AcceptedDamageCount { get; }
        public int RequestedBuffCount { get; }
        public int SkippedResultCount { get; }
        public int SkippedMissingGameplayTargetCount { get; }

        public bool Equals(CombatOnHitBuffApplicationSummary other)
        {
            return ResultCount == other.ResultCount
                && AcceptedDamageCount == other.AcceptedDamageCount
                && RequestedBuffCount == other.RequestedBuffCount
                && SkippedResultCount == other.SkippedResultCount
                && SkippedMissingGameplayTargetCount == other.SkippedMissingGameplayTargetCount;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatOnHitBuffApplicationSummary other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ResultCount;
                hash = (hash * 397) ^ AcceptedDamageCount;
                hash = (hash * 397) ^ RequestedBuffCount;
                hash = (hash * 397) ^ SkippedResultCount;
                hash = (hash * 397) ^ SkippedMissingGameplayTargetCount;
                return hash;
            }
        }
    }

    public sealed class CombatOnHitBuffApplicationSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.bridge.combat.on_hit_buff_application";

        private readonly CombatEntityGameplayMap _entityMap;
        private readonly GameplayBuffRequestBuffer _buffRequests;
        private readonly Func<IReadOnlyList<HitResolveResult>> _getHitResults;
        private readonly Func<HitResolveResult, CombatHitBuffApplicationConfig[]> _getOnHitBuffs;

        public CombatOnHitBuffApplicationSystem(
            CombatEntityGameplayMap entityMap,
            GameplayBuffRequestBuffer buffRequests,
            Func<IReadOnlyList<HitResolveResult>> getHitResults,
            Func<HitResolveResult, CombatHitBuffApplicationConfig[]> getOnHitBuffs,
            string systemId = DefaultSystemId,
            GameplaySystemPhase phase = GameplaySystemPhase.Resolution,
            int priority = 55)
        {
            _entityMap = entityMap ?? throw new ArgumentNullException(nameof(entityMap));
            _buffRequests = buffRequests ?? throw new ArgumentNullException(nameof(buffRequests));
            _getHitResults = getHitResults ?? throw new ArgumentNullException(nameof(getHitResults));
            _getOnHitBuffs = getOnHitBuffs ?? throw new ArgumentNullException(nameof(getOnHitBuffs));
            SystemId = systemId ?? string.Empty;
            Phase = phase;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase { get; }
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;
        public CombatOnHitBuffApplicationSummary LastSummary { get; private set; }

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            if (!IsEnabled)
                return;

            IReadOnlyList<HitResolveResult> results = _getHitResults();
            if (results == null || results.Count == 0)
            {
                LastSummary = default;
                return;
            }

            var summary = new MutableSummary { ResultCount = results.Count };
            for (int i = 0; i < results.Count; i++)
                ApplyResult(results[i], ref summary);

            LastSummary = summary.ToSummary();
        }

        private void ApplyResult(HitResolveResult result, ref MutableSummary summary)
        {
            if (result.Kind != HitResolveKind.Damage)
            {
                summary.SkippedResultCount++;
                return;
            }

            if (result.IsAcceptedDamage)
                summary.AcceptedDamageCount++;

            CombatHitBuffApplicationConfig[] onHitBuffs = _getOnHitBuffs(result);
            if (onHitBuffs == null || onHitBuffs.Length == 0)
                return;

            if (!_entityMap.TryGetGameplayId(result.TargetId, out GameplayEntityId targetEntityId))
            {
                summary.SkippedMissingGameplayTargetCount++;
                return;
            }

            for (int i = 0; i < onHitBuffs.Length; i++)
            {
                CombatHitBuffApplicationConfig config = onHitBuffs[i];
                _buffRequests.EnqueueApply(
                    targetEntityId,
                    config.BuffId,
                    sourceId: result.TraceId,
                    traceId: GetTraceIdentity(result.TraceId));
                summary.RequestedBuffCount++;
            }
        }

        private const int CombatTraceKind = 100;
        private static RuntimeTraceIdentity GetTraceIdentity(int traceId) => new RuntimeTraceIdentity(CombatTraceKind, 0, traceId, 0);

        private struct MutableSummary
        {
            public int ResultCount;
            public int AcceptedDamageCount;
            public int RequestedBuffCount;
            public int SkippedResultCount;
            public int SkippedMissingGameplayTargetCount;

            public CombatOnHitBuffApplicationSummary ToSummary()
            {
                return new CombatOnHitBuffApplicationSummary(
                    ResultCount,
                    AcceptedDamageCount,
                    RequestedBuffCount,
                    SkippedResultCount,
                    SkippedMissingGameplayTargetCount);
            }
        }
    }
}



