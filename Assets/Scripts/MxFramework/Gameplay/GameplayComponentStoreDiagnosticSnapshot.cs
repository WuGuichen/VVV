namespace MxFramework.Gameplay
{
    public readonly struct GameplayComponentStoreDiagnosticSnapshot
    {
        public GameplayComponentStoreDiagnosticSnapshot(string componentTypeName, int componentCount)
        {
            ComponentTypeName = componentTypeName ?? string.Empty;
            ComponentCount = componentCount;
        }

        public string ComponentTypeName { get; }
        public int ComponentCount { get; }
    }
}
