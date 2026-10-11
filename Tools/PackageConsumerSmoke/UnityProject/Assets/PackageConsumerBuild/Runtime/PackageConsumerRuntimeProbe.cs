using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.PackageConsumerSmoke
{
    internal static class PackageConsumerRuntimeProbe
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void VerifyPackageRuntimeCanConstruct()
        {
            var host = new RuntimeHost();
            host.Dispose();
        }
    }
}
