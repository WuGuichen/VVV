using MxFramework.Network.UnityTransport;
using NUnit.Framework;

namespace MxFramework.PackageConsumerSmoke
{
    public sealed class UnityTransportConsumerSmokeTests
    {
        [Test]
        public void UnityTransportAdapter_ConstructsWithoutStartingDriver()
        {
            var transport = new UnityTransportNetworkTransport();
            try
            {
                Assert.That(transport.TransportId, Does.StartWith("unity-transport:"));
                Assert.That(transport.RestartClient(), Is.False);
            }
            finally
            {
                transport.Dispose();
            }
        }
    }
}
