namespace Game.Network
{
    public static class NetworkStatusAdapter
    {
        public static NetworkStatus ToNetworkStatus(NetworkConnectionState state)
        {
            switch (state)
            {
                case NetworkConnectionState.Connected:
                case NetworkConnectionState.Authenticating:
                case NetworkConnectionState.Ready:
                    return NetworkStatus.Connected;
                case NetworkConnectionState.Connecting:
                    return NetworkStatus.Unstable;
                case NetworkConnectionState.Reconnecting:
                    return NetworkStatus.Reconnecting;
                default:
                    return NetworkStatus.Disconnected;
            }
        }

        public static ReconnectState ToReconnectState(NetworkConnectionState state)
        {
            switch (state)
            {
                case NetworkConnectionState.Connected:
                case NetworkConnectionState.Authenticating:
                case NetworkConnectionState.Ready:
                    return ReconnectState.Connected;
                case NetworkConnectionState.Reconnecting:
                    return ReconnectState.Reconnecting;
                case NetworkConnectionState.Failed:
                    return ReconnectState.Failed;
                default:
                    return ReconnectState.Idle;
            }
        }
    }
}
