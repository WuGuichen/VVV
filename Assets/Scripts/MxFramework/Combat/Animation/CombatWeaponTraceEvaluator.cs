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
        private readonly CombatActionRunner _actionRunner;
        private readonly ICombatActionTraceProvider _traceProvider;
        private readonly CombatPhysicsWorld _physicsWorld;
        private readonly Func<CombatEntityId, HitTargetStateFlags> _targetStateResolver;
        private readonly List<WeaponTraceFrame> _traceBuffer = new List<WeaponTraceFrame>();
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
        {
            _actionRunner = actionRunner ?? throw new ArgumentNullException(nameof(actionRunner));
            _traceProvider = traceProvider ?? throw new ArgumentNullException(nameof(traceProvider));
            _physicsWorld = physicsWorld ?? throw new ArgumentNullException(nameof(physicsWorld));
            _targetStateResolver = targetStateResolver ?? DefaultTargetStateResolver;
        }

        public void EvaluateAll(CombatFrame currentFrame, List<HitCandidate> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            CombatActionState[] runningActions = _actionRunner.GetRunningActions();
            int queryId = 0;
            for (int i = 0; i < runningActions.Length; i++)
            {
                CombatActionState actionState = runningActions[i];
                if (actionState.Phase != CombatActionPhase.Active)
                {
                    continue;
                }

                int actionInstanceId = _actionRunner.GetActionInstanceId(actionState.EntityId);
                if (actionInstanceId <= 0)
                {
                    continue;
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
                    CombatCapsuleQuery query = WeaponTraceQueryBuilder.BuildCurrentBladeCapsule(
                        trace,
                        actionState.EntityId,
                        actionState.ActionId,
                        queryId++,
                        traceIndex);

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
                            targetState: _targetStateResolver(physicsHit.TargetEntityId)));
                    }
                }
            }
        }

        private static HitTargetStateFlags DefaultTargetStateResolver(CombatEntityId targetEntityId)
        {
            return HitTargetStateFlags.Alive;
        }
    }
}
