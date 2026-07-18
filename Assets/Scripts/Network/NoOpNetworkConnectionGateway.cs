using UnityEngine;

namespace Game.Network
{
    internal sealed class NoOpNetworkConnectionGateway : INetworkConnectionGateway
    {
        internal static readonly NoOpNetworkConnectionGateway Instance = new NoOpNetworkConnectionGateway();

        private NoOpNetworkConnectionGateway()
        {
        }

        public NetworkConnectionState State => NetworkConnectionState.Disconnected;

        public bool IsConnected => false;

        public void Connect(string url)
        {
            Debug.LogWarning("[NetworkClient] No connection host is registered; Connect was ignored.");
        }

        public void Disconnect()
        {
        }
    }
}
