using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public static class GameplayCoreComponentSchemaDescriptors
    {
        public const string IdentityStableId = "mxframework.gameplay.identity";
        public const string TargetClassificationStableId =
            GameplayTargetClassificationComponentSchemaDescriptors.StableId;
        public const string TeamStableId = "mxframework.gameplay.team";
        public const string LifecycleStableId = "mxframework.gameplay.lifecycle";
        public const string TagsStableId = "mxframework.gameplay.tags";
        public const string StatusesStableId = "mxframework.gameplay.statuses";
        public const string ScatterPressureStableId = "mxframework.gameplay.scatter_pressure";
        public const string GuardPressureStableId = "mxframework.gameplay.guard_pressure";
        public const string ArmorIntegrityStableId = "mxframework.gameplay.armor_integrity";

        /// <summary>
        /// Frozen runtime hash schema id shared by every core component
        /// (RuntimeHashSchemaIds.GameplayComponentCore).
        /// </summary>
        private const int CoreSchemaHashId = RuntimeHashSchemaIds.GameplayComponentCore;

        /// <summary>
        /// Frozen field keys for the core component hash scopes. All eight core components share one
        /// schema id, so each gets its own table with keys contiguous from 1. The entity index and
        /// generation live at the entity object level in the contributor, not in the component payload.
        /// </summary>
        private static class HashFields
        {
            /// <summary>Identity payload fields.</summary>
            public static class Identity
            {
                public const int DefinitionId = 1;
                public const int VariantId = 2;
            }

            /// <summary>Team payload fields.</summary>
            public static class Team
            {
                public const int TeamId = 1;
            }

            /// <summary>Lifecycle payload fields.</summary>
            public static class Lifecycle
            {
                public const int State = 1;
            }

            /// <summary>Tags payload fields.</summary>
            public static class Tags
            {
                public const int TagCount = 1;
                public const int TagValue = 2;
            }

            /// <summary>Statuses payload fields.</summary>
            public static class Statuses
            {
                public const int StatusCount = 1;
                public const int StatusId = 2;
                public const int SourceBuffId = 3;
            }

            /// <summary>Scatter pressure payload fields.</summary>
            public static class ScatterPressure
            {
                public const int MaxScatterPressure = 1;
                public const int MaxScatterDebt = 2;
                public const int CurrentCohesionLimit = 3;
                public const int RecoveryRate = 4;
                public const int RecoveryDelayFrames = 5;
                public const int BrokenRecoveryDelayFrames = 6;
                public const int CurrentScatterPressure = 7;
                public const int CurrentBand = 8;
                public const int LastPressureFrame = 9;
                public const int LastCohesionBreakFrame = 10;
                public const int IsBroken = 11;
            }

            /// <summary>Guard pressure payload fields.</summary>
            public static class GuardPressure
            {
                public const int MaxPressure = 1;
                public const int RecoveryRate = 2;
                public const int RecoveryDelayFrames = 3;
                public const int CurrentPressure = 4;
                public const int CurrentBand = 5;
                public const int LastPressureFrame = 6;
                public const int IsBroken = 7;
            }

            /// <summary>Armor integrity payload fields.</summary>
            public static class ArmorIntegrity
            {
                public const int MaxIntegrity = 1;
                public const int CurrentIntegrity = 2;
                public const int IsBroken = 3;
            }
        }

        public static void RegisterDiagnostics(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new IdentityDiagnostics());
            GameplayTargetClassificationComponentSchemaDescriptors
                .RegisterDiagnostics(registry);
            registry.Register(new TeamDiagnostics());
            registry.Register(new LifecycleDiagnostics());
            registry.Register(new TagsDiagnostics());
            registry.Register(new StatusesDiagnostics());
            registry.Register(new ScatterPressureDiagnostics());
            registry.Register(new GuardPressureDiagnostics());
            registry.Register(new ArmorIntegrityDiagnostics());
        }

        public static void RegisterRuntimeHash(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new IdentityHash());
            GameplayTargetClassificationComponentSchemaDescriptors
                .RegisterRuntimeHash(registry);
            registry.Register(new TeamHash());
            registry.Register(new LifecycleHash());
            registry.Register(new TagsHash());
            registry.Register(new StatusesHash());
            registry.Register(new ScatterPressureHash());
            registry.Register(new GuardPressureHash());
            registry.Register(new ArmorIntegrityHash());
        }

        public static void RegisterSaveState(GameplayComponentSchemaRegistry registry)
        {
            if (registry == null)
                throw new System.ArgumentNullException(nameof(registry));

            registry.Register(new IdentitySaveState());
            GameplayTargetClassificationComponentSchemaDescriptors
                .RegisterSaveState(registry);
            registry.Register(new TeamSaveState());
            registry.Register(new LifecycleSaveState());
            registry.Register(new TagsSaveState());
            registry.Register(new StatusesSaveState());
            registry.Register(new ScatterPressureSaveState());
            registry.Register(new GuardPressureSaveState());
            registry.Register(new ArmorIntegritySaveState());
        }

        private sealed class IdentityDiagnostics : IGameplayComponentDiagnosticWriter<GameplayIdentityComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentIdentity,
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
                RuntimeHashSchemaIds.GameplayComponentTeam,
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
                RuntimeHashSchemaIds.GameplayComponentLifecycle,
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
                RuntimeHashSchemaIds.GameplayComponentTags,
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
                RuntimeHashSchemaIds.GameplayComponentStatuses,
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
                GameplayStatusEntry[] entries = component.ToEntries();
                writer.AddInt("count", entries.Length);
                for (int i = 0; i < entries.Length; i++)
                {
                    writer.AddInt("status." + i + ".id", entries[i].StatusId.Value);
                    writer.AddInt("status." + i + ".sourceBuffId", entries[i].SourceBuffId);
                }
            }
        }

        private sealed class ScatterPressureDiagnostics : IGameplayComponentDiagnosticWriter<GameplayScatterPressureComponent>
        {
            public GameplayComponentSchema Schema => CreateScatterPressureSchema();

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in GameplayScatterPressureComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                WriteEntity(writer, entityId);
                writer.AddInt("maxScatterPressure", component.MaxScatterPressure);
                writer.AddInt("maxScatterDebt", component.MaxScatterDebt);
                writer.AddInt("currentCohesionLimit", component.CurrentCohesionLimit);
                writer.AddInt("recoveryRate", component.RecoveryRate);
                writer.AddInt("recoveryDelayFrames", component.RecoveryDelayFrames);
                writer.AddInt("brokenRecoveryDelayFrames", component.BrokenRecoveryDelayFrames);
                writer.AddInt("currentScatterPressure", component.CurrentScatterPressure);
                writer.AddInt("currentBand", (int)component.CurrentBand);
                writer.AddLong("lastPressureFrame", component.LastPressureFrame);
                writer.AddLong("lastCohesionBreakFrame", component.LastCohesionBreakFrame);
                writer.AddBool("isBroken", component.IsBroken);
            }
        }

        private sealed class GuardPressureDiagnostics : IGameplayComponentDiagnosticWriter<GameplayGuardPressureComponent>
        {
            public GameplayComponentSchema Schema => CreateGuardPressureSchema();

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in GameplayGuardPressureComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                WriteEntity(writer, entityId);
                writer.AddInt("maxPressure", component.MaxPressure);
                writer.AddInt("recoveryRate", component.RecoveryRate);
                writer.AddInt("recoveryDelayFrames", component.RecoveryDelayFrames);
                writer.AddInt("currentPressure", component.CurrentPressure);
                writer.AddInt("currentBand", (int)component.CurrentBand);
                writer.AddLong("lastPressureFrame", component.LastPressureFrame);
                writer.AddBool("isBroken", component.IsBroken);
            }
        }

        private sealed class ArmorIntegrityDiagnostics : IGameplayComponentDiagnosticWriter<GameplayArmorIntegrityComponent>
        {
            public GameplayComponentSchema Schema => CreateArmorIntegritySchema();

            public void WriteDiagnostics(
                GameplayEntityId entityId,
                in GameplayArmorIntegrityComponent component,
                GameplayComponentDiagnosticWriter writer)
            {
                WriteEntity(writer, entityId);
                writer.AddInt("maxIntegrity", component.MaxIntegrity);
                writer.AddInt("currentIntegrity", component.CurrentIntegrity);
                writer.AddBool("isBroken", component.IsBroken);
            }
        }

        private static void WriteEntity(GameplayComponentDiagnosticWriter writer, GameplayEntityId entityId)
        {
            writer.AddInt("entity.index", entityId.Index);
            writer.AddInt("entity.generation", entityId.Generation);
        }

        private static GameplayComponentSchema CreateScatterPressureSchema()
        {
            return new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentScatterPressure,
                ScatterPressureStableId,
                1,
                typeof(GameplayScatterPressureComponent),
                "Gameplay Scatter Pressure",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);
        }

        private static GameplayComponentSchema CreateGuardPressureSchema()
        {
            return new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentGuardPressure,
                GuardPressureStableId,
                1,
                typeof(GameplayGuardPressureComponent),
                "Gameplay Guard Pressure",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);
        }

        private static GameplayComponentSchema CreateArmorIntegritySchema()
        {
            return new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentArmorIntegrity,
                ArmorIntegrityStableId,
                1,
                typeof(GameplayArmorIntegrityComponent),
                "Gameplay Armor Integrity",
                supportsDiagnostics: true,
                supportsHash: true,
                supportsSaveState: true);
        }

        private sealed class IdentityHash : IGameplayComponentHashWriter<GameplayIdentityComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentIdentity,
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
                accumulator.AddInt(HashFields.Identity.DefinitionId, component.DefinitionId);
                accumulator.AddInt(HashFields.Identity.VariantId, component.VariantId);
            }
        }

        private sealed class TeamHash : IGameplayComponentHashWriter<GameplayTeamComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentTeam,
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
                accumulator.AddInt(HashFields.Team.TeamId, component.TeamId);
            }
        }

        private sealed class LifecycleHash : IGameplayComponentHashWriter<GameplayLifecycleComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentLifecycle,
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
                accumulator.AddInt(HashFields.Lifecycle.State, (int)component.State);
            }
        }

        private sealed class TagsHash : IGameplayComponentHashWriter<GameplayTagComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentTags,
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
                accumulator.BeginArray(HashFields.Tags.TagCount, ids.Length);
                for (int i = 0; i < ids.Length; i++)
                {
                    accumulator.BeginElement(i);
                    accumulator.AddInt(HashFields.Tags.TagValue, ids[i].Value);
                    accumulator.EndElement();
                }
                accumulator.EndArray();
            }
        }

        private sealed class StatusesHash : IGameplayComponentHashWriter<GameplayStatusComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentStatuses,
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
                GameplayStatusEntry[] entries = component.ToEntries();
                accumulator.BeginArray(HashFields.Statuses.StatusCount, entries.Length);
                for (int i = 0; i < entries.Length; i++)
                {
                    accumulator.BeginElement(i);
                    accumulator.AddInt(HashFields.Statuses.StatusId, entries[i].StatusId.Value);
                    accumulator.AddInt(HashFields.Statuses.SourceBuffId, entries[i].SourceBuffId);
                    accumulator.EndElement();
                }
                accumulator.EndArray();
            }
        }

        private sealed class ScatterPressureHash : IGameplayComponentHashWriter<GameplayScatterPressureComponent>
        {
            public GameplayComponentSchema Schema => CreateScatterPressureSchema();

            public void WriteHash(
                GameplayEntityId entityId,
                in GameplayScatterPressureComponent component,
                MxFramework.Runtime.RuntimeHashAccumulator accumulator)
            {
                accumulator.AddInt(HashFields.ScatterPressure.MaxScatterPressure, component.MaxScatterPressure);
                accumulator.AddInt(HashFields.ScatterPressure.MaxScatterDebt, component.MaxScatterDebt);
                accumulator.AddInt(HashFields.ScatterPressure.CurrentCohesionLimit, component.CurrentCohesionLimit);
                accumulator.AddInt(HashFields.ScatterPressure.RecoveryRate, component.RecoveryRate);
                accumulator.AddInt(HashFields.ScatterPressure.RecoveryDelayFrames, component.RecoveryDelayFrames);
                accumulator.AddInt(HashFields.ScatterPressure.BrokenRecoveryDelayFrames, component.BrokenRecoveryDelayFrames);
                accumulator.AddInt(HashFields.ScatterPressure.CurrentScatterPressure, component.CurrentScatterPressure);
                accumulator.AddInt(HashFields.ScatterPressure.CurrentBand, (int)component.CurrentBand);
                accumulator.AddLong(HashFields.ScatterPressure.LastPressureFrame, component.LastPressureFrame);
                accumulator.AddLong(HashFields.ScatterPressure.LastCohesionBreakFrame, component.LastCohesionBreakFrame);
                accumulator.AddBool(HashFields.ScatterPressure.IsBroken, component.IsBroken);
            }
        }

        private sealed class GuardPressureHash : IGameplayComponentHashWriter<GameplayGuardPressureComponent>
        {
            public GameplayComponentSchema Schema => CreateGuardPressureSchema();

            public void WriteHash(
                GameplayEntityId entityId,
                in GameplayGuardPressureComponent component,
                MxFramework.Runtime.RuntimeHashAccumulator accumulator)
            {
                accumulator.AddInt(HashFields.GuardPressure.MaxPressure, component.MaxPressure);
                accumulator.AddInt(HashFields.GuardPressure.RecoveryRate, component.RecoveryRate);
                accumulator.AddInt(HashFields.GuardPressure.RecoveryDelayFrames, component.RecoveryDelayFrames);
                accumulator.AddInt(HashFields.GuardPressure.CurrentPressure, component.CurrentPressure);
                accumulator.AddInt(HashFields.GuardPressure.CurrentBand, (int)component.CurrentBand);
                accumulator.AddLong(HashFields.GuardPressure.LastPressureFrame, component.LastPressureFrame);
                accumulator.AddBool(HashFields.GuardPressure.IsBroken, component.IsBroken);
            }
        }

        private sealed class ArmorIntegrityHash : IGameplayComponentHashWriter<GameplayArmorIntegrityComponent>
        {
            public GameplayComponentSchema Schema => CreateArmorIntegritySchema();

            public void WriteHash(
                GameplayEntityId entityId,
                in GameplayArmorIntegrityComponent component,
                MxFramework.Runtime.RuntimeHashAccumulator accumulator)
            {
                accumulator.AddInt(HashFields.ArmorIntegrity.MaxIntegrity, component.MaxIntegrity);
                accumulator.AddInt(HashFields.ArmorIntegrity.CurrentIntegrity, component.CurrentIntegrity);
                accumulator.AddBool(HashFields.ArmorIntegrity.IsBroken, component.IsBroken);
            }
        }

        private sealed class IdentitySaveState : IGameplayComponentSaveStateAdapter<GameplayIdentityComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentIdentity,
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
                return GameplayComponentSchemaPayload.Write(Schema, new IdentityPayload(component.DefinitionId, component.VariantId));
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayIdentityComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<IdentityPayload> result =
                    GameplayComponentSchemaPayload.Read<IdentityPayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayIdentityComponent>.Failed(result.Error);

                try
                {
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayIdentityComponent>.Succeeded(
                        new GameplayIdentityComponent(result.Value.DefinitionId, result.Value.VariantId));
                }
                catch (System.Exception exception)
                {
                    return GameplayComponentSchemaPayload.Invalid<GameplayIdentityComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class TeamSaveState : IGameplayComponentSaveStateAdapter<GameplayTeamComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentTeam,
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
                return GameplayComponentSchemaPayload.Write(Schema, new TeamPayload(component.TeamId));
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayTeamComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<TeamPayload> result =
                    GameplayComponentSchemaPayload.Read<TeamPayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayTeamComponent>.Failed(result.Error);

                try
                {
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayTeamComponent>.Succeeded(
                        new GameplayTeamComponent(result.Value.TeamId));
                }
                catch (System.Exception exception)
                {
                    return GameplayComponentSchemaPayload.Invalid<GameplayTeamComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class LifecycleSaveState : IGameplayComponentSaveStateAdapter<GameplayLifecycleComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentLifecycle,
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
                return GameplayComponentSchemaPayload.Write(Schema, new LifecyclePayload((int)component.State));
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayLifecycleComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<LifecyclePayload> result =
                    GameplayComponentSchemaPayload.Read<LifecyclePayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayLifecycleComponent>.Failed(result.Error);

                if (!System.Enum.IsDefined(typeof(GameplayLifecycleState), result.Value.State))
                    return GameplayComponentSchemaPayload.Invalid<GameplayLifecycleComponent>(Schema, payload, "Lifecycle state is not defined: " + result.Value.State);

                try
                {
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayLifecycleComponent>.Succeeded(
                        new GameplayLifecycleComponent((GameplayLifecycleState)result.Value.State));
                }
                catch (System.Exception exception)
                {
                    return GameplayComponentSchemaPayload.Invalid<GameplayLifecycleComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class TagsSaveState : IGameplayComponentSaveStateAdapter<GameplayTagComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentTags,
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

                return GameplayComponentSchemaPayload.Write(Schema, new IdListPayload(values));
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayTagComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<IdListPayload> result =
                    GameplayComponentSchemaPayload.Read<IdListPayload>(Schema, payload);
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
                    return GameplayComponentSchemaPayload.Invalid<GameplayTagComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class StatusesSaveState : IGameplayComponentSaveStateAdapter<GameplayStatusComponent>
        {
            public GameplayComponentSchema Schema => new GameplayComponentSchema(
                RuntimeHashSchemaIds.GameplayComponentStatuses,
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
                GameplayStatusEntry[] entries = component.ToEntries();
                var values = new StatusEntryPayload[entries.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    values[i] = new StatusEntryPayload
                    {
                        Id = entries[i].StatusId.Value,
                        SourceBuffId = entries[i].SourceBuffId
                    };
                }

                return GameplayComponentSchemaPayload.Write(Schema, new StatusListPayload(values));
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayStatusComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<StatusListPayload> result =
                    GameplayComponentSchemaPayload.Read<StatusListPayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayStatusComponent>.Failed(result.Error);

                try
                {
                    StatusEntryPayload[] values = result.Value != null && result.Value.Statuses != null
                        ? result.Value.Statuses
                        : System.Array.Empty<StatusEntryPayload>();
                    var entries = new GameplayStatusEntry[values.Length];
                    for (int i = 0; i < values.Length; i++)
                    {
                        entries[i] = new GameplayStatusEntry(
                            new GameplayStatusId(values[i].Id),
                            values[i].SourceBuffId);
                    }

                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayStatusComponent>.Succeeded(new GameplayStatusComponent(entries));
                }
                catch (System.Exception exception)
                {
                    return GameplayComponentSchemaPayload.Invalid<GameplayStatusComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class ScatterPressureSaveState : IGameplayComponentSaveStateAdapter<GameplayScatterPressureComponent>
        {
            public GameplayComponentSchema Schema => CreateScatterPressureSchema();

            public MxFramework.Runtime.RuntimeCustomState WriteSaveState(
                GameplayEntityId entityId,
                in GameplayScatterPressureComponent component)
            {
                return GameplayComponentSchemaPayload.Write(
                    Schema,
                    new ScatterPressurePayload
                    {
                        MaxScatterPressure = component.MaxScatterPressure,
                        MaxScatterDebt = component.MaxScatterDebt,
                        CurrentCohesionLimit = component.CurrentCohesionLimit,
                        RecoveryRate = component.RecoveryRate,
                        RecoveryDelayFrames = component.RecoveryDelayFrames,
                        BrokenRecoveryDelayFrames = component.BrokenRecoveryDelayFrames,
                        CurrentScatterPressure = component.CurrentScatterPressure,
                        CurrentBand = (int)component.CurrentBand,
                        LastPressureFrame = component.LastPressureFrame,
                        LastCohesionBreakFrame = component.LastCohesionBreakFrame,
                        IsBroken = component.IsBroken
                    });
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayScatterPressureComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<ScatterPressurePayload> result =
                    GameplayComponentSchemaPayload.Read<ScatterPressurePayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayScatterPressureComponent>.Failed(result.Error);
                if (result.Value == null)
                    return GameplayComponentSchemaPayload.Invalid<GameplayScatterPressureComponent>(Schema, payload, "Scatter pressure payload is null.");
                if (!PressureBandUtility.IsDefinedValue(result.Value.CurrentBand))
                    return GameplayComponentSchemaPayload.Invalid<GameplayScatterPressureComponent>(Schema, payload, "Scatter pressure band is not defined: " + result.Value.CurrentBand);

                try
                {
                    var band = (PressureBand)result.Value.CurrentBand;
                    GameplayScatterPressureComponent.Validate(
                        result.Value.MaxScatterPressure,
                        result.Value.RecoveryRate,
                        result.Value.RecoveryDelayFrames,
                        result.Value.CurrentScatterPressure,
                        result.Value.LastPressureFrame,
                        band,
                        result.Value.IsBroken,
                        result.Value.CurrentCohesionLimit,
                        result.Value.MaxScatterDebt,
                        result.Value.BrokenRecoveryDelayFrames,
                        result.Value.LastCohesionBreakFrame);

                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayScatterPressureComponent>.Succeeded(
                        new GameplayScatterPressureComponent
                        {
                            MaxScatterPressure = result.Value.MaxScatterPressure,
                            MaxScatterDebt = result.Value.MaxScatterDebt <= 0 ? result.Value.MaxScatterPressure : result.Value.MaxScatterDebt,
                            CurrentCohesionLimit = result.Value.CurrentCohesionLimit <= 0 ? result.Value.MaxScatterPressure : result.Value.CurrentCohesionLimit,
                            RecoveryRate = result.Value.RecoveryRate,
                            RecoveryDelayFrames = result.Value.RecoveryDelayFrames,
                            BrokenRecoveryDelayFrames = result.Value.BrokenRecoveryDelayFrames,
                            CurrentScatterPressure = result.Value.CurrentScatterPressure,
                            CurrentBand = band,
                            LastPressureFrame = result.Value.LastPressureFrame,
                            LastCohesionBreakFrame = result.Value.LastCohesionBreakFrame,
                            IsBroken = result.Value.IsBroken
                        });
                }
                catch (System.Exception exception)
                {
                    return GameplayComponentSchemaPayload.Invalid<GameplayScatterPressureComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class GuardPressureSaveState : IGameplayComponentSaveStateAdapter<GameplayGuardPressureComponent>
        {
            public GameplayComponentSchema Schema => CreateGuardPressureSchema();

            public MxFramework.Runtime.RuntimeCustomState WriteSaveState(
                GameplayEntityId entityId,
                in GameplayGuardPressureComponent component)
            {
                return GameplayComponentSchemaPayload.Write(
                    Schema,
                    new GuardPressurePayload
                    {
                        MaxPressure = component.MaxPressure,
                        RecoveryRate = component.RecoveryRate,
                        RecoveryDelayFrames = component.RecoveryDelayFrames,
                        CurrentPressure = component.CurrentPressure,
                        CurrentBand = (int)component.CurrentBand,
                        LastPressureFrame = component.LastPressureFrame,
                        IsBroken = component.IsBroken
                    });
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayGuardPressureComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<GuardPressurePayload> result =
                    GameplayComponentSchemaPayload.Read<GuardPressurePayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayGuardPressureComponent>.Failed(result.Error);
                if (result.Value == null)
                    return GameplayComponentSchemaPayload.Invalid<GameplayGuardPressureComponent>(Schema, payload, "Guard pressure payload is null.");
                if (!PressureBandUtility.IsDefinedValue(result.Value.CurrentBand))
                    return GameplayComponentSchemaPayload.Invalid<GameplayGuardPressureComponent>(Schema, payload, "Guard pressure band is not defined: " + result.Value.CurrentBand);

                try
                {
                    var band = (PressureBand)result.Value.CurrentBand;
                    GameplayGuardPressureComponent.Validate(
                        result.Value.MaxPressure,
                        result.Value.RecoveryRate,
                        result.Value.RecoveryDelayFrames,
                        result.Value.CurrentPressure,
                        result.Value.LastPressureFrame,
                        band,
                        result.Value.IsBroken);

                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayGuardPressureComponent>.Succeeded(
                        new GameplayGuardPressureComponent
                        {
                            MaxPressure = result.Value.MaxPressure,
                            RecoveryRate = result.Value.RecoveryRate,
                            RecoveryDelayFrames = result.Value.RecoveryDelayFrames,
                            CurrentPressure = result.Value.CurrentPressure,
                            CurrentBand = band,
                            LastPressureFrame = result.Value.LastPressureFrame,
                            IsBroken = result.Value.IsBroken
                        });
                }
                catch (System.Exception exception)
                {
                    return GameplayComponentSchemaPayload.Invalid<GameplayGuardPressureComponent>(Schema, payload, exception);
                }
            }
        }

        private sealed class ArmorIntegritySaveState : IGameplayComponentSaveStateAdapter<GameplayArmorIntegrityComponent>
        {
            public GameplayComponentSchema Schema => CreateArmorIntegritySchema();

            public MxFramework.Runtime.RuntimeCustomState WriteSaveState(
                GameplayEntityId entityId,
                in GameplayArmorIntegrityComponent component)
            {
                return GameplayComponentSchemaPayload.Write(
                    Schema,
                    new ArmorIntegrityPayload
                    {
                        MaxIntegrity = component.MaxIntegrity,
                        CurrentIntegrity = component.CurrentIntegrity,
                        IsBroken = component.IsBroken
                    });
            }

            public MxFramework.Runtime.RuntimeSaveStateResult<GameplayArmorIntegrityComponent> ReadSaveState(
                GameplayEntityId entityId,
                MxFramework.Runtime.RuntimeCustomState payload)
            {
                MxFramework.Runtime.RuntimeSaveStateResult<ArmorIntegrityPayload> result =
                    GameplayComponentSchemaPayload.Read<ArmorIntegrityPayload>(Schema, payload);
                if (!result.Success)
                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayArmorIntegrityComponent>.Failed(result.Error);
                if (result.Value == null)
                    return GameplayComponentSchemaPayload.Invalid<GameplayArmorIntegrityComponent>(Schema, payload, "Armor integrity payload is null.");

                try
                {
                    GameplayArmorIntegrityComponent.Validate(
                        result.Value.MaxIntegrity,
                        result.Value.CurrentIntegrity,
                        result.Value.IsBroken);

                    return MxFramework.Runtime.RuntimeSaveStateResult<GameplayArmorIntegrityComponent>.Succeeded(
                        new GameplayArmorIntegrityComponent
                        {
                            MaxIntegrity = result.Value.MaxIntegrity,
                            CurrentIntegrity = result.Value.CurrentIntegrity,
                            IsBroken = result.Value.IsBroken
                        });
                }
                catch (System.Exception exception)
                {
                    return GameplayComponentSchemaPayload.Invalid<GameplayArmorIntegrityComponent>(Schema, payload, exception);
                }
            }
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

        private sealed class StatusListPayload
        {
            public StatusListPayload(StatusEntryPayload[] statuses)
            {
                Statuses = statuses ?? System.Array.Empty<StatusEntryPayload>();
            }

            public StatusEntryPayload[] Statuses { get; }
        }

        private sealed class StatusEntryPayload
        {
            public int Id { get; set; }
            public int SourceBuffId { get; set; }
        }

        private sealed class ScatterPressurePayload
        {
            public int MaxScatterPressure { get; set; }
            public int MaxScatterDebt { get; set; }
            public int CurrentCohesionLimit { get; set; }
            public int RecoveryRate { get; set; }
            public int RecoveryDelayFrames { get; set; }
            public int BrokenRecoveryDelayFrames { get; set; }
            public int CurrentScatterPressure { get; set; }
            public int CurrentBand { get; set; }
            public long LastPressureFrame { get; set; }
            public long LastCohesionBreakFrame { get; set; }
            public bool IsBroken { get; set; }
        }

        private sealed class GuardPressurePayload
        {
            public int MaxPressure { get; set; }
            public int RecoveryRate { get; set; }
            public int RecoveryDelayFrames { get; set; }
            public int CurrentPressure { get; set; }
            public int CurrentBand { get; set; }
            public long LastPressureFrame { get; set; }
            public bool IsBroken { get; set; }
        }

        private sealed class ArmorIntegrityPayload
        {
            public int MaxIntegrity { get; set; }
            public int CurrentIntegrity { get; set; }
            public bool IsBroken { get; set; }
        }
    }
}
