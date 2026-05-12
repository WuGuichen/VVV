namespace MxFramework.Gameplay
{
    public static class GameplayAbilityCooldownComponentSchemaDescriptors
    {
        public const string CooldownsStableId = "mxframework.gameplay.ability-cooldowns";

        public static void RegisterDiagnostics(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new CooldownDiagnostics());
        }

        public static void RegisterRuntimeHash(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new CooldownHash());
        }

        public static void RegisterSaveState(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new CooldownSaveState());
        }

        private static GameplayComponentSchema CreateSchema()
        {
            return new GameplayComponentSchema(
                CooldownsStableId,
                1,
                typeof(GameplayAbilityCooldownComponent),
                "Gameplay Ability Cooldowns",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);
        }

        private sealed class CooldownDiagnostics : IGameplayComponentDiagnosticWriter<GameplayAbilityCooldownComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in GameplayAbilityCooldownComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                writer.AddInt("entity.index", entityId.Index);
                writer.AddInt("entity.generation", entityId.Generation);
                GameplayAbilityCooldownEntry[] entries = component.ToArray();
                writer.AddInt("count", entries.Length);
                for (int i = 0; i < entries.Length; i++)
                {
                    writer.AddInt("cooldown." + i + ".abilityId", entries[i].AbilityId);
                    writer.AddLong("cooldown." + i + ".endFrame", entries[i].EndFrame);
                }
            }
        }

        private sealed class CooldownHash : IGameplayComponentHashWriter<GameplayAbilityCooldownComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public void WriteHash(
                GameplayEntityId entityId,
                in GameplayAbilityCooldownComponent component,
                MxFramework.Runtime.RuntimeHashAccumulator accumulator)
            {
                GameplayAbilityCooldownEntry[] entries = component.ToArray();
                accumulator.AddInt("count", entries.Length);
                for (int i = 0; i < entries.Length; i++)
                {
                    accumulator.AddInt("cooldown.abilityId", entries[i].AbilityId);
                    accumulator.AddLong("cooldown.endFrame", entries[i].EndFrame);
                }
            }
        }

        private sealed class CooldownSaveState : IGameplayComponentSaveStateAdapter<GameplayAbilityCooldownComponent>
        {
            public GameplayComponentSchema Schema => CreateSchema();

            public MxFramework.Runtime.RuntimeCustomState WriteSaveState(
                GameplayEntityId entityId,
                in GameplayAbilityCooldownComponent component)
            {
                GameplayAbilityCooldownEntry[] entries = component.ToArray();
                var payloadEntries = new CooldownEntryPayload[entries.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    payloadEntries[i] = new CooldownEntryPayload
                    {
                        AbilityId = entries[i].AbilityId,
                        EndFrame = entries[i].EndFrame
                    };
                }

                return WritePayload(Schema, new CooldownPayload { Cooldowns = payloadEntries });
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayAbilityCooldownComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<CooldownPayload> result =
                    ReadPayload<CooldownPayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayAbilityCooldownComponent>.Failed(result.Error);

                try
                {
                    CooldownEntryPayload[] payloadEntries = result.Value != null && result.Value.Cooldowns != null
                        ? result.Value.Cooldowns
                        : System.Array.Empty<CooldownEntryPayload>();
                    var entries = new GameplayAbilityCooldownEntry[payloadEntries.Length];
                    for (int i = 0; i < payloadEntries.Length; i++)
                        entries[i] = new GameplayAbilityCooldownEntry(payloadEntries[i].AbilityId, payloadEntries[i].EndFrame);

                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayAbilityCooldownComponent>.Succeeded(
                        new GameplayAbilityCooldownComponent(entries));
                }
                catch (System.Exception exception)
                {
                    return InvalidPayload<GameplayAbilityCooldownComponent>(Schema, payload, exception);
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

        private sealed class CooldownPayload
        {
            public CooldownEntryPayload[] Cooldowns { get; set; }
        }

        private sealed class CooldownEntryPayload
        {
            public int AbilityId { get; set; }
            public long EndFrame { get; set; }
        }
    }
}
