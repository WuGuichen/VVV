using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public interface IGameplayComponentSchemaDescriptor
    {
        GameplayComponentSchema Schema { get; }
    }

    public interface IGameplayComponentDiagnosticWriter<T> : IGameplayComponentSchemaDescriptor
        where T : struct, IGameplayComponent
    {
        void WriteDiagnostics(GameplayEntityId entityId, in T component, GameplayComponentDiagnosticWriter writer);
    }

    public interface IGameplayComponentHashWriter<T> : IGameplayComponentSchemaDescriptor
        where T : struct, IGameplayComponent
    {
        void WriteHash(GameplayEntityId entityId, in T component, RuntimeHashAccumulator accumulator);
    }

    public interface IGameplayComponentSaveStateAdapter<T> : IGameplayComponentSchemaDescriptor
        where T : struct, IGameplayComponent
    {
        RuntimeCustomState WriteSaveState(GameplayEntityId entityId, in T component);
        RuntimeSaveStateResult<T> ReadSaveState(GameplayEntityId entityId, RuntimeCustomState payload);
    }
}
