using System;
using Game.Network;
using Game.Online;

namespace Game.Tests.EditMode.Online.TestDoubles
{
    public sealed class FakeOnlineConnection : IOnlineConnection
    {
        public NetworkConnectionState State { get; private set; } = NetworkConnectionState.Disconnected;
        public int ConnectCalls { get; private set; }
        public int BeginAuthenticationCalls { get; private set; }
        public int MarkReadyCalls { get; private set; }
        public int DisconnectCalls { get; private set; }
        public string LastUrl { get; private set; }

        public event Action Connected;
        public event Action Disconnected;
        public event Action<string> Error;

        public void Connect(string url)
        {
            ConnectCalls++;
            LastUrl = url;
            State = NetworkConnectionState.Connecting;
        }

        public void BeginAuthentication()
        {
            BeginAuthenticationCalls++;
            State = NetworkConnectionState.Authenticating;
        }

        public void MarkReady()
        {
            MarkReadyCalls++;
            State = NetworkConnectionState.Ready;
        }

        public void Disconnect()
        {
            DisconnectCalls++;
            State = NetworkConnectionState.Disconnected;
        }

        public void RaiseConnected()
        {
            State = NetworkConnectionState.Connected;
            Connected?.Invoke();
        }

        public void RaiseDisconnected()
        {
            State = NetworkConnectionState.Reconnecting;
            Disconnected?.Invoke();
        }

        public void RaiseError(string reason)
        {
            State = NetworkConnectionState.Failed;
            Error?.Invoke(reason);
        }
    }
}
