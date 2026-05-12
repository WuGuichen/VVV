namespace MxFramework.Gameplay
{
    public static class GameplayCoreComponentSchemaDescriptors
    {
        public const string IdentityStableId = "mxframework.gameplay.identity";
        public const string TeamStableId = "mxframework.gameplay.team";
        public const string LifecycleStableId = "mxframework.gameplay.lifecycle";
        public const string TagsStableId = "mxframework.gameplay.tags";
        public const string StatusesStableId = "mxframework.gameplay.statuses";

        public static void RegisterDiagnostics(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new IdentityDiagnostics());
            registry.Register(new TeamDiagnostics());
            registry.Register(new LifecycleDiagnostics());
            registry.Register(new TagsDiagnostics());
            registry.Register(new StatusesDiagnostics());
        }

        public static void RegisterRuntimeHash(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new IdentityHash());
            registry.Register(new TeamHash());
            registry.Register(new LifecycleHash());
            registry.Register(new TagsHash());
            registry.Register(new StatusesHash());
        }

        public static void RegisterSaveState(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new IdentitySaveState());
            registry.Register(new TeamSaveState());
            registry.Register(new LifecycleSaveState());
            registry.Register(new TagsSaveState());
            registry.Register(new StatusesSaveState());
        }

        private sealed class IdentityDiagnostics : IGameplayComponentDiagnosticWriter<GameplayIdentityComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                IdentityStableId,
                1,
                typeof(GameplayIdentityComponent),
                "Gameplay Identity",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in GameplayIdentityComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                WriteEntity(writer, entityId);
                writer.AddInt("definitionId", component.DefinitionId);
                writer.AddInt("variantId", component.VariantId);
                writer.AddBool("isNone", component.IsNone);
            }
        }

        private sealed class TeamDiagnostics : IGameplayComponentDiagnosticWriter<GameplayTeamComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                TeamStableId,
                1,
                typeof(GameplayTeamComponent),
                "Gameplay Team",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in GameplayTeamComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                WriteEntity(writer, entityId);
                writer.AddInt("teamId", component.TeamId);
                writer.AddBool("isNeutral", component.IsNeutral);
            }
        }

        private sealed class LifecycleDiagnostics : IGameplayComponentDiagnosticWriter<GameplayLifecycleComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                LifecycleStableId,
                1,
                typeof(GameplayLifecycleComponent),
                "Gameplay Lifecycle",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in GameplayLifecycleComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                WriteEntity(writer, entityId);
                writer.AddInt("state", (int)component.State);
                writer.AddBool("isAlive", component.IsAlive);
                writer.AddBool("isTerminal", component.IsTerminal);
            }
        }

        private sealed class TagsDiagnostics : IGameplayComponentDiagnosticWriter<GameplayTagComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                TagsStableId,
                1,
                typeof(GameplayTagComponent),
                "Gameplay Tags",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in GameplayTagComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                WriteEntity(writer, entityId);
                GameplayTagId[] ids = component.ToArray();
                writer.AddInt("count", ids.Length);
                for (int i = 0; i < ids.Length; i++)
                    writer.AddInt("id." + i, ids[i].Value);
            }
        }

        private sealed class StatusesDiagnostics : IGameplayComponentDiagnosticWriter<GameplayStatusComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                StatusesStableId,
                1,
                typeof(GameplayStatusComponent),
                "Gameplay Statuses",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in GameplayStatusComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                WriteEntity(writer, entityId);
                GameplayStatusId[] ids = component.ToArray();
                writer.AddInt("count", ids.Length);
                for (int i = 0; i < ids.Length; i++)
                    writer.AddInt("id." + i, ids[i].Value);
            }
        }

        private static void WriteEntity(GameplayComponentDiagnosticWriter writer, GameplayEntityId entityId)
        {
            writer.AddInt("entity.index", entityId.Index);
            writer.AddInt("entity.generation", entityId.Generation);
        }

        private sealed class IdentityHash : IGameplayComponentHashWriter<GameplayIdentityComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                IdentityStableId,
                1,
                typeof(GameplayIdentityComponent),
                "Gameplay Identity",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public void WriteHash(
                GameplayEntityId entityId,
                in GameplayIdentityComponent component,
                MxFramework.Runtime.RuntimeHashAccumulator accumulator)
            {
                accumulator.AddInt("definitionId", component.DefinitionId);
                accumulator.AddInt("variantId", component.VariantId);
            }
        }

        private sealed class TeamHash : IGameplayComponentHashWriter<GameplayTeamComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                TeamStableId,
                1,
                typeof(GameplayTeamComponent),
                "Gameplay Team",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public void WriteHash(
                GameplayEntityId entityId,
                in GameplayTeamComponent component,
                MxFramework.Runtime.RuntimeHashAccumulator accumulator)
            {
                accumulator.AddInt("teamId", component.TeamId);
            }
        }

        private sealed class LifecycleHash : IGameplayComponentHashWriter<GameplayLifecycleComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                LifecycleStableId,
                1,
                typeof(GameplayLifecycleComponent),
                "Gameplay Lifecycle",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public void WriteHash(
                GameplayEntityId entityId,
                in GameplayLifecycleComponent component,
                MxFramework.Runtime.RuntimeHashAccumulator accumulator)
            {
                accumulator.AddInt("state", (int)component.State);
            }
        }

        private sealed class TagsHash : IGameplayComponentHashWriter<GameplayTagComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                TagsStableId,
                1,
                typeof(GameplayTagComponent),
                "Gameplay Tags",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public void WriteHash(
                GameplayEntityId entityId,
                in GameplayTagComponent component,
                MxFramework.Runtime.RuntimeHashAccumulator accumulator)
            {
                GameplayTagId[] ids = component.ToArray();
                accumulator.AddInt("count", ids.Length);
                for (int i = 0; i < ids.Length; i++)
                    accumulator.AddInt("id", ids[i].Value);
            }
        }

        private sealed class StatusesHash : IGameplayComponentHashWriter<GameplayStatusComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                StatusesStableId,
                1,
                typeof(GameplayStatusComponent),
                "Gameplay Statuses",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public void WriteHash(
                GameplayEntityId entityId,
                in GameplayStatusComponent component,
                MxFramework.Runtime.RuntimeHashAccumulator accumulator)
            {
                GameplayStatusId[] ids = component.ToArray();
                accumulator.AddInt("count", ids.Length);
                for (int i = 0; i < ids.Length; i++)
                    accumulator.AddInt("id", ids[i].Value);
            }
        }

        private sealed class IdentitySaveState : IGameplayComponentSaveStateAdapter<GameplayIdentityComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                IdentityStableId,
                1,
                typeof(GameplayIdentityComponent),
                "Gameplay Identity",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public MxFramework.Runtime.RuntimeCustomState WriteSaveState(
                GameplayEntityId entityId,
                in GameplayIdentityComponent component)
            {
                return WritePayload(Schema, new IdentityPayload(component.DefinitionId, component.VariantId));
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayIdentityComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<IdentityPayload> result = ReadPayload<IdentityPayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayIdentityComponent>.Failed(result.Error);

                try
                {
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayIdentityComponent>.Succeeded(
                        new GameplayIdentityComponent(result.Value.DefinitionId, result.Value.VariantId));
                }
                catch (System.Exception exception)
                {
                    return InvalidPayload<GameplayIdentityComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class TeamSaveState : IGameplayComponentSaveStateAdapter<GameplayTeamComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                TeamStableId,
                1,
                typeof(GameplayTeamComponent),
                "Gameplay Team",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public MxFramework.Runtime.RuntimeCustomState WriteSaveState(
                GameplayEntityId entityId,
                in GameplayTeamComponent component)
            {
                return WritePayload(Schema, new TeamPayload(component.TeamId));
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayTeamComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<TeamPayload> result = ReadPayload<TeamPayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayTeamComponent>.Failed(result.Error);

                try
                {
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayTeamComponent>.Succeeded(
                        new GameplayTeamComponent(result.Value.TeamId));
                }
                catch (System.Exception exception)
                {
                    return InvalidPayload<GameplayTeamComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class LifecycleSaveState : IGameplayComponentSaveStateAdapter<GameplayLifecycleComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                LifecycleStableId,
                1,
                typeof(GameplayLifecycleComponent),
                "Gameplay Lifecycle",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public MxFramework.Runtime.RuntimeCustomState WriteSaveState(
                GameplayEntityId entityId,
                in GameplayLifecycleComponent component)
            {
                return WritePayload(Schema, new LifecyclePayload((int)component.State));
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayLifecycleComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<LifecyclePayload> result = ReadPayload<LifecyclePayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayLifecycleComponent>.Failed(result.Error);

                if (!System.Enum.IsDefined(typeof(GameplayLifecycleState), result.Value.State))
                    return InvalidPayload<GameplayLifecycleComponent>(Schema, payload, "Lifecycle state is not defined: " + result.Value.State);

                try
                {
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayLifecycleComponent>.Succeeded(
                        new GameplayLifecycleComponent((GameplayLifecycleState)result.Value.State));
                }
                catch (System.Exception exception)
                {
                    return InvalidPayload<GameplayLifecycleComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class TagsSaveState : IGameplayComponentSaveStateAdapter<GameplayTagComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                TagsStableId,
                1,
                typeof(GameplayTagComponent),
                "Gameplay Tags",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public MxFramework.Runtime.RuntimeCustomState WriteSaveState(
                GameplayEntityId entityId,
                in GameplayTagComponent component)
            {
                GameplayTagId[] ids = component.ToArray();
                var values = new int[ids.Length];
                for (int i = 0; i < ids.Length; i++)
                    values[i] = ids[i].Value;

                return WritePayload(Schema, new IdListPayload(values));
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayTagComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<IdListPayload> result = ReadPayload<IdListPayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayTagComponent>.Failed(result.Error);

                int[] values = result.Value.Ids ?? System.Array.Empty<int>();
                GameplayTagId[] ids;
                try
                {
                    ids = new GameplayTagId[values.Length];
                    for (int i = 0; i < values.Length; i++)
                        ids[i] = new GameplayTagId(values[i]);

                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayTagComponent>.Succeeded(new GameplayTagComponent(ids));
                }
                catch (System.Exception exception)
                {
                    return InvalidPayload<GameplayTagComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class StatusesSaveState : IGameplayComponentSaveStateAdapter<GameplayStatusComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                StatusesStableId,
                1,
                typeof(GameplayStatusComponent),
                "Gameplay Statuses",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);

            public MxFramework.Runtime.RuntimeCustomState WriteSaveState(
                GameplayEntityId entityId,
                in GameplayStatusComponent component)
            {
                GameplayStatusId[] ids = component.ToArray();
                var values = new int[ids.Length];
                for (int i = 0; i < ids.Length; i++)
                    values[i] = ids[i].Value;

                return WritePayload(Schema, new IdListPayload(values));
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayStatusComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<IdListPayload> result = ReadPayload<IdListPayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayStatusComponent>.Failed(result.Error);

                int[] values = result.Value.Ids ?? System.Array.Empty<int>();
                GameplayStatusId[] ids;
                try
                {
                    ids = new GameplayStatusId[values.Length];
                    for (int i = 0; i < values.Length; i++)
                        ids[i] = new GameplayStatusId(values[i]);

                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayStatusComponent>.Succeeded(new GameplayStatusComponent(ids));
                }
                catch (System.Exception exception)
                {
                    return InvalidPayload<GameplayStatusComponent>(Schema, payload, exception);
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

        private static MxFramework.Runtime.RuntimeSaveStateResult<TComponent> InvalidPayload<TComponent>(
            GameplayComponentSchema schema,
            MxFramework.Runtime.RuntimeCustomState payload,
            string message)
        {
            return MxFramework.Runtime.RuntimeSaveStateResult<TComponent>.Failed(new MxFramework.Runtime.RuntimeSaveStateError(
                MxFramework.Runtime.RuntimeSaveStateErrorCode.InvalidDocument,
                "payload.payloadJson",
                "Component payload contains invalid value: " + message,
                payload != null ? payload.SchemaVersion : -1,
                schema.Version));
        }

        private readonly struct IdentityPayload
        {
            public IdentityPayload(int definitionId, int variantId)
            {
                DefinitionId = definitionId;
                VariantId = variantId;
            }

            public int DefinitionId { get; }
            public int VariantId { get; }
        }

        private readonly struct TeamPayload
        {
            public TeamPayload(int teamId)
            {
                TeamId = teamId;
            }

            public int TeamId { get; }
        }

        private readonly struct LifecyclePayload
        {
            public LifecyclePayload(int state)
            {
                State = state;
            }

            public int State { get; }
        }

        private sealed class IdListPayload
        {
            public IdListPayload(int[] ids)
            {
                Ids = ids ?? System.Array.Empty<int>();
            }

            public int[] Ids { get; }
        }
    }
}
