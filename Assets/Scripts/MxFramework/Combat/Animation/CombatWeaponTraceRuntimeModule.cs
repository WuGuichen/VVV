using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Runtime;

namespace MxFramework.Combat.Animation
{
    public sealed class CombatWeaponTraceRuntimeModule : RuntimeModule
    {
        public const string DefaultModuleId = "combat.weapontrace";
        public const int DefaultPriority = 10;

        private readonly List<HitCandidate> _rawCandidates = new List<HitCandidate>();
        private readonly List<HitCandidate> _deduplicatedCandidates = new List<HitCandidate>();
        private ICombatAnimationContext _animationContext;
        private CombatWeaponTraceEvaluator _evaluator;
        private CombatHitCollector _hitCollector;
        private CombatFixedStepDriver _fixedStepDriver;
        private CombatFixedStepActionHistory _actionHistory;

        public CombatWeaponTraceRuntimeModule(int priority = DefaultPriority)
            : base(DefaultModuleId, RuntimeTickStage.PostSimulation, priority)
        {
        }

        public override void Initialize(RuntimeHostContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            _animationContext = context.Services.Get<ICombatAnimationContext>();
            _fixedStepDriver = CombatFixedStepDriverServices.GetOrCreate(context);
            _actionHistory = CombatFixedStepDriverServices.GetOrCreateActionHistory(context);
            CombatActionRunner runner = _animationContext.ActionRunner;
            var physicsWorld = context.Services.Get<CombatPhysicsWorld>();
            var traceProvider = context.Services.Get<ICombatActionTraceProvider>();
            _evaluator = new CombatWeaponTraceEvaluator(runner, traceProvider, physicsWorld);
            _hitCollector = new CombatHitCollector();
        }

        public override void Tick(RuntimeTickContext context)
        {
            if (context.Stage != TickStage || _evaluator == null || _hitCollector == null)
            {
                return;
            }

            _rawCandidates.Clear();
            _deduplicatedCandidates.Clear();
            CombatFixedStepBatch batch = _fixedStepDriver.Advance(context);
            if (!batch.HasSteps || !_actionHistory.TryGetSnapshots(context.FrameIndex, out IReadOnlyList<CombatActionStepSnapshot> snapshots))
            {
                _animationContext.SetLastFrameHitCandidates(_deduplicatedCandidates);
                return;
            }

            for (int i = 0; i < snapshots.Count; i++)
            {
                CombatActionStepSnapshot snapshot = snapshots[i];
                _evaluator.EvaluateAll(snapshot.Frame, snapshot.ActionStates, _rawCandidates);
            }
            _hitCollector.Collect(_rawCandidates, _deduplicatedCandidates);
            _animationContext.SetLastFrameHitCandidates(_deduplicatedCandidates);
        }

        public override void Stop(RuntimeHostContext context)
        {
            _hitCollector?.Reset();
            _rawCandidates.Clear();
            _deduplicatedCandidates.Clear();
        }

        public override void Dispose()
        {
            _evaluator = null;
            _hitCollector = null;
            _animationContext = null;
            _fixedStepDriver = null;
            _actionHistory = null;
            _rawCandidates.Clear();
            _deduplicatedCandidates.Clear();
        }

    }
}
