namespace MxFramework.Runtime
{
    /// <summary>Participates in the ordered lifecycle owned by a <see cref="RuntimeHost"/>.</summary>
    /// <remarks>
    /// Registration transfers lifetime ownership to the host. A callback that throws after creating a side effect
    /// must still support compensation: <see cref="Stop"/> may follow an entered but incomplete <see cref="Start"/>,
    /// and <see cref="Dispose"/> may follow an entered but incomplete <see cref="Initialize"/>. The host attempts each
    /// applicable cleanup callback at most once and may dispose a registered module whose Initialize was never entered.
    /// </remarks>
    public interface IRuntimeModule
    {
        string ModuleId { get; }
        int Priority { get; }
        RuntimeTickStage TickStage { get; }

        void Initialize(RuntimeHostContext context);
        void Start(RuntimeHostContext context);
        /// <remarks>NoAlloc on the steady-state path after registration and initialization.</remarks>
        void Tick(RuntimeTickContext context);
        void Stop(RuntimeHostContext context);
        void Dispose();
    }
}
