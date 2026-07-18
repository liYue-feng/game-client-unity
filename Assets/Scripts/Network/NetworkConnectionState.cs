namespace Game.Network
{
    public enum NetworkConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Authenticating,
        Ready,
        Reconnecting,
        Failed
    }
}
