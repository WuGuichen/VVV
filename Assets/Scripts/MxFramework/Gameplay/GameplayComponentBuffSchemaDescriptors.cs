namespace MxFramework.Gameplay
{
    public static class GameplayComponentBuffSchemaDescriptors
    {
        public const string BuffsStableId = "mxframework.gameplay.buffs";

        public static void RegisterDiagnostics(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new BuffDiagnostics());
        }

        public static void RegisterRuntimeHash(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new BuffHash());
        }

        public static void RegisterSaveState(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new BuffSaveState());
        }

        private static GameplayComponentSchema CreateSchema()
        {
            return new GameplayComponentSchema(
                BuffsStableId,
                1,
                typeof(GameplayComponentBuffSetComponent),
                "Gameplay Component Buffs",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);
        }

        private sealed class BuffDiagnostics : IGameplayComponentDiagnosticWriter<GameplayComponentBuffSetComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in GameplayComponentBuffSetComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                writer.AddInt("entity.index", entityId.Index);
                writer.AddInt("entity.generation", entityId.Generation);
                GameplayComponentBuffEntry[] entries = component.ToArray();
                writer.AddInt("count", entries.Length);
                for (int i = 0; i < entries.Length; i++)
                {
                    writer.AddInt("buff." + i + ".id", entries[i].BuffId);
                    writer.AddInt("buff." + i + ".stacks", entries[i].StackCount);
                    writer.AddInt("buff." + i + ".maxStacks", entries[i].MaxStackCount);
                    writer.AddLong("buff." + i + ".endFrame", entries[i].EndFrame);
                    writer.AddBool("buff." + i + ".permanent", entries[i].IsPermanent);
                    writer.AddInt("buff." + i + ".sourceId", entries[i].SourceId);
                }
            }
        }

        private sealed class BuffHash : IGameplayComponentHashWriter<GameplayComponentBuffSetComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public void WriteHash(
                GameplayEntityId entityId,
                in GameplayComponentBuffSetComponent component,
                MxFramework.Runtime.RuntimeHashAccumulator accumulator)
            {
                GameplayComponentBuffEntry[] entries = component.ToArray();
                accumulator.AddInt("count", entries.Length);
                for (int i = 0; i < entries.Length; i++)
                {
                    accumulator.AddInt("buff.id", entries[i].BuffId);
                    accumulator.AddInt("buff.stacks", entries[i].StackCount);
                    accumulator.AddInt("buff.maxStacks", entries[i].MaxStackCount);
                    accumulator.AddLong("buff.endFrame", entries[i].EndFrame);
                    accumulator.AddInt("buff.permanent", entries[i].IsPermanent ? 1 : 0);
                    accumulator.AddInt("buff.sourceId", entries[i].SourceId);
                }
            }
        }

        private sealed class BuffSaveState : IGameplayComponentSaveStateAdapter<GameplayComponentBuffSetComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public MxFramework.Runtime.RuntimeCustomState WriteSaveState(
                GameplayEntityId entityId,
                in GameplayComponentBuffSetComponent component)
            {
                GameplayComponentBuffEntry[] entries = component.ToArray();
                var payloadEntries = new BuffEntryPayload[entries.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    payloadEntries[i] = new BuffEntryPayload
                    {
                        BuffId = entries[i].BuffId,
                        StackCount = entries[i].StackCount,
                        MaxStackCount = entries[i].MaxStackCount,
                        EndFrame = entries[i].EndFrame,
                        IsPermanent = entries[i].IsPermanent,
                        SourceId = entries[i].SourceId
                    };
                }

                return WritePayload(Schema, new BuffPayload { Buffs = payloadEntries });
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayComponentBuffSetComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<BuffPayload> result = ReadPayload<BuffPayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayComponentBuffSetComponent>.Failed(result.Error);

                try
                {
                    BuffEntryPayload[] payloadEntries = result.Value != null && result.Value.Buffs != null
                        ? result.Value.Buffs
                        : System.Array.Empty<BuffEntryPayload>();
                    var entries = new GameplayComponentBuffEntry[payloadEntries.Length];
                    for (int i = 0; i < payloadEntries.Length; i++)
                    {
                        entries[i] = new GameplayComponentBuffEntry(
                            payloadEntries[i].BuffId,
                            payloadEntries[i].StackCount,
                            payloadEntries[i].MaxStackCount,
                            payloadEntries[i].EndFrame,
                            payloadEntries[i].IsPermanent,
                            payloadEntries[i].SourceId);
                    }

                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayComponentBuffSetComponent>.Succeeded(
                        new GameplayComponentBuffSetComponent(entries));
                }
                catch (System.Exception exception)
                {
                    return InvalidPayload<GameplayComponentBuffSetComponent>(Schema, payload, exception);
                }
            }
        }

        private static MxFramework.Runtime.RuntimeCustomState WritePayload<TPayload>(
            GameplayComponentSchema schema,
            TPayload payload)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(
                payload,
                new Newtonsoft.Json.JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
                    Formatting = Newtonsoft.Json.Formatting.None,
                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Include
                });
            return new MxFramework.Runtime.RuntimeCustomState(schema.StableId, schema.Version, json);
        }

        private static MxFramework.Runtime.RuntimeSaveStateResult<TPayload> ReadPayload<TPayload>(
            GameplayComponentSchema schema,
            MxFramework.Runtime.RuntimeCustomState payload)
        {
            if (payload == null)
                return FailedPayload<TPayload>(schema, "payload", "Component payload is missing.");
            if (!string.Equals(payload.TypeId, schema.StableId, System.StringComparison.Ordinal))
                return FailedPayload<TPayload>(schema, "typeId", "Component payload type id does not match schema id.");
            if (payload.SchemaVersion != schema.Version)
                return MxFramework.Runtime.RuntimeSaveStateResult<TPayload>.Failed(new MxFramework.Runtime.RuntimeSaveStateError(
                    MxFramework.Runtime.RuntimeSaveStateErrorCode.UnsupportedVersion,
                    "payload.schemaVersion",
                    "Component payload schema version is not supported.",
                    payload.SchemaVersion,
                    schema.Version));

            try
            {
                TPayload value = Newtonsoft.Json.JsonConvert.DeserializeObject<TPayload>(payload.PayloadJson);
                return MxFramework.Runtime.RuntimeSaveStateResult<TPayload>.Succeeded(value);
            }
            catch (System.Exception exception)
            {
                return MxFramework.Runtime.RuntimeSaveStateResult<TPayload>.Failed(new MxFramework.Runtime.RuntimeSaveStateError(
                    MxFramework.Runtime.RuntimeSaveStateErrorCode.InvalidDocument,
                    "payload.payloadJson",
                    "Component payload json could not be parsed: " + exception.Message,
                    payload.SchemaVersion,
                    schema.Version,
                    exception));
            }
        }

        private static MxFramework.Runtime.RuntimeSaveStateResult<TPayload> FailedPayload<TPayload>(
            GameplayComponentSchema schema,
            string path,
            string message)
        {
            return MxFramework.Runtime.RuntimeSaveStateResult<TPayload>.Failed(new MxFramework.Runtime.RuntimeSaveStateError(
                MxFramework.Runtime.RuntimeSaveStateErrorCode.CustomStateMismatch,
                path,
                message,
                -1,
                schema.Version));
        }

        private static MxFramework.Runtime.RuntimeSaveStateResult<TComponent> InvalidPayload<TComponent>(
            GameplayComponentSchema schema,
            MxFramework.Runtime.RuntimeCustomState payload,
            System.Exception exception)
        {
            return MxFramework.Runtime.RuntimeSaveStateResult<TComponent>.Failed(new MxFramework.Runtime.RuntimeSaveStateError(
                MxFramework.Runtime.RuntimeSaveStateErrorCode.InvalidDocument,
                "payload.payloadJson",
                "Component payload contains invalid value: " + exception.Message,
                payload != null ? payload.SchemaVersion : -1,
                schema.Version,
                exception));
        }

        private sealed class BuffPayload
        {
            public BuffEntryPayload[] Buffs { get; set; }
        }

        private sealed class BuffEntryPayload
        {
            public int BuffId { get; set; }
            public int StackCount { get; set; }
            public int MaxStackCount { get; set; }
            public long EndFrame { get; set; }
            public bool IsPermanent { get; set; }
            public int SourceId { get; set; }
        }
    }
}
