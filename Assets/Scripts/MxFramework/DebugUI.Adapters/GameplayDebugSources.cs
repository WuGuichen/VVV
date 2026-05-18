using System;
using System.Text;
using MxFramework.Diagnostics;
using MxFramework.Gameplay;

namespace MxFramework.DebugUI.Adapters
{
    public sealed class GameplayDiagnosticSnapshotDebugSource : IFrameworkDebugSource
    {
        private readonly Func<GameplayDiagnosticSnapshot> _snapshotFactory;

        public GameplayDiagnosticSnapshotDebugSource(Func<GameplayDiagnosticSnapshot> snapshotFactory, string name = "Gameplay")
        {
            _snapshotFactory = snapshotFactory;
            Name = string.IsNullOrWhiteSpace(name) ? "Gameplay" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _snapshotFactory != null;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            GameplayDiagnosticSnapshot snapshot = _snapshotFactory != null ? _snapshotFactory() : null;
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
                    new FrameworkDebugSection("Summary", CreateGameplaySummary(snapshot)),
                    new FrameworkDebugSection("Entities", CreateEntities(snapshot)),
                    new FrameworkDebugSection("Ability Events", CreateAbilityEvents(snapshot)),
                    new FrameworkDebugSection("Attribute Events", CreateAttributeEvents(snapshot))
                });
        }

        private static string CreateGameplaySummary(GameplayDiagnosticSnapshot snapshot)
        {
            var diagnostics = new GameplayWorldDiagnostics();
            GameplayWorldDiagnosticsSummary summary = diagnostics.BuildSummary(snapshot);
            return "source: " + summary.SourceName
                + "\nabilitySource: " + snapshot.AbilitySource
                + "\nentities: " + summary.EntityCount
                + "\nalive: " + summary.AliveEntityCount
                + "\nattributes: " + summary.AttributeCount
                + "\nbuffs: " + summary.BuffCount
                + "\nmodifiers: " + summary.ModifierCount
                + "\nlastCastSuccess: " + (snapshot.LastCastSuccess ? "true" : "false")
                + "\nlastFailureReason: " + (snapshot.LastFailureReason ?? string.Empty);
        }

        private static string CreateEntities(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot.Entities.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.Entities.Count; i++)
            {
                GameplayEntitySnapshot entity = snapshot.Entities[i];
                builder.Append("entity=")
                    .Append(entity.EntityId)
                    .Append(" team=")
                    .Append(entity.TeamId)
                    .Append(" alive=")
                    .Append(entity.IsAlive ? "true" : "false")
                    .Append(" attributes=")
                    .Append(entity.Attributes.Count)
                    .Append(" buffs=")
                    .Append(entity.Buffs.Count)
                    .Append(" modifiers=")
                    .Append(entity.Modifiers.Count);
                if (i + 1 < snapshot.Entities.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private static string CreateAbilityEvents(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot.AbilityEvents.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.AbilityEvents.Count; i++)
            {
                GameplayAbilityEventSnapshot evt = snapshot.AbilityEvents[i];
                builder.Append(evt.EventType)
                    .Append(" ability=")
                    .Append(evt.AbilityId)
                    .Append(" caster=")
                    .Append(evt.CasterEntityId.HasValue ? evt.CasterEntityId.Value.ToString() : "-")
                    .Append(" target=")
                    .Append(evt.TargetEntityId.HasValue ? evt.TargetEntityId.Value.ToString() : "-")
                    .Append(" failure=")
                    .Append(evt.FailureReason ?? string.Empty);
                if (i + 1 < snapshot.AbilityEvents.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private static string CreateAttributeEvents(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot.AttributeEvents.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.AttributeEvents.Count; i++)
            {
                GameplayAttributeEventSnapshot evt = snapshot.AttributeEvents[i];
                builder.Append("attr=")
                    .Append(evt.AttributeId)
                    .Append(" base=")
                    .Append(evt.BaseValue)
                    .Append(" old=")
                    .Append(evt.OldValue)
                    .Append(" new=")
                    .Append(evt.NewValue)
                    .Append(" delta=")
                    .Append(evt.Delta)
                    .Append(" source=")
                    .Append(evt.SourceName ?? string.Empty);
                if (i + 1 < snapshot.AttributeEvents.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }
    }

    public sealed class GameplayComponentWorldDebugSource : IFrameworkDebugSource
    {
        private readonly GameplayComponentWorld _world;
        private readonly GameplayComponentWorldDiagnostics _diagnostics = new GameplayComponentWorldDiagnostics();

        public GameplayComponentWorldDebugSource(GameplayComponentWorld world, string name = "GameplayComponentWorld")
        {
            _world = world;
            Name = string.IsNullOrWhiteSpace(name) ? "GameplayComponentWorld" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _world != null;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            if (_world == null)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("Status", "unavailable") });
            }

            GameplayComponentWorldDiagnosticSnapshot snapshot = _diagnostics.BuildSnapshot(_world);
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Summary", CreateComponentSummary(snapshot)),
                    new FrameworkDebugSection("Stores", CreateStores(snapshot)),
                    new FrameworkDebugSection("Events", CreateEventQueue(snapshot))
                });
        }

        private static string CreateComponentSummary(GameplayComponentWorldDiagnosticSnapshot snapshot)
        {
            return "aliveEntities: " + snapshot.AliveEntityCount
                + "\nstores: " + snapshot.ComponentStoreCount
                + "\npendingEvents: " + snapshot.PendingEventCount;
        }

        private static string CreateStores(GameplayComponentWorldDiagnosticSnapshot snapshot)
        {
            if (snapshot.Stores.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.Stores.Count; i++)
            {
                GameplayComponentStoreDiagnosticSnapshot store = snapshot.Stores[i];
                builder.Append(store.ComponentTypeName)
                    .Append(" count=")
                    .Append(store.ComponentCount);
                if (i + 1 < snapshot.Stores.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private static string CreateEventQueue(GameplayComponentWorldDiagnosticSnapshot snapshot)
        {
            return "pending: " + snapshot.EventQueue.PendingCount
                + "\noldestFrame: " + snapshot.EventQueue.OldestFrame.Value
                + "\nnewestFrame: " + snapshot.EventQueue.NewestFrame.Value
                + "\nnextSequence: " + snapshot.EventQueue.NextSequence
                + "\neventType: " + snapshot.EventQueue.EventTypeName;
        }
    }
}
