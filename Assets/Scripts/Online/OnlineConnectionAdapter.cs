using System;
using Game.Network;

namespace Game.Online
{
    public sealed class OnlineConnectionAdapter : IOnlineConnection, IDisposable
    {
        private readonly NetworkClient _client;
        private readonly NetworkConnectionControllerHost _host;
        private bool _disposed;

        public OnlineConnectionAdapter(NetworkClient client, NetworkConnectionControllerHost host)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _client.OnConnected += HandleConnected;
            _client.OnDisconnected += HandleDisconnected;
            _client.OnError += HandleError;
        }

        public NetworkConnectionState State => _host.State;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<string> Error;

        public void Connect(string url)
        {
            if (!_disposed)
            {
                _host.Connect(url);
            }
        }

        public void BeginAuthentication()
        {
            if (!_disposed)
            {
                _host.BeginAuthentication();
            }
        }

        public void MarkReady()
        {
            if (!_disposed)
            {
                _host.MarkReady();
            }
        }

        public void Disconnect()
        {
            if (!_disposed)
            {
                _host.Disconnect();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _client.OnConnected -= HandleConnected;
            _client.OnDisconnected -= HandleDisconnected;
            _client.OnError -= HandleError;
            Connected = null;
            Disconnected = null;
            Error = null;
        }

        private void HandleConnected()
        {
            Connected?.Invoke();
        }

        private void HandleDisconnected()
        {
            Disconnected?.Invoke();
        }

        private void HandleError(string reason)
        {
            Error?.Invoke(reason);
        }
    }
}
