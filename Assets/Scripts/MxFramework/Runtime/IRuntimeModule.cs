namespace MxFramework.Runtime
{
    public interface IRuntimeModule
    {
        string ModuleId { get; }
        int Priority { get; }
        RuntimeTickStage TickStage { get; }

        void Initialize(RuntimeHostContext context);
        void Start(RuntimeHostContext context);
        void Tick(RuntimeTickContext context);
        void Stop(RuntimeHostContext context);
        void Dispose();
    }
}
