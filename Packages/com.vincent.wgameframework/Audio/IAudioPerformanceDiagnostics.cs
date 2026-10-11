namespace MxFramework.Audio
{
    /// <summary>Optional low-frequency backend performance diagnostics.</summary>
    public interface IAudioPerformanceDiagnostics
    {
        /// <remarks>AllocByDesign diagnostics path. Passing true may synchronously flush backend commands.</remarks>
        AudioBackendPerformanceMetrics CapturePerformanceMetrics(bool flushCommands = false);
    }
}
