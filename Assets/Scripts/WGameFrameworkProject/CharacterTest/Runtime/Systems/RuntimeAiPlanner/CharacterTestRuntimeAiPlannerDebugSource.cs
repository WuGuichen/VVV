using System;
using System.Globalization;
using System.Text;
using MxFramework.Diagnostics;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiPlannerDebugSource : IFrameworkDebugSource
    {
        public const string DefaultSourceName = "CharacterTest Runtime AI Planner";

        private readonly Func<CharacterTestRuntimeAiPlannerSnapshot> _snapshotProvider;

        public CharacterTestRuntimeAiPlannerDebugSource(
            CharacterTestRuntimeAiPlannerCommandModule module,
            string name = DefaultSourceName)
            : this(
                module != null ? (Func<CharacterTestRuntimeAiPlannerSnapshot>)module.CreateSnapshot : null,
                name)
        {
        }

        public CharacterTestRuntimeAiPlannerDebugSource(
            Func<CharacterTestRuntimeAiPlannerSnapshot> snapshotProvider,
            string name = DefaultSourceName)
        {
            _snapshotProvider = snapshotProvider;
            Name = string.IsNullOrWhiteSpace(name) ? DefaultSourceName : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _snapshotProvider != null;

        /// <remarks>AllocByDesign: debug/report snapshot formatting.</remarks>
        public FrameworkDebugSnapshot CreateSnapshot()
        {
            if (_snapshotProvider == null)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("Status", "unavailable") });
            }

            CharacterTestRuntimeAiPlannerSnapshot snapshot = _snapshotProvider();
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Summary", CreateSummary(snapshot)),
                    new FrameworkDebugSection("Agents", CreateAgents(snapshot)),
                    new FrameworkDebugSection("Validation", EmptyAsNone(snapshot.ValidationReport))
                });
        }

        private static string CreateSummary(CharacterTestRuntimeAiPlannerSnapshot snapshot)
        {
            if (snapshot == null)
                return "available=false";

            return "available=" + (snapshot.Available ? "true" : "false")
                + "\nprofile=" + EmptyAsNone(snapshot.ProfileId)
                + "\nagents=" + (snapshot.Agents != null ? snapshot.Agents.Count : 0);
        }

        private static string CreateAgents(CharacterTestRuntimeAiPlannerSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Agents == null || snapshot.Agents.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.Agents.Count; i++)
            {
                CharacterTestRuntimeAiPlannerAgentSnapshot agent = snapshot.Agents[i];
                if (i > 0)
                    builder.AppendLine();

                builder.Append("agent=").Append(agent.EntityId)
                    .Append(" state=").Append(EmptyAsNone(agent.ActiveStatePath))
                    .Append(" target=").Append(agent.TargetEntityId)
                    .Append('\n')
                    .Append("facts targetExists=").Append(agent.TargetExists ? "true" : "false")
                    .Append(" inAttackRange=").Append(agent.TargetInAttackRange ? "true" : "false")
                    .Append(" visible=").Append(agent.TargetVisible ? "true" : "false")
                    .Append(" dead=").Append(agent.TargetDead ? "true" : "false")
                    .Append(" blocked=").Append(agent.TargetLineOfSightBlocked ? "true" : "false")
                    .Append(" lastSeen=").Append(agent.TargetLastSeenAgeFrames)
                    .Append(" distance=").Append(agent.TargetDistanceAvailable
                        ? agent.TargetDistance.ToString("F2", CultureInfo.InvariantCulture)
                        : "unavailable")
                    .Append(" rootDistance=").Append(agent.TargetRootDistance.ToString("F2", CultureInfo.InvariantCulture))
                    .Append(" threat=").Append(agent.TargetThreatScore.ToString("F2", CultureInfo.InvariantCulture))
                    .Append(" path=").Append(agent.MovePathAvailable ? "true" : "false")
                    .Append(" direct=").Append(agent.MoveDirectPathClear ? "true" : "false")
                    .Append(" candidate=").Append(agent.MoveCandidateIndex)
                    .Append('\n')
                    .Append("perception surface=").Append(agent.SelfSurfaceKind)
                    .Append(" hardSlick=").Append(agent.SelfOnHardSlick ? "true" : "false")
                    .Append(" movable=").Append(agent.SelfNearMovableObject ? "true" : "false")
                    .Append(" obstacle=").Append(agent.EnvironmentNearObstacle ? "true" : "false")
                    .Append(" hazard=").Append(agent.EnvironmentNearHazard ? "true" : "false")
                    .Append(" useful=").Append(agent.EnvironmentNearUsefulObject ? "true" : "false")
                    .Append('\n')
                    .Append("utility score=").Append(agent.SelectedUtilityScore.ToString("F2", CultureInfo.InvariantCulture))
                    .Append(" cost=").Append(agent.SelectedActionCost.ToString("F2", CultureInfo.InvariantCulture))
                    .Append(" action=").Append(agent.SelectedActionId)
                    .Append(" kind=").Append(agent.SelectedActionKind)
                    .Append(" name=").Append(EmptyAsNone(agent.SelectedActionName))
                    .Append('\n')
                    .Append("movement speedScale=").Append(agent.MovementSpeedScale.ToString("F2", CultureInfo.InvariantCulture))
                    .Append(" strafeSide=").Append(agent.MovementStrafeSide)
                    .Append(" holdFrames=").Append(agent.MovementDirectionHoldFrames)
                    .Append(" reactionFrames=").Append(agent.MovementReactionDelayFrames)
                    .Append('\n')
                    .Append("lastCommand=").Append(EmptyAsNone(agent.LastCommand))
                    .Append(" suppressed=").Append(EmptyAsNone(agent.SuppressedReason))
                    .Append(" reject=").Append(EmptyAsNone(agent.RejectReason))
                    .Append(" transition=").Append(FormatTransition(agent))
                    .Append('\n')
                    .Append("decisionReason=").Append(CreateDecisionReason(agent))
                    .Append('\n')
                    .Append("conditionSummary=").Append(CreateConditionSummary(agent));
            }

            return builder.ToString();
        }

        private static string CreateDecisionReason(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (!string.IsNullOrWhiteSpace(agent.RejectReason))
                return "Rejected: " + agent.RejectReason;

            if (!string.IsNullOrWhiteSpace(agent.SuppressedReason)
                && !string.Equals(agent.SuppressedReason, "None", StringComparison.Ordinal))
            {
                return "Suppressed: " + agent.SuppressedReason;
            }

            if (agent.SelectedActionId <= 0)
                return "No executable action selected.";

            return "Transition=" + FormatTransition(agent)
                + "; selected=" + EmptyAsNone(agent.SelectedActionName)
                + "; score=" + agent.SelectedUtilityScore.ToString("F2", CultureInfo.InvariantCulture)
                + "; cost=" + agent.SelectedActionCost.ToString("F2", CultureInfo.InvariantCulture)
                + "; moveSpeed=" + agent.MovementSpeedScale.ToString("F2", CultureInfo.InvariantCulture)
                + "; strafeSide=" + agent.MovementStrafeSide;
        }

        private static string CreateConditionSummary(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            return "targetExists=" + (agent.TargetExists ? "true" : "false")
                + ", visible=" + (agent.TargetVisible ? "true" : "false")
                + ", dead=" + (agent.TargetDead ? "true" : "false")
                + ", blocked=" + (agent.TargetLineOfSightBlocked ? "true" : "false")
                + ", inRange=" + (agent.TargetInAttackRange ? "true" : "false")
                + ", lastSeen=" + agent.TargetLastSeenAgeFrames
                + ", threat=" + agent.TargetThreatScore.ToString("F2", CultureInfo.InvariantCulture)
                + ", path=" + (agent.MovePathAvailable ? "true" : "false")
                + ", hazard=" + (agent.EnvironmentNearHazard ? "true" : "false")
                + ", useful=" + (agent.EnvironmentNearUsefulObject ? "true" : "false");
        }

        private static string FormatTransition(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (agent.TransitionFromStateId <= 0 && agent.TransitionToStateId <= 0)
            {
                return "none";
            }

            return EmptyAsNone(agent.TransitionFromStateId) + "->" + EmptyAsNone(agent.TransitionToStateId);
        }

        private static string EmptyAsNone(int value)
        {
            return value <= 0 ? "none" : value.ToString(CultureInfo.InvariantCulture);
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }
    }
}
