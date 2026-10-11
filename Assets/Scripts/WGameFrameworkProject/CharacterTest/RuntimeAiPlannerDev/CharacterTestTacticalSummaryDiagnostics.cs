using System;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestTacticalSummaryDiagnostics
    {
        internal static bool TryDescribe(string diagnostic, out string reason, out string guidance)
        {
            reason = guidance = "";
            if (string.IsNullOrEmpty(diagnostic)) return false;
            foreach (string prefix in new[] { "WeaponBakeFrameUnavailable:", "SocketBakeFrameUnavailable:" })
            {
                int start = diagnostic.IndexOf(prefix, StringComparison.Ordinal);
                if (start < 0) continue;
                start += prefix.Length;
                int end = start;
                while (end < diagnostic.Length && char.IsDigit(diagnostic[end])) end++;
                string frame = end > start ? "（第 " + diagnostic.Substring(start, end - start) + " 帧）" : "";
                reason = (prefix.StartsWith("Weapon", StringComparison.Ordinal) ? "武器轨迹数据缺失或过期" : "动作挂点数据缺失或过期") + frame;
                guidance = "先按当前动作时间轴重新烘焙该角色的轨迹资源，再重建战术摘要。摘要重建只进行测量，不能补齐缺失的轨迹。";
                return true;
            }
            return false;
        }
    }
}
