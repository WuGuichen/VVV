using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public interface IGameplayComponentSpawnInitializer
    {
        string SchemaId { get; }

        RuntimeSaveStateResult<bool> Apply(
            GameplayComponentWorld world,
            GameplayEntityId entityId,
            GameplayComponentSpawnContext context);
    }

    public sealed class GameplayComponentSpawnInitializer<T> : IGameplayComponentSpawnInitializer
        where T : struct, IGameplayComponent
    {
        private readonly T _component;

        public GameplayComponentSpawnInitializer(string schemaId, T component)
        {
            if (string.IsNullOrWhiteSpace(schemaId))
                throw new ArgumentException("Gameplay component spawn initializer schema id cannot be empty.", nameof(schemaId));

            SchemaId = schemaId;
            _component = component;
        }

        public string SchemaId { get; }

        public RuntimeSaveStateResult<bool> Apply(
            GameplayComponentWorld world,
            GameplayEntityId entityId,
            GameplayComponentSpawnContext context)
        {
            if (world == null)
                return Failed("world", "Gameplay component spawn initializer requires a component world.", null);
            if (!entityId.IsValid)
                return Failed("entityId", "Gameplay component spawn initializer requires a valid entity id.", null);

            try
            {
                world.GetOrCreateStore<T>().Set(entityId, _component);
                return RuntimeSaveStateResult<bool>.Succeeded(true);
            }
            catch (Exception exception)
            {
                return Failed("initializer." + SchemaId, "Gameplay component spawn initializer failed: " + exception.Message, exception);
            }
        }

        private static RuntimeSaveStateResult<bool> Failed(string path, string message, Exception exception)
        {
            return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.InvalidDocument,
                path,
                message,
                exception: exception));
        }
    }
}
