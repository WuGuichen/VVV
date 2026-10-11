using System;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeSessionBuilder
    {
        internal ulong ReserveCardTrialStageRunId()
        {
            _tacticalStageRunId = NextStageRunId(HighestKnownTacticalStageRunId());
            return _tacticalStageRunId;
        }

        // Uses the same prepare/publish transaction as a confirmed tactical deployment.
        // The Lab owns only the frozen initial conditions, never a second executor.
        internal void RebuildCardTrialBattleSession(TacticalDeployment deployment)
        {
            if (deployment?.CardTrial == null) throw new ArgumentException("需要独立试验部署。", nameof(deployment));
            using var candidate = PrepareTacticalSession(deployment);
            PublishTacticalSession(candidate, "AI Lab 真实出牌测试 · 独立离线会话");
            _context.MainMenuVisible = false;
            _context.ManualPaused = false;
            _context.MainMenuCoordinator.SetPaused(false);
        }
    }
}
