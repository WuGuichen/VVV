using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public static class GameplayComponentAuraSourceSchemaDescriptors
    {
        public const string AuraSourcesStableId = "mxframework.gameplay.aura_sources";

        private const int SchemaHashId = RuntimeHashSchemaIds.GameplayComponentAuraSource;
        private const int FieldEntryCount = 1;
        private const int FieldAuraId = 2;
        private const int FieldEndFrame = 3;
        private const int FieldIsPermanent = 4;
        private const int FieldSourceId = 5;
        public static void RegisterDiagnostics(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new AuraSourceDiagnostics());
        }

        public static void RegisterRuntimeHash(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new AuraSourceHash());
        }

        public static void RegisterSaveState(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new AuraSourceSaveState());
        }

        private static GameplayComponentSchema CreateSchema()
        {
            return new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentAuraSource,
                AuraSourcesStableId,
                1,
                typeof(GameplayComponentAuraSourceSetComponent),
                "Gameplay Component Aura Sources",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);
        }

        private sealed class AuraSourceDiagnostics : IGameplayComponentDiagnosticWriter<GameplayComponentAuraSourceSetComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in GameplayComponentAuraSourceSetComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                writer.AddInt("entity.index", entityId.Index);
                writer.AddInt("entity.generation", entityId.Generation);
                GameplayComponentAuraSourceEntry[] entries = component.ToArray();
                writer.AddInt("count", entries.Length);
                for (int i = 0; i < entries.Length; i++)
                {
                    writer.AddInt("aura." + i + ".id", entries[i].AuraId);
                    writer.AddLong("aura." + i + ".endFrame", entries[i].EndFrame);
                    writer.AddBool("aura." + i + ".permanent", entries[i].IsPermanent);
                    writer.AddInt("aura." + i + ".sourceId", entries[i].SourceId);
                }
            }
        }

        private sealed class AuraSourceHash : IGameplayComponentHashWriter<GameplayComponentAuraSourceSetComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public void WriteHash(
                GameplayEntityId entityId,
                in GameplayComponentAuraSourceSetComponent component,
                RuntimeHashAccumulator accumulator)
            {
                GameplayComponentAuraSourceEntry[] entries = component.ToArray();
                accumulator.BeginArray(FieldEntryCount, entries.Length);
                for (int i = 0; i < entries.Length; i++)
                {
                    accumulator.BeginElement(i);
                    accumulator.AddInt(FieldAuraId, entries[i].AuraId);
                    accumulator.AddLong(FieldEndFrame, entries[i].EndFrame);
                    accumulator.AddBool(FieldIsPermanent, entries[i].IsPermanent);
                    accumulator.AddInt(FieldSourceId, entries[i].SourceId);
                    accumulator.EndElement();
                }
                accumulator.EndArray();
            }
        }

        private sealed class AuraSourceSaveState : IGameplayComponentSaveStateAdapter<GameplayComponentAuraSourceSetComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public MxFramework.Runtime.RuntimeCustomState WriteSaveState(
                GameplayEntityId entityId,
                in GameplayComponentAuraSourceSetComponent component)
            {
                GameplayComponentAuraSourceEntry[] entries = component.ToArray();
                var payloadEntries = new AuraSourceEntryPayload[entries.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    payloadEntries[i] = new AuraSourceEntryPayload
                    {
                        AuraId = entries[i].AuraId,
                        EndFrame = entries[i].EndFrame,
                        IsPermanent = entries[i].IsPermanent,
                        SourceId = entries[i].SourceId
                    };
                }

                return GameplayComponentSchemaPayload.Write(Schema, new AuraSourcePayload { AuraSources = payloadEntries });
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayComponentAuraSourceSetComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<AuraSourcePayload> result =
                    GameplayComponentSchemaPayload.Read<AuraSourcePayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayComponentAuraSourceSetComponent>.Failed(result.Error);

                try
                {
                    AuraSourceEntryPayload[] payloadEntries = result.Value != null && result.Value.AuraSources != null
                        ? result.Value.AuraSources
                        : System.Array.Empty<AuraSourceEntryPayload>();
                    var entries = new GameplayComponentAuraSourceEntry[payloadEntries.Length];
                    for (int i = 0; i < payloadEntries.Length; i++)
                    {
                        entries[i] = new GameplayComponentAuraSourceEntry(
                            payloadEntries[i].AuraId,
                            payloadEntries[i].EndFrame,
                            payloadEntries[i].IsPermanent,
                            payloadEntries[i].SourceId);
                    }

                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayComponentAuraSourceSetComponent>.Succeeded(
                        new GameplayComponentAuraSourceSetComponent(entries));
                }
                catch (System.Exception exception)
                {
                    return GameplayComponentSchemaPayload.Invalid<GameplayComponentAuraSourceSetComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class AuraSourcePayload
        {
            public AuraSourceEntryPayload[] AuraSources { get; set; }
        }

        private sealed class AuraSourceEntryPayload
        {
            public int AuraId { get; set; }
            public long EndFrame { get; set; }
            public bool IsPermanent { get; set; }
            public int SourceId { get; set; }
        }
    }
}
