namespace MxFramework.Runtime
{
    public enum RuntimeHostErrorPolicy
    {
        FailFast = 0,
        CollectAndStopFrame = 1,
        CollectAndContinue = 2
    }

    public sealed class RuntimeHostOptions
    {
        public RuntimeHostOptions()
        {
            ErrorPolicy = RuntimeHostErrorPolicy.FailFast;
            Services = new RuntimeServiceRegistry();
        }

        public RuntimeHostErrorPolicy ErrorPolicy { get; set; }
        public RuntimeServiceRegistry Services { get; set; }
    }
}
