using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Combat.Diagnostics;
using MxFramework.Diagnostics;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.DebugUI.Adapters
{
    public sealed class GameplayRuntimeEventTimelineDebugSource : IFrameworkDebugSource
    {
        private readonly Func<IReadOnlyList<GameplayRuntimeEvent>> _eventsFactory;
        private readonly DebugUiTimelineFilter _filter;
        private readonly int _maxEntries;

        public GameplayRuntimeEventTimelineDebugSource(
            Func<IReadOnlyList<GameplayRuntimeEvent>> eventsFactory,
            string name = "GameplayTimeline",
            DebugUiTimelineFilter filter = default,
            int maxEntries = 128)
        {
            _eventsFactory = eventsFactory;
            Name = string.IsNullOrWhiteSpace(name) ? "GameplayTimeline" : name;
            _filter = filter;
            _maxEntries = maxEntries;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _eventsFactory != null;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            IReadOnlyList<GameplayRuntimeEvent> events = _eventsFactory != null ? _eventsFactory() : null;
            if (events == null)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("Status", "events unavailable") });
            }

            DebugUiTimelineViewModel timeline = DebugUiTimelineViewModel.From(
                MapGameplayEvents(events),
                _filter,
                _maxEntries);
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Summary", "source: Gameplay\nrecentEvents: " + timeline.Count),
                    new FrameworkDebugSection("Timeline", DebugUiObservabilityFormatter.FormatTimeline(timeline))
                });
        }

        private static IReadOnlyList<DebugUiTimelineEntryViewModel> MapGameplayEvents(IReadOnlyList<GameplayRuntimeEvent> events)
        {
            var entries = new List<DebugUiTimelineEntryViewModel>(events.Count);
            for (int i = 0; i < events.Count; i++)
            {
                GameplayRuntimeEvent evt = events[i];
                string entityId = ResolveGameplayEntity(evt);
                entries.Add(new DebugUiTimelineEntryViewModel(
                    evt.Frame.Value,
                    "Gameplay",
                    evt.Type.ToString(),
                    entityId,
                    evt.TraceId,
                    CreateGameplaySummary(evt)));
            }

            return entries;
        }

        private static string ResolveGameplayEntity(GameplayRuntimeEvent evt)
        {
            if (evt.TryGetComponentEntityId(out GameplayEntityId componentEntityId))
                return componentEntityId.ToString();
            if (evt.TargetEntityId != 0)
                return evt.TargetEntityId.ToString();
            if (evt.CasterEntityId != 0)
                return evt.CasterEntityId.ToString();

            return string.Empty;
        }

        private static string CreateGameplaySummary(GameplayRuntimeEvent evt)
        {
            string summary = "command=" + evt.CommandId
                + " ability=" + evt.AbilityId
                + " target=" + evt.TargetEntityId;
            if (evt.AttributeId != 0)
            {
                summary += " attr=" + evt.AttributeId
                    + " old=" + evt.OldAttributeValue
                    + " new=" + evt.NewAttributeValue
                    + " delta=" + evt.AttributeDelta;
            }

            if (!string.IsNullOrEmpty(evt.Reason))
                summary += " reason=" + evt.Reason;
            if (evt.FailureCode != GameplayAbilityRuntimeFailureCode.None)
                summary += " failure=" + evt.FailureCode;

            return summary;
        }
    }

    public sealed class CombatTimelineDebugSource : IFrameworkDebugSource
    {
        private readonly Func<CombatDebugSnapshot> _snapshotFactory;
        private readonly DebugUiTimelineFilter _filter;
        private readonly int _maxEntries;

        public CombatTimelineDebugSource(
            Func<CombatDebugSnapshot> snapshotFactory,
            string name = "CombatTimeline",
            DebugUiTimelineFilter filter = default,
            int maxEntries = 128)
        {
            _snapshotFactory = snapshotFactory;
            Name = string.IsNullOrWhiteSpace(name) ? "CombatTimeline" : name;
            _filter = filter;
            _maxEntries = maxEntries;
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

            DebugUiTimelineViewModel timeline = DebugUiTimelineViewModel.From(
                MapCombatSnapshot(snapshot),
                _filter,
                _maxEntries);
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Summary", "source: Combat\nrecentEvents: " + timeline.Count),
                    new FrameworkDebugSection("Timeline", DebugUiObservabilityFormatter.FormatTimeline(timeline))
                });
        }

        private static IReadOnlyList<DebugUiTimelineEntryViewModel> MapCombatSnapshot(CombatDebugSnapshot snapshot)
        {
            var entries = new List<DebugUiTimelineEntryViewModel>(snapshot.Queries.Count + snapshot.Hits.Count);
            for (int i = 0; i < snapshot.Queries.Count; i++)
            {
                CombatQueryTrace query = snapshot.Queries[i];
                entries.Add(new DebugUiTimelineEntryViewModel(
                    query.Frame.Value,
                    "Combat",
                    "Query",
                    query.Query.SourceEntityId.ToString(),
                    query.Query.TraceId.ToString(),
                    "query=" + query.Query.QueryId + " kind=" + query.Query.Kind + " action=" + query.Query.ActionId));
            }

            for (int i = 0; i < snapshot.Hits.Count; i++)
            {
                CombatHitExplain hit = snapshot.Hits[i];
                entries.Add(new DebugUiTimelineEntryViewModel(
                    hit.Result.Frame.Value,
                    "Combat",
                    "Hit",
                    hit.Result.TargetId.ToString(),
                    hit.Result.TraceId.ToString(),
                    "kind=" + hit.Result.Kind
                    + " attacker=" + hit.Result.AttackerId
                    + " action=" + hit.Result.ActionId
                    + " damage=" + hit.Result.Damage
                    + " reason=" + hit.Reason));
            }

            return entries;
        }
    }

    public sealed class GameplayComponentWorldEntityWatchDebugSource : IFrameworkDebugSource
    {
        private readonly GameplayComponentWorld _world;
        private readonly string _entityFilter;

        public GameplayComponentWorldEntityWatchDebugSource(
            GameplayComponentWorld world,
            string name = "GameplayEntityWatch",
            string entityFilter = null)
        {
            _world = world;
            Name = string.IsNullOrWhiteSpace(name) ? "GameplayEntityWatch" : name;
            _entityFilter = entityFilter ?? string.Empty;
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

            DebugUiEntityWatchViewModel watch = DebugUiEntityWatchViewModel.From(MapWorld(_world), _entityFilter);
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Summary", "source: GameplayComponentWorld\nentities: " + watch.Count),
                    new FrameworkDebugSection("Entity Watch", DebugUiObservabilityFormatter.FormatEntityWatch(watch))
                });
        }

        private static IReadOnlyList<DebugUiEntityWatchEntryViewModel> MapWorld(GameplayComponentWorld world)
        {
            GameplayEntityId[] entities = world.CreateEntitySnapshot();
            var entries = new List<DebugUiEntityWatchEntryViewModel>(entities.Length);
            world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> attributeStore);
            world.TryGetStore(out GameplayComponentStore<GameplayPosturePressureComponent> postureStore);
            world.TryGetStore(out GameplayComponentStore<GameplayGuardPressureComponent> guardStore);
            world.TryGetStore(out GameplayComponentStore<GameplayArmorIntegrityComponent> armorStore);

            for (int i = 0; i < entities.Length; i++)
            {
                GameplayEntityId entityId = entities[i];
                string pressure = ReadPosture(postureStore, entityId);
                string guard = ReadGuard(guardStore, entityId);
                string armor = ReadArmor(armorStore, entityId);
                entries.Add(new DebugUiEntityWatchEntryViewModel(
                    entityId.ToString(),
                    string.Empty,
                    world.IsAlive(entityId) ? "alive" : "inactive",
                    ReadAttributes(attributeStore, entityId),
                    pressure,
                    guard,
                    armor,
                    string.Empty));
            }

            return entries;
        }

        private static string ReadAttributes(
            GameplayComponentStore<GameplayAttributeSetComponent> store,
            GameplayEntityId entityId)
        {
            if (store == null || !store.TryGet(entityId, out GameplayAttributeSetComponent component))
                return string.Empty;

            GameplayAttributeValue[] values = component.ToArray();
            if (values.Length == 0)
                return string.Empty;

            var builder = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                GameplayAttributeValue value = values[i];
                if (i > 0)
                    builder.Append(' ');
                builder.Append(value.AttributeId)
                    .Append('=')
                    .Append(value.CurrentValue)
                    .Append('/')
                    .Append(value.BaseValue);
            }

            return builder.ToString();
        }

        private static string ReadPosture(GameplayComponentStore<GameplayPosturePressureComponent> store, GameplayEntityId entityId)
        {
            if (store == null || !store.TryGet(entityId, out GameplayPosturePressureComponent component))
                return string.Empty;

            return component.CurrentBand + " " + component.CurrentPressure + "/" + component.MaxPressure;
        }

        private static string ReadGuard(GameplayComponentStore<GameplayGuardPressureComponent> store, GameplayEntityId entityId)
        {
            if (store == null || !store.TryGet(entityId, out GameplayGuardPressureComponent component))
                return string.Empty;

            return component.CurrentBand
                + " "
                + component.CurrentPressure
                + "/"
                + component.MaxPressure
                + (component.IsBroken ? " broken" : string.Empty);
        }

        private static string ReadArmor(GameplayComponentStore<GameplayArmorIntegrityComponent> store, GameplayEntityId entityId)
        {
            if (store == null || !store.TryGet(entityId, out GameplayArmorIntegrityComponent component))
                return string.Empty;

            return component.CurrentIntegrity
                + "/"
                + component.MaxIntegrity
                + (component.IsBroken ? " broken" : string.Empty);
        }
    }

    public sealed class RuntimeHostPerformanceCounterSource
    {
        private readonly RuntimeHost _host;

        public RuntimeHostPerformanceCounterSource(RuntimeHost host)
        {
            _host = host;
        }

        public FrameworkPerformanceCounterSnapshot Capture()
        {
            if (_host == null)
                return new FrameworkPerformanceCounterSnapshot("RuntimeHost", false, Array.Empty<FrameworkPerformanceCounterSample>());

            RuntimeHostDiagnostics diagnostics = _host.CaptureDiagnostics();
            return new FrameworkPerformanceCounterSnapshot(
                "RuntimeHost",
                true,
                new[]
                {
                    new FrameworkPerformanceCounterSample("runtime.tickCount", "Tick Count", "RuntimeHost", diagnostics.TickCount, "ticks", FrameworkPerformanceCounterCost.NoAlloc),
                    new FrameworkPerformanceCounterSample("runtime.moduleCount", "Module Count", "RuntimeHost", diagnostics.Modules.Count, "modules", FrameworkPerformanceCounterCost.NoAlloc),
                    new FrameworkPerformanceCounterSample("runtime.errorCount", "Error Count", "RuntimeHost", diagnostics.Errors.Count, "errors", FrameworkPerformanceCounterCost.NoAlloc)
                });
        }
    }

    public sealed class GameplayDiagnosticPerformanceCounterSource
    {
        private readonly Func<GameplayDiagnosticSnapshot> _snapshotFactory;

        public GameplayDiagnosticPerformanceCounterSource(Func<GameplayDiagnosticSnapshot> snapshotFactory)
        {
            _snapshotFactory = snapshotFactory;
        }

        public FrameworkPerformanceCounterSnapshot Capture()
        {
            GameplayDiagnosticSnapshot snapshot = _snapshotFactory != null ? _snapshotFactory() : null;
            if (snapshot == null)
                return new FrameworkPerformanceCounterSnapshot("Gameplay", false, Array.Empty<FrameworkPerformanceCounterSample>());

            return new FrameworkPerformanceCounterSnapshot(
                "Gameplay",
                true,
                new[]
                {
                    new FrameworkPerformanceCounterSample("gameplay.entityCount", "Entity Count", "Gameplay", snapshot.Entities.Count, "entities", FrameworkPerformanceCounterCost.NoAlloc),
                    new FrameworkPerformanceCounterSample("gameplay.abilityEventCount", "Ability Event Count", "Gameplay", snapshot.AbilityEvents.Count, "events", FrameworkPerformanceCounterCost.NoAlloc),
                    new FrameworkPerformanceCounterSample("gameplay.attributeEventCount", "Attribute Event Count", "Gameplay", snapshot.AttributeEvents.Count, "events", FrameworkPerformanceCounterCost.NoAlloc)
                });
        }
    }

    public sealed class CombatDebugPerformanceCounterSource
    {
        private readonly Func<CombatDebugSnapshot> _snapshotFactory;

        public CombatDebugPerformanceCounterSource(Func<CombatDebugSnapshot> snapshotFactory)
        {
            _snapshotFactory = snapshotFactory;
        }

        public FrameworkPerformanceCounterSnapshot Capture()
        {
            CombatDebugSnapshot snapshot = _snapshotFactory != null ? _snapshotFactory() : null;
            if (snapshot == null)
                return new FrameworkPerformanceCounterSnapshot("Combat", false, Array.Empty<FrameworkPerformanceCounterSample>());

            return new FrameworkPerformanceCounterSnapshot(
                "Combat",
                true,
                new[]
                {
                    new FrameworkPerformanceCounterSample("combat.queryCount", "Query Count", "Combat", snapshot.Queries.Count, "queries", FrameworkPerformanceCounterCost.NoAlloc),
                    new FrameworkPerformanceCounterSample("combat.hitResolveCount", "Hit Resolve Count", "Combat", snapshot.Hits.Count, "hits", FrameworkPerformanceCounterCost.NoAlloc),
                    new FrameworkPerformanceCounterSample("combat.inputCount", "Input Count", "Combat", snapshot.Inputs.Count, "inputs", FrameworkPerformanceCounterCost.NoAlloc)
                });
        }
    }
}
