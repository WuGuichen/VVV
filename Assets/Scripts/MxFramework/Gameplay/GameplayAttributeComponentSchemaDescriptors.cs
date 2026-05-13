namespace MxFramework.Gameplay
{
    public static class GameplayAttributeComponentSchemaDescriptors
    {
        public const string AttributesStableId = "mxframework.gameplay.attributes";

        public static void RegisterDiagnostics(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new AttributeDiagnostics());
        }

        public static void RegisterRuntimeHash(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new AttributeHash());
        }

        public static void RegisterSaveState(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new AttributeSaveState());
        }

        private static GameplayComponentSchema CreateSchema()
        {
            return new GameplayComponentSchema(
                AttributesStableId,
                1,
                typeof(GameplayAttributeSetComponent),
                "Gameplay Attributes",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);
        }

        private sealed class AttributeDiagnostics : IGameplayComponentDiagnosticWriter<GameplayAttributeSetComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in GameplayAttributeSetComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                writer.AddInt("entity.index", entityId.Index);
                writer.AddInt("entity.generation", entityId.Generation);
                GameplayAttributeValue[] values = component.ToArray();
                writer.AddInt("count", values.Length);
                for (int i = 0; i < values.Length; i++)
                {
                    writer.AddInt("attribute." + i + ".id", values[i].AttributeId);
                    writer.AddInt("attribute." + i + ".base", values[i].BaseValue);
                    writer.AddInt("attribute." + i + ".current", values[i].CurrentValue);
                }
            }
        }

        private sealed class AttributeHash : IGameplayComponentHashWriter<GameplayAttributeSetComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public void WriteHash(
                GameplayEntityId entityId,
                in GameplayAttributeSetComponent component,
                MxFramework.Runtime.RuntimeHashAccumulator accumulator)
            {
                GameplayAttributeValue[] values = component.ToArray();
                accumulator.AddInt("count", values.Length);
                for (int i = 0; i < values.Length; i++)
                {
                    accumulator.AddInt("attribute.id", values[i].AttributeId);
                    accumulator.AddInt("attribute.base", values[i].BaseValue);
                    accumulator.AddInt("attribute.current", values[i].CurrentValue);
                }
            }
        }

        private sealed class AttributeSaveState : IGameplayComponentSaveStateAdapter<GameplayAttributeSetComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public MxFramework.Runtime.RuntimeCustomState WriteSaveState(
                GameplayEntityId entityId,
                in GameplayAttributeSetComponent component)
            {
                GameplayAttributeValue[] values = component.ToArray();
                var payloadValues = new AttributeValuePayload[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    payloadValues[i] = new AttributeValuePayload
                    {
                        AttributeId = values[i].AttributeId,
                        BaseValue = values[i].BaseValue,
                        CurrentValue = values[i].CurrentValue
                    };
                }

                return GameplayComponentSchemaPayload.Write(Schema, new AttributeSetPayload { Attributes = payloadValues });
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayAttributeSetComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<AttributeSetPayload> result =
                    GameplayComponentSchemaPayload.Read<AttributeSetPayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayAttributeSetComponent>.Failed(result.Error);

                try
                {
                    AttributeValuePayload[] payloadValues = result.Value != null && result.Value.Attributes != null
                        ? result.Value.Attributes
                        : System.Array.Empty<AttributeValuePayload>();
                    var values = new GameplayAttributeValue[payloadValues.Length];
                    for (int i = 0; i < payloadValues.Length; i++)
                    {
                        values[i] = new GameplayAttributeValue(
                            payloadValues[i].AttributeId,
                            payloadValues[i].BaseValue,
                            payloadValues[i].CurrentValue);
                    }

                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayAttributeSetComponent>.Succeeded(
                        new GameplayAttributeSetComponent(values));
                }
                catch (System.Exception exception)
                {
                    return GameplayComponentSchemaPayload.Invalid<GameplayAttributeSetComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class AttributeSetPayload
        {
            public AttributeValuePayload[] Attributes { get; set; }
        }

        private sealed class AttributeValuePayload
        {
            public int AttributeId { get; set; }
            public int BaseValue { get; set; }
            public int CurrentValue { get; set; }
        }
    }
}
