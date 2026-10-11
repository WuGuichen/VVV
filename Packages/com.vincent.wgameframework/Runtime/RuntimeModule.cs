namespace MxFramework.Runtime
{
    /// <summary>Provides no-op defaults for an <see cref="IRuntimeModule"/> lifecycle.</summary>
    /// <remarks>
    /// Registration transfers lifetime ownership to the host. Overrides that create side effects before completing
    /// Initialize or Start must tolerate the matching host compensation after an exception. Dispose may also be called
    /// once for a registered instance whose Initialize callback was never entered.
    /// </remarks>
    public abstract class RuntimeModule : IRuntimeModule
    {
        protected RuntimeModule(string moduleId, RuntimeTickStage tickStage = RuntimeTickStage.Simulation, int priority = 0)
        {
            ModuleId = moduleId ?? string.Empty;
            TickStage = tickStage;
            Priority = priority;
        }

        public string ModuleId { get; }
        public int Priority { get; }
        public RuntimeTickStage TickStage { get; }

        public virtual void Initialize(RuntimeHostContext context)
        {
        }

        public virtual void Start(RuntimeHostContext context)
        {
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public virtual void Tick(RuntimeTickContext context)
        {
        }

        public virtual void Stop(RuntimeHostContext context)
        {
        }

        public virtual void Dispose()
        {
        }
    }
}
