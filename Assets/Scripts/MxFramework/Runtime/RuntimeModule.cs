namespace MxFramework.Runtime
{
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
