using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Animation
{
    public sealed class CombatWeaponTraceEvaluator
    {
        private static readonly Fix64 DefaultSweepMaxTipDistancePerSubstep = Fix64.FromRatio(25, 100);
        private const int ProximalTraceResolvePriority = -1;

        private readonly CombatActionRunner _actionRunner;
        private readonly ICombatActionTraceProvider _traceProvider;
        private readonly CombatPhysicsWorld _physicsWorld;
        private readonly Func<CombatEntityId, HitTargetStateFlags> _targetStateResolver;
        private readonly bool _useTipSweepCapsules;
        private readonly Fix64 _sweepMaxTipDistancePerSubstep;
        private readonly int _sweepMaxSubsteps;
        private readonly List<WeaponTraceFrame> _traceBuffer = new List<WeaponTraceFrame>();
        private readonly List<CombatCapsuleQuery> _queryBuffer = new List<CombatCapsuleQuery>();
        private readonly List<CombatQueryResult> _queryResults = new List<CombatQueryResult>();

        public CombatWeaponTraceEvaluator(
            CombatActionRunner actionRunner,
            ICombatActionTraceProvider traceProvider,
            CombatPhysicsWorld physicsWorld)
            : this(actionRunner, traceProvider, physicsWorld, null)
        {
        }

        public CombatWeaponTraceEvaluator(
            CombatActionRunner actionRunner,
            ICombatActionTraceProvider traceProvider,
            CombatPhysicsWorld physicsWorld,
            Func<CombatEntityId, HitTargetStateFlags> targetStateResolver)
            : this(
                actionRunner,
                traceProvider,
                physicsWorld,
                targetStateResolver,
                useTipSweepCapsules: false,
                DefaultSweepMaxTipDistancePerSubstep,
                maxSweepSubsteps: 8)
        {
        }

        public CombatWeaponTraceEvaluator(
            CombatActionRunner actionRunner,
            ICombatActionTraceProvider traceProvider,
            CombatPhysicsWorld physicsWorld,
            Func<CombatEntityId, HitTargetStateFlags> targetStateResolver,
            bool useTipSweepCapsules,
            Fix64 sweepMaxTipDistancePerSubstep,
            int maxSweepSubsteps)
        {
            _actionRunner = actionRunner ?? throw new ArgumentNullException(nameof(actionRunner));
            _traceProvider = traceProvider ?? throw new ArgumentNullException(nameof(traceProvider));
            _physicsWorld = physicsWorld ?? throw new ArgumentNullException(nameof(physicsWorld));
            _targetStateResolver = targetStateResolver ?? DefaultTargetStateResolver;
            _useTipSweepCapsules = useTipSweepCapsules;
            _sweepMaxTipDistancePerSubstep = sweepMaxTipDistancePerSubstep <= Fix64.Zero
                ? DefaultSweepMaxTipDistancePerSubstep
                : sweepMaxTipDistancePerSubstep;
            _sweepMaxSubsteps = Math.Max(1, maxSweepSubsteps);
        }

        public void EvaluateAll(CombatFrame currentFrame, List<HitCandidate> results)
        {
            EvaluateAll(currentFrame, _actionRunner.GetRunningActions(), results);
        }

        public void EvaluateAll(CombatFrame currentFrame, IReadOnlyList<CombatActionState> runningActions, List<HitCandidate> results)
        {
            if (runningActions == null)
            {
                throw new ArgumentNullException(nameof(runningActions));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            int queryId = 0;
            for (int i = 0; i < runningActions.Count; i++)
            {
                CombatActionState actionState = runningActions[i];
                if (actionState.Phase != CombatActionPhase.Active)
                {
                    continue;
                }

                int actionInstanceId = actionState.ActionInstanceId;
                if (actionInstanceId <= 0)
                {
                    actionInstanceId = _actionRunner.GetActionInstanceId(actionState.EntityId);
                    if (actionInstanceId <= 0)
                    {
                        continue;
                    }
                }

                _traceBuffer.Clear();
                _traceProvider.GetActiveTraces(
                    actionState.EntityId,
                    actionState.ActionId,
                    actionInstanceId,
                    actionState.LocalFrame,
                    _traceBuffer);

                for (int traceIndex = 0; traceIndex < _traceBuffer.Count; traceIndex++)
                {
                    WeaponTraceFrame trace = _traceBuffer[traceIndex];
                    if (_useTipSweepCapsules)
                    {
                        _queryBuffer.Clear();
                        int builtQueries = WeaponTraceQueryBuilder.BuildTipSweepCapsules(
                            trace,
                            actionState.EntityId,
                            actionState.ActionId,
                            queryId,
                            _sweepMaxTipDistancePerSubstep,
                            _sweepMaxSubsteps,
                            _queryBuffer);
                        queryId += builtQueries;

                        for (int queryIndex = 0; queryIndex < _queryBuffer.Count; queryIndex++)
                            QueryAndAppendCandidates(
                                actionState,
                                actionInstanceId,
                                trace,
                                currentFrame,
                                _queryBuffer[queryIndex],
                                results,
                                incomingSweep: true);
                    }
                    else
                    {
                        CombatCapsuleQuery query = WeaponTraceQueryBuilder.BuildCurrentBladeCapsule(
                            trace,
                            actionState.EntityId,
                            actionState.ActionId,
                            queryId++,
                            traceIndex);
                        QueryAndAppendCandidates(
                            actionState,
                            actionInstanceId,
                            trace,
                            currentFrame,
                            query,
                            results);
                    }

                    if (WeaponTraceQueryBuilder.TryBuildCurrentProximalCapsule(
                            trace,
                            actionState.EntityId,
                            actionState.ActionId,
                            queryId++,
                            traceIndex,
                            out CombatCapsuleQuery proximalQuery))
                    {
                        QueryAndAppendCandidates(
                            actionState,
                            actionInstanceId,
                            trace,
                            currentFrame,
                            proximalQuery,
                            results,
                            ProximalTraceResolvePriority);
                    }
                }
            }
        }

        private void QueryAndAppendCandidates(
            CombatActionState actionState,
            int actionInstanceId,
            WeaponTraceFrame trace,
            CombatFrame currentFrame,
            CombatCapsuleQuery query,
            List<HitCandidate> results,
            int resolvePriority = 0,
            bool incomingSweep = false)
        {
            _queryResults.Clear();
            _physicsWorld.QueryCapsule(query, _queryResults, includeSourceEntity: false);
            for (int hitIndex = 0; hitIndex < _queryResults.Count; hitIndex++)
            {
                CombatQueryResult physicsHit = _queryResults[hitIndex];
                results.Add(new HitCandidate(
                    actionState.EntityId,
                    physicsHit.TargetEntityId,
                    actionState.ActionId,
                    actionInstanceId,
                    trace.TraceId,
                    currentFrame,
                    physicsHit,
                    damage: 0,
                    staggerFrames: 0,
                    knockback: FixVector3.Zero,
                    targetState: _targetStateResolver(physicsHit.TargetEntityId),
                    resolvePriority: resolvePriority,
                    incomingDirection: incomingSweep ? query.PointB - query.PointA
                        : resolvePriority == ProximalTraceResolvePriority ? WeaponTraceQueryBuilder.ResolveProximalIncomingDisplacement(trace, physicsHit.Point)
                        : WeaponTraceQueryBuilder.ResolveBladeIncomingDisplacement(trace, physicsHit.Point)));
            }
        }

        private static HitTargetStateFlags DefaultTargetStateResolver(CombatEntityId targetEntityId)
        {
            return HitTargetStateFlags.Alive;
        }
    }
}
