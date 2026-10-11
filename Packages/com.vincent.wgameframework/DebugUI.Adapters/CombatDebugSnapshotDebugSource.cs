using System;
using System.Text;
using MxFramework.Combat.Diagnostics;
using MxFramework.Diagnostics;

namespace MxFramework.DebugUI.Adapters
{
    public sealed class CombatDebugSnapshotDebugSource : IFrameworkDebugSource
    {
        private readonly Func<CombatDebugSnapshot> _snapshotFactory;

        public CombatDebugSnapshotDebugSource(Func<CombatDebugSnapshot> snapshotFactory, string name = "Combat")
        {
            _snapshotFactory = snapshotFactory;
            Name = string.IsNullOrWhiteSpace(name) ? "Combat" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _snapshotFactory != null;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            CombatDebugSnapshot snapshot = _snapshotFactory != null ? _snapshotFactory() : null;
            if (snapshot == null)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("Status", "snapshot unavailable") });
            }

            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Summary", snapshot.Summary),
                    new FrameworkDebugSection("Inputs", CreateInputs(snapshot)),
                    new FrameworkDebugSection("Queries", CreateQueries(snapshot)),
                    new FrameworkDebugSection("Hits", CreateHits(snapshot))
                });
        }

        private static string CreateInputs(CombatDebugSnapshot snapshot)
        {
            if (snapshot.Inputs.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.Inputs.Count; i++)
            {
                CombatReplayInput input = snapshot.Inputs[i];
                builder.Append("frame=")
                    .Append(input.Frame.Value)
                    .Append(" entity=")
                    .Append(input.EntityId.Value)
                    .Append(" command=")
                    .Append(input.CommandId)
                    .Append(" value=")
                    .Append(input.Value)
                    .Append(" sourceOrder=")
                    .Append(input.SourceOrder);
                if (i + 1 < snapshot.Inputs.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private static string CreateQueries(CombatDebugSnapshot snapshot)
        {
            if (snapshot.Queries.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.Queries.Count; i++)
            {
                CombatQueryTrace query = snapshot.Queries[i];
                builder.Append("frame=")
                    .Append(query.Frame.Value)
                    .Append(" query=")
                    .Append(query.Query.QueryId)
                    .Append(" kind=")
                    .Append(query.Query.Kind)
                    .Append(" source=")
                    .Append(query.Query.SourceEntityId.Value)
                    .Append(" trace=")
                    .Append(query.Query.TraceId)
                    .Append(" action=")
                    .Append(query.Query.ActionId);
                if (i + 1 < snapshot.Queries.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private static string CreateHits(CombatDebugSnapshot snapshot)
        {
            if (snapshot.Hits.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.Hits.Count; i++)
            {
                CombatHitExplain hit = snapshot.Hits[i];
                builder.Append("frame=")
                    .Append(hit.Result.Frame.Value)
                    .Append(" attacker=")
                    .Append(hit.Result.AttackerId.Value)
                    .Append(" target=")
                    .Append(hit.Result.TargetId.Value)
                    .Append(" action=")
                    .Append(hit.Result.ActionId)
                    .Append(" trace=")
                    .Append(hit.Result.TraceId)
                    .Append(" kind=")
                    .Append(hit.Result.Kind)
                    .Append(" damage=")
                    .Append(hit.Result.Damage)
                    .Append(" reason=")
                    .Append(hit.Reason);
                if (i + 1 < snapshot.Hits.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }
    }
}
