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

    /// <summary>
    /// Writes the numeric protocol hash for a single component instance.
    /// </summary>
    /// <remarks>
    /// All field identifiers are frozen positive integers allocated in the owning descriptor;
    /// no string identity and no implicit boxing may reach the accumulator. The receiver is the
    /// combiner's shared v2 workspace, already scoped inside the component-world object, so
    /// every scope opened here must be closed before the method returns.
    /// </remarks>
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
