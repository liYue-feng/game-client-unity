namespace Game.Network
{
    public interface INetworkConnectionGateway
    {
        NetworkConnectionState State { get; }

        bool IsConnected { get; }

        void Connect(string url);

        void Disconnect();
    }
}
