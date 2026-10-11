using System;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        internal int TacticalBootstrapStartFrame { get; private set; }
        internal int TacticalBootstrapEndFrame { get; private set; }
        internal int TacticalBootstrapSuppressedTicks { get; private set; }
        internal long TacticalBootstrapSubmittedCommands { get; private set; }
        internal int TacticalBootstrapActiveActions { get; private set; }

        /// <summary>Runs only startup spawning/world work with automatic AI command generation suppressed.</summary>
        private bool BootstrapTacticalActors(out string diagnostic)
        {
            diagnostic = string.Empty;
            var runtime = _context?.Slice?.CharacterRuntime;
            var module = _context?.Slice?.RuntimeAiPlannerCommands;
            if (runtime == null || module == null || _gameManager == null) { diagnostic = "战术初始化运行上下文不可用。"; return false; }
            if (!module.BootstrapDecisionsSuppressed) { diagnostic = "战术初始化必须先抑制自动 AI 决策。"; return false; }
            int expected = CountEnabledSetupSlots();
            int CountActors() => runtime.Entities.Count(e => e.Control.Owner == CharacterTestCharacterControlOwner.AiControlled);
            TacticalBootstrapStartFrame = (int)_context.Slice.CurrentFrame.Value;
            long submitted = module.SubmittedCommandCount; int ticks = module.BootstrapSuppressedTicks;
            bool stepped = false;
            for (int i = 0; CountActors() < expected && i < MaxSessionInitializationFrames; i++)
            {
                if (!_gameManager.StepSimulationOnceForRuntimeAiPlannerDev()) { diagnostic = "战术角色初始化 Tick 失败。"; return false; }
                stepped = true;
            }
            if (CountActors() < expected) { diagnostic = "战术初始化未生成全部实际角色。"; return false; }
            // Complete the spawned actors' first physics/action registration before hashing actual inputs.
            if (stepped && !_gameManager.StepSimulationOnceForRuntimeAiPlannerDev()) { diagnostic = "战术角色注册 Tick 失败。"; return false; }
            TacticalBootstrapEndFrame = (int)_context.Slice.CurrentFrame.Value;
            TacticalBootstrapSuppressedTicks = module.BootstrapSuppressedTicks - ticks;
            TacticalBootstrapSubmittedCommands = module.SubmittedCommandCount - submitted;
            TacticalBootstrapActiveActions = runtime.Entities.Count(e => e.Control.Owner == CharacterTestCharacterControlOwner.AiControlled && e.Action.State == MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running);
            if (TacticalBootstrapSubmittedCommands != 0) { diagnostic = "初始化期间出现自动 AI 命令，拒绝建立基线。"; return false; }
            return true;
        }
    }
}
