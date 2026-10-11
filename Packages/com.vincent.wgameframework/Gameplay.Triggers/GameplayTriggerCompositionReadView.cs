using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    /// <summary>Internal immutable composition facts exposed only to the Config compiler friend assembly.</summary>
    internal static class GameplayTriggerCompositionReadView
    {
        internal static bool HostContainsModule(RuntimeHost host, IRuntimeModule module) =>
            host != null && module != null && host.ContainsModuleInstance(module);

        internal static GameplayTriggerCatalog GetCatalog(GameplayTriggerRuntime runtime) =>
            runtime?.AuthorityCatalog;
    }
}
