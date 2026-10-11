using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public static class GameplayComponentBuffSchemaDescriptors
    {
        public const string BuffsStableId = "mxframework.gameplay.buffs";

        private const int SchemaHashId = RuntimeHashSchemaIds.GameplayComponentBuff;
        private const int FieldEntryCount = 1;
        private const int FieldBuffId = 2;
        private const int FieldStackCount = 3;
        private const int FieldStartFrame = 4;
        private const int FieldEndFrame = 5;
        private const int FieldIsPermanent = 6;
        private const int FieldSourceId = 7;
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
                RuntimeHashSchemaIds.GameplayComponentBuff,
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
                    writer.AddLong("buff." + i + ".startFrame", entries[i].StartFrame);
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
                RuntimeHashAccumulator accumulator)
            {
                GameplayComponentBuffEntry[] entries = component.ToArray();
                accumulator.BeginArray(FieldEntryCount, entries.Length);
                for (int i = 0; i < entries.Length; i++)
                {
                    accumulator.BeginElement(i);
                    accumulator.AddInt(FieldBuffId, entries[i].BuffId);
                    accumulator.AddInt(FieldStackCount, entries[i].StackCount);
                    accumulator.AddLong(FieldStartFrame, entries[i].StartFrame);
                    accumulator.AddLong(FieldEndFrame, entries[i].EndFrame);
                    accumulator.AddBool(FieldIsPermanent, entries[i].IsPermanent);
                    accumulator.AddInt(FieldSourceId, entries[i].SourceId);
                    accumulator.EndElement();
                }
                accumulator.EndArray();
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
                        StartFrame = entries[i].StartFrame,
                        EndFrame = entries[i].EndFrame,
                        IsPermanent = entries[i].IsPermanent,
                        SourceId = entries[i].SourceId
                    };
                }

                return GameplayComponentSchemaPayload.Write(Schema, new BuffPayload { Buffs = payloadEntries });
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayComponentBuffSetComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<BuffPayload> result =
                    GameplayComponentSchemaPayload.Read<BuffPayload>(Schema, payload);
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
                            payloadEntries[i].StartFrame,
                            payloadEntries[i].EndFrame,
                            payloadEntries[i].IsPermanent,
                            payloadEntries[i].SourceId);
                    }

                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayComponentBuffSetComponent>.Succeeded(
                        new GameplayComponentBuffSetComponent(entries));
                }
                catch (System.Exception exception)
                {
                    return GameplayComponentSchemaPayload.Invalid<GameplayComponentBuffSetComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class BuffPayload
        {
            public BuffEntryPayload[] Buffs { get; set; }
        }

        private sealed class BuffEntryPayload
        {
            public int BuffId { get; set; }
            public int StackCount { get; set; }
            public long StartFrame { get; set; }
            public long EndFrame { get; set; }
            public bool IsPermanent { get; set; }
            public int SourceId { get; set; }
        }
    }
}
