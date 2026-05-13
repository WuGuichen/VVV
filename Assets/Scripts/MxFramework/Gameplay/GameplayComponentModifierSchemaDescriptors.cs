namespace MxFramework.Gameplay
{
    public static class GameplayComponentModifierSchemaDescriptors
    {
        public const string ModifiersStableId = "mxframework.gameplay.modifiers";

        public static void RegisterDiagnostics(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new ModifierDiagnostics());
        }

        public static void RegisterRuntimeHash(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new ModifierHash());
        }

        public static void RegisterSaveState(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new ModifierSaveState());
        }

        private static GameplayComponentSchema CreateSchema()
        {
            return new GameplayComponentSchema(
                ModifiersStableId,
                1,
                typeof(GameplayComponentModifierSetComponent),
                "Gameplay Component Modifiers",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);
        }

        private sealed class ModifierDiagnostics : IGameplayComponentDiagnosticWriter<GameplayComponentModifierSetComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in GameplayComponentModifierSetComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                writer.AddInt("entity.index", entityId.Index);
                writer.AddInt("entity.generation", entityId.Generation);
                GameplayComponentModifierEntry[] entries = component.ToArray();
                writer.AddInt("count", entries.Length);
                for (int i = 0; i < entries.Length; i++)
                {
                    writer.AddInt("modifier." + i + ".id", entries[i].ModifierId);
                    writer.AddInt("modifier." + i + ".attributeId", entries[i].AttributeId);
                    writer.AddInt("modifier." + i + ".addValue", entries[i].AddValue);
                    writer.AddInt("modifier." + i + ".sourceBuffId", entries[i].SourceBuffId);
                }
            }
        }

        private sealed class ModifierHash : IGameplayComponentHashWriter<GameplayComponentModifierSetComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public void WriteHash(
                GameplayEntityId entityId,
                in GameplayComponentModifierSetComponent component,
                MxFramework.Runtime.RuntimeHashAccumulator accumulator)
            {
                GameplayComponentModifierEntry[] entries = component.ToArray();
                accumulator.AddInt("count", entries.Length);
                for (int i = 0; i < entries.Length; i++)
                {
                    accumulator.AddInt("modifier.id", entries[i].ModifierId);
                    accumulator.AddInt("modifier.attributeId", entries[i].AttributeId);
                    accumulator.AddInt("modifier.addValue", entries[i].AddValue);
                    accumulator.AddInt("modifier.sourceBuffId", entries[i].SourceBuffId);
                }
            }
        }

        private sealed class ModifierSaveState : IGameplayComponentSaveStateAdapter<GameplayComponentModifierSetComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public MxFramework.Runtime.RuntimeCustomState WriteSaveState(
                GameplayEntityId entityId,
                in GameplayComponentModifierSetComponent component)
            {
                GameplayComponentModifierEntry[] entries = component.ToArray();
                var payloadEntries = new ModifierEntryPayload[entries.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    payloadEntries[i] = new ModifierEntryPayload
                    {
                        ModifierId = entries[i].ModifierId,
                        AttributeId = entries[i].AttributeId,
                        AddValue = entries[i].AddValue,
                        SourceBuffId = entries[i].SourceBuffId
                    };
                }

                return GameplayComponentSchemaPayload.Write(Schema, new ModifierPayload { Modifiers = payloadEntries });
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayComponentModifierSetComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<ModifierPayload> result =
                    GameplayComponentSchemaPayload.Read<ModifierPayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayComponentModifierSetComponent>.Failed(result.Error);

                try
                {
                    ModifierEntryPayload[] payloadEntries = result.Value != null && result.Value.Modifiers != null
                        ? result.Value.Modifiers
                        : System.Array.Empty<ModifierEntryPayload>();
                    var entries = new GameplayComponentModifierEntry[payloadEntries.Length];
                    for (int i = 0; i < payloadEntries.Length; i++)
                    {
                        entries[i] = new GameplayComponentModifierEntry(
                            payloadEntries[i].ModifierId,
                            payloadEntries[i].AttributeId,
                            payloadEntries[i].AddValue,
                            payloadEntries[i].SourceBuffId);
                    }

                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayComponentModifierSetComponent>.Succeeded(
                        new GameplayComponentModifierSetComponent(entries));
                }
                catch (System.Exception exception)
                {
                    return GameplayComponentSchemaPayload.Invalid<GameplayComponentModifierSetComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class ModifierPayload
        {
            public ModifierEntryPayload[] Modifiers { get; set; }
        }

        private sealed class ModifierEntryPayload
        {
            public int ModifierId { get; set; }
            public int AttributeId { get; set; }
            public int AddValue { get; set; }
            public int SourceBuffId { get; set; }
        }
    }
}
