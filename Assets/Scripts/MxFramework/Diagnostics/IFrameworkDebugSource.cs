namespace MxFramework.Diagnostics
{
    public interface IFrameworkDebugSource
    {
        string Name { get; }
        FrameworkDebugMode Mode { get; }
        bool IsAvailable { get; }
        FrameworkDebugSnapshot CreateSnapshot();
    }
}
