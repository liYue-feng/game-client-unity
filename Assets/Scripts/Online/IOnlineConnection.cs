using System;
using Game.Network;

namespace Game.Online
{
    public interface IOnlineConnection
    {
        NetworkConnectionState State { get; }
        event Action Connected;
        event Action Disconnected;
        event Action<string> Error;
        void Connect(string url);
        void BeginAuthentication();
        void MarkReady();
        void Disconnect();
    }
}
