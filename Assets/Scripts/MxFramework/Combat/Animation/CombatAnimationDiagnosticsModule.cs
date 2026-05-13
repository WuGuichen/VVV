using System;
using MxFramework.Runtime;

namespace MxFramework.Combat.Animation
{
    public sealed class CombatAnimationDiagnosticsModule : RuntimeModule
    {
        public const string DefaultModuleId = "combat.animation.diagnostics";
        public const int DefaultPriority = 10;

        private ICombatAnimationContext _animationContext;

        public CombatAnimationDiagnosticsModule(int priority = DefaultPriority)
            : base(DefaultModuleId, RuntimeTickStage.Diagnostics, priority)
        {
        }

        public override void Initialize(RuntimeHostContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            _animationContext = context.Services.Get<ICombatAnimationContext>();
        }

        public override void Tick(RuntimeTickContext context)
        {
            if (context.Stage != TickStage || _animationContext == null)
            {
                return;
            }

            CombatActionState[] runningActions = _animationContext.ActionRunner.GetRunningActions();
            int activePhaseCount = 0;
            for (int i = 0; i < runningActions.Length; i++)
            {
                if (runningActions[i].Phase == CombatActionPhase.Active)
                {
                    activePhaseCount++;
                }
            }

            _animationContext.SetLastSnapshot(new CombatAnimationSnapshot(
                runningActions.Length,
                activePhaseCount,
                _animationContext.LastFrameHitCandidates.Count,
                context.FrameIndex));
        }

        public override void Dispose()
        {
            _animationContext = null;
        }
    }
}
