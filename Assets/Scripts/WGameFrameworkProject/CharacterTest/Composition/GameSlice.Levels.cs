namespace MxFramework.Game.CharacterTest
{
    public sealed partial class GameSlice
    {
        internal void StartPreparedLevelHost() => _composition.InitializeAndStartHost();
        internal bool IsLevelDisposed => _disposed;
    }
}
