namespace MxFramework.Preview
{
    /// <summary>
    /// Hot-load entry point for preview Patches. The framework currently does not own
    /// a ConfigBuffFactory hot-reload pipeline; this interface lets the Preview server
    /// stay decoupled from any concrete config registry implementation.
    /// </summary>
    public interface IBuffPatchLoader
    {
        // Returns an aggregated id list parsed from the patch source (best effort).
        System.Collections.Generic.IReadOnlyList<string> LoadPatch(string sourceJson);
        void Clear();
    }
}
