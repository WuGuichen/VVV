using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Damage;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Unity.Profiling;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestCombatDamageApplier
    {
        private static readonly RuntimeCommand[] EmptyRuntimeCommands =
            Array.Empty<RuntimeCommand>();
        private static readonly HitResolveResult[] EmptyHitResolveResults =
            Array.Empty<HitResolveResult>();
        private const int CombatTraceKindId = 1012;
        private const int CohesionTraceKindId = 1013;
        private const int FrameOutputInitialCapacity = 128;
        private const int FrameOutputResultMultiplier = 4;
        private const string DeathReason = "hp_zero";
        private const int StandardLaunchPower = 100;
        private const float LaunchReferenceMassKg = 80f;
        private const float StandardLaunchVelocityYMetersPerSecond = 7.5f;
        private const float MinimumLaunchMassScale = 0.6f;
        private const float MaximumLaunchMassScale = 1.35f;
        private const int MaximumLaunchVelocityYMillimetersPerSecond = 15000;
        private const int LaunchPowerMotionStableEventId = CharacterTestAuthoredContentIds.D401_Id130;
        private const string HitResolveKindDamageReason = "Damage";
        private const string HitResolveKindBlockedReason = "Blocked";
        private const string HitResolveKindDuplicateReason = "Duplicate";
        private const string HitResolveKindFriendlyReason = "Friendly";
        private const string HitResolveKindInvincibleReason = "Invincible";
        private const string HitResolveKindEvadedReason = "Evaded";
        private const string HitResolveKindParriedReason = "Parried";
        private const string HitResolveKindSelfDamageReason = "SelfDamage";
        private const string HitResolveKindTargetDeadReason = "TargetDead";
        private const string HitResolveKindUnknownReason = "Unknown";
        private static readonly ProfilerMarker ScatterPressureRequestsMarker =
            new ProfilerMarker("CharacterTest.CombatDamageApplier.ScatterPressureRequests");

        private readonly List<RuntimeCommand> _damageApplicationCommands =
            new List<RuntimeCommand>(FrameOutputInitialCapacity);
        private readonly List<DamageTelemetryRow> _damageTelemetryRows =
            new List<DamageTelemetryRow>(FrameOutputInitialCapacity);
        private readonly List<DamageResolvedEvent> _damageResolvedEvents =
            new List<DamageResolvedEvent>(FrameOutputInitialCapacity);
        private readonly List<HitPresentationEvent> _hitPresentationEvents =
            new List<HitPresentationEvent>(FrameOutputInitialCapacity);
        private readonly List<PressureBandChangedEvent> _scatterPressureBandChangedEvents =
            new List<PressureBandChangedEvent>(FrameOutputInitialCapacity);
        private readonly List<ScatterPressureChangedEvent> _scatterPressureChangedEvents =
            new List<ScatterPressureChangedEvent>(FrameOutputInitialCapacity);
        private readonly List<ScatterImpactResolvedEvent> _scatterImpactResolvedEvents =
            new List<ScatterImpactResolvedEvent>(FrameOutputInitialCapacity);
        private readonly List<CohesionBreakEvent> _cohesionBreakEvents =
            new List<CohesionBreakEvent>(FrameOutputInitialCapacity);
        private readonly List<CharacterTestScatterPressureTrace> _scatterPressureTraces =
            new List<CharacterTestScatterPressureTrace>(FrameOutputInitialCapacity);
        private readonly List<CharacterTestDeathEvent> _deathEvents =
            new List<CharacterTestDeathEvent>(FrameOutputInitialCapacity);
        private readonly GameplayScatterPressureSystem _scatterPressureSystem =
            new GameplayScatterPressureSystem();
        private readonly GameplayAttributeCommandSystem _attributeCommandSystem =
            new GameplayAttributeCommandSystem();
        private readonly GameplayWorld _gameplayWorld = new GameplayWorld();
        private readonly GameplayCommandExecutionState _resolutionCommandState =
            new GameplayCommandExecutionState();
        private readonly GameplayCommandExecutionState _attributeCommandState =
            new GameplayCommandExecutionState();
        private readonly CharacterTestBodyPartDamageAuthorityBridge _bodyPartDamageBridge =
            new CharacterTestBodyPartDamageAuthorityBridge();
        private readonly Dictionary<int, CharacterTestCharacterEntity> _entitiesByCharacterId =
            new Dictionary<int, CharacterTestCharacterEntity>(128);
        private readonly Dictionary<GameplayEntityId, CharacterTestCharacterEntity> _entitiesByGameplayId =
            new Dictionary<GameplayEntityId, CharacterTestCharacterEntity>(128);
        private readonly Func<IReadOnlyList<HitResolveResult>> _getCurrentDamageResults;
        private readonly Func<HitResolveResult, DamageProfileResolveResult> _resolveCurrentDamagePayload;
        private readonly Action<DamageResolvedEvent> _addDamageResolvedEvent;
        private readonly Action<HitPresentationEvent> _addHitPresentationEvent;
        private CombatDamageApplicationSystem _damageApplicationSystem;
        private CombatEntityGameplayMap _damageApplicationMap;
        private GameplayComponentWorld _damageApplicationComponentWorld;
        private IReadOnlyList<HitResolveResult> _currentDamageResults = EmptyHitResolveResults;
        private Func<HitResolveResult, DamageProfileResolveResult> _currentDamagePayloadResolver;


        public CharacterTestCombatDamageApplier()
        {
            _getCurrentDamageResults = GetCurrentDamageResults;
            _resolveCurrentDamagePayload = ResolveCurrentDamagePayload;
            _addDamageResolvedEvent = AddDamageResolvedEvent;
            _addHitPresentationEvent = AddHitPresentationEvent;
            _scatterPressureSystem.PressureChangedEvents.Subscribe(evt => _scatterPressureChangedEvents.Add(evt));
            _scatterPressureSystem.ScatterImpactResolvedEvents.Subscribe(evt => _scatterImpactResolvedEvents.Add(evt));
            _scatterPressureSystem.BandChangedEvents.Subscribe(evt => _scatterPressureBandChangedEvents.Add(evt));
            _scatterPressureSystem.CohesionBreakEvents.Subscribe(evt => _cohesionBreakEvents.Add(evt));
        }

        public IReadOnlyList<DamageTelemetryRow> DamageTelemetryRows => _damageTelemetryRows;
        public IReadOnlyList<DamageResolvedEvent> DamageResolvedEvents => _damageResolvedEvents;
        public IReadOnlyList<HitPresentationEvent> HitPresentationEvents => _hitPresentationEvents;
        public IReadOnlyList<ScatterPressureChangedEvent> ScatterPressureChangedEvents => _scatterPressureChangedEvents;
        public IReadOnlyList<ScatterImpactResolvedEvent> ScatterImpactResolvedEvents => _scatterImpactResolvedEvents;
        public IReadOnlyList<PressureBandChangedEvent> ScatterPressureBandChangedEvents => _scatterPressureBandChangedEvents;
        public IReadOnlyList<CohesionBreakEvent> CohesionBreakEvents => _cohesionBreakEvents;
        public IReadOnlyList<CharacterTestScatterPressureTrace> ScatterPressureTraces => _scatterPressureTraces;
        public IReadOnlyList<CharacterTestDeathEvent> DeathEvents => _deathEvents;
        internal CharacterTestBodyPartDamageAuthorityBridge BodyPartDamageBridge =>
            _bodyPartDamageBridge;

        internal void ResetBodyPartAuthorityForRestore()
        {
            _bodyPartDamageBridge.ResetForAuthorityRestore();
        }

        public void ClearFrameOutputs()
        {
            _interactionPressureRequests.Clear();
            _derivedMarkRequests.Clear();
            _damageApplicationCommands.Clear();
            _damageTelemetryRows.Clear();
            _damageResolvedEvents.Clear();
            _hitPresentationEvents.Clear();
            _scatterPressureChangedEvents.Clear();
            _scatterImpactResolvedEvents.Clear();
            _scatterPressureBandChangedEvents.Clear();
            _cohesionBreakEvents.Clear();
            _scatterPressureTraces.Clear();
            _deathEvents.Clear();
            _resolutionCommandState.Clear();
            _attributeCommandState.Clear();
        }

        public void Apply(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            IReadOnlyList<HitResolveResult> results,
            Func<HitResolveResult, DamageProfileResolveResult> resolvePayload,
            IReadOnlyDictionary<CharacterTestResolvedHitKey, CharacterTestHitZoneResolution> hitZonesByHit = null,
            CharacterTestTraceRecorder traceRecorder = null,
            Func<HitResolveResult, CharacterTestAccessoryHitEvidence> resolveAccessoryEvidence = null)
        {
            if (world == null)
                return;

            actionRuntime?.BindRuntimeWorld(world);
            IReadOnlyList<HitResolveResult> safeResults = results ?? EmptyHitResolveResults;
            EnsureFrameCapacity(safeResults.Count);
            ConfigureEntityLookup(world);
            for (int i = 0; i < safeResults.Count; i++)
                _bodyPartDamageBridge.CacheResolvedHitOutcome(safeResults[i]);

            GameplaySystemContext resolutionContext = CreateGameplayContext(
                world,
                frame,
                EmptyRuntimeCommands,
                _resolutionCommandState);
            if (safeResults.Count > 0)
            {
                if (resolvePayload == null)
                    throw new ArgumentNullException(nameof(resolvePayload));

                _derivedMarkRequests.Clear();
                _currentDamageResults = _singleDamageResult;
                _currentDamagePayloadResolver = resolvePayload;
                try
                {
                    for (int resultIndex = 0; resultIndex < safeResults.Count; resultIndex++)
                    {
                        var result = safeResults[resultIndex];
                        _singleDamageResult[0] = result;
                        var target = FindEntityByCombatId(result.TargetId);
                        int hpBeforeCommit = target == null ? 0 : world.GetCurrentAttribute(target.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp);
                        _damageApplicationCommands.Clear();
                        GetDamageApplicationSystem(world).Tick(resolutionContext);
                        CommitInteractionHpCommands(world, frame);
                        int hpAfterCommit = target == null ? 0 : world.GetCurrentAttribute(target.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp);
                        var payload = resolvePayload(result);
                        CommitInteractionReceipt(world, frame, result, payload, System.Math.Max(0, hpBeforeCommit - hpAfterCommit), hpBeforeCommit > 0);
                        if (resolveAccessoryEvidence != null)
                            CommitAccessoryOutcome(world, frame, result, payload, resolveAccessoryEvidence(result), hpBeforeCommit > 0);
                    }
                    ApplyDerivedMarkRequests(world, frame, resolutionContext);
                }
                finally
                {
                    _currentDamageResults = EmptyHitResolveResults;
                    _currentDamagePayloadResolver = null;
                }
            }

            if (_damageApplicationCommands.Count > 0)
            {
                _attributeCommandSystem.Tick(CreateGameplayContext(
                    world,
                    frame,
                    _damageApplicationCommands,
                    _attributeCommandState));
            }

            _bodyPartDamageBridge.CompleteAuthorityFrame(frame.Value);
            _damageApplicationCommands.Clear();

            ApplyDeathTransitions(world, actionRuntime, frame, safeResults, traceRecorder);
            for (int i = 0; i < world.Entities.Count; i++)
                if (!world.Entities[i].CombatState.IsAlive) world.Entities[i].InteractionEffects.Clear();
            EnqueueCohesionLimitRequests(world, actionRuntime);
            using (ScatterPressureRequestsMarker.Auto())
            {
                EnqueueScatterPressureRequests(world, safeResults, resolvePayload, hitZonesByHit);
                EnqueueInteractionPressureRequests();
            }

            HoldBrokenRecoveryForLaunchedSpecialHits(world, frame);
            _scatterPressureSystem.Tick(resolutionContext);
            ApplyCohesionBreakReactions(world, actionRuntime, frame);
            ApplyLaunchSpecialHitReactions(world, actionRuntime, frame, safeResults, resolvePayload, hitZonesByHit);
            ApplyHitReactions(world, actionRuntime, frame, safeResults, resolvePayload, hitZonesByHit);
        }

        private void EnsureFrameCapacity(int resultCount)
        {
            if (resultCount <= 0)
                return;

            int requiredCapacity = Math.Max(
                FrameOutputInitialCapacity,
                resultCount * FrameOutputResultMultiplier);
            EnsureListCapacity(_damageApplicationCommands, requiredCapacity);
            EnsureListCapacity(_damageTelemetryRows, requiredCapacity);
            EnsureListCapacity(_damageResolvedEvents, requiredCapacity);
            EnsureListCapacity(_hitPresentationEvents, requiredCapacity);
            EnsureListCapacity(_scatterPressureChangedEvents, requiredCapacity);
            EnsureListCapacity(_scatterImpactResolvedEvents, requiredCapacity);
            EnsureListCapacity(_scatterPressureBandChangedEvents, requiredCapacity);
            EnsureListCapacity(_cohesionBreakEvents, requiredCapacity);
            EnsureListCapacity(_scatterPressureTraces, requiredCapacity);
            EnsureListCapacity(_deathEvents, requiredCapacity);
        }

        private static void EnsureListCapacity<T>(List<T> list, int requiredCapacity)
        {
            if (list.Capacity >= requiredCapacity)
                return;

            int capacity = Math.Max(FrameOutputInitialCapacity, list.Capacity);
            while (capacity < requiredCapacity)
                capacity *= 2;
            list.Capacity = capacity;
        }

        private void ConfigureEntityLookup(CharacterTestCharacterRuntimeWorld world)
        {
            _entitiesByCharacterId.Clear();
            _entitiesByGameplayId.Clear();
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null)
                    continue;

                _entitiesByCharacterId[entity.Id.Value] = entity;
                _entitiesByGameplayId[entity.GameplayEntityId] = entity;
            }
        }

        private CombatDamageApplicationSystem GetDamageApplicationSystem(CharacterTestCharacterRuntimeWorld world)
        {
            CombatEntityGameplayMap entityMap = world.CombatGameplayMap;
            GameplayComponentWorld componentWorld = world.GameplayComponentWorld;
            if (_damageApplicationSystem != null
                && ReferenceEquals(_damageApplicationMap, entityMap)
                && ReferenceEquals(_damageApplicationComponentWorld, componentWorld))
            {
                return _damageApplicationSystem;
            }

            _damageApplicationMap = entityMap;
            _damageApplicationComponentWorld = componentWorld;
            _damageApplicationSystem = new CombatDamageApplicationSystem(
                entityMap,
                componentWorld,
                null,
                _getCurrentDamageResults,
                _resolveCurrentDamagePayload,
                CharacterTestCharacterAttributeIds.Hp,
                _damageApplicationCommands,
                _damageTelemetryRows,
                _addDamageResolvedEvent,
                _addHitPresentationEvent);
            return _damageApplicationSystem;
        }

        private IReadOnlyList<HitResolveResult> GetCurrentDamageResults()
        {
            return _currentDamageResults ?? EmptyHitResolveResults;
        }

        private DamageProfileResolveResult ResolveCurrentDamagePayload(HitResolveResult result)
        {
            return _currentDamagePayloadResolver != null
                ? _currentDamagePayloadResolver(result)
                : default;
        }

        private void AddDamageResolvedEvent(DamageResolvedEvent evt)
        {
            _damageResolvedEvents.Add(evt);
            if (!_applyingInteractionDerived) _bodyPartDamageBridge.CacheResolvedDamageEvent(evt);
        }

        private void AddHitPresentationEvent(HitPresentationEvent evt)
        {
            if (!_applyingInteractionDerived) _hitPresentationEvents.Add(evt);
        }

        private void EnqueueCohesionLimitRequests(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null || !entity.BattleState.CountsAsAlive)
                    continue;

                if (!world.TryGetScatterPressure(entity, out GameplayScatterPressureComponent scatterPressure))
                    continue;

                int cohesionLimit = MxFramework.Combat.GameplayBridge.CombatCohesionLimitAdapter.CalculateCohesionLimit(
                    scatterPressure.MaxScatterPressure,
                    actionRuntime?.GetCurrentCohesionSupportProfile(entity.Id));
                if (cohesionLimit <= 0)
                    cohesionLimit = scatterPressure.MaxScatterPressure;

                _scatterPressureSystem.Enqueue(new GameplayCohesionLimitRequest(
                    entity.GameplayEntityId,
                    cohesionLimit,
                    entity.Id.Value,
                    GameplayScatterPressureEvents.UpdateCohesionLimitReason,
                    new RuntimeTraceIdentity(CohesionTraceKindId, entity.Id.Value, entity.Action.ActiveActionId.Value)));
            }
        }

        private void EnqueueScatterPressureRequests(
            CharacterTestCharacterRuntimeWorld world,
            IReadOnlyList<HitResolveResult> results,
            Func<HitResolveResult, DamageProfileResolveResult> resolvePayload,
            IReadOnlyDictionary<CharacterTestResolvedHitKey, CharacterTestHitZoneResolution> hitZonesByHit)
        {
            if (results == null || results.Count == 0 || resolvePayload == null)
                return;

            for (int i = 0; i < results.Count; i++)
            {
                HitResolveResult result = results[i];
                if (result.Kind != HitResolveKind.Damage && result.Kind != HitResolveKind.Blocked)
                    continue;

                DamageProfileResolveResult payload = resolvePayload(result);
                if (!payload.Success || payload.ImpactPower <= 0)
                    continue;

                if (!world.CombatGameplayMap.TryGetGameplayId(result.TargetId, out GameplayEntityId targetEntityId))
                    continue;

                CharacterTestCharacterEntity source = FindEntityByCombatId(result.AttackerId);
                CharacterTestCharacterEntity target = FindEntityByCombatId(result.TargetId);
                if (target == null || !target.BattleState.CanReceiveHpDamage)
                    continue;

                CharacterTestHitZoneResolution hitZone = ResolveHitZone(result, hitZonesByHit);
                CharacterTestImpactResponse impact = CharacterTestImpactResponseResolver.Resolve(
                    payload.ImpactPower,
                    hitZone.ImpulseScale,
                    source,
                    target,
                    CharacterTestCharacterMovementProfileSource.DefaultProfile);
                if (impact.ScatterImpact <= 0 && !impact.HasKnockback)
                    continue;

                int sourceEntityId = payload.Usage.SourceEntityId > 0
                    ? payload.Usage.SourceEntityId
                    : CharacterTestCombatEntityLookup.ResolveCharacterEntityId(result.AttackerId);
                RuntimeTraceIdentity traceId = new RuntimeTraceIdentity(CombatTraceKindId, sourceEntityId, result.ActionInstanceId, result.TraceId);
                var reason = new GameplayScatterPressureReason(
                    GameplayScatterPressureReason.ImpactPowerCode,
                    payload.Usage.StableEventId);
                bool enqueuedScatter = target.BattleState.CanReceiveScatter && impact.ScatterImpact > 0;
                if (enqueuedScatter)
                {
                    _scatterPressureSystem.Enqueue(new GameplayScatterPressureRequest(
                        targetEntityId,
                        impact.ScatterImpact,
                        sourceEntityId,
                        reason,
                        traceId));
                }

                if (target != null
                    && impact.HasKnockback
                    && !ShouldSuppressKnockback(payload))
                {
                    target.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.HorizontalVelocityOverride(
                        impact.KnockbackVelocity,
                        CharacterMovementMode.Idle,
                        payload.Usage.ActionId,
                        payload.Usage.StableEventId));
                }

                UpdateTelemetryImpact(i, impact);
                if (enqueuedScatter)
                {
                    int targetEntityIdValue = payload.Usage.TargetEntityId > 0
                        ? payload.Usage.TargetEntityId
                        : CharacterTestCombatEntityLookup.ResolveCharacterEntityId(result.TargetId);
                    _scatterPressureTraces.Add(new CharacterTestScatterPressureTrace(
                        targetEntityIdValue,
                        sourceEntityId,
                        impact.ScatterImpact,
                        payload.Usage.StableEventId,
                        result.TraceId,
                        impact.ImpactPower,
                        impact.EffectiveImpact,
                        impact.SourceMassKg,
                        impact.TargetMassKg,
                        impact.KnockbackSpeed,
                        impact.PreviewDistanceMeters));
                }
            }
        }

        private static bool ShouldSuppressKnockback(DamageProfileResolveResult payload)
        {
            return IsInteractionOwnedKnockback(payload.Usage.StableEventId);
        }

        internal static bool IsInteractionOwnedKnockback(int stableEventId)
        {
            return (stableEventId == CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId)
                || (stableEventId == CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileImpactStableEventId);
        }

        private void UpdateTelemetryImpact(int index, CharacterTestImpactResponse impact)
        {
            if (index < 0 || index >= _damageTelemetryRows.Count)
                return;

            _damageTelemetryRows[index] = _damageTelemetryRows[index].WithImpact(
                impact.ImpactPower,
                impact.EffectiveImpact,
                impact.SourceMassKg,
                impact.TargetMassKg,
                impact.ScatterImpact,
                impact.KnockbackSpeed,
                impact.PreviewDistanceMeters);
        }

        private static CharacterTestHitZoneResolution ResolveHitZone(
            HitResolveResult result,
            IReadOnlyDictionary<CharacterTestResolvedHitKey, CharacterTestHitZoneResolution> hitZonesByHit)
        {
            if (hitZonesByHit != null
                && hitZonesByHit.TryGetValue(new CharacterTestResolvedHitKey(result), out CharacterTestHitZoneResolution hitZone))
            {
                return hitZone;
            }

            return CharacterTestHitZoneResolution.Default;
        }

        private void ApplyCohesionBreakReactions(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame)
        {
            if (_cohesionBreakEvents.Count == 0)
                return;

            for (int i = 0; i < _cohesionBreakEvents.Count; i++)
            {
                CohesionBreakEvent breakEvent = _cohesionBreakEvents[i];
                CharacterTestCharacterEntity entity = FindEntity(breakEvent.EntityId);
                if (entity == null || !entity.BattleState.CanReceiveHpDamage)
                    continue;

                CharacterReactionContextBuildResult reactionContext =
                    CharacterReactionContextBuilder.FromCohesionBreakNoAlloc(breakEvent);
                bool startedReaction = actionRuntime != null
                    && actionRuntime.TryReact(entity.Id, reactionContext.Context);

                entity.ApplyCohesionBreak(frame.Value);
                CharacterTestBattleStateSystem.RefreshEntity(world, entity, frame, null);
            }
        }

        private void ApplyHitReactions(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            IReadOnlyList<HitResolveResult> results,
            Func<HitResolveResult, DamageProfileResolveResult> resolvePayload,
            IReadOnlyDictionary<CharacterTestResolvedHitKey, CharacterTestHitZoneResolution> hitZonesByHit)
        {
            if (actionRuntime == null || results == null || results.Count == 0 || resolvePayload == null)
                return;

            for (int i = 0; i < results.Count; i++)
            {
                HitResolveResult result = results[i];
                if (!result.IsAcceptedDamage || result.StaggerFrames <= 0)
                    continue;

                CharacterTestCharacterEntity target = FindEntityByCombatId(result.TargetId);
                if (target == null
                    || target.SpecialHitState.IsLaunched
                    || !target.BattleState.CanReceiveHpDamage
                    || !world.TryGetScatterPressure(target, out GameplayScatterPressureComponent scatterPressure)
                    || scatterPressure.LastCohesionBreakFrame == frame.Value)
                {
                    continue;
                }

                DamageProfileResolveResult payload = resolvePayload(result);
                CharacterTestCharacterCombatStateComponent reactionState =
                    target.CombatState.WithScatterPressure(
                        scatterPressure.CurrentScatterPressure,
                        scatterPressure.CurrentCohesionLimit,
                        scatterPressure.CurrentBand,
                        scatterPressure.BrokenRecoveryDelayFrames,
                        scatterPressure.LastCohesionBreakFrame);
                CharacterTestHitReactionResult reaction =
                    CharacterTestHitReactionResolver.Resolve(result, payload, reactionState);
                if (!reaction.ShouldReact)
                    continue;

                CharacterReactionContextBuildResult reactionContext =
                    CharacterReactionContextBuilder.FromHitSourceNoAlloc(CreateHitReactionSource(
                        frame,
                        result,
                        payload,
                        target,
                        scatterPressure,
                        ResolveHitZone(result, hitZonesByHit)));
                if (!reactionContext.Success)
                    continue;

                bool startedReaction = actionRuntime.TryReact(
                    target.Id,
                    reactionContext.Context,
                    allowFallback: true);
                if (startedReaction)
                    CharacterTestBattleStateSystem.RefreshEntity(world, target, frame, null);
            }
        }

        private void ApplyLaunchSpecialHitReactions(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            IReadOnlyList<HitResolveResult> results,
            Func<HitResolveResult, DamageProfileResolveResult> resolvePayload,
            IReadOnlyDictionary<CharacterTestResolvedHitKey, CharacterTestHitZoneResolution> hitZonesByHit)
        {
            if (results == null || results.Count == 0 || resolvePayload == null)
                return;

            for (int i = 0; i < results.Count; i++)
            {
                if (!TryResolveLaunchSpecialHitCandidate(
                    world,
                    frame,
                    results[i],
                    resolvePayload,
                    out LaunchSpecialHitCandidate candidate))
                {
                    continue;
                }

                ApplyLaunchSpecialHitCandidate(
                    world,
                    actionRuntime,
                    frame,
                    candidate,
                    hitZonesByHit);
            }
        }

        private bool TryResolveLaunchSpecialHitCandidate(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            HitResolveResult result,
            Func<HitResolveResult, DamageProfileResolveResult> resolvePayload,
            out LaunchSpecialHitCandidate candidate)
        {
            candidate = default;
            if (!result.IsAcceptedDamage)
                return false;

            CharacterTestCharacterEntity target = FindEntityByCombatId(result.TargetId);
            if (target == null
                || !target.BattleState.CanReceiveHpDamage
                || !world.TryGetScatterPressure(target, out GameplayScatterPressureComponent scatterPressure)
                || !scatterPressure.IsBroken
                || scatterPressure.CurrentBand != PressureBand.Broken)
            {
                return false;
            }

            CharacterTestSpecialHitSusceptibilityComponent susceptibility =
                target.SpecialHitSusceptibility;
            if (!susceptibility.CanBeLaunched)
                return false;

            DamageProfileResolveResult payload = resolvePayload(result);
            if (!payload.Success || payload.LaunchPower <= 0)
                return false;

            bool alreadyLaunched = target.SpecialHitState.IsLaunched;
            if (!alreadyLaunched
                && susceptibility.RequiresBrokenForLaunch
                && !IsExistingBrokenWindow(scatterPressure, frame.Value))
            {
                return false;
            }

            int launchVelocityY = ResolveLaunchVelocityYMillimetersPerSecond(
                payload.LaunchPower,
                target);
            if (launchVelocityY <= 0)
                return false;

            candidate = new LaunchSpecialHitCandidate(
                result,
                target,
                payload,
                scatterPressure,
                alreadyLaunched,
                launchVelocityY);
            return true;
        }

        private void ApplyLaunchSpecialHitCandidate(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            in LaunchSpecialHitCandidate candidate,
            IReadOnlyDictionary<CharacterTestResolvedHitKey, CharacterTestHitZoneResolution> hitZonesByHit)
        {
            CharacterTestCharacterEntity target = candidate.Target;
            if (candidate.AlreadyLaunched)
            {
                EnqueueLaunchVelocity(
                    target,
                    candidate.Payload,
                    candidate.LaunchVelocityYMillimetersPerSecond,
                    appendToCurrent: true);
                CharacterTestBattleStateSystem.RefreshEntity(world, target, frame, null);
                return;
            }

            if (!TryStartLaunchReaction(actionRuntime, frame, candidate, hitZonesByHit))
                return;

            target.SetSpecialHitState(CharacterTestSpecialHitStateComponent.Launched(
                frame.Value,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(candidate.Result.AttackerId),
                candidate.Result.TraceId,
                candidate.LaunchVelocityYMillimetersPerSecond,
                horizontalImpulseMillimetersPerSecond: 0));
            EnqueueLaunchVelocity(
                target,
                candidate.Payload,
                candidate.LaunchVelocityYMillimetersPerSecond,
                appendToCurrent: false);
            CharacterTestBattleStateSystem.RefreshEntity(world, target, frame, null);
        }

        private bool TryStartLaunchReaction(
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            in LaunchSpecialHitCandidate candidate,
            IReadOnlyDictionary<CharacterTestResolvedHitKey, CharacterTestHitZoneResolution> hitZonesByHit)
        {
            if (actionRuntime == null)
                return false;

            CharacterReactionContextBuildResult reactionContext =
                CharacterReactionContextBuilder.FromHitSourceNoAlloc(CreateLaunchHitReactionSource(
                    frame,
                    candidate.Result,
                    candidate.Payload,
                    candidate.Target,
                    candidate.ScatterPressure,
                    ResolveHitZone(candidate.Result, hitZonesByHit)));
            if (!reactionContext.Success)
                return false;

            return actionRuntime.TryReact(
                candidate.Target.Id,
                reactionContext.Context,
                allowFallback: true);
        }

        private void ApplyDeathTransitions(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime,
            RuntimeFrame frame,
            IReadOnlyList<HitResolveResult> results,
            CharacterTestTraceRecorder traceRecorder)
        {
            if (results == null || results.Count == 0)
                return;

            for (int i = 0; i < results.Count; i++)
            {
                HitResolveResult result = results[i];
                if (!result.IsAcceptedDamage)
                    continue;

                CharacterTestCharacterEntity target = FindEntityByCombatId(result.TargetId);
                if (target == null)
                    continue;

                CharacterTestBattleStateSystem.RefreshEntity(world, target, frame, null);
                if (!target.BattleState.CanReceiveHpDamage)
                    continue;

                int gameplayHp = world.GetCurrentAttribute(
                    target.GameplayEntityId,
                    CharacterTestCharacterAttributeIds.Hp);
                if (!TryFindDeathTelemetry(result, target, out DamageTelemetryRow deathTelemetry))
                {
                    if (gameplayHp <= 0)
                    {
                        TraceDeathSkippedForTelemetryMismatch(
                            traceRecorder,
                            frame,
                            target,
                            result,
                            gameplayHp);
                    }

                    continue;
                }

                int sourceEntityId = CharacterTestCombatEntityLookup.ResolveCharacterEntityId(result.AttackerId);
                int traceId = result.TraceId;
                CharacterActionId actionBefore = target.Action.ActiveActionId;
                CharacterTestCharacterLifeState lifeStateBefore = target.CombatState.LifeState;
                TraceDeathCandidate(
                    traceRecorder,
                    frame,
                    target,
                    sourceEntityId,
                    traceId,
                    deathTelemetry,
                    actionBefore,
                    lifeStateBefore);
                world.SetCurrentAttribute(target.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp, 0);
                world.ClearScatterPressure(target, frame.Value);
                world.SetGameplayLifecycle(target, GameplayLifecycleState.Destroyed);
                target.BeginDeath(frame.Value, sourceEntityId, traceId);
                CharacterTestBattleStateSystem.RefreshEntity(world, target, frame, null);
                _deathEvents.Add(new CharacterTestDeathEvent(
                    target.Id.Value,
                    sourceEntityId,
                    frame.Value,
                    traceId,
                    DeathReason));

                CharacterReactionContextBuildResult reactionContext =
                    CharacterReactionContextBuilder.FromDeathNoAlloc(
                        frame,
                        target.GameplayEntityId,
                        DeathReason,
                        CreateTraceIdentity(traceId));
                bool startedReaction = actionRuntime != null
                    && actionRuntime.TryReact(
                        target.Id,
                        reactionContext.Context,
                        allowFallback: false);
                CharacterTestBattleStateSystem.RefreshEntity(world, target, frame, null);
                TraceDeathBegin(
                    traceRecorder,
                    frame,
                    target,
                    sourceEntityId,
                    traceId,
                    startedReaction);
                if (!startedReaction)
                {
                    actionRuntime?.TerminateForLifecycle(
                        target.Id,
                        frame,
                        CharacterActionLifecycleReason.Death,
                        CreateTraceIdentity(traceId));
                    target.CompleteDeath(frame.Value);
                    world.SetGameplayLifecycle(target, GameplayLifecycleState.Destroyed);
                    CharacterTestBattleStateSystem.RefreshEntity(world, target, frame, null);
                    TraceDeathFallback(traceRecorder, frame, target, sourceEntityId, traceId);
                }
            }
        }

        private static void TraceDeathCandidate(
            CharacterTestTraceRecorder traceRecorder,
            RuntimeFrame frame,
            CharacterTestCharacterEntity target,
            int sourceEntityId,
            int traceId,
            DamageTelemetryRow telemetry,
            CharacterActionId actionBefore,
            CharacterTestCharacterLifeState lifeStateBefore)
        {
            if (traceRecorder == null || target == null)
                return;

            traceRecorder.AttackCombat(
                frame,
                target.Id.Value,
                "death candidate target=" + target.Id.Value
                + " source=" + sourceEntityId
                + " trace=" + traceId
                + " hp=" + telemetry.HpBefore + "->" + telemetry.HpAfter
                + " finalDamage=" + telemetry.FinalDamage
                + " telemetryTarget=" + telemetry.TargetEntityId
                + " telemetrySource=" + telemetry.SourceEntityId
                + " lifeBefore=" + lifeStateBefore
                + " actionBefore=" + CharacterTestCombatTraceText.EmptyAsNone(actionBefore));
        }

        private static void TraceDeathBegin(
            CharacterTestTraceRecorder traceRecorder,
            RuntimeFrame frame,
            CharacterTestCharacterEntity target,
            int sourceEntityId,
            int traceId,
            bool startedReaction)
        {
            if (traceRecorder == null || target == null)
                return;

            traceRecorder.AttackCombat(
                frame,
                target.Id.Value,
                "death begin target=" + target.Id.Value
                + " source=" + sourceEntityId
                + " trace=" + traceId
                + " lifeState=" + target.CombatState.LifeState
                + " lifecycle=Destroyed"
                + " action=" + CharacterTestCombatTraceText.EmptyAsNone(target.Action.ActiveActionId)
                + " reactionStarted=" + startedReaction
                + " reject=" + CharacterTestCombatTraceText.EmptyAsNone(target.Action.LastRejectReason));
        }

        private static void TraceDeathFallback(
            CharacterTestTraceRecorder traceRecorder,
            RuntimeFrame frame,
            CharacterTestCharacterEntity target,
            int sourceEntityId,
            int traceId)
        {
            if (traceRecorder == null || target == null)
                return;

            traceRecorder.AttackCombat(
                frame,
                target.Id.Value,
                "death fallback target=" + target.Id.Value
                + " source=" + sourceEntityId
                + " trace=" + traceId
                + " lifeState=" + target.CombatState.LifeState
                + " lifecycle=Destroyed"
                + " action=" + CharacterTestCombatTraceText.EmptyAsNone(target.Action.ActiveActionId),
                warning: true);
        }

        private void TraceDeathSkippedForTelemetryMismatch(
            CharacterTestTraceRecorder traceRecorder,
            RuntimeFrame frame,
            CharacterTestCharacterEntity target,
            HitResolveResult result,
            int gameplayHp)
        {
            if (traceRecorder == null || target == null)
                return;

            int sourceEntityId = CharacterTestCombatEntityLookup.ResolveCharacterEntityId(result.AttackerId);
            traceRecorder.AttackCombat(
                frame,
                target.Id.Value,
                "death skipped reason=NoMatchingTelemetry target=" + target.Id.Value
                + " source=" + sourceEntityId
                + " trace=" + result.TraceId
                + " resultDamage=" + result.Damage
                + " gameplayHp=" + gameplayHp
                + " telemetryRows=" + _damageTelemetryRows.Count
                + " lifeState=" + target.CombatState.LifeState,
                warning: true);
        }

        private bool TryFindDeathTelemetry(
            HitResolveResult result,
            CharacterTestCharacterEntity target,
            out DamageTelemetryRow telemetry)
        {
            for (int i = 0; i < _damageTelemetryRows.Count; i++)
            {
                DamageTelemetryRow candidate = _damageTelemetryRows[i];
                if (candidate.HpBefore <= 0 || candidate.HpAfter > 0 || candidate.FinalDamage <= 0)
                    continue;
                if (!IsDeathTelemetryAligned(candidate, result, target))
                    continue;

                telemetry = candidate;
                return true;
            }

            telemetry = default;
            return false;
        }

        private static bool IsDeathTelemetryAligned(
            DamageTelemetryRow telemetry,
            HitResolveResult result,
            CharacterTestCharacterEntity target)
        {
            if (target == null)
                return false;

            if (telemetry.TargetEntityId > 0
                && telemetry.TargetEntityId != target.Id.Value
                && telemetry.TargetEntityId != result.TargetId.Value)
            {
                return false;
            }

            int sourceEntityId = CharacterTestCombatEntityLookup.ResolveCharacterEntityId(result.AttackerId);
            if (telemetry.SourceEntityId > 0
                && telemetry.SourceEntityId != sourceEntityId
                && telemetry.SourceEntityId != result.AttackerId.Value)
            {
                return false;
            }

            return true;
        }

        private static bool IsExistingBrokenWindow(
            GameplayScatterPressureComponent scatterPressure,
            long frame)
        {
            return scatterPressure.IsBroken
                && scatterPressure.CurrentBand == PressureBand.Broken
                && scatterPressure.LastCohesionBreakFrame >= 0L
                && scatterPressure.LastCohesionBreakFrame < frame;
        }

        private static void HoldBrokenRecoveryForLaunchedSpecialHits(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame)
        {
            if (world == null)
                return;

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null && entity.SpecialHitState.IsLaunched)
                    world.HoldBrokenScatterRecovery(entity, frame.Value);
            }
        }

        private CharacterReactionHitSource CreateHitReactionSource(
            RuntimeFrame frame,
            HitResolveResult result,
            DamageProfileResolveResult payload,
            CharacterTestCharacterEntity target,
            GameplayScatterPressureComponent scatterPressure,
            CharacterTestHitZoneResolution hitZone)
        {
            return CreateHitReactionSource(
                frame,
                result,
                payload,
                target,
                scatterPressure,
                hitZone,
                hitZone.ReactionGroupId,
                isAirborne: false);
        }

        private CharacterReactionHitSource CreateLaunchHitReactionSource(
            RuntimeFrame frame,
            HitResolveResult result,
            DamageProfileResolveResult payload,
            CharacterTestCharacterEntity target,
            GameplayScatterPressureComponent scatterPressure,
            CharacterTestHitZoneResolution hitZone)
        {
            return CreateHitReactionSource(
                frame,
                result,
                payload,
                target,
                scatterPressure,
                hitZone,
                CharacterTestReactionGroupIds.SpecialLaunch,
                target.SpecialHitState.IsLaunched);
        }

        private CharacterReactionHitSource CreateHitReactionSource(
            RuntimeFrame frame,
            HitResolveResult result,
            DamageProfileResolveResult payload,
            CharacterTestCharacterEntity target,
            GameplayScatterPressureComponent scatterPressure,
            CharacterTestHitZoneResolution hitZone,
            int reactionGroupId,
            bool isAirborne)
        {
            int sourceEntityId = CharacterTestCombatEntityLookup.ResolveCharacterEntityId(result.AttackerId);
            return new CharacterReactionHitSource(
                frame,
                target.GameplayEntityId,
                hitZone.BodyPartId,
                hitZone.HitZoneId,
                payload.Success ? payload.DamageTypeId : 0,
                CharacterHitDirection.Unknown,
                payload.Success && payload.ImpactPower > 0 ? payload.ImpactPower : (int?)null,
                reactionGroupId: reactionGroupId,
                sourceId: sourceEntityId,
                reason: GetHitResolveKindReason(result.Kind),
                traceId: new RuntimeTraceIdentity(CombatTraceKindId, sourceEntityId, result.ActionInstanceId, result.TraceId),
                previousPressureBand: scatterPressure.CurrentBand,
                currentScatterPressureBand: scatterPressure.CurrentBand,
                previousPressure: scatterPressure.CurrentScatterPressure,
                currentScatterPressure: scatterPressure.CurrentScatterPressure,
                maxScatterPressure: scatterPressure.MaxScatterPressure,
                delta: 0,
                isAirborne: isAirborne,
                currentActionId: target.Action.ActiveActionId);
        }

        private static int ResolveLaunchVelocityYMillimetersPerSecond(
            int launchPower,
            CharacterTestCharacterEntity target)
        {
            if (launchPower <= 0)
                return 0;

            float targetMass = target != null && target.Physics.EffectiveMassKg > 0f
                ? target.Physics.EffectiveMassKg
                : CharacterTestImpactResponseResolver.DefaultMassKg;
            float massScale = Mathf.Clamp(
                Mathf.Sqrt(LaunchReferenceMassKg / targetMass),
                MinimumLaunchMassScale,
                MaximumLaunchMassScale);
            float velocityMetersPerSecond = StandardLaunchVelocityYMetersPerSecond
                * launchPower
                / StandardLaunchPower
                * massScale;
            int velocity = Mathf.RoundToInt(velocityMetersPerSecond * CharacterTestDeterministicUnits.PositionScale);
            return velocity <= 0
                ? 0
                : Math.Min(velocity, MaximumLaunchVelocityYMillimetersPerSecond);
        }

        private static void EnqueueLaunchVelocity(
            CharacterTestCharacterEntity target,
            DamageProfileResolveResult payload,
            int launchVelocityYMillimetersPerSecond,
            bool appendToCurrent)
        {
            if (target == null || launchVelocityYMillimetersPerSecond <= 0)
                return;

            int currentVelocityY = target.VerticalVelocityMillimetersPerSecond;
            int targetVelocityY = appendToCurrent
                ? currentVelocityY + launchVelocityYMillimetersPerSecond
                : Math.Max(currentVelocityY, launchVelocityYMillimetersPerSecond);
            targetVelocityY = Math.Min(
                Math.Max(0, targetVelocityY),
                MaximumLaunchVelocityYMillimetersPerSecond);
            if (targetVelocityY <= 0)
                return;

            target.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.VelocityOverrideRaw(
                target.HorizontalVelocityXMillimetersPerSecond,
                targetVelocityY,
                target.HorizontalVelocityYMillimetersPerSecond,
                CharacterTestCharacterMotionRequestSpace.World,
                CharacterMovementMode.Airborne,
                payload.Usage.ActionId,
                (payload.Usage.StableEventId == 0)
                    ? LaunchPowerMotionStableEventId
                    : payload.Usage.StableEventId));
        }

        private static string GetHitResolveKindReason(HitResolveKind kind)
        {
            switch (kind)
            {
                case HitResolveKind.Damage:
                    return HitResolveKindDamageReason;
                case HitResolveKind.Blocked:
                    return HitResolveKindBlockedReason;
                case HitResolveKind.Duplicate:
                    return HitResolveKindDuplicateReason;
                case HitResolveKind.Friendly:
                    return HitResolveKindFriendlyReason;
                case HitResolveKind.Invincible:
                    return HitResolveKindInvincibleReason;
                case HitResolveKind.Evaded:
                    return HitResolveKindEvadedReason;
                case HitResolveKind.Parried:
                    return HitResolveKindParriedReason;
                case HitResolveKind.SelfDamage:
                    return HitResolveKindSelfDamageReason;
                case HitResolveKind.TargetDead:
                    return HitResolveKindTargetDeadReason;
                default:
                    return HitResolveKindUnknownReason;
            }
        }

        private CharacterTestCharacterEntity FindEntity(GameplayEntityId gameplayEntityId)
        {
            return _entitiesByGameplayId.TryGetValue(gameplayEntityId, out CharacterTestCharacterEntity entity)
                ? entity
                : null;
        }

        private CharacterTestCharacterEntity FindEntityByCombatId(
            MxFramework.Combat.Core.CombatEntityId combatEntityId)
        {
            return FindEntityByCharacterId(CharacterTestCombatEntityLookup.ResolveCharacterEntityId(combatEntityId));
        }

        private CharacterTestCharacterEntity FindEntityByCharacterId(int entityId)
        {
            if (entityId <= 0)
                return null;

            return _entitiesByCharacterId.TryGetValue(entityId, out CharacterTestCharacterEntity entity)
                ? entity
                : null;
        }

        private static RuntimeTraceIdentity CreateTraceIdentity(int traceId)
        {
            return new RuntimeTraceIdentity(CombatTraceKindId, 0, 0L, traceId);
        }

        private GameplaySystemContext CreateGameplayContext(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            IReadOnlyList<RuntimeCommand> commands,
            GameplayCommandExecutionState commandState)
        {
            commandState.Clear();
            return new GameplaySystemContext(
                frame,
                0d,
                0d,
                _gameplayWorld,
                commands ?? EmptyRuntimeCommands,
                world.GameplayComponentWorld.Events,
                commandState,
                componentWorld: world.GameplayComponentWorld);
        }

        private readonly struct LaunchSpecialHitCandidate
        {
            public LaunchSpecialHitCandidate(
                HitResolveResult result,
                CharacterTestCharacterEntity target,
                DamageProfileResolveResult payload,
                GameplayScatterPressureComponent scatterPressure,
                bool alreadyLaunched,
                int launchVelocityYMillimetersPerSecond)
            {
                Result = result;
                Target = target;
                Payload = payload;
                ScatterPressure = scatterPressure;
                AlreadyLaunched = alreadyLaunched;
                LaunchVelocityYMillimetersPerSecond = launchVelocityYMillimetersPerSecond;
            }

            public HitResolveResult Result { get; }
            public CharacterTestCharacterEntity Target { get; }
            public DamageProfileResolveResult Payload { get; }
            public GameplayScatterPressureComponent ScatterPressure { get; }
            public bool AlreadyLaunched { get; }
            public int LaunchVelocityYMillimetersPerSecond { get; }
        }
    }

    internal readonly struct CharacterTestScatterPressureTrace
    {
        public CharacterTestScatterPressureTrace(
            int targetEntityId,
            int sourceEntityId,
            int delta,
            int damageId,
            int traceId,
            int impactPower = 0,
            float effectiveImpact = 0f,
            float sourceMassKg = 0f,
            float targetMassKg = 0f,
            float knockbackSpeed = 0f,
            float previewDistanceMeters = 0f)
        {
            TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
            SourceEntityId = sourceEntityId < 0 ? 0 : sourceEntityId;
            Delta = delta < 0 ? 0 : delta;
            DamageId = damageId;
            TraceId = traceId < 0 ? 0 : traceId;
            ImpactPower = impactPower < 0 ? 0 : impactPower;
            EffectiveImpact = effectiveImpact < 0f ? 0f : effectiveImpact;
            SourceMassKg = sourceMassKg < 0f ? 0f : sourceMassKg;
            TargetMassKg = targetMassKg < 0f ? 0f : targetMassKg;
            KnockbackSpeed = knockbackSpeed < 0f ? 0f : knockbackSpeed;
            PreviewDistanceMeters = previewDistanceMeters < 0f ? 0f : previewDistanceMeters;
        }

        public int TargetEntityId { get; }
        public int SourceEntityId { get; }
        public int Delta { get; }
        public int DamageId { get; }
        public int TraceId { get; }
        public int ImpactPower { get; }
        public float EffectiveImpact { get; }
        public float SourceMassKg { get; }
        public float TargetMassKg { get; }
        public float KnockbackSpeed { get; }
        public float PreviewDistanceMeters { get; }
    }

    internal readonly struct CharacterTestDeathEvent
    {
        public CharacterTestDeathEvent(
            int targetEntityId,
            int sourceEntityId,
            long frame,
            int traceId,
            string reason)
        {
            TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
            SourceEntityId = sourceEntityId < 0 ? 0 : sourceEntityId;
            Frame = frame < 0L ? 0L : frame;
            TraceId = traceId < 0 ? 0 : traceId;
            Reason = reason ?? string.Empty;
        }

        public int TargetEntityId { get; }
        public int SourceEntityId { get; }
        public long Frame { get; }
        public int TraceId { get; }
        public string Reason { get; }
    }
}


