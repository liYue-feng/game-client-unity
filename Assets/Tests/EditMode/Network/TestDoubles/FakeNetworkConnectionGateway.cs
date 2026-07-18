using Game.Network;

namespace Game.Tests.EditMode.Network.TestDoubles
{
    internal sealed class FakeNetworkConnectionGateway : INetworkConnectionGateway
    {
        public NetworkConnectionState State { get; set; } = NetworkConnectionState.Disconnected;

        public bool IsConnected => State == NetworkConnectionState.Connected ||
                                   State == NetworkConnectionState.Authenticating ||
                                   State == NetworkConnectionState.Ready;

        public int ConnectCalls { get; private set; }

        public int DisconnectCalls { get; private set; }

        public string LastUrl { get; private set; }

        public void Connect(string url)
        {
            ConnectCalls++;
            LastUrl = url;
        }

        public void Disconnect()
        {
            DisconnectCalls++;
        }
    }
}
