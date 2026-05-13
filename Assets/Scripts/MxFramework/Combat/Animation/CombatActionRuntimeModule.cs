using System;
using MxFramework.Combat.Core;
using MxFramework.Runtime;

namespace MxFramework.Combat.Animation
{
    public sealed class CombatActionRuntimeModule : RuntimeModule
    {
        public const string DefaultModuleId = "combat.action";
        public const int DefaultPriority = 10;

        private ICombatAnimationContext _animationContext;
        private CombatActionRunner _runner;

        public CombatActionRuntimeModule(int priority = DefaultPriority)
            : base(DefaultModuleId, RuntimeTickStage.Simulation, priority)
        {
        }

        public override void Initialize(RuntimeHostContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            _animationContext = context.Services.Get<ICombatAnimationContext>();
            var registry = context.Services.Get<CombatActionRegistry>();
            _runner = new CombatActionRunner(registry);
            _animationContext.SetActionRunner(_runner);
        }

        public override void Tick(RuntimeTickContext context)
        {
            if (context.Stage != TickStage || _runner == null)
            {
                return;
            }

            _runner.TickActions(ToCombatFrame(context.FrameIndex));
        }

        public override void Stop(RuntimeHostContext context)
        {
            if (_runner == null)
            {
                return;
            }

            CombatActionState[] runningActions = _runner.GetRunningActions();
            for (int i = 0; i < runningActions.Length; i++)
            {
                _runner.ForceCancel(runningActions[i].EntityId);
            }
        }

        public override void Dispose()
        {
            _runner = null;
            _animationContext = null;
        }

        private static CombatFrame ToCombatFrame(long frameIndex)
        {
            if (frameIndex > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex), "Combat frame cannot exceed Int32.MaxValue.");
            }

            return new CombatFrame((int)frameIndex);
        }
    }
}
