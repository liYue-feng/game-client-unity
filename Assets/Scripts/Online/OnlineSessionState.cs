namespace Game.Online
{
    public enum OnlineSessionState
    {
        Idle,
        Connecting,
        Authenticating,
        LoadingArchive,
        Ready,
        Reconnecting,
        Failed,
        Stopped
    }
}
