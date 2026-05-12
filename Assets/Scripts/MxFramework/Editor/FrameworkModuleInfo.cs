namespace MxFramework.Editor
{
    public readonly struct FrameworkModuleInfo
    {
        public FrameworkModuleInfo(
            string name,
            string assemblyName,
            string assetPath,
            string status,
            string dependencies)
        {
            Name = name ?? string.Empty;
            AssemblyName = assemblyName ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            Status = status ?? string.Empty;
            Dependencies = dependencies ?? string.Empty;
        }

        public string Name { get; }
        public string AssemblyName { get; }
        public string AssetPath { get; }
        public string Status { get; }
        public string Dependencies { get; }
    }
}
