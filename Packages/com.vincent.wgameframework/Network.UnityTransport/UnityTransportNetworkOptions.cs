using System;

namespace MxFramework.Network.UnityTransport
{
    public readonly struct UnityTransportNetworkOptions
    {
        public UnityTransportNetworkOptions(
            int maxConnections = 16,
            int maxPayloadBytes = 65536,
            int reliableWindowSize = 64,
            int receiveQueueCapacity = 2048,
            int sendQueueCapacity = 2048,
            string serverPeerId = "server")
        {
            if (maxConnections <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConnections), "Unity Transport max connections must be greater than zero.");
            if (maxPayloadBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPayloadBytes), "Unity Transport max payload bytes must be greater than zero.");
            if (reliableWindowSize <= 0 || reliableWindowSize > 64)
                throw new ArgumentOutOfRangeException(nameof(reliableWindowSize), "Unity Transport reliable window size must be in the range 1..64.");
            if (receiveQueueCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(receiveQueueCapacity), "Unity Transport receive queue capacity must be greater than zero.");
            if (sendQueueCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(sendQueueCapacity), "Unity Transport send queue capacity must be greater than zero.");

            MaxConnections = maxConnections;
            MaxPayloadBytes = maxPayloadBytes;
            ReliableWindowSize = reliableWindowSize;
            ReceiveQueueCapacity = receiveQueueCapacity;
            SendQueueCapacity = sendQueueCapacity;
            ServerPeerId = string.IsNullOrEmpty(serverPeerId) ? "server" : serverPeerId;
        }

        public int MaxConnections { get; }
        public int MaxPayloadBytes { get; }
        public int ReliableWindowSize { get; }
        public int ReceiveQueueCapacity { get; }
        public int SendQueueCapacity { get; }
        public string ServerPeerId { get; }

        public static UnityTransportNetworkOptions Default =>
            new UnityTransportNetworkOptions(
                maxConnections: 16,
                maxPayloadBytes: 65536,
                reliableWindowSize: 64,
                receiveQueueCapacity: 2048,
                sendQueueCapacity: 2048,
                serverPeerId: "server");
    }
}
