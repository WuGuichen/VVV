using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentSchemaRegistry
    {
        private readonly Dictionary<string, SchemaEntry> _entriesByStableId;
        private readonly Dictionary<Type, SchemaEntry> _entriesByType;
        private readonly List<SchemaEntry> _entries;

        public GameplayComponentSchemaRegistry()
        {
            _entriesByStableId = new Dictionary<string, SchemaEntry>(StringComparer.Ordinal);
            _entriesByType = new Dictionary<Type, SchemaEntry>();
            _entries = new List<SchemaEntry>();
        }

        public int Count => _entries.Count;

        public void Register(IGameplayComponentSchemaDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            GameplayComponentSchema schema = descriptor.Schema;
            ValidateDescriptorAgainstSchema(descriptor, schema);
            SchemaEntry entry = GetOrCreateEntry(schema);
            entry.Attach(descriptor);
        }

        public bool TryGetByStableId(string stableId, out GameplayComponentSchema schema)
        {
            if (!string.IsNullOrEmpty(stableId) && _entriesByStableId.TryGetValue(stableId, out SchemaEntry entry))
            {
                schema = entry.Schema;
                return true;
            }

            schema = default;
            return false;
        }

        public bool TryGetByType(Type componentType, out GameplayComponentSchema schema)
        {
            if (componentType != null && _entriesByType.TryGetValue(componentType, out SchemaEntry entry))
            {
                schema = entry.Schema;
                return true;
            }

            schema = default;
            return false;
        }

        public bool TryGetDiagnosticWriter<T>(out IGameplayComponentDiagnosticWriter<T> writer)
            where T : struct, IGameplayComponent
        {
            if (_entriesByType.TryGetValue(typeof(T), out SchemaEntry entry))
                return entry.TryGetDiagnosticWriter(out writer);

            writer = null;
            return false;
        }

        public bool TryGetHashWriter<T>(out IGameplayComponentHashWriter<T> writer)
            where T : struct, IGameplayComponent
        {
            if (_entriesByType.TryGetValue(typeof(T), out SchemaEntry entry))
                return entry.TryGetHashWriter(out writer);

            writer = null;
            return false;
        }

        public bool TryGetSaveStateAdapter<T>(out IGameplayComponentSaveStateAdapter<T> adapter)
            where T : struct, IGameplayComponent
        {
            if (_entriesByType.TryGetValue(typeof(T), out SchemaEntry entry))
                return entry.TryGetSaveStateAdapter(out adapter);

            adapter = null;
            return false;
        }

        public GameplayComponentSchema[] CreateSnapshot()
        {
            if (_entries.Count == 0)
                return Array.Empty<GameplayComponentSchema>();

            var snapshot = new GameplayComponentSchema[_entries.Count];
            for (int i = 0; i < _entries.Count; i++)
                snapshot[i] = _entries[i].Schema;

            Array.Sort(snapshot, CompareSchemas);
            return snapshot;
        }

        internal GameplayComponentHashAdapter[] CreateHashAdapters()
        {
            var adapters = new List<GameplayComponentHashAdapter>();
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].TryCreateHashAdapter(out GameplayComponentHashAdapter adapter))
                    adapters.Add(adapter);
            }

            if (adapters.Count == 0)
                return Array.Empty<GameplayComponentHashAdapter>();

            var snapshot = adapters.ToArray();
            Array.Sort(snapshot, CompareHashAdapters);
            return snapshot;
        }

        internal GameplayComponentSaveStateAdapter[] CreateSaveStateAdapters()
        {
            var adapters = new List<GameplayComponentSaveStateAdapter>();
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].TryCreateSaveStateAdapter(out GameplayComponentSaveStateAdapter adapter))
                    adapters.Add(adapter);
            }

            if (adapters.Count == 0)
                return Array.Empty<GameplayComponentSaveStateAdapter>();

            var snapshot = adapters.ToArray();
            Array.Sort(snapshot, CompareSaveStateAdapters);
            return snapshot;
        }

        internal bool TryGetSaveStateAdapterByStableId(
            string stableId,
            out GameplayComponentSaveStateAdapter adapter)
        {
            if (!string.IsNullOrEmpty(stableId)
                && _entriesByStableId.TryGetValue(stableId, out SchemaEntry entry))
                return entry.TryCreateSaveStateAdapter(out adapter);

            adapter = default;
            return false;
        }

        public void Clear()
        {
            _entriesByStableId.Clear();
            _entriesByType.Clear();
            _entries.Clear();
        }

        private SchemaEntry GetOrCreateEntry(GameplayComponentSchema schema)
        {
            bool hasStableId = _entriesByStableId.TryGetValue(schema.StableId, out SchemaEntry stableEntry);
            bool hasType = _entriesByType.TryGetValue(schema.ComponentType, out SchemaEntry typeEntry);

            if (hasStableId && hasType)
            {
                if (!ReferenceEquals(stableEntry, typeEntry))
                    throw new InvalidOperationException("Gameplay component schema stable id and component type belong to different entries.");
                if (!stableEntry.Schema.Equals(schema))
                    throw new InvalidOperationException("Gameplay component schema entry cannot be registered with conflicting metadata.");

                return stableEntry;
            }

            if (hasStableId)
                throw new InvalidOperationException("Gameplay component schema stable id is already registered: " + schema.StableId);
            if (hasType)
                throw new InvalidOperationException("Gameplay component schema component type is already registered: " + schema.ComponentType.FullName);

            var entry = new SchemaEntry(schema);
            _entriesByStableId.Add(schema.StableId, entry);
            _entriesByType.Add(schema.ComponentType, entry);
            _entries.Add(entry);
            return entry;
        }

        private static int CompareSchemas(GameplayComponentSchema left, GameplayComponentSchema right)
        {
            return string.CompareOrdinal(left.StableId, right.StableId);
        }

        private static int CompareHashAdapters(GameplayComponentHashAdapter left, GameplayComponentHashAdapter right)
        {
            return string.CompareOrdinal(left.Schema.StableId, right.Schema.StableId);
        }

        private static int CompareSaveStateAdapters(GameplayComponentSaveStateAdapter left, GameplayComponentSaveStateAdapter right)
        {
            return string.CompareOrdinal(left.Schema.StableId, right.Schema.StableId);
        }

        private static void ValidateDescriptorAgainstSchema(
            IGameplayComponentSchemaDescriptor descriptor,
            GameplayComponentSchema schema)
        {
            Type[] interfaces = descriptor.GetType().GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                Type interfaceType = interfaces[i];
                if (!interfaceType.IsGenericType)
                    continue;

                Type definition = interfaceType.GetGenericTypeDefinition();
                bool isDiagnostic = definition == typeof(IGameplayComponentDiagnosticWriter<>);
                bool isHash = definition == typeof(IGameplayComponentHashWriter<>);
                bool isSaveState = definition == typeof(IGameplayComponentSaveStateAdapter<>);
                if (!isDiagnostic && !isHash && !isSaveState)
                    continue;

                Type capabilityComponentType = interfaceType.GetGenericArguments()[0];
                if (capabilityComponentType != schema.ComponentType)
                    throw new InvalidOperationException("Gameplay component schema descriptor capability type does not match schema component type.");
                if (isDiagnostic && !schema.SupportsDiagnostics)
                    throw new InvalidOperationException("Gameplay component schema does not declare diagnostics support.");
                if (isHash && !schema.SupportsHash)
                    throw new InvalidOperationException("Gameplay component schema does not declare hash support.");
                if (isSaveState && !schema.SupportsSaveState)
                    throw new InvalidOperationException("Gameplay component schema does not declare SaveState support.");
            }
        }

        private sealed class SchemaEntry
        {
            private object _diagnosticWriter;
            private object _hashWriter;
            private IGameplayComponentHashRuntimeWriter _hashRuntimeWriter;
            private object _saveStateAdapter;
            private IGameplayComponentSaveStateRuntimeAdapter _saveStateRuntimeAdapter;
            private bool _schemaOnlyRegistered;

            public SchemaEntry(GameplayComponentSchema schema)
            {
                Schema = schema;
            }

            public GameplayComponentSchema Schema { get; }

            public void Attach(IGameplayComponentSchemaDescriptor descriptor)
            {
                bool attached = false;
                Type componentType = Schema.ComponentType;

                Type diagnosticType = typeof(IGameplayComponentDiagnosticWriter<>).MakeGenericType(componentType);
                if (diagnosticType.IsInstanceOfType(descriptor))
                {
                    if (!Schema.SupportsDiagnostics)
                        throw new InvalidOperationException("Gameplay component schema does not declare diagnostics support.");

                    AttachCapability(ref _diagnosticWriter, descriptor, "diagnostic writer");
                    attached = true;
                }

                Type hashType = typeof(IGameplayComponentHashWriter<>).MakeGenericType(componentType);
                if (hashType.IsInstanceOfType(descriptor))
                {
                    if (!Schema.SupportsHash)
                        throw new InvalidOperationException("Gameplay component schema does not declare hash support.");

                    AttachCapability(ref _hashWriter, descriptor, "hash writer");
                    _hashRuntimeWriter = CreateHashRuntimeWriter(descriptor, componentType);
                    attached = true;
                }

                Type saveType = typeof(IGameplayComponentSaveStateAdapter<>).MakeGenericType(componentType);
                if (saveType.IsInstanceOfType(descriptor))
                {
                    if (!Schema.SupportsSaveState)
                        throw new InvalidOperationException("Gameplay component schema does not declare SaveState support.");

                    AttachCapability(ref _saveStateAdapter, descriptor, "save state adapter");
                    _saveStateRuntimeAdapter = CreateSaveStateRuntimeAdapter(descriptor, componentType);
                    attached = true;
                }

                if (!attached && ImplementsGameplayCapability(descriptor.GetType()))
                    throw new InvalidOperationException("Gameplay component schema descriptor capability type does not match schema component type.");

                if (!attached)
                {
                    if (_schemaOnlyRegistered)
                        throw new InvalidOperationException("Gameplay component schema already has a schema-only descriptor.");

                    _schemaOnlyRegistered = true;
                    return;
                }
            }

            public bool TryGetDiagnosticWriter<T>(out IGameplayComponentDiagnosticWriter<T> writer)
                where T : struct, IGameplayComponent
            {
                writer = _diagnosticWriter as IGameplayComponentDiagnosticWriter<T>;
                return writer != null;
            }

            public bool TryGetHashWriter<T>(out IGameplayComponentHashWriter<T> writer)
                where T : struct, IGameplayComponent
            {
                writer = _hashWriter as IGameplayComponentHashWriter<T>;
                return writer != null;
            }

            public bool TryCreateHashAdapter(out GameplayComponentHashAdapter adapter)
            {
                if (_hashRuntimeWriter == null)
                {
                    adapter = default;
                    return false;
                }

                adapter = new GameplayComponentHashAdapter(Schema, _hashRuntimeWriter);
                return true;
            }

            public bool TryGetSaveStateAdapter<T>(out IGameplayComponentSaveStateAdapter<T> adapter)
                where T : struct, IGameplayComponent
            {
                adapter = _saveStateAdapter as IGameplayComponentSaveStateAdapter<T>;
                return adapter != null;
            }

            public bool TryCreateSaveStateAdapter(out GameplayComponentSaveStateAdapter adapter)
            {
                if (_saveStateRuntimeAdapter == null)
                {
                    adapter = default;
                    return false;
                }

                adapter = new GameplayComponentSaveStateAdapter(Schema, _saveStateRuntimeAdapter);
                return true;
            }

            private static void AttachCapability(ref object target, object capability, string capabilityName)
            {
                if (target != null && !ReferenceEquals(target, capability))
                    throw new InvalidOperationException("Gameplay component schema already has a " + capabilityName + ".");

                target = capability;
            }

            private static IGameplayComponentHashRuntimeWriter CreateHashRuntimeWriter(
                object descriptor,
                Type componentType)
            {
                Type runtimeWriterType = typeof(GameplayComponentHashRuntimeWriter<>).MakeGenericType(componentType);
                return (IGameplayComponentHashRuntimeWriter)Activator.CreateInstance(runtimeWriterType, descriptor);
            }

            private static IGameplayComponentSaveStateRuntimeAdapter CreateSaveStateRuntimeAdapter(
                object descriptor,
                Type componentType)
            {
                Type runtimeAdapterType = typeof(GameplayComponentSaveStateRuntimeAdapter<>).MakeGenericType(componentType);
                return (IGameplayComponentSaveStateRuntimeAdapter)Activator.CreateInstance(runtimeAdapterType, descriptor);
            }

            private static bool ImplementsGameplayCapability(Type descriptorType)
            {
                Type[] interfaces = descriptorType.GetInterfaces();
                for (int i = 0; i < interfaces.Length; i++)
                {
                    Type interfaceType = interfaces[i];
                    if (!interfaceType.IsGenericType)
                        continue;

                    Type definition = interfaceType.GetGenericTypeDefinition();
                    if (definition == typeof(IGameplayComponentDiagnosticWriter<>)
                        || definition == typeof(IGameplayComponentHashWriter<>)
                        || definition == typeof(IGameplayComponentSaveStateAdapter<>))
                        return true;
                }

                return false;
            }
        }
    }

    internal readonly struct GameplayComponentHashAdapter
    {
        private readonly IGameplayComponentHashRuntimeWriter _writer;

        public GameplayComponentHashAdapter(
            GameplayComponentSchema schema,
            IGameplayComponentHashRuntimeWriter writer)
        {
            Schema = schema;
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public GameplayComponentSchema Schema { get; }

        public bool TryWriteHash(
            GameplayComponentRegistry registry,
            GameplayEntityId entityId,
            RuntimeHashAccumulator accumulator)
        {
            return _writer.TryWriteHash(registry, entityId, accumulator);
        }

        public bool Contains(GameplayComponentRegistry registry, GameplayEntityId entityId)
        {
            return _writer.Contains(registry, entityId);
        }
    }

    internal interface IGameplayComponentHashRuntimeWriter
    {
        bool Contains(GameplayComponentRegistry registry, GameplayEntityId entityId);

        bool TryWriteHash(
            GameplayComponentRegistry registry,
            GameplayEntityId entityId,
            RuntimeHashAccumulator accumulator);
    }

    internal sealed class GameplayComponentHashRuntimeWriter<T> : IGameplayComponentHashRuntimeWriter
        where T : struct, IGameplayComponent
    {
        private readonly IGameplayComponentHashWriter<T> _writer;

        public GameplayComponentHashRuntimeWriter(object writer)
        {
            _writer = writer as IGameplayComponentHashWriter<T>
                ?? throw new ArgumentException("Gameplay component hash writer has an incompatible component type.", nameof(writer));
        }

        public bool Contains(GameplayComponentRegistry registry, GameplayEntityId entityId)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            return registry.TryGetStore(out GameplayComponentStore<T> store)
                && store.Contains(entityId);
        }

        public bool TryWriteHash(
            GameplayComponentRegistry registry,
            GameplayEntityId entityId,
            RuntimeHashAccumulator accumulator)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            if (!registry.TryGetStore(out GameplayComponentStore<T> store))
                return false;
            if (!store.TryGet(entityId, out T component))
                return false;

            _writer.WriteHash(entityId, component, accumulator);
            return true;
        }
    }

    internal readonly struct GameplayComponentSaveStateAdapter
    {
        private readonly IGameplayComponentSaveStateRuntimeAdapter _adapter;

        public GameplayComponentSaveStateAdapter(
            GameplayComponentSchema schema,
            IGameplayComponentSaveStateRuntimeAdapter adapter)
        {
            Schema = schema;
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public GameplayComponentSchema Schema { get; }

        public bool HasStore(GameplayComponentRegistry registry)
        {
            return _adapter.HasStore(registry);
        }

        public GameplayComponentEntrySaveState[] CaptureEntries(GameplayComponentRegistry registry)
        {
            return _adapter.CaptureEntries(registry);
        }

        public RuntimeSaveStateResult<RestoreComponentAction> CreateRestoreAction(
            GameplayEntityId entityId,
            RuntimeCustomState payload,
            string path)
        {
            return _adapter.CreateRestoreAction(entityId, payload, path);
        }
    }

    internal delegate void RestoreComponentAction(GameplayComponentRegistry registry);

    internal interface IGameplayComponentSaveStateRuntimeAdapter
    {
        bool HasStore(GameplayComponentRegistry registry);
        GameplayComponentEntrySaveState[] CaptureEntries(GameplayComponentRegistry registry);
        RuntimeSaveStateResult<RestoreComponentAction> CreateRestoreAction(
            GameplayEntityId entityId,
            RuntimeCustomState payload,
            string path);
    }

    internal sealed class GameplayComponentSaveStateRuntimeAdapter<T> : IGameplayComponentSaveStateRuntimeAdapter
        where T : struct, IGameplayComponent
    {
        private readonly IGameplayComponentSaveStateAdapter<T> _adapter;

        public GameplayComponentSaveStateRuntimeAdapter(object adapter)
        {
            _adapter = adapter as IGameplayComponentSaveStateAdapter<T>
                ?? throw new ArgumentException("Gameplay component SaveState adapter has an incompatible component type.", nameof(adapter));
        }

        public bool HasStore(GameplayComponentRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            return registry.TryGetStore(out GameplayComponentStore<T> _);
        }

        public GameplayComponentEntrySaveState[] CaptureEntries(GameplayComponentRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            if (!registry.TryGetStore(out GameplayComponentStore<T> store))
                return Array.Empty<GameplayComponentEntrySaveState>();

            GameplayComponentSnapshot<T>[] snapshot = store.CreateSnapshot();
            if (snapshot.Length == 0)
                return Array.Empty<GameplayComponentEntrySaveState>();

            var entries = new GameplayComponentEntrySaveState[snapshot.Length];
            for (int i = 0; i < snapshot.Length; i++)
            {
                GameplayComponentSnapshot<T> component = snapshot[i];
                RuntimeCustomState payload = _adapter.WriteSaveState(component.EntityId, component.Component);
                entries[i] = new GameplayComponentEntrySaveState(
                    component.EntityId.Index,
                    component.EntityId.Generation,
                    payload);
            }

            return entries;
        }

        public RuntimeSaveStateResult<RestoreComponentAction> CreateRestoreAction(
            GameplayEntityId entityId,
            RuntimeCustomState payload,
            string path)
        {
            RuntimeSaveStateResult<T> read = _adapter.ReadSaveState(entityId, payload);
            if (!read.Success)
                return RuntimeSaveStateResult<RestoreComponentAction>.Failed(read.Error);

            T component = read.Value;
            RestoreComponentAction action = registry => registry.GetOrCreateStore<T>().Set(entityId, component);
            return RuntimeSaveStateResult<RestoreComponentAction>.Succeeded(action);
        }
    }
}
