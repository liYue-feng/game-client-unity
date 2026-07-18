using System;
using WebSocketSharp;

namespace Game.Network
{
    public sealed class WebSocketTransportFactory : IWebSocketTransportFactory
    {
        public IWebSocketTransport Create(string url)
        {
            return new WebSocketTransport(url);
        }
    }

    public sealed class WebSocketTransport : IWebSocketTransport
    {
        private WebSocket _socket;

        public WebSocketTransport(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("WebSocket URL cannot be null or whitespace.", nameof(url));
            }

            _socket = new WebSocket(url);
            _socket.OnOpen += OnOpen;
            _socket.OnMessage += OnMessage;
            _socket.OnClose += OnClose;
            _socket.OnError += OnError;
        }

        public event Action Opened;

        public event Action<byte[]> MessageReceived;

        public event Action<NetworkCloseInfo> Closed;

        public event Action<string> Error;

        public bool IsAlive => _socket != null && _socket.IsAlive;

        public void ConnectAsync()
        {
            _socket.ConnectAsync();
        }

        public void Send(byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            _socket.Send(payload);
        }

        public void Close(ushort code, string reason)
        {
            _socket.Close((CloseStatusCode)code, reason ?? string.Empty);
        }

        public void Dispose()
        {
            var socket = _socket;
            if (socket == null)
            {
                return;
            }

            _socket = null;
            socket.OnOpen -= OnOpen;
            socket.OnMessage -= OnMessage;
            socket.OnClose -= OnClose;
            socket.OnError -= OnError;
            ((IDisposable)socket).Dispose();
        }

        private void OnOpen(object sender, EventArgs e)
        {
            Opened?.Invoke();
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            if (e.IsBinary)
            {
                MessageReceived?.Invoke(e.RawData);
            }
        }

        private void OnClose(object sender, CloseEventArgs e)
        {
            Closed?.Invoke(new NetworkCloseInfo(e.Code, e.Reason));
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            Error?.Invoke(e.Message);
        }
    }
}
