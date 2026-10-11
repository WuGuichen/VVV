namespace MxFramework.Gameplay
{
    /// <summary>
    /// Registers diagnostics, deterministic hash, and save-state support for target classification.
    /// </summary>
    /// <remarks>Allocation: LowFreqAlloc during schema registration and serialization.</remarks>
    public static class GameplayTargetClassificationComponentSchemaDescriptors
    {
        public const string StableId =
            "mxframework.gameplay.target_classification";

        /// <summary>Frozen runtime hash schema id for target classification.</summary>
        private const int SchemaHashId =
            MxFramework.Runtime.RuntimeHashSchemaIds.GameplayComponentTargetClassification;

        /// <summary>Frozen field keys for the component hash scope.</summary>
        private const int FieldSchemaState = 1;
        private const int FieldEntityIndex = 2;
        private const int FieldEntityGeneration = 3;
        private const int FieldIdentityType = 4;
        private const int FieldCapabilities = 5;


        public static void RegisterDiagnostics(
            GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new Diagnostics());
        }

        public static void RegisterRuntimeHash(
            GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new RuntimeHash());
        }

        public static void RegisterSaveState(
            GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new SaveState());
        }

        private static GameplayComponentSchema CreateSchema()
        {
            return new GameplayComponentSchema(
                MxFramework.Runtime.RuntimeHashSchemaIds.GameplayComponentTargetClassification,
                StableId,
                1,
                typeof(GameplayTargetClassificationComponent),
                "Gameplay Target Classification",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);
        }

        private sealed class Diagnostics
            : IGameplayComponentDiagnosticWriter<
                GameplayTargetClassificationComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in GameplayTargetClassificationComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                writer.AddInt("entity.index", entityId.Index);
                writer.AddInt("entity.generation", entityId.Generation);
                writer.AddInt("identityType", (int)component.IdentityType);
                writer.AddInt("capabilities", (int)component.Capabilities);
                writer.AddBool("isSpecified", component.IsSpecified);
            }
        }

        private sealed class RuntimeHash
            : IGameplayComponentHashWriter<
                GameplayTargetClassificationComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public void WriteHash(
                GameplayEntityId entityId,
                in GameplayTargetClassificationComponent component,
                MxFramework.Runtime.RuntimeHashAccumulator accumulator)
            {
                accumulator.AddInt(
                    FieldIdentityType,
                    (int)component.IdentityType);
                accumulator.AddInt(
                    FieldCapabilities,
                    (int)component.Capabilities);
            }
        }

        private sealed class SaveState
            : IGameplayComponentSaveStateAdapter<
                GameplayTargetClassificationComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public MxFramework.Runtime.RuntimeCustomState WriteSaveState(
                GameplayEntityId entityId,
                in GameplayTargetClassificationComponent component)
            {
                return GameplayComponentSchemaPayload.Write(
                    Schema,
                    new Payload
                    {
                        IdentityType = (int)component.IdentityType,
                        Capabilities = (int)component.Capabilities
                    });
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<
                GameplayTargetClassificationComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<Payload> result =
                    GameplayComponentSchemaPayload.Read<Payload>(
                        Schema,
                        payload);
                if (!result.Success)
                {
                    return MxFramework.Runtime.RuntimeSaveStateResult<
                        GameplayTargetClassificationComponent>.Failed(
                            result.Error);
                }

                if (result.Value == null)
                {
                    return GameplayComponentSchemaPayload.Invalid<
                        GameplayTargetClassificationComponent>(
                            Schema,
                            payload,
                            "Target classification payload is null.");
                }

                try
                {
                    return MxFramework.Runtime.RuntimeSaveStateResult<
                        GameplayTargetClassificationComponent>.Succeeded(
                            new GameplayTargetClassificationComponent(
                                (GameplayTargetIdentityTypes)
                                    result.Value.IdentityType,
                                (GameplayTargetCapabilities)
                                    result.Value.Capabilities));
                }
                catch (System.Exception exception)
                {
                    return GameplayComponentSchemaPayload.Invalid<
                        GameplayTargetClassificationComponent>(
                            Schema,
                            payload,
                            exception);
                }
            }
        }

        private sealed class Payload
        {
            public int IdentityType { get; set; }

            public int Capabilities { get; set; }
        }
    }
}
